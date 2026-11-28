using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 16: the hand boxes, the bar's cells and the worn slots' cells under Tab drawn with tg's own slot pictures of the player's
/// HUD style: which picture each kind of box takes (the entry's own state, the active mark, the marks, the belt cells' blank state of the same
/// style), what is drawn over it (the red border of a marked hand only, a refused press's rim only), and where the 32 x 32 picture is drawn in its
/// box at every screen and UI scale.
/// </summary>
public class SlotArtTests
{
    private const long HandLeftId = 2001, HandRightId = 2002;
    private const string Midnight = "icons/hud/screen_midnight.dmi";
    private const uint LeftPocket = 1u << 15, RightPocket = 1u << 16, IdSlot = 1u << 8, Uniform = 1u << 1;

    private static (PlayRig Rig, ReplicaWorld World) Start()
    {
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        return (rig, feed.Replica);
    }

    private static ScreenEntry Slot(ReplicaWorld world, uint slotId) => world.Screen.Values.Single(e => e.SlotId == slotId && e.HandIndex == 0);

    private static string State(ReplicaWorld world, TableRef appearance)
        => world.TryGetAppearance(appearance, out AppearanceEntry look) ? world.ResolveString(look.IconState) : null;

    // the entry with its appearance's icon state replaced (tg's update_icon_state: icon_full while the slot holds an item), its overlays kept
    private static ScreenEntry WithState(ReplicaWorld world, ScreenEntry entry, string state)
    {
        Assert.True(world.TryGetAppearance(entry.Appearance, out AppearanceEntry look));
        AppearanceEntry changed = look.WithOverlays(_ => true);
        changed.IconState = world.LocalString(state);
        return entry with { Appearance = world.LocalAppearance(changed) };
    }

