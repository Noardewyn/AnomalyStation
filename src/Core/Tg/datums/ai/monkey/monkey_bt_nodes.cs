// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/monkey/monkey_bt_nodes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5); the var overrides and the subtrees' json paths are generated.

/// Base equip behavior; handles blacklist updates and key cleanup on finish
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:10
public partial class datum_bt_node_ai_behavior_monkey_equip : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:13
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (!Dm.truthy(succeeded)) // Don't try to pick this item up again
            controller.set_blackboard_key_assoc(BB_MONKEY_BLACKLISTITEMS, controller.blackboard[target_key], TRUE);
        controller.clear_blackboard_key(BB_MONKEY_PICKUPTARGET);
        controller.clear_blackboard_key(BB_MONKEY_PICKUP_IS_PICKPOCKET);
    }

    /// Shared item equip proc
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:88
    public virtual async DmTask<object> equip_item(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        obj_item target = controller.blackboard[BB_MONKEY_PICKUPTARGET] as obj_item;
        object best_force = controller.blackboard[BB_MONKEY_BEST_FORCE_FOUND];

        if (!isturf(living_pawn.loc))
            return FALSE;
        if (target == null)
            return FALSE;
        if (Dm.truthy(target.anchored))
            return FALSE;

        if (Dm.num(target.force) > Dm.num(best_force)) // better weapon
        {
            await living_pawn.drop_all_held_items();
            await living_pawn.put_in_hands(target);
            controller.set_blackboard_key(BB_MONKEY_BEST_FORCE_FOUND, target.force);
            return TRUE;
        }

        if (Dm.truthy(target.slot_flags)) // wearable
        {
            await living_pawn.dropItemToGround(target, TRUE);
            await living_pawn.update_icons();
            if (!Dm.truthy(await living_pawn.equip_to_appropriate_slot(target)))
                return FALSE;
            return TRUE;
        }

        if (Dm.truthy(living_pawn.get_empty_held_indexes())) // any free hand
        {
            await living_pawn.put_in_hands(target);
            return TRUE;
        }

        return FALSE;
    }
}

/// Equip a weapon off the ground
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:22
public partial class datum_bt_node_ai_behavior_monkey_equip_ground : datum_bt_node_ai_behavior_monkey_equip
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:24
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[target_key] as obj_item;
        return Dm.num(!QDELETED(target));
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:28
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        return start_async();
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:35
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        object result = await equip_item(controller);
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }
}

/// Pickpocket a weapon from a mob
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:42
public partial class datum_bt_node_ai_behavior_monkey_equip_pickpocket : datum_bt_node_ai_behavior_monkey_equip
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:44
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[target_key] as obj_item;
        return Dm.num(!QDELETED(target));
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:48
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(controller.blackboard[BB_MONKEY_PICKPOCKETING])) // mid-snatch; wait
            return AI_BEHAVIOR_DELAY;
        Waiting.Detach(attempt_pickpocket(controller));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:54
    public virtual async DmTask attempt_pickpocket(datum_ai_controller controller = null)
    {
        obj_item target = controller.blackboard[BB_MONKEY_PICKUPTARGET] as obj_item;
        mob_living victim = target?.loc as mob_living;
        mob_living living_pawn = controller.pawn as mob_living;

        if (victim == null)
        {
            await finish_action(controller, FALSE);
            return;
        }

        victim.visible_message(span_warning(I18n.T("[] starts trying to take [] from []!", living_pawn, target, victim)), span_danger(I18n.T("[] tries to take []!", living_pawn, target)));
        controller.set_blackboard_key(BB_MONKEY_PICKPOCKETING, TRUE);

        object success = FALSE;

        if (Dm.truthy(await do_after(living_pawn, MONKEY_ITEM_SNATCH_DELAY, victim)) && target != null && Dm.truthy(await victim.IsReachableBy(living_pawn)))
        {
            foreach (obj_item I in Dm.each<obj_item>(victim.held_items))
            {
                if (I == target)
                {
                    victim.visible_message(span_danger(I18n.T("[] snatches [] from [].", living_pawn, target, victim)), span_userdanger(I18n.T("[] snatched []!", living_pawn, target)));
                    if (Dm.truthy(await victim.temporarilyRemoveItemFromInventory(target)))
                    {
                        if (!QDELETED(target) && !Dm.truthy(await equip_item(controller)))
                        {
                            await target.forceMove(living_pawn.drop_location() as atom);
                            success = TRUE;
                            break;
                        }
                    }
                    else
                        victim.visible_message(span_danger(I18n.T("[] tried to snatch [] from [], but failed!", living_pawn, target, victim)), span_userdanger(I18n.T("[] tried to grab []!", living_pawn, target)));
                }
            }
        }

        await finish_action(controller, success);
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:83
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.set_blackboard_key(BB_MONKEY_PICKPOCKETING, FALSE);
    }
}

