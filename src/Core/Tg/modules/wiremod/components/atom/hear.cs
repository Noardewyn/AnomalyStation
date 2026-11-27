// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/hear.dm
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
 * # Hear Component
 *
 * Listens for messages. Requires a shell.
 */
// tg: code/modules/wiremod/components/atom/hear.dm:6
public partial class obj_item_circuit_component_hear : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/hear.dm:24
    public override async DmTask populate_ports()
    {
        on = add_input_port("On", PORT_TYPE_BOOLEAN, @default: TRUE) as datum_port_input;
        message_port = add_output_port("Message", PORT_TYPE_STRING) as datum_port_output;
        language_port = add_output_port("Language", PORT_TYPE_STRING) as datum_port_output;
        speaker_port = add_output_port("Speaker", PORT_TYPE_ATOM) as datum_port_output;
        speaker_name = add_output_port("Speaker Name", PORT_TYPE_STRING) as datum_port_output;
        trigger_port = add_output_port("Triggered", PORT_TYPE_SIGNAL) as datum_port_output;
        become_hearing_sensitive(ROUNDSTART_TRAIT);
    }

    // tg: code/modules/wiremod/components/atom/hear.dm:33
    public override async DmTask register_shell(atom_movable shell = null)
    {
        if (parent.loc != shell)
        {
            shell.become_hearing_sensitive(CIRCUIT_HEAR_TRAIT);
            RegisterSignal(shell, COMSIG_MOVABLE_HEAR, nameof(on_shell_hear));
        }
    }

    // tg: code/modules/wiremod/components/atom/hear.dm:38
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        REMOVE_TRAIT(shell, TRAIT_HEARING_SENSITIVE, CIRCUIT_HEAR_TRAIT);
    }

    // tg: code/modules/wiremod/components/atom/hear.dm:41
    [SignalHandler]
    public virtual object on_shell_hear(datum source = null, DmList arguments = null)
    {
        return Waiting.NoSleep(Dm.Call(this, nameof(Hear), Dm.arglist(arguments)));
    }

    // tg: code/modules/wiremod/components/atom/hear.dm:45
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        if (!Dm.truthy(on.value))
            return FALSE;
        if (speaker == parent?.shell)
            return FALSE;

        message_port.set_output(raw_message);
        if (Dm.truthy(message_language))
            language_port.set_output(Dm.initial(message_language, "name"));
        speaker_port.set_output(speaker);
        speaker_name.set_output(speaker.get_voice());
        trigger_port.set_output(COMPONENT_SIGNAL);
        return TRUE;
    }
}
