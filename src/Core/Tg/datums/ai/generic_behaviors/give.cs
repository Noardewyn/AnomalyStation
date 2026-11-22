// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/give.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// gives the pawn's active held item to a blackboard-keyed target.
// tg: code/datums/ai/generic_behaviors/give.dm:2
public partial class datum_bt_node_ai_behavior_give : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/give.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        mob_living pawn = controller.pawn as mob_living;
        obj_item held_item = pawn.get_active_held_item() as obj_item;
        atom target = controller.blackboard[target_key] as atom;

        if (held_item == null) //if held_item is null, we pretend that action was successful
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;

        if (QDELETED(target) || !Dm.truthy(Waiting.NoSleep(target.IsReachableBy(pawn))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        if (target is not mob_living living_target) // target should reasonably only ever be set to a living mob
        {
            stack_trace("Tried to give an item to a non-living target!", "code/datums/ai/generic_behaviors/give.dm", 27);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        if (!Dm.truthy(Waiting.NoSleep(can_give_item(living_target, held_item))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        living_target.visible_message(
            span_info(I18n.T("[] starts trying to give [] to []!", pawn, held_item, living_target)),
            span_warning(I18n.T("[] tries to give you []!", pawn, held_item))
        );
        give_target = living_target;
        give_held_item = held_item;
        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/give.dm:40
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        mob_living living_target = give_target;
        obj_item held_item = give_held_item;
        object result_flags = AI_BEHAVIOR_FAILED;
        if (Dm.truthy(await do_after(pawn, 1 * SECONDS, living_target)))
            result_flags = await try_to_give_item(living_target, held_item);
        finish_async(result_flags);
        return null;
    }

    /// Returns a list(has_left_pocket, has_right_pocket, has_valid_hand) if the item can be given, null otherwise.
    // tg: code/datums/ai/generic_behaviors/give.dm:50
    public virtual async DmTask<object> can_give_item(mob_living target = null, obj_item held_item = null)
    {
        if (QDELETED(held_item) || QDELETED(target))
            return null;

        object has_left_pocket = await target.can_equip(held_item, ITEM_SLOT_LPOCKET);
        object has_right_pocket = await target.can_equip(held_item, ITEM_SLOT_RPOCKET);
        object has_valid_hand = null;

        foreach (object hand_index in Dm.each(target.get_empty_held_indexes()))
        {
            if (Dm.truthy(target.can_put_in_hand(held_item, hand_index)))
            {
                has_valid_hand = TRUE;
                break;
            }
        }

        if (!Dm.truthy(has_left_pocket) && !Dm.truthy(has_right_pocket) && !Dm.truthy(has_valid_hand))
            return null;

        return Dm.list(has_left_pocket, has_right_pocket, has_valid_hand);
    }

    // tg: code/datums/ai/generic_behaviors/give.dm:68
    public virtual async DmTask<object> try_to_give_item(mob_living target = null, obj_item held_item = null)
    {
        DmList give_slots = await can_give_item(target, held_item) as DmList;
        if (!Dm.truthy(give_slots))
            return AI_BEHAVIOR_FAILED;

        object has_left_pocket = give_slots[1];
        object has_valid_hand = give_slots[3];

        if (!Dm.truthy(has_valid_hand) || prob(50))
            await target.equip_to_slot_if_possible(held_item, !Dm.truthy(has_left_pocket) ? ITEM_SLOT_RPOCKET : (prob(50) ? ITEM_SLOT_LPOCKET : ITEM_SLOT_RPOCKET));
        else
            Waiting.Detach(target.put_in_hands(held_item));
        return AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/give.dm:82
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        give_target = null;
        give_held_item = null;
        controller.clear_blackboard_key(target_key);
    }
}
