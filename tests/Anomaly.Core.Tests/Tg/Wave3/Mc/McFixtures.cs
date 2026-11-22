using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

// Fixtures of the mc package's behaviour tests (written from code/controllers/master.dm, subsystem.dm, controller.dm,
// __DEFINES/MC.dm, _tick.dm and __HELPERS/stoplag.dm). The fixture subsystems have the port's fixture paths
// /datum/controller/subsystem/test_mc_<x> (rules 15) and share their overrides through McFixtureSubsystem, a C# base
// class without [DmPath] (rules 15: tests only, no DM type). They record what the MC does to them in a per-world trace,
// the recording harness of rules 15. Host-side records are [NotHashed]: they are not simulation state.

/// <summary>
/// One world for an mc test: GLOB with the vars the MC reads, the SSticker and SSblackbox stubs Master.Initialize uses
/// (master.dm:460, :508) and a Master whose New created no subsystem (the subsystem-type filter keeps none, lead decision
/// W3 G2); a test then appends its fixture subsystems to <c>Master.subsystems</c> in the order it wants, which is the
/// order <c>subtypesof</c> plus the init-stage sort give tg's Master (master.dm:110-125). Logging, chat, config and
/// world.change_fps are other packages' skeletons on this branch, so the world reports Dm.NotTranslated instead of
/// throwing; the tests assert on the MC's own state and on the DM runtimes (<see cref="DmRuntimes"/>).
/// </summary>
internal sealed class McWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();
    public readonly datum_controller_master Mc;

    public McWorld(ulong seed = 1)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        using (World.Enter())
        {
            DmWorldState.Current.NotTranslatedThrows = false;
            TgState.Current.SubsystemTypeFilter = _ => false;
            // tg's /datum/controller/global_vars/New sets GLOB and runs the InitGlobal procs (globals.dm:14, :51-63): the
            // boot package's; here only the globals the MC reads.
            datum_controller_global_vars glob = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
            TgState.Current.GLOB = glob;
            glob.gvars_datum_init_order = Dm.list();
            glob.InitGlobalsortInstance();
            glob.InitGlobalbitflags();
            glob.InitGlobalmidnight_rollovers();
            glob.InitGlobalrollovercheck_last_timeofday();
            Dm.@new<datum_controller_subsystem_ticker>(); // NEW_SS_GLOBAL(SSticker)
            Dm.@new<datum_controller_subsystem_blackbox>(); // NEW_SS_GLOBAL(SSblackbox)
            TgTestGenesis.Install(); // the logger, SSchat and the default configuration (Genesis, Master/New)
            Mc = Dm.@new<datum_controller_master>();
        }
    }

    /// <summary>Creates a fixture subsystem, names and configures it and appends it to <c>Master.subsystems</c>.</summary>
    public T Add<T>(string name, Action<T> configure = null) where T : McFixtureSubsystem
    {
        using (World.Enter())
        {
            T ss = Dm.@new<T>();
            ss.name = name;
            configure?.Invoke(ss);
            Dm.append(ref Mc.subsystems, ss);
            return ss;
        }
    }

    /// <summary>
    /// world/New's call of the Master's Initialize (game/world.dm:141) with no delay, at tick 0: stage 1 initializes,
    /// SetRunLevel(1) and StartProcessing(0) start Loop(1), whose first iteration runs at tick 0, then the other stages
    /// initialize in the same burst (CHECK_TICK never sleeps, DV3) and the set waitfor = 0 wrapper returns at the final
    /// sleep(1 TICKS). In tick 1 Loop(1) returns NEWSTAGES and StartProcessing runs Loop(4).
    /// </summary>
    public void Boot()
    {
        using (World.Enter())
            Waiting.NoSleep(Mc.Initialize(0, FALSE, FALSE));
    }

    /// <summary>
    /// Starts the MC without Initialize, as Master.Initialize does after the first stage (master.dm:445-451) but with
    /// every stage already completed: Loop(INITSTAGE_MAX) runs its first iteration now.
    /// </summary>
    public void StartAllStagesInitialized(object runlevel_flag = null)
    {
        using (World.Enter())
        {
            datum_controller_master.init_stage_completed = INITSTAGE_MAX;
            Mc.SetRunLevel(runlevel_flag ?? RUNLEVEL_LOBBY);
            Mc.StartProcessing(0);
        }
    }

    public void Step(int ticks) => World.Step(ticks);

    /// <summary>The trace lines, "tick text".</summary>
    public List<string> Trace => McTrace.Of(World).Lines;

    /// <summary>The trace lines of one tick, without the tick.</summary>
    public List<string> TraceAt(long tick)
    {
        string prefix = tick + " ";
        return Trace.Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).Select(l => l.Substring(prefix.Length)).ToList();
    }

    /// <summary>The DM runtimes reported so far, without the reports of other packages' skeletons on this branch.</summary>
    public List<Exception> DmRuntimes => Runtimes.Where(e => e is not DmNotTranslatedException).ToList();

    /// <summary>The order of the subsystem queue, head to tail (Master.queue_head, queue_next).</summary>
    public List<string> QueueNames()
    {
        var names = new List<string>();
        for (datum_controller_subsystem node = Mc.queue_head; node != null && names.Count < 100; node = node.queue_next)
            names.Add(Dm.str(node.name));
        return names;
    }
}

/// <summary>The per-world trace of the mc fixtures, "tick text".</summary>
internal sealed class McTrace
{
    public readonly List<string> Lines = new();

    public static McTrace Of(World world) => world.GetExtension(static _ => new McTrace());

    public static void Add(string text) => Of(World.Current).Lines.Add(World.Current.Tick + " " + text);
}

