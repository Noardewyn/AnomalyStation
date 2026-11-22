// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/bb_key_true.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes if the given blackboard key equals a true-ey value
// tg: code/datums/ai/generic_decorators/bb_key_true.dm:1
public partial class datum_bt_node_decorator_bb_key_true : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/bb_key_true.dm:5
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        object result = controller.blackboard[key];
        return Dm.num(Dm.truthy(result));
    }
}
