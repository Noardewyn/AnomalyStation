using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

/// <summary>
/// SStimer and the processing subsystems run by the mc package's Master Controller (controllers/master.dm), as in a
/// world: Master/New creates them through SUBSYSTEM_DEF and PROCESSING_SUBSYSTEM_DEF, Master.Initialize starts the Loop,
/// the tickers (SStimer, SStimed_actions) fire every tick in every runlevel, SSobj, SSprocessing and SSfastprocess fire
/// by their waits in the game runlevels.
/// </summary>
public class MasterDrivenTests
{
    private static readonly Type[] Subsystems =
    {
        typeof(datum_controller_subsystem_timer),
        typeof(datum_controller_subsystem_processing),
        typeof(datum_controller_subsystem_processing_fastprocess),
        typeof(datum_controller_subsystem_processing_obj),
        typeof(datum_controller_subsystem_processing_timed_actions),
        typeof(datum_controller_subsystem_garbage), // qdel reads SSgarbage
        // Master.Initialize writes SSticker.timeLeft (master.dm:460) and init_subsystem tallies SSblackbox (:508)
        typeof(datum_controller_subsystem_ticker),
        typeof(datum_controller_subsystem_blackbox),
    };

    private sealed class McWorld
    {
        public readonly World World;
        public readonly List<Exception> Runtimes = new();
        public readonly List<string> Log = new();

        public McWorld(ulong seed)
        {
            World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
            using (World.Enter())
            {
                // the config, the logging and chat procs and world.change_fps are other packages' skeletons on this
                // branch: reported once per site instead of thrown
                DmWorldState.Current.NotTranslatedThrows = false;
                TgState.Current.SubsystemTypeFilter = t => Subsystems.Contains(t);
                datum_controller_global_vars glob = new datum_controller_global_vars() /* the C# constructor: GLOB's New queues a timer (globals.dm:24) */;
                TgState.Current.GLOB = glob;
                glob.gvars_datum_init_order = Dm.list();
                glob.InitGlobalsortInstance();
                glob.InitGlobalbitflags();
                glob.InitGlobalmidnight_rollovers();
                glob.InitGlobalrollovercheck_last_timeofday();
                TgTestGenesis.Install(); // the logger, SSchat and the default configuration (Genesis, Master/New)
                Dm.@new<datum_controller_master>();
                // M10 slice F translated the ticker; here it is only what Master.Initialize writes, so it neither initializes nor fires
                SSticker.ss_flags = Dm.bitor(SSticker.ss_flags, SS_NO_INIT | SS_NO_FIRE);
            }
        }

        public void In(Action body)
        {
            using (World.Enter())
                body();
        }

        public void AssertOnlySkeletonReports()
        {
            var sites = Runtimes.Select(e => Assert.IsType<DmNotTranslatedException>(e).Where).ToList();
            Assert.Equal(sites.Distinct().Count(), sites.Count);
            Assert.All(sites, where => Assert.True(
                where.StartsWith("code/controllers/configuration/", StringComparison.Ordinal)
                || where.StartsWith("code/__HELPERS/logging/", StringComparison.Ordinal)
                || where.StartsWith("code/modules/tgchat/", StringComparison.Ordinal)
                || where.StartsWith("code/game/world.dm", StringComparison.Ordinal)
                || where == "code/controllers/master.dm:1045", // AttemptProfileDump: config.Get reads null there
                where));
        }
    }

    private static test_timers_worker Worker(McWorld w, datum_controller_subsystem_processing ss, string label)
    {
        test_timers_worker worker = Dm.@new<test_timers_worker>();
        worker.log = w.Log;
        worker.label = label;
        worker.ss = ss;
        worker.with_time = TRUE;
        START_PROCESSING(ss, worker);
        return worker;
    }

    private static List<float> TimesOf(List<string> log, string prefix)
        => log.Where(l => l.StartsWith(prefix, StringComparison.Ordinal))
              .Select(l => float.Parse(l[(l.IndexOf('@') + 1)..], System.Globalization.CultureInfo.InvariantCulture))
              .ToList();

