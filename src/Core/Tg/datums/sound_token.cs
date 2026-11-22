// Translated from tgstation (snapshot 1d99bd7f): code/datums/sound_token.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio slice 5 (loops and alarms): the whole file. The var block is generated (D111) but for `source` (below).
// The offset tg writes (calculate_offset) is in deciseconds (REALTIMEOFDAY, rust-g's lengths) although BYOND's sound.offset
// is in seconds; the port keeps tg's value and the client reads it as deciseconds (audio plan DV38).

// Sound tokens, a datumized handler for spatial sound.
// Uses the spatial grid to track clients in range and add them as listeners
// Updated by the SSsound_tokens subsystem every tick when requested by client so that if the source or listener moves, the sound updates accordingly.
// tg: code/datums/sound_token.dm:4
[DmPath("/datum/sound_token")]
public partial class datum_sound_token : datum
{
    /// The atom playing the sound.
    // tg: code/datums/sound_token.dm:6  var/atom/source (declared by hand: the types tool reads the name `source` as a
    // typepath var, rules 3.2 V3, because tg assigns typepaths to other vars of that name)
    public atom source;

    // tg: code/datums/sound_token.dm:42
    [DmParams("_source", "_sound", "_range", "_volume", "_falloff_exponent", "_falloff_distance", "_allowed_listeners", "_sound_duration_override", "_delete_on_end", "_repeating")]
    public override async DmTask New(params object[] __extra)
    {
        atom _source = Dm.arg<atom>(__extra, 0);
        object _sound = Dm.arg<object>(__extra, 1);
        object _range = Dm.arg<object>(__extra, 2);
        object _volume = Dm.arg<object>(__extra, 3);
        object _falloff_exponent = Dm.arg<object>(__extra, 4);
        object _falloff_distance = Dm.arg<object>(__extra, 5);
        object _allowed_listeners = Dm.arg<object>(__extra, 6);
        object _sound_duration_override = Dm.arg<object>(__extra, 7);
        object _delete_on_end = Dm.arg<object>(__extra, 8);
        object _repeating = Dm.arg<object>(__extra, 9);
        _range ??= 10;
        _volume ??= 50;
        _falloff_exponent ??= SOUND_FALLOFF_EXPONENT;
        _falloff_distance ??= SOUND_DEFAULT_FALLOFF_DISTANCE;
        source = _source;
        RegisterSignal(source, COMSIG_QDELETING, nameof(source_deleted));
        RegisterSignal(source, COMSIG_MOVABLE_MOVED, nameof(source_moved));
        RegisterSignal(source, COMSIG_ENTER_AREA, nameof(on_enter_area));

        range = _range;
        volume = _volume;
        falloff_exponent = _falloff_exponent;
        falloff_distance = _falloff_distance;
        sound_duration_override = _sound_duration_override;
        if (Dm.truthy(_delete_on_end))
            delete_on_end = Dm.num(_delete_on_end);
        repeating = Dm.num(_repeating); // a null _repeating reads as 0 in the number var (sound.repeat = repeating, :84)

        if (Dm.truthy(_allowed_listeners))
        {
            // tg indexes allowed_listeners while it is still null (no `= list()` at :10), a runtime when a caller passes
            // listeners; kept as tg has it (no translated caller passes them: TTS and emotes are not translated)
            foreach (object allowed_mob in Dm.each(_allowed_listeners))
                allowed_listeners[allowed_mob] = TRUE;
        }

        update_sound(_sound);

        null_sound = sound(channel: Dm.num_or_null(sound_channel));

        cell_tracker = Dm.@new<datum_cell_tracker>(range, range);
        update_tracked_cells();

        RegisterSignal(SSdcs, COMSIG_GLOB_PLAYER_LOGIN, nameof(player_login));
        RegisterSignal(SSdcs, COMSIG_GLOB_PLAYER_LOGOUT, nameof(player_logout));
    }


    // tg: code/datums/sound_token.dm:72
    public override object Destroy(float? force = null)
    {
        foreach (object listener in Dm.each(listeners))
            remove_listener((mob)listener);
        listeners = null;
        source = null;
        return base.Destroy(force);
    }

