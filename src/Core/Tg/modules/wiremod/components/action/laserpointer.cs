// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/laserpointer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The laser's dot is the laser pointer's flick overlay on the target's tile, drawn as the laser pointer item's
// (game/objects/items/devices/laserpointer.cs). The var block is generated.

/**
 * # laser pointer Component
 *
 * Points a laser at a tile or mob
 */
// tg: code/modules/wiremod/components/action/laserpointer.dm:6
public partial class obj_item_circuit_component_laserpointer : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/laserpointer.dm:23
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/action/laserpointer.dm:27  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_laserpointer__populate_options__component_options;

    // tg: code/modules/wiremod/components/action/laserpointer.dm:27
    public override void populate_options()
    {
        lasercolour_option = add_option_port("Laser Colour", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/action/laserpointer.dm:37
    public override async DmTask populate_ports()
    {
        target_input = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
        image_pixel_x = add_input_port("X-Axis Shift", PORT_TYPE_NUMBER) as datum_port_input;
        image_pixel_y = add_input_port("Y-Axis Shift", PORT_TYPE_NUMBER) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/action/laserpointer.dm:43
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom target = target_input.value as atom;
        atom_movable shell = parent.shell;
        turf target_location = get_turf(target);

        object pointer_icon_state = lasercolour_option.value;

        turf current_turf = get_location() as turf;
        if (get_dist(current_turf, target) > max_range || current_turf.z != target.z)
            return null;

        // only has cyborg flashing since felinid moving spikes time dilation when spammed and the other two features of laserpointers would be unbalanced when spammed
        if (iscyborg(target))
        {
            mob_living_silicon silicon = target as mob_living_silicon;
            log_combat(shell, silicon, "shone in the sensors", this);
            Waiting.NoSleep(silicon.flash_act(affect_silicon: TRUE)); /// no stunning, just a blind
            to_chat(silicon, span_danger(I18n.T("Your sensors were overloaded by a weakened laser shone by []!", shell)));
        }

        image laser_location = mutable_appearance(Dm.resource("icons/obj/weapons/guns/projectiles.dmi"), Dm.text("[]_laser", pointer_icon_state)) as image;

        laser_location.pixel_x = clamp(target.pixel_x + Dm.num(image_pixel_x.value), -15, 15);
        laser_location.pixel_y = clamp(target.pixel_y + Dm.num(image_pixel_y.value), -15, 15);

        target_location.flick_overlay_view(laser_location, 1 * SECONDS);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/action/laserpointer.dm:28
    internal DmList obj_item_circuit_component_laserpointer__populate_options__component_options = Dm.list(
        "red",
        "green",
        "blue",
        "purple");
}
