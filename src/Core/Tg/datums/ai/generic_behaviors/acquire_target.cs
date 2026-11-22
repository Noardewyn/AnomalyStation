// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/acquire_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (the admin event logger, as movement_types.cs) (gaps package C1). vision_range is hand-declared: a number or a blackboard key.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/ai/ai.dm  (the three singleton lookups of SSai_controllers)
    public static datum_targeting_strategy GET_TARGETING_STRATEGY(object targeting_type) => SSai_controllers.targeting_strategies[targeting_type] as datum_targeting_strategy;
    public static datum_target_priority_strategy GET_TARGET_PRIORITY_STRATEGY(object targeting_type) => SSai_controllers.target_priority_strategies[targeting_type] as datum_target_priority_strategy;
    public static datum_target_source GET_TARGET_SOURCE(object source_type) => SSai_controllers.target_sources[source_type] as datum_target_source;
}

///Baseline for target picking behaviors
// tg: code/datums/ai/generic_behaviors/acquire_target.dm:2
public partial class datum_bt_node_ai_behavior_acquire_target : datum_bt_node_ai_behavior
{
    /// How far to scan for candidates (passed to the target source). Can be a key too.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:12
    public object vision_range = 7;

    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:32
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        if (!Dm.truthy(can_search(controller)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        datum_targeting_strategy strategy = get_targeting_strategy(controller) as datum_targeting_strategy;
        datum_target_priority_strategy priority_strategy = get_target_priority_strategy(controller) as datum_target_priority_strategy;
        object resolved_vision_range = get_vision_range(controller);
        atom current_target = controller.blackboard[target_key] as atom;

        if (Dm.truthy(Waiting.NoSleep(should_keep_target(controller, strategy, priority_strategy, current_target, resolved_vision_range))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;

        if (!Dm.truthy(must_be_reachable))
            return Waiting.NoSleep(find_and_set_target(controller, strategy, priority_strategy, resolved_vision_range));

        // Kick off async reachability search.
        search_strategy = strategy;
        search_priority_strategy = priority_strategy;
        search_range = resolved_vision_range;
        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:57
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        search_strategy = null;
        search_priority_strategy = null;
        search_range = null;
    }

    /// Returns TRUE to abort the search before it starts (e.g. a detection field is already active).
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:64
    public virtual object can_search(datum_ai_controller controller = null)
    {
        return TRUE;
    }

    /// Returns TRUE if the current target is still good and we should skip the search.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:68
    public virtual async DmTask<object> should_keep_target(datum_ai_controller controller = null, datum_targeting_strategy strategy = null, datum_target_priority_strategy priority_strategy = null, atom current_target = null, object resolved_vision_range = null)
    {
        if (revalidation_mode == TARGET_KEEP_IF_SET)
            return Dm.num(current_target != null);
        if (revalidation_mode == TARGET_REVALIDATE)
            return Dm.num(current_target != null && Dm.truthy(await strategy.is_valid_target(controller.pawn as mob_living, current_target, resolved_vision_range, controller)));
        if (revalidation_mode == TARGET_RESELECT_WITH_SELECTION)
            return Dm.num(current_target != null && priority_strategy == null && Dm.truthy(await strategy.is_valid_target(controller.pawn as mob_living, current_target, resolved_vision_range, controller)));
        return FALSE;
    }

    ///Resolves the targeting strategy for this behavior
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:87
    public virtual object get_targeting_strategy(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(targeting_strategy))
            return GET_TARGETING_STRATEGY(typeof(datum_targeting_strategy_anything));
        if (ispath(targeting_strategy))
            return GET_TARGETING_STRATEGY(targeting_strategy);
        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(controller.blackboard[targeting_strategy]);
        if (strategy == null)
            throw Dm.CRASH(Dm.text("No targeting strategy was supplied in the blackboard for []", controller.pawn), "code/datums/ai/generic_behaviors/acquire_target.dm", 94);
        return strategy;
    }

    /// Resolves the optional target priority strategy for this behavior.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:98
    public virtual object get_target_priority_strategy(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(target_priority_strategy))
            return null;
        if (ispath(target_priority_strategy))
            return GET_TARGET_PRIORITY_STRATEGY(target_priority_strategy);
        return GET_TARGET_PRIORITY_STRATEGY(controller.blackboard[target_priority_strategy]);
    }

    /// Resolves a numeric vision range without mutating the behavior's configured value.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:106
    public virtual object get_vision_range(datum_ai_controller controller = null)
    {
        if (isnum(vision_range))
            return vision_range;
        object resolved_range = controller.blackboard[vision_range];
        if (!isnum(resolved_range))
            throw Dm.CRASH(Dm.text("No numeric vision range was supplied in blackboard key [] for []", vision_range, controller.pawn), "code/datums/ai/generic_behaviors/acquire_target.dm", 111);
        return resolved_range;
    }

    ///Actual behavior for collecting and filtering targets
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:115
    public virtual async DmTask<object> find_and_set_target(datum_ai_controller controller = null, datum_targeting_strategy targeting_strategy = null, datum_target_priority_strategy priority_strategy = null, object range = null)
    {
        mob_living living_mob = controller.pawn as mob_living;
        datum_target_source source = GET_TARGET_SOURCE(target_source);
        if (source == null)
            throw Dm.CRASH(Dm.text("No target source found for type [] on []", target_source, controller.pawn), "code/datums/ai/generic_behaviors/acquire_target.dm", 119);

        atom current_target = controller.blackboard[target_key] as atom;
        DmList candidates = source.collect_candidates(living_mob, controller, range) as DmList;

        if (!Dm.truthy(length(candidates)))
        {
            candidates = await on_no_candidates(controller, current_target, targeting_strategy, range) as DmList;
            if (!Dm.truthy(length(candidates)))
            {
                clear_stale_target(controller, current_target);
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
            }
        }

        candidates = await include_current_target(controller, candidates, targeting_strategy, priority_strategy, current_target, range) as DmList;
        DmList filtered = await filter_candidates(controller, candidates, targeting_strategy, priority_strategy, current_target, range) as DmList;

        if (!Dm.truthy(length(filtered)))
        {
            on_no_valid_candidates(controller, current_target);
            clear_stale_target(controller, current_target);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        atom target = pick_final_target(controller, filtered, priority_strategy, current_target) as atom;

        if (target == null)
        {
            on_no_valid_candidates(controller, current_target);
            clear_stale_target(controller, current_target);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        if (target != current_target)
            controller.set_blackboard_key(target_key, target);

        on_target_found(controller, target, targeting_strategy);

        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    /// Clears the target key when revalidating and the search turned up nothing, so a stale target doesn't linger.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:156
    public virtual void clear_stale_target(datum_ai_controller controller = null, atom current_target = null)
    {
        if (revalidation_mode == TARGET_KEEP_IF_SET || current_target == null)
            return;
        controller.clear_blackboard_key(target_key);
    }

    ///Fallback for no targets found.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:162
    public virtual async DmTask<object> on_no_candidates(datum_ai_controller controller = null, atom current_target = null, datum_targeting_strategy strategy = null, object range = null)
    {
        if (!Dm.truthy(target_loss_distance) || current_target == null)
            return Dm.list();
        if (Dm.truthy(await strategy.can_keep_target(controller.pawn as mob_living, current_target, target_loss_distance, controller)))
            return Dm.list(current_target);
        return Dm.list();
    }

    /// Includes a retainable current target in priority-aware reselection even if the target source omitted it.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:170
    public virtual async DmTask<object> include_current_target(datum_ai_controller controller = null, DmList candidates = null, datum_targeting_strategy strategy = null, datum_target_priority_strategy priority_strategy = null, atom current_target = null, object range = null)
    {
        if (priority_strategy == null || current_target == null || Dm.@in(current_target, candidates))
            return candidates;
        if (!Dm.truthy(await strategy.can_keep_target(controller.pawn as mob_living, current_target, Dm.truthy(target_loss_distance) ? target_loss_distance : range, controller)))
            return candidates;
        DmList candidates_with_current = candidates.Copy();
        Dm.append(ref candidates_with_current, current_target);
        return candidates_with_current;
    }

    /// Filters the candidate list to valid targets and excludes candidates ranked below a retainable current target.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:180
    public virtual async DmTask<object> filter_candidates(datum_ai_controller controller = null, DmList candidates = null, datum_targeting_strategy strategy = null, datum_target_priority_strategy priority_strategy = null, atom current_target = null, object range = null)
    {
        mob_living pawn = controller.pawn as mob_living;
        DmList ignore_list = Dm.truthy(ignore_list_key) ? controller.blackboard[ignore_list_key] as DmList : null;
        object current_priority = null;
        if (priority_strategy != null && current_target != null && !Dm.truthy(LAZYACCESS(ignore_list, current_target)))
        {
            if (Dm.truthy(await strategy.can_keep_target(pawn, current_target, Dm.truthy(target_loss_distance) ? target_loss_distance : range, controller)))
                current_priority = priority_strategy.get_target_priority(controller, current_target);
        }
        DmList filtered = Dm.list();
        foreach (atom candidate in Dm.each_cast<atom>(candidates))
        {
            if (Dm.truthy(LAZYACCESS(ignore_list, candidate)))
                continue;
            bool is_retained_current = candidate == current_target
                && Dm.truthy(target_loss_distance)
                && Dm.truthy(await strategy.can_keep_target(pawn, candidate, target_loss_distance, controller));
            if (!is_retained_current && !Dm.truthy(await strategy.is_valid_target(pawn, candidate, range, controller)))
                continue;
            if (current_priority != null && Dm.num(priority_strategy.get_target_priority(controller, candidate)) < Dm.num(current_priority))
                continue;
            Dm.append(ref filtered, candidate);
        }
        return filtered;
    }

    /// Called when filter_candidates produces nothing. Override to trigger side effects (e.g. spawning a detection field).
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:202
    public virtual void on_no_valid_candidates(datum_ai_controller controller = null, atom current_target = null)
    {
    }

    /// Called after a target is selected and written to the blackboard. Override for post-selection side effects.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:206
    public virtual void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
    }

    /// Picks the final target and keeps the current target on policy-defined ties.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:210
    public virtual object pick_final_target(datum_ai_controller controller = null, DmList filtered_targets = null, datum_target_priority_strategy priority_strategy = null, atom current_target = null)
    {
        atom selected_target = (priority_strategy != null
            ? priority_strategy.select_target(controller, filtered_targets)
            : pick(filtered_targets)) as atom;
        if (priority_strategy == null || current_target == null || selected_target == current_target)
            return selected_target;
        if (!Dm.@in(current_target, filtered_targets) || Dm.truthy(priority_strategy.can_replace_target(controller, current_target, selected_target)))
            return selected_target;
        return current_target;
    }

    /// perform_async(): selects candidates in policy order, checking reachability until one succeeds.
    // tg: code/datums/ai/generic_behaviors/acquire_target.dm:221
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_targeting_strategy strategy = search_strategy;
        datum_target_priority_strategy priority_strategy = search_priority_strategy;
        object range = search_range;
        mob_living living_mob = controller.pawn as mob_living;
        datum_target_source source = GET_TARGET_SOURCE(target_source);
        atom current_target = controller.blackboard[target_key] as atom;
        DmList candidates = source.collect_candidates(living_mob, controller, range) as DmList;
        if (!Dm.truthy(length(candidates)))
            candidates = await on_no_candidates(controller, current_target, strategy, range) as DmList;
        candidates = await include_current_target(controller, candidates, strategy, priority_strategy, current_target, range) as DmList;
        // Target sources can return shared lists so we Copy here
        candidates = candidates?.Copy();
        atom target = null;
        while (Dm.truthy(length(candidates)))
        {
            // A previous path search may have yielded, invalidating candidates or changing their priority.
            DmList filtered = await filter_candidates(controller, candidates, strategy, priority_strategy, current_target, range) as DmList;
            if (!Dm.truthy(length(filtered)))
                break;
            atom candidate = pick_final_target(controller, filtered, priority_strategy, current_target) as atom;
            if (candidate == null)
                break;
            Dm.remove(ref candidates, candidate);
            // get_path_to may sleep here  check abort flag after it returns.
            object can_reach = await controller.can_reach_target(candidate, reach_distance, minimum_distance);
            if (!Dm.truthy(async_still_valid()))
                return null;
            // Reachability can yield too. only select or blacklist a target that still matches
            if (!Dm.truthy(length(await filter_candidates(controller, Dm.list(candidate), strategy, priority_strategy, current_target, range))))
                continue;
            if (Dm.truthy(can_reach))
            {
                target = candidate;
                break;
            }
            controller.note_unreachable_target(candidate);
        }
        // If finish_action fired while we were sleeping, bail without touching anything.
        if (!Dm.truthy(async_still_valid()))
            return null;
        if (target != null)
        {
            if (!Dm.eq(target, controller.blackboard[target_key]))
                controller.set_blackboard_key(target_key, target);
            on_target_found(controller, target, strategy);
        }
        else
        {
            on_no_valid_candidates(controller, current_target);
            clear_stale_target(controller, current_target);
        }
        finish_async(target == null ? AI_BEHAVIOR_FAILED : AI_BEHAVIOR_SUCCEEDED);
        return null;
    }
}
