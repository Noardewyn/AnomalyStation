using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4D-1: the Tab inventory's strip view: the model read from tg's strip window data (strippable.dm's ui_data shapes),
/// the layout (the storages in the middle column, the strip panel at the right with the target's cells in the own panel's arrangement, the rows of the
/// hands and of the ID and the pockets) at the reference size and over a fuzz of screens and scales, the pick ids, the target the client tells, and the
/// Tab flag once per window.
/// </summary>
public class TabStripViewTests
{
    internal static UiValue Item(string name, bool interacting = false, int obscured = 0, params string[] alternates)
    {
        var fields = new List<(string, UiValue)>
        {
            ("obscured", UiValue.Of(obscured)), ("icon", UiValue.Of("icons/obj/clothing/shoes.dmi")), ("icon_state", UiValue.Of(name.Replace(' ', '_'))),
            ("name", UiValue.Of(name)), ("alternate", UiValue.ListOf(alternates.Select(UiValue.Of))),
        };
        if (interacting)
            fields.Add(("interacting", UiValue.Of(true)));
        return UiValue.ObjectOf(fields.ToArray());
    }

    // tg's payload of a slot with no item: {obscured: N} (LAZYSET always sets it), with interacting while the player works on it
    internal static UiValue Bare(int obscured = 0, bool interacting = false)
        => interacting ? UiValue.ObjectOf(("obscured", UiValue.Of(obscured)), ("interacting", UiValue.Of(true))) : UiValue.ObjectOf(("obscured", UiValue.Of(obscured)));

    internal static StripModel Model(IEnumerable<(string Key, UiValue Value)> items, int status = 2, string name = "John Doe", int window = 3)
        => StripModel.From(window, status, UiValue.ObjectOf(("items", UiValue.ObjectOf(items.ToArray())), ("name", UiValue.Of(name))));

    // a dressed human as tg's window shows him: every human slot, a few worn things, a full pocket, optionally cuffed
    internal static StripModel Dressed(bool cuffs = false, bool legcuffs = false, int status = 2, string name = "John Doe")
    {
        var items = new List<(string, UiValue)>
        {
            ("head", Bare()), ("back", Item("backpack")), ("mask", Bare()), ("neck", Bare()), ("eyes", Bare()), ("ears", Item("radio headset")),
            ("jumpsuit", Item("grey jumpsuit", alternates: new[] { "adjust_jumpsuit", "adjust_sensor" })), ("suit", Bare()), ("gloves", Bare()),
            ("shoes", Item("black shoes", alternates: "knot")), ("suit_storage", Bare()), ("id", Item("ID card")), ("belt", Bare()),
            ("left_pocket", Bare(2)), ("right_pocket", Bare()), ("left_hand", Bare()), ("right_hand", Bare()),
        };
        if (cuffs)
            items.Add(("handcuffs", Item("handcuffs")));
        if (legcuffs)
            items.Add(("legcuffs", Item("bola")));
        return Model(items, status, name);
    }

    private static int K(string key) => StripView.IndexOf(key);

    private static bool Overlaps(HudRect a, HudRect b) => a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0 && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Inside(HudRect inner, HudRect outer) => inner.Left >= outer.Left && inner.Right <= outer.Right && inner.Top >= outer.Top && inner.Bottom <= outer.Bottom;

    private static bool Whole(HudRect r) => r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height);

    // tg's human HUD entries still drawn under Tab (as TabInventoryTests has them): the combat toggle and the row left of it, tg's status column with the
    // reduced vitals, two rows of action buttons and the palette under them
    private static readonly string[] TgBottomRight = { "EAST-1:28,SOUTH:5", "EAST-2:26,SOUTH:5", "EAST-3:24,SOUTH:5", "EAST-4:22,SOUTH:5" };

    private static readonly string[] TgStatus =
    {
        "EAST-1:28,CENTER-1:19", "EAST-1:28,CENTER:21", "EAST-1:2,CENTER:21", "EAST-1:28,CENTER-3:14", "EAST-1:28,CENTER+1:21", "EAST-1:28,CENTER-4:14",
        "EAST-1:28,SOUTH+1:41",
    };

    private static IEnumerable<string> ActionButtons(int rows)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < 10; c++)
                yield return $"WEST+{c}:{4 + 2 * (c + 1)},NORTH{(r == 0 ? "+0" : "-" + r)}:-6";
        yield return $"WEST+0:23,NORTH-{rows}:5";
    }

    private static List<HudRect> TgDrawn(HudMetrics metrics, LiveHudLayout vitals)
        => TgBottomRight.Concat(vitals.Vitals ? Array.Empty<string>() : TgStatus).Concat(ActionButtons(2))
            .Select(loc => ScreenLoc.TryParse(loc, out ScreenLocSpec spec) ? ScreenLoc.Place(spec, metrics, 32, 32, 0, 0, out _) : throw new ArgumentException(loc)).ToList();

    // what the strip view must keep clear of: tg's entries still drawn, the vitals, every place a chip can take, and the docked chat column
    private static List<HudRect> Obstacles(HudMetrics metrics, LiveHudLayout vitals, TabInventoryLayout tab, List<HudRect> tgDrawn)
    {
        var all = new List<HudRect>(tgDrawn) { vitals.Doll, tab.Chat };
        if (vitals.Vitals)
            all.AddRange(new[] { vitals.Health, vitals.Mood });
        for (int n = 0; n <= LiveHud.MaxChips; n++)
            all.AddRange(LiveHud.AlertStack(metrics, n, vitals));
        return all;
    }

    private static (TabInventoryLayout Tab, StripLayout Strip, HandsLayout Hands) At(float width, float height, float scale, StripModel model)
        => At(width, height, scale, model, tg: false).Result;

    private static ((TabInventoryLayout Tab, StripLayout Strip, HandsLayout Hands) Result, List<HudRect> Obstacles) At(float width, float height, float scale, StripModel model, bool tg)
    {
        var metrics = new HudMetrics(width, height, scale);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        HandsLayout hands = LiveHud.Hands(metrics, true, false, tab: true);
        List<HudRect> tgDrawn = tg ? TgDrawn(metrics, vitals) : new List<HudRect>();
        TabInventoryLayout tab = TabInventory.Layout(metrics, hands, vitals, tgDrawn);
        StripLayout strip = StripView.Layout(tab.Middle, tab.Column, tab.Both, LiveHud.BarCell(hands, 1).Width, TabInventory.FirstScale(metrics), hands.Unit, model);
        return ((tab, strip, hands), Obstacles(metrics, vitals, tab, tgDrawn));
    }

    private static List<HudDrawn> DrawnOf(StripLayout l)
    {
        var d = new List<HudDrawn>();
        StripView.Drawn(l, d);
        return d;
    }

    [Fact]
    public void TheModelReadsEveryShapeOfTgsStripData()
    {
        StripModel m = Model(new[]
        {
            ("shoes", Item("black shoes", interacting: true, alternates: new[] { "knot", "nonsense" })),
            ("head", Bare()),
            ("suit", Bare(1)),
            ("left_pocket", Bare(2)),
            ("gloves", Item("insulated gloves", obscured: 3)),
            ("neck", Bare(3)),
            ("belt", Bare(0, interacting: true)),
            ("tail", Item("tail")), // a key tg's window does not place
        }, status: 1, name: "the monkey (412)");
        Assert.Equal("Stripping: the monkey (412)", m.Title);
        Assert.False(m.Interactive);
        StripSlot shoes = m.At(K("shoes"));
        Assert.Equal((StripLook.Item, "black shoes", "icons/obj/clothing/shoes.dmi", "black_shoes", true, false), (shoes.Look, shoes.Name, shoes.Icon, shoes.IconState, shoes.Interacting, shoes.Inaccessible));
        Assert.Equal(new[] { "knot", "nonsense" }, shoes.Alternates);
        Assert.Equal("black shoes", shoes.Tooltip);
        Assert.True(shoes.Full && shoes.Usable);
        // an empty slot: tg's {obscured: 0}
        StripSlot head = m.At(K("head"));
        Assert.Equal((StripLook.Empty, false, true, "headwear"), (head.Look, head.Full, head.Usable, head.Tooltip));
        // completely obscured: blocked, not usable; tg's window says "obscured suit"
        StripSlot suit = m.At(K("suit"));
        Assert.Equal((StripLook.Blocked, false, false, "obscured suit"), (suit.Look, suit.Full, suit.Usable, suit.Tooltip));
        // a full pocket: hidden, full and usable (the pickpocket), "obscured left pocket"
        StripSlot pocket = m.At(K("left_pocket"));
        Assert.Equal((StripLook.Hidden, true, true, "obscured left pocket"), (pocket.Look, pocket.Full, pocket.Usable, pocket.Tooltip));
        // inaccessible: the item shown, nobody may touch it
        StripSlot gloves = m.At(K("gloves"));
        Assert.Equal((StripLook.Item, true, false, true), (gloves.Look, gloves.Full, gloves.Usable, gloves.Inaccessible));
        Assert.Equal((StripLook.Empty, false), (m.At(K("neck")).Look, m.At(K("neck")).Usable));
        Assert.True(m.At(K("belt")).Interacting);
        // a key not shown by tg is null; one tg's window does not place is left out
        Assert.Null(m.At(K("mask")));
        Assert.Equal(-1, K("tail"));
        Assert.Null(m.At(-1));
        Assert.Null(m.At(StripView.Keys.Length));
        // tg's window draws at most three; the model keeps a bounded number
        StripModel many = Model(new[] { ("shoes", Item("shoes", alternates: Enumerable.Range(0, 40).Select(i => "a" + i).ToArray())) });
        Assert.Equal(StripModel.MaxAlternates, many.At(K("shoes")).Alternates.Count);
        // interactive at tg's UI_INTERACTIVE only
        Assert.True(Dressed(status: 2).Interactive);
        Assert.False(Dressed(status: 0).Interactive);
    }

    [Fact]
    public void TgsKeysGoInTheOwnPanelsCellsAndTheTwoRows()
    {
        // the twelve worn keys in TabInventory.CellOf's order: ears, head, eyes above; mask, neck; suit, uniform, gloves left; back, suit storage, belt
        // right; shoes below
        string[] worn = { "ears", "head", "eyes", "mask", "neck", "suit", "jumpsuit", "gloves", "back", "suit_storage", "belt", "shoes" };
        uint[] slots = { 1u << 4, 1u << 6, 1u << 3, 1u << 5, 1u << 12, 1u << 0, 1u << 1, 1u << 2, 1u << 10, 1u << 14, 1u << 9, 1u << 7 };
        for (int i = 0; i < worn.Length; i++)
        {
            Assert.Equal(TabInventory.CellOf(slots[i]), StripView.WornCell(K(worn[i])));
        }
        foreach (string row in new[] { "left_hand", "right_hand", "handcuffs", "legcuffs", "id", "left_pocket", "right_pocket" })
            Assert.Equal(-1, StripView.WornCell(K(row)));
        Assert.Equal(StripView.WornCell(K("ears")), StripView.WornCell(K("parrot_headset")));
        Assert.Equal(StripView.WornCell(K("neck")), StripView.WornCell(K("pet_collar")));
        // every key of tg's __DEFINES/strippable.dm has a place
        foreach (string key in new[] { "head", "back", "mask", "neck", "eyes", "ears", "jumpsuit", "suit", "gloves", "shoes", "suit_storage", "id", "belt", "left_pocket",
            "right_pocket", "left_hand", "right_hand", "handcuffs", "legcuffs", "pet_collar", "parrot_headset" })
            Assert.True(K(key) >= 0, key);
        // the blank slot pictures are tg's (hud/human.dm): an empty slot's own, a full one's template, the hands' always theirs
        Assert.Equal(("glasses", "template"), (StripView.ArtState(K("eyes"), false), StripView.ArtState(K("eyes"), true)));
        Assert.Equal(("pocket", "template_small"), (StripView.ArtState(K("left_pocket"), false), StripView.ArtState(K("left_pocket"), true)));
        Assert.Equal(("hand_l", "hand_r"), (StripView.ArtState(K("left_hand"), true), StripView.ArtState(K("right_hand"), false)));
        Assert.Equal("uniform", StripView.ArtState(K("jumpsuit"), false));
    }

    [Fact]
    public void AtTheReferenceSizeTheStoragesAreInTheMiddleAndThePanelAtTheRightWithTheTargetsCellsRoundTheSprite()
    {
        (TabInventoryLayout tab, StripLayout l, _) = At(1920, 1080, 1f, Dressed(cuffs: true));
        Assert.True(l.Shown);
        Assert.Equal(tab.Middle, l.Storages);
        Assert.True(tab.Middle.Width > 0f);
        Assert.True(Inside(l.Panel, tab.Column), $"{l.Panel} in {tab.Column}");
        Assert.False(Overlaps(l.Panel, l.Storages));
        // the sprite at 6 tg pixels a pixel and cells of the bar's 48, the panel from the column's top, as tall as its content
        Assert.Equal((6, 48f), (l.SpriteScale, l.Cell));
        Assert.Equal(tab.Column.Top, l.Panel.Top);
        Assert.Equal(new HudRect(tab.Column.Left + 4, tab.Column.Top + 4, tab.Column.Width - 8, 36), l.Title);
        Assert.Equal(new HudRect(l.Title.Right - 36, l.Title.Top, 36, 36), l.Close);
        // the worn cells round the sprite exactly as the own panel places them round its sprite (the same arrangement)
        Assert.True(TabInventory.Arrangement(6, 48f, out HudRect[] at, out _, out _, out _));
        for (int i = 0; i < 12; i++)
            Assert.Equal(new HudRect(l.Sprite.Left + at[i].Left, l.Sprite.Top + at[i].Top, 48, 48), l.Cells[i]);
        // under the shoes the hands' row (left, right, then the cuffs), centred, and under it the ID and the pockets
        HudRect shoes = l.Cells[K("shoes")];
        HudRect left = l.Cells[K("left_hand")], right = l.Cells[K("right_hand")], cuffs = l.Cells[K("handcuffs")];
        Assert.Equal(shoes.Bottom + 12, left.Top);
        Assert.Equal((left.Right + 4, right.Right + 4), (right.Left, cuffs.Left));
        Assert.Equal(default, l.Cells[K("legcuffs")]); // not shown by tg
        HudRect id = l.Cells[K("id")], lp = l.Cells[K("left_pocket")], rp = l.Cells[K("right_pocket")];
        Assert.Equal((left.Bottom + 4, left.Bottom + 4, left.Bottom + 4), (id.Top, lp.Top, rp.Top));
        Assert.Equal((id.Right + 4, lp.Right + 4), (lp.Left, rp.Left));
        float middle = l.Panel.Left + l.Panel.Width / 2f;
        Assert.True(Math.Abs((id.Left + rp.Right) / 2f - middle) <= 1f && Math.Abs((left.Left + cuffs.Right) / 2f - middle) <= 1f);
        Assert.True(Math.Abs(l.Sprite.Left + l.Sprite.Width / 2f - middle) <= 1f);
        Assert.Equal(rp.Bottom + 4, l.Panel.Bottom);
        // nothing overlaps, everything inside the panel; the mask and the collar keys absent from tg's data have no cell
        HudRect[] shown = l.Cells.Where(c => c.Width > 0).ToArray();
        Assert.Equal(18, shown.Length); // tg's seventeen human slots and the handcuffs
        foreach (HudRect c in shown.Append(l.Sprite).Append(l.Title))
            Assert.True(Inside(c, l.Panel), $"{c}");
        for (int i = 0; i < shown.Length; i++)
        {
            Assert.False(Overlaps(shown[i], l.Sprite));
            for (int j = 0; j < i; j++)
                Assert.False(Overlaps(shown[i], shown[j]));
        }
        Assert.Equal(default, l.Cells[K("pet_collar")]);
    }

    [Fact]
    public void ATallerThanTheColumnPanelStepsItsSpriteDownThenCutsItsBottomRowsAndATooNarrowColumnTakesBothColumns()
    {
        StripModel m = Dressed();
        // a column too short for the sprite at 6: it steps down, the cells keep the bar's size
        StripLayout shorter = StripView.Layout(new HudRect(0, 0, 300, 300), new HudRect(400, 0, 400, 520), new HudRect(0, 0, 800, 300), 48f, 6, 1f, m);
        Assert.True(shorter.SpriteScale < 6 && shorter.SpriteScale >= 1);
        Assert.Equal(48f, shorter.Cell);
        Assert.True(shorter.Panel.Height <= 520);
        // shorter still: a scale of 1, the cells the same size, the bottom rows cut at the panel (picked only inside it)
        StripLayout cut = StripView.Layout(new HudRect(0, 0, 300, 300), new HudRect(400, 0, 400, 300), new HudRect(0, 0, 800, 300), 48f, 6, 1f, m);
        Assert.Equal((1, 48f), (cut.SpriteScale, cut.Cell));
        Assert.Equal(new HudRect(400, 0, 400, 300), cut.Panel);
        Assert.True(cut.Cells[K("right_pocket")].Bottom > cut.Panel.Bottom);
        List<HudDrawn> drawn = DrawnOf(cut);
        Assert.DoesNotContain(drawn, d => d.EntryId == StripView.SlotId(K("right_pocket")));
        Assert.All(drawn, d => Assert.True(Inside(d.Rect, cut.Panel)));
        // a right column too narrow even at a scale of 1: the panel takes both columns and the storages give way
        StripLayout narrow = StripView.Layout(new HudRect(100, 0, 150, 900), new HudRect(262, 0, 90, 900), new HudRect(100, 0, 252, 900), 48f, 6, 1f, m);
        Assert.True(narrow.Shown);
        Assert.Equal(default, narrow.Storages);
        Assert.Equal(new HudRect(100, 0, 252, narrow.Panel.Height), narrow.Panel);
        // no right column at all: the same
        StripLayout none = StripView.Layout(new HudRect(100, 0, 400, 900), default, new HudRect(100, 0, 400, 900), 48f, 6, 1f, m);
        Assert.Equal((default(HudRect), 100f), (none.Storages, none.Panel.Left));
        // no window: nothing
        Assert.False(StripView.Layout(new HudRect(100, 0, 400, 900), new HudRect(600, 0, 400, 900), new HudRect(100, 0, 900, 900), 48f, 6, 1f, null).Shown);
    }

    [Fact]
    public void AFuzzOfScreensScalesCuffsAndNamesNeverThrowsNorDrawsOutsideNorOverlapsTheStoragesAndEveryIdIsItsOwn()
    {
        int checkedLayouts = 0;
        var models = new[]
        {
            Dressed(), Dressed(cuffs: true), Dressed(cuffs: true, legcuffs: true), Dressed(name: new string('W', 300)),
            Model(new[] { ("head", Bare()), ("pet_collar", Item("collar")), ("back", Bare()) }), Model(Array.Empty<(string, UiValue)>()),
        };
        foreach (float width in new float[] { 1, 64, 320, 640, 800, 1024, 1280, 1366, 1600, 1920, 2560, 3840, 900 })
        {
            foreach (float height in new float[] { 1, 48, 240, 480, 600, 720, 768, 900, 1080, 1440, 2160, 1600 })
            {
                foreach (float scale in new[] { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f })
                {
                    foreach ((StripModel model, bool tg) in models.SelectMany(m => new[] { (m, false), (m, true) }))
                    {
                        ((TabInventoryLayout tab, StripLayout l, HandsLayout hands), List<HudRect> obstacles) = At(width, height, scale, model, tg);
                        checkedLayouts++;
                        if (!l.Shown)
                            continue;
                        // clear of tg's entries still drawn, the vitals, every chip place and the chat column (on a narrow portrait screen they reach in)
                        foreach (HudRect o in obstacles)
                            Assert.False(Overlaps(l.Panel, o), $"{width}x{height}@{scale} tg={tg}: the panel {l.Panel} over {o}");
                        var screen = new HudRect(0, 0, width, height);
                        Assert.True(Inside(l.Panel, screen), $"{width}x{height}@{scale}: {l.Panel}");
                        // the panel stands in the right column, or with the storages given way in the two columns
                        if (l.Storages.Width > 0f)
                        {
                            Assert.Equal(tab.Middle, l.Storages);
                            Assert.True(Inside(l.Panel, tab.Column), $"{width}x{height}@{scale}: {l.Panel} in {tab.Column}");
                            Assert.False(Overlaps(l.Panel, l.Storages));
                            // the storages column placed there stays clear of the panel too
                            List<StoragePanel> panels = StorageColumn.Panels(StorageColumnTests.Carried(7, 3).Storages, null);
                            var column = new List<HudDrawn>();
                            StorageColumn.Drawn(StorageColumn.Layout(l.Storages, LiveHud.BarCell(hands, 1).Width, hands.Unit, panels, 0f, 2), column);
                            Assert.All(column, d => Assert.False(Overlaps(d.Rect, l.Panel), $"{width}x{height}@{scale}: {d.Rect} over {l.Panel}"));
                        }
                        List<HudDrawn> drawn = DrawnOf(l);
                        Assert.Equal(drawn.Count, drawn.Select(d => d.EntryId).Distinct().Count());
                        foreach (HudDrawn d in drawn)
                        {
                            Assert.True(Inside(d.Rect, l.Panel), $"{width}x{height}@{scale}: #{d.EntryId} {d.Rect} out of {l.Panel}");
                            Assert.True(d.Rect.Width > 0 && d.Rect.Height > 0);
                            Assert.True(StripView.Decode(d.EntryId, out StripPart part, out int index), $"#{d.EntryId}");
                            if (part == StripPart.Slot)
                            {
                                Assert.Equal(StripView.SlotId(index), d.EntryId);
                                Assert.NotNull(model.At(index));
                            }
                            // clear of every other id of the Tab inventory
                            Assert.False(StorageColumn.Decode(d.EntryId, out _, out _));
                            Assert.False(StorageColumn.DecodeStrip(d.EntryId, out _));
                            Assert.NotEqual(TabInventory.SpriteId, d.EntryId);
                            Assert.Equal(0, LiveHud.BeltCellOf(new ClickTarget(TargetKind.Screen, d.EntryId, 0, 0, 0, 16, 16)));
                        }
                        foreach (HudRect c in l.Cells.Where(c => c.Width > 0))
                            Assert.True(Whole(c));
                        Assert.True(l.SpriteScale >= 1);
                    }
                }
            }
        }
        Assert.True(checkedLayouts > 11000);
    }

    [Fact]
    public void EveryIdDecodesToItsPartAndNothingElseDoes()
    {
        Assert.True(StripView.Decode(StripView.PanelId, out StripPart p, out int i));
        Assert.Equal((StripPart.Panel, -1), (p, i));
        Assert.True(StripView.Decode(StripView.CloseId, out p, out _));
        Assert.Equal(StripPart.Close, p);
        Assert.True(StripView.Decode(StripView.SpriteId, out p, out _));
        Assert.Equal(StripPart.Sprite, p);
        for (int k = 0; k < StripView.Keys.Length; k++)
        {
            Assert.True(StripView.Decode(StripView.SlotId(k), out p, out i));
            Assert.Equal((StripPart.Slot, k), (p, i));
        }
        foreach (long other in new long[] { 0, 5, -1, -7, -100, -1000, -1001, -3000, -3001, -4999, -5003, -5009, StripView.SlotId(StripView.Keys.Length), long.MinValue })
            Assert.False(StripView.Decode(other, out _, out _), $"#{other}");
    }

    [Fact]
    public void TheTargetIsTheMobDraggedOntoOneselfOrTheOneMobOfThatNameNextToThePlayer()
    {
        var mobs = new List<(long, string, int)> { (10, "John Doe", 1), (11, "Jane Roe", 1), (12, "John Doe", 5), (13, "monkey (412)", 1) };
        // the dragged mob named as the window names it, wherever it stands
        Assert.Equal(12L, StripView.TargetOf("John Doe", 12, mobs));
        // dragged one is another: the one of that name next to the player
        Assert.Equal(10L, StripView.TargetOf("John Doe", 11, mobs));
        Assert.Equal(10L, StripView.TargetOf("John Doe", 0, mobs));
        // tg's "\the" before a name that is not proper
        Assert.Equal(13L, StripView.TargetOf("the monkey (412)", 0, mobs));
        Assert.True(StripView.Names("The monkey (412)", "monkey (412)"));
        Assert.False(StripView.Names("the monkey (412)", "monkey (41)"));
        Assert.False(StripView.Names("", "x"));
        // two of that name next to the player and none dragged: unknown
        mobs.Add((14, "John Doe", 0));
        Assert.Equal(0L, StripView.TargetOf("John Doe", 0, mobs));
        Assert.Equal(14L, StripView.TargetOf("John Doe", 14, mobs));
        // nobody of that name near
        Assert.Equal(0L, StripView.TargetOf("Nobody", 10, mobs));
        Assert.Equal(0L, StripView.TargetOf("John Doe", 0, null));
    }

    [Fact]
    public void TgsWayOfOpeningTheStripSentAgainWhileItsWindowIsOpenTurnsTheTabInventoryOn()
    {
        const long own = 5, bob = 9;
        // yes: a mob dropped onto oneself, a strip window open, Tab off, the human HUD
        Assert.True(StripView.ReopensStrip(bob, own, own, draggedIsMob: true, stripOpen: true, tabOn: false, humanHud: true));
        // no: each of those missing
        Assert.False(StripView.ReopensStrip(bob, 12, own, true, true, false, true)); // not onto oneself
        Assert.False(StripView.ReopensStrip(0, own, own, true, true, false, true)); // nothing dropped
        Assert.False(StripView.ReopensStrip(own, own, own, true, true, false, true)); // oneself onto oneself
        Assert.False(StripView.ReopensStrip(bob, own, 0, true, true, false, true)); // no own mob
        Assert.False(StripView.ReopensStrip(bob, own, own, false, true, false, true)); // an item, not a mob
        Assert.False(StripView.ReopensStrip(bob, own, own, true, false, false, true)); // no strip window open (a new one turns Tab on by itself)
        Assert.False(StripView.ReopensStrip(bob, own, own, true, true, true, true)); // Tab already on
        Assert.False(StripView.ReopensStrip(bob, own, own, true, true, false, false)); // another HUD
    }

    [Fact]
    public void ANewStripWindowTurnsTheTabInventoryOnOnceAndOnlyOnTheHumanHud()
    {
        var openings = new StripOpenings();
        Assert.False(openings.TakeNew(Array.Empty<int>(), true));
        // opened while another HUD is up: not taken; once the human HUD is up, taken once
        Assert.False(openings.TakeNew(new[] { 4 }, false));
        Assert.True(openings.TakeNew(new[] { 4 }, true));
        Assert.False(openings.TakeNew(new[] { 4 }, true));
        // a second window: once more
        Assert.True(openings.TakeNew(new[] { 4, 7 }, true));
        Assert.False(openings.TakeNew(new[] { 7, 4 }, true));
        // one closes and its index opens again: a new window
        Assert.False(openings.TakeNew(new[] { 7 }, true));
        Assert.True(openings.TakeNew(new[] { 7, 4 }, true));
        // closed while another HUD is up, then open again on the human HUD: new
        Assert.False(openings.TakeNew(Array.Empty<int>(), false));
        Assert.True(openings.TakeNew(new[] { 7 }, true));
        Assert.False(openings.TakeNew(null, true));
    }
}
