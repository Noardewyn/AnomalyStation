// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_is_holding_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Gates child on the mob held in a blackboard key currently holding any item in their hands.
/// Use "invert": true to gate on the target holding nothing.
// tg: code/datums/ai/generic_decorators/target_is_holding_item.dm:1
public partial class datum_bt_node_decorator_target_is_holding_item : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/target_is_holding_item.dm:9
    public override object register_observe_signals(atom pawn = null)
    {
        mob target = owning_controller?.blackboard[key] as mob;
        if (ismob(target))
        {
            observed_target = target;
            RegisterSignals(target, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM), nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_target_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/target_is_holding_item.dm:17
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_target != null)
        {
            UnregisterSignal(observed_target, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM));
            observed_target = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/target_is_holding_item.dm:24
    [SignalHandler]
    public virtual void on_target_key_changed(atom source = null, params object[] __extra)
    {
        mob target = owning_controller?.blackboard[key] as mob;
        if (target == observed_target)
            return;
        if (observed_target != null)
        {
            UnregisterSignal(observed_target, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM));
            observed_target = null;
        }
        if (ismob(target))
        {
            observed_target = target;
            RegisterSignals(target, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM), nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/target_is_holding_item.dm:38
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob target = controller.blackboard[key] as mob;
        if (QDELETED(target))
            return FALSE;
        return Dm.num(Dm.num(target.get_num_held_items()) > 0);
    }
}