    ///Lets us update the sound to a new one.
    // tg: code/datums/sound_token.dm:80
    public virtual void update_sound(object _sound = null, float? start_playing = null, object _repeating = null)
    {
        start_playing ??= FALSE;
        if (_repeating != null)
            repeating = Dm.num(_repeating);
        sound = sound(_sound);
        sound.repeat = repeating;
        if (!Dm.truthy(sound_channel))
            sound_channel = SSsounds.reserve_sound_channel_for_datum(this);
        sound.channel = Dm.num(sound_channel);
        sound_duration = Dm.truthy(sound_duration_override) ? sound_duration_override : SSsounds.get_sound_length(_sound);
        start_time = REALTIMEOFDAY();
        if (Dm.truthy(start_playing))
            force_update_all_listeners(FALSE);
        if (Dm.truthy(delete_on_end) && !Dm.truthy(repeating))
            _addtimer(Dm.@new<datum_callback>(this, nameof(on_sound_ended)), Dm.num(sound_duration), Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/datums/sound_token.dm", line: 93);
    }


    /// Updates the data of a listener, or adds them if they are not present.
    // tg: code/datums/sound_token.dm:97
    public virtual object add_or_update_listener(mob listener_mob = null)
    {
        if (listeners[listener_mob] == null)
        {
            if (!Dm.truthy(add_listener(listener_mob)))
                return FALSE;
        }
        else
            update_listener(listener_mob);
        return null;
    }

    /// Adds a listener to the sound. returns TRUE if we already were added, or for some reason couldnt be added.
    // tg: code/datums/sound_token.dm:105
    public virtual object add_listener(mob listener_mob = null)
    {
        if (listeners[listener_mob] != null)
            return TRUE;

        if (!Dm.truthy(listener_mob.client) || isnewplayer(listener_mob))
            return FALSE;

        if (Dm.truthy(allowed_listeners) && !Dm.truthy(allowed_listeners[listener_mob]))
            return FALSE;

        listeners[listener_mob] = NONE;
        LAZYOR(ref listener_mob.sound_tokens, this);
        if (source != listener_mob) //this is possible...yea... :/
            RegisterSignal(listener_mob, COMSIG_QDELETING, nameof(listener_deleted));
        RegisterSignals(listener_mob, Dm.list(SIGNAL_ADDTRAIT(TRAIT_DEAF), SIGNAL_REMOVETRAIT(TRAIT_DEAF)), nameof(listener_deafness_update));
        update_listener(listener_mob, FALSE);
        return TRUE;
    }

    /// Remove a listener from the sound.
    // tg: code/datums/sound_token.dm:124
    public virtual void remove_listener(mob listener_mob = null)
    {

        Dm.remove(ref listeners, listener_mob);
        LAZYREMOVE(ref listener_mob.sound_tokens, this);

        UnregisterSignal(listener_mob, Dm.list(COMSIG_QDELETING, SIGNAL_ADDTRAIT(TRAIT_DEAF), SIGNAL_REMOVETRAIT(TRAIT_DEAF)));
        Dm.output_to(listener_mob, null_sound); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
    }

    // tg: code/datums/sound_token.dm:132
    public virtual void update_listener(mob listener_mob = null, float? update_sound = null)
    {
        update_sound ??= TRUE;
        if (QDELETED(this))
            return;
        if (listeners[listener_mob] == null)
            return;

        turf source_turf = get_turf(source);
        turf listener_turf = get_turf(listener_mob);

        if (!Dm.truthy(source_turf) || !Dm.truthy(listener_turf))
            return;

        object is_muted = Dm.bitand(listeners[listener_mob], SOUND_MUTE);
        float should_be_muted = FALSE;

        if (source_turf.z != listener_turf.z)
            should_be_muted = TRUE;

        float distance = Dm.num(get_dist_euclidean(source_turf, listener_turf));
        if (distance > Dm.num(range))
        {
            should_be_muted = TRUE;
            if (Dm.truthy(should_be_muted) && Dm.truthy(is_muted))
                return;
        }

        should_be_muted = Dm.truthy(should_be_muted) ? should_be_muted : Dm.num(HAS_TRAIT(listener_mob, TRAIT_DEAF));
        if (Dm.truthy(should_be_muted) && Dm.truthy(is_muted))
            return;

        set_listener_status(listener_mob, Dm.truthy(should_be_muted) ? SOUND_MUTE : NONE);
        send_listener_sound(listener_mob, update_sound);
    }

    // tg: code/datums/sound_token.dm:163
    [DmPrivate]
    public virtual void send_listener_sound(mob listener_mob = null, object update_sound = null)
    {

        sound.status = Dm.num(Dm.bitor(sound_status, listeners[listener_mob]));
        if (Dm.truthy(update_sound))
            sound.status = Dm.num(Dm.bitor(sound.status, SOUND_UPDATE));
        else
            sound.offset = Dm.num(calculate_offset());

        if (Dm.truthy(Dm.bitand(sound.status, SOUND_MUTE)))
        {
            Dm.output_to(listener_mob, sound); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            return;
        }

        if (!Dm.truthy(listener_mob.playsound_local(get_turf(source), vol: volume, falloff_exponent: Dm.num_or_null(falloff_exponent), channel: Dm.num_or_null(sound_channel), sound_to_use: sound, max_distance: range, falloff_distance: Dm.num_or_null(falloff_distance), use_reverb: TRUE)))
        {
            sound.status = Dm.num(Dm.bitor(SOUND_UPDATE, SOUND_MUTE));
            Dm.output_to(listener_mob, sound); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
        sound.offset = 0; // sound.offset = null: BYOND's number var reads a null as 0
    }

    // tg: code/datums/sound_token.dm:181
    public virtual void update_all_listeners()
    {
        foreach (mob listener_mob in Dm.each<mob>(listeners))
        {
            if (Dm.truthy(listener_mob.client))
                SSsound_tokens.clients_needing_update[listener_mob.client] = TRUE;
        }
    }

    // tg: code/datums/sound_token.dm:186
    public virtual void force_update_all_listeners(float? update_sound = null)
    {
        update_sound ??= TRUE;
        foreach (mob listener_mob in Dm.each<mob>(listeners))
        {
            if (Dm.truthy(listener_mob.client))
                update_listener(listener_mob, update_sound);
        }
    }

    /// Setter for volume
    // tg: code/datums/sound_token.dm:192
    public virtual void set_volume(object new_volume = null, float? update_listeners = null)
    {
        update_listeners ??= TRUE;
        volume = new_volume;
        if (Dm.truthy(update_listeners))
            update_all_listeners();
    }

    /// Set the status of a listener. Does not update the sound.
    // tg: code/datums/sound_token.dm:198
    public virtual void set_listener_status(mob listener_mob = null, object new_status = null)
    {
        if (listeners[listener_mob] == null)
            return;

        listeners[listener_mob] = new_status;
    }

    /// Respond to TRAIT_DEAF addition/removal
    // tg: code/datums/sound_token.dm:205
    [SignalHandler]
    public virtual void listener_deafness_update(atom_movable source = null)
    {
        update_listener((mob)source);
    }

    // tg: code/datums/sound_token.dm:209
    [SignalHandler]
    public virtual void listener_deleted(datum source = null)
    {
        remove_listener((mob)source);
    }

    /// Respond to any mob in the world being logged into. Only adds if the mob is within range.
    // tg: code/datums/sound_token.dm:214
    [SignalHandler]
    public virtual void player_login(datum source = null, mob player = null)
    {
        turf player_turf = get_turf(player);
        turf source_turf = get_turf(this.source);
        if (!Dm.truthy(player_turf) || !Dm.truthy(source_turf))
            return;
        if (player_turf.z != source_turf.z)
            return;
        if (Dm.num(get_dist_euclidean(source_turf, player_turf)) > Dm.num(range))
            return;
        add_or_update_listener(player);
    }

    /// Respond to any cliented mob becoming uncliented
    // tg: code/datums/sound_token.dm:227
    [SignalHandler]
    public virtual void player_logout(datum source = null, mob player = null)
    {
        remove_listener(player);
    }

    /// If the sound source moves, update tracked cells then refresh all listener positions.
    // tg: code/datums/sound_token.dm:232
    [SignalHandler]
    public virtual void source_moved()
    {
        update_tracked_cells();
        update_all_listeners();
    }

    // tg: code/datums/sound_token.dm:237
    [SignalHandler]
    public virtual void source_deleted()
    {

        qdel(this);
    }

    ///Update env when source is entering new area
    // tg: code/datums/sound_token.dm:243
    [SignalHandler]
    public virtual void on_enter_area(datum source = null, area area_to_register = null)
    {
        set_new_environment(Dm.truthy(area_to_register.sound_environment) ? area_to_register.sound_environment : SOUND_ENVIRONMENT_NONE);
    }

    // tg: code/datums/sound_token.dm:247
    public virtual void set_new_environment(object new_env = null)
    {
        if (Dm.eq(sound.environment, new_env))
            return;
        sound.environment = new_env;
        update_all_listeners();
    }

    ///Calculates the offset to give the sound for people who start hearing it mid-play
    // tg: code/datums/sound_token.dm:254
    public virtual object calculate_offset()
    {
        float elapsed = REALTIMEOFDAY() - Dm.num(start_time);
        float freq_factor = (Dm.truthy(sound.frequency) ? sound.frequency : 100) / 100f;
        float pitch_factor = (Dm.truthy(sound.pitch) ? sound.pitch : 100) / 100f;
        object offset = elapsed * freq_factor * pitch_factor;
        if (Dm.truthy(repeating) && Dm.truthy(sound_duration))
            offset = Dm.mod(offset, sound_duration);
        return offset;
    }

    ///Update tracked cells; happens on movement. We need to check if anyone is now out of cell range and kick them out.
    // tg: code/datums/sound_token.dm:264
    public virtual void update_tracked_cells()
    {
        if (!Dm.truthy(get_turf(source)))
            return;

        DmList new_and_old = cell_tracker.recalculate_cells(get_turf(source)) as DmList;
        DmList added_cells = new_and_old[1] as DmList;
        DmList removed_cells = new_and_old[2] as DmList;

        foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(removed_cells))
            UnregisterSignal(cell, Dm.list(SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), SPATIAL_GRID_CELL_EXITED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS)));

