// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/pick_up.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Picks up a blackboard-keyed item. Pair with move_to_target in the BT tree for navigation.
// tg: code/datums/ai/generic_behaviors/pick_up.dm:2
public partial class datum_bt_node_ai_behavior_pick_up : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/pick_up.dm:9
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        return Dm.num(!QDELETED(target));
    }

    // tg: code/datums/ai/generic_behaviors/pick_up.dm:13
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        obj_item target = controller.blackboard[target_key] as obj_item;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        if (Dm.truthy(living_pawn.is_holding(target))) // already in hands
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        if (!Dm.truthy(Waiting.NoSleep(target.IsReachableBy(living_pawn)))) // cant reach!
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (Dm.truthy(living_pawn.get_active_held_item())) // something is in our hands already
        {
            if (!Dm.truthy(drop_held) || !Dm.truthy(Waiting.NoSleep(living_pawn.dropItemToGround(living_pawn.get_active_held_item() as obj_item))))
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        Waiting.Detach(controller.ai_interact(target, FALSE));
        return AI_BEHAVIOR_DELAY | (target.loc == living_pawn ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
    }
}
