// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/maintain_distance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Moves away from a target if too close, or toward it if too far, to stay within the blackboard-keyed distance band.
// tg: code/datums/ai/generic_behaviors/maintain_distance.dm:2
public partial class datum_bt_node_ai_behavior_maintain_distance : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/maintain_distance.dm:16
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (!isliving(target) || !Dm.truthy(can_see(controller.pawn, target, 10)))
            return FALSE;
        RegisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED, nameof(on_movement_failed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/maintain_distance.dm:23
    [SignalHandler]
    public virtual void on_movement_failed(atom source = null)
    {
        movement_failed = TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/maintain_distance.dm:27
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(movement_failed))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_FAILED;

        float minimum_distance = Dm.truthy(controller.blackboard[min_dist_key]) ? Dm.num(controller.blackboard[min_dist_key]) : 4;
        float maximum_distance = Dm.truthy(controller.blackboard[max_dist_key]) ? Dm.num(controller.blackboard[max_dist_key]) : 6;
        float range = get_dist(controller.pawn, target);

        if (range >= minimum_distance && range <= maximum_distance)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        if (range < minimum_distance)
        {
            if (!Dm.truthy(Waiting.NoSleep(retreat(controller, target, minimum_distance))))
                return AI_BEHAVIOR_FAILED;
        }
        else
        {
            controller.change_ai_movement_type(Dm.truthy(approach_movement_type) ? approach_movement_type : Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
            controller.movement.start_moving_towards(controller, target, maximum_distance);
        }

        return AI_BEHAVIOR_INSTANT;
    }

    // tg: code/datums/ai/generic_behaviors/maintain_distance.dm:51
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        UnregisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED);
        movement_failed = FALSE;
        controller.movement.stop_moving_towards(controller);
        controller.change_ai_movement_type(Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
        await base.finish_action(controller, succeeded);
    }

    /// Steps one tile away from target using backstep avoidance, falling back to shuffled directions if blocked.
    // tg: code/datums/ai/generic_behaviors/maintain_distance.dm:59
    public virtual async DmTask<object> retreat(datum_ai_controller controller = null, atom target = null, object minimum_distance = null)
    {
        controller.change_ai_movement_type(typeof(datum_ai_movement_basic_avoidance_backstep));
        mob pawn = controller.pawn as mob;
        if (Dm.truthy(face_target))
            pawn.face_atom(target);
        turf next_step = get_step_away(pawn, target);
        if (next_step != null && !Dm.truthy(await next_step.is_blocked_turf(exclude_mobs: TRUE)))
        {
            controller.movement.start_moving_towards(controller, next_step, 0, controller.movement_delay * 2);
            return TRUE;
        }
        DmList all_dirs = GLOB.alldirs.Copy();
        Dm.remove(ref all_dirs, get_dir(pawn, next_step));
        Dm.remove(ref all_dirs, get_dir(pawn, target));
        shuffle_inplace(all_dirs);
        foreach (object dir in Dm.each(all_dirs))
        {
            next_step = get_step(pawn, Dm.num(dir));
            if (next_step != null && !Dm.truthy(await next_step.is_blocked_turf(exclude_mobs: TRUE)))
            {
                controller.movement.start_moving_towards(controller, next_step, 0, controller.movement_delay * 2);
                return TRUE;
            }
        }
        return FALSE;
    }
}

/// Retreats to the furthest available open turf within reach rather than a single step.
// tg: code/datums/ai/generic_behaviors/maintain_distance.dm:80
public partial class datum_bt_node_ai_behavior_maintain_distance_cover_minimum_distance : datum_bt_node_ai_behavior_maintain_distance
{
    // tg: code/datums/ai/generic_behaviors/maintain_distance.dm:82
    public override async DmTask<object> retreat(datum_ai_controller controller = null, atom target = null, object minimum_distance = null)
    {
        atom_movable pawn = controller.pawn as atom_movable;
        float required_distance = Dm.num(minimum_distance) - get_dist(pawn, target);
        float best_distance = 0;
        turf chosen_turf = null;
        foreach (turf_open potential_turf in Dm.each<turf_open>(oview(required_distance, pawn)))
        {
            if (Dm.truthy(await potential_turf.is_blocked_turf()))
                continue;
            float new_distance = get_dist(potential_turf, target);
            if (new_distance > best_distance)
            {
                chosen_turf = potential_turf;
                best_distance = new_distance;
            }
        }
        if (chosen_turf == null)
            return FALSE;
        controller.movement.start_moving_towards(controller, chosen_turf, 0);
        return TRUE;
    }
}
