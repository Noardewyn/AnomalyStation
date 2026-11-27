using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate N1c (D316) through the real server on MetaStation: the infiltrator's navigation computer. Clicked, it gives the operative a
/// camera eye over the station's level, which her client looks from (DV382: the private block's eye, the eye entity, the landing zone's
/// images) and her movement keys move (tg's relaymove), not her body. Over a clear spot in space its Place button designates the
/// custom landing spot; its End Camera View button gives her view back; the infiltrator's console then flies the ship to that spot.
/// </summary>
public class NavigationComputerGameplayTests(ITestOutputHelper output)
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

    private static void RunUntil(RealHeadlessWorld w, Func<bool> done, int seconds)
    {
        for (int i = 0; i < seconds * 4 && !In(w, done); i++)
            w.Run(Second / 4);
    }

    private static string DockedAt(obj_docking_port_mobile ship) => ship.get_docked() is obj_docking_port_stationary dock ? Dm.str(dock.shuttle_id) : null;

    private static long ButtonOf<T>(RealHeadlessWorld w, obj_machinery_computer_camera_advanced console, mob_living_carbon_human me) where T : datum_action
        => In(w, () => (Dm.locate<T>(console.actions).viewers[me.hud_used] as atom_movable_screen_movable_action_button).dm_id);

    private static void ClickScreen(RealHeadlessWorld.Player p, long id)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    [MetaServerFact]
    public void The_navigation_computer_designates_a_landing_spot_that_the_infiltrator_flies_to()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 49, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);
        // the boot runs on to its last template loads (a Lavaland ruin's modular rooms): its runtimes are their open items, not this test's
        w.Run(Second);
        int bootErr = w.Err.ToString().Length;

        // the base and the infiltrator docked at it (as NukieGameplayTests)
        Do(w, () => Waiting.Detach(SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE)));
        RunUntil(w, () => SSshuttle.getDock("syndicate_away") != null, 60);
        var leader = In(w, () => Dm.@new<datum_antagonist_nukeop_leader>());
        Do(w, () => Waiting.Detach(me.mind.add_antag_datum(leader)));
        var ship = In(w, () => SSshuttle.getShuttle("syndicate") as obj_docking_port_mobile);
        Assert.NotNull(ship);
        RunUntil(w, () => DockedAt(ship) == "syndicate_away", 120);
        Assert.Equal("syndicate_away", In(w, () => DockedAt(ship)));

        // the navigation computer on the infiltrator's bridge: she stands at it, her hands empty, and clicks it
        var nav = In(w, () => Dm.each<obj_machinery_computer_camera_advanced_shuttle_docker_syndicate>(Dm.world).Single());
        Assert.Equal("syndicate", In(w, () => nav.shuttleId));
        Do(w, () =>
        {
            turf_open front = Dm.each<turf>(orange(1, nav)).OfType<turf_open>().First(t => !Dm.truthy(t.density));
            Waiting.NoSleep(me.forceMove(front));
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
        });
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(CryoGameplayTests.On(nav), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var eye = In(w, () => nav.eyeobj as mob_eye_camera_remote_shuttle_docker);
        Assert.NotNull(eye);
        Assert.Same(eye, In(w, () => me.remote_control));
        Assert.Same(eye, In(w, () => me.client.eye));
        // locked to the station's level, the eye starts over its middle
        Assert.True(In(w, () => Dm.truthy(is_station_level(eye.z))), "over the station");

        // her client looks from it: the private block names the eye, the replica holds the eye (a camera eye), and the landing zone's
        // tiles are her images
        w.Run(Second);
        Assert.Equal(In(w, () => eye.dm_id), alice.Session.Replica.EyeEntity);
        Assert.True(alice.Session.Replica.TryGet(In(w, () => eye.dm_id), out ReplicaEntity eyeEntity), "the eye is her client's");
        Assert.True(ObserverVision.IsCameraEye(eyeEntity.TypePath), eyeEntity.TypePath);
        int tiles = In(w, () => (int)eye.placement_images.len);
        output.WriteLine($"landing zone: {tiles} tiles, {In(w, () => eye.extra_images.len)} icons; the client holds {alice.Session.Replica.Images.Count} images");
        Assert.True(tiles > 50, "the infiltrator's footprint");
        Assert.True(alice.Session.Replica.Images.Count >= tiles);

        // her keys move the eye, not her body
        turf start = In(w, () => eye.loc as turf);
        turf body = In(w, () => me.loc as turf);
        alice.Yaw = 0; // north
        alice.Keys = MoveKeys.Forward;
        w.Run(Second / 2);
        alice.Keys = MoveKeys.None;
        w.Run(4);
        Assert.NotSame(start, In(w, () => eye.loc));
        Assert.True(In(w, () => ((turf)eye.loc).y) > In(w, () => start.y), "north");
        Assert.Same(body, In(w, () => me.loc));

        // over a clear spot in space (the first one the console finds, scanning the level's south-west), Place designates it
        Do(w, () =>
        {
            for (int x = 30; x < 200; x += 4)
                for (int y = 20; y < 120; y += 4)
                {
                    eye.setLoc(locate(x, y, eye.z));
                    if (Dm.eq(nav.checkLandingSpot(), SHUTTLE_DOCKER_LANDING_CLEAR))
                        return;
                }
        });
        Assert.Equal(SHUTTLE_DOCKER_LANDING_CLEAR, In(w, () => Dm.num(nav.checkLandingSpot())));
        (float ex, float ey, float ez) = In(w, () => (eye.x, eye.y, eye.z));
        output.WriteLine($"clear spot {ex},{ey},{ez}");
        ClickScreen(alice, ButtonOf<datum_action_innate_shuttledocker_place>(w, nav, me));
        w.Run(Second / 2);
        var spot = In(w, () => nav.my_port);
        Assert.NotNull(spot);
        Assert.Equal("syndicate_custom", In(w, () => Dm.str(spot.shuttle_id)));
        Assert.Same(In(w, () => locate(ex - nav.x_offset, ey - nav.y_offset, ez)), In(w, () => spot.loc));
        Assert.Contains(spot, In(w, () => Dm.each<obj_docking_port_stationary>(SSshuttle.stationary_docking_ports).ToList()));
        Assert.Equal(tiles, In(w, () => (int)eye.placed_images.len));

        // End Camera View: her view is hers again
        ClickScreen(alice, ButtonOf<datum_action_innate_camera_off>(w, nav, me));
        w.Run(Second / 2);
        Assert.Null(In(w, () => me.remote_control));
        Assert.Same(me, In(w, () => me.client.eye));
        Assert.Equal(0, alice.Session.Replica.EyeEntity);

        // the infiltrator's console flies the ship to the designated spot
        var console = In(w, () => Dm.each<obj_machinery_computer_shuttle_syndicate>(Dm.world).First(c => c is not obj_machinery_computer_shuttle_syndicate_recall
            && c is not obj_machinery_computer_shuttle_syndicate_drop_pod && Dm.eq(c.shuttleId, "syndicate")));
        Do(w, () => Waiting.NoSleep(me.forceMove(Dm.each<turf>(orange(1, console)).OfType<turf_open>().First(t => !Dm.truthy(t.density)))));
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.First(x => x.Interface == "ShuttleConsole");
        window.Context.Act("move", ("shuttle_id", "syndicate_custom"));
        RunUntil(w, () => DockedAt(ship) == "syndicate_custom", 180);
        Assert.Equal("syndicate_custom", In(w, () => DockedAt(ship)));
        Assert.Same(spot, In(w, () => ship.get_docked()));
        output.WriteLine("server errors at the end:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(40)));
        Assert.DoesNotContain("runtime", w.Err.ToString()[bootErr..]);
    }
}
