// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/reagent_container_defaults.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice C): the room is the toy room (TgToyRoom.Install); the generated-only types are skipped (below).

/// Checks if reagent container transfer amount defaults match with actual possible values
// tg: code/modules/unit_tests/reagent_container_defaults.dm:2 (/datum/unit_test/reagent_container_defaults)
public sealed class datum_unit_test_reagent_container_defaults : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/reagent_container_defaults.dm:4
    public override async DmTask Run()
    {
        foreach (Type container_type in Dm.each_cast<Type>(subtypesof(typeof(obj_item_reagent_containers))))
        {
            // ADAPTED: generated-only types are skipped until translated, as create_and_destroy does (DV25; the disease
            // bottles and the cyborgs' self-filling sprays).
            if (container_type.IsDefined(typeof(DmGeneratedOnlyAttribute), inherit: false))
                continue;
            obj_item_reagent_containers container = allocate(container_type) as obj_item_reagent_containers;
            if (!Dm.truthy(container.has_variable_transfer_amount))
                continue;
            object initial_value = Dm.initial(container, (obj_item_reagent_containers c) => c.amount_per_transfer_from_this);
            object index_of_initial_value = container.possible_transfer_amounts.Find(initial_value);
            if (Dm.num(index_of_initial_value) == 0)
                TEST_FAIL(Dm.text("Reagent container []: initial value of amount_per_transfer_from_this value ([]) not found in possible_transfer_amounts list", container_type, initial_value));
        }
    }
}
