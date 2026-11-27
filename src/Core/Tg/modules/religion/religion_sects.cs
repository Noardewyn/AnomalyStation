// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/religion_sects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ten sects with their favour, blessing, sacrifice and conversion. The var blocks are generated (D111).

/**
 * # Religious Sects
 *
 * Religious Sects are a way to convert the fun of having an active 'god' (admin) to code-mechanics so you aren't having to press adminwho.
 *
 * Sects are not meant to overwrite the fun of choosing a custom god/religion, but meant to enhance it.
 * The idea is that Space Jesus (or whoever you worship) can be an evil bloodgod who takes the lifeforce out of people, a nature lover, or all things righteous and good. You decide!
 *
 */
// tg: code/modules/religion/religion_sects.dm:10
public partial class datum_religion_sect : datum
{
    // tg: code/modules/religion/religion_sects.dm:48
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(desired_items))
            desired_items_typecache = typecacheof(desired_items) as DmList;
        // tg bug kept: rites_list holds typepaths, and locate() finds only instances, so deaconize is always added
        if (!Dm.truthy(Dm.locate<datum_religion_rites_deaconize>(rites_list)))
            Dm.append(ref rites_list, Dm.list(typeof(datum_religion_rites_deaconize)));
        await on_select();
    }

    /// Activates once selected
    // tg: code/modules/religion/religion_sects.dm:57
    [ShouldCallParent]
    public virtual async DmTask on_select()
    {
        SSblackbox.record_feedback("text", "sect_chosen", 1, name);
        await DmTask.CompletedTask;
    }

    /// Activates once selected and on newjoins, oriented around people who become holy.
    // tg: code/modules/religion/religion_sects.dm:62
    [ShouldCallParent]
    public virtual async DmTask<object> on_conversion(mob_living chap = null)
    {
        to_chat(chap, span_boldnotice(I18n.T("\"[]\"", quote)));
        to_chat(chap, span_notice(I18n.T("[]", desc)));
        chap.add_faction(FACTION_HOLY);
        await DmTask.CompletedTask;
        return null;
    }

    /// Activates if religious sect is reset by admins, should clean up anything you added on conversion.
    // tg: code/modules/religion/religion_sects.dm:69
    [ShouldCallParent]
    public virtual void on_deconversion(mob_living chap = null)
    {
        to_chat(chap, span_boldnotice(I18n.T("You have lost the approval of \\the [].", name)));
        if (Dm.eq(chap.mind.holy_role, HOLY_ROLE_HIGHPRIEST))
            to_chat(chap, span_notice(I18n.T("Return to an altar to reform your sect.")));
        chap.remove_faction(FACTION_HOLY);
    }

    /// Returns TRUE if the item can be sacrificed. Can be modified to fit item being tested as well as person offering. Returning TRUE will stop the attackby sequence and proceed to on_sacrifice.
    // tg: code/modules/religion/religion_sects.dm:77
    public virtual object can_sacrifice(obj_item sacrifice = null, mob_living chap = null)
    {
        object dot = null;
        dot = TRUE;
        if (Dm.eq(chap.mind.holy_role, HOLY_ROLE_DEACON))
        {
            to_chat(chap, span_warning(I18n.T("You are merely a deacon of [], and therefore cannot perform rites.", GLOB.deity)));
            return dot;
        }
        if (!Dm.truthy(is_type_in_typecache(sacrifice, desired_items_typecache)))
            return FALSE;
        return dot;
    }

    /// Activates when the sect sacrifices an item. This proc has NO bearing on the attackby sequence of other objects when used in conjunction with the religious_tool component.
    // tg: code/modules/religion/religion_sects.dm:86
    public virtual async DmTask<object> on_sacrifice(obj_item sacrifice = null, mob_living chap = null)
    {
        await DmTask.CompletedTask;
        return adjust_favor(default_item_favor, chap);
    }

    /// Returns a description for religious tools
    // tg: code/modules/religion/religion_sects.dm:90
    public virtual object tool_examine(mob_living holy_creature = null)
    {
        return I18n.T("You are currently at [] favor with [].", round(favor), GLOB.deity);
    }

    /// Adjust Favor by a certain amount. Can provide optional features based on a user. Returns actual amount added/removed
    // tg: code/modules/religion/religion_sects.dm:94
    public virtual object adjust_favor(float? amount = null, mob_living chap = null)
    {
        amount ??= 0;
        object dot = null;
        dot = amount;
        if (favor + Dm.num(amount) < 0)
            dot = favor; //if favor = 5 and we want to subtract 10, we'll only be able to subtract 5
        if ((favor + Dm.num(amount) > max_favor))
            dot = (max_favor - favor); //if favor = 5 and we want to add 10 with a max of 10, we'll only be able to add 5
        favor = clamp(0, max_favor, favor + Dm.num(amount));
        return dot;
    }

    /// Sets favor to a specific amount. Can provide optional features based on a user.
    // tg: code/modules/religion/religion_sects.dm:103
    public virtual object set_favor(float? amount = null, mob_living chap = null)
    {
        amount ??= 0;
        favor = clamp(0, max_favor, Dm.num(amount));
        return favor;
    }

    /// Activates when an individual uses a rite. Can provide different/additional benefits depending on the user.
    // tg: code/modules/religion/religion_sects.dm:108
    public virtual void on_riteuse(mob_living user = null, atom religious_tool = null)
    {
    }

    /// Replaces the bible's bless mechanic. Return TRUE if you want to not do the brain hit.
    // tg: code/modules/religion/religion_sects.dm:111
    public virtual async DmTask<object> sect_bless(mob_living target = null, mob_living chap = null)
    {
        if (!ishuman(target))
            return BLESSING_FAILED;

        mob_living_carbon_human blessed = target as mob_living_carbon_human;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(blessed.get_bodyparts()))
        {
            if (Dm.truthy(IS_ROBOTIC_LIMB(bodypart)))
            {
                to_chat(chap, span_warning(I18n.T("[] refuses to heal this metallic taint!", GLOB.deity)));
                return BLESSING_IGNORED;
            }
        }

        return await standard_bless_healing(blessed, chap);
    }

    // tg: code/modules/religion/religion_sects.dm:123
    public virtual async DmTask<object> standard_bless_healing(object blessed = null, mob_living chap = null)
    {
        if (!ishuman(blessed))
        {
            await ((mob_living)blessed).adjust_brute_loss(-10);
            await ((mob_living)blessed).adjust_fire_loss(-10);
            return BLESSING_SUCCESS;
        }

        mob_living_carbon_human blessed_human = (mob_living_carbon_human)blessed;
        float heal_amt = 10;
        DmList hurt_limbs = blessed_human.get_damaged_bodyparts(1, 1, BODYTYPE_ORGANIC) as DmList;

        if (!Dm.truthy(length(hurt_limbs)))
            return BLESSING_IGNORED;

        foreach (obj_item_bodypart affecting in Dm.each_cast<obj_item_bodypart>(hurt_limbs))
        {
            if (Dm.truthy(await affecting.heal_damage(heal_amt, heal_amt, required_bodytype: BODYTYPE_ORGANIC)))
                await blessed_human.update_damage_overlays();
        }

        blessed_human.visible_message(span_notice(I18n.T("[] heals [] with the power of []!", chap, blessed_human, GLOB.deity)));
        to_chat(blessed_human, span_boldnotice(I18n.T("May the power of [] compel you to be healed!", GLOB.deity)));
        playsound(chap, SFX_PUNCH, 25, TRUE, -1);
        await blessed_human.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        return BLESSING_SUCCESS;
    }

    /// What happens if we bless a corpse? By default just do the default smack behavior
    // tg: code/modules/religion/religion_sects.dm:146
    public virtual async DmTask<object> sect_dead_bless(mob_living target = null, mob_living chap = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }
}

