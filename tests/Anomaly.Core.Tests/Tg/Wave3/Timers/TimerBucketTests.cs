using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

/// <summary>
/// The bucket machinery of code/controllers/subsystem/timer.dm, written from the DM: the wait rounding of _addtimer
/// (:622-625), bucketJoin's head insertion (:585-589) and fire's pops from the head (:158), the secondary queue
/// (TIMER_MAX, :6, :556) and its migration (:187-214) with the for-loop var of controllers.md hazard 5 (rules 6.6), and
/// the wrap of the buckets (:143-146). Golden test (b) of docs/ai/specs/m0-reading/controllers.md section 11.
/// </summary>
public class TimerBucketTests
{
    [Fact]
    public void TimersDueInTheSameTickRunLastCreatedFirst()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "a", 1);
            TimerWorld.Ring(bell, "b", 1);
            TimerWorld.Ring(bell, "c", 1);
            TimerWorld.Ring(t.Bell(), "d", 1); // another target in the same bucket
        });
        t.Tick(2);
        Assert.Equal(new[] { "d@1", "c@1", "b@1", "a@1" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void AWaitOfZeroRunsInTheNextTickNeverInTheCurrentOne()
    {
        var t = new TimerWorld();
        t.Tick(4); // world.time 2
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "zero", 0);
            TimerWorld.Ring(bell, "tenth", 0.1f);
            TimerFire(); // a fire later in the same tick: nothing is due before world.time + tick_lag (:625)
        });
        Assert.Empty(t.Log);
        t.Tick(1);
        Assert.Equal(new[] { "tenth@2.5", "zero@2.5" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Theory]
    [InlineData(0f, 0.5f)]
    [InlineData(0.1f, 0.5f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.6f, 1f)]
    [InlineData(1f, 1f)]
    [InlineData(1.01f, 1.5f)]
    [InlineData(37.2f, 37.5f)]
    public void TheWaitIsRoundedUpToTheTickWithAOneTickMinimum(float wait, float rounded)
    {
        var t = new TimerWorld();
        object id = t.In(() => TimerWorld.Ring(t.Bell(), "w", wait, TIMER_STOPPABLE));
        t.In(() =>
        {
            // wait = max(CEILING(wait, world.tick_lag), world.tick_lag) (timer.dm:625)
            Assert.Equal(rounded, Dm.num(TimerWorld.Event(id).wait));
            Assert.True(Dm.eq(timeleft(id), rounded));
        });
        t.Tick((int)(rounded / 0.5f) - 1);
        Assert.Empty(t.Log);
        t.Tick(1);
        Assert.Equal(new[] { "w@" + Dm.str(rounded) }, t.Log);
    }

    [Fact]
    public void ATimerOfExactlyTimerMaxGoesToTheSecondaryQueueAndOneTickLessToTheLastBucket()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            // TIMER_MAX = head_offset + TICKS2DS(BUCKET_LEN + practical_offset - 1) = 0 + 600 (timer.dm:6); >= goes to the
            // secondary queue (:556)
            Assert.Equal(600f, datum_controller_subsystem_timer.TIMER_MAX(SStimer));
            object last = TimerWorld.Ring(bell, "599.5", 599.5f, TIMER_STOPPABLE);
            object queued = TimerWorld.Ring(bell, "600", 600f, TIMER_STOPPABLE);
            Assert.Equal(1f, SStimer.bucket_count);
            Assert.Same(TimerWorld.Event(last), SStimer.bucket_list[1200]); // BUCKET_POS: (1199 + 1) % 1200 || 1200 (:4)
            Assert.Equal(1200f, TimerWorld.Event(last).bucket_pos);
            Assert.Equal(new object[] { TimerWorld.Event(queued) }, Dm.each(SStimer.second_queue).Cast<object>());
            Assert.Equal(-1f, TimerWorld.Event(queued).bucket_pos);
            Assert.Equal(FALSE, TimerWorld.Event(queued).bucket_joined);
        });
        t.Tick(1199);
        Assert.Equal(new[] { "599.5@599.5" }, t.Log);
        t.Tick(1);
        Assert.Equal(new[] { "599.5@599.5", "600@600" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void LongTimersMigrateFromTheSecondaryQueueAndStillRunLastCreatedFirst()
    {
        var t = new TimerWorld();
        datum_timedevent a = null, b = null, c = null, d = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            a = TimerWorld.Event(TimerWorld.Ring(bell, "a", 61 * SECONDS, TIMER_STOPPABLE));
            d = TimerWorld.Event(TimerWorld.Ring(bell, "d", 60.5f * SECONDS, TIMER_STOPPABLE));
            b = TimerWorld.Event(TimerWorld.Ring(bell, "b", 61 * SECONDS, TIMER_STOPPABLE));
            c = TimerWorld.Event(TimerWorld.Ring(bell, "c", 61 * SECONDS, TIMER_STOPPABLE));
            // BINARY_INSERT by timeToRun, ties after the equal ones (__HELPERS/_lists.dm:142-160)
            Assert.Equal(new object[] { d, a, b, c }, Dm.each(SStimer.second_queue).Cast<object>());
            Assert.Equal(0f, SStimer.bucket_count);
        });

        // world.time 5: draining bucket 11 moves practical_offset to 12 and TIMER_MAX to 605.5, so d (605) migrates and
        // the loop stops at a (610): i-- leaves i = 1 and Cut(1, i + 1) cuts d alone (timer.dm:191-214)
        t.Tick(9);
        t.In(() => Assert.Equal(4, SStimer.second_queue.len));
        t.Tick(1);
        t.In(() =>
        {
            Assert.Equal(new object[] { a, b, c }, Dm.each(SStimer.second_queue).Cast<object>());
            Assert.Equal(1f, SStimer.bucket_count);
            Assert.Equal(TRUE, d.bucket_joined);
            Assert.Equal(11f, d.bucket_pos); // (1210 + 1) % 1200: the bucket just drained, now standing for 605
        });

        // world.time 10: TIMER_MAX 610.5, the whole queue migrates and the loop ends normally: i holds the last value in
        // range (3), so Cut(1, 4) empties it (rules 6.6, controllers.md hazard 5)
        t.Tick(10);
        t.In(() =>
        {
            Assert.Equal(0, SStimer.second_queue.len);
            Assert.Equal(4f, SStimer.bucket_count);
            // bucketJoin in queue order inserts each at the head (:585-589): c, b, a
            Assert.Same(c, SStimer.bucket_list[21]);
            Assert.Same(b, c.next);
            Assert.Same(a, b.next);
            Assert.Null(a.next);
        });

        t.Tick(1200);
        Assert.Equal(new[] { "d@605", "c@610", "b@610", "a@610" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void AMigratedTimerAndADirectlyBucketedOneOfTheSameTickRunLastCreatedFirst()
    {
        var t = new TimerWorld();
        t.In(() => TimerWorld.Ring(t.Bell(), "queued first", 610)); // secondary queue, migrates at world.time 10
        t.Tick(200); // world.time 100
        t.In(() =>
        {
            Assert.Equal(0, SStimer.second_queue.len);
            TimerWorld.Ring(t.Bell(), "bucketed later", 510); // TTR 610, straight into the same bucket
        });
        t.Tick(1020);
        Assert.Equal(new[] { "bucketed later@610", "queued first@610" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void AFireAfterMissedTicksRunsEveryOverdueBucketInTimeOrder()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "w3", 3);
            TimerWorld.Ring(bell, "w1", 1);
            TimerWorld.Ring(bell, "w2a", 2);
            TimerWorld.Ring(bell, "w2b", 2);
            TimerWorld.Ring(bell, "w6", 6);
        });
        t.World.Step(10); // world.time 5 without a fire: the MC did not fire SStimer
        t.In(TimerFire);
        // the while loop drains bucket after bucket up to world.time (timer.dm:156); each bucket last-created-first
        Assert.Equal(new[] { "w1@5", "w2b@5", "w2a@5", "w3@5" }, t.Log);
        t.In(() => Assert.Equal(12f, SStimer.practical_offset)); // the buckets of 0 .. 5 drained
        t.Tick(2);
        Assert.Equal("w6@6", t.Log[^1]);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TheBucketsWrapEveryMinuteAndALoopingTimerKeepsItsPeriodAcrossWraps()
    {
        var t = new TimerWorld();
        object id = t.In(() => TimerWorld.Ring(t.Bell(), "l", 10 * SECONDS, TIMER_LOOP | TIMER_STOPPABLE));
        t.Tick(2400); // 1200 ds: two wraps of BUCKET_LEN ticks
        t.In(() =>
        {
            // practical_offset passed BUCKET_LEN twice: head_offset += TICKS2DS(BUCKET_LEN) each time (timer.dm:143-146)
            Assert.Equal(1200f, SStimer.head_offset);
            Assert.Equal(2f, SStimer.practical_offset);
            Assert.True(Dm.eq(timeleft(id), 100f));
        });
        Assert.Equal(Enumerable.Range(1, 12).Select(i => "l@" + i * 100), t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void BucketCountTracksTheBucketedTimersAndNotTheQueuedOnes()
    {
        var t = new TimerWorld();
        object near = null, far = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            near = TimerWorld.Ring(bell, "near", 5, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "near2", 5);
            far = TimerWorld.Ring(bell, "far", 700, TIMER_STOPPABLE);
            Assert.Equal(2f, SStimer.bucket_count);
            Assert.True(Dm.eq(deltimer(far), TRUE)); // out of the secondary queue: bucket_count unchanged (timer.dm:479-483)
            Assert.Equal(2f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.second_queue.len);
            Assert.True(Dm.eq(deltimer(near), TRUE)); // bucket_joined behind near2, the head: bucket_count-- (:477-478)
            Assert.Equal(1f, SStimer.bucket_count);
        });
        t.Tick(10);
        Assert.Equal(new[] { "near2@5" }, t.Log);
        t.In(() => Assert.Equal(0f, SStimer.bucket_count));
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void RemovingATimerFromTheMiddleOfABucketKeepsTheOthersLinked()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "a", 2);
            object b = TimerWorld.Ring(bell, "b", 2, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "c", 2);
            datum_timedevent eb = TimerWorld.Event(b);
            datum_timedevent ec = eb.prev, ea = eb.next;
            Assert.True(Dm.eq(deltimer(b), TRUE)); // bucket_joined, not the head: prev and next re-linked (:487-491)
            Assert.Same(ea, ec.next);
            Assert.Same(ec, ea.prev);
            Assert.Null(eb.prev);
            Assert.Null(eb.next);
            Assert.Equal(-1f, eb.bucket_pos);
            Assert.Equal(2f, SStimer.bucket_count);
        });
        t.Tick(4);
        Assert.Equal(new[] { "c@2", "a@2" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    private static void TimerFire() => TimerWorld.FireTimer();
}
