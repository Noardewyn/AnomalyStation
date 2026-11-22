// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/item_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3: the whole file except vv_edit_var (admin variable editing, M10: a live-partial row). The var block is
// generated.

//Presets for item actions
// tg: code/datums/actions/item_action.dm:2
public partial class datum_action_item_action : datum_action
{
    // tg: code/datums/actions/item_action.dm:7
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        // If our button state is null, use the target's icon instead
        if (Dm.truthy(target) && button_icon_state == null)
            await AddComponent(typeof(datum_component_action_item_overlay), target);
    }

    // tg: code/datums/actions/item_action.dm:26
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        return await do_effect(trigger_flags);
    }

    // tg: code/datums/actions/item_action.dm:32
    public virtual async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (!Dm.truthy(target))
            return FALSE;
        obj_item item_target = target as obj_item;
        await item_target.ui_action_click(owner, this);
        return TRUE;
    }
}