/**** Mechanical God ****/

// tg: code/modules/religion/religion_sects.dm:159
public partial class datum_religion_sect_mechanical : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:171
    public override async DmTask<object> sect_bless(mob_living target = null, mob_living chap = null)
    {
        if (iscyborg(target))
        {
            mob_living_silicon_robot R = target as mob_living_silicon_robot;
            float charge_amount = 0.05f * STANDARD_CELL_CHARGE;
            if (Dm.eq(target.mind?.holy_role, HOLY_ROLE_HIGHPRIEST))
                charge_amount *= 2;
            if (R.cell != null)
                ((obj_item_stock_parts_power_store)R.cell).charge += charge_amount;
            R.visible_message(span_notice(I18n.T("[] charges [] with the power of []!", chap, R, GLOB.deity)));
            to_chat(R, span_boldnotice(I18n.T("You are charged by the power of []!", GLOB.deity)));
            await R.add_mood_event("blessing", typeof(datum_mood_event_blessing));
            playsound(chap, Dm.resource("sound/effects/bang.ogg"), 25, TRUE, -1);
            return BLESSING_SUCCESS;
        }

        if (!ishuman(target))
            return BLESSING_FAILED;

        mob_living_carbon_human blessed = target as mob_living_carbon_human;

        //first we determine if we can charge them
        float did_we_charge = FALSE;
        obj_item_organ_stomach_ethereal eth_stomach = blessed.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (eth_stomach is obj_item_organ_stomach_ethereal)
        {
            await eth_stomach.adjust_charge(0.06f * STANDARD_CELL_CHARGE);
            did_we_charge = TRUE;
        }

        //if we're not targeting a robot part we stop early
        obj_item_bodypart bodypart = blessed.get_bodypart(chap.zone_selected) as obj_item_bodypart;
        if (Dm.truthy(IS_ORGANIC_LIMB(bodypart)))
        {
            if (!Dm.truthy(did_we_charge))
            {
                to_chat(chap, span_warning(I18n.T("[] scoffs at the idea of healing such fleshy matter!", GLOB.deity)));
                return BLESSING_IGNORED;
            }

            blessed.visible_message(span_notice(I18n.T("[] charges [] with the power of []!", chap, blessed, GLOB.deity)));
            to_chat(blessed, span_boldnotice(I18n.T("You feel charged by the power of []!", GLOB.deity)));
            await blessed.add_mood_event("blessing", typeof(datum_mood_event_blessing));
            playsound(chap, Dm.resource("sound/machines/synth/synth_yes.ogg"), 25, TRUE, -1);
            return BLESSING_SUCCESS;
        }

        //charge(?) and go
        if (Dm.truthy(await bodypart.heal_damage(5, 5, BODYTYPE_ROBOTIC)))
            await blessed.update_damage_overlays();

        blessed.visible_message(span_notice(I18n.T("[] [] [] with the power of []!", chap, Dm.truthy(did_we_charge) ? I18n.T("repairs and charges") : I18n.T("repairs"), blessed, GLOB.deity)));
        to_chat(blessed, span_boldnotice(I18n.T("The inner machinations of [] [] you!", GLOB.deity, Dm.truthy(did_we_charge) ? I18n.T("repairs and charges") : I18n.T("repairs"))));
        playsound(chap, Dm.resource("sound/effects/bang.ogg"), 25, TRUE, -1);
        await blessed.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        return BLESSING_SUCCESS;
    }

    // tg: code/modules/religion/religion_sects.dm:219
    public override async DmTask<object> on_sacrifice(obj_item sacrifice = null, mob_living chap = null)
    {
        obj_item_stock_parts_power_store_cell power_cell = sacrifice as obj_item_stock_parts_power_store_cell;
        if (!(power_cell is obj_item_stock_parts_power_store_cell))
            return null;

        if (Dm.num(power_cell.charge__proc()) < 0.3f * STANDARD_CELL_CHARGE)
        {
            to_chat(chap, span_notice(I18n.T("[] does not accept pity amounts of power.", GLOB.deity)));
            return null;
        }

        adjust_favor(round(Dm.num(power_cell.charge__proc()) / (0.3f * STANDARD_CELL_CHARGE)), chap);
        to_chat(chap, span_notice(I18n.T("You offer []'s power to [], pleasing them.", power_cell, GLOB.deity)));
        qdel(power_cell);
        await DmTask.CompletedTask;
        return TRUE;
    }
}

