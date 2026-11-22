// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/virtual_pick_up_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Moves the item at target_key onto the pawn and records it in storage_key. Does not use hands  storage_key is the virtual carry slot. Clears target_key on finish.
// tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:2
public partial class datum_bt_node_ai_behavior_pick_up_item_virtual : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:8
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[target_key] as obj_item;
        return Dm.num(target != null && isturf(target.loc) && !Dm.truthy(target.anchored));
    }

    // tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[target_key] as obj_item;
        if (QDELETED(target) || !isturf(target.loc))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(Waiting.NoSleep(controller.pawn.Adjacent(target))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.NoSleep(_pickup(controller, target, storage_key));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:21
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }

    // tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:25
    public virtual async DmTask _pickup(datum_ai_controller controller = null, obj_item target = null, object storage_key = null)
    {
        atom pawn = controller.pawn;
        obj_item held = controller.blackboard[storage_key] as obj_item;
        if (held?.loc == pawn)
        {
            pawn.visible_message(span_notice(I18n.T("[] drops [].", pawn, held)));
            await held.forceMove(get_turf(pawn));
            controller.clear_blackboard_key(storage_key);
        }
        pawn.visible_message(span_notice(I18n.T("[] picks up [].", pawn, target)));
        await target.forceMove(pawn);
        controller.set_blackboard_key(storage_key, target);
    }
}

/// Passes the item at storage_key to whoever is at delivery_key (must already be adjacent).
// tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:37
public partial class datum_bt_node_ai_behavior_pass_item_virtual : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:43
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[delivery_key] as atom;
        return Dm.num(!QDELETED(target));
    }

    // tg: code/datums/ai/generic_behaviors/virtual_pick_up_item.dm:47
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[delivery_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob pawn = controller.pawn as mob;
        obj_item item = controller.blackboard[storage_key] as obj_item;
        if (QDELETED(item) || item.loc != pawn)
        {
            pawn.visible_message(span_notice(I18n.T("[] looks around as if [] [] lost something.", pawn, pawn.p_they(), pawn.p_have())));
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }
        pawn.visible_message(span_notice(I18n.T("[] delivers [] to [].", pawn, item, target)));
        Waiting.NoSleep(item.forceMove(get_turf(target)));
        controller.clear_blackboard_key(storage_key);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
