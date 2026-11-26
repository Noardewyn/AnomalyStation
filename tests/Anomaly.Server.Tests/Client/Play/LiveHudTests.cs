using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 6, slice B: which screen entries the live HUD re-draws (the doll, the combat toggle) or hides (tg's zone selector in the
/// captured view), combat mode read off the replica, the doll panel and the combat pill placed clear of tg's right-hand column at every HUD
/// scale, and clicks on the re-placed doll and on the pill reaching tg's entries with tg's icon pixels. Slice C: the hands known by hand index and
/// the bottom slots by slot id on the human HUD only, the active hand and its swaps, the hands' row and the bottom slots clear of each other and of
/// tg's HUD and the chat window, and a click on a hand box reaching tg's hand box or the held item. Slice D: the vitals column, the alert chips and
/// the hint strip.
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
        foreach (long id in new[] { IntentId, ThrowId, FirstSlotId })
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, world.Screen[id]));
        Assert.Equal(LiveHudRole.Health, LiveHud.RoleOf(world, world.Screen[HealthId])); // slice D: tg's health indicator, at ui_health
        // every other entry of the fake's HUD stays at tg's place, but slice C's: the two hands, the six bottom slots and the inventory toggle,
        // and slice D's health indicator
        Assert.Equal(3 + 2 + 6 + 1 + 1, world.Screen.Values.Count(e => LiveHud.RoleOf(world, e) != LiveHudRole.Tg));
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

    // ---- slice C: the hands and the bottom slots in the hotbar's gap ----

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
        // the alien's, a basic mob's and the other HUDs have no inventory toggle: their hands and slots keep tg's places, their alerts tg's column
        foreach (LiveHudRole role in new[] { LiveHudRole.LeftHand, LiveHudRole.RightHand, LiveHudRole.BottomSlot, LiveHudRole.Alert })
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
    public void TheSwapKeyAndTheHintStripsEntriesAreTheBoundKeys()
    {
        KeyBindings bindings = KeyBindings.Defaults();
        Assert.Equal("X", LiveHud.SwapKey(bindings));
        Assert.Equal(new[] { new PromptLine("Z", "Use"), new PromptLine("Q", "Drop"), new PromptLine("Ctrl+Q", "Throw") }, // swapping is the cap on the other hand
            LiveHud.HintLines(bindings));
        bindings.Set(PlayAction.SwapHand, "V", "MouseMiddle");
        bindings.Set(PlayAction.DropItem); // unbound: its line is left out
        Assert.Equal("V", LiveHud.SwapKey(bindings));
        Assert.Equal(new[] { "Z", "Ctrl+Q" }, LiveHud.HintLines(bindings).Select(l => l.Key));
        bindings.Set(PlayAction.SwapHand);
        Assert.Null(LiveHud.SwapKey(bindings));
    }

    // 16:9, 16:10, 4:3, 5:4 and 21:9 screens at every HUD scale the options offer in steps of 5 (50 to 300 %)
    private static readonly (int W, int H)[] HandSizes =
    {
        (1280, 720), (1920, 1080), (2560, 1440), (1024, 600), (1024, 768), (1280, 1024), (1440, 900), (1920, 1200), (2560, 1080), (3440, 1440), (3840, 2160),
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

    // tg's human HUD entries at the bottom right that stay at tg's places (hud/human.dm): the zone selector (in cursor mode), throw, sleep, the move
    // intent, rest and pull
    private static readonly string[] HumanBottomRight =
    {
        "EAST-1:28,SOUTH:5", // ui_zonesel
        "EAST-1:28,SOUTH+1:24", // ui_drop_throw
        "EAST-1:28,SOUTH+1:41", // ui_above_throw: sleep
        "EAST-2:26,SOUTH:5", // ui_movi
        "EAST-2:26,SOUTH+1:7", // ui_above_movement: rest
        "EAST-2:26, SOUTH+1:24", // ui_above_movement_top: pull
    };

    // tg's right-column status icons of the human HUD: at tg's places with the reduced column, in the vitals panel or chips with the full one
    private static readonly string[] HumanStatus =
    {
        "EAST-1:28,CENTER-1:19", // ui_health
        "EAST-1:28,CENTER:21", // ui_mood
        "EAST-1:2,CENTER:21", // ui_hunger
        "EAST-1:28,CENTER-3:14", // ui_stamina
        "EAST-1:28,CENTER+1:21", // ui_internal
        "EAST-1:28,CENTER-4:14", // ui_spacesuit
    };

    [Theory]
    [MemberData(nameof(HandScreens))]
    public void TheHandsTheBottomSlotsTheColumnAndTheStripAreOnTheScreenAndClearOfEachOtherAndOfTgsHud(int width, int height, float scale, bool chatOpen)
    {
        var metrics = new HudMetrics(width, height, scale);
        var screen = new HudRect(0, 0, width, height);
        HudRect chat = DefaultChat(width, height);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        // slice D: the arrangement depends on the screen and the scale alone
        Assert.Equal(layout, LiveHud.Layout(new HudMetrics(width, height, scale), human: true));
        Assert.Equal(LiveHud.VitalsColumn(metrics).HasValue, layout.Vitals);
        // tg's human HUD that stays at tg's places: the bottom row, the bottom right, and with the reduced column the status icons (tg's alerts are
        // chips on the human HUD, its doll is in our panel)
        var tg = HumanBottom.Concat(HumanBottomRight).Concat(layout.Vitals ? Array.Empty<string>() : HumanStatus)
            .Select(loc => (loc, rect: ScreenLoc.Place(Parse(loc), metrics, 32, 32, 0, 0, out _))).ToList();
        // the chat window closed, at its place in the round, and widened from its left edge to every tenth of the screen up to all of it
        var chatRights = new List<float> { 0f };
        if (chatOpen)
        {
            chatRights.Add(chat.Right);
            for (int tenth = 1; tenth <= 10; tenth++)
                chatRights.Add(width * tenth / 10f);
        }
        float u = UiTokens.Unit(metrics);
        List<HudRect> chips = LiveHud.AlertStack(metrics, 8, layout);
        // a hint strip of about the natural width of the default keys' entries (Z Use, Q Drop, Ctrl+Q Throw: about 300 reference pixels)
        float hint = 300f * u;
        HudRect prompt = KeyChips.PromptBlock(metrics, KeyChips.MaxPromptLines, UiTokens.PromptMaxWidth * u, width / 2f, height / 2f,
            KeyChips.SightReach(false, 0f, 70f, height, 1f), Math.Min(LiveHud.LeftOfPanel(layout, metrics), LiveHud.LeftOfAlerts(chips, metrics)));
        foreach (float chatRight in chatRights)
        foreach (bool leftActive in new[] { true, false })
        {
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, !leftActive, chatRight, layout, hint);
            var window = new HudRect(chat.Left, chat.Top, Math.Max(0f, chatRight - chat.Left), chat.Height);
            var row = new List<(string Name, HudRect Rect)> { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar) };
            var ours = row.Concat(new (string Name, HudRect Rect)[] { ("panel", layout.Panel), ("pill", layout.Pill) }).ToList();
            if (hands.Hint.Width > 0)
                ours.Add(("hint strip", hands.Hint));
            string at = $"{width}x{height} at {scale}, chat to {chatRight}, left {(leftActive ? "active" : "not active")}, {(layout.Vitals ? "full" : "reduced")}";
            foreach ((string name, HudRect rect) in ours)
            {
                Assert.True(rect.Width > 0 && rect.Height > 0 && Inside(rect, screen), $"{at}: {name} {rect}");
                foreach ((string loc, HudRect tgRect) in tg)
                {
                    // the hint strip alone sits on tg's drop and swap buttons: it keeps clear of their visible part, not their whole squares
                    HudRect against = name == "hint strip" && loc.StartsWith("CENTER", StringComparison.Ordinal) ? LiveHud.VisibleButton(Parse(loc), metrics) : tgRect;
                    Assert.False(Overlaps(rect, against), $"{at}: {name} {rect} over {loc} {against}");
                }
            }
            for (int i = 0; i < ours.Count; i++)
                for (int j = i + 1; j < ours.Count; j++)
                    Assert.False(Overlaps(ours[i].Rect, ours[j].Rect), $"{at}: {ours[i].Name} {ours[i].Rect} over {ours[j].Name} {ours[j].Rect}");
            // the row either stands wholly right of the chat window, clear of it, or exactly where it is with the chat window closed (the window,
            // if it reaches there, covers it as it covers tg's HUD)
            HandsLayout closed = LiveHud.Hands(metrics, leftActive, !leftActive);
            bool beside = row.All(r => window.Width <= 0 || !Overlaps(r.Rect, window)) && hands.Left.Left >= chatRight;
            Assert.True(beside || hands with { Hint = default, HintShare = 0f } == closed, $"{at}: the row {hands.Left}..{hands.Right} is neither clear of the chat window {window} nor at its place without it");
            if (hands with { Hint = default, HintShare = 0f } != closed)
                Assert.True(hands.Left.Left >= chatRight && !row.Any(r => Overlaps(r.Rect, window)), $"{at}: moved, yet over the chat window");
            // slice D: the hint strip never moves the row, is the same for either hand active, and where it shows it is clear of the chips, the prompt
            // block, and the chat window beside the row
            Assert.Equal(LiveHud.Hands(metrics, leftActive, !leftActive, chatRight), hands with { Hint = default, HintShare = 0f });
            Assert.Equal(LiveHud.Hands(metrics, !leftActive, leftActive, chatRight, layout, hint).Hint, hands.Hint);
            if (hands.Hint.Width > 0)
            {
                foreach (HudRect chip in chips)
                    Assert.False(Overlaps(hands.Hint, chip), $"{at}: hint strip {hands.Hint} over chip {chip}");
                Assert.False(Overlaps(hands.Hint, prompt), $"{at}: hint strip {hands.Hint} over the prompt block {prompt}");
                Assert.False(hands with { Hint = default, HintShare = 0f } != closed && Overlaps(hands.Hint, window), $"{at}: hint strip {hands.Hint} over the chat window {window}");
                // over the row, centred on the gap, no wider than the row with both boxes at the active size, its text at 60 % of the prompts' at least
                float activeSide = MathF.Round(UiTokens.HandActive * hands.Unit);
                float buttons = Math.Min(LiveHud.VisibleButton(Parse("CENTER-1:16,SOUTH+1:5"), metrics).Top, LiveHud.VisibleButton(Parse("CENTER:16,SOUTH+1:5"), metrics).Top);
                // the gap over the higher of the row's top line and what shows of tg's buttons, at the whole pixel above it
                Assert.Equal(MathF.Floor(Math.Min(hands.Hotbar.Bottom - activeSide, buttons)) - MathF.Round(UiTokens.PillGap * u), hands.Hint.Bottom);
                // the strip and its picture at whole pixels
                foreach (HudRect r in new[] { hands.Hint, LiveHud.HintPictureSquare(hands.Hint, u, hands.HintShare) })
                    Assert.True(r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height), $"{at}: hint {r} not at whole pixels");
                Assert.InRange(hands.Hint.Left + hands.Hint.Width / 2f - (hands.Hotbar.Left + hands.Hotbar.Width / 2f), -1f, 1f);
                Assert.True(hands.Hint.Width <= hands.Hotbar.Width + 2 * (MathF.Round(UiTokens.HandGap * hands.Unit) + activeSide) + 0.01f);
                Assert.True(hands.HintShare * UiTokens.HintShare >= UiTokens.HintLeastShare - 0.001f);
            }
            // the active hand 72 reference pixels and the other 56, both on the gap's bottom line, the gap between them. Moved beside the chat window
            // the smaller box is never less than tg's own slot at its native size; at its own place it is never made smaller than the room between
            // tg's inventory toggle and its bottom-right menu, and under the chat lines, leaves it (a 4:3 or 5:4 screen at 230 % and more: tg's own
            // menu takes the bottom's width)
            float k = hands.Unit, margin = UiTokens.DollPanelGap * u;
            HudRect active = leftActive ? hands.Left : hands.Right, other = leftActive ? hands.Right : hands.Left;
            Assert.InRange(active.Width, UiTokens.HandActive * k - 1f, UiTokens.HandActive * k + 1f);
            Assert.InRange(other.Width, UiTokens.HandInactive * k - 1f, UiTokens.HandInactive * k + 1f);
            const float half = UiTokens.HotbarWidth / 2f + UiTokens.HandGap + UiTokens.HandActive;
            float across = (ScreenLoc.Place(Parse("EAST-4:22,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Left - ScreenLoc.Place(Parse("WEST:6,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Right - 2 * margin) / (2 * half * u);
            float up = (height * 0.2f - 20f * u - margin) / ((UiTokens.HotbarMargin + UiTokens.HandActive) * u);
            float least = hands with { Hint = default, HintShare = 0f } == closed ? Math.Min(UiTokens.HandMinPixels, UiTokens.HandInactive * u * Math.Clamp(Math.Min(across, up), 0.1f, 1f)) : UiTokens.HandMinPixels;
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
            // the swap key's cap: inside the inactive hand's box at its top outer corner, never on the active one, a short or a long label; none
            // with neither of the two active
            float inset = Math.Max(1f, MathF.Round(UiTokens.SwapCapInset * k));
            foreach (float label in new[] { UiTokens.SwapCapHeight * k, 60f * k, 10000f })
            {
                HudRect cap = LiveHud.SwapCap(hands, leftActive, !leftActive, label);
                Assert.True(cap.Width > 0 && cap.Height > 0 && Inside(cap, other) && !Overlaps(cap, active), $"{at}: cap {cap} in {other}, active {active}");
                Assert.Equal(other.Top + inset, cap.Top);
                Assert.Equal(leftActive ? other.Right - inset : other.Left + inset, leftActive ? cap.Right : cap.Left);
            }
            Assert.Equal(default, LiveHud.SwapCap(LiveHud.Hands(metrics, false, false, chatRight), false, false, 40f));
            Assert.Equal(LiveHud.Hands(metrics, !leftActive, leftActive, chatRight).Hotbar, hands.Hotbar);
        }
        // slice D: the column fits under the version tag and the ping chip, and the full one stands against the screen's right edge, the pill over
        // tg's sleep button, the panel's four cells inside it in order
        // (the panel may climb into that room only where the pill is already at its smallest, a quarter: the reduced column at the largest scales)
        bool smallestPill = layout.Pill.Height <= MathF.Round(UiTokens.PillHeight * u * 0.25f);
        Assert.True(layout.Panel.Top >= UiTokens.CardTopRoom * u - 1f || smallestPill, $"{width}x{height} at {scale}: panel {layout.Panel} under the top room, pill {layout.Pill}");
        Assert.True(layout.Pill.Top >= layout.Panel.Bottom && layout.Panel.Width == layout.Pill.Width && layout.Panel.Left == layout.Pill.Left);
        if (layout.Vitals)
        {
            Assert.Equal(MathF.Round(width - UiTokens.VitalsMargin * u), layout.Panel.Right);
            Assert.Equal(MathF.Floor(ScreenLoc.Place(Parse("EAST-1:28,SOUTH+1:41"), metrics, 32, 32, 0, 0, out _).Top - MathF.Round(UiTokens.VitalsMargin * u)), layout.Pill.Bottom);
            HudRect[] cells = { layout.Doll, layout.Health, layout.Mood };
            // every rectangle of the column at whole pixels (tg's sleep button can stand at a fraction of one: 843.75 at 1920x1080)
            foreach (HudRect r in new[] { layout.Panel, layout.Pill, layout.Doll, layout.Health, layout.Mood,
                         LiveHud.VitalSquare(LiveHudRole.Hunger, layout).Value, LiveHud.VitalSquare(LiveHudRole.Mood, layout).Value, LiveHud.VitalSquare(LiveHudRole.Mood, layout, false).Value })
                Assert.True(r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height), $"{width}x{height} at {scale}: {r} not at whole pixels");
            for (int i = 0; i < cells.Length; i++)
            {
                Assert.True(Inside(cells[i], layout.Panel), $"{width}x{height} at {scale}: cell {i} {cells[i]} out of the panel {layout.Panel}");
                if (i > 0)
                    Assert.True(cells[i - 1].Bottom < cells[i].Top, $"{width}x{height} at {scale}: cells {i - 1} and {i}");
            }
            // the hunger and mood pair and the mood face alone inside the panel too (the pair made smaller until it fits its width)
            foreach (bool hunger in new[] { true, false })
                foreach (LiveHudRole role in new[] { LiveHudRole.Hunger, LiveHudRole.Mood })
                    Assert.True(Inside(LiveHud.VitalSquare(role, layout, hunger).Value, layout.Panel), $"{width}x{height} at {scale}: {role} {LiveHud.VitalSquare(role, layout, hunger)} out of the panel {layout.Panel}");
        }
        else
            Assert.True(layout.Panel.Right <= width - 2 * metrics.Tile, $"{width}x{height} at {scale}: the reduced panel {layout.Panel} not left of tg's right column");
        if (!chatOpen) // the alert chips do not depend on the chat window, and tg's alert column is not drawn on the human HUD
            AlertStackIsClear(metrics, layout, tg, $"{width}x{height} at {scale}");
    }

    // ---- slice D: tg's alerts as chips by the doll panel, above it or left of it ----

    // Zero to eight chips (tg's five alerts, then the stamina, internals and suit cell chips), in the place this screen and scale gives (above the doll
    // when five full-size chips fit there, else left of the column), the same place for one alert and for five: all there, square, on the
    // screen, clear of each other, of the version tag and the ping and FPS chip (the whole top band, as wide as the tag gets at a large scale), of
    // the doll panel and the pill, of the hands' row with either hand active, of tg's entries at tg's places, and of the widest prompt block beside
    // the crosshair, which ends before them; at full size above the doll, and left of it at full size while five fit, never under 24 screen pixels.
    private static void AlertStackIsClear(HudMetrics metrics, LiveHudLayout layout, List<(string loc, HudRect rect)> tg, string at)
    {
        float u = UiTokens.Unit(metrics), full = MathF.Round(UiTokens.AlertChip * u), stackGap = MathF.Round(UiTokens.AlertStackGap * u);
        var screen = new HudRect(0, 0, metrics.Width, metrics.Height);
        var others = new List<(string Name, HudRect Rect)>
        {
            ("version tag and ping chip", new HudRect(0, 0, metrics.Width, (UiTokens.CardTopRoom - UiTokens.ScreenMargin) * u)),
            ("panel", layout.Panel), ("pill", layout.Pill),
        };
        foreach (bool leftActive in new[] { true, false })
        {
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, !leftActive);
            others.AddRange(new[] { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar) });
        }
        others.AddRange(tg.Select(t => (t.loc, t.rect)));
        bool above = LiveHud.AlertsAbove(metrics, layout);
        // left of the column: the panel and the pill, one width
        float columnRight = Math.Min(layout.Panel.Left, layout.Pill.Left) - stackGap;
        float least = Math.Min(full, UiTokens.AlertChipMinPixels);
        List<HudRect> eight = LiveHud.AlertStack(metrics, 8, layout);
        for (int count = 0; count <= 8; count++)
        {
            List<HudRect> chips = LiveHud.AlertStack(metrics, count, layout);
            string where = $"{at}, {count} chips, {(above ? "above" : "left")}";
            Assert.Equal(count, chips.Count);
            // the widest prompt block beside the crosshair (three prompts and the stop-pulling line), cut before the panel and the chips
            float edge = Math.Min(LiveHud.LeftOfPanel(layout, metrics), LiveHud.LeftOfAlerts(chips, metrics));
            HudRect prompt = KeyChips.PromptBlock(metrics, 4, UiTokens.PromptMaxWidth * u, metrics.Width / 2f, metrics.Height / 2f, KeyChips.SightReach(false, 0f, 70f, metrics.Height, 1f), edge);
            for (int i = 0; i < count; i++)
            {
                HudRect chip = chips[i];
                Assert.True(Inside(chip, screen) && chip.Width == chip.Height && chip.Width >= least - 1f && chip.Width <= full, $"{where}: chip {i} {chip}");
                foreach ((string name, HudRect rect) in others.Append(("prompt block", prompt)))
                    Assert.False(rect.Width > 0 && Overlaps(chip, rect), $"{where}: chip {i} {chip} over {name} {rect}");
                for (int j = i + 1; j < count; j++)
                    Assert.False(Overlaps(chip, chips[j]), $"{where}: chips {i} {chip} and {j} {chips[j]}");
                if (above)
                {
                    // full size, right-aligned with the panel or in a column left of that, over the panel
                    Assert.Equal(full, chip.Width);
                    Assert.True(chip.Right <= layout.Panel.Right && chip.Bottom <= layout.Panel.Top - stackGap, $"{where}: chip {i} {chip} not over the panel {layout.Panel}");
                }
                else
                {
                    // the places of the eight, whatever the count: the first column's right edge the gap left of the column, its top level with the
                    // panel's, tg's order down the column, then the next column left of it
                    Assert.Equal(eight[i], chip);
                    Assert.True(chip.Right <= columnRight, $"{where}: chip {i} {chip} not left of the column {columnRight}");
                }
                // in both places tg's order down a column, then on in the next column to the left
                if (i > 0)
                    Assert.True(chips[i - 1].Right == chip.Right ? chips[i - 1].Bottom < chip.Top : chips[i - 1].Left > chip.Right, $"{where}: chip {i - 1} {chips[i - 1]} then {i} {chip}");
            }
            if (count == 0)
                continue;
            if (above)
            {
                // the first chips in the column at the panel's right edge, its last the gap over the panel
                Assert.Equal(layout.Panel.Right, chips[0].Right);
                Assert.Equal(new HudRect(layout.Panel.Right - full, layout.Panel.Top - stackGap - full, full, full), chips.Last(c => c.Right == layout.Panel.Right));
                if (count <= 5)
                    Assert.All(chips, c => Assert.Equal(layout.Panel.Right, c.Right));
            }
            else
            {
                Assert.Equal(columnRight, chips[0].Right);
                Assert.Equal(Math.Max(layout.Panel.Top, MathF.Ceiling(UiTokens.CardTopRoom * u)), chips[0].Top);
            }
        }
        // the place is the same for one alert and for five
        List<HudRect> one = LiveHud.AlertStack(metrics, 1, layout), five = LiveHud.AlertStack(metrics, 5, layout);
        if (above)
            Assert.True(one[0] == five[4], $"{at}: one and five");
        else
            Assert.Equal(five[0], one[0]);
    }


    [Fact]
    public void AnAlertWithAForeignPictureAtTgsAlertPlaceIsAnAlertToo()
    {
        // tg's tracker or blood worm alerts draw from their own icon files; at one of tg's five alert places (ui_alert1 to ui_alert5) in no HUD
        // group they are alerts all the same, so none is left behind in tg's column
        (_, ReplicaWorld world) = Start();
        TableRef tracker = world.LocalAppearance(new AppearanceEntry { Icon = world.LocalString("icons/obj/devices/tracker.dmi"), IconState = world.LocalString("pinonfar") });
        for (int i = 1; i <= 5; i++)
        {
            var entry = new ScreenEntry(9000 + i, tracker, $"EAST-1:28,CENTER+{6 - i}:{29 - 2 * i}");
            Assert.Equal(LiveHudRole.Alert, LiveHud.RoleOf(world, entry));
            // in a HUD group, a slot, a hand or an action button it is not (in tg's info group, there, another status display)
            Assert.Equal(LiveHudRole.StatusOther, LiveHud.RoleOf(world, entry with { Group = HudGroup.Info }));
            Assert.Equal(LiveHudRole.BottomSlot, LiveHud.RoleOf(world, entry with { SlotId = 7 }));
            Assert.NotEqual(LiveHudRole.Alert, LiveHud.RoleOf(world, entry with { HandIndex = 3 }));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { ActionTarget = 4 }));
        }
        // the same picture anywhere else is not an alert: tg's right column next to the alerts, a sixth place, the top left
        foreach (string loc in new[] { "EAST-1:28,CENTER-2:17", "EAST-1:28,CENTER+0:19", "EAST-1:28,CENTER+6:17", "WEST,NORTH", "" })
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, new ScreenEntry(9100, tracker, loc)));
    }

    [Fact]
    public void TgsAlertsAreKnownByIconFileOrHudStyleTemplateNeverByPlace()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        foreach (string name in FakeServer.AlertNames)
            rig.Session.DevCommand("alert me " + name);
        rig.Run(0.4);
        for (int i = 0; i < FakeServer.AlertNames.Length; i++)
        {
            ScreenEntry alert = world.Screen[FakeServer.AlertIdBase + i];
            Assert.Equal(LiveHudRole.Alert, LiveHud.RoleOf(world, alert));
            // the place says nothing: at the top left, in a hand's place, or with no place at all it is still an alert
            foreach (string loc in new[] { "WEST,NORTH", "CENTER+-1:16,SOUTH+0:5", "" })
                Assert.Equal(LiveHudRole.Alert, LiveHud.RoleOf(world, alert with { ScreenLoc = loc }));
            // in a HUD group, a native window or an action button it is not (in tg's info group at its place in the right status column it is another status display)
            foreach (byte group in new[] { HudGroup.Static, HudGroup.Info, HudGroup.Storage, HudGroup.ToggleableInventory })
                Assert.Equal(group == HudGroup.Info && LiveHud.InStatusColumn(Parse(alert.ScreenLoc)) ? LiveHudRole.StatusOther : LiveHudRole.Tg, LiveHud.RoleOf(world, alert with { Group = group }));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, alert with { ActionTarget = 9 }));
        }
        // weightless is drawn in the HUD's style (tg's USER_HUD_STYLE_INHERIT): the style's "template" with the alert as an overlay
        Assert.True(world.TryGetAppearance(world.Screen[FakeServer.AlertIdBase + 2].Appearance, out AppearanceEntry weightless));
        Assert.Equal("icons/hud/screen_midnight.dmi", world.ResolveResource(weightless.Icon));
        Assert.Equal("template", world.ResolveString(weightless.IconState));
        // a full slot of tg's inventory shows the style's "template" too, and stays a slot
        ScreenEntry belt = world.Screen[BeltSlotId] with { Appearance = world.Screen[FakeServer.AlertIdBase + 2].Appearance };
        Assert.Equal(LiveHudRole.BottomSlot, LiveHud.RoleOf(world, belt));
        // nothing else of the fake's HUD is an alert
        Assert.Equal(FakeServer.AlertNames.Length, world.Screen.Values.Count(e => LiveHud.RoleOf(world, e) == LiveHudRole.Alert));
    }

    [Fact]
    public void TheChipsKeepTgsOrderAlertOneFirst()
    {
        (PlayRig rig, ReplicaWorld world) = Start();
        foreach (string name in new[] { "cold", "buckled", "not_enough_oxy" })
            rig.Session.DevCommand("alert me " + name);
        rig.Run(0.4);
        long[] InTgsOrder() => world.Screen.Values.Where(e => LiveHud.RoleOf(world, e) == LiveHudRole.Alert)
            .OrderBy(e => LiveHud.AlertOrder(Parse(e.ScreenLoc))).Select(e => e.Id).ToArray();
        long Id(string name) => FakeServer.AlertIdBase + Array.IndexOf(FakeServer.AlertNames, name);
        Assert.Equal(new[] { Id("cold"), Id("buckled"), Id("not_enough_oxy") }, InTgsOrder());
        rig.Session.DevCommand("alert me cold"); // the first goes: tg moves the others up its column
        rig.Run(0.4);
        Assert.Equal(new[] { Id("buckled"), Id("not_enough_oxy") }, InTgsOrder());
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, LiveHud.AlertOrder(Parse($"EAST-1:28,CENTER+{6 - i}:{29 - 2 * i}")));
        Assert.Equal(int.MaxValue, LiveHud.AlertOrder(Parse("WEST,NORTH")));
    }

    [Fact]
    public void AtTheReferenceSizeTheChipsStackOverTheDollRightAlignedTheNewestNearest()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        float right = layout.Panel.Right, bottom = layout.Panel.Top - 6;
        Assert.True(LiveHud.AlertsAbove(metrics, layout)); // five fit above the doll here
        // alert 3 (the newest) on the panel, alert 2 over it, alert 1 at the top: 40 pixels, 4 apart
        List<HudRect> chips = LiveHud.AlertStack(metrics, 3, layout);
        Assert.Equal(new[]
        {
            new HudRect(right - 40, bottom - 128, 40, 40), new HudRect(right - 40, bottom - 84, 40, 40), new HudRect(right - 40, bottom - 40, 40, 40),
        }, chips);
        // a fourth alert goes on the panel and pushes the others up one place each
        List<HudRect> four = LiveHud.AlertStack(metrics, 4, layout);
        Assert.Equal(new HudRect(right - 40, bottom - 40, 40, 40), four[3]);
        for (int i = 0; i < 3; i++)
            Assert.Equal(chips[i] with { Top = chips[i].Top - 44 }, four[i]);
        Assert.Empty(LiveHud.AlertStack(metrics, 0, layout));
        // the picture square inside a chip: tg's 32 x 32 icon at one screen pixel per tg pixel, centred
        Assert.Equal(new HudRect(right - 36, bottom - 36, 32, 32), LiveHud.AlertPicture(four[3]));
        Assert.Equal(new HudRect(10, 10, 20, 20), LiveHud.AlertPicture(new HudRect(8, 8, 24, 24))); // a smaller chip, a smaller inset
    }

    [Fact]
    public void WithoutRoomAboveTheChipsGoLeftOfTheDollShrinkingThenInMoreColumns()
    {
        // at 250% on 1920x1080: a chip is 100 pixels, the top room 215; tg's bottom-right rows start at 585, so the column ends at 570
        var metrics = new HudMetrics(1920, 1080, 2.5f);
        LiveHudLayout At(float panelTop) => new(new HudRect(1400, panelTop, 375, 290), default, default, 7);
        Assert.True(LiveHud.AlertsAbove(metrics, At(215 + 15 + 540)));
        Assert.False(LiveHud.AlertsAbove(metrics, At(215 + 15 + 539)));
        // the panel at the top room: left of it, its top level with the panel's, 15 pixels from its edge; five full chips (540) do not fit in 355,
        // so all are made smaller together, to 65, still one column
        List<HudRect> shrunk = LiveHud.AlertStack(metrics, 5, At(215));
        Assert.All(shrunk, c => Assert.Equal(65f, c.Width));
        Assert.All(shrunk, c => Assert.Equal(1385f, c.Right));
        Assert.Equal(215f, shrunk[0].Top);
        Assert.True(shrunk[4].Bottom <= 570f);
        Assert.Equal(shrunk.Take(2), LiveHud.AlertStack(metrics, 2, At(215))); // two alerts take the first two of those places
        // the panel lower, 90 pixels left down to the rows: chips of 24, three to a column, then a second column left of the first
        List<HudRect> two = LiveHud.AlertStack(metrics, 5, At(480));
        Assert.All(two, c => Assert.Equal(24f, c.Width));
        Assert.Equal(new[] { 1385f, 1385f, 1385f }, two.Take(3).Select(c => c.Right));
        Assert.True(two[3].Right < two[0].Left && two[3].Top == two[0].Top && two[4].Top > two[3].Bottom);
        Assert.True(two.All(c => c.Bottom <= 570f && c.Top >= 480f));
        // a panel high in the top room starts the column under the room, not at the panel's top
        Assert.Equal(215f, LiveHud.AlertStack(metrics, 1, At(150))[0].Top);
    }

    [Fact]
    public void ANewAlertsPictureIsDrawnInItsChipWithoutTgsSlideIn()
    {
        // tg throws an alert in with transform = matrix(32, 0, MATRIX_TRANSLATE), animated back over a second (alert.dm:65-66); at its start
        // the look moves the picture 32 tg pixels right. With the chips left of the panel (1920x1080 at 150 %) that is over the panel
        var metrics = new HudMetrics(1920, 1080, 1.5f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        Assert.False(LiveHud.AlertsAbove(metrics, layout));
        HudRect chip = LiveHud.AlertStack(metrics, 1, layout)[0], picture = LiveHud.AlertPicture(chip);
        float perPixel = picture.Width / HudMetrics.IconPixels;
        foreach (Affine slide in new[] { Affine.Translate(32, 0), Affine.Translate(9.6, 0) }) // its start, and 0.7 s in
        {
            HudLook tgs = HudLook.Of(AnimatedLook.None with { Transform = slide }, perPixel);
            Assert.True(tgs.MoveX > 0f);
            Assert.True(Overlaps(picture with { Left = picture.Left + tgs.MoveX }, layout.Panel) || slide.C < 32); // tg's slide would reach the panel
            // in a chip on the human HUD: not moved, the picture inside its chip
            HudLook shown = LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Alert, humanHud: true), tgs);
            Assert.Equal((0f, 0f), (shown.MoveX, shown.MoveY));
            Assert.True(Inside(picture with { Left = picture.Left + shown.MoveX, Top = picture.Top + shown.MoveY }, chip));
            Assert.Equal(tgs with { MoveX = 0f, MoveY = 0f }, shown); // nothing else of the look changes
            // the stamina, internals, suit cell and other status chips too
            foreach (LiveHudRole role in new[] { LiveHudRole.Stamina, LiveHudRole.Internals, LiveHudRole.SpaceSuit, LiveHudRole.StatusOther })
                Assert.Equal(shown, LiveHud.ChipLook(LiveHud.Placed(role, humanHud: true, vitals: true), tgs));
            // at tg's places (another HUD, the reduced column) tg's slide plays
            Assert.Equal(tgs, LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Alert, humanHud: false), tgs));
            Assert.Equal(tgs, LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Stamina, humanHud: true, vitals: false), tgs));
        }
    }

    [Fact]
    public void OnAPortraitScreenTheColumnAndTheChipsStayOnTheScreen()
    {
        // tg's own entries collide on such screens; ours are only wholly on the screen, at every HUD scale the options offer
        foreach ((int w, int h) in new[] { (720, 1280), (360, 640) })
        {
            var screen = new HudRect(0, 0, w, h);
            for (int percent = 50; percent <= 300; percent += 5)
            {
                var metrics = new HudMetrics(w, h, percent / 100f);
                foreach (bool human in new[] { true, false })
                {
                    LiveHudLayout layout = LiveHud.Layout(metrics, human);
                    string at = $"{w}x{h} at {percent}%{(human ? "" : ", not human")}";
                    foreach (HudRect r in new[] { layout.Panel, layout.Pill, layout.Doll })
                        Assert.True(r.Width > 0 && Inside(r, screen), $"{at}: {r} off the screen (panel {layout.Panel})");
                    Assert.True(Inside(layout.Doll, layout.Panel), $"{at}: the doll {layout.Doll} out of the panel {layout.Panel}");
                    if (!human)
                        continue;
                    foreach (HudRect chip in LiveHud.AlertStack(metrics, 8, layout))
                        Assert.True(chip.Width >= 1 && Inside(chip, screen), $"{at}: chip {chip} off the screen");
                }
            }
        }
    }


    [Theory]
    [InlineData(1920, 1080, 1f)]
    [InlineData(1280, 720, 1.5f)]
    [InlineData(1024, 768, 2.5f)]
    public void AClickOrHoverAnywhereOnAChipIsTgsAlert(int width, int height, float scale)
    {
        var metrics = new HudMetrics(width, height, scale);
        List<HudRect> chips = LiveHud.AlertStack(metrics, 2, LiveHud.Layout(metrics, human: true));
        long first = FakeServer.AlertIdBase, second = FakeServer.AlertIdBase + 3;
        var picker = new HudPicker(new StubSurface(new List<HudDrawn> { LiveHud.AlertDrawn(first, chips[0]), LiveHud.AlertDrawn(second, chips[1]) }));
        for (int i = 0; i < 2; i++)
        {
            HudRect chip = chips[i];
            long id = i == 0 ? first : second;
            Assert.Equal(id, picker.PickScreen(chip.Left, chip.Top).Id); // the edge is the alert too, not only its picture
            Assert.Equal(id, picker.PickScreen(chip.Right - 1, chip.Bottom - 1).Id);
            Assert.Equal(id, picker.PickScreen(chip.Left + chip.Width / 2f, chip.Top + chip.Height / 2f).Id);
        }
        Assert.False(picker.PickScreen(chips[0].Left + 2, chips[0].Top - 1).IsHit);
        Assert.False(picker.PickScreen(chips[0].Left - 1, chips[0].Top + 2).IsHit);
    }

    [Fact]
    public void AClickOnTheBuckledChipReachesTgsAlertWhichUnbuckles()
    {
        // what the chip reports is the alert's screen entry: the click goes to it as a click on tg's alert does (the fake answers as tg's
        // /alert/buckled/Click: it unbuckles, and the alert goes)
        (PlayRig rig, ReplicaWorld world) = Start();
        rig.Session.DevCommand("alert me weightless");
        rig.Session.DevCommand("alert me buckled");
        rig.Run(0.4);
        long buckled = FakeServer.AlertIdBase + Array.IndexOf(FakeServer.AlertNames, "buckled");
        Assert.Equal("EAST-1:28,CENTER+4:25", world.Screen[buckled].ScreenLoc); // tg's second alert
        var metrics = new HudMetrics(1920, 1080, 1f);
        List<HudRect> chips = LiveHud.AlertStack(metrics, 2, LiveHud.Layout(metrics, human: true));
        PickHit hit = new HudPicker(new StubSurface(new List<HudDrawn> { LiveHud.AlertDrawn(FakeServer.AlertIdBase + 2, chips[0]), LiveHud.AlertDrawn(buckled, chips[1]) }))
            .PickScreen(chips[1].Left + 3, chips[1].Top + 3);
        Assert.Equal(buckled, hit.Id);
        ClickScreen(rig, hit.Id, hit.IconX, hit.IconY);
        Assert.False(world.Screen.ContainsKey(buckled));
        Assert.True(world.Screen.ContainsKey(FakeServer.AlertIdBase + 2));
        Assert.Equal("EAST-1:28,CENTER+5:27", world.Screen[FakeServer.AlertIdBase + 2].ScreenLoc);
    }

    [Fact]
    public void ACuffedOrMissingHandIsKnownByTgsMarksOnItsBox()
    {
        // tg's hand box (screen_objects.dm, inventory/hand update_overlays): handcuff_overlay is "markus" on a right hand and "gabrielle" on a left
        // one, blocked_overlay "blocked"; the active mark and a held item are not marks
        (PlayRig rig, ReplicaWorld world) = Start();
        Assert.False(LiveHud.HandMarked(world, world.Screen[HandLeftId])); // active, nothing held
        Assert.False(LiveHud.HandMarked(world, world.Screen[HandRightId]));
        rig.Session.Click(new ClickTarget(TargetKind.Entity, ((FakeFeed)rig.Feed).Server.ToolboxId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        rig.Run(0.4);
        ScreenEntry left = world.Screen[HandLeftId];
        Assert.False(LiveHud.HandMarked(world, left)); // holding the toolbox
        Assert.True(world.TryGetAppearance(left.Appearance, out AppearanceEntry look));
        foreach (string mark in new[] { "markus", "gabrielle", "blocked" })
        {
            AppearanceEntry marked = look.WithOverlays(_ => true);
            marked.Overlays.Add(world.LocalAppearance(new AppearanceEntry { IconState = world.LocalString(mark) }));
            Assert.True(LiveHud.HandMarked(world, left with { Appearance = world.LocalAppearance(marked) }), mark);
        }
        // another overlay (a held item the box does not list as a child) is not a mark
        AppearanceEntry other = look.WithOverlays(_ => true);
        other.Overlays.Add(world.LocalAppearance(new AppearanceEntry { IconState = world.LocalString("crowbar") }));
        Assert.False(LiveHud.HandMarked(world, left with { Appearance = world.LocalAppearance(other) }));
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
        // slice D: the hint strip over the row, centred on the gap, its bottom 6 over the visible top of tg's drop and swap buttons (higher than the row's top line
        // here), the same through a swap; it never moves the row, and there is none without the human HUD's column or without a width
        LiveHudLayout column = LiveHud.Layout(metrics, human: true);
        HandsLayout hinted = LiveHud.Hands(metrics, true, false, 0f, column, 300f);
        float dropTop = LiveHud.VisibleButton(Parse("CENTER-1:16,SOUTH+1:5"), metrics).Top; // its visible part: the lower 10 of its 32 tg pixels
        Assert.Equal(996.75f - 22.5f, dropTop);
        Assert.Equal(new HudRect(810, 936, 300, 32), hinted.Hint); // 6 over the whole pixel above it (974)
        Assert.Equal(1f, hinted.HintShare);
        Assert.Equal(hinted.Hint, LiveHud.Hands(metrics, false, true, 0f, column, 300f).Hint);
        Assert.Equal(hinted with { Hint = default, HintShare = 0f }, left);
        Assert.Equal(default, LiveHud.Hands(metrics, true, false, 0f, default, 300f).Hint);
        Assert.Equal(default, LiveHud.Hands(metrics, true, false, 0f, column, 0f).Hint);
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

    // ---- slice D: the vitals column ----

    [Fact]
    public void TgsRightColumnIndicatorsAreKnownByTgsPlacesForThem()
    {
        (_, ReplicaWorld world) = Start();
        TableRef look = world.Screen[HealthId].Appearance;
        foreach ((string loc, LiveHudRole role) in new[]
        {
            ("EAST-1:28,CENTER-1:19", LiveHudRole.Health), ("EAST-1:28,CENTER:21", LiveHudRole.Mood), ("EAST-1:2,CENTER:21", LiveHudRole.Hunger),
            ("EAST-1:28,CENTER-3:14", LiveHudRole.Stamina), ("EAST-1:28,CENTER+1:21", LiveHudRole.Internals), ("EAST-1:28,CENTER-4:14", LiveHudRole.SpaceSuit),
        })
        {
            var entry = new ScreenEntry(9200, look, loc) { Group = HudGroup.Info };
            Assert.Equal(role, LiveHud.RoleOf(world, entry));
            // what it is, not its picture: in a native window, as an action button or a hand it is not
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { Group = HudGroup.Storage }));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, entry with { ActionTarget = 3 }));
            Assert.NotEqual(role, LiveHud.RoleOf(world, entry with { HandIndex = 3 }));
        }
        // the same picture at another place of the right column is no vital: of tg's info group in the status column (CENTER) another status display, a chip;
        // at the sleep button's place tg's
        foreach (string loc in new[] { "EAST-1:28,CENTER-2:17", "EAST-1:28,CENTER-1:15", "EAST-1:28,SOUTH+1:41" })
            Assert.Equal(loc.Contains("CENTER") ? LiveHudRole.StatusOther : LiveHudRole.Tg, LiveHud.RoleOf(world, new ScreenEntry(9201, look, loc) { Group = HudGroup.Info }));
    }

    [Fact]
    public void TheIndicatorsArePlacedOnlyWithTheFullColumnOfTheHumanHudTheStaminaInternalsAndSuitCellAsChips()
    {
        foreach (LiveHudRole role in new[] { LiveHudRole.Health, LiveHudRole.Mood, LiveHudRole.Hunger })
        {
            Assert.Equal(role, LiveHud.Placed(role, humanHud: true, vitals: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: true, vitals: false));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false, vitals: true));
        }
        foreach (LiveHudRole role in new[] { LiveHudRole.Stamina, LiveHudRole.Internals, LiveHudRole.SpaceSuit })
        {
            Assert.Equal(LiveHudRole.Alert, LiveHud.Placed(role, humanHud: true, vitals: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: true, vitals: false));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false, vitals: true));
        }
        // tg's alerts are chips with either column
        Assert.Equal(LiveHudRole.Alert, LiveHud.Placed(LiveHudRole.Alert, humanHud: true, vitals: false));
    }

    [Fact]
    public void TheStaminaInternalsAndSuitCellChipsAndTheHungerBarShowOnlyWhileTgDrawsAPicture()
    {
        // tg's suit cell indicator clears its icon state with no suit on (SPACESUIT_NO_ICON) and starts at "spacesuit_0", which screen_gen.dmi has not
        (_, ReplicaWorld world) = Start();
        ScreenEntry At(string state) => new(9300, world.LocalAppearance(new AppearanceEntry
        {
            Icon = world.LocalString("icons/hud/screen_gen.dmi"), IconState = state == null ? default : world.LocalString(state),
        }), "EAST-1:28,CENTER-4:14");
        bool Exists(string icon, string state) => icon == "icons/hud/screen_gen.dmi" && state.StartsWith("spacesuit_", StringComparison.Ordinal) && state != "spacesuit_0";
        Assert.Equal(LiveHudRole.SpaceSuit, LiveHud.RoleOf(world, At("spacesuit_high")));
        Assert.True(LiveHud.IndicatorShown(world, At("spacesuit_high"), Exists));
        Assert.False(LiveHud.IndicatorShown(world, At("spacesuit_0"), Exists));
        Assert.False(LiveHud.IndicatorShown(world, At(null), Exists));
        Assert.False(LiveHud.IndicatorShown(world, At(""), Exists));
        // the stamina indicator (ui_stamina) and the hunger bar (ui_hunger) the same way
        ScreenEntry stamina = At("stamina_full") with { ScreenLoc = "EAST-1:28,CENTER-3:14" };
        Assert.Equal(LiveHudRole.Stamina, LiveHud.RoleOf(world, stamina));
        Assert.False(LiveHud.IndicatorShown(world, stamina, Exists));
        Assert.True(LiveHud.IndicatorShown(world, stamina, (_, _) => true));
        Assert.False(LiveHud.IndicatorShown(world, At(null) with { ScreenLoc = "EAST-1:2,CENTER:21" }, Exists));
        // tg hides a fed human's hunger bar at alpha 0 with its "hungerbar" state kept (screen_objects.dm, screen/hunger update_appearance): not
        // shown, so the mood face stands alone, centred; a chip at alpha 0 is not shown either; one mid-fade is
        ScreenEntry Faded(string state, string loc, byte alpha) => new(9301, world.LocalAppearance(new AppearanceEntry
        {
            Icon = world.LocalString("icons/hud/screen_gen.dmi"), IconState = world.LocalString(state), Alpha = alpha,
        }), loc);
        bool All(string icon, string state) => true;
        ScreenEntry hunger = Faded("hungerbar", "EAST-1:2,CENTER:21", 0);
        Assert.Equal(LiveHudRole.Hunger, LiveHud.RoleOf(world, hunger));
        Assert.False(LiveHud.IndicatorShown(world, hunger, All));
        Assert.True(LiveHud.IndicatorShown(world, Faded("hungerbar", "EAST-1:2,CENTER:21", 255), All));
        LiveHudLayout layout = LiveHud.Layout(new HudMetrics(1920, 1080, 1f), human: true);
        HudRect alone = LiveHud.VitalSquare(LiveHudRole.Mood, layout, LiveHud.IndicatorShown(world, hunger, All)).Value;
        Assert.InRange(alone.Left + alone.Width / 2f - (layout.Mood.Left + layout.Mood.Width / 2f), -1f, 1f); // centred in its cell
        Assert.False(LiveHud.IndicatorShown(world, Faded("spacesuit_high", "EAST-1:28,CENTER-4:14", 0), All));
        Assert.True(LiveHud.IndicatorShown(world, Faded("spacesuit_high", "EAST-1:28,CENTER-4:14", 128), All));
    }

    [Fact]
    public void AnyOtherEntryOfTgsRightStatusColumnIsAChipAfterTheSuitCellWithTheFullColumn()
    {
        // tg's alien plasma display on a human (alien/organs.dm: add_screen_object(..., HUD_GROUP_INFO, ui_loc = ui_alienplasmadisplay_human
        // "EAST,CENTER-5:8")), and a made-up info-group display higher in the column one tile in (EAST-1)
        (_, ReplicaWorld world) = Start();
        TableRef look = world.Screen[HealthId].Appearance;
        var upper = new ScreenEntry(9400, look, "EAST-1:28,CENTER+3:10") { Group = HudGroup.Info };
        var lower = new ScreenEntry(9401, look, "EAST,CENTER-5:8") { Group = HudGroup.Info };
        Assert.True(LiveHud.InStatusColumn(Parse(lower.ScreenLoc)));
        Assert.Equal(LiveHudRole.StatusOther, LiveHud.RoleOf(world, upper));
        Assert.Equal(LiveHudRole.StatusOther, LiveHud.RoleOf(world, lower));
        // only of tg's info group, and not a slot, a hand or an action button
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, lower with { Group = HudGroup.None }));
        Assert.Equal(LiveHudRole.BottomSlot, LiveHud.RoleOf(world, lower with { SlotId = 5 }));
        Assert.NotEqual(LiveHudRole.StatusOther, LiveHud.RoleOf(world, lower with { HandIndex = 3 }));
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, lower with { ActionTarget = 2 }));
        // an info-group entry tg anchors at the bottom, the top, the left or further in is not one
        foreach (string loc in new[] { "EAST,SOUTH+1:24", "EAST-1:28,SOUTH+1:41", "EAST,NORTH-1:10", "WEST,CENTER", "WEST:6,CENTER-5:8", "EAST-2:28,CENTER+3:10", "EAST-1:28,CENTER-5:8 to EAST,CENTER-4:8" })
        {
            Assert.False(LiveHud.InStatusColumn(Parse(loc)), loc);
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, lower with { ScreenLoc = loc }));
        }
        // the doll (at ui_healthdoll, in that column) and the known indicators keep their roles
        Assert.Equal(LiveHudRole.Doll, LiveHud.RoleOf(world, world.Screen[DollId]));
        Assert.Equal(LiveHudRole.Health, LiveHud.RoleOf(world, world.Screen[HealthId]));
        // a chip with the full column, tg's place with the reduced one or off the human HUD
        Assert.Equal(LiveHudRole.Alert, LiveHud.Placed(LiveHudRole.StatusOther, humanHud: true, vitals: true));
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.StatusOther, humanHud: true, vitals: false));
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.StatusOther, humanHud: false, vitals: true));
        // the order: tg's alerts, stamina, internals, suit cell, then these, tg's top to bottom
        int[] order =
        {
            LiveHud.ChipOrder(LiveHudRole.Alert, Parse("EAST-1:28,CENTER+1:19")), LiveHud.ChipOrder(LiveHudRole.Stamina, default),
            LiveHud.ChipOrder(LiveHudRole.Internals, default), LiveHud.ChipOrder(LiveHudRole.SpaceSuit, default),
            LiveHud.ChipOrder(LiveHudRole.StatusOther, Parse(upper.ScreenLoc)), LiveHud.ChipOrder(LiveHudRole.StatusOther, Parse(lower.ScreenLoc)),
        };
        Assert.Equal(order.OrderBy(o => o), order);
        Assert.Equal(order.Length, order.Distinct().Count());
    }

    [Fact]
    public void AtTheReferenceSizeTheFullColumnStandsAtTheRightEdgeOverTgsSleepButton()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        Assert.True(layout.Vitals);
        Assert.Equal(3, layout.DollScale);
        // 128 wide, 8 from the right edge; the pill's bottom 8 over tg's sleep button (ui_above_throw, its top at 843.75 here) to the whole pixel above,
        // the panel 6 over the pill
        float sleepTop = ScreenLoc.Place(Parse("EAST-1:28,SOUTH+1:41"), metrics, 32, 32, 0, 0, out _).Top;
        Assert.Equal(843.75f, sleepTop);
        Assert.Equal(new HudRect(1784, 803, 128, 32), layout.Pill);
        // the panel: 8 over and under, the doll at 3 (96), the health and the mood cells at 2 (64 each), 4 between: three cells
        float panelHeight = 8 + 96 + 4 + 64 + 4 + 64 + 8;
        Assert.Equal(new HudRect(1784, layout.Pill.Top - 6 - panelHeight, 128, panelHeight), layout.Panel);
        float top = layout.Panel.Top + 8;
        Assert.Equal(new HudRect(1784 + 16, top, 96, 96), layout.Doll);
        Assert.Equal(new HudRect(1784 + 32, top + 100, 64, 64), layout.Health);
        Assert.Equal(new HudRect(1784 + 6, top + 168, 116, 64), layout.Mood); // as wide as the 58 tg pixel pair at 2, centred
        Assert.Equal((2, 2), (layout.CellScale, layout.PairScale));
        // while tg draws the hunger bar: the bar at the pair's left, the mood face 26 tg pixels right of it (tg's EAST-1:2 and EAST-1:28)
        Assert.Equal(new HudRect(1790, top + 168, 64, 64), LiveHud.VitalSquare(LiveHudRole.Hunger, layout));
        Assert.Equal(new HudRect(1790 + 52, top + 168, 64, 64), LiveHud.VitalSquare(LiveHudRole.Mood, layout));
        Assert.Equal(layout.Health, LiveHud.VitalSquare(LiveHudRole.Health, layout));
        // without the hunger bar the mood face alone, centred in its cell at 2
        Assert.Equal(new HudRect(1784 + 32, top + 168, 64, 64), LiveHud.VitalSquare(LiveHudRole.Mood, layout, hungerShown: false));
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Stamina, layout)); // a chip now
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Doll, layout));
        // the panel's size does not depend on which entries show: the layout has no input but the screen and the scale
        Assert.Equal(layout, LiveHud.Layout(metrics, human: true));
        // the HUDs that are not human keep slice B's panel left of tg's right column
        Assert.False(LiveHud.Layout(metrics).Vitals);
        Assert.True(LiveHud.Layout(metrics).Panel.Right <= 1920 - 2 * metrics.Tile);
    }

    [Fact]
    public void WhereTheFullColumnDoesNotFitTheReducedOneStandsLeftOfTgsRightColumn()
    {
        var metrics = new HudMetrics(1280, 720, 3f);
        Assert.Null(LiveHud.VitalsColumn(metrics));
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        float u = UiTokens.Unit(metrics);
        Assert.False(layout.Vitals);
        Assert.Equal(MathF.Round(UiTokens.VitalsWidth * u), layout.Panel.Width);
        Assert.Equal(layout.Panel.Width, layout.Pill.Width);
        Assert.True(layout.Panel.Right <= 1280 - 2 * metrics.Tile - UiTokens.DollPanelGap * u + 1f);
        Assert.Equal(MathF.Round(2 * UiTokens.VitalsPad * u + 32 * layout.DollScale), layout.Panel.Height); // the doll cell alone
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Health, layout)); // tg's indicators stay at tg's places
        Assert.Equal(default, layout.Health);
    }

    [Fact]
    public void TheHintStripsWidthPictureAndFitAndThePillsWordNeverCut()
    {
        // the natural width: 8 at each end, the picture, then 12 before each entry, its cap, 6, its verb
        Assert.Equal(16 + 32 + 3 * (12 + 6) + 19 + 28 + 20 + 36 + 51 + 44, LiveHud.HintWidth(1f, new[] { (19f, 28f), (20f, 36f), (51f, 44f) }), 3);
        Assert.Equal(16 + 32, LiveHud.HintWidth(1f, Array.Empty<(float, float)>()), 3); // nothing bound: the picture alone
        // the picture: tg's 32 x 32 icon at the largest whole scale the strip's height holds
        Assert.Equal(32f, LiveHud.HintPicture(40f));
        Assert.Equal(64f, LiveHud.HintPicture(80f));
        Assert.Equal(20f, LiveHud.HintPicture(20f));
        Assert.Equal(new HudRect(1008, 504, 32, 32), LiveHud.HintPictureSquare(new HudRect(1000, 500, 300, 40), 1f, 1f));
        Assert.Equal(default, LiveHud.HintPictureSquare(default, 1f, 1f));
        // wider than the row (624 at the reference size: the gap and both boxes at the active size): smaller as one, its text to 60 % of the
        // prompts' at least (0.75 of the compact 80 %); below that left out
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout column = LiveHud.Layout(metrics, human: true);
        HandsLayout wide = LiveHud.Hands(metrics, true, false, 0f, column, 700f);
        Assert.Equal(624f / 700f, wide.HintShare, 4);
        Assert.InRange(wide.Hint.Width, 623f, 624f);
        Assert.Equal(MathF.Round(32f * 624f / 700f), wide.Hint.Height);
        Assert.Equal(0.75f, LiveHud.Hands(metrics, true, false, 0f, column, 624f / 0.75f).HintShare, 3);
        Assert.Equal(default, LiveHud.Hands(metrics, true, false, 0f, column, 624f / 0.74f).Hint);
        // the pill's chip and word: smaller together, never cut
        Assert.Equal(1f, LiveHud.PillFit(90f, 114f));
        Assert.Equal(114f / 150f, LiveHud.PillFit(150f, 114f), 4);
    }

    [Fact]
    public void AboveTheDollChipsBeyondTheColumnGoOnInASecondColumn()
    {
        // room over the panel for exactly five: chips 0 to 4 (tg's alerts first) in the column at the edge, top down; the chips after them (the
        // internals and suit cell, 5 and 6) in the next column to the left, top down; every column stands on the panel, 6 over it
        var metrics = new HudMetrics(1920, 1080, 1f);
        float top = MathF.Ceiling(UiTokens.CardTopRoom);
        var layout = new LiveHudLayout(new HudRect(1784, top + 6 + 5 * 40 + 4 * 4, 128, 300), default, new HudRect(1784, 900, 128, 32), 3);
        Assert.True(LiveHud.AlertsAbove(metrics, layout));
        List<HudRect> chips = LiveHud.AlertStack(metrics, 7, layout);
        float bottom = layout.Panel.Top - 6;
        (int Column, int Row)[] places = { (0, 0), (0, 1), (0, 2), (0, 3), (0, 4), (1, 0), (1, 1) };
        int[] inColumn = { 5, 2 };
        for (int i = 0; i < chips.Count; i++)
        {
            (int column, int row) = places[i];
            float columnTop = bottom - inColumn[column] * 44 + 4;
            Assert.Equal(new HudRect(1912 - 40 - column * 44, columnTop + row * 44, 40, 40), chips[i]);
        }
        Assert.Equal(bottom, chips[4].Bottom);
        Assert.Equal(bottom, chips[6].Bottom);
        Assert.True(chips.All(c => c.Top >= top));

        // left of the panel (1920x1080 at 250%, chips of 24, three to a column, 2 apart): chips 0 to 2 in the column next to the panel, top down
        // from the panel's top; 3 and 4 in the next column to the left, top-aligned with it
        var big = new HudMetrics(1920, 1080, 2.5f);
        var low = new LiveHudLayout(new HudRect(1400, 480, 375, 290), default, default, 7);
        Assert.False(LiveHud.AlertsAbove(big, low));
        List<HudRect> left = LiveHud.AlertStack(big, 5, low);
        (int Column, int Row)[] leftPlaces = { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1) };
        for (int i = 0; i < left.Count; i++)
        {
            (int column, int row) = leftPlaces[i];
            Assert.Equal(new HudRect(1385 - 24 - column * 26, 480 + row * 26, 24, 24), left[i]);
        }
    }
}
