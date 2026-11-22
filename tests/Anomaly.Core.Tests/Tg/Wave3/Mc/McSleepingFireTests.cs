using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Mc;

/// <summary>
/// A fire() that sleeps (controllers.md 3.8; scope R1): ignite is set waitfor = FALSE and sets '. = SS_SLEEPING' before
/// fire() (subsystem.dm:139-158), so RunQueue gets SS_SLEEPING and dequeues the subsystem in that state (master.dm:902-951);
/// CheckQueue skips it while it sleeps (:806); when fire() resumes, MC_TICK_CHECK sees state != SS_RUNNING and pause()
/// turns SS_SLEEPING into SS_PAUSING (:275-281), and ignite's tail requeues it as SS_PAUSED (subsystem.dm:153-158). The
/// tick-usage pause path is unreachable with the virtual tick usage (DV3) and not tested.
/// </summary>
public class McSleepingFireTests
{
    private static readonly float LobbyAndDefault = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT);

    private static Action<McFixtureSubsystem> Sleeper(float flags, float wait, float sleep_ds, float check_after_sleep = TRUE)
        => ss =>
        {
            ss.ss_flags = Dm.bitor(flags, SS_NO_INIT);
            ss.wait = wait;
            ss.init_stage = INITSTAGE_LAST;
            ss.runlevels = LobbyAndDefault;
            ss.fire_sleep_ds = sleep_ds;
            ss.fire_sleeps_left = sleep_ds > 0 ? 1 : 0;
            ss.check_after_sleep = check_after_sleep;
        };

    [Fact]
    public void IgniteReturnsSsSleepingWhileFireSleepsAndTheStateWhenFireReturns()
    {
        var w = new McWorld();
        var sleeper = w.Add<datum_controller_subsystem_test_mc_a>("Sleeper", Sleeper(NONE, 10, 1));
        var plain = w.Add<datum_controller_subsystem_test_mc_b>("Plain", Sleeper(NONE, 10, 0));
        using (w.World.Enter())
        {
            datum_controller_master.current_ticklimit = 60;
            plain.state = SS_RUNNING; // as RunQueue leaves it (master.dm:896)
            Assert.True(Dm.eq(plain.ignite(FALSE), SS_RUNNING)); // '. = state' (subsystem.dm:149)
            Assert.Equal(60f, plain.tick_allocation_last);
            Assert.Equal(MC_AVERAGE(0, 60), plain.tick_allocation_avg);

            sleeper.state = SS_RUNNING;
            Assert.True(Dm.eq(sleeper.ignite(FALSE), SS_SLEEPING)); // '. = SS_SLEEPING' at fire()'s sleep (:147-148)
            Assert.True(Dm.eq(sleeper.state, SS_RUNNING)); // ignite does not set it: RunQueue stores what ignite returned
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void ASleepingFireIsDequeuedAsSleepingThenRequeuedAsPausedAndResumed()
    {
        var w = new McWorld();
        var ss = w.Add<datum_controller_subsystem_test_mc_a>("Sleeper", Sleeper(NONE, 10, 2));
        using (w.World.Enter())
        {
            ss.priority = 50;
            ss.enqueue();
            Assert.True(Dm.eq(w.Mc.RunQueue(), 1));

            Assert.True(Dm.eq(ss.state, SS_SLEEPING));
            Assert.Null(w.Mc.queue_head);
            Assert.Equal(0f, w.Mc.queue_priority_count);
            Assert.Equal(1f, ss.times_fired); // RunQueue counts the run that slept (master.dm:937-938)
            Assert.True(Dm.eq(ss.queued_time, 0));
            Assert.Equal(0f + 10, ss.next_fire); // update_nextfire ran with the queued_time of 0 ds
        }
        w.Step(3);
        using (w.World.Enter())
            Assert.True(Dm.eq(ss.state, SS_SLEEPING)); // sleep(2) is 4 ticks
        w.Step(1);

        Assert.Equal(new[] { "Sleeper woke, state 4", "Sleeper paused, state 5" }, w.TraceAt(4));
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(ss.state, SS_PAUSED));
            Assert.Equal(1f, ss.slept_count);
            Assert.Equal(new[] { "Sleeper" }, w.QueueNames());
            Assert.Equal(50f, w.Mc.queue_priority_count);
            Assert.True(Dm.eq(ss.queued_priority, 50));
            // ignite keeps the queued_time it had, QT (subsystem.dm:155-158), which RunQueue had zeroed
            Assert.True(Dm.eq(ss.queued_time, 0));
            Assert.Equal(0f, ss.paused_ticks); // counted only for an SS_PAUSED return (master.dm:924-928)

            Assert.True(Dm.eq(w.Mc.RunQueue(), 1)); // RunQueue resumes it: queue_node_paused (:894)
            Assert.Equal(new long[] { 4 }, ss.resumed_fires);
            Assert.True(Dm.eq(ss.state, SS_IDLE));
            Assert.Equal(2f, ss.times_fired);
            Assert.Equal(2f, ss.last_fire);
            Assert.Equal(0f + 10, ss.next_fire); // from queued_time 0 again
            Assert.Null(w.Mc.queue_head);
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void AFireThatSleepsAndReturnsWithoutATickCheckGoesBackToIdle()
    {
        var w = new McWorld();
        var ss = w.Add<datum_controller_subsystem_test_mc_a>("Sleeper", Sleeper(NONE, 10, 2, check_after_sleep: FALSE));
        using (w.World.Enter())
        {
            ss.enqueue();
            w.Mc.RunQueue();
        }
        w.Step(4);
        Assert.Equal(new[] { "Sleeper woke, state 4", "Sleeper end" }, w.TraceAt(4));
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(ss.state, SS_IDLE)); // subsystem.dm:150-152
            Assert.Equal(1f, ss.slept_count);
            Assert.Null(w.Mc.queue_head);
            Assert.Equal(1f, ss.times_fired);
        }
    }

    [Fact]
    public void TgQuirk_InTheLoopASleptAndPausedFireRunsResumedAtOnceAndItsNextRunComesTheTickAfter()
    {
        var w = new McWorld();
        var sleeper = w.Add<datum_controller_subsystem_test_mc_a>("Sleeper", Sleeper(NONE, 10, 30));
        var plain = w.Add<datum_controller_subsystem_test_mc_b>("Plain", Sleeper(NONE, 10, 0));
        w.Boot();
        w.Step(30);
        using (w.World.Enter())
        {
            Assert.True(Dm.eq(sleeper.state, SS_SLEEPING));
            Assert.DoesNotContain("Sleeper", w.QueueNames());
        }
        w.Step(70); // to tick 100

        // sleep(30) from tick 1 wakes in tick 61, before the MC's own wake-up of that tick (it slept first, in tick 1):
        // fire() pauses, ignite requeues it as SS_PAUSED, the MC's RunQueue resumes it in the same tick, before Plain,
        // which CheckQueue enqueued after it (equal priority)
        Assert.Equal(new[]
        {
            "Sleeper woke, state 4", "Sleeper paused, state 5",
            "Sleeper fire resumed", "Sleeper end",
            "Plain fire", "Plain end",
        }, w.TraceAt(61));
        // no new run while it slept, although next_fire (10.5 ds) passed; the resumed run completes with queued_time 0
        // (subsystem.dm:155-158), so next_fire = 0 + 10 ds is already past and the next run comes in tick 62
        Assert.Equal(new long[] { 1, 62, 82 }, sleeper.fires);
        Assert.Equal(new long[] { 61 }, sleeper.resumed_fires);
        Assert.Equal(McCadenceTests.Every(1, 20, 100), plain.fires);
        using (w.World.Enter())
        {
            Assert.Equal(4f, sleeper.times_fired);
            Assert.Equal(1f, sleeper.slept_count);
            Assert.True(Dm.eq(sleeper.state, SS_IDLE));
        }
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void InTheLoopAFireThatSleptAndReturnedIdleIsRefiredInTheSameTickWhenItIsDue()
    {
        var w = new McWorld();
        var sleeper = w.Add<datum_controller_subsystem_test_mc_a>("Sleeper", Sleeper(NONE, 10, 30, check_after_sleep: FALSE));
        var plain = w.Add<datum_controller_subsystem_test_mc_b>("Plain", Sleeper(NONE, 10, 0));
        w.Boot();
        w.Step(100);

        // the woken fire() returns before the MC runs in tick 61 and leaves the subsystem SS_IDLE with next_fire 10.5 ds
        // (set when it slept), so that tick's CheckQueue enqueues it again, after Plain (equal priority, name order)
        Assert.Equal(new[] { "Sleeper woke, state 4", "Sleeper end", "Plain fire", "Plain end", "Sleeper fire", "Sleeper end" },
            w.TraceAt(61));
        Assert.Equal(new long[] { 1, 61, 81 }, sleeper.fires);
        Assert.Empty(sleeper.resumed_fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void ASleepingTickerIsNotFiredAgainUntilItsFireReturns()
    {
        var w = new McWorld();
        var ticker = w.Add<datum_controller_subsystem_test_mc_a>("Ticker", Sleeper(SS_TICKER, 1, 5));
        w.Boot();
        w.Step(40);

        long first = ticker.fires[0];
        Assert.InRange(first, 1, 2);
        Assert.Equal(new[] { first + 10 }, ticker.resumed_fires); // sleep(5) is 10 ticks
        // a ticker's next_fire is world.time + tick_lag * wait (subsystem.dm:191): the next run is the tick after
        Assert.Equal(new[] { first }.Concat(McCadenceTests.Every(first + 11, 1, 40)), ticker.fires);
        Assert.Empty(w.DmRuntimes);
    }

    [Fact]
    public void TgQuirk_AKeepTimingFireThatSleepsAdvancesNextFireTwiceAndSkipsACycle()
    {
        var w = new McWorld();
        var keep = w.Add<datum_controller_subsystem_test_mc_a>("Keep", Sleeper(SS_KEEP_TIMING, 10, 2));
        var control = w.Add<datum_controller_subsystem_test_mc_b>("Control", Sleeper(SS_KEEP_TIMING, 10, 0));
        w.Boot();
        w.Step(70);

        // Both first fire in tick 15 (last_fire 0 + 7.5 ds). RunQueue's update_nextfire (next_fire += wait) runs for the
        // run that slept (next_fire 10 ds) and again for the resumed run (20 ds), so the run due at 10 ds never happens.
        Assert.Equal(new long[] { 15, 30, 45, 60 }, control.fires);
        Assert.Equal(new long[] { 15, 40, 60 }, keep.fires);
        Assert.Equal(new long[] { 19 }, keep.resumed_fires);
        Assert.Empty(w.DmRuntimes);
    }
}
