// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/atmos_alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E6, D287): the atmospheric alert console, the air alarms in danger (priority) and in
// warning (minor) in its AtmosAlertConsole window, each cleared by a click until its next process, and the alert:1 and alert:2
// screens. The var blocks are generated (D111).

// tg: code/game/machinery/computer/atmos_alert.dm:1
public partial class obj_machinery_computer_atmos_alert : obj_machinery_computer
{
    private static ref DmList process__station_alert_areas => ref TgState.Current.obj_machinery_computer_atmos_alert__process__station_alert_areas;

    // tg: code/game/machinery/computer/atmos_alert.dm:13
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // GAP(types): the board instance is circuit_value (_machinery.cs, M5 slice 7)
        obj_item_circuitboard_computer_atmos_alert my_circuit = circuit_value as obj_item_circuitboard_computer_atmos_alert;
        Dm.append(ref dot, span_info(I18n.T("The console is set to [].", Dm.truthy(my_circuit.station_only) ? I18n.T("track all station and mining alarms") : I18n.T("track alarms on the same z-level"))));
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_alert.dm:18
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosAlertConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_alert.dm:25
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList priority = Dm.list();
        data["priority"] = priority;
        foreach (object zone in Dm.each(priority_alarms))
            Dm.append(ref priority, zone);
        DmList minor = Dm.list();
        data["minor"] = minor;
        foreach (object zone in Dm.each(minor_alarms))
            Dm.append(ref minor, zone);

        return data;
    }

    // tg: code/game/machinery/computer/atmos_alert.dm:37
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "clear"))
        {
            object zone = @params?["zone"];
            if (Dm.@in(zone, priority_alarms))
            {
                to_chat(usr, span_notice(I18n.T("Priority alarm for [] cleared.", zone)));
                Dm.remove(ref priority_alarms, zone);
                dot = TRUE;
            }
            if (Dm.@in(zone, minor_alarms))
            {
                to_chat(usr, span_notice(I18n.T("Minor alarm for [] cleared.", zone)));
                Dm.remove(ref minor_alarms, zone);
                dot = TRUE;
            }
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_alert.dm:55
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (!Dm.truthy(dot))
            return FALSE;

        float alarm_count = Dm.length(priority_alarms) + Dm.length(minor_alarms);

        priority_alarms.Cut();
        minor_alarms.Cut();

        // An area list used for station_only circuits, so we only send an alarm if we're in one of the station or mining home areas
        if (process__station_alert_areas == null)
        {
            process__station_alert_areas = Dm.list();
            foreach (object area_type in Dm.each(Dm.add(GLOB.the_station_areas, Dm.typesof(typeof(area_mine)))))
                process__station_alert_areas[area_type] = TRUE;
        }

        // Setting up a variable for checking our circuit's station_only
        // GAP(types): the board instance is circuit_value (_machinery.cs, M5 slice 7)
        obj_item_circuitboard_computer_atmos_alert my_circuit = circuit_value as obj_item_circuitboard_computer_atmos_alert;
        foreach (obj_machinery_airalarm air_alarm in Dm.each_cast<obj_machinery_airalarm>(GLOB.air_alarms))
        {
            // If the circuit has station_only, check if alarm areas are in the station list
            if (Dm.truthy(my_circuit.station_only))
            {
                if (!Dm.truthy(process__station_alert_areas[air_alarm.my_area.type]))
                    continue;
            }
            // Otherwise just check if alarms match the console's z-level
            else if (air_alarm.z != z)
                continue;

            switch (air_alarm.danger_level)
            {
                case AIR_ALARM_ALERT_NONE:
                    continue;
                case AIR_ALARM_ALERT_WARNING:
                    Dm.append(ref minor_alarms, get_area_name(air_alarm, format_text: TRUE));
                    break;
                case AIR_ALARM_ALERT_HAZARD:
                    Dm.append(ref priority_alarms, get_area_name(air_alarm, format_text: TRUE));
                    break;
            }
        }

        // Either we got new alarms, or we have no alarms anymore
        if ((alarm_count == 0) != (Dm.length(minor_alarms) + Dm.length(priority_alarms) == 0))
            update_appearance(UPDATE_ICON);

        return TRUE;
    }

    // tg: code/game/machinery/computer/atmos_alert.dm:97
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;
        if (Dm.truthy(Dm.length(priority_alarms)))
        {
            Dm.append(ref dot, "alert:2");
            return dot;
        }
        if (Dm.truthy(Dm.length(minor_alarms)))
            Dm.append(ref dot, "alert:1");
        return dot;
    }
}

public sealed partial class TgState
{
    /// tg <c>var/static/list/station_alert_areas</c> in <c>/obj/machinery/computer/atmos_alert/process</c> (rules 10.6).
    // tg: code/game/machinery/computer/atmos_alert.dm:66
    internal DmList obj_machinery_computer_atmos_alert__process__station_alert_areas;
}
