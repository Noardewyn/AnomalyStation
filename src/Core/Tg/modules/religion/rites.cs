// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/rites.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the rite's invocation (its favour, its spoken lines between the do_afters, its effect) and the rites of the
// mechanical, greedy, maintenance and sparring gods. The var blocks are generated (D111).

// tg: code/modules/religion/rites.dm:1
public partial class datum_religion_rites : datum
{
    // tg: code/modules/religion/rites.dm:18
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (GLOB.religious_sect == null)
            return;
        LAZYADD(ref GLOB.religious_sect.active_rites, this);
    }

    // tg: code/modules/religion/rites.dm:24
    public override object Destroy(float? force = null)
    {
        if (GLOB.religious_sect != null)
            LAZYREMOVE(ref GLOB.religious_sect.active_rites, this);
        return base.Destroy(force);
    }

    // tg: code/modules/religion/rites.dm:29
    public virtual object can_afford(mob_living user = null)
    {
        if (Dm.num(GLOB.religious_sect?.favor) < favor_cost)
        {
            to_chat(user, span_warning(I18n.T("This rite requires more favor!")));
            return FALSE;
        }
        return TRUE;
    }

    ///Called to perform the invocation of the rite, with args being the performer and the altar where it's being performed. Maybe you want it to check for something else?
    // tg: code/modules/religion/rites.dm:36
    public virtual async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!Dm.truthy(can_afford(user)))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You begin to perform the rite of []...", name)));
        if (!Dm.truthy(ritual_invocations))
        {
            if (Dm.truthy(await do_after(user, ritual_length)))
                return TRUE;
            return FALSE;
        }
        float first_invoke = TRUE;
        foreach (object i in Dm.each(ritual_invocations))
        {
            if (Dm.truthy(first_invoke)) //instant invoke
            {
                await user.say(i);
                first_invoke = FALSE;
                continue;
            }
            if (!Dm.truthy(length(ritual_invocations))) //we divide so we gotta protect
                return FALSE;
            if (!Dm.truthy(await do_after(user, Dm.div(ritual_length, length(ritual_invocations)))))
                return FALSE;
            await user.say(i);
        }
        if (!Dm.truthy(await do_after(user, Dm.div(ritual_length, length(ritual_invocations))))) //because we start at 0 and not the first fraction in invocations, we still have another fraction of ritual_length to complete
            return FALSE;
        if (Dm.truthy(invoke_msg))
            await user.say(invoke_msg);
        return TRUE;
    }

    ///Does the thing if the rite was successfully performed. return value denotes that the effect successfully (IE a harm rite does harm)
    // tg: code/modules/religion/rites.dm:63
    [ShouldCallParent]
    public virtual async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        GLOB.religious_sect.on_riteuse(user, religious_tool);
        await DmTask.CompletedTask;
        return TRUE;
    }

    ///Called if invoke effect returns TRUE, for effects meant to occur only if the rite passes.
    // tg: code/modules/religion/rites.dm:69
    [ShouldCallParent]
    public virtual async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        if (!Dm.truthy(Dm.bitand(rite_flags, RITE_ONE_TIME_USE)))
            return;
        GLOB.religious_sect.rites_list.Remove(this.type);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/religion/rites.dm:75
    public virtual void refund(float? percent = null)
    {
        percent ??= 1.0f;
        GLOB.religious_sect.adjust_favor(favor_cost * Dm.num(percent));
    }
}

/**** Mechanical God ****/

