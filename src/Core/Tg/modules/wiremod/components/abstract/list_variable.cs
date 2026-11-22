// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/abstract/list_variable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

// tg: code/modules/wiremod/components/abstract/list_variable.dm:1
public partial class obj_item_circuit_component_variable_list : obj_item_circuit_component_variable
{
    // tg: code/modules/wiremod/components/abstract/list_variable.dm:4
    public override object get_variable_list(obj_item_integrated_circuit integrated_circuit = null)
    {
        return integrated_circuit.list_variables;
    }
}
