// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/bot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (the admin event logger, section 104) (gaps package C3).

// tg: code/modules/mob/living/basic/bots/bot_ai.dm:26
public partial class datum_targeting_strategy_basic_bot : datum_targeting_strategy_basic
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        atom the_target = target;
        datum_ai_controller_basic_controller_bot my_controller = living_mob.ai_controller as datum_ai_controller_basic_controller_bot;
        if (my_controller == null)
            return FALSE;
        if (!ishuman(the_target) || Dm.truthy(LAZYACCESS(my_controller.blackboard[BB_TEMPORARY_IGNORE_LIST] as DmList, the_target)))
            return FALSE;
        mob_living living_target = (mob_living)the_target;
        if (living_target.mind == null)
            return FALSE;
        if (get_turf(living_mob) == get_turf(living_target))
            return await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(await my_controller.can_reach_target(living_target, distance: 10)))
        {
            my_controller.add_to_blacklist(living_target);
            return FALSE;
        }
        return await base.is_valid_target(living_mob, target, vision_range, controller);
    }
}

// tg: code/modules/mob/living/basic/bots/bot_ai.dm:1
public partial class datum_ai_controller_basic_controller_bot : datum_ai_controller_basic_controller
{
    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:42
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        RegisterSignal(new_pawn, COMSIG_BOT_RESET, nameof(reset_bot));
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_BOT_SUMMON_TARGET), nameof(clear_summon));
        RegisterSignal(new_pawn, COMSIG_MOB_AI_MOVEMENT_STARTED, nameof(on_movement_start));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:50
    [SignalHandler]
    public virtual void on_movement_start(mob_living_basic_bot source = null, atom target = null)
    {
        if (target == blackboard[BB_BEACON_TARGET])
        {
            source.update_bot_mode(new_mode: BOT_PATROL);
            return;
        }

        source.clear_path_hud(remove_hud: FALSE);
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:59
    public virtual void add_to_blacklist(atom target = null, object duration = null)
    {
        if (QDELETED(target))
            return;
        object final_duration = Dm.truthy(duration) ? duration : blackboard[BB_UNREACHABLE_LIST_COOLDOWN];
        set_blackboard_key_assoc_lazylist(BB_TEMPORARY_IGNORE_LIST, target, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_from_blacklist), target), Dm.num(final_duration), file: "code/modules/mob/living/basic/bots/bot_ai.dm", line: 64);
    }

    /// Unreachable targets get added to the temporary ignore list so we stop pathing to them. Subtypes override to skip blacklisting in stationary mode.
    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:67
    public override void note_unreachable_target(atom target = null)
    {
        add_to_blacklist(target);
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:70
    public virtual void remove_from_blacklist(atom target = null)
    {
        if (QDELETED(target))
            return;
        remove_from_blackboard_lazylist_key(BB_TEMPORARY_IGNORE_LIST, target);
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:75
    [SignalHandler]
    public virtual void clear_summon()
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)pawn;
        Waiting.NoSleep(bot_pawn.bot_reset());
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:81
    public override void setup_able_to_run()
    {
        base.setup_able_to_run();
        RegisterSignal(pawn, COMSIG_BOT_MODE_FLAGS_SET, nameof(update_able_to_run));
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:85
    public override void clear_able_to_run()
    {
        UnregisterSignal(pawn, Dm.list(COMSIG_BOT_MODE_FLAGS_SET));
        base.clear_able_to_run();
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:89
    public override object get_able_to_run()
    {
        mob_living_basic_bot bot_pawn = (mob_living_basic_bot)pawn;
        if (!Dm.truthy(Dm.bitand(bot_pawn.bot_mode_flags, BOT_MODE_ON)))
            return AI_UNABLE_TO_RUN;
        return base.get_able_to_run();
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:95
    public override object get_access()
    {
        mob_living_basic_bot basic_bot = (mob_living_basic_bot)pawn;
        return basic_bot.access_card?.access;
    }

    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:99
    [SignalHandler]
    public virtual void reset_bot()
    {
        Waiting.NoSleep(cancel_current_plan());
        if (!Dm.truthy(length(reset_keys)))
            return;
        foreach (object key in Dm.each<object>(reset_keys))
            clear_blackboard_key(key);
    }

    ///set the target if we can reach them
    // tg: code/modules/mob/living/basic/bots/bot_ai.dm:108
    public virtual async DmTask<object> set_if_can_reach(object key = null, object target = null, object duration = null, float? distance = null, float? bypass_add_to_blacklist = null, float? minimum_distance = null)
    {
        distance ??= 10;
        bypass_add_to_blacklist ??= FALSE;
        minimum_distance ??= 0;
        if (Dm.truthy(await can_reach_target(target as atom, distance, minimum_distance)))
        {
            set_blackboard_key(key, target);
            return TRUE;
        }
        if (Dm.truthy(bypass_add_to_blacklist))
            return FALSE;
        object final_duration = Dm.truthy(duration) ? duration : blackboard[BB_UNREACHABLE_LIST_COOLDOWN];
        add_to_blacklist(target as atom, final_duration);
        return FALSE;
    }
}
