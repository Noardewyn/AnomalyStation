using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8z slice D (D234, D237, D240, D241, D244, interactions.md) on MetaStation through the real server and the clients' own stacks:
/// Alice points at a chair with shift-middle-click (Carol reads the line and her replica holds tg's arrow for 2.5 s), and at the pen in
/// her own hand (the thought bubble, no arrow); Bob, pulled by Alice, presses the resist key and is free; Alice rests by the HUD button
/// (the sleep button shows), sleeps by it and by the radial's Sleep verb (tg's Yes/No alert, then "You are already sleeping!"); a healthy
/// Bob cannot succumb, in crit he succumbs by his alert's prompt.
/// </summary>
public class PointVerbHudGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static List<turf> OpenTiles(RealHeadlessWorld w, int x, int y, int z, int count) => In(w, () =>
    {
        var found = new List<turf>();
        for (int radius = 0; radius < 6 && found.Count < count; radius++)
        {
            for (int dx = -radius; dx <= radius && found.Count < count; dx++)
            {
                for (int dy = -radius; dy <= radius && found.Count < count; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;
                    if (locate(x + dx, y + dy, z) is turf_open floor && !Dm.truthy(floor.density) && !Dm.each_cast<atom>(floor.contents).Any(a => Dm.truthy(a.density) || a is obj_structure_chair))
                        found.Add(floor);
                }
            }
        }
        return found;
    });

    // Answers the open tgui alert of a player with a button.
    private static void Choose(RealHeadlessWorld.Player p, string button)
    {
        UiWindowState alert = Assert.Single(p.Session.Replica.UiWindows.Values, v => v.Interface == "AlertModal");
        Assert.True(p.Session.UiAct(alert.Window, "choose", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("choice", UiValue.Of(button)) })));
    }

    [MetaServerFact]
    public void Alice_points_Bob_resists_by_key_Alice_rests_and_sleeps_by_button_and_verb_and_Bob_succumbs_in_crit()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 103, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var aliceTexts = new List<ServerText>();
        var bobTexts = new List<ServerText>();
        var carolTexts = new List<ServerText>();
        alice.Client.ServerTextReceived += aliceTexts.Add;
        bob.Client.ServerTextReceived += bobTexts.Add;
        carol.Client.ServerTextReceived += carolTexts.Add;
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        int z = In(w, () => (int)aliceMob.z);
        string aliceName = In(w, () => aliceMob.name);

        // the three in the medbay lobby by its chairs, none of them buckled
        List<turf> tiles = OpenTiles(w, 94, 104, z, 3);
        Assert.Equal(3, tiles.Count);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(tiles[0]));
            Waiting.NoSleep(bobMob.forceMove(tiles[1]));
            Waiting.NoSleep(carolMob.forceMove(tiles[2]));
        });
        w.Run(Second);
        var chair = In(w, () => Dm.each_cast<atom>(range(3, aliceMob)).OfType<obj_structure_chair>().First());

        // 1. shift-middle-click on the chair: the line to the watcher, tg's arrow on her replica, gone after 2.5 s
        Assert.True(alice.Session.Click(On(chair), ClickButton.Middle, ClickModifiers.Shift));
        w.Run(4);
        Assert.Contains(carolTexts, t => t.Html.Contains(aliceName + "</span> points at", StringComparison.Ordinal));
        Assert.Contains(aliceTexts, t => t.Html.Contains("You point at", StringComparison.Ordinal));
        var arrow = In(w, () => Dm.each_cast<atom>(range(1, aliceMob)).OfType<obj_effect_temp_visual_point>().SingleOrDefault());
        Assert.NotNull(arrow);
        Assert.Equal("arrow", In(w, () => arrow.icon_state));
        Assert.True(carol.Session.Replica.TryGet(arrow.dm_id, out _), "the watcher's replica holds the arrow");
        {
            Assert.True(carol.Session.Replica.TryGet(arrow.dm_id, out ReplicaEntity seen));
            Assert.True(carol.Session.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
            (int dx, int dy) = In(w, () => ((int)(chair.x - aliceMob.x) * 32, (int)(chair.y - aliceMob.y) * 32));
            Assert.Equal((dx, dy), (look.PixelX, look.PixelY)); // the arrow's end over the chair (tg's animate), where the client draws it
        }
        w.Run(3 * Second);
        Assert.False(carol.Session.Replica.TryGet(arrow.dm_id, out _), "the arrow is gone after 2.5 s");

        // 1b. DV232: with the click's aim the arrow carries the point the aim met, for every watcher: the chair's picture where the ray
        // through its middle at 10 pixels crosses it, and a floor point off a tile's centre
        (int ax, int ay) = In(w, () => ((int)aliceMob.x, (int)aliceMob.y));
        ShotAim AimAt(double x, double y, double h)
        {
            const double eye = 20; // tile pixels over the floor
            double metres = TileSpace.TileSize / 32.0;
            double dx = (x - ax * 32) * metres, dy = (y - ay * 32) * metres, dh = (h - eye) * metres, length = Math.Sqrt(dx * dx + dy * dy + dh * dh);
            return PlaySession.AimOf(new PickRay((ax - 0.5) * TileSpace.TileSize, (ay - 0.5) * TileSpace.TileSize, eye * metres, z, dx / length, dy / length, dh / length, 30));
        }
        ShotState PointOf(RealHeadlessWorld.Player clicker, ClickTarget target, ShotAim aim)
        {
            Assert.True(clicker.Session.Click(target, ClickButton.Middle, ClickModifiers.Shift, aim));
            w.Run(4);
            var mark = In(w, () => Dm.each_cast<atom>(range(1, aliceMob)).OfType<obj_effect_temp_visual_point>().Single());
            Assert.True(carol.Session.Replica.TryGet(mark.dm_id, out ReplicaEntity seen));
            Assert.True(seen.Shot.HasValue, "the arrow carries the aimed point");
            w.Run(3 * Second);
            return seen.Shot.Value;
        }
        (int cx, int cy) = In(w, () => ((int)chair.x * 32, (int)chair.y * 32));
        ShotState onChair = PointOf(alice, On(chair), AimAt(cx, cy, 10));
        Assert.InRange(onChair.OriginX, cx - 1, cx + 1);
        Assert.InRange(onChair.OriginY, cy - 1, cy + 1);
        Assert.InRange(onChair.HeightPixels, 9, 11);
        turf floor = OpenTiles(w, ax, ay, z, 5).Last();
        (int fx, int fy) = In(w, () => ((int)floor.x * 32, (int)floor.y * 32));
        ShotState onFloor = PointOf(alice, new ClickTarget(TargetKind.Turf, 0, fx / 32, fy / 32, z, 21, 9), AimAt(fx + 5, fy - 7, 0));
        Assert.InRange(onFloor.OriginX, fx + 4, fx + 6);
        Assert.InRange(onFloor.OriginY, fy - 8, fy - 6);
        Assert.InRange(onFloor.HeightPixels, 0, 0.5);
        // a wall: the point on the face of its tile the ray from the eye to its middle enters
        turf wall = In(w, () => Dm.each_cast<atom>(view(7, aliceMob)).OfType<turf_closed>().OrderBy(t => Math.Abs(Dm.num(t.x) - ax) + Math.Abs(Dm.num(t.y) - ay)).First());
        (int wx, int wy) = In(w, () => ((int)wall.x * 32, (int)wall.y * 32));
        ShotState onWall = PointOf(alice, new ClickTarget(TargetKind.Turf, 0, wx / 32, wy / 32, z, 16, 16), AimAt(wx, wy, 16));
        Assert.InRange(Math.Max(Math.Abs(onWall.OriginX - wx), Math.Abs(onWall.OriginY - wy)), 15, 16);
        Assert.InRange(onWall.HeightPixels, 15, 21);

        // 2. the pen in her own hand: the thought bubble over her, no arrow
        var pen = In(w, () => Dm.@new<obj_item_pen>(aliceMob.loc as atom));
        Do(w, () => Waiting.NoSleep(aliceMob.put_in_hands(pen)));
        w.Run(3 * Second); // the point verb's emote cooldown aside, tg's next point
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, pen.dm_id, 0, 0, 0, 16, 16), ClickButton.Middle, ClickModifiers.Shift));
        w.Run(4);
        Assert.True(In(w, () => Dm.truthy(aliceMob.update_overlays_on_z) && aliceMob.update_overlays_on_z.Count > 0), "the thought bubble is on her");
        Assert.Empty(In(w, () => Dm.each_cast<atom>(range(1, aliceMob)).OfType<obj_effect_temp_visual_point>().ToList()));
        w.Run(3 * Second);
        Assert.True(In(w, () => !Dm.truthy(aliceMob.update_overlays_on_z) || aliceMob.update_overlays_on_z.Count == 0), "the bubble is cleared");

        // 3. Bob, pulled by Alice, presses B: tg's resist keybinding breaks the passive pull
        Do(w, () => Waiting.NoSleep(aliceMob.start_pulling(bobMob)));
        w.Run(4);
        Assert.Same(bobMob, In(w, () => aliceMob.pulling));
        bob.Session.Keybinding(KeybindingCode.Resist);
        w.Run(4);
        bob.Session.Keybinding(KeybindingCode.Resist, up: true);
        w.Run(4);
        Assert.Null(In(w, () => aliceMob.pulling));

        // 4. the rest button: Alice lies down, the sleep button shows; the sleep button asks, Yes: asleep
        long rest = In(w, () => ((datum)aliceMob.hud_used.screen_objects[HUD_MOB_REST]).dm_id);
        long sleepButton = In(w, () => ((datum)aliceMob.hud_used.screen_objects[HUD_MOB_SLEEP]).dm_id);
        Assert.Equal(INVISIBILITY_ABSTRACT, In(w, () => ((atom)aliceMob.hud_used.screen_objects[HUD_MOB_SLEEP]).invisibility));
        Assert.True(alice.Session.Click(Screen(rest), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(aliceMob.resting)));
        Assert.Equal("act_rest_on", In(w, () => ((atom)aliceMob.hud_used.screen_objects[HUD_MOB_REST]).icon_state));
        Assert.Equal(0, In(w, () => ((atom)aliceMob.hud_used.screen_objects[HUD_MOB_SLEEP]).invisibility));
        Assert.True(alice.Session.Click(Screen(sleepButton), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Choose(alice, "Yes");
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(aliceMob.IsSleeping())), "asleep by the button");
        Do(w, () => Waiting.NoSleep(aliceMob.SetSleeping(0)));
        w.Run(Second);

        // 5. the radial's Sleep verb: tg's alert, Yes: asleep; again: "You are already sleeping!"
        Assert.Contains(RadialModel.Build(null, 0, false), e => e.Kind == RadialKind.Sleep);
        alice.Session.Keybinding(KeybindingCode.VerbSleep);
        w.Run(Second / 2);
        Choose(alice, "Yes");
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(aliceMob.IsSleeping())), "asleep by the verb");
        alice.Session.Keybinding(KeybindingCode.VerbSleep);
        w.Run(Second / 2);
        Assert.Contains(aliceTexts, t => t.Html.Contains("You are already sleeping!", StringComparison.Ordinal));

        // 6. Succumb: healthy, tg's refusal; in crit, his alert's prompt (the last words empty) kills him
        bob.Session.Keybinding(KeybindingCode.VerbSuccumb);
        w.Run(Second / 2);
        Assert.Contains(bobTexts, t => t.Html.Contains("You are unable to succumb to death! This life continues.", StringComparison.Ordinal));
        Do(w, () => Waiting.NoSleep(bobMob.adjust_brute_loss(150)));
        w.Run(Second);
        Assert.True(In(w, () => Anomaly.Core.Tg.GlobalProcs.CAN_SUCCUMB(bobMob)), "in crit, Bob may succumb"); // soft or hard crit
        long succumbAlert = In(w, () => ((datum)bobMob.alerts[ALERT_SUCCUMB]).dm_id);
        Assert.True(bob.Session.Click(Screen(succumbAlert), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState prompt = Assert.Single(bob.Session.Replica.UiWindows.Values, v => v.Interface == "TextInputModal");
        Assert.True(bob.Session.UiAct(prompt.Window, "submit", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("entry", UiValue.Of("")) })));
        w.Run(Second);
        Assert.Equal(DEAD, In(w, () => bobMob.stat));
        Assert.Contains(bobTexts, t => t.Html.Contains("You have given up life and succumbed to death.", StringComparison.Ordinal));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        output.WriteLine("point, resist key, rest and sleep buttons, Sleep and Succumb ok");
    }
}
