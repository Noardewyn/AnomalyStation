// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/index.dm
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
 * # Index Component
 *
 * Return the index of a list
 */
// tg: code/modules/wiremod/components/list/index.dm:6
public partial class obj_item_circuit_component_index : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/index.dm:24
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/list/index.dm:27
    public virtual void make_list_port()
    {
        list_port = add_input_port("List", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/index.dm:30
    public override async DmTask populate_ports()
    {
        index_port = add_input_port("Index", index_type) as datum_port_input;
        make_list_port();

        output = add_output_port("Value", PORT_TYPE_ANY) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/index.dm:36
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_type = list_options.value;
            list_port.set_datatype(PORT_TYPE_LIST(new_type));
            output.set_datatype(new_type);
        }
    }

    // tg: code/modules/wiremod/components/list/index.dm:42
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object index = index_port.value;
        DmList list_input = list_port.value as DmList;

        if (!islist(list_input) || !Dm.truthy(index))
        {
            output.set_output(null);
            return null;
        }

        if (isnum(index) && (Dm.lt(index, 1) || Dm.gt(index, length(list_input))))
        {
            output.set_output(null);
            return null;
        }

        output.set_output(list_input[index]);
        return null;
    }
}

// tg: code/modules/wiremod/components/list/index.dm:57
public partial class obj_item_circuit_component_index_assoc_string : obj_item_circuit_component_index
{
    // tg: code/modules/wiremod/components/list/index.dm:63
    public override void make_list_port()
    {
        list_port = add_input_port("List", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/index.dm:66
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_type = list_options.value;
            list_port.set_datatype(PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, new_type));
            output.set_datatype(new_type);
        }
    }
}
