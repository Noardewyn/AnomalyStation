// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/abstract/variable.dm
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
 * # Variable Component
 *
 * Abstract component for handling variables
 */
// tg: code/modules/wiremod/components/abstract/variable.dm:6
public partial class obj_item_circuit_component_variable : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/abstract/variable.dm:18
    public override void populate_options()
    {
        variable_name = add_option_port("Variable", null) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/abstract/variable.dm:21
    public override async DmTask<object> add_to(obj_item_integrated_circuit added_to = null)
    {
        object dot = await base.add_to(added_to);
        variable_name.possible_options = get_variable_list(added_to) as DmList;
        return dot;
    }

    // tg: code/modules/wiremod/components/abstract/variable.dm:25
    public virtual object get_variable_list(obj_item_integrated_circuit integrated_circuit = null)
    {
        return integrated_circuit.circuit_variables;
    }

    // tg: code/modules/wiremod/components/abstract/variable.dm:28
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        variable_name.possible_options = null;
        base.removed_from(removed_from);
    }

    // tg: code/modules/wiremod/components/abstract/variable.dm:32
    public override void pre_input_received(datum_port_input port = null)
    {
        if (parent == null)
            return;

        object variable_string = variable_name.value;
        if (!Dm.truthy(variable_string))
        {
            remove_current_variable();
            return;
        }

        datum_circuit_variable variable = parent.circuit_variables[variable_string] as datum_circuit_variable;
        if (variable == null)
        {
            remove_current_variable();
            return;
        }

        set_current_variable(variable);
    }

    // tg: code/modules/wiremod/components/abstract/variable.dm:48
    [SignalHandler]
    public virtual void remove_current_variable()
    {
        if (current_variable != null)
        {
            if (Dm.truthy(should_listen))
                current_variable.remove_listener(this);
            UnregisterSignal(current_variable, COMSIG_QDELETING);
            current_variable = null;
        }
    }

    // tg: code/modules/wiremod/components/abstract/variable.dm:56
    public virtual void set_current_variable(datum_circuit_variable variable = null)
    {
        if (variable == current_variable)
            return;

        remove_current_variable();
        current_variable = variable;
        if (Dm.truthy(should_listen))
            current_variable.add_listener(this);
        RegisterSignal(current_variable, COMSIG_QDELETING, nameof(remove_current_variable));
    }
}
