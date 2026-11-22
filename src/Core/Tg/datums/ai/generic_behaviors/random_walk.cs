// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/random_walk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// BT-native idle wander leaves.
// tg: code/datums/ai/generic_behaviors/random_walk.dm:2
public partial class datum_bt_node_ai_behavior_idle_random_walk : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/random_walk.dm:6
    public virtual object can_move(mob_living living_pawn = null)
    {
        return Dm.num(living_pawn != null && Dm.truthy(Dm.bitand(living_pawn.mobility_flags, MOBILITY_MOVE)) && isturf(living_pawn.loc) && !Dm.truthy(living_pawn.pulledby));
    }

    // tg: code/datums/ai/generic_behaviors/random_walk.dm:9
    public virtual async DmTask<object> try_random_step(mob_living living_pawn = null, object seconds_per_tick = null, object step_walk_chance = null)
    {
        if (Dm.truthy(LAZYLEN(living_pawn.do_afters)))
            return FALSE;

        if (!SPT_PROB(Dm.num(step_walk_chance), seconds_per_tick) || !Dm.truthy(can_move(living_pawn)))
            return FALSE;

        float move_dir = Dm.num(pick(GLOB.alldirs));
        turf destination_turf = get_step(living_pawn, move_dir);
        if (!Dm.truthy(destination_turf?.can_cross_safely(living_pawn)))
            return FALSE;

        await living_pawn.Move(destination_turf, move_dir);
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/random_walk.dm:24
    public virtual async DmTask<object> try_near_target_step(mob_living living_pawn = null, atom target = null, object seconds_per_tick = null, object step_walk_chance = null, object minimum_distance = null)
    {
        if (target == null || !Dm.truthy(can_move(living_pawn)) || Dm.truthy(LAZYLEN(living_pawn.do_afters)))
            return FALSE;

        if (!SPT_PROB(Dm.num(step_walk_chance), seconds_per_tick))
            return FALSE;

        float distance = get_dist(target, living_pawn);
        if (distance > Dm.num(minimum_distance))
            return await try_random_step(living_pawn, seconds_per_tick, step_walk_chance);

        DmList possible_turfs = Dm.list();
        foreach (object direction in Dm.each(GLOB.alldirs))
        {
            turf possible_step = get_step(living_pawn, Dm.num(direction));
            if (get_dist(possible_step, target) > Dm.num(minimum_distance))
                continue;
            if (Dm.truthy(await possible_step.is_blocked_turf()) || !Dm.truthy(possible_step.can_cross_safely(living_pawn)))
                continue;
            Dm.append(ref possible_turfs, possible_step);
        }

        if (!Dm.truthy(length(possible_turfs)))
            return FALSE;

        turf picked_turf = pick(possible_turfs) as turf;
        await living_pawn.Move(picked_turf, get_dir(living_pawn, picked_turf));
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/random_walk.dm:51
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        Waiting.NoSleep(try_random_step(living_pawn, seconds_per_tick, walk_chance));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

// Only walk if we don't have a target.
// tg: code/datums/ai/generic_behaviors/random_walk.dm:58
public partial class datum_bt_node_ai_behavior_idle_random_walk_no_target : datum_bt_node_ai_behavior_idle_random_walk
{
    // tg: code/datums/ai/generic_behaviors/random_walk.dm:62
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(controller.blackboard_key_exists(target_key)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return base.perform(seconds_per_tick, controller);
    }
}

// Only walk if we are not on the target's location.
// tg: code/datums/ai/generic_behaviors/random_walk.dm:68
public partial class datum_bt_node_ai_behavior_idle_random_walk_not_while_on_target : datum_bt_node_ai_behavior_idle_random_walk
{
    // tg: code/datums/ai/generic_behaviors/random_walk.dm:72
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (!QDELETED(target) && ((isturf(target) && controller.pawn.loc == target) || ((target as atom_movable)?.loc == controller.pawn.loc)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return base.perform(seconds_per_tick, controller);
    }
}

// Walk more when healthy and on rust (explore for more), or damaged and off rust (seek healing rust).
// tg: code/datums/ai/generic_behaviors/random_walk.dm:79
public partial class datum_bt_node_ai_behavior_idle_random_walk_rust : datum_bt_node_ai_behavior_idle_random_walk
{
    // tg: code/datums/ai/generic_behaviors/random_walk.dm:81
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        turf my_turf = get_turf(living_pawn);
        bool on_rust = HAS_TRAIT(my_turf, TRAIT_RUSTY);
        bool damaged = living_pawn.health < living_pawn.maxHealth;
        Waiting.NoSleep(try_random_step(living_pawn, seconds_per_tick, on_rust == damaged ? 10 : 50));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

// Walk randomly, but stay near a target when possible.
// tg: code/datums/ai/generic_behaviors/random_walk.dm:90
public partial class datum_bt_node_ai_behavior_idle_random_walk_walk_near_target : datum_bt_node_ai_behavior_idle_random_walk
{
    // tg: code/datums/ai/generic_behaviors/random_walk.dm:96
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            target = null;

        if (!Dm.truthy(can_move(living_pawn)) || Dm.truthy(LAZYLEN(living_pawn.do_afters)) || !SPT_PROB(walk_chance, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        if (target == null || get_dist(target, living_pawn) > minimum_distance)
        {
            Waiting.NoSleep(try_random_step(living_pawn, seconds_per_tick, walk_chance));
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
        }

        if (!Dm.truthy(Waiting.NoSleep(try_near_target_step(living_pawn, target, seconds_per_tick, walk_chance, minimum_distance))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
