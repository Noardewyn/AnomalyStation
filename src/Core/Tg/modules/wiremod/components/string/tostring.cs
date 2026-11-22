// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/string/tostring.dm
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
 * # To String Component
 *
 * Converts any value into a string
 */
// tg: code/modules/wiremod/components/string/tostring.dm:6
public partial class obj_item_circuit_component_tostring : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/string/tostring.dm:21
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_ANY) as datum_port_input;
        output = add_output_port("Output", PORT_TYPE_STRING) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/string/tostring.dm:25
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object value = input_port.value;
        if (isatom(value))
        {
            turf location = get_location() as turf;
            turf target_location = get_turf(value as atom);
            if (target_location.z != location.z || get_dist(location, target_location) > max_range)
            {
                output.set_output(PORT_TYPE_ATOM);
                return null;
            }
        }

        output.set_output(Dm.text("[]", value));
        return null;
    }
}
