// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/find_furthest_turf_from_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Finds the open turf furthest from the keyed target and stores it, used to pick an escape destination.
/// Returns INSTANT SUCCESS if a turf is found, INSTANT FAILURE if none are available.
// tg: code/datums/ai/generic_behaviors/find_furthest_turf_from_target.dm:3
public partial class datum_bt_node_ai_behavior_find_furthest_turf_from_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/find_furthest_turf_from_target.dm:11
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(living_target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        float best_distance = 0;
        turf chosen_turf = null;
        foreach (turf_open potential_destination in Dm.each<turf_open>(oview(range, living_target)))
        {
            if (Dm.truthy(Waiting.NoSleep(potential_destination.is_blocked_turf())))
                continue;
            float new_distance = get_dist(potential_destination, living_target);
            if (new_distance > best_distance)
            {
                chosen_turf = potential_destination;
                best_distance = new_distance;
            }
            if (Dm.eq(best_distance, range))
                break; // already at the furthest possible distance
        }

        if (chosen_turf == null)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(set_key, chosen_turf);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
