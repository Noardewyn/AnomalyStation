// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/bileworm/bileworm_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Passes when the worm should burrow and reposition: it's been scared, or its target has gotten within flee distance.
// tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_ai.dm:15
public partial class datum_bt_node_decorator_bileworm_should_resurface : datum_bt_node_decorator
{
    // tg: code/modules/mob/living/basic/lavaland/bileworm/bileworm_ai.dm:19
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (Dm.truthy(controller.blackboard[BB_BILEWORM_SCARED]))
            return TRUE;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return FALSE;
        return Dm.num(get_dist(controller.pawn, target) <= Dm.num(controller.blackboard[BB_BILEWORM_FLEE_DISTANCE]));
    }
}
