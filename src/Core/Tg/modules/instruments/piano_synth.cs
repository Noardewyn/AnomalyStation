// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/piano_synth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the synthesizer and the headphones (silicons SI2: the service cyborg's synthesizer; SI6c: their songs), their circuit
// shell and its synth components, which play a song from a circuit.

// tg: code/modules/instruments/piano_synth.dm:2
public partial class obj_item_instrument_piano_synth : obj_item_instrument
{
    // tg: code/modules/instruments/piano_synth.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        song.allowed_instrument_ids = SSinstruments.synthesizer_instrument_ids;
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(Dm.@new(circuit_type)), shell_capacity));
        return dot;
    }

    // tg: code/modules/instruments/piano_synth.dm:18
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(mouse_playable) || !ismouse(user))
            return await base.attack_animal(user, modifiers);
        await song.ui_interact(user);
        return null;
    }
}

// tg: code/modules/instruments/piano_synth.dm:23
public partial class obj_item_instrument_piano_synth_headphones : obj_item_instrument_piano_synth
{
    // tg: code/modules/instruments/piano_synth.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        RegisterSignal(this, COMSIG_INSTRUMENT_START, nameof(update_icon_for_playing_music));
        RegisterSignal(this, COMSIG_INSTRUMENT_END, nameof(update_icon_for_playing_music));
        return dot;
    }

    // Called by a component signal to update musical note VFX for songs playing while worn.
    // tg: code/modules/instruments/piano_synth.dm:51
    public virtual void update_icon_for_playing_music(datum source = null, datum starting_song = null, atom player = null)
    {
        update_appearance();
    }

    // tg: code/modules/instruments/piano_synth.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_instrument_piano_synth_headphones h) => h.icon_state), Dm.truthy(song?.playing) ? "_on" : null);
        return dot;
    }
}

// tg: code/modules/instruments/piano_synth.dm:70
public partial class obj_item_circuit_component_synth : obj_item_circuit_component
{
    // tg: code/modules/instruments/piano_synth.dm:108
    public override async DmTask populate_ports()
    {
        song = add_input_port("Song", PORT_TYPE_LIST(PORT_TYPE_STRING), trigger: nameof(import_song)) as datum_port_input;
        play = add_input_port("Play", PORT_TYPE_SIGNAL, trigger: nameof(start_playing)) as datum_port_input;
        stop = add_input_port("Stop", PORT_TYPE_SIGNAL, trigger: nameof(stop_playing)) as datum_port_input;
        repetitions = add_input_port("Repetitions", PORT_TYPE_NUMBER, trigger: nameof(set_repetitions)) as datum_port_input;
        beats_per_min = add_input_port("BPM", PORT_TYPE_NUMBER, trigger: nameof(set_bpm)) as datum_port_input;
        selected_instrument = add_option_port("Selected Instrument", SSinstruments.synthesizer_instrument_ids, trigger: nameof(set_instrument)) as datum_port_input_option;
        volume = add_input_port("Volume", PORT_TYPE_NUMBER, trigger: nameof(set_volume)) as datum_port_input;
        volume_dropoff = add_input_port("Volume Dropoff Threshold", PORT_TYPE_NUMBER, trigger: nameof(set_dropoff)) as datum_port_input;
        note_shift = add_input_port("Note Shift", PORT_TYPE_NUMBER, trigger: nameof(set_note_shift)) as datum_port_input;
        sustain_mode = add_option_port("Note Sustain Mode", SSinstruments.note_sustain_modes, trigger: nameof(set_sustain_mode)) as datum_port_input_option;
        sustain_value = add_input_port("Note Sustain Value", PORT_TYPE_NUMBER, trigger: nameof(set_sustain_value)) as datum_port_input;
        note_decay = add_input_port("Held Note Decay", PORT_TYPE_NUMBER, trigger: nameof(set_sustain_decay)) as datum_port_input;

        is_playing = add_output_port("Currently Playing", PORT_TYPE_BOOLEAN) as datum_port_output;
        started_playing = add_output_port("Started Playing", PORT_TYPE_SIGNAL) as datum_port_output;
        stopped_playing = add_output_port("Stopped Playing", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/instruments/piano_synth.dm:126
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        synth = shell as obj_item_instrument_piano_synth;
        RegisterSignal(synth, COMSIG_INSTRUMENT_START, nameof(on_song_start));
        RegisterSignal(synth, COMSIG_INSTRUMENT_END, nameof(on_song_end));
        RegisterSignal(synth, COMSIG_INSTRUMENT_SHOULD_STOP_PLAYING, nameof(continue_if_autoplaying));
    }

    // tg: code/modules/instruments/piano_synth.dm:133
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        if (synth.song.music_player == this)
            synth.song.stop_playing();
        UnregisterSignal(synth, Dm.list(COMSIG_INSTRUMENT_START, COMSIG_INSTRUMENT_END, COMSIG_INSTRUMENT_SHOULD_STOP_PLAYING));
        synth = null;
        await base.unregister_shell(shell);
    }