/**** Pyre God ****/

// tg: code/modules/religion/religion_sects.dm:234
public partial class datum_religion_sect_pyre : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:245
    public override async DmTask on_select()
    {
        await base.on_select();
        await AddComponent(typeof(datum_component_sect_nullrod_bonus), Dm.list(
            Dm.kv(typeof(obj_item_gun_ballistic_bow_divine), Dm.list(
                typeof(datum_religion_rites_blazing_star)))));
    }

    // tg: code/modules/religion/religion_sects.dm:253
    public override async DmTask<object> on_sacrifice(obj_item sacrifice = null, mob_living chap = null)
    {
        obj_item_flashlight_flare_candle offering = sacrifice as obj_item_flashlight_flare_candle;
        if (!(offering is obj_item_flashlight_flare_candle))
            return null;
        if (!Dm.truthy(offering.light_on))
        {
            to_chat(chap, span_notice(I18n.T("The candle needs to be lit to be offered!")));
            return null;
        }
        to_chat(chap, span_notice(I18n.T("[] is pleased with your sacrifice.", GLOB.deity)));
        adjust_favor(40, chap); //it's not a lot but hey there's a pacifist favor option at least
        qdel(offering);
        await DmTask.CompletedTask;
        return TRUE;
    }
}

// tg: code/modules/religion/religion_sects.dm:266
public partial class datum_religion_sect_greed : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:264  #define GREEDY_HEAL_COST 50 (file-local, #undef at :311)
    private const int GREEDY_HEAL_COST = 50;

    // tg: code/modules/religion/religion_sects.dm:275
    public override object tool_examine(mob_living holy_creature = null) //display money policy
    {
        return I18n.T("In the eyes of [], your wealth is your favor.", GLOB.deity);
    }

    // tg: code/modules/religion/religion_sects.dm:278
    public override async DmTask<object> sect_bless(mob_living target = null, mob_living chap = null)
    {
        mob_living blessed_living = target;
        if (!ishuman(blessed_living))
            return BLESSING_FAILED;

        datum_bank_account account = chap.get_bank_account() as datum_bank_account;
        if (account == null)
        {
            to_chat(chap, span_warning(I18n.T("You need a way to pay for the heal!")));
            return BLESSING_IGNORED;
        }

        if (Dm.num(account.account_balance) < GREEDY_HEAL_COST)
        {
            to_chat(chap, span_warning(I18n.T("Healing from [] costs [] [] for 30 health!", GLOB.deity, GREEDY_HEAL_COST, MONEY_NAME)));
            return BLESSING_IGNORED;
        }

        mob_living_carbon_human blessed = blessed_living as mob_living_carbon_human;
        foreach (obj_item_bodypart robolimb in Dm.each_cast<obj_item_bodypart>(blessed.get_bodyparts()))
        {
            if (Dm.truthy(IS_ROBOTIC_LIMB(robolimb)))
            {
                to_chat(chap, span_warning(I18n.T("[] refuses to heal this metallic taint!", GLOB.deity)));
                return BLESSING_IGNORED;
            }
        }

        account.adjust_money(-GREEDY_HEAL_COST, "Church Donation: Treatment");
        float heal_amt = 30;
        DmList hurt_limbs = blessed.get_damaged_bodyparts(1, 1, BODYTYPE_ORGANIC) as DmList;
        if (!Dm.truthy(length(hurt_limbs)))
            return BLESSING_IGNORED;

        foreach (obj_item_bodypart affecting in Dm.each_cast<obj_item_bodypart>(hurt_limbs))
        {
            if (Dm.truthy(await affecting.heal_damage(heal_amt, heal_amt, required_bodytype: BODYTYPE_ORGANIC)))
                await blessed.update_damage_overlays();
        }

        blessed.visible_message(span_notice(I18n.T("[] barters a heal for [] from []!", chap, blessed, GLOB.deity)));
        to_chat(blessed, span_boldnotice(I18n.T("May the power of [] compel you to be healed! Thank you for choosing []!", GLOB.deity, GLOB.deity)));
        playsound(chap, Dm.resource("sound/effects/cashregister.ogg"), 60, TRUE);
        await blessed.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        return BLESSING_SUCCESS;
    }
}

