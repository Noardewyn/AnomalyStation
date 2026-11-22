// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/set_bb_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Sets the given blackboard key to a value.
// tg: code/datums/ai/generic_behaviors/set_bb_key.dm:2
public partial class datum_bt_node_ai_behavior_set_bb_key : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/set_bb_key.dm:8
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        controller.set_blackboard_key(target_key, value);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
