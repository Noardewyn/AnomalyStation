// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/shuttle_move_deferred_checks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the circuits' MMI component defers its connection checks with it). The var block is generated.

// This component allows for certain movement checks, particularly those involving linked atoms existing on the same z-level, to be deferred when a shuttle moves.
// tg: code/datums/components/shuttle_move_deferred_checks.dm:2
public partial class datum_component_shuttle_move_deferred_checks : datum_component
{
    // tg: code/datums/components/shuttle_move_deferred_checks.dm:13
    [DmParams("check")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object check = Dm.arg(__extra, 0);
        object dot = await base.Initialize(__extra);
        if (!Dm.truthy(check))
            return COMPONENT_INCOMPATIBLE;
        this.check = check;
        return dot;
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:19
    public override object Destroy(float? force = null)
    {
        targets = null;
        check = null;
        moving_targets = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:25
    [DmParams("source", "check")]
    public override async DmTask<object> on_source_add(object source = null, params object[] __extra)
    {
        object check = Dm.arg(__extra, 0);
        object dot = await base.on_source_add(source, __extra);
        atom_movable movable = Dm.locate_ref(source) as atom_movable;
        if (!(movable is atom_movable) || !Dm.eq(check, this.check))
            return COMPONENT_INCOMPATIBLE;
        LAZYADD(ref targets, movable);
        RegisterSignal(movable, COMSIG_MOVABLE_MOVED, nameof(on_target_moved));
        RegisterSignal(movable, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, nameof(before_target_shuttle_move));
        RegisterSignal(movable, COMSIG_ATOM_AFTER_SHUTTLE_MOVE, nameof(after_target_shuttle_move));
        RegisterSignal(movable, COMSIG_QDELETING, nameof(on_target_deleted));
        return dot;
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:36
    public override void on_source_remove(object source = null)
    {
        atom_movable movable = Dm.locate_ref(source) as atom_movable;
        if (!(movable is atom_movable))
            return;
        LAZYREMOVE(ref targets, movable);
        LAZYREMOVE(ref moving_targets, movable);
        UnregisterSignal(movable, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, COMSIG_ATOM_AFTER_SHUTTLE_MOVE, COMSIG_QDELETING));
        base.on_source_remove(source);
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:45
    public virtual void call_check()
    {
        if (istype(check, typeof(datum_callback)))
        {
            datum_callback callback_check = check as datum_callback;
            Waiting.NoSleep(callback_check.Invoke());
        }
        else
            Waiting.NoSleep(Dm.Call(parent, Dm.str(check)));
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:52
    [SignalHandler]
    public virtual void on_target_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (Dm.truthy(LAZYLEN(moving_targets)))
            return;
        call_check();
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:58
    [SignalHandler]
    public virtual void before_target_shuttle_move(atom source = null)
    {
        LAZYOR(ref moving_targets, source);
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:62
    [SignalHandler]
    public virtual void after_target_shuttle_move(atom source = null)
    {
        // tg adds the target again here where it means to remove it (LAZYOR, not LAZYREMOVE), so a target moved once by a
        // shuttle defers the checks for good; kept as tg has it
        LAZYOR(ref moving_targets, source);
        if (!Dm.truthy(LAZYLEN(moving_targets)))
            call_check();
    }

    // tg: code/datums/components/shuttle_move_deferred_checks.dm:68
    [SignalHandler]
    public virtual void on_target_deleted(datum source = null)
    {
        on_source_remove(REF(source));
    }
}
