using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 6, slice B: which screen entries the live HUD re-draws (the doll, the combat toggle) or hides (tg's zone selector in the
/// captured view), combat mode read off the replica, the doll panel and the combat pill placed clear of tg's right-hand column at every HUD
/// scale, and clicks on the re-placed doll and on the pill reaching tg's entries with tg's icon pixels. Slice C: the hands known by hand index and
/// the bottom slots by slot id on the human HUD only, the active hand and its swaps, the hands' row, the bottom slots and the held-item card clear
/// of each other and of tg's HUD and the chat window, and a click on a hand box reaching tg's hand box or the held item.
/// </summary>
public class LiveHudTests
{
    private const long DollId = 1001, HealthId = 1002, IntentId = 1004, HandLeftId = 2001, HandRightId = 2002, SwapId = 2003, DropId = 2004, ThrowId = 2005,
        FirstSlotId = 2010, BeltSlotId = 2024;

    private static (PlayRig Rig, ReplicaWorld World) Start()
    {
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        return (rig, feed.Replica);
    }

    private static void ClickScreen(PlayRig rig, long id, byte iconX, byte iconY)
    {
        Assert.True(rig.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, iconX, iconY), ClickButton.Left, ClickModifiers.None));
        rig.Run(0.4);
    }

    [Fact]
    public void TheDollTheZoneSelectorAndTheCombatToggleAreRecognisedAndNothingElse()
    {
        (_, ReplicaWorld world) = Start();
        Assert.Equal(LiveHudRole.Doll, LiveHud.RoleOf(world, world.Screen[DollId]));
        Assert.Equal(LiveHudRole.ZoneSelector, LiveHud.RoleOf(world, world.Screen[FakeServer.ZoneSelId]));
        Assert.Equal(LiveHudRole.CombatToggle, LiveHud.RoleOf(world, world.Screen[FakeServer.CombatId]));
        foreach (long id in new[] { HealthId, IntentId, ThrowId, FirstSlotId })
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, world.Screen[id]));
        // every other entry of the fake's HUD stays at tg's place, but slice C's: the two hands, the six bottom slots and the inventory toggle
        Assert.Equal(3 + 2 + 6 + 1, world.Screen.Values.Count(e => LiveHud.RoleOf(world, e) != LiveHudRole.Tg));
        // tg puts them where the fake does: the zone selector at ui_zonesel, the toggle at ui_acti
        Assert.Equal("EAST-1:28,SOUTH:5", world.Screen[FakeServer.ZoneSelId].ScreenLoc);
        Assert.Equal("EAST-3:24,SOUTH:5", world.Screen[FakeServer.CombatId].ScreenLoc);
    }

    [Fact]
    public void AnEntryOfANativeWindowIsNeverRedrawnEvenWithTheToggleOrSelectorIconState()
    {
        (_, ReplicaWorld world) = Start();
        foreach (ScreenEntry entry in new[] { world.Screen[FakeServer.CombatId], world.Screen[FakeServer.ZoneSelId] })
        {
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { Group = HudGroup.Storage }));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { Group = HudGroup.ToggleableInventory }));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { ActionTarget = 5 }));
            // decided by icon state, not place
            Assert.NotEqual(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { ScreenLoc = "WEST,NORTH" }));
        }
    }

    [Fact]
    public void TheZoneSelectorIsShownOnlyWhileTheCursorIsFree()
    {
        Assert.False(LiveHud.Shown(LiveHudRole.ZoneSelector, cursorMode: false));
        Assert.True(LiveHud.Shown(LiveHudRole.ZoneSelector, cursorMode: true));
        foreach (LiveHudRole role in new[] { LiveHudRole.Tg, LiveHudRole.Doll, LiveHudRole.CombatToggle })
        {
            Assert.True(LiveHud.Shown(role, cursorMode: false));
            Assert.True(LiveHud.Shown(role, cursorMode: true));
        }
    }

    [Fact]
    public void AClickOnTheShownZoneSelectorStillSelectsTheZoneTheEyesMouthAndGroinIncluded()
    {
        // in cursor mode the selector is tg's, at tg's place: its clicks go to the server as before (the fake answers with tg's get_zone_at)
        (PlayRig rig, ReplicaWorld world) = Start();
        string Selected() => world.TryGetAppearance(world.Screen[FakeServer.ZoneSelId].Appearance, out AppearanceEntry app)
            && world.TryGetAppearance(app.Overlays[0], out AppearanceEntry overlay) ? world.ResolveString(overlay.IconState) : null;
        Assert.Equal("chest", Selected());
        ClickScreen(rig, FakeServer.ZoneSelId, 16, 26);
        Assert.Equal("eyes", Selected());
        ClickScreen(rig, FakeServer.ZoneSelId, 16, 12);
        Assert.Equal("groin", Selected());
        ClickScreen(rig, FakeServer.ZoneSelId, 16, 24);
        Assert.Equal("mouth", Selected());
    }

    [Fact]
    public void CombatModeIsReadFromTheToggleOnAndOff()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        Assert.False(LiveHud.CombatOn(world, world.Screen[FakeServer.CombatId]));
        Assert.Equal("Peaceful", LiveHud.CombatText(false));
        rig.Session.ToggleCombatMode(); // the key (F by default)
        rig.Run(0.4);
        Assert.True(LiveHud.CombatOn(world, world.Screen[FakeServer.CombatId]));
        Assert.True(HeldHands.Find(world).Combat); // the swing sound's reading agrees
        Assert.Equal("Combat", LiveHud.CombatText(true));
        ClickScreen(rig, FakeServer.CombatId, 16, 16); // tg's click on the toggle, which the pill reports
        Assert.False(LiveHud.CombatOn(world, world.Screen[FakeServer.CombatId]));
        Assert.False(HeldHands.Find(world).Combat);
    }

    [Fact]
    public void AWoundedLimbCarriesTgsRedWoundOutlineAndHealingRemovesIt()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        FilterEntry Outline(string zone)
        {
            var zones = new List<FoundZone>();
            HudReader.Collect(world, world.Screen[DollId].Appearance, zones);
            FoundZone found = zones.Single(z => z.Zone.Zone == zone);
            Assert.True(world.TryGetAppearance(found.Node, out AppearanceEntry limb));
            return limb.Filters?.SingleOrDefault();
        }
        Assert.Null(Outline("r_arm"));
        rig.Session.DevCommand("wound me r_arm slash severe");
        rig.Run(0.4);
        FilterEntry wound = Outline("r_arm");
        Assert.NotNull(wound);
        Assert.Equal(FilterKind.Outline, wound.Kind);
        Assert.Equal(0xFF0033u, wound.Rgba >> 8); // tg's #FF0033 (screen_objects.dm:1031)
        Assert.Null(Outline("l_arm"));
        rig.Session.DevCommand("heal me");
        rig.Run(0.4);
        Assert.Null(Outline("r_arm"));
    }

    // tg's entries that stand in the right-hand column (EAST-1) and the bottom-right buttons around the panel's column (__DEFINES/hud.dm)
    private static readonly string[] RightColumn =
    {
        "EAST-1:28,CENTER-1:19", // ui_health
        "EAST-1:28,CENTER+1:21", // ui_internal
        "EAST-1:28,CENTER:21", // ui_mood
        "EAST-1:2,CENTER:21", // ui_hunger
        "EAST-1:28,CENTER-4:14", // ui_spacesuit
        "EAST-1:28,CENTER-3:14", // ui_stamina
        "EAST-1:28,CENTER-3:15", // ui_living_pull
        "EAST-1:28,CENTER-1:15", // ui_living_healthdoll, ui_borg_health
        "EAST-1:28,SOUTH:5", // ui_zonesel
        "EAST-1:28,SOUTH+1:7", // ui_below_throw, ui_borg_radio
        "EAST-1:28,SOUTH+1:24", // ui_drop_throw
        "EAST-1:28,SOUTH+1:41", // ui_above_throw
        "BOTTOM+1:8,RIGHT-1:28", // ui_floor_change
        "EAST-1:28,SOUTH:6", // ui_basic_combat_toggle
        "EAST-2:26,SOUTH:5", // ui_movi
        "EAST-2:26,SOUTH+1:7", // ui_above_movement
        "EAST-2:26, SOUTH+1:24", // ui_above_movement_top
        "EAST-3:24,SOUTH+1:7", // ui_human_crafting
        "EAST-3:24,SOUTH+1:24", // ui_human_area
        "EAST-3:7,SOUTH+1:24", // ui_human_language
    };

    private static bool Overlaps(HudRect a, HudRect b) => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Inside(HudRect inner, HudRect outer) =>
        inner.Left >= outer.Left - 0.01f && inner.Right <= outer.Right + 0.01f && inner.Top >= outer.Top - 0.01f && inner.Bottom <= outer.Bottom + 0.01f;

    public static IEnumerable<object[]> Screens()
    {
        foreach ((int w, int h) in new[] { (1024, 600), (1280, 720), (1920, 1080), (2560, 1440) })
        {
            foreach (float scale in new[] { 0.75f, 1f, 1.25f, 2f, 2.5f, 2.85f, 3f })
                yield return new object[] { w, h, scale };
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void ThePanelAndThePillAreOnTheScreenAndClearOfTgsRightColumn(int width, int height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        LiveHudLayout layout = LiveHud.Layout(metrics);
        var screen = new HudRect(0, 0, width, height);
        Assert.True(Inside(layout.Panel, screen), $"panel {layout.Panel}");
        Assert.True(Inside(layout.Pill, screen), $"pill {layout.Pill}");
        Assert.True(Inside(layout.Doll, layout.Panel), $"doll {layout.Doll} in {layout.Panel}");
        Assert.True(layout.Pill.Top >= layout.Panel.Bottom);
        // one column: the panel as wide as the pill, the doll centred in it
        Assert.Equal(layout.Panel.Left, layout.Pill.Left);
        Assert.Equal(layout.Panel.Width, layout.Pill.Width);
        Assert.InRange(layout.Doll.Left - layout.Panel.Left - (layout.Panel.Right - layout.Doll.Right), -1f, 1f);
        // the doll: a whole multiple of tg's pixels, the panel just around it (no title)
        Assert.True(layout.DollScale >= 1);
        Assert.Equal(32f * layout.DollScale, layout.Doll.Width);
        Assert.Equal(32f * layout.DollScale, layout.Doll.Height);
        float pad = UiTokens.DollPanelPad * UiTokens.Unit(metrics);
        Assert.InRange(layout.Panel.Height - layout.Doll.Height, 2 * pad - 1f, 2 * pad + 1f);
        Assert.True(layout.Panel.Width >= layout.Doll.Width + 2 * pad - 1f);
        // at the middle right
        Assert.True(layout.Panel.Left > width / 2f, $"{layout.Panel}");
        Assert.True(layout.Panel.Top < height / 2f, $"{layout.Panel}");
        foreach (string loc in RightColumn)
        {
            Assert.True(ScreenLoc.TryParse(loc, out ScreenLocSpec spec), loc);
            HudRect tg = ScreenLoc.Place(spec, metrics, 32, 32, 0, 0, out _);
            Assert.False(Overlaps(layout.Panel, tg), $"panel {layout.Panel} over {loc} {tg}");
            Assert.False(Overlaps(layout.Pill, tg), $"pill {layout.Pill} over {loc} {tg}");
        }
        float pulseTop = ScreenLoc.Place(Parse(RightColumn[0]), metrics, 32, 32, 0, 0, out _).Top;
        for (int slot = 1; slot <= 5; slot++)
        {
            HudRect alert = ScreenLoc.PlaceAlert(slot, 5, metrics, pulseTop);
            Assert.False(Overlaps(layout.Panel, alert) || Overlaps(layout.Pill, alert), $"alert {slot} {alert}");
        }
        // the zone labels and the prompts end before both
        float edge = LiveHud.LeftOfPanel(layout, metrics);
        Assert.True(edge < layout.Panel.Left && edge < layout.Pill.Left);
    }

    private static ScreenLocSpec Parse(string loc) => ScreenLoc.TryParse(loc, out ScreenLocSpec spec) ? spec : default;

    [Fact]
    public void TheDollIsThreeTgPixelsPerPixelAtTheReferenceSizeAndShrinksOnlyWhenItDoesNotFit()
    {
        Assert.Equal(3, LiveHud.Layout(new HudMetrics(1920, 1080, 1f)).DollScale);
        Assert.Equal(6, LiveHud.Layout(new HudMetrics(1920, 1080, 2f)).DollScale);
        Assert.Equal(2, LiveHud.Layout(new HudMetrics(1280, 720, 1f)).DollScale);
        // at 300% on 1080 lines the full 9x doll would push the pill over tg's buttons (the probe: pill 444 to 540 over ui_human_area from 486)
        LiveHudLayout big = LiveHud.Layout(new HudMetrics(1920, 1080, 3f));
        Assert.InRange(big.DollScale, 1, 8);
        HudRect area = ScreenLoc.Place(Parse("EAST-3:24,SOUTH+1:24"), new HudMetrics(1920, 1080, 3f), 32, 32, 0, 0, out _);
        Assert.True(big.Pill.Bottom <= area.Top, $"{big.Pill} {area}");
        Assert.True(big.Panel.Top >= 0f);
    }

    [Fact]
    public void FitIconKeepsTheIconSquareInTheBoxAndThePictureAroundIt()
    {
        var box = new HudRect(100, 200, 96, 96);
        Assert.Equal(box, ScreenLoc.FitIcon(box, 32, 32, 0, 0));
        // a picture with 2 pixels around its icon square (a filter's outline): the square stays the box
        Assert.Equal(new HudRect(94, 194, 108, 108), ScreenLoc.FitIcon(box, 36, 36, 2, 2));
        // a box that is not square: the icon fills the shorter side, centred
        Assert.Equal(new HudRect(132, 200, 32, 32), ScreenLoc.FitIcon(new HudRect(100, 200, 96, 32), 32, 32, 0, 0));
    }

    private sealed class StubSurface(List<HudDrawn> drawn) : IHudSurface
    {
        public void Collect(List<HudDrawn> bottomToTop) => bottomToTop.AddRange(drawn);
    }

    // The screen point at the middle of tg's icon pixel (x, y) (1-based, y up) of an icon square drawn at `square`.
    private static (float X, float Y) PixelCentre(HudRect square, int x, int y)
    {
        float k = square.Width / 32f;
        return (square.Left + (x - 0.5f) * k, square.Bottom - (y - 0.5f) * k);
    }

    [Theory]
    [InlineData(1920, 1080, 1f)]
    [InlineData(1280, 720, 1.25f)]
    [InlineData(2560, 1440, 2f)]
    [InlineData(1920, 1080, 3f)]
    public void AClickOnTheRePlacedDollIsTheSameTgIconPixelAndAClickOnThePillIsTheToggle(int width, int height, float scale)
    {
        LiveHudLayout layout = LiveHud.Layout(new HudMetrics(width, height, scale));
        // what HudLayer.Collect reports: the doll's icon square once (whatever size its zones' pictures are: a wound outline makes them 36 x 36
        // with the square at 2,2) and the pill under the combat toggle's id
        HudDrawn doll = LiveHud.DollDrawn(DollId, layout);
        Assert.Equal(layout.Doll, doll.Rect);
        Assert.Null(doll.Image);
        var picker = new HudPicker(new StubSurface(new List<HudDrawn> { doll, LiveHud.PillDrawn(FakeServer.CombatId, layout) }));
        foreach ((int x, int y) in new[] { (1, 1), (3, 3), (16, 16), (22, 18), (32, 32), (5, 29) })
        {
            (float sx, float sy) = PixelCentre(layout.Doll, x, y);
            PickHit hit = picker.PickScreen(sx, sy);
            Assert.Equal(DollId, hit.Id);
            Assert.Equal((byte)x, hit.IconX);
            Assert.Equal((byte)y, hit.IconY);
        }
        // a wounded limb's grown picture is drawn around that square, its icon pixels on the same screen pixels
        HudRect grown = ScreenLoc.FitIcon(layout.Doll, 36, 36, 2, 2);
        float k = layout.Doll.Width / 32f;
        Assert.Equal(layout.Doll.Left - 2 * k, grown.Left, 2);
        Assert.Equal(layout.Doll.Bottom + 2 * k, grown.Bottom, 2);
        // just outside the square is not the doll
        Assert.NotEqual(DollId, picker.PickScreen(layout.Doll.Left - 1, layout.Doll.Top + 1).Id);

        PickHit pill = picker.PickScreen(layout.Pill.Left + layout.Pill.Width / 2f, layout.Pill.Top + layout.Pill.Height / 2f);
        Assert.Equal(PickSurface.Screen, pill.Surface);
        Assert.Equal(FakeServer.CombatId, pill.Id);
        Assert.Equal(FakeServer.CombatId, picker.PickScreen(layout.Pill.Left + 1, layout.Pill.Bottom - 1).Id);
        Assert.Equal(PickHit.None, picker.PickScreen(layout.Pill.Left - 1, layout.Pill.Top + 1));
    }

    // The probe (2026-11-24): on the top screen line of a tg pixel row the pick said the row above (1697,503 at 1920x1080: drawn 14,22, picked 14,23).
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void EveryScreenPixelOfTheDollsSquareIsTheTgPixelDrawnThere(int scale)
    {
        // the doll's square as LiveHud places it (whole pixels), at k screen pixels per tg pixel; mouse positions are whole screen pixels
        var square = new HudRect(1601, 437, 32 * scale, 32 * scale);
        var picker = new HudPicker(new StubSurface(new List<HudDrawn> { LiveHud.DollDrawn(DollId, new LiveHudLayout(square, square, default, scale)) }));
        for (int y = 0; y < 32 * scale; y++)
        {
            for (int x = 0; x < 32 * scale; x++)
            {
                PickHit hit = picker.PickScreen(square.Left + x, square.Top + y);
                Assert.Equal(DollId, hit.Id);
                // drawn there: column x / k from the left, row y / k from the top (tg counts y up from 1)
                Assert.Equal((byte)(x / scale + 1), hit.IconX);
                Assert.Equal((byte)(32 - y / scale), hit.IconY);
            }
        }
        // the real layout's square too, at its own scale
        LiveHudLayout layout = LiveHud.Layout(new HudMetrics(1920, 1080, 1f));
        PickHit probe = new HudPicker(new StubSurface(new List<HudDrawn> { LiveHud.DollDrawn(DollId, layout) }))
            .PickScreen(layout.Doll.Left + 13 * layout.DollScale, layout.Doll.Top + 10 * layout.DollScale); // the top line of tg's row 22
        Assert.Equal(((byte)14, (byte)22), (probe.IconX, probe.IconY));
    }

    [Fact]
    public void ACompoundFractureDisablesTheLimbOnTheFakesDoll()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        ZoneState State(string zone)
        {
            var zones = new List<FoundZone>();
            HudReader.Collect(world, world.Screen[DollId].Appearance, zones);
            return zones.Single(z => z.Zone.Zone == zone).Zone;
        }
        rig.Session.DevCommand("wound me l_arm blunt critical"); // tg: bones.dm, the compound fracture is disabling
        rig.Session.DevCommand("wound me head blunt critical"); // the head cannot be disabled (can_be_disabled)
        rig.Run(0.4);
        Assert.Equal(ZoneKind.Disabled, State("l_arm").Kind);
        Assert.Equal(ZoneKind.Level, State("head").Kind);
        rig.Session.DevCommand("heal me");
        rig.Run(0.4);
        Assert.Equal(ZoneKind.Level, State("l_arm").Kind);
    }

    [Fact]
    public void APromptBesideTheCrosshairEndsBeforeThePanelAndIsNarrowedNotMoved()
    {
        foreach ((int w, int h, float scale) in new[] { (1920, 1080, 2f), (1280, 720, 2f), (1920, 1080, 1f), (1024, 600, 3f) })
        {
            var metrics = new HudMetrics(w, h, scale);
            LiveHudLayout layout = LiveHud.Layout(metrics);
            float edge = LiveHud.LeftOfPanel(layout, metrics);
            float reach = KeyChips.SightReach(false, 0f, 70f, h, 1f);
            // a long name ("reinforced wall (wall/r wall)" at 200%: about 700 pixels wide)
            HudRect block = KeyChips.PromptBlock(metrics, 2, 700f * UiTokens.Unit(metrics), w / 2f, h / 2f, reach, edge);
            Assert.True(block.Right <= edge + 0.01f, $"{w}x{h} {scale}: {block} past {edge}");
            Assert.False(Overlaps(block, layout.Panel) || Overlaps(block, layout.Pill));
            float room = edge - (w / 2f + reach + UiTokens.PromptOffsetX * UiTokens.Unit(metrics));
            if (room >= UiTokens.PromptMinWidth * UiTokens.Unit(metrics))
            {
                Assert.True(block.Left > w / 2f, $"{w}x{h} {scale}: it stays right of the crosshair, cut");
                Assert.Equal(Math.Min(room, Math.Min(700f, UiTokens.PromptMaxWidth) * UiTokens.Unit(metrics)), block.Width, 2);
            }
            else
                Assert.True(block.Right < w / 2f, $"{w}x{h} {scale}: no room right of it, so left of the crosshair");
            // a short one that fits is where it was without the panel
            HudRect small = KeyChips.PromptBlock(metrics, 2, 40f, w / 2f, h / 2f, reach, edge);
            Assert.True(small.Right <= edge + 0.01f);
            if (room >= 40f)
                Assert.Equal(KeyChips.PromptBlock(metrics, 2, 40f, w / 2f, h / 2f, reach), small);
        }
    }

    // ---- slice C: the hands, the bottom slots in the hotbar's gap, the held-item card ----

    // the fake's six bottom slots: ID, back, left and right pocket, suit storage, belt (FakeServer.SlotDefs)
    private static readonly long[] BottomSlotIds = { 2012, 2015, 2016, 2017, 2018, BeltSlotId };

    [Fact]
    public void TheHandsAreKnownByHandIndexAndTheBottomSlotsBySlotIdNeverByPlace()
    {
        (_, ReplicaWorld world) = Start();
        Assert.Equal(LiveHudRole.LeftHand, LiveHud.RoleOf(world, world.Screen[HandLeftId]));
        Assert.Equal(LiveHudRole.RightHand, LiveHud.RoleOf(world, world.Screen[HandRightId]));
        Assert.Equal(LiveHudRole.InventoryToggle, LiveHud.RoleOf(world, world.Screen[FakeServer.InventoryToggleId]));
        foreach (long id in BottomSlotIds)
            Assert.Equal(LiveHudRole.BottomSlot, LiveHud.RoleOf(world, world.Screen[id]));
        // tg's swap, drop and throw buttons stay at tg's places
        foreach (long id in new[] { SwapId, DropId, ThrowId })
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, world.Screen[id]));
        ScreenEntry left = world.Screen[HandLeftId], right = world.Screen[HandRightId];
        // the place says nothing: the left hand at the right hand's place is the left hand, and the hand index wins over the picture
        Assert.Equal(LiveHudRole.LeftHand, LiveHud.RoleOf(world, left with { ScreenLoc = right.ScreenLoc }));
        Assert.Equal(LiveHudRole.RightHand, LiveHud.RoleOf(world, left with { HandIndex = 2 }));
        // a third and fourth hand keep tg's place
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, left with { HandIndex = 3 }));
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, right with { HandIndex = 4 }));
        // a slot of the toggled inventory or of a storage window is the window's, whatever its slot id
        ScreenEntry belt = world.Screen[BeltSlotId];
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, belt with { Group = HudGroup.ToggleableInventory }));
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, belt with { Group = HudGroup.Storage }));
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, belt with { SlotId = 0 }));
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, left with { ActionTarget = 7 }));
    }

    [Fact]
    public void OnlyTheHumanHudsHandsAndBottomSlotsAreRePlaced()
    {
        // the alien's, a basic mob's and the other HUDs have no inventory toggle: their hands and slots keep tg's places
        foreach (LiveHudRole role in new[] { LiveHudRole.LeftHand, LiveHudRole.RightHand, LiveHudRole.BottomSlot })
        {
            Assert.Equal(role, LiveHud.Placed(role, humanHud: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false));
        }
        // the doll, the combat toggle and the zone selector are slice B's on every HUD
        foreach (LiveHudRole role in new[] { LiveHudRole.Doll, LiveHudRole.CombatToggle, LiveHudRole.ZoneSelector, LiveHudRole.InventoryToggle, LiveHudRole.Tg })
            Assert.Equal(role, LiveHud.Placed(role, humanHud: false));
        Assert.True(LiveHud.Shown(LiveHudRole.LeftHand, cursorMode: false) && LiveHud.Shown(LiveHudRole.BottomSlot, cursorMode: false));
    }

    [Fact]
    public void TheActiveHandIsTgsOverlayAndASwapByKeyByButtonOrByAClickOnTheOtherHandMovesIt()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        bool LeftActive() => LiveHud.IsActiveHand(world, world.Screen[HandLeftId]);
        bool RightActive() => LiveHud.IsActiveHand(world, world.Screen[HandRightId]);
        Assert.True(LeftActive());
        Assert.False(RightActive());
        rig.Session.Keybinding(KeybindingCode.SwapHands); // X
        rig.Run(0.3);
        Assert.False(LeftActive());
        Assert.True(RightActive());
        ClickScreen(rig, HandLeftId, 16, 16); // what the left hand box reports: tg's hand box, whose Click swaps to it
        Assert.True(LeftActive());
        Assert.False(RightActive());
        ClickScreen(rig, SwapId, 16, 16); // tg's swap button
        Assert.True(RightActive());
    }

    [Fact]
    public void TheSwapChipAndTheCardsLinesAreTheBoundKeys()
    {
        KeyBindings bindings = KeyBindings.Defaults();
        Assert.Equal(new PromptLine("X", "Swap hand"), LiveHud.SwapLine(bindings));
        Assert.Equal(new[] { new PromptLine("Z", "Use"), new PromptLine("Q", "Drop"), new PromptLine("Ctrl+Q", "Throw") }, // swapping is the chip by the hands
            LiveHud.CardLines(bindings));
        bindings.Set(PlayAction.SwapHand, "V", "MouseMiddle");
        bindings.Set(PlayAction.DropItem); // unbound: its line is left out
        Assert.Equal(new PromptLine("V", "Swap hand"), LiveHud.SwapLine(bindings));
        Assert.Equal(new[] { "Z", "Ctrl+Q" }, LiveHud.CardLines(bindings).Select(l => l.Key));
        bindings.Set(PlayAction.SwapHand);
        Assert.Null(LiveHud.SwapLine(bindings));
        Assert.Equal(("Left hand", "Right hand"), (LiveHud.HandName(true), LiveHud.HandName(false)));
    }

    // 16:9, 16:10, 4:3, 5:4 and 21:9 screens at every HUD scale the options offer in steps of 5 (50 to 300 %)
    private static readonly (int W, int H)[] HandSizes =
    {
        (1280, 720), (1920, 1080), (2560, 1440), (1024, 600), (1024, 768), (1280, 1024), (1440, 900), (1920, 1200), (2560, 1080), (3440, 1440),
    };

    public static IEnumerable<object[]> HandScreens()
    {
        foreach ((int w, int h) in HandSizes)
        {
            for (int percent = 50; percent <= 300; percent += 5)
            {
                yield return new object[] { w, h, percent / 100f, true };
                yield return new object[] { w, h, percent / 100f, false };
            }
        }
    }

    // The chat window at its place in the round (GameScene.Chat.cs ChatRoundDefault) at the default chat scale (PlayOptions.ChatWindowScaleFor:
    // the height over 540), its least size (ChatWindow.MinSize) kept, as ChatWindow.Place puts it.
    private static HudRect DefaultChat(int w, int h)
    {
        float scale = h / 540f;
        float width = Math.Max(360f, 0.39f * w / scale) * scale, height = Math.Max(240f, 0.319f * h / scale) * scale;
        return new HudRect(Math.Clamp(0.006f * w, 0f, Math.Max(0f, w - width)), Math.Clamp(0.631f * h, 0f, Math.Max(0f, h - height)), width, height);
    }

    // tg's human HUD at the bottom (hud/human.dm, __DEFINES/hud.dm): the inventory toggle, the drop and swap buttons over the hands, and the
    // bottom-right menu with the zone selector, the move intent, pull, resist, throw, rest and sleep
    private static readonly string[] HumanBottom =
    {
        "WEST:6,SOUTH:5", // ui_inventory
        "CENTER-1:16,SOUTH+1:5", // ui_swaphand_position(1): drop
        "CENTER:16,SOUTH+1:5", // ui_swaphand_position(2): swap
        "EAST-4:22,SOUTH:5", // ui_human_floor_changer
        "EAST-4:22,SOUTH+1:7", // ui_human_memories_menu
        "EAST-3:7,SOUTH+1:7", // ui_human_navigate
        "EAST-3:7,SOUTH+1:24", // ui_human_language
        "EAST-3:24,SOUTH:5", // ui_acti (the combat toggle, drawn as the pill)
        "EAST-3:24, SOUTH+1:7", // ui_above_intent: resist
    };

    [Theory]
    [MemberData(nameof(HandScreens))]
    public void TheHandsTheBottomSlotsAndTheCardAreOnTheScreenAndClearOfEachOtherAndOfTgsHud(int width, int height, float scale, bool chatOpen)
    {
        var metrics = new HudMetrics(width, height, scale);
        var screen = new HudRect(0, 0, width, height);
        HudRect chat = DefaultChat(width, height);
        LiveHudLayout layout = LiveHud.Layout(metrics, card: true);
        var tg = RightColumn.Concat(HumanBottom).Select(loc => (loc, rect: ScreenLoc.Place(Parse(loc), metrics, 32, 32, 0, 0, out _))).ToList();
        float pulseTop = ScreenLoc.Place(Parse(RightColumn[0]), metrics, 32, 32, 0, 0, out _).Top;
        for (int slot = 1; slot <= 5; slot++)
            tg.Add(("alert " + slot, ScreenLoc.PlaceAlert(slot, 5, metrics, pulseTop)));
        // the chat window closed, at its place in the round, and widened from its left edge to every tenth of the screen up to all of it
        var chatRights = new List<float> { 0f };
        if (chatOpen)
        {
            chatRights.Add(chat.Right);
            for (int tenth = 1; tenth <= 10; tenth++)
                chatRights.Add(width * tenth / 10f);
        }
        foreach (float chatRight in chatRights)
        foreach (bool leftActive in new[] { true, false })
        {
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, !leftActive, chatRight);
            var window = new HudRect(chat.Left, chat.Top, Math.Max(0f, chatRight - chat.Left), chat.Height);
            var row = new List<(string Name, HudRect Rect)> { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar) };
            if (hands.SwapChip.Width > 0) // left out only where no clear place exists at this size
                row.Add(("swap chip", hands.SwapChip));
            var ours = row.Concat(new (string Name, HudRect Rect)[] { ("card", layout.Card), ("panel", layout.Panel), ("pill", layout.Pill) }).ToList();
            string at = $"{width}x{height} at {scale}, chat to {chatRight}, left {(leftActive ? "active" : "not active")}";
            foreach ((string name, HudRect rect) in ours)
            {
                Assert.True(rect.Width > 0 && rect.Height > 0 && Inside(rect, screen), $"{at}: {name} {rect}");
                foreach ((string loc, HudRect tgRect) in tg)
                    Assert.False(Overlaps(rect, tgRect), $"{at}: {name} {rect} over {loc} {tgRect}");
            }
            for (int i = 0; i < ours.Count; i++)
                for (int j = i + 1; j < ours.Count; j++)
                    Assert.False(Overlaps(ours[i].Rect, ours[j].Rect), $"{at}: {ours[i].Name} {ours[i].Rect} over {ours[j].Name} {ours[j].Rect}");
            // the row either stands wholly right of the chat window, clear of it, or exactly where it is with the chat window closed (the window,
            // if it reaches there, covers it as it covers tg's HUD)
            HandsLayout closed = LiveHud.Hands(metrics, leftActive, !leftActive);
            bool beside = row.All(r => window.Width <= 0 || !Overlaps(r.Rect, window)) && hands.Left.Left >= chatRight;
            Assert.True(beside || hands == closed, $"{at}: the row {hands.Left}..{hands.Right} is neither clear of the chat window {window} nor at its place without it");
            if (hands != closed)
                Assert.True(hands.Left.Left >= chatRight && !row.Any(r => Overlaps(r.Rect, window)), $"{at}: moved, yet over the chat window");
            // the active hand 72 reference pixels and the other 56, both on the gap's bottom line, the gap between them. Moved beside the chat window
            // the smaller box is never less than tg's own slot at its native size; at its own place it is never made smaller than the room between
            // tg's inventory toggle and its bottom-right menu, and under the chat lines, leaves it (a 4:3 or 5:4 screen at 230 % and more: tg's own
            // menu takes the bottom's width)
            float k = hands.Unit, u = UiTokens.Unit(metrics), margin = UiTokens.DollPanelGap * u;
            HudRect active = leftActive ? hands.Left : hands.Right, other = leftActive ? hands.Right : hands.Left;
            Assert.InRange(active.Width, UiTokens.HandActive * k - 1f, UiTokens.HandActive * k + 1f);
            Assert.InRange(other.Width, UiTokens.HandInactive * k - 1f, UiTokens.HandInactive * k + 1f);
            const float half = UiTokens.HotbarWidth / 2f + UiTokens.HandGap + UiTokens.HandActive;
            float across = (ScreenLoc.Place(Parse("EAST-4:22,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Left - ScreenLoc.Place(Parse("WEST:6,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Right - 2 * margin) / (2 * half * u);
            float up = (height * 0.2f - 20f * u - margin) / ((UiTokens.HotbarMargin + UiTokens.HandActive + UiTokens.SwapChipGap + UiTokens.ChipHeight) * u);
            float least = hands == closed ? Math.Min(UiTokens.HandMinPixels, UiTokens.HandInactive * u * Math.Clamp(Math.Min(across, up), 0.1f, 1f)) : UiTokens.HandMinPixels;
            Assert.True(other.Width >= least - 1f, $"{at}: the smaller hand box {other} under {least} pixels");
            Assert.Equal(hands.Hotbar.Bottom, hands.Left.Bottom);
            Assert.Equal(hands.Hotbar.Bottom, hands.Right.Bottom);
            Assert.True(hands.Left.Right < hands.Hotbar.Left && hands.Hotbar.Right < hands.Right.Left);
            Assert.InRange(hands.Hotbar.Width, UiTokens.HotbarWidth * k - 1f, UiTokens.HotbarWidth * k + 1f);
            // one to nine bottom slots: each a square wholly in the gap, a hotbar slot's size, left to right, never over each other (a gap between
            // them from a gap of one screen pixel up)
            for (int count = 1; count <= UiTokens.HotbarSlots; count++)
            {
                for (int i = 0; i < count; i++)
                {
                    HudRect cell = LiveHud.SlotCell(hands, i, count);
                    Assert.True(Inside(cell, hands.Hotbar), $"{at}: slot {i} of {count} {cell} out of the gap {hands.Hotbar}");
                    Assert.InRange(cell.Width, UiTokens.HotbarSlot * k - 1.5f, UiTokens.HotbarSlot * k + 1f);
                    Assert.Equal(cell.Width, cell.Height);
                    if (i > 0)
                    {
                        HudRect previous = LiveHud.SlotCell(hands, i - 1, count);
                        Assert.True(UiTokens.HotbarSlotGap * k >= 1f ? previous.Right < cell.Left : previous.Right <= cell.Left, $"{at}: slots {i - 1} {previous} and {i} {cell} of {count}");
                    }
                }
            }
            // the swap chip by the right hand, over it (it may stand higher, over tg's drop and swap buttons, or reach right of the box), the same
            // through a swap
            if (hands.SwapChip.Width > 0)
                Assert.True(hands.SwapChip.Bottom <= hands.Right.Top && hands.SwapChip.Left <= hands.Right.Right && hands.SwapChip.Right >= hands.Right.Left,
                    $"{at}: chip {hands.SwapChip} not by {hands.Right}");
            Assert.Equal(LiveHud.Hands(metrics, !leftActive, leftActive, chatRight).SwapChip, hands.SwapChip);
            Assert.Equal(LiveHud.Hands(metrics, !leftActive, leftActive, chatRight).Hotbar, hands.Hotbar);
        }
        // the card at the bottom right, in the doll's column under the pill, and the prompts end before it
        Assert.True(layout.Card.Top >= layout.Pill.Bottom && Math.Abs(layout.Card.Right - layout.Panel.Right) <= 1f, $"card {layout.Card} panel {layout.Panel}");
        Assert.True(layout.Card.Right > width / 2f); // at the right (on a narrow screen at a large scale it reaches left of the middle)
        Assert.InRange(layout.CardUnit, 0.25f * UiTokens.Unit(metrics) - 0.001f, UiTokens.Unit(metrics) + 0.001f);
        // the card does not push the doll under the version tag and the ping chip at the top, unless even the smallest card would not fit
        if (layout.CardUnit > 0.25f * UiTokens.Unit(metrics) + 0.001f)
            Assert.True(layout.Panel.Top >= UiTokens.CardTopRoom * UiTokens.Unit(metrics) - 1f, $"panel {layout.Panel} under the top room");
    }

    [Fact]
    public void AtTheReferenceSizeTheRowIsFullSizeAtTheBottomCentreAndKeepsItsPlacesThroughASwap()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        HandsLayout left = LiveHud.Hands(metrics, leftActive: true, rightActive: false), right = LiveHud.Hands(metrics, leftActive: false, rightActive: true);
        Assert.Equal(1f, left.Unit);
        Assert.Equal(new HudRect(960 - 232, 1080 - 16 - 48, 464, 48), left.Hotbar); // 9 x 48 + 8 x 4, centred, 16 over the bottom edge
        Assert.Equal(new HudRect(960 - 232 - 8 - 72, 1080 - 16 - 72, 72, 72), left.Left);
        Assert.Equal(new HudRect(960 + 232 + 8, 1080 - 16 - 56, 56, 56), left.Right);
        // a swap moves the highlight, not the gap
        Assert.Equal(left.Hotbar, right.Hotbar);
        Assert.Equal(new HudRect(960 - 232 - 8 - 56, 1080 - 16 - 56, 56, 56), right.Left);
        Assert.Equal(new HudRect(960 + 232 + 8, 1080 - 16 - 72, 72, 72), right.Right);
        // the chat window in the row's way moves it right only as far as it must, at full size while it fits
        HandsLayout beside = LiveHud.Hands(metrics, true, false, DefaultChat(1920, 1080).Right);
        Assert.Equal(1f, beside.Unit);
        Assert.Equal(DefaultChat(1920, 1080).Right + UiTokens.DollPanelGap, beside.Left.Left, 0);
        // the card keeps its room at the full size there and the doll stands where slice B put it
        LiveHudLayout layout = LiveHud.Layout(metrics, card: true);
        Assert.Equal(1f, layout.CardUnit);
        Assert.Equal(new HudRect(layout.Card.Left, layout.Card.Top, UiTokens.CardWidth, UiTokens.CardHeight), layout.Card);
        Assert.Equal(LiveHud.Layout(metrics).DollScale, layout.DollScale);
        Assert.Equal(default, LiveHud.Layout(metrics).Card); // no card room off the human HUD
    }

    [Theory]
    [InlineData(1920, 1080, 1f, true)]
    [InlineData(1280, 720, 1.5f, false)]
    [InlineData(1920, 1080, 2.5f, true)]
    public void AClickOnAHandBoxIsTgsHandAndOnTheHeldItemsPixelsTheItem(int width, int height, float scale, bool leftActive)
    {
        // what HudLayer.Collect reports for a hand: the box under the hand's id, and over it the picture square with the held item as the hand's
        // child (an item 16 x 16 in its icon's lower left, as ModelTests' window cell)
        const long itemId = 400123;
        HandsLayout hands = LiveHud.Hands(new HudMetrics(width, height, scale), leftActive, !leftActive);
        HudRect box = hands.Left, square = LiveHud.HandPicture(box, hands.Unit);
        Raster item = Raster.Solid(16, 16, 0xFFFFFFFF);
        var picker = new HudPicker(new StubSurface(new List<HudDrawn>
        {
            new(HandLeftId, box, null, 0, 0),
            new(HandLeftId, square, null, 0, 0, new[] { new HudChildDrawn(itemId, item, 0, 0) }),
            new(HandRightId, hands.Right, null, 0, 0),
        }));
        Assert.True(Inside(square, box));
        // the item's pixels where its picture is drawn: ChildPicture puts its 16 x 16 at the square's lower left quarter
        HudRect drawn = LiveHud.ChildPicture(square, 16, 16, 0, 0);
        Assert.Equal(square.Left, drawn.Left, 2);
        Assert.Equal(square.Bottom, drawn.Bottom, 2);
        Assert.Equal(square.Width / 2f, drawn.Width, 2);
        Assert.Equal(itemId, picker.PickScreen(drawn.Left + 1, drawn.Bottom - 1).Id);
        Assert.Equal(itemId, picker.PickScreen(drawn.Right - 1, drawn.Top + 1).Id);
        // the rest of the square and the box's frame are the hand box
        Assert.Equal(HandLeftId, picker.PickScreen(square.Right - 1, square.Top + 1).Id);
        Assert.Equal(HandLeftId, picker.PickScreen(box.Left + 1, box.Top + 1).Id);
        Assert.Equal(HandRightId, picker.PickScreen(hands.Right.Left + hands.Right.Width / 2f, hands.Right.Bottom - 1).Id);
        // between the boxes (the gap) and over the row is nothing of theirs
        Assert.False(picker.PickScreen(hands.Hotbar.Left + hands.Hotbar.Width / 2f, hands.Hotbar.Top + 1).IsHit);
        Assert.False(picker.PickScreen(box.Left + 1, box.Top - 2).IsHit);
    }

    [Fact]
    public void AHeldItemTooBigForTheBoxIsDrawnFittedWhereThePickHitsIt()
    {
        // DV575: a 64 x 64 picture drawn at half its size in the square, its top right quarter opaque
        var pixels = new byte[64 * 64 * 4];
        for (int row = 0; row < 32; row++)
            for (int col = 32; col < 64; col++)
                pixels[(row * 64 + col) * 4 + 3] = 255;
        var square = new HudRect(100, 200, 64, 64);
        HudRect drawn = LiveHud.ChildPicture(square, 64, 64, 16, 16);
        Assert.Equal(square, drawn);
        var picker = new HudPicker(new StubSurface(new List<HudDrawn>
        {
            new(HandRightId, square, null, 0, 0, new[] { new HudChildDrawn(400201, new Raster(64, 64, pixels), 16, 16, Fit: HudChildDrawn.FitOf(64, 64)) }),
        }));
        Assert.Equal(400201, picker.PickScreen(drawn.Right - 2, drawn.Top + 2).Id);
        Assert.Equal(HandRightId, picker.PickScreen(drawn.Left + 2, drawn.Bottom - 2).Id);
        // a holder moved by its pixel offset moves the picture with it, as the pick does
        HudRect shifted = LiveHud.ChildPicture(square, 32, 32, 0, 0, shiftX: 4, shiftY: 2);
        Assert.Equal(square.Left + 4 * 2, shifted.Left, 2);
        Assert.Equal(square.Bottom - 2 * 2, shifted.Bottom, 2);
    }

    [Fact]
    public void WithAThirdOrFourthHandActiveNeitherBoxIsHighlightedOrLarger()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        HandsLayout neither = LiveHud.Hands(metrics, leftActive: false, rightActive: false);
        Assert.Equal(UiTokens.HandInactive, neither.Left.Width);
        Assert.Equal(UiTokens.HandInactive, neither.Right.Width);
        Assert.Equal(LiveHud.Hands(metrics, true, false).Hotbar, neither.Hotbar); // the gap stays put
        // tg's active mark on a third hand (held_index 3, a left hand: lhandactive) makes it the active hand, and neither of the first two
        (_, ReplicaWorld world) = Start();
        ScreenEntry left = world.Screen[HandLeftId], right = world.Screen[HandRightId];
        Assert.True(LiveHud.IsActiveHand(world, left));
        Assert.True(world.TryGetAppearance(left.Appearance, out AppearanceEntry marked));
        AppearanceEntry unmarked = marked.WithOverlays(o => !(world.TryGetAppearance(o, out AppearanceEntry e) && world.ResolveString(e.IconState) == "lhandactive"));
        ScreenEntry third = left with { Id = 2099, HandIndex = 3 };
        Assert.True(LiveHud.IsActiveHand(world, third));
        Assert.False(LiveHud.IsActiveHand(world, left with { Appearance = world.LocalAppearance(unmarked) }));
        Assert.False(LiveHud.IsActiveHand(world, right));
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, third));
    }

    // The fake's world as an appearance source (the maptext collector's, as ReplicaTables is for the client).
    private sealed class WorldTables(ReplicaWorld world) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => world.TryGetAppearance(reference, out entry);

        public string ResolveString(TableRef reference) => world.ResolveString(reference);

        public string ResolveResource(TableRef reference) => world.ResolveResource(reference);
    }

    [Fact]
    public void AHandBoxDrawsTgsHandcuffsAndBlockedMarkOverItsItemAndShowsTheHeldStacksAmount()
    {
        // tg's hand box as the server sends it: the held item flattened as an overlay and listed as the box's child, the active mark, and
        // update_overlays' handcuff_overlay ("markus") and blocked_overlay ("blocked") (screen_objects.dm); the held stack carries its amount as
        // maptext (stack.dm update_overlays)
        (PlayRig rig, ReplicaWorld world) = Start();
        rig.Session.Click(new ClickTarget(TargetKind.Entity, ((FakeFeed)rig.Feed).Server.ToolboxId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        rig.Run(0.4);
        ScreenEntry hand = world.Screen[HandLeftId];
        ScreenChild toolbox = Assert.Single(hand.VisChildren.Items);
        Assert.True(world.TryGetAppearance(hand.Appearance, out AppearanceEntry look));
        TableRef cuffs = world.LocalAppearance(new AppearanceEntry { IconState = world.LocalString("markus") });
        TableRef blocked = world.LocalAppearance(new AppearanceEntry { IconState = world.LocalString("blocked") });
        AppearanceEntry withMarks = look.WithOverlays(_ => true);
        withMarks.Overlays.Add(cuffs);
        withMarks.Overlays.Add(blocked);
        ScreenEntry cuffed = hand with { Appearance = world.LocalAppearance(withMarks) };
        Assert.Equal(new[] { cuffs, blocked }, LiveHud.HandOverlays(world, cuffed)); // not the toolbox (drawn as the child), not the active mark
        Assert.Empty(LiveHud.HandOverlays(world, hand));
        Assert.Empty(LiveHud.HandOverlays(world, world.Screen[HandRightId])); // an empty hand, not active
        // a held item the box does not list as a child (mouse_opacity 0) is drawn from the box's overlays
        Assert.Equal(new[] { toolbox.Appearance }, LiveHud.HandOverlays(world, hand with { VisChildren = null }));

        // a stack of 30: its amount is a maptext of the item, which the box's picture carries, so the box shows it where tg's slot does
        var stackLook = new AppearanceEntry { IconState = world.LocalString("sheet-metal"), Maptext = world.LocalString("30") };
        TableRef stack = world.LocalAppearance(stackLook);
        AppearanceEntry holding = look.WithOverlays(o => !o.Equals(toolbox.Appearance));
        holding.Overlays.Add(stack);
        ScreenEntry stacked = hand with { Appearance = world.LocalAppearance(holding), VisChildren = new ScreenChildren { Items = new[] { new ScreenChild(toolbox.Id, stack) } } };
        Anomaly.Client.Shared.Maptext.MaptextItem text = Assert.Single(new Anomaly.Client.Shared.Maptext.MaptextCollector().Collect(new WorldTables(world), stacked.Appearance));
        Assert.Equal("30", text.Text);
        Assert.Equal(stack, text.Source);
        Assert.Empty(LiveHud.HandOverlays(world, stacked)); // the stack's picture is the child's, its amount the maptext's
    }

    [Theory]
    [InlineData(1920, 1080, 1f)]
    [InlineData(1920, 1080, 2.5f)]
    [InlineData(1280, 720, 1.5f)]
    public void AHandsOrAMovedBottomSlotsMaptextStandsOnItsSquareThereAndScalesWithIt(int width, int height, float scale)
    {
        // a stack's amount (tg's maptext at the icon's lower left) on an item in a hand or in a moved bottom slot: on that box's square, at that
        // square's size (32 tg pixels across it), not at the HUD's tg scale of the entry's own place
        var metrics = new HudMetrics(width, height, scale);
        HandsLayout hands = LiveHud.Hands(metrics, true, false);
        Assert.Equal(LiveHud.HandPicture(hands.Left, hands.Unit), LiveHud.MaptextSquare(LiveHudRole.LeftHand, hands, 0, 6));
        Assert.Equal(LiveHud.HandPicture(hands.Right, hands.Unit), LiveHud.MaptextSquare(LiveHudRole.RightHand, hands, 0, 6));
        for (int cell = 0; cell < 6; cell++)
        {
            HudRect slot = LiveHud.SlotCell(hands, cell, 6);
            Assert.Equal(slot, LiveHud.MaptextSquare(LiveHudRole.BottomSlot, hands, cell, 6));
            // the slot's tg picture is fitted to that cell (HudLayer.Place), so its icon square is the cell
            Assert.Equal(slot, ScreenLoc.FitIcon(slot, 32, 32, 0, 0));
            Assert.True(slot.Width / 32f < metrics.PixelScale, "the cell's scale is not the HUD's tg scale, which made the amount too large");
        }
        // an entry at its own place (or a slot of another HUD, which LiveHud.Placed keeps there) keeps the HUD's own square and scale
        foreach (LiveHudRole role in new[] { LiveHudRole.Tg, LiveHudRole.Doll, LiveHudRole.CombatToggle, LiveHudRole.ZoneSelector, LiveHudRole.InventoryToggle })
            Assert.Null(LiveHud.MaptextSquare(role, hands, 0, 6));
        Assert.Null(LiveHud.MaptextSquare(LiveHud.Placed(LiveHudRole.BottomSlot, humanHud: false), hands, 0, 6));
    }
}
