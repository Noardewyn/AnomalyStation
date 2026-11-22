// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/bots/bot_subtrees.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (the admin event logger, section 104) (gaps package C3).

/**
 * Searches for a valid target in oview and sets a blackboard key when found.
 * Subtypes override valid_target() to refine selection criteria.
 * looking_for is an optional typecache pre-filter; pass null to check all atoms via valid_target().
 */
// tg: code/datums/ai/bots/bot_subtrees.dm:8
public partial class datum_bt_node_ai_behavior_bot_search : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:21
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller is not datum_ai_controller_basic_controller_bot bot_controller)
        {
            stack_trace(Dm.text("attempted to give [] the bot search behavior!", controller.pawn), "code/datums/ai/bots/bot_subtrees.dm", 23);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        if (looking_for == null)
            looking_for = get_looking_for_typecache();

        // Build candidate list synchronously (no sleeping), then hand off to async.
        DmList ignore_list = bot_controller.blackboard[BB_TEMPORARY_IGNORE_LIST] as DmList;
        DmList candidates = Dm.list();
        foreach (atom potential_target in Dm.each_cast<atom>(Dm.truthy(turf_search) ? RANGE_TURFS(radius, bot_controller.pawn) : oview(radius, bot_controller.pawn)))
        {
            if (looking_for != null && !Dm.truthy(is_type_in_typecache(potential_target, looking_for)))
                continue;
            if (Dm.truthy(LAZYACCESS(ignore_list, potential_target)))
                continue;
            if (!Dm.truthy(Waiting.NoSleep(valid_target(bot_controller, potential_target))))
                continue;
            if (!Dm.truthy(can_see(bot_controller.pawn, potential_target, radius)))
                continue;
            Dm.append(ref candidates, potential_target);
        }

        if (!Dm.truthy(length(candidates)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        candidate_stash = candidates;
        return start_async();
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:55
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        object found = FALSE;
        foreach (atom potential_target in Dm.each_cast<atom>(candidate_stash))
        {
            if (!Dm.truthy(async_still_valid()))
                break;
            if (Dm.truthy(await bot_controller.set_if_can_reach(key: target_key, target: potential_target, distance: Dm.num(pathing_distance), bypass_add_to_blacklist: Dm.num(bypass_add_blacklist), minimum_distance: Dm.num(minimum_distance))))
            {
                found = TRUE;
                break;
            }
        }
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(found) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:70
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        candidate_stash = null;
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:74
    public virtual DmList get_looking_for_typecache()
    {
        return null;
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:77
    public virtual async DmTask<object> valid_target(datum_ai_controller_basic_controller_bot controller = null, atom my_target = null)
    {
        return TRUE;
    }
}

///Performs bot speech from a list of options
// tg: code/datums/ai/bots/bot_subtrees.dm:82
public partial class datum_bt_node_ai_behavior_bot_speech : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:87
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[announce_key] as datum_action_cooldown_bot_announcement;
        if (announcement == null || !Dm.truthy(length(list_to_pick_from)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        announcement.announce(pick(list_to_pick_from));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

///Interact with an object. Could probably be moved to a generic behavior as the only unique thing is the blacklist.
// tg: code/datums/ai/bots/bot_subtrees.dm:96
public partial class datum_bt_node_ai_behavior_bot_interact : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:100
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(living_pawn, target)) > 1)
            return AI_BEHAVIOR_INSTANT;
        Waiting.NoSleep(living_pawn.UnarmedAttack(target, proximity_flag: TRUE));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:110
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        atom target = bot_controller.blackboard[target_key] as atom;
        if (Dm.truthy(clear_target))
            bot_controller.clear_blackboard_key(target_key);
        if (!Dm.truthy(succeeded) && target != null)
            bot_controller.add_to_blacklist(target);
    }
}

/// Searches GLOB.deliverybeacons for a beacon whose location matches the tag in tag_key, and sets it as target_key.
// tg: code/datums/ai/bots/bot_subtrees.dm:124
public partial class datum_bt_node_ai_behavior_find_delivery_beacon : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:130
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object beacon_tag = controller.blackboard[tag_key];
        if (beacon_tag == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        foreach (obj_machinery_navbeacon beacon in Dm.each_cast<obj_machinery_navbeacon>(GLOB.deliverybeacons))
        {
            if (!Dm.eq(beacon.location, beacon_tag))
                continue;
            controller.set_blackboard_key(target_key, beacon);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

///Find the closest beacon and set it as the target
// tg: code/datums/ai/bots/bot_subtrees.dm:142
public partial class datum_bt_node_ai_behavior_find_first_beacon_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:145
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        float closest_distance = INFINITY;
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        atom final_target = null;
        atom previous_target = controller.blackboard[BB_PREVIOUS_BEACON_TARGET] as atom;
        foreach (obj_machinery_navbeacon beacon in Dm.each_cast<obj_machinery_navbeacon>(GLOB.navbeacons[Dm.text("[]", bot_pawn.z)]))
        {
            float dist = Dm.num(get_dist(bot_pawn, beacon));
            if (beacon == previous_target || dist <= 1)
                continue;
            if (dist > closest_distance)
                continue;
            closest_distance = dist;
            final_target = beacon;
        }

        if (final_target == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        controller.set_blackboard_key(target_key, final_target);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

///Find the next beacon from a previous target and set it as the new target
// tg: code/datums/ai/bots/bot_subtrees.dm:168
public partial class datum_bt_node_ai_behavior_find_next_beacon_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:172
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        obj_machinery_navbeacon prev_beacon = controller.blackboard[BB_PREVIOUS_BEACON_TARGET] as obj_machinery_navbeacon;
        if (QDELETED(prev_beacon))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        atom final_target = null;
        foreach (obj_machinery_navbeacon beacon in Dm.each_cast<obj_machinery_navbeacon>(GLOB.navbeacons[Dm.text("[]", bot_pawn.z)]))
        {
            if (Dm.eq(beacon.location, prev_beacon.codes[NAVBEACON_PATROL_NEXT]))
            {
                final_target = beacon;
                break;
            }
        }

        if (final_target == null)
        {
            controller.clear_blackboard_key(BB_PREVIOUS_BEACON_TARGET);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        controller.set_blackboard_key(BB_PREVIOUS_BEACON_TARGET, final_target);
        controller.set_blackboard_key(target_key, final_target);
        return AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:195
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
    }
}

/// Records the beacon as visited and clears the target key once the bot is on the same turf.
// tg: code/datums/ai/bots/bot_subtrees.dm:200
public partial class datum_bt_node_ai_behavior_arrive_at_beacon : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:203
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_machinery_navbeacon beacon = controller.blackboard[target_key] as obj_machinery_navbeacon;
        if (QDELETED(beacon))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(controller.pawn, beacon)) > 0)
            return AI_BEHAVIOR_INSTANT;
        controller.set_blackboard_key(BB_PREVIOUS_BEACON_TARGET, beacon);
        controller.clear_blackboard_key(target_key);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Completes summon travel once the bot reaches the summon target's turf.
// tg: code/datums/ai/bots/bot_subtrees.dm:215
public partial class datum_bt_node_ai_behavior_complete_summon_travel : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:218
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        if (QDELETED(bot_pawn))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        atom target = controller.blackboard[target_key] as atom;
        if (Dm.num(get_dist(bot_pawn, target)) > 0)
            return AI_BEHAVIOR_INSTANT;
        bot_pawn.calling_ai_ref = null;
        bot_pawn.update_bot_mode(new_mode: BOT_IDLE);
        controller.clear_blackboard_key(target_key);
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

///Find a valid authority to salute and set them as the target
// tg: code/datums/ai/bots/bot_subtrees.dm:231
public partial class datum_bt_node_ai_behavior_find_valid_authority : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:234
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        foreach (mob_living nearby_mob in Dm.each<mob_living>(oview(7, controller.pawn)))
        {
            if (!HAS_TRAIT(nearby_mob, TRAIT_COMMISSIONED))
                continue;
            controller.set_blackboard_key(target_key, nearby_mob);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }
}

///Salute the authority /(o.o)
// tg: code/datums/ai/bots/bot_subtrees.dm:243
public partial class datum_bt_node_ai_behavior_salute_authority : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/bots/bot_subtrees.dm:247
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!Dm.truthy(controller.blackboard_key_exists(target_key)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        DmList salute_list = controller.blackboard[salute_keys] as DmList;
        if (!Dm.truthy(length(salute_list)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)controller.pawn;
        obj_item our_hat = Dm.locate<obj_item_clothing_head>(bot_pawn);
        if (our_hat != null)
            Dm.append(ref salute_list, Dm.text("tips [] at ", our_hat));
        bot_pawn.manual_emote((Dm.text("[] []!", pick(salute_list), controller.blackboard[target_key])));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/bots/bot_subtrees.dm:260
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
