// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/find_target_facing_turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Finds the turf directly in front of the keyed target (the tile it is facing) and stores it,
/// so we can line up an action on its front. Fails if the turf is missing or blocked.
// tg: code/datums/ai/generic_behaviors/find_target_facing_turf.dm:3
public partial class datum_bt_node_ai_behavior_find_target_facing_turf : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/find_target_facing_turf.dm:9
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        turf facing_turf = get_step(target, Dm.num(target.dir));
        if (facing_turf == null || Dm.truthy(Waiting.NoSleep(facing_turf.is_blocked_turf(ignore_atoms: Dm.list(controller.pawn)))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(set_key, facing_turf);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