// tg: code/modules/religion/rites.dm:80
public partial class datum_religion_rites_synthconversion : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:90
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ismovable(religious_tool))
        {
            to_chat(user, span_warning(I18n.T("This rite requires a religious device that individuals can be buckled to.")));
            return FALSE;
        }
        atom_movable movable_reltool = religious_tool as atom_movable;
        if (movable_reltool == null)
            return FALSE;
        if (Dm.truthy(LAZYLEN(movable_reltool.buckled_mobs)))
            to_chat(user, span_warning(I18n.T("You're going to convert the one buckled on [].", movable_reltool)));
        else
        {
            if (!Dm.truthy(movable_reltool.can_buckle)) //yes, if you have somehow managed to have someone buckled to something that now cannot buckle, we will still let you perform the rite!
            {
                to_chat(user, span_warning(I18n.T("This rite requires a religious device that individuals can be buckled to.")));
                return FALSE;
            }
            if (isandroid(user))
            {
                to_chat(user, span_warning(I18n.T("You've already converted yourself. To convert others, they must be buckled to [].", movable_reltool)));
                return FALSE;
            }
            to_chat(user, span_warning(I18n.T("You're going to convert yourself with this ritual.")));
        }
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/rites.dm:109
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        if (!ismovable(religious_tool))
            throw Dm.CRASH(Dm.text("[]'s perform_rite had a movable atom that has somehow turned into a non-movable!", name), "code/modules/religion/rites.dm", 114);
        atom_movable movable_reltool = religious_tool as atom_movable;
        mob_living_carbon_human rite_target = null;
        if (!Dm.truthy(movable_reltool?.buckled_mobs?.len))
            rite_target = user as mob_living_carbon_human;
        else
        {
            foreach (object buckled in Dm.each(movable_reltool.buckled_mobs))
            {
                if (ishuman(buckled))
                {
                    rite_target = buckled as mob_living_carbon_human;
                    break;
                }
            }
        }
        if (rite_target == null)
            return FALSE;
        rite_target.set_species(typeof(datum_species_android));
        rite_target.visible_message(span_notice(I18n.T("[] has been converted by the rite of []!", rite_target, name)));
        return TRUE;
    }
}

// tg: code/modules/religion/rites.dm:129
public partial class datum_religion_rites_machine_blessing : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:140
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        object altar_turf = get_turf(religious_tool);
        object blessing = pick_weight_recursive(
            Dm.list(
                // Arms
                Dm.kv(Dm.list(
                    Dm.kv(typeof(obj_item_organ_cyberimp_arm_toolkit_combat), 1),
                    Dm.kv(typeof(obj_item_organ_cyberimp_arm_toolkit_surgery), 1000000),
                    Dm.kv(typeof(obj_item_organ_cyberimp_arm_toolkit_toolset), 1500000)), 15),
                // Eyes
                Dm.kv(Dm.list(
                    Dm.kv(typeof(obj_item_organ_cyberimp_eyes_hud_diagnostic), 1),
                    Dm.kv(typeof(obj_item_organ_cyberimp_eyes_hud_medical), 1),
                    Dm.kv(typeof(obj_item_organ_eyes_robotic_glow), 1),
                    Dm.kv(typeof(obj_item_organ_eyes_robotic_shield), 2)), 15),
                // Chest
                Dm.kv(Dm.list(
                    Dm.kv(typeof(obj_item_organ_cyberimp_chest_reviver), 1),
                    Dm.kv(typeof(obj_item_organ_cyberimp_chest_thrusters), 2)), 9),
                // Brain / Head
                Dm.kv(Dm.list(
                    Dm.kv(typeof(obj_item_organ_cyberimp_brain_anti_drop), 50),
                    Dm.kv(typeof(obj_item_organ_cyberimp_brain_connector), 50),
                    Dm.kv(typeof(obj_item_organ_cyberimp_brain_anti_stun), 10)), 10),
                // Misc
                Dm.kv(Dm.list(
                    Dm.kv(typeof(obj_item_organ_cyberimp_mouth_breathing_tube), 1)), 5)));
        await Dm.new_async((Type)blessing, altar_turf);
        return TRUE;
    }
}

/*********Greedy God**********/

