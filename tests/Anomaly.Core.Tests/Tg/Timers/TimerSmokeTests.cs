using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Timers;

/// <summary>
/// Translator's smoke tests of code/controllers/subsystem/timer.dm: the translation compiles, the subsystem comes up as
/// SUBSYSTEM_DEF(timer) and PreInit make it, the timer API answers its edge cases, and (once the mc package's macros are
/// merged) a timer runs through the buckets and the secondary queue. The behaviour tests are the independent
/// test-writer's.
/// </summary>
public class TimerSmokeTests
{
    [Fact]
    public void SubsystemDefCreatesSStimerAndPreInitSizesTheBuckets()
    {
        var t = new TimersTestWorld();
        t.World.Step(4);
        using (t.World.Enter())
        {
            Assert.Null(SStimer);
            var ss = Dm.@new<datum_controller_subsystem_timer>();
            Assert.Same(ss, SStimer); // NEW_SS_GLOBAL(SStimer) (__DEFINES/MC.dm:29)
            Assert.Equal("Timer", ss.name);
            Assert.Equal(1f, ss.wait);
            Assert.Equal((float)FIRE_PRIORITY_TIMER, ss.priority);
            Assert.Equal((float)(SS_TICKER | SS_NO_INIT), ss.ss_flags);
            // PreInit (timer.dm:52-55): BUCKET_LEN = world.fps * 1 * 60 buckets
            Assert.Equal(1200f, ss.bucket_list.len);
            Assert.Equal(2f, ss.head_offset);
            Assert.Equal(0.5f, ss.bucket_resolution);
            Assert.Equal(1f, ss.practical_offset);
            Assert.Equal(0, ss.second_queue.Count);
            Assert.True(Dm.truthy(datum_controller_subsystem_timer.bucket_auto_reset));
            Assert.Equal(0f, datum_controller_subsystem_timer.last_invoke_warning);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheTimerApiAnswersNullAndUnknownIds()
    {
        var t = new TimersTestWorld();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            Assert.True(Dm.eq(deltimer(null), FALSE));
            Assert.Null(timeleft(null));
            updatetimedelay(null, 5);

            // an id that is not (or no longer) in timer_id_dict
            Assert.True(Dm.eq(deltimer("17"), FALSE));
            Assert.Null(timeleft("17"));
            updatetimedelay("17", 5);
            Assert.Empty(t.Runtimes);

            // TIMER_ID_NULL is what addtimer returns without TIMER_STOPPABLE: a CRASH at the use (timer.dm:669, :691, :714)
            Assert.Equal(669, Assert.Throws<DmCrashException>(() => deltimer(TIMER_ID_NULL)).DmLine);
            Assert.Equal(691, Assert.Throws<DmCrashException>(() => timeleft(TIMER_ID_NULL)).DmLine);
            Assert.Equal(714, Assert.Throws<DmCrashException>(() => updatetimedelay(TIMER_ID_NULL, 5)).DmLine);
        }
    }

    [Fact]
    public void ATimedEventWithoutTimerInfoRendersAsNotFilled()
    {
        var t = new TimersTestWorld();
        using (t.World.Enter())
            Assert.Equal("Event not filled", Dm.str(new datum_timedevent())); // operator"" (timer.dm:495-497)
    }

    [TimerMacrosFact]
    public void ATimedEventRendersAsTgsDebugText()
    {
        var t = new TimersTestWorld();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            var target = Dm.@new<test_timers_target>();
            datum_callback cb = Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "a");
            object id = _addtimer(cb, 3, TIMER_STOPPABLE | TIMER_UNIQUE, file: "code/x.dm", line: 9);
            var te = (datum_timedevent)SStimer.timer_id_dict[id];
            // bucketJoin's timer_info (timer.dm:536-546) through operator"" (:505-507)
            Assert.Equal(Dm.text("Timer: 1 ([]), TTR: 3, wait:3 Flags: TIMER_UNIQUE, TIMER_STOPPABLE, callBack: [], "
                                 + "callBack.object: /datum/test_timers_target(/datum/test_timers_target), callBack.delegate:ring, source: code/x.dm:9",
                                 text_ref(te), text_ref(cb)), Dm.str(te));
        }
    }

    [TimerMacrosFact]
    public void ATimerRunsItsCallbackOnceAtItsTickAndIsDeleted()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        object id;
        test_timers_target target;
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            target = Dm.@new<test_timers_target>();
            target.trace = trace;
            id = _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "a"), 5 * SECONDS, TIMER_STOPPABLE, file: "code/x.dm", line: 1);
            Assert.True(Dm.eq(id, "1")); // num2text(nextid, 100) (timer.dm:410)
            Assert.True(Dm.eq(timeleft(id), 50f));
            Assert.Same(SStimer.timer_id_dict[id], Assert.Single(Dm.each(target._active_timers).Cast<object>())); // LAZYADD (timer.dm:421)
            Assert.Equal(1f, SStimer.bucket_count);
        }
        t.StepAndFireTimer(99);
        Assert.Empty(trace);
        t.StepAndFireTimer(1); // world.time 50
        Assert.Equal(new[] { "a@50" }, trace);
        using (t.World.Enter())
        {
            Assert.Null(timeleft(id));
            Assert.Null(target._active_timers); // the timer's Destroy took itself off (timer.dm:430-432)
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.timer_id_dict.Count);
        }
        t.StepAndFireTimer(20);
        Assert.Equal(new[] { "a@50" }, trace);
        Assert.Empty(t.Runtimes);
    }

    [TimerMacrosFact]
    public void TimersOfOneTickRunLastCreatedFirstAndAWaitOfZeroWaitsATick()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            var target = Dm.@new<test_timers_target>();
            target.trace = trace;
            Assert.True(Dm.eq(_addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "first"), 1, file: "code/x.dm", line: 1), TIMER_ID_NULL));
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "second"), 1, file: "code/x.dm", line: 2);
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "zero"), 0, file: "code/x.dm", line: 3);
        }
        t.StepAndFireTimer(2);
        // wait 0 becomes world.tick_lag (timer.dm:625); bucketJoin inserts at the head of a bucket (:585-589)
        Assert.Equal(new[] { "zero@0.5", "second@1", "first@1" }, trace);
        Assert.Empty(t.Runtimes);
    }

    [TimerMacrosFact]
    public void ALongTimerWaitsInTheSecondaryQueueUntilItsBucketComesUp()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            var target = Dm.@new<test_timers_target>();
            target.trace = trace;
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "late"), 61 * SECONDS, file: "code/x.dm", line: 1);
            Assert.Equal(1, SStimer.second_queue.Count); // TIMER_MAX: past the 1200 buckets of a minute (timer.dm:556)
            Assert.Equal(0f, SStimer.bucket_count);
        }
        t.StepAndFireTimer(1219);
        Assert.Empty(trace);
        using (t.World.Enter())
            Assert.Equal(0, SStimer.second_queue.Count); // migrated into a bucket as the buckets drained (:187-214)
        t.StepAndFireTimer(1);
        Assert.Equal(new[] { "late@610" }, trace);
        Assert.Empty(t.Runtimes);
    }

    [TimerMacrosFact]
    public void UniqueTimersAreFoundByTheirHashAndLoopTimersRearmUntilDeleted()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        object loop;
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            var target = Dm.@new<test_timers_target>();
            target.trace = trace;
            object first = _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "u"), 3, TIMER_UNIQUE | TIMER_STOPPABLE, file: "code/x.dm", line: 1);
            object again = _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "u"), 3, TIMER_UNIQUE | TIMER_STOPPABLE, file: "code/x.dm", line: 2);
            Assert.True(Dm.eq(first, again)); // the existing timer's id (timer.dm:650-652)
            Assert.Equal(1, SStimer.hashes.Count);
            loop = _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "loop"), 2, TIMER_LOOP | TIMER_STOPPABLE, file: "code/x.dm", line: 3);
        }
        t.StepAndFireTimer(12); // to world.time 6
        Assert.Equal(new[] { "loop@2", "u@3", "loop@4", "loop@6" }, trace);
        using (t.World.Enter())
        {
            Assert.Equal(0, SStimer.hashes.Count);
            Assert.True(Dm.eq(deltimer(loop), TRUE));
        }
        t.StepAndFireTimer(10);
        Assert.Equal(4, trace.Count);
        Assert.Empty(t.Runtimes);
    }

    [TimerMacrosFact]
    public void ManyRandomTimersEachRunOnceAtTheirTickAcrossBucketWraps()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        var rnd = new Random(5); // test input only; the world's own RNG is untouched
        var live = new List<(object id, float ttr, int k)>();
        test_timers_target target;
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            target = Dm.@new<test_timers_target>();
            target.trace = trace;
        }
        int n = 0;
        for (int tick = 0; tick < 3000; tick++) // 1500 ds: the buckets wrap twice (BUCKET_LEN ticks, timer.dm:143-146)
        {
            using (t.World.Enter())
            {
                for (int j = 0; j < 3; j++)
                {
                    float wait = rnd.Next(0, 1500) * 0.5f; // up to 75 s: some go to the secondary queue
                    int k = n++;
                    object id = _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), k), wait, TIMER_STOPPABLE, file: "code/x.dm", line: 1);
                    live.Add((id, Dm.world.time + Math.Max(wait, 0.5f), k));
                }
                if (rnd.Next(4) == 0)
                {
                    var victim = live[rnd.Next(live.Count)];
                    if (Dm.truthy(deltimer(victim.id)))
                        live.Remove(victim);
                }
            }
            t.StepAndFireTimer(1);
        }
        t.StepAndFireTimer(1600);

        var fired = trace.Select(x => x.Split('@')).ToDictionary(p => int.Parse(p[0]), p => float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(live.Count, trace.Count);
        foreach (var x in live)
            Assert.Equal(x.ttr, fired[x.k]);
        using (t.World.Enter())
        {
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.second_queue.Count);
            Assert.Equal(0, SStimer.timer_id_dict.Count);
        }
        Assert.Empty(t.Runtimes);
    }

    [TimerMacrosFact]
    public void DeletingTheOwnerDeletesItsPendingTimers()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            var target = Dm.@new<test_timers_target>();
            target.trace = trace;
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "a"), 3, file: "code/x.dm", line: 1);
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "b"), 3, TIMER_DELETE_ME, file: "code/x.dm", line: 2);
            qdel(target); // datum.dm:133-139 qdels its _active_timers; each leaves its bucket (timer.dm:445-447)
            Assert.Equal(0f, SStimer.bucket_count);
        }
        t.StepAndFireTimer(10);
        Assert.Empty(trace);
        Assert.Empty(t.Runtimes);
    }

    [TimerClientTimeFact]
    public void ClientTimeTimersRunOnTheirOwnListAndALoopingOneSurvivesTheLastPass()
    {
        var t = new TimersTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_timer>();
            var target = Dm.@new<test_timers_target>();
            target.trace = trace;
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "c"), 3, TIMER_CLIENT_TIME, file: "code/x.dm", line: 1);
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "cl"), 2, TIMER_CLIENT_TIME | TIMER_LOOP, file: "code/x.dm", line: 2);
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "o"), 3, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/x.dm", line: 3);
            _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "o"), 3, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/x.dm", line: 4);
            object lp = _addtimer(Dm.@new<datum_callback>(target, nameof(test_timers_target.ring), "lp"), 1, TIMER_LOOP | TIMER_STOPPABLE, file: "code/x.dm", line: 5);
            updatetimedelay(lp, 3); // comes into effect when it re-arms (timer.dm:179)
            Assert.Equal(2, SStimer.clienttime_timers.Count);
        }
        t.StepAndFireTimer(16);
        // client timers run before the buckets in a fire (timer.dm:106-139); the looping client timer, re-inserted by
        // BINARY_INSERT while it is the list's last entry, is kept by Cut(1, next_clienttime_timer_index+1) (:137-139)
        Assert.Equal("lp@1,cl@2,c@3,o@3,cl@4,lp@4,cl@6,lp@7,cl@8", string.Join(",", trace));
        Assert.Empty(t.Runtimes);
    }
}
