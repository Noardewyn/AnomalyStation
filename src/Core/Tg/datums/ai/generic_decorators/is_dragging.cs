// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/is_dragging.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on pawn currently pulling something. Use invert = TRUE for the opposite. Checked each tick.
// tg: code/datums/ai/generic_decorators/is_dragging.dm:1
public partial class datum_bt_node_decorator_is_dragging : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/is_dragging.dm:4
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        return Dm.num(Dm.truthy(living_pawn.pulling));
    }
}
