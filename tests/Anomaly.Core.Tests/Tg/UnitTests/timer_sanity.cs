// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/timer_sanity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// tg: code/modules/unit_tests/timer_sanity.dm:1 (/datum/unit_test/timer_sanity; class named by rules 2.3)
public sealed class datum_unit_test_timer_sanity : TgUnitTest
{
    // tg: code/modules/unit_tests/timer_sanity.dm:1
    public override async DmTask Run()
    {
        // ADAPTED: tg runs this check in a booted world whose SStimer has served every timer of the server's start and
        // of the tests before it. The harness has no Master Controller: it creates SStimer (SUBSYSTEM_DEF, PreInit; since
        // M4b slice 2 TgUnitTest's world does, as tg's MC always does) and this test gives it a workload of tg's own timer
        // kinds (QDEL_IN, QDEL_IN_STOPPABLE, unique, overriding and looping timers, timers past the buckets' minute,
        // deltimer), firing it every tick as the MC fires a ticker of wait 1.
        string qdel_ref = "/proc/" + nameof(GlobalProcs.qdel); // GLOBAL_PROC_REF(qdel)
        var stoppable = new List<object>();
        for (int i = 1; i <= 40; i++)
        {
            datum thing = allocate<datum>();
            // QDEL_IN(thing, i % 10 + 1) and QDEL_IN_STOPPABLE(thing, i * 20) (__DEFINES/qdel.dm:59-61; times up to
            // GC_FILTER_QUEUE pass the thing itself, the stoppable ones keep it here too)
            if (i % 2 == 0)
                _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, qdel_ref, thing), i % 10 + 1, file: "code/modules/unit_tests/timer_sanity.dm", line: 2);
            else
                stoppable.Add(_addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, qdel_ref, thing), i * 20, TIMER_STOPPABLE, file: "code/modules/unit_tests/timer_sanity.dm", line: 2));
            // a unique timer on the datum itself (process() of /datum is harmless: it returns PROCESS_KILL); a unique timer
            // with a GLOBAL_PROC callback hashes REF(GLOBAL_PROC), a text, which has no ref in the port yet (shim open
            // item, dm-conformance.md)
            _addtimer(Dm.@new<datum_callback>(thing, nameof(datum.process)), 10, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/unit_tests/timer_sanity.dm", line: 2);
        }
        object loop = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, qdel_ref, null), 5, TIMER_LOOP | TIMER_STOPPABLE, file: "code/modules/unit_tests/timer_sanity.dm", line: 2);
        for (int tick = 0; tick < 1500; tick++)
        {
            if (tick % 50 == 0 && stoppable.Count > 0)
            {
                deltimer(stoppable[0]);
                stoppable.RemoveAt(0);
            }
            if (tick == 1000)
                deltimer(loop);
            SStimer.state = SS_RUNNING;
            await SStimer.fire(FALSE);
            SStimer.state = SS_IDLE;
            await sleep(world.tick_lag);
        }

        TEST_ASSERT(SStimer.bucket_count >= 0,
            "SStimer is going into negative bucket count from something");
    }
}
