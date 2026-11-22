// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/_status_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P1 (the living core): the whole file is translated except vv_edit_var (:218, View Variables, rules 13).

/// Status effects are used to apply temporary or permanent effects to mobs.
/// This file contains their code, plus code for applying and removing them.
// tg: code/datums/status_effects/_status_effect.dm:3
[DmPath("/datum/status_effect")]
public partial class datum_status_effect : datum
{
    /// The ID of the effect. ID is used in adding and removing effects to check for duplicates, among other things.
    // tg: code/datums/status_effects/_status_effect.dm:5
    // (object, not string: the V3 grep matches typepath literals assigned to other vars named id, _globalvars/pipe_info.dm:98,
    // and a typepath var cannot hold the text default, rules 3.2 and 3.5; as /datum/movespeed_modifier/var/id)
    public object id = "effect";
    /// This is how long the status effect lasts in deciseconds.
    /// You can put STATUS_EFFECT_PERMANENT (or INFINITY) here for infinite duration.
    // tg: code/datums/status_effects/_status_effect.dm:8
    // (float?: the null-use grep matches, modules/admin/sql_ban_system.dm:189 and others)
    public float? duration = STATUS_EFFECT_PERMANENT;
    /// This is how long between [proc/tick] calls in deciseconds.
    /// This has to be a multiple of the [var/wait] of the subsystem this status effect is running on, which is based on [var/processing_speed].
    /// Putting STATUS_EFFECT_NO_TICK here will stop [proc/tick] calls, and if [var/duration] is STATUS_EFFECT_PERMANENT, it stops processing entirely.
    /// Putting STATUS_EFFECT_AUTO_TICK here will make every subsystem tick call [proc/tick], making the tick interval depend entirely on [var/processing_speed]
    // tg: code/datums/status_effects/_status_effect.dm:13
    public float tick_interval = 1 * SECONDS;
    /// The time until the next [proc/tick] call, gets set to [var/tick_interval] after every [proc/tick] call and decrements on every [proc/process] call.
    // tg: code/datums/status_effects/_status_effect.dm:15
    public object time_until_next_tick;
    /// The mob affected by the status effect.
    // tg: code/datums/status_effects/_status_effect.dm:17  VAR_FINAL/mob/living/owner
    [DmFinal]
    public mob_living owner;
    /// How many of the effect can be on one mob, and/or what happens when you try to add a duplicate.
    // tg: code/datums/status_effects/_status_effect.dm:19
    public float status_type = STATUS_EFFECT_UNIQUE;
    /// If TRUE, we call [proc/on_remove] when owner is deleted. Otherwise, we call [proc/be_replaced].
    // tg: code/datums/status_effects/_status_effect.dm:21
    public float on_remove_on_mob_delete = FALSE;
    /// The typepath to the alert thrown by the status effect when created.
    /// Status effect "name"s and "description"s are shown to the owner here.
    // tg: code/datums/status_effects/_status_effect.dm:24
    public Type alert_type = typeof(atom_movable_screen_alert_status_effect);
    /// The alert itself, created in [proc/on_creation] (if alert_type is specified).
    // tg: code/datums/status_effects/_status_effect.dm:26  VAR_FINAL/atom/movable/screen/alert/status_effect/linked_alert
    [DmFinal]
    public atom_movable_screen_alert_status_effect linked_alert;
    /// If TRUE, and we have an alert, we will show a duration on the alert
    // tg: code/datums/status_effects/_status_effect.dm:28
    public float show_duration = FALSE;
    /// Used to define if the status effect should be using SSfastprocess or SSprocessing
    // tg: code/datums/status_effects/_status_effect.dm:30
    public float processing_speed = STATUS_EFFECT_FAST_PROCESS;
    /// Do we self-terminate when a fullheal is called?
    // tg: code/datums/status_effects/_status_effect.dm:32
    public float remove_on_fullheal = FALSE;
    /// If remove_on_fullheal is TRUE, what flag do we need to be removed?
    // tg: code/datums/status_effects/_status_effect.dm:34
    public float heal_flag_necessary = HEAL_STATUS;
    /// A particle effect, for things like embers - Should be set on update_particles()
    // tg: code/datums/status_effects/_status_effect.dm:36  VAR_FINAL/obj/effect/abstract/particle_holder/particle_effect
    [DmFinal]
    public obj_effect_abstract_particle_holder particle_effect;
}

