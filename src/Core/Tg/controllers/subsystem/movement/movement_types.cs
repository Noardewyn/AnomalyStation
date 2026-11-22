// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/movement/movement_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 1: the zero-gravity drift): the template /datum/move_loop (:1-196), /datum/move_manager/proc/stop_looping
// (:174) and the smooth move (:985-1076, the drift's loop) translated; M4b slice 3 (a slip's slide, the force_move
// component): /datum/move_loop/has_target (:247-275) and move_towards with its helpers move_towards and home_onto (:673-831);
// M4b, the extinguisher: /datum/move_loop/has_target/move_towards_budget (:833-845); M4b, the extinguisher, and M4c slice 6
// (the effect systems' particles, effect_system.dm:106): move and force_move_dir with /datum/move_loop/move and
// /datum/move_loop/move/force (:195-245); M6 slice 5 (the gibs' streak): /datum/move_loop/has_target/dist_bound and move_to (:562-625). The other loops (force_move, jps, move_away, the conveyors and the rest) come with their users. The EVLOG_TEXT and EVLOG_PATH lines are the admin event logger's
// (__DEFINES/event_logger.dm: GLOB.event_logger, which runs only while an admin records, M10): their condition is false,
// so they are left out at the site.

///Template class of the movement datums, handles the timing portion of the loops
// tg: code/controllers/subsystem/movement/movement_types.dm:2  (the other vars are generated, D111)
public partial class datum_move_loop : datum
{
    ///The subsystem we're processing on
    // tg: code/controllers/subsystem/movement/movement_types.dm:6
    public datum_controller_subsystem_movement controller;

    // tg: code/controllers/subsystem/movement/movement_types.dm:31
    [DmParams("owner", "controller", "moving", "priority", "flags", "extra_info")]
    public override async DmTask New(params object[] __extra)
    {
        datum_movement_packet owner = Dm.arg<datum_movement_packet>(__extra, 0);
        datum_controller_subsystem_movement controller = Dm.arg<datum_controller_subsystem_movement>(__extra, 1);
        atom_movable moving = Dm.arg<atom_movable>(__extra, 2);
        object priority = Dm.arg<object>(__extra, 3);
        object flags = Dm.arg<object>(__extra, 4);
        datum extra_info = Dm.arg<datum>(__extra, 5);
        this.owner = owner;
        this.controller = controller;
        this.extra_info = extra_info;
        if (extra_info != null)
            RegisterSignal(extra_info, COMSIG_QDELETING, nameof(info_deleted));
        this.moving = moving;
        this.priority = Dm.num(priority);
        this.flags = flags;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:41  (the subtypes' own setup arguments follow in __extra)
    public virtual object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        delay ??= 1;
        timeout ??= INFINITY;
        if (!ismovable(moving) || owner == null)
            return FALSE;

        this.delay = max(Dm.num(delay), world.tick_lag); //Please...
        this.lifetime = Dm.num(timeout);
        return TRUE;
    }

    ///check if this exact moveloop datum already exists (in terms of vars) so we can avoid creating a new one to overwrite the old duplicate
    // tg: code/controllers/subsystem/movement/movement_types.dm:50  (the subtypes' own arguments follow in __extra)
    public virtual object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        delay ??= 1;
        timeout ??= INFINITY;
        // SHOULD_CALL_PARENT(TRUE) is dropped (rules 7.9)
        if (Dm.eq(loop_type, type) && Dm.eq(priority, this.priority) && Dm.eq(flags, this.flags) && Dm.eq(delay, this.delay) && Dm.eq(timeout, lifetime))
            return TRUE;
        return FALSE;
    }

