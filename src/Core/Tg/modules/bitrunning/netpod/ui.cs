// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the NetpodOutfits window (the client's Tgui/Interfaces/NetpodOutfits.cs draws it).

// tg: code/modules/bitrunning/netpod/ui.dm:1
public partial class obj_machinery_netpod : obj_machinery
{
    // tg: code/modules/bitrunning/netpod/ui.dm:1
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational) || occupant != null)
            return null;

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "NetpodOutfits");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/bitrunning/netpod/ui.dm:12
    public override DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["netsuit"] = netsuit;
        return DmTask<object>.FromResult(data);
    }

    // tg: code/modules/bitrunning/netpod/ui.dm:19
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        if (!Dm.truthy(length(cached_outfits)))
            Dm.append(ref cached_outfits, make_outfit_collection("Jobs", subtypesof(typeof(datum_outfit_job))));

        data["collections"] = cached_outfits;

        return data;
    }

    // tg: code/modules/bitrunning/netpod/ui.dm:30
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        switch (action)
        {
            case "select_outfit":
                Type new_suit = resolve_outfit(@params["outfit"]) as Type;
                if (new_suit != null)
                {
                    netsuit = new_suit;
                    return TRUE;
                }
                break;
        }

        return FALSE;
    }

    // tg: code/modules/bitrunning/netpod/ui.dm:44
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return Dm.truthy(panel_open) ? UI_CLOSE : await base.ui_status(user, state);
    }
}
