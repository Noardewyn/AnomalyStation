// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/string/textcase.dm
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
 * # Text Component
 *
 * Either makes the text upper case or lower case.
 */
// tg: code/modules/wiremod/components/string/textcase.dm:9
public partial class obj_item_circuit_component_textcase : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/string/textcase.dm:1-2  (#undef at :50-51)
    private const string COMP_TEXT_LOWER = "To Lower";
    private const string COMP_TEXT_UPPER = "To Upper";

    // tg: code/modules/wiremod/components/string/textcase.dm:25  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_textcase__populate_options__component_options;

    // tg: code/modules/wiremod/components/string/textcase.dm:24
    public override void populate_options()
    {
        textcase_options = add_option_port("Textcase Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/string/textcase.dm:31
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_STRING) as datum_port_input;
        output = add_output_port("Output", PORT_TYPE_STRING) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/string/textcase.dm:35
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object value = input_port.value;
        if (value == null)
            return null;

        object result = null;
        switch (textcase_options.value)
        {
            case COMP_TEXT_LOWER:
                result = LOWER_TEXT(value);
                break;
            case COMP_TEXT_UPPER:
                result = Dm.uppertext(value);
                break;
        }

        output.set_output(result);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/string/textcase.dm:25
    internal DmList obj_item_circuit_component_textcase__populate_options__component_options = Dm.list(
        "To Lower",
        "To Upper");
}