/// Gathers weapon upgrade candidates: nearby ground items, then held items of nearby humans.
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:119
public partial class datum_target_source_monkey_weapon_upgrade : datum_target_source
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:121
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item ground_item in Dm.each<obj_item>(oview(range, pawn)))
            Dm.append(ref candidates, ground_item);
        foreach (mob_living_carbon_human nearby_human in Dm.each<mob_living_carbon_human>(oview(range, pawn)))
            Dm.append(ref candidates, nearby_human.held_items);
        return candidates;
    }
}

/// Weapon upgrade candidate: not two-handed, not blacklisted, and hits harder than our bite.
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:130
public partial class datum_targeting_strategy_monkey_weapon_upgrade : datum_targeting_strategy
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:132
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        obj_item candidate = target as obj_item;
        if (HAS_TRAIT(candidate, TRAIT_NEEDS_TWO_HANDS) || Dm.truthy((controller.blackboard[BB_MONKEY_BLACKLISTITEMS] as DmList)[candidate]))
            return FALSE;
        if (Dm.num(candidate.force) < 2) // our bite already does ~2 damage
            return FALSE;
        return TRUE;
    }
}

/// Scans nearby items and mobs for a better weapon and sets BB_MONKEY_PICKUPTARGET
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:143
public partial class datum_bt_node_ai_behavior_acquire_target_update_interaction_target_monkey_find_weapon : datum_bt_node_ai_behavior_acquire_target_update_interaction_target
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:145
    public override object can_search(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (Dm.locate<obj_item>(living_pawn.held_items) == null)
            controller.set_blackboard_key(BB_MONKEY_BEST_FORCE_FOUND, 0);
        if (Dm.truthy(controller.blackboard[BB_MONKEY_GUN_NEURONS_ACTIVATED]) && Dm.locate<obj_item_gun>(living_pawn.held_items) != null)
            return FALSE; // already have a gun
        return base.can_search(controller);
    }

    /// Prefers any gun once gun neurons are activated, else the strongest candidate that beats our current best held item.
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:154
    public override object pick_final_target(datum_ai_controller controller = null, DmList filtered_targets = null, datum_target_priority_strategy priority_strategy = null, atom current_target = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;

        if (Dm.truthy(controller.blackboard[BB_MONKEY_GUN_NEURONS_ACTIVATED]))
        {
            foreach (obj_item candidate in Dm.each_cast<obj_item>(filtered_targets))
            {
                if (isgun(candidate))
                    return candidate;
            }
        }

        float top_force = 0;
        foreach (obj_item held in Dm.each<obj_item>(living_pawn.held_items))
        {
            if (HAS_TRAIT(held, TRAIT_NEEDS_TWO_HANDS) || Dm.truthy((controller.blackboard[BB_MONKEY_BLACKLISTITEMS] as DmList)[held]))
                continue;
            top_force = Math.Max(top_force, Dm.num(held.force));
        }

        obj_item best = null;
        foreach (obj_item candidate in Dm.each_cast<obj_item>(filtered_targets))
        {
            if (Dm.num(candidate.force) <= top_force)
                continue;
            best = candidate;
            top_force = Dm.num(candidate.force);
        }
        return best;
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:176
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        controller.set_blackboard_key(BB_MONKEY_PICKUP_IS_PICKPOCKET, ismob(target.loc) ? TRUE : null);
    }
}

