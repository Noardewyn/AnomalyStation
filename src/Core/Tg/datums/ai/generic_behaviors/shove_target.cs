// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/shove_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

//Shoves the blackboard target
// tg: code/datums/ai/generic_behaviors/shove_target.dm:3
public partial class datum_bt_node_ai_behavior_shove_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/shove_target.dm:9
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        mob_living living_target = controller.blackboard[target_key] as mob_living;

        if (!isliving(living_pawn) || !isliving(living_target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        if (world.time < Dm.num(living_pawn.next_move))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        if (!prob(shove_chance))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        if (!Dm.truthy(Waiting.NoSleep(living_target.IsReachableBy(living_pawn))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        // Standing up, not ourselves, not sharing a turf.
        if (!Dm.truthy(living_pawn.can_disarm(living_target)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        // Right click only shoves off an empty hand, and the click chain uses whichever hand is active,
        // so an occupied active hand has to be swapped out of first or we'd swing our weapon instead.
        if (!Dm.truthy(free_up_hand(living_pawn)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        Waiting.Detach(controller.ai_interact(living_target, TRUE, Dm.list(Dm.kv(RIGHT_CLICK, TRUE))));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }

    /// Makes sure the active hand is empty, swapping to a free one if it isn't. FALSE when every hand is full.
    // tg: code/datums/ai/generic_behaviors/shove_target.dm:38
    public virtual object free_up_hand(mob_living living_pawn = null)
    {
        if (living_pawn.get_active_held_item() == null)
            return TRUE;
        DmList empty_hands = living_pawn.get_empty_held_indexes() as DmList;
        if (!Dm.truthy(length(empty_hands)))
            return FALSE;
        living_pawn.swap_hand(empty_hands[1]);
        return Dm.num(living_pawn.get_active_held_item() == null);
    }
}
