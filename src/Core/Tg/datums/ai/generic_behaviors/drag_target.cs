// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/drag_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/**
 * BT-native drag behavior. Moves to the target and starts pulling it.
 * If already pulling the target, returns SUCCESS immediately (idempotent).
 * Does NOT clear the target key on finish  callers must clear it when done.
 * Use move_to_target after this to drag the pulled mob/item to a destination.
 */
// tg: code/datums/ai/generic_behaviors/drag_target.dm:7
public partial class datum_bt_node_ai_behavior_drag_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/drag_target.dm:11
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living our_mob = controller.pawn as mob_living;
        atom_movable target = controller.blackboard[target_key] as atom_movable;
        if (QDELETED(target) || Dm.truthy(target.anchored) || Dm.truthy(target.pulledby))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (our_mob.pulling == target)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        if (!Dm.truthy(Waiting.NoSleep(our_mob.Adjacent(target))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(our_mob.start_pulling(target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
