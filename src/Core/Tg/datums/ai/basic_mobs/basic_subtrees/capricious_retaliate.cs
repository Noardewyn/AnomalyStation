// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_subtrees/capricious_retaliate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Random chance to add things to our retaliate list
// tg: code/datums/ai/basic_mobs/basic_subtrees/capricious_retaliate.dm:2
public partial class datum_bt_node_ai_behavior_capricious_retaliate : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_subtrees/capricious_retaliate.dm:7
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom pawn = controller.pawn;

        if (Dm.truthy(controller.blackboard_key_exists(BB_BASIC_MOB_RETALIATE_LIST)))
        {
            float deaggro_chance = Dm.truthy(controller.blackboard[BB_RANDOM_DEAGGRO_CHANCE]) ? Dm.num(controller.blackboard[BB_RANDOM_DEAGGRO_CHANCE]) : 10;
            if (prob(deaggro_chance)) //Chance to chill the fuck out. This prob() should be matched with the frequency of calling.
            {
                pawn.visible_message(span_notice(I18n.T("[] calms down.", pawn)));
                controller.clear_blackboard_key(BB_BASIC_MOB_RETALIATE_LIST);
                controller.clear_blackboard_key(BB_CURRENT_TARGET);
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED; // De-aggroed
            }
        }

        float aggro_chance = Dm.truthy(controller.blackboard[BB_RANDOM_AGGRO_CHANCE]) ? Dm.num(controller.blackboard[BB_RANDOM_AGGRO_CHANCE]) : 0.5f;
        if (!prob(aggro_chance)) //Check if we should get pissed at someone REEE
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        object aggro_range = Dm.truthy(controller.blackboard[BB_AGGRO_RANGE]) ? controller.blackboard[BB_AGGRO_RANGE] : 9;
        DmList potential_targets = Dm.sub(hearers(aggro_range, get_turf(pawn)), pawn) as DmList;
        if (!Dm.truthy(length(potential_targets)))
        {
            failed_targeting(pawn);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        if (!ispath(targeting_strategy))
            targeting_strategy = controller.blackboard[targeting_strategy];

        datum_targeting_strategy target_helper = GET_TARGETING_STRATEGY(targeting_strategy);

        if (Dm.truthy(ignore_faction))
            controller.set_blackboard_key(BB_TEMPORARILY_IGNORE_FACTION, TRUE);

        mob_living final_target = null;
        while (final_target == null && Dm.truthy(length(potential_targets)))
        {
            mob_living test_target = pick_n_take(potential_targets) as mob_living;
            if (Dm.truthy(Waiting.NoSleep(target_helper.is_valid_target(pawn as mob_living, test_target, vision_range: aggro_range))))
                final_target = test_target;
        }

        if (final_target == null)
        {
            failed_targeting(pawn);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        // Add to shitlist  set_blackboard_key_assoc_lazylist calls post_blackboard_key_set, waking the combat branch
        controller.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, final_target, world.time);
        pawn.visible_message(span_warning(I18n.T("[] glares grumpily at []!", pawn, final_target)));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_subtrees/capricious_retaliate.dm:51
    public virtual void failed_targeting(atom pawn = null)
    {
        pawn.visible_message(span_notice(I18n.T("[] grumbles.", pawn)));
    }

    // tg: code/datums/ai/basic_mobs/basic_subtrees/capricious_retaliate.dm:54
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded) || !Dm.truthy(ignore_faction))
            return;
        object usually_ignores_faction = Dm.truthy(controller.blackboard[BB_ALWAYS_IGNORE_FACTION]) ? controller.blackboard[BB_ALWAYS_IGNORE_FACTION] : FALSE;
        controller.set_blackboard_key(BB_TEMPORARILY_IGNORE_FACTION, usually_ignores_faction);
    }
}