    ///Called when a loop is starting by a movement subsystem
    // tg: code/controllers/subsystem/movement/movement_types.dm:57
    public virtual void loop_started()
    {
        // SHOULD_CALL_PARENT(TRUE) is dropped (rules 7.9)
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_START]))
            _SendSignal(COMSIG_MOVELOOP_START, Dm.list(this));
        Dm.combine(ref status, MOVELOOP_STATUS_RUNNING);
        //If this is our first time starting to move with this loop
        //And we want to start consistently fast
        if (!Dm.truthy(timer) && Dm.truthy(Dm.bitand(flags, MOVEMENT_LOOP_START_FAST)))
        {
            // + tick_lag because we want to avoid weird jumping in atoms that were just created (and avoid inconsistencies around subsystem timing)
            timer = NEXT_VISUAL_TICK() + world.tick_lag;
            return;
        }
        //And we're meant to start instantly
        if (!Dm.truthy(timer) && Dm.truthy(Dm.bitand(flags, MOVEMENT_LOOP_START_INSTANT)))
        {
            timer = NEXT_VISUAL_TICK();
            return;
        }
        timer = NEXT_VISUAL_TICK() + delay;
    }

    ///Called when a loop is stopped, doesn't stop the loop itself
    // tg: code/controllers/subsystem/movement/movement_types.dm:74
    public virtual void loop_stopped()
    {
        // SHOULD_CALL_PARENT(TRUE) is dropped (rules 7.9)
        Dm.mask(ref status, Dm.bitnot(MOVELOOP_STATUS_RUNNING));
        // EVLOG_TEXT(moving, EVLOG_CATEGORY_MOVELOOPS, "Moveloop stopped"): the admin event logger (file header)
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_STOP]))
            _SendSignal(COMSIG_MOVELOOP_STOP, Dm.list(this));
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:80  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void info_deleted(datum source = null)
    {
        extra_info = null;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:84
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (owner != null)
            owner.remove_loop(controller, this);
        owner = null;
        moving = null;
        controller = null;
        extra_info = null;
        return base.Destroy(force);
    }

    ///Exists as a helper so outside code can modify delay in a sane way
    // tg: code/controllers/subsystem/movement/movement_types.dm:94
    public virtual void set_delay(object new_delay = null)
    {
        delay = max(Dm.num(new_delay), world.tick_lag);
    }

    ///Pauses the move loop for some passed in period
    ///This functionally means shifting its timer up, and clearing it from its current bucket
    // tg: code/controllers/subsystem/movement/movement_types.dm:99
    public virtual void pause_for(object time = null)
    {
        if (controller == null || !Dm.truthy(Dm.bitand(status, MOVELOOP_STATUS_RUNNING))) //No controller or not running? go away
            return;
        //Dequeue us from our current bucket
        controller.dequeue_loop(this);
        //Offset our timer
        timer = world.time + Dm.num(time);
        //Now requeue us with our new target start time
        controller.queue_loop(this);
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:109  (/datum/process: the move loop's step, called by its
    // subsystem; nothing in it sleeps)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (controller == null)
        {
            qdel(this);
            return null;
        }

        float old_delay = delay; //The signal can sometimes change delay

        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_PREPROCESS_CHECK]) ? NONE : _SendSignal(COMSIG_MOVELOOP_PREPROCESS_CHECK, Dm.list(this))), MOVELOOP_SKIP_STEP))) //Chance for the object to react
            return null;

        lifetime -= old_delay; //This needs to be based on work over time, not just time passed

        if (lifetime < 0) //Otherwise lag would make things look really weird
        {
            qdel(this);
            return null;
        }

        float visual_delay = controller.visual_delay;
        object old_dir = moving.dir;
        atom old_loc = moving.loc;

        if (owner != null)
            owner.processing_move_loop_flags = Dm.num(flags);
        object result = move(); //Result is an enum value. Enums defined in __DEFINES/movement.dm

        if (moving != null)
        {
            object direction = get_dir(old_loc, moving.loc);
            if (Dm.truthy(moving._listen_lookup?[COMSIG_MOVABLE_MOVED_FROM_LOOP]))
                moving._SendSignal(COMSIG_MOVABLE_MOVED_FROM_LOOP, Dm.list(moving, this, old_dir, direction));
            // if(result) EVLOG_PATH(moving, EVLOG_CATEGORY_MOVELOOPS, ...): the admin event logger (file header)
        }
        if (owner != null)
            owner.processing_move_loop_flags = NONE;

        if (Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_POSTPROCESS]))
            _SendSignal(COMSIG_MOVELOOP_POSTPROCESS, Dm.list(this, result, delay * visual_delay));

        if (QDELETED(this) || !Dm.eq(result, MOVELOOP_SUCCESS)) //Can happen
            return null;

        if (Dm.truthy(Dm.bitand(flags, MOVEMENT_LOOP_IGNORE_GLIDE)))
            return null;

        // a runtime in tg: pre_move sets an unclamped delay, and a fast mob's is 0 (a statue: speed -1 plus the walk's 1): "Division by zero" after each step
        moving.set_glide_size(MOVEMENT_ADJUSTED_GLIDE_SIZE(delay > 0 ? delay : world.tick_lag, visual_delay));
        return null;
    }

    ///Handles the actual move, overriden by children
    ///Returns FALSE if nothing happen, TRUE otherwise
    // tg: code/controllers/subsystem/movement/movement_types.dm:151
    public virtual object move()
    {
        return MOVELOOP_FAILURE;
    }

    ///Pause our loop untill restarted with resume_loop()
    // tg: code/controllers/subsystem/movement/movement_types.dm:156
    public virtual void pause_loop()
    {
        if (controller == null || !Dm.truthy(Dm.bitand(status, MOVELOOP_STATUS_RUNNING)) || Dm.truthy(Dm.bitand(status, MOVELOOP_STATUS_PAUSED))) //we dead
            return;

        //Dequeue us from our current bucket
        controller.dequeue_loop(this);
        Dm.combine(ref status, MOVELOOP_STATUS_PAUSED);
    }

    ///Resume our loop after being paused by pause_loop()
    // tg: code/controllers/subsystem/movement/movement_types.dm:165
    public virtual void resume_loop()
    {
        if (controller == null || Dm.bitand(status, Dm.bitor(MOVELOOP_STATUS_RUNNING, MOVELOOP_STATUS_PAUSED)) != Dm.bitor(MOVELOOP_STATUS_RUNNING, MOVELOOP_STATUS_PAUSED))
            return;

        timer = world.time;
        controller.queue_loop(this);
        Dm.mask(ref status, Dm.bitnot(MOVELOOP_STATUS_PAUSED));
    }
}

