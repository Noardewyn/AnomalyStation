// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/container_attackable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on the container being worth attacking (pawn.obj_damage > damage_deflection); fails if pacifist = TRUE.
// tg: code/datums/ai/generic_decorators/container_attackable.dm:1
public partial class datum_bt_node_decorator_container_attackable : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/container_attackable.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (Dm.truthy(pacifist))
            return FALSE;
        if (controller.pawn is not mob_living_basic basic_pawn)
            return FALSE;
        atom target = controller.blackboard[target_key] as atom;
        if (target == null)
            return FALSE;
        return Dm.num(basic_pawn.obj_damage > Dm.num(((obj)target).damage_deflection));
    }
}
