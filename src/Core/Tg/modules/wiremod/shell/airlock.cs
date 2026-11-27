// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/airlock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/shell/airlock.dm:1
public partial class datum_wires_airlock_shell : datum_wires_airlock
{
    // tg: code/modules/wiremod/shell/airlock.dm:5
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        // Don't allow them to re-enable autoclose.
        if (Dm.eq(wire, WIRE_TIMING))
            return;
        await base.on_cut(wire, mend, source);
    }
}

// tg: code/modules/wiremod/shell/airlock.dm:11
public partial class obj_machinery_door_airlock_shell : obj_machinery_door_airlock
{
    // tg: code/modules/wiremod/shell/airlock.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_shell),
            Dm.named("unremovable_circuit_components", Dm.list(Dm.@new<obj_item_circuit_component_airlock>(), Dm.@new<obj_item_circuit_component_airlock_access_event>(), Dm.@new<obj_item_circuit_component_remotecam_airlock>())),
            Dm.named("capacity", SHELL_CAPACITY_LARGE),
            Dm.named("shell_flags", SHELL_FLAG_ALLOW_FAILURE_ACTION | SHELL_FLAG_REQUIRE_ANCHOR)
        ));
        return dot;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:24
    public override object check_access(obj_item I = null)
    {
        return FALSE;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:27
    public override object canAIControl(mob user = null)
    {
        return FALSE;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:30
    public override object canAIHack(params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:33
    public override async DmTask<object> allowed__proc(mob accessor = null)
    {
        mob user = accessor;
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_AIRLOCK_SHELL_ALLOWED, Dm.list(this, user)), ACCESS_ALLOWED)))
            return TRUE;
        return Dm.num(isAdminGhostAI__gap(user));
    }

    // GAP(lead): /proc/isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared (an admin observer with AI interact, the
    // admin tools): no mob is one yet.
    private static bool isAdminGhostAI__gap(mob user) => false;

    // tg: code/modules/wiremod/shell/airlock.dm:38
    public override async DmTask<object> get_wires()
    {
        return Dm.@new<datum_wires_airlock_shell>(this);
    }
}

// tg: code/modules/wiremod/shell/airlock.dm:41
public partial class obj_item_circuit_component_airlock : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/airlock.dm:73
    public override async DmTask populate_ports()
    {
        // Input Signals
        bolt = add_input_port("Bolt", PORT_TYPE_SIGNAL) as datum_port_input;
        unbolt = add_input_port("Unbolt", PORT_TYPE_SIGNAL) as datum_port_input;
        open = add_input_port("Open", PORT_TYPE_SIGNAL) as datum_port_input;
        close = add_input_port("Close", PORT_TYPE_SIGNAL) as datum_port_input;
        // States
        is_open = add_output_port("Is Open", PORT_TYPE_BOOLEAN) as datum_port_output;
        is_bolted = add_output_port("Is Bolted", PORT_TYPE_BOOLEAN) as datum_port_output;
        // Output Signals
        opened = add_output_port("Opened", PORT_TYPE_SIGNAL) as datum_port_output;
        closed = add_output_port("Closed", PORT_TYPE_SIGNAL) as datum_port_output;
        bolted = add_output_port("Bolted", PORT_TYPE_SIGNAL) as datum_port_output;
        unbolted = add_output_port("Unbolted", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:89
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_machinery_door_airlock)))
        {
            attached_airlock = shell as obj_machinery_door_airlock;
            RegisterSignal(shell, COMSIG_AIRLOCK_SET_BOLT, nameof(on_airlock_set_bolted));
            RegisterSignal(shell, COMSIG_AIRLOCK_OPEN, nameof(on_airlock_open));
            RegisterSignal(shell, COMSIG_AIRLOCK_CLOSE, nameof(on_airlock_closed));
        }
    }

    // tg: code/modules/wiremod/shell/airlock.dm:97
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        attached_airlock = null;
        UnregisterSignal(shell, Dm.list(
            COMSIG_AIRLOCK_SET_BOLT,
            COMSIG_AIRLOCK_OPEN,
            COMSIG_AIRLOCK_CLOSE
        ));
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/airlock.dm:106
    [SignalHandler]
    public virtual void on_airlock_set_bolted(datum source = null, object should_bolt = null)
    {
        is_bolted.set_output(should_bolt);
        if (Dm.truthy(should_bolt))
            bolted.set_output(COMPONENT_SIGNAL);
        else
            unbolted.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/airlock.dm:114
    [SignalHandler]
    public virtual void on_airlock_open(datum source = null, object force = null)
    {
        is_open.set_output(TRUE);
        opened.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/airlock.dm:119
    [SignalHandler]
    public virtual void on_airlock_closed(datum source = null, object forced = null)
    {
        is_open.set_output(FALSE);
        closed.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/airlock.dm:124
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_airlock == null)
            return null;

        if (COMPONENT_TRIGGERED_BY(bolt, port))
            attached_airlock.bolt();
        if (COMPONENT_TRIGGERED_BY(unbolt, port))
            attached_airlock.unbolt();
        if (COMPONENT_TRIGGERED_BY(open, port) && Dm.truthy(attached_airlock.density))
            Waiting.Detach(attached_airlock.open());
        if (COMPONENT_TRIGGERED_BY(close, port) && !Dm.truthy(attached_airlock.density))
            Waiting.Detach(attached_airlock.close());
        return null;
    }
}

// tg: code/modules/wiremod/shell/airlock.dm:137
public partial class obj_item_circuit_component_airlock_access_event : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/airlock.dm:156
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_machinery_door_airlock)))
        {
            attached_airlock = shell as obj_machinery_door_airlock;
            RegisterSignals(shell, Dm.list(
                COMSIG_AIRLOCK_SHELL_ALLOWED
            ), nameof(handle_allowed));
        }
    }

    // tg: code/modules/wiremod/shell/airlock.dm:164
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        attached_airlock = null;
        UnregisterSignal(shell, Dm.list(
            COMSIG_AIRLOCK_SHELL_ALLOWED
        ));
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/airlock.dm:172
    public override async DmTask populate_ports()
    {
        open_airlock = add_input_port("Should Open Airlock", PORT_TYPE_RESPONSE_SIGNAL, trigger: nameof(should_open_airlock)) as datum_port_input;
        accessing_entity = add_output_port("Accessing Entity", PORT_TYPE_ATOM) as datum_port_output;
        event_triggered = add_output_port("Event Triggered", PORT_TYPE_INSTANT_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:178
    [ShouldNotSleep]
    public virtual void should_open_airlock(datum_port_input port = null, DmList return_values = null)
    {
        if (return_values == null)
            return;
        return_values["should_open"] = TRUE;
    }

    // tg: code/modules/wiremod/shell/airlock.dm:184
    [SignalHandler]
    public virtual object handle_allowed(datum source = null, mob accesser = null)
    {
        if (attached_airlock == null)
            return null;

        SScircuit_component.queue_instant_run();
        accessing_entity.set_output(accesser);
        event_triggered.set_output(COMPONENT_SIGNAL);
        DmList result = SScircuit_component.execute_instant_run() as DmList;

        if (result == null)
        {
            attached_airlock.visible_message(span_warning(I18n.T("[]'s circuitry overheats!", attached_airlock)));
            return null;
        }

        if (Dm.truthy(result["should_open"]))
            return ACCESS_ALLOWED;
        else
            return ACCESS_DISALLOWED;
    }
}