// tg: code/controllers/subsystem/movement/movement_types.dm:174  (interleaved types, rules 1.9)
public partial class datum_move_manager : datum
{
    ///Removes the atom from some movement subsystem. Defaults to SSmovement
    // tg: code/controllers/subsystem/movement/movement_types.dm:174
    public virtual object stop_looping(atom_movable moving = null, datum_controller_subsystem_movement subsystem = null)
    {
        subsystem ??= SSmovement;
        datum_movement_packet our_info = moving.move_packet;
        if (our_info == null)
            return FALSE;
        return our_info.remove_subsystem(subsystem);
    }

    /**
     * Helper proc for the smooth_move datum
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * angle - Angle at which we want to move
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to INFINITY
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:1003
    public virtual object smooth_move(atom_movable moving = null, object angle = null, object delay = null, object timeout = null, datum_controller_subsystem_movement subsystem = null, float? priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving, subsystem, typeof(datum_move_loop_smooth_move), priority, flags, extra_info, delay, timeout, angle);
    }
}

public partial class datum_move_manager : datum
{
    /**
     * Replacement for walk()
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * direction - The direction we want to move in
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to infinity
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:195  (M4b: the extinguisher's push on an unanchored chair)
    public virtual object move(object moving = null, object direction = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_move), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, direction);
    }

    /**
     * Like move(), but we don't care about collision at all
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * direction - The direction we want to move in
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to infinity
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:235
    public virtual object force_move_dir(object moving = null, object direction = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_move_force), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, direction);
    }
}

///Replacement for walk()
// tg: code/controllers/subsystem/movement/movement_types.dm:199  (the var is generated, D111; M4c slice 6)
public partial class datum_move_loop_move : datum_move_loop
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:202  (dir is the loop's own argument, after timeout)
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object dir = Dm.arg<object>(__extra, 0);
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return dot;
        direction = dir;
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:208
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        object dir = Dm.arg<object>(__extra, 0);
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && Dm.eq(direction, dir))
            return TRUE;
        return FALSE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:213
    public override object move()
    {
        atom old_loc = moving.loc;
        Waiting.NoSleep(moving.Move(get_step(moving, Dm.num(direction)), direction, FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));
        // We cannot rely on the return value of Move(), we care about teleports and it doesn't
        // Moving also can be null on occasion, if the move deleted it and therefor us
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }
}

// tg: code/controllers/subsystem/movement/movement_types.dm:239  (M4c slice 6)
public partial class datum_move_loop_move_force : datum_move_loop_move
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:241
    public override object move()
    {
        atom old_loc = moving.loc;
        Waiting.NoSleep(moving.forceMove(get_step(moving, Dm.num(direction))));
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }
}

// tg: code/controllers/subsystem/movement/movement_types.dm:247  (the var is generated, D111; M4b slice 3)
public partial class datum_move_loop_has_target : datum_move_loop
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:251  (chasing is the loop's own argument, after timeout)
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object chasing = Dm.arg<object>(__extra, 0);
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (!isatom(chasing))
        {
            qdel(this);
            return FALSE;
        }

        target = (atom)chasing;

        if (!isturf(target))
            RegisterSignal(target, COMSIG_QDELETING, nameof(handle_no_target)); //Don't do this for turfs, because we don't care
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:264
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        object chasing = Dm.arg<object>(__extra, 0);
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && Dm.eq(chasing, target))
            return TRUE;
        return FALSE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:269
    public override object Destroy(float? force = null)
    {
        target = null;
        return base.Destroy(force);
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:273
    [SignalHandler]
    public virtual void handle_no_target()
    {
        // SIGNAL_HANDLER
        qdel(this);
    }
}

///Base class of move_to and move_away, deals with the distance and target aspect of things
// tg: code/controllers/subsystem/movement/movement_types.dm:564  (M6 slice 5, the gibs' streak; the var is generated, D111)
public partial class datum_move_loop_has_target_dist_bound : datum_move_loop_has_target
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:567  (dist is the loop's own argument, after chasing)
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object dist = Dm.arg<object>(__extra, 1) ?? 0f;
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return dot;
        distance = Dm.num(dist);
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:573
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        object dist = Dm.arg<object>(__extra, 1) ?? 0f;
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && distance == Dm.num(dist))
            return TRUE;
        return FALSE;
    }

    ///Returns FALSE if the movement should pause, TRUE otherwise
    // tg: code/controllers/subsystem/movement/movement_types.dm:579
    public virtual object check_dist()
    {
        return FALSE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:582
    public override object move()
    {
        if (!Dm.truthy(check_dist())) //If we're too close don't do the move
            return MOVELOOP_FAILURE;
        return MOVELOOP_SUCCESS;
    }
}

public partial class datum_move_manager : datum
{
    /**
     * Wrapper around walk_to()
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * chasing - The atom we want to move towards
     * min_dist - the closest we're allower to get to the target
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to infinity
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:604
    public virtual object move_to(object moving = null, object chasing = null, object min_dist = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_has_target_dist_bound_move_to), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, chasing, min_dist);
    }
}

///Wrapper around walk_to()
// tg: code/controllers/subsystem/movement/movement_types.dm:608  (M6 slice 5)
public partial class datum_move_loop_has_target_dist_bound_move_to : datum_move_loop_has_target_dist_bound
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:610
    public override object check_dist()
    {
        return get_dist(moving, target) > distance ? TRUE : FALSE; //If you get too close, stop moving closer
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:613
    public override object move()
    {
        object dot = base.move();
        if (!Dm.truthy(dot))
            return dot;
        atom old_loc = moving.loc;
        turf next = get_step_to(moving, target);
        Waiting.NoSleep(moving.Move(next, get_dir(moving, next), FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }
}

public partial class datum_move_manager : datum
{
    ///Used for force-move loops
    // tg: code/controllers/subsystem/movement/movement_types.dm:673
    public virtual object move_towards(object moving = null, object chasing = null, object delay = null, object home = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_has_target_move_towards), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, chasing, home);
    }

    /**
     * Helper proc for homing onto something with move_towards
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * chasing - The atom we want to move towards
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * home - Should we move towards the object at all times? Or launch towards them, but allow walls and such to take us off track. Defaults to FALSE
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to INFINITY
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:692
    public virtual object home_onto(object moving = null, object chasing = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return move_towards(moving, chasing, delay, TRUE, timeout, subsystem, priority, flags, extra_info);
    }
}

public partial class datum_move_manager : datum
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:638
    public virtual object move_away(object moving = null, object chasing = null, object max_dist = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, object extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_has_target_dist_bound_move_away), priority == null ? null : Dm.num(priority), flags, extra_info as datum, delay, timeout, chasing, max_dist);
    }
}

///Wrapper around walk_away()
// tg: code/controllers/subsystem/movement/movement_types.dm:642
public partial class datum_move_loop_has_target_dist_bound_move_away : datum_move_loop_has_target_dist_bound
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:644
    public override object check_dist()
    {
        return get_dist(moving, target) < distance ? TRUE : FALSE; //If you get too far out, stop moving away
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:647
    public override object move()
    {
        object dot = base.move();
        if (!Dm.truthy(dot))
            return dot;
        atom old_loc = moving.loc;
        turf next = get_step_away(moving, target);
        Waiting.NoSleep(moving.Move(next, get_dir(moving, next), FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }
}

///Used as a alternative to walk_towards
// tg: code/controllers/subsystem/movement/movement_types.dm:696  (the vars are generated, D111; M4b slice 3)
public partial class datum_move_loop_has_target_move_towards : datum_move_loop_has_target
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:712  (chasing and home are the loop's own arguments)
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object home = Dm.arg<object>(__extra, 1) ?? FALSE;
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        this.home = Dm.num(home);

        if (Dm.truthy(this.home))
        {
            if (ismovable(target))
                RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(update_slope)); //If it can move, update your slope when it does
            RegisterSignal(moving, COMSIG_MOVABLE_MOVED, nameof(handle_move));
        }
        update_slope();
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:724
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        object home = Dm.arg<object>(__extra, 1) ?? FALSE;
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && Dm.eq(home, this.home))
            return TRUE;
        return FALSE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:729
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(home))
        {
            if (ismovable(target))
                UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
            if (Dm.truthy(moving))
                UnregisterSignal(moving, COMSIG_MOVABLE_MOVED);
        }
        return base.Destroy(force);
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:737
    public override object move()
    {
        //Move our tickers forward a step, we're guaranteed at least one step forward because of how the code is written
        if (x_rate != 0) //Did you know that rounding by 0 throws a divide by 0 error?
            x_ticker = FLOOR(x_ticker + x_rate, x_rate);
        if (y_rate != 0)
            y_ticker = FLOOR(y_ticker + y_rate, y_rate);

        float x = moving.x;
        float y = moving.y;
        float z = moving.z;

        moving_towards = locate(x + round(x_ticker) * x_sign, y + round(y_ticker) * y_sign, z);
        //The tickers serve as good methods of tracking remainder
        if (x_ticker >= 1)
            x_ticker = MODULUS(x_ticker, 1); //I swear to god if you somehow go up by one then one in a tick I'm gonna go mad
        if (y_ticker >= 1)
            y_ticker = MODULUS(x_ticker, 1);
        atom old_loc = moving.loc;
        Waiting.NoSleep(moving.Move(moving_towards, get_dir(moving, moving_towards), FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));

        //YOU FOUND THEM! GOOD JOB
        if (Dm.truthy(home) && get_turf(moving) == get_turf(target))
        {
            x_rate = 0;
            y_rate = 0;
            return null;
        }
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:764
    [SignalHandler]
    public virtual void handle_move(object source = null, atom OldLoc = null, object Dir = null, float? Forced = null)
    {
        // SIGNAL_HANDLER
        if (QDELETED(this))
            return;

        if (moving.loc != moving_towards && Dm.truthy(home)) //If we didn't go where we should have, update slope to account for the deviation
            update_slope();
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:772
    public override void handle_no_target()
    {
        if (Dm.truthy(home))
        {
            base.handle_no_target();
            return;
        }
        target = null;
    }

    /**
     * Recalculates the slope between our object and the target, sets our rates to it
     *
     * The math below is reminiscent of something like y = mx + b
     * Except we don't need to care about axis, since we do all our movement in steps of 1
     * Because of that all that matters is we only move one tile at a time
     * So we take the smaller delta, divide it by the larger one, and get smaller step per large step
     * Then we set the large step to 1, and we're done. This way we're guaranteed to never move more then a tile at once
     * And we can have nice lines
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:787
    [SignalHandler]
    public virtual void update_slope()
    {
        // SIGNAL_HANDLER
        if (QDELETED(this))
            return;

        //You'll notice this is rise over run, except we flip the formula upside down depending on the larger number
        //This is so we never move more then one tile at once
        float delta_y = target.y - moving.y;
        float delta_x = target.x - moving.x;
        //It's more convienent to store delta x and y as absolute values
        //and modify them right at the end then it is to deal with rounding errors
        x_sign = (delta_x > 0) ? 1 : -1;
        y_sign = (delta_y > 0) ? 1 : -1;
        delta_x = abs(delta_x);
        delta_y = abs(delta_y);

        if (delta_x >= delta_y)
        {
            if (delta_x == 0) //Just go up/down
            {
                x_rate = 0;
                y_rate = 1;
                return;
            }
            x_rate = 1;
            y_rate = delta_y / delta_x; //rise over run, you know the deal
        }
        else
        {
            if (delta_y == 0) //Just go right/left
            {
                x_rate = 1;
                y_rate = 0;
                return;
            }
            x_rate = delta_x / delta_y; //Keep the larger step size at 1
            y_rate = 1;
        }
    }
}

public partial class datum_move_manager : datum
{
    /**
     * Wrapper around walk_towards, not recommended, as it's movement ends up being a bit stilted
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * chasing - The atom we want to move towards
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to infinity
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:833  (M4b: the extinguisher's water)
    public virtual object move_towards_legacy(object moving = null, object chasing = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_has_target_move_towards_budget), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, chasing);
    }
}

///The actual implementation of walk_towards()
// tg: code/controllers/subsystem/movement/movement_types.dm:837  (M4b: the extinguisher's water)
public partial class datum_move_loop_has_target_move_towards_budget : datum_move_loop_has_target
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:839
    public override object move()
    {
        turf target_turf = get_step_towards(moving, target);
        atom old_loc = moving.loc;
        Waiting.NoSleep(moving.Move(target_turf, get_dir(moving, target_turf), FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }
}

// tg: code/controllers/subsystem/movement/movement_types.dm:1006  (the vars are generated, D111)
public partial class datum_move_loop_smooth_move : datum_move_loop
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:1021  (angle is the loop's own argument, after timeout)
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object angle = Dm.arg<object>(__extra, 0);
        object dot = base.setup(delay, timeout);
        if (!Dm.truthy(dot))
            return FALSE;
        set_angle(angle);
        saved_delay = delay;
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:1028
    public override void set_delay(object new_delay = null)
    {
        new_delay = round(Dm.num(new_delay), world.tick_lag);
        base.set_delay(new_delay);
        saved_delay = delay;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:1033  (tg names its own arguments chasing and home, and ..()
    // passes them on; no argument is named angle, so `angle == src.angle` reads src.angle twice and is always true, as in tg)
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && Dm.eq(angle, this.angle))
            return TRUE;
        return FALSE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:1038
    public override object move()
    {
        // DEVIATION(DV69): a weightless capsule mob's drift is moved by the locomotion bridge, continuously at this loop's angle and
        // delay (LocomotionSeam.DriftOf, DriftMove); the loop keeps its timing and its post-process checks (the drift handler's halt).
        if (LocomotionSeam.DriftsContinuously(moving))
            return MOVELOOP_SUCCESS;
        atom old_loc = moving.loc;
        // Defaulting to 2 because if one rate is 0 the other is guaranteed to be 1, so maxing out at 1 to_move
        float x_to_move = x_rate > 0 ? (1 - x_ticker) / x_rate : 2;
        float y_to_move = y_rate > 0 ? (1 - y_ticker) / y_rate : 2;
        float move_dist = min(x_to_move, y_to_move);
        x_ticker += x_rate * move_dist;
        y_ticker += y_rate * move_dist;

        // Per Bresenham's, if we are closer to the next tile's center move diagonally. Checked by seeing if we pass into the next tile after moving another half a tile
        bool move_x = (x_ticker + x_rate * 0.5f) > 1;
        bool move_y = (y_ticker + y_rate * 0.5f) > 1;
        if (move_x)
            x_ticker = 0;
        if (move_y)
            y_ticker = 0;

        turf next_turf = locate(moving.x + (move_x ? x_sign : 0), moving.y + (move_y ? y_sign : 0), moving.z);
        Waiting.NoSleep(moving.Move(next_turf, get_dir(moving, next_turf), FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));

        if (old_loc == moving?.loc)
            return MOVELOOP_FAILURE;

        delay = Dm.num(saved_delay);
        if (move_x && move_y)
            delay *= 1.4f;

        return MOVELOOP_SUCCESS;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:1067
    public virtual void set_angle(object new_angle = null)
    {
        angle = Dm.num(new_angle);
        x_rate = sin(Dm.num(angle));
        y_rate = cos(Dm.num(angle));
        x_sign = sign(x_rate);
        y_sign = sign(y_rate);
        x_rate = abs(x_rate);
        y_rate = abs(y_rate);
        x_ticker = 0;
        y_ticker = 0;
    }
}

// Gaps package C1 (the AI's movement): /datum/move_loop/has_target/jps and freeze; C3: the mulebot's frustrations loop.
public partial class datum_move_manager : datum
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:327
    public virtual object jps_move(object moving = null, object chasing = null, object delay = null, object timeout = null, object repath_delay = null, object max_path_length = null, object minimum_distance = null, DmList access = null, object simulated_only = null, turf avoid = null, object skip_first = null, object subsystem = null, object diagonal_handling = null, object priority = null, object flags = null, datum extra_info = null, DmList initial_path = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_has_target_jps), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, chasing, repath_delay, max_path_length, minimum_distance, access, simulated_only, avoid, skip_first, diagonal_handling, initial_path);
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:491
    public virtual object frustrations_move(object moving = null, object chasing = null, object delay = null, object timeout = null, object repath_delay = null, object max_path_length = null, object minimum_distance = null, DmList access = null, object simulated_only = null, turf avoid = null, object skip_first = null, object subsystem = null, object diagonal_handling = null, object priority = null, object flags = null, datum extra_info = null, DmList initial_path = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_has_target_jps_frustrations), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, chasing, repath_delay, max_path_length, minimum_distance, access, simulated_only, avoid, skip_first, diagonal_handling, initial_path);
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:859
    public virtual object freeze(object moving = null, object halted_turf = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_freeze), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, halted_turf);
    }
}

// tg: code/controllers/subsystem/movement/movement_types.dm:363
public partial class datum_move_loop_has_target_jps : datum_move_loop_has_target
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:389
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.append(ref on_finish_callbacks, Dm.@new<datum_callback>(this, nameof(on_finish_pathing)));
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:393
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object repath_delay = Dm.arg(__extra, 1);
        object max_path_length = Dm.arg(__extra, 2);
        object minimum_distance = Dm.arg(__extra, 3);
        DmList access = Dm.arg<DmList>(__extra, 4);
        object simulated_only = Dm.arg(__extra, 5);
        turf avoid = Dm.arg<turf>(__extra, 6);
        object skip_first = Dm.arg(__extra, 7);
        object diagonal_handling = Dm.arg(__extra, 8);
        DmList initial_path = Dm.arg<DmList>(__extra, 9);
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return dot;
        this.repath_delay = repath_delay;
        this.max_path_length = max_path_length;
        this.minimum_distance = minimum_distance;
        this.access = access;
        this.simulated_only = simulated_only;
        this.avoid = avoid;
        this.skip_first = skip_first;
        this.diagonal_handling = diagonal_handling;
        movement_path = initial_path?.Copy();
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:407
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        object repath_delay = Dm.arg(__extra, 1);
        object max_path_length = Dm.arg(__extra, 2);
        object minimum_distance = Dm.arg(__extra, 3);
        DmList access = Dm.arg<DmList>(__extra, 4);
        object simulated_only = Dm.arg(__extra, 5);
        turf avoid = Dm.arg<turf>(__extra, 6);
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && Dm.eq(repath_delay, this.repath_delay) && Dm.eq(max_path_length, this.max_path_length) && Dm.eq(minimum_distance, this.minimum_distance) && Dm.equiv(access, this.access) && Dm.eq(simulated_only, this.simulated_only) && avoid == this.avoid)
            return TRUE;
        return FALSE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:412
    public override void loop_started()
    {
        base.loop_started();
        if (movement_path == null)
            Waiting.DetachSync(() => recalculate_path());
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:417
    public override void loop_stopped()
    {
        base.loop_stopped();
        movement_path = null;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:422
    public override object Destroy(float? force = null)
    {
        avoid = null;
        // Pending pathfinds share this list so we need to clear it to release their callbacks to us
        on_finish_callbacks.Cut();
        on_finish_callbacks = null;
        return base.Destroy(force);
    }

    ///Tries to calculate a new path for this moveloop.
    // tg: code/controllers/subsystem/movement/movement_types.dm:430
    public virtual void recalculate_path()
    {
        if (!(repath_cooldown <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1)
            return;
        repath_cooldown = world.time + Dm.num(repath_delay);
        if (Dm.truthy(SSpathfinder.pathfind(moving, target, max_path_length == null ? null : Dm.num(max_path_length), minimum_distance, access, simulated_only == null ? null : Dm.num(simulated_only), avoid, skip_first == null ? null : Dm.num(skip_first), diagonal_handling == null ? null : Dm.num(diagonal_handling), on_finish_callbacks)))
        {
            is_pathing = TRUE;
            if (Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_JPS_REPATH]))
                _SendSignal(COMSIG_MOVELOOP_JPS_REPATH, Dm.list(this));
        }
    }

    ///Called when a path has finished being created
    // tg: code/controllers/subsystem/movement/movement_types.dm:439
    public virtual void on_finish_pathing(DmList path = null)
    {
        movement_path = path;
        is_pathing = FALSE;
        // if(moving) EVLOG_PATH(...): the admin event logger (file header)
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_JPS_FINISHED_PATHING]))
            _SendSignal(COMSIG_MOVELOOP_JPS_FINISHED_PATHING, Dm.list(this, path));
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:446
    public override object move()
    {
        if (!Dm.truthy(length(movement_path)))
        {
            if (Dm.truthy(is_pathing))
                return MOVELOOP_NOT_READY;
            else
            {
                Waiting.DetachSync(() => recalculate_path());
                return MOVELOOP_FAILURE;
            }
        }

        turf next_step = movement_path[1] as turf;
        atom old_loc = moving.loc;
        Waiting.NoSleep(moving.Move(next_step, get_dir(moving, next_step), FALSE, Dm.num(!Dm.truthy(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE)))));
        // Movement callbacks can stop this loop or delete its mover or target.
        if (QDELETED(this))
            return MOVELOOP_FAILURE;

        object dot = old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;

        // this check if we're on exactly the next tile may be overly brittle for dense objects who may get bumped slightly
        // to the side while moving but could maybe still follow their path without needing a whole new path
        if (get_turf(moving) == next_step)
        {
            if (Dm.truthy(length(movement_path)))
                movement_path.Cut(1, 2);
        }
        else
            return handle_move_attempt_failure();
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:473
    public virtual object handle_move_attempt_failure()
    {
        Waiting.DetachSync(() => recalculate_path());
        return MOVELOOP_FAILURE;
    }
}

/// As close as you can get to a "do-nothing" move loop, the pure intention of this is to absolutely resist all and any automated movement until the move loop times out.
// tg: code/controllers/subsystem/movement/movement_types.dm:863
public partial class datum_move_loop_freeze : datum_move_loop
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:865
    public override object move()
    {
        return MOVELOOP_SUCCESS; // it's successful because it's not moving. we autoclear outselves when `timeout` is reached
    }
}

// tg: code/controllers/subsystem/movement/movement_types.dm:478
public partial class datum_move_loop_has_target_jps_frustrations : datum_move_loop_has_target_jps
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:527
    // tg's own quirk: frustrations_move passes repath_delay and max_path_length in these two positions; so it is here
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        object maximum_frustrations = Dm.arg(__extra, 1) ?? 10;
        object frustration_delay = Dm.arg(__extra, 2) ?? 2 * SECONDS;
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return dot;
        this.maximum_frustrations = maximum_frustrations;
        this.frustration_delay = frustration_delay;
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:534
    public override void recalculate_path()
    {
        if (Dm.truthy(initial_path_drawn) && current_frustrations < Dm.num(maximum_frustrations))
            return;
        base.recalculate_path();
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:539
    public override void loop_stopped()
    {
        base.loop_stopped();
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:542
    public override void on_finish_pathing(DmList path = null)
    {
        base.on_finish_pathing(path);
        if (movement_path != null)
            initial_path_drawn = TRUE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:547
    public override object handle_move_attempt_failure()
    {
        if (!Dm.truthy(initial_path_drawn))
        {
            Waiting.DetachSync(() => recalculate_path());
            return MOVELOOP_FAILURE;
        }
        if (!(frustration_cooldown <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1)
            return NONE;
        frustration_cooldown = world.time + Dm.num(frustration_delay);
        current_frustrations++;
        if (Dm.truthy(_listen_lookup?[COMSIG_MOVELOOP_JPS_FRUSTRATION_INCREMENTED]))
            _SendSignal(COMSIG_MOVELOOP_JPS_FRUSTRATION_INCREMENTED, Dm.list(this, current_frustrations));
        if (current_frustrations >= Dm.num(maximum_frustrations))
        {
            current_frustrations = 0;
            Waiting.DetachSync(() => recalculate_path());
            return MOVELOOP_FAILURE;
        }
        return null;
    }
}

public partial class datum_move_manager : datum
{
    /**
     * Helper proc for the walk_rand() functionality
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
     *
     * Arguments:
     * moving - The atom we want to move
     * directions - A list of acceptable directions to try and move in. Defaults to GLOB.alldirs
     * delay - How many deci-seconds to wait between fires. Defaults to the lowest value, 0.1
     * timeout - Time in deci-seconds until the moveloop self expires. Defaults to infinity
     * subsystem - The movement subsystem to use. Defaults to SSmovement. Only one loop can exist for any one subsystem
     * priority - Defines how different move loops override each other. Lower numbers beat higher numbers, equal defaults to what currently exists. Defaults to MOVEMENT_DEFAULT_PRIORITY
     * flags - Set of bitflags that effect move loop behavior in some way. Check _DEFINES/movement.dm
     *
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:883  (silicons SI2: the serving tray's scattered items)
    public virtual object move_rand(object moving = null, object directions = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        if (!Dm.truthy(directions))
            directions = GLOB.alldirs;
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_move_rand), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout, directions);
    }
}

/**
 * This isn't actually the same as walk_rand
 * Because walk_rand is really more like walk_to_rand
 * It appears to pick a spot outside of range, and move towards it, then pick a new spot, etc.
 * I can't actually replicate this on our side, because of how bad our pathfinding is, and cause I'm not totally sure I know what it's doing.
 * I can just implement a random-walk though
**/
// tg: code/controllers/subsystem/movement/movement_types.dm:895
public partial class datum_move_loop_move_rand : datum_move_loop
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:898
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        DmList directions = Dm.arg<DmList>(__extra, 0);
        object dot = base.setup(delay, timeout, __extra);
        if (!Dm.truthy(dot))
            return dot;
        potential_directions = directions;
        return dot;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:904
    public override object compare_loops(object loop_type = null, object priority = null, object flags = null, object extra_info = null, object delay = null, object timeout = null, params object[] __extra)
    {
        DmList directions = Dm.arg<DmList>(__extra, 0);
        if (Dm.truthy(base.compare_loops(loop_type, priority, flags, extra_info, delay, timeout, __extra)) && Dm.length(Dm.bitor(potential_directions, directions)) == Dm.length(potential_directions)) //i guess this could be useful if actually it really has yet to move
            return MOVELOOP_SUCCESS;
        return MOVELOOP_FAILURE;
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:909
    public override object move()
    {
        DmList potential_dirs = potential_directions.Copy();
        while (Dm.truthy(potential_dirs.len))
        {
            object testdir = pick(potential_dirs);
            turf moving_towards = get_step(moving, Dm.num(testdir));
            atom old_loc = moving.loc;
            Waiting.NoSleep(moving.Move(moving_towards, testdir, FALSE, Dm.not(Dm.bitand(flags, MOVEMENT_LOOP_NO_DIR_UPDATE))));
            if (old_loc != moving?.loc) //If it worked, we're done
                return MOVELOOP_SUCCESS;
            Dm.remove(ref potential_dirs, testdir);
        }
        return MOVELOOP_FAILURE;
    }
}

public partial class datum_move_manager : datum
{
    /**
     * Helper proc for the disposal holder's move loop: it moves a holder through its chain of pipes
     *
     * Returns TRUE if the loop sucessfully started, or FALSE if it failed
    **/
    // tg: code/controllers/subsystem/movement/movement_types.dm:961  (D325: disposals)
    public virtual object move_disposals(object moving = null, object delay = null, object timeout = null, object subsystem = null, object priority = null, object flags = null, datum extra_info = null)
    {
        return add_to_loop(moving as atom_movable, subsystem as datum_controller_subsystem_movement, typeof(datum_move_loop_disposal_holder), priority == null ? null : Dm.num(priority), flags, extra_info, delay, timeout);
    }
}

/// Disposal holders need to move through a chain of pipes
/// Rather then through the world. This supports this
/// If this ever changes, get rid of this, add drift component like logic to the holder
/// And move them to move()
// tg: code/controllers/subsystem/movement/movement_types.dm:968  (D325)
public partial class datum_move_loop_disposal_holder : datum_move_loop
{
    // tg: code/controllers/subsystem/movement/movement_types.dm:970
    public override object setup(object delay = null, object timeout = null, params object[] __extra)
    {
        delay ??= 1;
        timeout ??= INFINITY;
        // This is a horrible pattern.
        // Move loops should almost never need to be one offs. Please don't do this if you can help it
        if (!istype(moving, typeof(obj_structure_disposalholder)))
        {
            stack_trace(Dm.text("You tried to make a [] object move like a disposals holder, stop that!", moving.type), "code/controllers/subsystem/movement/movement_types.dm", 974);
            return FALSE;
        }
        return base.setup(delay, timeout, __extra);
    }

    // tg: code/controllers/subsystem/movement/movement_types.dm:978
    public override object move()
    {
        obj_structure_disposalholder holder = moving as obj_structure_disposalholder;
        if (holder.current_pipe == null)
            return FALSE;
        atom old_loc = moving.loc;
        holder.current_pipe = Waiting.NoSleep(holder.current_pipe.transfer(holder)) as obj_structure_disposalpipe;
        return old_loc != moving?.loc ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE;
    }
}
