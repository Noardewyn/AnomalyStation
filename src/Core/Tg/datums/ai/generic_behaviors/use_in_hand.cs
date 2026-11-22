// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/use_in_hand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Uses the pawn's currently active held item in-hand.
// tg: code/datums/ai/generic_behaviors/use_in_hand.dm:2
public partial class datum_bt_node_ai_behavior_use_in_hand : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/use_in_hand.dm:4
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        object held = pawn.get_active_held_item();
        if (!Dm.truthy(held))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(pawn.activate_hand());
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