/// Selects a target from BB_MONKEY_ENEMIES or picks any visible mob if aggressive.  This should be ported to new targetting but its so fkn bespoke
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:181
public partial class datum_bt_node_ai_behavior_monkey_set_combat_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:185
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList enemies = controller.blackboard[enemies_key] as DmList;

        if (HAS_TRAIT(living_pawn, TRAIT_PACIFISM) || (!Dm.truthy(length(enemies)) && !Dm.truthy(controller.blackboard[BB_MONKEY_AGGRESSIVE])))
        {
            living_pawn.set_combat_mode(FALSE);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }

        DmList valids = Dm.list();
        foreach (mob_living possible_enemy in Dm.each<mob_living>(view(MONKEY_ENEMY_VISION, living_pawn)))
        {
            if (possible_enemy == living_pawn)
                continue;
            if (!Dm.truthy(enemies[possible_enemy]))
            {
                if (!Dm.truthy(controller.blackboard[BB_MONKEY_AGGRESSIVE]))
                    continue;
                if (Dm.truthy(possible_enemy.has_faction(Dm.list(FACTION_MONKEY, FACTION_JUNGLE))) && !Dm.truthy(controller.blackboard[BB_MONKEY_TARGET_MONKEYS]))
                    continue;
                if (Dm.truthy(possible_enemy.incapacitated)) // Dont bother, theyre fucked.
                    continue;
            }
            float dist = get_dist(living_pawn, possible_enemy);
            valids[possible_enemy] = CEILING(100 / (dist != 0 ? dist : 1), 1);
        }

        if (!Dm.truthy(length(valids)))
        {
            living_pawn.set_combat_mode(FALSE);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }

        mob_living target = pick_weight(valids) as mob_living;

        living_pawn.set_combat_mode(TRUE);
        controller.set_blackboard_key(attack_target_key, target);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Attacks the target mob; SUCCEEDED when target is gone, FAILED when target goes down (Which lets us flush the fucker instead)
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:221
public partial class datum_bt_node_ai_behavior_monkey_attack_mob : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:228
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        mob_living target = controller.blackboard[target_key] as mob_living;
        mob_living living_pawn = controller.pawn as mob_living;
        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(controller.blackboard[BB_TARGETING_STRATEGY]);

        if (QDELETED(target) || !Dm.truthy(Waiting.NoSleep(strategy.is_valid_target(living_pawn, target))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        obj_item holding_weapon = null;
        foreach (obj_item potential_weapon in Dm.each<obj_item>(target.held_items))
        {
            if (!Dm.truthy(Dm.bitand(potential_weapon.item_flags, ABSTRACT)))
            {
                holding_weapon = potential_weapon;
                break;
            }
        }

        attack_seconds_per_tick = Dm.num(seconds_per_tick);
        attack_holding_weapon = holding_weapon;
        return start_async();
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:250
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[target_key] as mob_living;
        object seconds_per_tick = attack_seconds_per_tick;
        obj_item holding_weapon = attack_holding_weapon;
        object attack_results = await monkey_attack(controller, target, seconds_per_tick, holding_weapon != null && SPT_PROB(MONKEY_ATTACK_DISARM_PROB, seconds_per_tick), holding_weapon);
        if (!Dm.truthy(async_still_valid()))
            return null;
        bool succeeded = false;
        if (Dm.truthy(attack_results) && !Dm.truthy(controller.blackboard[BB_MONKEY_AGGRESSIVE]))
        {
            succeeded = true;
            object hatred_value = (controller.blackboard[BB_MONKEY_ENEMIES] as DmList)[target];
            // A forced target may not have a grudge yet; give it one that can be satisfied.
            if (hatred_value == null)
            {
                hatred_value = 1;
                controller.set_blackboard_key_assoc(BB_MONKEY_ENEMIES, target, hatred_value);
            }
            if (prob(MONKEY_HATRED_REDUCTION_PROB))
            {
                hatred_value = Dm.num(hatred_value) - 1;
                if (Dm.num(hatred_value) <= 0)
                {
                    controller.remove_thing_from_blackboard_key(BB_MONKEY_ENEMIES, target);
                    // The combat tree loops until its target is cleared, even after a successful attack.
                    if (Dm.eq(controller.blackboard[target_key], target))
                        controller.clear_blackboard_key(target_key);
                }
                else
                    controller.set_blackboard_key_assoc(BB_MONKEY_ENEMIES, target, hatred_value);
            }
        }
        finish_async(succeeded ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }

    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:277
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        attack_seconds_per_tick = 0;
        attack_holding_weapon = null;
    }

    /// Attack with held weapon or bite; try to disarm if target is holding something
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:283
    public virtual async DmTask<object> monkey_attack(datum_ai_controller controller = null, mob_living target = null, object seconds_per_tick = null, bool disarm = false, object holding_weapon = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;

        if (Dm.num(living_pawn.next_move) > world.time)
            return FALSE;

        living_pawn.face_atom(target);

        obj_item potential_weapon = Dm.locate<obj_item>(living_pawn.held_items);

        if (Dm.truthy(await target.IsReachableBy(living_pawn, potential_weapon?.reach)))
        {
            if (potential_weapon == null)
            {
                await controller.ai_interact(target: target, modifiers: disarm ? Dm.list(Dm.kv(RIGHT_CLICK, TRUE)) : null, combat_mode: TRUE);
                if (disarm && holding_weapon != null && Dm.truthy((controller.blackboard[BB_MONKEY_BLACKLISTITEMS] as DmList)[holding_weapon]))
                    controller.remove_thing_from_blackboard_key(BB_MONKEY_BLACKLISTITEMS, holding_weapon);
            }
            else
            {
                if (potential_weapon != living_pawn.get_active_held_item())
                    living_pawn.swap_hand(living_pawn.get_inactive_hand_index());
                await controller.ai_interact(target: target, combat_mode: TRUE);
            }
            controller.override_blackboard_key(BB_MONKEY_GUN_WORKED, TRUE);
            return TRUE;
        }

        if (potential_weapon == null)
            return FALSE;

        // target out of melee reach — try ranged or throw
        atom real_target = target;
        if (prob(10)) // artificial miss
            real_target = pick(oview(2, target)) as atom;

        obj_item_gun gun = Dm.locate<obj_item_gun>(living_pawn.held_items);
        bool can_shoot = gun != null && Dm.truthy(gun.can_shoot());
        if (gun != null && Dm.truthy(controller.blackboard[BB_MONKEY_GUN_WORKED]) && prob(95))
        {
            if (gun != living_pawn.get_active_held_item())
                living_pawn.swap_hand(living_pawn.get_inactive_hand_index());
            await controller.ai_interact(target: real_target, combat_mode: TRUE);
            controller.override_blackboard_key(BB_MONKEY_GUN_WORKED, can_shoot ? TRUE : Dm.num(prob(80)));
            if (can_shoot)
                controller.override_blackboard_key(BB_MONKEY_GUN_NEURONS_ACTIVATED, TRUE);
        }
        else
        {
            await living_pawn.throw_item(real_target);
            controller.override_blackboard_key(BB_MONKEY_GUN_WORKED, TRUE);
        }
        return TRUE;
    }
}

