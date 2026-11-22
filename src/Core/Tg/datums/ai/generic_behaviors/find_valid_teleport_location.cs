// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/find_valid_teleport_location.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Finds a random open turf near the keyed target that the target can see, used to pick an ambush teleport destination.
/// Returns SUCCESS if a turf is found, FAILURE if none are available.
// tg: code/datums/ai/generic_behaviors/find_valid_teleport_location.dm:3
public partial class datum_bt_node_ai_behavior_find_valid_teleport_location : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/find_valid_teleport_location.dm:11
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        DmList possible_turfs = Dm.list();
        foreach (turf_open potential_turf in Dm.each<turf_open>(oview(range, target)))
        {
            if (Dm.truthy(Waiting.NoSleep(potential_turf.is_blocked_turf())))
                continue;
            if (!Dm.truthy(can_see(target, potential_turf, range)))
                continue;
            Dm.append(ref possible_turfs, potential_turf);
        }

        if (!Dm.truthy(length(possible_turfs)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(set_key, pick(possible_turfs));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
