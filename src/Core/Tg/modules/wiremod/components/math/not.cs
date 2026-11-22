// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/not.dm
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
 * # Logic Component
 *
 * General logic unit with AND OR capabilities
 */
// tg: code/modules/wiremod/components/math/not.dm:6
public partial class obj_item_circuit_component_not : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/not.dm:18
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_ANY) as datum_port_input;

        result = add_output_port("Result", PORT_TYPE_BOOLEAN) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/not.dm:23
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        result.set_output(Dm.not(input_port.value));
        return null;
    }
}