    private static McWorld Scenario(ulong seed)
    {
        var w = new McWorld(seed);
        w.In(() =>
        {
            // SStimer's PreInit ran in Master/New at world.time 0 (timer.dm:52-55)
            Assert.Equal(0f, SStimer.head_offset);
            test_timers_bell bell = Dm.@new<test_timers_bell>();
            bell.log = w.Log;
            TimerWorld.Ring(bell, "early", 1); // due before the MC fires SStimer at all
            TimerWorld.Ring(bell, "five", 5 * SECONDS);
            TimerWorld.Ring(bell, "a", 30);
            TimerWorld.Ring(bell, "b", 30);
            TimerWorld.Ring(bell, "long", 61 * SECONDS);
            TimerWorld.Ring(bell, "loop", 25 * SECONDS, TIMER_LOOP);
            Worker(w, SStimed_actions, "ta");
            Worker(w, SSobj, "obj");
            Worker(w, SSprocessing, "proc");
            Worker(w, SSfastprocess, "fast");
            // world/New's call (game/world.dm:141): the set waitfor = 0 wrapper returns at the body's sleep(10)
            Waiting.NoSleep(Master.Initialize(10, FALSE, TRUE));
        });
        w.World.Step(100);
        w.In(() => Master.SetRunLevel(RUNLEVEL_GAME)); // the stand-in for SSticker (scope R6)
        w.World.Step(1300);
        return w;
    }

    [Fact]
    public void TheMasterFiresSStimerEveryTickAndTheTimersRunAtTheirTimes()
    {
        McWorld w = Scenario(4);
        var rings = w.Log.Where(l => !l.Contains(':')).ToList();
        // SStimer, a ticker, joins Loop(4) after the first stage at tick 20, staggered by TICKS2DS(rand(0, 1))
        // (master.dm:610); "early" was due at 1 and runs at its first fire
        Assert.Matches("^early@(10.5|11|11.5)$", rings[0]);
        // same-tick timers last-created-first; the loop re-armed from each fire every 25 s; the 61 s timer through the
        // secondary queue
        Assert.Equal(new[] { "b@30", "a@30", "five@50", "loop@250", "loop@500", "long@610" }, rings.Skip(1));
        w.In(() =>
        {
            Assert.Equal(0, SStimer.second_queue.len);
            Assert.Equal(1f, SStimer.bucket_count); // the loop
            Assert.True(SStimer.times_fired >= 1375);
        });
        w.AssertOnlySkeletonReports();
    }

    [Fact]
    public void TheMasterProcessesEachProcessingSubsystemByItsWait()
    {
        McWorld w = Scenario(4);
        // SStimed_actions is a ticker: every tick from its first fire, in the lobby too (master.dm:638-641)
        List<float> ta = TimesOf(w.Log, "ta:0.1@");
        Assert.InRange(ta[0], 10.5f, 11.5f);
        Assert.Equal(Enumerable.Range(0, ta.Count).Select(i => ta[0] + i * 0.5f), ta);
        Assert.Equal(700f, ta[^1]);
        // the others run in the game runlevel only (default runlevels), from SetRunLevel(GAME) at world.time 50 with a
        // random offset of up to 2 s (master.dm:722), then every wait: 2 s, 1 s, 0.2 s
        foreach ((string prefix, float wait) in new[] { ("obj:2@", 20f), ("proc:1@", 10f), ("fast:0.2@", 2f) })
        {
            List<float> times = TimesOf(w.Log, prefix);
            Assert.InRange(times[0], 50.5f, 70.5f);
            for (int i = 1; i < times.Count; i++)
                Assert.Equal(wait, times[i] - times[i - 1]);
            Assert.True(times[^1] > 700 - wait);
        }
        w.In(() =>
        {
            // SSOBJ_DT and DELTA_WORLD_TIME (__DEFINES/subsystems.dm:278, :281) read the subsystem as RunQueue left it
            Assert.Equal(2f, SSOBJ_DT());
            Assert.Equal(TimesOf(w.Log, "obj:2@")[^1], SSobj.last_fire);
            Assert.Equal((700f - SSobj.last_fire) * 0.1f, DELTA_WORLD_TIME(SSobj));
        });
        w.AssertOnlySkeletonReports();
    }

    [Fact]
    public void TheSameMasterRunTwiceGivesTheSameOrderAndWorldState()
    {
        McWorld a = Scenario(8), b = Scenario(8);
        Assert.Equal(a.Log, b.Log);
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
    }
}
