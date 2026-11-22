// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_priority_strategies/mining_strategies.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Prioritizes NODE drones until attacked, then swaps back onto miners
// tg: code/datums/ai/basic_mobs/target_priority_strategies/mining_strategies.dm:16
public partial class datum_target_priority_strategy_mining : datum_target_priority_strategy
{
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/mining_strategies.dm:2-14 (file-local)
    private const float AGGRO_PRIORITY_ASHWALKER = 1;
    private const float AGGRO_PRIORITY_BASICMOB = 3;
    private const float AGGRO_PRIORITY_HUMAN = 5;
    private const float AGGRO_PRIORITY_MINER = 8;
    private const float AGGRO_PRIORITY_RETALIATE = 15;

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/mining_strategies.dm:22
    public override object get_target_priority(datum_ai_controller controller = null, atom target = null)
    {
        if (target is not mob_living living_target)
            return base.get_target_priority(controller, target);

        // If a mob recently attacked us, it has higher priority than NODE drones, otherwise we care about NODE drones more
        DmList shitlist = controller.blackboard[BB_BASIC_MOB_RETALIATE_LIST] as DmList;
        if (Dm.truthy(shitlist?[target]) && Dm.num(shitlist[target]) + retaliate_aggro_memory > world.time)
        {
            // Does not decay until it passes the retaliate memory threshold to avoid constant target swapping
            return AGGRO_PRIORITY_RETALIATE;
        }

        if (target is mob_living_basic_node_drone)
            return node_priority;

        // Check if they've recently attacked any of our friends, if so - increase their priority by 1 for each attack in the past [retaliate_aggro_memory] seconds
        DmList allies_shitlist = controller.blackboard[BB_MINING_MOB_REINFORCEMENTS_REQUESTS] as DmList;
        DmList target_requests = allies_shitlist?[target] as DmList;
        float aggro_boost = 0;
        if (target_requests != null)
        {
            float total_requests = length(target_requests);
            // Goes end-to-start because we do not care about requests older than [retaliate_aggro_memory]
            for (float i = 1; i <= total_requests; i++)
            {
                object request_time = target_requests[total_requests - i + 1];
                if (Dm.num(request_time) + retaliate_aggro_memory < world.time)
                    break;
                aggro_boost += 1;
            }
        }

        if (Dm.truthy(aggro_boost) || HAS_TRAIT(target, TRAIT_MINING_AGGRO))
            return AGGRO_PRIORITY_MINER + aggro_boost;

        if (target is mob_living_basic)
            return AGGRO_PRIORITY_BASICMOB;

        return Dm.truthy(living_target.has_faction(FACTION_ASHWALKER)) ? AGGRO_PRIORITY_ASHWALKER : AGGRO_PRIORITY_HUMAN;
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/mining_strategies.dm:56
    public override object select_target(datum_ai_controller controller = null, DmList targets = null)
    {
        float max_priority = 0;
        float min_distance = INFINITY;
        object lucky_fella = null;

        // Selects highest priority targets, then picks the closest
        foreach (atom target in Dm.each_cast<atom>(targets))
        {
            float target_prio = Dm.num(get_target_priority(controller, target));
            if (target_prio < max_priority)
                continue;

            if (target_prio > max_priority)
            {
                max_priority = target_prio;
                min_distance = INFINITY;
            }

            float target_dist = get_dist(controller.pawn, target);
            if (target_dist < min_distance)
            {
                lucky_fella = target;
                min_distance = target_dist;
            }
        }

        return pick(lucky_fella);
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/mining_strategies.dm:78
    public override object can_replace_target(datum_ai_controller controller = null, atom current_target = null, atom candidate = null)
    {
        float current_priority = Dm.num(get_target_priority(controller, current_target));
        float candidate_priority = Dm.num(get_target_priority(controller, candidate));
        if (candidate_priority != current_priority)
            return Dm.num(candidate_priority > current_priority);
        return Dm.num(get_dist(controller.pawn, candidate) < get_dist(controller.pawn, current_target));
    }
}
