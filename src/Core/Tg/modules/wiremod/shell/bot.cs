// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/bot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/**
 * # Bot
 *
 * Immobile (but not dense) shells that can interact with world.
 */
// tg: code/modules/wiremod/shell/bot.dm:6
public partial class obj_structure_bot : obj_structure
{
    // tg: code/modules/wiremod/shell/bot.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_shell),
            Dm.named("unremovable_circuit_components", Dm.list(Dm.@new<obj_item_circuit_component_bot>())),
            Dm.named("capacity", SHELL_CAPACITY_LARGE),
            Dm.named("shell_flags", SHELL_FLAG_USB_PORT)
        ));
        return dot;
    }
}

// tg: code/modules/wiremod/shell/bot.dm:24
public partial class obj_item_circuit_component_bot : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/bot.dm:33
    public override async DmTask populate_ports()
    {
        entity = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/bot.dm:37
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignal(shell, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
    }

    // tg: code/modules/wiremod/shell/bot.dm:40
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, COMSIG_ATOM_ATTACK_HAND);
    }

    // tg: code/modules/wiremod/shell/bot.dm:43
    [SignalHandler]
    public virtual void on_attack_hand(atom source = null, mob user = null)
    {
        source.balloon_alert(user, I18n.T("pushed button"));
        playsound(source, SFX_TERMINAL_TYPE, 25, FALSE);
        entity.set_output(user);
        signal.set_output(COMPONENT_SIGNAL);
    }
}
