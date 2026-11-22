// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/target_retaliate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Pick a target from our retaliate list
// tg: code/datums/ai/generic_behaviors/target_retaliate.dm:3
public partial class datum_bt_node_ai_behavior_acquire_target_target_from_retaliate_list : datum_bt_node_ai_behavior_acquire_target
{
    /// If FALSE, temporarily ignores faction during the search. (Hand-declared: garden_gnome.bt.json leaves an unbound "$..." text in it, which DM's truthiness reads as TRUE.)
    // tg: code/datums/ai/generic_behaviors/target_retaliate.dm:11
    public object check_faction = FALSE;

    // tg: code/datums/ai/generic_behaviors/target_retaliate.dm:4 (a class with a hand-declared var keeps its constructor by hand, types-converter.md)
    public datum_bt_node_ai_behavior_acquire_target_target_from_retaliate_list()
    {
        target_source = typeof(datum_target_source_from_bb_list_retaliate_list);
        target_priority_strategy = BB_TARGET_PRIORITY_STRATEGY;
        revalidation_mode = TARGET_RESELECT_WITH_SELECTION;
        time_between_perform = 2 * SECONDS;
        vision_range = 9;
    }

    // tg: code/datums/ai/generic_behaviors/target_retaliate.dm:14
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!Dm.truthy(check_faction)) // This is lame, but comeon man the polar bears kept killing each other
            controller.set_blackboard_key(BB_TEMPORARILY_IGNORE_FACTION, TRUE);
        object dot = base.perform(seconds_per_tick, controller);
        object usually_ignores_faction = Dm.truthy(controller.blackboard[BB_ALWAYS_IGNORE_FACTION]) ? controller.blackboard[BB_ALWAYS_IGNORE_FACTION] : FALSE;
        controller.set_blackboard_key(BB_TEMPORARILY_IGNORE_FACTION, usually_ignores_faction);
        return dot;
    }

    // tg: code/datums/ai/generic_behaviors/target_retaliate.dm:21
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        atom hiding = strategy.find_hidden_mobs(controller.pawn as mob_living, target) as atom;
        if (hiding != null)
            controller.set_blackboard_key(hiding_location_key, hiding);
        else if (Dm.truthy(hiding_location_key))
            controller.clear_blackboard_key(hiding_location_key);
    }

    // tg: code/datums/ai/generic_behaviors/target_retaliate.dm:28
    public override void on_no_valid_candidates(datum_ai_controller controller = null, atom current_target = null)
    {
        if (current_target != null)
            controller.clear_blackboard_key(target_key);
    }
}
