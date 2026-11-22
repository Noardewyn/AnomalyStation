using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

/// <summary>
/// The rest of the mc package's contract: stoplag (__HELPERS/stoplag.dm), the small subsystem procs (subsystem.dm:
/// pause, state_letter, update_nextfire(reset_time), postpone, Destroy), the Master's Shutdown, map-loading hooks and
/// OnConfigLoad (master.dm:135, :1013-1041), the MC.dm averages, the stack end detector, and determinism (rules 15).
/// </summary>
public class McMiscTests
{
    private static readonly float LobbyAndDefault = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);

    /// <summary>Starts stoplag(delay) at the current tick and steps until it returns: (ticks slept, value).</summary>
    private static (int ticks, object value) RunStoplag(McWorld w, object delay)
    {
        DmTask<object> task;
        using (w.World.Enter())
            task = delay == null ? stoplag() : stoplag(delay);
        int ticks = 0;
        while (!task.IsCompleted && ticks < 1000)
        {
            w.Step(1);
            ticks++;
        }
        Assert.True(task.TryGetResult(out object value));
        return (ticks, value);
    }

    [Fact]
    public void StoplagSleepsOneTickUntilEveryStageInitializedWhateverTheDelay()
    {
        var w = new McWorld();
        (int ticks, object value) = RunStoplag(w, 10); // init_stage_completed is null (stoplag.dm:8-10)
        Assert.Equal(1, ticks);
        Assert.True(Dm.eq(value, 1));
        using (w.World.Enter())
            datum_controller_master.init_stage_completed = INITSTAGE_LAST - 1;
        (ticks, value) = RunStoplag(w, 10);
        Assert.Equal(1, ticks);
        Assert.True(Dm.eq(value, 1));
    }

    [Theory]
    [InlineData(null, 1, 1)] // initial_delay defaults to world.tick_lag (:11-12)
    [InlineData(0f, 1, 1)] // 0 is false: world.tick_lag
    [InlineData(0.3f, 1, 1)] // DS2TICKS 0.6: ceil 1; sleep(0.3) is one tick
    [InlineData(10f, 20, 20)]
    [InlineData(1.25f, 2, 3)] // DS2TICKS 2.5: the count is ceil(2.5) = 3, the sleep rounds down to 2 ticks (waiting-model 8)
    public void StoplagReturnsTheCeilingOfTheDelayInTicksOnceTheMcIsUp(object delay, int ticks_slept, int returned)
    {
        var w = new McWorld();
        using (w.World.Enter())
            datum_controller_master.init_stage_completed = INITSTAGE_MAX;
        // with the tick usage of 0 (DV3), DELTA_CALC is 1 and the do-while runs once (stoplag.dm:19-25)
        (int ticks, object value) = RunStoplag(w, delay);
        Assert.Equal(ticks_slept, ticks);
        Assert.True(Dm.eq(value, returned));
    }

    [Fact]
    public void PauseMarksARunningSubsystemPausedAndASleepingOnePausingAndAlwaysReturnsOne()
    {
        var w = new McWorld();
        var ss = w.Add<datum_controller_subsystem_test_mc_a>("A");
        using (w.World.Enter())
        {
            (float from, float to)[] cases =
            {
                (SS_RUNNING, SS_PAUSED), (SS_SLEEPING, SS_PAUSING), (SS_IDLE, SS_IDLE), (SS_QUEUED, SS_QUEUED),
                (SS_PAUSED, SS_PAUSED), (SS_PAUSING, SS_PAUSING),
            };
            foreach ((float from, float to) in cases)
            {
                ss.state = from;
                Assert.True(Dm.eq(ss.pause(), 1));
                Assert.True(Dm.eq(ss.state, to));
            }
        }
    }

    [Fact]
    public void StateLetterNamesEachState()
    {
        var w = new McWorld();
        var ss = w.Add<datum_controller_subsystem_test_mc_a>("A");
        using (w.World.Enter())
        {
            (float state, string letter)[] cases =
            {
                (SS_RUNNING, "R"), (SS_QUEUED, "Q"), (SS_PAUSED, "P"), (SS_PAUSING, "P"), (SS_SLEEPING, "S"), (SS_IDLE, " "), (9, null),
            };
            foreach ((float state, string letter) in cases)
            {
                ss.state = state;
                Assert.Equal(letter, (string)ss.state_letter());
            }
        }
    }

    [Fact]
    public void UpdateNextfireWithResetTimeIgnoresOverrunQueuedTimeAndPostponedFires()
    {
        var w = new McWorld();
        var ticker = w.Add<datum_controller_subsystem_test_mc_a>("Ticker", ss => { ss.ss_flags = SS_TICKER; ss.wait = 3; });
        var keep = w.Add<datum_controller_subsystem_test_mc_b>("Keep", ss => { ss.ss_flags = SS_KEEP_TIMING; ss.wait = 20; });
        var post = w.Add<datum_controller_subsystem_test_mc_c>("Post", ss => { ss.ss_flags = SS_POST_FIRE_TIMING; ss.wait = 20; });
        var plain = w.Add<datum_controller_subsystem_test_mc_d>("Plain", ss => { ss.wait = 20; });
        w.Step(10); // world.time 5
        using (w.World.Enter())
        {
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { ticker, keep, post, plain })
            {
                ss.tick_overrun = 100;
                ss.queued_time = 3;
                ss.next_fire = 7;
                ss.postponed_fires = 2;
            }
            // the normal update (subsystem.dm:190-197); tick_overrun adds tick_lag * overrun / 100
            ticker.update_nextfire();
            keep.update_nextfire();
            post.update_nextfire();
            plain.update_nextfire();
            Assert.Equal(5f + 1.5f, ticker.next_fire);
            Assert.Equal(7f + 20, keep.next_fire);
            Assert.Equal(5f + 20 + 0.5f, post.next_fire);
            Assert.Equal(3f + 20 + 0.5f, plain.next_fire);
            Assert.Equal(2f, plain.postponed_fires);

            // reset_time (:182-188)
            foreach (McFixtureSubsystem ss in new McFixtureSubsystem[] { ticker, keep, post, plain })
                ss.update_nextfire(TRUE);
            Assert.Equal(5f + 1.5f, ticker.next_fire);
            Assert.Equal(5f + 20, keep.next_fire);
            Assert.Equal(5f + 20, post.next_fire);
            Assert.Equal(5f + 20, plain.next_fire);
            Assert.All(new McFixtureSubsystem[] { ticker, keep, post, plain }, ss => Assert.Equal(0f, ss.postponed_fires));
        }
    }

    [Fact]
    public void PostponeAddsWholeCyclesOnlyWhenTheSubsystemCanFire()
    {
        var w = new McWorld();
        var ss = w.Add<datum_controller_subsystem_test_mc_a>("A");
        using (w.World.Enter())
        {
            ss.postpone();
            Assert.Equal(1f, ss.postponed_fires);
            ss.postpone(2);
            Assert.Equal(3f, ss.postponed_fires);
            ss.postpone(0.5f);
            ss.postpone(0);
            Assert.Equal(3f, ss.postponed_fires);
            ss.can_fire = FALSE;
            ss.postpone(5);
            Assert.Equal(3f, ss.postponed_fires);
        }
    }

    [Fact]
    public void ADeletedSubsystemLeavesTheQueueAndTheMasterAndNeverFiresAgain()
    {
        var w = new McWorld();
        using (w.World.Enter())
            Dm.@new<datum_controller_subsystem_garbage>(); // qdel queues through SSgarbage (DV7)
        Action<McFixtureSubsystem> fires = ss =>
        {
            ss.ss_flags = SS_NO_INIT;
            ss.wait = 10;
            ss.init_stage = INITSTAGE_LAST;
            ss.runlevels = LobbyAndDefault;
        };
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", fires);
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", fires);
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", fires);
        w.Boot();
        w.Step(30);
        using (w.World.Enter())
        {
            a.enqueue();
            b.enqueue();
            c.enqueue();
            qdel(b);
            // subsystem.dm:167-173
            Assert.Equal(new[] { "A", "C" }, w.QueueNames());
            Assert.False(Dm.@in(b, w.Mc.subsystems));
            Assert.Equal(0f, b.can_fire);
            Assert.True(Dm.truthy(Dm.bitand(b.ss_flags, SS_NO_FIRE)));
            Assert.True(QDELETED(b));
            a.dequeue();
            c.dequeue();
            w.Mc.queue_priority_count = 0;
        }
        w.Step(40);
        Assert.Equal(new long[] { 1, 21 }, b.fires);
        Assert.Equal(new long[] { 1, 21, 41, 61 }, a.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void WithRollingUsageEachRunLogsItsTickAndUsageAndTheLogKeepsTheLastFiveSeconds()
    {
        var w = new McWorld();
        var ticker = w.Add<datum_controller_subsystem_test_mc_a>("Ticker", ss =>
        {
            ss.ss_flags = Dm.bitor(SS_TICKER, SS_NO_INIT);
            ss.wait = 1;
            ss.init_stage = INITSTAGE_LAST;
            ss.runlevels = LobbyAndDefault;
        });
        using (w.World.Enter())
            w.Mc.use_rolling_usage = TRUE;
        w.Boot();
        w.Step(150);

        using (w.World.Enter())
        {
            // RunQueue prunes, then appends (DS2TICKS(world.time), tick usage) (master.dm:906-910); prune_rolling_usage
            // drops the pairs logged before world.time - rolling_usage_length (5 SECONDS, 100 ticks; subsystem.dm:318-324)
            DmList log = ticker.rolling_usage;
            Assert.Equal(2f * 101, log.len);
            for (int i = 0; i <= 100; i++)
            {
                Assert.True(Dm.eq(log[2 * i + 1], 50 + i));
                Assert.True(Dm.eq(log[2 * i + 2], 0));
            }
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void ShutdownStopsProcessingAndShutsTheSubsystemsDownInReverseInitOrder()
    {
        var w = new McWorld();
        Action<McFixtureSubsystem> stage(float s) => ss => { ss.ss_flags = SS_NO_FIRE; ss.init_stage = s; };
        var a = w.Add<datum_controller_subsystem_test_mc_a>("A", stage(INITSTAGE_EARLY));
        var b = w.Add<datum_controller_subsystem_test_mc_b>("B", stage(INITSTAGE_LAST));
        var c = w.Add<datum_controller_subsystem_test_mc_c>("C", stage(INITSTAGE_MAIN));
        w.Boot();
        w.Step(1);
        using (w.World.Enter())
            Waiting.NoSleep(w.Mc.Shutdown());

        Assert.Equal(new[] { "A init", "C init", "B init" }, w.Trace.Where(l => l.EndsWith(" init", StringComparison.Ordinal)).Select(l => l[(l.IndexOf(' ') + 1)..]));
        Assert.Equal(new[] { "B shutdown", "C shutdown", "A shutdown" }, w.TraceAt(1).Where(l => l.EndsWith("shutdown", StringComparison.Ordinal)));
        using (w.World.Enter())
        {
            Assert.Equal(0f, w.Mc.processing);
            Assert.Equal(new object[] { b, c, a }, Dm.each(w.Mc.subsystems)); // sorted by init_order, then reversed (:137-138)
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void StartLoadingMapWaitsForTheMapBeingLoadedThenNotifiesEverySubsystem()
    {
        var w = new McWorld();
        w.Add<datum_controller_subsystem_test_mc_a>("A");
        w.Add<datum_controller_subsystem_test_mc_b>("B");
        DmTask first, second;
        using (w.World.Enter())
        {
            first = w.Mc.StartLoadingMap();
            Assert.True(first.IsCompleted);
            Assert.Equal(TRUE, w.Mc.map_loading);
            second = w.Mc.StartLoadingMap(); // "disallow more than one map to load at once": stoplag until it is done
            Assert.False(second.IsCompleted);
        }
        w.Step(5);
        Assert.False(second.IsCompleted);
        using (w.World.Enter())
            w.Mc.StopLoadingMap();
        w.Step(1);
        Assert.True(second.IsCompleted);
        Assert.Equal(new[]
        {
            "0 A start loading map", "0 B start loading map",
            "5 A stop loading map", "5 B stop loading map",
            "6 A start loading map", "6 B start loading map",
        }, w.Trace);
        using (w.World.Enter())
            Assert.Equal(TRUE, w.Mc.map_loading);
    }

    [Fact]
    public void OnConfigLoadReachesEverySubsystemInListOrder()
    {
        var w = new McWorld();
        w.Add<datum_controller_subsystem_test_mc_b>("B");
        w.Add<datum_controller_subsystem_test_mc_a>("A");
        using (w.World.Enter())
            Waiting.NoSleep(w.Mc.OnConfigLoad());
        Assert.Equal(new[] { "B config loaded", "A config loaded" }, w.TraceAt(0));
    }

    [Fact]
    public void RecreateMcRefusesWithinItsTimeoutAndHalvesTheRestartCountBeforeRestartClear()
    {
        var w = new McWorld();
        datum_controller_master old = w.Mc;
        using (w.World.Enter())
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            // the new Master's New calls Recover (master.dm:104), which is not translated (rules 13): new fails, -1 (:268-269)
            Assert.True(Dm.eq(Waiting.NoSleep(Recreate_MC()), -1));
            Assert.Equal(0f, old.processing); // "stop ticking this one" (:264-265)
            Assert.Equal(1f, datum_controller_master.restart_count);
            Assert.Equal(50f, datum_controller_master.restart_timeout);
            Assert.Equal(100f, datum_controller_master.restart_clear);
            Assert.True(Dm.eq(Waiting.NoSleep(Recreate_MC()), 0)); // within restart_timeout (:258-259)
        }
        w.Step(120); // world.time 60: past the timeout, before restart_clear
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(Waiting.NoSleep(Recreate_MC()), -1));
            Assert.Equal(1.5f, datum_controller_master.restart_count); // 1 * 0.5, then ++ (:260-262)
            Assert.Equal(60f + 75, datum_controller_master.restart_timeout);
            Assert.Equal(60f + 150, datum_controller_master.restart_clear);
            Assert.Same(old, Master);
        }
    }

    [Fact]
    public void WithTheOverviewFastUpdateTheLoopCallsTheDeferredUiRefreshEachIteration()
    {
        var w = new McWorld();
        int before; // the configuration's deferred leaves (TgTestGenesis loads it): not the MC's
        using (w.World.Enter())
        {
            w.Mc.overview_fast_update = TRUE;
            before = DmWorldState.Current.DeferredCount;
        }
        w.StartAllStagesInitialized(); // the first iteration, at tick 0
        w.Step(5);
        using (w.World.Enter())
        {
            Assert.Equal(before + 6, DmWorldState.Current.DeferredCount);
            Assert.Contains("code/controllers/master.dm:251", DmWorldState.Current.DeferredSites);
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void TheMcAveragesWeighTheOldAndTheNewValue()
    {
        Assert.Equal(7f, MC_AVERAGE_FAST(10, 0), 4);
        Assert.Equal(8f, MC_AVERAGE(10, 0), 4);
        Assert.Equal(9f, MC_AVERAGE_SLOW(10, 0), 4);
        Assert.Equal(9f, MC_AVG_FAST_UP_SLOW_DOWN(10, 0), 4); // falling: slow
        Assert.Equal(3f, MC_AVG_FAST_UP_SLOW_DOWN(0, 10), 4); // rising: fast
        Assert.Equal(1f, MC_AVG_SLOW_UP_FAST_DOWN(0, 10), 4);
        Assert.Equal(7f, MC_AVG_SLOW_UP_FAST_DOWN(10, 0), 4);
        Assert.Equal(4.2f, MC_AVG_OVER_TIME(4, 1, 10, 2), 4); // (10 - 2) / 10 * 4 + 1
        Assert.Equal(4.2f, MC_AVG_SECONDS(4, 1, 2), 4); // 1 SECONDS is 10 ds
        Assert.Equal(599f / 600 * 4 + 1, MC_AVG_MINUTES(4, 1, 1), 4);
    }

    [Fact]
    public void TheStackEndDetectorPrimesOnceAndChecksItsCanary()
    {
        var w = new McWorld();
        using (w.World.Enter())
        {
            datum_stack_end_detector detector = Dm.@new<datum_stack_end_detector>();
            Assert.True(Dm.truthy(detector.check())); // before priming
            object canary = detector.prime_canary();
            Assert.IsType<datum_stack_canary>(canary);
            Assert.True(Dm.truthy(detector.check())); // the canary is alive
            DmCrashException e = Assert.Throws<DmCrashException>(() => detector.prime_canary());
            Assert.Equal("Prime_canary called twice", e.Message);
            GC.KeepAlive(canary);
        }
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameWorldStateAndTheSameFires()
    {
        (ulong hash, List<string> trace) Run()
        {
            var w = new McWorld(seed: 5);
            Action<McFixtureSubsystem> fires(float flags, float wait, float runlevels, float init_stage) => ss =>
            {
                ss.ss_flags = flags;
                ss.wait = wait;
                ss.runlevels = runlevels;
                ss.init_stage = init_stage;
            };
            w.Add<datum_controller_subsystem_test_mc_a>("Ticker One", fires(SS_TICKER, 1, LobbyAndDefault, INITSTAGE_LAST));
            w.Add<datum_controller_subsystem_test_mc_b>("Ticker Two", fires(SS_TICKER, 3, LobbyAndDefault, INITSTAGE_EARLY));
            w.Add<datum_controller_subsystem_test_mc_c>("Lobby Main", fires(NONE, 10, LobbyAndDefault, INITSTAGE_MAIN));
            w.Add<datum_controller_subsystem_test_mc_d>("Game", fires(SS_KEEP_TIMING, 15, RUNLEVEL_GAME, INITSTAGE_MAIN));
            w.Add<datum_controller_subsystem_test_mc_e>("Background", fires(Dm.bitor(SS_BACKGROUND, SS_POST_FIRE_TIMING), 5, RUNLEVELS_DEFAULT, INITSTAGE_LAST));
            var sleeper = w.Add<datum_controller_subsystem_test_mc_f>("Sleeper", fires(NONE, 10, LobbyAndDefault, INITSTAGE_LAST));
            using (w.World.Enter())
            {
                sleeper.fire_sleep_ds = 7;
                sleeper.fire_sleeps_left = 2;
            }
            w.Boot();
            w.Step(120);
            using (w.World.Enter())
                w.Mc.SetRunLevel(RUNLEVEL_GAME);
            w.Step(200);
            Assert.Empty(w.DmRuntimes);
            return (w.World.ComputeStateHash(), w.Trace);
        }

        (ulong hash1, List<string> trace1) = Run();
        (ulong hash2, List<string> trace2) = Run();
        Assert.Equal(trace1, trace2);
        Assert.Equal(hash1, hash2);
        Assert.Contains(trace1, l => l.EndsWith("Sleeper paused, state 5", StringComparison.Ordinal));
        Assert.Contains(trace1, l => l.EndsWith("Game fire", StringComparison.Ordinal));
    }
}
