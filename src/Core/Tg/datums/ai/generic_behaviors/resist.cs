// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/resist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/datums/ai/generic_behaviors/resist.dm:1
public partial class datum_bt_node_ai_behavior_resist : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/resist.dm:1
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/resist.dm:8
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(async_still_valid()))
            return null;
        mob_living living_pawn = controller.pawn as mob_living;
        await living_pawn.execute_resist();
        finish_async(AI_BEHAVIOR_SUCCEEDED);
        return null;
    }
}
