using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 6, slice B: which screen entries the live HUD re-draws (the doll, the combat toggle) or hides (tg's zone selector in the
/// captured view), combat mode read off the replica, the doll panel and the combat pill placed clear of tg's right-hand column at every HUD
/// scale, and clicks on the re-placed doll and on the pill reaching tg's entries with tg's icon pixels.
/// </summary>
public class LiveHudTests
{
    private const long DollId = 1001, HealthId = 1002, IntentId = 1004, HandLeftId = 2001, ThrowId = 2005, FirstSlotId = 2010;

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
        foreach (long id in new[] { HealthId, IntentId, HandLeftId, ThrowId, FirstSlotId })
            Assert.Equal(LiveHudRole.Tg, LiveHud.RoleOf(world, world.Screen[id]));
        // every other entry of the fake's HUD stays at tg's place
        Assert.Equal(3, world.Screen.Values.Count(e => LiveHud.RoleOf(world, e) != LiveHudRole.Tg));
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
}
