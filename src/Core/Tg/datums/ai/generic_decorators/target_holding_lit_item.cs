// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_holding_lit_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes when the keyed target is a carbon holding a lit item whose type is in a blackboard-keyed type list.
/// Use "invert": true for the opposite.
// tg: code/datums/ai/generic_decorators/target_holding_lit_item.dm:1
public partial class datum_bt_node_decorator_target_holding_lit_item : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/target_holding_lit_item.dm:7
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        if (controller.blackboard[key] is not mob_living_carbon target)
            return FALSE;
        DmList item_types = controller.blackboard[item_types_key] as DmList;
        if (!Dm.truthy(length(item_types)))
            return FALSE;
        foreach (obj_item held_item in Dm.each<obj_item>(target.held_items))
        {
            if (!Dm.truthy(is_type_in_list(held_item, item_types)))
                continue;
            if (Dm.truthy(held_item.light_on))
                return TRUE;
        }
        return FALSE;
    }
}
