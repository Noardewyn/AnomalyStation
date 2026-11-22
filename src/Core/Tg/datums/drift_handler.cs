// Translated from tgstation (snapshot 1d99bd7f): code/datums/drift_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated (M4b slice 1: the zero-gravity drift; the space wind blows people into space). The W4.0 contracts declared the
// type and the signatures /atom/movable/proc/newtonian_move calls (atoms_movable.dm:1324); the var block is generated
// (D111).

///Component that handles drifting
///Manages a movement loop that actually does the legwork of moving someone
///Alongside dealing with the post movement input blocking required to make things look nice
// tg: code/datums/drift_handler.dm:4
[DmPath("/datum/drift_handler")]
public partial class datum_drift_handler : datum
{
    /// <summary>DEVIATION(DV412): the wind blew the parent into this drift (its push came within <see cref="atom_movable.BlownWindow"/>), so no handhold stops it.</summary>
    public bool dm_blown;

    /// Accepts three args. The direction to drift in, if the drift is instant or not, and if it's not instant, the delay on the start
    // tg: code/datums/drift_handler.dm:19
    [DmParams("parent", "inertia_angle", "instant", "start_delay", "drift_force")]
    public override async DmTask New(params object[] __extra)
    {
        atom_movable parent = Dm.arg<atom_movable>(__extra, 0);
        object inertia_angle = Dm.arg<object>(__extra, 1);
        object instant = Dm.arg<object>(__extra, 2) ?? FALSE;
        object start_delay = Dm.arg<object>(__extra, 3) ?? 0;
        object drift_force = Dm.arg<object>(__extra, 4) ?? 1;
        await base.New();
        this.parent = parent;
        parent.drift_handler = this;
        dm_blown = Dm.num(world.time) - parent.dm_blown_at <= atom_movable.BlownWindow;
        float flags = MOVEMENT_LOOP_OUTSIDE_CONTROL;
        if (Dm.truthy(instant))
            flags = Dm.bitor(flags, MOVEMENT_LOOP_START_INSTANT);
        this.drift_force = Dm.num(drift_force);
        drifting_loop = GLOB.move_manager.smooth_move(
            moving: parent,
            angle: inertia_angle,
            delay: get_loop_delay(parent),
            subsystem: SSnewtonian_movement,
            priority: MOVEMENT_SPACE_PRIORITY,
            flags: flags) as datum_move_loop_smooth_move;

        if (drifting_loop == null)
        {
            qdel(this);
            return;
        }

        RegisterSignal(drifting_loop, COMSIG_MOVELOOP_START, nameof(drifting_start));
        RegisterSignal(drifting_loop, COMSIG_MOVELOOP_STOP, nameof(drifting_stop));
        RegisterSignal(drifting_loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(before_move));
        RegisterSignal(drifting_loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(after_move));
        RegisterSignal(drifting_loop, COMSIG_QDELETING, nameof(loop_death));
        if (Dm.truthy(Dm.bitand(drifting_loop.status, MOVELOOP_STATUS_RUNNING)))
            drifting_start(drifting_loop); // There's a good chance it'll autostart, gotta catch that

        object visual_delay = get_loop_delay(parent);

        // Start delay is essentially a more granular version of instant
        // Isn't used in the standard case, just for things that have odd wants
        if (!Dm.truthy(instant) && Dm.truthy(start_delay))
        {
            drifting_loop.pause_for(start_delay);
            visual_delay = start_delay;
        }

        apply_initial_visuals(visual_delay);
        // Fire the engines!
        if (Dm.num(drifting_loop.timer) <= world.time)
            SSnewtonian_movement.fire_moveloop(drifting_loop);
    }

