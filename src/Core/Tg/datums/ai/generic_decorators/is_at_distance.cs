// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/is_at_distance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Decorator that requires the controller's pawn to be within range of a blackboard target.
// tg: code/datums/ai/generic_decorators/is_at_distance.dm:1
public partial class datum_bt_node_decorator_is_at_distance : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/is_at_distance.dm:12
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return FALSE;

        atom_movable pawn = controller.pawn as atom_movable;
        float dist = get_dist(pawn, target);
        bool reachable = !Dm.truthy(require_reach) || Dm.truthy(await target.IsReachableBy(pawn));

        return Dm.num((maximum_distance == -1 || dist <= maximum_distance) && dist >= min_distance && reachable);
    }
}
