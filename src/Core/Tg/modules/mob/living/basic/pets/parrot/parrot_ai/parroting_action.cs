// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2; its text-to-speech voice since D310).

/// When a parrot... parrots... it occasionally asks for a fresh phrase to repeat, then squawks it (sometimes over the radio).
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm:6
public partial class datum_bt_node_ai_behavior_parrot_repeat_speech : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm:1  #define MAXIMUM_PARROT_PITCH 24 (file-local)
    // DEVIATION(DV355): at most 4 semitones up, not 24: with Poly's slower filter, higher garbles the synthesis (see poly.cs)
    private const float MAXIMUM_PARROT_PITCH = 4;

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_parrot speaking_pawn = controller.pawn as mob_living_basic_parrot;

        object switch_up_probability = controller.blackboard[BB_PARROT_PHRASE_CHANGE_PROBABILITY];
        if (prob(Dm.num(switch_up_probability)) || controller.blackboard[BB_PARROT_REPEAT_STRING] == null)
        {
            object phrase_result = !Dm.truthy(speaking_pawn._listen_lookup?[COMSIG_NEEDS_NEW_PHRASE]) ? NONE : speaking_pawn._SendSignal(COMSIG_NEEDS_NEW_PHRASE, Dm.list(speaking_pawn));
            if (Dm.truthy(Dm.bitand(phrase_result, NO_NEW_PHRASE_AVAILABLE)))
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        if (!prob(Dm.num(controller.blackboard[BB_PARROT_REPEAT_PROBABILITY])))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        DmList speech = controller.blackboard[BB_PARROT_REPEAT_STRING] as DmList;
        if (speech == null)
        {
            stack_trace("Parrot repeat speech somehow got a null phrase while not getting `NO_NEW_PHRASE_AVAILABLE`!", "code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm", 19);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        }

        DmList available_channels = speaking_pawn.get_available_channels();
        string modified_speech = Dm.str(speech["line"]);
        bool use_radio = prob(50); // we might not even use the radio if we even have a channel

        bool has_channel_prefix = Dm.@in(copytext_char(modified_speech, 1, 2), GLOB.department_radio_prefixes) && Dm.@in(copytext_char(modified_speech, 2, 3), GLOB.department_radio_keys); // determine if we need to crop the channel prefix

        if (!Dm.truthy(length(available_channels))) // might not even use the radio at all
        {
            if (has_channel_prefix)
                modified_speech = copytext_char(modified_speech, 3);
        }
        else
        {
            if (has_channel_prefix)
                modified_speech = Dm.text("[][]", use_radio ? pick(available_channels) : "", copytext_char(modified_speech, 3));
            else
                modified_speech = Dm.text("[][]", use_radio ? pick(available_channels) : "", modified_speech);
        }

        if (Dm.truthy(SStts?.tts_enabled))
        {
            remember_own_voice(controller);
            modify_voice(speaking_pawn, speech);
        }
        Waiting.Detach(speaking_pawn.say(modified_speech, forced: "AI Controller"));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm:42
    public virtual void modify_voice(mob_living speaking_pawn = null, DmList speech = null)
    {
        if (Dm.truthy(SStts.available_speakers.Find(speech["voice"])))
            speaking_pawn.voice = speech["voice"];
        if (Dm.truthy(speech["pitch"]) && Dm.truthy(SStts.pitch_enabled))
            speaking_pawn.pitch = Math.Min(Dm.num(speech["pitch"]) + Dm.num(rand(6, 12)), MAXIMUM_PARROT_PITCH);
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parroting_action.dm:48
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (!Dm.truthy(succeeded))
            return;
        var living_pawn = controller.pawn as mob_living;
        // DEVIATION(DV264): the voice the parrot had before it repeated someone, not its type's initial one (tg's living_pawn::voice):
        // Poly's voice and pitch are set in her Initialize, so tg's reset leaves her voiceless after her first repeated line
        if (controller.blackboard[BB_PARROT_OWN_VOICE] is DmList own)
        {
            controller.clear_blackboard_key(BB_PARROT_OWN_VOICE);
            living_pawn.voice = own[1];
            living_pawn.pitch = Dm.num(own[2]);
        }
        else
        {
            living_pawn.voice = Dm.initial(living_pawn, (mob_living p) => p.voice);
            living_pawn.pitch = Dm.initial(living_pawn, (mob_living p) => p.pitch);
        }
    }

    /// DV264: the parrot's own voice and pitch, kept on its blackboard while it repeats someone else's (the behavior is shared by every
    /// parrot); the first repeat of a run keeps it, finish_action gives it back.
    public const string BB_PARROT_OWN_VOICE = "port_parrot_own_voice";

    public virtual void remember_own_voice(datum_ai_controller controller = null)
    {
        if (controller.blackboard[BB_PARROT_OWN_VOICE] == null && controller.pawn is mob_living pawn)
            controller.set_blackboard_key(BB_PARROT_OWN_VOICE, Dm.list(pawn.voice, pawn.pitch));
    }
}
