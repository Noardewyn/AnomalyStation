// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/move_to_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Moves toward a blackboard-keyed target each tick. Succeeds when within required_dist by default.
/// Pass finish_on_arrival = FALSE to keep running indefinitely (used in combat parallels).
/// Pass movement_type to temporarily override the controller's ai_movement; resets to the initial type on finish.
// tg: code/datums/ai/generic_behaviors/move_to_target.dm:4
public partial class datum_bt_node_ai_behavior_move_to_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/move_to_target.dm:18
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return FALSE;
        if (Dm.truthy(movement_type))
            controller.change_ai_movement_type(movement_type);
        RegisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED, nameof(on_movement_failed));
        controller.movement.start_moving_towards(controller, target, required_dist);
        tracked_target = target;
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/move_to_target.dm:29
    [SignalHandler]
    public virtual void on_movement_failed(atom source = null)
    {
        movement_failed = TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/move_to_target.dm:33
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(movement_failed))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_FAILED;
        if (target != tracked_target)
        {
            controller.movement.start_moving_towards(controller, target, required_dist);
            tracked_target = target;
        }
        else if (!Dm.truthy(controller.movement.moving_controllers[controller]))
            controller.movement.start_moving_towards(controller, target, required_dist);
        if (Dm.truthy(finish_on_arrival) && get_dist(controller.pawn, target) <= required_dist)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
        return AI_BEHAVIOR_INSTANT;
    }

    // tg: code/datums/ai/generic_behaviors/move_to_target.dm:48
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        UnregisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED);
        movement_failed = FALSE;
        tracked_target = null;
        controller.movement.stop_moving_towards(controller);
        if (Dm.truthy(movement_type))
            controller.change_ai_movement_type(Dm.initial(controller, (datum_ai_controller c) => c.ai_movement));
        await base.finish_action(controller, succeeded);
    }
}