/// Rallies nearby monkeys against the current attack target
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:327
public partial class datum_bt_node_ai_behavior_recruit_monkeys : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:330
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        mob_living attack_target = controller.blackboard[target_key] as mob_living;

        foreach (mob_living nearby_monkey in Dm.each<mob_living>(view(living_pawn, MONKEY_ENEMY_VISION)))
        {
            if (QDELETED(nearby_monkey) || nearby_monkey.ai_controller is not datum_ai_controller_monkey monkey_brain)
                continue;
            if (prob(MONKEY_RECRUIT_PROB))
                continue;
            monkey_brain.add_blackboard_key_assoc(BB_MONKEY_ENEMIES, attack_target, MONKEY_RECRUIT_HATED_AMOUNT);
        }

        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Scans nearby humans for patrons to serve; fails if bartender present or fewer than 1 patron found
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:345
public partial class datum_bt_node_ai_behavior_monkey_find_patrons : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:349
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList nearby_patrons = Dm.list();

        foreach (mob_living_carbon_human human_mob in Dm.each<mob_living_carbon_human>(oview(5, living_pawn)))
        {
            if (human_mob.mind?.assigned_role is datum_job_bartender)
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED; // my boss is on duty!
            if (IS_UNCONSCIOUS_OR_CRIT(human_mob) || HAS_TRAIT(human_mob, TRAIT_LESSER_HUMANOID))
                continue;
            if (!Dm.truthy(human_mob.get_empty_held_indexes()))
                continue;
            Dm.append(ref nearby_patrons, human_mob);
        }

        if (!Dm.truthy(length(nearby_patrons)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        controller.override_blackboard_key(patrons_key, nearby_patrons);
        if (!Dm.truthy(controller.blackboard[give_target_key]))
            controller.blackboard[give_target_key] = pick(nearby_patrons);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Gathers press targets: filtered to BB_MONKEY_PRESS_TYPEPATH's type if set, else any nearby obj.
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:372
public partial class datum_target_source_monkey_press_target : datum_target_source
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:374
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        object locate_path = controller.blackboard[BB_MONKEY_PRESS_TYPEPATH];
        DmList candidates = Dm.list();
        foreach (obj potential_candidate in Dm.each<obj>(oview(range, pawn)))
        {
            if (Dm.truthy(locate_path))
            {
                if (istype(potential_candidate, locate_path as Type))
                {
                    Dm.append(ref candidates, potential_candidate);
                    break; //found a bell fuck everything else
                }
            }
            Dm.append(ref candidates, potential_candidate);
        }
        return candidates;
    }
}

/// Idle wander/emote behavior. Reads emote lists from BB_MONKEY_IDLE_COMMON_EMOTES and BB_MONKEY_IDLE_RARE_EMOTES.
// tg: code/datums/ai/monkey/monkey_bt_nodes.dm:386
public partial class datum_bt_node_ai_behavior_monkey_idle : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/monkey/monkey_bt_nodes.dm:388
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (SPT_PROB(25, seconds_per_tick) && Dm.truthy(Dm.bitand(living_pawn.mobility_flags, MOBILITY_MOVE)) && isturf(living_pawn.loc) && living_pawn.pulledby == null)
        {
            object move_dir = pick(GLOB.alldirs);
            Waiting.NoSleep(living_pawn.Move(get_step(living_pawn, Dm.num(move_dir)), move_dir));
        }
        else if (SPT_PROB(5, seconds_per_tick))
            Waiting.Detach(living_pawn.emote(pick(controller.blackboard[BB_MONKEY_IDLE_COMMON_EMOTES] as DmList)));
        else if (SPT_PROB(1, seconds_per_tick))
            Waiting.Detach(living_pawn.emote(pick(controller.blackboard[BB_MONKEY_IDLE_RARE_EMOTES] as DmList)));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