/// <summary>
/// The shared overrides of the fixture subsystems: fire, Initialize, Shutdown, the map loading hooks and OnConfigLoad
/// record their calls; what fire and Initialize do besides is configured per subsystem by the vars below, set before
/// the MC reads them (as a subtype's var overrides would).
/// </summary>
public class McFixtureSubsystem : datum_controller_subsystem
{
    [NotHashed] public readonly List<long> fires = new();
    [NotHashed] public readonly List<long> resumed_fires = new();
    [NotHashed] public readonly List<float> seen_ticklimits = new();
    [NotHashed] public readonly List<float> seen_allocations = new();
    [NotHashed] public long initialized_at = -1;

    /// <summary>fire() sleeps this long (ds) on its next <see cref="fire_sleeps_left"/> unresumed runs.</summary>
    public float fire_sleep_ds = 0;
    public float fire_sleeps_left = 0;
    /// <summary>After its sleep, fire() runs MC_TICK_CHECK (__DEFINES/MC.dm:1) and returns when it trips.</summary>
    public float check_after_sleep = TRUE;
    /// <summary>fire() CRASHes.</summary>
    public float fire_crashes = FALSE;
    /// <summary>fire() is the base /datum/controller/subsystem/fire (subsystem.dm:163), as for a subtype that has none.</summary>
    public float uses_base_fire = FALSE;
    /// <summary>update_nextfire() CRASHes (a runtime inside RunQueue and CheckQueue).</summary>
    public float nextfire_crashes = FALSE;

    /// <summary>What Initialize() returns.</summary>
    public object init_result = SS_INIT_SUCCESS;
    public float init_sleep_ds = 0;
    public float init_crashes = FALSE;

    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (Dm.truthy(resumed))
            resumed_fires.Add(World.Current.Tick);
        else
            fires.Add(World.Current.Tick);
        seen_ticklimits.Add(datum_controller_master.current_ticklimit);
        seen_allocations.Add(tick_allocation_last);
        McTrace.Add(Dm.text("[] fire[]", name, Dm.truthy(resumed) ? " resumed" : ""));
        if (Dm.truthy(uses_base_fire))
        {
            await base.fire(resumed);
            return;
        }
        if (Dm.truthy(fire_crashes))
            throw Dm.CRASH(Dm.text("[] crashed in fire", name), "code/test_mc.dm", 1);
        if (!Dm.truthy(resumed) && fire_sleeps_left > 0)
        {
            fire_sleeps_left--;
            await sleep(fire_sleep_ds);
            McTrace.Add(Dm.text("[] woke, state []", name, state));
            // MC_TICK_CHECK (__DEFINES/MC.dm:1), expanded inline as translated code does (rules 11.1)
            if (Dm.truthy(check_after_sleep) && Dm.truthy((TICK_USAGE() > datum_controller_master.current_ticklimit || !Dm.eq(state, SS_RUNNING)) ? pause() : 0))
            {
                McTrace.Add(Dm.text("[] paused, state []", name, state));
                return;
            }
        }
        McTrace.Add(Dm.text("[] end", name));
    }

    public override void update_nextfire(float? reset_time = null)
    {
        if (Dm.truthy(nextfire_crashes))
            throw Dm.CRASH(Dm.text("[] crashed in update_nextfire", name), "code/test_mc.dm", 2);
        base.update_nextfire(reset_time);
    }

    public override async DmTask<object> Initialize(params object[] __extra)
    {
        initialized_at = World.Current.Tick;
        McTrace.Add(Dm.text("[] init", name));
        if (Dm.truthy(init_crashes))
            throw Dm.CRASH(Dm.text("[] crashed in Initialize", name), "code/test_mc.dm", 3);
        if (init_sleep_ds > 0)
        {
            await sleep(init_sleep_ds);
            McTrace.Add(Dm.text("[] init woke", name));
        }
        return init_result;
    }

    public override async DmTask Shutdown() => McTrace.Add(Dm.text("[] shutdown", name));

    public override async DmTask StartLoadingMap() => McTrace.Add(Dm.text("[] start loading map", name));

    public override void StopLoadingMap(params object[] __extra) => McTrace.Add(Dm.text("[] stop loading map", name));

    public override async DmTask OnConfigLoad() => McTrace.Add(Dm.text("[] config loaded", name));
}

[DmPath("/datum/controller/subsystem/test_mc_a")]
public class datum_controller_subsystem_test_mc_a : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_b")]
public class datum_controller_subsystem_test_mc_b : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_c")]
public class datum_controller_subsystem_test_mc_c : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_d")]
public class datum_controller_subsystem_test_mc_d : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_e")]
public class datum_controller_subsystem_test_mc_e : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_f")]
public class datum_controller_subsystem_test_mc_f : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_g")]
public class datum_controller_subsystem_test_mc_g : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_h")]
public class datum_controller_subsystem_test_mc_h : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_i")]
public class datum_controller_subsystem_test_mc_i : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_j")]
public class datum_controller_subsystem_test_mc_j : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_k")]
public class datum_controller_subsystem_test_mc_k : McFixtureSubsystem { }

[DmPath("/datum/controller/subsystem/test_mc_l")]
public class datum_controller_subsystem_test_mc_l : McFixtureSubsystem { }

/// <summary>A datum that listens for COMSIG_SUBSYSTEM_POST_INITIALIZE (master.dm:521).</summary>
[DmPath("/datum/test_mc_listener")]
public class datum_test_mc_listener : datum
{
    [NotHashed] public readonly List<string> heard = new();

    [SignalHandler]
    public virtual void on_post_initialize(datum source = null)
    {
        heard.Add(Dm.str(((datum_controller)source).name));
    }
}
