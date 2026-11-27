// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/keyboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/shell/keyboard.dm:1
public partial class obj_item_keyboard_shell : obj_item
{
    // tg: code/modules/wiremod/shell/keyboard.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_keyboard_shell>()
        ), SHELL_CAPACITY_SMALL));
        return dot;
    }
}

// tg: code/modules/wiremod/shell/keyboard.dm:20
public partial class obj_item_circuit_component_keyboard_shell : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/keyboard.dm:31
    public override async DmTask populate_ports()
    {
        entity = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
        output = add_output_port("Message", PORT_TYPE_STRING) as datum_port_output;
        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/keyboard.dm:36
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        RegisterSignal(shell, COMSIG_ITEM_ATTACK_SELF, nameof(send_trigger));
    }

    // tg: code/modules/wiremod/shell/keyboard.dm:40
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, COMSIG_ITEM_ATTACK_SELF);
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/keyboard.dm:44
    [SignalHandler]
    public virtual void send_trigger(atom source = null, mob user = null)
    {
        Waiting.Detach(use_keyboard(user));
    }

    // tg: code/modules/wiremod/shell/keyboard.dm:48
    public virtual async DmTask use_keyboard(mob user = null)
    {
        if (HAS_TRAIT(user, TRAIT_ILLITERATE))
        {
            to_chat(user, span_warning(I18n.T("You start mashing keys at random!")));
            return;
        }

        object message = await tgui_input_text(user, "Input your text", "Keyboard", max_length: MAX_MESSAGE_LEN);
        entity.set_output(user);
        output.set_output(message);
        signal.set_output(COMPONENT_SIGNAL);
    }
}
