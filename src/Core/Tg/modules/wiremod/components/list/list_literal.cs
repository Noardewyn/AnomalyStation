// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/list_literal.dm
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
 * # List Literal Component
 *
 * Return a list literal.
 */
// tg: code/modules/wiremod/components/list/list_literal.dm:6
public partial class obj_item_circuit_component_list_literal : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/list_literal.dm:27
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/list/list_literal.dm:30
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            list_output.set_datatype(PORT_TYPE_LIST(new_datatype));
            foreach (datum_port_input port_to_set in Dm.each_cast<datum_port_input>(entry_ports))
                port_to_set.set_datatype(new_datatype);
        }
    }

    // tg: code/modules/wiremod/components/list/list_literal.dm:37
    public override async DmTask populate_ports()
    {
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", entry_ports),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_ANY),
            Dm.named("prefix", "Index"),
            Dm.named("minimum_amount", 1),
            Dm.named("maximum_amount", 20));
        list_output = add_output_port("Value", PORT_TYPE_LIST(PORT_TYPE_ANY), order: 1.1f) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/list_literal.dm:49
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList new_literal = Dm.list();
        datum_circuit_datatype handler = GLOB.circuit_datatypes[list_options.value] as datum_circuit_datatype;
        foreach (datum_port_input entry_port in Dm.each_cast<datum_port_input>(entry_ports))
        {
            object value = entry_port.value;
            // To prevent people from infinitely making lists to crash the server
            if (islist(value) && Dm.num(get_list_count(value as DmList, max_list_count)) >= max_list_count)
            {
                visible_message(I18n.T("[] begins to overheat!", this));
                return null;
            }
            object value_to_add = handler.convert_value(list_output, value);
            if (isdatum(value_to_add))
                value_to_add = WEAKREF(value_to_add as datum);
            Dm.append(new_literal, Dm.list(value_to_add));
        }

        list_output.set_output(new_literal);
        return null;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/wiremod/components/list/list_literal.dm:65
    public static object get_list_count(DmList value = null, object max_list_count = null)
    {
        DmList lists_to_check = Dm.list();
        Dm.append(lists_to_check, Dm.list(value));
        float lists = 1;
        while (Dm.truthy(length(lists_to_check)))
        {
            DmList list_to_iterate = lists_to_check[length(lists_to_check)] as DmList;
            lists_to_check.len--;
            foreach (DmList list_data in Dm.each<DmList>(list_to_iterate))
            {
                Dm.append(lists_to_check, Dm.list(list_data));
                lists += 1;
            }
            if (Dm.gt(lists, max_list_count))
                return lists;
        }
        return lists;
    }
}