// tg: code/modules/religion/religion_sects.dm:315
public partial class datum_religion_sect_burden : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:327
    public override async DmTask<object> on_conversion(mob_living chap = null)
    {
        mob_living new_convert = chap;
        await base.on_conversion(chap);
        if (!ishuman(new_convert))
        {
            to_chat(new_convert, span_warning(I18n.T("[] needs higher level creatures to fully comprehend the suffering. You are not burdened.", GLOB.deity)));
            return null;
        }
        await ((mob_living_carbon_human)new_convert).gain_trauma(typeof(datum_brain_trauma_special_burdened), TRAUMA_RESILIENCE_ABSOLUTE);
        return null;
    }

    // tg: code/modules/religion/religion_sects.dm:334
    public override void on_deconversion(mob_living chap = null)
    {
        mob_living new_convert = chap;
        if (ishuman(new_convert))
            ((mob_living_carbon_human)new_convert).cure_trauma_type(typeof(datum_brain_trauma_special_burdened), TRAUMA_RESILIENCE_ABSOLUTE);
        base.on_deconversion(chap);
    }

    // tg: code/modules/religion/religion_sects.dm:339
    public override object tool_examine(mob_living holy_creature = null) //display burden level
    {
        mob_living burdened = holy_creature;
        if (ishuman(burdened))
        {
            datum_brain_trauma_special_burdened burden = ((mob_living_carbon_human)burdened).has_trauma_type(typeof(datum_brain_trauma_special_burdened)) as datum_brain_trauma_special_burdened;
            if (burden != null)
                return I18n.T("You are at burden level []/9.", burden.burden_level);
        }
        return I18n.T("You are not burdened.");
    }

    // tg: code/modules/religion/religion_sects.dm:346
    public override async DmTask<object> sect_bless(mob_living target = null, mob_living chap = null)
    {
        mob_living_carbon target_carbon = target as mob_living_carbon;
        mob_living_carbon chaplain = chap as mob_living_carbon;
        if (!(target_carbon is mob_living_carbon) || !(chaplain is mob_living_carbon))
            return BLESSING_FAILED;

        datum_brain_trauma_special_burdened burden = chaplain.has_trauma_type(typeof(datum_brain_trauma_special_burdened)) as datum_brain_trauma_special_burdened;
        if (burden == null)
            return BLESSING_FAILED;

        float burden_modifier = max(1 - 0.07f * burden.burden_level, 0.01f);
        object transferred = FALSE;
        DmList hurt_limbs = Dm.add(target_carbon.get_damaged_bodyparts(1, 1, BODYTYPE_ORGANIC), target_carbon.get_wounded_bodyparts(BODYTYPE_ORGANIC)) as DmList;
        DmList chaplains_limbs = Dm.list();
        foreach (obj_item_bodypart possible_limb in Dm.each<obj_item_bodypart>(chaplain.get_bodyparts()))
        {
            if (Dm.truthy(IS_ORGANIC_LIMB(possible_limb)))
                Dm.append(ref chaplains_limbs, possible_limb);
        }

        if (Dm.truthy(length(chaplains_limbs)))
        {
            foreach (obj_item_bodypart affected_limb in Dm.each_cast<obj_item_bodypart>(hurt_limbs))
            {
                obj_item_bodypart chaplains_limb = chaplain.get_bodypart(Dm.str(affected_limb.body_zone)) as obj_item_bodypart;
                if (chaplains_limb == null || !Dm.truthy(IS_ORGANIC_LIMB(chaplains_limb)))
                    chaplains_limb = pick(chaplains_limbs) as obj_item_bodypart;
                float brute_damage = affected_limb.brute_dam;
                float burn_damage = affected_limb.burn_dam;
                if ((Dm.truthy(brute_damage) || Dm.truthy(burn_damage)))
                {
                    transferred = TRUE;
                    await affected_limb.heal_damage(brute_damage, burn_damage, required_bodytype: BODYTYPE_ORGANIC);
                    await chaplains_limb.receive_damage(brute_damage * burden_modifier, burn_damage * burden_modifier, forced: TRUE, wound_bonus: CANT_WOUND);
                }
                foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(affected_limb.wounds))
                {
                    transferred = TRUE;
                    await iter_wound.remove_wound();
                    await iter_wound.apply_wound(chaplains_limb);
                }
            }

            if (HAS_TRAIT_FROM(target_carbon, TRAIT_HUSK, BURN))
            {
                transferred = TRUE;
                await target_carbon.cure_husk(BURN);
                chaplain.become_husk(BURN);
            }
        }

        object toxin_damage = target_carbon.get_tox_loss();
        if (Dm.truthy(toxin_damage) && !HAS_TRAIT(chaplain, TRAIT_TOXIMMUNE))
        {
            transferred = TRUE;
            await target_carbon.adjust_tox_loss(Dm.neg(toxin_damage));
            await chaplain.adjust_tox_loss(Dm.mul(toxin_damage, burden_modifier), forced: TRUE);
        }

        object suffocation_damage = target_carbon.get_oxy_loss();
        if (Dm.truthy(suffocation_damage) && !HAS_TRAIT(chaplain, TRAIT_NOBREATH))
        {
            transferred = TRUE;
            await target_carbon.adjust_oxy_loss(Dm.neg(suffocation_damage));
            await chaplain.adjust_oxy_loss(Dm.mul(suffocation_damage, burden_modifier));
        }

        object cached_blood_volume = target_carbon.get_blood_volume();
        if (Dm.lt(cached_blood_volume, BLOOD_VOLUME_SAFE))
        {
            if (Dm.truthy(target_carbon.get_blood_compatibility(chaplain)))
            {
                object amount_to_transfer = Dm.sub(BLOOD_VOLUME_SAFE, cached_blood_volume);
                Dm.combine(ref transferred, await chaplain.transfer_blood_to(target_carbon, amount_to_transfer, ignore_low_blood: TRUE));
            }
        }
        else if (Dm.gt(cached_blood_volume, BLOOD_VOLUME_EXCESS))
            Dm.combine(ref transferred, await target_carbon.transfer_blood_to(chaplain, Dm.sub(cached_blood_volume, BLOOD_VOLUME_EXCESS)));

        await target_carbon.update_damage_overlays();
        await chaplain.update_damage_overlays();
        if (!Dm.truthy(transferred))
        {
            to_chat(chaplain, span_warning(I18n.T("They hold no burden!")));
            return BLESSING_IGNORED;
        }

        target_carbon.visible_message(span_notice(I18n.T("[] takes on []'s burden!", chaplain, target_carbon)));
        to_chat(target_carbon, span_boldnotice(I18n.T("May the power of [] compel you to be healed!", GLOB.deity)));
        playsound(chaplain, SFX_PUNCH, 25, vary: TRUE, extrarange: -1);
        await target_carbon.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        return BLESSING_SUCCESS;
    }

    // tg: code/modules/religion/religion_sects.dm:415
    public override async DmTask<object> sect_dead_bless(mob_living target = null, mob_living chap = null)
    {
        return await sect_bless(target, chap);
    }
}

