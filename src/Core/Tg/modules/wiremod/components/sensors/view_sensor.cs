// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/sensors/view_sensor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. tg's view() is the server's line of sight over the map (Builtins.view), which the 3D world keeps, so the
// sensor sees what tg's sees. The var block is generated.

/**
 * # View Sensor
 *
 * Returns all movable objects in view.
 */
// tg: code/modules/wiremod/components/sensors/view_sensor.dm:7
public partial class obj_item_circuit_component_view_sensor : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/sensors/view_sensor.dm:30
    public override async DmTask populate_ports()
    {
        range = add_input_port("Range", PORT_TYPE_NUMBER, @default: maximum_range) as datum_port_input;
        result = add_output_port("Result", PORT_TYPE_LIST(PORT_TYPE_ATOM)) as datum_port_output;
        cooldown = add_output_port("Scan On Cooldown", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/sensors/view_sensor.dm:35
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Scan Cooldown: []", DisplayTimeText(view_cooldown)), "orange", "stopwatch"));
        return dot;
    }

    // tg: code/modules/wiremod/components/sensors/view_sensor.dm:39
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (parent.shell == null)
            return null;

        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(parent.shell, COOLDOWN_CIRCUIT_VIEW_SENSOR)))
        {
            result.set_output(null);
            cooldown.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (parent == null || parent.shell == null)
        {
            result.set_output(null);
            return null;
        }

        if (!isturf(parent.shell.loc))
        {
            if (isliving(parent.shell.loc))
            {
                mob_living owner = parent.shell.loc as mob_living;
                if (parent.shell != owner.get_active_held_item() && parent.shell != owner.get_inactive_held_item())
                {
                    result.set_output(null);
                    return null;
                }
            }
            else
            {
                result.set_output(null);
                return null;
            }
        }

        float range_value = clamp(Dm.num(range.value), 0, maximum_range);
        DmList object_list = Dm.list();

        foreach (atom_movable target in Dm.each<atom_movable>(Builtins.view(range_value, get_turf(parent.shell))))
        {
            if (target.mouse_opacity == MOUSE_OPACITY_TRANSPARENT)
                continue;
            if (target.invisibility > see_invisible)
                continue;
            if (Dm.truthy(target.IsObscured()))
                continue;
            Dm.append(object_list, target);
        }

        result.set_output(object_list);
        // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
        atom_movable cd_source = parent.shell;
        LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_VIEW_SENSOR, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_VIEW_SENSOR), view_cooldown, file: "code/modules/wiremod/components/sensors/view_sensor.dm", line: 72));
        return null;
    }
}
