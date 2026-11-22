// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/comparison.dm
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
 * # Comparison Component
 *
 * Compares two objects
 */
// tg: code/modules/wiremod/components/math/comparison.dm:6
public partial class obj_item_circuit_component_compare_comparison : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/math/comparison.dm:22  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_compare_comparison__populate_options__component_options;

    // tg: code/modules/wiremod/components/math/comparison.dm:20
    public override void populate_options()
    {
        comparison_option = add_option_port("Comparison Option", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/math/comparison.dm:31
    public override async DmTask populate_custom_ports()
    {
        first_port = add_input_port("A", PORT_TYPE_ANY) as datum_port_input;
        second_port = add_input_port("B", PORT_TYPE_ANY) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/math/comparison.dm:35
    public override void pre_input_received(datum_port_input port = null)
    {
        switch (comparison_option.value)
        {
            case COMP_COMPARISON_EQUAL:
            case COMP_COMPARISON_NOT_EQUAL:
                if (!Dm.eq(current_type, PORT_TYPE_ANY))
                {
                    current_type = PORT_TYPE_ANY;
                    first_port.set_datatype(PORT_TYPE_ANY);
                    second_port.set_datatype(PORT_TYPE_ANY);
                }
                break;
            default:
                if (!Dm.eq(current_type, PORT_TYPE_NUMBER))
                {
                    current_type = PORT_TYPE_NUMBER;
                    first_port.set_datatype(PORT_TYPE_NUMBER);
                    second_port.set_datatype(PORT_TYPE_NUMBER);
                }
                break;
        }
    }

    // tg: code/modules/wiremod/components/math/comparison.dm:49
    public override object do_comparisons()
    {
        object input1 = first_port.value;
        object input2 = second_port.value;
        object current_option = comparison_option.value;

        switch (current_option)
        {
            case COMP_COMPARISON_EQUAL:
                return Dm.num(Dm.eq(input1, input2));
            case COMP_COMPARISON_NOT_EQUAL:
                return Dm.num(!Dm.eq(input1, input2));
            case COMP_COMPARISON_GREATER_THAN:
                return Dm.num(Dm.gt(input1, input2));
            case COMP_COMPARISON_GREATER_THAN_OR_EQUAL:
                return Dm.num(Dm.ge(input1, input2));
            case COMP_COMPARISON_LESS_THAN:
                return Dm.num(Dm.lt(input1, input2));
            case COMP_COMPARISON_LESS_THAN_OR_EQUAL:
                return Dm.num(Dm.le(input1, input2));
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/math/comparison.dm:22
    internal DmList obj_item_circuit_component_compare_comparison__populate_options__component_options = Dm.list(
        COMP_COMPARISON_EQUAL,
        COMP_COMPARISON_NOT_EQUAL,
        COMP_COMPARISON_GREATER_THAN,
        COMP_COMPARISON_LESS_THAN,
        COMP_COMPARISON_GREATER_THAN_OR_EQUAL,
        COMP_COMPARISON_LESS_THAN_OR_EQUAL);
}
