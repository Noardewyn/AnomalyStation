// Translated from tgstation (snapshot 1d99bd7f): code/game/sound/sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1; M10 slice F: /client/proc/playtitlemusic, the lobby music). SEND_SOUND is Dm.output_to (rules 12), which hands the sound to the world's ISoundSink
// (presentation-seam.md section 4). tg's own quirk is kept: playsound passes 14 arguments to playsound_local, so its own
// min_volume (3) never reaches it and playsound_local runs with its default 5.
// The signatures differ from slots.tsv in three places (Appendix A): playsound_local's turf_source is atom (callers pass a
// mob or an obj, sound.dm:190, janitor_key.dm:55, and the proc tests isturf), and the volume_preference of playsound and
// sound_to_playing_players is object (a preference typepath, radio.dm:739, security_level.dm:23).

public static partial class GlobalProcs
{
    /**
     * playsound is a proc used to play a 3D sound in a specific range. This uses SOUND_RANGE + extra_range to determine that.
     *
     * Arguments:
     * * source - Origin of sound.
     * * soundin - Either a file, or a string that can be used to get an SFX.
     * * vol - The volume of the sound, excluding falloff and pressure affection.
     * * vary - bool that determines if the sound changes pitch every time it plays.
     * * extrarange - modifier for sound range. This gets added on top of SOUND_RANGE.
     * * falloff_exponent - Rate of falloff for the audio. Higher means quicker drop to low volume. Should generally be over 1 to indicate a quick dive to 0 rather than a slow dive.
     * * frequency - playback speed of audio.
     * * channel - The channel the sound is played at.
     * * pressure_affected - Whether or not difference in pressure affects the sound (E.g. if you can hear in space).
     * * ignore_walls - Whether or not the sound can pass through walls.
     * * falloff_distance - Distance at which falloff begins. Sound is at peak volume (in regards to falloff) aslong as it is in this range.
     * * volume_preference - Optional: Will be checked to modify the volume of the sound for each listener.
     * * min_volume - minimum volume the sound can reach at max_range.
     */
    // tg: code/game/sound/sound.dm:19
    public static object playsound(atom source = null, object soundin = null, object vol = null, object vary = null, object extrarange = null, float? falloff_exponent = null, object frequency = null, float? channel = null, float? pressure_affected = null, float? ignore_walls = null, float? falloff_distance = null, float? use_reverb = null, object volume_preference = null, float? min_volume = null)
    {
        falloff_exponent ??= SOUND_FALLOFF_EXPONENT;
        channel ??= 0;
        pressure_affected ??= TRUE;
        ignore_walls ??= TRUE;
        falloff_distance ??= SOUND_DEFAULT_FALLOFF_DISTANCE;
        use_reverb ??= TRUE;
        min_volume ??= 3;
        if (isarea(source))
            throw Dm.CRASH("playsound(): source is an area", "code/game/sound/sound.dm", 21);

        if (islist(soundin))
            throw Dm.CRASH("playsound(): soundin attempted to pass a list! Consider using pick()", "code/game/sound/sound.dm", 24);

        if (!Dm.truthy(soundin))
            throw Dm.CRASH("playsound(): no soundin passed", "code/game/sound/sound.dm", 27);


        turf turf_source = get_turf(source);
        if (!Dm.truthy(turf_source))
            return null;

        //allocate a channel if necessary now so its the same for everyone
        channel = Dm.truthy(channel) ? channel : Dm.num_or_null(SSsounds.random_available_channel());

        // isdatum(soundin): BYOND's /sound is a /datum (OpenDream DMStandard/Types/Sound.dm: parent_type = /datum); the port's
        // sound is the shim's builtin type, outside tg's datum class tree, so the test also asks for it.
        sound S = (isdatum(soundin) || soundin is sound) ? (sound)soundin : sound(get_sfx(soundin));
        float maxdistance = SOUND_RANGE + Dm.num(extrarange);
        float source_z = turf_source.z;

