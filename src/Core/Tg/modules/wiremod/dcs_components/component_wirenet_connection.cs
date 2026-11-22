// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/dcs_components/component_wirenet_connection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the circuit component's link to the powernet of a cable layer under its anchored shell
// (the wirenet components that use it are CI2's). tg's AddElement/RemoveElement of connect_loc on the component itself (not an
// atom) is kept as tg writes it. The var block is generated (D111).

// tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:1
public partial class datum_component_circuit_component_wirenet_connection : datum_component
{
    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:10  var/static/list/turf_connections (rules 10.6)
    private static ref DmList turf_connections => ref TgState.Current.datum_component_circuit_component_wirenet_connection__turf_connections;

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:30
    [DmParams("layer_1_action", "layer_2_action", "layer_3_action", "connection_callback", "disconnection_callback", "post_set_cable_layer_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object layer_1_action = Dm.arg(__extra, 0) ?? CABLE_LAYER_1_NAME;
        object layer_2_action = Dm.arg(__extra, 1) ?? CABLE_LAYER_2_NAME;
        object layer_3_action = Dm.arg(__extra, 2) ?? CABLE_LAYER_3_NAME;
        datum_callback connection_callback = Dm.arg<datum_callback>(__extra, 3);
        datum_callback disconnection_callback = Dm.arg<datum_callback>(__extra, 4);
        datum_callback post_set_cable_layer_callback = Dm.arg<datum_callback>(__extra, 5);
        await base.Initialize(__extra);
        if (!istype(parent, typeof(obj_item_circuit_component)))
            return COMPONENT_INCOMPATIBLE;
        this.layer_1_action = layer_1_action;
        this.layer_2_action = layer_2_action;
        this.layer_3_action = layer_3_action;
        this.connection_callback = connection_callback;
        this.disconnection_callback = disconnection_callback;
        this.post_set_cable_layer_callback = post_set_cable_layer_callback;
        return null;
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:41
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        connection_callback = null;
        disconnection_callback = null;
        post_set_cable_layer_callback = null;
        return dot;
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:47
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_CIRCUIT_COMPONENT_PERFORM_ACTION, nameof(on_action));
        RegisterSignal(parent, COMSIG_CIRCUIT_COMPONENT_ADDED, nameof(on_parent_added_to_circuit));
        RegisterSignal(parent, COMSIG_CIRCUIT_COMPONENT_REMOVED, nameof(on_parent_removed_from_circuit));
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        unset_shell();
        UnregisterSignal(parent, Dm.list(COMSIG_CIRCUIT_COMPONENT_PERFORM_ACTION, COMSIG_CIRCUIT_COMPONENT_ADDED, COMSIG_CIRCUIT_COMPONENT_REMOVED));
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:56
    [SignalHandler]
    public virtual void on_parent_added_to_circuit(object _source = null, obj_item_integrated_circuit circuit = null)
    {
        RegisterSignal(circuit, COMSIG_CIRCUIT_SET_SHELL, nameof(on_circuit_set_shell));
        RegisterSignal(circuit, COMSIG_CIRCUIT_SHELL_REMOVED, nameof(unset_shell));
        if (circuit.shell != null)
            set_shell(circuit.shell);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:63
    [SignalHandler]
    public virtual void on_parent_removed_from_circuit(object _source = null, obj_item_integrated_circuit circuit = null)
    {
        unset_shell();
        UnregisterSignal(circuit, Dm.list(COMSIG_CIRCUIT_SET_SHELL, COMSIG_CIRCUIT_SHELL_REMOVED));
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:68
    [SignalHandler]
    public virtual void on_circuit_set_shell(object _source = null, atom_movable shell = null)
    {
        set_shell(shell);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:72
    public virtual void set_shell(atom_movable new_shell = null)
    {
        tracked_shell = new_shell;
        if (isassembly(new_shell))
        {
            RegisterSignals(new_shell, Dm.list(COMSIG_ASSEMBLY_ATTACHED, COMSIG_ASSEMBLY_ADDED_TO_BUTTON), nameof(on_assembly_shell_attached));
            RegisterSignals(new_shell, Dm.list(COMSIG_ASSEMBLY_DETACHED, COMSIG_ASSEMBLY_REMOVED_FROM_BUTTON), nameof(on_assembly_shell_detached));
        }
        else
            set_tracked_movable(new_shell);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:80
    [SignalHandler]
    public virtual void unset_shell()
    {
        unset_tracked_movable();
        if (tracked_shell == null)
            return;
        if (isassembly(tracked_shell))
            UnregisterSignal(tracked_shell, Dm.list(COMSIG_ASSEMBLY_ATTACHED, COMSIG_ASSEMBLY_DETACHED, COMSIG_ASSEMBLY_ADDED_TO_BUTTON, COMSIG_ASSEMBLY_REMOVED_FROM_BUTTON));
        tracked_shell = null;
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:89
    [SignalHandler]
    public virtual void on_assembly_shell_attached(object _source = null, atom holder = null)
    {
        if (ismovable(holder))
            set_tracked_movable(holder as atom_movable);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:94
    [SignalHandler]
    public virtual void on_assembly_shell_detached()
    {
        unset_tracked_movable();
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:98
    public virtual void set_tracked_movable(atom_movable new_tracked_movable = null)
    {
        if (tracked_movable == new_tracked_movable) //Should only happen when an assembly holder the assembly was attached to calls on_attach when it itself is attached to something
            return;
        tracked_movable = new_tracked_movable;
        RegisterSignal(new_tracked_movable, COMSIG_MOVABLE_SET_ANCHORED, nameof(on_tracked_movable_set_anchored));
        RegisterSignal(new_tracked_movable, COMSIG_QDELETING, nameof(unset_tracked_movable));
        if (Dm.truthy(tracked_movable.anchored))
            try_set_tracked_node();
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:107
    [SignalHandler]
    public virtual void unset_tracked_movable()
    {
        unset_tracked_node();
        if (tracked_movable == null)
            return;
        UnregisterSignal(tracked_movable, Dm.list(COMSIG_MOVABLE_SET_ANCHORED, COMSIG_QDELETING));
        tracked_movable = null;
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:115
    [SignalHandler]
    public virtual void on_tracked_movable_set_anchored(atom_movable source = null, object now_anchored = null)
    {
        if (Dm.truthy(now_anchored))
            try_set_tracked_node();
        else
            unset_tracked_node();
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:122
    [SignalHandler]
    public virtual void try_set_tracked_node()
    {
        if (tracked_node != null)
            unset_tracked_node();
        turf our_turf = get_turf(tracked_movable);
        obj_structure_cable node = our_turf.get_cable_node(cable_layer) as obj_structure_cable;
        if (node == null)
        {
            Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), turf_connections));
            return;
        }
        set_tracked_node(node);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:133
    [SignalHandler]
    public virtual void on_atom_initialized_on_turf(object _source = null, obj_structure_cable initialized = null)
    {
        if (!istype(initialized, typeof(obj_structure_cable)))
            return;
        if (!Dm.truthy(Dm.bitand(initialized.cable_layer, cable_layer)))
            return;
        set_tracked_node(initialized);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:141
    public virtual void set_tracked_node(obj_structure_cable node = null)
    {
        tracked_node = node;
        RemoveElement(typeof(datum_element_connect_loc), turf_connections);
        RegisterSignal(tracked_movable, COMSIG_MOVABLE_MOVED, nameof(unset_tracked_node)); //Because of wack cases of something pushing an anchored object
        RegisterSignal(node, COMSIG_CABLE_ADDED_TO_POWERNET, nameof(set_tracked_powernet));
        RegisterSignal(node, COMSIG_CABLE_REMOVED_FROM_POWERNET, nameof(unset_tracked_powernet));
        RegisterSignal(node, COMSIG_QDELETING, nameof(unset_tracked_node));
        if (node.powernet != null)
            set_tracked_powernet(node.powernet);
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:151
    [SignalHandler]
    public virtual void unset_tracked_node()
    {
        unset_tracked_powernet();
        if (tracked_node == null)
            return;
        UnregisterSignal(tracked_movable, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(tracked_node, Dm.list(COMSIG_CABLE_ADDED_TO_POWERNET, COMSIG_CABLE_REMOVED_FROM_POWERNET, COMSIG_QDELETING));
        tracked_node = null;
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:160
    [SignalHandler]
    public virtual void set_tracked_powernet(datum_powernet source = null)
    {
        tracked_powernet = source;
        if (connection_callback != null)
            Waiting.NoSleep(connection_callback.Invoke(source));
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:165
    [SignalHandler]
    public virtual void unset_tracked_powernet()
    {
        if (tracked_powernet == null)
            return;
        if (disconnection_callback != null)
            Waiting.NoSleep(disconnection_callback.Invoke(tracked_powernet));
        tracked_powernet = null;
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:172
    [SignalHandler]
    public virtual void on_action(obj_item_circuit_component component = null, mob user = null, object action = null)
    {
        switch (action)
        {
            case CABLE_LAYER_1_NAME:
                Waiting.NoSleep(set_cable_layer(CABLE_LAYER_1));
                break;
            case CABLE_LAYER_2_NAME:
                Waiting.NoSleep(set_cable_layer(CABLE_LAYER_2));
                break;
            case CABLE_LAYER_3_NAME:
                Waiting.NoSleep(set_cable_layer(CABLE_LAYER_3));
                break;
        }
    }

    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:182
    public virtual async DmTask set_cable_layer(object new_layer = null)
    {
        if (Dm.eq(cable_layer, new_layer))
            return;
        cable_layer = Dm.num(new_layer);
        if (post_set_cable_layer_callback != null)
            await post_set_cable_layer_callback.Invoke(new_layer);
        if (Dm.truthy(tracked_movable?.anchored))
            try_set_tracked_node();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/dcs_components/component_wirenet_connection.dm:10  var/static/list/turf_connections
    internal DmList datum_component_circuit_component_wirenet_connection__turf_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(datum_component_circuit_component_wirenet_connection.on_atom_initialized_on_turf)));
}
