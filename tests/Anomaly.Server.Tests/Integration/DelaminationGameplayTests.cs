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
/// The M7 delamination slice (G6, D163) through the real server on MetaStation's engine: the crystal, with its default explosive
/// strategy, is kept past its explosion point (its damage edited as tg's admins do in view variables), so its countdown starts
/// (supermatter.dm:337) and runs out (:637). The delamination irradiates and demoralizes the station (its sound is pinned by
/// DelaminationTests), scatters ten anomalies over MetaStation's event spawns (four at once) and explodes with calculate_explosion's ranges:
/// the client sees the crystal go, the engine's walls and floors torn up and the anomalies appear, and the engineer eight tiles
/// away is hurt. No new runtime. The client is not sent the delamination's charge.ogg: tg plays it with no volume (effect_demoralize's
/// playsound_local without vol, delamination_effects.dm:38, so sound.dm:110 sets null and :145 makes it 0), and the server's sound
/// budget drops the quietest plays of the blast's tick first; the crystal's alarms wait SUPERMATTER_WARNING_DELAY of REALTIMEOFDAY
/// (_sm_delam.dm:48), which DV3 starts at midnight, so a crystal delaminating in the round's first minute sounds none.
/// A forced singularity delamination: the client sees the singularity and the engine room's loose items pulled into it.
/// </summary>
public class DelaminationGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    [MetaServerFact]
    public void A_crystal_kept_past_its_explosion_point_counts_down_and_explodes_and_the_client_sees_the_blast_and_the_anomalies()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true) /* the anomalies land all over the station */, strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).Where(c => Dm.truthy(is_station_level(c.z))).ToList()));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        long smId = In(w, () => sm.dm_id);
        Assert.Contains(replica.Entities, e => e.Id == smId);
        Assert.True(In(w, () => GLOB.generic_event_spawns.len) >= 10, "MetaStation maps its generic event spawns (landmarks.dm:473)");
        Assert.IsType<datum_sm_delam_explosive>(In(w, () => sm.delamination_strategy));
        // the engineer stands eight tiles north of the crystal, inside the heavy range of 35 * 0.205 + 2 (delamination_effects.dm:84)
        mob_living engineer = w.MobOf(p);
        In(w, () => Waiting.NoSleep(engineer.forceMove(locate(smx, smy + 8, 1))));
        w.Run(Second);
        var before = new Dictionary<(int, int), string>();
        for (int x = smx - 3; x <= smx + 3; x++)
            for (int y = smy - 3; y <= smy + 3; y++)
                before[(x, y)] = replica.TurfTypeAt(x, y, 1);
        float healthBefore = In(w, () => Dm.num(engineer.health));

        // kept past its explosion point (it heals while cool): the countdown starts at the next cycle and runs its 15 seconds
        bool counted = false;
        for (int t = 0; t < 30 * Second && In(w, () => !QDELETED(sm)); t++)
        {
            if (t % Second == 0) // as tg's admins edit its damage in view variables (the countdown starts above 100, supermatter.dm:337)
                In(w, () => sm.damage = Dm.num(sm.explosion_point) + 10);
            w.Run(1);
            counted |= In(w, () => !QDELETED(sm) && Dm.truthy(sm.final_countdown));
        }
        Assert.True(counted, "the countdown started (supermatter.dm:337)");
        Assert.True(In(w, () => QDELETED(sm)), "the countdown ran out and the crystal delaminated (_sm_delam.dm:32)");

        w.Run(2 * Second);
        Assert.DoesNotContain(replica.Entities, e => e.Id == smId);
        int torn = before.Count(c => replica.TurfTypeAt(c.Key.Item1, c.Key.Item2, 1) != c.Value);
        output.WriteLine($"{torn} of {before.Count} tiles around the crystal changed in the client; the engineer's health {healthBefore} -> {In(w, () => engineer.health)}, dead {In(w, () => engineer.stat == DEAD)}");
        Assert.True(torn > 0, "the client sees the engine's turfs torn up by the blast");
        Assert.True(In(w, () => QDELETED(engineer) || engineer.stat == DEAD || Dm.num(engineer.health) < healthBefore), "the engineer is hurt by the blast");
        List<string> anomalies = replica.Entities.Where(e => e.TypePath != null && e.TypePath.StartsWith("/obj/effect/anomaly/", StringComparison.Ordinal)).Select(e => e.TypePath).ToList();
        output.WriteLine($"the client sees {anomalies.Count} anomalies: {string.Join(", ", anomalies)}");
        Assert.True(anomalies.Count >= 4, "a main engine's four instant anomalies (delamination_effects.dm:66-68)");

        // the rest of the anomalies over the next 5 to 10 seconds, and the singularity goes on eating
        w.Run(12 * Second);
        w.World.RuntimeErrorSink = sink;
        output.WriteLine($"runtimes: {string.Join(" | ", errors.Select(e => e.ToString()[..Math.Min(e.ToString().Length, 2500)]).Distinct())}");
        Assert.Empty(errors);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_singularity_delamination_on_meta_makes_a_singularity_the_client_sees_pulling_the_engine_room_in()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 92, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), seed: 202, noInterest: true) /* the anomalies land all over the station */, strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).Where(c => Dm.truthy(is_station_level(c.z))).ToList()));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        // the engineer watches from 30 tiles north, outside its pull
        In(w, () => Waiting.NoSleep(w.MobOf(p).forceMove(locate(smx, smy + 30, 1))));
        // the loose items within 10 tiles of the crystal, as the client sees them
        List<(long Id, int X, int Y)> items = In(w, () => Dm.each<obj_item>(Dm.world)
            .Where(i => !QDELETED(i) && i.loc is turf && i.z == 1 && Math.Max(Math.Abs(i.x - smx), Math.Abs(i.y - smy)) is > 1 and <= 10)
            .Select(i => (i.dm_id, (int)i.x, (int)i.y)).ToList());
        w.Run(Second);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Contains(replica.Entities, e => e.Id == i.Id));

        p.Session.DevCommand($"smdelam {smx} {smy} 1 singularity");
        for (int i = 0; i < 20 && !texts.Any(t => t.Contains("delaminates", StringComparison.Ordinal)); i++) // a payday line may come first
            w.Run(1);
        output.WriteLine("reply: " + string.Join(" | ", texts));
        Assert.Contains(texts, t => t.Contains("delaminates (singularity)", StringComparison.Ordinal));
        Assert.True(In(w, () => QDELETED(sm)), "the crystal is eaten by its singularity (delamination_effects.dm:103)");
        obj_singularity singulo = In(w, () => Dm.each_cast<datum_component_singularity>(GLOB.singularities).Select(c => c.parent as obj_singularity).Single());
        long singuloId = In(w, () => singulo.dm_id);
        w.Run(2);
        Assert.Contains(replica.Entities, e => e.Id == singuloId && e.TypePath == "/obj/singularity");

        w.Run(15 * Second);
        (int X, int Y) at = In(w, () => ((int)singulo.x, (int)singulo.y));
        // the items that started within its pull of where it is now, moved off their tile or eaten: the singularity's random walk
        // (singularity.dm:236) can take it a dozen tiles off the crystal in these 15 s
        int reach = In(w, () => (int)Dm.num((singulo.GetComponent(typeof(datum_component_singularity)) as datum_component_singularity).grav_pull));
        var inReach = items.Where(i => Math.Max(Math.Abs(i.X - at.X), Math.Abs(i.Y - at.Y)) <= reach).ToList();
        int pulled = inReach.Count(i => replica.Entities.FirstOrDefault(e => e.Id == i.Id) is not { } seen || seen.State.X != i.X || seen.State.Y != i.Y);
        output.WriteLine($"stage {In(w, () => singulo.current_size)}, energy {In(w, () => singulo.energy)} at {at} (the crystal was at ({smx}, {smy})); {pulled} of the {inReach.Count} items within its pull of {reach} moved or eaten in the client");
        Assert.NotEmpty(inReach);
        Assert.True(pulled > inReach.Count / 2, "the client sees the engine room's loose items pulled into the singularity");
        Assert.Contains(replica.Entities, e => e.Id == singuloId);

        w.World.RuntimeErrorSink = sink;
        List<string> others = errors.Select(e => e.ToString()).Where(t => !SingularityHitbyRuntime(t)).ToList();
        output.WriteLine($"{errors.Count - others.Count} of tg's own hitby runtimes; others: {string.Join(" | ", others.Select(t => t[..Math.Min(t.Length, 2500)]).Distinct())}");
        Assert.Empty(others);
        Assert.Equal(bootRuntimes + errors.Count, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_player_in_the_path_of_a_singularity_delamination_is_gibbed_once_without_runtimes()
    {
        // the M7 visual check: the singularity eating a person threw in gib (a body with no species eaten again and again)
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true) /* the anomalies land all over the station */, strict: false);
        MetaServer.SettleAir(w);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        w.Run(2 * Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).Where(c => Dm.truthy(is_station_level(c.z))).ToList()));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        mob_living engineer = w.MobOf(p);
        In(w, () => Waiting.NoSleep(engineer.forceMove(locate(smx, smy + 3, 1))));
        w.Run(Second);
        p.Session.DevCommand($"smdelam {smx} {smy} 1 singularity");
        // pulled in and out cold first; how long the rest takes is the singularity's random walk
        for (int i = 0; i < 90 * Second && !In(w, () => QDELETED(engineer)); i++)
            w.Run(1);
        Assert.True(In(w, () => QDELETED(engineer)), "the engineer was eaten and gibbed");
        Assert.True(In(w, () => !Dm.@in(engineer, GLOB.alive_mob_list) && !Dm.@in(engineer, GLOB.mob_living_list)));

        w.World.RuntimeErrorSink = sink;
        List<string> others = errors.Select(e => e.ToString()).Where(t => !SingularityHitbyRuntime(t)).ToList();
        output.WriteLine($"{errors.Count - others.Count} of tg's own hitby runtimes; others: {string.Join(" | ", others.Select(t => t[..Math.Min(t.Length, 2500)]).Distinct())}");
        Assert.Empty(others);
    }

    [MetaServerFact]
    public void A_tesla_delamination_on_meta_with_a_player_near_the_crystal_runs_without_runtimes()
    {
        // the M7 visual check: smdelam during a running countdown delaminated the crystal twice (DevCommands.SmDelam now refuses)
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 94, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true) /* the anomalies land all over the station */, strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        w.Run(2 * Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).Where(c => Dm.truthy(is_station_level(c.z))).ToList()));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        In(w, () => Waiting.NoSleep(w.MobOf(p).forceMove(locate(smx + 6, smy, 1))));
        w.Run(Second);
        p.Session.DevCommand($"smdelam {smx} {smy} 1 tesla");
        for (int i = 0; i < 20 && !texts.Any(t => t.Contains("delaminates", StringComparison.Ordinal)); i++) // a payday line may come first
            w.Run(1);
        Assert.Contains(texts, t => t.Contains("delaminates (tesla)", StringComparison.Ordinal));
        Assert.True(In(w, () => QDELETED(sm)), "the crystal is gone (_sm_delam.dm:32)");
        Assert.NotEmpty(In(w, () => Dm.each<obj_energy_ball>(Dm.world).Where(b => !QDELETED(b)).ToList()));
        w.Run(20 * Second);

        w.World.RuntimeErrorSink = sink;
        List<string> others = errors.Select(e => e.ToString()).Where(t => !SingularityHitbyRuntime(t)).ToList();
        output.WriteLine($"{errors.Count - others.Count} of tg's own hitby runtimes; others: {string.Join(" | ", others.Select(t => t[..Math.Min(t.Length, 2500)]).Distinct())}");
        Assert.Empty(others);
    }

    [MetaServerFact]
    public void A_singularity_delamination_and_two_blasts_on_meta_run_without_runtimes()
    {
        // the M7 visual check's server: a singularity delamination, then two consoles' explode 4 8 14 at (140,140) and (190,160);
        // a dimensional anomaly's listed turf replaced by a blast, an inert air tank destroyed by one, a singularity at the monkeys
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 95, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true) /* the anomalies land all over the station */, strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        w.Run(2 * Second);
        var errors = new List<string>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        // a stack_trace's exception is never thrown: its caller is the sink's own stack
        w.World.RuntimeErrorSink = e => { errors.Add(e is DmCrashException ? e + "\n" + Environment.StackTrace : e.ToString()); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).Where(c => Dm.truthy(is_station_level(c.z))).ToList()));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        In(w, () => Waiting.NoSleep(w.MobOf(p).forceMove(locate(smx, smy + 30, 1))));
        w.Run(Second);
        p.Session.DevCommand($"smdelam {smx} {smy} 1 singularity");
        w.Run(33 * Second);
        p.Session.DevCommand("explode 140 140 1 4 8 14");
        w.Run(22 * Second);
        p.Session.DevCommand("explode 190 160 1 4 8 14");
        w.Run(60 * Second);

        w.World.RuntimeErrorSink = sink;
        List<string> others = errors.Where(t => !SingularityHitbyRuntime(t)).ToList();
        output.WriteLine($"{errors.Count - others.Count} of tg's own hitby runtimes: {string.Join(" | ", errors.Except(others).Select(t => t[..Math.Min(t.Length, 4000)]).Distinct().Take(3))}");
        output.WriteLine($"others: {string.Join(" | ", others.Select(t => t[..Math.Min(t.Length, 2500)]).Distinct())}");
        Assert.Empty(others);
    }

    /// <summary>
    /// tg's own runtimes of a thing struck next to a singularity: <c>/atom/movable/hitby</c> steps the unanchored thing (a window
    /// unanchors in its pull from stage two, window.dm:128) into the singularity, which eats it, and then <c>/atom/hitby</c>'s addtimer
    /// on the deleted thing (atom_act.dm:149, timer.dm:619) and <c>/obj/hitby</c>'s take_damage (obj_defense.dm:8, atom_defense.dm:21)
    /// report it; neither parent checks QDELETED after the step (atoms_movable.dm:1369).
    /// </summary>
    internal static bool SingularityHitbyRuntime(string runtime)
        => (runtime.Contains("taking damage after deletion", StringComparison.Ordinal) && runtime.Contains(".hitby(", StringComparison.Ordinal))
            || runtime.Contains("addtimer called with a callback assigned to a qdeleted object", StringComparison.Ordinal)
            // an air alarm the singularity pulls into another area: on_enter_area runs power_change (and check_danger) before it sets my_area
            // (_air_alarm.dm:174, :573), a runtime in tg too when the new turf has air; which alarm lands where depends on the random stream
            || (runtime.Contains("obj_machinery_airalarm.check_danger", StringComparison.Ordinal) && runtime.Contains("obj_machinery_airalarm.on_enter_area", StringComparison.Ordinal))
            // an airlock pump eaten during start_cycle's stoplag: get_turf(src) is null after it (airlock_pump.dm:310), a runtime in tg too
            || (runtime.StartsWith("System.NullReferenceException", StringComparison.Ordinal) && runtime.Contains("airlock_pump.start_cycle", StringComparison.Ordinal))
            // and its airlocks eaten during the same stoplag: unlocked_airlocks[1].say(message) indexes the emptied list (airlock_pump.dm:366), a
            // runtime in tg too; which of the pump and its airlocks the singularity reaches first depends on the random stream
            || (runtime.StartsWith("Anomaly.Core.DmShim.DmRuntimeException: list index out of bounds", StringComparison.Ordinal) && runtime.Contains("airlock_pump.stop_cycle", StringComparison.Ordinal))
            // a firelock the singularity or a blast took off its turf while its alarm starts: the merger finds no turf for it
            // (_merger.dm:29 Refresh from get_turf(origin)), so GetMergeGroup's mergers[id] reads a null list (atom_merger.dm:13), a
            // runtime in tg too; the random stream that the live floors' map-load rolls shift (gaps G2) decides which firelock it is
            || (runtime.StartsWith("System.NullReferenceException", StringComparison.Ordinal) && runtime.Contains("atom.GetMergeGroup", StringComparison.Ordinal) && runtime.Contains("firedoor.start_activation_process", StringComparison.Ordinal));
}
