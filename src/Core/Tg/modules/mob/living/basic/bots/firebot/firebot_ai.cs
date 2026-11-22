// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/firebot/firebot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:3
public partial class datum_ai_controller_basic_controller_bot_firebot : datum_ai_controller_basic_controller_bot
{
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:18
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        return dot;
    }

    /// Firebot skips blacklisting unreachable targets while stationary, matching the old set_if_can_reach bypass.
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:42
    public override void note_unreachable_target(atom target = null)
    {
        mob_living_basic_bot_firebot bot_pawn = (mob_living_basic_bot_firebot)pawn;
        if (Dm.truthy(Dm.bitand(bot_pawn.firebot_mode_flags, FIREBOT_STATIONARY_MODE)))
            return;
        base.note_unreachable_target(target);
    }
}

// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:25
public partial class datum_bt_node_ai_behavior_announce_fire_detected : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:27
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        datum_ai_controller_basic_controller_bot_firebot fire_controller = (datum_ai_controller_basic_controller_bot_firebot)controller;
        if (!(fire_controller.announcement_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
        datum_action_cooldown_bot_announcement announcement = fire_controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        if (announcement == null)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
        DmList lines = fire_controller.blackboard[BB_FIREBOT_FIRE_DETECTED_LINES] as DmList;
        if (!Dm.truthy(length(lines)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
        announcement.announce(pick(lines));
        fire_controller.announcement_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START, ANNOUNCEMENT_TIMER
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Gathers nearby living mobs to extinguish; empty unless people-extinguishing is on, range clamped to adjacent tiles when stationary.
// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:49
public partial class datum_target_source_firebot_targets : datum_target_source
{
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:51
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        mob_living_basic_bot_firebot bot_pawn = (mob_living_basic_bot_firebot)pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.firebot_mode_flags, FIREBOT_EXTINGUISH_PEOPLE)))
            return Dm.list();
        if (Dm.truthy(Dm.bitand(bot_pawn.firebot_mode_flags, FIREBOT_STATIONARY_MODE)))
            range = 1;
        DmList candidates = Dm.list();
        foreach (mob_living candidate in Dm.each<mob_living>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

/// Valid if the mob is on fire (or anyone, while emagged) and is a type this firebot is allowed to extinguish.
// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:63
public partial class datum_targeting_strategy_extinguishable_person : datum_targeting_strategy
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living target_mob)
            return FALSE;
        mob_living_basic_bot_firebot bot_pawn = (mob_living_basic_bot_firebot)living_mob;
        if (!Dm.truthy(target_mob.on_fire) && !Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)))
            return FALSE;
        return Dm.num(Dm.truthy(is_type_in_list(target_mob, controller.blackboard[BB_FIREBOT_CAN_EXTINGUISH] as DmList)));
    }
}

/// Gathers turfs in range; empty unless flame-extinguishing is enabled.
// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:77
public partial class datum_target_source_range_turfs_firebot_hotspots : datum_target_source_range_turfs
{
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:79
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        mob_living_basic_bot_firebot bot_pawn = (mob_living_basic_bot_firebot)pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.firebot_mode_flags, FIREBOT_EXTINGUISH_FLAMES)))
            return Dm.list();
        return base.collect_candidates(pawn, controller, range);
    }
}

/// Valid if the turf is an open turf with an active fire.
// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:86
public partial class datum_targeting_strategy_burning_hotspot : datum_targeting_strategy
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not turf_open open_turf)
            return FALSE;
        return Dm.num(open_turf.active_hotspot != null);
    }
}

// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:97
public partial class datum_bt_node_ai_behavior_bot_interact_extinguish : datum_bt_node_ai_behavior_bot_interact
{
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:99
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        // if we couldn't reach OR we emagged a living target, blacklist them
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        atom target = bot_controller.blackboard[target_key] as atom;
        mob_living_basic_bot living_bot = (mob_living_basic_bot)bot_controller.pawn;
        if (!Dm.truthy(succeeded) || (isliving(target) && Dm.truthy(Dm.bitand(living_bot.bot_access_flags, BOT_COVER_EMAGGED))))
            bot_controller.add_to_blacklist(target);
    }
}

// tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:109
public partial class datum_bt_node_ai_behavior_handle_firebot_speech : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/firebot/firebot_ai.dm:113
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(speech_prob, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        mob_living_basic_bot living_bot = (mob_living_basic_bot)controller.pawn;
        DmList idle_lines = (Dm.truthy(Dm.bitand(living_bot.bot_access_flags, BOT_COVER_EMAGGED)) ? controller.blackboard[BB_FIREBOT_EMAGGED_LINES] : controller.blackboard[BB_FIREBOT_IDLE_LINES]) as DmList;
        if (!Dm.truthy(length(idle_lines)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        announcement?.announce(pick(idle_lines));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
