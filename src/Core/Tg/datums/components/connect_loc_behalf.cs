// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/connect_loc_behalf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (wet floors and slipping): the whole file (a slippery mob or item hooks the turf it is on through it).

/// This component behaves similar to connect_loc, hooking into a signal on a tracked object's turf
/// It has the ability to react to that signal on behalf of a separate listener however
/// This has great use, primarily for components, but it carries with it some overhead
/// So we do it separately as it needs to hold state which is very likely to lead to bugs if it remains as an element.
// tg: code/datums/components/connect_loc_behalf.dm:5
public partial class datum_component_connect_loc_behalf : datum_component
{
    // tg: code/datums/components/connect_loc_behalf.dm:15
    [DmParams("tracked", "connections")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object tracked = Dm.arg(__extra, 0);
        DmList connections = Dm.arg<DmList>(__extra, 1);
        object dot = await base.Initialize(Dm.forward(__extra, tracked, connections));
        if (!(tracked is atom_movable))
            return COMPONENT_INCOMPATIBLE;
        this.connections = connections;
        this.tracked = (atom_movable)tracked;
        return dot;
    }

    // tg: code/datums/components/connect_loc_behalf.dm:22
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(tracked, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(tracked, COMSIG_QDELETING, nameof(handle_tracked_qdel));
        update_signals();
    }

    // tg: code/datums/components/connect_loc_behalf.dm:27
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        unregister_signals();
        UnregisterSignal(tracked, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_QDELETING
        ));

        tracked = null;
    }

    // tg: code/datums/components/connect_loc_behalf.dm:36
    [SignalHandler]
    public virtual void handle_tracked_qdel()
    {
        // SIGNAL_HANDLER
        qdel(this);
    }

    // tg: code/datums/components/connect_loc_behalf.dm:40
    public virtual void update_signals()
    {
        unregister_signals();
        //You may ask yourself, isn't this just silencing an error?
        //The answer is yes, but there's no good cheap way to fix it
        //What happens is the tracked object or hell the listener gets say, deleted, which makes targets[old_loc] return a null
        //The null results in a bad index, because of course it does
        //It's not a solvable problem though, since both actions, the destroy and the move, are sourced from the same signal send
        //And sending a signal should be agnostic of the order of listeners
        //So we need to either pick the order agnositic, or destroy safe
        //And I picked destroy safe. Let's hope this is the right path!
        if (tracked.loc == null)
            return;

        tracked_loc = tracked.loc;

        foreach (object signal in Dm.each(connections))
            parent.RegisterSignal(tracked_loc, signal, connections[signal]);
    }

    // tg: code/datums/components/connect_loc_behalf.dm:58
    public virtual void unregister_signals()
    {
        if (tracked_loc == null)
            return;

        // (a turf replaced under the tracked atom, the singularity eating its own tile, is followed to its cell's new turf by
        // UnregisterSignal: TURF-IDENTITY, turf.dm_identity)
        parent.UnregisterSignal(tracked_loc, connections);

        tracked_loc = null;
    }

    // tg: code/datums/components/connect_loc_behalf.dm:66
    [SignalHandler]
    public virtual void on_moved(object sigtype = null, atom_movable tracked = null, atom old_loc = null)
    {
        // SIGNAL_HANDLER
        update_signals();
    }
}
