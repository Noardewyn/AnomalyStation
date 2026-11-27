// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/vent_pointer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The vent pointer: a pinpointer that points at the nearest ore vent on its level that nobody has scanned or tapped yet.

// tg: code/modules/mining/equipment/vent_pointer.dm:1
public partial class obj_item_pinpointer_vent : obj_item_pinpointer
{
    // tg: code/modules/mining/equipment/vent_pointer.dm:9
    public override void scan_for_target()
    {
        float closest_dist = INFINITY;

        foreach (obj_structure_ore_vent vent in Dm.each<obj_structure_ore_vent>(SSore_generation.possible_vents))
        {
            if (Dm.truthy(vent.discovered) || Dm.truthy(vent.tapped))
                continue;
            if (!Dm.eq(vent.z, loc.z))
                continue;

            float target_dist = get_dist(this, vent);
            if (target_dist < closest_dist)
            {
                closest_dist = target_dist;
                target = vent;
            }
        }
    }
}
