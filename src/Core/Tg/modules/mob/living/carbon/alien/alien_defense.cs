// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/alien_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file (acid_act came with the acid, silicons SI1c).

// tg: code/modules/mob/living/carbon/alien/alien_defense.dm:2
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:2
    public override object get_eye_protection()
    {
        return Dm.num(base.get_eye_protection()) + FLASH_PROTECTION_WELDER; //potential cyber implants + natural eye protection
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:5
    public override object get_ear_protection(float? ignore_deafness = null)
    {
        ignore_deafness ??= FALSE;
        return Dm.num(base.get_ear_protection(ignore_deafness)) + EAR_PROTECTION_HEAVY; //no ears
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:8
    [DmParams("AM", "skipcatch", "hitpush", "blocked", "throwingdatum")]
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        atom_movable AM = hitting_atom;
        await base.hitby(AM, skipcatch: TRUE, hitpush: FALSE);
        return null;
    }

    /*Code for aliens attacking aliens. Because aliens act on a hivemind, I don't see them as very aggressive with each other.
    As such, they can either help or harm other aliens. Help works like the human help command while harm is a simple nibble.
    In all, this is a lot like the monkey code. /N
    */
    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:16
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(user.combat_mode))
        {
            if (user == this && Dm.truthy(check_self_for_injuries()))
                return null;
            await set_resting(FALSE);
            await AdjustStun(-6 * SECONDS);
            await AdjustKnockdown(-6 * SECONDS);
            await AdjustImmobilized(-6 * SECONDS);
            await AdjustParalyzed(-6 * SECONDS);
            await AdjustUnconscious(-6 * SECONDS);
            await AdjustSleeping(-10 * SECONDS);
            visible_message(span_notice(I18n.T("[] nuzzles [] trying to wake [] up!", user.name, this, p_them())));
        }
        else if (health > 0)
        {
            await user.do_attack_animation(this, ATTACK_EFFECT_BITE);
            playsound(loc, Dm.resource("sound/items/weapons/bite.ogg"), 50, TRUE, -1);
            visible_message(span_danger(I18n.T("[] bites []!", user.name, this)),
                            span_userdanger(I18n.T("[] bites you!", user.name)), span_hear(I18n.T("You hear a chomp!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_danger(I18n.T("You bite []!", this)));
            await adjust_brute_loss(1);
            log_combat(user, this, "attacked");
        }
        else
            to_chat(user, span_warning(I18n.T("[] is too injured for that.", name)));
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:40
    [DmParams("L", "modifiers")]
    public override async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_alien_larva L = user as mob_living_carbon_alien_larva;
        return await attack_alien(L);
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:44
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await ((mob_living)user).disarm(this);
            return TRUE;
        }
        if (Dm.truthy(((mob_living)user).combat_mode))
            await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        else
        {
            await help_shake_act(user as mob_living_carbon);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:58
    public override object get_shove_flags(mob_living shover = null, obj_item weapon = null)
    {
        object dot = base.get_shove_flags(shover, weapon);
        if (weapon == null || IS_UNCONSCIOUS_OR_CRIT(this))
            dot = Dm.bitand(dot, Dm.bitnot(Dm.bitor(Dm.bitor(SHOVE_CAN_MOVE, SHOVE_CAN_HIT_SOMETHING), SHOVE_CAN_STAGGER)));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:63
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await base.attack_paw(user, modifiers)))
        {
            if (stat != DEAD)
            {
                obj_item_bodypart affecting = get_bodypart(get_random_valid_zone(user.zone_selected) as string) as obj_item_bodypart;
                await apply_damage(rand(1, 3), BRUTE, affecting);
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:69  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper
    // of waiting-model.md 5.4; gib and the damage may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.ex_act(severity, target, __extra);
        if (!Dm.truthy(dot.Value) || QDELETED(this))
            return FALSE;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                await gib(DROP_ALL_REMAINS);
                break;

            case EXPLODE_HEAVY:
                await take_overall_damage(60, 60);
                if (prob(50))
                    await Unconscious(2 * SECONDS);
                await sound_damage(30, 240 * SECONDS);
                break;

            case EXPLODE_LIGHT:
                await take_overall_damage(30, 0);
                await sound_damage(15, 120 * SECONDS);
                break;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:90
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        return FALSE;//aliens are immune to acid.
    }

    // tg: code/modules/mob/living/carbon/alien/alien_defense.dm:93
    public override async DmTask on_fire_stack(object seconds_per_tick = null, datum_status_effect_fire_handler_fire_stacks fire_handler = null)
    {
        adjust_bodytemperature((BODYTEMP_HEATING_MAX + (Dm.num(fire_handler.stacks) * 12)) * 0.5f * Dm.num(seconds_per_tick));
    }
}