// tg: code/datums/status_effects/_status_effect.dm:38
public partial class datum_status_effect : datum
{
    // tg: code/datums/status_effects/_status_effect.dm:38
    [DmParams("arguments")]
    public override async DmTask New(params object[] __extra)
    {
        DmList arguments = Dm.arg<DmList>(__extra, 0);
        await Dm.Call(this, nameof(on_creation), Dm.arglist(arguments));
    }

    /// Called from New() with any supplied status effect arguments.
    /// Not guaranteed to exist by the end.
    /// Returning FALSE from on_apply will stop on_creation and self-delete the effect.
    // tg: code/datums/status_effects/_status_effect.dm:44
    public virtual async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        if (Dm.truthy(new_owner))
            owner = new_owner;
        DmList args = Dm.args(new_owner, __extra);
        if (QDELETED(owner) || !Dm.truthy(await Dm.Call(this, nameof(on_apply), Dm.arglist(args.Copy(2)))))
        {
            qdel(this);
            return null;
        }
        if (Dm.truthy(owner))
        {
            LAZYADD(ref owner.status_effects, this);
            RegisterSignal(owner, COMSIG_LIVING_POST_FULLY_HEAL, nameof(remove_effect_on_heal));
        }

        if (Dm.eq(duration, INFINITY))
        {
            // we will optionally allow INFINITY, because i imagine it'll be convenient in some places,
            // but we'll still set it to -1 / STATUS_EFFECT_PERMANENT for proper unified handling
            duration = STATUS_EFFECT_PERMANENT;
        }

        if (tick_interval != STATUS_EFFECT_NO_TICK)
            time_until_next_tick = tick_interval;

        if (Dm.truthy(alert_type))
        {
            atom_movable_screen_alert_status_effect new_alert = owner.throw_alert(id, alert_type) as atom_movable_screen_alert_status_effect;
            new_alert.attached_effect = this; //so the alert can reference us, if it needs to
            linked_alert = new_alert; //so we can reference the alert, if we need to
            update_shown_duration();
        }

        if (!Dm.eq(duration, STATUS_EFFECT_PERMANENT) || tick_interval != STATUS_EFFECT_NO_TICK) //don't process if we don't care
            start_processing();

