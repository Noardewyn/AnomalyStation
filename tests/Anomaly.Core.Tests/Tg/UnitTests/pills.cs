// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/pills.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice C): the room is the toy room (TgToyRoom.Install), which makes SSmobs.

// tg: code/modules/unit_tests/pills.dm:1 (/datum/unit_test/pills)
public sealed class datum_unit_test_pills : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/pills.dm:1
    public override async DmTask Run()
    {
        mob_living_carbon_human human = allocate<mob_living_carbon_human_consistent>();
        obj_item_reagent_containers_applicator_pill pill_one = allocate<obj_item_reagent_containers_applicator_pill_iron>();
        obj_item_reagent_containers_applicator_pill pill_two = allocate<obj_item_reagent_containers_applicator_pill_tox>();

        TEST_ASSERT_EQUAL(human.has_reagent(typeof(datum_reagent_iron)), FALSE, "Human somehow has iron before taking the any pills");
        TEST_ASSERT_EQUAL(human.has_reagent(typeof(datum_reagent_toxin)), FALSE, "Human somehow has iron before taking the any pills");

        pill_one.layers_remaining = 0;
        await pill_one.interact_with_atom(human, human);
        human.Life(SSMOBS_DT());

        TEST_ASSERT(Dm.truthy(human.has_reagent(typeof(datum_reagent_iron))), "Human doesn't have iron after taking the first pill");

        pill_two.layers_remaining = 4;
        await pill_two.interact_with_atom(human, human);
        human.Life(SSMOBS_DT());
        TEST_ASSERT_EQUAL(human.has_reagent(typeof(datum_reagent_toxin)), FALSE, "Human has toxin despite consuming a pill with four layers after a single tick");
    }
}
