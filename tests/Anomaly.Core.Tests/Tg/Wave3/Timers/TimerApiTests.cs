using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

/// <summary>
/// The timer API of code/controllers/subsystem/timer.dm, written from the DM: _addtimer (:611-657) with TIMER_STOPPABLE,
/// TIMER_UNIQUE, TIMER_OVERRIDE, TIMER_NO_HASH_WAIT, TIMER_LOOP and TIMER_DELETE_ME, /datum/timedevent New and Destroy
/// (:391-455), deltimer (:665), timeleft (:687), updatetimedelay (:710), the timers of a deleted datum (datums/datum.dm:
/// 133-139), and the timer callback as an isolation boundary (rules 9.2). Golden test (c) of controllers.md section 11.
/// </summary>
public class TimerApiTests
{
    // ---- TIMER_STOPPABLE and ids ----

    [Fact]
    public void StoppableTimersGetTextIdsInCreationOrderAndTheOthersTheNullId()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object s1 = TimerWorld.Ring(bell, "s1", 1, TIMER_STOPPABLE);
            object n = TimerWorld.Ring(bell, "n", 1);
            object s2 = TimerWorld.Ring(bell, "s2", 1, TIMER_STOPPABLE);
            // id = num2text(nextid, 100), nextid++ only for TIMER_STOPPABLE (timer.dm:409-415)
            Assert.Equal("1", Assert.IsType<string>(s1));
            Assert.True(Dm.eq(n, TIMER_ID_NULL));
            Assert.Equal("2", s2);
            Assert.Equal(2, SStimer.timer_id_dict.len);
            Assert.Equal("1", TimerWorld.Event(s1).id);
            // the unstoppable one is in the bucket with id TIMER_ID_NULL (:393)
            datum_timedevent unstoppable = TimerWorld.Event(s1).prev;
            Assert.True(Dm.eq(unstoppable.id, TIMER_ID_NULL));
            Assert.Same(TimerWorld.Event(s2), unstoppable.prev);
        });
        var other = new TimerWorld(); // nextid is a proc static: per world (rules 10.6)
        Assert.Equal("1", other.In(() => TimerWorld.Ring(other.Bell(), "x", 1, TIMER_STOPPABLE)));
    }

    [Fact]
    public void TgQuirk_TimerIdsStopAdvancingAtTheFloatLimit()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TgState.Current.datum_timedevent__New__nextid = SHORT_REAL_LIMIT - 1;
            object a = TimerWorld.Ring(bell, "a", 1, TIMER_STOPPABLE);
            object b = TimerWorld.Ring(bell, "b", 1, TIMER_STOPPABLE);
            object c = TimerWorld.Ring(bell, "c", 1, TIMER_STOPPABLE);
            // nextid += min(1, 2 ** round(nextid / SHORT_REAL_LIMIT)) adds 1, which a float32 2^24 cannot hold
            // (timer.dm:411-412): the ids repeat and the later timer takes the dictionary slot
            Assert.Equal("16777215", a);
            Assert.Equal("16777216", b);
            Assert.Equal("16777216", c);
            Assert.Equal(2, SStimer.timer_id_dict.len);
            Assert.Equal(16777216f, TgState.Current.datum_timedevent__New__nextid);
            Assert.True(Dm.eq(deltimer(c), TRUE)); // deletes the last one; b is out of reach by id
            Assert.True(Dm.eq(deltimer(c), FALSE));
        });
        t.Tick(2);
        Assert.Equal(new[] { "b@1", "a@1" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    // ---- TIMER_UNIQUE, TIMER_NO_HASH_WAIT, TIMER_OVERRIDE ----

    [Fact]
    public void AUniqueDuplicateIsNotCreatedAndGetsTheExistingId()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object first = TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE | TIMER_STOPPABLE);
            object again = TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE | TIMER_STOPPABLE);
            // TIMER_STOPPABLE is not part of the hash (timer.dm:635-639): a plain UNIQUE duplicate gets the id too
            object plain = TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE);
            Assert.Equal("1", first);
            Assert.Equal("1", again);
            Assert.Equal("1", plain);
            Assert.Equal(1, SStimer.hashes.len);
            Assert.Equal(1f, SStimer.bucket_count);
            Assert.Equal(1, SStimer.timer_id_dict.len);
            Assert.Same(TimerWorld.Event(first), SStimer.hashes[TimerWorld.Event(first).hash]);
        });
        t.Tick(10);
        Assert.Equal(new[] { "u@3" }, t.Log);
        t.In(() => Assert.Equal(0, SStimer.hashes.len)); // Destroy took its hash out (:427-428)
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TgQuirk_AUniqueDuplicateOfAnUnstoppableTimerReturnsNullNotTheNullId()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object first = TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE);
            object stoppable = TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE | TIMER_STOPPABLE);
            Assert.True(Dm.eq(first, TIMER_ID_NULL));
            // `. = hash_timer.id` only when the existing timer is stoppable, else a bare return (timer.dm:650-652)
            Assert.Null(stoppable);
            Assert.True(Dm.eq(deltimer(stoppable), FALSE));
            Assert.Equal(0, SStimer.timer_id_dict.len);
        });
        t.Tick(10);
        Assert.Equal(new[] { "u@3" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TheUniqueHashCoversTargetProcArgumentsWaitAndClientTime()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            test_timers_bell other = t.Bell();
            TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE);
            TimerWorld.Ring(bell, "u", 4, TIMER_UNIQUE); // another wait: another hash
            TimerWorld.Ring(bell, "v", 3, TIMER_UNIQUE); // other arguments
            TimerWorld.Ring(other, "u", 3, TIMER_UNIQUE); // another target ("([REF])" in the hash)
            TimerWorld.Add(bell, nameof(test_timers_bell.boom), "u", 3, TIMER_UNIQUE); // another proc
            TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE | TIMER_CLIENT_TIME); // flags & TIMER_CLIENT_TIME is hashed
            TimerWorld.Ring(bell, "u", 3, TIMER_UNIQUE | TIMER_LOOP); // other flags are not: a duplicate
            Assert.Equal(6, SStimer.hashes.len);
            Assert.Equal(5f, SStimer.bucket_count);
            Assert.Equal(1, SStimer.clienttime_timers.len);
        });
    }

    [Fact]
    public void NoHashWaitMakesTimersOfAnotherWaitDuplicatesThatKeepTheFirstTime()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object first = TimerWorld.Ring(bell, "n", 3, TIMER_UNIQUE | TIMER_NO_HASH_WAIT | TIMER_STOPPABLE);
            Assert.Equal(first, TimerWorld.Ring(bell, "n", 5, TIMER_UNIQUE | TIMER_NO_HASH_WAIT | TIMER_STOPPABLE));
            Assert.True(Dm.eq(timeleft(first), 3f));
        });
        t.Tick(20);
        Assert.Equal(new[] { "n@3" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void OverrideReplacesTheUniqueTimerWithTheNewOne()
    {
        var t = new TimerWorld();
        test_timers_bell bell = null;
        object old = t.In(() =>
        {
            bell = t.Bell();
            return TimerWorld.Ring(bell, "o", 3, TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_STOPPABLE);
        });
        t.Tick(2); // world.time 1
        t.In(() =>
        {
            datum_timedevent oldEvent = TimerWorld.Event(old);
            object fresh = TimerWorld.Ring(bell, "o", 3, TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_STOPPABLE);
            Assert.Equal("2", fresh);
            // the old one lost its hash first and was qdel'd (timer.dm:646-648)
            Assert.True(QDELETED(oldEvent));
            Assert.Null(oldEvent.hash);
            Assert.Null(timeleft(old));
            Assert.True(Dm.eq(deltimer(old), FALSE));
            Assert.Same(TimerWorld.Event(fresh), SStimer.hashes[TimerWorld.Event(fresh).hash]);
            Assert.Equal(1, SStimer.hashes.len);
            Assert.Equal(1f, SStimer.bucket_count);
            Assert.Equal(new object[] { TimerWorld.Event(fresh) }, Dm.each(bell._active_timers).Cast<object>());
        });
        t.Tick(10);
        Assert.Equal(new[] { "o@4" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void OverrideWithoutUniqueOnlyWarnsAndAddsATimer()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "x", 1, TIMER_OVERRIDE);
            TimerWorld.Ring(bell, "x", 1, TIMER_OVERRIDE);
        });
        t.Tick(2);
        Assert.Equal(new[] { "x@1", "x@1" }, t.Log);
        // stack_trace("TIMER_OVERRIDE used without TIMER_UNIQUE") at timer.dm:654, which reports and returns (rules 9.5)
        Assert.Equal(2, t.Runtimes.Count);
        Assert.All(t.Runtimes, e => Assert.Equal(654, Assert.IsType<DmCrashException>(e).DmLine));
    }

    [Fact]
    public void AUniqueTimerAddedAgainFromItsOwnCallbackRunsAgain()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            bell.rearms_left = 2;
            Assert.Equal("1", TimerWorld.Add(bell, nameof(test_timers_bell.rearm), "r", 1, TIMER_UNIQUE | TIMER_STOPPABLE));
        });
        t.Tick(10);
        // in its callback the running timer is spent, so _addtimer pretends it is gone and takes its hash away, so that
        // its Destroy does not delete the new one's (timer.dm:643-644, :427)
        Assert.Equal(new[] { "r@1", "rearmed=2", "r@2", "rearmed=3", "r@3" }, t.Log);
        t.In(() =>
        {
            Assert.Equal(0, SStimer.hashes.len);
            Assert.Equal(0, SStimer.timer_id_dict.len);
            Assert.Equal(0f, SStimer.bucket_count);
        });
        Assert.Empty(t.Runtimes);
    }

    // ---- TIMER_LOOP ----

    [Fact]
    public void ALoopingTimerRearmsFromItsFireTimeUntilDeleted()
    {
        var t = new TimerWorld();
        object id = t.In(() => TimerWorld.Ring(t.Bell(), "l", 1, TIMER_LOOP | TIMER_STOPPABLE));
        t.Tick(4); // world.time 2
        t.In(() => Assert.True(Dm.eq(timeleft(id), 1f)));
        t.World.Step(3); // world.time 3.5: SStimer missed the ticks of 2.5 and 3
        t.In(TimerWorld.FireTimer);
        // timeToRun = world.time + wait at the fire, not the old timeToRun + wait (timer.dm:179)
        t.In(() => Assert.True(Dm.eq(timeleft(id), 1f)));
        t.Tick(2);
        Assert.Equal(new[] { "l@1", "l@2", "l@3.5", "l@4.5" }, t.Log);
        t.In(() =>
        {
            Assert.True(Dm.eq(deltimer(id), TRUE));
            Assert.Equal(0f, SStimer.bucket_count);
        });
        t.Tick(10);
        Assert.Equal(4, t.Log.Count);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ALoopingTimerWithoutAnIdStopsWhenItsTargetIsDeleted()
    {
        var t = new TimerWorld();
        test_timers_bell bell = null;
        t.In(() =>
        {
            bell = t.Bell();
            Assert.True(Dm.eq(TimerWorld.Ring(bell, "l", 1, TIMER_LOOP), TIMER_ID_NULL));
        });
        t.Tick(5);
        t.In(() =>
        {
            qdel(bell); // between two runs the loop timer is not spent: the datum's Destroy qdels it (datum.dm:133-139)
            Assert.Equal(0f, SStimer.bucket_count);
        });
        t.Tick(10);
        Assert.Equal(new[] { "l@1", "l@2" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TgQuirk_ALoopingTimerOutlivesATargetThatDeletesItselfFromTheLoopsCallback()
    {
        var t = new TimerWorld();
        test_timers_bell bell = null;
        object id = t.In(() =>
        {
            bell = t.Bell();
            return TimerWorld.Add(bell, nameof(test_timers_bell.self_destruct), "sd", 1, TIMER_LOOP | TIMER_STOPPABLE);
        });
        t.Tick(2);
        t.In(() =>
        {
            // the running timer is spent while the datum's Destroy runs, so Destroy skips it (datum.dm:137-138) and the
            // loop re-arms (timer.dm:175-180); the callback still has its object, so it keeps calling the deleted datum
            Assert.True(QDELETED(bell));
            Assert.Null(bell._active_timers);
            Assert.True(Dm.eq(timeleft(id), 1f));
        });
        t.Tick(4);
        Assert.Equal(new[] { "sd@1", "sd@2", "sd@3" }, t.Log);
        t.In(() => Assert.True(Dm.eq(deltimer(id), TRUE)));
        t.Tick(4);
        Assert.Equal(3, t.Log.Count);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void DeleteMeLetsTheDatumsDestroyStopTheLoopRunningItsCallback()
    {
        var t = new TimerWorld();
        t.In(() => TimerWorld.Add(t.Bell(), nameof(test_timers_bell.self_destruct), "sd", 1, TIMER_LOOP | TIMER_STOPPABLE | TIMER_DELETE_ME));
        t.Tick(10);
        Assert.Equal(new[] { "sd@1" }, t.Log); // QDELETED in the callback: not re-inserted (timer.dm:176-177)
        t.In(() =>
        {
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.timer_id_dict.len);
        });
        Assert.Empty(t.Runtimes);
    }

    // ---- TIMER_DELETE_ME, deltimer and timeleft from a timer's own callback ----

    [Theory]
    [InlineData(TIMER_STOPPABLE, "c@1 timeleft= deltimer=0")]
    [InlineData(TIMER_STOPPABLE | TIMER_DELETE_ME, "c@1 timeleft= deltimer=1")]
    [InlineData(TIMER_STOPPABLE | TIMER_LOOP, "c@1 timeleft= deltimer=0,c@2 timeleft= deltimer=0,c@3 timeleft= deltimer=0")]
    [InlineData(TIMER_STOPPABLE | TIMER_LOOP | TIMER_DELETE_ME, "c@1 timeleft= deltimer=1")]
    public void InItsOwnCallbackATimerIsSpentAndOnlyDeleteMeLetsDeltimerStopIt(int flags, string log)
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            bell.my_id = TimerWorld.Add(bell, nameof(test_timers_bell.check_own), "c", 1, flags);
        });
        t.Tick(6);
        // timeleft of a spent timer is null (timer.dm:698-699); deltimer needs !spent or TIMER_DELETE_ME (:676)
        Assert.Equal(log, string.Join(",", t.Log));
        Assert.Empty(t.Runtimes);
    }

    // ---- deltimer ----

    [Fact]
    public void DeltimerTakesAnIdOrATimedEventAndAnswersWhetherItDeleted()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object a = TimerWorld.Ring(bell, "a", 2, TIMER_STOPPABLE);
            object b = TimerWorld.Ring(bell, "b", 2, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "c", 2, TIMER_STOPPABLE);
            datum_timedevent eb = TimerWorld.Event(b);

            Assert.True(Dm.eq(deltimer(null), FALSE));
            Assert.True(Dm.eq(deltimer(""), FALSE));
            Assert.True(Dm.eq(deltimer(0), FALSE));
            Assert.True(Dm.eq(deltimer(a), TRUE));
            Assert.True(Dm.eq(deltimer(a), FALSE)); // gone from timer_id_dict
            Assert.True(Dm.eq(deltimer(eb), TRUE)); // a /datum/timedevent is qdel'd as it is (:670-672)
            Assert.True(Dm.eq(deltimer(eb), TRUE)); // ... even when it is deleted already: qdel returns quietly
            Assert.True(Dm.eq(deltimer(b), FALSE));
            Assert.Equal(1, SStimer.timer_id_dict.len);
            Assert.Equal(1f, SStimer.bucket_count);
            Assert.Equal(1, bell._active_timers.len);

            Assert.Equal(669, Assert.Throws<DmCrashException>(() => deltimer(TIMER_ID_NULL)).DmLine);
        });
        t.Tick(6);
        Assert.Equal(new[] { "c@2" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    // ---- timeleft ----

    [Fact]
    public void TimeleftCountsDownToTheTimeToRun()
    {
        var t = new TimerWorld();
        object id = null;
        datum_timedevent ev = null;
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            id = TimerWorld.Ring(bell, "a", 5, TIMER_STOPPABLE);
            ev = TimerWorld.Event(id);
            Assert.True(Dm.eq(timeleft(id), 5f));
            Assert.True(Dm.eq(timeleft(ev), 5f));
            Assert.Null(timeleft(null));
            Assert.Null(timeleft("999"));
            Assert.Equal(691, Assert.Throws<DmCrashException>(() => timeleft(TIMER_ID_NULL)).DmLine);
        });
        t.Tick(3);
        t.In(() => Assert.True(Dm.eq(timeleft(id), 3.5f)));
        t.Tick(7);
        t.In(() =>
        {
            Assert.Null(timeleft(id)); // ran and deleted: not in timer_id_dict
            Assert.True(QDELETED(ev));
            // TgQuirk: a /datum/timedevent answers timeToRun - world.time with no spent check (timer.dm:692-694)
            Assert.True(Dm.eq(timeleft(ev), 0f));
        });
        t.Tick(4);
        t.In(() => Assert.True(Dm.eq(timeleft(ev), -2f)));
        Assert.Empty(t.Runtimes);
    }

    // ---- updatetimedelay ----

    [Fact]
    public void UpdatetimedelayChangesALoopsWaitFromItsNextRearm()
    {
        var t = new TimerWorld();
        object id = t.In(() => TimerWorld.Ring(t.Bell(), "l", 1, TIMER_LOOP | TIMER_STOPPABLE));
        t.Tick(3); // world.time 1.5: rang at 1, re-armed for 2
        t.In(() =>
        {
            updatetimedelay(id, 3);
            Assert.True(Dm.eq(TimerWorld.Event(id).wait, 3f));
            Assert.True(Dm.eq(timeleft(id), 0.5f)); // "Will come into effect on the next process" (timer.dm:704)
        });
        t.Tick(15);
        Assert.Equal(new[] { "l@1", "l@2", "l@5", "l@8" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void UpdatetimedelayCrashesForAnIdOfATimerThatDoesNotLoopButNotForTheTimedEvent()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            object id = TimerWorld.Ring(bell, "n", 2, TIMER_STOPPABLE);
            Assert.Equal(725, Assert.Throws<DmCrashException>(() => updatetimedelay(id, 5)).DmLine);
            Assert.True(Dm.eq(TimerWorld.Event(id).wait, 2f));
            // TgQuirk: the /datum/timedevent form sets wait without the TIMER_LOOP check (timer.dm:715-718); a timer that
            // does not loop never reads it again, so it still runs at its time
            updatetimedelay(TimerWorld.Event(id), 5);
            Assert.True(Dm.eq(TimerWorld.Event(id).wait, 5f));
            Assert.True(Dm.eq(timeleft(id), 2f));

            updatetimedelay(null, 5);
            updatetimedelay("999", 5);
            Assert.Equal(714, Assert.Throws<DmCrashException>(() => updatetimedelay(TIMER_ID_NULL, 5)).DmLine);
        });
        t.Tick(10);
        Assert.Equal(new[] { "n@2" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    // ---- _addtimer's checks ----

    [Fact]
    public void AddtimerCrashesWithoutACallbackAndForAnInfiniteWait()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            DmCrashException none = Assert.Throws<DmCrashException>(() => _addtimer(null, 5, file: "code/test/timers.dm", line: 1));
            Assert.Equal(612, none.DmLine); // ASSERT(istype(callback), ...) (timer.dm:612, rules 9.5)
            Assert.Contains("addtimer called without a callback", none.Message);
            datum_callback cb = Dm.@new<datum_callback>(t.Bell(), nameof(test_timers_bell.ring), "x");
            Assert.Equal(628, Assert.Throws<DmCrashException>(() => _addtimer(cb, INFINITY, file: "code/test/timers.dm", line: 1)).DmLine);
            Assert.Equal(628, Assert.Throws<DmCrashException>(() => _addtimer(cb, 2e31f, TIMER_CLIENT_TIME, file: "code/test/timers.dm", line: 1)).DmLine);
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.clienttime_timers.len);
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ANegativeWaitWarnsAndRunsInTheNextTick()
    {
        var t = new TimerWorld();
        t.In(() => TimerWorld.Ring(t.Bell(), "neg", -5));
        t.Tick(1);
        Assert.Equal(new[] { "neg@0.5" }, t.Log);
        Assert.Equal(616, Assert.IsType<DmCrashException>(Assert.Single(t.Runtimes)).DmLine); // stack_trace (timer.dm:616)
    }

    [Fact]
    public void ATimerOnADeletedTargetWarnsAndStillRuns()
    {
        var t = new TimerWorld();
        test_timers_bell bell = null;
        t.In(() =>
        {
            bell = t.Bell();
            qdel(bell);
            object id = TimerWorld.Ring(bell, "late", 1, TIMER_STOPPABLE);
            // stack_trace at timer.dm:619; the target is QDELETED but not QDESTROYING, so New lists the timer (:420-421)
            Assert.Equal(new object[] { TimerWorld.Event(id) }, Dm.each(bell._active_timers).Cast<object>());
        });
        t.Tick(2);
        Assert.Equal(new[] { "late@1" }, t.Log); // InvokeAsync only checks that the callback has an object (callback.dm:139)
        Assert.Equal(619, Assert.IsType<DmCrashException>(Assert.Single(t.Runtimes)).DmLine);
    }

    [Fact]
    public void ATimerAddedOnATargetInsideItsDestroyIsNotItsActiveTimerAndOutlivesIt()
    {
        var t = new TimerWorld();
        test_timers_phoenix phoenix = null;
        t.In(() =>
        {
            phoenix = Dm.@new<test_timers_phoenix>();
            phoenix.log = t.Log;
            qdel(phoenix); // its Destroy adds a timer while gc_destroyed is GC_CURRENTLY_BEING_QDELETED
            Assert.Null(phoenix._active_timers); // QDESTROYING: no LAZYADD (timer.dm:420)
            Assert.Equal(1f, SStimer.bucket_count);
        });
        t.Tick(2);
        Assert.Equal(new[] { "ashes@1" }, t.Log);
        Assert.Empty(t.Runtimes); // QDESTROYING also skips the stack_trace of :618
    }

    [Fact]
    public void TgQuirk_ATimedEventThatCrashesInNewKeepsItsIdAndItsDeletionDrivesBucketCountNegative()
    {
        var t = new TimerWorld(notTranslatedThrows: false);
        t.Tick(4); // world.time 2
        t.In(() =>
        {
            datum_callback cb = Dm.@new<datum_callback>(t.Bell(), nameof(test_timers_bell.ring), "x");
            // new /datum/timedevent with a time to run in the past: the CRASH of timer.dm:417-418, which addtimer's
            // clamping never lets happen; it comes after New registered the id (:409-415) and before bucketJoin (:423)
            DmCrashException crash = Assert.Throws<DmCrashException>(() => Dm.@new<datum_timedevent>(cb, -1, TIMER_STOPPABLE, SStimer, null, null));
            Assert.Equal(418, crash.DmLine);
            Assert.Equal(1, SStimer.timer_id_dict.len);
            Assert.Equal(0f, SStimer.bucket_count);
            // its Destroy ejects a timer that is in no bucket and not in the secondary queue: bucket_count-- (:479-483),
            // the state tg's timer_sanity unit test watches for
            Assert.True(Dm.eq(deltimer("1"), TRUE));
            Assert.Equal(-1f, SStimer.bucket_count);
        });
        // the CRASH message reads get_timer_debug_string, not translated (package decision): reported, then null
        t.AssertOnlySkeletonReports("code/controllers/subsystem/timer.dm:221");
        Assert.Single(t.Runtimes);
    }

    // ---- callbacks ----

    [Fact]
    public void ARuntimeInACallbackEndsOnlyThatCallbackAndTheTimerIsStillDeleted()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "a", 1, TIMER_STOPPABLE);
            TimerWorld.Add(bell, nameof(test_timers_bell.boom), "b", 1, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "c", 1, TIMER_STOPPABLE);
        });
        t.Tick(2);
        Assert.Equal(new[] { "c@1", "b@1", "a@1" }, t.Log);
        Assert.Equal("boom b", Assert.IsType<InvalidOperationException>(Assert.Single(t.Runtimes)).Message);
        t.In(() =>
        {
            Assert.Equal(0, SStimer.timer_id_dict.len);
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(1f, SStimer.last_invoke_tick); // set after the failed callback too (timer.dm:173)
        });
    }

    [Fact]
    public void ALoopingTimerWhoseCallbackFailsKeepsLooping()
    {
        var t = new TimerWorld();
        t.In(() => TimerWorld.Add(t.Bell(), nameof(test_timers_bell.boom), "b", 1, TIMER_LOOP));
        t.Tick(6);
        Assert.Equal(new[] { "b@1", "b@2", "b@3" }, t.Log);
        Assert.Equal(3, t.Runtimes.Count);
    }

    [Fact]
    public void ASleepingCallbackDoesNotHoldUpTheFireAndItsTimerIsDeletedAtOnce()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "r", 1, TIMER_STOPPABLE);
            TimerWorld.Add(bell, nameof(test_timers_bell.nap), "n", 1, TIMER_STOPPABLE);
        });
        t.Tick(2);
        // InvokeAsync is set waitfor = FALSE (callback.dm:129-130): the fire goes on at the callback's sleep
        Assert.Equal(new[] { "n sleeps@1", "r@1" }, t.Log);
        t.In(() => Assert.Equal(0, SStimer.timer_id_dict.len));
        t.Tick(1);
        Assert.Equal(new[] { "n sleeps@1", "r@1", "n wakes@1.5" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void QdelInDeletesTheItemThroughAGlobalProcCallback()
    {
        var t = new TimerWorld();
        test_timers_bell item = null;
        object id = t.In(() =>
        {
            item = t.Bell();
            // QDEL_IN_STOPPABLE(item, 5) (__DEFINES/qdel.dm:61): 5 is not past GC_FILTER_QUEUE, so the item itself
            object timer = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), item), 5, TIMER_STOPPABLE, file: "code/test/timers.dm", line: 7);
            Assert.Null(item._active_timers); // a GLOBAL_PROC callback is nobody's active timer (timer.dm:420)
            // getcallingtype's GLOBAL_PROC branch (:596-597) in the debug text of operator"" (:505-507)
            Assert.Contains("TTR: 5, wait:5 Flags: TIMER_STOPPABLE, ", Dm.str(TimerWorld.Event(timer)));
            Assert.EndsWith("callBack.object: some_magic_bullshit(GLOBAL_PROC), callBack.delegate:/proc/qdel, source: code/test/timers.dm:7", Dm.str(TimerWorld.Event(timer)));
            return timer;
        });
        t.Tick(9);
        t.In(() => Assert.False(QDELETED(item)));
        t.Tick(1);
        t.In(() =>
        {
            Assert.True(QDELETED(item));
            Assert.Null(timeleft(id));
        });
        Assert.Empty(t.Runtimes);
    }

    // ---- the timers of a deleted datum (datums/datum.dm:133-139) ----

    [Fact]
    public void DeletingADatumDeletesItsPendingTimersWhereverTheyWait()
    {
        var t = new TimerWorld();
        test_timers_bell bell = null;
        t.In(() =>
        {
            bell = t.Bell();
            TimerWorld.Ring(bell, "bucket", 2);
            TimerWorld.Ring(bell, "stoppable", 3, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "queued", 61 * SECONDS, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "unique", 4, TIMER_UNIQUE);
            Assert.Equal(4, bell._active_timers.len);
            TimerWorld.Ring(t.Bell(), "bystander", 3);
            qdel(bell);
            Assert.Null(bell._active_timers);
            Assert.Equal(1f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.second_queue.len);
            Assert.Equal(0, SStimer.timer_id_dict.len);
            Assert.Equal(0, SStimer.hashes.len);
        });
        t.Tick(1300);
        Assert.Equal(new[] { "bystander@3" }, t.Log);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ADatumDeletedFromItsTimersCallbackDeletesItsOtherTimersEvenInTheSameBucket()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "same bucket", 1, TIMER_STOPPABLE);
            TimerWorld.Ring(bell, "later", 5);
            TimerWorld.Add(bell, nameof(test_timers_bell.self_destruct), "sd", 1, TIMER_STOPPABLE); // runs first (LIFO)
        });
        t.Tick(12);
        Assert.Equal(new[] { "sd@1" }, t.Log);
        t.In(() =>
        {
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.timer_id_dict.len);
        });
        Assert.Empty(t.Runtimes);
    }

    // ---- MC_TICK_CHECK inside the fire ----

    [Fact]
    public void TgQuirk_AFireWhileTheSubsystemIsNotRunningStopsAfterEachTimerAndTheNextFireGoesOn()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            test_timers_bell bell = t.Bell();
            TimerWorld.Ring(bell, "a", 1);
            TimerWorld.Ring(bell, "b", 1);
            TimerWorld.Ring(bell, "c", 1);
        });
        t.World.Step(2);
        t.In(() =>
        {
            // MC_TICK_CHECK also trips on src.state != SS_RUNNING (__DEFINES/MC.dm:1); pause() leaves SS_IDLE as it is.
            // So each such fire ends after one bucket (timer.dm:215-216), an empty one included ...
            SStimer.state = SS_IDLE;
            Waiting.NoSleep(SStimer.fire(FALSE));
            Waiting.NoSleep(SStimer.fire(FALSE));
            Assert.Empty(t.Log);
            Assert.Equal(3f, SStimer.practical_offset); // the empty buckets of world.time 0 and 0.5
            // ... and inside a bucket after one timer (:184-185)
            Waiting.NoSleep(SStimer.fire(FALSE));
            Assert.Equal(new[] { "c@1" }, t.Log);
            Assert.Equal(3f, SStimer.practical_offset); // the bucket of world.time 1 is not done
            Waiting.NoSleep(SStimer.fire(TRUE));
            Assert.Equal(new[] { "c@1", "b@1" }, t.Log);
            Assert.True(Dm.eq(SStimer.state, SS_IDLE));
            TimerWorld.FireTimer();
        });
        Assert.Equal(new[] { "c@1", "b@1", "a@1" }, t.Log);
        Assert.Empty(t.Runtimes);
    }
}
