// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/clear_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The key is hand-declared: a blackboard key, text (the generator read another type's var of the same name).

/// BT-native version: clears a single blackboard key. Returns INSTANT SUCCESS.
// tg: code/datums/ai/generic_behaviors/clear_key.dm:2
public partial class datum_bt_node_ai_behavior_clear_key : datum_bt_node_ai_behavior
{
    /// Blackboard key to clear.
    // tg: code/datums/ai/generic_behaviors/clear_key.dm:4
    public object key;

    // tg: code/datums/ai/generic_behaviors/clear_key.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        controller.clear_blackboard_key(key);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