// tg: code/modules/religion/religion_sects.dm:418
public partial class datum_religion_sect_honorbound : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:428
    public override async DmTask<object> on_conversion(mob_living chap = null)
    {
        mob_living new_convert = chap;
        await base.on_conversion(chap);
        if (!ishuman(new_convert))
        {
            to_chat(new_convert, span_warning(I18n.T("[] has no respect for lower creatures, and refuses to make you honorbound.", GLOB.deity)));
            return FALSE;
        }
        await ((mob_living_carbon)new_convert).gain_trauma(typeof(datum_brain_trauma_special_honorbound), TRAUMA_RESILIENCE_MAGIC);
        return null;
    }

    // tg: code/modules/religion/religion_sects.dm:435
    public override void on_deconversion(mob_living chap = null)
    {
        mob_living new_convert = chap;
        if (ishuman(new_convert))
            ((mob_living_carbon_human)new_convert).cure_trauma_type(typeof(datum_brain_trauma_special_honorbound), TRAUMA_RESILIENCE_MAGIC);
        base.on_deconversion(chap);
    }
}

// tg: code/modules/religion/religion_sects.dm:442
public partial class datum_religion_sect_maintenance : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:440  #define MINIMUM_YUCK_REQUIRED 5 (file-local, #undef at :483)
    private const int MINIMUM_YUCK_REQUIRED = 5;

    // tg: code/modules/religion/religion_sects.dm:452
    public override async DmTask<object> sect_bless(mob_living target = null, mob_living chap = null)
    {
        mob_living blessed_living = target;
        if (!ishuman(blessed_living))
            return BLESSING_FAILED;

        mob_living_carbon_human blessed = blessed_living as mob_living_carbon_human;
        if (Dm.truthy(((datum_reagents)blessed.reagents).has_reagent(typeof(datum_reagent_drug_maint_sludge))))
        {
            to_chat(blessed, span_warning(I18n.T("[] has already empowered them.", GLOB.deity)));
            return BLESSING_IGNORED;
        }

        await ((datum_reagents)blessed.reagents).add_reagent(typeof(datum_reagent_drug_maint_sludge), 5);
        blessed.visible_message(span_notice(I18n.T("[] empowers [] with the power of []!", chap, blessed, GLOB.deity)));
        to_chat(blessed, span_boldnotice(I18n.T("The power of [] has made you harder to wound for a while!", GLOB.deity)));
        playsound(chap, SFX_PUNCH, 25, TRUE, -1);
        await blessed.add_mood_event("blessing", typeof(datum_mood_event_blessing));
        return BLESSING_SUCCESS; //trust me, you'll be feeling the pain from the maint drugs all well enough
    }

    // tg: code/modules/religion/religion_sects.dm:468
    public override async DmTask<object> on_sacrifice(obj_item sacrifice = null, mob_living chap = null)
    {
        obj_item_reagent_containers offering = sacrifice as obj_item_reagent_containers;
        mob_living user = chap;
        if (!(offering is obj_item_reagent_containers))
            return null;
        object wanted_yuck = ((datum_reagents)offering.reagents).has_reagent(typeof(datum_reagent_yuck), MINIMUM_YUCK_REQUIRED);
        object favor_earned = ((datum_reagents)offering.reagents).get_reagent_amount(typeof(datum_reagent_yuck));
        if (!Dm.truthy(wanted_yuck))
        {
            to_chat(user, span_warning(I18n.T("[] does not have enough organic slurry for [] to enjoy.", offering, GLOB.deity)));
            return null;
        }
        to_chat(user, span_notice(I18n.T("[] loves organic slurry.", GLOB.deity)));
        adjust_favor(Dm.num(favor_earned), user);
        playsound(get_turf(offering), Dm.resource("sound/items/drink.ogg"), 50, TRUE);
        await ((datum_reagents)offering.reagents).clear_reagents();
        return TRUE;
    }
}

