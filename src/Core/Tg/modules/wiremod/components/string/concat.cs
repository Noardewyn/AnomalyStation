// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/string/concat.dm
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
 * # Concatenate Component
 *
 * General string concatenation component. Puts strings together.
 */
// tg: code/modules/wiremod/components/string/concat.dm:6
public partial class obj_item_circuit_component_concat : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/string/concat.dm:22
    public override async DmTask populate_ports()
    {
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", concat_ports),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_STRING),
            Dm.named("prefix", "Port"),
            Dm.named("minimum_amount", 2));

        output = add_output_port("Output", PORT_TYPE_STRING, order: 1.1f) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/string/concat.dm:34
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        string result = "";
        foreach (datum_port_input input_port in Dm.each_cast<datum_port_input>(concat_ports))
        {
            object value = input_port.value;
            if (value == null)
                continue;

            result += Dm.text("[]", value);
        }

        output.set_output(result);
        return null;
    }
}
