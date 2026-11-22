// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/attack_obstacles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Destroy shit int he way
// tg: code/datums/ai/generic_behaviors/attack_obstacles.dm:3
public partial class datum_bt_node_ai_behavior_attack_obstructions : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/attack_obstacles.dm:13
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic basic_mob = controller.pawn as mob_living_basic;
        atom target = controller.blackboard[target_key] as atom;

        if (QDELETED(target))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        turf next_step = get_step_towards(basic_mob, target);

        if (!Dm.truthy(Waiting.NoSleep(next_step.is_blocked_turf(exclude_mobs: TRUE, source_atom: controller.pawn))))
        {
            if (Dm.truthy(can_ignore_step))
                return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED; // Path clear  let selector fall through to melee
            if (get_step_to(controller.pawn, next_step) != null) //pathing could still fail because byond sucks.
                return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED; // Path clear  let selector fall through to melee
        }

        float dir_to_next_step = get_dir(basic_mob, next_step);
        DmList dirs_to_move = Dm.list();
        if (Dm.truthy(ISDIAGONALDIR(dir_to_next_step)))
        {
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(direction, dir_to_next_step)))
                    Dm.append(ref dirs_to_move, direction);
            }
        }
        else
            Dm.append(ref dirs_to_move, dir_to_next_step);

        foreach (object direction in Dm.each(dirs_to_move))
        {
            if (Dm.truthy(Waiting.NoSleep(attack_in_direction(controller, basic_mob, direction))))
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED; // Nothing smashable  let selector fall through
    }

    // tg: code/datums/ai/generic_behaviors/attack_obstacles.dm:42
    public virtual async DmTask<object> attack_in_direction(datum_ai_controller controller = null, mob_living_basic basic_mob = null, object direction = null)
    {
        turf next_step = get_step(basic_mob, Dm.num(direction));
        if (!Dm.truthy(await next_step.is_blocked_turf(exclude_mobs: TRUE, source_atom: controller.pawn)))
        {
            if (get_step_to(controller.pawn, next_step) != null) //pathing could still fail because byond sucks.
                return FALSE;
        }

        foreach (object item in Dm.each(next_step.contents))
        {
            if (item is not obj @object || !Dm.truthy(can_smash_object(basic_mob, @object)))
                continue;
            await basic_mob.melee_attack(@object);
            return TRUE;
        }

        if (Dm.truthy(can_attack_turfs))
        {
            await basic_mob.melee_attack(next_step);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/ai/generic_behaviors/attack_obstacles.dm:59
    public virtual object can_smash_object(mob_living_basic basic_mob = null, obj @object = null)
    {
        if (!Dm.truthy(@object.density) && !Dm.truthy(can_attack_dense_objects))
            return FALSE;
        if (Dm.truthy(@object.IsObscured()))
            return FALSE;
        if (Dm.num(basic_mob.see_invisible) < Dm.num(@object.invisibility))
            return FALSE;
        DmList whitelist = ((datum_ai_controller)basic_mob.ai_controller).blackboard[BB_OBSTACLE_TARGETING_WHITELIST] as DmList;
        if (Dm.truthy(whitelist) && !Dm.truthy(is_type_in_typecache(@object, whitelist)))
            return FALSE;
        return TRUE;
    }
}
