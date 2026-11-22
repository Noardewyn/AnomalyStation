// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The destination is hand-declared: an atom.

/// Moves to line up with the target along a cardinal direction, so a directional ability can fire down the lane.
/// Reports SUCCESS once lined up and within range, FAILURE when the target is gone, too far, or pathing gives up.
// tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:3
public partial class datum_bt_node_ai_behavior_move_to_cardinal : datum_bt_node_ai_behavior
{
    /// The cardinal tile of our target we are currently moving toward.
    // tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:12
    public atom destination;

    // tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:16
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return FALSE;
        RegisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED, nameof(on_movement_failed));
        await move_towards_nearest_cardinal(controller, target);
        return TRUE;
    }

    // tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:24
    [SignalHandler]
    public virtual void on_movement_failed(atom source = null)
    {
        movement_failed = TRUE;
    }

    /// Begin moving toward the closest unblocked cardinal tile of our target.
    // tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:29
    public virtual async DmTask move_towards_nearest_cardinal(datum_ai_controller controller = null, atom target = null)
    {
        atom move_target = null;
        float closest = INFINITY;
        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            turf cardinal_turf = get_ranged_target_turf(target, dir, minimum_distance) as turf;
            if (Dm.truthy(await cardinal_turf.is_blocked_turf()))
                continue;
            float distance_to = get_dist(controller.pawn, cardinal_turf);
            if (distance_to >= closest)
                continue;
            closest = distance_to;
            move_target = cardinal_turf;
        }
        if (move_target == null)
            move_target = target;
        controller.movement.start_moving_towards(controller, move_target, 0);
        destination = move_target;
    }

    // tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:45
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(movement_failed))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        float distance_to_target = get_dist(controller.pawn, target);
        if (distance_to_target > maximum_distance)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (!Dm.@in(get_dir(controller.pawn, target), GLOB.cardinals) || distance_to_target < minimum_distance)
        {
            Waiting.NoSleep(move_towards_nearest_cardinal(controller, target));
            return AI_BEHAVIOR_INSTANT;
        }
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_subtrees/move_to_cardinal.dm:59
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        UnregisterSignal(controller.pawn, COMSIG_MOB_AI_MOVEMENT_FAILED);
        movement_failed = FALSE;
        controller.movement.stop_moving_towards(controller);
        await base.finish_action(controller, succeeded);
    }
}
