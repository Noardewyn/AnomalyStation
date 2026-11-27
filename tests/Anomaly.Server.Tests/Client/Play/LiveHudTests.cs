using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 6, slice B: which screen entries the live HUD re-places (the doll) or hides (tg's zone selector in the captured view),
/// the doll placed clear of tg's right-hand column at every HUD scale, and clicks on the re-placed doll reaching tg's entry with tg's icon pixels.
/// Slice C: the hands known by hand index and the bottom slots by slot id on the human HUD only, the active hand and its swaps, the hands' row and
/// the bottom slots clear of each other and of tg's HUD, and a click on a hand box reaching tg's hand box or the held item.
/// Slice D: the alert chips and the hint strip. Slice E: the bare vitals stack, the status chips, tg's combat toggle in the corner and the Tab row,
/// tg's buttons that a key does hidden, and the keys' buttons found.
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
        Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, world.Screen[FirstSlotId])); // a slot of the toggled inventory: the window's
        Assert.Equal(LiveHudRole.Health, LiveHud.RoleOf(world, world.Screen[HealthId])); // slice D: tg's health indicator, at ui_health
        // slice E: tg's move intent, throw, drop and swap buttons, which keys do
        foreach (long id in new[] { IntentId, ThrowId, DropId, SwapId })
            Assert.Equal(LiveHudRole.KeyButton, LiveHud.RoleOf(world, world.Screen[id]));
        // every other entry of the fake's HUD stays at tg's place, but slice C's: the two hands, the six bottom slots and the inventory toggle,
        // slice D's health indicator, and slice E's buttons: those four and the fake's nine others (FakeServer.HudButtons)
        Assert.Equal(3 + 2 + 6 + 1 + 1 + 4 + 9, world.Screen.Values.Count(e => LiveHud.RoleOf(world, e) != LiveHudRole.Tg));
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
    public void TgsCombatToggleFollowsTheKeyAndItsOwnClick()
    {
        // slice E: tg's own toggle, drawn with tg's picture in the corner, its icon state "combat" or "combat_off"
        (PlayRig rig, ReplicaWorld world) = Start();
        string State() => world.TryGetAppearance(world.Screen[FakeServer.CombatId].Appearance, out AppearanceEntry app) ? world.ResolveString(app.IconState) : null;
        Assert.Equal("combat_off", State());
        rig.Session.ToggleCombatMode(); // the key (F by default)
        rig.Run(0.4);
        Assert.Equal("combat", State());
        Assert.True(HeldHands.Find(world).Combat); // the swing sound's reading agrees
        ClickScreen(rig, FakeServer.CombatId, 16, 16); // tg's click on the toggle (under Tab)
        Assert.Equal("combat_off", State());
        Assert.False(HeldHands.Find(world).Combat);
        Assert.Equal(LiveHudRole.CombatToggle, LiveHud.Placed(LiveHud.RoleOf(world, world.Screen[FakeServer.CombatId]), humanHud: true, vitals: true));
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
    public void OnAnotherHudTheDollIsOnTheScreenAndClearOfTgsRightColumn(int width, int height, float scale)
    {
        // every HUD but the human one: the reduced arrangement, the doll alone and bare (slice E took the panel and the pill away)
        var metrics = new HudMetrics(width, height, scale);
        LiveHudLayout layout = LiveHud.Layout(metrics);
        var screen = new HudRect(0, 0, width, height);
        Assert.False(layout.Vitals);
        Assert.Equal(layout.Doll, layout.Stack);
        Assert.True(Inside(layout.Doll, screen), $"doll {layout.Doll}");
        // the doll: a whole multiple of tg's pixels, at whole pixels
        Assert.True(layout.DollScale >= 1);
        Assert.Equal(32f * layout.DollScale, layout.Doll.Width);
        Assert.Equal(32f * layout.DollScale, layout.Doll.Height);
        Assert.True(layout.Doll.Left == MathF.Round(layout.Doll.Left) && layout.Doll.Top == MathF.Round(layout.Doll.Top), $"{layout.Doll}");
        // at the middle right
        Assert.True(layout.Doll.Left > width / 2f, $"{layout.Doll}");
        Assert.True(layout.Doll.Top < height / 2f, $"{layout.Doll}");
        foreach (string loc in RightColumn)
        {
            Assert.True(ScreenLoc.TryParse(loc, out ScreenLocSpec spec), loc);
            HudRect tg = ScreenLoc.Place(spec, metrics, 32, 32, 0, 0, out _);
            Assert.False(Overlaps(layout.Doll, tg), $"doll {layout.Doll} over {loc} {tg}");
        }
        float pulseTop = ScreenLoc.Place(Parse(RightColumn[0]), metrics, 32, 32, 0, 0, out _).Top;
        for (int slot = 1; slot <= 5; slot++)
        {
            HudRect alert = ScreenLoc.PlaceAlert(slot, 5, metrics, pulseTop);
            Assert.False(Overlaps(layout.Doll, alert), $"alert {slot} {alert}");
        }
        // the zone labels and the prompts end before it
        Assert.True(LiveHud.LeftOfVitals(layout, metrics) < layout.Doll.Left);
    }

    private static ScreenLocSpec Parse(string loc) => ScreenLoc.TryParse(loc, out ScreenLocSpec spec) ? spec : default;

    [Fact]
    public void TheDollIsThreeTgPixelsPerPixelAtTheReferenceSizeAndShrinksOnlyWhenItDoesNotFit()
    {
        Assert.Equal(3, LiveHud.Layout(new HudMetrics(1920, 1080, 1f)).DollScale);
        Assert.Equal(6, LiveHud.Layout(new HudMetrics(1920, 1080, 2f)).DollScale);
        Assert.Equal(2, LiveHud.Layout(new HudMetrics(1280, 720, 1f)).DollScale);
        // at 300% on 1080 lines the doll stays above tg's buttons (ui_human_area from 486)
        LiveHudLayout big = LiveHud.Layout(new HudMetrics(1920, 1080, 3f));
        Assert.InRange(big.DollScale, 1, 9);
        HudRect area = ScreenLoc.Place(Parse("EAST-3:24,SOUTH+1:24"), new HudMetrics(1920, 1080, 3f), 32, 32, 0, 0, out _);
        Assert.True(big.Doll.Bottom <= area.Top, $"{big.Doll} {area}");
        Assert.True(big.Doll.Top >= 0f);
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
    [InlineData(1920, 1080, 1f, false)]
    [InlineData(1280, 720, 1.25f, false)]
    [InlineData(2560, 1440, 2f, false)]
    [InlineData(1920, 1080, 3f, false)]
    [InlineData(1920, 1080, 1f, true)]
    [InlineData(1280, 720, 1.25f, true)]
    [InlineData(2560, 1440, 2f, true)]
    [InlineData(1920, 1080, 3f, true)]
    public void AClickOnTheRePlacedDollIsTheSameTgIconPixel(int width, int height, float scale, bool human)
    {
        LiveHudLayout layout = LiveHud.Layout(new HudMetrics(width, height, scale), human);
        // what HudLayer.Collect reports: the doll's icon square once (whatever size its zones' pictures are: a wound outline makes them 36 x 36
        // with the square at 2,2)
        HudDrawn doll = LiveHud.DollDrawn(DollId, layout);
        Assert.Equal(layout.Doll, doll.Rect);
        Assert.Null(doll.Image);
        var picker = new HudPicker(new StubSurface(new List<HudDrawn> { doll }));
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
        var picker = new HudPicker(new StubSurface(new List<HudDrawn> { LiveHud.DollDrawn(DollId, new LiveHudLayout(square, scale)) }));
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
    public void APromptBesideTheCrosshairEndsBeforeTheVitalsAndIsNarrowedNotMoved()
    {
        foreach ((int w, int h, float scale, bool human) in new[] { (1920, 1080, 2f, false), (1280, 720, 2f, false), (1920, 1080, 1f, false), (1024, 600, 3f, false),
                     (1920, 1080, 2f, true), (1280, 720, 2f, true), (1024, 600, 3f, true) })
        {
            var metrics = new HudMetrics(w, h, scale);
            LiveHudLayout layout = LiveHud.Layout(metrics, human);
            float edge = LiveHud.LeftOfVitals(layout, metrics);
            float reach = KeyChips.SightReach(false, 0f, 70f, h, 1f);
            // a long name ("reinforced wall (wall/r wall)" at 200%: about 700 pixels wide)
            HudRect block = KeyChips.PromptBlock(metrics, 2, 700f * UiTokens.Unit(metrics), w / 2f, h / 2f, reach, edge);
            Assert.True(block.Right <= edge + 0.01f, $"{w}x{h} {scale}: {block} past {edge}");
            Assert.False(Overlaps(block, layout.Doll) || Overlaps(block, layout.Health) || Overlaps(block, layout.Mood));
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

    // the fake's six bottom slots (FakeServer.SlotDefs): the ID slot and the left and right pocket are the bar's cells 10, 8 and 9; the back, suit
    // storage and the belt the worn slots
    private const long IdSlotId = 2012, LeftPocketId = 2016, RightPocketId = 2017;
    private static readonly long[] BarSlotIds = { IdSlotId, LeftPocketId, RightPocketId };
    private static readonly long[] WornSlotIds = { 2015, 2018, BeltSlotId };

    [Fact]
    public void TheHandsAreKnownByHandIndexAndTheBottomSlotsBySlotIdNeverByPlace()
    {
        (_, ReplicaWorld world) = Start();
        Assert.Equal(LiveHudRole.LeftHand, LiveHud.RoleOf(world, world.Screen[HandLeftId]));
        Assert.Equal(LiveHudRole.RightHand, LiveHud.RoleOf(world, world.Screen[HandRightId]));
        Assert.Equal(LiveHudRole.InventoryToggle, LiveHud.RoleOf(world, world.Screen[FakeServer.InventoryToggleId]));
        foreach (long id in BarSlotIds)
            Assert.Equal(LiveHudRole.BarSlot, LiveHud.RoleOf(world, world.Screen[id]));
        foreach (long id in WornSlotIds)
            Assert.Equal(LiveHudRole.WornSlot, LiveHud.RoleOf(world, world.Screen[id]));
        // package 3: the pockets and the ID slot by tg's slot ids, never by place: the left pocket at the ID slot's place is still cell 8
        Assert.Equal(new[] { 10, 8, 9 }, BarSlotIds.Select(id => LiveHud.BarCellOfSlot(world.Screen[id].SlotId)));
        Assert.All(WornSlotIds, id => Assert.Equal(0, LiveHud.BarCellOfSlot(world.Screen[id].SlotId)));
        Assert.Equal(LiveHudRole.BarSlot, LiveHud.RoleOf(world, world.Screen[LeftPocketId] with { ScreenLoc = world.Screen[IdSlotId].ScreenLoc }));
        Assert.Equal(LiveHudRole.WornSlot, LiveHud.RoleOf(world, world.Screen[IdSlotId] with { SlotId = world.Screen[BeltSlotId].SlotId }));
        // tg's swap, drop and throw buttons are buttons a key does (slice E)
        foreach (long id in new[] { SwapId, DropId, ThrowId })
            Assert.Equal(LiveHudRole.KeyButton, LiveHud.RoleOf(world, world.Screen[id]));
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
        foreach (LiveHudRole role in new[] { LiveHudRole.LeftHand, LiveHudRole.RightHand, LiveHudRole.BarSlot, LiveHudRole.WornSlot, LiveHudRole.Alert })
        {
            Assert.Equal(role, LiveHud.Placed(role, humanHud: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false));
        }
        // the doll, the combat toggle and the zone selector are slice B's on every HUD
        foreach (LiveHudRole role in new[] { LiveHudRole.Doll, LiveHudRole.CombatToggle, LiveHudRole.ZoneSelector, LiveHudRole.InventoryToggle, LiveHudRole.Tg })
            Assert.Equal(role, LiveHud.Placed(role, humanHud: false));
        // the hands and the bar always; package 3: tg's belt, back and suit-storage slots only under Tab, at tg's places on another HUD always
        Assert.True(LiveHud.Shown(LiveHudRole.LeftHand, cursorMode: false) && LiveHud.Shown(LiveHudRole.BarSlot, cursorMode: false));
        Assert.False(LiveHud.Shown(LiveHudRole.WornSlot, cursorMode: false));
        Assert.True(LiveHud.Shown(LiveHudRole.WornSlot, cursorMode: true));
        Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHudRole.WornSlot, humanHud: false), cursorMode: false));
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
                yield return new object[] { w, h, percent / 100f };
        }
    }

    // tg's human HUD entries still drawn at the bottom right (slice E, hud/human.dm, __DEFINES/hud.dm): the combat toggle in the corner (ui_zonesel's
    // place), and under Tab the zone selector at ui_movi's, the memories button at ui_acti's and the area creator at ui_human_floor_changer's
    private static readonly string[] HumanCorner = { "EAST-1:28,SOUTH:5" };

    private static readonly string[] HumanTabRow = { "EAST-2:26,SOUTH:5", "EAST-3:24,SOUTH:5", "EAST-4:22,SOUTH:5" };

    // tg's right-column status icons of the human HUD and its sleep button: at tg's places with the reduced arrangement, chips with the full stack
    private static readonly string[] HumanStatus =
    {
        "EAST-1:28,CENTER-1:19", // ui_health
        "EAST-1:28,CENTER:21", // ui_mood
        "EAST-1:2,CENTER:21", // ui_hunger
        "EAST-1:28,CENTER-3:14", // ui_stamina
        "EAST-1:28,CENTER+1:21", // ui_internal
        "EAST-1:28,CENTER-4:14", // ui_spacesuit
        "EAST-1:28,SOUTH+1:41", // ui_above_throw: sleep
    };

    private static bool Whole(HudRect r) => r.Left == MathF.Round(r.Left) && r.Top == MathF.Round(r.Top) && r.Width == MathF.Round(r.Width) && r.Height == MathF.Round(r.Height);

    // tg's entries still drawn on the human HUD at this screen and scale, in first person or under Tab
    private static List<(string loc, HudRect rect)> HumanTg(HudMetrics metrics, LiveHudLayout layout, bool tab)
        => HumanCorner.Concat(tab ? HumanTabRow : Array.Empty<string>()).Concat(layout.Vitals ? Array.Empty<string>() : HumanStatus)
            .Select(loc => (loc, ScreenLoc.Place(Parse(loc), metrics, 32, 32, 0, 0, out _))).ToList();

    [Theory]
    [MemberData(nameof(HandScreens))]
    public void TheHandsTheBarTheWornRowTheVitalsAndTheStripAreOnTheScreenAndClearOfEachOtherAndOfTgsHud(int width, int height, float scale)
    {
        // the chat window does not move the row (a window over it covers it as it covers tg's HUD), so the row depends on the screen, the scale and
        // the active hand alone
        var metrics = new HudMetrics(width, height, scale);
        var screen = new HudRect(0, 0, width, height);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        // the arrangement depends on the screen and the scale alone
        Assert.Equal(layout, LiveHud.Layout(new HudMetrics(width, height, scale), human: true));
        Assert.Equal(LiveHud.VitalsColumn(metrics).HasValue, layout.Vitals);
        float u = UiTokens.Unit(metrics);
        List<HudRect> chips = LiveHud.AlertStack(metrics, LiveHud.MaxChips, layout);
        // a hint strip of about the natural width of the default keys' entries (Z Use, Q Drop, Ctrl+Q Throw: about 300 reference pixels)
        float hint = 300f * u;
        HudRect prompt = KeyChips.PromptBlock(metrics, KeyChips.MaxPromptLines, UiTokens.PromptMaxWidth * u, width / 2f, height / 2f,
            KeyChips.SightReach(false, 0f, 70f, height, 1f), Math.Min(LiveHud.LeftOfVitals(layout, metrics), LiveHud.LeftOfAlerts(chips, metrics)));
        var vitals = new List<(string Name, HudRect Rect)> { ("doll", layout.Doll) };
        if (layout.Vitals)
            vitals.AddRange(new[] { ("health", layout.Health), ("mood", layout.Mood) });
        // every place a chip takes for zero to twelve chips (above the doll a column's chips stand on the doll, so fewer chips stand elsewhere)
        List<HudRect> anyChip = Enumerable.Range(0, LiveHud.MaxChips + 1).SelectMany(n => LiveHud.AlertStack(metrics, n, layout)).Distinct().ToList();
        foreach (bool tab in new[] { false, true })
        foreach ((bool leftActive, bool rightActive) in new[] { (true, false), (false, true), (false, false) })
        {
            var tg = HumanTg(metrics, layout, tab);
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, rightActive, layout, hint, tab: tab);
            var row = new List<(string Name, HudRect Rect)> { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar) };
            // package 3: the ten cells in the bar; under Tab the worn slots' row over its right end, and no hint strip
            var cells = Enumerable.Range(1, HotbarMessage.CellCount).Select(c => (Name: $"cell {c}", Rect: LiveHud.BarCell(hands, c))).ToList();
            var worn = Enumerable.Range(0, LiveHud.WornSlots).Select(i => (Name: $"worn slot {i}", Rect: LiveHud.WornCell(hands, i, LiveHud.WornSlots))).ToList();
            var ours = new List<(string Name, HudRect Rect)> { row[0], row[1] }.Concat(cells).Concat(vitals).ToList();
            if (tab && hands.WornRight > 0f)
                ours.AddRange(worn);
            if (hands.Hint.Width > 0)
                ours.Add(("hint strip", hands.Hint));
            string at = $"{width}x{height} at {scale}, {(tab ? "Tab" : "first person")}, active {(leftActive ? "left" : rightActive ? "right" : "neither")}, {(layout.Vitals ? "full" : "reduced")}";
            Assert.True(!tab || hands.Hint.Width == 0, $"{at}: a hint strip under Tab");
            foreach ((string name, HudRect rect) in ours)
            {
                Assert.True(rect.Width > 0 && rect.Height > 0 && Inside(rect, screen), $"{at}: {name} {rect}");
                foreach ((string loc, HudRect tgRect) in tg)
                    Assert.False(Overlaps(rect, tgRect), $"{at}: {name} {rect} over {loc} {tgRect}");
                foreach (HudRect chip in anyChip)
                    Assert.False(Overlaps(rect, chip), $"{at}: {name} {rect} over chip {chip}");
            }
            for (int i = 0; i < ours.Count; i++)
                for (int j = i + 1; j < ours.Count; j++)
                    Assert.False(Overlaps(ours[i].Rect, ours[j].Rect), $"{at}: {ours[i].Name} {ours[i].Rect} over {ours[j].Name} {ours[j].Rect}");
            // slice D: the hint strip never moves the row, is the same for either hand active, and where it shows it is clear of the chips and the
            // prompt block
            HandsLayout closed = LiveHud.Hands(metrics, leftActive, rightActive);
            Assert.Equal(closed, hands with { Hint = default, HintShare = 0f });
            Assert.Equal(LiveHud.Hands(metrics, rightActive, leftActive, layout, hint, tab: tab).Hint, hands.Hint);
            Assert.True(hands.WornRight > 0f, $"{at}: the worn row left out");
            if (tab && hands.WornRight > 0f)
            {
                // the worn slots' row: whole pixel squares of a cell's size, side by side to the bar's right end, over the bar and clear of the prompt block
                for (int i = 0; i < worn.Count; i++)
                {
                    HudRect cell = worn[i].Rect;
                    Assert.True(Whole(cell) && cell.Width == cell.Height, $"{at}: worn slot {i} {cell}");
                    Assert.InRange(cell.Width, UiTokens.HotbarSlot * hands.Unit - 1.5f, UiTokens.HotbarSlot * hands.Unit + 1f);
                    Assert.True(cell.Bottom <= hands.Hotbar.Top && cell.Left >= hands.Hotbar.Left, $"{at}: worn slot {i} {cell} not over the bar {hands.Hotbar}");
                    Assert.False(Overlaps(cell, prompt), $"{at}: worn slot {i} {cell} over the prompt block {prompt}");
                    if (i > 0)
                        Assert.True(worn[i - 1].Rect.Right <= cell.Left, $"{at}: worn slots {i - 1} and {i}");
                }
                // over the bar's right end; where tg's entries reach down over it (a 5:4 screen at the largest scales) over its left end
                Assert.True(worn[^1].Rect.Right == hands.Hotbar.Right || worn[0].Rect.Left == hands.Hotbar.Left, $"{at}: the worn row {worn[0].Rect}..{worn[^1].Rect} at neither end of the bar {hands.Hotbar}");
            }
            if (hands.Hint.Width > 0)
            {
                foreach (HudRect chip in chips)
                    Assert.False(Overlaps(hands.Hint, chip), $"{at}: hint strip {hands.Hint} over chip {chip}");
                Assert.False(Overlaps(hands.Hint, prompt), $"{at}: hint strip {hands.Hint} over the prompt block {prompt}");
                // over the row, centred on the gap, no wider than the row with both boxes at the active size, its text at 60 % of the prompts' at least
                float activeSide = MathF.Round(UiTokens.HandActive * hands.Unit);
                // slice E: 6 over the row's top line (the active box), at the whole pixel above it
                Assert.Equal(MathF.Floor(hands.Hotbar.Bottom - activeSide) - MathF.Round(UiTokens.HintRise * u), hands.Hint.Bottom);
                // the strip and its picture at whole pixels
                foreach (HudRect r in new[] { hands.Hint, LiveHud.HintPictureSquare(hands.Hint, u, hands.HintShare) })
                    Assert.True(Whole(r), $"{at}: hint {r} not at whole pixels");
                Assert.InRange(hands.Hint.Left + hands.Hint.Width / 2f - (hands.Hotbar.Left + hands.Hotbar.Width / 2f), -1f, 1f);
                Assert.True(hands.Hint.Width <= hands.Hotbar.Width + 2 * (MathF.Round(UiTokens.HandGap * hands.Unit) + activeSide) + 0.01f);
                Assert.True(hands.HintShare * UiTokens.HintShare >= UiTokens.HintLeastShare - 0.001f);
            }
            // the active hand 72 reference pixels and the other 56, both on the bar's bottom line, the bar between them. The smaller box is never
            // made smaller than the room between tg's inventory toggle and its bottom-right menu, and under the chat lines, leaves it, nor than tg's
            // own 32-pixel slot unless that room is less (a 4:3 or 5:4 screen at 230 % and more: tg's own menu takes the bottom's width)
            float k = hands.Unit, margin = UiTokens.DollPanelGap * u;
            HudRect active = leftActive ? hands.Left : hands.Right, other = leftActive ? hands.Right : hands.Left;
            Assert.InRange(active.Width, (leftActive || rightActive ? UiTokens.HandActive : UiTokens.HandInactive) * k - 1f, (leftActive || rightActive ? UiTokens.HandActive : UiTokens.HandInactive) * k + 1f);
            Assert.InRange(other.Width, UiTokens.HandInactive * k - 1f, UiTokens.HandInactive * k + 1f);
            const float half = UiTokens.HotbarWidth / 2f + UiTokens.HandGap + UiTokens.HandActive;
            float across = (ScreenLoc.Place(Parse("EAST-4:22,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Left - ScreenLoc.Place(Parse("WEST:6,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Right - 2 * margin) / (2 * half * u);
            float up = (height * 0.2f - 20f * u - margin) / ((UiTokens.HotbarMargin + UiTokens.HandActive) * u);
            float least = Math.Min(32f, UiTokens.HandInactive * u * Math.Clamp(Math.Min(across, up), 0.1f, 1f));
            Assert.True(other.Width >= least - 1f, $"{at}: the smaller hand box {other} under {least} pixels");
            Assert.Equal(hands.Hotbar.Bottom, hands.Left.Bottom);
            Assert.Equal(hands.Hotbar.Bottom, hands.Right.Bottom);
            Assert.True(hands.Left.Right < hands.Hotbar.Left && hands.Hotbar.Right < hands.Right.Left);
            Assert.InRange(hands.Hotbar.Width, UiTokens.HotbarWidth * k - 1f, UiTokens.HotbarWidth * k + 1f);
            // package 3: the ten cells, each a whole pixel square wholly in the bar, a cell's size, left to right, never over each other (a gap between
            // them from a gap of one screen pixel up), the gap between two groups the wider; the last at the bar's right end
            for (int c = 1; c <= HotbarMessage.CellCount; c++)
            {
                HudRect cell = cells[c - 1].Rect;
                Assert.True(Inside(cell, hands.Hotbar) && Whole(cell) && cell.Width == cell.Height && cell.Bottom == hands.Hotbar.Bottom, $"{at}: cell {c} {cell} in the bar {hands.Hotbar}");
                Assert.InRange(cell.Width, UiTokens.HotbarSlot * k - 1.5f, UiTokens.HotbarSlot * k + 1f);
                if (c > 1)
                {
                    HudRect previous = cells[c - 2].Rect;
                    float gap = c is 8 or 10 ? UiTokens.HotbarGroupGap : UiTokens.HotbarSlotGap;
                    Assert.True(gap * k >= 1f ? previous.Right < cell.Left : previous.Right <= cell.Left, $"{at}: cells {c - 1} {previous} and {c} {cell}");
                    Assert.InRange(cell.Left - previous.Right, gap * k - 1.5f, gap * k + 1.5f);
                }
            }
            Assert.Equal(hands.Hotbar.Right, cells[^1].Rect.Right);
            // the swap key's cap: inside the inactive hand's box at its top outer corner, never on the active one, a short or a long label; none
            // with neither of the two active
            float inset = Math.Max(1f, MathF.Round(UiTokens.SwapCapInset * k));
            foreach (float label in new[] { UiTokens.SwapCapHeight * k, 60f * k, 10000f })
            {
                HudRect cap = LiveHud.SwapCap(hands, leftActive, rightActive, label);
                if (!leftActive && !rightActive)
                {
                    Assert.Equal(default, cap);
                    continue;
                }
                Assert.True(cap.Width > 0 && cap.Height > 0 && Inside(cap, other) && !Overlaps(cap, active), $"{at}: cap {cap} in {other}, active {active}");
                Assert.Equal(other.Top + inset, cap.Top);
                Assert.Equal(leftActive ? other.Right - inset : other.Left + inset, leftActive ? cap.Right : cap.Left);
            }
            // the bar stays put through a swap, and so do its cells and the worn row
            Assert.Equal(LiveHud.Hands(metrics, !leftActive, leftActive).Hotbar, hands.Hotbar);
        }
        // slice E: the pictures at whole pixels, the doll a whole multiple of tg's pixels, under the version tag and the ping chip (the reduced doll
        // climbs into that room only at one tg pixel per pixel, the least it can be)
        foreach ((string name, HudRect rect) in vitals)
            Assert.True(Whole(rect), $"{width}x{height} at {scale}: {name} {rect} not at whole pixels");
        Assert.True(layout.Doll.Top >= MathF.Ceiling(UiTokens.CardTopRoom * u) || (!layout.Vitals && layout.DollScale == 1), $"{width}x{height} at {scale}: doll {layout.Doll} under the top room");
        if (layout.Vitals)
        {
            // three pictures, no box: the doll at S, the health indicator and the mood face at S - 1 (at least 1), all centred on one axis, the doll's
            // square (the widest) touching the screen's right edge, 8 apart, the mood face's bottom 8 over tg's combat toggle in the corner (at the
            // whole pixel above it)
            float gap = MathF.Round(UiTokens.VitalsMargin * u), axis = width - 16 * layout.DollScale;
            int cell = Math.Max(1, layout.DollScale - 1);
            Assert.Equal(cell, layout.CellScale);
            Assert.Equal(axis, LiveHud.Axis(layout));
            Assert.Equal(new HudRect(width - 32 * layout.DollScale, layout.Doll.Top, 32 * layout.DollScale, 32 * layout.DollScale), layout.Doll);
            Assert.Equal(new HudRect(axis - 16 * cell, layout.Doll.Bottom + gap, 32 * cell, 32 * cell), layout.Health);
            Assert.Equal(new HudRect(axis - 16 * cell, layout.Health.Bottom + gap, 32 * cell, 32 * cell), layout.Mood);
            Assert.True(layout.Health.Width <= layout.Doll.Width && layout.Doll.Right == width);
            HudRect corner = ScreenLoc.Place(Parse("EAST-1:28,SOUTH:5"), metrics, 32, 32, 0, 0, out _);
            Assert.Equal(MathF.Floor(corner.Top - gap), layout.Mood.Bottom);
            Assert.Equal(layout.Health, LiveHud.VitalSquare(LiveHudRole.Health, layout));
            Assert.Equal(layout.Mood, LiveHud.VitalSquare(LiveHudRole.Mood, layout));
        }
        else
            Assert.True(layout.Doll.Right <= width - 2 * metrics.Tile, $"{width}x{height} at {scale}: the reduced doll {layout.Doll} not left of tg's right column");
        // the alert chips: tg's alert column is not drawn on the human HUD
        {
            AlertStackIsClear(metrics, layout, HumanTg(metrics, layout, tab: true), $"{width}x{height} at {scale}");
            foreach (HudRect chip in chips)
                Assert.True(Whole(chip) && Whole(LiveHud.AlertPicture(chip)), $"{width}x{height} at {scale}: chip {chip} not at whole pixels");
        }
    }

    // ---- slice D: tg's alerts as chips by the vitals, above them or left of them ----

    // Zero to twelve chips (tg's five alerts, then slice E's status chips), in the place this screen and scale gives (above the doll when five
    // full-size chips fit there, else left of the pictures), the same place for one alert and for five: all there, square, on the screen, clear
    // of each other, of the version tag and the ping and FPS chip (the whole top band, as wide as the tag gets at a large scale), of the vitals,
    // of the hands' row with either hand active, of tg's entries still drawn (the Tab row with them), and of the widest prompt block beside the
    // crosshair, which ends before them; at full size above the doll, and left of it at full size while five fit, never under 24 screen pixels.
    private static void AlertStackIsClear(HudMetrics metrics, LiveHudLayout layout, List<(string loc, HudRect rect)> tg, string at)
    {
        float u = UiTokens.Unit(metrics), full = MathF.Round(UiTokens.AlertChip * u), stackGap = MathF.Round(UiTokens.AlertStackGap * u);
        var screen = new HudRect(0, 0, metrics.Width, metrics.Height);
        var others = new List<(string Name, HudRect Rect)>
        {
            ("version tag and ping chip", new HudRect(0, 0, metrics.Width, (UiTokens.CardTopRoom - UiTokens.ScreenMargin) * u)),
            ("doll", layout.Doll), ("health", layout.Health), ("mood", layout.Mood),
        };
        foreach (bool leftActive in new[] { true, false })
        {
            HandsLayout hands = LiveHud.Hands(metrics, leftActive, !leftActive);
            others.AddRange(new[] { ("left hand", hands.Left), ("right hand", hands.Right), ("hotbar", hands.Hotbar) });
            others.AddRange(Enumerable.Range(0, LiveHud.WornSlots).Select(i => ($"worn slot {i}", LiveHud.WornCell(hands, i, LiveHud.WornSlots)))); // under Tab
        }
        others.AddRange(tg.Select(t => (t.loc, t.rect)));
        bool above = LiveHud.AlertsAbove(metrics, layout);
        // left of the pictures: the doll, the widest of them
        float columnRight = layout.Doll.Left - stackGap;
        float least = Math.Min(full, UiTokens.AlertChipMinPixels);
        List<HudRect> all = LiveHud.AlertStack(metrics, LiveHud.MaxChips, layout);
        for (int count = 0; count <= LiveHud.MaxChips; count++)
        {
            List<HudRect> chips = LiveHud.AlertStack(metrics, count, layout);
            string where = $"{at}, {count} chips, {(above ? "above" : "left")}";
            Assert.Equal(count, chips.Count);
            // the widest prompt block beside the crosshair (three prompts and the stop-pulling line), cut before the vitals and the chips
            float edge = Math.Min(LiveHud.LeftOfVitals(layout, metrics), LiveHud.LeftOfAlerts(chips, metrics));
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
                    // full size, in the column on the axis or in a column left of that, over the doll
                    Assert.Equal(full, chip.Width);
                    Assert.True(chip.Right <= layout.Stack.Right && chip.Bottom <= layout.Doll.Top - stackGap, $"{where}: chip {i} {chip} not over the doll {layout.Doll}");
                }
                else
                {
                    // the places of the twelve, whatever the count: the first column's right edge the gap left of the doll, its top level with the
                    // doll's, tg's order down the column, then the next column left of it
                    Assert.Equal(all[i], chip);
                    Assert.True(chip.Right <= columnRight, $"{where}: chip {i} {chip} not left of the pictures {columnRight}");
                }
                // in both places tg's order down a column, then on in the next column to the left
                if (i > 0)
                    Assert.True(chips[i - 1].Right == chip.Right ? chips[i - 1].Bottom < chip.Top : chips[i - 1].Left > chip.Right, $"{where}: chip {i - 1} {chips[i - 1]} then {i} {chip}");
            }
            if (count == 0)
                continue;
            if (above)
            {
                // the first chips in the column centred on the full stack's axis (right-aligned with the reduced doll), its last the gap over the doll
                float firstLeft = layout.Vitals ? MathF.Round(LiveHud.Axis(layout) - full / 2f) : layout.Stack.Right - full;
                Assert.Equal(firstLeft, chips[0].Left);
                Assert.Equal(new HudRect(firstLeft, layout.Doll.Top - stackGap - full, full, full), chips.Last(c => c.Left == firstLeft));
                if (layout.Vitals)
                    Assert.InRange(chips[0].Left + full / 2f - LiveHud.Axis(layout), -0.5f, 0.5f);
                if (count <= 5)
                    Assert.All(chips, c => Assert.Equal(firstLeft, c.Left));
            }
            else
            {
                Assert.Equal(columnRight, chips[0].Right);
                Assert.Equal(Math.Max(layout.Doll.Top, MathF.Ceiling(UiTokens.CardTopRoom * u)), chips[0].Top);
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
            Assert.Equal(LiveHudRole.WornSlot, LiveHud.RoleOf(world, entry with { SlotId = 7 }));
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
        Assert.Equal(LiveHudRole.WornSlot, LiveHud.RoleOf(world, belt));
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
    public void AtTheReferenceSizeTheChipsStackOverTheDollOnItsAxisTheNewestNearest()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        float right = 1892, bottom = layout.Doll.Top - 6; // 1852..1892: centred on the stack's axis (1872), the doll's square 1824..1920
        Assert.Equal(1872f, LiveHud.Axis(layout));
        Assert.True(LiveHud.AlertsAbove(metrics, layout)); // five fit above the doll here
        // alert 3 (the newest) on the doll, alert 2 over it, alert 1 at the top: 40 pixels, 4 apart
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
        // at 250% on 1920x1080: a chip is 100 pixels, the top room 215; with the reduced arrangement (a doll alone, here a made-up 375 x 290) tg's
        // bottom-right rows start at 585, so the column ends at 570
        var metrics = new HudMetrics(1920, 1080, 2.5f);
        LiveHudLayout At(float panelTop) => new(new HudRect(1400, panelTop, 375, 290), 7);
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
        // the look moves the picture 32 tg pixels right. With the chips left of the doll (1920x1080 at 200 %) that is over the doll
        var metrics = new HudMetrics(1920, 1080, 2f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        Assert.False(LiveHud.AlertsAbove(metrics, layout));
        HudRect chip = LiveHud.AlertStack(metrics, 1, layout)[0], picture = LiveHud.AlertPicture(chip);
        float perPixel = picture.Width / HudMetrics.IconPixels;
        foreach (Affine slide in new[] { Affine.Translate(32, 0), Affine.Translate(9.6, 0) }) // its start, and 0.7 s in
        {
            HudLook tgs = HudLook.Of(AnimatedLook.None with { Transform = slide }, perPixel);
            Assert.True(tgs.MoveX > 0f);
            Assert.True(Overlaps(picture with { Left = picture.Left + tgs.MoveX }, layout.Doll) || slide.C < 32); // tg's slide would reach the doll
            // in a chip on the human HUD: not moved, the picture inside its chip
            HudLook shown = LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Alert, humanHud: true), tgs);
            Assert.Equal((0f, 0f), (shown.MoveX, shown.MoveY));
            Assert.True(Inside(picture with { Left = picture.Left + shown.MoveX, Top = picture.Top + shown.MoveY }, chip));
            Assert.Equal(tgs with { MoveX = 0f, MoveY = 0f }, shown); // nothing else of the look changes
            // the hunger, stamina, internals, suit cell, other status and sleep chips too
            foreach (LiveHudRole role in new[] { LiveHudRole.Hunger, LiveHudRole.Stamina, LiveHudRole.Internals, LiveHudRole.SpaceSuit, LiveHudRole.StatusOther, LiveHudRole.Sleep })
                Assert.Equal(shown, LiveHud.ChipLook(LiveHud.Placed(role, humanHud: true, vitals: true), tgs));
            // at tg's places (another HUD, the reduced column) tg's slide plays
            Assert.Equal(tgs, LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Alert, humanHud: false), tgs));
            Assert.Equal(tgs, LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Stamina, humanHud: true, vitals: false), tgs));
        }
    }

    [Fact]
    public void OnAPortraitScreenTheVitalsAndTheChipsStayOnTheScreen()
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
                    foreach (HudRect r in layout.Vitals ? new[] { layout.Doll, layout.Health, layout.Mood } : new[] { layout.Doll })
                        Assert.True(r.Width > 0 && Inside(r, screen), $"{at}: {r} off the screen");
                    if (!human)
                        continue;
                    foreach (HudRect chip in LiveHud.AlertStack(metrics, LiveHud.MaxChips, layout))
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
        // package 3: ten cells of 48, 4 apart in a group and 12 between the groups (7, 2 and 1): 10 x 48 + 7 x 4 + 2 x 12 = 532, centred, 16 over
        // the bottom edge
        Assert.Equal(new HudRect(960 - 266, 1080 - 16 - 48, 532, 48), left.Hotbar);
        Assert.Equal(new HudRect(960 - 266 - 8 - 72, 1080 - 16 - 72, 72, 72), left.Left);
        Assert.Equal(new HudRect(960 + 266 + 8, 1080 - 16 - 56, 56, 56), left.Right);
        float[] lefts = { 694, 746, 798, 850, 902, 954, 1006, 1066, 1118, 1178 }; // the belt's seven, the pockets' two, the ID slot
        for (int c = 1; c <= 10; c++)
            Assert.Equal(new HudRect(lefts[c - 1], 1016, 48, 48), LiveHud.BarCell(left, c));
        // under Tab tg's belt, back and suit-storage slots in a row of cells over the bar's right end, 4 over it
        Assert.Equal(new HudRect(1074, 964, 48, 48), LiveHud.WornCell(left, 0, 3));
        Assert.Equal(new HudRect(1126, 964, 48, 48), LiveHud.WornCell(left, 1, 3));
        Assert.Equal(new HudRect(1178, 964, 48, 48), LiveHud.WornCell(left, 2, 3));
        // a swap moves the highlight, not the bar
        Assert.Equal(left.Hotbar, right.Hotbar);
        Assert.Equal(new HudRect(960 - 266 - 8 - 56, 1080 - 16 - 56, 56, 56), right.Left);
        Assert.Equal(new HudRect(960 + 266 + 8, 1080 - 16 - 72, 72, 72), right.Right);
        // slice D: the hint strip over the row, centred on the gap; slice E: its bottom 6 over the row's top line, the active box's (992), the same
        // through a swap; it never moves the row, and there is none without the human HUD's vitals or without a width
        LiveHudLayout column = LiveHud.Layout(metrics, human: true);
        HandsLayout hinted = LiveHud.Hands(metrics, true, false, column, 300f);
        Assert.Equal(new HudRect(810, 954, 300, 32), hinted.Hint);
        Assert.Equal(1f, hinted.HintShare);
        Assert.Equal(hinted.Hint, LiveHud.Hands(metrics, false, true, column, 300f).Hint);
        Assert.Equal(hinted with { Hint = default, HintShare = 0f }, left);
        Assert.Equal(default, LiveHud.Hands(metrics, true, false, default, 300f).Hint);
        Assert.Equal(default, LiveHud.Hands(metrics, true, false, column, 0f).Hint);
        // package 3: under Tab there is no strip (the worn slots' row has its place), and the row is the same
        HandsLayout tab = LiveHud.Hands(metrics, true, false, column, 300f, name: 80f, tab: true);
        Assert.Equal(default, tab.Hint);
        Assert.Equal(0f, tab.HintName);
        Assert.Equal(left, tab);
    }

    [Fact]
    public void TheHeldItemsNameLeadsTheHintStripAndALongOneIsCutToTheRow()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout column = LiveHud.Layout(metrics, human: true);
        // a name 80 wide: the strip is the 300 of its picture and keys, a gap of 12 and the name, still centred on the bar, at its full size
        HandsLayout named = LiveHud.Hands(metrics, true, false, column, 300f, name: 80f);
        Assert.Equal(new HudRect(764, 954, 392, 32), named.Hint);
        Assert.Equal((1f, 80f), (named.HintShare, named.HintName));
        // no name: the strip as before
        HandsLayout unnamed = LiveHud.Hands(metrics, true, false, column, 300f);
        Assert.Equal((new HudRect(810, 954, 300, 32), 0f), (unnamed.Hint, unnamed.HintName));
        // too long a name is cut to the room the row leaves (both boxes at the active size: 532 + 2 x (8 + 72) = 692): the strip is as wide as the
        // row, never wider, and keeps its full size
        HandsLayout cut = LiveHud.Hands(metrics, true, false, column, 300f, name: 5000f);
        Assert.Equal(692f - 300f - 12f, cut.HintName);
        Assert.Equal(new HudRect(960 - 346, 954, 692, 32), cut.Hint);
        Assert.Equal(1f, cut.HintShare);
        // a strip that alone fills the row has no room for a name: it is shrunk or left out as before, the name never makes it smaller
        HandsLayout full = LiveHud.Hands(metrics, true, false, column, 800f, name: 80f);
        Assert.Equal(0f, full.HintName);
        Assert.Equal(LiveHud.Hands(metrics, true, false, column, 800f), full);
        // the text: tg's lower-case name capitalised, cut with an ellipsis to the width (here 10 pixels a letter); nothing for no name
        static float Measure(string s) => 10f * s.Length;
        Assert.Equal("Welding tool", LiveHud.HintName("welding tool", 120f, Measure));
        Assert.Equal("Welding…", LiveHud.HintName("welding tool", 90f, Measure)); // "Welding " less its space, then the ellipsis
        Assert.Equal("…", LiveHud.HintName("welding tool", 15f, Measure));
        Assert.Equal(string.Empty, LiveHud.HintName("welding tool", 5f, Measure));
        Assert.Null(LiveHud.HintName(null, 500f, Measure));
        Assert.Null(LiveHud.HintName(" ", 500f, Measure));
        Assert.Equal(80f, LiveHud.HintNameWidth(metrics, named, 300f, 79.4f)); // whole pixels
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
    public void AHandsOrAWornSlotsMaptextStandsOnItsSquareThereAndScalesWithIt(int width, int height, float scale)
    {
        // a stack's amount (tg's maptext at the icon's lower left) on an item in a hand or in a worn slot under Tab: on that box's square, at that
        // square's size (32 tg pixels across it), not at the HUD's tg scale of the entry's own place
        var metrics = new HudMetrics(width, height, scale);
        HandsLayout hands = LiveHud.Hands(metrics, true, false);
        Assert.Equal(LiveHud.HandPicture(hands.Left, hands.Unit), LiveHud.MaptextSquare(LiveHudRole.LeftHand, hands, 0, 3));
        Assert.Equal(LiveHud.HandPicture(hands.Right, hands.Unit), LiveHud.MaptextSquare(LiveHudRole.RightHand, hands, 0, 3));
        for (int cell = 0; cell < LiveHud.WornSlots; cell++)
        {
            HudRect slot = LiveHud.WornCell(hands, cell, LiveHud.WornSlots);
            Assert.Equal(slot, LiveHud.MaptextSquare(LiveHudRole.WornSlot, hands, cell, LiveHud.WornSlots));
            // the slot's tg picture is fitted to that cell (HudLayer.Place), so its icon square is the cell
            Assert.Equal(slot, ScreenLoc.FitIcon(slot, 32, 32, 0, 0));
            Assert.True(slot.Width / 32f < metrics.PixelScale, "the cell's scale is not the HUD's tg scale, which made the amount too large");
        }
        // an entry at its own place (or a slot of another HUD, which LiveHud.Placed keeps there) keeps the HUD's own square and scale; a bar slot's
        // amount is its cell's (drawn from the hotbar model), not its entry's
        foreach (LiveHudRole role in new[] { LiveHudRole.Tg, LiveHudRole.Doll, LiveHudRole.CombatToggle, LiveHudRole.ZoneSelector, LiveHudRole.InventoryToggle, LiveHudRole.BarSlot })
            Assert.Null(LiveHud.MaptextSquare(role, hands, 0, 3));
        Assert.Null(LiveHud.MaptextSquare(LiveHud.Placed(LiveHudRole.WornSlot, humanHud: false), hands, 0, 3));
    }

    // ---- slices D and E: the vitals ----

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
    public void TheIndicatorsArePlacedOnlyWithTheFullStackOfTheHumanHudTheStatusIndicatorsAsChips()
    {
        foreach (LiveHudRole role in new[] { LiveHudRole.Health, LiveHudRole.Mood })
        {
            Assert.Equal(role, LiveHud.Placed(role, humanHud: true, vitals: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: true, vitals: false));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false, vitals: true));
        }
        // slice E: the hunger bar and the sleep button are chips too
        foreach (LiveHudRole role in new[] { LiveHudRole.Hunger, LiveHudRole.Stamina, LiveHudRole.Internals, LiveHudRole.SpaceSuit, LiveHudRole.Sleep })
        {
            Assert.Equal(LiveHudRole.Alert, LiveHud.Placed(role, humanHud: true, vitals: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: true, vitals: false));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false, vitals: true));
        }
        // tg's alerts are chips with either arrangement
        Assert.Equal(LiveHudRole.Alert, LiveHud.Placed(LiveHudRole.Alert, humanHud: true, vitals: false));
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Hunger, LiveHud.Layout(new HudMetrics(1920, 1080, 1f), human: true)));
    }

    [Fact]
    public void TheStatusChipsTheHungerBarAmongThemShowOnlyWhileTgDrawsAPicture()
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
        // tg hides a fed human's hunger bar at alpha 0 with its "hungerbar" state kept (screen_objects.dm, screen/hunger update_appearance): no
        // chip then; a chip at alpha 0 is not shown either; one mid-fade is
        ScreenEntry Faded(string state, string loc, byte alpha) => new(9301, world.LocalAppearance(new AppearanceEntry
        {
            Icon = world.LocalString("icons/hud/screen_gen.dmi"), IconState = world.LocalString(state), Alpha = alpha,
        }), loc);
        bool All(string icon, string state) => true;
        ScreenEntry hunger = Faded("hungerbar", "EAST-1:2,CENTER:21", 0);
        Assert.Equal(LiveHudRole.Hunger, LiveHud.RoleOf(world, hunger));
        Assert.False(LiveHud.IndicatorShown(world, hunger, All));
        Assert.True(LiveHud.IndicatorShown(world, Faded("hungerbar", "EAST-1:2,CENTER:21", 255), All));
        Assert.False(LiveHud.IndicatorShown(world, Faded("spacesuit_high", "EAST-1:28,CENTER-4:14", 0), All));
        Assert.True(LiveHud.IndicatorShown(world, Faded("spacesuit_high", "EAST-1:28,CENTER-4:14", 128), All));
    }

    [Fact]
    public void AnyOtherEntryOfTgsRightStatusColumnIsAChipAfterTheSuitCellWithTheFullStack()
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
        Assert.Equal(LiveHudRole.WornSlot, LiveHud.RoleOf(world, lower with { SlotId = 5 }));
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
        // a chip with the full stack, tg's place with the reduced one or off the human HUD
        Assert.Equal(LiveHudRole.Alert, LiveHud.Placed(LiveHudRole.StatusOther, humanHud: true, vitals: true));
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.StatusOther, humanHud: true, vitals: false));
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.StatusOther, humanHud: false, vitals: true));
        // the order (slice E): tg's alerts, hunger, stamina, internals, suit cell, then these, tg's top to bottom, then sleep
        int[] order =
        {
            LiveHud.ChipOrder(LiveHudRole.Alert, Parse("EAST-1:28,CENTER+1:19")), LiveHud.ChipOrder(LiveHudRole.Hunger, default),
            LiveHud.ChipOrder(LiveHudRole.Stamina, default), LiveHud.ChipOrder(LiveHudRole.Internals, default), LiveHud.ChipOrder(LiveHudRole.SpaceSuit, default),
            LiveHud.ChipOrder(LiveHudRole.StatusOther, Parse(upper.ScreenLoc)), LiveHud.ChipOrder(LiveHudRole.StatusOther, Parse(lower.ScreenLoc)),
            LiveHud.ChipOrder(LiveHudRole.Sleep, Parse("EAST-1:28,SOUTH+1:41")),
        };
        Assert.Equal(order.OrderBy(o => o), order);
        Assert.Equal(order.Length, order.Distinct().Count());
    }

    [Fact]
    public void AtTheReferenceSizeTheVitalsStandBareAtTheRightEdgeOverTgsCombatToggle()
    {
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        Assert.True(layout.Vitals);
        Assert.Equal((3, 2), (layout.DollScale, layout.CellScale));
        // tg's combat toggle in the corner (ui_zonesel), its top at 996.75 here: the mood face's bottom 8 over the whole pixel above it (988)
        Assert.Equal(996.75f, ScreenLoc.Place(Parse("EAST-1:28,SOUTH:5"), metrics, 32, 32, 0, 0, out _).Top);
        // one axis (1872), the doll's square against the edge (1824..1920), 8 between pictures: the mood face and the health indicator at 2 (64, 1840..1904), the doll at 3 (96)
        Assert.Equal(new HudRect(1840, 924, 64, 64), layout.Mood);
        Assert.Equal(new HudRect(1840, 852, 64, 64), layout.Health);
        Assert.Equal(new HudRect(1824, 748, 96, 96), layout.Doll);
        Assert.Equal(new HudRect(1824, 748, 96, 240), layout.Stack);
        Assert.Equal(layout.Health, LiveHud.VitalSquare(LiveHudRole.Health, layout));
        Assert.Equal(layout.Mood, LiveHud.VitalSquare(LiveHudRole.Mood, layout)); // alone, hungry or not
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Stamina, layout)); // a chip
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Doll, layout));
        // the stack's size does not depend on which entries show: the layout has no input but the screen and the scale
        Assert.Equal(layout, LiveHud.Layout(metrics, human: true));
        // the HUDs that are not human keep the doll alone left of tg's right column
        Assert.False(LiveHud.Layout(metrics).Vitals);
        Assert.True(LiveHud.Layout(metrics).Doll.Right <= 1920 - 2 * metrics.Tile);
    }

    [Fact]
    public void WhereTheFullStackDoesNotFitTheReducedDollStandsLeftOfTgsRightColumn()
    {
        // a screen 200 lines high at 300 %: the three pictures at one tg pixel per pixel (32 + 32 + 32 and two gaps of 4) do not fit between the top
        // room (48) and the combat toggle (its top at 153.75)
        var metrics = new HudMetrics(1920, 200, 3f);
        Assert.Null(LiveHud.VitalsColumn(metrics));
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        float u = UiTokens.Unit(metrics);
        Assert.False(layout.Vitals);
        Assert.True(layout.Doll.Right <= 1920 - 2 * metrics.Tile - UiTokens.DollPanelGap * u + 1f);
        Assert.Equal(32f * layout.DollScale, layout.Doll.Width);
        Assert.Null(LiveHud.VitalSquare(LiveHudRole.Health, layout)); // tg's indicators stay at tg's places
        Assert.Equal(default, layout.Health);
        Assert.Equal(layout.Doll, layout.Stack);
        // and the full stack wherever it fits: every landscape screen the options offer at 300 %
        foreach ((int w, int h) in HandSizes)
            Assert.NotNull(LiveHud.VitalsColumn(new HudMetrics(w, h, 3f)));
    }

    [Fact]
    public void TheHintStripsWidthPictureAndFit()
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
        // wider than the row (692 at the reference size since package 3: the bar of ten cells and both boxes at the active size): smaller as one,
        // its text to 60 % of the prompts' at least (0.75 of the compact 80 %); below that left out
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout column = LiveHud.Layout(metrics, human: true);
        HandsLayout wide = LiveHud.Hands(metrics, true, false, column, 760f);
        Assert.Equal(692f / 760f, wide.HintShare, 4);
        Assert.InRange(wide.Hint.Width, 691f, 692f);
        Assert.Equal(MathF.Round(32f * 692f / 760f), wide.Hint.Height);
        Assert.Equal(0.75f, LiveHud.Hands(metrics, true, false, column, 692f / 0.75f).HintShare, 3);
        Assert.Equal(default, LiveHud.Hands(metrics, true, false, column, 692f / 0.74f).Hint);
    }

    [Fact]
    public void AboveTheDollChipsBeyondTheColumnGoOnInASecondColumn()
    {
        // room over the doll for exactly five: chips 0 to 4 (tg's alerts first) in the column at the edge, top down; the chips after them (the
        // internals and suit cell, 5 and 6) in the next column to the left, top down; every column stands on the doll, 6 over it
        var metrics = new HudMetrics(1920, 1080, 1f);
        float top = MathF.Ceiling(UiTokens.CardTopRoom);
        var layout = new LiveHudLayout(new HudRect(1784, top + 6 + 5 * 40 + 4 * 4, 128, 300), 3); // a made-up doll alone, its right edge at 1912
        Assert.True(LiveHud.AlertsAbove(metrics, layout));
        List<HudRect> chips = LiveHud.AlertStack(metrics, 7, layout);
        float bottom = layout.Doll.Top - 6;
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

        // left of the doll (1920x1080 at 250%, chips of 24, three to a column, 2 apart): chips 0 to 2 in the column next to the doll, top down
        // from the doll's top; 3 and 4 in the next column to the left, top-aligned with it
        var big = new HudMetrics(1920, 1080, 2.5f);
        var low = new LiveHudLayout(new HudRect(1400, 480, 375, 290), 7);
        Assert.False(LiveHud.AlertsAbove(big, low));
        List<HudRect> left = LiveHud.AlertStack(big, 5, low);
        (int Column, int Row)[] leftPlaces = { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1) };
        for (int i = 0; i < left.Count; i++)
        {
            (int column, int row) = leftPlaces[i];
            Assert.Equal(new HudRect(1385 - 24 - column * 26, 480 + row * 26, 24, 24), left[i]);
        }
    }

    // ---- slice E: tg's buttons that a key does, the Tab row, the menu keys ----

    [Fact]
    public void TgsButtonsOfTheHumanHudAreKnownByTheirIconStatesInAHudGroup()
    {
        (_, ReplicaWorld world) = Start();
        ScreenEntry Button(string state, byte group, string loc = "EAST-3:24,SOUTH+1:7") => new(9500, world.LocalAppearance(new AppearanceEntry
        {
            Icon = world.LocalString("icons/hud/screen_midnight.dmi"), IconState = world.LocalString(state),
        }), loc) { Group = group };
        foreach ((string state, LiveHudRole role) in new[]
        {
            ("pull", LiveHudRole.KeyButton), ("pull0", LiveHudRole.KeyButton), ("act_resist", LiveHudRole.KeyButton), ("act_rest", LiveHudRole.KeyButton),
            ("act_rest_on", LiveHudRole.KeyButton), ("act_throw", LiveHudRole.KeyButton), ("act_throw_on", LiveHudRole.KeyButton), ("running", LiveHudRole.KeyButton),
            ("walking", LiveHudRole.KeyButton), ("act_drop", LiveHudRole.KeyButton), ("act_swap", LiveHudRole.KeyButton), ("floor_change", LiveHudRole.KeyButton),
            ("floor_change_v", LiveHudRole.KeyButton), ("craft", LiveHudRole.Crafting), ("talk_wheel", LiveHudRole.Language), ("navigate", LiveHudRole.Navigate),
            ("memories", LiveHudRole.Memories), ("area_edit", LiveHudRole.AreaCreator), ("act_sleep", LiveHudRole.Sleep), ("act_sleep_on", LiveHudRole.Sleep),
        })
        {
            Assert.Equal(role, LiveHud.RoleOf(world, Button(state, HudGroup.Static)));
            Assert.Equal(role, LiveHud.RoleOf(world, Button(state, HudGroup.Hotkeys, "WEST,NORTH"))); // what it is, wherever tg puts it
            // in no HUD group (an alert's, say) or in a native window it is no button
            Assert.NotEqual(role, LiveHud.RoleOf(world, Button(state, HudGroup.None)));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, Button(state, HudGroup.Storage)));
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, Button(state, HudGroup.Static) with { ActionTarget = 4 }));
        }
        // the fake's human HUD has them at tg's places
        Assert.Equal(LiveHudRole.Crafting, LiveHud.RoleOf(world, world.Screen[FakeServer.CraftId]));
        Assert.Equal(LiveHudRole.Language, LiveHud.RoleOf(world, world.Screen[FakeServer.LanguageId]));
        Assert.Equal(LiveHudRole.Navigate, LiveHud.RoleOf(world, world.Screen[FakeServer.NavigateId]));
        Assert.Equal(LiveHudRole.Memories, LiveHud.RoleOf(world, world.Screen[FakeServer.MemoriesId]));
        Assert.Equal(LiveHudRole.AreaCreator, LiveHud.RoleOf(world, world.Screen[FakeServer.AreaCreatorId]));
        foreach (long id in new[] { FakeServer.FloorChangerId, FakeServer.PullButtonId, FakeServer.ResistId, FakeServer.RestId })
            Assert.Equal(LiveHudRole.KeyButton, LiveHud.RoleOf(world, world.Screen[id]));
    }

    [Fact]
    public void OnTheHumanHudTheButtonsAKeyDoesAreHiddenAndTheTabRowShowsUnderTabOnly()
    {
        // hidden: no picture, no pick, no tooltip, in first person and under Tab; on another HUD at tg's place
        foreach (LiveHudRole role in new[] { LiveHudRole.KeyButton, LiveHudRole.Crafting, LiveHudRole.Language, LiveHudRole.Navigate, LiveHudRole.InventoryToggle })
        {
            LiveHudRole placed = LiveHud.Placed(role, humanHud: true, vitals: true);
            Assert.Equal(LiveHudRole.Hidden, placed);
            Assert.Equal(placed, LiveHud.Placed(role, humanHud: true, vitals: false));
            Assert.False(LiveHud.Shown(placed, cursorMode: false));
            Assert.False(LiveHud.Shown(placed, cursorMode: true));
            Assert.Null(LiveHud.HumanPlace(placed));
            Assert.True(LiveHud.Shown(LiveHud.Placed(role, humanHud: false), cursorMode: false));
        }
        // the memories button and the area creator: under Tab only on the human HUD, always at tg's place on another
        foreach (LiveHudRole role in new[] { LiveHudRole.Memories, LiveHudRole.AreaCreator })
        {
            Assert.Equal(role, LiveHud.Placed(role, humanHud: true, vitals: true));
            Assert.False(LiveHud.Shown(role, cursorMode: false));
            Assert.True(LiveHud.Shown(role, cursorMode: true));
            Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(role, humanHud: false));
            Assert.True(LiveHud.Shown(LiveHud.Placed(role, humanHud: false), cursorMode: false));
        }
        // tg's combat toggle is drawn in first person and under Tab
        Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHudRole.CombatToggle, humanHud: true, vitals: true), cursorMode: false));
        Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHudRole.CombatToggle, humanHud: true, vitals: true), cursorMode: true));
        // the places: the combat toggle in the corner (ui_zonesel), the Tab row left of it at ui_movi, ui_acti and ui_human_floor_changer
        Assert.Equal(Parse("EAST-1:28,SOUTH:5"), LiveHud.HumanPlace(LiveHudRole.CombatToggle));
        Assert.Equal(Parse("EAST-2:26,SOUTH:5"), LiveHud.HumanPlace(LiveHudRole.ZoneSelector));
        Assert.Equal(Parse("EAST-3:24,SOUTH:5"), LiveHud.HumanPlace(LiveHudRole.Memories));
        Assert.Equal(Parse("EAST-4:22,SOUTH:5"), LiveHud.HumanPlace(LiveHudRole.AreaCreator));
        foreach (LiveHudRole role in new[] { LiveHudRole.Tg, LiveHudRole.Doll, LiveHudRole.Alert, LiveHudRole.LeftHand, LiveHudRole.Health })
            Assert.Null(LiveHud.HumanPlace(role));
        // one row at the reference size, left to right, none over another
        var metrics = new HudMetrics(1920, 1080, 1f);
        HudRect[] row = new[] { LiveHudRole.AreaCreator, LiveHudRole.Memories, LiveHudRole.ZoneSelector, LiveHudRole.CombatToggle }
            .Select(r => ScreenLoc.Place(LiveHud.HumanPlace(r).Value, metrics, 32, 32, 0, 0, out _)).ToArray();
        for (int i = 1; i < row.Length; i++)
        {
            Assert.Equal(row[0].Top, row[i].Top);
            Assert.True(row[i - 1].Right <= row[i].Left, $"{row[i - 1]} {row[i]}");
        }
    }

    [Fact]
    public void TheMenuKeysFindTgsButtonsAndNothingOnAHudWithoutThem()
    {
        (_, ReplicaWorld world) = Start();
        Assert.Equal(FakeServer.CraftId, LiveHud.ButtonEntry(world, PlayAction.CraftingMenu));
        Assert.Equal(FakeServer.LanguageId, LiveHud.ButtonEntry(world, PlayAction.LanguageMenu));
        Assert.Equal(FakeServer.NavigateId, LiveHud.ButtonEntry(world, PlayAction.Navigate));
        Assert.Equal(0, LiveHud.ButtonEntry(world, PlayAction.Resist)); // a key with no button of its own
        Assert.Null(LiveHud.ButtonOf(PlayAction.ToggleCombatMode));
        // a HUD without them (the fake without its human inventory HUD stands for another mob's): nothing to click
        var bare = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: false);
        new PlayRig(bare).Run(0.5);
        foreach (PlayAction action in new[] { PlayAction.CraftingMenu, PlayAction.LanguageMenu, PlayAction.Navigate })
            Assert.Equal(0, LiveHud.ButtonEntry(bare.Replica, action));
    }

    [Fact]
    public void TheStaminaChipShowsOnlyWhileStaminaIsDownAndTheChipsAfterItCloseUp()
    {
        // tg sets "stamina_full" (a blue bolt) after its first update with nothing lost (living.dm:1458-1470): a chip only from stamina_1 on
        (_, ReplicaWorld world) = Start();
        ScreenEntry Entry(long id, string icon, string state, string loc, byte group = HudGroup.Info) => new(id, world.LocalAppearance(new AppearanceEntry
        {
            Icon = world.LocalString(icon), IconState = world.LocalString(state),
        }), loc) { Group = group };
        bool All(string icon, string state) => true;
        ScreenEntry Stamina(string state) => Entry(9600, "icons/hud/screen_gen.dmi", state, "EAST-1:28,CENTER-3:14");
        Assert.Equal(LiveHudRole.Stamina, LiveHud.RoleOf(world, Stamina("stamina_full")));
        Assert.False(LiveHud.ChipShown(LiveHudRole.Stamina, world, Stamina("stamina_full"), All));
        foreach (string down in new[] { "stamina_1", "stamina_3", "stamina_5", "stamina_crit", "stamina_dead" })
            Assert.True(LiveHud.ChipShown(LiveHudRole.Stamina, world, Stamina(down), All), down);
        Assert.False(LiveHud.ChipShown(LiveHudRole.Stamina, world, Stamina("stamina0"), (_, s) => s != "stamina0")); // tg's first state has no art
        // the other indicators keep their rule; an alert always has its chip
        ScreenEntry internals = Entry(9601, "icons/hud/screen_gen.dmi", "internal1", "EAST-1:28,CENTER+1:21");
        Assert.True(LiveHud.ChipShown(LiveHudRole.Internals, world, internals, All));
        ScreenEntry alert = Entry(9602, "icons/hud/screen_alert.dmi", "not_enough_oxy", "EAST-1:28,CENTER+5:27", HudGroup.None);
        Assert.Equal(LiveHudRole.Alert, LiveHud.RoleOf(world, alert));
        Assert.True(LiveHud.ChipShown(LiveHudRole.Alert, world, alert, All));
        // the chips as HudLayer lists them (shown ones in tg's order): with full stamina the internals chip takes the place right after the alert
        var metrics = new HudMetrics(1920, 1080, 1f);
        LiveHudLayout layout = LiveHud.Layout(metrics, human: true);
        List<HudRect> Chips(ScreenEntry stamina)
        {
            var shown = new[] { alert, stamina, internals }.Where(e => LiveHud.ChipShown(LiveHud.RoleOf(world, e), world, e, All)).ToList();
            return LiveHud.AlertStack(metrics, shown.Count, layout);
        }
        Assert.Equal(2, Chips(Stamina("stamina_full")).Count);
        Assert.Equal(3, Chips(Stamina("stamina_3")).Count);
        Assert.Equal(LiveHud.AlertStack(metrics, 2, layout), Chips(Stamina("stamina_full")));
        // with the reduced arrangement or on another HUD tg's entry is no chip: it is drawn at tg's place in every state, stamina_full included
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.Stamina, humanHud: true, vitals: false));
        Assert.Equal(LiveHudRole.Tg, LiveHud.Placed(LiveHudRole.Stamina, humanHud: false, vitals: true));
        Assert.True(LiveHud.Shown(LiveHud.Placed(LiveHudRole.Stamina, humanHud: true, vitals: false), cursorMode: false));
    }

    [Fact]
    public void TheHungerChipComesAndGoesWithTgsFadeTargetAtFullOpacity()
    {
        // tg fades its hunger bar in and out over a second (screen_objects.dm:1237-1247); animate() sets the entry's alpha to the fade's end at
        // once (as BYOND does), so the entry's own alpha is where the fade is going and the animation chain where it is now
        (_, ReplicaWorld world) = Start();
        ScreenEntry Bar(byte target) => new(9700, world.LocalAppearance(new AppearanceEntry
        {
            Icon = world.LocalString("icons/hud/screen_gen.dmi"), IconState = world.LocalString("hungerbar"), Alpha = target,
        }), "EAST-1:2,CENTER:21") { Group = HudGroup.Info };
        bool All(string icon, string state) => true;
        Assert.Equal(LiveHudRole.Hunger, LiveHud.RoleOf(world, Bar(255)));
        // fading in (going to 255, at 0.2 of the way now): a chip, its picture at full opacity
        Assert.True(LiveHud.ChipShown(LiveHudRole.Hunger, world, Bar(255), All));
        HudLook midFade = HudLook.None with { Alpha = 0.2f };
        HudLook chip = LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Hunger, humanHud: true, vitals: true), midFade, LiveHudRole.Hunger);
        Assert.Equal(1f, chip.Alpha);
        // fading out (going to 0, still at 0.8 now): no chip from the start of the fade
        Assert.False(LiveHud.ChipShown(LiveHudRole.Hunger, world, Bar(0), All));
        // only the hunger chip: another chip keeps tg's alpha, and at tg's places (the reduced arrangement, another HUD) the bar keeps tg's fade
        Assert.Equal(0.2f, LiveHud.ChipLook(LiveHudRole.Alert, midFade, LiveHudRole.Internals).Alpha);
        Assert.Equal(0.2f, LiveHud.ChipLook(LiveHudRole.Alert, midFade).Alpha);
        Assert.Equal(0.2f, LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Hunger, humanHud: true, vitals: false), midFade, LiveHudRole.Hunger).Alpha);
        Assert.Equal(0.2f, LiveHud.ChipLook(LiveHud.Placed(LiveHudRole.Hunger, humanHud: false, vitals: true), midFade, LiveHudRole.Hunger).Alpha);
    }
}
