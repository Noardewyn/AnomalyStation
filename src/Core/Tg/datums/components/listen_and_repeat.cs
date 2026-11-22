// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/listen_and_repeat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// Simple element that will deterministically set a value based on stuff that the source has heard and will then compel the source to repeat it.
/// Requires a valid AI Blackboard.
// tg: code/datums/components/listen_and_repeat.dm:20
public partial class datum_component_listen_and_repeat : datum_component
{
    /// The maximum number of phrases we can store in our speech buffer
    // tg: code/datums/components/listen_and_repeat.dm:2
    private const int MAX_SPEECH_BUFFER_SIZE = 500;
    /// Tendency we have to ignore radio chatter
    // tg: code/datums/components/listen_and_repeat.dm:4
    private const int RADIO_IGNORE_CHANCE = 10;
    /// The line we will re-iterate
    // tg: code/datums/components/listen_and_repeat.dm:6
    private const string MESSAGE_LINE = "line";
    /// the tts voice it should be said in
    // tg: code/datums/components/listen_and_repeat.dm:8
    private const string MESSAGE_VOICE = "voice";
    /// the tone it should be said in
    // tg: code/datums/components/listen_and_repeat.dm:10
    private const string MESSAGE_PITCH = "pitch";
    /// the blip base it should be said in
    // tg: code/datums/components/listen_and_repeat.dm:12
    private const string MESSAGE_BLIP_BASE = "blip_base";
    /// the blip base it should be said in
    // tg: code/datums/components/listen_and_repeat.dm:14
    private const string MESSAGE_BLIP_NUMBER = "blip_number";

    /// list we give speech that doesnt have a voice or a pitch
    // tg: code/datums/components/listen_and_repeat.dm:28
    private static DmList invalid_voice => TgState.Current.datum_component_listen_and_repeat__invalid_voice ??= Dm.list(Dm.kv(MESSAGE_VOICE, "invalid"), Dm.kv(MESSAGE_PITCH, 0), Dm.kv(MESSAGE_BLIP_BASE, "male"), Dm.kv(MESSAGE_BLIP_NUMBER, 1));

    // tg: code/datums/components/listen_and_repeat.dm:35
    [DmParams("desired_phrases", "blackboard_key")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList desired_phrases = Dm.arg<DmList>(__extra, 0);
        object blackboard_key = Dm.arg(__extra, 1);
        await base.Initialize(__extra);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        foreach (object speech in Dm.each<object>(desired_phrases))
        {
            DmList voiced = desired_phrases[speech] as DmList;
            if (voiced == null || !Dm.truthy(voiced[MESSAGE_VOICE]) || !Dm.truthy(voiced[MESSAGE_PITCH]))
            {
                LAZYSET(ref speech_buffer, speech, invalid_voice);
                continue;
            }
            LAZYSET(ref speech_buffer, speech, voiced);
        }

        this.blackboard_key = blackboard_key;

        RegisterSignal(parent, COMSIG_MOVABLE_PRE_HEAR, nameof(on_hear));
        RegisterSignal(parent, COMSIG_NEEDS_NEW_PHRASE, nameof(set_new_blackboard_phrase));
        RegisterSignal(parent, COMSIG_LIVING_WRITE_MEMORY, nameof(on_write_memory));

        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return null;
    }

    // tg: code/datums/components/listen_and_repeat.dm:54
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return base.Destroy(force);
    }

    /// Called when we hear something.
    // tg: code/datums/components/listen_and_repeat.dm:59
    [SignalHandler]
    public virtual void on_hear(datum source = null, DmList passed_arguments = null)
    {
        object message = passed_arguments[HEARING_RAW_MESSAGE];
        object speaker = passed_arguments[HEARING_SPEAKER];
        bool over_radio = Dm.truthy(passed_arguments[HEARING_RADIO_FREQ]);
        if (speaker == source) // don't parrot ourselves
            return;

        DmList speaker_sound;

        if (!Dm.truthy(SStts?.tts_enabled) || speaker is not atom_movable movable_speaker)
            speaker_sound = invalid_voice;
        else
        {
            speaker_sound = Dm.list();
            speaker_sound[MESSAGE_VOICE] = Dm.truthy(movable_speaker.voice) ? movable_speaker.voice : "invalid";
            speaker_sound[MESSAGE_PITCH] = Dm.truthy(movable_speaker.pitch) && Dm.truthy(SStts.pitch_enabled) ? movable_speaker.pitch : 0;
            speaker_sound[MESSAGE_BLIP_BASE] = Dm.truthy(movable_speaker.blip_base) ? movable_speaker.blip_base : "male";
            speaker_sound[MESSAGE_BLIP_NUMBER] = Dm.truthy(movable_speaker.blip_number) ? movable_speaker.blip_number : 1;
        }

        if (over_radio && prob(RADIO_IGNORE_CHANCE))
            return;

        float number_of_excess_strings = LAZYLEN(speech_buffer) - MAX_SPEECH_BUFFER_SIZE;
        if (number_of_excess_strings > 0) // only remove if we're overfull
        {
            for (int i = 1; i <= number_of_excess_strings; i++)
                LAZYREMOVE(ref speech_buffer, pick(speech_buffer));
        }

        LAZYSET(ref speech_buffer, html_decode(message), speaker_sound);
    }

    /// Called to set a new value for the blackboard key.
    // tg: code/datums/components/listen_and_repeat.dm:91
    [SignalHandler]
    public virtual object set_new_blackboard_phrase(datum source = null)
    {
        atom_movable atom_source = source as atom_movable;
        datum_ai_controller controller = atom_source.ai_controller as datum_ai_controller;
        if (!Dm.truthy(LAZYLEN(speech_buffer)))
        {
            controller.clear_blackboard_key(blackboard_key);
            return NO_NEW_PHRASE_AVAILABLE;
        }

        object selected_phrase = pick(speech_buffer);
        DmList to_return = Dm.list(Dm.kv(MESSAGE_LINE, selected_phrase));

        if (speech_buffer[selected_phrase] is DmList phrase_voice)
        {
            to_return[MESSAGE_VOICE] = phrase_voice[MESSAGE_VOICE];
            to_return[MESSAGE_PITCH] = phrase_voice[MESSAGE_PITCH];
            to_return[MESSAGE_BLIP_BASE] = phrase_voice[MESSAGE_BLIP_BASE];
            to_return[MESSAGE_BLIP_NUMBER] = phrase_voice[MESSAGE_BLIP_NUMBER];
        }

        controller.override_blackboard_key(blackboard_key, to_return);
        return null;
    }

    /// Exports all the speech buffer data to a dedicated blackboard key on the source.
    // tg: code/datums/components/listen_and_repeat.dm:112
    [SignalHandler]
    public virtual void on_write_memory(datum source = null, object dead = null, object gibbed = null)
    {
        atom_movable atom_source = source as atom_movable;
        datum_ai_controller controller = atom_source.ai_controller as datum_ai_controller;
        if (!Dm.truthy(LAZYLEN(speech_buffer))) // what? well whatever let's just move on
            return;

        controller.override_blackboard_key(BB_EXPORTABLE_STRING_BUFFER_LIST, speech_buffer.Copy());
    }
}

public sealed partial class TgState
{
    internal DmList datum_component_listen_and_repeat__invalid_voice;
}