    // tg: code/datums/drift_handler.dm:61
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(drifting_loop))
            qdel(drifting_loop);
        drifting_loop = null;
        parent.inertia_moving = FALSE;
        parent.drift_handler = null;
        return base.Destroy(force);
    }

    // tg: code/datums/drift_handler.dm:69
    public virtual void apply_initial_visuals(object visual_delay = null)
    {
        // If something "somewhere" doesn't want us to apply our glidesize delays, don't
        if (Dm.truthy(Dm.bitand((!Dm.truthy(parent._listen_lookup?[COMSIG_MOVABLE_DRIFT_VISUAL_ATTEMPT]) ? NONE : parent._SendSignal(COMSIG_MOVABLE_DRIFT_VISUAL_ATTEMPT, Dm.list(parent))), DRIFT_VISUAL_FAILED)))
            return;

        // Ignore the next glide because it's literally just us
        ignore_next_glide = TRUE;
        parent.set_glide_size(MOVEMENT_ADJUSTED_GLIDE_SIZE(Dm.num(visual_delay), SSnewtonian_movement.visual_delay));
        if (!ismob(parent))
            return;
        mob mob_parent = parent as mob;
        //Ok this is slightly weird, but basically, we need to force the client to glide at our rate
        //Make sure moving into a space move looks like a space move essentially
        //There is an inbuilt assumption that gliding will be added as a part of a move call, but eh
        //It's ok if it's not, it's just important if it is.
        if (mob_parent.client != null)
            mob_parent.client.visual_delay = MOVEMENT_ADJUSTED_GLIDE_SIZE(Dm.num(visual_delay), SSnewtonian_movement.visual_delay);
    }

    /**
     * An impulse is being applied to this existing drift, react accordingly
     *
     * * inertia_angle - angle of the new impulse
     * * start_delay - if the new impulse has a delay before it starts, this is it
     * * additional_force - how much force the new impulse has
     * force is not added onto additional force, it will either override it entirely (if larger or a different direction) or be ignored (if smaller and same direction)
     * controlled_cap - the maximum amount of force this impulse can apply, regardless of input
     * force_loop - should we force the loop to fire immediately to react to this change, or wait for the next visual tick?
     * Generally, if the new impulse has a start delay, you should wait, otherwise it'll look really jank
     *
     * Return FALSE if the loop becomes invalid and should be replaced
     * Return TRUE if the loop is still valid and should be kept
     */
    // tg: code/datums/drift_handler.dm:100
    public virtual object newtonian_impulse(object inertia_angle = null, object start_delay = null, object additional_force = null, float? controlled_cap = null, float? force_loop = null)
    {
        controlled_cap ??= INERTIA_FORCE_CAP;
        force_loop ??= TRUE;
        // We've been told to move in the middle of deletion process, tell parent to create a new handler instead
        if (drifting_loop == null)
        {
            qdel(this);
            return FALSE;
        }

        float new_force = clamp(Dm.num(additional_force) / parent.inertia_force_weight, 0, Dm.num(controlled_cap));
        if (new_force < drift_force && Dm.eq(drifting_loop.angle, inertia_angle)) // If we're already moving faster in this direction, don't change anything
            return TRUE;

        drift_force = new_force;
        if (drift_force < 0.1f) // Rounding issues
        {
            qdel(this);
            return TRUE;
        }

        drifting_loop.set_angle(inertia_angle);
        drifting_loop.set_delay(get_loop_delay(parent));
        // We have to forcefully fire it here to avoid stuttering in case of server lag
        if (Dm.num(drifting_loop.timer) <= world.time && Dm.truthy(force_loop))
            SSnewtonian_movement.fire_moveloop(drifting_loop);
        return TRUE;
    }

    // tg: code/datums/drift_handler.dm:123  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void drifting_start(datum source = null)
    {
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(handle_move));
        // We will use glide size to intuit how long to delay our loop's next move for
        // This way you can't ride two movements at once while drifting, since that'd be dumb as fuck
        RegisterSignal(parent, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, nameof(handle_glidesize_update));
        // If you stop pulling something mid drift, I want it to retain that momentum
        RegisterSignal(parent, COMSIG_ATOM_NO_LONGER_PULLING, nameof(stopped_pulling));
    }

    // tg: code/datums/drift_handler.dm:132  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void drifting_stop(datum source = null)
    {
        parent.inertia_moving = FALSE;
        ignore_next_glide = FALSE;
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, COMSIG_ATOM_NO_LONGER_PULLING));
    }

    // tg: code/datums/drift_handler.dm:138  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void before_move(datum source = null)
    {
        parent.inertia_moving = TRUE;
        old_dir = parent.dir;
        delayed = FALSE;
    }

    // tg: code/datums/drift_handler.dm:144  (SIGNAL_HANDLER is dropped, rules 7.9; Process_Spacemove of a mob may reach
    // a push-off's newtonian_move, which does not sleep, so it runs through Waiting.NoSleep)
    public virtual void after_move(datum source = null, object result = null, object visual_delay = null)
    {
        if (Dm.eq(result, MOVELOOP_FAILURE))
        {
            qdel(this);
            return;
        }

        Waiting.NoSleep(parent.setDir(old_dir));
        parent.inertia_moving = FALSE;
        if (Dm.truthy(Waiting.NoSleep(parent.Process_Spacemove(Dm.num(angle2dir(Dm.num(drifting_loop.angle))), continuous_move: TRUE))))
        {
            glide_to_halt(visual_delay);
            return;
        }

        ignore_next_glide = TRUE;
    }

    // tg: code/datums/drift_handler.dm:158  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void loop_death(datum source = null)
    {
        drifting_loop = null;
    }

    // tg: code/datums/drift_handler.dm:162  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void handle_move(datum source = null, atom old_loc = null)
    {
        // This can happen, because signals once sent cannot be stopped
        if (QDELETED(this))
            return;
        if (!isturf(parent.loc))
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(parent.inertia_moving))
            return;
        if (!Dm.truthy(Waiting.NoSleep(parent.Process_Spacemove(Dm.num(angle2dir(Dm.num(drifting_loop.angle))), continuous_move: TRUE))))
            return;
        qdel(this);
    }

    /// We're going to take the passed in glide size
    /// and use it to manually delay our loop for that period
    /// to allow the other movement to complete
    // tg: code/datums/drift_handler.dm:178  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void handle_glidesize_update(datum source = null, object glide_size = null)
    {
        // If we aren't drifting, or this is us, fuck off
        if (drifting_loop == null || Dm.truthy(parent.inertia_moving))
            return;
        // If we are drifting, but this set came from the moveloop itself, drop the input
        // I'm sorry man
        if (Dm.truthy(ignore_next_glide))
        {
            ignore_next_glide = FALSE;
            return;
        }
        float glide_delay = round(ICON_SIZE_ALL / Dm.num(glide_size), 1) * world.tick_lag;
        drifting_loop.pause_for(glide_delay);
        delayed = TRUE;
    }

    /// If we're pulling something and stop, we want it to continue at our rate and such
    // tg: code/datums/drift_handler.dm:194  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void stopped_pulling(datum source = null, atom_movable was_pulling = null)
    {
        // This does mean it falls very slightly behind, but otherwise they'll potentially run into us
        float next_move_in = Dm.num(drifting_loop.timer) - world.time + world.tick_lag;
        Waiting.NoSleep(was_pulling.newtonian_move(angle2dir(Dm.num(drifting_loop.angle)), start_delay: next_move_in, drift_force: drift_force, controlled_cap: drift_force));
    }

    // tg: code/datums/drift_handler.dm:200
    public virtual void glide_to_halt(object glide_for = null)
    {
        if (!ismob(parent))
        {
            qdel(this);
            return;
        }

        mob mob_parent = parent as mob;
        client our_client = mob_parent.client;
        // If we're not active, don't do the glide because it'll look dumb as fuck
        if (our_client == null || Dm.truthy(delayed))
        {
            qdel(this);
            return;
        }

        block_inputs_until = world.time + Dm.num(glide_for) + 1;
        // QDEL_IN(src, glide_for + 1) (__DEFINES/qdel.dm:59), expanded inline (rules 11.1 (f))
        float time__1 = Dm.num(glide_for) + 1;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (time__1 > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this)), time__1, file: "code/datums/drift_handler.dm", line: 212);
        qdel(drifting_loop);
        RegisterSignal(parent, COMSIG_MOB_CLIENT_PRE_MOVE, nameof(allow_final_movement));
    }

    // tg: code/datums/drift_handler.dm:217  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual object allow_final_movement(datum source = null)
    {
        // Some things want to allow movement out of spacedrift, we should let them
        if (Dm.truthy(Dm.bitand((!Dm.truthy(parent._listen_lookup?[COMSIG_MOVABLE_DRIFT_BLOCK_INPUT]) ? NONE : parent._SendSignal(COMSIG_MOVABLE_DRIFT_BLOCK_INPUT, Dm.list(parent))), DRIFT_ALLOW_INPUT)))
            return NONE;
        if (world.time >= Dm.num(block_inputs_until))
            return NONE;
        return COMSIG_MOB_CLIENT_BLOCK_PRE_MOVE;
    }

    // tg: code/datums/drift_handler.dm:225
    public virtual object get_loop_delay(atom_movable movable = null)
    {
        return (DEFAULT_INERTIA_SPEED / ((1 - INERTIA_SPEED_COEF) + drift_force * INERTIA_SPEED_COEF)) * min(movable.inertia_move_multiplier_passive, movable.inertia_move_multiplier_active);
    }
}
