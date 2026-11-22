// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3); playing the song is a deferred leaf (instruments, /datum/song, section 109).

// tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:1
public partial class datum_ai_controller_basic_controller_bot_vibebot : datum_ai_controller_basic_controller_bot
{
    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:16
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_SET(BB_VIBEBOT_PARTY_TARGET), nameof(play_music));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:22
    [SignalHandler]
    public virtual void play_music(datum source = null, object blackboard_key = null)
    {
        Dm.Deferred("code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:25", "play_music: the song on the piano (/datum/song and the instruments are not ported, section 109)");
    }
}

/// Valid if the target is a conscious human who's in a bad mood or having a birthday  someone who could use cheering up.
// tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:45
public partial class datum_targeting_strategy_capable_human_party_friend : datum_targeting_strategy_capable_human
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        mob_living_carbon_human human_target = (mob_living_carbon_human)target;
        return Dm.num(Dm.num(human_target.mob_mood?.mood_level) < MOOD_LEVEL_NEUTRAL || HAS_TRAIT(human_target, TRAIT_BIRTHDAY_BOY));
    }
}

// tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:52
public partial class datum_bt_node_ai_behavior_vibebot_party : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:56
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(living_target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(controller.pawn, living_target)) > 1)
            return AI_BEHAVIOR_INSTANT;
        datum_action_cooldown ability = controller.blackboard[ability_key] as datum_action_cooldown;
        if (ability != null)
            Waiting.Detach(ability.Trigger(null, null, living_target));
        mob_living living_pawn = controller.pawn as mob_living;
        living_pawn.manual_emote(Dm.text("celebrates with []!", living_target));
        Waiting.Detach(living_pawn.emote("flip"));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/bots/vibebot/vibebot_ai.dm:70
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        atom target = bot_controller.blackboard[target_key] as atom;
        if (target != null)
            bot_controller.add_to_blacklist(target);
        bot_controller.clear_blackboard_key(target_key);
    }
}
