// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/reagent_mob_expose.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// testing the mob expose procs are working
// ADAPTED (M8 slice C): the room is the toy room (TgToyRoom.Install), which makes SSmobs; /datum/reagent/method_patch_test is
// the test's own type (test code, no DM type of the port); the drink is a beaker, not tg's glass bottle (drinks are food,
// D188: the bottle is an inert generated type with no reagents), fed by the same cup attack.

// tg: code/modules/unit_tests/reagent_mob_expose.dm:3
[DmPath("/datum/reagent/method_patch_test")]
public class datum_reagent_method_patch_test : datum_reagent
{
    public datum_reagent_method_patch_test()
    {
        name = "method patch test";
    }

    // tg: code/modules/unit_tests/reagent_mob_expose.dm:6
    public override async DmTask expose_mob(mob_living target = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= PATCH;
        await base.expose_mob(target, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, PATCH)))
            await target.set_brute_loss(20);
        if (Dm.truthy(Dm.bitand(methods, INJECT)))
            await target.set_brute_loss(10);
    }
}

// tg: code/modules/unit_tests/reagent_mob_expose.dm:13 (/datum/unit_test/reagent_mob_expose)
public sealed class datum_unit_test_reagent_mob_expose : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/reagent_mob_expose.dm:13
    public override async DmTask Run()
    {
        // Life() is handled just by tests
        SSmobs.pause();

        mob_living_carbon_human human = allocate<mob_living_carbon_human_consistent>();
        obj_item_reagent_containers_dropper dropper = allocate<obj_item_reagent_containers_dropper>();
        obj_item_reagent_containers_cup_beaker drink = allocate<obj_item_reagent_containers_cup_beaker>();
        obj_item_reagent_containers_applicator_patch patch = allocate<obj_item_reagent_containers_applicator_patch>();
        obj_item_reagent_containers_syringe syringe = allocate<obj_item_reagent_containers_syringe>();
        datum_reagents drink_reagents = (datum_reagents)drink.reagents;

        // INGEST
        TEST_ASSERT_EQUAL(human.fire_stacks, 0, "Human has fire stacks before taking phlogiston");
        await drink_reagents.add_reagent(typeof(datum_reagent_phlogiston), 10);
        await drink.melee_attack_chain(human, human);
        TEST_ASSERT_EQUAL(human.fire_stacks, 1, "Human does not have fire stacks after taking phlogiston");
        human.Life(SSMOBS_DT());
        TEST_ASSERT(Dm.num(human.fire_stacks) > 1, "Human fire stacks did not increase after life tick");

        // TOUCH
        await ((datum_reagents)dropper.reagents).add_reagent(typeof(datum_reagent_water), 5);
        await dropper.melee_attack_chain(human, human);
        TEST_ASSERT(Dm.num(human.fire_stacks) < 0, "Human still has fire stacks after touching water");

        // VAPOR
        await human.fully_heal(ALL);
        TEST_ASSERT_NULL(human.has_status_effect(typeof(datum_status_effect_drowsiness)), "Human is drowsy at the start of testing");
        await drink_reagents.add_reagent(typeof(datum_reagent_nitrous_oxide), 10);
        await drink_reagents.trans_to(human, 10, methods: VAPOR);
        TEST_ASSERT_NOTNULL(human.has_status_effect(typeof(datum_status_effect_drowsiness)), "Human is not drowsy after exposure to vapors");
        await drink_reagents.clear_reagents();
        await drink_reagents.add_reagent(typeof(datum_reagent_water), 10);
        float old_fire_stacks = Dm.num(human.fire_stacks);
        await drink_reagents.trans_to(human, 10, methods: VAPOR);
        TEST_ASSERT(Dm.num(human.fire_stacks) < old_fire_stacks, "Human does not get wetter after being exposed to water by vapors");

        // PATCH
        await human.fully_heal(ALL);
        TEST_ASSERT_EQUAL(human.get_brute_loss(), 0, "Human health did not set properly");
        await ((datum_reagents)patch.reagents).add_reagent(typeof(datum_reagent_method_patch_test), 1);
        patch.self_delay = 0;
        await patch.interact_with_atom(human, human);
        ((datum_embedding)patch.get_embed()).process(Dm.num(SSdcs.wait) / 10);
        human.Life(SSMOBS_DT());
        TEST_ASSERT_EQUAL(human.get_brute_loss(), 20, "Human health did not update after patch was applied");

        // INJECT
        await ((datum_reagents)syringe.reagents).add_reagent(typeof(datum_reagent_method_patch_test), 1);
        await syringe.melee_attack_chain(human, human);
        TEST_ASSERT_EQUAL(human.get_brute_loss(), 10, "Human health did not update after injection from syringe");

        // INHALE
        await human.fully_heal(ALL);
        TEST_ASSERT_NULL(human.has_status_effect(typeof(datum_status_effect_hallucination)), "Human is hallucinating at the start of testing");
        await drink_reagents.add_reagent(typeof(datum_reagent_nitrous_oxide), 10);
        await drink_reagents.trans_to(human, 10, methods: INHALE);
        TEST_ASSERT_NOTNULL(human.has_status_effect(typeof(datum_status_effect_hallucination)), "Human is not hallucinating after exposure to vapors");
    }

    // tg: code/modules/unit_tests/reagent_mob_expose.dm:71
    public override void Destroy()
    {
        SSmobs.ignite();
        base.Destroy();
    }
}
