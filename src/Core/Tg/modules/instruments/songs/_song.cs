// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/songs/_song.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole file. The var block is generated (D111); the static lookups are TgState fields.

/**
 * # Song datum
 *
 * These are the actual backend behind instruments.
 * They attach to an atom and provide the editor + playback functionality.
 */
// tg: code/modules/instruments/songs/_song.dm:7
public partial class datum_song : datum
{
    // tg: code/modules/instruments/songs/_song.dm:99-100  var/static/list/note_offset_lookup, accent_lookup
    public DmList note_offset_lookup => TgState.Current.datum_song__note_offset_lookup ??= Dm.list(9, 11, 0, 2, 4, 5, 7);
    public DmList accent_lookup => TgState.Current.datum_song__accent_lookup ??= Dm.list(Dm.kv("b", -1), Dm.kv("s", 1), Dm.kv("#", 1), Dm.kv("n", 0));

    // tg: code/modules/instruments/songs/_song.dm:127
    [DmParams("parent", "instrument_ids", "new_range")]
    public override async DmTask New(params object[] __extra)
    {
        atom parent = Dm.arg(__extra, 0) as atom;
        object instrument_ids = Dm.arg(__extra, 1);
        object new_range = Dm.arg(__extra, 2);
        SSinstruments.on_song_new(this);
        lines = Dm.list();
        tempo = sanitize_tempo(tempo, TRUE);
        this.parent = parent;
        if (Dm.truthy(instrument_ids))
            allowed_instrument_ids = instrument_ids is DmList ids ? ids : Dm.list(instrument_ids);
        if (Dm.length(allowed_instrument_ids) > 0)
            set_instrument(allowed_instrument_ids[1]);
        hearing_mobs = Dm.list();
        volume = Math.Clamp(Dm.num(volume), min_volume, max_volume);
        update_sustain();
        if (Dm.truthy(new_range))
            instrument_range = Dm.num(new_range);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/instruments/songs/_song.dm:142
    public override object Destroy(float? force = null)
    {
        stop_playing();
        SSinstruments.on_song_del(this);
        lines = null;
        if (using_instrument != null)
        {
            using_instrument.songs_using.Remove(this);
            using_instrument = null;
        }
        allowed_instrument_ids = null;
        parent = null;
        return base.Destroy(force);
    }

    /**
     * Checks and stores which mobs can hear us. Terminates sounds for mobs that leave our range.
     */
    // tg: code/modules/instruments/songs/_song.dm:156
    public virtual void do_hearcheck()
    {
        last_hearcheck = world.time;
        DmList old = hearing_mobs.Copy();
        hearing_mobs.Cut();
        turf source = get_turf(parent);
        foreach (mob M in Dm.each<mob>(get_hearers_in_view(instrument_range, source)))
            hearing_mobs[M] = get_dist(M, source);
        foreach (mob exited in Dm.each<mob>(Dm.sub(old, hearing_mobs)))
            terminate_sound_mob(exited);
    }

    /**
     * Sets our instrument, caching anything necessary for faster accessing. Accepts an ID, typepath, or instantiated instrument datum.
     */
    // tg: code/modules/instruments/songs/_song.dm:170
    public virtual void set_instrument(object I = null)
    {
        terminate_all_sounds();
        object old_legacy = null;
        if (using_instrument != null)
        {
            using_instrument.songs_using.Remove(this);
            old_legacy = Dm.bitand(using_instrument.instrument_flags, INSTRUMENT_LEGACY);
        }
        using_instrument = null;
        cached_samples = null;
        cached_legacy_ext = null;
        cached_legacy_dir = null;
        legacy = null;
        if (istext(I) || ispath(I))
            I = SSinstruments.instrument_data[I];
        if (I is datum_instrument instrument)
        {
            using_instrument = instrument;
            instrument.songs_using.Add(this);
            object instrument_legacy = Dm.bitand(instrument.instrument_flags, INSTRUMENT_LEGACY);
            if (Dm.truthy(instrument_legacy))
            {
                cached_legacy_ext = instrument.legacy_instrument_ext;
                cached_legacy_dir = instrument.legacy_instrument_path;
                legacy = TRUE;
            }
            else
            {
                cached_samples = instrument.samples;
                legacy = FALSE;
            }
            if (old_legacy == null || !Dm.eq(old_legacy, instrument_legacy))
            {
                if (Dm.truthy(playing))
                    compile_chords();
            }
        }
    }

    /**
     * Attempts to start playing our song.
     */
    // tg: code/modules/instruments/songs/_song.dm:201
    public virtual void start_playing(atom user = null)
    {
        if (Dm.truthy(playing))
            return;
        if (!Dm.truthy(using_instrument?.ready()))
        {
            to_chat(user, span_warning(I18n.T("An error has occured with []. Please reset the instrument.", this)));
            return;
        }
        compile_chords();
        if (Dm.length(compiled_chords) == 0)
        {
            to_chat(user, span_warning(I18n.T("Song is empty.")));
            return;
        }
        playing = TRUE;
        //we can not afford to runtime, since we are going to be doing sound channel reservations and if we runtime it means we have a channel allocation leak.
        //wrap the rest of the stuff to ensure stop_playing() is called.
        do_hearcheck();
        if (Dm.truthy(parent._listen_lookup?[COMSIG_INSTRUMENT_START]))
            parent._SendSignal(COMSIG_INSTRUMENT_START, Dm.list(parent, this, user));
        if (user != null && Dm.truthy(user._listen_lookup?[COMSIG_ATOM_STARTING_INSTRUMENT]))
            user._SendSignal(COMSIG_ATOM_STARTING_INSTRUMENT, Dm.list(user, this));
        elapsed_delay = 0;
        delay_by = 0;
        current_chord = 1;
        music_player = user;
        START_PROCESSING(SSinstruments, this);
        if (Dm.truthy(id))
            sync_play(user);
    }

    /**
     * Attempts to find other instruments with the same ID and syncs them to our song.
     */
    // tg: code/modules/instruments/songs/_song.dm:228
    public virtual void sync_play(atom user = null)
    {
        foreach (datum_song other_instrument in Dm.each<datum_song>(SSinstruments.songs.Copy()))
        {
            if (other_instrument == this || other_instrument.id != id)
                continue;
            if (Dm.truthy(other_instrument.playing))
                continue;
            atom other_player = other_instrument.find_sync_player();
            if (other_player == null || !Dm.@in(other_player, view(Dm.truthy(user) ? user : parent)))
                continue;
            // copies the main song info to target songs
            other_instrument.lines = lines.Copy();
            other_instrument.max_repeats = max_repeats;
            other_instrument.tempo = tempo;
            other_instrument.start_playing(other_player);
        }
    }

    /**
     * Finds a player which would reasonably be able to play this song.
     */
    // tg: code/modules/instruments/songs/_song.dm:246
    public virtual atom find_sync_player() => null;

    /**
     * Stops playing, terminating all sounds if in synthesized mode. Clears hearing_mobs.
     *
     * Arguments:
     * * finished: boolean, whether the song ended via reaching the end.
     */
    // tg: code/modules/instruments/songs/_song.dm:255
    public virtual void stop_playing(object finished = null)
    {
        finished ??= FALSE;
        if (!Dm.truthy(playing))
            return;
        playing = FALSE;
        if (!Dm.truthy(debug_mode))
            compiled_chords = null;
        STOP_PROCESSING(SSinstruments, this);
        if (Dm.truthy(parent?._listen_lookup?[COMSIG_INSTRUMENT_END]))
            parent._SendSignal(COMSIG_INSTRUMENT_END, Dm.list(parent, finished));
        terminate_all_sounds(TRUE);
        hearing_mobs.Cut();
        music_player = null;
    }

    /**
     * Processes our song.
     */
    // tg: code/modules/instruments/songs/_song.dm:270
    public virtual void process_song(object wait = null)
    {
        if (Dm.length(compiled_chords) == 0)
        {
            stop_playing(TRUE);
            return;
        }
        if (Dm.eq(should_stop_playing(music_player), STOP_PLAYING))
        {
            stop_playing(FALSE);
            return;
        }
        var chord = compiled_chords[current_chord] as DmList;
        elapsed_delay = Dm.num(elapsed_delay) + 1;
        if (Dm.num(elapsed_delay) < Dm.num(delay_by))
            return;
        play_chord(chord);
        elapsed_delay = 0;
        delay_by = tempodiv_to_delay(chord[Dm.length(chord)]);
        current_chord = Dm.num(current_chord) + 1;
        if (Dm.num(current_chord) <= Dm.length(compiled_chords))
            return;
        if (!Dm.truthy(repeat))
        {
            stop_playing(TRUE);
            return;
        }
        repeat--;
        current_chord = 1;
        if (Dm.truthy(parent._listen_lookup?[COMSIG_INSTRUMENT_REPEAT]))
            parent._SendSignal(COMSIG_INSTRUMENT_REPEAT, Dm.list(parent, TRUE));
    }

    /**
     * Converts a tempodiv to ticks to elapse before playing the next chord, taking into account our tempo.
     */
    // tg: code/modules/instruments/songs/_song.dm:297
    public virtual float tempodiv_to_delay(object tempodiv = null)
    {
        if (!Dm.truthy(tempodiv))
            tempodiv = 1; // no division by 0. some song converters tend to use 0 for when it wants to have no div, for whatever reason.
        return Math.Max(1, round((tempo / Dm.num(tempodiv)) / world.tick_lag, 1));
    }

    /**
     * Compiles chords.
     */
    // tg: code/modules/instruments/songs/_song.dm:305
    public virtual void compile_chords()
    {
        if (Dm.truthy(legacy))
            compile_legacy();
        else
            compile_synthesized();
    }

    /**
     * Plays a chord.
     */
    // tg: code/modules/instruments/songs/_song.dm:311
    public virtual void play_chord(DmList chord = null)
    {
        // last value is timing information
        for (int i = 1; i <= Dm.length(chord) - 1; i++)
        {
            if (Dm.truthy(legacy))
            {
                var note = (DmList)chord[i];
                playkey_legacy(note[1], note[2], note[3], music_player);
            }
            else
                playkey_synth(chord[i], music_player);
        }
    }

    /**
     * Checks if we should halt playback.
     */
    // tg: code/modules/instruments/songs/_song.dm:319
    public virtual object should_stop_playing(atom player = null)
    {
        if (QDELETED(player) || using_instrument == null || !Dm.truthy(playing))
            return STOP_PLAYING;
        if (!Dm.truthy(parent._listen_lookup?[COMSIG_INSTRUMENT_SHOULD_STOP_PLAYING]))
            return NONE; // SEND_SIGNAL with no listener
        return parent._SendSignal(COMSIG_INSTRUMENT_SHOULD_STOP_PLAYING, Dm.list(parent, player));
    }

    /// Sets and sanitizes the repeats variable.
    // tg: code/modules/instruments/songs/_song.dm:325
    public virtual void set_repeats(object new_repeats_value = null)
    {
        if (Dm.truthy(playing))
            return; //So that people cant keep adding to repeat. If the do it intentionally, it could result in the server crashing.
        repeat = round(Dm.num(new_repeats_value));
        if (repeat < 0)
            repeat = 0;
        if (repeat > max_repeats)
            repeat = max_repeats;
    }

    /**
     * Sanitizes tempo to a value that makes sense and fits the current world.tick_lag.
     */
    // tg: code/modules/instruments/songs/_song.dm:338
    public virtual float sanitize_tempo(object new_tempo = null, object initializing = null)
    {
        initializing ??= FALSE;
        float t = Math.Abs(Dm.num(new_tempo));
        if (!Dm.truthy(initializing)) // not only is it not helpful while initializing but it will runtime really hard since nothing is set up
        {
            if (Dm.truthy(parent?._listen_lookup?[COMSIG_INSTRUMENT_TEMPO_CHANGE]))
                parent._SendSignal(COMSIG_INSTRUMENT_TEMPO_CHANGE, Dm.list(parent, this));
        }
        return Math.Clamp(round(t, world.tick_lag), world.tick_lag, 5 * SECONDS);
    }

    /**
     * Gets our beats per minute based on our tempo.
     */
    // tg: code/modules/instruments/songs/_song.dm:347
    public virtual float get_bpm() => 600 / tempo;

    /**
     * Sets our tempo from a beats-per-minute, sanitizing it to a valid number first.
     */
    // tg: code/modules/instruments/songs/_song.dm:353
    public virtual void set_bpm(object bpm = null) => tempo = sanitize_tempo(600 / Dm.num(bpm));

    // tg: code/modules/instruments/songs/_song.dm:356
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(playing))
            return PROCESS_KILL;
        // it's expected this ticks at every world.tick_lag. if it lags, do not attempt to catch up.
        process_song(world.tick_lag);
        process_decay(world.tick_lag);
        return null;
    }

    /**
     * Updates our cached linear/exponential falloff stuff, saving calculations down the line.
     */
    // tg: code/modules/instruments/songs/_song.dm:366
    public virtual void update_sustain()
    {
        // Exponential is easy
        cached_exponential_dropoff = sustain_exponential_dropoff;
        // Linear, not so much, since it's a target duration from 100 volume rather than an exponential rate.
        float target_duration = sustain_linear_duration;
        float volume_diff = Math.Max(0, 100 - sustain_dropoff_volume);
        float volume_decrease_per_decisecond = volume_diff / target_duration;
        cached_linear_dropoff = volume_decrease_per_decisecond;
    }

    /**
     * Setter for setting output volume.
     */
    // tg: code/modules/instruments/songs/_song.dm:378
    public virtual void set_volume(object volume = null)
    {
        this.volume = Math.Clamp(round(Dm.num(volume), 1), Math.Max(0, min_volume), Math.Min(100, max_volume));
        update_sustain();
    }

    /**
     * Setter for setting how low the volume has to get before a note is considered "dead" and dropped
     */
    // tg: code/modules/instruments/songs/_song.dm:385
    public virtual void set_dropoff_volume(object volume = null)
    {
        sustain_dropoff_volume = Math.Clamp(round(Dm.num(volume), 0.01f), INSTRUMENT_MIN_SUSTAIN_DROPOFF, 100);
        update_sustain();
    }

    /**
     * Setter for setting exponential falloff factor.
     */
    // tg: code/modules/instruments/songs/_song.dm:392
    public virtual void set_exponential_drop_rate(object drop = null)
    {
        sustain_exponential_dropoff = Math.Clamp(round(Dm.num(drop), 0.00001f), INSTRUMENT_EXP_FALLOFF_MIN, INSTRUMENT_EXP_FALLOFF_MAX);
        update_sustain();
    }

    /**
     * Setter for setting linear falloff duration.
     */
    // tg: code/modules/instruments/songs/_song.dm:399
    public virtual void set_linear_falloff_duration(object duration = null)
    {
        sustain_linear_duration = Math.Clamp(round(Dm.num(duration) * 10, world.tick_lag), world.tick_lag, INSTRUMENT_MAX_TOTAL_SUSTAIN);
        update_sustain();
    }
}

