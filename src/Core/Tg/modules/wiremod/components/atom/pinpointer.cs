// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/pinpointer.dm
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
 * # Proximity Pinpointer Component
 *
 * Return the location of its input.
 */
// tg: code/modules/wiremod/components/atom/pinpointer.dm:6
public partial class obj_item_circuit_component_pinpointer : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/pinpointer.dm:22
    public override async DmTask populate_ports()
    {
        target = add_input_port("Target entity", PORT_TYPE_ATOM, FALSE) as datum_port_input;

        x_pos = add_output_port("X", PORT_TYPE_NUMBER) as datum_port_output;
        y_pos = add_output_port("Y", PORT_TYPE_NUMBER) as datum_port_output;
        z_pos = add_output_port("Z", PORT_TYPE_NUMBER) as datum_port_output;
        on_error = add_output_port("Failed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/pinpointer.dm:30
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (target.value == null)
        {
            x_pos.set_output(null);
            y_pos.set_output(null);
            z_pos.set_output(null);
            on_error.set_output(COMPONENT_SIGNAL);
            return null;
        }

        atom target_entity = target.value as atom;

        if (Dm.truthy(is_in_sight(target_entity, get_location() as atom)) && IN_GIVEN_RANGE(get_location(), target_entity, Dm.num(max_range)))
        {
            turf location = get_turf(target_entity);

            x_pos.set_output(location?.x);
            y_pos.set_output(location?.y);
            z_pos.set_output(location?.z);
        }
        else
        {
            x_pos.set_output(null);
            y_pos.set_output(null);
            z_pos.set_output(null);
            on_error.set_output(COMPONENT_SIGNAL);
            return TRUE;
        }
        return null;
    }
}
