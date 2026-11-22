// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # Make carp rift
 * Plan to teleport to a target, using a carp rift ability
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:5
public partial class datum_bt_node_ai_behavior_make_carp_rift : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:13
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        object dot = await base.setup(controller);
        if (!Dm.truthy(controller.blackboard[ability_key]) || !Dm.truthy(controller.blackboard[target_key]))
            return FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:18
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = controller.blackboard[ability_key] as datum_action_cooldown_mob_cooldown_lesser_carp_rift;
        atom target = controller.blackboard[target_key] as atom;

        if (!Dm.truthy(Waiting.NoSleep(validate_target(controller, target, ability))))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        turf target_destination = Waiting.NoSleep(find_target_turf(controller, target, ability)) as turf;
        if (target_destination == null)
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        rift_destination = target_destination;
        return start_async();
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:36
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = controller.blackboard[ability_key] as datum_action_cooldown_mob_cooldown_lesser_carp_rift;
        object result = await ability.InterceptClickOn(controller.pawn as mob_living, null, rift_destination);
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:43
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        rift_destination = null;
    }

    /// Return true if your target is valid for the action
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:48
    public virtual async DmTask<object> validate_target(datum_ai_controller controller = null, atom target = null, datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = null)
    {
        if (ability == null)
            return FALSE;
        if (target == null)
            return FALSE;
        return TRUE;
    }

    /// Return the turf to teleport to, override for your behaviour
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:56
    public virtual async DmTask<object> find_target_turf(datum_ai_controller controller = null, atom target = null, datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = null)
    {
        throw Dm.CRASH("Called unimplemented target finding proc on carp rift behaviour");
    }
}

/// Make a rift to flee from your target
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:62
public partial class datum_bt_node_ai_behavior_make_carp_rift_away : datum_bt_node_ai_behavior_make_carp_rift
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:64
    public override async DmTask<object> find_target_turf(datum_ai_controller controller = null, atom target = null, datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = null)
    {
        return get_ranged_target_turf_direct(controller.pawn as atom, target, range: ability.max_range, offset: 180f);
    }
}

/// Make a rift to chase your target
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:68
public partial class datum_bt_node_ai_behavior_make_carp_rift_towards : datum_bt_node_ai_behavior_make_carp_rift
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:76
    public override async DmTask<object> validate_target(datum_ai_controller controller = null, atom target = null, datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = null)
    {
        object dot = await base.validate_target(controller, target, ability);
        if (!Dm.truthy(dot))
            return FALSE;

        if (Dm.truthy(teleport_if_far))
        {
            float distance = Dm.num(get_dist(get_turf(controller.pawn), get_turf(target)));
            if (distance >= ability.max_range + teleport_buffer_distance) // Perform if we are far away
                return TRUE;
        }

        if (Dm.truthy(teleport_if_blocked))
        {
            turf next_move = get_step_towards(controller.pawn, target);
            if (Dm.truthy(await next_move.is_blocked_turf(exclude_mobs: TRUE))) // Perform if target is behind cover
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:92
    public override async DmTask<object> find_target_turf(datum_ai_controller controller = null, atom target = null, datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = null)
    {
        turf target_turf = get_turf(target) as turf;
        float distance = Dm.num(get_dist(get_turf(controller.pawn), target_turf));
        turf chosen_turf;
        atom pawn = controller.pawn as atom;
        if (distance <= ability.max_range)
            chosen_turf = target_turf;
        else
        {
            object run_direction = get_dir(controller.pawn, get_step_towards(controller.pawn, target_turf));
            chosen_turf = get_ranged_target_turf(pawn, run_direction, ability.max_range) as turf;
        }

        if (chosen_turf == null)
            return null;

        float rift_to_target_distance = Dm.num(get_dist(target_turf, chosen_turf));
        if (rift_to_target_distance < teleport_buffer_distance)
        {
            object away_direction = get_dir(controller.pawn, get_step_away(controller.pawn, target_turf));
            turf backed_away_turf = get_ranged_target_turf(pawn, away_direction, teleport_buffer_distance - rift_to_target_distance) as turf;
            if (distance > Dm.num(get_dist(backed_away_turf, target_turf)))
                chosen_turf = backed_away_turf; // Avoid edge case pointless teleports from being up against a wall
        }
        return chosen_turf;
    }
}

/// Teleport towards something even if we're not far away or blocked
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:116
public partial class datum_bt_node_ai_behavior_make_carp_rift_towards_unvalidated : datum_bt_node_ai_behavior_make_carp_rift_towards
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:118
    public override async DmTask<object> validate_target(datum_ai_controller controller = null, atom target = null, datum_action_cooldown_mob_cooldown_lesser_carp_rift ability = null)
    {
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:121
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
            await controller.cancel_current_plan();
    }
}

/// Find a rift to go through if it is closer to our target than walking
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:127
public partial class datum_bt_node_ai_behavior_find_carp_rift_shortcut : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:137
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target) || (Dm.truthy(controller.blackboard[BB_CARPS_FEAR_FISHERMAN]) && HAS_TRAIT(target, TRAIT_SCARY_FISHERMAN)))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        float distance_to_target = Dm.num(get_dist(controller.pawn, target));
        if (distance_to_target <= minimum_distance)
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        foreach (obj_effect_temp_visual_lesser_carp_rift_entrance rift in Dm.each<obj_effect_temp_visual_lesser_carp_rift_entrance>(orange(controller.pawn, search_distance)))
        {
            float exit_count = Dm.length(rift.exit_locs);
            if (exit_count == 0)
                continue;
            turf rift_exit = rift.exit_locs[(int)exit_count] as turf;
            if ((Dm.num(get_dist(rift_exit, target)) + Dm.num(get_dist(rift, target))) >= distance_to_target)
                continue;
            controller.set_blackboard_key(destination_key, get_turf(rift));
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
        }

        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
    }
}

/// Find a rift nearby to enter, however we can
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:157
public partial class datum_bt_node_ai_behavior_find_carp_rift_shortcut_nearby : datum_bt_node_ai_behavior_find_carp_rift_shortcut
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_rift_actions.dm:162
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        foreach (obj_effect_temp_visual_lesser_carp_rift_entrance rift in Dm.each<obj_effect_temp_visual_lesser_carp_rift_entrance>(orange(controller.pawn, search_distance)))
        {
            controller.set_blackboard_key(destination_key, get_turf(rift));
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
        }
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
    }
}
