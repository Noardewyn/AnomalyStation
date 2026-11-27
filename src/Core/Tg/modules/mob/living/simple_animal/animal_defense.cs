// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/animal_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.

// tg: code/modules/mob/living/simple_animal/animal_defense.dm:1
public partial class mob_living_simple_animal : mob_living
{
    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:1
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // so that martial arts don't double dip
        if (Dm.truthy(await base.attack_hand(user, modifiers, __extra)))
            return TRUE;

        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await ((mob_living)user).disarm(this);
            return TRUE;
        }

        if (!Dm.truthy(((mob_living)user).combat_mode))
        {
            if (stat == DEAD)
                return null;
            visible_message(span_notice(I18n.T("[] [] [].", user, response_help_continuous, this)),
                            span_notice(I18n.T("[] [] you.", user, response_help_continuous)), null, null, user);
            to_chat(user, span_notice(I18n.T("You [] [].", response_help_simple, this)));
            playsound(loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1);
        }
        else
        {
            if (HAS_TRAIT(user, TRAIT_PACIFISM))
            {
                to_chat(user, span_warning(I18n.T("You don't want to hurt []!", this)));
                return null;
            }
            if (Dm.truthy(await check_block(user, harm_intent_damage, I18n.T("[]'s punch", user), UNARMED_ATTACK, 0, BRUTE)))
                return null;
            await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
            visible_message(span_danger(I18n.T("[] [] []!", user, response_harm_continuous, this)),
                            span_userdanger(I18n.T("[] [] you!", user, response_harm_continuous)), null, COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_danger(I18n.T("You [] []!", response_harm_simple, this)));
            playsound(loc, attacked_sound, 25, TRUE, -1);
            await apply_damage(harm_intent_damage);
            log_combat(user, this, "attacked");
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:32
    public override void get_shoving_message(mob_living shover = null, obj_item weapon = null, object shove_flags = null)
    {
        if (weapon != null) // no "gently pushing aside" if you're pressing a shield at them.
        {
            base.get_shoving_message(shover, weapon, shove_flags);
            return;
        }
        bool moved = !Dm.truthy(Dm.bitand(shove_flags, SHOVE_BLOCKED));
        shover.visible_message(
            span_danger(I18n.T("[] [] [][]!", shover.name, response_disarm_continuous, this, moved ? I18n.T(", pushing []", p_them()) : "")),
            span_danger(I18n.T("You [] [][]!", response_disarm_simple, this, moved ? I18n.T(", pushing []", p_them()) : "")),
            span_hear(I18n.T("You hear aggressive shuffling!")),
            COMBAT_MESSAGE_RANGE,
            Dm.list(this)
        );
        to_chat(this, span_userdanger(I18n.T("You're [] by []!", moved ? I18n.T("pushed") : I18n.T("shoved"), shover.name)));
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:45
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        object dot = await base.attack_hulk(user, __extra);
        if (!Dm.truthy(dot))
            return dot;
        playsound(loc, SFX_PUNCH, 25, TRUE, -1);
        visible_message(span_danger(I18n.T("[] punches []!", user, this)),
                        span_userdanger(I18n.T("You're punched by []!", user)), null, COMBAT_MESSAGE_RANGE, user);
        to_chat(user, span_danger(I18n.T("You punch []!", this)));
        await adjust_brute_loss(15);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:55
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await base.attack_paw(user, modifiers))) //successful monkey bite.
        {
            if (stat != DEAD)
                return await apply_damage(rand(1, 3));
        }
        if (!Dm.truthy(((mob_living)user).combat_mode))
        {
            if (health > 0)
            {
                visible_message(span_notice(I18n.T("[] [] [].", user.name, response_help_continuous, this)),
                                span_notice(I18n.T("[] [] you.", user.name, response_help_continuous)), null, COMBAT_MESSAGE_RANGE, user);
                to_chat(user, span_notice(I18n.T("You [] [].", response_help_simple, this)));
                playsound(loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 50, TRUE, -1);
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:67
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await base.attack_alien(user, modifiers))) //if harm or disarm intent.
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            {
                playsound(loc, Dm.resource("sound/items/weapons/pierce.ogg"), 25, TRUE, -1);
                visible_message(span_danger(I18n.T("[] [] []!", user, response_disarm_continuous, name)),
                                span_userdanger(I18n.T("[] [] you!", user, response_disarm_continuous)), null, COMBAT_MESSAGE_RANGE, user);
                to_chat(user, span_danger(I18n.T("You [] []!", response_disarm_simple, name)));
                log_combat(user, this, "disarmed");
            }
            else
            {
                float damage = rand(Dm.num(user.melee_damage_lower), Dm.num(user.melee_damage_upper));
                visible_message(span_danger(I18n.T("[] slashes at []!", user, this)),
                                span_userdanger(I18n.T("You're slashed at by []!", user)), null, COMBAT_MESSAGE_RANGE, user);
                to_chat(user, span_danger(I18n.T("You slash at []!", this)));
                playsound(loc, Dm.resource("sound/items/weapons/slice.ogg"), 25, TRUE, -1);
                await apply_damage(damage);
                log_combat(user, this, "attacked");
            }
            return 1;
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:85
    public override async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_alien_larva L = user as mob_living_carbon_alien_larva;
        object dot = await base.attack_larva(user, modifiers);
        if (Dm.truthy(dot) && stat != DEAD) //successful larva bite
        {
            float damage_done = Dm.num(await apply_damage(rand(Dm.num(L.melee_damage_lower), Dm.num(L.melee_damage_upper)), BRUTE));
            if (damage_done > 0)
                L.amount_grown = min(Dm.num(L.amount_grown) + damage_done, XENOMORPH_MAX_GROWTH);
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:92
    public override async DmTask<object> attack_drone(mob_living_basic_drone user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode)) //No kicking dogs even as a rogue drone. Use a weapon.
            return null;
        return await base.attack_drone(user, modifiers);
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:97
    public override async DmTask<object> attack_drone_secondary(mob_living_basic_drone user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return await base.attack_drone_secondary(user, modifiers);
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:102
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object dot = base.ex_act(severity, target, __extra);
        if (!Dm.truthy(dot) || QDELETED(this))
            return FALSE;
        float bomb_armor = Dm.num(getarmor(null, BOMB));
        float bomb_multi = clamp(1 - bomb_armor / 166.6f, 0, 1); // Scales linearly down to 40% damage at 100 armor
        float bloss = 0;
        if (Dm.eq(severity, EXPLODE_DEVASTATE))
        {
            if (bomb_armor < EXPLODE_GIB_THRESHOLD)
            {
                investigate_log("has been gibbed by an explosion.", INVESTIGATE_DEATHS);
                Waiting.NoSleep(gib(DROP_ALL_REMAINS));
            }
            else
                bloss = 600;
        }
        else if (Dm.eq(severity, EXPLODE_HEAVY))
            bloss = 60;
        else if (Dm.eq(severity, EXPLODE_LIGHT))
            bloss = 30;
        if (Dm.truthy(bloss))
        {
            bloss = round(bloss * bomb_multi, 0.1f);
            Waiting.NoSleep(adjust_brute_loss(bloss));
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:125
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        await adjust_brute_loss(20);
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:129
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        if (!Dm.truthy(no_effect) && !Dm.truthy(visual_effect_icon) && Dm.truthy(melee_damage_upper))
        {
            if (Dm.truthy(attack_vis_effect) && !iswallturf(attacked_atom)) // override the standard visual effect.
                visual_effect_icon = attack_vis_effect;
            else if (Dm.num(melee_damage_upper) < 10)
                visual_effect_icon = ATTACK_EFFECT_PUNCH;
            else
                visual_effect_icon = ATTACK_EFFECT_SMASH;
        }
        await base.do_attack_animation(attacked_atom, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
    }

    // tg: code/modules/mob/living/simple_animal/animal_defense.dm:139
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(mob_biotypes, MOB_ROBOTIC)))
        {
            if (Dm.eq(severity, EMP_LIGHT))
            {
                visible_message(span_danger(I18n.T("[] shakes violently, its parts coming loose!", this)));
                await apply_damage(maxHealth * 0.6f);
                Shake(duration: 1 * SECONDS);
            }
            else if (Dm.eq(severity, EMP_HEAVY))
            {
                visible_message(span_danger(I18n.T("[] suddenly bursts apart!", this)));
                await apply_damage(maxHealth);
            }
        }
        return dot;
    }
}
