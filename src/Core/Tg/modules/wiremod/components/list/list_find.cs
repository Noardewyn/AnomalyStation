// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/list/list_find.dm
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
 * # List Find Component
 *
 * Finds an element in a list and returns the index.
 */
// tg: code/modules/wiremod/components/list/list_find.dm:6
public partial class obj_item_circuit_component_listin : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/list/list_find.dm:30
    public override void populate_options()
    {
        list_type = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/list/list_find.dm:33
    public override async DmTask populate_ports()
    {
        list_to_check = add_input_port("List", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        to_check = add_input_port("To Check", PORT_TYPE_ANY) as datum_port_input;

        found = add_output_port("Succeeded", PORT_TYPE_SIGNAL) as datum_port_output;
        not_found = add_output_port("Failed", PORT_TYPE_SIGNAL) as datum_port_output;
        result = add_output_port("Result", PORT_TYPE_NUMBER) as datum_port_output;
        index = add_output_port("Index", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/list/list_find.dm:42
    public override void pre_input_received(datum_port_input port = null)
    {
        base.pre_input_received(port);
        list_to_check.set_datatype(PORT_TYPE_LIST(list_type.value));
        to_check.set_datatype(list_type.value);
    }

    // tg: code/modules/wiremod/components/list/list_find.dm:47
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList info = list_to_check.value as DmList;
        if (!Dm.truthy(info))
            return null;
        object data_to_check = to_check.value;

        if (isdatum(data_to_check))
            data_to_check = WEAKREF(data_to_check as datum);

        float actual_result = info.Find(data_to_check);
        index.set_output(actual_result);
        if (actual_result != 0)
        {
            result.set_output(TRUE);
            found.set_output(COMPONENT_SIGNAL);
        }
        else
        {
            result.set_output(FALSE);
            not_found.set_output(COMPONENT_SIGNAL);
        }
        return null;
    }
}
