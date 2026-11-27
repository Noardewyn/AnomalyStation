// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Use tentacles on the current target  only after tracking them for MIN_TIME_TO_TENTACLE, and only if not already leg-grappled
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:15
public partial class datum_bt_node_ai_behavior_targeted_mob_ability_goliath_tentacles : datum_bt_node_ai_behavior_targeted_mob_ability
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:18
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        object target__any = controller.blackboard[target_key];
        if (!(isliving(target__any) || ismecha(target__any)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (isliving(target__any) && target.get_item_by_slot(ITEM_SLOT_LEGCUFFED) != null)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        object time_on_target = controller.blackboard[BB_BASIC_MOB_HAS_TARGET_TIME] ?? 0;
        if (Dm.num(time_on_target) < min_target_time)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return base.perform(seconds_per_tick, controller);
    }
}

/// Randomly picks a nearby undig asteroid turf to dig and stores it in target_key
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:31
public partial class datum_bt_node_ai_behavior_goliath_find_diggable_turf : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:36
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        DmList nearby_turfs = RANGE_TURFS(scan_range, pawn);
        turf_open_misc_asteroid check_turf = pick(nearby_turfs) as turf_open_misc_asteroid;
        if (check_turf == null || Dm.truthy(check_turf.dug))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        controller.set_blackboard_key(target_key, check_turf);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Melee-attacks the stored hole target turf and clears the key when done
// tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:46
public partial class datum_bt_node_ai_behavior_goliath_dig : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:50
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        turf target_turf = controller.blackboard[target_key] as turf;
        mob_living_basic basic_mob = controller.pawn as mob_living_basic;
        if (target_turf == null || !Dm.truthy(Waiting.NoSleep(target_turf.IsReachableBy(basic_mob))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        Waiting.NoSleep(basic_mob.melee_attack(target_turf));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/lavaland/goliath/goliath_ai.dm:58
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
