// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Tries to use a specified ability on the current target
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:2
public partial class datum_bt_node_ai_behavior_targeted_mob_ability : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        datum_action_cooldown ability = controller.blackboard[ability_key] as datum_action_cooldown;
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(ability) || QDELETED(target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (Dm.truthy(maximum_distance) && get_dist(controller.pawn, target) > maximum_distance)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (Dm.truthy(require_adjacency) && !Dm.truthy(Waiting.NoSleep(controller.pawn.Adjacent(target))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(Waiting.NoSleep(ability.IsAvailable())))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        mob pawn_mob = controller.pawn as mob;
        pawn_mob.face_atom(target);
        return start_async();
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:30
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_action_cooldown ability = controller.blackboard[ability_key] as datum_action_cooldown;
        atom target = controller.blackboard[target_key] as atom;
        object result = await ability.Trigger(null, null, target);
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }
}

// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:42
public partial class datum_bt_node_ai_behavior_targeted_mob_ability_and_plan_execute : datum_bt_node_ai_behavior_targeted_mob_ability
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:44
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        controller.set_blackboard_key(BB_BASIC_MOB_EXECUTION_TARGET, controller.blackboard[target_key]);
        await base.finish_action(controller, succeeded);
    }
}

// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:48
public partial class datum_bt_node_ai_behavior_targeted_mob_ability_and_clear_target : datum_bt_node_ai_behavior_targeted_mob_ability
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/targeted_mob_ability.dm:50
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
