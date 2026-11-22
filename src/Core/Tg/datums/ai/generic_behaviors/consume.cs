// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/consume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Uses the pawn's held food/drink item on themselves until consumed.
// tg: code/datums/ai/generic_behaviors/consume.dm:2
public partial class datum_bt_node_ai_behavior_consume : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/consume.dm:9
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        obj_item target = controller.blackboard[target_key] as obj_item;
        if (QDELETED(target) || !Dm.truthy(living_pawn.is_holding(target)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        Waiting.Detach(controller.ai_interact(living_pawn, FALSE));

        return AI_BEHAVIOR_DELAY | (Dm.truthy(is_content(living_pawn, target)) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
    }

    // tg: code/datums/ai/generic_behaviors/consume.dm:19
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (!Dm.truthy(succeeded))
            return;
        controller.set_blackboard_key(hunger_timer_key, world.time + rand(12 * SECONDS, 60 * SECONDS));

        mob_living living_pawn = controller.pawn as mob_living;
        obj_item target = controller.blackboard[target_key] as obj_item;
        if (!QDELETED(target) && !Dm.truthy(DOING_INTERACTION_WITH_TARGET(living_pawn, target)))
        {
            controller.clear_blackboard_key(target_key);
            await living_pawn.dropItemToGround(target); // drops empty drink glasses
        }
        foreach (obj_item_trash trash in Dm.each<obj_item_trash>(living_pawn.held_items))
            await living_pawn.dropItemToGround(trash); // drops spawned trash items
    }

    /// Check if the target is fully consumed, or being actively consumed, or if we're just bored of eating it
    // tg: code/datums/ai/generic_behaviors/consume.dm:34
    public virtual object is_content(mob_living living_pawm = null, obj_item target = null)
    {
        if (QDELETED(target))
            return TRUE;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(living_pawm, target)))
            return TRUE;
        if (Dm.num((target.reagents as datum_reagents)?.total_volume) <= 0)
            return TRUE;
        // Even if we don't finish it all we can randomly decide to be done
        return Dm.num(prob(10));
    }
}
