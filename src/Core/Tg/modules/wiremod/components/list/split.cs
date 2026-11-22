// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/split.dm
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
 * # Split component
 *
 * Splits a string
 */
// tg: code/modules/wiremod/components/list/split.dm:6
public partial class obj_item_circuit_component_split : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/split.dm:22
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_STRING) as datum_port_input;
        separator = add_input_port("Seperator", PORT_TYPE_STRING) as datum_port_input;
        output = add_output_port("Output", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/split.dm:27
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object separator_value = separator.value;
        if (separator_value == null)
            return null;

        object value = input_port.value;
        if (value == null)
            return null;

        DmList result = Dm.splittext(value, separator_value);

        output.set_output(result);
        return null;
    }
}
