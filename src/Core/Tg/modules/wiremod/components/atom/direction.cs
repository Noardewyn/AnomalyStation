// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/direction.dm
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
 * # Direction Component
 *
 * Return the direction of a mob relative to the component
 */
// tg: code/modules/wiremod/components/atom/direction.dm:6
public partial class obj_item_circuit_component_direction : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/direction.dm:28
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Maximum Range: [] tiles", max_range), "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/atom/direction.dm:32
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Targeted Entity", PORT_TYPE_ATOM) as datum_port_input;

        output = add_output_port("Direction", PORT_TYPE_STRING) as datum_port_output;
        distance = add_output_port("Distance", PORT_TYPE_NUMBER) as datum_port_output;

        north = add_output_port("North", PORT_TYPE_SIGNAL) as datum_port_output;
        east = add_output_port("East", PORT_TYPE_SIGNAL) as datum_port_output;
        south = add_output_port("South", PORT_TYPE_SIGNAL) as datum_port_output;
        west = add_output_port("West", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/direction.dm:43
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        atom @object = input_port.value as atom;
        if (@object == null)
            return null;
        turf location = get_location() as turf;
        float measured_distance = get_dist(location, @object);

        if (@object.z != location.z || measured_distance > max_range)
        {
            output.set_output(null);
            return null;
        }

        float direction = get_dir(location, get_turf(@object));
        output.set_output(dir2text(direction));

        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            north.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            south.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            east.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(Dm.bitand(direction, WEST)))
            west.set_output(COMPONENT_SIGNAL);

        distance.set_output(measured_distance);
        return null;
    }
}
