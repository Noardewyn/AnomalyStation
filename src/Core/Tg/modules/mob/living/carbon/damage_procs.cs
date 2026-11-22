// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/damage_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "carbon": damage_procs.dm): every carbon override of the damage API and the
// bodypart helpers, with damage spread over the bodyparts. The stamina crit status effect is P1's (GAP below).

// tg: code/modules/mob/living/carbon/damage_procs.dm:1
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/damage_procs.dm:1
    public override async DmTask<object> apply_damage(float? damage = null, string damagetype = null, object def_zone = null, float? blocked = null, float? forced = null, float? spread_damage = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null, object attack_direction = null, object attacking_item = null, float? wound_clothing = null)
    {
        damage ??= 0;
        damagetype ??= BRUTE;
        blocked ??= 0;
        forced ??= FALSE;
        spread_damage ??= FALSE;
        wound_bonus ??= 0;
        exposed_wound_bonus ??= 0;
        sharpness ??= NONE;
        wound_clothing ??= TRUE;
        object dot = null;
        // Spread damage should always have def zone be null
        if (Dm.truthy(spread_damage))
            def_zone = null;

        // Otherwise if def zone is null, we'll get a random bodypart / zone to hit.
        // ALso we'll automatically covnert string def zones into bodyparts to pass into parent call.
        else if (!isbodypart(def_zone))
        {
            object random_zone = check_zone((def_zone is var or__1 && Dm.truthy(or__1) ? or__1 : get_random_valid_zone(def_zone)));
            def_zone = (get_bodypart(random_zone as string) is var or__2 && Dm.truthy(or__2) ? or__2 : get_bodypart());
        }

        dot = await base.apply_damage(damage, damagetype, def_zone, blocked, forced, spread_damage, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing);
        // Taking brute or burn to bodyparts gives a damage flash
        if (Dm.truthy(def_zone) && (damagetype == BRUTE || damagetype == BURN))
            damageoverlaytemp += Dm.num(dot);

        return dot;
    }

    //These procs fetch a cumulative total damage from all bodyparts
    // tg: code/modules/mob/living/carbon/damage_procs.dm:32
    public override object get_brute_loss()
    {
        float amount = 0;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            amount += bodypart.brute_dam;
        return round(amount, DAMAGE_PRECISION);
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:38
    public override object get_fire_loss()
    {
        float amount = 0;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            amount += bodypart.burn_dam;
        return round(amount, DAMAGE_PRECISION);
    }

    /**
     * Returns the amount of bruteloss across all bodyparts meeting the matching bodytype.
     * Useful for if you would like to check the bruteloss for only organic bodyparts, for example.
     *
     * Arguments:
     * *  required_bodytype - The bodytype(s) to match against.
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:53
    public virtual object get_brute_loss_for_type(float? required_bodytype = null)
    {
        required_bodytype ??= ALL;
        float amount = 0;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (!Dm.truthy(Dm.bitand(bodypart.bodytype, required_bodytype)))
                continue;
            amount += bodypart.brute_dam;
        }
        return round(amount, DAMAGE_PRECISION);
    }

    /**
     * Returns the amount of fireloss across all bodyparts meeting the matching bodytype.
     * Useful for if you would like to check the fireloss for only organic bodyparts, for example.
     *
     * Arguments:
     * *  required_bodytype - The bodytype(s) to match against.
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:68
    public virtual object get_fire_loss_for_type(float? required_bodytype = null)
    {
        required_bodytype ??= ALL;
        float amount = 0;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (!Dm.truthy(Dm.bitand(bodypart.bodytype, required_bodytype)))
                continue;
            amount += bodypart.burn_dam;
        }
        return round(amount, DAMAGE_PRECISION);
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:75
    public override async DmTask<object> adjust_brute_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        object dot = null;
        if (!Dm.truthy(can_adjust_brute_loss(amount, forced, required_bodytype)))
            return 0;
        if (Dm.gt(amount, 0))
            dot = await take_overall_damage(brute: Dm.num(amount), updating_health: updating_health, forced: forced, required_bodytype: required_bodytype);
        else
            dot = await heal_overall_damage(brute: abs(Dm.num(amount)), required_bodytype: required_bodytype, updating_health: updating_health, forced: forced);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:83
    public override async DmTask<object> set_brute_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        object current = get_brute_loss();
        object diff = Dm.sub(amount, current);
        if (!Dm.truthy(diff))
            return FALSE;
        return await adjust_brute_loss(diff, updating_health, forced, required_bodytype);
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:92
    public override async DmTask<object> adjust_fire_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        object dot = null;
        if (!Dm.truthy(can_adjust_fire_loss(amount, forced, required_bodytype)))
            return 0;
        if (Dm.gt(amount, 0))
            dot = await take_overall_damage(burn: Dm.num(amount), updating_health: updating_health, forced: forced, required_bodytype: required_bodytype);
        else
            dot = await heal_overall_damage(burn: abs(Dm.num(amount)), required_bodytype: required_bodytype, updating_health: updating_health, forced: forced);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:100
    public override async DmTask<object> set_fire_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        object current = get_fire_loss();
        object diff = Dm.sub(amount, current);
        if (!Dm.truthy(diff))
            return FALSE;
        return await adjust_fire_loss(diff, updating_health, forced, required_bodytype);
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:109
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
        if (Dm.eq(damage_type, STAMINA) && Dm.gt(amount, 0) && (maxHealth - staminaloss) <= crit_threshold && stat != DEAD)
            await apply_status_effect(typeof(datum_status_effect_incapacitating_stamcrit));
    }

    /**
     * If an organ exists in the slot requested, and we are capable of taking damage (we don't have TRAIT_GODMODE), call the damage proc on that organ.
     *
     * Arguments:
     * * slot - organ slot, like [ORGAN_SLOT_HEART]
     * * amount - damage to be done
     * * maximum - currently an arbitrarily large number, can be set so as to limit damage
     * * required_organ_flag - targets only a specific organ type if set to ORGAN_ORGANIC or ORGAN_ROBOTIC
     *
     * Returns: The net change in damage from apply_organ_damage()
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:126
    public override async DmTask<object> adjust_organ_loss(object slot = null, object amount = null, object maximum = null, object required_organ_flag = null)
    {
        required_organ_flag ??= NONE;
        obj_item_organ affected_organ = get_organ_slot(slot) as obj_item_organ;
        if (affected_organ == null || HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        if (Dm.truthy(required_organ_flag) && !Dm.truthy(Dm.bitand(affected_organ.organ_flags, required_organ_flag)))
            return FALSE;
        return await affected_organ.apply_organ_damage(amount, maximum);
    }

    /**
     * If an organ exists in the slot requested, and we are capable of taking damage (we don't have TRAIT_GODMODE), call the set damage proc on that organ, which can
     * set or clear the failing variable on that organ, making it either cease or start functions again, unlike adjust_organ_loss.
     *
     * Arguments:
     * * slot - organ slot, like [ORGAN_SLOT_HEART]
     * * amount - damage to be set to
     * * required_organ_flag - targets only a specific organ type if set to ORGAN_ORGANIC or ORGAN_ROBOTIC
     *
     * Returns: The net change in damage from set_organ_damage()
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:145
    [DmParams("slot", "amount", "required_organ_flag")]
    public override async DmTask<object> set_organ_loss(object slot = null, object amount = null, object maximum = null, object required_organ_flag = null)
    {
        // The override's third DM parameter is required_organ_flag: a positional third argument arrives in the slot's third
        // C# parameter (rules 7.2), and a fourth one is an extra the override drops, as in DM.
        required_organ_flag = maximum;
        required_organ_flag ??= NONE;
        obj_item_organ affected_organ = get_organ_slot(slot) as obj_item_organ;
        if (affected_organ == null || HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        if (Dm.truthy(required_organ_flag) && !Dm.truthy(Dm.bitand(affected_organ.organ_flags, required_organ_flag)))
            return FALSE;
        if (Dm.eq(affected_organ.damage, amount))
            return FALSE;
        return await affected_organ.set_organ_damage(amount);
    }

    /**
     * If an organ exists in the slot requested, return the amount of damage that organ has
     *
     * Arguments:
     * * slot - organ slot, like [ORGAN_SLOT_HEART]
     * * required_organ_flag - if you only want to check the damage of organs with the specified organ_flag(s) then you can use this.
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:162
    public override object get_organ_loss(object slot = null, object required_organ_flag = null)
    {
        required_organ_flag ??= NONE;
        obj_item_organ affected_organ = get_organ_slot(slot) as obj_item_organ;
        if (affected_organ != null)
        {
            if (Dm.truthy(required_organ_flag) && !Dm.truthy(Dm.bitand(affected_organ.organ_flags, required_organ_flag)))
                return null;
            return affected_organ.damage;
        }
        return null;
    }

    ////////////////////////////////////////////

    ///Returns a list of damaged bodyparts
    // tg: code/modules/mob/living/carbon/damage_procs.dm:172
    public virtual object get_damaged_bodyparts(float? brute = null, float? burn = null, float? required_bodytype = null, object target_zone = null)
    {
        brute ??= FALSE;
        burn ??= FALSE;
        required_bodytype ??= NONE;
        DmList parts = Dm.list();
        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(BP.bodytype, required_bodytype)))
                continue;
            if (target_zone != null && !Dm.eq(BP.body_zone, target_zone))
                continue;
            if ((Dm.truthy(brute) && Dm.truthy(BP.brute_dam)) || (Dm.truthy(burn) && Dm.truthy(BP.burn_dam)))
                Dm.append(ref parts, BP);
        }
        return parts;
    }

    ///Returns a list of damageable bodyparts
    // tg: code/modules/mob/living/carbon/damage_procs.dm:184
    public virtual object get_damageable_bodyparts(object required_bodytype = null)
    {
        DmList parts = Dm.list();
        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(BP.bodytype, required_bodytype)))
                continue;
            if (BP.brute_dam + BP.burn_dam < BP.max_damage)
                Dm.append(ref parts, BP);
        }
        return parts;
    }

    ///Returns a list of bodyparts with wounds (in case someone has a wound on an otherwise fully healed limb)
    // tg: code/modules/mob/living/carbon/damage_procs.dm:195
    public virtual object get_wounded_bodyparts(object required_bodytype = null)
    {
        DmList parts = Dm.list();
        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(BP.bodytype, required_bodytype)))
                continue;
            if (Dm.truthy(LAZYLEN(BP.wounds)))
                Dm.append(ref parts, BP);
        }
        return parts;
    }

    /**
     * Heals ONE bodypart randomly selected from damaged ones.

     * It automatically updates damage overlays if necessary
     *
     * It automatically updates health status
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:211
    public override async DmTask<object> heal_bodypart_damage(float? brute = null, float? burn = null, float? updating_health = null, float? required_bodytype = null, object target_zone = null)
    {
        brute ??= 0;
        burn ??= 0;
        updating_health ??= TRUE;
        required_bodytype ??= NONE;
        object dot = null;
        dot = FALSE;
        DmList parts = get_damaged_bodyparts(brute, burn, required_bodytype, target_zone) as DmList;
        if (!Dm.truthy(parts.len))
            return dot;

        obj_item_bodypart picked = pick(parts) as obj_item_bodypart;
        object damage_calculator = picked.get_damage(); //heal_damage returns update status T/F instead of amount healed so we dance gracefully around this
        if (Dm.truthy(await picked.heal_damage(abs(Dm.num(brute)), abs(Dm.num(burn)), required_bodytype: required_bodytype)))
            await update_damage_overlays();
        return (Dm.sub(damage_calculator, picked.get_damage()));
    }

    /**
     * Damages ONE bodypart randomly selected from damagable ones.
     *
     * It automatically updates damage overlays if necessary
     *
     * It automatically updates health status
     */
    // tg: code/modules/mob/living/carbon/damage_procs.dm:231
    public override async DmTask<object> take_bodypart_damage(float? brute = null, float? burn = null, float? updating_health = null, object required_bodytype = null, float? check_armor = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null)
    {
        brute ??= 0;
        burn ??= 0;
        updating_health ??= TRUE;
        check_armor ??= FALSE;
        wound_bonus ??= 0;
        exposed_wound_bonus ??= 0;
        sharpness ??= NONE;
        object dot = null;
        dot = FALSE;
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return dot;
        DmList parts = get_damageable_bodyparts(required_bodytype) as DmList;
        if (!Dm.truthy(parts.len))
            return dot;

        obj_item_bodypart picked = pick(parts) as obj_item_bodypart;
        object damage_calculator = picked.get_damage();
        if (Dm.truthy(await picked.receive_damage(abs(Dm.num(brute)), abs(Dm.num(burn)), Dm.truthy(check_armor) ? Dm.num(run_armor_check(picked, (Dm.truthy(brute) ? MELEE : Dm.truthy(burn) ? FIRE : null))) : FALSE, wound_bonus: wound_bonus, exposed_wound_bonus: exposed_wound_bonus, sharpness: sharpness)))
            await update_damage_overlays();
        return (Dm.sub(damage_calculator, picked.get_damage()));
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:245
    public override async DmTask<object> heal_overall_damage(float? brute = null, float? burn = null, float? stamina = null, object required_bodytype = null, float? updating_health = null, float? forced = null)
    {
        brute ??= 0;
        burn ??= 0;
        stamina ??= 0;
        updating_health ??= TRUE;
        forced ??= FALSE;
        object dot = null;
        dot = FALSE;
        // treat negative args as positive
        brute = abs(Dm.num(brute));
        burn = abs(Dm.num(burn));

        DmList parts = get_damaged_bodyparts(brute, burn, required_bodytype == null ? null : Dm.num(required_bodytype)) as DmList;

        object update = NONE;
        while (Dm.truthy(parts.len) && (brute > 0 || burn > 0))
        {
            obj_item_bodypart picked = pick(parts) as obj_item_bodypart;

            float brute_was = picked.brute_dam;
            float burn_was = picked.burn_dam;
            dot = Dm.add(dot, picked.get_damage());

            update = Dm.bitor(update, await picked.heal_damage(brute, burn, updating_health: FALSE, forced: forced, required_bodytype: required_bodytype));

            dot = Dm.sub(dot, picked.get_damage()); // return the net amount of damage healed

            brute = round(Dm.num(brute) - (brute_was - picked.brute_dam), DAMAGE_PRECISION);
            burn = round(Dm.num(burn) - (burn_was - picked.burn_dam), DAMAGE_PRECISION);

            Dm.remove(ref parts, picked);
        }

        if (!Dm.truthy(dot)) // no change? no need to update anything
            return dot;

        if (Dm.truthy(updating_health))
            await updatehealth();
        if (Dm.truthy(update))
            await update_damage_overlays();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/damage_procs.dm:280
    public override async DmTask<object> take_overall_damage(float? brute = null, float? burn = null, float? stamina = null, float? updating_health = null, float? forced = null, object required_bodytype = null)
    {
        brute ??= 0;
        burn ??= 0;
        stamina ??= 0;
        updating_health ??= TRUE;
        forced ??= FALSE;
        object dot = null;
        dot = FALSE;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return dot;
        // treat negative args as positive
        brute = abs(Dm.num(brute));
        burn = abs(Dm.num(burn));

        DmList parts = get_damageable_bodyparts(required_bodytype) as DmList;
        object update = NONE;
        while (Dm.truthy(parts.len) && (brute > 0 || burn > 0))
        {
            obj_item_bodypart picked = pick(parts) as obj_item_bodypart;
            float brute_per_part = round(Dm.num(brute) / parts.len, DAMAGE_PRECISION);
            float burn_per_part = round(Dm.num(burn) / parts.len, DAMAGE_PRECISION);

            float brute_was = picked.brute_dam;
            float burn_was = picked.burn_dam;
            dot = Dm.add(dot, picked.get_damage());

            // disabling wounds from these for now cuz your entire body snapping cause your heart stopped would suck
            update = Dm.bitor(update, await picked.receive_damage(brute_per_part, burn_per_part, blocked: FALSE, updating_health: FALSE, forced: forced, required_bodytype: required_bodytype, wound_bonus: CANT_WOUND));

            dot = Dm.sub(dot, picked.get_damage()); // return the net amount of damage healed

            brute = round(Dm.num(brute) - (picked.brute_dam - brute_was), DAMAGE_PRECISION);
            burn = round(Dm.num(burn) - (picked.burn_dam - burn_was), DAMAGE_PRECISION);

            Dm.remove(ref parts, picked);
        }

        if (!Dm.truthy(dot)) // no change? no need to update anything
            return dot;

        if (Dm.truthy(updating_health))
            await updatehealth();
        if (Dm.truthy(update))
            await update_damage_overlays();
        return dot;
    }
}
