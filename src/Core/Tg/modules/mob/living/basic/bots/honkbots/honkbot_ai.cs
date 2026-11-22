// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (gaps package C3).

// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:1
public partial class datum_ai_controller_basic_controller_bot_honkbot : datum_ai_controller_basic_controller_bot
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:14
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        // on_clear_target intentionally removed  it caused grab-then-immediately-release bug
        // Cleanup is handled by on_stop_pulling instead
        RegisterSignal(new_pawn, COMSIG_ATOM_NO_LONGER_PULLING, nameof(on_stop_pulling));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:22
    [SignalHandler]
    public virtual void on_stop_pulling(datum source = null)
    {
        if (!Dm.truthy(blackboard_key_exists(BB_SLIP_TARGET)))
            return;

        atom slip_target = blackboard[BB_SLIP_TARGET] as atom;
        add_to_blacklist(slip_target);
        clear_blackboard_key(BB_SLIP_TARGET);
    }
}

// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:39
public partial class datum_bt_node_ai_behavior_use_mob_ability_random_honk : datum_bt_node_ai_behavior_use_mob_ability
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:41
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(5, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        return base.perform(seconds_per_tick, controller);
    }
}

/// Valid if the target is a visible human who isn't buckled and has gravity  someone a slip can actually knock over.
// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:49
public partial class datum_targeting_strategy_can_see_slip_victim : datum_targeting_strategy_can_see
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living_carbon_human candidate)
            return FALSE;
        return Dm.num(candidate.buckled == null && Dm.truthy(candidate.has_gravity()));
    }
}

// Positions the pulled victim onto the slippery item by stepping away, then releases.
// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:60
public partial class datum_bt_node_ai_behavior_release_and_slip : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:63
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living victim = controller.blackboard[victim_key] as mob_living;
        mob_living our_mob = controller.pawn as mob_living;
        if (QDELETED(victim) || our_mob.pulling != victim)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        DmList possible_dirs = GLOB.alldirs.Copy();
        Dm.remove(ref possible_dirs, get_dir(our_mob, victim));
        foreach (object direction in Dm.each<object>(possible_dirs.Copy()))
        {
            turf possible_turf = get_step(our_mob, Dm.num(direction)) as turf;
            if (Dm.truthy(Waiting.NoSleep(possible_turf.is_blocked_turf(source_atom: our_mob))))
                Dm.remove(ref possible_dirs, direction);
        }
        if (Dm.truthy(length(possible_dirs)))
            Waiting.NoSleep(step(our_mob, Dm.num(pick(possible_dirs))));
        Waiting.NoSleep(our_mob.stop_pulling());
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Gathers nearby atoms matching the slippery-item typepaths stored in BB_SLIPPERY_ITEMS.
// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:83
public partial class datum_target_source_honkbot_slippery : datum_target_source
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:85
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList slippery_items = controller.blackboard[BB_SLIPPERY_ITEMS] as DmList;
        if (!Dm.truthy(length(slippery_items)))
            return Dm.list();
        return typecache_filter_list(oview(range, pawn), typecacheof(slippery_items) as DmList);
    }
}

/// Valid only if the target is within line of sight (not just within range).
// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:92
public partial class datum_targeting_strategy_can_see : datum_targeting_strategy
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        return can_see(living_mob, target, vision_range);
    }
}

/// Valid if the target is a conscious clown (by trait, or a borg running the clown model).
// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:99
public partial class datum_targeting_strategy_clown_friend : datum_targeting_strategy
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living target_mob || IS_UNCONSCIOUS_OR_CRIT(target_mob))
            return FALSE;
        if (HAS_TRAIT(target_mob, TRAIT_PERCEIVED_AS_CLOWN))
            return TRUE;
        if (target_mob is mob_living_silicon_robot robot_target)
            return Dm.num(robot_target.model is obj_item_robot_model_clown);
        return FALSE;
    }
}

// tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:113
public partial class datum_bt_node_ai_behavior_play_with_clown : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:116
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;
        mob_living living_target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(living_target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(controller.pawn, living_target)) > 1)
            return AI_BEHAVIOR_INSTANT;
        mob_living living_pawn = controller.pawn as mob_living;
        living_pawn.manual_emote(Dm.text("celebrates with []!", living_target));
        Waiting.Detach(living_pawn.emote("flip"));
        Waiting.Detach(living_pawn.emote("beep"));
        return start_async();
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:131
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        datum_action honk_ability = controller.blackboard[BB_HONK_ABILITY] as datum_action;
        object result = honk_ability == null ? null : await honk_ability.Trigger();
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot_ai.dm:138
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        mob_living living_target = bot_controller.blackboard[target_key] as mob_living;
        if (living_target != null)
            bot_controller.add_to_blacklist(living_target);
        bot_controller.clear_blackboard_key(target_key);
    }
}
