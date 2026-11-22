// Translated from tgstation (snapshot 1d99bd7f): code/datums/looping_sounds/_looping_sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio slice 5 (loops and alarms): the whole file. The var block is generated (D111). The loop's timers run on
// SSsound_loops (controllers/subsystem/sound_loops.dm), a token loop's sound is a /datum/sound_token (datums/sound_token.dm).

/**
 * A datum for sounds that need to loop, with a high amount of configurability.
 */
// tg: code/datums/looping_sounds/_looping_sound.dm:4
[DmPath("/datum/looping_sound")]
public partial class datum_looping_sound : datum
{
    // tg: code/datums/looping_sounds/_looping_sound.dm:79
    [DmParams("_parent", "start_immediately", "_direct", "_skip_starting_sounds", "sound_channel")]
    public override async DmTask New(params object[] __extra)
    {
        object _parent = Dm.arg<object>(__extra, 0);
        object start_immediately = Dm.arg<object>(__extra, 1);
        object _direct = Dm.arg<object>(__extra, 2);
        object _skip_starting_sounds = Dm.arg<object>(__extra, 3);
        object sound_channel = Dm.arg<object>(__extra, 4);
        start_immediately ??= FALSE;
        _direct ??= FALSE;
        _skip_starting_sounds ??= FALSE;
        if (!Dm.truthy(mid_sounds))
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", "A looping sound datum was created without sounds to play.", "code/datums/looping_sounds/_looping_sound.dm", 87, this, usr));
            return;
        }

        set_parent(_parent);
        direct = _direct;
        skip_starting_sounds = Dm.num(_skip_starting_sounds);
        if (Dm.truthy(sound_channel))
            this.sound_channel = sound_channel;

