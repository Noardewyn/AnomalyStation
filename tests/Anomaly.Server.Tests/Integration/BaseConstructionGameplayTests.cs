using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner (2026-11-14): Meta's aux base construction console let him fly over the whole map and gave no way out. tg's console
/// (construction_console*.dm, construction_actions.dm) puts its drone in the aux base, keeps it there, builds with its internal RCD and
/// places fans and turrets, and its Log out button gives the view back.
/// </summary>
public class BaseConstructionGameplayTests(ITestOutputHelper output)
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

    private static long ButtonOf<T>(RealHeadlessWorld w, obj_machinery_computer_camera_advanced console, mob_living_carbon_human me) where T : datum_action
        => In(w, () => (Dm.locate<T>(console.actions).viewers[me.hud_used] as atom_movable_screen_movable_action_button).dm_id);

    private static void ClickScreen(RealHeadlessWorld.Player p, long id)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    [MetaServerFact]
    public void The_aux_base_console_builds_inside_the_aux_base_and_logs_out()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 49, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);

        var console = In(w, () => Dm.each<obj_machinery_computer_camera_advanced_base_construction_aux>(Dm.world).Single());
        Assert.Equal(4f, In(w, () => Dm.num(console.structures["fans"])));
        Assert.Equal(4, In(w, () => Dm.each<datum_action_innate_construction>(console.actions).Count())); // besides Log out and the levels' up and down
        Do(w, () =>
        {
            turf_open front = Dm.each<turf>(orange(1, console)).OfType<turf_open>().First(t => !Dm.truthy(t.density));
            Waiting.NoSleep(me.forceMove(front));
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
        });
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var eye = In(w, () => console.eyeobj as mob_eye_camera_remote_base_construction);
        Assert.NotNull(eye);
        Assert.Same(eye, In(w, () => me.remote_control));
        bool InBase() => In(w, () => get_area(eye) is area_shuttle_auxiliary_base);
        Assert.True(InBase(), "the drone starts in the aux base");
        Assert.Equal(0f, In(w, () => eye.invisibility)); // seen while in use

        // the keys move the drone, and never out of the aux base
        foreach (double yaw in new[] { 0, Math.PI / 2, Math.PI, -Math.PI / 2 })
        {
            alice.Yaw = yaw;
            alice.Keys = MoveKeys.Forward;
            w.Run(Second * 2);
            alice.Keys = MoveKeys.None;
            w.Run(4);
            Assert.True(InBase(), "still in the aux base after moving toward yaw " + yaw);
        }

        // a tiny fan placed where the drone is
        Do(w, () => eye.setLoc(Dm.each<turf_open>(get_area_turfs(typeof(area_shuttle_auxiliary_base)) as DmList).First(t => !Dm.truthy(t.density)
            && !Dm.truthy(Waiting.NoSleep(t.is_blocked_turf())))));
        turf spot = In(w, () => eye.loc as turf);
        ClickScreen(alice, ButtonOf<datum_action_innate_construction_place_structure_fan>(w, console, me));
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.locate<obj_structure_fans_tiny>(spot.contents) != null), "a fan on the drone's tile");
        Assert.Equal(3f, In(w, () => Dm.num(console.structures["fans"])));

        // Log out: her view is hers again, the drone hidden
        ClickScreen(alice, ButtonOf<datum_action_innate_camera_off_base_construction>(w, console, me));
        w.Run(Second / 2);
        Assert.Null(In(w, () => me.remote_control));
        Assert.Same(me, In(w, () => me.client.eye));
        Assert.NotEqual(0f, In(w, () => eye.invisibility));
        output.WriteLine("server errors at the end:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(40)));
        Assert.DoesNotContain("runtime", w.Err.ToString());
    }
}