        if (Dm.num(falloff_distance) >= maxdistance)
            throw Dm.CRASH("playsound(): falloff_distance is equal to or higher than maxdistance! Bump up extrarange or reduce the falloff_distance.", "code/game/sound/sound.dm", 42);

        if (Dm.truthy(vary) && !Dm.truthy(frequency))
            frequency = get_rand_frequency(); // skips us having to do it per-sound later. should just make this a macro tbh

        DmList listeners = null;

        turf above_turf = GET_TURF_ABOVE(turf_source) as turf;
        turf below_turf = GET_TURF_BELOW(turf_source) as turf;

        // tg passes a fourth argument (TRUE) to get_hearers_in_range and get_hearers_in_view, which declare three: dropped (rules 7.3)
        if (Dm.truthy(ignore_walls))
        {
            listeners = get_hearers_in_range(maxdistance, turf_source, RECURSIVE_CONTENTS_CLIENT_MOBS) as DmList;
            if (Dm.truthy(above_turf) && istransparentturf(above_turf))
                Dm.append(ref listeners, get_hearers_in_range(maxdistance, above_turf, RECURSIVE_CONTENTS_CLIENT_MOBS));

            if (Dm.truthy(below_turf) && istransparentturf(turf_source))
                Dm.append(ref listeners, get_hearers_in_range(maxdistance, below_turf, RECURSIVE_CONTENTS_CLIENT_MOBS));
        }

        else //these sounds don't carry through walls
        {
            listeners = get_hearers_in_view(maxdistance, turf_source, RECURSIVE_CONTENTS_CLIENT_MOBS) as DmList;

            if (Dm.truthy(above_turf) && istransparentturf(above_turf))
                Dm.append(ref listeners, get_hearers_in_view(maxdistance, above_turf, RECURSIVE_CONTENTS_CLIENT_MOBS));

            if (Dm.truthy(below_turf) && istransparentturf(turf_source))
                Dm.append(ref listeners, get_hearers_in_view(maxdistance, below_turf, RECURSIVE_CONTENTS_CLIENT_MOBS));
            foreach (mob listening_ghost in Dm.each_cast<mob>(datum_controller_subsystem_mobs.dead_players_by_zlevel[source_z]))
                Dm.append(ref listeners, listening_ghost);
        }

        foreach (mob listening_mob in Dm.each<mob>(listeners))//had nulls sneak in here, hence the typecheck
        {
            turf mob_turf = get_turf(listening_mob);
            if (!Dm.truthy(mob_turf))
                continue;
            if (Dm.num(get_dist_euclidean(mob_turf, turf_source)) <= maxdistance)
                listening_mob.playsound_local(turf_source, soundin, vol, vary, frequency, falloff_exponent, channel, pressure_affected, S, maxdistance, falloff_distance, 1, use_reverb, volume_preference);
        }

        return listeners;
    }

    // tg: code/game/sound/sound.dm:184
    public static void sound_to_playing_players(object soundin = null, float? volume = null, float? vary = null, float? frequency = null, float? channel = null, float? pressure_affected = null, sound S = null, object volume_preference = null)
    {
        volume ??= 100;
        vary ??= FALSE;
        frequency ??= 0;
        channel ??= 0;
        pressure_affected ??= FALSE;
        if (S == null)
            S = sound(get_sfx(soundin));
        foreach (object m in Dm.each(GLOB.player_list))
        {
            if (ismob(m) && !isnewplayer(m))
            {
                mob M = (mob)m;
                M.playsound_local(M, null, volume, vary, frequency, null, channel, pressure_affected, S, volume_preference: volume_preference);
            }
        }
    }

    ///get a random frequency.
    // tg: code/game/sound/sound.dm:209
    public static object get_rand_frequency()
    {
        return rand(32000, 55000);
    }

    ///get_rand_frequency but lower range.
    // tg: code/game/sound/sound.dm:213
    public static object get_rand_frequency_low_range()
    {
        return rand(38000, 45000);
    }

