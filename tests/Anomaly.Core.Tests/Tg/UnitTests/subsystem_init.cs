using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// Tests that all subsystems that need to properly initialize.
// tg: code/modules/unit_tests/subsystem_init.dm:2 (/datum/unit_test/subsystem_init)
// ADAPTED: abstract until the wave-3 merge. tg runs its unit tests in a booted world, and the harness boots none yet (its
// stand-in has no Master, TgUnitTest.cs); Tg/Boot/BootIntegrationTests runs this test in a TgTestWorld meanwhile. When the
// harness boots through TgWorld.Boot, the class becomes sealed and runs on the harness like the other ported tests.
public abstract class datum_unit_test_subsystem_init : TgUnitTest
{
    protected datum_unit_test_subsystem_init()
    {
        test_flags = UNIT_TEST_MAP_TEST;
    }

    // tg: code/modules/unit_tests/subsystem_init.dm:5
    public override async DmTask Run()
    {
        foreach (datum_controller_subsystem subsystem in Dm.each_cast<datum_controller_subsystem>(Master.subsystems))
        {
            if (Dm.truthy(Dm.bitand(subsystem.ss_flags, SS_NO_INIT)))
                continue;
            if (Dm.truthy(subsystem.initialized))
                continue;

            float should_fail = Dm.num(!Dm.truthy(Dm.bitand(subsystem.ss_flags, SS_OK_TO_FAIL_INIT)));
            DmList message_strings = Dm.list(Dm.text("[] ([]) is a subsystem meant to initialize but could not get initialized.", subsystem, subsystem.type));

            if (subsystem.initialization_failure_message != null)
                Dm.append(ref message_strings, Dm.text("The subsystem reported the following: []", subsystem.initialization_failure_message));

            if (Dm.truthy(should_fail))
            {
                TEST_FAIL(jointext(message_strings, "\n"));
                continue;
            }

            Dm.append(ref message_strings, "This subsystem is marked as SS_OK_TO_FAIL_INIT. This is still a bug, but it is non-blocking.");
            TEST_NOTICE(this, jointext(message_strings, "\n"));
        }
    }
}
