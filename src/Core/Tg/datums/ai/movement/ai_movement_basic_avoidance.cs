// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/movement/ai_movement_basic_avoidance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Uses Byond's basic obstacle avoidance movement
// tg: code/datums/ai/movement/ai_movement_basic_avoidance.dm:2
public partial class datum_ai_movement_basic_avoidance : datum_ai_movement
{
    // tg: code/datums/ai/movement/ai_movement_basic_avoidance.dm:7
    public override object start_moving_towards(datum_ai_controller controller = null, atom current_movement_target = null, object min_distance = null, object delay_override = null)
    {
        object dot = base.start_moving_towards(controller, current_movement_target, min_distance, delay_override);
        if (!Dm.truthy(dot))
            return FALSE;
        atom_movable moving = controller.pawn as atom_movable;
        object min_dist = controller.blackboard[BB_CURRENT_MIN_MOVE_DISTANCE];
        object delay = Dm.truthy(delay_override) ? delay_override : controller.movement_delay;
        datum_move_loop loop = GLOB.move_manager.move_to(moving, current_movement_target, min_dist, delay, flags: move_flags, subsystem: SSai_movement, extra_info: controller) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(pre_move));
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_move));
        return dot;
    }

    // tg: code/datums/ai/movement/ai_movement_basic_avoidance.dm:18
    public override void update_movement_target(datum_ai_controller controller = null, atom new_target = null)
    {
        base.update_movement_target(controller, new_target);

        datum_move_loop_has_target loop = GLOB.move_manager.processing_on(controller.pawn as atom_movable, SSai_movement) as datum_move_loop_has_target;
        if (loop != null)
            loop.target = new_target;
    }

    // tg: code/datums/ai/movement/ai_movement_basic_avoidance.dm:25
    public override object allowed_to_move(datum_move_loop source = null)
    {
        turf target_turf = get_step_towards(source.moving, ((datum_move_loop_has_target)source).target);
        if (!Dm.truthy(target_turf?.can_cross_safely(source.moving)))
            return FALSE;
        return base.allowed_to_move(source);
    }
}
