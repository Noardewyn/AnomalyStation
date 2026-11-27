// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/computer_circuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the modular computer's own circuit component, on the shell of every laptop, PDA and console. The var block is
// generated.

///A simple circuit component compatible with stationary consoles, laptops and PDAs, independent from programs.
// tg: code/modules/modular_computers/computers/item/computer_circuit.dm:2
public partial class obj_item_circuit_component_modpc : obj_item_circuit_component
{
    /// tg's `set_color` port (computer_circuit.dm:23): the name is atom.color's setter in C#, so the var is declared here under another name.
    public datum_port_input set_color_port;

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:25
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_item_modular_computer)))
            computer = shell as obj_item_modular_computer;
        else if (istype(shell, typeof(obj_machinery_modular_computer)))
        {
            obj_machinery_modular_computer console = shell as obj_machinery_modular_computer;
            computer = console.cpu;
        }

        if (computer == null)
            return;

        RegisterSignal(computer, COMSIG_MODULAR_COMPUTER_TURNED_ON, nameof(computer_on));
        RegisterSignal(computer, COMSIG_MODULAR_COMPUTER_SHUT_DOWN, nameof(computer_off));

        /**
         * Some mod pc have lights while some don't, but populate_ports()
         * is called before we get to know which object this has attahed to,
         * I hope you're cool with me doing it here.
         */
        if (Dm.truthy(computer.has_light) && lights == null)
        {
            lights = add_input_port("Toggle Lights", PORT_TYPE_SIGNAL, trigger: nameof(toggle_flashlight)) as datum_port_input;
            red = add_input_port("Red", PORT_TYPE_NUMBER) as datum_port_input;
            green = add_input_port("Green", PORT_TYPE_NUMBER) as datum_port_input;
            blue = add_input_port("Blue", PORT_TYPE_NUMBER) as datum_port_input;
            set_color_port = add_input_port("Set Color", PORT_TYPE_SIGNAL, trigger: nameof(set_flashlight_color)) as datum_port_input;
        }
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:51
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        if (computer != null)
        {
            UnregisterSignal(computer, Dm.list(COMSIG_MODULAR_COMPUTER_TURNED_ON, COMSIG_MODULAR_COMPUTER_SHUT_DOWN));
            computer = null;
        }
        await base.unregister_shell(shell);
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:57
    public override async DmTask populate_ports()
    {
        on_off = add_input_port("Turn On/Off", PORT_TYPE_SIGNAL, trigger: nameof(toggle_power)) as datum_port_input;
        print_text = add_input_port("Print Text", PORT_TYPE_STRING) as datum_port_input;
        print = add_input_port("Print", PORT_TYPE_SIGNAL, trigger: nameof(print_text__proc)) as datum_port_input;

        is_on = add_output_port("Turned On", PORT_TYPE_SIGNAL) as datum_port_output;
        is_off = add_output_port("Shut Down", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:65
    public override void pre_input_received(datum_port_input port = null)
    {
        if (computer == null)
            return;
        // tg sanitises the print signal port's value, not the text's; kept as tg has it
        if (COMPONENT_TRIGGERED_BY(print_text, port))
            print.set_value(html_encode(trim(print.value, MAX_PAPER_LENGTH)));
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:71
    public virtual void print_text__proc(datum source = null)
    {
        if (Dm.truthy(computer.enabled))
            computer.print_text(print_text.value);
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:75
    public virtual void toggle_power(datum source = null)
    {
        if (Dm.truthy(computer.enabled))
            Waiting.DetachSync(() => computer.shutdown_computer());
        else
            Waiting.Detach(computer.turn_on());
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:81
    public virtual async DmTask toggle_flashlight(datum source = null)
    {
        computer.toggle_flashlight();
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:84
    public virtual async DmTask set_flashlight_color(datum source = null)
    {
        red.set_value(clamp(red.value, 0, 255));
        blue.set_value(clamp(blue.value, 0, 255));
        green.set_value(clamp(green.value, 0, 255));
        computer.set_flashlight_color(rgb(Dm.num(red.value is var or__1 && Dm.truthy(or__1) ? or__1 : 0), Dm.num(green.value is var or__2 && Dm.truthy(or__2) ? or__2 : 0), Dm.num(blue.value is var or__3 && Dm.truthy(or__3) ? or__3 : 0)));
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:90
    [SignalHandler]
    public virtual void computer_on(datum source = null, mob user = null)
    {
        is_on.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/modular_computers/computers/item/computer_circuit.dm:94
    [SignalHandler]
    public virtual void computer_off(datum source = null, object loud = null)
    {
        is_off.set_output(COMPONENT_SIGNAL);
    }
}
