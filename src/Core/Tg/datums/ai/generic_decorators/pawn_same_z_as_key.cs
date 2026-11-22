// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_same_z_as_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes if the pawn is on the same z-level as the atom stored in the given blackboard key.
// tg: code/datums/ai/generic_decorators/pawn_same_z_as_key.dm:1
public partial class datum_bt_node_decorator_pawn_same_z_as_key : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/pawn_same_z_as_key.dm:5
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        if (QDELETED(target))
            return FALSE;
        return Dm.num(controller.pawn.z == target.z);
    }
}
