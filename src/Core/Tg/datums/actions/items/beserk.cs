// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/beserk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the berserker helmet's rage. The var blocks are generated.

// tg: code/datums/actions/items/beserk.dm:1
public partial class datum_action_item_action_berserk_mode : datum_action_item_action
{
    // tg: code/datums/actions/items/beserk.dm:9
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_clothing_head_hooded_berserker berserk = target as obj_item_clothing_head_hooded_berserker;
        berserk.berserk_mode(owner as mob_living_carbon_human);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/datums/actions/items/beserk.dm:14
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!(target is obj_item_clothing_head_hooded_berserker))
            return FALSE;

        obj_item_clothing_head_hooded_berserker berserk = target as obj_item_clothing_head_hooded_berserker;
        if (Dm.truthy(berserk.berserk_active))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("You are already berserk!")));
            return FALSE;
        }
        if (berserk.berserk_charge < 100)
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("You don't have a full charge.")));
            return FALSE;
        }
        return TRUE;
    }
}
