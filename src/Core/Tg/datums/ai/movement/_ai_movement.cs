// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/movement/_ai_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The var block is generated.

///This datum is an abstract class that can be overridden for different types of movement
// tg: code/datums/ai/movement/_ai_movement.dm:2
public partial class datum_ai_movement : datum
{
    //Override this to setup the moveloop you want to use
    // tg: code/datums/ai/movement/_ai_movement.dm:9
    public virtual object start_moving_towards(datum_ai_controller controller = null, atom current_movement_target = null, object min_distance = null, object delay_override = null)
    {
        object old_movement_target = moving_controllers[controller];
        if (Dm.truthy(old_movement_target))
        {
            if (Dm.eq(old_movement_target, current_movement_target))
                return FALSE;
            else
            {
                update_movement_target(controller, current_movement_target);
                return FALSE;
            }
        }
        controller.consecutive_pathing_attempts = 0;
        controller.set_blackboard_key(BB_CURRENT_MIN_MOVE_DISTANCE, min_distance);
        moving_controllers[controller] = current_movement_target;
        if (Dm.truthy(controller.pawn._listen_lookup?[COMSIG_MOB_AI_MOVEMENT_STARTED]))
            controller.pawn._SendSignal(COMSIG_MOB_AI_MOVEMENT_STARTED, Dm.list(controller.pawn, current_movement_target));
        controller.set_blackboard_key(BB_CURRENT_MOVEMENT_TARGET, current_movement_target);
        return TRUE;
    }

    // tg: code/datums/ai/movement/_ai_movement.dm:24
    public virtual void update_movement_target(datum_ai_controller controller = null, atom new_target = null)
    {
        moving_controllers[controller] = new_target;
        controller.set_blackboard_key(BB_CURRENT_MOVEMENT_TARGET, new_target);
    }

    // tg: code/datums/ai/movement/_ai_movement.dm:28
    public virtual void stop_moving_towards(datum_ai_controller controller = null)
    {
        controller.consecutive_pathing_attempts = 0;
        Dm.remove(ref moving_controllers, controller);
        // We got deleted as we finished an action
        controller.clear_blackboard_key(BB_CURRENT_MOVEMENT_TARGET);
        if (!QDELETED(controller.pawn))
            GLOB.move_manager.stop_looping(controller.pawn as atom_movable, SSai_movement);
    }

    // tg: code/datums/ai/movement/_ai_movement.dm:37
    public virtual void increment_pathing_failures(datum_ai_controller controller = null)
    {
        controller.consecutive_pathing_attempts = Dm.num(controller.consecutive_pathing_attempts) + 1;
        if (Dm.num(controller.consecutive_pathing_attempts) >= Dm.num(max_pathing_attempts))
            fail_movement(controller);
    }

    // tg: code/datums/ai/movement/_ai_movement.dm:42
    public virtual void fail_movement(datum_ai_controller controller = null)
    {
        stop_moving_towards(controller);
        if (Dm.truthy(controller.pawn._listen_lookup?[COMSIG_MOB_AI_MOVEMENT_FAILED]))
            controller.pawn._SendSignal(COMSIG_MOB_AI_MOVEMENT_FAILED, Dm.list(controller.pawn, moving_controllers[controller]));
    }

    // tg: code/datums/ai/movement/_ai_movement.dm:46
    public virtual void reset_pathing_failures(datum_ai_controller controller = null)
    {
        controller.consecutive_pathing_attempts = 0;
    }

    // tg: code/datums/ai/movement/_ai_movement.dm:49
    public virtual object allowed_to_move(datum_move_loop source = null)
    {
        atom_movable pawn = source.moving;
        datum_ai_controller controller = source.extra_info as datum_ai_controller;

        if (Dm.truthy(Dm.bitand(controller.ai_traits, STOP_MOVING_WHEN_PULLED)) && Dm.truthy(pawn.pulledby)) //Need to store more state. Annoying.
            return FALSE;

        if (HAS_TRAIT(pawn, TRAIT_AI_MOVEMENT_HALTED))
            return FALSE;

        if (!isturf(pawn.loc)) //No moving if not on a turf
            return FALSE;

        if (isliving(pawn))
        {
            mob_living pawn_mob = (mob_living)pawn;
            if (!Dm.truthy(Dm.bitand(pawn_mob.mobility_flags, MOBILITY_MOVE)))
                return FALSE;
            // Bandaid fix: AI controllers don't call /Process_Grab because it's a client proc,
            // and thus, we need to check that grabbed mobs cuffed/crit can't move
            // That proc should probably be moved onto the mob instead of clients
            if (Dm.truthy(INCAPACITATED_IGNORING(pawn_mob, INCAPABLE_STASIS)) && Dm.truthy(pawn.pulledby))
                return FALSE;
        }

        if (HAS_TRAIT(pawn, TRAIT_NO_TRANSFORM))
            return FALSE;

        return TRUE;
    }

    ///Anything to do before moving; any checks if the pawn should be able to move should be placed in allowed_to_move() and called by this proc
    // tg: code/datums/ai/movement/_ai_movement.dm:80
    [SignalHandler]
    public virtual object pre_move(datum_move_loop source = null)
    {
        datum_ai_controller controller = source.extra_info as datum_ai_controller;

        // Check if this controller can actually run, so we don't chase people with corpses
        if (!Dm.truthy(controller.able_to_run))
        {
            qdel(source); //stop moving
            return MOVELOOP_SKIP_STEP;
        }

        source.delay = Dm.num(controller.movement_delay);

        if (Dm.truthy(allowed_to_move(source)))
            return NONE;
        increment_pathing_failures(controller);
        return MOVELOOP_SKIP_STEP;
    }

    //Anything to do post movement
    // tg: code/datums/ai/movement/_ai_movement.dm:98
    [SignalHandler]
    public virtual void post_move(datum_move_loop source = null, object succeeded = null)
    {
        datum_ai_controller controller = source.extra_info as datum_ai_controller;
        if (Dm.eq(succeeded, MOVELOOP_SUCCESS))
            reset_pathing_failures(controller);
        else if (Dm.eq(succeeded, MOVELOOP_FAILURE))
            increment_pathing_failures(controller);
    }
}
