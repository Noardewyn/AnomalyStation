// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/adjust.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3: the whole file (the breath mask's "Adjust" button and the space helmet's "Adjust Visor" button). The var
// blocks are generated.

// tg: code/datums/actions/items/adjust.dm:1
public partial class datum_action_item_action_adjust : datum_action_item_action
{
    // tg: code/datums/actions/items/adjust.dm:4
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        obj_item item_target = target as obj_item;
        name = I18n.T("Adjust []", item_target.name);
    }

    // tg: code/datums/actions/items/adjust.dm:9
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (!isclothing(target))
            throw Dm.CRASH(Dm.text("adjust_visor action attempted to trigger on a non-clothing atom [] ([]) owned by [] ([]!", target, target?.GetType(), owner, owner?.GetType()), "code/datums/actions/items/adjust.dm", 11);
        obj_item_clothing as_clothing = target as obj_item_clothing;
        await as_clothing.adjust_visor(owner as mob_living);
        return TRUE;
    }
}

// tg: code/datums/actions/items/adjust.dm:16
public partial class datum_action_item_action_adjust_style : datum_action_item_action
{
    // tg: code/datums/actions/items/adjust.dm:19
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        obj_item item_target = target as obj_item;
        name = I18n.T("Adjust []'s Style", item_target.name);
    }
}

// tg: code/datums/actions/items/adjust.dm:24
public partial class datum_action_item_action_adjust_visor : datum_action_item_action
{
    // tg: code/datums/actions/items/adjust.dm:27
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        obj_item item_target = target as obj_item;
        name = I18n.T("Adjust [] Visor", item_target.name);
    }
}
