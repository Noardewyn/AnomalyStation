// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/face_target_or_face_initial.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Faces the target each tick while it's within range. When the target is absent or out of range, resets to BB_STARTING_DIRECTION.
/// Captures BB_STARTING_DIRECTION once on setup from the pawn's current facing direction.
// tg: code/datums/ai/generic_behaviors/face_target_or_face_initial.dm:3
public partial class datum_bt_node_ai_behavior_face_target_or_face_initial : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/face_target_or_face_initial.dm:6
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        await base.setup(controller);
        mob_living we = controller.pawn as mob_living;
        if (we == null)
            return FALSE;
        atom_movable target = controller.blackboard[target_key] as atom_movable;
        if (!ismovable(target) || !isturf(target.loc))
            return FALSE;
        controller.set_blackboard_key(BB_STARTING_DIRECTION, we.dir);
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/face_target_or_face_initial.dm:16
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living we = controller.pawn as mob_living;
        atom_movable target = controller.blackboard[target_key] as atom_movable;
        if (target == null || get_dist(we, target) > 8)
        {
            we.dir = Dm.num(controller.blackboard[BB_STARTING_DIRECTION]);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        we.face_atom(target);
        return AI_BEHAVIOR_DELAY;
    }
}
