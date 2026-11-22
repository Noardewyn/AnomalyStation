// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/organ_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps package C5): the base's check; the toggle and use actions' names came with gaps package P6 (the cybernetic implants'
// buttons); go_feral came with the cat fangs (gaps SP1, the felinids).

// tg: code/datums/actions/items/organ_action.dm:2
public partial class datum_action_item_action_organ_action : datum_action_item_action
{
    // tg: code/datums/actions/items/organ_action.dm:6
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        obj_item_organ attached_organ = target as obj_item_organ;
        if (attached_organ.owner == null)
            return FALSE;
        return await base.IsAvailable(feedback);
    }
}

// tg: code/datums/actions/items/organ_action.dm:11
public partial class datum_action_item_action_organ_action_toggle : datum_action_item_action_organ_action
{
    // tg: code/datums/actions/items/organ_action.dm:14
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        obj_item_organ organ_target = target as obj_item_organ;
        name = I18n.T("Toggle []", organ_target.name);
    }
}

// tg: code/datums/actions/items/organ_action.dm:19
public partial class datum_action_item_action_organ_action_use : datum_action_item_action_organ_action
{
    // tg: code/datums/actions/items/organ_action.dm:22
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        obj_item_organ organ_target = target as obj_item_organ;
        name = I18n.T("Use []", organ_target.name);
    }
}

// tg: code/datums/actions/items/organ_action.dm:28 (gaps SP1: the cat fangs)
public partial class datum_action_item_action_organ_action_go_feral : datum_action_item_action_organ_action
{
    // tg: code/datums/actions/items/organ_action.dm:33
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name = pick(ability_name) as string;
    }

    // tg: code/datums/actions/items/organ_action.dm:37
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_organ_fangs_cat cat_fangs = target as obj_item_organ_fangs_cat;
        cat_fangs.toggle_feral();
        if (!Dm.truthy(cat_fangs.feral_mode))
        {
            background_icon_state = "bg_default";
            button_icon_state = "feral_mode_off";
            to_chat(cat_fangs.owner, span_notice(I18n.T("You will make unarmed attacks normally.")));
        }
        else
        {
            background_icon_state = "bg_default_on";
            button_icon_state = "feral_mode_on";
            to_chat(cat_fangs.owner, span_notice(I18n.T("You will bite when making an unarmed attack.")));
        }
        await build_all_button_icons();
        return TRUE;
    }
}

