// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/damage_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/damage_procs.dm:23
public partial class mob_living : mob
{
    /**
     * Applies damage to this mob.
     *
     * Sends [COMSIG_MOB_APPLY_DAMAGE]
     *
     * Arguuments:
     * * damage - Amount of damage
     * * damagetype - What type of damage to do. one of [BRUTE], [BURN], [TOX], [OXY], [STAMINA], [BRAIN].
     * * def_zone - What body zone is being hit. Or a reference to what bodypart is being hit.
     * * blocked - Percent modifier to damage. 100 = 100% less damage dealt, 50% = 50% less damage dealt.
     * * forced - "Force" exactly the damage dealt. This means it skips damage modifier from blocked.
     * * spread_damage - For carbons, spreads the damage across all bodyparts rather than just the targeted zone.
     * * wound_bonus - Bonus modifier for wound chance.
     * * exposed_wound_bonus - Bonus modifier for wound chance on bare skin.
     * * sharpness - Sharpness of the weapon.
     * * attack_direction - Direction of the attack from the attacker to [src].
     * * attacking_item - Item that is attacking [src].
     * * wound_clothing - If this should cause damage to clothing.
     *
     * Returns the amount of damage dealt.
     */
    // tg: code/modules/mob/living/damage_procs.dm:23
    [ShouldCallParent]
    public virtual async DmTask<object> apply_damage(float? damage = null, string damagetype = null, object def_zone = null, float? blocked = null, float? forced = null, float? spread_damage = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null, object attack_direction = null, object attacking_item = null, float? wound_clothing = null)
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

