// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/ai_interact.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Interacts with the atom at target_key once, with optional combat_mode (default FALSE).
// tg: code/datums/ai/generic_behaviors/ai_interact.dm:2
public partial class datum_bt_node_ai_behavior_ai_interact : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/ai_interact.dm:8
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        return Dm.num(!QDELETED(target));
    }

    // tg: code/datums/ai/generic_behaviors/ai_interact.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(target, combat_mode));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