///all greed rites cost money instead
// tg: code/modules/religion/rites.dm:181
public partial class datum_religion_rites_greed : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:186
    public override object can_afford(mob_living user = null)
    {
        datum_bank_account account = user.get_bank_account() as datum_bank_account;
        if (account == null)
        {
            to_chat(user, span_warning(I18n.T("You need a way to pay for the rite!")));
            return FALSE;
        }
        if (Dm.num(account.account_balance) < money_cost)
        {
            to_chat(user, span_warning(I18n.T("This rite requires more money!")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/religion/rites.dm:196
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        object dot = null;
        datum_bank_account account = user.get_bank_account() as datum_bank_account;
        if (account == null || Dm.num(account.account_balance) < money_cost)
        {
            to_chat(user, span_warning(I18n.T("This rite requires more money!")));
            return FALSE;
        }
        account.adjust_money(-money_cost, "Church Donation: Rite");
        dot = await base.invoke_effect(user, religious_tool);
        return dot;
    }
}

// tg: code/modules/religion/rites.dm:204
public partial class datum_religion_rites_greed_vendatray : datum_religion_rites_greed
{
    // tg: code/modules/religion/rites.dm:210
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        object altar_turf = get_turf(religious_tool);
        Dm.@new<obj_structure_displaycase_forsale>(altar_turf);
        playsound(get_turf(religious_tool), Dm.resource("sound/effects/cashregister.ogg"), 60, TRUE);
        return TRUE;
    }
}

// tg: code/modules/religion/rites.dm:217
public partial class datum_religion_rites_greed_custom_vending : datum_religion_rites_greed
{
    // tg: code/modules/religion/rites.dm:223
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        object altar_turf = get_turf(religious_tool);
        // the custom vendor itself is inert until its own package (not-translated.md section 244)
        Dm.@new<obj_machinery_vending_custom_greed>(altar_turf);
        playsound(get_turf(religious_tool), Dm.resource("sound/effects/cashregister.ogg"), 60, TRUE);
        return TRUE;
    }
}

/*********Maintenance God**********/

// tg: code/modules/religion/rites.dm:232
public partial class datum_religion_rites_maint_adaptation : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:242
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ishuman(user))
            return FALSE;
        //uses HAS_TRAIT_FROM because junkies are also hopelessly addicted
        if (HAS_TRAIT_FROM(user, TRAIT_HOPELESSLY_ADDICTED, "maint_adaptation"))
        {
            to_chat(user, span_warning(I18n.T("You've already adapted.")));
            return FALSE;
        }
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/rites.dm:251
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        to_chat(user, span_warning(I18n.T("You feel your genes rattled and reshaped. <b>You're becoming something new.</b>")));
        await user.emote("laugh");
        ADD_TRAIT(user, TRAIT_HOPELESSLY_ADDICTED, "maint_adaptation");
        //addiction sends some nasty mood effects but we want the maint adaption to be enjoyed like a fine wine
        await user.add_mood_event("maint_adaptation", typeof(datum_mood_event_maintenance_adaptation));
        if (iscarbon(user))
        {
            mob_living_carbon vomitorium = user as mob_living_carbon;
            await vomitorium.vomit(VOMIT_CATEGORY_DEFAULT);
            datum_dna dna = vomitorium.has_dna() as datum_dna;
            if (dna != null)
                await dna.add_mutation(typeof(datum_mutation_stimmed), MUTATION_SOURCE_MAINT_ADAPT); //some fluff mutations
            if (dna != null)
                await dna.add_mutation(typeof(datum_mutation_strong), MUTATION_SOURCE_MAINT_ADAPT);
        }
        user.mind.add_addiction_points(typeof(datum_addiction_maintenance_drugs), 1000);//ensure addiction
        return null;
    }
}

// tg: code/modules/religion/rites.dm:266
public partial class datum_religion_rites_adapted_eyes : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:273
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ishuman(user))
            return FALSE;
        if (!HAS_TRAIT_FROM(user, TRAIT_HOPELESSLY_ADDICTED, "maint_adaptation"))
        {
            to_chat(user, span_warning(I18n.T("You need to adapt to maintenance first.")));
            return FALSE;
        }
        obj_item_organ_eyes_night_vision_maintenance_adapted adapted = ((mob_living_carbon_human)user).get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes_night_vision_maintenance_adapted;
        if (adapted != null && adapted is obj_item_organ_eyes_night_vision_maintenance_adapted)
        {
            to_chat(user, span_warning(I18n.T("Your eyes are already adapted!")));
            return FALSE;
        }
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/rites.dm:285
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        obj_item_organ_eyes oldeyes = human_user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        to_chat(user, span_warning(I18n.T("You feel your eyes adapt to the darkness!")));
        if (oldeyes != null)
        {
            await oldeyes.Remove(human_user, special: TRUE);
            qdel(oldeyes);//eh
        }
        obj_item_organ_eyes_night_vision_maintenance_adapted neweyes = Dm.@new<obj_item_organ_eyes_night_vision_maintenance_adapted>();
        await neweyes.Insert(human_user, special: TRUE);
        return null;
    }
}

