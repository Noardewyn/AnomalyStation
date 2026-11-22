// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/movement/ai_movement_complete_stop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Come to a complete stop for a set amount of time.
// tg: code/datums/ai/movement/ai_movement_complete_stop.dm:2
public partial class datum_ai_movement_complete_stop : datum_ai_movement
{
    // tg: code/datums/ai/movement/ai_movement_complete_stop.dm:5
    public override object start_moving_towards(datum_ai_controller controller = null, atom current_movement_target = null, object min_distance = null, object delay_override = null)
    {
        object dot = base.start_moving_towards(controller, current_movement_target, min_distance, delay_override);
        if (!Dm.truthy(dot))
            return FALSE;
        atom_movable moving = controller.pawn as atom_movable;
        object stopping_time = controller.blackboard[BB_STATIONARY_SECONDS];
        float delay_time = Dm.num(stopping_time) * 0.5f; // no real reason to fire any more often than this really
        // assume that the current_movement_target is our location
        datum_move_loop loop = GLOB.move_manager.freeze(moving, current_movement_target, delay: delay_time, timeout: stopping_time, subsystem: SSai_movement, extra_info: controller) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(pre_move));
        return dot;
    }

    // tg: code/datums/ai/movement/ai_movement_complete_stop.dm:17
    public override object allowed_to_move(datum_move_loop source = null)
    {
        return FALSE;
    }
}
