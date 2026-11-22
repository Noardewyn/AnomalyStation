// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/foreach.dm
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
 * # For Each Component
 *
 * Sends a signal for each item in a list
 */
// tg: code/modules/wiremod/components/list/foreach.dm:6
public partial class obj_item_circuit_component_foreach : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/foreach.dm:33
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/list/foreach.dm:36
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            list_to_iterate.set_datatype(PORT_TYPE_LIST(new_datatype));
            element.set_datatype(new_datatype);
        }
    }

    // tg: code/modules/wiremod/components/list/foreach.dm:42
    public override async DmTask populate_ports()
    {
        list_to_iterate = add_input_port("List Input", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        next_index = add_input_port("Next Index", PORT_TYPE_SIGNAL, trigger: nameof(trigger_next_index)) as datum_port_input;
        reset_index = add_input_port("Reset And Trigger", PORT_TYPE_SIGNAL, trigger: nameof(restart)) as datum_port_input;

        element = add_output_port("Element", PORT_TYPE_ANY) as datum_port_output;
        current_index = add_output_port("Index", PORT_TYPE_NUMBER) as datum_port_output;
        on_next_index = add_output_port("Next Index", PORT_TYPE_SIGNAL) as datum_port_output;
        on_finished = add_output_port("On Finished", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/foreach.dm:52
    [ShouldNotSleep]
    public virtual void restart(datum_port_input port = null)
    {
        current_actual_index = 1;
        trigger_next_index(port);
    }

    // tg: code/modules/wiremod/components/list/foreach.dm:57
    [ShouldNotSleep]
    public virtual void trigger_next_index(datum_port_input port = null)
    {
        DmList to_check = list_to_iterate.value as DmList;
        if (!Dm.truthy(to_check))
            return;
        if (current_actual_index > length(to_check))
        {
            on_finished.set_output(COMPONENT_SIGNAL);
            return;
        }
        element.set_output(to_check[current_actual_index]);
        current_index.set_output(current_actual_index);
        on_next_index.set_output(COMPONENT_SIGNAL);
        current_actual_index += 1;
    }
}
