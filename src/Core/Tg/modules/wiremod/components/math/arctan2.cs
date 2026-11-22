// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/arctan2.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # Arctangent 2 function
 * A variant of arctan. When given a deltaX and deltaY, returns the angle. I will blow you out of the sky
 */
// tg: code/modules/wiremod/components/math/arctan2.dm:5
public partial class obj_item_circuit_component_arctan2 : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/arctan2.dm:20
    public override async DmTask populate_ports()
    {
        input_port_x = add_input_port("Delta X", PORT_TYPE_NUMBER) as datum_port_input;
        input_port_y = add_input_port("Delta Y", PORT_TYPE_NUMBER) as datum_port_input;
        output = add_output_port("Angle", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/arctan2.dm:25
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        output.set_output(Dm.arctan(Dm.num(input_port_x.value), Dm.num(input_port_y.value)));
        return null;
    }
}
