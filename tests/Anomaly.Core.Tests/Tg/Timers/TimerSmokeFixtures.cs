using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Timers;

// Fixtures of the translator's smoke tests for controllers/subsystem/timer.dm and the processing subsystems (wave 3,
// timers and processing package). Fixture paths follow translation-rules.md 15: /datum/test_timers_<name>.

/// <summary>A world for the timer and processing smoke tests, with its runtimes collected.</summary>
internal sealed class TimersTestWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public TimersTestWorld(ulong seed = 11)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        using (World.Enter())
        {
            AddGlob();
            Dm.@new<datum_controller_subsystem_garbage>(); // qdel (garbage.dm:346) reads SSgarbage
        }
    }

    /// <summary>
    /// GLOB with the vars REALTIMEOFDAY reads (__DEFINES/maths.dm:26-27, the client-time timers), set through TgState (the
    /// lead's note on the GLOB accessor, rules 10.6). Call inside a world's scope.
    /// </summary>
    public static void AddGlob()
    {
        datum_controller_global_vars glob = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
        TgState.Current.GLOB = glob;
        glob.gvars_datum_init_order = Dm.list();
        glob.InitGlobalmidnight_rollovers();
        glob.InitGlobalrollovercheck_last_timeofday();
    }

    /// <summary>
    /// One MC fire of a subsystem as RunQueue starts it (controllers/master.dm: the subsystem is SS_RUNNING while it
    /// fires, so MC_TICK_CHECK does not pause it), called inside the world's scope.
    /// </summary>
    public static void Fire(datum_controller_subsystem ss)
    {
        ss.state = SS_RUNNING;
        Waiting.NoSleep(ss.fire(FALSE));
        ss.state = SS_IDLE;
    }

    /// <summary>Steps the world one tick at a time and fires SStimer in each tick, <paramref name="ticks"/> times.</summary>
    public void StepAndFireTimer(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            World.Step();
            using (World.Enter())
                Fire(SStimer);
        }
    }
}

/// <summary>A callback target that records its calls with the world time.</summary>
[DmPath("/datum/test_timers_target")]
public class test_timers_target : datum
{
    [NotHashed]
    public List<string> trace;

    public virtual void ring(object label = null)
    {
        trace.Add(Dm.str(label) + "@" + Dm.str(world.time));
    }
}

/// <summary>A processed datum that records its process() calls and returns <see cref="returns"/>.</summary>
[DmPath("/datum/test_timers_processed")]
public class test_timers_processed : datum
{
    [NotHashed]
    public List<string> trace;

    public string label = "processed";

    public object returns = 0;

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        trace.Add(label + " " + Dm.str(seconds_per_tick));
        return returns;
    }
}

/// <summary>A processed datum whose process() sleeps (it inherits set waitfor = FALSE, processing.dm:48-49).</summary>
[DmPath("/datum/test_timers_sleeper")]
public class test_timers_sleeper : datum
{
    [NotHashed]
    public List<string> trace;

    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick, __extra, null), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick, object[] __extra, DotBox<object> dot)
    {
        trace.Add("sleeper start");
        await sleep(1);
        trace.Add("sleeper end");
        return PROCESS_KILL;
    }
}

/// <summary>
/// A fact that drives the timer buckets: they compute with CEILING, ROUND_UP and TICKS2DS (__DEFINES/maths.dm,
/// __DEFINES/time.dm), which the mc package of wave 3 translates and which the lead merges before this package. Skipped
/// while they are still skeletons.
/// </summary>
public sealed class TimerMacrosFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Translated = new(() =>
    {
        var w = new World();
        using (w.Enter())
        {
            try
            {
                CEILING(1, 0.5f);
                ROUND_UP(1);
                TICKS2DS(1);
                return true;
            }
            catch (DmNotTranslatedException)
            {
                return false;
            }
        }
    });

    public TimerMacrosFactAttribute()
    {
        if (!Translated.Value)
            Skip = "needs CEILING, ROUND_UP and TICKS2DS of the mc package (wave 3), merged before the timers package";
    }
}

/// <summary>
/// A <see cref="TimerMacrosFactAttribute"/> that also needs REALTIMEOFDAY (__DEFINES/maths.dm:26, mc package) in a world
/// with GLOB, for the TIMER_CLIENT_TIME timers.
/// </summary>
public sealed class TimerClientTimeFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Available = new(() =>
    {
        var w = new World();
        using (w.Enter())
        {
            try
            {
                TimersTestWorld.AddGlob();
                CEILING(1, 0.5f);
                ROUND_UP(1);
                TICKS2DS(1);
                REALTIMEOFDAY();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    });

    public TimerClientTimeFactAttribute()
    {
        if (!Available.Value)
            Skip = "needs CEILING, ROUND_UP, TICKS2DS and REALTIMEOFDAY of the mc package (wave 3) in a bare world";
    }
}