    [Fact]
    public void EachBoxTakesTgsOwnSlotPictureAsTheServerSendsItAndTheBeltCellsTheBlankStateOfTheSameStyle()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        ScreenEntry left = world.Screen[HandLeftId], right = world.Screen[HandRightId];
        // the hands: tg's hand_l and hand_r of the player's HUD style (hud.dm build_hand_slots)
        Assert.Equal((Midnight, "hand_l"), LiveHud.SlotArt(world, left));
        Assert.Equal((Midnight, "hand_r"), LiveHud.SlotArt(world, right));
        // the pockets, the ID slot and a worn slot: their own empty pictures (human.dm icon_state), and tg's icon_full when they hold something
        Assert.Equal((Midnight, "pocket"), LiveHud.SlotArt(world, Slot(world, LeftPocket)));
        Assert.Equal((Midnight, "pocket"), LiveHud.SlotArt(world, Slot(world, RightPocket)));
        Assert.Equal((Midnight, "id"), LiveHud.SlotArt(world, Slot(world, IdSlot)));
        Assert.Equal((Midnight, "uniform"), LiveHud.SlotArt(world, Slot(world, Uniform)));
        Assert.Equal((Midnight, "template_small"), LiveHud.SlotArt(world, WithState(world, Slot(world, LeftPocket), "template_small")));
        Assert.Equal((Midnight, "template"), LiveHud.SlotArt(world, WithState(world, Slot(world, Uniform), "template")));
        // the picture is the entry's own state alone: a held item (an overlay and a child of the box) does not change it
        rig.Session.Click(new ClickTarget(TargetKind.Entity, ((FakeFeed)rig.Feed).Server.ToolboxId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        rig.Run(0.4);
        ScreenEntry holding = world.Screen[HandLeftId];
        Assert.NotNull(holding.VisChildren);
        Assert.Equal((Midnight, "hand_l"), LiveHud.SlotArt(world, holding));
        // an entry with no readable picture has none
        Assert.Null(LiveHud.SlotArt(world, holding with { Appearance = TableRef.None }));

        // the belt's seven cells, which have no slot of tg's: tg's blank slot state of the icon file the pockets and the ID slot are drawn from
        Assert.Equal("template_small", LiveHud.BlankSlotState);
        Assert.Equal((Midnight, "template_small"), LiveHud.BlankSlotArt(world, new[] { Slot(world, LeftPocket), Slot(world, IdSlot), left }));
        // another HUD style (the player's preference) is followed
        Assert.True(world.TryGetAppearance(Slot(world, RightPocket).Appearance, out AppearanceEntry pocket));
        AppearanceEntry plasma = pocket.WithOverlays(_ => true);
        plasma.Icon = world.LocalString("icons/hud/screen_plasmafire.dmi");
        ScreenEntry styled = Slot(world, RightPocket) with { Appearance = world.LocalAppearance(plasma) };
        Assert.Equal(("icons/hud/screen_plasmafire.dmi", "template_small"), LiveHud.BlankSlotArt(world, new[] { styled, left }));
        // with no pocket or ID slot the hands' style; with nothing readable, no picture
        Assert.Equal((Midnight, "template_small"), LiveHud.BlankSlotArt(world, new[] { holding with { Appearance = TableRef.None }, right }));
        Assert.Null(LiveHud.BlankSlotArt(world, Array.Empty<ScreenEntry>()));
        Assert.Null(LiveHud.BlankSlotArt(world, new[] { holding with { Appearance = TableRef.None } }));
    }

    [Fact]
    public void TheActiveHandShowsTgsActiveMarkAndAMarkedHandTgsMarkWithTheRedBorderAndNoOtherBorder()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        ScreenEntry left = world.Screen[HandLeftId], right = world.Screen[HandRightId];
        // tg's active mark (screen_objects.dm, inventory/hand update_overlays: "lhandactive" on the left hand while it is active) is drawn with the
        // box's picture; the other hand has none
        Assert.Equal("lhandactive", State(world, LiveHud.ActiveMark(world, left)));
        Assert.True(LiveHud.ActiveMark(world, right).IsNone);
        rig.Session.Keybinding(KeybindingCode.SwapHands);
        rig.Run(0.3);
        Assert.True(LiveHud.ActiveMark(world, world.Screen[HandLeftId]).IsNone);
        Assert.Equal("rhandactive", State(world, LiveHud.ActiveMark(world, world.Screen[HandRightId])));
        // the active mark is not one of the marks drawn over the items
        Assert.Empty(LiveHud.HandOverlays(world, world.Screen[HandRightId]));

        // the handcuffs and the blocked mark: drawn over the items, and the box gets the red border over them, active or not
        Assert.True(world.TryGetAppearance(world.Screen[HandRightId].Appearance, out AppearanceEntry look));
        foreach (string mark in new[] { "markus", "gabrielle", "blocked" })
        {
            AppearanceEntry marked = look.WithOverlays(_ => true);
            TableRef overlay = world.LocalAppearance(new AppearanceEntry { Icon = world.LocalString("icons/hud/screen_gen.dmi"), IconState = world.LocalString(mark) });
            marked.Overlays.Add(overlay);
            ScreenEntry hand = world.Screen[HandRightId] with { Appearance = world.LocalAppearance(marked) };
            Assert.Equal(new[] { overlay }, LiveHud.HandOverlays(world, hand));
            Assert.Equal("rhandactive", State(world, LiveHud.ActiveMark(world, hand))); // still the active hand under its mark
            Assert.True(LiveHud.HandMarked(world, hand), mark);
            Assert.Equal(UiTokens.HandMarkedBorder, LiveHud.HandFrame(LiveHud.HandMarked(world, hand)));
        }
        // no amber border on the active hand any more (tg's active mark shows it), and no rim on the other one
        Assert.False(LiveHud.HandMarked(world, world.Screen[HandRightId]));
        Assert.Equal(0u, LiveHud.HandFrame(marked: false));
        Assert.Equal(UiTokens.Security, UiTokens.HandMarkedBorder);
    }

