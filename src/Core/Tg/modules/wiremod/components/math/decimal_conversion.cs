// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/decimal_conversion.dm
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
 * # Decimal Conversion Component
 *
 * Return a number from an array of binary inputs.
 */
// tg: code/modules/wiremod/components/math/decimal_conversion.dm:6
public partial class obj_item_circuit_component_decimal_conversion : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/decimal_conversion.dm:22
    public override async DmTask populate_ports()
    {
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", bit_array),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("port_type", PORT_TYPE_NUMBER),
            Dm.named("prefix", "Bit"),
            Dm.named("minimum_amount", 1),
            Dm.named("maximum_amount", MAX_BITFIELD_SIZE));
        number = add_output_port("Number", PORT_TYPE_NUMBER, order: 1.1f) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/math/decimal_conversion.dm:34
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!Dm.truthy(length(bit_array)))
            return null;

        float result = 0;
        for (float iteration = 1, iteration__end = length(bit_array); iteration <= iteration__end; iteration++)
        {
            datum_port_input bit = bit_array[iteration] as datum_port_input;
            if (Dm.truthy(bit.value))
                result += Dm.pow(2, iteration - 1);
        }
        number.set_output(result);
        return null;
    }
}
