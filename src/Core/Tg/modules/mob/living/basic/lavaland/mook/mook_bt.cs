// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/mook/mook_bt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The subtrees are var blocks (generated); their trees are tg's compiled .bt.json (BehaviorTrees/).

///hey buddy I went to mook village and everyone there knew you
// tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:2
public partial class datum_bt_node_ai_behavior_find_village : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:7
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_effect_landmark home = Dm.locate<obj_effect_landmark_mook_village>(GLOB.landmarks_list);
        if (home == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        controller.set_blackboard_key(target_key, home);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Mook-specific mineral wall finder that skips turfs in BB_BLACKLIST_MINERAL_TURFS.
// tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:15
public partial class datum_bt_node_ai_behavior_find_mineral_wall_mook : datum_bt_node_ai_behavior_find_mineral_wall
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:17
    public override async DmTask<object> check_if_mineable(datum_ai_controller controller = null, turf target_wall = null)
    {
        DmList forbidden = controller.blackboard[BB_BLACKLIST_MINERAL_TURFS] as DmList;
        if (Dm.truthy(is_type_in_list(target_wall, forbidden)))
            return FALSE;
        return await base.check_if_mineable(controller, target_wall);
    }
}

///Wander in a random direction to find ore
// tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:24
public partial class datum_bt_node_ai_behavior_calculate_wander_destination : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:31
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        atom anchor = controller.blackboard[anchor_key] as atom;
        if (QDELETED(anchor))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.eq(anchor.z, living_pawn.z))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        DmList angle_directions = Dm.list();
        foreach (object direction in Dm.each(GLOB.alldirs))
            Dm.append(ref angle_directions, dir2angle(direction));

        float angle_to_home = Dm.num(get_angle(living_pawn, anchor));
        Dm.remove(ref angle_directions, angle_to_home);
        Dm.remove(ref angle_directions, angle_to_home + 45);
        Dm.remove(ref angle_directions, angle_to_home - 45);
        shuffle_inplace(angle_directions);

        turf best = get_turf(living_pawn) as turf;
        foreach (object angle in Dm.each(angle_directions))
        {
            turf candidate = _get_furthest_turf(living_pawn, angle, anchor);
            if (candidate == null)
                continue;
            float dist = get_dist(anchor, candidate);
            if (dist <= get_dist(anchor, best))
                continue;
            best = candidate;
            if (dist >= Dm.num(wander_distance))
                break;
        }

        controller.set_blackboard_key(destination_key, best);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:64
    public virtual turf _get_furthest_turf(atom source = null, object angle = null, atom anchor = null)
    {
        turf result = null;
        for (float i = 1; i <= Dm.num(wander_distance); i++)
        {
            turf candidate = get_ranged_target_turf_direct(source, anchor, range: i, offset: angle) as turf;
            if (Dm.truthy(Waiting.NoSleep(candidate.is_blocked_turf(source_atom: source))))
                break;
            result = candidate;
        }
        return result;
    }
}

///gotta get south of the material stand
// tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:73
public partial class datum_bt_node_ai_behavior_find_deposit_position : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:81
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom stand = controller.blackboard[stand_key] as atom;
        if (QDELETED(stand))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living pawn = controller.pawn as mob_living;
        DmList candidates = Dm.list();
        foreach (object direction in Dm.each(Dm.list(SOUTH, SOUTHWEST, SOUTHEAST)))
        {
            turf candidate = get_step(stand, Dm.num(direction));
            if (!Dm.truthy(Waiting.NoSleep(candidate.is_blocked_turf())))
                Dm.append(ref candidates, candidate);
        }
        turf destination = get_closest_atom(typeof(turf), candidates, pawn) as turf;
        if (destination == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        controller.set_blackboard_key(destination_key, destination);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

///OH SHIT STORM COMING (or maybe we found ore :3)
// tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:98
public partial class datum_bt_node_decorator_mook_has_flee_reason : datum_bt_node_decorator
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:100
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(
            COMSIG_AI_BLACKBOARD_KEY_SET(BB_STORM_APPROACHING),
            COMSIG_AI_BLACKBOARD_KEY_SET(BB_SIMPLE_CARRY_ITEM),
            COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_SIMPLE_CARRY_ITEM)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:108
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(
            COMSIG_AI_BLACKBOARD_KEY_SET(BB_STORM_APPROACHING),
            COMSIG_AI_BLACKBOARD_KEY_SET(BB_SIMPLE_CARRY_ITEM),
            COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_SIMPLE_CARRY_ITEM)));
    }

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_bt.dm:115
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        return Dm.truthy(controller.blackboard[BB_STORM_APPROACHING]) || controller.blackboard[BB_SIMPLE_CARRY_ITEM] != null ? TRUE : FALSE;
    }
}
