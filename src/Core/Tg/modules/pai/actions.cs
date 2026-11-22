// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file.

// tg: code/modules/pai/actions.dm:1
public partial class datum_action_innate_pai : datum_action_innate
{
    // tg: code/modules/pai/actions.dm:6
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!ispAI(owner))
            return FALSE;
        pai_owner = (mob_living_silicon_pai)owner;
        return await base.Trigger(clicker, trigger_flags, __extra);
    }
}

// tg: code/modules/pai/actions.dm:12
public partial class datum_action_innate_pai_software : datum_action_innate_pai
{
    // tg: code/modules/pai/actions.dm:18
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await pai_owner.ui_act(); // tg calls ui_act() with no action: the window does not open (a tg slip)
        return dot;
    }
}

// tg: code/modules/pai/actions.dm:24
public partial class datum_action_innate_pai_shell : datum_action_innate_pai
{
    // tg: code/modules/pai/actions.dm:30
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(pai_owner.holoform))
            await pai_owner.fold_in(0);
        else
            await pai_owner.fold_out();
        return dot;
    }
}

// tg: code/modules/pai/actions.dm:39
public partial class datum_action_innate_pai_chassis : datum_action_innate_pai
{
    // tg: code/modules/pai/actions.dm:45
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await pai_owner.choose_chassis();
        return dot;
    }
}

// tg: code/modules/pai/actions.dm:51
public partial class datum_action_innate_pai_rest : datum_action_innate_pai
{
    // tg: code/modules/pai/actions.dm:57
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await pai_owner.toggle_resting();
        return dot;
    }
}

// tg: code/modules/pai/actions.dm:63
public partial class datum_action_innate_pai_light : datum_action_innate_pai
{
    // tg: code/modules/pai/actions.dm:70
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        pai_owner.toggle_integrated_light();
        return dot;
    }
}

// tg: code/modules/pai/actions.dm:76
public partial class datum_action_innate_pai_messenger : datum_action_innate_pai
{
    // tg: code/modules/pai/actions.dm:82
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        var pai_holder = owner.loc as obj_item_pai_card;
        if (pai_holder?.loc is not obj_item_modular_computer computer_host)
        {
            owner.balloon_alert(owner, "not in a pda!");
            return dot;
        }
        await computer_host.interact(owner);
        return dot;
    }
}