        // Remove listeners whose mob is no longer in any remaining member cell
        if (Dm.truthy(removed_cells.len))
        {
            foreach (mob listener_mob in Dm.each_cast<mob>(listeners))
            {
                float still_in_range = FALSE;
                foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(cell_tracker.member_cells))
                {
                    if (Dm.@in(listener_mob, cell.client_contents))
                    {
                        still_in_range = TRUE;
                        break;
                    }
                }
                if (!Dm.truthy(still_in_range))
                    remove_listener(listener_mob);
            }
        }

        foreach (datum_spatial_grid_cell cell in Dm.each_cast<datum_spatial_grid_cell>(added_cells))
        {
            RegisterSignal(cell, SPATIAL_GRID_CELL_ENTERED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), nameof(on_cell_client_entered));
            RegisterSignal(cell, SPATIAL_GRID_CELL_EXITED(SPATIAL_GRID_CONTENTS_TYPE_CLIENTS), nameof(on_cell_client_exited));
            foreach (mob listener_mob in Dm.each_cast<mob>(cell.client_contents))
                add_or_update_listener(listener_mob);
        }
    }

    /// Signal handler for SPATIAL_GRID_CELL_ENTERED on tracked cells. Adds newly arriving mobs as listeners.
    // tg: code/datums/sound_token.dm:293
    [SignalHandler]
    public virtual void on_cell_client_entered(datum source = null, DmList entering_mobs = null)
    {

        foreach (mob listener_mob in Dm.each_cast<mob>(entering_mobs))
        {
            if (listeners[listener_mob] != null) // already added
                continue;
            add_or_update_listener(listener_mob);
        }
    }

    /// Signal handler for SPATIAL_GRID_CELL_EXITED on tracked cells. Removes mobs who have left all member cells.
    // tg: code/datums/sound_token.dm:302
    [SignalHandler]
    public virtual void on_cell_client_exited(datum source = null, DmList exiting_mobs = null)
    {
        foreach (mob listener_mob in Dm.each_cast<mob>(exiting_mobs))
        {
            float still_in_range = FALSE;
            if (Dm.@in(SSspatial_grid.get_cell_of(listener_mob), cell_tracker.member_cells))
                still_in_range = TRUE;

            if (!Dm.truthy(still_in_range))
                remove_listener(listener_mob);
        }
    }

    ///The sound should have ended on all clients. Time to destroy the sound token.
    // tg: code/datums/sound_token.dm:313
    public virtual void on_sound_ended()
    {
        qdel(this);
    }
}
