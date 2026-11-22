// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/drop_all_held_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Drops everything the pawn is currently holding in its hands onto its current turf.
// tg: code/datums/ai/generic_behaviors/drop_all_held_items.dm:2
public partial class datum_bt_node_ai_behavior_drop_all_held_items : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/drop_all_held_items.dm:4
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (!Dm.truthy(living_pawn.get_num_held_items()))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.NoSleep(living_pawn.drop_all_held_items());
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
