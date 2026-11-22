// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/random_chance_from_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes with a probability taken from a blackboard key (0-100 integer).
// tg: code/datums/ai/generic_decorators/random_chance_from_key.dm:1
public partial class datum_bt_node_decorator_random_chance_from_key : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/random_chance_from_key.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        object chance = controller.blackboard[chance_key];
        if (!Dm.truthy(chance))
            return FALSE;
        return Dm.num(prob(Dm.num(chance)));
    }
}
