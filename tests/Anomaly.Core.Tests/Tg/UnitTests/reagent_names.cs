// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/reagent_names.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// The whole test (M8 slice A), over every valid reagent type (their names are generated, D111); get_chem_id reads them as
// GLOB.chemical_reagents_list would (its GAP).

/// Test that all reagent names are different in order to prevent #65231 and tests that searching for that reagent by name gives the correct one
// tg: code/modules/unit_tests/reagent_names.dm:2
public sealed class datum_unit_test_reagent_names : TgUnitTest
{
    // tg: code/modules/unit_tests/reagent_names.dm:4
    public override async DmTask Run()
    {
        DmList used_names = Dm.list();

        foreach (Type reagent in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent))))
        {
            // Make sure names are different
            string name = Dm.initial(reagent, (datum_reagent p) => p.name);
            if (!Dm.truthy(name))
                continue;

            if (Dm.@in(name, used_names))
                TEST_FAIL(Dm.text("[] shares a name with [] ([])", used_names[name], reagent, name));
            else
                used_names[name] = reagent;

            // Now make sure searching for that name gets us the right reagent
            Type found_reagent = get_chem_id(name) as Type;

            if (found_reagent == null)
                TEST_FAIL(Dm.text("Searching for [] ([]) returned nothing", reagent, name));

            string found_name = found_reagent == null ? null : Dm.initial(found_reagent, (datum_reagent p) => p.name);

            if (found_reagent != reagent)
                TEST_FAIL(Dm.text("Searching for [] ([]) returned [] ([]) instead", reagent, name, found_reagent, found_name));
        }
    }
}