        await update_particles();
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_STATUS_APPLIED]))
            owner._SendSignal(COMSIG_LIVING_STATUS_APPLIED, Dm.list(owner, this));
        return TRUE;
    }

    // tg: code/datums/status_effects/_status_effect.dm:75
    public override object Destroy(float? force = null)
    {
        stop_processing();
        if (Dm.truthy(owner))
        {
            linked_alert = null;
            owner.clear_alert(id);
            LAZYREMOVE(ref owner.status_effects, this);
            Waiting.NoSleep(on_remove());
            UnregisterSignal(owner, COMSIG_LIVING_POST_FULLY_HEAL);
            if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_STATUS_REMOVED]))
                owner._SendSignal(COMSIG_LIVING_STATUS_REMOVED, Dm.list(owner, this));
            owner = null;
        }
        if (Dm.truthy(particle_effect))
            QDEL_NULL(ref particle_effect);
        return base.Destroy(force);
    }

    /// Updates the status effect alert's maptext (if possible)
    // tg: code/datums/status_effects/_status_effect.dm:90
    [DmProtected]
    public virtual void update_shown_duration()
    {
        if (!Dm.truthy(linked_alert) || !Dm.truthy(show_duration))
            return;

        linked_alert.maptext = MAPTEXT_TINY_UNICODE(Dm.text("<span style='text-align:center'>[]s</span>", round(Dm.num(duration) / 10, 1)));
    }

    // Status effect process. Handles adjusting its duration and ticks.
    // If you're adding processed effects, put them in [proc/tick]
    // instead of extending / overriding the process() proc.
    // tg: code/datums/status_effects/_status_effect.dm:100  (set waitfor = FALSE inherited from /datum/process; tick() may
    // sleep in a subtype, so the definition is the wrapper and its __body, waiting-model.md 5.4)
    [ShouldNotOverride]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick, __extra, null), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick, object[] __extra, DotBox<object> dot)
    {
        if (QDELETED(owner))
        {
            qdel(this);
            return null;
        }

        if (!Dm.eq(duration, STATUS_EFFECT_PERMANENT))
            duration = Dm.num_or_null(max(0, Dm.sub(duration, Dm.mul(seconds_per_tick, SECONDS)))); // doing it first means its more up to date for ticks to read

        if (tick_interval != STATUS_EFFECT_NO_TICK)
            time_until_next_tick = max(0, Dm.sub(time_until_next_tick, Dm.mul(seconds_per_tick, SECONDS))); // same here

        if (tick_interval == STATUS_EFFECT_AUTO_TICK)
            await tick(seconds_per_tick);
        else if (tick_interval != STATUS_EFFECT_NO_TICK && Dm.le(time_until_next_tick, 0))
        {
            time_until_next_tick = tick_interval; // same here as well
            await tick(tick_interval / 10);
        }

        if (QDELING(this))
            return null; // tick deleted us, no need to continue

        if (!Dm.eq(duration, STATUS_EFFECT_PERMANENT))
        {
            if (Dm.num(duration) <= 0)
            {
                qdel(this);
                return null;
            }
            update_shown_duration();
        }
        return null;
    }

    /// Called whenever the effect is applied in on_created
    /// Returning FALSE will cause it to delete itself during creation instead.
    // tg: code/datums/status_effects/_status_effect.dm:130
    public virtual async DmTask<object> on_apply(params object[] __extra)
    {
        return TRUE;
    }

    /// Gets and formats examine text associated with our status effect.
    /// Return 'null' to have no examine text appear (default behavior).
    // tg: code/datums/status_effects/_status_effect.dm:135
    public virtual object get_examine_text(mob examiner = null)
    {
        return null;
    }

    /**
     * Called every tick from process().
     * This is only called of tick_interval is not -1.
     *
     * Note that every tick =/= every processing cycle.
     *
     * * seconds_between_ticks = This is how many SECONDS that elapse between ticks.
     * This is a constant value based upon the initial tick interval set on the status effect.
     * It is similar to seconds_per_tick, from processing itself, but adjusted to the status effect's tick interval.
     */
    // tg: code/datums/status_effects/_status_effect.dm:148
    public virtual async DmTask<object> tick(object seconds_between_ticks = null)
    {
        return null;
    }

    /// Called whenever the buff expires or is removed (qdeleted)
    /// Note that at the point this is called, it is out of the
    /// owner's status_effects list, but owner is not yet null
    // tg: code/datums/status_effects/_status_effect.dm:154
    public virtual async DmTask<object> on_remove()
    {
        return null;
    }

    /// Called instead of on_remove when a status effect
    /// of status_type STATUS_EFFECT_REPLACE is replaced by itself,
    /// or when a status effect with on_remove_on_mob_delete
    /// set to FALSE has its mob deleted
    // tg: code/datums/status_effects/_status_effect.dm:161
    public virtual async DmTask be_replaced()
    {
        linked_alert = null;
        owner.clear_alert(id);
        LAZYREMOVE(ref owner.status_effects, this);
        owner = null;
        qdel(this);
    }

    /// Called before being fully removed (before on_remove)
    /// Returning FALSE will cancel removal
    // tg: code/datums/status_effects/_status_effect.dm:170
    public virtual object before_remove(params object[] __extra)
    {
        return TRUE;
    }

    /// Called when a status effect of status_type STATUS_EFFECT_REFRESH
    /// has its duration refreshed in apply_status_effect - is passed New() args
    // tg: code/datums/status_effects/_status_effect.dm:175
    public virtual async DmTask refresh(object effect = null, params object[] __extra)
    {
        duration = Dm.initial(this, (datum_status_effect p) => p.duration);
    }

    /// Adds nextmove modifier multiplicatively to the owner while applied
    // tg: code/datums/status_effects/_status_effect.dm:179
    public virtual object nextmove_modifier()
    {
        return 1;
    }

    /// Adds nextmove adjustment additiviely to the owner while applied
    // tg: code/datums/status_effects/_status_effect.dm:183
    public virtual object nextmove_adjust()
    {
        return 0;
    }

    /// Signal proc for [COMSIG_LIVING_POST_FULLY_HEAL] to remove us on fullheal
    // tg: code/datums/status_effects/_status_effect.dm:187
    [SignalHandler]
    public virtual void remove_effect_on_heal(datum source = null, object heal_flags = null)
    {

        if (!Dm.truthy(remove_on_fullheal))
            return;

        if (!Dm.truthy(heal_flag_necessary) || Dm.truthy(Dm.bitand(heal_flags, heal_flag_necessary)))
            qdel(this);
    }

    /// Removes [seconds] of duration from the status effect.
    /// Returns whether or not the status effect was qdeleted due to running out of duration.
    // tg: code/datums/status_effects/_status_effect.dm:198
    public virtual object remove_duration(object seconds = null)
    {
        if (Dm.eq(duration, STATUS_EFFECT_PERMANENT)) // Infinite duration
            return FALSE;

        duration = Dm.num(duration) - Dm.num(Dm.mul(seconds, SECONDS));
        if (Dm.num(duration) <= 0)
        {
            qdel(this);
            return TRUE;
        }

        update_shown_duration();
        return FALSE;
    }

    /**
     * Updates the particles for the status effects
     * Should be handled by subtypes!
     */
    // tg: code/datums/status_effects/_status_effect.dm:214
    public virtual async DmTask update_particles()
    {
        return;
    }

    // tg: code/datums/status_effects/_status_effect.dm:218  /datum/status_effect/vv_edit_var: View Variables, not translated (rules 13).

    /// Stops ticking. Entirely stops processing if the effect is permanent.
    // tg: code/datums/status_effects/_status_effect.dm:231
    public virtual void stop_ticking()
    {
        // If we have a set duration, we can't stop processing as duration is also handled in process
        if (!Dm.eq(duration, STATUS_EFFECT_PERMANENT))
        {
            time_until_next_tick = STATUS_EFFECT_NO_TICK;
            return;
        }

        // But if we have are permanent, there is no reason to keep processing if we don't tick
        stop_processing();
    }

    /// Stops processing, removing it from relevant subsystems
    // tg: code/datums/status_effects/_status_effect.dm:241
    public virtual void stop_processing()
    {
        switch (processing_speed)
        {
            case STATUS_EFFECT_FAST_PROCESS:
                STOP_PROCESSING(SSfastprocess, this);
                break;
            case STATUS_EFFECT_NORMAL_PROCESS:
                STOP_PROCESSING(SSprocessing, this);
                break;
            case STATUS_EFFECT_PRIORITY:
                STOP_PROCESSING(SSpriority_effects, this);
                break;
        }
    }

    /// (Re)starts ticking, also (re)starting processing if the effect is permanent
    // tg: code/datums/status_effects/_status_effect.dm:251
    public virtual void start_ticking()
    {
        // If we have a set duration, we assume we're processing already, so just reset the timer
        if (!Dm.eq(duration, STATUS_EFFECT_PERMANENT))
        {
            time_until_next_tick = tick_interval;
            return;
        }

        // But if we are permanent, we probably need to start processing
        start_processing();
    }

    /// (Re)starts processing, adding it to relevant subsystems
    // tg: code/datums/status_effects/_status_effect.dm:261
    public virtual void start_processing()
    {
        switch (processing_speed)
        {
            case STATUS_EFFECT_FAST_PROCESS:
                START_PROCESSING(SSfastprocess, this);
                break;
            case STATUS_EFFECT_NORMAL_PROCESS:
                START_PROCESSING(SSprocessing, this);
                break;
            case STATUS_EFFECT_PRIORITY:
                START_PROCESSING(SSpriority_effects, this);
                break;
        }
    }
}

/// Alert base type for status effect alerts
// tg: code/datums/status_effects/_status_effect.dm:271
[DmPath("/atom/movable/screen/alert/status_effect")]
public partial class atom_movable_screen_alert_status_effect : atom_movable_screen_alert
{
    // tg: code/datums/status_effects/_status_effect.dm:272-274  the var overrides of the type block (rules 10.2)
    public atom_movable_screen_alert_status_effect()
    {
        name = "Curse of Mundanity";
        desc = "You don't feel any different...";
        maptext_y = 2;
    }

    /// The status effect we're linked to
    // tg: code/datums/status_effects/_status_effect.dm:276
    public datum_status_effect attached_effect;

    // tg: code/datums/status_effects/_status_effect.dm:278
    public override object Destroy(float? force = null)
    {
        attached_effect = null; //Don't keep a ref now
        return base.Destroy(force);
    }
}
