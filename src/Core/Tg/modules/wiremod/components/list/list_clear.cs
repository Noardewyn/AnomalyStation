// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/list_clear.dm
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
 * # List Clear Component
 *
 * Clears an element to a list.
 */
// tg: code/modules/wiremod/components/list/list_clear.dm:6
public partial class obj_item_circuit_component_variable_list_listclear : obj_item_circuit_component_variable_list
{
    // tg: code/modules/wiremod/components/list/list_clear.dm:13
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (current_variable == null)
            return null;
        current_variable.set_value(Dm.list());
        return null;
    }
}
