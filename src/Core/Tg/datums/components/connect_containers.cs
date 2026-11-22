// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/connect_containers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 8: a proximity monitor follows its host through the containers it is in). The var block is
// generated (D111).

/// This component behaves similar to connect_loc_behalf, but it's nested and hooks a signal onto all MOVABLES containing this atom.
// tg: code/datums/components/connect_containers.dm:2
public partial class datum_component_connect_containers : datum_component
{
    // tg: code/datums/components/connect_containers.dm:13
    [DmParams("tracked", "connections")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object tracked = Dm.arg(__extra, 0);
        DmList connections = Dm.arg<DmList>(__extra, 1);
        object dot = await base.Initialize(Dm.forward(__extra, tracked, connections));
        if (!ismovable(tracked))
            return COMPONENT_INCOMPATIBLE;

        this.connections = connections;
        set_tracked((atom_movable)tracked);
        return dot;
    }

    // tg: code/datums/components/connect_containers.dm:21
    public override object Destroy(float? force = null)
    {
        set_tracked(null);
        return base.Destroy(force);
    }

    // tg: code/datums/components/connect_containers.dm:25
    [DmParams("new_component", "tracked", "connections")]
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        atom_movable tracked = Dm.arg(__extra, 0) as atom_movable;
        DmList connections = Dm.arg<DmList>(__extra, 1);
        // Both the signal names and their handlers must match.
        if (!Dm.truthy(deep_compare_list(this.connections, connections)))
            return FALSE; // Different set of connections.
        if (this.tracked != tracked)
            set_tracked(tracked); // Different target for the same set of connections, track the new target.
        return TRUE; // No new component.
    }

    // tg: code/datums/components/connect_containers.dm:33
    public virtual void set_tracked(atom_movable new_tracked = null)
    {
        if (Dm.truthy(tracked))
        {
            UnregisterSignal(tracked, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
            unregister_signals(tracked);
        }
        tracked = new_tracked;
        if (!Dm.truthy(tracked))
            return;
        RegisterSignal(tracked, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(tracked, COMSIG_QDELETING, nameof(handle_tracked_qdel));
        update_signals(tracked);
    }

    // tg: code/datums/components/connect_containers.dm:44
    [SignalHandler]
    public virtual void handle_tracked_qdel()
    {
        // SIGNAL_HANDLER
        qdel(this);
    }

    // tg: code/datums/components/connect_containers.dm:48
    public virtual void update_signals(atom_movable listener = null)
    {
        if (!ismovable(listener.loc))
            return;

        foreach (atom_movable container in Dm.each_cast<atom_movable>(get_nested_locs(listener)))
        {
            RegisterSignal(container, COMSIG_MOVABLE_MOVED, nameof(on_moved));
            foreach (object signal in Dm.each(connections))
                parent.RegisterSignal(container, signal, connections[signal]);
        }
    }

    // tg: code/datums/components/connect_containers.dm:57
    public virtual void unregister_signals(atom location = null)
    {
        if (!ismovable(location))
            return;

        foreach (atom_movable target in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs((atom_movable)location), location)))
        {
            UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
            parent.UnregisterSignal(target, connections);
        }
    }

    // tg: code/datums/components/connect_containers.dm:65
    [SignalHandler]
    public virtual void on_moved(atom_movable listener = null, atom old_loc = null)
    {
        // SIGNAL_HANDLER
        unregister_signals(old_loc);
        update_signals(listener);
    }
}
