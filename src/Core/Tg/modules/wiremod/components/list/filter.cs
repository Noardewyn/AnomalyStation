// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/filter.dm
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
 * Filters
 */
// tg: code/modules/wiremod/components/list/filter.dm:6
public partial class obj_item_circuit_component_filter_list : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/filter.dm:40
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/list/filter.dm:43
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            list_to_filter.set_datatype(PORT_TYPE_LIST(new_datatype));
            finished_list.set_datatype(PORT_TYPE_LIST(new_datatype));
            element.set_datatype(new_datatype);
        }
    }

    // tg: code/modules/wiremod/components/list/filter.dm:50
    public override async DmTask populate_ports()
    {
        list_to_filter = add_input_port("List Input", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        accept_entry = add_input_port("Accept Entry", PORT_TYPE_SIGNAL, trigger: nameof(accept_entry_port)) as datum_port_input;

        element = add_output_port("Element", PORT_TYPE_ANY) as datum_port_output;
        current_index = add_output_port("Index", PORT_TYPE_NUMBER) as datum_port_output;
        on_next_index = add_output_port("Next Index", PORT_TYPE_SIGNAL) as datum_port_output;
        finished_list = add_output_port("Filtered List", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_output;
        on_finished = add_output_port("On Finished", PORT_TYPE_SIGNAL) as datum_port_output;
        on_failed = add_output_port("On Failed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/filter.dm:61
    [ShouldNotSleep]
    public virtual void accept_entry_port(datum_port_input port = null, DmList return_values = null)
    {
        if (Dm.truthy(return_values))
            return_values["accept_entry"] = TRUE;
    }

    // tg: code/modules/wiremod/components/list/filter.dm:66
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        float index = 1;
        DmList filtered_list = Dm.list();
        foreach (object element_in_list in Dm.each(list_to_filter.value))
        {
            if (Dm.gt(index, limit) && !Dm.truthy(parent.admin_only))
                break;
            SScircuit_component.queue_instant_run();
            element.set_output(element_in_list);
            current_index.set_output(index);
            on_next_index.set_output(COMPONENT_SIGNAL);
            index += 1;
            DmList result = SScircuit_component.execute_instant_run() as DmList;
            if (!Dm.truthy(result))
            {
                balloon_alert_to_viewers(I18n.T("[] starts to overheat!", this));
                on_failed.set_output(COMPONENT_SIGNAL);
                return null;
            }
            if (Dm.truthy(result["accept_entry"]))
                Dm.append(filtered_list, Dm.list(element_in_list));
        }
        finished_list.set_output(filtered_list);
        on_finished.set_output(COMPONENT_SIGNAL);
        return null;
    }
}
