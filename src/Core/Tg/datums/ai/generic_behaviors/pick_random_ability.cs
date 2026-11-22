// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/pick_random_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Picks a random available ability from a pool of blackboard keys, excluding the last-used one.
/// Writes the selected action object to result_key for a targeted_mob_ability leaf to fire.
/// Returns INSTANT FAILURE if no ability in the pool is currently available.
// tg: code/datums/ai/generic_behaviors/pick_random_ability.dm:4
public partial class datum_bt_node_ai_behavior_pick_random_ability : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/pick_random_ability.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        DmList possible = ability_keys.Copy();
        object last_used = Dm.truthy(last_used_key) ? controller.blackboard[last_used_key] : null;
        if (Dm.truthy(last_used))
            Dm.remove(ref possible, last_used);
        foreach (object bb_key in Dm.each(possible))
        {
            datum_action ability = controller.blackboard[bb_key] as datum_action;
            if (QDELETED(ability) || !Dm.truthy(Waiting.NoSleep(ability.IsAvailable())))
                Dm.remove(ref possible, bb_key);
        }
        if (!Dm.truthy(length(possible)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        object picked_key = pick(possible);
        if (Dm.truthy(last_used_key))
            controller.set_blackboard_key(last_used_key, picked_key);
        controller.set_blackboard_key(result_key, controller.blackboard[picked_key]);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
