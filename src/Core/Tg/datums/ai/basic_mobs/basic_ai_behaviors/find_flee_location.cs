// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/find_flee_location.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Finds a nearby flee waypoint away from a threat and stores it in a blackboard key.
/// Each step tries get_step_away first, then falls back to shuffled directions if blocked.
/// Returns INSTANT SUCCESS if at least one step was found, INSTANT FAILURE if completely cornered.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_flee_location.dm:4
public partial class datum_bt_node_ai_behavior_find_flee_location : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_flee_location.dm:11
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object run_distance = Dm.truthy(controller.blackboard[BB_BASIC_MOB_FLEE_DISTANCE]) ? controller.blackboard[BB_BASIC_MOB_FLEE_DISTANCE] : DEFAULT_BASIC_FLEE_DISTANCE;
        atom target = (Dm.truthy(controller.blackboard[hiding_location_key]) ? controller.blackboard[hiding_location_key] : controller.blackboard[target_key]) as atom;
        if (QDELETED(target) || !Dm.truthy(can_see(controller.pawn, target, run_distance)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        turf flee_turf = get_turf(controller.pawn);
        for (float step = 1; step <= flee_steps; step++)
        {
            turf next_step = get_flee_step(controller, target, flee_turf) as turf;
            if (next_step == null)
                break;
            flee_turf = next_step;
        }
        if (flee_turf == get_turf(controller.pawn))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(destination_key, flee_turf);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_flee_location.dm:30
    public virtual object get_flee_step(datum_ai_controller controller = null, atom target = null, turf start_turf = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        datum_can_pass_info pass_info = Dm.@new<datum_can_pass_info>(pawn, controller.get_access());
        turf next_step = get_step_away(start_turf, target);
        if (next_step != null && next_step != start_turf && !Dm.truthy(next_step.density) && !Dm.truthy(start_turf.LinkBlockedWithAccess(next_step, pass_info)))
            return next_step;
        DmList all_dirs = GLOB.alldirs.Copy();
        Dm.remove(ref all_dirs, get_dir(start_turf, next_step));
        Dm.remove(ref all_dirs, get_dir(start_turf, target));
        shuffle_inplace(all_dirs);
        foreach (object dir in Dm.each(all_dirs))
        {
            next_step = get_step(start_turf, Dm.num(dir));
            if (next_step != null && !Dm.truthy(next_step.density) && !Dm.truthy(start_turf.LinkBlockedWithAccess(next_step, pass_info)))
                return next_step;
        }
        return null;
    }
}
