// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/speech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The subtypes' lists are generated.

///Random speech behavior, for speech thats random
// tg: code/datums/ai/generic_behaviors/speech.dm:2
public partial class datum_bt_node_ai_behavior_random_speech : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/speech.dm:15
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!prob(speech_chance))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        float audible = length(emote_hear);
        float visible = length(emote_see);
        float spoken = length(speak);
        float total = audible + visible + spoken;
        if (!Dm.truthy(total))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        if (controller.pawn is not mob_living pawn)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        float roll = rand(1, total);

        if (roll <= audible)
        {
            pawn.manual_emote(pick(emote_hear));
            if (Dm.truthy(length(sound)))
                playsound(pawn, pick(sound), 80, vary: TRUE, pressure_affected: TRUE, ignore_walls: FALSE);
        }
        else if (roll <= audible + visible)
            pawn.manual_emote(pick(emote_see));
        else
            Waiting.Detach(speak__proc(pawn, controller));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/speech.dm:42
    public virtual async DmTask speak__proc(mob_living pawn = null, datum_ai_controller controller = null)
    {
        await pawn.say(pick(speak), forced: "AI Controller");
        if (Dm.truthy(length(sound)))
            playsound(pawn, pick(sound), 80, vary: TRUE);
    }
}

/// Make spooky sounds, if we have a corpse inside then impersonate them
// tg: code/datums/ai/generic_behaviors/speech.dm:118
public partial class datum_bt_node_ai_behavior_random_speech_legion : datum_bt_node_ai_behavior_random_speech
{
    // tg: code/datums/ai/generic_behaviors/speech.dm:126
    public override async DmTask speak__proc(mob_living pawn = null, datum_ai_controller controller = null)
    {
        mob_living_carbon_human victim = controller.blackboard[BB_LEGION_CORPSE] as mob_living_carbon_human;
        if (QDELETED(victim) || prob(30))
        {
            await base.speak__proc(pawn, controller);
            return;
        }

        if (HAS_MIND_TRAIT(victim, TRAIT_MIMING)) // mimes cant talk
            return;

        DmList remembered_speech = controller.blackboard[BB_LEGION_RECENT_LINES] as DmList ?? Dm.list();

        if (Dm.truthy(length(remembered_speech)) && prob(50)) // Don't spam the radio
        {
            await pawn.say(pick(remembered_speech), forced: "AI Controller");
            return;
        }

        obj_item_radio mob_radio = null;
        foreach (obj_item_radio found in Dm.each<obj_item_radio>(victim))
        {
            mob_radio = found;
            break;
        }
        if (QDELETED(mob_radio))
        {
            await base.speak__proc(pawn, controller); // No radio, just talk funny
            return;
        }
        mob_radio.talk_into(pawn, pick(Dm.add(radio_speech, remembered_speech)), pick(RADIO_CHANNEL_SUPPLY, RADIO_CHANNEL_COMMON));
    }
}

///Speech behavior that reads from a blackboard to pick what to say. Useful for things with dynamic speech behaviors
// tg: code/datums/ai/generic_behaviors/speech.dm:146
public partial class datum_bt_node_ai_behavior_random_speech_blackboard : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/speech.dm:148
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        DmList speech_lines = controller.blackboard[BB_BASIC_MOB_SPEAK_LINES] as DmList;
        if (speech_lines == null)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        float speech_chance = Dm.truthy(speech_lines[BB_SPEAK_CHANCE]) ? Dm.num(speech_lines[BB_SPEAK_CHANCE]) : 1;
        if (!prob(speech_chance))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        DmList emote_hear = speech_lines[BB_EMOTE_HEAR] as DmList ?? Dm.list();
        DmList emote_see = speech_lines[BB_EMOTE_SEE] as DmList ?? Dm.list();
        DmList speak = speech_lines[BB_EMOTE_SAY] as DmList ?? Dm.list();
        DmList sounds = speech_lines[BB_EMOTE_SOUND] as DmList ?? Dm.list();

        float total = length(emote_hear) + length(emote_see) + length(speak);
        if (!Dm.truthy(total))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        if (controller.pawn is not mob_living pawn)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        object sound_to_play = Dm.truthy(length(sounds)) ? pick(sounds) : null;
        float roll = rand(1, total);

        if (roll <= length(emote_hear))
        {
            pawn.manual_emote(pick(emote_hear));
            if (Dm.truthy(sound_to_play))
                playsound(pawn, sound_to_play, 80, vary: TRUE, pressure_affected: TRUE, ignore_walls: FALSE);
        }
        else if (roll <= length(emote_hear) + length(emote_see))
            pawn.manual_emote(pick(emote_see));
        else
        {
            Waiting.Detach(pawn.say(pick(speak), forced: "AI Controller"));
            if (Dm.truthy(sound_to_play))
                playsound(pawn, sound_to_play, 80, vary: TRUE);
        }

        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
