// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_farther_than_from_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes if the pawn is farther than the distance stored in distance_key from the atom stored in anchor_key.
// tg: code/datums/ai/generic_decorators/pawn_farther_than_from_key.dm:1
public partial class datum_bt_node_decorator_pawn_farther_than_from_key : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_farther_than_from_key.dm:8
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom anchor = controller.blackboard[anchor_key] as atom;
        if (QDELETED(anchor))
            return FALSE;
        object min_dist = controller.blackboard[distance_key];
        return Dm.num(get_dist(controller.pawn, anchor) > Dm.num(min_dist));
    }
}
