using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

/// <summary>
/// TIMER_CLIENT_TIME timers (timer.dm:106-139, REALTIMEOFDAY with its midnight rollover, __DEFINES/maths.dm:26-27) and
/// reset_buckets (timer.dm:234-332) with the 1.5-minute auto-reset of fire (:91-104), written from the DM. The three
/// numeric for loops whose var is read after the loop (:110, :192, :292; controllers.md hazard 5, rules 6.6) are pinned
/// here and in TimerBucketTests.
/// </summary>
public class TimerClientTimeAndResetTests
{
    // ---- TIMER_CLIENT_TIME ----

    [Fact]
    public void ALoopingClientTimerReinsertedOnTheLastPassSurvivesTheCut()
    {
        var t = new TimerWorld();
        t.In(() => TimerWorld.Ring(t.Bell(), "cl", 2, TIMER_CLIENT_TIME | TIMER_LOOP | TIMER_STOPPABLE));
        t.Tick(4);
        t.In(() =>
        {
            // the timer was the list's last entry: BINARY_INSERT put it again at index 2, past the loop's end; the loop
            // var holds the last value in range (1), so Cut(1, next_clienttime_timer_index + 1) keeps the new entry
            // (timer.dm:110, :132, :137-139)
            datum_timedevent cl = Assert.IsType<datum_timedevent>(Assert.Single(Dm.each(SStimer.clienttime_timers).Cast<object>()));
            Assert.True(Dm.eq(cl.timeToRun, 4f));
            Assert.Equal(0f, SStimer.next_clienttime_timer_index);
        });
        t.Tick(16);
        Assert.Equal(new[] { "cl@2", "cl@4", "cl@6", "cl@8", "cl@10" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ALoopingClientTimerReinsertedBeforeALaterOneKeepsTheListInTimeOrder()
    {
        var t = new TimerWorld();
        datum_timedevent cl = null, c = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            cl = TimerWorld.Event(TimerWorld.Ring(bell, "cl", 2, TIMER_CLIENT_TIME | TIMER_LOOP | TIMER_STOPPABLE));
            c = TimerWorld.Event(TimerWorld.Ring(bell, "c", 3, TIMER_CLIENT_TIME | TIMER_STOPPABLE));
        });
        t.Tick(4); // world.time 2: cl ran and went back in behind c; the pass stopped at c (index 2, then i-- and break)
        t.In(() => Assert.Equal(new object[] { c, cl }, Dm.each(SStimer.clienttime_timers).Cast<object>()));
        t.Tick(2); // c ran and was deleted; cut with it
        t.In(() => Assert.Equal(new object[] { cl }, Dm.each(SStimer.clienttime_timers).Cast<object>()));
        t.Tick(6);
        Assert.Equal(new[] { "cl@2", "c@3", "cl@4", "cl@6" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TgQuirk_ClientTimersRunBeforeTheBucketsAndFirstCreatedFirst()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "b1", 1);
            TimerWorld.Ring(bell, "c1", 1, TIMER_CLIENT_TIME);
            TimerWorld.Ring(bell, "c2", 1, TIMER_CLIENT_TIME);
            TimerWorld.Ring(bell, "b2", 1);
        });
        t.Tick(2);
        // the client-time list is sorted with ties after the equal ones (BINARY_INSERT) and run from its start, the
        // buckets are run from their heads (timer.dm:106-139 before :156)
        Assert.Equal(new[] { "c1@1", "c2@1", "b2@1", "b1@1" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.2f, 1f)]
    [InlineData(1f, 1f)]
    [InlineData(2.5f, 3f)]
    public void AClientTimersWaitIsRoundedUpToAWholeDecisecond(float wait, float rounded)
    {
        var t = new TimerWorld();
        object id = t.In(() => TimerWorld.Ring(t.Bell(), "c", wait, TIMER_CLIENT_TIME | TIMER_STOPPABLE));
        // wait = max(ceil(wait), 1) (timer.dm:622-623); timeleft counts against REALTIMEOFDAY (:700)
        t.In(() => Assert.True(Dm.eq(timeleft(id), rounded)));
        t.Tick((int)(rounded * 2) - 1);
        Assert.Empty(t.Log);
        t.Tick(1);
        Assert.Equal(new[] { "c@" + Dm.str(rounded) }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void AClientTimerRunsOnTimeAcrossMidnight()
    {
        var t = new TimerWorld(realtimeStart: 864000 - 2); // world.timeofday is 2 ds before midnight at world.time 0
        object id = t.In(() =>
        {
            object i = TimerWorld.Ring(t.Bell(), "m", 5, TIMER_CLIENT_TIME | TIMER_STOPPABLE);
            Assert.True(Dm.eq(TimerWorld.Event(i).timeToRun, 864003f));
            return i;
        });
        t.Tick(6); // world.time 3: world.timeofday is 1 again, REALTIMEOFDAY adds one MIDNIGHT_ROLLOVER
        t.In(() =>
        {
            Assert.Equal(1f, Dm.world.timeofday);
            Assert.Equal(1f, TgState.Current.GLOB.midnight_rollovers);
            Assert.True(Dm.eq(timeleft(id), 2f));
        });
        t.Tick(3);
        Assert.Empty(t.Log);
        t.Tick(1);
        Assert.Equal(new[] { "m@5" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ADeletedClientTimerLeavesTheClientTimeList()
    {
        var t = new TimerWorld();
        test_timers_bell owner = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            owner = t.Bell();
            object id = TimerWorld.Ring(bell, "by id", 2, TIMER_CLIENT_TIME | TIMER_STOPPABLE);
            TimerWorld.Ring(owner, "by owner", 2, TIMER_CLIENT_TIME);
            TimerWorld.Ring(bell, "kept", 3, TIMER_CLIENT_TIME);
            Assert.True(Dm.eq(deltimer(id), TRUE));
            qdel(owner);
            // Destroy of an unspent client timer: clienttime_timers -= src (timer.dm:439-443)
            Assert.Equal(1, SStimer.clienttime_timers.len);
            Assert.Equal(0f, SStimer.bucket_count);
        });
        t.Tick(8);
        Assert.Equal(new[] { "kept@3" }, t.Log);
        t.In(() => Assert.Equal(0, SStimer.clienttime_timers.len));
        Assert.Empty(t.Runtimes);
    }

    // ---- reset_buckets ----

    [Fact]
    public void TgQuirk_AResetKeepsEveryTimerOnTimeButRunsSameTickTimersFirstCreatedFirst()
    {
        var t = new TimerWorld(notTranslatedThrows: false); // reset_buckets WARNINGs: warning is the logging package's
        datum_timedevent e = null, f = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "a", 1);
            TimerWorld.Ring(bell, "b", 1);
            TimerWorld.Ring(bell, "c", 1);
            TimerWorld.Ring(bell, "d", 3);
            e = TimerWorld.Event(TimerWorld.Ring(bell, "e", 61 * SECONDS, TIMER_STOPPABLE));
            f = TimerWorld.Event(TimerWorld.Ring(bell, "f", 62 * SECONDS, TIMER_STOPPABLE));
        });
        t.Tick(1);
        t.In(() => SStimer.bucket_resolution = null); // what fire's invalid-timer branches do (timer.dm:164, :200, :207)
        t.Tick(1); // world.time 1: reset_buckets, then the drain
        t.In(() =>
        {
            Assert.Equal(1f, SStimer.bucket_reset_count);
            Assert.Equal(1f, SStimer.head_offset); // world.time (:258); no timer is overdue
            Assert.Equal(0.5f, SStimer.bucket_resolution);
            // the loop over alltimers stopped at e (i-- and break): Cut(1, i + 1) left e and f queued (:292-331)
            Assert.Equal(new object[] { e, f }, Dm.each(SStimer.second_queue).Cast<object>());
            Assert.Equal(1f, SStimer.bucket_count);
        });
        // alltimers is collected head first and sorted stably, and the rebuild inserts at the head again (:242-249,
        // :277, :316-326): the bucket of world.time 1 now runs a, b, c, where it would have run c, b, a
        Assert.Equal(new[] { "a@1", "b@1", "c@1" }, t.Log);
        t.Tick(1240);
        Assert.Equal(new[] { "a@1", "b@1", "c@1", "d@3", "e@610", "f@620" }, t.Log);
        t.AssertOnlySkeletonReports();
    }

    [Fact]
    public void AResetThatBucketsEveryTimerLeavesAnEmptySecondaryQueue()
    {
        var t = new TimerWorld(notTranslatedThrows: false);
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "a", 1);
            TimerWorld.Ring(bell, "b", 2);
            TimerWorld.Ring(bell, "c", 2);
            SStimer.reset_buckets();
            // the loop over alltimers ended normally: i is its last value in range (3) and Cut(1, i + 1) cuts all
            // (timer.dm:292, :329-331; rules 6.6)
            Assert.NotNull(SStimer.second_queue);
            Assert.Equal(0, SStimer.second_queue.len);
            Assert.Equal(3f, SStimer.bucket_count);
            Assert.Equal(1200, SStimer.bucket_list.len);
        });
        t.Tick(4);
        Assert.Equal(new[] { "a@1", "b@2", "c@2" }, t.Log);
        t.AssertOnlySkeletonReports();
    }

    [Fact]
    public void AResetMovesTheHeadBackToTheEarliestOverdueTimer()
    {
        var t = new TimerWorld(notTranslatedThrows: false);
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "w2", 2);
            TimerWorld.Ring(bell, "w1", 1);
            TimerWorld.Ring(bell, "w3", 3);
        });
        t.World.Step(10); // world.time 5, SStimer did not fire
        t.In(() =>
        {
            SStimer.bucket_resolution = null;
            TimerWorld.FireTimer();
            Assert.Equal(1f, SStimer.head_offset); // the earliest timeToRun (timer.dm:282-284)
        });
        Assert.Equal(new[] { "w1@5", "w2@5", "w3@5" }, t.Log);
        t.AssertOnlySkeletonReports();
    }

    [Fact]
    public void AResetDropsASpentTimerFromTheBuckets()
    {
        var t = new TimerWorld(notTranslatedThrows: false);
        object id = t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object spent = TimerWorld.Ring(bell, "spent", 2, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "ok", 2);
            TimerWorld.Event(spent).spent = 1;
            return spent;
        });
        t.In(() =>
        {
            SStimer.reset_buckets();
            // "Invalid timer": WARNING and qdel (timer.dm:303-308); it is no longer counted
            Assert.Null(timeleft(id));
            Assert.Equal(0, SStimer.timer_id_dict.len);
            Assert.Equal(1f, SStimer.bucket_count);
        });
        t.Tick(4);
        Assert.Equal(new[] { "ok@2" }, t.Log);
        // the WARNING's text reads get_timer_debug_string, not translated (package decision)
        t.AssertOnlySkeletonReports("code/controllers/subsystem/timer.dm:221");
    }

    [Fact]
    public void TgQuirk_AQueuedTimerThatWouldNeedABacktrackIsCutFromTheQueueAndNeverRuns()
    {
        var t = new TimerWorld(notTranslatedThrows: false);
        object bad = null, good = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            bad = TimerWorld.Ring(bell, "bad", 610, TIMER_STOPPABLE);
            good = TimerWorld.Ring(bell, "good", 620, TIMER_STOPPABLE);
            TimerWorld.Event(bad).timeToRun = 0.25f; // a broken state: before the bucket the migration fills next
        });
        t.Tick(1);
        t.In(() =>
        {
            // timer.dm:206-210: bucket_resolution = null, stack_trace and a break without i--, so Cut(1, i + 1) cuts the
            // bad timer with the ones before it (:213-214)
            Assert.Null(SStimer.bucket_resolution);
            Assert.Equal(new object[] { TimerWorld.Event(good) }, Dm.each(SStimer.second_queue).Cast<object>());
        });
        t.Tick(1300); // the next fire resets the buckets from the buckets and the queue: the bad timer is in neither
        Assert.Equal(new[] { "good@620" }, t.Log);
        t.In(() =>
        {
            Assert.Equal(1f, SStimer.bucket_reset_count);
            Assert.Same(TimerWorld.Event(bad), SStimer.timer_id_dict[bad]); // lost, but still registered
            Assert.False(QDELETED(TimerWorld.Event(bad)));
        });
        Assert.Equal(208, Assert.IsType<DmCrashException>(Assert.Single(t.Runtimes, e => e is DmCrashException)).DmLine);
        t.Runtimes.RemoveAll(e => e is DmCrashException);
        t.AssertOnlySkeletonReports("code/controllers/subsystem/timer.dm:221"); // the stack_trace's text
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void AfterOneAndAHalfMinutesWithoutATimerRunTheFireWarnsAndResetsTheBuckets(bool autoReset, int resets)
    {
        var t = new TimerWorld(notTranslatedThrows: false);
        t.In(() =>
        {
            datum_controller_subsystem_timer.bucket_auto_reset = Dm.num(autoReset);
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "first", 1);
            TimerWorld.Ring(bell, "stuck", 5);
        });
        t.Tick(2); // "first" ran: last_invoke_tick 1
        t.World.Step(1998); // world.time 1000: the MC did not fire SStimer for 999 ds
        t.In(() =>
        {
            TimerWorld.FireTimer();
            // lit 1 < last_check 100, head_offset 0 < 100, last_invoke_warning 0 < 100 (timer.dm:97)
            Assert.Equal(1000f, datum_controller_subsystem_timer.last_invoke_warning);
            Assert.Equal((float)resets, SStimer.bucket_reset_count);
            Assert.Equal(1000f, SStimer.last_invoke_tick);
        });
        Assert.Equal(new[] { "first@1", "stuck@1000" }, t.Log);
        // message_admins, WARNING and CONFIG_GET are other packages' skeletons; dump_timer_buckets is not translated
        // (package decision: the debug dump of this broken-state path)
        t.AssertOnlySkeletonReports("code/controllers/subsystem/timer.dm:61");
        Assert.Contains(t.Runtimes, e => ((DmNotTranslatedException)e).Where == "code/controllers/subsystem/timer.dm:61");
    }
}
