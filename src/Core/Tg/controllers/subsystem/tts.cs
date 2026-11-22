// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/tts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (D307, D308): Initialize, queue_tts_message, play_tts, the radio's half of fire, clear_radio_message, random_tts_voice and
// can_hear_radio_tts. DEVIATION(DV262): tg's subsystem asks a TTS server for each line's audio (HTTP requests, a heap of queued requests,
// per-speaker queues timed by the audio's length) and plays the files; the port's clients synthesize the lines themselves (D16), so a
// line goes to its listeners' clients as text, voice, pitch and filters at once and each client keeps a speaker's lines in order. A
// radio broadcast waits, as in tg, until its line is "completed" (here: queued), then plays from each radio that receives it. The blips
// are not translated: a listener who does not know the language hears the line scrambled as their chat shows it.

// tg: code/controllers/subsystem/tts.dm:4 SUBSYSTEM_DEF(tts)
public partial class datum_controller_subsystem_tts : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/tts.dm:1-2  #define TTS_REQUEST_REF, TTS_REQUEST_EXPIRE (file-local, #undef at the end)
    private const string TTS_REQUEST_REF = "ref";
    private const string TTS_REQUEST_EXPIRE = "expiry_time";

    /// DEVIATION(DV262): tg's speakers are the TTS server's (/tts-voices, VCTK's); the port's are the client model's ten, named so that
    /// random_tts_voice tells them by "Man" and "Woman" (godot/scripts/Shared/Speech/SpeechModels.cs SpeechVoices, the same order).
    public static readonly System.Collections.Immutable.ImmutableArray<string> PortSpeakers =
    [
        "Woman 1", "Woman 2", "Woman 3", "Woman 4", "Woman 5", "Man 1", "Man 2", "Man 3", "Man 4", "Man 5",
    ];

    // The New() that SUBSYSTEM_DEF(tts) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SStts) (__DEFINES/MC.dm:29) expanded inline.
    // tg: code/controllers/subsystem/tts.dm:4
    public override async DmTask New(params object[] __extra)
    {
        if (SStts != this)
        {
            if (SStts is datum_controller_subsystem_tts)
            {
                Recover();
                qdel(SStts);
            }
            SStts = this;
        }
        // DEVIATION(DV262): no HTTP requests to poll, so the subsystem never fires
        Dm.combine(ref ss_flags, SS_NO_FIRE);
        PreInit();
    }

    // tg: code/controllers/subsystem/tts.dm:117
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // tg: :118-125  the TTS server's address in the config and establish_connection_to_tts (:72): DEVIATION(DV262) the voices are
        // the clients' own, so the subsystem is always on and pitch is always available
        available_speakers = Dm.list(System.Linq.Enumerable.ToArray<object>(PortSpeakers));
        tts_enabled = TRUE;
        completed_tts_messages = Dm.list();
        queued_radio_messages = Dm.list();
        queued_radio_messages_compression = Dm.list();
        tram_voice = pick(available_speakers);
        computer_voice = pick(available_speakers);
        pitch_enabled = TRUE;
        return SS_INIT_SUCCESS;
    }

    /// D312: the port's special filters of a whispered and a shouted line (the clients voice them, DV262).
    public const string TTS_FILTER_WHISPER = "whisper", TTS_FILTER_YELL = "yell";

    // tg: code/controllers/subsystem/tts.dm:126  play_tts(target, listeners, audio, audio_blips, language, range, volume_offset,
    // ignore_observers, source_speaker, audio_length, audio_length_blips, volume_preference, volume_signal): the audio is the line's text,
    // voice and pitch (DV262); the volume preference is the client's own (Settings, Speech).
    // The filter, the special filters and the radio's frequency travel with the line for the client (D308).
    public virtual void play_tts(object target = null, DmList listeners = null, string message = null, object language = null, float range = 7, float volume_offset = 0, float ignore_observers = 0, object source_speaker = null, object speaker = null, float pitch = 0, string filter = null, string special_filters = null, float radio_frequency = 0)
    {
        atom actual_target = (target as datum_weakref)?.resolve() as atom;
        turf turf_source = null;
        if (actual_target != null)
            turf_source = get_turf(actual_target);
        if (turf_source == null && actual_target != null) // if there's a target, we better have a turf
            return;

        // tg: :134  var/channel = SSsounds.random_available_channel(): the client plays each line on its own voice
        DmList final_listeners = listeners;
        if (!Dm.truthy(ignore_observers) && actual_target != null)
            Dm.append(ref final_listeners, datum_controller_subsystem_mobs.dead_players_by_zlevel[turf_source.z]); //observers always hear through walls
        foreach (object hearer in Dm.each(final_listeners))
        {
            if (hearer == null)
                continue;
            atom_movable hearer_atom;
            if (hearer is datum_weakref weakref)
                hearer_atom = weakref.resolve() as atom_movable;
            else
                hearer_atom = hearer as atom_movable;
            if (hearer_atom == null || QDELING(hearer_atom))
                continue;
            if (!ismob(hearer_atom))
                continue;
            mob listening_mob = hearer_atom.get_listening_mob() as mob;
            if (listening_mob?.client == null)
                continue;
            // tg: :154-162  the volume, sound_tts and hear-self-radio preferences: the client's own settings (DV262)

            float sound_volume = (listening_mob == actual_target ? 60 : 85) + volume_offset;
            // tg: :165-170  the language: a listener who does not understand it hears blips; the port voices the line as their chat
            // shows it (translate_language scrambles it, DV262)
            string heard = listening_mob.translate_language(actual_target as atom_movable, language, message) as string;
            bool own = listening_mob == source_speaker; // tg: :160 the hear-self-radio preference, the client's (DV262)
            // D310: playsound_local's own checks (game/sound/sound.dm): a deaf listener hears nothing; with pressure_affected the air's
            // pressure at the listener and the speaker scales the volume, at least 0.15 within a tile (the distance falloff is the client's)
            if (HAS_TRAIT(listening_mob, TRAIT_DEAF))
                continue;
            if (actual_target != null && get_dist(listening_mob, turf_source) <= range)
            {
                turf turf_loc = get_turf(listening_mob);
                float pressure_factor = 1;
                datum_gas_mixture hearer_env = Waiting.NoSleep(turf_loc.return_air()) as datum_gas_mixture;
                datum_gas_mixture source_env = Waiting.NoSleep(turf_source.return_air()) as datum_gas_mixture;
                if (Dm.truthy(hearer_env) && Dm.truthy(source_env))
                {
                    float pressure = Dm.num(min(hearer_env.return_pressure(), source_env.return_pressure()));
                    if (pressure < ONE_ATMOSPHERE)
                        pressure_factor = max(Dm.div(pressure - SOUND_MINIMUM_PRESSURE, ONE_ATMOSPHERE - SOUND_MINIMUM_PRESSURE), 0f);
                }
                else //space
                    pressure_factor = 0;
                if (Dm.num(get_dist_euclidean(turf_loc, turf_source)) <= 1)
                    pressure_factor = max(pressure_factor, 0.15f); //touching the source of the sound
                sound_volume *= pressure_factor;
                if (sound_volume <= 0)
                    continue; // tg plays it silent
                DmWorldState.Current.Sinks.Messages.Tts(listening_mob.client, actual_target, heard, speaker as string, pitch, sound_volume, filter, special_filters, radio_frequency, own);
            }
            else if (actual_target == null)
                DmWorldState.Current.Sinks.Messages.Tts(listening_mob.client, null, heard, speaker as string, pitch, sound_volume, filter, special_filters, radio_frequency, own);
        }
        // tg: :204-226  the threed_sound datums that move the playing sound with its speaker: the client keeps the voice on the speaker
    }

    // tg: code/controllers/subsystem/tts.dm:409
    public virtual void queue_tts_message(datum target = null, object message = null, object language = null, object speaker = null, object filter = null, DmList listeners = null, float local = 0, float message_range = 7, float volume_offset = 0, float pitch = 0, object special_filters = null, object blip_base = null, object blip_number = null, float force_blips = 0, object identifier = null)
    {
        if (!Dm.truthy(tts_enabled))
            return;

        // tg: :413-415  the tmp/tts folder for the audio files: none (DV262)

        // If there is no alphanumeric char, the output will usually be static, so
        // don't bother sending
        // DEVIATION(DV262): a letter or digit of any script (tg's regex [a-zA-Z0-9]: its server voices English only)
        string text = message as string;
        if (string.IsNullOrEmpty(text) || !System.Linq.Enumerable.Any(text, char.IsLetterOrDigit))
            return;

        string shell_scrubbed_input = tts_speech_filter(text);
        if (!Dm.@in(speaker, available_speakers))
            return;
        // tg: :424-449  the HTTP requests and the speaker's queue (DV262): the line goes out at once, each client keeps a speaker's lines
        // in order. tg's local preview is SEND_SOUND to the target (a client) when the audio is ready (:353-361).
        if (Dm.truthy(local))
        {
            DmWorldState.Current.Sinks.Messages.Tts(target, null, shell_scrubbed_input, speaker as string, pitch, 100, filter as string, special_filters as string);
            return;
        }
        play_tts(WEAKREF(target), listeners, shell_scrubbed_input, language, message_range, volume_offset, FALSE, null, speaker, pitch, filter as string, special_filters as string);
        // tg: :385-388  the played line is completed for 30 s, which lets its radio broadcasts play (TTS_REQUEST_REF holds the request; the
        // port keeps the line's own data, DV262)
        if (identifier is string id && id != "invalid")
        {
            completed_tts_messages[id] = Dm.list(
                Dm.kv(TTS_REQUEST_REF, Dm.list(Dm.kv("target", target), Dm.kv("message", shell_scrubbed_input), Dm.kv("language", language), Dm.kv("speaker", speaker),
                    Dm.kv("filter", filter), Dm.kv("special_filters", special_filters), Dm.kv("pitch", pitch), Dm.kv("volume_offset", volume_offset))),
                Dm.kv(TTS_REQUEST_EXPIRE, world.time + 30 * SECONDS));
            play_queued_radio();
        }
    }

    // tg: code/controllers/subsystem/tts.dm:396-437  the radio's half of fire: each queued broadcast whose line is completed plays from every
    // radio that received it to its hearers, garbled past the compression threshold; the completed lines expire after 30 s. DEVIATION(DV262):
    // called when a line is completed or a broadcast is queued, not every tick (the subsystem has no requests to wait on).
    public virtual void play_queued_radio()
    {
        foreach (object identifier in Dm.each(queued_radio_messages?.Copy()))
        {
            DmList radio_list = queued_radio_messages[identifier] as DmList;
            if (!Dm.truthy(completed_tts_messages?[identifier]))
                continue;

            DmList tts_request = (completed_tts_messages[identifier] as DmList)[TTS_REQUEST_REF] as DmList;
            foreach (object radio in Dm.each(radio_list?.Copy()))
            {
                DmList hearer_list = radio_list[radio] as DmList;
                if (radio is datum_weakref weakref)
                {
                    obj radio_obj = weakref.resolve() as obj;
                    if (QDELETED(radio_obj))
                    {
                        radio_list.Remove(radio);
                        continue;
                    }
                }

                // the radio gibberish audio: the line garbled as the radio's chat garbles it (broadcasting.dm's Gibberish)
                string message = tts_request["message"] as string;
                if (Dm.gt(queued_radio_messages_compression[identifier], COMPRESSION_REPLACE_CHARACTER_THRESHOLD))
                    message = Gibberish(message, TRUE) as string;
                // D312: the radio carries the words, not the whisper they were said in around the speaker (tg whispers a headset line)
                string specials = tts_request["special_filters"] as string;
                float volume_offset = Dm.num(tts_request["volume_offset"]);
                if (specials != null && specials.Split('|').Contains(TTS_FILTER_WHISPER))
                {
                    specials = string.Join("|", specials.Split('|').Where(f => f != TTS_FILTER_WHISPER));
                    volume_offset = 0;
                }
                play_tts(
                    target: Dm.eq(radio, TTS_GHOST_RADIO) ? null : radio,
                    listeners: hearer_list,
                    message: message,
                    language: tts_request["language"],
                    range: INFINITY,
                    volume_offset: volume_offset,
                    ignore_observers: TRUE,
                    source_speaker: (tts_request["target"] as datum_weakref)?.resolve() ?? tts_request["target"],
                    speaker: tts_request["speaker"],
                    pitch: Dm.num(tts_request["pitch"]),
                    filter: tts_request["filter"] as string,
                    special_filters: specials,
                    radio_frequency: Dm.num(queued_radio_messages_frequency[identifier]));
            }

            clear_radio_message(identifier);
        }

        foreach (object identifier in Dm.each(completed_tts_messages?.Copy()))
        {
            if (Dm.lt(world.time, (completed_tts_messages[identifier] as DmList)[TTS_REQUEST_EXPIRE]))
                continue;

            clear_radio_message(identifier);
        }
    }

    // tg: code/controllers/subsystem/tts.dm:495
    public virtual void clear_radio_message(object identifier = null)
    {
        (completed_tts_messages[identifier] as DmList)?.Remove(TTS_REQUEST_REF); // don't stick around
        completed_tts_messages.Remove(identifier);
        queued_radio_messages.Remove(identifier);
        queued_radio_messages_compression.Remove(identifier);
        queued_radio_messages_frequency.Remove(identifier);
    }

    /// The frequency of each queued broadcast (D308: the client's departmental-radio option reads it; tg's broadcasting checks the
    /// preference itself, can_hear_radio_tts).
    public DmList queued_radio_messages_frequency = Dm.list();

    /// Helper to get a random TTS voice for a certain gender. Passing no gender just results in a random voice.
    // tg: code/controllers/subsystem/tts.dm:502
    public virtual object random_tts_voice(object gender = null)
    {
        gender ??= NEUTER;
        if (!Dm.truthy(tts_enabled))
            return null;

        float sanity = 0;
        while (sanity < 10)
        {
            object voice = pick(available_speakers);
            // tg's findtext checks (:509-514): fits_gender (DV262: DM's findtext ignores case, so "Man" is in "Woman 1" too)
            if (fits_gender(voice, gender))
                return voice;
            sanity += 1;
        }

        return pick(available_speakers); // failsafe
    }

    /// D311: a man's voice for a male, a woman's for a female, any for another gender (the port's voice names, PortSpeakers).
    public static bool fits_gender(object voice, object gender)
    {
        if (Dm.eq(gender, MALE))
            return voice is string v && v.StartsWith("Man ", StringComparison.Ordinal);
        if (Dm.eq(gender, FEMALE))
            return voice is string w && w.StartsWith("Woman ", StringComparison.Ordinal);
        return true;
    }
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_tts SStts => ref TgState.Current.SStts;

    /**
     * Checks if the passed mob can hear radio TTS
     *
     * * hearer - The mob to check if they can hear radio TTS
     * * radio_frequency - The frequency of the radio TTS message
     */
    // tg: code/controllers/subsystem/tts.dm:668
    public static object can_hear_radio_tts(mob hearer = null, object radio_frequency = null)
    {
        if (!Dm.truthy(SStts?.tts_enabled))
            return FALSE;

        if (HAS_TRAIT(hearer, TRAIT_DEAF) || IS_UNCONSCIOUS(hearer))
            return FALSE;

        // tg: :675-686  the sound_tts and sound_tts_radio preferences (off, departmental only): the client's Settings > Speech (DV262)
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/tts.dm:4  SUBSYSTEM_DEF(tts): GLOBAL_REAL(SStts, /datum/controller/subsystem/tts)
    internal datum_controller_subsystem_tts SStts;
}
