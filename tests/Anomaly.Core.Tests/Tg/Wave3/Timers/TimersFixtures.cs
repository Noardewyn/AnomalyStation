using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

// Fixtures of the independent behaviour tests of the timers and processing package (wave 3): code/controllers/subsystem/
// timer.dm, code/controllers/subsystem/processing/{processing,fastprocess,obj,timed_actions}.dm. Fixture paths follow
// translation-rules.md 15 (/datum/test_timers_<name>); the translator's smoke fixtures (Tg/Timers) use other names.

/// <summary>
/// A world with what SStimer reads: GLOB with the vars of sortTim (reset_buckets) and REALTIMEOFDAY (client-time
/// timers), SSgarbage (qdel) and a fresh SStimer created by SUBSYSTEM_DEF's New (NEW_SS_GLOBAL, PreInit). The tests
/// drive SStimer.fire tick by tick, as the MC fires a ticker subsystem of wait 1.
/// </summary>
internal sealed class TimerWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();
    public readonly List<string> Log = new();

    public TimerWorld(ulong seed = 3, double realtimeStart = 0, bool notTranslatedThrows = true, int stepsBeforeSStimer = 0)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        using (World.Enter())
        {
            DmWorldState.Current.RealtimeStart = realtimeStart;
            DmWorldState.Current.NotTranslatedThrows = notTranslatedThrows;
            // GLOB is set through TgState (the lead's note: main splits the GLOB accessor, rules 10.6)
            datum_controller_global_vars glob = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
            TgState.Current.GLOB = glob;
            glob.gvars_datum_init_order = Dm.list();
            glob.InitGlobalsortInstance(); // sortTim (reset_buckets, timer.dm:277)
            glob.InitGlobalmidnight_rollovers(); // REALTIMEOFDAY (__DEFINES/maths.dm:26-27)
            glob.InitGlobalrollovercheck_last_timeofday();
            Dm.@new<datum_controller_subsystem_garbage>(); // qdel reads SSgarbage (garbage.dm:346)
            TgTestGenesis.Install(); // the logger, SSchat and the default configuration (Genesis, Master/New)
        }
        World.Step(stepsBeforeSStimer);
        using (World.Enter())
            Dm.@new<datum_controller_subsystem_timer>();
    }

    /// <summary>Runs <paramref name="body"/> inside the world's scope.</summary>
    public void In(Action body)
    {
        using (World.Enter())
            body();
    }

    /// <summary>Returns <paramref name="body"/>'s value, computed inside the world's scope.</summary>
    public T In<T>(Func<T> body)
    {
        using (World.Enter())
            return body();
    }

    /// <summary>
    /// One fire of SStimer as the MC's RunQueue starts it (the subsystem is SS_RUNNING while it fires, master.dm:896,
    /// so MC_TICK_CHECK only trips on tick usage, which is 0, DV3). Call inside the world's scope.
    /// </summary>
    public static void FireTimer()
    {
        SStimer.state = SS_RUNNING;
        Waiting.NoSleep(SStimer.fire(FALSE));
        SStimer.state = SS_IDLE;
    }

    /// <summary>Steps the world one tick and fires SStimer in it, <paramref name="ticks"/> times.</summary>
    public void Tick(int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            World.Step();
            using (World.Enter())
                FireTimer();
        }
    }

    /// <summary>A bell that rings into <see cref="Log"/> (call inside the world's scope).</summary>
    public test_timers_bell Bell()
    {
        test_timers_bell b = Dm.@new<test_timers_bell>();
        b.log = Log;
        return b;
    }

    /// <summary>
    /// addtimer(CALLBACK(target, PROC_REF(proc), label), wait, flags): the expansion of the addtimer macro
    /// (__DEFINES/subsystems.dm:218, rules 11.1 (f)) with this test file as its location.
    /// </summary>
    public static object Add(datum target, string proc, object label, float wait, float flags = 0)
        => _addtimer(Dm.@new<datum_callback>(target, proc, label), wait, flags, file: "code/test/timers.dm", line: 1);

    /// <summary>addtimer of <see cref="test_timers_bell.ring"/> with <paramref name="label"/>.</summary>
    public static object Ring(test_timers_bell bell, object label, float wait, float flags = 0)
        => Add(bell, nameof(test_timers_bell.ring), label, wait, flags);

    /// <summary>The timed event that a stoppable timer id names (call inside the world's scope).</summary>
    public static datum_timedevent Event(object id) => SStimer.timer_id_dict[id] as datum_timedevent;

    /// <summary>
    /// The runtimes are only reports of other packages' skeletons on this branch (logging: warning, message_admins;
    /// boot: config), each reported once per site, plus <paramref name="ownSites"/>, sites of this package that the
    /// package leaves Dm.NotTranslated on purpose (dump_timer_buckets, get_timer_debug_string).
    /// </summary>
    public void AssertOnlySkeletonReports(params string[] ownSites)
    {
        Assert.All(Runtimes, e =>
        {
            var nt = Assert.IsType<DmNotTranslatedException>(e);
            Assert.True(nt.Where.StartsWith("code/__HELPERS/logging/", StringComparison.Ordinal)
                        || nt.Where.StartsWith("code/modules/admin/", StringComparison.Ordinal)
                        || nt.Where.StartsWith("code/controllers/configuration/", StringComparison.Ordinal)
                        || ownSites.Contains(nt.Where), nt.Where);
        });
    }
}