// subtype for handheld instruments, like violin
// tg: code/modules/instruments/songs/_song.dm:417
public partial class datum_song_handheld : datum_song
{
    // tg: code/modules/instruments/songs/_song.dm:419
    public override object should_stop_playing(atom player = null)
    {
        object dot = base.should_stop_playing(player);
        if (Dm.eq(dot, STOP_PLAYING) || Dm.eq(dot, IGNORE_INSTRUMENT_CHECKS))
            return dot;
        var I = (obj_item_instrument)parent;
        return Dm.truthy(I.can_play(player)) ? NONE : STOP_PLAYING;
    }

    // tg: code/modules/instruments/songs/_song.dm:426
    public override atom find_sync_player()
    {
        var instrument = (obj_item_instrument)parent;
        var player = get(parent, typeof(mob_living)) as mob_living;
        if (Dm.truthy(instrument.can_play(player)))
            return player;
        return null;
    }
}

// subtype for stationary structures, like pianos
// tg: code/modules/instruments/songs/_song.dm:434
public partial class datum_song_stationary : datum_song
{
    // tg: code/modules/instruments/songs/_song.dm:436
    public override object should_stop_playing(atom player = null)
    {
        object dot = base.should_stop_playing(player);
        if (Dm.eq(dot, STOP_PLAYING) || Dm.eq(dot, IGNORE_INSTRUMENT_CHECKS))
            return dot;
        var M = (obj_structure_musician)parent;
        return Dm.truthy(M.can_play(player)) ? NONE : STOP_PLAYING;
    }

    // tg: code/modules/instruments/songs/_song.dm:443
    public override atom find_sync_player()
    {
        var piano = (obj_structure_musician)parent;
        foreach (mob_living player in Dm.each<mob_living>(view(parent, 1)))
        {
            if (Dm.truthy(piano.can_play(player)))
                return player;
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/instruments/songs/_song.dm:99-100  /datum/song/var/static/list/note_offset_lookup, accent_lookup
    internal DmList datum_song__note_offset_lookup;
    internal DmList datum_song__accent_lookup;
}
