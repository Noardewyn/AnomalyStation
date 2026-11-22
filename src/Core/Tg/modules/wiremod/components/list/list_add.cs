// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/list_add.dm
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
 * # List Add Component
 *
 * Adds an element to a list.
 */
// tg: code/modules/wiremod/components/list/list_add.dm:6
public partial class obj_item_circuit_component_variable_list_listadd : obj_item_circuit_component_variable_list
{
    // tg: code/modules/wiremod/components/list/list_add.dm:22
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Max List Size: []", max_list_size), "orange", "sitemap"));
        return dot;
    }

    // tg: code/modules/wiremod/components/list/list_add.dm:26
    public override async DmTask populate_ports()
    {
        to_add = add_input_port("To Add", PORT_TYPE_ANY) as datum_port_input;
        allow_duplicate = add_input_port("Allow Duplicate", PORT_TYPE_NUMBER, @default: 0) as datum_port_input;
        failed = add_output_port("Failed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/list_add.dm:31
    public override void pre_input_received(datum_port_input port = null)
    {
        base.pre_input_received(port);
        if (current_variable != null)
            to_add.set_datatype(current_variable.datatype_handler.get_datatype(1));
    }

    // tg: code/modules/wiremod/components/list/list_add.dm:36
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (current_variable == null)
            return null;
        DmList info = current_variable.value as DmList;
        object data_to_add = to_add.value;

        if (length(info) >= max_list_size)
        {
            failed.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (isdatum(data_to_add))
            data_to_add = WEAKREF(data_to_add as datum);

        if (!Dm.truthy(allow_duplicate.value))
            Dm.combine(info, data_to_add);
        else
            Dm.append(info, data_to_add);
        return null;
    }
}
