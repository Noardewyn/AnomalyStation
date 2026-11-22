// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/variables/setter.dm
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
 * # Setter Component
 *
 * Stores the current input when triggered into a variable.
 */
// tg: code/modules/wiremod/components/variables/setter.dm:6
public partial class obj_item_circuit_component_variable_setter : obj_item_circuit_component_variable
{
    // tg: code/modules/wiremod/components/variables/setter.dm:20
    public override object get_variable_list(obj_item_integrated_circuit integrated_circuit = null)
    {
        return integrated_circuit.modifiable_circuit_variables;
    }

    // tg: code/modules/wiremod/components/variables/setter.dm:23
    public override async DmTask populate_ports()
    {
        input_port = add_input_port("Input", PORT_TYPE_ANY) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/variables/setter.dm:26
    public override void pre_input_received(datum_port_input port = null)
    {
        base.pre_input_received(port);
        if (port == variable_name)
            input_port.set_datatype(current_variable.datatype);
    }

    // tg: code/modules/wiremod/components/variables/setter.dm:31
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (current_variable == null)
            return null;
        current_variable.set_value(input_port.value);
        return null;
    }
}

// The trigger setter (setter.dm:15) is a var block only (generated, D111).
