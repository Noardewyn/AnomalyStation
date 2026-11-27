// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/setvar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Set Variable Component
 *
 * A component that sets a variable on an object
 */
// tg: code/modules/wiremod/components/admin/setvar.dm:6
public partial class obj_item_circuit_component_set_variable : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/setvar.dm:26
    public override async DmTask populate_ports()
    {
        resolve_weakrefs = add_input_port("Resolve Weakrefs", PORT_TYPE_NUMBER, @default: TRUE) as datum_port_input;
        entity = add_input_port("Target", PORT_TYPE_DATUM) as datum_port_input;
        variable_name = add_input_port("Variable Name", PORT_TYPE_STRING) as datum_port_input;
        new_value = add_input_port("New Value", PORT_TYPE_ANY) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/admin/setvar.dm:32
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum @object = entity.value as datum;
        object var_name = variable_name.value;
        if (!Dm.truthy(var_name) || @object == null)
            return null;

        object resolved_new_value = new_value.value;
        if (islist(resolved_new_value) && Dm.truthy(resolve_weakrefs.value))
        {
            DmList to_resolve = resolved_new_value as DmList;
            resolved_new_value = recursive_list_resolve(to_resolve);
        }

        log_admin_circuit(Dm.text("[] set the variable '[]' on [] to [].", parent.get_creator(), var_name, @object, resolved_new_value));
        Waiting.NoSleep(@object.vv_edit_var(var_name, resolved_new_value));
        return null;
    }
}