    ///Used to convert a SFX define into a .ogg so we can add some variance to sounds. If soundin is already a .ogg, we simply return it
    // tg: code/game/sound/sound.dm:217
    public static object get_sfx(object soundin = null)
    {
        if (!istext(soundin))
            return soundin;
        datum_sound_effect sfx = GLOB.sfx_datum_by_key[soundin] as datum_sound_effect;
        object picked = sfx?.return_sfx();
        return Dm.truthy(picked) ? picked : soundin;
    }


    /**
     * Creates a soundtoken datum (a sound that updates for movement).
     * allowed_listeners is an optional list of mobs that are the only ones that can hear this sound ever.
     * sound_length is an optional length of the sound. Things like TTS need to pass this since we can't dynamically grab the length in that case.
     */
    // tg: code/game/sound/sound.dm:229
    // The token is /datum/sound_token (datums/sound_token.cs, M4b audio slice 5); the translated caller is a token loop's end
    // sound (/datum/looping_sound/play with delete_when_finished); the emotes and the tongue are not translated yet.
    public static object playsoundtoken(atom source = null, object soundin = null, object volume = null, object range = null, float? falloff_exponent = null, float? falloff_distance = null, object allowed_listeners = null, object sound_length = null)
    {
        falloff_exponent ??= SOUND_FALLOFF_EXPONENT;
        falloff_distance ??= SOUND_DEFAULT_FALLOFF_DISTANCE;
        return Dm.@new<datum_sound_token>(source, soundin, range, volume, falloff_exponent, falloff_distance, allowed_listeners, sound_length, Dm.named("_delete_on_end", TRUE));
    }
}

