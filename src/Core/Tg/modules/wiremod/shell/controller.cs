// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/controller.dm
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
 * # Compact Remote
 *
 * A handheld device with several buttons.
 * In game, this translates to having different signals for normal usage, alt-clicking, and ctrl-clicking when in your hand.
 */
// tg: code/modules/wiremod/shell/controller.dm:7
public partial class obj_item_controller : obj_item
{
    // tg: code/modules/wiremod/shell/controller.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_controller>()
        ), SHELL_CAPACITY_MEDIUM));
        return dot;
    }
}

// tg: code/modules/wiremod/shell/controller.dm:26
public partial class obj_item_circuit_component_controller : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/controller.dm:38
    public override async DmTask populate_ports()
    {
        entity = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
        alt = add_output_port("Alternate Signal", PORT_TYPE_SIGNAL) as datum_port_output;
        right = add_output_port("Extra Signal", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/controller.dm:44
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignal(shell, COMSIG_ITEM_ATTACK_SELF, nameof(send_trigger));
        RegisterSignal(shell, COMSIG_CLICK_ALT, nameof(send_alternate_signal));
        RegisterSignal(shell, COMSIG_ITEM_ATTACK_SELF_SECONDARY, nameof(send_right_signal));
    }

    // tg: code/modules/wiremod/shell/controller.dm:49
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(
            COMSIG_ITEM_ATTACK_SELF,
            COMSIG_ITEM_ATTACK_SELF_SECONDARY,
            COMSIG_CLICK_ALT));
    }

    // tg: code/modules/wiremod/shell/controller.dm:56
    public virtual void handle_trigger(atom source = null, object user = null, object port_name = null, datum_port_output port_signal = null)
    {
        source.balloon_alert(user as mob, I18n.T("clicked [] button", port_name));
        playsound(source, SFX_KEYBOARD_CLICKS, 25, FALSE);
        entity.set_output(user);
        port_signal.set_output(COMPONENT_SIGNAL);
    }

    /**
     * Called when the shell item is used in hand
     */
    // tg: code/modules/wiremod/shell/controller.dm:65
    [SignalHandler]
    public virtual void send_trigger(atom source = null, mob user = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.Adjacent(source))))
            return;
        handle_trigger(source, user, "primary", signal);
    }

    /**
     * Called when the shell item is alt-clicked
     */
    // tg: code/modules/wiremod/shell/controller.dm:74
    [SignalHandler]
    public virtual object send_alternate_signal(atom source = null, mob user = null)
    {
        handle_trigger(source, user, "alternate", alt);
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Called when the shell item is right-clicked in active hand
     */
    // tg: code/modules/wiremod/shell/controller.dm:84
    [SignalHandler]
    public virtual object send_right_signal(atom source = null, mob user = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(source))))
            return null;

        handle_trigger(source, user, "extra", right);
        return CLICK_ACTION_SUCCESS;
    }
}
