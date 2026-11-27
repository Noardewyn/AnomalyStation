// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/scanner.dm
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
 * # Scanner
 *
 * A handheld device that lets you flash it over people.
 */
// tg: code/modules/wiremod/shell/scanner.dm:6
public partial class obj_item_wiremod_scanner : obj_item
{
    // tg: code/modules/wiremod/shell/scanner.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_wiremod_scanner>()
        ), SHELL_CAPACITY_SMALL));
        return dot;
    }
}

// tg: code/modules/wiremod/shell/scanner.dm:25
public partial class obj_item_circuit_component_wiremod_scanner : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/scanner.dm:40
    public override async DmTask populate_ports()
    {
        attacker = add_output_port("Scanner", PORT_TYPE_ATOM) as datum_port_output;
        attacking = add_output_port("Scanned Entity", PORT_TYPE_ATOM) as datum_port_output;
        signal = add_output_port("Scanned", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/scanner.dm:45
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignal(shell, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(handle_interaction));
    }

    // tg: code/modules/wiremod/shell/scanner.dm:48
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, COMSIG_ITEM_INTERACTING_WITH_ATOM);
    }

    /**
     * Called when the shell item attacks something
     */
    // tg: code/modules/wiremod/shell/scanner.dm:54
    [SignalHandler]
    public virtual object handle_interaction(atom source = null, mob user = null, atom target = null, params object[] __extra)
    {
        source.balloon_alert(user, I18n.T("scanned object"));
        playsound(source, SFX_TERMINAL_TYPE, 25, FALSE);
        attacker.set_output(user);
        attacking.set_output(target);
        signal.set_output(COMPONENT_SIGNAL);
        return ITEM_INTERACT_SUCCESS;
    }
}
