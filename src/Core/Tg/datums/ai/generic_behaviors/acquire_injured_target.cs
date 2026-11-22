// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/acquire_injured_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/datums/ai/generic_behaviors/acquire_injured_target.dm:1
public partial class datum_bt_node_ai_behavior_retrieve_injured_rider : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/acquire_injured_target.dm:5
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_mob = controller.pawn as mob_living;
        if (!Dm.truthy(length(living_mob.buckled_mobs)) || !isliving(living_mob.buckled_mobs[1]))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        mob_living rider = living_mob.buckled_mobs[1] as mob_living;
        if (rider.stat == STABLE || rider.stat == DEAD || rider.health >= rider.maxHealth)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(target_key, rider);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
