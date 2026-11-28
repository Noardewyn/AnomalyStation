using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4B-2: the Tab inventory's right column: its panels from the server's Storages message (the belt's beyond the bar,
/// a locked storage's header, none for a hidden one), where they are drawn at the reference size, on every HUD screen and scale clear of the rest of
/// the HUD, the scrolling, and the pick ids that are ours.
/// </summary>
public class StorageColumnTests
{
    // tg's human HUD entries still drawn under Tab at the bottom right (hud/human.dm): the combat toggle and the row left of it; tg's status column
    // with the reduced vitals
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

    private static HudRect Place(string loc, HudMetrics metrics)
        => ScreenLoc.Place(ScreenLoc.TryParse(loc, out ScreenLocSpec spec) ? spec : throw new ArgumentException(loc), metrics, 32, 32, 0, 0, out _);

    private static List<HudRect> TgDrawn(HudMetrics metrics, LiveHudLayout vitals, int actionRows)
        => TgBottomRight.Concat(vitals.Vitals ? Array.Empty<string>() : TgStatus).Concat(ActionButtons(actionRows)).Select(loc => Place(loc, metrics)).ToList();

    internal static StorageItem Item(long id, string name = null, uint look = 0, int count = 0)
        => new(id, name ?? "item " + id, look == 0 ? TableRef.None : new TableRef(look, true), count);

    internal static CarriedStorage Storage(long id, StoragePlace place, string name, int slots, int used, int limit, IEnumerable<StorageItem> items = null,
        bool locked = false, bool hidden = false, byte hand = 0)
        => new() { Entity = id, Place = place, Hand = hand, Name = name, Slots = slots, Used = used, Limit = limit, Locked = locked, Hidden = hidden, Items = (items ?? Array.Empty<StorageItem>()).ToList() };

    // the test's carried storages: a backpack of 21 slots holding 5, a box of 7 holding 3 in the left pocket, a cigarette packet whose contents tg
    // hides in the right pocket, a locked box in the left hand; and a belt of `beltSlots` holding `beltItems`, the first seven on the bar
    internal static (List<CarriedStorage> Storages, HotbarModel Bar) Carried(int beltSlots = 7, int beltItems = 3)
    {
        var storages = new List<CarriedStorage>
        {
            Storage(100, StoragePlace.Back, "backpack", 21, 13, 21, Enumerable.Range(1, 5).Select(i => Item(100 + i, look: (uint)(100 + i)))),
            Storage(200, StoragePlace.Belt, "toolbelt", beltSlots, beltItems * 2, 21, Enumerable.Range(1, beltItems).Select(i => Item(200 + i, look: (uint)(200 + i)))),
            Storage(300, StoragePlace.LeftPocket, "box", 7, 3, 14, Enumerable.Range(1, 3).Select(i => Item(300 + i, look: (uint)(300 + i), count: i == 1 ? 5 : 0))),
            Storage(400, StoragePlace.RightPocket, "packet of cigarettes", 6, 0, 6, hidden: true),
            Storage(500, StoragePlace.Hand, "lockbox", 4, 0, 8, locked: true, hand: 1),
        };
        var cells = new HotbarCell[HotbarMessage.CellCount];
        for (int c = 0; c < cells.Length; c++)
            cells[c] = c < Math.Min(beltItems, HotbarMessage.BeltCells) ? new HotbarCell(HotbarCellState.Holding, new TableRef((uint)(201 + c), true), 201 + c) : HotbarCell.Empty;
        var bar = new HotbarModel();
        bar.Apply(new HotbarMessage { Bar = true, BeltWorn = true, Cells = cells }, 0, 0, 0);
        return (storages, bar);
    }

    private static StorageColumnLayout Reference(List<StoragePanel> panels, float scroll = 0f)
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        HandsLayout hands = LiveHud.Hands(metrics, true, false, vitals, tab: true);
        TabInventoryLayout tab = TabInventory.Layout(metrics, hands, vitals, TgDrawn(metrics, vitals, 1));
        return StorageColumn.Layout(tab.Column, LiveHud.BarCell(hands, 1).Width, hands.Unit, panels, scroll);
    }

    private static HudRect R(float x, float y, float w, float h) => new(x, y, w, h);

    private static bool Overlaps(HudRect a, HudRect b) => a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0 && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Inside(HudRect inner, HudRect outer) => inner.Left >= outer.Left && inner.Right <= outer.Right && inner.Top >= outer.Top && inner.Bottom <= outer.Bottom;

    private static bool Whole(HudRect r) => r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height);

    private static HudRect[] Row(float left, float top, int count) => Enumerable.Range(0, count).Select(i => R(left + 52 * i, top, 48, 48)).ToArray();

    private static List<HudDrawn> DrawnOf(StorageColumnLayout l)
    {
        var d = new List<HudDrawn>();
        StorageColumn.Drawn(l, d);
        return d;
    }

    [Fact]
    public void AtTheReferenceSizeTheColumnAndItsPanelsAreWhereTheDesignPutsThem()
    {
        // a backpack of 21 slots holding 5, a box of 7 holding 3 in a pocket, a locked box in a hand; a hidden cigarette packet and a belt of 7 (its
        // three items on the bar) have no panel
        (List<CarriedStorage> s, HotbarModel bar) = Carried(beltSlots: 7, beltItems: 3);
        List<StoragePanel> panels = StorageColumn.Panels(s, StorageColumn.BeltOnBar(bar));
        Assert.Equal(new long[] { 100, 300, 500 }, panels.Select(p => p.Storage));
        StorageColumnLayout l = Reference(panels);
        // the column: from 64 % of 1920 (1229) and the gap of 8 to the vitals' left edge (1812, 12 left of them), from the chat column's top (141,
        // under tg's action buttons) to 8 over the bottom row (992)
        Assert.Equal(R(1237, 141, 575, 843), l.Column);
        // the backpack: a backing as wide as the column, its header 4 inside it as tall as an item's picture in a cell (36) with the bag's picture at
        // its left, under it 4 apart 21 cells of the bar's 48, ten to a row
        StoragePanelRects bag = l.Panels[0];
        Assert.Equal(R(1237, 141, 575, 200), bag.Backing);
        Assert.Equal(R(1241, 145, 567, 36), bag.Header);
        Assert.Equal(R(1241, 145, 36, 36), bag.Picture);
        Assert.Equal(Row(1241, 185, 10).Concat(Row(1241, 237, 10)).Concat(Row(1241, 289, 1)), bag.Cells);
        // 12 under it the pocket box's seven cells, then the locked box's header alone
        Assert.Equal(R(1237, 353, 575, 96), l.Panels[1].Backing);
        Assert.Equal(R(1241, 357, 567, 36), l.Panels[1].Header);
        Assert.Equal(Row(1241, 397, 7), l.Panels[1].Cells);
        Assert.Equal(R(1237, 461, 575, 44), l.Panels[2].Backing);
        Assert.Equal(R(1241, 465, 567, 36), l.Panels[2].Header);
        Assert.Empty(l.Panels[2].Cells);
        Assert.Equal(364f, l.Content);
        Assert.Equal(0f, l.MaxScroll);
        Assert.Equal(default, l.Thumb); // no scroll bar: they fit
        // a belt of 9 holding 9 (seven on the bar) has a panel of its two slots beyond the bar, after the backpack
        (s, bar) = Carried(beltSlots: 9, beltItems: 9);
        panels = StorageColumn.Panels(s, StorageColumn.BeltOnBar(bar));
        Assert.Equal(new long[] { 100, 200, 300, 500 }, panels.Select(p => p.Storage));
        l = Reference(panels);
        Assert.Equal(R(1237, 353, 575, 96), l.Panels[1].Backing);
        Assert.Equal(Row(1241, 397, 2), l.Panels[1].Cells);
        Assert.Equal(R(1237, 461, 575, 96), l.Panels[2].Backing);
        Assert.Equal(R(1237, 569, 575, 44), l.Panels[3].Backing);
        Assert.Equal(472f, l.Content);
    }

    [Fact]
    public void EachPanelShowsEverySlotItsItemsInTgsOrderItsRoomAndTheBeltOnlyBeyondTheBar()
    {
        (List<CarriedStorage> s, HotbarModel bar) = Carried(beltSlots: 9, beltItems: 9);
        long[] onBar = StorageColumn.BeltOnBar(bar);
        Assert.Equal(new long[] { 201, 202, 203, 204, 205, 206, 207 }, onBar);
        List<StoragePanel> panels = StorageColumn.Panels(s, onBar);
        StoragePanel bag = panels[0];
        Assert.Equal(("backpack", "13 / 21", false, 21), (bag.Title, bag.Room, bag.Locked, bag.Cells.Length));
        Assert.Equal(new long[] { 101, 102, 103, 104, 105 }, bag.Cells.Take(5).Select(c => c.Entity));
        Assert.All(bag.Cells.Skip(5), c => Assert.Equal(0, c.Entity)); // the empty slots, drop targets
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }.Concat(Enumerable.Repeat(0, 16)), bag.Wishes);
        // the belt: titled with its name, its room the whole belt's, the two items no bar cell shows; a wish into the belt names a bar cell (DV693,
        // DV696), so its panel's cells, which are off the bar, wish for none
        StoragePanel belt = panels[1];
        Assert.Equal(("toolbelt", "18 / 21", 2), (belt.Title, belt.Room, belt.Cells.Length));
        Assert.Equal(new long[] { 208, 209 }, belt.Cells.Select(c => c.Entity));
        Assert.Equal(new[] { 0, 0 }, belt.Wishes);
        Assert.Equal(5, panels[2].Cells[0].Count); // a stack's amount comes with its item
        StoragePanel locked = panels[3];
        Assert.Equal(("lockbox", "Locked", true, 0), (locked.Title, locked.Room, locked.Locked, locked.Cells.Length));
        Assert.DoesNotContain(panels, p => p.Storage == 400); // the hidden packet
        // a belt of 7 has no panel; a belt of 9 holding no more than the bar shows still has its two slots beyond it, empty
        (s, bar) = Carried(beltSlots: 7, beltItems: 7);
        Assert.DoesNotContain(StorageColumn.Panels(s, StorageColumn.BeltOnBar(bar)), p => p.Place == StoragePlace.Belt);
        (s, bar) = Carried(beltSlots: 9, beltItems: 4);
        StoragePanel emptyBelt = StorageColumn.Panels(s, StorageColumn.BeltOnBar(bar)).Single(p => p.Place == StoragePlace.Belt);
        Assert.Equal(new long[] { 0, 0 }, emptyBelt.Cells.Select(c => c.Entity));
        // tg's INFINITY of slots (sent as 65535) shows the most cells the message can fill
        var endless = new List<CarriedStorage> { Storage(1, StoragePlace.Back, "bag of holding", 65535, 2, 65535, new[] { Item(2) }) };
        Assert.Equal(UiTokens.StorageMaxCells, StorageColumn.Panels(endless, null)[0].Cells.Length);
        // no message yet, or a mob that is no human: no panels
        Assert.Empty(StorageColumn.Panels(StoragesMessage.None.Storages, null));
    }

    [Fact]
    public void TheBarsBeltCellsHoldTheItemsTheHotbarMessageNames()
    {
        // two screwdrivers of one picture (items 11 and 13): each cell is the item its message names, whatever the order in the belt; an item out of
        // its cell in a hand is still the cell's; an empty cell holds none
        var cells = new HotbarCell[HotbarMessage.CellCount];
        Array.Fill(cells, HotbarCell.Empty);
        cells[0] = new HotbarCell(HotbarCellState.Holding, new TableRef(5, true), 13);
        cells[2] = new HotbarCell(HotbarCellState.Holding, new TableRef(5, true), 11);
        cells[3] = new HotbarCell(HotbarCellState.Out, new TableRef(6, true), 14);
        cells[4] = new HotbarCell(HotbarCellState.Holding, new TableRef(6, true), 12);
        cells[7] = new HotbarCell(HotbarCellState.Holding, new TableRef(7, true), 40); // a pocket: not a belt cell
        var bar = new HotbarModel();
        bar.Apply(new HotbarMessage { Bar = true, BeltWorn = true, Cells = cells }, 0, 0, 0);
        Assert.Equal(new long[] { 13, 0, 11, 14, 12, 0, 0 }, StorageColumn.BeltOnBar(bar));
        bar.Apply(new HotbarMessage { Bar = true, BeltWorn = false, Cells = cells }, 0, 0, 0);
        Assert.All(StorageColumn.BeltOnBar(bar), id => Assert.Equal(0, id)); // no belt worn
        Assert.All(StorageColumn.BeltOnBar(null), id => Assert.Equal(0, id));
    }

    // many carried storages: enough to overflow the column at any size
    internal static List<StoragePanel> Many()
    {
        var storages = new List<CarriedStorage>
        {
            Storage(100, StoragePlace.Back, "backpack", 21, 0, 21),
            Storage(300, StoragePlace.LeftPocket, "box", 7, 0, 14),
            Storage(310, StoragePlace.RightPocket, "box", 7, 0, 14),
        };
        for (int h = 1; h <= 5; h++)
            storages.Add(Storage(400 + h, StoragePlace.Hand, "toolbox " + h, 21, 0, 28, hand: (byte)h));
        return StorageColumn.Panels(storages, null);
    }

    [Theory]
    [MemberData(nameof(LiveHudTests.HandScreens), MemberType = typeof(LiveHudTests))]
    public void OnEveryScreenTheColumnIsAtWholePixelsAndClearOfEverythingElse(int width, int height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        var screen = new HudRect(0, 0, width, height);
        LiveHudLayout vitals = LiveHud.Layout(metrics, human: true);
        float u = UiTokens.Unit(metrics);
        List<HudRect> chips = Enumerable.Range(0, LiveHud.MaxChips + 1).SelectMany(n => LiveHud.AlertStack(metrics, n, vitals)).Distinct().ToList();
        (List<CarriedStorage> carried, HotbarModel bar) = Carried(9, 9);
        foreach (int actionRows in new[] { 0, 1, 2 })
        {
            List<HudRect> tg = TgDrawn(metrics, vitals, actionRows);
            HandsLayout hands = LiveHud.Hands(metrics, true, false, vitals, 300f * u, tab: true);
            TabInventoryLayout tab = TabInventory.Layout(metrics, hands, vitals, tg);
            HudRect column = tab.Column;
            string at = $"{width}x{height} at {scale}, {actionRows} action rows: column {column}";
            if (column.Width <= 0f)
                continue; // no room: no panels
            Assert.True(Whole(column) && Inside(column, screen), at);
            var others = new List<HudRect> { tab.Chat, tab.Sprite, hands.Left, hands.Right, hands.Hotbar, vitals.Doll };
            if (vitals.Vitals)
                others.AddRange(new[] { vitals.Health, vitals.Mood });
            others.AddRange(tab.Cells ?? Array.Empty<HudRect>());
            others.AddRange(Enumerable.Range(1, HotbarMessage.CellCount).Select(c => LiveHud.BarCell(hands, c)));
            others.AddRange(chips);
            others.AddRange(tg);
            foreach (HudRect other in others)
                Assert.False(Overlaps(column, other), $"{at} over {other}");
            foreach (List<StoragePanel> panels in new[] { StorageColumn.Panels(carried, StorageColumn.BeltOnBar(bar)), Many() })
            {
                foreach (float scroll in new[] { 0f, 100f, float.MaxValue })
                {
                    StorageColumnLayout l = StorageColumn.Layout(column, LiveHud.BarCell(hands, 1).Width, hands.Unit, panels, scroll);
                    Assert.InRange(l.Scroll, 0f, l.MaxScroll);
                    foreach (HudDrawn d in DrawnOf(l))
                        Assert.True(Whole(d.Rect) && Inside(d.Rect, column), $"{at}: {d.EntryId} at {d.Rect}");
                    foreach (StoragePanelRects p in l.Panels)
                    {
                        Assert.True(Whole(p.Backing) && Whole(p.Header) && Whole(p.Picture), at);
                        Assert.True(Inside(p.Header, p.Backing) && p.Backing.Left >= column.Left && p.Backing.Right <= column.Right, at);
                        for (int i = 0; i < p.Cells.Length; i++)
                        {
                            Assert.True(Whole(p.Cells[i]) && Inside(p.Cells[i], p.Backing) && !Overlaps(p.Cells[i], p.Header), $"{at}: cell {p.Cells[i]}");
                            if (i > 0)
                                Assert.False(Overlaps(p.Cells[i], p.Cells[i - 1]), at);
                        }
                    }
                    for (int i = 1; i < l.Panels.Length; i++)
                        Assert.True(l.Panels[i].Backing.Top >= l.Panels[i - 1].Backing.Bottom, at);
                    if (l.MaxScroll > 0f)
                    {
                        Assert.True(Whole(l.Thumb) && Inside(l.Thumb, l.Track) && Inside(l.Track, column), at);
                        Assert.All(l.Panels, p => Assert.False(Overlaps(p.Backing, l.Track), at)); // the bar stands beside the panels
                    }
                }
            }
        }
    }

    [Fact]
    public void TheWheelScrollsTheColumnByARowKeptBetweenItsEnds()
    {
        List<StoragePanel> panels = Many();
        StorageColumnLayout top = Reference(panels);
        // eight panels are taller than the column (843): the content's height, the overflow, a scroll bar at the column's right edge
        Assert.True(top.Content > top.Column.Height, $"{top.Content}");
        Assert.Equal(top.Content - top.Column.Height, top.MaxScroll);
        Assert.Equal(R(1808, 141, 4, 843), top.Track);
        Assert.Equal(top.Column.Top, top.Thumb.Top);
        Assert.Equal(1237 + 575 - 4 - 4f, top.Panels[0].Backing.Right); // the panels narrower by the bar and a gap
        // one step of the wheel is one row of cells (48 and the gap of 4), clamped at both ends
        Assert.Equal(52f, StorageColumn.Scrolled(0f, 1, 1f, top.MaxScroll));
        Assert.Equal(104f, StorageColumn.Scrolled(52f, 1, 1f, top.MaxScroll));
        Assert.Equal(0f, StorageColumn.Scrolled(52f, -3, 1f, top.MaxScroll));
        Assert.Equal(top.MaxScroll, StorageColumn.Scrolled(top.MaxScroll - 10f, 2, 1f, top.MaxScroll));
        Assert.Equal(104f, StorageColumn.Scrolled(0f, 1, 2f, top.MaxScroll)); // at twice the scale twice as far
        Assert.Equal(0f, StorageColumn.Scrolled(0f, 5, 1f, 0f)); // nothing to scroll
        // the layout keeps the scroll place between its ends: everything moves up by it, the thumb down
        Assert.Equal(0f, Reference(panels, -50f).Scroll);
        StorageColumnLayout bottom = Reference(panels, float.MaxValue);
        Assert.Equal(top.MaxScroll, bottom.Scroll);
        Assert.Equal(top.Column.Bottom, bottom.Thumb.Bottom);
        Assert.Equal(top.Column.Bottom, bottom.Panels[^1].Backing.Bottom); // the last panel ends at the column's bottom
        Assert.Equal(top.Panels[0].Backing.Top - top.MaxScroll, bottom.Panels[0].Backing.Top);
        // scrolled by 60 the first row of cells (from 185 to 233) is cut at the column's top (141 + 60): picked only on its part inside the column
        StorageColumnLayout cut = Reference(panels, 60f);
        HudRect first = cut.Panels[0].Cells[0];
        Assert.Equal(R(1241, 125, 48, 48), first);
        List<HudDrawn> drawn = DrawnOf(cut);
        Assert.Equal(R(1241, 141, 48, 32), drawn.Single(d => d.EntryId == StorageColumn.CellId(0, 0)).Rect);
        Assert.DoesNotContain(drawn, d => d.Rect.Top < cut.Column.Top || d.Rect.Bottom > cut.Column.Bottom);
        // a panel scrolled wholly out is not picked at all
        Assert.DoesNotContain(DrawnOf(bottom), d => d.EntryId == StorageColumn.PanelId(0));
    }

    [Fact]
    public void AColumnTooNarrowOrTooLowForACellPlacesNothingAndTheLayoutAlwaysMatchesItsPanels()
    {
        // every width and height from 0 to 60 with storages listed, at 100 % and at 300 %: no throw; the layout's panels are all the panels or none
        // (what the HUD's view draws, HudLayer.UpdateColumn, follows the layout's), nothing is picked without them, the thumb never taller than its track
        (List<CarriedStorage> s, HotbarModel bar) = Carried(9, 9);
        foreach (List<StoragePanel> panels in new[] { StorageColumn.Panels(s, StorageColumn.BeltOnBar(bar)), Many() })
        {
            foreach ((float cell, float unit) in new[] { (48f, 1f), (144f, 3f) })
            {
                for (int width = 0; width <= 60; width++)
                {
                    foreach (int height in new[] { 0, 1, 5, 30, 60, 843 })
                    {
                        var column = new HudRect(1237, 141, width, height);
                        StorageColumnLayout l = StorageColumn.Layout(column, cell, unit, panels, 100f);
                        string at = $"{width}x{height} at {unit}";
                        Assert.True(l.Panels.Length == 0 || l.Panels.Length == panels.Count, at);
                        List<HudDrawn> drawn = DrawnOf(l);
                        if (l.Panels.Length == 0)
                            Assert.Empty(drawn);
                        Assert.All(drawn, d => Assert.True(Inside(d.Rect, column), $"{at}: {d.Rect}"));
                        Assert.True(l.Thumb.Height <= l.Track.Height, $"{at}: thumb {l.Thumb} track {l.Track}");
                        Assert.All(l.Panels.SelectMany(p => p.Cells), c => Assert.True(c.Width >= UiTokens.TabCellMinPixels, $"{at}: {c}"));
                    }
                }
            }
        }
        // under one cell of the least size beside a scroll bar no panel is placed
        Assert.Empty(StorageColumn.Layout(new HudRect(0, 0, 27, 843), 48f, 1f, Many(), 0f).Panels);
        Assert.NotEmpty(StorageColumn.Layout(new HudRect(0, 0, 28, 843), 48f, 1f, Many(), 0f).Panels);
    }

    [Fact]
    public void ThePanelsPickIdsAreOursAndNamePanelAndCell()
    {
        var ids = new HashSet<long> { TabInventory.SpriteId };
        for (int c = 1; c <= HotbarMessage.BeltCells; c++)
            ids.Add(LiveHud.BeltCellId(c));
        // slice 4C-2: every entry of the message has ids, the other container's (the last) too
        for (int p = 0; p < Limits.MaxStorageEntries; p++)
        {
            Assert.True(StorageColumn.Decode(StorageColumn.PanelId(p), out int panel, out int cell));
            Assert.Equal((p, -1), (panel, cell));
            Assert.True(ids.Add(StorageColumn.PanelId(p)));
            for (int c = 0; c < UiTokens.StorageMaxCells; c++)
            {
                long id = StorageColumn.CellId(p, c);
                Assert.True(StorageColumn.Decode(id, out panel, out cell));
                Assert.Equal((p, c), (panel, cell));
                Assert.True(LiveHud.IsOurs(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 0, 0)));
                Assert.True(ids.Add(id), $"{id} named twice");
            }
        }
        // slice 4C-2: the Nearby strip's backing and each of its cells: ours, named once, none a panel's
        Assert.True(StorageColumn.DecodeStrip(StorageColumn.StripId, out int strip) && strip == -1);
        Assert.True(ids.Add(StorageColumn.StripId));
        for (int c = 0; c < NearbyItems.MaxItems; c++)
        {
            long id = StorageColumn.StripCellId(c);
            Assert.True(StorageColumn.DecodeStrip(id, out strip));
            Assert.Equal(c, strip);
            Assert.False(StorageColumn.Decode(id, out _, out _));
            Assert.True(LiveHud.IsOurs(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 0, 0)));
            Assert.True(ids.Add(id), $"{id} named twice");
        }
        foreach (long other in new long[] { 0, 5, -1, -7, -100, -999, StorageColumn.PanelId(Limits.MaxStorageEntries), StorageColumn.CellId(0, UiTokens.StorageMaxCells) })
        {
            Assert.False(StorageColumn.Decode(other, out _, out _), other.ToString());
            Assert.False(StorageColumn.DecodeStrip(other, out _), other.ToString());
        }
        Assert.False(StorageColumn.DecodeStrip(StorageColumn.StripCellId(NearbyItems.MaxItems), out _));
        Assert.False(StorageColumn.DecodeStrip(StorageColumn.StripId + 1, out _));
    }

    [Fact]
    public void AStoragesPictureIsTheItemDrawnInItsSlotOrHand()
    {
        var screen = new Dictionary<long, ScreenEntry>
        {
            [7] = new ScreenEntry(7, new TableRef(1, true), "x")
            {
                SlotId = 1u << 10,
                VisChildren = new ScreenChildren { Items = new[] { new ScreenChild(100, new TableRef(42, true)) { IsItem = true } } },
            },
        };
        Assert.Equal(new TableRef(42, true), StorageColumn.PictureOf(screen, 100));
        Assert.True(StorageColumn.PictureOf(screen, 300).IsNone);
    }
}