    [Fact]
    public void TheActiveMarkIsAPictureOfTheHandsStyleWithItsOwnIconFileOrWithoutOne()
    {
        (_, ReplicaWorld world) = Start();
        ScreenEntry left = world.Screen[HandLeftId], right = world.Screen[HandRightId];
        // the mark with its icon file (the real replicator's encoding: tg's text overlay made an appearance of the atom's icon by build_appearance_list,
        // Integration/HotbarSlotArtGameplayTests): drawn from that file
        Assert.Equal((Midnight, "lhandactive"), LiveHud.ActiveMarkArt(world, left));
        Assert.Null(LiveHud.ActiveMarkArt(world, right));
        // a bare text overlay (an icon state and no icon file, as BYOND keeps a string overlay before it takes its atom's icon): the hand's own file
        Assert.True(world.TryGetAppearance(left.Appearance, out AppearanceEntry look));
        AppearanceEntry bare = look.WithOverlays(o => LiveHud.ActiveMark(world, left) != o);
        bare.Overlays.Add(world.LocalAppearance(new AppearanceEntry { IconState = world.LocalString("lhandactive") }));
        ScreenEntry textOverlay = left with { Appearance = world.LocalAppearance(bare) };
        Assert.True(world.TryGetAppearance(LiveHud.ActiveMark(world, textOverlay), out AppearanceEntry mark));
        Assert.True(mark.Icon.IsNone);
        Assert.Equal((Midnight, "lhandactive"), LiveHud.ActiveMarkArt(world, textOverlay));
        // a mark from another icon file keeps its own
        AppearanceEntry other = look.WithOverlays(o => LiveHud.ActiveMark(world, left) != o);
        other.Overlays.Add(world.LocalAppearance(new AppearanceEntry { Icon = world.LocalString("icons/hud/screen_glass.dmi"), IconState = world.LocalString("lhandactive") }));
        Assert.Equal(("icons/hud/screen_glass.dmi", "lhandactive"), LiveHud.ActiveMarkArt(world, left with { Appearance = world.LocalAppearance(other) }));
        // a hand with no readable picture has no mark drawn
        Assert.Null(LiveHud.ActiveMarkArt(world, left with { Appearance = TableRef.None }));
    }

    [Fact]
    public void ASlotsPictureTakesTheEntrysColourAndAlphaTimesTheCellsOpacity()
    {
        (_, ReplicaWorld world) = Start();
        ScreenEntry pocket = Slot(world, LeftPocket);
        ScreenEntry With(Action<AppearanceEntry> change)
        {
            Assert.True(world.TryGetAppearance(pocket.Appearance, out AppearanceEntry look));
            AppearanceEntry copy = look.WithOverlays(_ => true);
            change(copy);
            return pocket with { Appearance = world.LocalAppearance(copy) };
        }
        // as tg draws it: white, opaque; at a half-seen cell (a belt cell with no belt) half
        Assert.Equal(0xFFFFFFFFu, LiveHud.SlotTint(world, pocket));
        Assert.Equal(0xFFFFFF80u, LiveHud.SlotTint(world, pocket, 0.5f));
        // tg's locked slot (human.dm update_locked_slots: alpha 128): half; on a half-seen cell a quarter
        ScreenEntry locked = With(e => e.Alpha = 128);
        Assert.Equal(0xFFFFFF80u, LiveHud.SlotTint(world, locked));
        Assert.Equal(0xFFFFFF40u, LiveHud.SlotTint(world, locked, 0.5f));
        // tg's colour (the HUD colour preference) is kept, its own alpha counted with the entry's
        Assert.Equal(0x3366CCFFu, LiveHud.SlotTint(world, With(e => e.Rgba = 0x3366CCFF)));
        Assert.Equal(0x3366CC40u, LiveHud.SlotTint(world, With(e => { e.Rgba = 0x3366CC80; e.Alpha = 128; })));
        // no entry (a belt cell): white at the cell's opacity; an entry with no appearance the same
        Assert.Equal(0xFFFFFF80u, LiveHud.SlotTint(world, null, 0.5f));
        Assert.Equal(0xFFFFFFFFu, LiveHud.SlotTint(world, pocket with { Appearance = TableRef.None }));
    }

