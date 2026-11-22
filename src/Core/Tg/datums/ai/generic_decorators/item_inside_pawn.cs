// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/item_inside_pawn.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/**
 * Gates child on the item at the given blackboard key being located anywhere inside the pawn
 * (recursive contents check via locate()). Clears the key and returns FALSE if the item is deleted.
 * Does NOT clear the key if the item simply isn't inside the pawn  other branches may still use it.
 */
// tg: code/datums/ai/generic_decorators/item_inside_pawn.dm:1
public partial class datum_bt_node_decorator_item_inside_pawn : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/item_inside_pawn.dm:12
    public override object register_observe_signals(atom pawn = null)
    {
        obj_item target = owning_controller?.blackboard[key] as obj_item;
        if (target != null)
        {
            observed_item = target;
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_signal_changed), @override: TRUE);
        }
        RegisterSignals(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_item_key_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/item_inside_pawn.dm:20
    public override void unregister_observe_signals(atom pawn = null)
    {
        if (observed_item != null)
        {
            UnregisterSignal(observed_item, COMSIG_MOVABLE_MOVED);
            observed_item = null;
        }
        UnregisterSignal(pawn, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/item_inside_pawn.dm:27
    [SignalHandler]
    public virtual void on_item_key_changed(atom source = null, params object[] __extra)
    {
        obj_item target = owning_controller?.blackboard[key] as obj_item;
        if (target == observed_item)
            return;
        if (observed_item != null)
        {
            UnregisterSignal(observed_item, COMSIG_MOVABLE_MOVED);
            observed_item = null;
        }
        if (target != null)
        {
            observed_item = target;
            RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_signal_changed), @override: TRUE);
        }
        if (owning_controller != null)
            Waiting.NoSleep(on_observed_change(owning_controller, null));
    }

    // tg: code/datums/ai/generic_decorators/item_inside_pawn.dm:41
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[key] as obj_item;
        if (QDELETED(target))
        {
            controller.clear_blackboard_key(key);
            return FALSE;
        }
        return Dm.num(Dm.@in(target, controller.pawn.contents));
    }
}
