// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_legcuffed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the carbon held in a blackboard key being legcuffed. Use "invert": true to gate on not being legcuffed.
// tg: code/datums/ai/generic_decorators/target_legcuffed.dm:1
public partial class datum_bt_node_decorator_target_legcuffed : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/target_legcuffed.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (controller.blackboard[key] is not mob_living_carbon target)
            return FALSE;
        return Dm.num(target.legcuffed != null);
    }
}