    [Fact]
    public void TgsMarksGoOverTheWholeBoxAndAHeldItemFromTheOverlaysInThePictureSquare()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        rig.Session.Click(new ClickTarget(TargetKind.Entity, ((FakeFeed)rig.Feed).Server.ToolboxId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        rig.Run(0.4);
        ScreenEntry hand = world.Screen[HandLeftId];
        ScreenChild toolbox = Assert.Single(hand.VisChildren.Items);
        Assert.True(world.TryGetAppearance(hand.Appearance, out AppearanceEntry look));
        TableRef cuffs = world.LocalAppearance(new AppearanceEntry { Icon = world.LocalString("icons/hud/screen_gen.dmi"), IconState = world.LocalString("gabrielle") });
        AppearanceEntry withCuffs = look.WithOverlays(_ => true);
        withCuffs.Overlays.Add(cuffs);
        // the box no longer lists the toolbox as a child (mouse_opacity 0): it is one of the overlays drawn over the items, with the cuffs
        ScreenEntry both = hand with { Appearance = world.LocalAppearance(withCuffs), VisChildren = null };
        List<TableRef> overlays = LiveHud.HandOverlays(world, both);
        Assert.Equal(new[] { toolbox.Appearance, cuffs }, overlays);
        Assert.False(LiveHud.IsSlotMark(world, toolbox.Appearance)); // the item: in the picture square, as the box's children
        Assert.True(LiveHud.IsSlotMark(world, cuffs)); // tg's mark: over the slot's whole picture
        foreach (string mark in new[] { "markus", "blocked" })
            Assert.True(LiveHud.IsSlotMark(world, world.LocalAppearance(new AppearanceEntry { IconState = world.LocalString(mark) })), mark);
        Assert.False(LiveHud.IsSlotMark(world, LiveHud.ActiveMark(world, hand)));
        // where each lands at the reference size: the item's 32 x 32 in the 60 square inset 6, the cuffs' in the box's whole 72
        HandsLayout hands = LiveHud.Hands(new HudMetrics(1920, 1080, 1f), true, false);
        HudRect art = LiveHud.SlotArtSquare(hands.Left), square = LiveHud.HandPicture(hands.Left, hands.Unit);
        HudRect Where(TableRef overlay) => LiveHud.ChildPicture(LiveHud.IsSlotMark(world, overlay) ? art : square, 32, 32, 0, 0);
        Assert.Equal(new HudRect(620, 998, 60, 60), Where(toolbox.Appearance));
        Assert.Equal(new HudRect(614, 992, 72, 72), Where(cuffs));
    }

    [Fact]
    public void AtTheReferenceSizeTgsPictureFillsEachBoxAndTheItemKeepsItsSquareInside()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        HandsLayout hands = LiveHud.Hands(metrics, leftActive: true, rightActive: false);
        // the active hand's 72, the other's 56, a cell's 48: tg's 32 x 32 picture fills each, at 2.25, 1.75 and 1.5 screen pixels per tg pixel
        Assert.Equal(new HudRect(614, 992, 72, 72), LiveHud.SlotArtSquare(hands.Left));
        Assert.Equal(new HudRect(1234, 1008, 56, 56), LiveHud.SlotArtSquare(hands.Right));
        Assert.Equal(new HudRect(694, 1016, 48, 48), LiveHud.SlotArtSquare(LiveHud.BarCell(hands, 1)));
        Assert.Equal(new HudRect(1066, 1016, 48, 48), LiveHud.SlotArtSquare(LiveHud.BarCell(hands, 8)));
        Assert.Equal(new HudRect(1178, 1016, 48, 48), LiveHud.SlotArtSquare(LiveHud.BarCell(hands, 10)));
        // tg's active mark (32 x 32 at the picture's corner) lands exactly on the picture
        Assert.Equal(new HudRect(614, 992, 72, 72), ScreenLoc.FitIcon(LiveHud.SlotArtSquare(hands.Left), 32, 32, 0, 0));
        // the held item keeps its square, 6 in from the box's edge, as before
        Assert.Equal(new HudRect(620, 998, 60, 60), LiveHud.HandPicture(hands.Left, hands.Unit));
        Assert.Equal(new HudRect(700, 1022, 36, 36), LiveHud.HandPicture(LiveHud.BarCell(hands, 1), hands.Unit));
        // a box that is not square: the largest square of whole pixels, centred
        Assert.Equal(new HudRect(15, 20, 40, 40), LiveHud.SlotArtSquare(new HudRect(10.4f, 20f, 50f, 40f)));
        Assert.Equal(new HudRect(10, 23, 40, 40), LiveHud.SlotArtSquare(new HudRect(10f, 20.2f, 40.6f, 46.5f)));
        Assert.Equal(new HudRect(10, 35, 0, 0), LiveHud.SlotArtSquare(new HudRect(10f, 20f, 0f, 30f)));
    }

