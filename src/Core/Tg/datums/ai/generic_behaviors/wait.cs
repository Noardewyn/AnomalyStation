// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/wait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Does nothing for a given duration (deciseconds, so use the SECONDS define pls), then succeeds. duration = 0 waits forever.
// tg: code/datums/ai/generic_behaviors/wait.dm:2
public partial class datum_bt_node_ai_behavior_wait : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/wait.dm:8
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        end_time = Dm.num(duration) > 0 ? world.time + Dm.num(duration) : 0;
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/wait.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!Dm.truthy(end_time) || world.time < end_time)
            return AI_BEHAVIOR_INSTANT;
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/wait.dm:17
    public override async DmTask reset_tick_state()
    {
        end_time = 0;
        await base.reset_tick_state();
    }
}