// tg: code/game/sound/sound.dm:101  (/mob procs defined in this file, rules 1.9 "interleaved types")
public partial class mob : atom_movable
{
    /**
     * Plays a sound with a specific point of origin for src mob
     * Affected by pressure, distance, terrain and environment (see arguments)
     *
     * Arguments:
     * * turf_source - The turf our sound originates from, if this is not a turf, the sound is played with no spatial audio
     * * soundin - Either a file, or a string that can be used to get an SFX.
     * * vol - The volume of the sound, excluding falloff and pressure affection.
     * * vary - bool that determines if the sound changes pitch every time it plays.
     * * frequency - playback speed of audio.
     * * falloff_exponent - Rate of falloff for the audio. Higher means quicker drop to low volume. Should generally be over 1 to indicate a quick dive to 0 rather than a slow dive.
     * * channel - Optional: The channel the sound is played at.
     * * pressure_affected - bool Whether or not difference in pressure affects the sound (E.g. if you can hear in space).
     * * sound_to_use - Optional: Will default to soundin when absent
     * * max_distance - number, determines the maximum distance of our sound
     * * falloff_distance - Distance at which falloff begins. Sound is at peak volume (in regards to falloff) aslong as it is in this range.
     * * distance_multiplier - Default 1, multiplies the maximum distance of our sound
     * * use_reverb - bool default TRUE, determines if our sound has reverb
     * * volume_preference - Optional: Will be checked to modify the volume of the sound.
     * * min_volume - minimum volume the sound can reach at max_range.
     */
    // tg: code/game/sound/sound.dm:101
    public virtual object playsound_local(atom turf_source = null, object soundin = null, object vol = null, object vary = null, object frequency = null, float? falloff_exponent = null, float? channel = null, float? pressure_affected = null, sound sound_to_use = null, object max_distance = null, float? falloff_distance = null, float? distance_multiplier = null, float? use_reverb = null, object volume_preference = null, float? min_volume = null)
    {
        falloff_exponent ??= SOUND_FALLOFF_EXPONENT;
        channel ??= 0;
        pressure_affected ??= TRUE;
        falloff_distance ??= SOUND_DEFAULT_FALLOFF_DISTANCE;
        distance_multiplier ??= 1;
        use_reverb ??= TRUE;
        min_volume ??= 5;
        if (!Dm.truthy(client) || HAS_TRAIT(this, TRAIT_DEAF))
            return null;

        if (sound_to_use == null)
            sound_to_use = sound(get_sfx(soundin));

        sound_to_use.wait = 0; //No queue
        sound_to_use.channel = Dm.truthy(channel) ? Dm.num(channel) : Dm.num(SSsounds.random_available_channel());
        sound_to_use.volume = Dm.num(vol);

        if (Dm.truthy(vary))
        {
            if (Dm.truthy(frequency))
                sound_to_use.frequency = Dm.num(frequency);
            else
                sound_to_use.frequency = Dm.num(get_rand_frequency());
        }

        float distance = 0;

        if (isturf(turf_source))
        {
            turf turf_loc = get_turf(this);

            //sound volume falloff with distance
            distance = Dm.num(get_dist_euclidean(turf_loc, turf_source)) * Dm.num(distance_multiplier);

            if (Dm.truthy(max_distance)) //If theres no max_distance we're not a 3D sound, so no falloff.
                sound_to_use.volume -= CALCULATE_SOUND_VOLUME_RATIO(vol, distance, max_distance, falloff_distance, falloff_exponent) * (Dm.num(vol) - Dm.num(min_volume));

            if (Dm.truthy(pressure_affected))
            {
                //Atmosphere affects sound
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

                if (distance <= 1)
                    pressure_factor = max(pressure_factor, 0.15f); //touching the source of the sound

                sound_to_use.volume *= pressure_factor;
                //End Atmosphere affecting sound
            }

            float dx = turf_source.x - turf_loc.x; // Hearing from the right/left
            sound_to_use.x = dx * Dm.num(distance_multiplier);
            float dz = turf_source.y - turf_loc.y; // Hearing from infront/behind
            sound_to_use.z = dz * Dm.num(distance_multiplier);
            float dy = (turf_source.z - turf_loc.z) * 5 * Dm.num(distance_multiplier); // Hearing from  above / below, multiplied by 5 because we assume height is further along coords.
            sound_to_use.y = dy;

            sound_to_use.falloff = Dm.truthy(max_distance) ? Dm.num(max_distance) : 1; //use max_distance, else just use 1 as we are a direct sound so falloff isnt relevant.

            // Sounds can't have their own environment. A sound's environment will be:
            // 1. the mob's
            // 2. the area's (defaults to SOUND_ENVRIONMENT_NONE)
            if (sound_environment_override != SOUND_ENVIRONMENT_NONE)
                sound_to_use.environment = sound_environment_override;
            else
            {
                area A = get_area(this) as area;
                sound_to_use.environment = A.sound_environment;
            }

            if (!Dm.truthy(use_reverb) || Dm.eq(sound_to_use.environment, SOUND_ENVIRONMENT_NONE))
            {
                if (!Dm.truthy(sound_to_use.echo))
                    sound_to_use.echo = Dm.@new<DmList>(18);
                ((DmList)sound_to_use.echo)[3] = -10000;
                ((DmList)sound_to_use.echo)[4] = -10000;
            }
        }

        // Apply user-specific volume modifier, if necessary
        if (ispath(volume_preference) && Dm.truthy(client.prefs))
        {
            float client_volume_modifier = Dm.num(client.prefs.read_preference(volume_preference));
            sound_to_use.volume *= (client_volume_modifier / 100);
            if (sound_to_use.volume < 0.1f)
                return FALSE; // Too quiet to hear
        }

        if (HAS_TRAIT(this, TRAIT_SOUND_DEBUGGED))
            to_chat(this, span_admin(Dm.text("Max Range-[] Distance-[] Vol-[] Sound-[]", max_distance, distance, round(sound_to_use.volume, 0.01f), sound_to_use.file)));

        sound_to_use.port_pressure_unaffected = !Dm.truthy(pressure_affected); // PORT (DV37): the feed's muffle follows tg's flag
        Dm.output_to(this, sound_to_use); // SEND_SOUND(src, sound_to_use) (DIRECT_OUTPUT, rules 12)
        return TRUE;
    }