    public static IEnumerable<object[]> Screens()
    {
        foreach ((int w, int h) in new[] { (1024, 600), (1280, 720), (1280, 1024), (1920, 1080), (1920, 1200), (2560, 1440), (3840, 2160) })
        {
            for (int percent = 50; percent <= 300; percent += 5)
                yield return new object[] { w, h, percent };
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void TgsPictureFillsEveryBoxAtWholePixelsOnEveryScreenAndScale(int width, int height, int percent)
    {
        var metrics = new HudMetrics(width, height, percent / 100f);
        foreach (bool leftActive in new[] { true, false })
        {
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, !leftActive);
            var boxes = new List<(string What, HudRect Box, float Unit)> { ("left hand", hands.Left, hands.Unit), ("right hand", hands.Right, hands.Unit) };
            for (int cell = 1; cell <= HotbarMessage.CellCount; cell++)
                boxes.Add(($"cell {cell}", LiveHud.BarCell(hands, cell), hands.Unit));
            TabInventoryLayout panel = TabInventory.Layout(metrics, hands, LiveHud.Layout(metrics, human: true), Array.Empty<HudRect>());
            if (panel.Shown)
            {
                for (int cell = 0; cell < panel.Cells.Length; cell++)
                    boxes.Add(($"worn cell {cell}", panel.Cells[cell], panel.CellUnit));
            }
            foreach ((string what, HudRect box, float unit) in boxes)
            {
                string at = $"{width}x{height} {percent}% {what} {box}";
                HudRect art = LiveHud.SlotArtSquare(box);
                // inside the box, square, at whole pixels; the boxes are whole-pixel squares, so it is the box itself
                Assert.True(art.Left >= box.Left && art.Top >= box.Top && art.Right <= box.Right && art.Bottom <= box.Bottom, at);
                Assert.Equal(art.Width, art.Height);
                foreach (float v in new[] { art.Left, art.Top, art.Width })
                    Assert.True(v == MathF.Round(v), $"{at}: {art}");
                Assert.Equal(box, art);
                // the item's square stays inside tg's picture
                HudRect item = LiveHud.HandPicture(box, unit);
                Assert.True(item.Left >= art.Left && item.Top >= art.Top && item.Right <= art.Right && item.Bottom <= art.Bottom, $"{at}: item {item}");
            }
        }
    }

    [RealIconsFact]
    public void EveryHumanHudStyleHasTheBlankStateTheSizeOfItsPocketAndIdPictures()
    {
        // tg's HUD styles the human HUD takes (ui_style2icon); the belt cells' blank state is drawn beside the pocket and ID cells: in every style it
        // exists and covers the same pixels as those pictures do, so the bar reads as one row of one size. Retro draws its pocket, ID and belt
        // slots as bare symbols and its blank state as nothing (tg's full slot there shows the item alone): its belt cells show no picture.
        IconLibrary icons = RealIcons.NewLibrary();
        foreach (string style in new[] { "midnight", "retro", "plasmafire", "slimecore", "operative", "clockwork", "glass", "trasenknox", "detective" })
        {
            string file = $"icons/hud/screen_{style}.dmi";
            Assert.True(icons.TryGetIcon(file, out IIcon sheet), file);
            HudRect? blank = Opaque(sheet, LiveHud.BlankSlotState, file);
            if (style == "retro")
                Assert.Null(blank);
            else
            {
                foreach (string state in new[] { "pocket", "id", "belt" })
                    Assert.Equal(Opaque(sheet, state, file), blank);
            }
            foreach (string state in new[] { "hand_l", "hand_r", "lhandactive", "rhandactive", "template" })
                Assert.True(sheet.TryGetState(state, out _), $"{file} {state}");
        }
        // the default style's: 28 x 28 inside tg's 32 x 32 square, 2 in from each side, so in a 48 cell its frame is 3 in from the cell's edge
        Assert.True(icons.TryGetIcon("icons/hud/screen_midnight.dmi", out IIcon midnight));
        Assert.Equal(new HudRect(2, 2, 28, 28), Opaque(midnight, LiveHud.BlankSlotState, "midnight"));
        Assert.Equal(new HudRect(0, 0, 32, 32), Opaque(midnight, "hand_l", "midnight"));
    }

    // the opaque bounds of a state's first picture, in its 32 x 32 square; null when it draws nothing
    private static HudRect? Opaque(IIcon sheet, string state, string file)
    {
        Assert.True(sheet.TryGetState(state, out IIconState found), $"{file} {state}");
        Raster picture = found.Get(0, 0);
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < picture.Height; y++)
        {
            for (int x = 0; x < picture.Width; x++)
            {
                if ((picture.Get(x, y) & 0xFF) == 0)
                    continue;
                (left, right, top, bottom) = (Math.Min(left, x), Math.Max(right, x), Math.Min(top, y), Math.Max(bottom, y));
            }
        }
        return right < 0 ? null : new HudRect(left, top, right - left + 1, bottom - top + 1);
    }
}
