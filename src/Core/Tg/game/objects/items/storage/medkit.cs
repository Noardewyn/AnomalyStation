// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/medkit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: M8 slice C: the medkits (their contents, the medibot skins). Deferred leaves: the handcuffable handle (the
// cuffable_item element is not translated); not translated: building a medibot from a medkit (medibots, D188). The
// suicides are live-partial rows (M10). Gaps SR1 (D333): the organ transport box (its coolant keeps what it holds frozen, its
// working light) and the test tube rack (its sprite counts the tubes). The var blocks are generated.

// tg: code/game/objects/items/storage/medkit.dm:11
public partial class obj_item_storage_medkit : obj_item_storage
{
    // tg: code/game/objects/items/storage/medkit.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/cuffable_item)
        Dm.Deferred("code/game/objects/items/storage/medkit.dm:32", "Initialize: AddElement(/datum/element/cuffable_item), handcuffing the medkit to a hand (the element is not translated)");
        return dot;
    }

    //medibot assembly
    // tg: code/game/objects/items/storage/medkit.dm:387
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, typeof(obj_item_bodypart_arm_left_robot)) && !istype(tool, typeof(obj_item_bodypart_arm_right_robot)))
            return await base.tool_act(user, tool, modifiers);
        //Making a medibot!
        if (contents.len >= 1)
        {
            balloon_alert(user, I18n.T("items inside!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_bot_assembly_medbot medbot_assembly = Dm.@new<obj_item_bot_assembly_medbot>(drop_location());
        medbot_assembly.set_skin(get_medbot_skin());
        medbot_assembly.balloon_alert(user, "arm added");
        medbot_assembly.robot_arm = tool.type;
        medbot_assembly.medkit_type = type;
        qdel(tool);
        object held_index = user.is_holding(this);
        qdel(this);
        if (Dm.truthy(held_index))
            await user.put_in_hand(medbot_assembly, held_index);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Gets what skin (icon_state) this medkit uses for a medbot
    // tg: code/game/objects/items/storage/medkit.dm:408
    public virtual object get_medbot_skin()
    {
        return "generic";
    }
}

// tg: code/game/objects/items/storage/medkit.dm:34
public partial class obj_item_storage_medkit_regular : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:42
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
            Dm.kv(typeof(obj_item_stack_medical_suture), 2),
            Dm.kv(typeof(obj_item_stack_medical_mesh), 2),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), 1),
            Dm.kv(typeof(obj_item_healthanalyzer_simple), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:54
public partial class obj_item_storage_medkit_emergency : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:60
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_healthanalyzer_simple), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
            Dm.kv(typeof(obj_item_stack_medical_bandage), 1),
            Dm.kv(typeof(obj_item_stack_medical_ointment), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_ekit), 2),
            Dm.kv(typeof(obj_item_storage_pill_bottle_iron), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:73
public partial class obj_item_storage_medkit_surgery : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:80
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_healthanalyzer), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze_twelve), 1),
            Dm.kv(typeof(obj_item_stack_medical_suture), 2),
            Dm.kv(typeof(obj_item_stack_medical_mesh), 2),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), 1),
            Dm.kv(typeof(obj_item_surgical_drapes), 1),
            Dm.kv(typeof(obj_item_scalpel), 1),
            Dm.kv(typeof(obj_item_hemostat), 1),
            Dm.kv(typeof(obj_item_cautery), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:96
public partial class obj_item_storage_medkit_surgery_cmo : obj_item_storage_medkit_surgery
{
    // tg: code/game/objects/items/storage/medkit.dm:96
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_healthanalyzer_advanced), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze_twelve), 1),
            Dm.kv(typeof(obj_item_stack_medical_suture), 2),
            Dm.kv(typeof(obj_item_stack_medical_mesh), 2),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), 1),
            Dm.kv(typeof(obj_item_surgical_drapes), 1),
            Dm.kv(typeof(obj_item_scalpel), 1),
            Dm.kv(typeof(obj_item_hemostat), 1),
            Dm.kv(typeof(obj_item_cautery), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:112
public partial class obj_item_storage_medkit_surgery_syndie : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:139
    public override object get_medbot_skin()
    {
        return "bezerk";
    }

    // tg: code/game/objects/items/storage/medkit.dm:120
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_scalpel_advanced), 1),
            Dm.kv(typeof(obj_item_retractor_advanced), 1),
            Dm.kv(typeof(obj_item_cautery_advanced), 1),
            Dm.kv(typeof(obj_item_surgical_drapes), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze_twelve), 1),
            Dm.kv(typeof(obj_item_reagent_containers_medigel_sterilizine), 1),
            Dm.kv(typeof(obj_item_bonesetter), 1),
            Dm.kv(typeof(obj_item_blood_filter), 1),
            Dm.kv(typeof(obj_item_stack_medical_bone_gel), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_sticky_tape_surgical), 1),
            Dm.kv(typeof(obj_item_reagent_containers_syringe), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_sodium_thiopental), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:142
public partial class obj_item_storage_medkit_ancient : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:146
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
            Dm.kv(typeof(obj_item_stack_medical_bruise_pack), 3),
            Dm.kv(typeof(obj_item_stack_medical_ointment), 3)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:159
