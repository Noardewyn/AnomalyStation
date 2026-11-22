using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

/// <summary>
/// The processing subsystems, written from code/controllers/subsystem/processing/processing.dm (fire, :17-33; the root
/// /datum/proc/process, :48-50) and fastprocess.dm, obj.dm, timed_actions.dm, with START_PROCESSING and STOP_PROCESSING
/// (__DEFINES/MC.dm:31-32): reverse start order, STOP_PROCESSING and qdel during a run, PROCESS_KILL, a sleeping
/// process() (controllers.md hazard 9), a failing one (rules 9.2) and the resumed protocol of MC_TICK_CHECK. Golden
/// test (d) of controllers.md section 11.
/// </summary>
public class ProcessingTests
{
    private static datum_controller_subsystem_processing NewSs(Type type) => (datum_controller_subsystem_processing)Dm.@new(type);

    /// <summary>One fire as RunQueue starts it (SS_RUNNING, master.dm:896), inside the world's scope.</summary>
    private static void Fire(datum_controller_subsystem ss, float resumed = FALSE)
    {
        ss.state = SS_RUNNING;
        Waiting.NoSleep(ss.fire(resumed));
        ss.state = SS_IDLE;
    }

    private static test_timers_worker Worker(TimerWorld t, datum_controller_subsystem_processing ss, string label, bool start = true)
    {
        test_timers_worker w = Dm.@new<test_timers_worker>();
        w.log = t.Log;
        w.label = label;
        w.ss = ss;
        if (start)
            START_PROCESSING(ss, w);
        return w;
    }

    private static object[] Items(DmList list) => Dm.each(list).Cast<object>().ToArray();

    public static TheoryData<Type, string> Subsystems() => new()
    {
        { typeof(datum_controller_subsystem_processing), "1" },
        { typeof(datum_controller_subsystem_processing_fastprocess), "0.2" },
        { typeof(datum_controller_subsystem_processing_obj), "2" },
        { typeof(datum_controller_subsystem_processing_timed_actions), "0.1" },
    };

