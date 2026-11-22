// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/logic.dm
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
 * # Logic Component
 *
 * General logic unit with AND OR capabilities
 */
// tg: code/modules/wiremod/components/math/logic.dm:10
public partial class obj_item_circuit_component_compare_logic : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/math/logic.dm:1-3  (#undef at :73-75)
    private const string COMP_LOGIC_AND = "AND";
    private const string COMP_LOGIC_OR = "OR";
    private const string COMP_LOGIC_XOR = "XOR";

    // tg: code/modules/wiremod/components/math/logic.dm:26  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_compare_logic__populate_options__component_options;

    // tg: code/modules/wiremod/components/math/logic.dm:25
    public override void populate_options()
    {
        logic_options = add_option_port("Logic Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/math/logic.dm:33
    public override async DmTask populate_custom_ports()
    {
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", comparison_ports),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_ANY),
            Dm.named("prefix", "Port"),
            Dm.named("order", 0.9f),
            Dm.named("minimum_amount", 2));
    }

    // tg: code/modules/wiremod/components/math/logic.dm:44
    public override object do_comparisons()
    {
        object dot = FALSE;
        object current_option = logic_options.value;

        // Used by XOR
        float total_ports = 0;
        float total_true_ports = 0;
        foreach (datum_port_input port in Dm.each_cast<datum_port_input>(comparison_ports))
        {
            total_ports += 1;
            switch (current_option)
            {
                case COMP_LOGIC_AND:
                    if (!Dm.truthy(port.value))
                        return FALSE;
                    dot = TRUE;
                    break;
                case COMP_LOGIC_OR:
                    if (Dm.truthy(port.value))
                        return TRUE;
                    break;
                case COMP_LOGIC_XOR:
                    if (Dm.truthy(port.value))
                    {
                        dot = TRUE;
                        total_true_ports += 1;
                    }
                    break;
            }
        }

        if (Dm.eq(current_option, COMP_LOGIC_XOR))
        {
            if (total_ports == total_true_ports)
                return FALSE;
            if (Dm.truthy(dot))
                return TRUE;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/math/logic.dm:26
    internal DmList obj_item_circuit_component_compare_logic__populate_options__component_options = Dm.list(
        "AND",
        "OR",
        "XOR");
}
