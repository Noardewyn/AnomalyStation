// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/connect_range.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 8: the proximity monitors hook the turfs in their range through it). The var block is generated
// (D111).

/**
 * This component behaves similar to connect_loc_behalf but for all turfs in range, hooking into a signal on each of them.
 * Just like connect_loc_behalf, It can react to that signal on behalf of a separate listener.
 * Good for components, though it carries some overhead. Can't be an element as that may lead to bugs.
 */
// tg: code/datums/components/connect_range.dm:6
public partial class datum_component_connect_range : datum_component
{
    // tg: code/datums/components/connect_range.dm:24
    [DmParams("tracked", "connections", "range", "works_in_containers")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom tracked = Dm.arg(__extra, 0) as atom;
        DmList connections = Dm.arg<DmList>(__extra, 1);
        object range = Dm.arg(__extra, 2);
        object works_in_containers = Dm.arg(__extra, 3) ?? TRUE;
        if (!isatom(tracked) || isarea(tracked) || Dm.lt(range, 0))
            return COMPONENT_INCOMPATIBLE;
        this.connections = connections;
        this.range = range;
        this.works_in_containers = works_in_containers;
        set_tracked(tracked);
        return null;
    }

    // tg: code/datums/components/connect_range.dm:32
    public override object Destroy(float? force = null)
    {
        set_tracked(null);
        return base.Destroy(force);
    }

    // tg: code/datums/components/connect_range.dm:36
    [DmParams("component", "original", "tracked", "connections", "range", "works_in_containers")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        atom tracked = Dm.arg(__extra, 0) as atom;
        DmList connections = Dm.arg<DmList>(__extra, 1);
        object range = Dm.arg(__extra, 2);
        object works_in_containers = Dm.arg(__extra, 3);
        // Both the signal names and their handlers must match.
        if (!Dm.truthy(deep_compare_list(this.connections, connections)))
        {
            stack_trace(Dm.text("connect_range component attached to [] tried to inherit another connect_range component with different connections", parent), "code/datums/components/connect_range.dm", 39);
            return;
        }
        if (this.tracked != tracked)
            set_tracked(tracked);
        if (Dm.eq(this.range, range) && Dm.eq(this.works_in_containers, works_in_containers))
            return;
        //Unregister the signals with the old settings.
        unregister_signals(isturf(tracked) ? tracked : tracked.loc, turfs);
        turfs = Dm.list();
        this.range = range;
        this.works_in_containers = works_in_containers;
        //Re-register the signals with the new settings.
        update_signals(this.tracked);
    }

    // tg: code/datums/components/connect_range.dm:53
    public virtual void set_tracked(atom new_tracked = null)
    {
        if (Dm.truthy(tracked)) //Unregister the signals from the old tracked and its surroundings
        {
            unregister_signals(isturf(tracked) ? tracked : tracked.loc, turfs);
            turfs = Dm.list();
            UnregisterSignal(tracked, Dm.list(
                COMSIG_MOVABLE_MOVED,
                COMSIG_QDELETING
            ));
        }
        tracked = new_tracked;
        if (!Dm.truthy(tracked))
            return;
        //Register signals on the new tracked atom and its surroundings.
        RegisterSignal(tracked, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(tracked, COMSIG_QDELETING, nameof(handle_tracked_qdel));
        update_signals(tracked);
    }

    // tg: code/datums/components/connect_range.dm:69
    [SignalHandler]
    public virtual void handle_tracked_qdel()
    {
        // SIGNAL_HANDLER
        qdel(this);
    }

    // tg: code/datums/components/connect_range.dm:73
    public virtual void update_signals(atom target = null, atom old_loc = null)
    {
        turf current_turf = get_turf(target);
        if (current_turf == null)
        {
            unregister_signals(old_loc, turfs);
            turfs = Dm.list();
            return;
        }

        bool loc_is_movable = ismovable(target.loc);

        if (loc_is_movable)
        {
            if (!Dm.truthy(works_in_containers))
            {
                unregister_signals(old_loc, turfs);
                turfs = Dm.list();
                return;
            }
        }

        // Reconcile subscriptions even on the same turf: the container may have changed.
        DmList old_turfs = turfs;
        turfs = RANGE_TURFS(range, current_turf);
        unregister_signals(old_loc, Dm.sub(old_turfs, turfs) as DmList);
        if (loc_is_movable)
        {
            //Keep track of possible movement of all movables the target is in.
            foreach (atom_movable container in Dm.each_cast<atom_movable>(get_nested_locs(target as atom_movable)))
                RegisterSignal(container, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        }
        foreach (turf target_turf in Dm.each_cast<turf>(Dm.sub(turfs, old_turfs)))
        {
            foreach (object signal in Dm.each(connections))
                parent.RegisterSignal(target_turf, signal, connections[signal]);
        }
    }

    // tg: code/datums/components/connect_range.dm:100
    public virtual void unregister_signals(atom location = null, DmList remove_from = null)
    {
        //The location is null or is a container and the component shouldn't have register signals on it
        if (location == null || (!Dm.truthy(works_in_containers) && !isturf(location)))
            return;

        if (ismovable(location))
        {
            foreach (atom_movable target in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs((atom_movable)location), location)))
                UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
        }

        if (!Dm.truthy(length(remove_from)))
            return;
        foreach (turf target_turf in Dm.each_cast<turf>(remove_from))
            parent.UnregisterSignal(target_turf, connections);
    }

    // tg: code/datums/components/connect_range.dm:114
    [SignalHandler]
    public virtual void on_moved(atom_movable movable = null, atom old_loc = null)
    {
        // SIGNAL_HANDLER
        if (QDELETED(this)) //Basically, if a mob moves it will trigger the movable signal on its own field. If the mob finds a target when moving it will qdel this, but because it also moved into the field this will run, crash, and burn. so we're checking qdeleted.
            return;
        update_signals(movable, old_loc);
    }
}
