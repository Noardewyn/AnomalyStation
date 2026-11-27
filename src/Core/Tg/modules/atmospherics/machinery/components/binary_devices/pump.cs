// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/binary_devices/pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the pressure pump): the init, the ctrl and alt clicks (on/off, maximum pressure), the picture,
// process_atmos and can_unwrench. Deferred leaves: the screentip context (M9) and the investigate logs (M10). Generated live-partial stub: add_context (M9). The M7 supermatter slice, part 2: the tgui window (ui_interact, ui_data, ui_act). The USB port and its circuit component (/obj/item/circuit_component/atmos_pump) are live. The var block is generated.

// Every cycle, the pump uses the air in air_in to try and make air_out the perfect pressure.
//
// node1, air1, network1 correspond to input
// node2, air2, network2 correspond to output
//
// Thus, the two variables affect pump operation are set in New():
//   air1.volume
//     This is the volume of gas available to the pump that may be transferred to the output
//   air2.volume
//     Higher quantities of this cause more air to be perfected later
//     but overall network volume is also increased as this increases...

// tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:13
public partial class obj_machinery_atmospherics_components_binary_pump : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_atmos_pump)), only_root_path: TRUE)));
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool
        Dm.Deferred("code/modules/atmospherics/machinery/components/binary_devices/pump.dm:30", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:37
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (Dm.truthy(is_operational))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            balloon_alert(user, Dm.text("turned []", Dm.truthy(on) ? "on" : "off"));
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(user)), INVESTIGATE_ATMOS);
            return CLICK_ACTION_SUCCESS;
        }
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:45
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (target_pressure == MAX_OUTPUT_PRESSURE)
            return CLICK_ACTION_BLOCKING;

        target_pressure = MAX_OUTPUT_PRESSURE;
        investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(user)), INVESTIGATE_ATMOS);
        balloon_alert(user, Dm.text("pressure output set to [] kPa", target_pressure));
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:55
    public override void update_icon_nopipes(params object[] __extra)
    {
        icon_state = (Dm.truthy(on) && Dm.truthy(is_operational)) ? Dm.text("pump_on-[]", set_overlay_offset(piping_layer)) : Dm.text("pump_off-[]", set_overlay_offset(piping_layer));
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:58
    public override object process_atmos(object seconds_per_tick = null)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational))
            return null;

        datum_gas_mixture input_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture output_air = airs[2] as datum_gas_mixture;
        datum_gas_mixture output_pipenet_air = (parents[2] as datum_pipeline).air;

        if (Dm.truthy(input_air.pump_gas_to(output_air, target_pressure, output_pipenet_air: output_pipenet_air)))
            update_parents();
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:69  (the M7 supermatter slice, part 2: the engine loop's pumps)
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AtmosPump", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:75
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["pressure"] = round(Dm.num(target_pressure));
        data["max_pressure"] = round(MAX_OUTPUT_PRESSURE);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:82
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "power"))
        {
            set_on(Dm.truthy(on) ? FALSE : TRUE);
            investigate_log(Dm.text("was turned [] by []", Dm.truthy(on) ? "on" : "off", key_name(usr)), INVESTIGATE_ATMOS);
            dot = TRUE;
        }
        else if (Dm.eq(action, "pressure"))
        {
            object pressure = @params["pressure"];
            if (Dm.eq(pressure, "max"))
            {
                pressure = MAX_OUTPUT_PRESSURE;
                dot = TRUE;
            }
            else if (text2num(pressure) != null)
            {
                pressure = text2num(pressure);
                dot = TRUE;
            }
            if (Dm.truthy(dot))
            {
                target_pressure = clamp(Dm.num(pressure), 0, MAX_OUTPUT_PRESSURE);
                investigate_log(Dm.text("was set to [] kPa by []", target_pressure, key_name(usr)), INVESTIGATE_ATMOS);
            }
        }
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:104
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (Dm.truthy(dot) && Dm.truthy(on) && Dm.truthy(is_operational))
        {
            to_chat(user, span_warning(Dm.text("You cannot unwrench [], turn it off first!", this)));
            return FALSE;
        }
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:134
public partial class obj_item_circuit_component_atmos_pump : obj_item_circuit_component
{
    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:166
    public override async DmTask populate_ports()
    {
        pressure_value = add_input_port("New Pressure", PORT_TYPE_NUMBER, trigger: nameof(set_pump_pressure)) as datum_port_input;
        on = add_input_port("Turn On", PORT_TYPE_SIGNAL, trigger: nameof(set_pump_on)) as datum_port_input;
        off = add_input_port("Turn Off", PORT_TYPE_SIGNAL, trigger: nameof(set_pump_off)) as datum_port_input;
        request_data = add_input_port("Request Port Data", PORT_TYPE_SIGNAL, trigger: nameof(request_pump_data)) as datum_port_input;

        input_pressure = add_output_port("Input Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        output_pressure = add_output_port("Output Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        input_temperature = add_output_port("Input Temperature", PORT_TYPE_NUMBER) as datum_port_output;
        output_temperature = add_output_port("Output Temperature", PORT_TYPE_NUMBER) as datum_port_output;

        is_active = add_output_port("Active", PORT_TYPE_BOOLEAN) as datum_port_output;
        turned_on = add_output_port("Turned On", PORT_TYPE_SIGNAL) as datum_port_output;
        turned_off = add_output_port("Turned Off", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:181
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_atmospherics_components_binary_pump)))
        {
            connected_pump = shell as obj_machinery_atmospherics_components_binary_pump;
            RegisterSignal(connected_pump, COMSIG_ATMOS_MACHINE_SET_ON, nameof(handle_pump_activation));
        }
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:187
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        UnregisterSignal(connected_pump, COMSIG_ATMOS_MACHINE_SET_ON);
        connected_pump = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:192
    public override void pre_input_received(datum_port_input port = null)
    {
        pressure_value.set_value(clamp(pressure_value.value, 0, MAX_OUTPUT_PRESSURE));
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:195
    [SignalHandler]
    public virtual void handle_pump_activation(datum source = null, object active = null)
    {
        is_active.set_output(active);
        if (Dm.truthy(active))
            turned_on.set_output(COMPONENT_SIGNAL);
        else
            turned_off.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:203
    [ShouldNotSleep]
    public virtual void set_pump_pressure()
    {
        if (connected_pump == null)
            return;
        connected_pump.target_pressure = Dm.num(pressure_value.value);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:209
    [ShouldNotSleep]
    public virtual void set_pump_on()
    {
        if (connected_pump == null)
            return;
        connected_pump.set_on(TRUE);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:215
    [ShouldNotSleep]
    public virtual void set_pump_off()
    {
        if (connected_pump == null)
            return;
        connected_pump.set_on(FALSE);
        connected_pump.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/binary_devices/pump.dm:222
    [ShouldNotSleep]
    public virtual void request_pump_data()
    {
        if (connected_pump == null)
            return;
        datum_gas_mixture air_input = connected_pump.airs[1] as datum_gas_mixture;
        datum_gas_mixture air_output = connected_pump.airs[2] as datum_gas_mixture;
        input_pressure.set_output(air_input.return_pressure());
        output_pressure.set_output(air_output.return_pressure());
        input_temperature.set_output(air_input.return_temperature());
        output_temperature.set_output(air_output.return_temperature());
    }
}