public partial class obj_item_storage_medkit_fire : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:166
    public override object get_medbot_skin()
    {
        return "burn";
    }

    // tg: code/game/objects/items/storage/medkit.dm:173
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_aiuri), 3),
            Dm.kv(typeof(obj_item_reagent_containers_spray_hercuri), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_oxandrolone), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:183
public partial class obj_item_storage_medkit_toxin : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:190
    public override object get_medbot_skin()
    {
        return "tox";
    }

    // tg: code/game/objects/items/storage/medkit.dm:198
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_storage_pill_bottle_multiver_less), 1),
            Dm.kv(typeof(obj_item_reagent_containers_syringe_syriniver), 3),
            Dm.kv(typeof(obj_item_storage_pill_bottle_potassiodide), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_penacid), 1),
            Dm.kv(typeof(obj_item_healthanalyzer_simple_disease), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:210
public partial class obj_item_storage_medkit_o2 : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:217
    public override object get_medbot_skin()
    {
        return "oxy";
    }

    // tg: code/game/objects/items/storage/medkit.dm:224
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_syringe_convermol), 3),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_salbutamol), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), 1),
            Dm.kv(typeof(obj_item_storage_pill_bottle_iron), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:234
public partial class obj_item_storage_medkit_brute : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:241
    public override object get_medbot_skin()
    {
        return "brute";
    }

    // tg: code/game/objects/items/storage/medkit.dm:248
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_libital), 3),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
            Dm.kv(typeof(obj_item_storage_pill_bottle_probital), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_salacid), 1),
            Dm.kv(typeof(obj_item_healthanalyzer_simple), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:260
public partial class obj_item_storage_medkit_advanced : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:268
    public override object get_medbot_skin()
    {
        return "adv";
    }

    // tg: code/game/objects/items/storage/medkit.dm:271
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_synthflesh), 3),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_atropine), 2),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
            Dm.kv(typeof(obj_item_storage_pill_bottle_penacid), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:281
public partial class obj_item_storage_medkit_tactical_lite : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:287
    public override object get_medbot_skin()
    {
        return "bezerk";
    }

    // tg: code/game/objects/items/storage/medkit.dm:290
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_healthanalyzer_advanced), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_atropine), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
            Dm.kv(typeof(obj_item_stack_medical_suture_medicated), 2),
            Dm.kv(typeof(obj_item_stack_medical_mesh_advanced), 2)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:302
public partial class obj_item_storage_medkit_tactical : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:310
    public override object get_medbot_skin()
    {
        return "bezerk";
    }

    // tg: code/game/objects/items/storage/medkit.dm:313
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_cautery), 1),
            Dm.kv(typeof(obj_item_scalpel), 1),
            Dm.kv(typeof(obj_item_healthanalyzer_advanced), 1),
            Dm.kv(typeof(obj_item_hemostat), 1),
            Dm.kv(typeof(obj_item_reagent_containers_medigel_sterilizine), 1),
            Dm.kv(typeof(obj_item_storage_box_bandages), 1),
            Dm.kv(typeof(obj_item_surgical_drapes), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_atropine), 2),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 2),
            Dm.kv(typeof(obj_item_stack_medical_suture_medicated), 2),
            Dm.kv(typeof(obj_item_stack_medical_mesh_advanced), 2),
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_libital), 4),
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_aiuri), 4)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:333
public partial class obj_item_storage_medkit_tactical_premium : obj_item_storage_medkit_tactical
{
    // tg: code/game/objects/items/storage/medkit.dm:343
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stack_medical_suture_medicated), 2),
            Dm.kv(typeof(obj_item_stack_medical_mesh_advanced), 2),
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_libital), 3),
            Dm.kv(typeof(obj_item_reagent_containers_applicator_patch_aiuri), 3),
            Dm.kv(typeof(obj_item_healthanalyzer_advanced), 1),
            Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 2),
            Dm.kv(typeof(obj_item_mod_module_thread_ripper), 1),
            Dm.kv(typeof(obj_item_mod_module_surgical_processor_preloaded), 1),
            Dm.kv(typeof(obj_item_mod_module_defibrillator_combat), 1),
            Dm.kv(typeof(obj_item_mod_module_health_analyzer), 1),
            Dm.kv(typeof(obj_item_autosurgeon_syndicate_emaggedsurgerytoolset), 1),
            Dm.kv(typeof(obj_item_reagent_containers_hypospray_combat_empty), 1),
            Dm.kv(typeof(obj_item_storage_box_bandages), 1),
            Dm.kv(typeof(obj_item_storage_box_evilmeds), 1),
            Dm.kv(typeof(obj_item_reagent_containers_medigel_sterilizine), 1),
            Dm.kv(typeof(obj_item_clothing_glasses_hud_health_night_science), 1)
        );
        generate_items_inside(items_inside, this);
    }

    // tg: code/game/objects/items/storage/medkit.dm:340
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_lead), 10));
    }
}

