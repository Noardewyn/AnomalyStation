// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (gaps package C3).

// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:4
public partial class datum_ai_controller_basic_controller_bot_cleanbot : datum_ai_controller_basic_controller_bot
{
    ///list that ties each flag to its list key
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:25
    public static DmList clean_flags => TgState.Current.datum_ai_controller_basic_controller_bot_cleanbot__clean_flags ??= Dm.list(
        Dm.kv(BB_CLEANABLE_BLOOD, CLEANBOT_CLEAN_BLOOD),
        Dm.kv(BB_HUNTABLE_PESTS, CLEANBOT_CLEAN_PESTS),
        Dm.kv(BB_CLEANABLE_DRAWINGS, CLEANBOT_CLEAN_DRAWINGS),
        Dm.kv(BB_HUNTABLE_TRASH, CLEANBOT_CLEAN_TRASH));
}

/// Gathers nearby cleanable atoms: decals plus whatever types the cleanbot's currently enabled janitor mode flags allow.
// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:34
public partial class datum_target_source_cleanbot_cleanables : datum_target_source
{
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:36
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList final_hunt_list = Dm.list();
        Dm.append(final_hunt_list, controller.blackboard[BB_CLEANABLE_DECALS]);
        mob_living_basic_bot_cleanbot bot_pawn = (mob_living_basic_bot_cleanbot)pawn;
        foreach (object list_key in Dm.each<object>(datum_ai_controller_basic_controller_bot_cleanbot.clean_flags))
        {
            if (!Dm.truthy(Dm.bitand(bot_pawn.janitor_mode_flags, datum_ai_controller_basic_controller_bot_cleanbot.clean_flags[list_key])))
                continue;
            Dm.append(final_hunt_list, controller.blackboard[list_key]);
        }
        if (!Dm.truthy(length(final_hunt_list)))
            return Dm.list();
        DmList type_filter = typecacheof(final_hunt_list) as DmList;
        return typecache_filter_list(oview(range, pawn), type_filter);
    }
}

///clean that shit bro fr fr 67
// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:50
public partial class datum_bt_node_ai_behavior_execute_clean : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:53
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(living_pawn, target)) > 1)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        Waiting.NoSleep(living_pawn.UnarmedAttack(target, proximity_flag: TRUE));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:65
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        bot_controller.set_blackboard_key(BB_POST_CLEAN_COOLDOWN, 5 * SECONDS + world.time); // POST_CLEAN_COOLDOWN
        atom target = bot_controller.blackboard[target_key] as atom;
        if (!Dm.truthy(succeeded) && target != null)
        {
            bot_controller.clear_blackboard_key(target_key);
            bot_controller.add_to_blacklist(target);
            return;
        }
        if (QDELETED(target) || Dm.truthy(is_type_in_typecache(target, bot_controller.blackboard[BB_HUNTABLE_TRASH] as DmList)))
            return;
        if (!iscarbon(target))
        {
            bot_controller.clear_blackboard_key(target_key);
            return;
        }
        DmList speech_list = bot_controller.blackboard[BB_CLEANBOT_EMAGGED_PHRASES] as DmList;
        if (Dm.truthy(length(speech_list)))
        {
            mob_living living_pawn = bot_controller.pawn as mob_living;
            if (!QDELETED(living_pawn))
                Waiting.Detach(living_pawn.say(pick(speech_list), forced: "ai controller"));
        }
        bot_controller.clear_blackboard_key(target_key);
    }
}

/// Valid if the target is a conscious human janitor-whisperer the cleanbot hasn't already befriended.
// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:87
public partial class datum_targeting_strategy_capable_human_cleanbot_whisperer : datum_targeting_strategy_capable_human
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!HAS_TRAIT(target, TRAIT_CLEANBOT_WHISPERER))
            return FALSE;
        return Dm.num(!Dm.truthy(living_mob.has_ally(REF(target))));
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList datum_ai_controller_basic_controller_bot_cleanbot__clean_flags;
}

///Tells the cleanbot to go clean a target
// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:101
public partial class datum_pet_command_clean : datum_pet_command
{
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:109
    public override async DmTask<object> set_command_target(mob_living parent = null, atom target = null)
    {
        if (target == null || !istype(target, typeof(obj_effect_decal_cleanable)))
            return FALSE;
        if (parent.ai_controller == null)
            return FALSE;
        if (Dm.truthy(LAZYACCESS(((datum_ai_controller)parent.ai_controller).blackboard[BB_TEMPORARY_IGNORE_LIST] as DmList, target)))
            return FALSE;
        return await base.set_command_target(parent, target);
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot_ai.dm:118
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[BB_CURRENT_PET_TARGET] as atom;
        if (QDELETED(target))
        {
            controller.clear_blackboard_key(BB_ACTIVE_PET_COMMAND);
            return null;
        }
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_clean_pet_target));
        return null;
    }
}
