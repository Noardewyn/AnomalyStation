// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/reagent_mod_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice A): tg feeds the dummy a debug hotdog (fooditem.attack, the edible component: food waits, D194); here
// the ketchup reaches the stomach the way a bite's does, a holder's trans_to with INGEST. The room is the toy room.

// tg: code/modules/unit_tests/reagent_mod_procs.dm:1
public sealed class datum_unit_test_reagent_mob_procs : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/reagent_mod_procs.dm:1
    public override async DmTask Run()
    {
        mob_living_carbon_human human = allocate<mob_living_carbon_human_consistent>();
        datum_reagents bite = allocate<datum_reagents>(10);
        await bite.add_reagent(typeof(datum_reagent_consumable_ketchup), 5);

        TEST_ASSERT_EQUAL(human.has_reagent(typeof(datum_reagent_consumable_ketchup)), FALSE, "Human somehow has ketchup before eating");
        TEST_ASSERT_EQUAL(human.has_reagent(typeof(datum_reagent_medicine_epinephrine)), FALSE, "Human somehow has epinephrine before injecting");

        await bite.trans_to(human, bite.total_volume, methods: INGEST);
        await ((datum_reagents)human.reagents).add_reagent(typeof(datum_reagent_medicine_epinephrine), 5);

        TEST_ASSERT(Dm.truthy(human.has_reagent(typeof(datum_reagent_consumable_ketchup))), "Human doesn't have ketchup after eating");
        TEST_ASSERT(Dm.truthy(human.has_reagent(typeof(datum_reagent_medicine_epinephrine))), "Human doesn't have epinephrine after injecting");
    }
}
