// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/length.dm
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
 * # Length Component
 *
 * Return the length of an input
 */
// tg: code/modules/wiremod/components/math/length.dm:6
public partial class obj_item_circuit_component_length : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/length.dm:18
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_ANY) as datum_port_input;

        output = add_output_port("Length", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/length.dm:23
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        output.set_output(length(input_port.value));
        return null;
    }
}
