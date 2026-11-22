// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/atmos_computers/_atmos_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E5 (the atmospherics control consoles): the whole file, with the AtmosControlConsole window
// (godot/scripts/Shared/Tgui/Interfaces/AtmosControlConsole.cs). The var blocks of the console and its subtypes
// (here and in atmos_controls.dm) are generated.

/// GENERAL AIR CONTROL (a.k.a atmos computer)
// tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:3
public partial class obj_machinery_computer_atmos_control : obj_machinery_computer
{
    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:23
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        scan();
    }

    ///Scans the z level for new air sensors & monitors
    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:29
    public virtual void scan()
    {
        // PRIVATE_PROC(TRUE)

        //collect all sensors that are the closest to this computer
        DmList closest_sensors = Dm.list();
        turf comp_turf = get_turf(this);
        foreach (obj_machinery sensor in Dm.each_cast<obj_machinery>(Dm.add(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_air_sensor)), SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_meter_monitored)))))
        {
            //same z level
            if (sensor.z != z)
                continue;
            //infer chamber id
            object chamber_id = "";
            if (sensor is obj_machinery_air_sensor air_sense)
                chamber_id = air_sense.chamber_id;
            else
            {
                obj_machinery_meter_monitored meter = (obj_machinery_meter_monitored)sensor;
                chamber_id = meter.chamber_id;
            }
            //track & collect closest sensors
            if (!Dm.truthy(closest_sensors[chamber_id]))
            {
                closest_sensors[chamber_id] = sensor;
                continue;
            }
            obj_machinery target = (obj_machinery)closest_sensors[chamber_id];
            if (get_dist(comp_turf, get_turf(sensor)) < get_dist(comp_turf, get_turf(target)))
                closest_sensors[chamber_id] = sensor;
        }
        //convert sensor list to id tags
        connected_sensors.Cut();
        foreach (object chamber_id in Dm.each(closest_sensors))
        {
            obj_machinery target = (obj_machinery)closest_sensors[chamber_id];
            connected_sensors[chamber_id] = target.id_tag;
        }
    }

    /// Reconnect only works for station based chambers.
    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:61
    public virtual async DmTask<object> reconnect(mob user = null)
    {
        if (!Dm.truthy(reconnecting))
            return FALSE;

        scan();

        // We only prompt the user with the sensors that are actually available.
        DmList available_devices = Dm.list();
        foreach (object chamber_identifier in Dm.each(connected_sensors))
        {
            //this sensor was destroyed at the time of reconnecting
            obj_machinery sensor = GLOB.objects_by_id_tag[connected_sensors[chamber_identifier]] as obj_machinery;
            if (QDELETED(sensor))
            {
                Dm.remove(ref connected_sensors, chamber_identifier);
                continue;
            }

            //non master computers don't have access to these station moniters. Only done to give master computer's special access to these chambers and make them feel special or something
            if (Dm.eq(chamber_identifier, ATMOS_GAS_MONITOR_DISTRO))
                continue;
            if (Dm.eq(chamber_identifier, ATMOS_GAS_MONITOR_WASTE))
                continue;

            available_devices[GLOB.station_gas_chambers[chamber_identifier]] = chamber_identifier;
        }

        // As long as we dont put any funny chars in the strings it should match.
        object new_name = await tgui_input_list(user, I18n.T("Select the device set"), I18n.T("Reconnect"), available_devices);
        if (new_name == null)
            return FALSE;
        object new_id = available_devices[new_name];
        if (new_id == null)
            return FALSE;

        Dm.remove(ref atmos_chambers, last_chamber_id);
        atmos_chambers[new_id] = new_name;
        last_chamber_id = (string)new_id;

        name = I18n.T("[][]", new_name, Dm.truthy(control) ? I18n.T(" Control") : I18n.T(" Monitor"));

        return TRUE;
    }

    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:100
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosControlConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:107
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["maxInput"] = MAX_TRANSFER_RATE;
        data["maxOutput"] = MAX_OUTPUT_PRESSURE;
        data["control"] = control;
        data["reconnecting"] = reconnecting;
        data["defaultGas"] = get_default_gas();
        Dm.append(data, return_atmos_handbooks());
        return data;
    }

    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:117
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["chambers"] = Dm.list();
        foreach (object chamber_id in Dm.each(atmos_chambers))
        {
            DmList chamber_info = Dm.list();
            chamber_info["id"] = chamber_id;
            chamber_info["name"] = atmos_chambers[chamber_id];

            obj_machinery sensor = GLOB.objects_by_id_tag[connected_sensors[chamber_id]] as obj_machinery;
            if (!QDELETED(sensor))
                chamber_info["gasmix"] = gas_mixture_parser(await sensor.return_air() as datum_gas_mixture);

            if (sensor is obj_machinery_air_sensor air_sensor) //distro & waste loop are not air sensors and don't have these functions
            {
                obj_machinery_atmospherics_components_unary_outlet_injector input = GLOB.objects_by_id_tag[Dm.truthy(air_sensor.inlet_id) ? air_sensor.inlet_id : ""] as obj_machinery_atmospherics_components_unary_outlet_injector;
                if (!QDELETED(input))
                {
                    chamber_info["input_info"] = Dm.list(
                        Dm.kv("active", input.on),
                        Dm.kv("amount", input.volume_rate));
                }

                obj_machinery_atmospherics_components_unary_vent_pump output = GLOB.objects_by_id_tag[Dm.truthy(air_sensor.outlet_id) ? air_sensor.outlet_id : ""] as obj_machinery_atmospherics_components_unary_vent_pump;
                if (!QDELETED(output))
                {
                    chamber_info["output_info"] = Dm.list(
                        Dm.kv("active", output.on),
                        Dm.kv("amount", output.internal_pressure_bound));
                }
            }

            Dm.append((DmList)data["chambers"], Dm.list(chamber_info));
        }
        return data;
    }

    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:150
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || !(Dm.truthy(control) || Dm.truthy(reconnecting)))
            return dot;

        object chamber = @params["chamber"];
        if (Dm.eq(action, "toggle_input"))
        {
            if (!Dm.@in(chamber, atmos_chambers))
                return TRUE;

            // the typed var/obj/machinery/air_sensor of a meter's chamber (distro, waste) reads as no sensor here
            obj_machinery_air_sensor sensor = GLOB.objects_by_id_tag[connected_sensors[chamber]] as obj_machinery_air_sensor;
            if (QDELETED(sensor))
                return TRUE;

            obj_machinery_atmospherics_components_unary_outlet_injector input = GLOB.objects_by_id_tag[Dm.truthy(sensor.inlet_id) ? sensor.inlet_id : ""] as obj_machinery_atmospherics_components_unary_outlet_injector;
            if (QDELETED(input))
                return TRUE;

            input.on = Dm.truthy(input.on) ? FALSE : TRUE;
            input.update_appearance(UPDATE_ICON);
            return TRUE;
        }
        else if (Dm.eq(action, "toggle_output"))
        {
            if (!Dm.@in(chamber, atmos_chambers))
                return TRUE;

            obj_machinery_air_sensor sensor = GLOB.objects_by_id_tag[connected_sensors[chamber]] as obj_machinery_air_sensor;
            if (QDELETED(sensor))
                return TRUE;

            obj_machinery_atmospherics_components_unary_vent_pump output = GLOB.objects_by_id_tag[Dm.truthy(sensor.outlet_id) ? sensor.outlet_id : ""] as obj_machinery_atmospherics_components_unary_vent_pump;
            if (QDELETED(output))
                return TRUE;

            output.on = Dm.truthy(output.on) ? FALSE : TRUE;
            output.update_appearance(UPDATE_ICON);
            return TRUE;
        }
        else if (Dm.eq(action, "adjust_input"))
        {
            if (!Dm.@in(chamber, atmos_chambers))
                return TRUE;

            obj_machinery_air_sensor sensor = GLOB.objects_by_id_tag[connected_sensors[chamber]] as obj_machinery_air_sensor;
            if (QDELETED(sensor))
                return TRUE;

            obj_machinery_atmospherics_components_unary_outlet_injector input = GLOB.objects_by_id_tag[Dm.truthy(sensor.inlet_id) ? sensor.inlet_id : ""] as obj_machinery_atmospherics_components_unary_outlet_injector;
            if (QDELETED(input))
                return TRUE;

            float? target = text2num(@params["rate"]);
            if (target == null)
                return TRUE;
            target = clamp(target.Value, 0, MAX_TRANSFER_RATE);

            input.volume_rate = clamp(target.Value, 0, Dm.num(min(((datum_gas_mixture)input.airs[1]).volume, MAX_TRANSFER_RATE)));
            return TRUE;
        }
        else if (Dm.eq(action, "adjust_output"))
        {
            if (!Dm.@in(chamber, atmos_chambers))
                return TRUE;

            obj_machinery_air_sensor sensor = GLOB.objects_by_id_tag[connected_sensors[chamber]] as obj_machinery_air_sensor;
            if (QDELETED(sensor))
                return TRUE;

            obj_machinery_atmospherics_components_unary_vent_pump output = GLOB.objects_by_id_tag[Dm.truthy(sensor.outlet_id) ? sensor.outlet_id : ""] as obj_machinery_atmospherics_components_unary_vent_pump;
            if (QDELETED(output))
                return TRUE;

            float? target = text2num(@params["rate"]);
            if (target == null)
                return TRUE;
            target = clamp(target.Value, 0, ATMOS_PUMP_MAX_PRESSURE);

            output.internal_pressure_bound = target.Value;
            return TRUE;
        }
        else if (Dm.eq(action, "reconnect"))
        {
            await reconnect(ui.user);
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/atmos_computers/_atmos_control.dm:233
    public virtual object get_default_gas()
    {
        foreach ((object gas_path, object gas_id) in Dm.pairs(GLOB.meta_gas_info[META_GAS_ID]))
        {
            if (Dm.eq(gas_id, atmos_chambers[1]))
                return gas_id;
        }

        return null;
    }
}
