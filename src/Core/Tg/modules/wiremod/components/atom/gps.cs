// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/gps.dm
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
 * # GPS Component
 *
 * Return the location of this
 */
// tg: code/modules/wiremod/components/atom/gps.dm:6
public partial class obj_item_circuit_component_gps : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/gps.dm:18
    public override async DmTask populate_ports()
    {
        x_pos = add_output_port("X", PORT_TYPE_NUMBER) as datum_port_output;
        y_pos = add_output_port("Y", PORT_TYPE_NUMBER) as datum_port_output;
        z_pos = add_output_port("Z", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/atom/gps.dm:23
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        turf location = get_location() as turf;

        x_pos.set_output(location?.x);
        y_pos.set_output(location?.y);
        z_pos.set_output(location?.z);
        return null;
    }
}
