// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/succeed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/datums/ai/generic_behaviors/succeed.dm:1
public partial class datum_bt_node_ai_behavior_succeed : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/succeed.dm:3
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
