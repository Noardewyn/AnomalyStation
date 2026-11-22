// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/assoc_list_remove.dm
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
 * # Associative List Remove Component
 *
 * Removes an element from an assoc list.
 */
// tg: code/modules/wiremod/components/list/assoc_list_remove.dm:6
public partial class obj_item_circuit_component_variable_assoc_list_list_remove : obj_item_circuit_component_variable_assoc_list
{
    // tg: code/modules/wiremod/components/list/assoc_list_remove.dm:16
    public override async DmTask populate_ports()
    {
        to_remove = add_input_port("To Remove", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/assoc_list_remove.dm:19
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (current_variable == null)
            return null;
        DmList info = current_variable.value as DmList;
        object value_to_remove = to_remove.value;

        Dm.remove(info, value_to_remove);
        return null;
    }
}
