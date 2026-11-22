// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/songs/play_synthesized.dm and play_legacy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole files.

public partial class datum_song : datum
{
    // The chord parser both compilers share (play_synthesized.dm:5-39 and play_legacy.dm:5-39 are the same loop but for what a
    // note compiles to): each note calls emit(key, accent, octave).
    private void compile_notes(Action<DmList, int, string, int> emit)
    {
        DmList lines = this.lines; //cache for hyepr speed!
        compiled_chords = Dm.list();
        var octaves = new[] { 3, 3, 3, 3, 3, 3, 3 };
        var accents = new[] { "n", "n", "n", "n", "n", "n", "n" };
        foreach (object line in Dm.each(lines))
        {
            DmList chords = splittext(LOWER_TEXT(line), ",") as DmList;
            foreach (object chord in Dm.each(chords))
            {
                DmList compiled_chord = Dm.list();
                object tempodiv = 1;
                DmList notes_tempodiv = splittext(chord, "/") as DmList;
                int len = (int)Dm.length(notes_tempodiv);
                if (len >= 2)
                    tempodiv = text2num(notes_tempodiv[2]);
                if (len > 0) //some dunkass is going to do ,,,, to make 3 rests instead of ,/1 because there's no standardization so let's be prepared for that.
                {
                    DmList notes = splittext(notes_tempodiv[1], "-") as DmList;
                    foreach (object noteObj in Dm.each(notes))
                    {
                        string note = Dm.str(noteObj);
                        if (note.Length == 0)
                            continue;
                        // 1-7, A-G
                        int key = note[0] - 96;
                        if (key < 1 || key > 7)
                            continue;
                        for (int i = 2; i <= note.Length; i++)
                        {
                            string oct_acc = note.Substring(i - 1, 1);
                            float num = Dm.num(text2num(oct_acc));
                            if (num == 0) //it's an accidental
                                accents[key - 1] = oct_acc; //if they misspelled it/fucked up that's on them lmao, no safety checks.
                            else //octave
                                octaves[key - 1] = (int)Math.Clamp(num, octave_min, octave_max);
                        }
                        emit(compiled_chord, key, accents[key - 1], octaves[key - 1]);
                    }
                }
                compiled_chord.Add(tempodiv); //this goes last
                if (Dm.length(compiled_chord) > 0)
                    compiled_chords.Add(Dm.list(compiled_chord)); // compiled_chords[++compiled_chords.len] = compiled_chord
            }
        }
    }

    /**
     * Compiles our lines into "chords" with numbers. This makes there have to be a bit of lag at the beginning of the song, but repeats will not have to parse it again, and overall playback won't be impacted by as much lag.
     */
    // tg: code/modules/instruments/songs/play_synthesized.dm:4
    public virtual void compile_synthesized()
    {
        if (Dm.length(lines) == 0)
            return;
        compile_notes((compiled_chord, key, accent, octave) =>
            compiled_chord.Add(Math.Clamp(Dm.num(note_offset_lookup[key]) + octave * 12 + Dm.num(accent_lookup[accent]), key_min, key_max)));
    }