    // tg: code/game/sound/sound.dm:192
    public virtual void stop_sound_channel(object chan = null)
    {
        Dm.output_to(this, sound(null, repeat: 0, wait: 0, channel: Dm.num_or_null(chan))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
    }

    // tg: code/game/sound/sound.dm:195
    public virtual void set_sound_channel_volume(object channel = null, object volume = null)
    {
        sound S = sound(null, FALSE, FALSE, Dm.num_or_null(channel), Dm.num_or_null(volume));
        S.status = SOUND_UPDATE;
        Dm.output_to(this, S); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
    }
}

// tg: code/game/sound/sound.dm:200  (the /client proc of this file, rules 1.9 "interleaved types")
public partial class client : datum
{
    // tg: code/game/sound/sound.dm:200
    public virtual void playtitlemusic(float? volume_multiplier = null)
    {
        Waiting.Detach(playtitlemusic__async(volume_multiplier ?? 1)); // set waitfor = FALSE
    }

    private async DmTask playtitlemusic__async(float volume_multiplier)
    {
        // UNTIL(SSticker.login_music) (__DEFINES/_helpers.dm:31, expanded inline): wait for SSticker init to set the login music
        while (!Dm.truthy(SSticker.login_music))
            await stoplag();

        // DEVIATION(DV244): tg plays the title track once and then the lobby is silent; here the next track follows, after the
        // client faded the last one out and a short pause. A new call (the track overridden) takes the playlist over.
        int serial = ++title_music_serial;
        object track = SSticker.login_music;
        while (true)
        {
            float music_volume = Dm.num(prefs.read_preference(typeof(datum_preference_numeric_volume_sound_lobby_volume))) * volume_multiplier * TITLE_MUSIC_GAIN;
            if (!((prefs != null && Dm.truthy(music_volume)) && !Dm.truthy(config.Get(typeof(datum_config_entry_flag_disallow_title_music)))))
                return;
            Dm.output_to(this, sound(track, repeat: 0, wait: 0, volume: music_volume, channel: CHANNEL_LOBBYMUSIC)); // SEND_SOUND (DIRECT_OUTPUT, rules 12): MAD JAMS

            // The host's table, not SSsounds.get_sound_length: a host without the sound index just keeps one track, with no runtime.
            float track_length = SoundLengths.Length(track as string) ?? 0;
            if (track_length <= 0)
                return;
            // The channel is cut TITLE_MUSIC_FADE before the track ends: the client fades a cut lobby track out from where it was.
            await sleep(Math.Max(track_length - TITLE_MUSIC_FADE, 0));
            if (serial != title_music_serial || mob is not mob_dead_new_player)
                return;
            mob.stop_sound_channel(CHANNEL_LOBBYMUSIC);
            await sleep(TITLE_MUSIC_FADE + TITLE_MUSIC_GAP);
            if (serial != title_music_serial || mob is not mob_dead_new_player)
                return;

            DmList others = SSticker.lobby_playlist?.Copy() ?? Dm.list();
            if (length(others) > 1)
                Dm.remove(ref others, track);
            if (!Dm.truthy(length(others)))
                return;
            track = pick(others);
        }
    }

    /// DV244: how long the client fades the lobby track out (SoundPlayer.FadeOutSeconds), and the silence after it, in deciseconds.
    private const float TITLE_MUSIC_FADE = 4 * SECONDS;
    private const float TITLE_MUSIC_GAP = 2 * SECONDS;

    /// DV244: the lobby track is half as loud as tg's volume setting makes it (the owner, 2026-11-01: too loud even at 10%).
    private const float TITLE_MUSIC_GAIN = 0.5f;

    /// DV244: which run of playtitlemusic plays the lobby playlist.
    private int title_music_serial;
}
