// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/typecast.dm
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
 * # Typecast Component
 *
 * A component that casts a value to a type if it matches or outputs null.
 */
// tg: code/modules/wiremod/components/utility/typecast.dm:6
public partial class obj_item_circuit_component_typecast : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/typecast.dm:26  var/static/list/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_typecast__populate_options__component_options;

    // tg: code/modules/wiremod/components/utility/typecast.dm:20
    public override async DmTask populate_ports()
    {
        current_type = typecast_options.value;
        input_value = add_input_port("Input", PORT_TYPE_ANY) as datum_port_input;
        output_value = add_output_port("Output", current_type) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/utility/typecast.dm:25
    public override void populate_options()
    {
        typecast_options = add_option_port("Typecast Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/utility/typecast.dm:34
    public override void pre_input_received(datum_port_input port = null)
    {
        object current_option = typecast_options.value;
        if (!Dm.eq(current_type, current_option))
        {
            current_type = current_option;
            output_value.set_datatype(current_type);
        }
    }

    // tg: code/modules/wiremod/components/utility/typecast.dm:41
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object current_option = typecast_options.value;
        object value = input_value.value;
        object value_to_set = null;
        switch (current_option)
        {
            case PORT_TYPE_STRING:
                if (istext(value))
                    value_to_set = value;
                break;
            case PORT_TYPE_NUMBER:
                if (isnum(value))
                    value_to_set = value;
                break;
            case PORT_COMPOSITE_TYPE_LIST:
                if (islist(value))
                    value_to_set = value;
                break;
            case PORT_TYPE_ATOM:
                if (isatom(value))
                    value_to_set = value;
                break;
        }

        output_value.set_output(value_to_set);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/utility/typecast.dm:26
    internal DmList obj_item_circuit_component_typecast__populate_options__component_options = Dm.list(
        PORT_TYPE_STRING,
        PORT_TYPE_NUMBER,
        PORT_COMPOSITE_TYPE_LIST,
        PORT_TYPE_ATOM);
}
