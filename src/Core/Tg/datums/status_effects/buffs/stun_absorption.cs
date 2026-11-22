// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/stun_absorption.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4c, D316: His Grace's stun immunity). The attack log lines are comments. The var block is generated
// (D111) but source, which tg passes as a string id (so the type block's overrides are the constructor here).

/**
 * # Stun absorption
 *
 * A status effect effectively functions as [TRAIT_STUNIMMUNE], but with additional effects tied to it,
 * such as showing a message on trigger / examine, or only blocking a limited amount of stuns.
 *
 * Apply this via [/mob/living/proc/add_stun_absorption]. If you do not supply a duration,
 * remove this via [/mob/living/proc/remove_stun_absorption].
 */
// tg: code/datums/status_effects/buffs/stun_absorption.dm:10
public partial class datum_status_effect_stun_absorption : datum_status_effect
{
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:11-14  the var overrides of the type block (rules 10.2): a hand-written
    // class that declares a var writes its own constructor
    public datum_status_effect_stun_absorption()
    {
        id = "absorb_stun";
        tick_interval = STATUS_EFFECT_NO_TICK;
        alert_type = null;
        status_type = STATUS_EFFECT_MULTIPLE;
    }

    /// The string key sourcer of the stun absorption, used for logging
    public object source; // tg: code/datums/status_effects/buffs/stun_absorption.dm:17

    /// Static list of all generic "stun received " signals that we will react to and block.
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:40  var/static/list/incapacitation_effect_signals (rules 10.6)
    public static DmList incapacitation_effect_signals => TgState.Current.datum_status_effect_stun_absorption__incapacitation_effect_signals ??= Dm.list(
        COMSIG_LIVING_STATUS_IMMOBILIZE,
        COMSIG_LIVING_STATUS_INCAPACITATE,
        COMSIG_LIVING_STATUS_KNOCKDOWN,
        COMSIG_LIVING_STATUS_PARALYZE,
        COMSIG_LIVING_STATUS_STUN);

    // tg: code/datums/status_effects/buffs/stun_absorption.dm:48
    [DmParams("new_owner", "source", "duration", "priority", "shown_message", "self_message", "examine_message", "max_seconds_of_stuns_blocked", "delete_after_passing_max", "recharge_time")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object source = Dm.arg(__extra, 0);
        object duration = Dm.arg(__extra, 1);
        if (isnum(duration))
            this.duration = Dm.num_or_null(duration);

        this.source = source;
        priority = Dm.num(Dm.arg(__extra, 2) ?? -1);
        shown_message = Dm.arg(__extra, 3);
        self_message = Dm.arg(__extra, 4);
        examine_message = Dm.arg(__extra, 5);
        max_seconds_of_stuns_blocked = Dm.num(Dm.arg(__extra, 6) ?? INFINITY);
        delete_after_passing_max = Dm.arg(__extra, 7) ?? TRUE;
        recharge_time = Dm.arg(__extra, 8) ?? 1 * MINUTES;

        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/buffs/stun_absorption.dm:75
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner.mind != null || owner.client != null)
            owner.log_message(Dm.text("gained stun absorption (from: [])", Dm.truthy(source) ? source : "Unknown"), LOG_ATTACK);

