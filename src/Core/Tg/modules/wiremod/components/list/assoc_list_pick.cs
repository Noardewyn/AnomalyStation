// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/assoc_list_pick.dm
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
 * # associative List pick component
 *
 * Allows user to select 1 entry from a list
 * For actuel code refer to code\modules\wiremod\components\list\list_pick.dm
 */
// tg: code/modules/wiremod/components/list/assoc_list_pick.dm:7
public partial class obj_item_circuit_component_list_pick_assoc : obj_item_circuit_component_list_pick
{
    // tg: code/modules/wiremod/components/list/assoc_list_pick.dm:12
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/list/assoc_list_pick.dm:15
    public override void make_list_port()
    {
        input_list = add_input_port("List", PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, PORT_TYPE_ANY)) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/assoc_list_pick.dm:18
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_type = list_options.value;
            input_list.set_datatype(PORT_TYPE_ASSOC_LIST(PORT_TYPE_STRING, new_type));
            output.set_datatype(new_type);
        }
    }

    // tg: code/modules/wiremod/components/list/assoc_list_pick.dm:24
    public override void choose_item(object choice = null, DmList choice_list = null)
    {
        if (Dm.truthy(choice_list[choice]))
        {
            output.set_output(choice_list[choice]);
            success.set_output(COMPONENT_SIGNAL);
        }
        else
            failure.set_output(COMPONENT_SIGNAL);
    }
}
