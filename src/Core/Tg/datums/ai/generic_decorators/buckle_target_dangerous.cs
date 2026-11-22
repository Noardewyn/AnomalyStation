// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/buckle_target_dangerous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the buckle target having TRAIT_DANGEROUS_BUCKLE; fails if pacifist = TRUE.
// tg: code/datums/ai/generic_decorators/buckle_target_dangerous.dm:1
public partial class datum_bt_node_decorator_buckle_target_dangerous : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/buckle_target_dangerous.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (Dm.truthy(pacifist))
            return FALSE;
        atom target = controller.blackboard[target_key] as atom;
        return Dm.num(target != null && HAS_TRAIT(target, TRAIT_DANGEROUS_BUCKLE));
    }
}