/// <summary>A timer target: its procs write "label@world.time" (and more) into a shared log.</summary>
[DmPath("/datum/test_timers_bell")]
public class test_timers_bell : datum
{
    [NotHashed]
    public List<string> log;

    /// <summary>The id of a timer of this bell, for the callbacks that act on their own timer.</summary>
    public object my_id;

    /// <summary>How many more times <see cref="rearm"/> adds its timer again.</summary>
    public float rearms_left;

    public virtual void ring(object label = null)
    {
        log.Add(Dm.str(label) + "@" + Dm.str(world.time));
    }

    /// <summary>Rings, then raises a runtime error.</summary>
    public virtual void boom(object label = null)
    {
        log.Add(Dm.str(label) + "@" + Dm.str(world.time));
        throw new InvalidOperationException("boom " + Dm.str(label));
    }

    /// <summary>Rings, sleeps a tick, rings again.</summary>
    public virtual async DmTask nap(object label = null)
    {
        log.Add(Dm.str(label) + " sleeps@" + Dm.str(world.time));
        await sleep(world.tick_lag);
        log.Add(Dm.str(label) + " wakes@" + Dm.str(world.time));
    }

    /// <summary>Rings and logs what deltimer and timeleft say about this bell's own timer, from inside its callback.</summary>
    public virtual void check_own(object label = null)
    {
        log.Add(Dm.str(label) + "@" + Dm.str(world.time) + " timeleft=" + Dm.str(timeleft(my_id)) + " deltimer=" + Dm.str(deltimer(my_id)));
    }

    /// <summary>Rings and qdels this bell (a target deleted from its own timer's callback).</summary>
    public virtual void self_destruct(object label = null)
    {
        log.Add(Dm.str(label) + "@" + Dm.str(world.time));
        qdel(this);
    }

    /// <summary>
    /// Rings and adds the same UNIQUE timer again while <see cref="rearms_left"/> lasts: tg's common re-arm-from-the-
    /// callback pattern, which meets its own spent timer in SStimer.hashes (timer.dm:641-644).
    /// </summary>
    public virtual void rearm(object label = null)
    {
        log.Add(Dm.str(label) + "@" + Dm.str(world.time));
        if (rearms_left-- > 0)
            log.Add("rearmed=" + Dm.str(TimerWorld.Add(this, nameof(rearm), label, 1, TIMER_UNIQUE | TIMER_STOPPABLE)));
    }
}

/// <summary>
/// A processed datum: process() logs "label:seconds_per_tick", optionally acts on another datum of its subsystem
/// (STOP_PROCESSING, START_PROCESSING, qdel) and returns <see cref="returns"/>.
/// </summary>
[DmPath("/datum/test_timers_worker")]
public class test_timers_worker : datum
{
    [NotHashed]
    public List<string> log;

    public string label;
    public object returns;
    public datum_controller_subsystem_processing ss;
    public datum stops;
    public datum starts;
    public datum deletes;
    public float throws;
    /// <summary>When set, the log line also names world.time ("label:seconds_per_tick@time").</summary>
    public float with_time;

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        log.Add(label + ":" + Dm.str(seconds_per_tick) + (Dm.truthy(with_time) ? "@" + Dm.str(world.time) : ""));
        if (stops != null)
            STOP_PROCESSING(ss, stops);
        if (starts != null)
            START_PROCESSING(ss, starts);
        if (deletes != null)
            qdel(deletes);
        if (Dm.truthy(throws))
            throw new InvalidOperationException("worker " + label);
        return returns;
    }
}

/// <summary>
/// A processed datum whose process() sleeps before it returns PROCESS_KILL: it inherits set waitfor = FALSE
/// (processing.dm:48-49, controllers.md hazard 9), so it is the wrapper of waiting-model.md 5.4 and its caller gets null
/// at the sleep (tg's comment at pod.dm:25-30: "can't rely on return PROCESS_KILL").
/// </summary>
[DmPath("/datum/test_timers_dozer")]
public class test_timers_dozer : datum
{
    [NotHashed]
    public List<string> log;

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick, __extra, null), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick, object[] __extra, DotBox<object> dot)
    {
        log.Add("dozer sleeps@" + Dm.str(world.time));
        await sleep(world.tick_lag);
        log.Add("dozer wakes@" + Dm.str(world.time));
        return PROCESS_KILL;
    }
}

/// <summary>A bell whose Destroy adds a timer on itself (tg does this, e.g. a QDEL_IN in a Destroy).</summary>
[DmPath("/datum/test_timers_phoenix")]
public class test_timers_phoenix : test_timers_bell
{
    public override object Destroy(float? force = null)
    {
        TimerWorld.Ring(this, "ashes", 1);
        return base.Destroy(force);
    }
}
