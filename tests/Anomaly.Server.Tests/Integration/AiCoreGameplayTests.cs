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
/// Gaps package C1 (code/datums/ai) on MetaStation through the real server and a client's own stack. A toolbox comes alive as a machine mimic in a hallway a few tiles from a
/// crewman: it sees him, walks up (the client's replica shows the entity moving) and hits him, with tg's attack verb in his chat and his brute damage rising. A telegraph box
/// reaches the client with tg's picture and goes away after its lifetime.
/// </summary>
public class AiCoreGameplayTests(ITestOutputHelper output)
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

    // a straight, clear stretch of a primary hallway: seven free tiles eastward
    private static turf HallwayStart()
    {
        for (int y = 1; y <= world.maxy; y++)
        {
            for (int x = 1; x <= world.maxx - 6; x++)
            {
                if (locate(x, y, 1) is not turf_open t || get_area(t) is not area_station_hallway_primary)
                    continue;
                bool clear = true;
                for (int i = 0; i <= 6 && clear; i++)
                {
                    turf step = locate(x + i, y, 1);
                    clear = step is turf_open && !Dm.truthy(step.density) && !Dm.each_cast<atom>(step.contents).Any(a => a is mob || Dm.truthy(a.density));
                }
                if (clear)
                    return t;
            }
        }
        return null;
    }

    [MetaServerFact]
    public void A_machine_mimic_chases_and_hits_a_crewman_and_the_client_sees_it_move()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 96, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // SSai_controllers and its twin run on Meta: nothing to do yet
        Assert.NotNull(In(w, () => (object)SSai_controllers));
        Assert.NotNull(In(w, () => (object)SSlow_priority_ai_controllers));

        turf start = In(w, HallwayStart);
        Assert.NotNull(start);
        Do(w, () => Waiting.NoSleep(me.forceMove(locate(start.x + 6, start.y, start.z))));
        mob_living_basic_mimic_copy_machine mimic = null;
        Do(w, () =>
        {
            var toolbox = Dm.@new<obj_item_storage_toolbox>(start);
            mimic = Dm.@new<mob_living_basic_mimic_copy_machine>(start, toolbox);
        });
        w.Run(2);
        Assert.False(InertSeam.IsInert(mimic));
        Assert.Equal(AI_STATUS_ON, In(w, () => ((datum_ai_controller)mimic.ai_controller).ai_status as string));
        float firstX = replica.Entities.Single(e => e.Id == mimic.dm_id).State.X;
        output.WriteLine($"the mimic at {In(w, () => mimic.x)}, the crewman at {In(w, () => me.x)}");

        // it walks up to him: the replica follows it
        float bruteBefore = In(w, () => Dm.num(me.get_brute_loss()));
        bool hit = false;
        for (int i = 0; i < 60 * Second && !hit; i++)
        {
            w.Run(1);
            hit = In(w, () => Dm.num(me.get_brute_loss())) > bruteBefore;
        }
        output.WriteLine($"after the chase: the mimic at {In(w, () => mimic.x)}, the crewman at {In(w, () => me.x)}, brute {In(w, () => Dm.num(me.get_brute_loss()))}");
        Assert.True(hit, "the mimic reached and hit the crewman");
        Assert.True(In(w, () => get_dist(mimic, me)) <= 1);
        Assert.NotEqual(firstX, replica.Entities.Single(e => e.Id == mimic.dm_id).State.X);

        // the telegraph reaches the client with tg's picture and lives its two seconds
        obj_effect_temp_visual_telegraphing_circle box = null;
        Do(w, () => box = Dm.@new<obj_effect_temp_visual_telegraphing_circle>(get_turf(me)));
        w.Run(4);
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == box.dm_id);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal("target_circle", replica.ResolveString(look.IconState));
        w.Run(4 * Second);
        Assert.DoesNotContain(replica.Entities, e => e.Id == box.dm_id);

        // the rough cost: the empty subsystems' fire and one idle creature's plan (a hostile proteon does not aggro on sight)
        Do(w, () =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                Waiting.NoSleep(SSpathfinder.fire());
                Waiting.NoSleep(SSlow_priority_ai_controllers.fire());
            }
            output.WriteLine($"pathfinder and low-priority fire, nothing to do: {clock.Elapsed.TotalMilliseconds * 1000 / 1000:F1} us a pair");
            var proteon = Dm.@new<mob_living_basic_construct_proteon_hostile>(get_turf(mimic));
            datum_ai_controller brain = (datum_ai_controller)proteon.ai_controller;
            clock.Restart();
            for (int i = 0; i < 1000; i++)
                brain.SelectBehaviors(0.25f);
            output.WriteLine($"an idle creature's SelectBehaviors: {clock.Elapsed.TotalMilliseconds * 1000 / 1000:F1} us a plan (4 plans a second)");
        });

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
