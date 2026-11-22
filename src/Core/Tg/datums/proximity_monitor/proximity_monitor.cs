// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/proximity_monitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 8: the gravity generator's graviton field, gravitygenerator.dm:345). The var block is generated
// (D111); the type-level static loc_connections lives in TgState (rules 10.6).

// tg: code/datums/proximity_monitor/proximity_monitor.dm:1
public partial class datum_proximity_monitor : datum
{
    ///The signals of the connect range component, needed to monitor the turfs in range.
    // tg: code/datums/proximity_monitor/proximity_monitor.dm:11
    internal static ref DmList loc_connections => ref TgState.Current.datum_proximity_monitor__loc_connections;

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:17
    [DmParams("_host", "range", "_ignore_if_not_on_turf")]
    public override async DmTask New(params object[] __extra)
    {
        atom _host = Dm.arg<atom>(__extra, 0);
        object range = Dm.arg<object>(__extra, 1);
        object _ignore_if_not_on_turf = Dm.arg<object>(__extra, 2) ?? TRUE;
        ignore_if_not_on_turf = _ignore_if_not_on_turf;
        current_range = range;
        await set_host(_host);
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:22
    public virtual async DmTask set_host(atom new_host = null, atom new_receiver = null)
    {
        if (new_host == host)
            return;
        if (Dm.truthy(host)) //No need to delete the connect range and containers comps. They'll be updated with the new tracked host.
            UnregisterSignal(host, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
        if (Dm.truthy(hasprox_receiver))
            UnregisterSignal(hasprox_receiver, COMSIG_QDELETING);
        if (Dm.truthy(new_receiver))
        {
            hasprox_receiver = new_receiver;
            if (new_receiver != new_host)
                RegisterSignal(new_receiver, COMSIG_QDELETING, nameof(on_host_or_receiver_del));
        }
        else if (hasprox_receiver == host) //Default case
            hasprox_receiver = new_host;
        host = new_host;
        RegisterSignal(new_host, COMSIG_QDELETING, nameof(on_host_or_receiver_del));
        await AddComponent(typeof(datum_component_connect_containers), host, set_host__containers_connections);
        RegisterSignal(host, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(host, COMSIG_MOVABLE_Z_CHANGED, nameof(on_z_change));
        await set_range(current_range, TRUE);
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:37  var/static/list/containers_connections (a proc-level static, rules 10.6)
    private static ref DmList set_host__containers_connections => ref TgState.Current.datum_proximity_monitor__set_host__containers_connections;

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:43
    [SignalHandler]
    public virtual void on_host_or_receiver_del(datum source = null)
    {
        // SIGNAL_HANDLER
        qdel(this);
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:47
    public override object Destroy(float? force = null)
    {
        host = null;
        hasprox_receiver = null;
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:52
    public virtual async DmTask<object> set_range(object range = null, object force_rebuild = null)
    {
        force_rebuild ??= FALSE;
        object dot = null;
        if (!Dm.truthy(force_rebuild) && Dm.eq(range, current_range))
            return FALSE;
        dot = TRUE;
        current_range = range;

        //If the connect_range component exists already, this will just update its range. No errors or duplicates.
        await AddComponent(typeof(datum_component_connect_range), host, loc_connections, range, Dm.num(!Dm.truthy(ignore_if_not_on_turf)));
        return dot;
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:61
    [SignalHandler]
    public virtual void on_moved(atom_movable source = null, atom old_loc = null)
    {
        // SIGNAL_HANDLER
        if (source == host)
            { if (hasprox_receiver != null) Waiting.NoSleep(hasprox_receiver.HasProximity(host as atom_movable)); }
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:66
    [SignalHandler]
    public virtual void on_z_change()
    {
        // SIGNAL_HANDLER
        return;
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:70
    public virtual async DmTask set_ignore_if_not_on_turf(object does_ignore = null)
    {
        does_ignore ??= TRUE;
        if (Dm.eq(ignore_if_not_on_turf, does_ignore))
            return;
        ignore_if_not_on_turf = does_ignore;
        //Update the ignore_if_not_on_turf
        await AddComponent(typeof(datum_component_connect_range), host, loc_connections, current_range, ignore_if_not_on_turf);
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:77
    [SignalHandler]
    public virtual void on_uncrossed(atom source = null, atom_movable gone = null, object direction = null) //Used by the advanced subtype for effect fields.
    {
        // SIGNAL_HANDLER
        if (source != host)
            { if (hasprox_receiver != null) Waiting.NoSleep(hasprox_receiver.OnProximityExit(gone)); }
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:82
    [SignalHandler]
    public virtual void on_entered(atom source = null, atom_movable arrived = null, turf old_loc = null)
    {
        // SIGNAL_HANDLER
        if (source != host)
            { if (hasprox_receiver != null) Waiting.NoSleep(hasprox_receiver.HasProximity(arrived)); }
    }

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:87
    [SignalHandler]
    public virtual void on_initialized(turf location = null, atom created = null, object init_flags = null)
    {
        // SIGNAL_HANDLER
        if (location != host)
            { if (hasprox_receiver != null) Waiting.NoSleep(hasprox_receiver.HasProximity(created as atom_movable)); }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/proximity_monitor/proximity_monitor.dm:11  var/static/list/loc_connections
    internal DmList datum_proximity_monitor__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_proximity_monitor.on_entered)),
        Dm.kv(COMSIG_ATOM_EXITED, nameof(datum_proximity_monitor.on_uncrossed)),
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(datum_proximity_monitor.on_initialized))
    );

    // tg: code/datums/proximity_monitor/proximity_monitor.dm:37  var/static/list/containers_connections
    internal DmList datum_proximity_monitor__set_host__containers_connections = Dm.list(
        Dm.kv(COMSIG_MOVABLE_MOVED, nameof(datum_proximity_monitor.on_moved)),
        Dm.kv(COMSIG_MOVABLE_Z_CHANGED, nameof(datum_proximity_monitor.on_z_change))
    );
}
