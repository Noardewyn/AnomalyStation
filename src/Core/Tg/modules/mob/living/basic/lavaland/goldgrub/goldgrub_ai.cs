// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The controllers and the eat subtree are var blocks (generated); their trees are tg's compiled .bt.json (BehaviorTrees/).

// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:25
public partial class datum_pet_command_grub_spit : datum_pet_command
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:32
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        datum_action_cooldown spit_ability = controller.blackboard[BB_SPIT_ABILITY] as datum_action_cooldown;
        if (spit_ability == null || !Dm.truthy(await spit_ability.IsAvailable()))
            return null;
        controller.set_blackboard_key(BB_PET_ACTIVE_ABILITY, spit_ability);
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_untargeted_ability));
        controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:40
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return I18n.T("signals [] to spit its ores!", living_pet);
    }
}

/**
 * Burrows underground (or stays underground) when danger is present or a storm is approaching.
 * Returns RUNNING while underground+storm (blocks everything).
 * Returns FAILURE when no action needed so the selector passes through.
 */
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:52
public partial class datum_bt_node_ai_behavior_dig_away_from_danger : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:54
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        bool currently_underground = is_jaunting(controller.pawn as atom);
        object storm_approaching = controller.blackboard[BB_STORM_APPROACHING];
        datum_action_cooldown dig_ability = controller.blackboard[BB_BURROW_ABILITY] as datum_action_cooldown;

        if (currently_underground && Dm.truthy(storm_approaching))
            return AI_BEHAVIOR_DELAY; // Stay underground while storm approaches  RUNNING blocks everything

        if (dig_ability == null || !Dm.truthy(Waiting.NoSleep(dig_ability.IsAvailable())))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        bool has_target = Dm.truthy(controller.blackboard_key_exists(BB_CURRENT_TARGET));

        if (currently_underground && !has_target)
        {
            // No target/danger while underground  emerge
            Waiting.Detach(dig_ability.Trigger()); // INVOKE_ASYNC
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }

        if (Dm.truthy(storm_approaching) || has_target)
        {
            // Go underground to escape
            Waiting.Detach(dig_ability.Trigger()); // INVOKE_ASYNC
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }

        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/**
 * While jaunting (underground) with a target, moves the grub's phased form further away.
 * Returns FAILURE when not jaunting so the selector passes through.
 */
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:83
public partial class datum_bt_node_ai_behavior_burrow_through_ground : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:44  #define BURROW_RANGE 5 (file-local, #undef at :214)
    private const float BURROW_RANGE = 5;

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:87
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        atom target = controller.blackboard[BB_CURRENT_TARGET] as atom;
        if (!is_jaunting(living_pawn) || QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        atom_movable phased = living_pawn.loc as atom_movable;
        DmList turfs_list = RANGE_TURFS(BURROW_RANGE, phased);
        float current_max_distance = 0;
        turf selected_turf = null;

        foreach (turf possible_turf in Dm.each<turf>(turfs_list))
        {
            if (!ismineralturf(possible_turf) && !isasteroidturf(possible_turf))
                continue;
            float distance_to_target = get_dist(possible_turf, target);
            if (distance_to_target > current_max_distance)
            {
                current_max_distance = distance_to_target;
                selected_turf = possible_turf;
            }
            if (distance_to_target == BURROW_RANGE)
                break;
        }

        if (selected_turf == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        Waiting.NoSleep(phased.forceMove(selected_turf));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Finds an ore pile. Sets BB_ORE_TARGET. Skips forbidden types and fetch targets.
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:116
public partial class datum_bt_node_ai_behavior_find_ore : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:120
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living grub_pawn = controller.pawn as mob_living;
        DmList forbidden = controller.blackboard[BB_ORE_IGNORE_TYPES] as DmList;
        object pet_target = controller.blackboard[BB_CURRENT_PET_TARGET];
        foreach (obj_item_stack_ore candidate in Dm.each<obj_item_stack_ore>(oview(range, grub_pawn)))
        {
            if (Dm.truthy(is_type_in_list(candidate, forbidden)) || !isturf(candidate.loc))
                continue;
            if (Dm.eq(candidate, pet_target))
                continue;
            if (!Dm.truthy(can_see(grub_pawn, candidate, range)))
                continue;
            controller.set_blackboard_key(BB_ORE_TARGET, candidate);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Finds a boulder the goldgrub can break. Sets BB_BOULDER_TARGET.
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:134
public partial class datum_bt_node_ai_behavior_find_boulder : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:139
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living grub_pawn = controller.pawn as mob_living;
        object pet_target = controller.blackboard[BB_CURRENT_PET_TARGET];
        foreach (obj_item_boulder candidate in Dm.each<obj_item_boulder>(oview(9, grub_pawn)))
        {
            if (Dm.eq(candidate, pet_target))
                continue;
            if (!Dm.truthy(can_see(grub_pawn, candidate, 9)))
                continue;
            controller.set_blackboard_key(BB_BOULDER_TARGET, candidate);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Finds an ore vent to harvest. Sets BB_VENT_TARGET.
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:150
public partial class datum_bt_node_ai_behavior_find_ore_vent : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:155
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(25, seconds_per_tick))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living grub_pawn = controller.pawn as mob_living;
        foreach (obj_structure_ore_vent candidate in Dm.each<obj_structure_ore_vent>(oview(15, grub_pawn)))
        {
            if (Dm.@in(candidate, grub_pawn))
                continue;
            turf vent_turf = candidate.drop_location() as turf;
            float counter = 0;
            bool too_many = false;
            foreach (obj_item boulder in Dm.each<obj_item>(vent_turf.contents))
            {
                counter++;
                if (counter > MAX_BOULDERS_PER_VENT)
                {
                    too_many = true;
                    break;
                }
            }
            if (too_many || !Dm.truthy(can_see(grub_pawn, candidate, 15)))
                continue;
            controller.set_blackboard_key(BB_VENT_TARGET, candidate);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Finds a grub egg to protect/pull. Sets BB_LOW_PRIORITY_HUNTING_TARGET.
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:175
public partial class datum_bt_node_ai_behavior_find_grub_egg : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:180
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(75, seconds_per_tick))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living grub_pawn = controller.pawn as mob_living;
        if (grub_pawn.pulling != null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        foreach (obj_item_food_egg_green_grub_egg egg in Dm.each<obj_item_food_egg_green_grub_egg>(oview(9, grub_pawn)))
        {
            if (!Dm.truthy(can_see(grub_pawn, egg, 9)))
                continue;
            controller.set_blackboard_key(BB_LOW_PRIORITY_HUNTING_TARGET, egg);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Eats/interacts with a target at the given blackboard key. Must be adjacent.
// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:192
public partial class datum_bt_node_ai_behavior_grub_eat : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:198
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(Waiting.NoSleep((controller.pawn as atom).Adjacent(target))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(target, FALSE)); // INVOKE_ASYNC
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_ai.dm:207
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
