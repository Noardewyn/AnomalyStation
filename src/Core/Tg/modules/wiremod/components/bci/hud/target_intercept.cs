// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/bci/hud/target_intercept.dm
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
 * # Target Intercept Component
 *
 * When activated intercepts next click and outputs clicked atom.
 * Requires a BCI shell.
 */
// tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:8
public partial class obj_item_circuit_component_target_intercept : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:20
    public override async DmTask populate_ports()
    {
        trigger_input = add_input_port("Activate", PORT_TYPE_SIGNAL) as datum_port_input;
        trigger_output = add_output_port("Triggered", PORT_TYPE_SIGNAL) as datum_port_output;
        clicked_atom = add_output_port("Targeted Object", PORT_TYPE_ATOM) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:25
    public override async DmTask register_shell(atom_movable shell = null)
    {
        if (istype(shell, typeof(obj_item_organ_cyberimp_bci)))
        {
            bci = shell as obj_item_organ_cyberimp_bci;
            RegisterSignal(shell, COMSIG_ORGAN_REMOVED, nameof(on_organ_removed));
        }
    }

    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:30
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        bci = null;
        UnregisterSignal(shell, COMSIG_ORGAN_REMOVED);
    }

    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:34
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (bci == null)
            return null;

        if (parent.shell == null)
            return null;

        mob_living owner = bci.owner;
        if (owner == null || !(owner is mob_living) || owner.client == null || IS_UNCONSCIOUS_OR_CRIT(owner))
            return null;

        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(parent.shell, COOLDOWN_CIRCUIT_TARGET_INTERCEPT)))
            return null;

        to_chat(owner, I18n.T("<B>Left-click to trigger target interceptor!</B>"));
        owner.client.click_intercept = this;
        return null;
    }

    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:51
    [SignalHandler]
    public virtual void on_organ_removed(datum source = null, mob_living_carbon owner = null)
    {
        if (owner.client != null && owner.client.click_intercept == this)
            owner.client.click_intercept = null;
    }

    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:57
    public virtual void InterceptClickOn(mob user = null, object @params = null, atom @object = null)
    {
        user.client.click_intercept = null;
        clicked_atom.set_output(@object);
        trigger_output.set_output(COMPONENT_SIGNAL);
        if (parent.shell != null)
        {
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            atom_movable cd_source = parent.shell;
            LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_TARGET_INTERCEPT, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_TARGET_INTERCEPT), intercept_cooldown, file: "code/modules/wiremod/components/bci/hud/target_intercept.dm", line: 62));
        }
    }

    // tg: code/modules/wiremod/components/bci/hud/target_intercept.dm:64
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        Dm.append(ref dot, create_ui_notice(Dm.text("Target Interception Cooldown: []", DisplayTimeText(intercept_cooldown)), "orange", "stopwatch"));
        return dot;
    }
}
