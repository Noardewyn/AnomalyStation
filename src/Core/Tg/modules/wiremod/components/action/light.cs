// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/light.dm
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
 * # Light Component
 *
 * Emits a light of a specific brightness and colour. Requires a shell.
 */
// tg: code/modules/wiremod/components/action/light.dm:6
public partial class obj_item_circuit_component_light : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/light.dm:26
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Brightness: []", max_power), "orange", "lightbulb"));
        return dot;
    }

    // tg: code/modules/wiremod/components/action/light.dm:30
    public override async DmTask populate_ports()
    {
        red = add_input_port("Red", PORT_TYPE_NUMBER) as datum_port_input;
        green = add_input_port("Green", PORT_TYPE_NUMBER) as datum_port_input;
        blue = add_input_port("Blue", PORT_TYPE_NUMBER) as datum_port_input;
        brightness = add_input_port("Brightness", PORT_TYPE_NUMBER) as datum_port_input;

        on = add_input_port("On", PORT_TYPE_BOOLEAN) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/action/light.dm:38
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        trigger_component();
    }

    // tg: code/modules/wiremod/components/action/light.dm:42
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        await shell.set_light_on(FALSE);
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/components/action/light.dm:46
    public override void pre_input_received(datum_port_input port = null)
    {
        brightness.set_value(clamp((brightness.value is var or__1 && Dm.truthy(or__1) ? or__1 : 0), 0, max_power));
        red.set_value(clamp(red.value, 0, 255));
        blue.set_value(clamp(blue.value, 0, 255));
        green.set_value(clamp(green.value, 0, 255));
        DmList hsl = rgb2num(rgb(Dm.num(red.value is var or__2 && Dm.truthy(or__2) ? or__2 : 0), Dm.num(green.value is var or__3 && Dm.truthy(or__3) ? or__3 : 0), Dm.num(blue.value is var or__4 && Dm.truthy(or__4) ? or__4 : 0)), COLORSPACE_HSL);
        shell_light_color = rgb(Dm.num(hsl[1]), Dm.num(hsl[2]), max(min_lightness, Dm.num(hsl[3])), space: COLORSPACE_HSL);
    }

    // tg: code/modules/wiremod/components/action/light.dm:54
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (parent.shell != null)
            set_atom_light(parent.shell);
        return null;
    }

    // tg: code/modules/wiremod/components/action/light.dm:58
    public virtual void set_atom_light(atom_movable target_atom = null)
    {
        // Clamp anyways just for safety
        float bright_val = min(max(Dm.num(brightness.value is var or__1 && Dm.truthy(or__1) ? or__1 : 0), 0), max_power);

        target_atom.set_light_power(bright_val);
        target_atom.set_light_range(bright_val);
        target_atom.set_light_color(shell_light_color);
        Waiting.NoSleep(target_atom.set_light_on(Dm.num(Dm.truthy(on.value))));
    }
}