    // tg: code/modules/instruments/piano_synth.dm:140
    public virtual async DmTask start_playing(datum_port_input port = null)
    {
        synth.song.start_playing(this);
    }

    // tg: code/modules/instruments/piano_synth.dm:143
    [SignalHandler]
    public virtual void on_song_start(datum source = null, datum starting_song = null, atom player = null)
    {
        is_playing.set_output(TRUE);
        started_playing.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(beats_per_min.value))
            synth.song.set_bpm(beats_per_min.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:150
    [SignalHandler]
    public virtual object continue_if_autoplaying(datum source = null, atom music_player = null)
    {
        if (music_player == this)
            return IGNORE_INSTRUMENT_CHECKS;
        return null;
    }

    // tg: code/modules/instruments/piano_synth.dm:155
    public virtual void stop_playing(datum_port_input port = null)
    {
        synth.song.stop_playing();
    }

    // tg: code/modules/instruments/piano_synth.dm:158
    [SignalHandler]
    public virtual void on_song_end()
    {
        is_playing.set_output(FALSE);
        stopped_playing.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/instruments/piano_synth.dm:163
    public virtual void import_song()
    {
        synth.song.ParseSong(new_song: song.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:166
    public virtual void set_repetitions()
    {
        synth.song.set_repeats(repetitions.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:169
    public virtual void set_bpm()
    {
        if (Dm.truthy(beats_per_min.value))
            synth.song.set_bpm(beats_per_min.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:173
    public virtual void set_instrument()
    {
        synth.song.set_instrument(selected_instrument.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:176
    public virtual void set_volume()
    {
        synth.song.set_volume(volume.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:179
    public virtual void set_dropoff()
    {
        synth.song.set_dropoff_volume(volume_dropoff.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:182
    public virtual void set_note_shift()
    {
        synth.song.note_shift = clamp(Dm.num(note_shift.value), synth.song.note_shift_min, synth.song.note_shift_max);
    }

    // tg: code/modules/instruments/piano_synth.dm:185
    public virtual void set_sustain_mode()
    {
        if (!Dm.@in(sustain_mode.value, SSinstruments.note_sustain_modes))
            return;
        synth.song.sustain_mode = (string)sustain_mode.value;
    }

    // tg: code/modules/instruments/piano_synth.dm:190
    public virtual void set_sustain_value()
    {
        string sw__1 = synth.song.sustain_mode;
        if (sw__1 == SUSTAIN_LINEAR)
            synth.song.set_linear_falloff_duration(sustain_value.value);
        else if (sw__1 == SUSTAIN_EXPONENTIAL)
            synth.song.set_exponential_drop_rate(sustain_value.value);
    }

    // tg: code/modules/instruments/piano_synth.dm:197
    public virtual void set_sustain_decay()
    {
        synth.song.full_sustain_held_note = Dm.not(Dm.not(synth.song.full_sustain_held_note));
    }
}
