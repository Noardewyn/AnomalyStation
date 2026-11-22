// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/near_village_humans.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gathers nearby carbon humans within BB_MAXIMUM_DISTANCE_TO_VILLAGE of the BB_HOME_VILLAGE anchor.
// tg: code/datums/ai/basic_mobs/target_sources/near_village_humans.dm:2
public partial class datum_target_source_near_village_humans : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/near_village_humans.dm:4
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        atom anchor = controller.blackboard[BB_HOME_VILLAGE] as atom;
        object max_dist = Dm.truthy(controller.blackboard[BB_MAXIMUM_DISTANCE_TO_VILLAGE]) ? controller.blackboard[BB_MAXIMUM_DISTANCE_TO_VILLAGE] : range;
        DmList candidates = Dm.list();
        foreach (mob_living_carbon_human candidate in Dm.each<mob_living_carbon_human>(oview(max_dist, pawn)))
        {
            if (anchor != null && get_dist(candidate, anchor) > Dm.num(max_dist))
                continue;
            Dm.append(ref candidates, candidate);
        }
        return candidates;
    }
}
