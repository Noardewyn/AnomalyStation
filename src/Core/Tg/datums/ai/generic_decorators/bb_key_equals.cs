// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/bb_key_equals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes if the given blackboard key equals the expected value.
// tg: code/datums/ai/generic_decorators/bb_key_equals.dm:1
public partial class datum_bt_node_decorator_bb_key_equals : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/bb_key_equals.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return bb_key_equals(controller, key, value);
    }
}