        object damage_amount = damage;
        if (!Dm.truthy(forced))
        {
            blocked = Dm.num(blocked) + damage_resistance;
            damage_amount = Dm.mul(damage_amount, ((100 - Dm.num(blocked)) / 100));
            damage_amount = Dm.mul(damage_amount, get_incoming_damage_modifier(Dm.num(damage_amount), damagetype, def_zone, sharpness, attack_direction, attacking_item));
        }
        if (Dm.le(damage_amount, 0))
            return 0;

        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_APPLY_DAMAGE]))
            _SendSignal(COMSIG_MOB_APPLY_DAMAGE, Dm.list(this, damage_amount, damagetype, def_zone, blocked, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing));

        object damage_dealt = 0;
        switch (damagetype)
        {
            case BRUTE:
                if (isbodypart(def_zone))
                {
                    obj_item_bodypart actual_hit = def_zone as obj_item_bodypart;
                    object delta = actual_hit.get_damage();
                    if (Dm.truthy(await actual_hit.receive_damage(
                        brute: Dm.num(damage_amount),
                        burn: 0,
                        forced: forced,
                        wound_bonus: wound_bonus,
                        exposed_wound_bonus: exposed_wound_bonus,
                        sharpness: sharpness,
                        attack_direction: attack_direction,
                        damage_source: attacking_item,
                        wound_clothing: wound_clothing
                    )))
                        await update_damage_overlays();
                    damage_dealt = Dm.sub(actual_hit.get_damage(), delta); // Unfortunately bodypart receive_damage doesn't return damage dealt so we do it manually
                }
                else
                    damage_dealt = Dm.mul(-1, await adjust_brute_loss(damage_amount, forced: forced));
                break;
            case BURN:
                if (isbodypart(def_zone))
                {
                    obj_item_bodypart actual_hit = def_zone as obj_item_bodypart;
                    object delta = actual_hit.get_damage();
                    if (Dm.truthy(await actual_hit.receive_damage(
                        brute: 0,
                        burn: Dm.num(damage_amount),
                        forced: forced,
                        wound_bonus: wound_bonus,
                        exposed_wound_bonus: exposed_wound_bonus,
                        sharpness: sharpness,
                        attack_direction: attack_direction,
                        damage_source: attacking_item,
                        wound_clothing: wound_clothing
                    )))
                        await update_damage_overlays();
                    damage_dealt = Dm.sub(actual_hit.get_damage(), delta); // See above
                }
                else
                    damage_dealt = Dm.mul(-1, await adjust_fire_loss(damage_amount, forced: forced));
                break;
            case TOX:
                damage_dealt = Dm.mul(-1, await adjust_tox_loss(damage_amount, forced: forced));
                break;
            case OXY:
                damage_dealt = Dm.mul(-1, await adjust_oxy_loss(damage_amount, forced: forced));
                break;
            case STAMINA:
                damage_dealt = Dm.mul(-1, await adjust_stamina_loss(damage_amount, forced: forced));
                break;
            case BRAIN:
                damage_dealt = Dm.mul(-1, await adjust_organ_loss(ORGAN_SLOT_BRAIN, damage_amount));
                break;
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_AFTER_APPLY_DAMAGE]))
            _SendSignal(COMSIG_MOB_AFTER_APPLY_DAMAGE, Dm.list(this, damage_dealt, damagetype, def_zone, blocked, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing));
        return damage_dealt;
    }

    /**
     * Used in tandem with [/mob/living/proc/apply_damage] to calculate modifier applied into incoming damage
     */
    // tg: code/modules/mob/living/damage_procs.dm:104
    [ShouldCallParent, ShouldBePure]
    public virtual object get_incoming_damage_modifier(float? damage = null, string damagetype = null, object def_zone = null, float? sharpness = null, object attack_direction = null, object attacking_item = null)
    {
        damage ??= 0;
        damagetype ??= BRUTE;
        sharpness ??= NONE;

        DmList damage_mods = Dm.list();
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_APPLY_DAMAGE_MODIFIERS]))
            _SendSignal(COMSIG_MOB_APPLY_DAMAGE_MODIFIERS, Dm.list(this, damage_mods, damage, damagetype, def_zone, sharpness, attack_direction, attacking_item));

        object final_mod = 1;
        foreach (object new_mod in Dm.each(damage_mods))
            final_mod = Dm.mul(final_mod, new_mod);

        return final_mod;
    }

    /**
     * Simply a wrapper for calling mob adjustXLoss() procs to heal a certain damage type,
     * when you don't know what damage type you're healing exactly.
     */
    // tg: code/modules/mob/living/damage_procs.dm:128
    public virtual async DmTask<object> heal_damage_type(float? heal_amount = null, string damagetype = null, float? update_health = null)
    {
        heal_amount ??= 0;
        damagetype ??= BRUTE;
        update_health ??= TRUE;
        heal_amount = abs(Dm.num(heal_amount)) * -1;

        switch (damagetype)
        {
            case BRUTE:
                return await adjust_brute_loss(heal_amount, update_health);
            case BURN:
                return await adjust_fire_loss(heal_amount, update_health);
            case TOX:
                return await adjust_tox_loss(heal_amount, update_health);
            case OXY:
                return await adjust_oxy_loss(heal_amount, update_health);
            case STAMINA:
                return await adjust_stamina_loss(heal_amount, update_health);
        }
        return null;
    }

    /// return the damage amount for the type given
    /**
     * Simply a wrapper for calling mob getXLoss() procs to get a certain damage type,
     * when you don't know what damage type you're getting exactly.
     */
    // tg: code/modules/mob/living/damage_procs.dm:148
    public virtual object get_current_damage_of_type(string damagetype = null)
    {
        damagetype ??= BRUTE;
        switch (damagetype)
        {
            case BRUTE:
                return get_brute_loss();
            case BURN:
                return get_fire_loss();
            case TOX:
                return get_tox_loss();
            case OXY:
                return get_oxy_loss();
            case STAMINA:
                return get_stamina_loss();
        }
        return null;
    }

    /// return the total damage of all types which update your health
    // tg: code/modules/mob/living/damage_procs.dm:162
    public virtual object get_total_damage(float? precision = null)
    {
        precision ??= DAMAGE_PRECISION;
        return round(Dm.num(Dm.add(Dm.add(Dm.add(get_brute_loss(), get_fire_loss()), get_tox_loss()), get_oxy_loss())), Dm.num(precision));
    }

    /// Applies multiple damages at once via [apply_damage][/mob/living/proc/apply_damage]
    // tg: code/modules/mob/living/damage_procs.dm:166
    public virtual async DmTask<object> apply_damages(float? brute = null, float? burn = null, float? tox = null, float? oxy = null, object def_zone = null, float? blocked = null, float? stamina = null, float? brain = null)
    {
        brute ??= 0;
        burn ??= 0;
        tox ??= 0;
        oxy ??= 0;
        blocked ??= 0;
        stamina ??= 0;
        brain ??= 0;
        object total_damage = 0;
        if (Dm.truthy(brute))
            total_damage = Dm.add(total_damage, await apply_damage(brute, BRUTE, def_zone, blocked));
        if (Dm.truthy(burn))
            total_damage = Dm.add(total_damage, await apply_damage(burn, BURN, def_zone, blocked));
        if (Dm.truthy(tox))
            total_damage = Dm.add(total_damage, await apply_damage(tox, TOX, def_zone, blocked));
        if (Dm.truthy(oxy))
            total_damage = Dm.add(total_damage, await apply_damage(oxy, OXY, def_zone, blocked));
        if (Dm.truthy(stamina))
            total_damage = Dm.add(total_damage, await apply_damage(stamina, STAMINA, def_zone, blocked));
        if (Dm.truthy(brain))
            total_damage = Dm.add(total_damage, await apply_damage(brain, BRAIN, def_zone, blocked));
        return total_damage;
    }

    /// applies various common status effects or common hardcoded mob effects
    // tg: code/modules/mob/living/damage_procs.dm:192
    public virtual async DmTask<object> apply_effect(float? effect = null, string effecttype = null, float? blocked = null)
    {
        effect ??= 0;
        effecttype ??= EFFECT_STUN;
        blocked ??= 0;
        float hit_percent = (100 - Dm.num(blocked)) / 100;
        if (!Dm.truthy(effect) || (hit_percent <= 0))
            return FALSE;
        switch (effecttype)
        {
            case EFFECT_STUN:
                await Stun(Dm.num(effect) * hit_percent);
                break;
            case EFFECT_KNOCKDOWN:
                await Knockdown(Dm.num(effect) * hit_percent);
                break;
            case EFFECT_PARALYZE:
                await Paralyze(Dm.num(effect) * hit_percent);
                break;
            case EFFECT_IMMOBILIZE:
                await Immobilize(Dm.num(effect) * hit_percent);
                break;
            case EFFECT_UNCONSCIOUS:
                await Unconscious(Dm.num(effect) * hit_percent);
                break;
        }

        return TRUE;
    }

    /**
     * Applies multiple effects at once via [/mob/living/proc/apply_effect]
     *
     * Pretty much only used for projectiles applying effects on hit,
     * don't use this for anything else please just cause the effects directly
     */
    // tg: code/modules/mob/living/damage_procs.dm:216
    public virtual async DmTask<object> apply_effects(float? stun = null, float? knockdown = null, float? unconscious = null, float? slur = null, float? stutter = null, float? eyeblur = null, float? drowsy = null, float? blocked = null, float? stamina = null, float? jitter = null, float? paralyze = null, float? immobilize = null)
    {
        stun ??= 0;
        knockdown ??= 0;
        unconscious ??= 0;
        slur ??= 0 * SECONDS; // Speech impediment, not technically an effect
        stutter ??= 0 * SECONDS; // Ditto
        eyeblur ??= 0 * SECONDS;
        drowsy ??= 0 * SECONDS;
        blocked ??= 0; // This one's not an effect, don't be confused - it's block chance
        stamina ??= 0; // This one's a damage type, and not an effect
        jitter ??= 0 * SECONDS;
        paralyze ??= 0;
        immobilize ??= 0;

        if (Dm.num(blocked) >= 100)
            return FALSE;

        if (Dm.truthy(stun))
            await apply_effect(stun, EFFECT_STUN, blocked);
        if (Dm.truthy(knockdown))
            await apply_effect(knockdown, EFFECT_KNOCKDOWN, blocked);
        if (Dm.truthy(unconscious))
            await apply_effect(unconscious, EFFECT_UNCONSCIOUS, blocked);
        if (Dm.truthy(paralyze))
            await apply_effect(paralyze, EFFECT_PARALYZE, blocked);
        if (Dm.truthy(immobilize))
            await apply_effect(immobilize, EFFECT_IMMOBILIZE, blocked);

        if (Dm.truthy(stamina))
            await apply_damage(stamina, STAMINA, null, blocked);

        // The M7 projectiles slice: the debuffs of a shot (a beam's eye blur)
        if (Dm.truthy(drowsy))
            await adjust_drowsiness(drowsy);
        if (Dm.truthy(eyeblur))
            await adjust_eye_blur_up_to(eyeblur, eyeblur);
        if (Dm.truthy(jitter) && !Dm.truthy(check_stun_immunity(CANSTUN)))
            await adjust_jitter(jitter);
        if (Dm.truthy(slur))
            await adjust_slurring(slur);
        if (Dm.truthy(stutter))
            await adjust_stutter(stutter);

        return TRUE;
    }

    // tg: code/modules/mob/living/damage_procs.dm:261
    public virtual object get_brute_loss()
    {
        return bruteloss;
    }

    // tg: code/modules/mob/living/damage_procs.dm:264
    public virtual object can_adjust_brute_loss(object amount = null, object forced = null, object required_bodytype = null)
    {
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_ADJUST_BRUTE_DAMAGE]) ? NONE : _SendSignal(COMSIG_LIVING_ADJUST_BRUTE_DAMAGE, Dm.list(this, BRUTE, amount, forced))), COMPONENT_IGNORE_CHANGE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/damage_procs.dm:271
    public virtual async DmTask<object> adjust_brute_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        if (Dm.gt(amount, 0) && !Dm.truthy(forced)) //carbon mobs override this proc, so the damage modifier check is also performed on [limb/receive_damage()]
            amount = Dm.mul(amount, GET_PHYSIOLOGY(this, BRUTE));

        if (!Dm.truthy(amount) || !Dm.truthy(can_adjust_brute_loss(amount, forced, required_bodytype)))
            return 0;
        float difference = bruteloss;
        bruteloss = Dm.num(clamp((Dm.add(bruteloss, Dm.mul(amount, config.Get(typeof(datum_config_entry_number_damage_multiplier))))), 0, maxHealth * 2));
        difference -= bruteloss;

        return await on_damage_loss(amount, updating_health, forced, BRUTE, difference);
    }


    // tg: code/modules/mob/living/damage_procs.dm:284
    public virtual async DmTask<object> set_brute_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return 0;
        float difference = bruteloss;
        bruteloss = Dm.num(amount);
        difference -= bruteloss;

        return await on_damage_loss(-difference, updating_health, forced, BRUTE, difference);
    }

    /**
     * This proc resets any burn, tox and oxy change that the mob has received and adds the amount/difference to bruteloss.
     * Used by simple and basic mobs. For the sake of simplicity, all damage they receive (beside stamina) is funneled into a single damage type. It makes things easier for other
     * coders I guess.
     */
    // tg: code/modules/mob/living/damage_procs.dm:298
    [DmProtected]
    public virtual object simple_transfer_to_brute_loss(object amount = null)
    {
        //Again, this is workaround to the lack of support that simple/basic mobs have for multiple damage types in the code, do not use it for other things.
        fireloss = 0;
        toxloss = 0;
        oxyloss = 0;

        float difference = bruteloss;
        bruteloss = Dm.num(clamp(Dm.add(bruteloss, amount), 0, maxHealth * 2));
        difference -= bruteloss;

        return difference;
    }

    // tg: code/modules/mob/living/damage_procs.dm:310
    public virtual object get_oxy_loss()
    {
        return oxyloss;
    }

    // tg: code/modules/mob/living/damage_procs.dm:313
    public virtual object can_adjust_oxy_loss(object amount = null, object forced = null, object required_biotype = null)
    {
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_ADJUST_OXY_DAMAGE]) ? NONE : _SendSignal(COMSIG_LIVING_ADJUST_OXY_DAMAGE, Dm.list(this, OXY, amount, forced))), COMPONENT_IGNORE_CHANGE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/damage_procs.dm:320
    public virtual async DmTask<object> adjust_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        if (Dm.gt(amount, 0) && !Dm.truthy(forced))
            amount = Dm.mul(amount, GET_PHYSIOLOGY(this, OXY));

        if (!Dm.truthy(amount) || !Dm.truthy(can_adjust_oxy_loss(amount, forced, required_biotype)))
            return 0;

        float difference = oxyloss;
        oxyloss = Dm.num(clamp((Dm.add(oxyloss, Dm.mul(amount, config.Get(typeof(datum_config_entry_number_damage_multiplier))))), 0, maxHealth * 2));
        difference -= oxyloss;

        return await on_damage_loss(amount, updating_health, forced, OXY, difference);
    }

    // tg: code/modules/mob/living/damage_procs.dm:333
    public virtual async DmTask<object> set_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        if (!Dm.truthy(forced))
        {
            if (HAS_TRAIT(this, TRAIT_GODMODE))
                return 0;
        }

        float difference = oxyloss;
        oxyloss = Dm.num(amount);
        difference -= oxyloss;

        return await on_damage_loss(-difference, updating_health, forced, OXY, difference);
    }

    // tg: code/modules/mob/living/damage_procs.dm:344
    public virtual object get_tox_loss()
    {
        return toxloss;
    }

    // tg: code/modules/mob/living/damage_procs.dm:347
    public virtual object can_adjust_tox_loss(object amount = null, object forced = null, float? required_biotype = null)
    {
        required_biotype ??= ALL;
        if (!Dm.truthy(forced) && (HAS_TRAIT(this, TRAIT_GODMODE) || !Dm.truthy(Dm.bitand(mob_biotypes, required_biotype))))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_ADJUST_TOX_DAMAGE]) ? NONE : _SendSignal(COMSIG_LIVING_ADJUST_TOX_DAMAGE, Dm.list(this, TOX, amount, forced))), COMPONENT_IGNORE_CHANGE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/damage_procs.dm:354
    public virtual async DmTask<object> adjust_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        if (Dm.gt(amount, 0) && !Dm.truthy(forced))
            amount = Dm.mul(amount, GET_PHYSIOLOGY(this, TOX));

        if (!Dm.truthy(amount) || !Dm.truthy(can_adjust_tox_loss(amount, forced, required_biotype)))
            return 0;

        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_TOXINLOVER)) //damage becomes healing and healing becomes damage
        {
            amount = Dm.neg(amount);
            if (HAS_TRAIT(this, TRAIT_TOXIMMUNE)) //Prevents toxin damage, but not healing
                amount = min(amount, 0);
            if (Dm.gt(amount, 0))
                adjust_blood_volume(Dm.mul(-5, amount));
            else
                adjust_blood_volume(Dm.neg(amount));
        }

        else if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_TOXIMMUNE)) //Prevents toxin damage, but not healing
            amount = min(amount, 0);

        float difference = toxloss;
        toxloss = Dm.num(clamp((Dm.add(toxloss, Dm.mul(amount, config.Get(typeof(datum_config_entry_number_damage_multiplier))))), 0, maxHealth * 2));
        difference -= toxloss;

        return await on_damage_loss(amount, updating_health, forced, TOX, difference);
    }

    // tg: code/modules/mob/living/damage_procs.dm:379
    public virtual async DmTask<object> set_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return 0;
        if (!Dm.truthy(forced) && !Dm.truthy(Dm.bitand(mob_biotypes, required_biotype)))
            return 0;
        float difference = toxloss;
        toxloss = Dm.num(amount);
        difference -= toxloss;

        return await on_damage_loss(-difference, updating_health, forced, TOX, difference);
    }

    // tg: code/modules/mob/living/damage_procs.dm:390
    public virtual object get_fire_loss()
    {
        return fireloss;
    }

    // tg: code/modules/mob/living/damage_procs.dm:393
    public virtual object can_adjust_fire_loss(object amount = null, object forced = null, object required_bodytype = null)
    {
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_ADJUST_BURN_DAMAGE]) ? NONE : _SendSignal(COMSIG_LIVING_ADJUST_BURN_DAMAGE, Dm.list(this, BURN, amount, forced))), COMPONENT_IGNORE_CHANGE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/damage_procs.dm:400
    public virtual async DmTask<object> adjust_fire_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        if (Dm.gt(amount, 0) && !Dm.truthy(forced)) //carbon mobs override this proc, so the damage modifier check is also performed on [limb/receive_damage()]
            amount = Dm.mul(amount, GET_PHYSIOLOGY(this, BURN));

        if (!Dm.truthy(amount) || !Dm.truthy(can_adjust_fire_loss(amount, forced, required_bodytype)))
            return 0;
        float difference = fireloss;
        fireloss = Dm.num(clamp((Dm.add(fireloss, Dm.mul(amount, config.Get(typeof(datum_config_entry_number_damage_multiplier))))), 0, maxHealth * 2));
        difference -= fireloss;

        return await on_damage_loss(amount, updating_health, forced, BURN, difference);
    }

    // tg: code/modules/mob/living/damage_procs.dm:412
    public virtual async DmTask<object> set_fire_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;
        required_bodytype ??= ALL;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return 0;
        float difference = fireloss;
        fireloss = Dm.num(amount);
        difference -= fireloss;

        return await on_damage_loss(-difference, updating_health, forced, BURN, difference);
    }

    // tg: code/modules/mob/living/damage_procs.dm:421
    public virtual async DmTask<object> adjust_organ_loss(object slot = null, object amount = null, object maximum = null, object required_organ_flag = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/damage_procs.dm:424
    public virtual async DmTask<object> set_organ_loss(object slot = null, object amount = null, object maximum = null, object required_organ_flag = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/damage_procs.dm:427
    public virtual object get_organ_loss(object slot = null, object required_organ_flag = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/damage_procs.dm:430
    public virtual object get_stamina_loss()
    {
        return staminaloss;
    }

    // tg: code/modules/mob/living/damage_procs.dm:433
    public virtual object can_adjust_stamina_loss(object amount = null, object forced = null, float? required_biotype = null)
    {
        required_biotype ??= ALL;
        if (!Dm.truthy(forced) && (!Dm.truthy(Dm.bitand(mob_biotypes, required_biotype)) || HAS_TRAIT(this, TRAIT_GODMODE)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_ADJUST_STAMINA_DAMAGE]) ? NONE : _SendSignal(COMSIG_LIVING_ADJUST_STAMINA_DAMAGE, Dm.list(this, STAMINA, amount, forced))), COMPONENT_IGNORE_CHANGE)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/damage_procs.dm:440
    public virtual async DmTask<object> adjust_stamina_loss(object amount = null, float? updating_stamina = null, float? forced = null, float? required_biotype = null)
    {
        updating_stamina ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        if (Dm.gt(amount, 0) && !Dm.truthy(forced))
            amount = Dm.mul(amount, GET_PHYSIOLOGY(this, STAMINA));

        if (!Dm.truthy(amount) || !Dm.truthy(can_adjust_stamina_loss(amount, forced, required_biotype)))
            return 0;

        float old_amount = staminaloss;
        staminaloss = Dm.num(clamp((Dm.add(staminaloss, Dm.mul(amount, config.Get(typeof(datum_config_entry_number_damage_multiplier))))), 0, max_stamina));
        float delta = old_amount - staminaloss;
        return await on_damage_loss(amount, updating_stamina, forced, STAMINA, delta);
    }

    // tg: code/modules/mob/living/damage_procs.dm:452
    public virtual async DmTask<object> set_stamina_loss(object amount = null, float? updating_stamina = null, float? forced = null, float? required_biotype = null)
    {
        updating_stamina ??= TRUE;
        forced ??= FALSE;
        required_biotype ??= ALL;
        if (!Dm.truthy(forced) && HAS_TRAIT(this, TRAIT_GODMODE))
            return 0;
        if (!Dm.truthy(forced) && !Dm.truthy(Dm.bitand(mob_biotypes, required_biotype)))
            return 0;
        float old_amount = staminaloss;
        staminaloss = Dm.num(amount);
        float delta = old_amount - staminaloss;
        return await on_damage_loss(-delta, updating_stamina, forced, STAMINA, delta);
    }

    /**
     * Called at the end of each set/adjust_x_loss proc, regardless of whether damage was actually inflicted/healed or not.
     *
     * params:
     * * amount: the amount of damage taken/recovered after value modifiers BUT BEFORE clamping the value between [maxHealth]/[max_stamina] * 2 and 0
     * * updating_health: if the caller proc is to be followed by an [updatehealth()] proc call
     * * forced: if the caller proc has had the forced arg set to TRUE (ignores damage modifiers
     * * damage_type: one of BRUTE, BURN, OXY, TOX or STAMINA
     * * difference: the difference between the old and new (brute/fire/oxy/tox/stamina)loss var values. Unlike amount, negative values is damage received, positive is healing.
     */
    // tg: code/modules/mob/living/damage_procs.dm:472
    [ShouldCallParent]
    public virtual async DmTask<object> on_damage_loss(object amount = null, object updating_health = null, object forced = null, object damage_type = null, object difference = null)
    {
        if (Dm.truthy(difference)) // only call on_damage_loss_changed if there's an actual change
        {
            //We pass down 'difference' and not 'amount' as that's the actual change, with its sign swapped to match
            //how positive values represent damage and negative represent healing in adjust_x_loss() calls once again (less confusing this way).
            await on_damage_loss_changed(Dm.neg(difference), updating_health, forced, damage_type);
        }
        return difference; //I'm pretty sure only mob_damage unit tests need this return value, but who knows...
    }

    ///Called by [on_damage_loss()], if the damage was actually inflicted or healed (difference different than 0)
    // tg: code/modules/mob/living/damage_procs.dm:481
    [ShouldCallParent]
    public virtual async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        if (Dm.eq(damage_type, STAMINA) && Dm.gt(amount, 0))
            timed_stamina_reset();
        if (Dm.truthy(updating_health))
            await updatehealth();
    }

    /// Reset the stamina damage back to 0 after an amount of time (dictated by the [stamina_regen_time] var). However the timer is reset everytime the mob takes stamina damage.
    // tg: code/modules/mob/living/damage_procs.dm:489
    public virtual void timed_stamina_reset()
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_stamina_loss), 0, TRUE, TRUE), stamina_regen_time, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/mob/living/damage_procs.dm", line: 490);
    }

    /**
     * heal ONE external organ, organ gets randomly selected from damaged ones.
     *
     * returns the net change in damage
     */
    // tg: code/modules/mob/living/damage_procs.dm:497
    public virtual async DmTask<object> heal_bodypart_damage(float? brute = null, float? burn = null, float? updating_health = null, float? required_bodytype = null, object target_zone = null)
    {
        brute ??= 0;
        burn ??= 0;
        updating_health ??= TRUE;
        required_bodytype ??= NONE;
        object dot = null;
        dot = (Dm.add(await adjust_brute_loss(-abs(Dm.num(brute)), updating_health: FALSE), await adjust_fire_loss(-abs(Dm.num(burn)), updating_health: FALSE)));
        if (!Dm.truthy(dot)) // no change, no need to update
            return FALSE;
        if (Dm.truthy(updating_health))
            await updatehealth();
        return dot;
    }

    /// damage ONE external organ, organ gets randomly selected from damaged ones.
    // tg: code/modules/mob/living/damage_procs.dm:505
    public virtual async DmTask<object> take_bodypart_damage(float? brute = null, float? burn = null, float? updating_health = null, object required_bodytype = null, float? check_armor = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null)
    {
        brute ??= 0;
        burn ??= 0;
        updating_health ??= TRUE;
        check_armor ??= FALSE;
        wound_bonus ??= 0;
        exposed_wound_bonus ??= 0;
        sharpness ??= NONE;
        object dot = null;
        dot = (Dm.add(await adjust_brute_loss(abs(Dm.num(brute)), updating_health: FALSE), await adjust_fire_loss(abs(Dm.num(burn)), updating_health: FALSE)));
        if (!Dm.truthy(dot)) // no change, no need to update
            return FALSE;
        if (Dm.truthy(updating_health))
            await updatehealth();
        return dot;
    }

    /// heal MANY bodyparts, in random order. note: stamina arg nonfunctional for carbon mobs
    // tg: code/modules/mob/living/damage_procs.dm:513
    public virtual async DmTask<object> heal_overall_damage(float? brute = null, float? burn = null, float? stamina = null, object required_bodytype = null, float? updating_health = null, float? forced = null)
    {
        brute ??= 0;
        burn ??= 0;
        stamina ??= 0;
        updating_health ??= TRUE;
        forced ??= FALSE;
        object dot = null;
        dot = (Dm.add(Dm.add(await adjust_brute_loss(-abs(Dm.num(brute)), updating_health: FALSE, forced: forced),
                await adjust_fire_loss(-abs(Dm.num(burn)), updating_health: FALSE, forced: forced)),
                await adjust_stamina_loss(-abs(Dm.num(stamina)), updating_stamina: FALSE, forced: forced)));
        if (!Dm.truthy(dot)) // no change, no need to update
            return FALSE;
        if (Dm.truthy(updating_health))
            await updatehealth();
        return dot;
    }

    /// damage MANY bodyparts, in random order. note: stamina arg nonfunctional for carbon mobs
    // tg: code/modules/mob/living/damage_procs.dm:523
    public virtual async DmTask<object> take_overall_damage(float? brute = null, float? burn = null, float? stamina = null, float? updating_health = null, float? forced = null, object required_bodytype = null)
    {
        brute ??= 0;
        burn ??= 0;
        stamina ??= 0;
        updating_health ??= TRUE;
        forced ??= FALSE;
        object dot = null;
        dot = (Dm.add(Dm.add(await adjust_brute_loss(abs(Dm.num(brute)), updating_health: FALSE, forced: forced),
                await adjust_fire_loss(abs(Dm.num(burn)), updating_health: FALSE, forced: forced)),
                await adjust_stamina_loss(abs(Dm.num(stamina)), updating_stamina: FALSE, forced: forced)));
        if (!Dm.truthy(dot)) // no change, no need to update
            return FALSE;
        if (Dm.truthy(updating_health))
            await updatehealth();
        return dot;
    }

    ///heal up to amount damage, in a given order
    // tg: code/modules/mob/living/damage_procs.dm:533
    public virtual async DmTask<object> heal_ordered_damage(object amount = null, DmList damage_types = null, float? update_health = null)
    {
        update_health ??= TRUE;
        object dot = null;
        dot = 0; //we'll return the amount of damage healed
        foreach (object damagetype in Dm.each(damage_types))
        {
            object amount_to_heal = min(abs(Dm.num(amount)), get_current_damage_of_type((string)damagetype)); //heal only up to the amount of damage we have
            if (Dm.truthy(amount_to_heal))
            {
                dot = Dm.add(dot, await heal_damage_type(Dm.num(amount_to_heal), (string)damagetype, FALSE));
                amount = Dm.sub(amount, amount_to_heal); //remove what we healed from our current amount
            }
            if (!Dm.truthy(amount))
                break;
        }
        if (Dm.truthy(dot) && Dm.truthy(update_health))
            await updatehealth();
        return dot;
    }
}
