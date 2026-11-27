// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/scanner_gate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, with the gate's scanline overlays. The var blocks are generated.

// tg: code/modules/wiremod/shell/scanner_gate.dm:1
public partial class obj_structure_scanner_gate_shell : obj_structure
{
    // tg: code/modules/wiremod/shell/scanner_gate.dm:11  var/static/list/loc_connections
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_scanner_gate_shell__Initialize__loc_connections;

    // tg: code/modules/wiremod/shell/scanner_gate.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_scanline("passive");
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));

        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_scanner_gate>()
        ), SHELL_CAPACITY_LARGE, SHELL_FLAG_REQUIRE_ANCHOR));
        return dot;
    }

    // tg: code/modules/wiremod/shell/scanner_gate.dm:20
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(locked))
            return null;
        await set_anchored(Dm.not(anchored));
        tool.play_tool_sound(this);
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("secured") : I18n.T("unsecured"));
        return TRUE;
    }

    // tg: code/modules/wiremod/shell/scanner_gate.dm:28
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        set_scanline("scanning", 10);
        SEND_SIGNAL(COMSIG_SCANGATE_SHELL_PASS, Dm.list(this, AM));
    }

    // tg: code/modules/wiremod/shell/scanner_gate.dm:33
    public virtual void set_scanline(object type = null, object duration = null)
    {
        cut_overlays();
        add_overlay(type);
        if (Dm.truthy(duration))
            _addtimer(Dm.@new<datum_callback>(this, nameof(set_scanline), "passive"), Dm.num(duration), TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_STOPPABLE, file: "code/modules/wiremod/shell/scanner_gate.dm", line: 37);
    }
}

// tg: code/modules/wiremod/shell/scanner_gate.dm:39
public partial class obj_item_circuit_component_scanner_gate : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/scanner_gate.dm:47
    public override async DmTask populate_ports()
    {
        scanned = add_output_port("Scanned Object", PORT_TYPE_ATOM) as datum_port_output;
        trigger_output = add_output_port("Triggered", PORT_TYPE_SIGNAL, order: 2) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/scanner_gate.dm:51
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_structure_scanner_gate_shell)))
        {
            attached_gate = shell as obj_structure_scanner_gate_shell;
            RegisterSignal(attached_gate, COMSIG_SCANGATE_SHELL_PASS, nameof(on_trigger));
            RegisterSignal(parent, COMSIG_CIRCUIT_SET_LOCKED, nameof(on_set_locked));
            attached_gate.locked = parent.locked;
        }
    }

    // tg: code/modules/wiremod/shell/scanner_gate.dm:59
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(attached_gate, COMSIG_SCANGATE_SHELL_PASS);
        if (attached_gate != null)
        {
            attached_gate.locked = FALSE;
            UnregisterSignal(parent, COMSIG_CIRCUIT_SET_LOCKED);
        }
        attached_gate = null;
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/scanner_gate.dm:68
    [SignalHandler]
    public virtual void on_trigger(datum source = null, atom_movable passed = null)
    {
        scanned.set_output(passed);
        trigger_output.set_output(COMPONENT_SIGNAL);
    }

    /**
     * Locks the attached bot when the circuit is locked.
     *
     * Arguments:
     * * new_value - A boolean that determines if the circuit is locked or not.
     **/
    // tg: code/modules/wiremod/shell/scanner_gate.dm:79
    [SignalHandler]
    public virtual void on_set_locked(datum source = null, object new_value = null)
    {
        attached_gate.locked = Dm.num(new_value);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/shell/scanner_gate.dm:11
    internal DmList obj_structure_scanner_gate_shell__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_structure_scanner_gate_shell.on_entered)));
}