// tg: code/modules/religion/rites.dm:295
public partial class datum_religion_rites_adapted_food : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:304
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_food could_mold in Dm.each<obj_item_food>(get_turf(religious_tool)))
        {
            if (could_mold is obj_item_food_badrecipe_moldy)
                continue;
            mold_target = could_mold; //moldify this o great one
            return await base.perform_rite(user, religious_tool);
        }
        to_chat(user, span_warning(I18n.T("You need to place food on [] to do this!", religious_tool)));
        return FALSE;
    }

    // tg: code/modules/religion/rites.dm:313
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        obj_item_food moldify = mold_target;
        mold_target = null;
        if (QDELETED(moldify) || !(get_turf(religious_tool) == moldify.loc)) //check if the same food is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        to_chat(user, span_warning(I18n.T("[] becomes rancid!", moldify)));
        await user.emote("laugh");
        Dm.@new<obj_item_food_badrecipe_moldy>(get_turf(religious_tool));
        qdel(moldify);
        return TRUE;
    }
}

// tg: code/modules/religion/rites.dm:326
public partial class datum_religion_rites_ritual_totem : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:334
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_stack_sheet_mineral_wood could_totem in Dm.each<obj_item_stack_sheet_mineral_wood>(get_turf(religious_tool)))
        {
            converted = could_totem; //totemify this o great one
            return await base.perform_rite(user, religious_tool);
        }
        to_chat(user, span_warning(I18n.T("You need a piece of wood to do this!")));
        return FALSE;
    }

    // tg: code/modules/religion/rites.dm:341
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        object altar_turf = get_turf(religious_tool);
        obj_item_stack_sheet_mineral_wood padala = converted;
        converted = null;
        if (QDELETED(padala) || !(get_turf(religious_tool) == padala.loc)) //check if the same food is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        to_chat(user, span_warning(I18n.T("[] reshapes into a totem!", padala)));
        if (!Dm.truthy(await padala.use(1)))//use one wood
            return null;
        await user.emote("laugh");
        Dm.@new<obj_item_ritual_totem>(altar_turf);
        return TRUE;
    }
}

///sparring god rites

// tg: code/modules/religion/rites.dm:358
public partial class datum_religion_rites_sparring_contract : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:365
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_paper could_contract in Dm.each<obj_item_paper>(get_turf(religious_tool)))
        {
            if (Dm.truthy(could_contract.get_total_length())) //blank paper pls
                continue;
            contract_target = could_contract;
            return await base.perform_rite(user, religious_tool);
        }
        to_chat(user, span_warning(I18n.T("You need to place blank paper on [] to do this!", religious_tool)));
        return FALSE;
    }

    // tg: code/modules/religion/rites.dm:374
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        obj_item_paper blank_paper = contract_target;
        turf tool_turf = get_turf(religious_tool);
        contract_target = null;
        if (QDELETED(blank_paper) || !(tool_turf == blank_paper.loc)) //check if the same paper is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        blank_paper.visible_message(span_notice(I18n.T("words magically form on []!", blank_paper)));
        playsound(tool_turf, Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;
        if (sect.existing_contract != null)
        {
            sect.existing_contract.visible_message(span_warning(I18n.T("[] fizzles into nothing!", this)));
            qdel(sect.existing_contract);
        }
        sect.existing_contract = Dm.@new<obj_item_sparring_contract>(tool_turf);
        qdel(blank_paper);
        return TRUE;
    }
}

