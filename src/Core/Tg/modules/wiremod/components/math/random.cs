// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/random.dm
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
 * # Random Component
 *
 * Generates a random number between specific values
 */
// tg: code/modules/wiremod/components/math/random.dm:6
public partial class obj_item_circuit_component_random : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/random.dm:21
    public override async DmTask populate_ports()
    {
        minimum = add_input_port("Minimum", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;
        maximum = add_input_port("Maximum", PORT_TYPE_NUMBER, trigger: null) as datum_port_input;

        output = add_output_port("Output", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/random.dm:27
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        float min_val = Dm.num(Dm.truthy(minimum.value) ? minimum.value : 0);
        float max_val = Dm.num(Dm.truthy(maximum.value) ? maximum.value : 0);

        if (max_val < min_val)
        {
            output.set_output(0);
            return null;
        }

        output.set_output(Dm.rand(min_val, max_val));
        return null;
    }
}
