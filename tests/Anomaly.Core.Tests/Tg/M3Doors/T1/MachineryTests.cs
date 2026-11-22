using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

/// <summary>A machine that records the calls SSmachines and SSatoms make on it.</summary>
[DmPath("/obj/machinery/t1_probe")]
public class obj_machinery_t1_probe : obj_machinery
{
    public List<string> sink;
    public string label = "?";
    public float? seconds_seen;
    public bool kill_in_process;

    /// <summary>What the next probe made on this thread starts with (its Initialize runs inside New, before a test can set fields).</summary>
    [ThreadStatic] public static List<string> NextSink;
    [ThreadStatic] public static string NextLabel;

    public obj_machinery_t1_probe()
    {
        sink = NextSink;
        label = NextLabel ?? "?";
    }

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        sink?.Add($"init {label} mapload={Dm.truthy(mapload)}");
        return base.Initialize(mapload, __extra);
    }

    public override async DmTask post_machine_initialize()
    {
        sink?.Add($"post {label}");
        await base.post_machine_initialize();
    }

    public override object process_early(params object[] __extra)
    {
        sink?.Add($"early {label}");
        return null;
    }

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        sink?.Add($"process {label} t={Dm.world.time}");
        seconds_seen = Dm.num(seconds_per_tick);
        return kill_in_process ? PROCESS_KILL : null;
    }

    public override object process_late()
    {
        sink?.Add($"late {label}");
        return null;
    }
}

[DmPath("/obj/machinery/t1_probe/manual")]
public class obj_machinery_t1_probe_manual : obj_machinery_t1_probe
{
    public obj_machinery_t1_probe_manual() { processing_flags = START_PROCESSING_MANUALLY; }
}

[DmPath("/obj/machinery/t1_probe/unpowered")]
public class obj_machinery_t1_probe_unpowered : obj_machinery_t1_probe
{
    public obj_machinery_t1_probe_unpowered() { use_power = NO_POWER_USE; }
}

// T1: /obj/machinery (_machinery.dm) and SSmachines (machines.dm): registration, processing flags and order, power status,
// and the Initialize / LateInitialize sequence of machines on a loaded map.

public class MachineryTests
{
    private static obj_machinery_t1_probe Probe(TgToyRoom room, string label, List<string> sink, int x = 3, int y = 3)
        => room.In(() =>
        {
            obj_machinery_t1_probe.NextSink = sink;
            obj_machinery_t1_probe.NextLabel = label;
            try { return Dm.@new<obj_machinery_t1_probe>(locate(x, y, 1)); }
            finally { obj_machinery_t1_probe.NextSink = null; obj_machinery_t1_probe.NextLabel = null; }
        });

