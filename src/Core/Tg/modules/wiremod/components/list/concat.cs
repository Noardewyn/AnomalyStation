// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/concat.dm
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
 * # Concat List Component
 *
 * Concatenates a list with a separator
 */
// tg: code/modules/wiremod/components/list/concat.dm:6
public partial class obj_item_circuit_component_concat_list : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/concat.dm:21
    public override async DmTask populate_ports()
    {
        list_port = add_input_port("List", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        separator = add_input_port("Seperator", PORT_TYPE_STRING) as datum_port_input;

        output = add_output_port("Output", PORT_TYPE_STRING) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/concat.dm:27
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object seperator = separator.value;
        if (!Dm.truthy(seperator))
            return null;

        DmList list_input = list_port.value as DmList;
        if (!Dm.truthy(list_input))
            return null;

        DmList text_list = Dm.list();
        foreach (object entry in Dm.each(list_input))
        {
            if (isdatum(entry))
                Dm.append(text_list, PORT_TYPE_ATOM);
            else
                Dm.append(text_list, Dm.text("[]", entry));
        }

        output.set_output(text_list.Join(Dm.str(seperator)));
        return null;
    }
}
