// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/// Finds the next point on the carp's migration path
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:4
public partial class datum_bt_node_ai_behavior_find_next_carp_migration_step : datum_bt_node_ai_behavior
{
    private const int CARP_DESTINATION_SEARCH_RANGE = 3; // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:1

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        DmList blackboard_points = controller.blackboard[path_key] as DmList;
        foreach (turf migration_point in Dm.each_cast<turf>(blackboard_points?.Copy()))
        {
            Dm.remove(ref blackboard_points, migration_point);
            if (Dm.num(get_dist(controller.pawn, migration_point)) > CARP_DESTINATION_SEARCH_RANGE)
            {
                controller.set_blackboard_key(target_key, migration_point);
                return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
            }
        }
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
    }
}

/// Forgets the migration target once the carp is near it
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:21
public partial class datum_bt_node_ai_behavior_clear_arrived_migration_target : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:25
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        turf moving_to = controller.blackboard[target_key] as turf;
        if (QDELETED(moving_to) || Dm.num(get_dist(controller.pawn, moving_to)) <= 3)
            controller.clear_blackboard_key(target_key);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}

/// True when the next step toward the migration target is blocked
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:33
public partial class datum_bt_node_decorator_carp_path_blocked : datum_bt_node_decorator
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_migration.dm:37
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return FALSE;
        turf next_step = get_step_towards(controller.pawn, target);
        return next_step == null ? null : await next_step.is_blocked_turf(exclude_mobs: TRUE);
    }
}
