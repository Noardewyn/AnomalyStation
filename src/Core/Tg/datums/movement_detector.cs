// Translated from tgstation (snapshot 1d99bd7f): code/datums/movement_detector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// A datum to handle the busywork of registering signals to handle in depth tracking of a movable
// tg: code/datums/movement_detector.dm:2
[DmPath("/datum/movement_detector")]
public partial class datum_movement_detector : datum
{
    // tg: code/datums/movement_detector.dm:3
    public atom_movable tracked;
    // tg: code/datums/movement_detector.dm:4
    public datum_callback listener;

    // tg: code/datums/movement_detector.dm:6
    [DmParams("target", "listener")]
    public override async DmTask New(params object[] __extra)
    {
        atom_movable target = Dm.arg<atom_movable>(__extra, 0);
        datum_callback listener = Dm.arg<datum_callback>(__extra, 1);
        if (Dm.truthy(target))
            track(target, listener);
    }

    // tg: code/datums/movement_detector.dm:10
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        untrack();
        tracked = null;
        listener = null;
        return base.Destroy(force);
    }

    /// Sets up tracking of the given movable atom
    // tg: code/datums/movement_detector.dm:17
    // (slots.tsv lists target as undecided, rules 3.3: the while(ismovable(target)) test fails only when target.loc has
    // made it a non-movable, and nothing reads it after that, so it keeps its declared type; the loop's as-downcast ends
    // the loop on a turf or null, as the DM test does.)
    public virtual void track(atom_movable target = null, datum_callback listener = null)
    {
        untrack();
        tracked = target;
        this.listener = listener;

        while (ismovable(target))
        {
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(move_react));
            target = target.loc as atom_movable;
        }
    }

    /// Stops tracking
    // tg: code/datums/movement_detector.dm:27
    public virtual void untrack()
    {
        if (!Dm.truthy(tracked))
            return;
        atom_movable target = tracked;
        while (ismovable(target))
        {
            UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
            target = target.loc as atom_movable;
        }
    }

    /**
     * Reacts to any movement that would cause a change in coordinates of the tracked movable atom
     * This works by detecting movement of either the tracked object, or anything it is inside, recursively
     */
    // tg: code/datums/movement_detector.dm:39
    // A COMSIG_MOVABLE_MOVED handler that declares 3 of the signal's 6 arguments (the binder drops the rest, rules 7.7).
    [SignalHandler]
    public virtual void move_react(atom_movable mover = null, atom oldloc = null, object direction = null)
    {
        turf newturf = get_turf(tracked);

        if (Dm.truthy(oldloc) && !isturf(oldloc))
        {
            atom target = oldloc;
            while (ismovable(target))
            {
                UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
                target = target.loc;
            }
        }
        if (tracked.loc != newturf)
        {
            atom target = mover.loc;
            while (ismovable(target))
            {
                RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(move_react), TRUE);
                target = target.loc;
            }
        }

        Waiting.NoSleep(listener.Invoke(tracked, mover, oldloc, direction));
    }
}
