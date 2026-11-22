// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/satellite/satellite_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: the satellite control console with its SatelliteControl window (every satellite's id, mode and
// state, a toggle for each, and the meteor shield's coverage when the goal is the round's). The var block is generated (D111).

// tg: code/game/machinery/satellite/satellite_control.dm:1
public partial class obj_machinery_computer_sat_control : obj_machinery_computer
{
    // tg: code/game/machinery/satellite/satellite_control.dm:8
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "SatelliteControl", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/satellite/satellite_control.dm:15
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "toggle":
                await toggle(text2num(@params["id"]));
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/satellite/satellite_control.dm:25
    public virtual async DmTask toggle(object toggled_id = null)
    {
        turf current_turf = get_turf(this);
        foreach (obj_machinery_satellite satellite in Dm.each_cast<obj_machinery_satellite>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_satellite))))
        {
            if (!Dm.eq(satellite.id, toggled_id))
                continue;
            if (Dm.truthy(Dm.bitand(satellite.obj_flags, EMAGGED)))
            {
                to_chat(usr, span_warning(I18n.T("The satellite doesn't seem to respond...?")));
                return;
            }
            if (Dm.truthy(is_valid_z_level(get_turf(satellite), current_turf)))
                await satellite.toggle();
        }
    }

    // tg: code/game/machinery/satellite/satellite_control.dm:36
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["satellites"] = Dm.list();
        foreach (obj_machinery_satellite sat in Dm.each_cast<obj_machinery_satellite>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_satellite))))
        {
            Dm.append(data["satellites"] as DmList, Dm.list(Dm.list(
                Dm.kv("id", sat.id),
                Dm.kv("active", sat.active),
                Dm.kv("mode", sat.mode)
            )));
        }
        data["notice"] = notice;

        var goal = SSstation.get_station_goal(typeof(datum_station_goal_station_shield)) as datum_station_goal_station_shield;
        if (goal != null)
        {
            data["meteor_shield"] = TRUE;
            data["meteor_shield_coverage"] = goal.get_coverage();
            data["meteor_shield_coverage_max"] = goal.coverage_goal;
        }
        return data;
    }
}
