// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/subsystem_flags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Ensures subystem flags are set in a coherent way
// tg: code/modules/unit_tests/subsystem_flags.dm:2 (/datum/unit_test/subsystem_flags; class named by rules 2.3)
public sealed class datum_unit_test_subsystem_flags : TgUnitTest
{
    // tg: code/modules/unit_tests/subsystem_flags.dm:4
    public override async DmTask Run()
    {
        // ADAPTED: subtypesof(/datum/controller/subsystem) also lists the test assembly's fixture subsystems; the production
        // build's subsystem types are those the world's default subsystem-type filter keeps (the Tg assembly's).
        foreach (Type sub_lad in Dm.each_cast<Type>(TgState.Current.FilterSubsystemTypes(subtypesof(typeof(datum_controller_subsystem)))))
        {
            if (Dm.bitand(Dm.initial(sub_lad, (datum_controller_subsystem p) => p.ss_flags), Dm.bitor(SS_TICKER, SS_KEEP_TIMING)) == Dm.bitor(SS_TICKER, SS_KEEP_TIMING))
            {
                // ADAPTED: get_matching_bitflags("ss_flags", ...) (the VV bitfield names, __HELPERS/bitflag_lists.dm) is not
                // translated; the message names the raw flags value.
                TEST_FAIL(Dm.text("[] {[]} had both SS_TICKER and SEE_KEEP_TIMING set, this is redundant! You should likely remove SS_KEEP_TIMING.", sub_lad, Dm.initial(sub_lad, (datum_controller_subsystem p) => p.ss_flags)));
            }
            if (Dm.bitand(Dm.initial(sub_lad, (datum_controller_subsystem p) => p.ss_flags), Dm.bitor(SS_POST_FIRE_TIMING, SS_KEEP_TIMING)) == Dm.bitor(SS_POST_FIRE_TIMING, SS_KEEP_TIMING))
            {
                TEST_FAIL(Dm.text("[] {[]} had both SS_POST_FIRE_TIMING and SEE_KEEP_TIMING set, this is redundant! You should likely remove SS_KEEP_TIMING as SS_POST_FIRE_TIMING overrides it.", sub_lad, Dm.initial(sub_lad, (datum_controller_subsystem p) => p.ss_flags)));
            }
        }
    }
}
