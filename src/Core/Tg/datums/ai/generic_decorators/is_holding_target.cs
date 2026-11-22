// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/is_holding_target.dm
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
 * Gates child on the pawn currently holding the item at the given blackboard key in its hands.
 * Clears the key and returns BT_FAILURE if the item has been deleted.
 * Returns BT_FAILURE without clearing the key if the item exists but is not held  the item
 * may be in a bag or on the floor and other branches may still need to locate it.
 */
// tg: code/datums/ai/generic_decorators/is_holding_target.dm:1
public partial class datum_bt_node_decorator_is_holding_target : datum_bt_node_decorator
{
    public object key;

    // tg: code/datums/ai/generic_decorators/is_holding_target.dm:11
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM, COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/datums/ai/generic_decorators/is_holding_target.dm:15
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM, COMSIG_AI_BLACKBOARD_KEY_SET(key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(key)));
    }

    // tg: code/datums/ai/generic_decorators/is_holding_target.dm:18
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[key] as obj_item;
        mob mob_pawn = controller.pawn as mob;
        if (QDELETED(target))
        {
            controller.clear_blackboard_key(key);
            return FALSE;
        }
        return Dm.num(Dm.truthy(mob_pawn.is_holding(target)));
    }
}
