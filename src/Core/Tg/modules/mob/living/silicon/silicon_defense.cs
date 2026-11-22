// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/silicon_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/silicon_defense.dm:2
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/silicon_defense.dm:2
    public override async DmTask<object> grippedby(mob_living user = null, float? instant = null)
    {
        instant ??= FALSE;
        return null; //can't upgrade a simple pull into a more aggressive grab.
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:5
    public override object get_ear_protection(float? ignore_deafness = null)
    {
        ignore_deafness ??= FALSE;
        return Dm.add(base.get_ear_protection(ignore_deafness), EAR_PROTECTION_HEAVY); //no ears
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:8
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        object dot = await base.attack_alien(user, modifiers);
        if (!Dm.truthy(dot)) //if harm or disarm intent
            return dot;
        mob_living_carbon_alien_adult adult = user as mob_living_carbon_alien_adult;
        object damage = rand(adult.melee_damage_lower, adult.melee_damage_upper);
        if (prob(90))
        {
            playsound(loc, Dm.resource("sound/items/weapons/slash.ogg"), 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[] slashes at []!", user, this)),
                            span_userdanger(I18n.T("[] slashes at you!", user)), null, null, user);
            to_chat(user, span_danger(I18n.T("You slash at []!", this)));
            if (prob(8))
                await flash_act(affect_silicon: 1);
            await adjust_brute_loss(damage);
            log_combat(user, this, "attacked");
        }
        else
        {
            playsound(loc, Dm.resource("sound/items/weapons/slashmiss.ogg"), 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[]'s swipe misses []!", user, this)),
                            span_danger(I18n.T("You avoid []'s swipe!", user)), null, null, user);
            to_chat(user, span_warning(I18n.T("Your swipe misses []!", this)));
            log_combat(user, this, "attacked and missed");
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:29
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_animal(user, modifiers);
        object damage_received = dot;
        if (prob(Dm.num(damage_received)))
        {
            foreach (mob_living buckled in Dm.each<mob_living>(buckled_mobs))
            {
                await buckled.Paralyze(2 * SECONDS);
                await unbuckle_mob(buckled);
                buckled.visible_message(
                    span_danger(I18n.T("[] is knocked off of [] by []!", buckled, this, user)),
                    span_userdanger(I18n.T("You're knocked off of [] by []!", this, user)),
                    ignored_mobs: user);
                to_chat(user, span_danger(I18n.T("You knock [] off of []!", buckled, this)));
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:43
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:46
    public override async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_alien_larva L = user as mob_living_carbon_alien_larva;
        if (!Dm.truthy(L.combat_mode))
            visible_message(span_notice(I18n.T("[] rubs its head against [].", L.name, this)));
        return null;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:50
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        object dot = await base.attack_hulk(user, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await adjust_brute_loss(rand(10, 15));
        playsound(loc, SFX_PUNCH, 25, TRUE, -1);
        visible_message(span_danger(I18n.T("[] punches []!", user, this)),
                        span_userdanger(I18n.T("[] punches you!", user)), null, COMBAT_MESSAGE_RANGE, user);
        to_chat(user, span_danger(I18n.T("You punch []!", this)));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:60
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await human_user.disarm(this);
            return TRUE;
        }

        if (Dm.truthy(has_buckled_mobs()) && !Dm.truthy(human_user.combat_mode))
        {
            await user_unbuckle_mob(buckled_mobs[1] as mob_living, user);
            return TRUE;
        }
        if (Dm.truthy(human_user.combat_mode))
        {
            await human_user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
            playsound(this.loc, Dm.resource("sound/effects/bang.ogg"), 10, TRUE);
            visible_message(span_danger(I18n.T("[] punches [], but doesn't leave a dent!", user, this)),
                            span_warning(I18n.T("[] punches you, but doesn't leave a dent!", user)), null, COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_danger(I18n.T("You punch [], but don't leave a dent!", this)));
            return TRUE;
        }
        else
        {
            visible_message(span_notice(I18n.T("[] pets [].", user, this)), span_notice(I18n.T("[] pets you.", user)), null, null, user);
            to_chat(user, span_notice(I18n.T("You pet [].", this)));
            if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_PAT_BORG]))
                user._SendSignal(COMSIG_MOB_PAT_BORG, Dm.list(user));
            return TRUE;
        }
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:85
    public override async DmTask<object> check_block(atom hit_by = null, object damage = null, string attack_text = null, float? attack_type = null, float? armour_penetration = null, string damage_type = null, params object[] __extra)
    {
        object attack_flag = Dm.arg(__extra, 0);
        object dot = await base.check_block(hit_by, damage, attack_text, attack_type, armour_penetration, damage_type, __extra);
        if (Dm.eq(dot, SUCCESSFUL_BLOCK))
            return SUCCESSFUL_BLOCK;
        if (damage_type == BRUTE && Dm.eq(attack_type, UNARMED_ATTACK) && Dm.eq(attack_flag, MELEE) && Dm.le(damage, 10))
        {
            playsound(this, Dm.resource("sound/effects/bang.ogg"), 10, TRUE);
            visible_message(span_danger(I18n.T("[] doesn't leave a dent on []!", attack_text, this)), vision_distance: COMBAT_MESSAGE_RANGE);
            return SUCCESSFUL_BLOCK;
        }
        return FAILED_BLOCK;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:95
    public override async DmTask<object> attack_drone(mob_living_basic_drone user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        return await base.attack_drone(user, modifiers);
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:100
    public override async DmTask<object> attack_drone_secondary(mob_living_basic_drone user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return await base.attack_drone_secondary(user, modifiers);
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:105
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        to_chat(this, span_danger(I18n.T("Warning: Electromagnetic pulse detected.")));
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)) || QDELETED(this))
            return dot;
        object sw__1 = severity;
        if (Dm.eq(sw__1, 1))
            await this.take_bodypart_damage(burn: 20);
        else if (Dm.eq(sw__1, 2))
            await this.take_bodypart_damage(burn: 10);
        to_chat(this, span_userdanger(I18n.T("*BZZZT*")));
        foreach (mob_living M in Dm.each<mob_living>(buckled_mobs))
        {
            if (prob(Dm.num(Dm.mul(severity, 50))))
            {
                await unbuckle_mob(M);
                await M.Paralyze(40);
                M.visible_message(span_boldwarning(I18n.T("[] is thrown off of []!", M, this)));
            }
        }
        await flash_act(affect_silicon: 1);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:123
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        piercing_hit ??= FALSE;
        if (hitting_projectile.damage_type == BURN)
            hitting_projectile.damage_type = BRUTE; //Burn is for wire damage. Brute is the outer chassis.
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;

        object prob_of_knocking_dudes_off = 0;
        if (hitting_projectile.damage_type == BRUTE)
            prob_of_knocking_dudes_off = hitting_projectile.damage * 1.5f;
        if (Dm.truthy(hitting_projectile.stun) || Dm.truthy(hitting_projectile.knockdown) || Dm.truthy(hitting_projectile.paralyze))
            prob_of_knocking_dudes_off = 100;

        if (prob(Dm.num(prob_of_knocking_dudes_off)))
        {
            foreach (mob_living buckled in Dm.each<mob_living>(buckled_mobs))
            {
                buckled.visible_message(span_boldwarning(I18n.T("[] is knocked off of [] by []!", buckled, this, hitting_projectile)));
                await unbuckle_mob(buckled);
                await buckled.Paralyze(4 * SECONDS);
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:142
    public override async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        intensity ??= 1;
        override_blindness_check ??= 0;
        affect_silicon ??= 0;
        visual ??= 0;
        type ??= typeof(atom_movable_screen_fullscreen_flash_static);
        length ??= 25;
        if (Dm.truthy(affect_silicon))
            return await base.flash_act(intensity, override_blindness_check, affect_silicon, visual, type, length);
        return null;
    }

    /// If an item does this or more throwing damage it will slow a borg down on hit
    // tg: code/modules/mob/living/silicon/silicon_defense.dm:147  #define CYBORG_SLOWDOWN_THRESHOLD (#undef at :161)
    private const float CYBORG_SLOWDOWN_THRESHOLD = 10;

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:149
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        atom_movable AM = hitting_atom;
        object dot = await base.hitby(AM, skipcatch, hitpush, blocked, throwingdatum);
        if (Dm.truthy(dot) || Dm.lt(AM.throwforce, CYBORG_SLOWDOWN_THRESHOLD)) // can cyborgs even catch things?
            return dot;
        await apply_status_effect(typeof(datum_status_effect_borg_slow), Dm.div(AM.throwforce, 20));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:155
    public override async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        object dot = await base.attack_effects(damage_done, hit_zone, armor_block, attacking_item, attacker);
        if (Dm.lt(damage_done, CYBORG_SLOWDOWN_THRESHOLD))
            return dot;
        await apply_status_effect(typeof(datum_status_effect_borg_slow), Dm.div(damage_done, 60));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon_defense.dm:163
    public override object hypnosis_vulnerable()
    {
        return FALSE; //It obeys its laws
    }
}
