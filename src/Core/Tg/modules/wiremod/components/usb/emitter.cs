// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/usb/emitter.dm
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
 * USB wiremod interface for emitter machines
 */
// tg: code/modules/wiremod/components/usb/emitter.dm:4
public partial class obj_item_circuit_component_emitter : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/usb/emitter.dm:21
    public override async DmTask populate_ports()
    {
        fire = add_input_port("Fire", PORT_TYPE_SIGNAL, trigger: nameof(fire_emitter)) as datum_port_input;

        turned_on = add_output_port("Turned On", PORT_TYPE_SIGNAL) as datum_port_output;
        turned_off = add_output_port("Turned Off", PORT_TYPE_SIGNAL) as datum_port_output;
        has_fired = add_output_port("Has Fired", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/usb/emitter.dm:28
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_power_emitter)))
        {
            attached_emitter = shell as obj_machinery_power_emitter;
            RegisterSignal(attached_emitter, COMSIG_EMITTER_MACHINE_SET_ON, nameof(handle_emitter_activation));
            RegisterSignal(attached_emitter, COMSIG_EMITTER_MACHINE_ON_FIRE, nameof(handle_emitter_on_fire));
        }
    }

    // tg: code/modules/wiremod/components/usb/emitter.dm:35
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        UnregisterSignal(attached_emitter, COMSIG_EMITTER_MACHINE_SET_ON);
        UnregisterSignal(attached_emitter, COMSIG_EMITTER_MACHINE_ON_FIRE);
        attached_emitter = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/wiremod/components/usb/emitter.dm:41
    [SignalHandler]
    public virtual void handle_emitter_activation(datum source = null, object active = null)
    {
        if (Dm.truthy(active))
            turned_on.set_output(COMPONENT_SIGNAL);
        else
            turned_off.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/usb/emitter.dm:48
    [SignalHandler]
    public virtual void handle_emitter_on_fire(datum source = null, object active = null)
    {
        has_fired.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/components/usb/emitter.dm:52
    [ShouldNotSleep]
    public virtual void fire_emitter()
    {
        if (attached_emitter == null)
            return;
        if (Dm.truthy(attached_emitter.locked)) // do not fire if emitter is locked
            return;
        Waiting.NoSleep(attached_emitter.fire_beam_pulse());
    }
}