    /**
     * Plays a specific numerical key from our instrument to anyone who can hear us.
     * Does a hearing check if enough time has passed.
     */
    // tg: code/modules/instruments/songs/play_synthesized.dm:45
    public virtual object playkey_synth(object key = null, atom player = null)
    {
        key = Math.Clamp(Dm.num(key) + note_shift, key_min, key_max);
        if ((world.time - datum_controller_subsystem_processing_instruments.musician_hearcheck_mindelay) > last_hearcheck)
            do_hearcheck();
        var instrument_key = using_instrument.samples[num2text(Dm.num(key))] as datum_instrument_key; //See how fucking easy it is to make a number text? You don't need a complicated 9 line proc!
        //Should probably add channel limiters here at some point but I don't care right now.
        object channel = pop_channel();
        if (channel == null)
            return FALSE;
        object dot = TRUE;
        sound copy = Dm.sound(instrument_key.sample);
        float volume = Dm.num(this.volume) * using_instrument.volume_multiplier;
        copy.frequency = Dm.num(instrument_key.frequency);
        copy.volume = volume;
        string channel_text = num2text(Dm.num(channel));
        channels_playing[channel_text] = 100;
        last_channel_played = channel_text;
        foreach (mob hearing_mob in Dm.each<mob>(hearing_mobs.Copy()))
        {
            if (player != null && HAS_TRAIT(player, TRAIT_MUSICIAN) && hearing_mob is mob_living living_hearer)
                Waiting.NoSleep(living_hearer.apply_status_effect(typeof(datum_status_effect_good_music)));
            object pref_volume = hearing_mob?.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_instruments));
            if (!Dm.truthy(pref_volume))
                continue;
            hearing_mob.playsound_local(get_turf(parent), null, volume * (Dm.num(pref_volume) / 100), FALSE, instrument_key.frequency, exponential_falloff, Dm.num(channel), null, copy);
            // Could do environment and echo later but not for now
        }
        return dot;
    }

    /**
     * Stops all sounds we are "responsible" for. Only works in synthesized mode.
     */
    // tg: code/modules/instruments/songs/play_synthesized.dm:75
    public virtual void terminate_all_sounds(object clear_channels = null)
    {
        clear_channels ??= TRUE;
        foreach (mob i in Dm.each<mob>(hearing_mobs))
            terminate_sound_mob(i);
        if (Dm.truthy(clear_channels))
        {
            channels_playing.Cut();
            channels_idle.Cut();
            SSinstruments.current_instrument_channels -= using_sound_channels;
            using_sound_channels = 0;
            SSsounds.free_datum_channels(this);
        }
    }

    /**
     * Stops all sounds we are responsible for in a given person. Only works in synthesized mode.
     */
    // tg: code/modules/instruments/songs/play_synthesized.dm:88
    public virtual void terminate_sound_mob(mob M = null)
    {
        foreach (object channel in Dm.each(channels_playing))
            M.stop_sound_channel(text2num(channel));
    }

    /**
     * Pops a channel we have reserved so we don't have to release and re-request them from SSsounds every time we play a note. This is faster.
     */
    // tg: code/modules/instruments/songs/play_synthesized.dm:95
    public virtual object pop_channel()
    {
        if (Dm.length(channels_idle) > 0) //just pop one off of here if we have one available
        {
            object popped = text2num(channels_idle[1]);
            channels_idle.Cut(1, 2);
            return popped;
        }
        if (using_sound_channels >= max_sound_channels)
            return null;
        object dot = SSinstruments.reserve_instrument_channel(this);
        if (dot != null)
            using_sound_channels++;
        return dot;
    }

    /**
     * Decays our channels and updates their volumes to mobs who can hear us.
     *
     * Arguments:
     * * wait_ds - the deciseconds we should decay by. This is to compensate for any lag, as otherwise songs would get pretty nasty during high time dilation.
     */
    // tg: code/modules/instruments/songs/play_synthesized.dm:112
    public virtual void process_decay(object wait_ds = null)
    {
        float wait = Dm.num(wait_ds);
        float linear_dropoff = cached_linear_dropoff * wait;
        float exponential_dropoff = MathF.Pow(cached_exponential_dropoff, wait);
        foreach (object channel in Dm.each(channels_playing.Copy()))
        {
            bool held_note = Dm.truthy(full_sustain_held_note) && Dm.eq(channel, last_channel_played);
            float current_volume = Dm.num(channels_playing[channel]);
            if (!held_note)
            {
                switch (sustain_mode)
                {
                    case SUSTAIN_LINEAR:
                        current_volume -= linear_dropoff;
                        break;
                    case SUSTAIN_EXPONENTIAL:
                        current_volume /= exponential_dropoff;
                        break;
                }
            }
            channels_playing[channel] = current_volume;
            bool dead = !held_note && current_volume <= sustain_dropoff_volume;
            object channelnumber = text2num(channel);
            if (dead)
            {
                channels_playing.Remove(channel);
                channels_idle.Add(channel);
                foreach (mob hearing_mob in Dm.each<mob>(hearing_mobs))
                    hearing_mob.stop_sound_channel(channelnumber);
            }
            else
            {
                foreach (mob hearing_mob in Dm.each<mob>(hearing_mobs))
                {
                    object pref_volume = hearing_mob?.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_instruments));
                    hearing_mob.set_sound_channel_volume(channelnumber, (current_volume * 0.01f) * Dm.num(volume) * using_instrument.volume_multiplier * (Dm.num(pref_volume) / 100));
                }
            }
        }
    }

    /**
     * Compiles our lines into "chords" with filenames for legacy playback. This makes there have to be a bit of lag at the beginning of the song, but repeats will not have to parse it again, and overall playback won't be impacted by as much lag.
     */
    // tg: code/modules/instruments/songs/play_legacy.dm:4
    public virtual void compile_legacy()
    {
        if (Dm.length(lines) == 0)
            return;
        compile_notes((compiled_chord, key, accent, octave) => compiled_chord.Add(Dm.list(Dm.list(key, accent, octave)))); // compiled_chord[++compiled_chord.len] = list(...)
    }

    /**
     * Proc to play a legacy note. Just plays the sound to hearing mobs (and does hearcheck if necessary), no fancy channel/sustain/management.
     *
     * Arguments:
     * * note is a number from 1-7 for A-G
     * * acc is either "b", "n", or "#"
     * * oct is 1-8 (or 9 for C)
     */
    // tg: code/modules/instruments/songs/play_legacy.dm:49
    public virtual void playkey_legacy(object noteObj = null, object accObj = null, object octObj = null, atom player = null)
    {
        int note = (int)Dm.num(noteObj);
        string acc = Dm.str(accObj);
        int oct = (int)Dm.num(octObj);
        // handle accidental -> B<>C of E<>F
        if (acc == "b" && (note == 3 || note == 6)) // C or F
        {
            if (note == 3)
                oct--;
            note--;
            acc = "n";
        }
        else if (acc == "#" && (note == 2 || note == 5)) // B or E
        {
            if (note == 2)
                oct++;
            note++;
            acc = "n";
        }
        else if (acc == "#" && note == 7) //G#
        {
            note = 1;
            acc = "b";
        }
        else if (acc == "#") // mass convert all sharps to flats, octave jump already handled
        {
            acc = "b";
            note++;
        }

        // check octave, C is allowed to go to 9
        if (oct < 1 || (note == 3 ? oct > 9 : oct > 8))
            return;

        // now generate name
        string soundfile = Dm.text("sound/runtime/instruments/[]/[][][].[]", cached_legacy_dir, ascii2text(note + 64), acc, oct, cached_legacy_ext);
        // make sure the note exists
        if (!Dm.truthy(fexists(soundfile)))
            return;
        // and play
        turf source = get_turf(parent);
        if ((world.time - datum_controller_subsystem_processing_instruments.musician_hearcheck_mindelay) > last_hearcheck)
            do_hearcheck();
        sound music_played = Dm.sound(Dm.resource(soundfile));
        foreach (mob M in Dm.each<mob>(hearing_mobs.Copy()))
        {
            if (player != null && HAS_TRAIT(player, TRAIT_MUSICIAN) && M is mob_living L)
                Waiting.NoSleep(L.apply_status_effect(typeof(datum_status_effect_good_music)));
            object pref_volume = M?.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_instruments));
            if (!Dm.truthy(pref_volume))
                continue;
            M.playsound_local(source, null, Dm.num(volume) * using_instrument.volume_multiplier * (Dm.num(pref_volume) / 100), sound_to_use: music_played, falloff_exponent: exponential_falloff);
            // Could do environment and echo later but not for now
        }
    }
}
