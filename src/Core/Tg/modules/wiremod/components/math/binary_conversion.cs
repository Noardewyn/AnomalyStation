// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/math/binary_conversion.dm
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
 * # Binary Conversion Component
 *
 * Return an array of binary digits from a number input.
 */
// tg: code/modules/wiremod/components/math/binary_conversion.dm:6
public partial class obj_item_circuit_component_binary_conversion : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/math/binary_conversion.dm:23
    public override async DmTask populate_ports()
    {
        await AddComponent(typeof(datum_component_circuit_component_add_port),
            Dm.named("port_list", bit_array),
            Dm.named("add_action", "add"),
            Dm.named("remove_action", "remove"),
            Dm.named("is_output", TRUE),
            Dm.named("port_type", PORT_TYPE_BOOLEAN),
            Dm.named("prefix", "Bit"),
            Dm.named("minimum_amount", 1),
            Dm.named("maximum_amount", MAX_BITFIELD_SIZE));
        number = add_input_port("Number", PORT_TYPE_NUMBER, order: 1.1f) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/math/binary_conversion.dm:36
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!Dm.truthy(length(bit_array)))
            return null;

        float to_convert = Dm.num(number.value);
        bool is_negative = false;
        if (Dm.num(number.value) < 0)
        {
            is_negative = true;
            to_convert = -to_convert;
        }
        float len = length(bit_array);
        for (float iteration = 1; iteration <= len; iteration++)
        {
            datum_port_output bit = bit_array[iteration] as datum_port_output;
            if (iteration == 1 && is_negative)
            {
                bit.set_output(TRUE);
                continue;
            }
            bit.set_output(Dm.num(Dm.truthy(Dm.bitand(to_convert, Dm.shl(1, len - iteration)))));
        }
        return null;
    }
}
