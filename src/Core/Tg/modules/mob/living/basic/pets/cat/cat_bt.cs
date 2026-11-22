// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/cat/cat_bt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// Behavior tree behaviors, subtrees and decorators for the cat AI family.

/// Passes when the cat is currently carrying a piece of huntable food.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:6
public partial class datum_bt_node_decorator_cat_holding_food : datum_bt_node_decorator
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return controller.pawn is mob_living_basic_pet_cat cat && cat.held_food != null ? TRUE : FALSE;
    }
}

/// Pounces on a mouse, occasionally killing it. Movement is handled externally.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:12
public partial class datum_bt_node_ai_behavior_play_with_mouse : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:18
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        return !QDELETED(target) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:22
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_mouse target = controller.blackboard[target_key] as mob_living_basic_mouse;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        float chance = target is mob_living_basic_mouse_brown_tom ? 5 : consume_chance;
        if (prob(chance))
        {
            target.splat();
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:32
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        mob_living living_pawn = controller.pawn as mob_living;
        atom target = controller.blackboard[target_key] as atom;
        controller.clear_blackboard_key(target_key);
        if (target == null || QDELETED(living_pawn))
            return;
        string manual_emote = Dm.text("attempts to hunt []...", target);
        manual_emote += Dm.truthy(succeeded) ? "and succeeds!" : "but fails!";
        living_pawn.manual_emote(manual_emote);
    }
}

/// Drops a piece of carried food at a kitten's feet.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:45
public partial class datum_bt_node_ai_behavior_deliver_food_to_kitten : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:51
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        return !QDELETED(target) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:55
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living living_pawn = controller.pawn as mob_living;
        atom_movable food = controller.blackboard[food_key] as atom_movable;
        if (food == null || !Dm.@in(food, living_pawn.contents))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.NoSleep(food.forceMove(get_turf(living_pawn)));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:66
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
        controller.clear_blackboard_key(food_key);
    }
}

/// Engages a rival tom in a territorial yowling contest until one of them backs down
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:73
public partial class datum_bt_node_ai_behavior_territorial_struggle : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:81
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target))
            return FALSE;
        // Only fight if our rival is still locked onto us.
        if ((target.ai_controller as datum_ai_controller)?.blackboard[target_key] != living_pawn)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:91
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;

        mob_living living_pawn = controller.pawn as mob_living;
        DmList threaten_list = controller.blackboard[cries_key] as DmList;
        if (Dm.truthy(length(threaten_list)))
            Waiting.Detach(living_pawn.say(pick(threaten_list), forced: "ai_controller"));

        if (!prob(end_battle_chance))
            return AI_BEHAVIOR_DELAY;

        // 50/50 chance we lose.
        if (prob(50))
            controller.set_blackboard_key(BB_BASIC_MOB_FLEE_TARGET, target);
        else
            (target.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_MOB_FLEE_TARGET, living_pawn);
        (target.ai_controller as datum_ai_controller)?.clear_blackboard_key(target_key);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:112
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

/// Finds a rival male cat to fight, marking ourselves as their target too so the fight is mutual.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:118
public partial class datum_bt_node_ai_behavior_find_cat_tresspasser : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:124
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        return Dm.eq(living_pawn.gender, MALE) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:128
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList ignore_types = controller.blackboard[BB_BABIES_CHILD_TYPES] as DmList;
        foreach (mob_living_basic_pet_cat potential_enemy in Dm.each<mob_living_basic_pet_cat>(oview(search_range, living_pawn)))
        {
            if (!Dm.eq(potential_enemy.gender, MALE))
                continue;
            if (Dm.truthy(is_type_in_list(potential_enemy, ignore_types)))
                continue;
            datum_ai_controller enemy_controller = potential_enemy.ai_controller as datum_ai_controller;
            if (enemy_controller == null)
                continue;
            // They're already engaged in a battle, leave them alone!
            if (Dm.truthy(enemy_controller.blackboard_key_exists(BB_TRESSPASSER_TARGET)))
                continue;
            // You choose me and I choose you.
            enemy_controller.set_blackboard_key(BB_TRESSPASSER_TARGET, living_pawn);
            controller.set_blackboard_key(target_key, potential_enemy);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Points at a target and meows for food. Used to beg humans and call kittens to dinner.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:150
public partial class datum_bt_node_ai_behavior_beacon_for_food : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:156
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        return !QDELETED(target) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:160
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living living_pawn = controller.pawn as mob_living;
        DmList meowing_list = controller.blackboard[meows_key] as DmList;
        if (Dm.truthy(length(meowing_list)))
            Waiting.Detach(living_pawn.say(pick(meowing_list), forced: "ai_controller"));
        Waiting.Detach(living_pawn._pointed(target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:171
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

/// Finds a conscious human carrying food worth begging for.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:178
public partial class datum_bt_node_ai_behavior_find_human_to_beg : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:182
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        object locate_items = controller.blackboard[BB_HUNTABLE_PREY];
        foreach (mob_living_carbon_human human_target in Dm.each<mob_living_carbon_human>(oview(search_range, living_pawn)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(human_target) || human_target.mind == null)
                continue;
            foreach (obj_item held_item in Dm.each<obj_item>(human_target.held_items))
            {
                if (Dm.truthy(is_type_in_typecache(held_item, locate_items)))
                {
                    controller.set_blackboard_key(target_key, human_target);
                    return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
                }
            }
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

/// Leaves the cat house we are currently residing in, occasionally.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:197
public partial class datum_bt_node_ai_behavior_leave_cat_home : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:199
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if ((controller.pawn as atom)?.loc is not obj_structure_cat_house home)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (!prob(leave_home_chance))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(home, FALSE));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Celebrates around a decorated donut with a spin.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:210
public partial class datum_bt_node_ai_behavior_hunt_target_decorate_donuts : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:212
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.spin(spintime: 4, speed: 1);
        return null;
    }
}

/// Enters (or exits if already resident) a cat house keyed in target_key.
// tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:217
public partial class datum_bt_node_ai_behavior_enter_cat_home : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:219
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        obj_structure_cat_house home = controller.blackboard[target_key] as obj_structure_cat_house;
        return !QDELETED(home) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:223
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_structure_cat_house home = controller.blackboard[target_key] as obj_structure_cat_house;
        if (QDELETED(home))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living_basic_pet_cat living_pawn = controller.pawn as mob_living_basic_pet_cat;
        if (living_pawn == home.resident_cat || home.resident_cat == null)
        {
            Waiting.Detach(controller.ai_interact(home, FALSE));
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }

    // tg: code/modules/mob/living/basic/pets/cat/cat_bt.dm:233
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
