// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/arithmetic.dm
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
 * # Arithmetic Component
 *
 * General arithmetic unit with add/sub/mult/divide capabilities
 * This one only works with numbers.
 */
// tg: code/modules/wiremod/components/math/arithmetic.dm:16
public partial class obj_item_circuit_component_arithmetic : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/arithmetic.dm:1-8  (#undef at :93-100)
    private const string COMP_ARITHMETIC_ADD = "Add";
    private const string COMP_ARITHMETIC_SUBTRACT = "Subtract";
    private const string COMP_ARITHMETIC_MULTIPLY = "Multiply";
    private const string COMP_ARITHMETIC_DIVIDE = "Divide";
    private const string COMP_ARITHMETIC_MODULO = "Modulo";
    private const string COMP_ARITHMETIC_MIN = "Minimum";
    private const string COMP_ARITHMETIC_MAX = "Maximum";
    private const string COMP_ARITHMETIC_POWER = "Power";

    // tg: code/modules/wiremod/components/math/arithmetic.dm:33  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_arithmetic__populate_options__component_options;

    // tg: code/modules/wiremod/components/math/arithmetic.dm:33
    public override void populate_options()
    {
        arithmetic_option = add_option_port("Arithmetic Option", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/math/arithmetic.dm:46
    public override async DmTask populate_ports()
    {
        arithmetic_ports = Dm.list();
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", arithmetic_ports),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_NUMBER),
            Dm.named("prefix", "Port"),
            Dm.named("minimum_amount", 2));
        output = add_output_port("Output", PORT_TYPE_NUMBER, order: 1.1f) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/arithmetic.dm:58
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList ports = arithmetic_ports.Copy();
        datum_port_input first_port = popleft(ports) as datum_port_input;
        object result = first_port.value;

        foreach (datum_port_input input_port in Dm.each_cast<datum_port_input>(ports))
        {
            object value = input_port.value;
            if (value == null)
                continue;

            bool @break = false;
            switch (arithmetic_option.value)
            {
                case COMP_ARITHMETIC_ADD:
                    result = Dm.add(result, value);
                    break;
                case COMP_ARITHMETIC_SUBTRACT:
                    result = Dm.sub(result, value);
                    break;
                case COMP_ARITHMETIC_MULTIPLY:
                    result = Dm.mul(result, value);
                    break;
                case COMP_ARITHMETIC_DIVIDE:
                    // Protect from div by zero errors.
                    if (Dm.eq(value, 0))
                    {
                        result = null;
                        @break = true;
                        break;
                    }
                    result = Dm.div(result, value);
                    break;
                case COMP_ARITHMETIC_MODULO:
                    result = Dm.mod(result, value);
                    break;
                case COMP_ARITHMETIC_MAX:
                    result = Dm.max(result, value);
                    break;
                case COMP_ARITHMETIC_MIN:
                    result = Dm.min(result, value);
                    break;
                case COMP_ARITHMETIC_POWER:
                    result = Dm.pow(result, value);
                    break;
            }
            if (@break)
                break;
        }

        output.set_output(result);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/math/arithmetic.dm:33
    internal DmList obj_item_circuit_component_arithmetic__populate_options__component_options = Dm.list(
        "Add",
        "Subtract",
        "Multiply",
        "Divide",
        "Modulo",
        "Minimum",
        "Maximum",
        "Power");
}
