// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/ability_available.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates on whether a mob ability stored in a blackboard key is currently available. Needs to poll since we dont have nice signals to register too : (
// tg: code/datums/ai/generic_decorators/ability_available.dm:1
public partial class datum_bt_node_decorator_ability_available : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/ability_available.dm:7
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        datum_action action = controller.blackboard[ability_key] as datum_action;
        return Dm.num(!QDELETED(action) && Dm.truthy(await action.IsAvailable()));
    }
}