// tg: code/modules/religion/religion_sects.dm:484
public partial class datum_religion_sect_spar : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:510
    public override object tool_examine(mob_living holy_creature = null)
    {
        return I18n.T("You have [] sparring matches won in []'s name to redeem. You have lost [] holy matches. You will be excommunicated after losing three matches.", round(favor), GLOB.deity, matches_lost);
    }
}

// tg: code/modules/religion/religion_sects.dm:513
public partial class datum_religion_sect_music : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:531
    public override async DmTask<object> on_conversion(mob_living chap = null)
    {
        object dot = null;
        dot = await base.on_conversion(chap);
        Dm.@new<obj_item_choice_beacon_music>(get_turf(chap));
        return dot;
    }
}

// tg: code/modules/religion/religion_sects.dm:535
public partial class datum_religion_sect_dreams : datum_religion_sect
{
    // tg: code/modules/religion/religion_sects.dm:568
    public override async DmTask<object> on_conversion(mob_living chap = null)
    {
        object dot = null;
        dot = await base.on_conversion(chap);
        RegisterSignal(chap, COMSIG_PRE_DREAMING, nameof(pre_dream));
        RegisterSignal(chap, COMSIG_START_DREAMING, nameof(on_dream));
        if (Dm.truthy(dream_protection))
            await chap.apply_status_effect(typeof(datum_status_effect_dream_protection));
        return dot;
    }

