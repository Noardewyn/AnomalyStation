// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/getvar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. tg's object.vars is the port's var access by name (Dm.has_var, Dm.get_var). The var block is generated.

/**
 * # Get Variable Component
 *
 * A component that gets a variable on an object
 */
// tg: code/modules/wiremod/components/admin/getvar.dm:6
public partial class obj_item_circuit_component_get_variable : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/getvar.dm:28
    public override void populate_options()
    {
        getvar_options = add_option_port("Variable Options", Dm.list("Object", "Global")) as datum_port_input_option;
        expected_output_type = add_option_port("Expected Output Type", GLOB.wiremod_fundamental_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/admin/getvar.dm:32
    public override async DmTask populate_ports()
    {
        entity = add_input_port("Target", PORT_TYPE_DATUM) as datum_port_input;
        variable_name = add_input_port("Variable Name", PORT_TYPE_STRING, order: 2) as datum_port_input;
        output_value = add_output_port("Output Value", PORT_TYPE_ANY, order: 2) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/admin/getvar.dm:37
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == getvar_options)
        {
            remove_input_port(entity);
            entity = null;
            if (Dm.eq(getvar_options.value, "Object"))
                entity = add_input_port("Target", PORT_TYPE_DATUM) as datum_port_input;
        }

        if (port == expected_output_type)
        {
            if (!Dm.eq(output_value.datatype, expected_output_type.value))
                output_value.set_datatype(expected_output_type.value);
        }
    }

    // tg: code/modules/wiremod/components/admin/getvar.dm:48
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum @object = entity?.value as datum;
        if (Dm.eq(getvar_options.value, "Global"))
            @object = GLOB;

        object var_name = variable_name.value;
        if (!Dm.truthy(var_name) || @object == null)
        {
            output_value.set_output(null);
            return null;
        }

        if (!Dm.truthy(@object.can_vv_get(var_name)) || !Dm.has_var(@object, Dm.str(var_name)))
        {
            output_value.set_output(null);
            return null;
        }

        log_admin_circuit(Dm.text("[] requested the variable '[]' on [].", parent.get_creator(), var_name, @object));
        output_value.set_output(Dm.get_var(@object, Dm.str(var_name)));
        return null;
    }
}
