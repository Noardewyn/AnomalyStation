// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/speech.dm
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
 * # Speech Component
 *
 * Sends a message. Requires a shell.
 */
// tg: code/modules/wiremod/components/action/speech.dm:6
public partial class obj_item_circuit_component_speech : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/speech.dm:20
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Speech Cooldown: []", DisplayTimeText(speech_cooldown)), "orange", "stopwatch"));
        return dot;
    }

    // tg: code/modules/wiremod/components/action/speech.dm:24
    public override async DmTask populate_ports()
    {
        message = add_input_port("Message", PORT_TYPE_STRING, trigger: null) as datum_port_input;
        quietmode = add_input_port("Quiet Mode", PORT_TYPE_BOOLEAN, @default: FALSE) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/action/speech.dm:28
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (parent.shell == null)
            return null;

        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(parent.shell, COOLDOWN_CIRCUIT_SPEECH)))
            return null;

        if (Dm.truthy(message.value))
        {
            atom_movable shell = parent.shell;
            Waiting.NoSleep(shell.say(message.value, forced: Dm.text("circuit speech | []", parent.get_creator()), message_range: Dm.gt(quietmode.value, 0) ? WHISPER_RANGE : MESSAGE_RANGE));
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            LAZYSET(ref shell.cooldowns, COOLDOWN_CIRCUIT_SPEECH, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), shell, COOLDOWN_CIRCUIT_SPEECH), speech_cooldown, file: "code/modules/wiremod/components/action/speech.dm", line: 38));
        }
        return null;
    }
}
