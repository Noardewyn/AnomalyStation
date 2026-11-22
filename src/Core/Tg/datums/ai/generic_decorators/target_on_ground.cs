// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_on_ground.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Check fi target is still on a turf
// tg: code/datums/ai/generic_decorators/target_on_ground.dm:1
public partial class datum_bt_node_decorator_validate_target_on_turf : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/target_on_ground.dm:6
    public override object register_observe_signals(atom pawn = null)
    {
        atom target = owning_controller?.blackboard[key] as atom;
        if (target != null)
        {
            observed_target = target;
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/target_on_ground.dm:14
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_target != null)
        {
            UnregisterSignal(observed_target, COMSIG_MOVABLE_MOVED);
            observed_target = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/target_on_ground.dm:21
    [SignalHandler]
    public virtual void on_key_changed(atom pawn = null, params object[] __extra)
    {
        atom target = owning_controller?.blackboard[key] as atom;
        if (target == observed_target)
            return;
        if (observed_target != null)
        {
            UnregisterSignal(observed_target, COMSIG_MOVABLE_MOVED);
            observed_target = null;
        }
        if (target != null)
        {
            observed_target = target;
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/target_on_ground.dm:34
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        if (QDELETED(target) || !isturf(target.loc))
        {
            controller.clear_blackboard_key(key);
            return FALSE;
        }
        return TRUE;
    }

    /// Check without side effects for observer path.
    // tg: code/datums/ai/generic_decorators/target_on_ground.dm:42
    public override async DmTask<object> evaluate_for_observer(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[key] as atom;
        return Dm.num(!QDELETED(target) && isturf(target.loc));
    }
}
