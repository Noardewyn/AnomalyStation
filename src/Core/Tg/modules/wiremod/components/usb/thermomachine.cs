// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/usb/thermomachine.dm
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
 * USB wiremod interface for temperature control unit machines
 */
// tg: code/modules/wiremod/components/usb/thermomachine.dm:4
public partial class obj_item_circuit_component_thermomachine : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/usb/thermomachine.dm:32
    public override async DmTask populate_ports()
    {
        temperature = add_input_port("Set Temperature", PORT_TYPE_NUMBER, trigger: nameof(set_temperature)) as datum_port_input;
        on = add_input_port("Turn On", PORT_TYPE_SIGNAL, trigger: nameof(set_machine_on)) as datum_port_input;
        off = add_input_port("Turn Off", PORT_TYPE_SIGNAL, trigger: nameof(set_machine_off)) as datum_port_input;
        request_data = add_input_port("Request Port Data", PORT_TYPE_SIGNAL, trigger: nameof(request_pump_data)) as datum_port_input;

        port_pressure = add_output_port("Pressure", PORT_TYPE_NUMBER) as datum_port_output;
        port_temperature = add_output_port("Temperature", PORT_TYPE_NUMBER) as datum_port_output;

        is_active = add_output_port("Active", PORT_TYPE_NUMBER) as datum_port_output;
        turned_on = add_output_port("Turned On", PORT_TYPE_SIGNAL) as datum_port_output;
        turned_off = add_output_port("Turned Off", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:45
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_atmospherics_components_unary_thermomachine)))
        {
            connected_machine = shell as obj_machinery_atmospherics_components_unary_thermomachine;
            RegisterSignal(connected_machine, COMSIG_ATMOS_MACHINE_SET_ON, nameof(handle_pump_activation));
        }
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:51
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        UnregisterSignal(connected_machine, COMSIG_ATMOS_MACHINE_SET_ON);
        connected_machine = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:56
    public override void pre_input_received(datum_port_input port = null)
    {
        if (connected_machine != null)
        {
            float min = connected_machine.min_temperature;
            float max = connected_machine.max_temperature;
            temperature.set_value(clamp(temperature.value, min, max));
        }
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:62
    [SignalHandler]
    public virtual void handle_pump_activation(datum source = null, object active = null)
    {
        is_active.set_output(active);
        if (Dm.truthy(active))
            turned_on.set_output(COMPONENT_SIGNAL);
        else
            turned_off.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:70
    [ShouldNotSleep]
    public virtual void set_temperature()
    {
        if (connected_machine == null)
            return;
        float min = connected_machine.min_temperature;
        float max = connected_machine.max_temperature;
        connected_machine.target_temperature = Dm.num_or_null(clamp(temperature.value, min, max));
        Waiting.NoSleep(connected_machine.update_icon_state());
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:79
    [ShouldNotSleep]
    public virtual void set_machine_on()
    {
        if (connected_machine == null)
            return;
        connected_machine.set_on(TRUE);
        connected_machine.update_use_power(ACTIVE_POWER_USE);
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:86
    [ShouldNotSleep]
    public virtual void set_machine_off()
    {
        if (connected_machine == null)
            return;
        connected_machine.set_on(FALSE);
        connected_machine.update_use_power(IDLE_POWER_USE);
        connected_machine.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/wiremod/components/usb/thermomachine.dm:94
    [ShouldNotSleep]
    public virtual void request_pump_data()
    {
        if (connected_machine == null)
            return;
        datum_gas_mixture port = connected_machine.airs[1] as datum_gas_mixture;
        port_pressure.set_output(port.return_pressure());
        port_temperature.set_output(port.return_temperature());
    }
}
