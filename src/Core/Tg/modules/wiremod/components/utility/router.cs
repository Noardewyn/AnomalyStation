// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/router.dm
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
 * # Router Component
 *
 * Writes one of multiple inputs to one of multiple outputs.
 */
// tg: code/modules/wiremod/components/utility/router.dm:6
public partial class obj_item_circuit_component_router : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/router.dm:29
    public override void populate_options()
    {
        router_options = add_option_port("Router Options", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/utility/router.dm:32
    public override async DmTask populate_ports()
    {
        current_type = router_options.value;
        if (input_port_amount > 1)
            input_selector = add_input_port("Input Selector", PORT_TYPE_NUMBER, @default: 1) as datum_port_input;
        if (output_port_amount > 1)
            output_selector = add_input_port("Output Selector", PORT_TYPE_NUMBER, @default: 1) as datum_port_input;
        ins = Dm.list();
        for (float port_id = 1; port_id <= input_port_amount; port_id++)
            Dm.append(ins, add_input_port(input_port_amount > 1 ? Dm.text("Input []", port_id) : "Input", current_type));
        outs = Dm.list();
        for (float port_id = 1; port_id <= output_port_amount; port_id++)
            Dm.append(outs, add_output_port(output_port_amount > 1 ? Dm.text("Output []", port_id) : "Output", current_type));
    }

    // tg: code/modules/wiremod/components/utility/router.dm:45
    public override object Destroy(float? force = null)
    {
        input_selector = null;
        output_selector = null;
        ins.Cut();
        ins = null;
        outs.Cut();
        outs = null;
        return base.Destroy(force);
    }

    // If I is in range, L[I]. If I is out of range, wrap around.
    // tg: code/modules/wiremod/components/utility/router.dm:56  #define WRAPACCESS(L, I) L[(((I || 1)-1)%length(L)+length(L))%length(L)+1] (#undef at :76)
    private static object WRAPACCESS(DmList L, object I)
        => L[Dm.mod(Dm.mod(Dm.num(Dm.truthy(I) ? I : 1) - 1, length(L)) + length(L), length(L)) + 1];

    // tg: code/modules/wiremod/components/utility/router.dm:57
    public override void pre_input_received(datum_port_input port = null)
    {
        object current_option = router_options.value;
        if (!Dm.eq(current_type, current_option))
        {
            current_type = current_option;
            foreach (datum_port_input input in Dm.each_cast<datum_port_input>(ins))
                input.set_datatype(current_type);
            foreach (datum_port_output output in Dm.each_cast<datum_port_output>(outs))
                output.set_datatype(current_type);
        }
    }

    // tg: code/modules/wiremod/components/utility/router.dm:66
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum_port_input input = WRAPACCESS(ins, input_selector != null ? input_selector.value : 1) as datum_port_input;
        datum_port_output output = WRAPACCESS(outs, output_selector != null ? output_selector.value : 1) as datum_port_output;
        output.set_output(input.value);
        return null;
    }
}
