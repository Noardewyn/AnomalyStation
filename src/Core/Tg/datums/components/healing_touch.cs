// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/healing_touch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// A mob with this component will be able to heal certain targets by attacking them.
// tg: code/datums/components/healing_touch.dm:10
public partial class datum_component_healing_touch : datum_component
{
    // tg: code/datums/components/healing_touch.dm:53
    [DmParams("heal_brute", "heal_burn", "heal_tox", "heal_oxy", "heal_stamina", "heal_time", "interaction_key", "extra_checks", "valid_targets_typecache", "valid_biotypes", "required_bodytype", "self_targeting", "action_text", "complete_text", "show_health", "heal_color", "required_modifier", "after_healed", "healing_multiplier")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.heal_brute = Dm.arg(__extra, 0) ?? 20f;
        this.heal_burn = Dm.arg(__extra, 1) ?? 20f;
        this.heal_tox = Dm.arg(__extra, 2) ?? 0f;
        this.heal_oxy = Dm.arg(__extra, 3) ?? 0f;
        this.heal_stamina = Dm.arg(__extra, 4) ?? 0f;
        this.heal_time = Dm.arg(__extra, 5) ?? 2 * SECONDS;
        this.interaction_key = Dm.arg(__extra, 6) ?? DOAFTER_SOURCE_HEAL_TOUCH;
        this.extra_checks = Dm.arg(__extra, 7) as datum_callback;
        this.valid_targets_typecache = (Dm.arg(__extra, 8) as DmList ?? Dm.list()).Copy();
        this.valid_biotypes = Dm.arg(__extra, 9) ?? Dm.bitor(MOB_ORGANIC, MOB_MINERAL);
        this.required_bodytype = Dm.arg(__extra, 10) ?? BODYTYPE_ORGANIC;
        this.self_targeting = Dm.arg(__extra, 11) ?? HEALING_TOUCH_NOT_SELF;
        this.action_text = Dm.arg(__extra, 12) ?? "%SOURCE% begins healing %TARGET%";
        this.complete_text = Dm.arg(__extra, 13) ?? "%SOURCE% finishes healing %TARGET%";
        this.show_health = Dm.arg(__extra, 14) ?? FALSE;
        this.heal_color = Dm.arg(__extra, 15) ?? COLOR_HEALING_CYAN;
        this.required_modifier = Dm.arg(__extra, 16);
        this.after_healed = Dm.arg(__extra, 17) as datum_callback;
        this.healing_multiplier = Dm.arg(__extra, 18) as datum_callback;

        RegisterSignal(parent, COMSIG_LIVING_UNARMED_ATTACK, nameof(try_healing)); // Players
        RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(try_healing)); // NPCs
        ((parent as mob_living).ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_MOB_HEALER, TRUE);
        return null;
    }

    // Let's populate this list as we actually use it, this thing has too many args
    // tg: code/datums/components/healing_touch.dm:103  (its named heal_color bound since syndicate X2: the support guardian's colour)
    [DmParams("new_component", "i_am_original", "heal_color")]
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        this.heal_color = Dm.arg(__extra, 0);
    }

    // tg: code/datums/components/healing_touch.dm:110
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        ((parent as mob_living).ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_MOB_HEALER, FALSE);
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET));
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/healing_touch.dm:116
    public override object Destroy(float? force = null)
    {
        extra_checks = null;
        return base.Destroy(force);
    }

    /// Validate our target, and interrupt the attack chain to start healing it if it is allowed
    // tg: code/datums/components/healing_touch.dm:121
    [SignalHandler]
    public virtual object try_healing(mob_living healer = null, atom target = null, object proximity = null, DmList modifiers = null)
    {
        if (target is not mob_living living_target)
            return null;

        if (required_modifier != null && !Dm.truthy(LAZYACCESS(modifiers, required_modifier)))
            return null;

        if (Dm.length(valid_targets_typecache) > 0 && !Dm.truthy(is_type_in_typecache(target, valid_targets_typecache)))
            return null; // Fall back to attacking it

        if (extra_checks != null && !Dm.truthy(Waiting.NoSleep(extra_checks.Invoke(healer, target))))
            return COMPONENT_CANCEL_ATTACK_CHAIN;

        if (Dm.truthy(DOING_INTERACTION(healer, interaction_key)))
        {
            healer.balloon_alert(healer, "busy!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (Dm.eq(self_targeting, HEALING_TOUCH_NOT_SELF))
        {
            if (target == healer)
            {
                healer.balloon_alert(healer, "can't heal yourself!");
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }
        }
        else if (Dm.eq(self_targeting, HEALING_TOUCH_SELF_ONLY))
        {
            if (target != healer)
            {
                healer.balloon_alert(healer, "can only heal yourself!");
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }
        }

        if (Dm.num(living_target.health) >= Dm.num(living_target.maxHealth))
        {
            target.balloon_alert(healer, "not hurt!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (!Dm.truthy(has_healable_damage(living_target)))
        {
            target.balloon_alert(healer, "can't heal that!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (living_target.stat == DEAD)
        {
            target.balloon_alert(healer, "they're dead!");
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        Waiting.Detach(heal_target(healer, living_target)); // INVOKE_ASYNC

        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Returns true if the target has a kind of damage which we can heal
    // tg: code/datums/components/healing_touch.dm:167
    public virtual object has_healable_damage(mob_living target = null)
    {
        if (valid_biotypes != null && !Dm.truthy(Dm.bitand(valid_biotypes, target.mob_biotypes)))
            return FALSE;
        if (Dm.num(target.get_stamina_loss()) > 0 && Dm.truthy(heal_stamina))
            return TRUE;
        if (Dm.num(target.get_oxy_loss()) > 0 && Dm.truthy(heal_oxy))
            return TRUE;
        if (Dm.num(target.get_tox_loss()) > 0 && Dm.truthy(heal_tox))
            return TRUE;
        if (target is not mob_living_carbon carbon_target)
            return (Dm.num(target.get_brute_loss()) > 0 && Dm.truthy(heal_brute)) || (Dm.num(target.get_fire_loss()) > 0 && Dm.truthy(heal_burn)) ? TRUE : FALSE;
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(carbon_target.get_bodyparts()))
        {
            if (!(Dm.truthy(part.brute_dam) && Dm.truthy(heal_brute)) && !(Dm.truthy(part.burn_dam) && Dm.truthy(heal_burn)))
                continue;
            if (required_bodytype != null && !Dm.truthy(Dm.bitand(part.bodytype, required_bodytype)))
                continue;
            return TRUE;
        }
        return FALSE;
    }

    /// Perform a do_after and then heal our target
    // tg: code/datums/components/healing_touch.dm:188
    public virtual async DmTask heal_target(mob_living healer = null, mob_living target = null)
    {
        if (Dm.truthy(action_text))
            healer.visible_message(span_notice(Dm.text("[]", format_string(action_text, healer, target))));

        if (Dm.truthy(heal_time) && !Dm.truthy(await do_after(healer, Dm.num(heal_time), target, interaction_key: interaction_key)))
        {
            healer.balloon_alert(healer, "interrupted!");
            return;
        }

        if (Dm.truthy(complete_text))
            healer.visible_message(span_notice(Dm.text("[]", format_string(complete_text, healer, target))));

        float mult = 1;
        if (healing_multiplier != null)
        {
            mult = Dm.num(await healing_multiplier.Invoke(healer, target));
            if (mult == 0)
                return;
        }

        float healed = Dm.num(await target.heal_overall_damage(Dm.num(heal_brute) * mult, Dm.num(heal_burn) * mult, Dm.num(heal_stamina) * mult, required_bodytype: required_bodytype, updating_health: FALSE));
        healed += Dm.num(await target.adjust_oxy_loss(-Dm.num(heal_oxy) * mult, updating_health: FALSE, required_biotype: Dm.num(valid_biotypes)));
        healed += Dm.num(await target.adjust_tox_loss(-Dm.num(heal_tox) * mult, updating_health: FALSE, required_biotype: Dm.num(valid_biotypes)));
        if (healed <= 0)
            return;

        await target.updatehealth();
        Dm.@new<obj_effect_temp_visual_heal>(get_turf(target), heal_color);
        if (after_healed != null)
            await after_healed.Invoke(target);

        if (!Dm.truthy(show_health))
            return;
        object formatted_string = format_string(Dm.text("%TARGET% now has <b>[] health.</b>", Dm.text("[]%", round(Dm.num(target.health) / Dm.num(target.maxHealth) * 100, 0.1f))), healer, target); // health_percentage (__HELPERS/mobs.dm)
        to_chat(healer, span_danger(Dm.text("[]", formatted_string)));
    }

    /// Reformats the passed string with the replacetext keys
    // tg: code/datums/components/healing_touch.dm:226
    public virtual object format_string(object @string = null, atom source = null, atom target = null)
    {
        object final_message = replacetext(Dm.text("[]", @string), "%SOURCE%", Dm.text("[]", source));
        return replacetext(Dm.text("[]", final_message), "%TARGET%", Dm.text("[]", target));
    }
}
