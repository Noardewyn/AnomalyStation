using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Xenobiology through the real server with a headless client: a joined scientist injects a grey slime's extract with plasma from a
/// syringe and a baby grey slime comes out of it (recipes/slime_extracts.dm:28); at Meta's slime management console she looks from its
/// camera eye (her client's eye entity), her keys move the eye, and its Pick up and Place buttons take a slime into
/// the console and spit it out where the eye looks.
/// </summary>
public class XenobiologyGameplayTests(ITestOutputHelper output)
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

    private static turf FreeBeside(mob who) => new[] { EAST, WEST, NORTH, SOUTH }.Select(d => get_step(who, d) as turf)
        .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)));

    private static long ButtonOf<T>(RealHeadlessWorld w, obj_machinery_computer_camera_advanced console, mob_living_carbon_human me) where T : datum_action
        => In(w, () => (Dm.locate<T>(console.actions).viewers[me.hud_used] as atom_movable_screen_movable_action_button).dm_id);

    private static void ClickScreen(RealHeadlessWorld.Player p, long id)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    [Fact]
    public void A_scientist_injects_a_grey_extract_with_plasma_and_a_baby_slime_comes_out()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13860, TimeoutSteps = 6000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var heard = new List<string>();
        alice.Client.ServerTextReceived += t => heard.Add(t.Html ?? "");
        w.Run(Second);
        var extract = In(w, () =>
        {
            var made = Dm.@new<obj_item_slime_extract_grey>(FreeBeside(me));
            var syringe = Dm.@new<obj_item_reagent_containers_syringe>(me.loc);
            Waiting.NoSleep(((datum_reagents)syringe.reagents).add_reagent(typeof(datum_reagent_toxin_plasma), 5));
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(syringe))));
            return made;
        });
        turf spot = In(w, () => extract.loc as turf);
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(On(extract), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        var slime = In(w, () => Dm.each<mob_living_basic_slime>(range(2, spot)).SingleOrDefault());
        Assert.NotNull(slime);
        Assert.True(In(w, () => slime.slime_type is datum_slime_type_grey && slime.life_stage == SLIME_LIFE_STAGE_BABY));
        Assert.True(In(w, () => QDELETED(extract)), "the spent extract is gone");
        // her client sees the slime and reads the reaction
        w.Run(Second / 2);
        Assert.True(alice.Session.Replica.TryGet(In(w, () => slime.dm_id), out _), "the slime is in her replica");
        Assert.Contains(heard, h => h.Contains("a new baby slime emerges from it!", StringComparison.Ordinal));
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(20)));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_scientist_drives_the_slime_consoles_eye_and_moves_a_slime_with_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13861, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);

        // Meta's lab: the console beside the monkey recycler; she stands at it, her hands empty, and clicks it
        var console = In(w, () => Dm.each<obj_machinery_computer_camera_advanced_xenobio>(Dm.world).First(c => c.connected_recycler_ref?.resolve() != null));
        Do(w, () =>
        {
            turf_open front = Dm.each<turf>(orange(1, console)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)));
            Waiting.NoSleep(me.forceMove(front));
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
        });
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var eye = In(w, () => console.eyeobj as mob_eye_camera_remote_xenobio);
        Assert.NotNull(eye);
        Assert.Same(eye, In(w, () => me.remote_control));
        string lab = In(w, () => Dm.str((get_area(console) as area).name));
        Assert.Contains("Xenobiology Lab", lab);
        Assert.Equal(lab, In(w, () => eye.allowed_area));
        w.Run(Second);
        Assert.Equal(In(w, () => eye.dm_id), alice.Session.Replica.EyeEntity);
        Assert.True(alice.Session.Replica.TryGet(In(w, () => eye.dm_id), out ReplicaEntity eyeEntity), "the eye is her client's");
        Assert.True(ObserverVision.IsCameraEye(eyeEntity.TypePath), eyeEntity.TypePath);

        // her keys move the eye, not her body (it keeps to the lab, xenobio_camera.dm:11: she tries each way until one leads on)
        turf start = In(w, () => eye.loc as turf);
        turf body = In(w, () => me.loc as turf);
        foreach (double yaw in new[] { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 })
        {
            alice.Yaw = yaw;
            alice.Keys = MoveKeys.Forward;
            w.Run(Second / 2);
            alice.Keys = MoveKeys.None;
            w.Run(4);
            if (!ReferenceEquals(start, In(w, () => eye.loc)))
                break;
        }
        output.WriteLine($"the eye from {In(w, () => start.x)},{In(w, () => start.y)} to {In(w, () => ((turf)eye.loc).x)},{In(w, () => ((turf)eye.loc).y)}");
        Assert.NotSame(start, In(w, () => eye.loc));
        Assert.Same(body, In(w, () => me.loc));
        // the lab or an area tg marks as xenobiology's (its maintenance, the pens)
        Assert.True(In(w, () => Dm.eq((get_area(eye) as area).name, lab) || Dm.truthy(Dm.bitand((get_area(eye) as area).area_flags, XENOBIOLOGY_COMPATIBLE))),
            In(w, () => Dm.str((get_area(eye) as area).name)));

        // over one of the pens' slimes, Pick up takes it into the console
        var slime = In(w, () => Dm.each<mob_living_basic_slime>(Dm.world).Where(sl => Dm.eq((get_area(sl) as area)?.name, lab) || Dm.truthy(Dm.bitand((get_area(sl) as area)?.area_flags ?? 0, XENOBIOLOGY_COMPATIBLE)))
            .OrderBy(sl => get_dist(sl, console)).First());
        Do(w, () =>
        {
            ADD_TRAIT(slime, TRAIT_IMMOBILIZED, "test");
            eye.setLoc(slime.loc);
        });
        w.Run(4);
        Assert.Same(In(w, () => slime.loc), In(w, () => eye.loc));
        ClickScreen(alice, ButtonOf<datum_action_innate_slime_pick_up>(w, console, me));
        w.Run(Second / 2);
        Assert.Same(console, In(w, () => slime.loc));
        Assert.True(In(w, () => me.hud_used.screen_objects[HUD_XENOBIO_CONSOLE] is atom_movable_screen_xenobio_console), "the console's HUD");

        // the eye goes back to where it started; Place spits the slime out there
        Do(w, () => eye.setLoc(start));
        w.Run(4);
        ClickScreen(alice, ButtonOf<datum_action_innate_slime_place>(w, console, me));
        w.Run(Second);
        Assert.Same(start, In(w, () => slime.loc));

        // End Camera View: her view is hers again
        ClickScreen(alice, ButtonOf<datum_action_innate_camera_off>(w, console, me));
        w.Run(Second / 2);
        Assert.Null(In(w, () => me.remote_control));
        Assert.Equal(0, alice.Session.Replica.EyeEntity);
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(40)));
        Assert.DoesNotContain("runtime", w.Err.ToString());
    }
}
