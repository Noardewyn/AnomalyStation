// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Perform a melee attack on the target specified.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:5
public partial class datum_bt_node_ai_behavior_basic_melee_attack : datum_bt_node_ai_behavior
{
    /// Amount of time to wait before executing attack if not specified
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:2
    private const float DEFAULT_ATTACK_DELAY = 0.4f * SECONDS;

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:10
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        object dot = await base.setup(controller);

        if (!ispath(targeting_strategy))
            targeting_strategy = controller.blackboard[targeting_strategy];

        if (!Dm.truthy(targeting_strategy))
            throw Dm.CRASH(Dm.text("No targeting strategy was supplied in the blackboard for []", controller.pawn), "code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm", 18);
        atom target = (Dm.truthy(controller.blackboard[hiding_location_key]) ? controller.blackboard[hiding_location_key] : controller.blackboard[target_key]) as atom;
        if (QDELETED(target))
            return FALSE;
        return dot;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:23
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (target == null)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(Waiting.NoSleep(target.IsReachableBy(controller.pawn as atom_movable))))
        {
            controller.clear_blackboard_key(BB_BASIC_MOB_MELEE_COOLDOWN_TIMER);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }

        object can_attack_time = controller.blackboard[BB_BASIC_MOB_MELEE_COOLDOWN_TIMER];
        if (can_attack_time == null)
        {
            object blackboard_delay = controller.blackboard[BB_BASIC_MOB_MELEE_DELAY];
            float attack_delay = blackboard_delay == null ? DEFAULT_ATTACK_DELAY : Dm.num(blackboard_delay);
            controller.set_blackboard_key(BB_BASIC_MOB_MELEE_COOLDOWN_TIMER, world.time + attack_delay);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }
        if (Dm.num(can_attack_time) > world.time)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        if (controller.pawn is mob_living pawn && world.time < Dm.num(pawn.next_move))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(targeting_strategy);
        if (!Dm.truthy(Waiting.NoSleep(strategy.is_valid_target(controller.pawn as mob_living, target, controller: controller))))
        {
            controller.clear_blackboard_key(target_key);
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }

        object hiding_target = strategy.find_hidden_mobs(controller.pawn as mob_living, target); //If this is valid, theyre hidden in something!

        controller.set_blackboard_key(hiding_location_key, hiding_target);

        atom final_target = (Dm.truthy(hiding_target) ? hiding_target : target) as atom;
        Waiting.Detach(controller.ai_interact(final_target, TRUE));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Single-hit variant: terminates after one successful attack and always clears the target key.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:59
public partial class datum_bt_node_ai_behavior_basic_melee_attack_interact_once : datum_bt_node_ai_behavior_basic_melee_attack
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:61
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

//Basic ranged attack behavior
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:66
public partial class datum_bt_node_ai_behavior_basic_ranged_attack : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:76
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        await base.setup(controller);
        if (HAS_TRAIT(controller.pawn, TRAIT_HANDS_BLOCKED))
            return FALSE;
        atom target = (Dm.truthy(controller.blackboard[hiding_location_key]) ? controller.blackboard[hiding_location_key] : controller.blackboard[target_key]) as atom;
        if (QDELETED(target))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:85
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic basic_mob = controller.pawn as mob_living_basic;
        atom target = controller.blackboard[target_key] as atom;

        if (!ispath(targeting_strategy))
            targeting_strategy = controller.blackboard[targeting_strategy];

        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(targeting_strategy);

        atom hiding_target = strategy.find_hidden_mobs(basic_mob, target) as atom;
        atom final_target = hiding_target != null ? hiding_target : target;
        controller.set_blackboard_key(hiding_location_key, hiding_target);

        if (!Dm.truthy(can_see(basic_mob, final_target, max_range)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        if (Dm.truthy(avoid_friendly_fire) && Dm.truthy(Waiting.NoSleep(check_friendly_in_path(basic_mob, target, strategy))))
        {
            Waiting.NoSleep(adjust_position(basic_mob, target));
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        }

        Waiting.NoSleep(basic_mob.RangedAttack(final_target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:108
    public virtual async DmTask<object> check_friendly_in_path(mob_living source = null, atom target = null, datum_targeting_strategy targeting_strategy = null)
    {
        DmList turfs_list = calculate_trajectory(source, target) as DmList;
        foreach (turf possible_turf in Dm.each_cast<turf>(turfs_list))
        {
            foreach (mob_living potential_friend in Dm.each<mob_living>(possible_turf))
            {
                if (!Dm.truthy(await targeting_strategy.is_valid_target(source, potential_friend)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:116
    public virtual async DmTask adjust_position(mob_living living_pawn = null, atom target = null)
    {
        turf our_turf = get_turf(living_pawn);
        DmList possible_turfs = Dm.list();
        foreach (object direction in Dm.each(GLOB.alldirs))
        {
            turf target_turf = get_step(our_turf, Dm.num(direction));
            if (target_turf == null)
                continue;
            if (Dm.truthy(await target_turf.is_blocked_turf()) || get_dist(target_turf, target) > get_dist(living_pawn, target))
                continue;
            Dm.append(ref possible_turfs, target_turf);
        }
        if (!Dm.truthy(length(possible_turfs)))
            return;
        turf picked_turf = get_closest_atom(typeof(turf), possible_turfs, target) as turf;
        await step(living_pawn, get_dir(living_pawn, picked_turf));
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/basic_attacking.dm:131
    public virtual object calculate_trajectory(mob_living source = null, atom target = null)
    {
        DmList turf_list = get_line(source, target) as DmList;
        float list_length = length(turf_list) - 1;
        for (float i = 1; i <= list_length; i++)
        {
            turf current_turf = turf_list[i] as turf;
            turf next_turf = turf_list[i + 1] as turf;
            float direction_to_turf = get_dir(current_turf, next_turf);
            if (!Dm.truthy(ISDIAGONALDIR(direction_to_turf)))
                continue;
            foreach (object cardinal_direction in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(cardinal_direction, direction_to_turf)))
                    Dm.append(ref turf_list, get_step(current_turf, Dm.num(cardinal_direction)));
            }
        }
        Dm.remove(ref turf_list, get_turf(source));
        Dm.remove(ref turf_list, get_turf(target));
        return turf_list;
    }
}