    // tg: code/modules/religion/religion_sects.dm:575
    public override void on_deconversion(mob_living chap = null)
    {
        base.on_deconversion(chap);
        UnregisterSignal(chap, COMSIG_PRE_DREAMING);
        UnregisterSignal(chap, COMSIG_START_DREAMING);
        chap.remove_status_effect(typeof(datum_status_effect_dream_protection));
    }

    // tg: code/modules/religion/religion_sects.dm:581
    [SignalHandler]
    public virtual void pre_dream(mob_living chap = null, DmList dream_pool = null)
    {
        // prioritize specific portents if they're in the pool
        if (Dm.truthy(Dm.locate<datum_dream_specific_portent>(dream_pool)))
            return;
        if (!(vague_portent_cooldown <= world.time)) // COOLDOWN_FINISHED(src, vague_portent_cooldown), inline (rules 11.1 (a))
            return;
        if (!prob(vague_portent_chance))
            return;

        dream_pool[Dm.@new<datum_dream_random_vague_portent>()] = Dm.initial(typeof(datum_dream_random), (datum_dream_random p) => p.weight) * 0.1f;
    }

    // tg: code/modules/religion/religion_sects.dm:594
    [SignalHandler]
    public virtual void on_dream(mob_living chap = null, datum_dream dream_instance = null)
    {
        // no reward for the dream your god is specifically giving you
        if (dream_instance is datum_dream_specific_portent)
            return;

        float dream_favor = 10;
        // but you do get a slight boost for having a *vague* one
        if (dream_instance is datum_dream_random_vague_portent)
        {
            vague_portent_cooldown = world.time + 30 * SECONDS; // COOLDOWN_START(src, vague_portent_cooldown, 30 SECONDS)
            dream_favor *= 1.5f;
        }

        to_chat(chap, span_cyan(I18n.T("[] approves of your slumber.", GLOB.deity)));
        adjust_favor(dream_favor, chap);
    }

