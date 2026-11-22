// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/variables/getter.dm
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
 * # Getter Component
 *
 * Gets the current value from a variable.
 */
// tg: code/modules/wiremod/components/variables/getter.dm:6
public partial class obj_item_circuit_component_variable_getter : obj_item_circuit_component_variable
{
    // tg: code/modules/wiremod/components/variables/getter.dm:16
    public override async DmTask populate_ports()
    {
        value = add_output_port("Value", PORT_TYPE_ANY) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/variables/getter.dm:19
    public override void pre_input_received(datum_port_input port = null)
    {
        base.pre_input_received(port);
        if (current_variable != null)
        {
            value.set_datatype(current_variable.datatype);
            value.set_value(current_variable.value);
        }
    }
}
