// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_is_type.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the atom held in a blackboard key being an instance of a given typepath. Use "invert": true for the opposite.
// tg: code/datums/ai/generic_decorators/target_is_type.dm:1
public partial class datum_bt_node_decorator_target_is_type : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/target_is_type.dm:7
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        if (QDELETED(target) || target_type == null)
            return FALSE;
        return Dm.num(istype(target, target_type));
    }
}