    // dream blessing only works on dreaming targets.
    // blessing someone asleep causes them to dream, and blessing a dreamer rewards favor.
    // it also heals regardless of if the target is mechanical or organic. do robots dream of electric sheep?
    // tg: code/modules/religion/religion_sects.dm:613
    public override async DmTask<object> sect_bless(mob_living target = null, mob_living chap = null)
    {
        if (HAS_TRAIT(target, TRAIT_DREAMING))
        {
            object result = await standard_bless_healing(target, chap);
            string tarref = Dm.str(REF(target));
            if (!Dm.truthy(LAZYFIND(recent_bless_refs, tarref)))
            {
                adjust_favor(20 * (target.mind == null ? 0.5f : 1) * (Dm.truthy(target.IsSleeping()) ? 1 : 0.5f), chap);
                LAZYADD(ref recent_bless_refs, tarref);
                _addtimer(Dm.@new<datum_callback>(this, nameof(clear_bless_ref), tarref), 6 * MINUTES, file: "code/modules/religion/religion_sects.dm", line: 620);
                result = BLESSING_SUCCESS;
            }

            if (Dm.eq(result, BLESSING_SUCCESS))
                to_chat(chap, span_cyan(I18n.T("[] approves of []'s slumber.", GLOB.deity, target)));
            return result;
        }

        if (IS_UNCONSCIOUS(target))
        {
            if (iscarbon(target))
            {
                mob_living_carbon sleeper = target as mob_living_carbon;
                sleeper.dream();
            }

            to_chat(chap, span_cyan(I18n.T("[] blesses []'s slumber.", GLOB.deity, target)));
            object result = await standard_bless_healing(target, chap);
            if (Dm.truthy(dream_protection) && target.mind != null && Dm.truthy(await target.apply_status_effect(typeof(datum_status_effect_dream_protection_temporary))))
                result = BLESSING_SUCCESS;

            if (Dm.eq(result, BLESSING_SUCCESS))
                to_chat(chap, span_cyan(I18n.T("[] blesses []'s slumber.", GLOB.deity, target)));
            return result;
        }

        to_chat(chap, span_warning(I18n.T("[] has no interest in blessing the waking.", GLOB.deity)));
        return BLESSING_IGNORED;
    }

    // tg: code/modules/religion/religion_sects.dm:644
    public override async DmTask<object> sect_dead_bless(mob_living target = null, mob_living chap = null)
    {
        string tarref = Dm.str(REF(target));
        if (Dm.truthy(LAZYFIND(recent_bless_refs, tarref)))
            return BLESSING_IGNORED;

        to_chat(chap, span_cyan(I18n.T("[] watches over []'s eternal rest.", GLOB.deity, target)));
        if (Dm.truthy(dream_protection) && target.mind != null)
            await target.apply_status_effect(typeof(datum_status_effect_dream_protection_deceased));
        adjust_favor(10 * (target.mind != null ? 1 : 0.5f), chap);
        LAZYADD(ref recent_bless_refs, tarref);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_bless_ref), tarref), 6 * MINUTES, file: "code/modules/religion/religion_sects.dm", line: 655);
        return BLESSING_SUCCESS;
    }

    // tg: code/modules/religion/religion_sects.dm:657
    public virtual void clear_bless_ref(object tarref = null)
    {
        LAZYREMOVE(ref recent_bless_refs, tarref);
    }
}
