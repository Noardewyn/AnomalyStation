// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/optional.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Always reports SUCCESS to the parent, even if the child fails. Lets a sequence continue past a step that's allowed to fail. RUNNING still passes through unchanged.
// tg: code/datums/ai/generic_decorators/optional.dm:1
public partial class datum_bt_node_decorator_optional : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/optional.dm:4
    public override object tick(datum_ai_controller controller = null, object seconds_per_tick = null)
    {
        object dot = base.tick(controller, seconds_per_tick);
        if (Dm.eq(dot, BT_FAILURE))
            return BT_SUCCESS;
        return dot;
    }
}
