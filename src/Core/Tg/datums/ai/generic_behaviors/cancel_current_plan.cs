// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/cancel_current_plan.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Cancels the controller's current plan, causing the BT to re-evaluate from the root next tick.
// tg: code/datums/ai/generic_behaviors/cancel_current_plan.dm:2
public partial class datum_bt_node_ai_behavior_cancel_current_plan : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/cancel_current_plan.dm:4
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        Waiting.NoSleep(controller.cancel_current_plan());
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