    [Fact]
    public void MachinesRegisterByExactTypeAndByTypeAndSubtypesAndUnregisterOnDestroy()
    {
        var room = T1.Room();
        var plain = Probe(room, "plain", null);
        var manual = room.In(() => Dm.@new<obj_machinery_t1_probe_manual>(locate(4, 3, 1)));
        room.In(() =>
        {
            // machines.dm:38-41, :55-72
            Assert.True(Dm.@in(plain, (DmList)SSmachines.get_all_machines()));
            Assert.True(Dm.@in(plain, (DmList)SSmachines.get_machines_by_type(typeof(obj_machinery_t1_probe))));
            Assert.False(Dm.@in(manual, (DmList)SSmachines.get_machines_by_type(typeof(obj_machinery_t1_probe)))); // exact type only
            var both = (DmList)SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_t1_probe));
            Assert.True(Dm.@in(plain, both));
            Assert.True(Dm.@in(manual, both));
            // get_machines_by_type returns a copy (`machines?.Copy() || list()`)
            var copy = (DmList)SSmachines.get_machines_by_type(typeof(obj_machinery_t1_probe));
            Dm.remove(ref copy, plain);
            Assert.True(Dm.@in(plain, (DmList)SSmachines.get_machines_by_type(typeof(obj_machinery_t1_probe))));
            // a type with no machine gives an empty list, a non-machine type is a CRASH (:46, :62)
            Assert.Equal(0f, Dm.num(((DmList)SSmachines.get_machines_by_type(typeof(obj_machinery_t1_probe_unpowered))).len));
            Assert.ThrowsAny<Exception>(() => SSmachines.get_machines_by_type_and_subtypes(typeof(obj_item)));
            Assert.ThrowsAny<Exception>(() => SSmachines.get_machines_by_type(typeof(obj_item)));
            qdel(plain);
            Assert.False(Dm.@in(plain, (DmList)SSmachines.get_all_machines()));
            Assert.False(Dm.@in(plain, (DmList)SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_t1_probe))));
            Assert.False(Dm.@in(plain, SSmachines.processing)); // Destroy: end_processing (:227)
            Assert.True(Dm.@in(manual, (DmList)SSmachines.get_all_machines()));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ProcessingFlagsDecideWhetherAMachineStartsProcessingOnInit()
    {
        var room = T1.Room();
        var auto = Probe(room, "auto", null);
        var manual = room.In(() => Dm.@new<obj_machinery_t1_probe_manual>(locate(4, 3, 1)));
        room.In(() =>
        {
            // _machinery.dm:134 START_PROCESSING_ON_INIT (default), :183 `if(processing_flags & START_PROCESSING_ON_INIT) begin_processing()`
            Assert.True(Dm.@in(auto, SSmachines.processing));
            Assert.True(Dm.truthy(Dm.bitand(auto.datum_flags, DF_ISPROCESSING)));
            Assert.False(Dm.@in(manual, SSmachines.processing));
            Assert.False(Dm.truthy(Dm.bitand(manual.datum_flags, DF_ISPROCESSING)));
            manual.begin_processing();
            Assert.True(Dm.@in(manual, SSmachines.processing));
            manual.begin_processing(); // START_PROCESSING checks the flag: once
            Assert.Equal(1, Dm.each(SSmachines.processing).Count(o => ReferenceEquals(o, manual)));
            manual.end_processing();
            Assert.False(Dm.@in(manual, SSmachines.processing));
            Assert.False(Dm.truthy(Dm.bitand(manual.datum_flags, DF_ISPROCESSING)));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMachineWithTheDefaultProcessLeavesProcessingAtItsFirstFire()
    {
        // _machinery.dm:310 `/obj/machinery/process() return PROCESS_KILL`; machines.dm:154 removes it and clears DF_ISPROCESSING
        var room = T1.Room();
        var airlock = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var killer = Probe(room, "killer", new List<string>());
        room.In(() =>
        {
            killer.kill_in_process = true;
            Assert.True(Dm.@in(airlock, SSmachines.processing));
        });
        room.Step(45); // wait = 2 SECONDS = 40 ticks
        room.In(() =>
        {
            Assert.False(Dm.@in(airlock, SSmachines.processing));
            Assert.False(Dm.truthy(Dm.bitand(airlock.datum_flags, DF_ISPROCESSING)));
            Assert.False(Dm.@in(killer, SSmachines.processing));
            Assert.True(Dm.@in(airlock, (DmList)SSmachines.get_all_machines())); // still a machine
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void EachFireRunsEarlyThenMachinesThenLateEachFromTheEndOfItsListEveryTwoSeconds()
    {
        // machines.dm:96-165: processing_early (popped from the end), the APC steps, then `processing`, then processing_late
        var room = T1.Room();
        var log = new List<string>();
        var a = Probe(room, "A", log);
        var b = Probe(room, "B", log);
        var c = Probe(room, "C", log);
        room.In(() =>
        {
            foreach (var p in new[] { a, b, c })
            {
                Dm.append(ref SSmachines.processing_early, p);
                Dm.append(ref SSmachines.processing_late, p);
            }
        });
        int firedBefore = room.In(() => (int)Dm.num(SSmachines.times_fired));
        log.Clear();
        room.Step(41);
        room.Step(40);
        room.In(() => Assert.True(Dm.num(SSmachines.times_fired) >= firedBefore + 2));
        var oneFire = new[] { "early C", "early B", "early A", "process C", "process B", "process A", "late C", "late B", "late A" };
        var kinds = log.Select(l => l.Split(' ')[0] + " " + l.Split(' ')[1]).ToList();
        Assert.Equal(oneFire.Concat(oneFire).ToArray(), kinds.Take(18).ToArray());
        // the two fires are 2 SECONDS (20 ds) apart; SS_KEEP_TIMING
        var times = log.Where(l => l.StartsWith("process C")).Select(l => float.Parse(l.Split("t=")[1], System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(20f, times[1] - times[0]);
        room.In(() => Assert.True(Dm.eq(a.seconds_seen, 2))); // process(wait * 0.1)
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMachineInAnOrdinaryAreaIsPoweredOnEveryChannelAndSpaceNeverIs()
    {
        // areas.dm:85-87 power_* = TRUE and :492 powered(): requires_power areas are powered by their flags, which only
        // an APC turns off (inert until M7); /area/space has always_unpowered (areas.dm:51 powered returns FALSE)
        var room = T1.Room();
        var probe = Probe(room, "p", null);
        var free = room.In(() => Dm.@new<obj_machinery_t1_probe_unpowered>(locate(5, 3, 1)));
        room.In(() =>
        {
            foreach (float chan in new[] { AREA_USAGE_EQUIP, AREA_USAGE_LIGHT, AREA_USAGE_ENVIRON })
                Assert.True(Dm.truthy(probe.powered__proc(chan)));
            Assert.False(Dm.truthy(Dm.bitand(probe.machine_stat, NOPOWER)));
            Assert.True(Dm.truthy(probe.is_operational));
            Assert.True(Dm.truthy(free.powered__proc(AREA_USAGE_EQUIP)));
            var space = (area)GLOB.areas_by_type[typeof(area_space)];
            foreach (float chan in new[] { AREA_USAGE_EQUIP, AREA_USAGE_LIGHT, AREA_USAGE_ENVIRON })
                Assert.False(Dm.truthy(space.powered(chan)));
            // use_power NO_POWER_USE: powered without asking the area (power.dm:121 `!use_power && !ignore_use_power`)
            Assert.True(Dm.truthy(free.powered__proc(AREA_USAGE_EQUIP)));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void MachinesOnALoadedMapInitializeThenLateInitializeInCreationOrder()
    {
        // atoms_initializing_EXPENSIVE.dm: Initialize(mapload = TRUE) for every atom, then the LATELOAD ones' LateInitialize
        // (_machinery.dm:202 -> post_machine_initialize, which does power_change); SSmachines (dependent on SSatoms)
        // then initializes and fires once
        var log = new List<string>();
        obj_machinery_t1_probe a = null, b = null;
        var room = T1.Room(mapExtras: w =>
        {
            a = Dm.@new<obj_machinery_t1_probe>(locate(3, 3, 1));
            a.label = "A";
            a.sink = log;
            b = Dm.@new<obj_machinery_t1_probe>(locate(4, 3, 1));
            b.label = "B";
            b.sink = log;
        });
        int post = log.IndexOf("post A");
        Assert.Equal(new[] { "init A mapload=True", "init B mapload=True", "post A", "post B" }, log.Take(4).ToArray());
        Assert.True(post == 2);
        // SSmachines.Initialize (machines.dm:32) calls fire(); its first MC_TICK_CHECK (`src.state != SS_RUNNING` while the
        // subsystem is only initializing) pauses and returns, so only the last machine of `processing` ran: since silicons SI6b that
        // is the Newscaster app's prototype newscaster (SSmodular_computers makes every program at init, after the map's machines;
        // its process kills it), so neither probe has processed yet
        Assert.DoesNotContain(log.Skip(4).Take(2), l => l.StartsWith("process ", StringComparison.Ordinal));
        room.In(() => Assert.True(Dm.each<obj_machinery_newscaster>(GLOB.allCasters).Any(c => c.loc == null), "the app's newscaster is in nullspace"));
        room.In(() =>
        {
            Assert.True(Dm.truthy(Dm.bitand(a.flags_1, INITIALIZED_1)));
            Assert.True(Dm.@in(a, (DmList)SSmachines.get_all_machines()));
            Assert.False(Dm.truthy(Dm.bitand(a.machine_stat, NOPOWER)));
        });
        // a machine made after the map: Initialize(mapload = FALSE) and its LateInitialize at once
        log.Clear();
        var c = Probe(room, "C", log);
        Assert.Equal(new[] { "init C mapload=False", "post C" }, log.ToArray());
        T1.NoRuntimes(room);
    }
}
