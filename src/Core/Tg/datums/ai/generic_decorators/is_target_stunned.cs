// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/is_target_stunned.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the mob held in a blackboard key being paralyzed (stunned and on the ground).
/// Use "invert": true to gate on the target NOT being stunned. Used by arrest bots like ED209 to close in once the target is incapacitated.
// tg: code/datums/ai/generic_decorators/is_target_stunned.dm:1
public partial class datum_bt_node_decorator_is_target_stunned : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/is_target_stunned.dm:7
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[key] as mob_living;
        if (QDELETED(target) || !isliving(target))
            return FALSE;
        return Dm.num(Dm.truthy(target.IsParalyzed()));
    }
}
