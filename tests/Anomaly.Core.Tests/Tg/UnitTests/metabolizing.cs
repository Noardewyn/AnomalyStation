// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/metabolizing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// PARTIAL (M8 slice A): tg's metabolization test over the reagent types with a hand-written class (the generated-only ones
// metabolize with the base reagent's behaviour, D188). Gaps RG3 (D339): the on_mob_end_metabolize test (methamphetamine's
// movespeed modifier, gained and lost with the meth of a pill). Gaps RG2 (D339): the addictions test (a mind's addiction points
// from meth by pill, by syringe and by both).
// ADAPTED: tg runs it on the unit test map; the room is the toy room (TgToyRoom.Install), which makes SSmobs.

// tg: code/modules/unit_tests/metabolizing.dm:1
public sealed class datum_unit_test_metabolization : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        // tg's strings/ files, as the server mounts them: since gaps RG1 a drink's drunkenness slurs, and the drunk slurring reads
        // strings/slurring_drunk_text.json (speech_debuffs.dm:233, through strings()); the toy room mounts the genetics ones only
        if (Anomaly.Core.Tests.Tools.TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(world_under_test).Files, Anomaly.Core.Tests.Tools.TgClone.Root);
    }

    // tg: code/modules/unit_tests/metabolizing.dm:1
    public override async DmTask Run()
    {
        // Pause natural mob life so it can be handled entirely by the test
        SSmobs.pause();

        mob_living_carbon_human human = allocate<mob_living_carbon_human_consistent>();
        DmList blacklisted_reagents = Dm.list(
            typeof(datum_reagent_eigenstate) //Creates clones after a delay which get into other tests
        );
        DmList reagents_to_check = Dm.sub(typesof_written(valid_subtypesof(typeof(datum_reagent))), blacklisted_reagents) as DmList;
        TEST_ASSERT(Dm.@in(typeof(datum_reagent_water), reagents_to_check), "water is not among the reagents checked");
        foreach (Type reagent_type in Dm.each_cast<Type>(reagents_to_check))
            await test_reagent(human, reagent_type);
    }

    // tg: code/modules/unit_tests/metabolizing.dm:12
    private async DmTask test_reagent(mob_living_carbon C, Type reagent_type)
    {
        await ((datum_reagents)C.reagents).add_reagent(reagent_type, 10);
        // tg passes metabolize(C, SSMOBS_DT, 0, can_overdose = TRUE): the 0 (a times_fired argument of an older signature)
        // lands on can_overdose, and the named argument then sets it to TRUE
        await ((datum_reagents)C.reagents).metabolize(C, SSMOBS_DT(), can_overdose: TRUE);
        await ((datum_reagents)C.reagents).clear_reagents();
    }

    // tg: code/modules/unit_tests/metabolizing.dm:17
    public override void Destroy()
    {
        SSmobs.ignite();
        base.Destroy();
    }
}

// tg: code/modules/unit_tests/metabolizing.dm:22 (/datum/unit_test/on_mob_end_metabolize)
public sealed class datum_unit_test_on_mob_end_metabolize : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/metabolizing.dm:22
    public override async DmTask Run()
    {
        SSmobs.pause();

        mob_living_carbon_human user = allocate<mob_living_carbon_human_consistent>();
        obj_item_reagent_containers_applicator_pill pill = allocate<obj_item_reagent_containers_applicator_pill>();
        Type meth = typeof(datum_reagent_drug_methamphetamine);

        // Give them enough meth to be consumed in 2 metabolizations
        await ((datum_reagents)pill.reagents).add_reagent(meth, 1.9f * Dm.initial(meth, (datum_reagent_drug_methamphetamine r) => r.metabolization_rate) * SSMOBS_DT());
        pill.layers_remaining = 0;
        await pill.interact_with_atom(user, user);

        user.Life(SSMOBS_DT());

        TEST_ASSERT(Dm.truthy(((datum_reagents)user.reagents).has_reagent(meth)), "User does not have meth in their system after consuming it");
        TEST_ASSERT(Dm.truthy(user.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_methamphetamine))), "User consumed meth, but did not gain movespeed modifier");

        user.Life(SSMOBS_DT());

        TEST_ASSERT(!Dm.truthy(((datum_reagents)user.reagents).has_reagent(meth)), "User still has meth in their system when it should've finished metabolizing");
        TEST_ASSERT(!Dm.truthy(user.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_methamphetamine))), "User still has movespeed modifier despite not containing any more meth");
    }

    // tg: code/modules/unit_tests/metabolizing.dm:44
    public override void Destroy()
    {
        SSmobs.ignite();
        base.Destroy();
    }
}

