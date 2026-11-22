// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/eyestab.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// An element that lets you stab people in the eyes when targeting them
// tg: code/datums/elements/eyestab.dm:12
public partial class datum_element_eyestab : datum_element
{
    /// What's the probability a clumsy person stabs themselves in the eyes?
    private const float CLUMSY_ATTACK_SELF_CHANCE = 50;
    /// The damage threshold (of the victim's eyes) after which they start taking more serious effects
    private const float EYESTAB_BLEEDING_THRESHOLD = 10;
    /// The damage threshold (of the victim's eyes) after which they can go blind
    private const float EYESTAB_BLINDING_THRESHOLD = 30;
    /// How much blur we can apply
    private const float EYESTAB_MAX_BLUR = 4 * MINUTES;

    // tg: code/datums/elements/eyestab.dm:18
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        if (__extra.Length > 0 && __extra[0] != null)
            this.damage = Dm.num(__extra[0]);
        RegisterSignal(target, COMSIG_ITEM_ATTACK, nameof(on_item_attack));
        return dot;
    }

    // tg: code/datums/elements/eyestab.dm:29
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ITEM_ATTACK);
        return dot;
    }

    // tg: code/datums/elements/eyestab.dm:34
    [SignalHandler]
    public virtual object on_item_attack(datum source = null, mob_living target = null, mob_living user = null)
    {
        if (!Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_EYES))
            return null;
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
            return null;
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(CLUMSY_ATTACK_SELF_CHANCE))
            target = user;
        if (Dm.truthy(target.is_eyes_covered()) || isalien(target) || isbrain(target))
            return null;
        Waiting.Detach(perform_eyestab(source as obj_item, target, user));
        return COMPONENT_SKIP_ATTACK;
    }

    // tg: code/datums/elements/eyestab.dm:49
    public virtual async DmTask perform_eyestab(obj_item item = null, mob_living target = null, mob_living user = null)
    {
        obj_item_bodypart target_limb = (target as mob_living_carbon)?.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (ishuman(target) && target_limb == null)
            return;
        item.add_fingerprint(user);
        playsound(item, item.hitsound, 30, TRUE, -1);
        await user.do_attack_animation(target);
        if (target == user)
            user.visible_message(
                span_danger(I18n.T("[] stabs []self in the eye with []!", user, user.p_them(), item)),
                span_userdanger(I18n.T("You stab yourself in the eye with []!", item)));
        else
            target.visible_message(
                span_danger(I18n.T("[] stabs [] in the eye with []!", user, target, item)),
                span_userdanger(I18n.T("[] stabs you in the eye with []!", user, item)));

        if (target_limb != null)
            await target.apply_damage(damage, BRUTE, target_limb, attacking_item: item);
        else
            await target.take_bodypart_damage(damage);

        await target.add_mood_event("eye_stab", typeof(datum_mood_event_eye_stab));

        log_combat(user, target, "attacked", item.name, Dm.text("(Combat mode: [])", Dm.truthy(user.combat_mode) ? "On" : "Off"));
        obj_item_organ_eyes eyes = (target as mob_living_carbon)?.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null)
            return;

        await target.adjust_eye_blur_up_to(6 * SECONDS, EYESTAB_MAX_BLUR);
        bool started_bleeding = Dm.num(eyes.damage) < EYESTAB_BLEEDING_THRESHOLD;
        await eyes.apply_organ_damage(rand(2, 4));
        if (Dm.num(eyes.damage) < EYESTAB_BLEEDING_THRESHOLD)
            return;

        // At over 10 damage we apply a lot of eye blur
        await target.adjust_eye_blur_up_to(30 * SECONDS, EYESTAB_MAX_BLUR);
        if (!Dm.eq(target.stat, DEAD) && started_bleeding)
            to_chat(target, span_danger(I18n.T("Your eyes start to bleed profusely!")));

        // At over 10 damage, we cause at least enough eye damage to force nearsightedness
        if (!Dm.truthy(target.is_nearsighted_from(EYE_DAMAGE)) && Dm.num(eyes.damage) <= Dm.num(eyes.low_threshold))
            await eyes.set_organ_damage(eyes.low_threshold);

        // At over 10 damage, there is a 50% chance they drop all their items
        if (prob(50) && !Dm.eq(target.stat, DEAD))
        {
            DmList dropped = await target.drop_all_held_items() as DmList;
            if (Dm.truthy(length(dropped)))
                to_chat(target, span_danger(I18n.T("You drop what you're holding and clutch at your eyes!")));
            await target.adjust_eye_blur_up_to(20 * SECONDS, EYESTAB_MAX_BLUR);
            await target.Unconscious(2 * SECONDS);
            await target.Paralyze(4 * SECONDS);
        }

        // A solid chance of getting a permanent scar over one of your eyes, if you have at least one unscarred eyeball
        if (prob(Dm.num(eyes.damage) - EYESTAB_BLEEDING_THRESHOLD + 1))
        {
            DmList valid_sides = Dm.list();
            if (!Dm.truthy(Dm.bitand(eyes.scarring, RIGHT_EYE_SCAR)))
                valid_sides.Add(RIGHT_EYE_SCAR);
            if (!Dm.truthy(Dm.bitand(eyes.scarring, LEFT_EYE_SCAR)))
                valid_sides.Add(LEFT_EYE_SCAR);
            if (Dm.truthy(length(valid_sides)))
            {
                object picked_side = pick(valid_sides);
                to_chat(target, span_userdanger(I18n.T("You feel searing pain shoot though your [] eye!", Dm.eq(picked_side, RIGHT_EYE_SCAR) ? "right" : "left")));
                // oof ouch my eyes
                datum_wound_pierce_bleed_severe_eye eye_puncture = Dm.@new<datum_wound_pierce_bleed_severe_eye>();
                await eye_puncture.apply_wound(eyes.bodypart_owner, wound_source: "eye stab", __extra: new object[] { picked_side });
                await eyes.apply_scar(picked_side);
            }
        }

        if (Dm.num(eyes.damage) < EYESTAB_BLINDING_THRESHOLD)
            return;

        // At over 30 damage, there is a chance (based on eye damage) of going blind
        if (prob(Dm.num(eyes.damage) - EYESTAB_BLINDING_THRESHOLD + 1))
        {
            if (!Dm.truthy(target.is_blind_from(EYE_DAMAGE)))
                await eyes.set_organ_damage(eyes.maxHealth);
            // Also cause some temp blindness, so that they're still blind even if they get healed
            await target.adjust_temp_blindness_up_to(20 * SECONDS, 1 * MINUTES);
        }
    }
}
