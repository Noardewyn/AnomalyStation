// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/assoc_literal.dm
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
 * # Assoc List Literal Component
 *
 * Return an associative list literal.
 */
// tg: code/modules/wiremod/components/list/assoc_literal.dm:6
public partial class obj_item_circuit_component_assoc_literal : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/assoc_literal.dm:30
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            list_output.set_datatype(PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, new_datatype));
            foreach (datum_port_input port_to_set in Dm.each_cast<datum_port_input>(entry_ports))
                port_to_set.set_datatype(new_datatype);
        }
    }

    // tg: code/modules/wiremod/components/list/assoc_literal.dm:37
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/list/assoc_literal.dm:40
    public override async DmTask populate_ports()
    {
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", key_ports),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_STRING),
            Dm.named("prefix", "Key"),
            Dm.named("minimum_amount", 1),
            Dm.named("maximum_amount", 20));
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", entry_ports),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_ANY),
            Dm.named("prefix", "Value"),
            Dm.named("minimum_amount", 1),
            Dm.named("maximum_amount", 20));
        list_output = add_output_port("Value", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY), order: 1.1f) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/assoc_literal.dm:61
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList new_literal = Dm.list();
        datum_circuit_datatype value_handler = GLOB.circuit_datatypes[list_options.value] as datum_circuit_datatype;
        datum_circuit_datatype key_handler = GLOB.circuit_datatypes[PORT_TYPE_STRING] as datum_circuit_datatype;
        for (float index = 1, index__end = length(entry_ports); index <= index__end; index++)
        {
            object entry_value = (entry_ports[index] as datum_port).value;
            // To prevent people from infinitely making lists to crash the server
            if (islist(entry_value) && Dm.num(get_list_count(entry_value as DmList, max_list_count)) >= max_list_count)
            {
                visible_message(I18n.T("[] begins to overheat!", this));
                return null;
            }
            object value_to_add = value_handler.convert_value(port, entry_value);
            if (isdatum(value_to_add))
                value_to_add = WEAKREF(value_to_add as datum);
            new_literal[key_handler.convert_value(port, (key_ports[index] as datum_port).value)] = value_to_add;
        }

        list_output.set_output(new_literal);
        return null;
    }
}
