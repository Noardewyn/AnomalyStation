// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/table/select.dm
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
 * # Select Component
 *
 * Selects a list from a list of lists by a specific column. Used only by USBs for communications to and from computers with lists of varying sizes.
 */
// tg: code/modules/wiremod/components/table/select.dm:6
public partial class obj_item_circuit_component_select : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/table/select.dm:29  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_select__populate_options__component_options;

    // tg: code/modules/wiremod/components/table/select.dm:28
    public override void populate_options()
    {
        comparison_options = add_option_port("Comparison Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/table/select.dm:39
    public override async DmTask populate_ports()
    {
        received_table = add_input_port("Input", PORT_TYPE_TABLE) as datum_port_input;
        column_name = add_input_port("Column Name", PORT_TYPE_STRING) as datum_port_input;
        comparison_input = add_input_port("Comparison Input", PORT_TYPE_ANY) as datum_port_input;

        filtered_table = add_output_port("Output", PORT_TYPE_TABLE) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/table/select.dm:46
    public override void pre_input_received(datum_port_input port = null)
    {
        object current_option = comparison_options.value;
        switch (current_option)
        {
            case COMP_COMPARISON_EQUAL:
            case COMP_COMPARISON_NOT_EQUAL:
                if (!Dm.eq(current_type, PORT_TYPE_ANY))
                {
                    current_type = PORT_TYPE_ANY;
                    comparison_input.set_datatype(PORT_TYPE_ANY);
                }
                break;
            default:
                if (!Dm.eq(current_type, PORT_TYPE_NUMBER))
                {
                    current_type = PORT_TYPE_NUMBER;
                    comparison_input.set_datatype(PORT_TYPE_NUMBER);
                }
                break;
        }
    }

    // tg: code/modules/wiremod/components/table/select.dm:59
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object current_option = comparison_options.value;
        DmList input_list = received_table.value as DmList;
        if (!islist(input_list) || isnum(column_name.value))
            return null;

        object comparison_value = comparison_input.value;
        DmList new_list = Dm.list();
        foreach (DmList entry in Dm.each<DmList>(input_list))
        {
            object anything = entry[column_name.value];
            if (islist(anything))
                continue;
            if (!Dm.eq(current_option, COMP_COMPARISON_EQUAL) && !Dm.eq(current_option, COMP_COMPARISON_NOT_EQUAL) && !isnum(anything))
                continue;
            bool add_to_list = false;
            switch (current_option)
            {
                case COMP_COMPARISON_EQUAL:
                    add_to_list = Dm.eq(anything, comparison_value);
                    break;
                case COMP_COMPARISON_NOT_EQUAL:
                    add_to_list = !Dm.eq(anything, comparison_value);
                    break;
                case COMP_COMPARISON_GREATER_THAN:
                    add_to_list = Dm.gt(anything, comparison_value);
                    break;
                case COMP_COMPARISON_GREATER_THAN_OR_EQUAL:
                    add_to_list = Dm.ge(anything, comparison_value);
                    break;
                case COMP_COMPARISON_LESS_THAN:
                    add_to_list = Dm.lt(anything, comparison_value);
                    break;
                case COMP_COMPARISON_LESS_THAN_OR_EQUAL:
                    add_to_list = Dm.le(anything, comparison_value);
                    break;
            }

            if (add_to_list)
                Dm.append(new_list, Dm.list(entry));
        }

        filtered_table.set_output(new_list);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/table/select.dm:29
    internal DmList obj_item_circuit_component_select__populate_options__component_options = Dm.list(
        COMP_COMPARISON_EQUAL,
        COMP_COMPARISON_NOT_EQUAL,
        COMP_COMPARISON_GREATER_THAN,
        COMP_COMPARISON_LESS_THAN,
        COMP_COMPARISON_GREATER_THAN_OR_EQUAL,
        COMP_COMPARISON_LESS_THAN_OR_EQUAL);
}
