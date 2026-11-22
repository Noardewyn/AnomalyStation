// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/find_nearby.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Picks a random visible, non-abstract atom within range 2 and stores it in a blackboard key.
// tg: code/datums/ai/generic_behaviors/find_nearby.dm:2
public partial class datum_bt_node_ai_behavior_find_nearby : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/find_nearby.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        DmList possible_targets = Dm.list();
        foreach (atom thing in Dm.each<atom>(view(2, controller.pawn)))
        {
            if (!Dm.truthy(thing.mouse_opacity))
                continue;
            if (Dm.truthy(thing.IsObscured()))
                continue;
            if (thing is obj_item item && Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT)))
                continue;
            Dm.append(ref possible_targets, thing);
        }
        if (!Dm.truthy(possible_targets.len))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        controller.set_blackboard_key(target_key, pick(possible_targets));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