// tg: code/modules/religion/rites.dm:392
public partial class datum_religion_rites_declare_arena : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:401
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        object dot = null;
        DmList filtered = Dm.list();
        foreach (area unfiltered_area in Dm.each_cast<area>(get_sorted_areas()))
        {
            if (unfiltered_area is area_centcom) //youuu dont need thaaat
                continue;
            if (!Dm.truthy(Dm.bitand(unfiltered_area.area_flags, HIDDEN_AREA)))
                Dm.append(ref filtered, unfiltered_area);
        }
        area_instance = await tgui_input_list(user, I18n.T("Choose an area to mark as an arena!"), I18n.T("Arena Declaration"), filtered) as area;
        if (area_instance == null)
            return FALSE;
        dot = await base.perform_rite(user, religious_tool);
        return dot;
    }

    // tg: code/modules/religion/rites.dm:413
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        object dot = null;
        dot = await base.invoke_effect(user, religious_tool);
        datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;
        sect.arenas[area_instance.name] = area_instance.type;
        to_chat(user, span_warning(I18n.T("[] is a now an option to select on sparring contracts.", area_instance)));
        return dot;
    }
}

// tg: code/modules/religion/rites.dm:419
public partial class datum_religion_rites_ceremonial_weapon : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:428
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        object not_rigid = null;
        datum_material_requirement requirement = SSmaterials.requirements[typeof(datum_material_requirement_rigid_material)] as datum_material_requirement;
        foreach (obj_item_stack_sheet could_blade in Dm.each<obj_item_stack_sheet>(get_turf(religious_tool)))
        {
            datum_material blade_mat = await SSmaterials.get_material(could_blade.material_type) as datum_material;
            if (blade_mat == null)
                continue;
            if (!Dm.truthy(requirement.valid_material(blade_mat)))
            {
                not_rigid = blade_mat;
                continue;
            }
            if (could_blade.amount < 5)
                continue;
            converted = could_blade;
            return await base.perform_rite(user, religious_tool);
        }
        // We've found a material but it wasn't solid enough.
        if (Dm.truthy(not_rigid))
            to_chat(user, span_warning(I18n.T("[] is not suitable for being made into gear!", not_rigid)));
        else
            to_chat(user, span_warning(I18n.T("You need at least five sheets of a rigid material to make gear!")));
        return FALSE;
    }

    // tg: code/modules/religion/rites.dm:449
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        object altar_turf = get_turf(religious_tool);
        obj_item_stack_sheet used_for_blade = converted;
        converted = null;
        if (QDELETED(used_for_blade) || !(get_turf(religious_tool) == used_for_blade.loc) || used_for_blade.amount < 5) //check if the same food is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        object material_used = used_for_blade.material_type;
        to_chat(user, span_warning(I18n.T("[] reshapes into a ceremonial blade!", used_for_blade)));
        if (!Dm.truthy(await used_for_blade.use(5)))//use 5 of the material
            return null;
        obj_item_ceremonial_blade blade = Dm.@new<obj_item_ceremonial_blade>(altar_turf);
        await blade.set_custom_materials(Dm.list(Dm.kv(await SSmaterials.get_material(material_used), SHEET_MATERIAL_AMOUNT * 5)));
        return TRUE;
    }
}

// tg: code/modules/religion/rites.dm:465
public partial class datum_religion_rites_unbreakable : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:472
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ishuman(user))
            return FALSE;
        if (HAS_TRAIT_FROM(user, TRAIT_UNBREAKABLE, INNATE_TRAIT))
        {
            to_chat(user, span_warning(I18n.T("Your spirit is already unbreakable!")));
            return FALSE;
        }
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/rites.dm:480
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        to_chat(user, span_nicegreen(I18n.T("You feel []'s will to keep fighting pouring into you!", GLOB.deity)));
        await user.AddComponent(typeof(datum_component_unbreakable));
        return null;
    }
}

// tg: code/modules/religion/rites.dm:485
public partial class datum_religion_rites_tenacious : datum_religion_rites
{
    // tg: code/modules/religion/rites.dm:492
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ishuman(user))
            return FALSE;
        if (HAS_TRAIT_FROM(user, TRAIT_TENACIOUS, INNATE_TRAIT))
        {
            to_chat(user, span_warning(I18n.T("Your spirit is already tenacious!")));
            return FALSE;
        }
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/rites.dm:500
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        to_chat(user, span_nicegreen(I18n.T("You feel []'s tenacity pouring into you!", GLOB.deity)));
        await user.AddElement(typeof(datum_element_tenacious));
        return null;
    }
}
