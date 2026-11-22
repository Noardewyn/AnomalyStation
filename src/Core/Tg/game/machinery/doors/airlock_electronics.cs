// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/airlock_electronics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 6 (airlocks): the airlock electronics and their tgui window "AirlockElectronics" (D148, protocol 16: the client's
// native window, Shared/Tgui/Interfaces/AirlockElectronics.cs): the accesses, one-access, the unrestricted sides, the passed
// name and cycle id, which finish_door gives the airlock. The examine is translated (X1). The var block is generated.

// tg: code/game/machinery/doors/airlock_electronics.dm:1
public partial class obj_item_electronics_airlock : obj_item_electronics
{
    // tg: code/game/machinery/doors/airlock_electronics.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Has a neat <i>selection menu</i> for modifying airlock access levels.")));
        return dot;
    }

    /**
     * Create a copy of the electronics
     * Arguments
     * * [location][atom]- the location to create the new copy in
     */
    // tg: code/game/machinery/doors/airlock_electronics.dm:30
    public virtual obj_item_electronics_airlock create_copy(atom location = null)
    {
        //create a copy
        obj_item_electronics_airlock new_electronics = Dm.@new<obj_item_electronics_airlock>(location);
        //copy all params
        new_electronics.accesses = accesses.Copy();
        new_electronics.one_access = one_access;
        new_electronics.unres_sides = unres_sides;
        new_electronics.passed_name = passed_name;
        new_electronics.passed_cycle_id = passed_cycle_id;
        new_electronics.shell = shell;
        //return copy
        return new_electronics;
    }

    // tg: code/game/machinery/doors/airlock_electronics.dm:44
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/machinery/doors/airlock_electronics.dm:47
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AirlockElectronics", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/doors/airlock_electronics.dm:53
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList regions = Dm.list();
        DmList tgui_region_data = SSid_access.all_region_access_tgui;
        foreach (object region in Dm.each(SSid_access.station_regions))
            Dm.append(ref regions, tgui_region_data[region]);

        data["regions"] = regions;
        return data;
    }

    // tg: code/game/machinery/doors/airlock_electronics.dm:64
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["accesses"] = accesses;
        data["oneAccess"] = one_access;
        data["unres_direction"] = unres_sides;
        data["passedName"] = passed_name;
        data["passedCycleId"] = passed_cycle_id;
        data["shell"] = shell;
        return data;
    }

    ///shared by rcd & airlock electronics
    // tg: code/game/machinery/doors/airlock_electronics.dm:75
    public virtual void do_action(object action = null, DmList @params = null)
    {
        switch (action)
        {
            case "clear_all":
                accesses = Dm.list();
                one_access = 0;
                break;
            case "grant_all":
                accesses = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
                break;
            case "one_access":
                one_access = Dm.num(!Dm.truthy(one_access));
                break;
            case "set":
            {
                object access = @params["access"];
                if (!Dm.@in(access, accesses))
                    Dm.append(ref accesses, access);
                else
                    Dm.remove(ref accesses, access);
                break;
            }
            case "set_shell":
                shell = Dm.num(!!Dm.truthy(@params["on"]));
                break;
            case "direc_set":
            {
                object unres_direction = text2num(@params["unres_direction"]);
                unres_sides = Dm.num(Dm.bitxor(unres_sides, unres_direction)); //XOR, toggles only the bit that was clicked
                break;
            }
            case "grant_region":
            {
                object region = @params["region"];
                if (region == null)
                    return;
                Dm.combine(ref accesses, SSid_access.get_region_access_list(Dm.list(region)));
                break;
            }
            case "deny_region":
            {
                object region = @params["region"];
                if (region == null)
                    return;
                Dm.remove(ref accesses, SSid_access.get_region_access_list(Dm.list(region)));
                break;
            }
            case "passedName":
            {
                object new_name = trim(Dm.text("[]", @params["passedName"]), 30);
                passed_name = new_name;
                break;
            }
            case "passedCycleId":
            {
                object new_cycle_id = trim(@params["passedCycleId"], 30);
                passed_cycle_id = new_cycle_id;
                break;
            }
        }
    }

    // tg: code/game/machinery/doors/airlock_electronics.dm:112
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        do_action(action, @params);
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock_electronics.dm:119
    public override object ui_host(mob user = null)
    {
        if (Dm.truthy(holder))
            return holder;
        return this;
    }
}
