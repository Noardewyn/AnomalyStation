// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/usb/temperature_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * USB wiremod interface for temperature pumps
 */
// tg: code/modules/wiremod/components/usb/temperature_pump.dm:4
public partial class obj_item_circuit_component_atmos_temperature_pump : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:38
    public override async DmTask populate_ports()
    {
        heat_transfer_rate = add_input_port("Set Heat Transfer Rate", PORT_TYPE_NUMBER, trigger: nameof(set_pump_heat_rate)) as datum_port_input;
        on = add_input_port("Turn On", PORT_TYPE_SIGNAL, trigger: nameof(set_pump_on)) as datum_port_input;
        off = add_input_port("Turn Off", PORT_TYPE_SIGNAL, trigger: nameof(set_pump_off)) as datum_port_input;
        request_data = add_input_port("Request Port Data", PORT_TYPE_SIGNAL, trigger: nameof(request_pump_data)) as datum_port_input;

        heat_transfer_rate_state = add_output_port("Heat Transfer Rate", PORT_TYPE_NUMBER) as datum_port_output;
        input_pressure = add_output_port("Input Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        output_pressure = add_output_port("Output Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        input_temperature = add_output_port("Input Temperature", PORT_TYPE_NUMBER) as datum_port_output;
        output_temperature = add_output_port("Output Temperature", PORT_TYPE_NUMBER) as datum_port_output;

        is_active = add_output_port("Active", PORT_TYPE_NUMBER) as datum_port_output;
        turned_on = add_output_port("Turned On", PORT_TYPE_SIGNAL) as datum_port_output;
        turned_off = add_output_port("Turned Off", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:54
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_atmospherics_components_binary_temperature_pump)))
        {
            connected_pump = shell as obj_machinery_atmospherics_components_binary_temperature_pump;
            RegisterSignal(connected_pump, COMSIG_ATMOS_MACHINE_SET_ON, nameof(handle_pump_activation));
        }
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:60
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        UnregisterSignal(connected_pump, COMSIG_ATMOS_MACHINE_SET_ON);
        connected_pump = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:65
    public override void pre_input_received(datum_port_input port = null)
    {
        heat_transfer_rate.set_value(clamp(heat_transfer_rate.value, 0, connected_pump != null ? connected_pump.max_heat_transfer_rate : 100));
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:68
    [SignalHandler]
    public virtual void handle_pump_activation(datum source = null, object active = null)
    {
        is_active.set_output(active);
        if (Dm.truthy(active))
            turned_on.set_output(COMPONENT_SIGNAL);
        else
            turned_off.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:76
    [ShouldNotSleep]
    public virtual void set_pump_heat_rate()
    {
        if (connected_pump == null)
            return;
        connected_pump.heat_transfer_rate = Dm.num(heat_transfer_rate.value);
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:82
    [ShouldNotSleep]
    public virtual void set_pump_on()
    {
        if (connected_pump == null)
            return;
        connected_pump.set_on(TRUE);
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:88
    [ShouldNotSleep]
    public virtual void set_pump_off()
    {
        if (connected_pump == null)
            return;
        connected_pump.set_on(FALSE);
        connected_pump.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/wiremod/components/usb/temperature_pump.dm:95
    [ShouldNotSleep]
    public virtual void request_pump_data()
    {
        if (connected_pump == null)
            return;
        datum_gas_mixture air_input = connected_pump.airs[1] as datum_gas_mixture;
        datum_gas_mixture air_output = connected_pump.airs[2] as datum_gas_mixture;
        heat_transfer_rate_state.set_output(connected_pump.heat_transfer_rate);
        input_pressure.set_output(air_input.return_pressure());
        output_pressure.set_output(air_output.return_pressure());
        input_temperature.set_output(air_input.return_temperature());
        output_temperature.set_output(air_output.return_temperature());
    }
}
