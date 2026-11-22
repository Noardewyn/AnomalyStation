// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/use_on_object.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Uses the pawn's held item (or unarmed) on a blackboard-keyed target.
// tg: code/datums/ai/generic_behaviors/use_on_object.dm:2
public partial class datum_bt_node_ai_behavior_use_on_object : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/use_on_object.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        Waiting.Detach(controller.ai_interact(target, FALSE));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