// tg: code/modules/unit_tests/metabolizing.dm:48
public sealed class datum_unit_test_addictions : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/metabolizing.dm:48
    public override async DmTask Run()
    {
        SSmobs.pause();

        mob_living_carbon_human pill_user = allocate<mob_living_carbon_human_consistent>();
        mob_living_carbon_human syringe_user = allocate<mob_living_carbon_human_consistent>();
        mob_living_carbon_human pill_syringe_user = allocate<mob_living_carbon_human_consistent>();

        if (SSjob == null)
            Dm.@new<datum_controller_subsystem_job>(); // a mind's New reads SSjob (_mind.dm:116), which tg's MC always creates
        if (SSantag_hud == null)
            Dm.@new<datum_controller_subsystem_processing_antag_hud>(); // transfer_to adds the mind's antagonist hud, processed by SSantag_hud
        datum_mind pill_mind = Dm.@new<datum_mind>((object)null);
        pill_mind.active = TRUE;
        await pill_mind.transfer_to(pill_user);

        datum_mind syringe_mind = Dm.@new<datum_mind>((object)null);
        syringe_mind.active = TRUE;
        await syringe_mind.transfer_to(syringe_user);

        datum_mind pill_syringe_mind = Dm.@new<datum_mind>((object)null);
        pill_syringe_mind.active = TRUE;
        await pill_syringe_mind.transfer_to(pill_syringe_user);

        var pill = allocate<obj_item_reagent_containers_applicator_pill>();
        var pill_two = allocate<obj_item_reagent_containers_applicator_pill>();

        var syringe = allocate<obj_item_reagent_containers_syringe>();

        var meth = allocate<datum_reagent_drug_methamphetamine>();

        object addiction_type_to_check = null;

        foreach (object key in Dm.each(meth.addiction_types))
            addiction_type_to_check = key; //idk how to do this otherwise

        // Let's start with stomach metabolism
        await ((datum_reagents)pill.reagents).add_reagent(meth.type, 5);
        pill.layers_remaining = 0;
        await pill.interact_with_atom(pill_user, pill_user);

        // Set the metabolism efficiency to 1.0 so it transfers all reagents to the body in one go.
        var pill_belly = (obj_item_organ_stomach)pill_user.get_organ_slot(ORGAN_SLOT_STOMACH);
        pill_belly.metabolism_efficiency = 1;

        pill_user.Life();

        TEST_ASSERT(Dm.truthy(pill_user.mind.addiction_points?[addiction_type_to_check]), "User did not gain addiction points after metabolizing meth");

        // Then injected metabolism
        syringe.volume = 5;
        syringe.amount_per_transfer_from_this = 5;
        await ((datum_reagents)syringe.reagents).add_reagent(meth.type, 5);
        await syringe.melee_attack_chain(syringe_user, syringe_user);

        syringe_user.Life();

        TEST_ASSERT(Dm.truthy(syringe_user.mind.addiction_points?[addiction_type_to_check]), "User did not gain addiction points after metabolizing meth");

        // One half syringe
        await ((datum_reagents)syringe.reagents).remove_all();
        syringe.volume = 5;
        syringe.amount_per_transfer_from_this = 5;
        await ((datum_reagents)syringe.reagents).add_reagent(meth.type, (5 * 0.5f) + 1);

        // One half pill
        await ((datum_reagents)pill_two.reagents).add_reagent(meth.type, (5 * 0.5f) + 1);
        pill_two.layers_remaining = 0;
        await pill_two.interact_with_atom(pill_syringe_user, pill_syringe_user);
        await syringe.melee_attack_chain(pill_syringe_user, pill_syringe_user);

        // Set the metabolism efficiency to 1.0 so it transfers all reagents to the body in one go.
        pill_belly = (obj_item_organ_stomach)pill_syringe_user.get_organ_slot(ORGAN_SLOT_STOMACH);
        pill_belly.metabolism_efficiency = 1;

        pill_syringe_user.Life();

        TEST_ASSERT(Dm.truthy(pill_syringe_user.mind.addiction_points?[addiction_type_to_check]), "User did not gain addiction points after metabolizing meth");
    }

    // tg: code/modules/unit_tests/metabolizing.dm:122
    public override void Destroy()
    {
        SSmobs.ignite();
        base.Destroy();
    }
}
