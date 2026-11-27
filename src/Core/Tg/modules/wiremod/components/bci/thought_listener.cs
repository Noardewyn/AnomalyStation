// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/thought_listener.dm
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
 * # Thought Listener Component
 *
 * Allows user to input a string.
 * Requires a BCI shell.
 */
// tg: code/modules/wiremod/components/bci/thought_listener.dm:8
public partial class obj_item_circuit_component_thought_listener : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/thought_listener.dm:26
    public override async DmTask populate_ports()
    {
        input_name = add_input_port("Input Name", PORT_TYPE_STRING) as datum_port_input;
        input_desc = add_input_port("Input Description", PORT_TYPE_STRING) as datum_port_input;
        output = add_output_port("Received Thought", PORT_TYPE_STRING) as datum_port_output;
        trigger_output = add_output_port("Triggered", PORT_TYPE_SIGNAL) as datum_port_output;
        failure = add_output_port("On Failure", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/bci/thought_listener.dm:33
    public override async DmTask register_shell(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
            bci = shell as obj_item_organ_cyberimp_bci;
    }

    // tg: code/modules/wiremod/components/bci/thought_listener.dm:37
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        bci = null;
    }

    // tg: code/modules/wiremod/components/bci/thought_listener.dm:40
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!Dm.truthy(ready))
        {
            failure.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (bci == null)
        {
            failure.set_output(COMPONENT_SIGNAL);
            return null;
        }

        mob_living owner = bci.owner;

        if (owner == null || !(owner is mob_living) || owner.client == null || IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            failure.set_output(COMPONENT_SIGNAL);
            return null;
        }

        Waiting.Detach(thought_listen(owner));
        ready = FALSE;
        return null;
    }

    // tg: code/modules/wiremod/components/bci/thought_listener.dm:58
    public virtual async DmTask thought_listen(mob_living owner = null)
    {
        object message = await tgui_input_text(owner, Dm.truthy(input_desc.value) ? input_desc.value : "", Dm.truthy(input_name.value) ? input_name.value : "Thought Listener", "", max_length: MAX_MESSAGE_LEN);
        if (QDELETED(owner) || IS_UNCONSCIOUS_OR_CRIT(owner))
            return;
        output.set_output(message);
        trigger_output.set_output(COMPONENT_SIGNAL);
        ready = TRUE;
    }
}
