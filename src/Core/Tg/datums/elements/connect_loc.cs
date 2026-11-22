// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/connect_loc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// This element hooks a signal onto the loc the current object is on.
/// When the object moves, it will unhook the signal and rehook it to the new object.
// tg: code/datums/elements/connect_loc.dm:3
[DmPath("/datum/element/connect_loc")]
public partial class datum_element_connect_loc : datum_element
{
    // tg: code/datums/elements/connect_loc.dm:4-5
    public datum_element_connect_loc()
    {
        element_flags = Dm.bitor(ELEMENT_BESPOKE, ELEMENT_NO_LIST_UNIT_TEST);
        argument_hash_start_idx = 2;
    }

    /// An assoc list of signal -> procpath to register to the loc this object is on.
    // tg: code/datums/elements/connect_loc.dm:8
    public DmList connections;

    // tg: code/datums/elements/connect_loc.dm:10
    [DmParams("listener", "connections")]
    public override async DmTask<object> Attach(datum listener = null, params object[] __extra)
    {
        DmList connections = Dm.arg<DmList>(__extra, 0);
        object dot = null;
        dot = await base.Attach(listener, Dm.forward(__extra, connections));
        if (!(listener is atom_movable))
            return ELEMENT_INCOMPATIBLE;

        this.connections = connections;

        RegisterSignal(listener, COMSIG_MOVABLE_MOVED, nameof(on_moved), @override: TRUE);
        update_signals((atom_movable)listener);
        return dot;
    }

    // tg: code/datums/elements/connect_loc.dm:20
    [DmParams("listener")]
    public override object Detach(datum listener = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Detach(listener, __extra);
        unregister_signals(listener, ((atom_movable)listener).loc);
        UnregisterSignal(listener, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    // tg: code/datums/elements/connect_loc.dm:25
    public virtual void update_signals(atom_movable listener = null)
    {
        atom listener_loc = listener.loc;
        if (QDELETED(listener) || QDELETED(listener_loc))
            return;

        foreach (object signal in Dm.each(connections))
            //override=TRUE because more than one connect_loc element instance tracked object can be on the same loc
            listener.RegisterSignal(listener_loc, signal, connections[signal], @override: TRUE);
    }

    // tg: code/datums/elements/connect_loc.dm:34
    public virtual void unregister_signals(datum listener = null, atom old_loc = null)
    {
        if (old_loc == null)
            return;

        listener.UnregisterSignal(old_loc, connections);
    }

    // tg: code/datums/elements/connect_loc.dm:40
    [SignalHandler]
    public virtual void on_moved(atom_movable listener = null, atom old_loc = null)
    {
        unregister_signals(listener, old_loc);
        update_signals(listener);
    }
}
