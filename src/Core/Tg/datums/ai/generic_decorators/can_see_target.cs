// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/can_see_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG line (gaps package C1).

/**
 * Gates child on a blackboard key holding a visible, non-deleted atom.
 * If the target is null, deleted, or not visible to the pawn, clears the key and returns BT_FAILURE.
 */
// tg: code/datums/ai/generic_decorators/can_see_target.dm:1
public partial class datum_bt_node_decorator_can_see_target : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/can_see_target.dm:11
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        if (QDELETED(target) || !Dm.truthy(can_see(controller.pawn, target, range)))
        {
            controller.clear_blackboard_key(key);
            return FALSE;
        }
        return TRUE;
    }
}
