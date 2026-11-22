// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/trigonometry.dm
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
 * # Trigonometric Component
 *
 * General trigonometric unit with sine, cosine, tangent and their inverse functions.
 * This one only works with numbers.
 */
// tg: code/modules/wiremod/components/math/trigonometry.dm:15
public partial class obj_item_circuit_component_trigonometry : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/trigonometry.dm:1-6  (#undef at :65-70)
    private const string COMP_TRIGONOMETRY_SINE = "Sine";
    private const string COMP_TRIGONOMETRY_COSINE = "Cosine";
    private const string COMP_TRIGONOMETRY_TANGENT = "Tangent";
    private const string COMP_TRIGONOMETRY_ARCSINE = "Arcsine";
    private const string COMP_TRIGONOMETRY_ARCCOSINE = "Arccosine";
    private const string COMP_TRIGONOMETRY_ARCTANGENT = "Arctangent";

    // tg: code/modules/wiremod/components/math/trigonometry.dm:31  var/static/component_functions
    private static ref DmList populate_options__component_functions => ref TgState.Current.obj_item_circuit_component_trigonometry__populate_options__component_functions;

    // tg: code/modules/wiremod/components/math/trigonometry.dm:30
    public override void populate_options()
    {
        trigonometric_function = add_option_port("Trigonometric Function", populate_options__component_functions) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/math/trigonometry.dm:41
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_NUMBER) as datum_port_input;
        output = add_output_port("Output", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/trigonometry.dm:45
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object result = input_port.value;

        switch (trigonometric_function.value)
        {
            case COMP_TRIGONOMETRY_SINE:
                result = Dm.sin(Dm.num(result));
                break;
            case COMP_TRIGONOMETRY_COSINE:
                result = Dm.cos(Dm.num(result));
                break;
            case COMP_TRIGONOMETRY_TANGENT:
                result = Dm.tan(Dm.num(result));
                break;
            case COMP_TRIGONOMETRY_ARCSINE:
                result = Dm.arcsin(Dm.num(result));
                break;
            case COMP_TRIGONOMETRY_ARCCOSINE:
                result = Dm.arccos(Dm.num(result));
                break;
            case COMP_TRIGONOMETRY_ARCTANGENT:
                result = Dm.arctan(Dm.num(result));
                break;
        }

        output.set_output(result);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/math/trigonometry.dm:31
    internal DmList obj_item_circuit_component_trigonometry__populate_options__component_functions = Dm.list(
        "Sine",
        "Cosine",
        "Tangent",
        "Arcsine",
        "Arccosine",
        "Arctangent");
}
