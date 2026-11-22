// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/table/index_table.dm
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
 * # Index Table Component
 *
 * Gets the row of a table as an associative list using the index inputted. Will return no value if the index is invalid or a proper table is not returned.
 */
// tg: code/modules/wiremod/components/table/index_table.dm:6
public partial class obj_item_circuit_component_index_table : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/table/index_table.dm:21
    public override async DmTask populate_ports()
    {
        received_table = add_input_port("Input", PORT_TYPE_TABLE) as datum_port_input;
        target_index = add_input_port("Index", PORT_TYPE_NUMBER) as datum_port_input;

        output_list = add_output_port("Output", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY)) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/table/index_table.dm:27
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList target_list = received_table.value as DmList;
        if (!islist(target_list) || !Dm.truthy(length(target_list)))
        {
            output_list.set_output(null);
            return null;
        }

        object index = target_index.value;
        if (Dm.lt(index, 1) || Dm.gt(index, length(target_list)))
        {
            output_list.set_output(null);
            return null;
        }

        output_list.set_output(target_list[index]);
        return null;
    }
}
