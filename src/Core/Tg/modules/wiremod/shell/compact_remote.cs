// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/compact_remote.dm
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
 * A handheld device with one big button.
 */
// tg: code/modules/wiremod/shell/compact_remote.dm:6
public partial class obj_item_compact_remote : obj_item
{
    // tg: code/modules/wiremod/shell/compact_remote.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_compact_remote>()
        ), SHELL_CAPACITY_SMALL));
        return dot;
    }
}

// tg: code/modules/wiremod/shell/compact_remote.dm:25
public partial class obj_item_circuit_component_compact_remote : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/compact_remote.dm:34
    public override async DmTask populate_ports()
    {
        entity = add_output_port("User", PORT_TYPE_ATOM) as datum_port_output;
        signal = add_output_port("Signal", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/compact_remote.dm:38
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignal(shell, COMSIG_ITEM_ATTACK_SELF, nameof(send_trigger));
    }

    // tg: code/modules/wiremod/shell/compact_remote.dm:41
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, COMSIG_ITEM_ATTACK_SELF);
    }

    /**
     * Called when the shell item is used in hand.
     */
    // tg: code/modules/wiremod/shell/compact_remote.dm:47
    [SignalHandler]
    public virtual void send_trigger(atom source = null, mob user = null)
    {
        source.balloon_alert(user, I18n.T("clicked primary button"));
        playsound(source, SFX_TERMINAL_TYPE, 25, FALSE);
        entity.set_output(user);
        signal.set_output(COMPONENT_SIGNAL);
    }
}
