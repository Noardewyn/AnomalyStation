// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_nutrition_below.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes if the pawn's nutrition is below the given threshold.
// tg: code/datums/ai/generic_decorators/pawn_nutrition_below.dm:1
public partial class datum_bt_node_decorator_pawn_nutrition_below : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_nutrition_below.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (controller.pawn is not mob_living pawn)
            return FALSE;
        return Dm.num(Dm.num(pawn.nutrition) < nutrition_threshold);
    }
}
