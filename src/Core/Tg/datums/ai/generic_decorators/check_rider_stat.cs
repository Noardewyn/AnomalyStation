// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/check_rider_stat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Checks the health status of our rider, if any. returns false if we dont have a rider to begin with
// tg: code/datums/ai/generic_decorators/check_rider_stat.dm:1
public partial class datum_bt_node_decorator_check_rider_stat : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/check_rider_stat.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (!Dm.truthy(length(living_pawn.buckled_mobs)))
            return FALSE;
        mob_living buckled_to = living_pawn.buckled_mobs[1] as mob_living;
        return Dm.num(buckled_to.stat == target_stat);
    }
}
