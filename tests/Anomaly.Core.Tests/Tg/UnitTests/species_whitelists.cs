// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/species_whitelists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Gaps SP1 (D338): the whole file.

// tg: code/modules/unit_tests/species_whitelists.dm:1 (/datum/unit_test/species_whitelist_check; class named by rules 2.3)
public sealed class datum_unit_test_species_whitelist_check : TgUnitTest
{
    // tg: code/modules/unit_tests/species_whitelists.dm:1
    public override async DmTask Run()
    {
        foreach (Type typepath in Dm.each_cast<Type>(subtypesof(typeof(datum_species))))
        {
            if (Dm.eq(Dm.initial(typepath, (datum_species p) => p.changesource_flags), NONE))
                TEST_FAIL(Dm.text("A species type was detected with no changesource flags: []", typepath));
        }
        await DmTask.CompletedTask;
    }
}
