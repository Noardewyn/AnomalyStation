// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/pawn_turf_has_trait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the pawn's current turf having a given trait. Use invert = TRUE to gate on the trait being absent.
// tg: code/datums/ai/generic_decorators/pawn_turf_has_trait.dm:1
public partial class datum_bt_node_decorator_pawn_turf_has_trait : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/pawn_turf_has_trait.dm:5
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        turf my_turf_in_the_middle_of_my_turf = get_turf(controller.pawn);
        return Dm.num(HAS_TRAIT(my_turf_in_the_middle_of_my_turf, trait));
    }
}
