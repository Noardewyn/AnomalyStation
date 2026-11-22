// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8x slice A: the whole file (the cryo cell's connection to the pipe network). The var block is generated.

///To be used when there is the need of an atmos connection without repathing everything (eg: cryo.dm)
// tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:2
public partial class datum_gas_machine_connector : datum
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:7
    [DmParams("location", "connecting_machine", "direction", "gas_volume")]
    public override async DmTask New(params object[] __extra)
    {
        object location = Dm.arg<object>(__extra, 0);
        obj_machinery connecting_machine = Dm.arg<object>(__extra, 1) as obj_machinery;
        object direction = Dm.arg<object>(__extra, 2) ?? SOUTH;
        object gas_volume = Dm.arg<object>(__extra, 3);
        connected_machine = connecting_machine;
        if (connected_machine == null)
        {
            qdel(this);
            return;
        }

        gas_connector = Dm.@new<obj_machinery_atmospherics_components_unary>(location);
        gas_connector.dir = connected_machine.dir;
        ((datum_gas_mixture)gas_connector.airs[1]).volume = Dm.num(gas_volume);

        SSair.start_processing_machine(connected_machine);
        register_with_machine();
        gas_connector.set_init_directions();
        gas_connector.atmos_init();
        SSair.add_to_rebuild_queue(gas_connector);
        RegisterSignal(gas_connector, COMSIG_QDELETING, nameof(connector_deleted));
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:25
    public override object Destroy(float? force = null)
    {
        connected_machine = null;
        QDEL_NULL(ref gas_connector);
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:30
    [SignalHandler]
    public virtual void connector_deleted()
    {
        gas_connector = null;
        if (!QDELETED(connected_machine))
            qdel(connected_machine);
    }

    /**
     * Register various signals that are required for the proper work of the connector
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:39
    public virtual void register_with_machine()
    {
        RegisterSignal(connected_machine, COMSIG_MOVABLE_PRE_MOVE, nameof(pre_move_connected_machine));
        RegisterSignal(connected_machine, COMSIG_MOVABLE_MOVED, nameof(moved_connected_machine));
        RegisterSignal(connected_machine, COMSIG_MACHINERY_DEFAULT_ROTATE_WRENCH, nameof(wrenched_connected_machine));
        RegisterSignal(connected_machine, COMSIG_OBJ_DECONSTRUCT, nameof(deconstruct_connected_machine));
        RegisterSignal(connected_machine, COMSIG_QDELETING, nameof(destroy_connected_machine));
    }

    /**
     * Unregister the signals previously registered
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:49
    public virtual void unregister_from_machine()
    {
        UnregisterSignal(connected_machine, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOVABLE_PRE_MOVE,
            COMSIG_MACHINERY_DEFAULT_ROTATE_WRENCH,
            COMSIG_OBJ_DECONSTRUCT,
            COMSIG_QDELETING
        ));
    }

    /**
     * Called when the machine has been moved, reconnect to the pipe network
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:61
    [SignalHandler]
    public virtual void moved_connected_machine(obj_machinery source = null, atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        if (Dm.truthy(forced)) // Called from parent doing abstract_move()
        {
            Waiting.NoSleep(gas_connector.abstract_move(get_turf(connected_machine)));
            return; // No side-effects means no disconnections
        }

        Waiting.NoSleep(gas_connector.forceMove(get_turf(connected_machine)));
        reconnect_connector();
    }

    /**
     * Called before the machine moves, disconnect from the pipe network
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:73
    [SignalHandler]
    public virtual void pre_move_connected_machine()
    {
        disconnect_connector();
    }

    /**
     * Called when the machine has been rotated, resets the connection to the pipe network with the new direction
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:80
    [SignalHandler]
    public virtual void wrenched_connected_machine()
    {
        disconnect_connector();
        reconnect_connector();
    }

    /**
     * Called when the machine has been deconstructed
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:88
    [SignalHandler]
    public virtual void deconstruct_connected_machine()
    {
        relocate_airs();
    }

    /**
     * Called when the machine has been destroyed
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:96
    [SignalHandler]
    public virtual void destroy_connected_machine()
    {
        disconnect_connector();
        SSair.stop_processing_machine(connected_machine);
        unregister_from_machine();
        qdel(this);
    }

    /**
     * Handles the disconnection from the pipe network
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:107
    public virtual void disconnect_connector()
    {
        obj_machinery_atmospherics node = gas_connector.nodes[1] as obj_machinery_atmospherics;
        if (node != null)
        {
            if (Dm.@in(gas_connector, node.nodes)) //Only if it's actually connected. On-pipe version would is one-sided.
                node.disconnect(gas_connector);
            gas_connector.nodes[1] = null;
        }
        if (Dm.truthy(gas_connector.parents[1]))
            gas_connector.nullify_pipenet(gas_connector.parents[1] as datum_pipeline);
    }

    /**
     * Handles the reconnection to the pipe network
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:119
    public virtual void reconnect_connector()
    {
        gas_connector.dir = connected_machine.dir;
        gas_connector.set_init_directions();
        obj_machinery_atmospherics node = gas_connector.nodes[1] as obj_machinery_atmospherics;
        gas_connector.atmos_init();
        node = gas_connector.nodes[1] as obj_machinery_atmospherics;
        if (node != null)
        {
            node.atmos_init();
            node.add_member(gas_connector);
            gas_connector.update_parents();
        }
        SSair.add_to_rebuild_queue(gas_connector);
    }

    /**
     * Handles air relocation to the pipe network/environment
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/machine_connector.dm:134
    public virtual void relocate_airs(mob user = null)
    {
        turf local_turf = get_turf(connected_machine);
        datum_gas_mixture inside_air = gas_connector.airs[1] as datum_gas_mixture;
        if (Dm.num(inside_air.total_moles()) > 0)
        {
            if (!Dm.truthy(gas_connector.nodes[1]))
            {
                Waiting.NoSleep(local_turf.assume_air(inside_air));
                return;
            }
            datum_gas_mixture parents_air = ((datum_pipeline)gas_connector.parents[1]).air;
            if (gas_connector.nodes[1] is obj_machinery_atmospherics_components_unary_portables_connector portable_devices_connector)
            {
                if (!Dm.truthy(portable_devices_connector.connected_device))
                {
                    Waiting.NoSleep(local_turf.assume_air(inside_air));
                    return;
                }
            }
            parents_air.merge(inside_air);
        }
    }
}
