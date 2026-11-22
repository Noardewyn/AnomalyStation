// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/ai/slime_bt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Changes the slime's face, depending on what's going on
// tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:8
public partial class datum_bt_node_ai_behavior_change_slime_face : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!prob(5))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        mob_living_basic_slime slime_pawn = controller.pawn as mob_living_basic_slime;
        if (slime_pawn == null || slime_pawn.stat != 0)
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        object current_mood = slime_pawn.current_mood;
        object new_mood;

        if (Dm.truthy(controller.blackboard[BB_SLIME_RABID]) || Dm.num(LAZYLEN(controller.blackboard[BB_BASIC_MOB_RETALIATE_LIST])) > 0)
            new_mood = SLIME_MOOD_ANGRY;
        else if (Dm.truthy(controller.blackboard[BB_SLIME_HUNGER_DISABLED]))
            new_mood = SLIME_MOOD_SMILE;
        else if (Dm.truthy(controller.blackboard[BB_CURRENT_TARGET]))
            new_mood = SLIME_MOOD_MISCHIEVOUS;
        else
            new_mood = pick(SLIME_MOOD_SAD, SLIME_MOOD_SMILE, SLIME_MOOD_POUT);

        if (!Dm.eq(current_mood, new_mood))
        {
            slime_pawn.current_mood = new_mood as string;
            Waiting.NoSleep(slime_pawn.regenerate_icons());
        }

        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}

// tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:40
public partial class datum_bt_node_decorator_slime_wants_to_eat : datum_bt_node_decorator
{
    // tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:42
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living_basic_slime slime_pawn = controller.pawn as mob_living_basic_slime;
        if (slime_pawn == null || slime_pawn.buckled != null)
            return FALSE;
        if (!Dm.eq(controller.blackboard[BB_SLIME_HUNGER_LEVEL], SLIME_HUNGER_NONE))
            return TRUE;
        if (Dm.truthy(controller.blackboard[BB_SLIME_RABID]))
            return TRUE;
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:53
public partial class datum_bt_node_ai_behavior_feed_on_slime_target : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:57
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_slime slime_pawn = controller.pawn as mob_living_basic_slime;
        if (slime_pawn == null) //bro lmao comeon
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        if (slime_pawn.buckled != null)
        {
            if (slime_pawn.buckled == target) //we got em boys
                return AI_BEHAVIOR_DELAY;
            slime_pawn.stop_feeding();
            return AI_BEHAVIOR_FAILED; //epic fail; try again
        }

        if (!Dm.truthy(slime_pawn.can_feed_on(target)))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        if (!Dm.eq(target.body_position, STANDING_UP) || prob(20))
        {
            Waiting.Detach(slime_pawn.start_feeding(target));
            return AI_BEHAVIOR_DELAY;
        }

        if (target.client != null && Dm.num(target.health) >= 20)
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        Waiting.Detach(slime_pawn.start_feeding(target));
        return AI_BEHAVIOR_DELAY;
    }

    // tg: code/modules/mob/living/basic/slime/ai/slime_bt.dm:82
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        mob_living_basic_slime slime_pawn = controller.pawn as mob_living_basic_slime;
        slime_pawn.stop_feeding();
    }
}
