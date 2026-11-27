// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/to_type.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # To Type Component
 *
 * Converts a string into a typepath. Useful for adding components.
 */
// tg: code/modules/wiremod/components/admin/to_type.dm:6
public partial class obj_item_circuit_component_to_type : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/to_type.dm:18
    public override async DmTask populate_ports()
    {
        input_path = add_input_port("Type", PORT_TYPE_STRING) as datum_port_input;
        type_output = add_output_port("Typepath", PORT_TYPE_ANY) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/to_type.dm:22
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        type_output.set_output(text2path(input_path.value as string));
        return null;
    }
}
