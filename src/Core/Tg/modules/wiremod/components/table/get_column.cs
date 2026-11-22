// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/table/get_column.dm
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
 * # Get Column Component
 *
 * Gets the column of a table and returns it as a regular list.
 */
// tg: code/modules/wiremod/components/table/get_column.dm:6
public partial class obj_item_circuit_component_get_column : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/table/get_column.dm:21
    public override async DmTask populate_ports()
    {
        received_table = add_input_port("Input", PORT_TYPE_TABLE) as datum_port_input;
        column_name = add_input_port("Column Name", PORT_TYPE_STRING) as datum_port_input;
        output_list = add_output_port("Output", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/table/get_column.dm:26
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList input_list = received_table.value as DmList;
        if (!islist(input_list) || isnum(column_name.value))
            return null;

        DmList new_list = Dm.list();
        foreach (DmList entry in Dm.each<DmList>(input_list))
        {
            object anything = entry[column_name.value];
            if (islist(anything))
                continue;
            Dm.append(new_list, anything);
        }

        output_list.set_output(new_list);
        return null;
    }
}
