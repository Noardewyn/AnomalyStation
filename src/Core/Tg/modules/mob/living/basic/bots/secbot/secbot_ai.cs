// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/secbot/secbot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/secbot/secbot_ai.dm:14
public partial class datum_targeting_strategy_basic_secbot : datum_targeting_strategy_basic
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        atom the_target = target;
        datum_ai_controller_basic_controller_bot my_controller = living_mob.ai_controller as datum_ai_controller_basic_controller_bot;
        if (my_controller == null)
            return FALSE;
        if (!ishuman(the_target) || Dm.truthy(LAZYACCESS(my_controller.blackboard[BB_TEMPORARY_IGNORE_LIST] as DmList, the_target)))
            return FALSE;
        mob_living_carbon_human human_target = (mob_living_carbon_human)the_target;
        if (human_target.handcuffed != null || IS_UNCONSCIOUS_OR_CRIT(human_target))
            return FALSE;
        if (Dm.@in(human_target, my_controller.blackboard[BB_BASIC_MOB_RETALIATE_LIST]))
            return TRUE;
        mob_living_basic_bot_secbot my_bot = (mob_living_basic_bot_secbot)living_mob;
        if (Dm.truthy(human_target.IsParalyzed()) && !Dm.truthy(Dm.bitand(my_bot.security_mode_flags, SECBOT_HANDCUFF_TARGET)))
            return FALSE;
        object assess_flags = my_bot.judgement_criteria();
        object assessed_threat = human_target.assess_threat(assess_flags);
        if (Dm.num(assessed_threat) > THREAT_ASSESS_DANGEROUS)
            my_controller.set_blackboard_key(BB_CURRENT_CRIMINAL_ASSESSMENT, assessed_threat);
        return Dm.num(Dm.num(assessed_threat) > THREAT_ASSESS_DANGEROUS);
    }
}

// tg: code/modules/mob/living/basic/bots/secbot/secbot_ai.dm:1
public partial class datum_ai_controller_basic_controller_bot_secbot : datum_ai_controller_basic_controller_bot
{
    // tg: code/modules/mob/living/basic/bots/secbot/secbot_ai.dm:34
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_SET(BB_CURRENT_TARGET), nameof(on_target_set));
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_CURRENT_TARGET), nameof(on_clear_target));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot_ai.dm:41
    [SignalHandler]
    public virtual void on_target_set()
    {
        datum_action_cooldown_bot_announcement announcement = blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        object threat_level = blackboard[BB_CURRENT_CRIMINAL_ASSESSMENT] ?? 5;
        announcement.announce(Dm.text("Level [] infraction alert!", threat_level));
        playsound(pawn, pick(
            Dm.resource("sound/mobs/non-humanoids/beepsky/criminal.ogg"),
            Dm.resource("sound/mobs/non-humanoids/beepsky/justice.ogg"),
            Dm.resource("sound/mobs/non-humanoids/beepsky/freeze.ogg")), 50, FALSE);
        mob_living_basic_bot_secbot my_bot = (mob_living_basic_bot_secbot)pawn;
        my_bot.update_bot_mode(new_mode: BOT_HUNT);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot_ai.dm:54
    [SignalHandler]
    public virtual void on_clear_target()
    {
        clear_blackboard_key(BB_CURRENT_CRIMINAL_ASSESSMENT);
    }
}