        if (Dm.truthy(start_immediately))
            start();
    }

    // tg: code/datums/looping_sounds/_looping_sound.dm:99
    public override object Destroy(float? force = null)
    {
        stop(TRUE);
        return base.Destroy(force);
    }

    /**
     * The proc to actually kickstart the whole sound sequence. This is what you should call to start the `looping_sound`.
     *
     * Arguments:
     * * on_behalf_of - The new object to set as a parent.
     */
    // tg: code/datums/looping_sounds/_looping_sound.dm:109
    public virtual void start(object on_behalf_of = null)
    {
        if (Dm.truthy(on_behalf_of))
            set_parent(on_behalf_of);
        if (Dm.truthy(timer_id) || Dm.truthy(loop_started))
            return;

        if (!Dm.truthy(use_sound_tokens) && !Dm.truthy(sound_channel) && Dm.truthy(reserve_random_channel))
        {
            sound_channel = SSsounds.reserve_sound_channel();
            reserved_channel = sound_channel;
        }
        on_start();
    }

    /**
     * The proc to call to stop the sound loop.
     *
     * Arguments:
     * * null_parent - Whether or not we should set the parent to null (useful when destroying the `looping_sound` itself). Defaults to FALSE.
     */
    // tg: code/datums/looping_sounds/_looping_sound.dm:127
    public virtual void stop(float? null_parent = null)
    {
        null_parent ??= FALSE;
        stop_current();
        if (Dm.truthy(null_parent))
            set_parent(null);
        if (!Dm.truthy(timer_id) && !Dm.truthy(loop_started))
            return;
        on_stop();
        if (Dm.truthy(timer_id))
        {
            deltimer(timer_id, SSsound_loops);
            timer_id = null;
        }
        loop_started = FALSE;
        native_repeat_active = FALSE;

        if (Dm.truthy(reserved_channel))
        {
            sound_channel = null;
            SSsounds.free_sound_channel(reserved_channel);
        }
    }

    /// The proc that handles starting the actual core sound loop.
    // tg: code/datums/looping_sounds/_looping_sound.dm:146
    public virtual void start_sound_loop()
    {
        loop_started = TRUE;
        if (Dm.truthy(can_native_repeat()))
        {
            native_repeat_active = TRUE;
            play(resolve_single_sound(), repeat_sound: TRUE);
            if (Dm.truthy(max_loops))
                timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(stop)), Dm.num(mid_length) * Dm.num(max_loops), Dm.bitor(Dm.bitor(TIMER_CLIENT_TIME, TIMER_DELETE_ME), TIMER_STOPPABLE), SSsound_loops, file: "code/datums/looping_sounds/_looping_sound.dm", line: 152);
            return;
        }
        sound_loop();
        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(sound_loop), world.time), Dm.num(mid_length), Dm.bitor(Dm.bitor(Dm.bitor(TIMER_CLIENT_TIME, TIMER_STOPPABLE), TIMER_LOOP), TIMER_DELETE_ME), SSsound_loops, file: "code/datums/looping_sounds/_looping_sound.dm", line: 155);
    }

    /**
     * A simple proc handling the looping of the sound itself.
     *
     * Arguments:
     * * start_time - The time at which the `mid_sounds` started being played (so we know when to stop looping).
     */
    // tg: code/datums/looping_sounds/_looping_sound.dm:163
    public virtual void sound_loop(object start_time = null)
    {
        if (Dm.truthy(max_loops) && world.time >= Dm.num(start_time) + Dm.num(mid_length) * Dm.num(max_loops))
        {
            stop();
            return;
        }
        // If we have a timer, we're varying mid length, and this is happening while we're runnin mid_sounds
        if (Dm.truthy(timer_id) && Dm.truthy(mid_length_vary) && Dm.truthy(start_time))
            updatetimedelay(timer_id, Dm.num(mid_length) + rand(-mid_length_vary, mid_length_vary), timer_subsystem: SSsound_loops);
        if (!Dm.truthy(chance) || prob(Dm.num(chance)))
            play(get_sound());
    }

    /**
     * Applies a new mid length to the sound
     */
    // tg: code/datums/looping_sounds/_looping_sound.dm:176
    public virtual void set_mid_length(object new_mid = null)
    {
        mid_length = new_mid;
        if (Dm.truthy(native_repeat_active)) // Native repeat is driven by the sound file's own length, not mid_length
            return;
        if (!Dm.truthy(timer_id))
            return;
        updatetimedelay(timer_id, Dm.num(mid_length) + rand(-mid_length_vary, mid_length_vary), timer_subsystem: SSsound_loops);
    }

    /**
     * The proc that handles actually playing the sound.
     *
     * Arguments:
     * * soundfile - The soundfile we want to play.
     * * volume_override - The volume we want to play the sound at, overriding the `volume` variable.
     * * repeat_sound - Whether the sound should loop natively via sound.repeat (token path only).
     * * delete_when_finished - Whether to play the sound on a one-shot token that deletes itself once the sound ends, instead of on our own token (token path only).
     */
    // tg: code/datums/looping_sounds/_looping_sound.dm:193
    public virtual void play(object soundfile = null, object volume_override = null, float? repeat_sound = null, float? delete_when_finished = null)
    {
        repeat_sound ??= FALSE;
        delete_when_finished ??= FALSE;

        if (Dm.truthy(use_sound_tokens))
        {
            if (Dm.truthy(delete_when_finished)) // Not kept as our token, or we'd keep reusing it after it deletes itself
            {
                playsoundtoken(parent, soundfile, Dm.truthy(volume_override) ? volume_override : volume, SOUND_RANGE + extra_range, Dm.num_or_null(falloff_exponent), Dm.num_or_null(falloff_distance));
                return;
            }
            if (Dm.truthy(sound_token_instance))
            {
                sound_token_instance.set_volume(Dm.truthy(volume_override) ? volume_override : volume, FALSE); // Don't update, we'll do that after
                sound_token_instance.update_sound(soundfile, TRUE, repeat_sound);
            }
            else
                sound_token_instance = Dm.@new<datum_sound_token>(parent, soundfile, SOUND_RANGE + extra_range, Dm.truthy(volume_override) ? volume_override : volume, falloff_exponent, falloff_distance, Dm.named("_repeating", repeat_sound));
            return;
        }
        sound sound_to_play = sound(soundfile);
        sound_to_play.channel = Dm.truthy(sound_channel) ? Dm.num(sound_channel) : Dm.num(SSsounds.random_available_channel());
        sound_to_play.volume = Dm.truthy(volume_override) ? Dm.num(volume_override) : Dm.num(volume); //Use volume as fallback if theres no override
        if (Dm.truthy(direct))
            Dm.output_to(parent, sound_to_play); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        else
        {
            playsound(
                parent,
                sound_to_play,
                volume,
                vary,
                extra_range,
                falloff_exponent: Dm.num_or_null(falloff_exponent),
                channel: sound_to_play.channel,
                pressure_affected: pressure_affected,
                ignore_walls: ignore_walls,
                falloff_distance: Dm.num_or_null(falloff_distance),
                use_reverb: use_reverb
            );
        }
    }

    /// Returns the sound we should now be playing.
    // tg: code/datums/looping_sounds/_looping_sound.dm:226
    public virtual object get_sound(object _mid_sounds = null)
    {
        object dot = null;
        // var/list/play_from: mid_sounds may also be a single file (rules 3.2: the declared list type is only a hint in DM)
        object play_from = Dm.truthy(_mid_sounds) ? _mid_sounds : mid_sounds;
        if (!Dm.truthy(each_once))
        {
            dot = play_from;
            while (!isfile(dot) && dot != null)
                dot = pick_weight_recursive((DmList)dot);
            return dot;
        }

        if (Dm.truthy(in_order))
        {
            dot = play_from;
            audio_index++;
            if (audio_index > Dm.num(length(play_from)))
                audio_index = 1;
            return ((DmList)dot)[audio_index];
        }

        if (!Dm.truthy(length(cut_list)))
            cut_list = shuffle(((DmList)play_from).Copy()) as DmList;
        DmList tree = Dm.list();
        dot = cut_list;
        while (!isfile(dot) && dot != null)
        {
            // Tree is a list of lists containign files
            // If an entry in the tree goes to 0 length, we cut it from the list
            Dm.append(ref tree, Dm.list(dot));
            dot = pick_weight_recursive((DmList)dot);
        }

        if (!isfile(dot))
            return null;

        // Remove the sound file
        DmList leaf = (DmList)tree[length(tree)]; // tree[length(tree)] -= . : the indexed list, changed in place (rules 11.1)
        Dm.remove(ref leaf, dot);

        // Walk the tree bottom up, remove any lists that are empty
        // Don't do anything for the topmost list, cause we do not care
        for (float i = Dm.num(length(tree)); i >= 2; i += -1)
        {
            DmList branch = tree[i] as DmList;
            if (Dm.truthy(length(branch)))
                break;
            DmList parent_branch = (DmList)tree[i - 1]; // tree[i - 1] -= list(branch): the indexed list, changed in place
            Dm.remove(ref parent_branch, Dm.list(branch)); // Remove the empty list
        }
        return dot;
    }



    /// Returns the lone soundfile mid_sounds resolves to, or null if it can pick between more than one file.
    // tg: code/datums/looping_sounds/_looping_sound.dm:269
    public virtual object resolve_single_sound()
    {
        if (isfile(mid_sounds) || istext(mid_sounds))
            return mid_sounds;
        if (islist(mid_sounds) && Dm.eq(length(mid_sounds), 1))
        {
            object only_sound = ((DmList)mid_sounds)[1];
            if (isfile(only_sound) || istext(only_sound))
                return only_sound;
        }
        return null;
    }

    /// Whether this loop qualifies for native sound.repeat instead of re-firing on a timer every mid_length.
    // tg: code/datums/looping_sounds/_looping_sound.dm:279
    public virtual object can_native_repeat()
    {
        if (!Dm.truthy(use_sound_tokens) || Dm.truthy(never_native_repeat))
            return FALSE;
        if (Dm.truthy(chance) || Dm.truthy(mid_length_vary) || Dm.truthy(each_once) || Dm.truthy(in_order))
            return FALSE;
        return Dm.truthy(resolve_single_sound()) ? TRUE : FALSE;
    }

    /// A proc that's there to handle delaying the main sounds if there's a start_sound, and simply starting the sound loop in general.
    // tg: code/datums/looping_sounds/_looping_sound.dm:287
    public virtual void on_start()
    {
        object start_wait = 0;
        if (Dm.truthy(start_sound) && !Dm.truthy(skip_starting_sounds))
        {
            play(start_sound, start_volume);
            start_wait = start_length;
        }
        if (Dm.truthy(start_wait))
            timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(start_sound_loop)), Dm.num(start_wait), Dm.bitor(Dm.bitor(TIMER_CLIENT_TIME, TIMER_DELETE_ME), TIMER_STOPPABLE), SSsound_loops, file: "code/datums/looping_sounds/_looping_sound.dm", line: 293);
        else
            start_sound_loop();
    }

    /// Stops sound playing on current channel, if specified
    // tg: code/datums/looping_sounds/_looping_sound.dm:298
    public virtual void stop_current()
    {
        QDEL_NULL(ref sound_token_instance);
        if (!Dm.truthy(sound_channel) || !ismob(parent))
            return;
        mob mob_parent = (mob)parent;
        mob_parent.stop_sound_channel(sound_channel);
    }

    /// Simple proc that's executed when the looping sound is stopped, so that the `end_sound` can be played, if there's one.
    // tg: code/datums/looping_sounds/_looping_sound.dm:306
    public virtual void on_stop()
    {
        if (Dm.truthy(end_sound) && Dm.truthy(loop_started))
            play(end_sound, end_volume, delete_when_finished: TRUE);
    }

    /// A simple proc to change who our parent is set to, also handling registering and unregistering the QDELETING signals on the parent.
    // tg: code/datums/looping_sounds/_looping_sound.dm:311
    public virtual void set_parent(object new_parent = null)
    {
        if (Dm.truthy(parent))
            UnregisterSignal(parent, COMSIG_QDELETING);
        parent = (atom)new_parent;
        if (Dm.truthy(parent))
            RegisterSignal(parent, COMSIG_QDELETING, nameof(handle_parent_del));
    }

    /// A simple proc that lets us know whether the sounds are currently active or not.
    // tg: code/datums/looping_sounds/_looping_sound.dm:319
    public virtual object is_active()
    {
        return Dm.truthy(loop_started) ? loop_started : (Dm.truthy(timer_id) ? TRUE : FALSE);
    }

    /// A simple proc to handle the deletion of the parent, so that it does not force it to hard-delete.
    // tg: code/datums/looping_sounds/_looping_sound.dm:323
    [SignalHandler]
    public virtual void handle_parent_del(datum source = null)
    {
        set_parent(null);
    }
}