// tg: code/game/objects/items/storage/medkit.dm:366
public partial class obj_item_storage_medkit_coroner : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:374
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(empty))
            return;
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_formaldehyde), 1),
            Dm.kv(typeof(obj_item_reagent_containers_medigel_sterilizine), 1),
            Dm.kv(typeof(obj_item_reagent_containers_blood), 1),
            Dm.kv(typeof(obj_item_bodybag), 2),
            Dm.kv(typeof(obj_item_reagent_containers_syringe), 1)
        );
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:408
public partial class obj_item_storage_medkit_proc : obj_item_storage_medkit
{
    // tg: code/game/objects/items/storage/medkit.dm:408
    public override object get_medbot_skin()
    {
        return "generic";
    }
}

/// A box which takes in coolant and uses it to preserve organs and body parts
// tg: code/game/objects/items/storage/medkit.dm:411
public partial class obj_item_storage_organbox : obj_item_storage
{
    // tg: code/game/objects/items/storage/medkit.dm:427
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        create_reagents(100, TRANSPARENT);

        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/storage/medkit.dm:434
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        ///if there is enough coolant var
        object using_coolant = coolant_to_spend();
        if (using_coolant == null)
        {
            if (Dm.truthy(cooling))
            {
                cooling = FALSE;
                update_appearance();
                foreach (obj stored in Dm.each<obj>(contents))
                    stored.unfreeze();
            }
            return null;
        }

        float amount_used = 0.05f * Dm.num(seconds_per_tick);
        if (!Dm.eq(using_coolant, typeof(datum_reagent_cryostylane)))
            amount_used *= 2;
        Waiting.NoSleep(((datum_reagents)reagents).remove_reagent(using_coolant, amount_used));

        if (Dm.truthy(cooling))
            return null;
        cooling = TRUE;
        update_appearance();
        foreach (obj stored in Dm.each<obj>(contents))
            Waiting.NoSleep(stored.freeze());
        return null;
    }

    /// Returns which coolant we are about to use, or null if there isn't any
    // tg: code/game/objects/items/storage/medkit.dm:458
    public virtual object coolant_to_spend()
    {
        if (Dm.truthy(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_cryostylane))))
            return typeof(datum_reagent_cryostylane);
        if (Dm.truthy(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_consumable_ice))))
            return typeof(datum_reagent_consumable_ice);
        return null;
    }

    // tg: code/game/objects/items/storage/medkit.dm:465
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(cooling) ? "-working" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/storage/medkit.dm:469
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (is_reagent_container(tool) && Dm.truthy(tool.is_open_container()))
        {
            obj_item_reagent_containers RC = tool as obj_item_reagent_containers;
            object units = await ((datum_reagents)RC.reagents).trans_to(this, RC.amount_per_transfer_from_this, transferred_by: user);
            if (Dm.truthy(units))
            {
                balloon_alert(user, I18n.T("[]u transferred", units));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }
        if (istype(tool, typeof(obj_item_plunger)))
        {
            balloon_alert(user, I18n.T("plunging..."));
            if (Dm.truthy(await do_after(user, 1 * SECONDS, target: this)))
            {
                balloon_alert(user, I18n.T("plunged"));
                await ((datum_reagents)reagents).clear_reagents();
            }
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/game/objects/items/storage/medkit.dm:485
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (HAS_TRAIT(user, TRAIT_RESISTCOLD)) //if they're immune to cold, just do the box suicide
        {
            obj_item_bodypart_head myhead = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            if (Dm.truthy(myhead))
            {
                user.visible_message(span_suicide(I18n.T("[] puts [] head into \\the [] and begins closing it! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
                if (Dm.truthy(await myhead.dismember()))
                    await myhead.forceMove(this); //force your enemies to kill themselves with your head collection box!
                playsound(user, "desecration-01.ogg", 50, TRUE, -1);
                return BRUTELOSS;
            }
            user.visible_message(span_suicide(I18n.T("[] is beating []self with \\the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
            return BRUTELOSS;
        }
        user.visible_message(span_suicide(I18n.T("[] is putting [] head inside the [], it looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        user.adjust_bodytemperature(-300);
        await user.apply_status_effect(typeof(datum_status_effect_freon));
        return FIRELOSS;
    }
}

/// A subtype of organ storage box which starts with a full coolant tank
// tg: code/game/objects/items/storage/medkit.dm:502
public partial class obj_item_storage_organbox_preloaded : obj_item_storage_organbox
{
    // tg: code/game/objects/items/storage/medkit.dm:504
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_cryostylane), ((datum_reagents)reagents).maximum_volume));
        return dot;
    }
}

// tg: code/game/objects/items/storage/medkit.dm:508
public partial class obj_item_storage_test_tube_rack : obj_item_storage
{
    // tg: code/game/objects/items/storage/medkit.dm:521
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, contents.len > 0 ? contents.len : null);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/objects/items/storage/medkit.dm:525
public partial class obj_item_storage_test_tube_rack_full : obj_item_storage_test_tube_rack
{
    // tg: code/game/objects/items/storage/medkit.dm:525
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= atom_storage.max_slots; i++)
            Dm.@new<obj_item_reagent_containers_cup_tube>(this);
        update_appearance(UPDATE_ICON_STATE);
    }
}
