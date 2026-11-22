// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:21
public partial class datum_bt_node_ai_behavior_commence_trashtalk : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:24
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!Dm.truthy(controller.blackboard_key_exists(target_key)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        object frustration_count = controller.blackboard[BB_WASH_FRUSTRATION];
        controller.set_blackboard_key(BB_WASH_FRUSTRATION, Math.Min(Dm.num(frustration_count) + 1, 8)); // BOT_FRUSTRATION_LIMIT
        if (Dm.num(controller.blackboard[BB_WASH_FRUSTRATION]) < 5) // BOT_ANGER_THRESHOLD
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        announcement?.announce(pick(controller.blackboard[BB_WASH_THREATS] as DmList));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Valid if the target is a conscious human with bloodied clothing (or anyone, while emagged).
// tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:39
public partial class datum_targeting_strategy_capable_human_washable_human : datum_targeting_strategy_capable_human
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)living_mob;
        if (Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)))
            return TRUE;
        mob_living_carbon_human human_target = (mob_living_carbon_human)target;
        foreach (atom clothing in Dm.each<atom>(human_target.get_equipped_items(Dm.bitor(INCLUDE_HELD, INCLUDE_PROSTHETICS))))
        {
            if (Dm.truthy(GET_ATOM_BLOOD_DNA_LENGTH(clothing)))
                return TRUE;
        }
        return FALSE;
    }
}

/// Finds someone to wash and announces it; while emagged the target is blacklisted so the bot washes each person only once.
// tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:53
public partial class datum_bt_node_ai_behavior_acquire_target_update_interaction_target_hygiene_wash : datum_bt_node_ai_behavior_acquire_target_update_interaction_target
{
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:55
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)bot_controller.pawn;
        if (Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)))
            bot_controller.add_to_blacklist(target);
        datum_action_cooldown_bot_announcement announcement = bot_controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        announcement.announce(pick(bot_controller.blackboard[BB_WASH_FOUND] as DmList));
    }
}

// tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:63
public partial class datum_bt_node_ai_behavior_wash_target : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:66
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_carbon_human unclean_target = controller.blackboard[target_key] as mob_living_carbon_human;
        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        if (QDELETED(unclean_target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(living_pawn, unclean_target)) > 0)
            return AI_BEHAVIOR_INSTANT;
        Waiting.NoSleep(living_pawn.melee_attack(unclean_target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot_ai.dm:76
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
        object wash_frustration = controller.blackboard[BB_WASH_FRUSTRATION];
        controller.set_blackboard_key(BB_WASH_FRUSTRATION, 0);
        if (!Dm.truthy(succeeded) || Dm.num(wash_frustration) <= 5) // BOT_ANGER_THRESHOLD
            return;
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        announcement.announce(pick(controller.blackboard[BB_WASH_DONE] as DmList));
    }
}
