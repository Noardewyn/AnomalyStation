// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/assoc_list_set.dm
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
 * # Assoc List Set Component
 *
 * Sets a string value on an assoc list.
 */
// tg: code/modules/wiremod/components/list/assoc_list_set.dm:6
public partial class obj_item_circuit_component_variable_assoc_list_list_set : obj_item_circuit_component_variable_assoc_list
{
    // tg: code/modules/wiremod/components/list/assoc_list_set.dm:22
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Max List Size: []", max_list_size), "orange", "sitemap"));
        return dot;
    }

    // tg: code/modules/wiremod/components/list/assoc_list_set.dm:26
    public override async DmTask populate_ports()
    {
        key = add_input_port("Key", PORT_TYPE_STRING) as datum_port_input;
        value = add_input_port("Value", PORT_TYPE_ANY) as datum_port_input;
        failed = add_output_port("Failed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/assoc_list_set.dm:31
    public override void pre_input_received(datum_port_input port = null)
    {
        base.pre_input_received(port);
        if (current_variable != null)
            value.set_datatype(current_variable.datatype_handler.get_datatype(2));
    }

    // tg: code/modules/wiremod/components/list/assoc_list_set.dm:36
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (current_variable == null)
            return null;
        DmList info = current_variable.value as DmList;
        object key_to_set = key.value;
        object value_to_set = value.value;

        if (!Dm.truthy(key_to_set))
        {
            failed.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (length(info) >= max_list_size)
        {
            failed.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (isdatum(value_to_set))
            value_to_set = WEAKREF(value_to_set as datum);

        info[key_to_set] = value_to_set;
        return null;
    }
}
