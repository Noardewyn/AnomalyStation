// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the avatar's Open Domain Information action and its AvatarHelp window (the client's
// Tgui/Interfaces/AvatarHelp.cs draws it). The var blocks are generated (D111).

// tg: code/modules/bitrunning/abilities.dm:1
public partial class datum_avatar_help_text : datum
{
    // tg: code/modules/bitrunning/abilities.dm:5
    [DmParams("help_text")]
    public override async DmTask New(params object[] __extra)
    {
        object help_text = Dm.arg(__extra, 0);
        this.help_text = help_text;
        await DmTask.CompletedTask;
    }

    // tg: code/modules/bitrunning/abilities.dm:8
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AvatarHelp");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/bitrunning/abilities.dm:14
    public override object ui_state(mob user = null) => GLOB.always_state;

    // tg: code/modules/bitrunning/abilities.dm:17
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["help_text"] = help_text;

        return data;
    }
}

/// Displays information about the current virtual domain.
// tg: code/modules/bitrunning/abilities.dm:25
public partial class datum_action_avatar_domain_info : datum_action
{
    // tg: code/modules/bitrunning/abilities.dm:30
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name = "Open Domain Information";
    }

    // tg: code/modules/bitrunning/abilities.dm:34
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;

        await ((datum)target).ui_interact(owner);
        return dot;
    }
}
