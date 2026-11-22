// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/species_unique_id.dm
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

/**
 * Every species should use a species ID unique to it and it alone. This test runs through every subtype of /datum/species, and checks for a species ID.
 * Every ID is written to a list, gathered_species_ids, and if a previously written ID is written again, this test will fail.
 */
// tg: code/modules/unit_tests/species_unique_id.dm:5 (/datum/unit_test/species_unique_id; class named by rules 2.3)
public sealed class datum_unit_test_species_unique_id : TgUnitTest
{
    // tg: code/modules/unit_tests/species_unique_id.dm:7
    public override async DmTask Run()
    {
        DmList gathered_species_ids = Dm.list();
        foreach (Type species in Dm.each_cast<Type>(subtypesof(typeof(datum_species))))
        {
            object species_id = Dm.initial(species, (datum_species p) => p.id);
            if (Dm.truthy(gathered_species_ids[species_id]))
                TEST_FAIL(Dm.text("Duplicate species ID! [] is not unique to a single species.", species_id));
            else
                gathered_species_ids[species_id] = TRUE;
        }
        await DmTask.CompletedTask;
    }
}