        RegisterSignals(owner, incapacitation_effect_signals, nameof(try_absorb_incapacitating_effect));
        RegisterSignal(owner, COMSIG_LIVING_GENERIC_STUN_CHECK, nameof(try_absorb_generic_effect));
        return TRUE;
    }

    // tg: code/datums/status_effects/buffs/stun_absorption.dm:83
    public override async DmTask<object> on_remove()
    {
        if (owner.mind != null || owner.client != null)
            owner.log_message(Dm.text("lost stun absorption (from: [])", Dm.truthy(source) ? source : "Unknown"), LOG_ATTACK);

        UnregisterSignal(owner, incapacitation_effect_signals);
        UnregisterSignal(owner, COMSIG_LIVING_GENERIC_STUN_CHECK);
        return null;
    }

    // tg: code/datums/status_effects/buffs/stun_absorption.dm:90
    public override object get_examine_text(mob examiner = null)
    {
        if (Dm.truthy(can_absorb_stun()))
            return replacetext(examine_message, "%EFFECT_OWNER_THEYRE", owner.p_Theyre());
        return null; // no message if we can't absorb stuns, duh.
    }

    /**
     * Signal proc for generic stun signals being sent, such as [COMSIG_LIVING_STATUS_STUN] or [COMSIG_LIVING_STATUS_KNOCKDOWN].
     *
     * When we get stunned, we will try to absorb a number of seconds from the stun, and return [COMPONENT_NO_STUN] if we succeed.
     */
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:100
    [SignalHandler]
    public virtual object try_absorb_incapacitating_effect(mob_living source = null, object amount = null, object ignore_canstun = null)
    {
        // we blocked a stun this tick that resulting is us qdeling, so stop
        if (QDELING(this))
            return NONE;

        // Amount less than (or equal to) zero is removing stuns, so we don't want to block that
        if (Dm.num(amount) <= 0 || Dm.truthy(ignore_canstun))
            return NONE;

        if (!Dm.truthy(absorb_stun(Dm.num(amount))))
            return NONE;

        return COMPONENT_NO_STUN;
    }

    /**
     * Signal proc for [COMSIG_LIVING_GENERIC_STUN_CHECK]. (Note, this includes being stamcrit)
     *
     * Whenever a generic stun check is done against us, we'll just try to block it with "0 second" stun.
     * This prevents spam us from showing feedback messages, and is for the generic "can be stunned" check.
     */
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:122
    [SignalHandler]
    public virtual object try_absorb_generic_effect(mob_living source = null, object check_flags = null)
    {
        if (QDELING(this))
            return NONE;

        // "0 amount" / "0 seconds of stun" is used so no feedback is sent on success
        if (!Dm.truthy(absorb_stun(0)))
            return NONE;

        return COMPONENT_NO_STUN;
    }

    /// Simply checks if the owner of the effect is in a valid state to absorb stuns.
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:135
    public virtual object can_absorb_stun()
    {
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return FALSE;
        if (seconds_of_stuns_absorbed > max_seconds_of_stuns_blocked)
            return FALSE;
        return TRUE;
    }

    /**
     * Absorb a number of seconds of stuns.
     * If we hit the max amount of absorption, we will qdel ourself in this proc.
     *
     * * amount - this is the number of deciseconds being absorbed at once.
     *
     * Returns TRUE on successful absorption, or FALSE otherwise.
     */
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:150
    public virtual object absorb_stun(float amount)
    {
        if (!Dm.truthy(can_absorb_stun()))
            return FALSE;

        // Now we gotta check that no other stun absorption we have is blocking us
        foreach (datum_status_effect_stun_absorption similar_effect in Dm.each<datum_status_effect_stun_absorption>(owner.status_effects))
        {
            if (similar_effect == this)
                continue;
            // they blocked a stun this tick that resulted in them qdeling, so disregard
            if (QDELING(similar_effect))
                continue;
            // if we have another stun absorption with higher priority,
            // don't do anything, let them handle it instead
            if (similar_effect.priority > priority)
                return FALSE;
        }

        // At this point, a stun was successfully absorbed

        // Only do effects if the amount was > 0 seconds
        if (amount > 0 * SECONDS)
        {
            // Show the message
            if (Dm.truthy(shown_message))
            {
                // We do this replacement meme, instead of just setting it up in creation,
                // so that we respect indentity changes done while active
                object really_shown_message = replacetext(shown_message, "%EFFECT_OWNER", Dm.text("[]", owner));
                owner.visible_message(really_shown_message, ignored_mobs: owner);
            }

            // Send the self message
            if (Dm.truthy(self_message))
                to_chat(owner, self_message);

            // Count seconds absorbed
            seconds_of_stuns_absorbed += amount;
            if (Dm.truthy(delete_after_passing_max))
            {
                if (seconds_of_stuns_absorbed >= max_seconds_of_stuns_blocked)
                    qdel(this);
            }
            else if (Dm.num(recharge_time) > 0 * SECONDS)
                _addtimer(Dm.@new<datum_callback>(this, nameof(recharge_absorption), amount), Dm.num(recharge_time), file: "code/datums/status_effects/buffs/stun_absorption.dm", line: 188);
        }

        return TRUE;
    }

    /// Used in callbacks to "recharge" the effect after passing the max seconds of stuns blocked.
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:193
    public virtual void recharge_absorption(object amount)
    {
        seconds_of_stuns_absorbed = max(seconds_of_stuns_absorbed - Dm.num(amount), 0);
    }
}

public sealed partial class TgState
{
    internal DmList datum_status_effect_stun_absorption__incapacitation_effect_signals;
}

public partial class mob_living
{
    /**
     * [proc/apply_status_effect] wrapper specifically for [/datum/status_effect/stun_absorption],
     * specifically so that it's easier to apply stun absorptions with named arguments.
     *
     * If the mob already has a stun absorption from the same source, will not re-apply the effect,
     * unless the new effect's priority is higher than the old effect's priority.
     *
     * Returns an instance of a stun absorption effect, or NULL if failure
     */
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:218
    public virtual async DmTask<object> add_stun_absorption(
        object source = null,
        object duration = null,
        float priority = -1,
        object message = null,
        object self_message = null,
        object examine_message = null,
        float max_seconds_of_stuns_blocked = INFINITY,
        object delete_after_passing_max = null,
        object recharge_time = null,
        object recharge_alert = null)
    {
        delete_after_passing_max ??= TRUE;

        // Handle duplicate sources
        foreach (datum_status_effect_stun_absorption existing_effect in Dm.each<datum_status_effect_stun_absorption>(status_effects))
        {
            if (!Dm.eq(existing_effect.source, source))
                continue;

            // If an existing effect's priority is greater or equal to our passed priority...
            if (existing_effect.priority >= priority)
            {
                // don't bother re-applying the effect, and return
                return null;
            }

            // otherwise, delete existing and replace with new
            qdel(existing_effect);
        }

        return await apply_status_effect(
            typeof(datum_status_effect_stun_absorption),
            source,
            duration,
            priority,
            message,
            self_message,
            examine_message,
            max_seconds_of_stuns_blocked,
            delete_after_passing_max,
            recharge_time);
    }

    /**
     * Removes all stub absorptions with the passed source.
     *
     * Returns TRUE if an effect was deleted, FALSE otherwise
     */
    // tg: code/datums/status_effects/buffs/stun_absorption.dm:262
    public virtual object remove_stun_absorption(object source)
    {
        object dot = FALSE;
        foreach (datum_status_effect_stun_absorption effect in Dm.each<datum_status_effect_stun_absorption>(status_effects))
        {
            if (!Dm.eq(effect.source, source))
                continue;

            qdel(effect);
            dot = TRUE;
        }

        return dot;
    }
}