    [Theory]
    [MemberData(nameof(Subsystems))]
    public void AFireProcessesInReverseStartOrderWithATenthOfTheWait(Type type, string seconds_per_tick)
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(type);
            test_timers_worker w1 = Worker(t, ss, "w1"), w2 = Worker(t, ss, "w2"), w3 = Worker(t, ss, "w3");
            Assert.Equal(new object[] { w1, w2, w3 }, Items(ss.processing));
            Assert.True(Dm.truthy(Dm.bitand(w1.datum_flags, DF_ISPROCESSING)));
            Fire(ss);
            // current_run[current_run.len] first (processing.dm:23-25); seconds_per_tick = wait * 0.1 (:28)
            Assert.Equal(new[] { "w3:" + seconds_per_tick, "w2:" + seconds_per_tick, "w1:" + seconds_per_tick }, t.Log);
            Assert.Equal(new object[] { w1, w2, w3 }, Items(ss.processing));
            Assert.Equal(0, ss.currentrun.len);
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ProcessKillStopsProcessingSoThatALaterStartWorksAgain()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing_obj));
            test_timers_worker killed = Worker(t, ss, "killed");
            killed.returns = PROCESS_KILL;
            datum plain = Dm.@new<datum>(); // the root process() returns PROCESS_KILL (processing.dm:48-50)
            START_PROCESSING(ss, plain);
            test_timers_worker kept = Worker(t, ss, "kept");

            Fire(ss);
            Assert.Equal(new object[] { kept }, Items(ss.processing));
            Assert.False(Dm.truthy(Dm.bitand(killed.datum_flags, DF_ISPROCESSING))); // STOP_PROCESSING (:30)
            Assert.False(Dm.truthy(Dm.bitand(plain.datum_flags, DF_ISPROCESSING)));

            Fire(ss);
            killed.returns = null;
            START_PROCESSING(ss, killed); // "fully stop so that a future START_PROCESSING will work" (:29)
            Fire(ss);
            Assert.Equal(new[] { "kept:2", "killed:2", "kept:2", "killed:2", "kept:2" }, t.Log);
            Assert.Equal(new object[] { kept, killed }, Items(ss.processing));
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ADatumStoppedDuringARunIsNotProcessedInIt()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing));
            test_timers_worker w1 = Worker(t, ss, "w1"), w2 = Worker(t, ss, "w2"), w3 = Worker(t, ss, "w3");
            w3.stops = w1;
            Fire(ss);
            // STOP_PROCESSING also takes it out of currentrun (__DEFINES/MC.dm:32), the list the fire walks (:21)
            Assert.Equal(new[] { "w3:1", "w2:1" }, t.Log);
            Assert.Equal(new object[] { w2, w3 }, Items(ss.processing));
            Assert.False(Dm.truthy(Dm.bitand(w1.datum_flags, DF_ISPROCESSING)));
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ADatumStartedDuringARunWaitsForTheNextFire()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing));
            test_timers_worker w1 = Worker(t, ss, "w1"), w2 = Worker(t, ss, "w2");
            test_timers_worker late = Worker(t, ss, "late", start: false);
            w2.starts = late;
            Fire(ss); // currentrun is a copy of processing made at the start (:18-19)
            Assert.Equal(new object[] { w1, w2, late }, Items(ss.processing));
            Fire(ss); // w2 starts it again: DF_ISPROCESSING keeps it from being added twice (__DEFINES/MC.dm:31)
            Assert.Equal(new[] { "w2:1", "w1:1", "late:1", "w2:1", "w1:1" }, t.Log);
            Assert.Equal(3, ss.processing.len);
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TgQuirk_ADeletedDatumIsDroppedWithoutProcessAndKeepsItsProcessingFlag()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing));
            test_timers_worker w1 = Worker(t, ss, "w1"), w2 = Worker(t, ss, "w2"), w3 = Worker(t, ss, "w3");
            w3.deletes = w1; // deleted during the run, before its turn
            Fire(ss);
            Assert.Equal(new[] { "w3:1", "w2:1" }, t.Log);
            Assert.Equal(new object[] { w2, w3 }, Items(ss.processing)); // processing -= thing (:26-27)
            Assert.True(QDELETED(w1));
            // the QDELETED branch does not STOP_PROCESSING: the flag stays set on the deleted datum
            Assert.True(Dm.truthy(Dm.bitand(w1.datum_flags, DF_ISPROCESSING)));

            w3.deletes = null;
            qdel(w2); // deleted between two fires
            Fire(ss);
            Assert.Equal(new[] { "w3:1", "w2:1", "w3:1" }, t.Log);
            Assert.Equal(new object[] { w3 }, Items(ss.processing));
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ASleepingProcessDoesNotHoldUpTheFireAndItsLaterProcessKillIsLost()
    {
        var t = new TimerWorld();
        datum_controller_subsystem_processing ss = null;
        test_timers_dozer dozer = null;
        t.In(() =>
        {
            ss = NewSs(typeof(datum_controller_subsystem_processing_fastprocess));
            Worker(t, ss, "w1");
            dozer = Dm.@new<test_timers_dozer>();
            dozer.log = t.Log;
            START_PROCESSING(ss, dozer);
            Fire(ss);
            // the inherited set waitfor = FALSE returns null at the sleep (hazard 9): not PROCESS_KILL, the fire goes on
            Assert.Equal(new[] { "dozer sleeps@0", "w1:0.2" }, t.Log);
            Assert.Equal(2, ss.processing.len);
        });
        t.World.Step(1);
        t.In(() =>
        {
            Assert.Equal("dozer wakes@0.5", t.Log[^1]);
            // its return value went nowhere: still processed (tg's pod.dm:25-30 "can't rely on return PROCESS_KILL")
            Assert.Equal(2, ss.processing.len);
            Assert.True(Dm.truthy(Dm.bitand(dozer.datum_flags, DF_ISPROCESSING)));
            Fire(ss);
        });
        Assert.Equal("dozer sleeps@0.5", t.Log[^2]);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ARuntimeInProcessEndsOnlyThatDatumsTurnAndItStaysProcessed()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing_obj));
            Worker(t, ss, "w1");
            Worker(t, ss, "w2").throws = TRUE;
            Worker(t, ss, "w3");
            Fire(ss);
            Fire(ss);
            // the per-item isolation boundary (rules 9.2 item 5): the failed call's value is null, not PROCESS_KILL
            Assert.Equal(new[] { "w3:2", "w2:2", "w1:2", "w3:2", "w2:2", "w1:2" }, t.Log);
            Assert.Equal(3, ss.processing.len);
        });
        Assert.Equal(2, t.Runtimes.Count);
        Assert.All(t.Runtimes, e => Assert.Equal("worker w2", Assert.IsType<InvalidOperationException>(e).Message));
    }

    [Fact]
    public void APausedFireResumesItsCurrentRunAndANewFireCopiesProcessingAgain()
    {
        var t = new TimerWorld();
        t.In(() =>
        {
            datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing));
            test_timers_worker w1 = Worker(t, ss, "w1"), w2 = Worker(t, ss, "w2"), w3 = Worker(t, ss, "w3");
            // not SS_RUNNING: MC_TICK_CHECK pauses after every datum (__DEFINES/MC.dm:1; processing.dm:31-32)
            ss.state = SS_PAUSED;
            Waiting.NoSleep(ss.fire(FALSE));
            Assert.Equal(new object[] { w1, w2 }, Items(ss.currentrun));
            Waiting.NoSleep(ss.fire(TRUE)); // resumed: no new copy (:18-19)
            test_timers_worker late = Worker(t, ss, "late");
            Waiting.NoSleep(ss.fire(TRUE));
            Waiting.NoSleep(ss.fire(TRUE)); // currentrun is empty: nothing to do
            Assert.Equal(new[] { "w3:1", "w2:1", "w1:1" }, t.Log);
            Waiting.NoSleep(ss.fire(FALSE));
            Assert.Equal("late:1", t.Log[^1]);
            Assert.Equal(new object[] { w1, w2, w3 }, Items(ss.currentrun));
            Assert.True(Dm.eq(ss.state, SS_PAUSED));
        });
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void TheSameProcessingRunTwiceGivesTheSameWorldState()
    {
        ulong Run()
        {
            var t = new TimerWorld(seed: 9);
            t.In(() =>
            {
                datum_controller_subsystem_processing ss = NewSs(typeof(datum_controller_subsystem_processing_obj));
                test_timers_worker w1 = Worker(t, ss, "w1"), w2 = Worker(t, ss, "w2"), w3 = Worker(t, ss, "w3");
                w3.stops = w1;
                w2.returns = PROCESS_KILL;
                Fire(ss);
                START_PROCESSING(ss, w1);
                Fire(ss);
            });
            Assert.Equal(new[] { "w3:2", "w2:2", "w1:2", "w3:2" }, t.Log);
            t.World.Step(3);
            return t.World.ComputeStateHash();
        }
        Assert.Equal(Run(), Run());
    }
}
