// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Command magicarp to use their spell on the target
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:5
public partial class datum_bt_node_decorator_carp_should_flee : datum_bt_node_decorator
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:13
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        atom flee_from = controller.blackboard[target_key] as atom;
        if (QDELETED(flee_from))
            return FALSE;
        if (Dm.truthy(controller.blackboard[BB_CARPS_FEAR_FISHERMAN]) && HAS_TRAIT(flee_from, TRAIT_SCARY_FISHERMAN))
            return TRUE;
        return !Dm.truthy(controller.blackboard[BB_BASIC_MOB_STOP_FLEEING]) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:21
    public override object register_observe_signals(atom pawn = null)
    {
        RegisterSignals(pawn, Dm.list(
            COMSIG_AI_BLACKBOARD_KEY_SET(target_key),
            COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_key),
            COMSIG_AI_BLACKBOARD_KEY_SET(BB_BASIC_MOB_STOP_FLEEING),
            COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_BASIC_MOB_STOP_FLEEING)), nameof(on_signal_changed));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:30
    public override void unregister_observe_signals(atom pawn = null)
    {
        UnregisterSignal(pawn, Dm.list(
            COMSIG_AI_BLACKBOARD_KEY_SET(target_key),
            COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_key),
            COMSIG_AI_BLACKBOARD_KEY_SET(BB_BASIC_MOB_STOP_FLEEING),
            COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_BASIC_MOB_STOP_FLEEING)));
    }
}

/**
 * Find a target for the magicarp's spell
 * This behaviour is a bit more complicated than most because we need to find a target for a spell, which varies
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:42
public partial class datum_bt_node_ai_behavior_find_magicarp_spell_target : datum_bt_node_ai_behavior
{
    private const int MAGICARP_SPELL_TARGET_SEEK_RANGE = 4; // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:1
    private const int MAGICARP_SPELL_ENEMY_SEEK_RANGE = 9; // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:2

    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:52
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        datum_action_cooldown using_action = controller.blackboard[ability_key] as datum_action_cooldown;
        if (using_action == null || !Dm.truthy(Waiting.NoSleep(using_action.IsAvailable())))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);

        atom found;
        object special = controller.blackboard[special_targeting_key];
        if (Dm.eq(special, MAGICARP_SPELL_CORPSES))
            found = find_friendly_corpse(controller);
        else if (Dm.eq(special, MAGICARP_SPELL_OBJECTS))
            found = find_animatable(controller);
        else if (Dm.eq(special, MAGICARP_SPELL_WALLS))
            found = find_nearest_wall(controller);
        else
            found = Waiting.NoSleep(find_nearest_enemy(controller)) as atom;

        if (found == null)
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        controller.set_blackboard_key(target_key, found);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }

    /// Find the nearest mob which isn't a scary fisherman
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:75
    public virtual async DmTask<object> find_nearest_enemy(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        datum_targeting_strategy strategy = GET_TARGETING_STRATEGY(controller.blackboard[targeting_strategy_key]);
        if (strategy == null)
            return null;
        DmList candidates = Dm.list();
        foreach (mob_living candidate in Dm.each<mob_living>(oview(MAGICARP_SPELL_ENEMY_SEEK_RANGE, living_pawn)))
        {
            if (HAS_TRAIT(candidate, TRAIT_SCARY_FISHERMAN))
                continue;
            if (!Dm.truthy(await strategy.is_valid_target(living_pawn, candidate, MAGICARP_SPELL_ENEMY_SEEK_RANGE, controller)))
                continue;
            Dm.append(ref candidates, candidate);
        }
        if (Dm.length(candidates) == 0)
            return null;
        return get_closest_atom(typeof(mob_living), candidates, living_pawn);
    }

    /// Find an object to animate
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:92
    public virtual atom find_animatable(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList nearby_items = Dm.list();
        foreach (obj new_friend in Dm.each<obj>(oview(MAGICARP_SPELL_TARGET_SEEK_RANGE, living_pawn)))
        {
            if (!isitem(new_friend) && !isstructure(new_friend))
                continue;
            if (Dm.truthy(is_type_in_list(new_friend, GLOB.animatable_blacklist)))
                continue;
            if (Dm.num(living_pawn.see_invisible) < Dm.num(new_friend.invisibility))
                continue;
            Dm.append(ref nearby_items, new_friend);
        }
        if (Dm.length(nearby_items) > 0)
            return pick(nearby_items) as atom;
        return null;
    }

    /// Find a wall to open
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:108
    public virtual atom find_nearest_wall(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList nearby_walls = Dm.list();
        foreach (turf_closed new_wall in Dm.each<turf_closed>(oview(MAGICARP_SPELL_TARGET_SEEK_RANGE, living_pawn)))
        {
            if (isindestructiblewall(new_wall))
                continue;
            Dm.append(ref nearby_walls, new_wall);
        }
        if (Dm.length(nearby_walls) > 0)
            return get_closest_atom(typeof(turf_closed), nearby_walls, living_pawn) as atom;
        return null;
    }

    /// Find a dead body to resurrect
    // tg: code/modules/mob/living/basic/space_fauna/carp/carp_ai_actions.dm:120
    public virtual atom find_friendly_corpse(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        DmList nearby_bodies = Dm.list();
        foreach (mob_living dead_pal in Dm.each<mob_living>(oview(MAGICARP_SPELL_TARGET_SEEK_RANGE, living_pawn)))
        {
            if (!isturf(dead_pal.loc))
                continue;
            if (dead_pal.stat == 0 || Dm.num(dead_pal.health) > 0)
                continue;
            if (Dm.num(living_pawn.see_invisible) < Dm.num(dead_pal.invisibility))
                continue;
            if (!Dm.truthy(living_pawn.faction_check_atom(dead_pal)))
                continue;
            Dm.append(ref nearby_bodies, dead_pal);
        }
        if (Dm.length(nearby_bodies) > 0)
            return pick(nearby_bodies) as atom;
        return null;
    }
}
