// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/find_unwebbed_turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Find a nearby unwebbed turf to spin webs on and store it in a blackboard key.
/// Returns INSTANT SUCCESS if we already have a valid target, or if we find a new one.
/// Returns INSTANT FAILURE if no valid turf is nearby.
// tg: code/datums/ai/generic_behaviors/find_unwebbed_turf.dm:4
public partial class datum_bt_node_ai_behavior_find_unwebbed_turf : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/find_unwebbed_turf.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living spider = controller.pawn as mob_living;
        atom current_target = controller.blackboard[target_key] as atom;
        if (current_target != null && !has_web(current_target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        controller.clear_blackboard_key(target_key);
        turf our_turf = get_turf(spider);
        if (Dm.truthy(Waiting.NoSleep(is_valid_web_turf(our_turf, spider))))
        {
            controller.set_blackboard_key(target_key, our_turf);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
        }

        DmList turfs_by_range = Dm.list();
        for (float i = 1, i__end = scan_range; i <= i__end; i++)
            turfs_by_range[Dm.text("[]", i)] = Dm.list();
        foreach (turf turf_in_view in Dm.each<turf>(oview(scan_range, our_turf)))
        {
            if (!Dm.truthy(Waiting.NoSleep(is_valid_web_turf(turf_in_view, spider))))
                continue;
            Dm.append_at(turfs_by_range, Dm.text("[]", get_dist(our_turf, turf_in_view)), turf_in_view);
        }

        DmList final_turfs = null;
        foreach (object turf_list in Dm.each(turfs_by_range))
        {
            if (Dm.truthy(length(turfs_by_range[turf_list])))
            {
                final_turfs = turfs_by_range[turf_list] as DmList;
                break;
            }
        }
        if (!Dm.truthy(length(final_turfs)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(target_key, pick(final_turfs));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }

    // locate(/obj/structure/spider/stickyweb) in location
    private static bool has_web(atom location)
    {
        foreach (obj_structure_spider_stickyweb web in Dm.each<obj_structure_spider_stickyweb>(location))
            return true;
        return false;
    }

    // tg: code/datums/ai/generic_behaviors/find_unwebbed_turf.dm:40
    public virtual async DmTask<object> is_valid_web_turf(turf target_turf = null, mob_living spider = null)
    {
        if (has_web(target_turf))
            return FALSE;
        if (HAS_TRAIT(target_turf, TRAIT_SPINNING_WEB_TURF))
            return FALSE;
        return Dm.num(!Dm.truthy(await target_turf.is_blocked_turf(source_atom: spider)));
    }
}
