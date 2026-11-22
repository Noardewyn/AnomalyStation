// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Target priority datum, exists to allow target acquisition behaviors to have custom selection policies. Singleton.
// tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:2
public partial class datum_target_priority_strategy : datum
{
    /// Returns a number representing the priority of a target, higher -> more likely to be selected.
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:5
    public virtual object get_target_priority(datum_ai_controller controller = null, atom target = null)
    {
        return 1;
    }

    /// Returns a single atom from the list of passed targets
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:9
    public virtual object select_target(datum_ai_controller controller = null, DmList targets = null)
    {
        DmList target_priorities = Dm.list();
        foreach (atom target in Dm.each_cast<atom>(targets))
            target_priorities[target] = get_target_priority(controller, target);
        return pick_weight(target_priorities);
    }

    /// Returns TRUE if candidate may replace current_target during a reselection pass.
    /// The default is sticky on ties to avoid otherwise-equivalent targets changing every search tick.
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:17
    public virtual object can_replace_target(datum_ai_controller controller = null, atom current_target = null, atom candidate = null)
    {
        return Dm.num(Dm.num(get_target_priority(controller, candidate)) > Dm.num(get_target_priority(controller, current_target)));
    }
}

/// Selects the closest candidate.
// tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:21
public partial class datum_target_priority_strategy_nearest : datum_target_priority_strategy
{
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:23
    public override object get_target_priority(datum_ai_controller controller = null, atom target = null)
    {
        return -get_dist(controller.pawn, target);
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:26
    public override object select_target(datum_ai_controller controller = null, DmList targets = null)
    {
        return get_closest_atom(typeof(atom), targets, get_turf(controller.pawn));
    }
}

/// Selects the living candidate with the lowest health.
// tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:30
public partial class datum_target_priority_strategy_most_wounded : datum_target_priority_strategy
{
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:32
    public override object get_target_priority(datum_ai_controller controller = null, atom target = null)
    {
        if (target is not mob_living living_target)
            return -INFINITY;
        return -living_target.health;
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:38
    public override object select_target(datum_ai_controller controller = null, DmList targets = null)
    {
        DmList living_targets = Dm.list();
        foreach (mob_living living_target in Dm.each<mob_living>(targets))
            Dm.append(ref living_targets, living_target);
        if (!Dm.truthy(length(living_targets)))
            return base.select_target(controller, targets);
        sortTim(living_targets, "/proc/" + nameof(GlobalProcs.cmp_mob_health));
        return living_targets[living_targets.len];
    }
}

/// Selects candidates carrying the trait configured in trait_key before candidates without it.
// tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:48
public partial class datum_target_priority_strategy_prioritize_trait : datum_target_priority_strategy
{
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:52
    public override object get_target_priority(datum_ai_controller controller = null, atom target = null)
    {
        return HAS_TRAIT(target, controller.blackboard[trait_key]) ? 2 : 1;
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:55
    public override object select_target(datum_ai_controller controller = null, DmList targets = null)
    {
        DmList priority_targets = Dm.list();
        object priority_trait = controller.blackboard[trait_key];
        foreach (atom target in Dm.each<atom>(targets))
        {
            if (HAS_TRAIT(target, priority_trait))
                Dm.append(ref priority_targets, target);
        }
        return base.select_target(controller, Dm.truthy(length(priority_targets)) ? priority_targets : targets);
    }
}

/// Chooses a nonempty target type tier by weight, then randomly chooses a target within that tier.
/// This prevents a large number of low-priority candidates from drowning out one high-priority candidate.
// tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:65
public partial class datum_target_priority_strategy_type_weighted : datum_target_priority_strategy
{
    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:71
    public virtual object matches_priority_type(atom target = null, object priority_type = null)
    {
        return Dm.num(istype(target, priority_type as Type));
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:74
    public override object get_target_priority(datum_ai_controller controller = null, atom target = null)
    {
        DmList priorities = controller.blackboard[priorities_key] as DmList;
        foreach (object priority_type in Dm.each(priorities))
        {
            if (Dm.truthy(matches_priority_type(target, priority_type)))
                return priorities[priority_type];
        }
        object fallback = controller.blackboard[fallback_priority_key];
        return Dm.truthy(fallback) ? fallback : 1;
    }

    // tg: code/datums/ai/basic_mobs/target_priority_strategies/_target_priority_strategy.dm:81
    public override object select_target(datum_ai_controller controller = null, DmList targets = null)
    {
        DmList priorities = controller.blackboard[priorities_key] as DmList;
        if (!Dm.truthy(length(priorities)))
            return base.select_target(controller, targets);

        DmList type_buckets = Dm.list();
        DmList fallback_targets = Dm.list();
        foreach (atom target in Dm.each_cast<atom>(targets))
        {
            object matched_type = null;
            foreach (object priority_type in Dm.each(priorities))
            {
                if (Dm.truthy(matches_priority_type(target, priority_type)))
                {
                    matched_type = priority_type;
                    break;
                }
            }
            if (matched_type == null)
            {
                Dm.append(ref fallback_targets, target);
                continue;
            }
            DmList bucket = type_buckets[matched_type] as DmList;
            if (bucket == null)
            {
                bucket = Dm.list();
                type_buckets[matched_type] = bucket;
            }
            Dm.append(ref bucket, target);
        }

        const string fallback_tier = "fallback";
        DmList available_weights = Dm.list();
        foreach (object priority_type in Dm.each(priorities))
        {
            if (Dm.truthy(length(type_buckets[priority_type])))
                available_weights[priority_type] = priorities[priority_type];
        }
        if (Dm.truthy(length(fallback_targets)))
        {
            object fallback = controller.blackboard[fallback_priority_key];
            available_weights[fallback_tier] = Dm.truthy(fallback) ? fallback : 1;
        }

        object selected_tier = pick_weight(available_weights);
        DmList selected_targets = Dm.eq(selected_tier, fallback_tier) ? fallback_targets : type_buckets[selected_tier] as DmList;
        return pick(selected_targets);
    }
}
