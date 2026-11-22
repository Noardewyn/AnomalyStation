// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/jukebox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the music player datum that plays its track to the mobs in range (each listener's sound
// placed relative to the player, muted out of range, when deaf or by the jukebox volume preference), the single-mob player and the
// tracks. The var blocks are generated (D111). Tracks from the server's config folder play only if the clients have the file (DV490).

/**
 * ## Jukebox datum
 *
 * Plays music to nearby mobs when hosted in a movable or a turf.
 */
// tg: code/datums/components/jukebox.dm:14
public partial class datum_jukebox : datum
{
    // Reasons for appling STATUS_MUTE to a mob's sound status (the file's #defines, #undef'd at its end)
    // tg: code/datums/components/jukebox.dm:3  #define MUTE_DEAF (1<<0)
    /// The mob is deaf
    private const float MUTE_DEAF = 1 << 0;
    // tg: code/datums/components/jukebox.dm:5  #define MUTE_PREF (1<<1)
    /// The mob has disabled jukeboxes in their preferences
    private const float MUTE_PREF = 1 << 1;
    // tg: code/datums/components/jukebox.dm:7  #define MUTE_RANGE (1<<2)
    /// The mob is out of range of the jukebox
    private const float MUTE_RANGE = 1 << 2;

    // tg: code/datums/components/jukebox.dm:43
    [DmParams("new_parent")]
    public override async DmTask New(params object[] __extra)
    {
        var new_parent = Dm.arg(__extra, 0) as atom;
        if (!ismovable(new_parent) && !isturf(new_parent))
        {
            stack_trace(Dm.text("[] created on non-turf or non-movable: [])", type, new_parent != null ? Dm.text("[] ([])", new_parent, new_parent.type) : "null"),
                file: "code/datums/components/jukebox.dm", line: 45);
            qdel(this);
            return;
        }

        parent = new_parent;

        if (sound_range == null)
        {
            sound_range = world.view;
            DmList worldviewsize = getviewsize(sound_range);
            x_cutoff = ceil(Dm.num(worldviewsize[1]) * 1.25f / 2); // * 1.25 gives us some extra range to fade out with
            z_cutoff = ceil(Dm.num(worldviewsize[2]) * 1.25f / 2); // and / 2 is because world view is the whole screen, and we want the centre
        }

        if (Dm.truthy(requires_range_check))
            await AddComponent(typeof(datum_component_connect_range), parent, New__connections, max(Dm.num(x_cutoff), Dm.num(z_cutoff)));

        songs = init_songs() as DmList;
        if (Dm.truthy(length(songs)))
            selection = songs[pick(songs)] as datum_track;

        RegisterSignal(parent, COMSIG_ENTER_AREA, nameof(on_enter_area));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(parent, COMSIG_QDELETING, nameof(parent_delete));
    }

    // tg: code/datums/components/jukebox.dm:56  var/static/list/connections = list(COMSIG_ATOM_ENTERED = PROC_REF(check_new_listener))
    private static ref DmList New__connections => ref TgState.Current.datum_jukebox__New__connections;

    // tg: code/datums/components/jukebox.dm:69
    public override object Destroy(float? force = null)
    {
        unlisten_all();
        parent = null;
        selection = null;
        songs.Cut();
        active_song_sound = null;
        return base.Destroy(force);
    }

    /// When our parent is deleted, we should go too.
    // tg: code/datums/components/jukebox.dm:78
    [SignalHandler]
    public virtual void parent_delete(datum source = null)
    {
        qdel(this);
    }

    /**
     * Initializes the track list.
     *
     * By default, this loads all tracks from the config datum.
     *
     * Returns
     * * An assoc list of track names to /datum/track. Track names must be unique.
     */
    // tg: code/datums/components/jukebox.dm:90
    public virtual object init_songs()
    {
        return load_songs_from_config();
    }

    /// Loads the config sounds once, and returns a copy of them.
    // tg: code/datums/components/jukebox.dm:94
    public virtual object load_songs_from_config(float? force = null)
    {
        force ??= FALSE;
        ref DmList config_songs = ref load_songs_from_config__config_songs;
        if (config_songs == null || Dm.truthy(force))
        {
            config_songs = Dm.list();
            DmList tracks = flist(CONFIG_JUKEBOX_SOUNDS);
            foreach (object track_file in Dm.each<object>(tracks))
            {
                var new_track = Dm.@new<datum_track>();
                new_track.song_path = file(Dm.text("[][]", CONFIG_JUKEBOX_SOUNDS, track_file));
                DmList track_data = splittext(track_file, "+") as DmList;
                if (!Dm.truthy(length(track_data)) || !Dm.truthy(IS_SOUND_FILE_SAFE(new_track.song_path)))
                    continue;
                object track_name = track_data[JUKEBOX_NAME];
                track_name = strip_filepath_extension(track_name, datum_controller_subsystem_sounds.safe_formats);
                new_track.song_name = Dm.str(track_name);
                new_track.song_length = Dm.num(SSsounds.get_sound_length(new_track.song_path));
                if (track_data.len >= 3) // Bandaid for legacy tracks to not use the length for the bpm rather then the actual beats.
                {
                    ref float logged_to_admins = ref load_songs_from_config__logged_to_admins;
                    log_game(Dm.text("[] track data seems to be using the legacy format; we will attempt to make it work.", new_track.song_path));
                    if (!Dm.truthy(logged_to_admins))
                    {
                        message_admins("The jukebox has tracks uploaded in a legacy format. Length is now fetched programmatically, with title and beats being the only required fields.");
                        logged_to_admins = TRUE;
                    }
                    new_track.song_beat_deciseconds = Dm.num(text2num(track_data[3]));
                }
                else if (track_data.len >= 2)
                    new_track.song_beat_deciseconds = Dm.num(text2num(track_data[JUKEBOX_BEATS]));
                config_songs[new_track.song_name] = new_track;
            }

            if (!Dm.truthy(length(config_songs)))
            {
                var default_track = Dm.@new<datum_track_default>();
                config_songs[default_track.song_name] = default_track;
            }
        }

        // returns a copy so it can mutate if desired.
        return config_songs.Copy();
    }

    // tg: code/datums/components/jukebox.dm:94  var/static/list/config_songs
    private static ref DmList load_songs_from_config__config_songs => ref TgState.Current.datum_jukebox__load_songs_from_config__config_songs;
    // tg: code/datums/components/jukebox.dm:107  var/static/logged_to_admins = FALSE
    private static ref float load_songs_from_config__logged_to_admins => ref TgState.Current.datum_jukebox__load_songs_from_config__logged_to_admins;

    // tg: code/__DEFINES/jukebox.dm:1  #define CONFIG_JUKEBOX_SOUNDS "[global.config.directory]/jukebox_music/sounds/"
    private static string CONFIG_JUKEBOX_SOUNDS => Dm.text("[]/jukebox_music/sounds/", config.directory);

    /**
     * Returns a set of general data relating to the jukebox for use in TGUI.
     *
     * Returns
     * * A list of UI data
     */
    // tg: code/datums/components/jukebox.dm:133
    public virtual object get_ui_data()
    {
        DmList data = Dm.list();
        DmList songs_data = Dm.list();
        foreach (object song_name in Dm.each<object>(songs))
        {
            var one_song = songs[song_name] as datum_track;
            UNTYPED_LIST_ADD(ref songs_data, Dm.list(
                Dm.kv("name", song_name),
                Dm.kv("length", DisplayTimeText(one_song.song_length)),
                Dm.kv("beat", Dm.truthy(one_song.song_beat_deciseconds) ? one_song.song_beat_deciseconds : "Unknown")));
        }

        data["active"] = active_song_sound != null ? TRUE : FALSE;
        data["songs"] = songs_data;
        data["track_selected"] = selection?.song_name;
        data["looping"] = sound_loops;
        data["volume"] = volume;
        return data;
    }

    /**
     * Sets the sound's range to a new value. This can be a number or a view size string "XxY".
     * Then updates any mobs listening to it.
     */
    // tg: code/datums/components/jukebox.dm:155
    public virtual void set_sound_range(object new_range = null)
    {
        if (Dm.eq(sound_range, new_range))
            return;
        sound_range = new_range;
        DmList worldviewsize = getviewsize(sound_range);
        x_cutoff = ceil(Dm.num(worldviewsize[1]) / 2);
        z_cutoff = ceil(Dm.num(worldviewsize[2]) / 2);
        update_all();
    }

    /**
     * Sets the sound's volume to a new value.
     * Then updates any mobs listening to it.
     */
    // tg: code/datums/components/jukebox.dm:168
    public virtual void set_new_volume(object new_vol = null)
    {
        float vol = clamp(Dm.num(new_vol), 0, Dm.num(Dm.initial(GetType(), (datum_jukebox j) => j.volume)));
        if (volume == vol)
            return;
        volume = vol;
        if (active_song_sound == null)
            return;
        active_song_sound.volume = vol;
        update_all();
    }

    /// Sets volume to the maximum possible value, the initial volume value.
    // tg: code/datums/components/jukebox.dm:179
    public virtual void set_volume_to_max()
    {
        set_new_volume(Dm.initial(GetType(), (datum_jukebox j) => j.volume));
    }

    /**
     * Sets the sound's environment to a new value.
     * Then updates any mobs listening to it.
     */
    // tg: code/datums/components/jukebox.dm:186
    public virtual void set_new_environment(object new_env = null)
    {
        if (active_song_sound == null || Dm.eq(active_song_sound.environment, new_env))
            return;
        active_song_sound.environment = new_env;
        update_all();
    }

    /// Helper to stop the music for all mobs listening to the music.
    // tg: code/datums/components/jukebox.dm:193
    public virtual void unlisten_all()
    {
        foreach (mob listening in Dm.each_cast<mob>(listeners.Copy()))
            deregister_listener(listening);
        active_song_sound = null;
    }

    /// Helper to update all mobs currently listening to the music.
    // tg: code/datums/components/jukebox.dm:199
    public virtual void update_all()
    {
        foreach (mob listening in Dm.each_cast<mob>(listeners.Copy()))
            update_listener(listening);
    }

    /// Helper to kickstart the music for all mobs in hearing range of the jukebox.
    // tg: code/datums/components/jukebox.dm:204
    public virtual void start_music(params object[] __extra)
    {
        foreach (mob nearby in Dm.each<mob>(hearers(sound_range, parent)))
            register_listener(nearby);
    }

    /// Helper to get all mobs currently, ACTIVELY listening to the jukebox.
    // tg: code/datums/components/jukebox.dm:209
    public virtual object get_active_listeners()
    {
        DmList all_listeners = Dm.list();
        foreach (mob listener in Dm.each_cast<mob>(listeners))
        {
            if (Dm.truthy(Dm.bitand(listeners[listener], SOUND_MUTE)))
                continue;
            Dm.append(ref all_listeners, listener);
        }
        return all_listeners;
    }

    /// Registers the passed mob as a new listener to the jukebox.
    // tg: code/datums/components/jukebox.dm:218
    public virtual void register_listener(mob new_listener = null)
    {
        listeners[new_listener] = NONE;
        RegisterSignal(new_listener, COMSIG_QDELETING, nameof(listener_deleted));

        if (new_listener.client == null)
        {
            RegisterSignal(new_listener, COMSIG_MOB_LOGIN, nameof(listener_login));
            return;
        }

        RegisterSignals(new_listener, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOB_JUKEBOX_PREFERENCE_APPLIED), nameof(listener_moved));
        RegisterSignals(new_listener, Dm.list(SIGNAL_ADDTRAIT(TRAIT_DEAF), SIGNAL_REMOVETRAIT(TRAIT_DEAF)), nameof(listener_deaf));
        object pref_volume = new_listener.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_jukebox));
        if (HAS_TRAIT(new_listener, TRAIT_DEAF) || !Dm.truthy(pref_volume))
            listeners[new_listener] = Dm.bitor(listeners[new_listener], SOUND_MUTE);

        if (active_song_sound == null)
        {
            var juke_area = get_area(parent) as area;
            active_song_sound = sound(selection.song_path);
            active_song_sound.channel = CHANNEL_JUKEBOX;
            active_song_sound.priority = 255;
            active_song_sound.falloff = 2;
            active_song_sound.volume = Dm.num(volume) * (Dm.num(pref_volume) / 100);
            active_song_sound.y = 1;
            active_song_sound.environment = Dm.truthy(juke_area.sound_environment) ? juke_area.sound_environment : SOUND_ENVIRONMENT_NONE;
            active_song_sound.repeat = sound_loops;
        }

        update_listener(new_listener);
        // if you have a sound with status SOUND_UPDATE,
        // and try to play it to a client who is not listening to the sound already,
        // it will not work.
        // so we only add this status AFTER the first update, which plays the first sound.
        // and after that it's fine to keep it on the sound so it updates as the x/z does.
        listeners[new_listener] = Dm.bitor(listeners[new_listener], SOUND_UPDATE);
    }

    /// Deregisters mobs on deletion.
    // tg: code/datums/components/jukebox.dm:254
    [SignalHandler]
    public virtual void listener_deleted(mob source = null)
    {
        deregister_listener(source);
    }

    /// Updates the sound's position on mob movement.
    // tg: code/datums/components/jukebox.dm:259
    [SignalHandler]
    public virtual void listener_moved(mob source = null)
    {
        update_listener(source);
    }

    /// Allows mobs who are clientless when the music starts to hear it when they log in.
    // tg: code/datums/components/jukebox.dm:264
    [SignalHandler]
    public virtual void listener_login(mob source = null)
    {
        deregister_listener(source);
        register_listener(source);
    }

    /// Updates the sound's mute status when the mob's deafness updates.
    // tg: code/datums/components/jukebox.dm:270
    [SignalHandler]
    public virtual void listener_deaf(mob source = null)
    {
        if (HAS_TRAIT(source, TRAIT_DEAF))
            listeners[source] = Dm.bitor(listeners[source], SOUND_MUTE);
        else if (!Dm.truthy(unmute_listener(source, MUTE_DEAF)))
            return;
        update_listener(source);
    }

    /**
     * Unmutes the passed mob's sound from the passed reason.
     *
     * Arguments
     * * mob/listener - The mob to unmute.
     * * reason - The reason to unmute them for. Can be a combination of MUTE_DEAF, MUTE_PREF, MUTE_RANGE.
     */
    // tg: code/datums/components/jukebox.dm:286
    public virtual object unmute_listener(mob listener = null, object reason = null)
    {
        // We need to check everything BUT the reason we're unmuting for
        // Because if we're muted for a different reason we don't wanna touch it
        reason = Dm.bitnot(reason);

        if (Dm.truthy(Dm.bitand(reason, MUTE_DEAF)) && HAS_TRAIT(listener, TRAIT_DEAF))
            return FALSE;
        object pref_volume = listener.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_jukebox));
        if (Dm.truthy(Dm.bitand(reason, MUTE_PREF)) && !Dm.truthy(pref_volume))
            return FALSE;

        if (Dm.truthy(Dm.bitand(reason, MUTE_RANGE)))
        {
            var sound_turf = get_turf(parent);
            var listener_turf = get_turf(listener);
            if (sound_turf == null || listener_turf == null)
                return FALSE;
            if (sound_turf.z != listener_turf.z)
                return FALSE;
            if (abs(sound_turf.x - listener_turf.x) > Dm.num(x_cutoff))
                return FALSE;
            if (abs(sound_turf.y - listener_turf.y) > Dm.num(z_cutoff))
                return FALSE;
        }

        listeners[listener] = Dm.bitand(listeners[listener], Dm.bitnot(SOUND_MUTE));
        return TRUE;
    }

    /// Deregisters the passed mob as a listener to the jukebox, stopping the music.
    // tg: code/datums/components/jukebox.dm:313
    public virtual void deregister_listener(mob no_longer_listening = null)
    {
        Dm.remove(ref listeners, no_longer_listening);
        no_longer_listening.stop_sound_channel(CHANNEL_JUKEBOX);
        UnregisterSignal(no_longer_listening, Dm.list(
            COMSIG_MOB_LOGIN,
            COMSIG_QDELETING,
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOB_JUKEBOX_PREFERENCE_APPLIED,
            SIGNAL_ADDTRAIT(TRAIT_DEAF),
            SIGNAL_REMOVETRAIT(TRAIT_DEAF)));
    }

    /// Updates the passed mob's sound in according to their position and status.
    // tg: code/datums/components/jukebox.dm:328
    public virtual void update_listener(mob listener = null)
    {
        active_song_sound.status = Dm.num(listeners[listener] ?? NONE);

        var sound_turf = get_turf(parent);
        var listener_turf = get_turf(listener);
        if (sound_turf == null || listener_turf == null) // ??
        {
            active_song_sound.x = 0;
            active_song_sound.z = 0;
        }
        else if (sound_turf.z != listener_turf.z) // Could MAYBE model multi-z jukeboxes but that's too complex for now
            listeners[listener] = Dm.bitor(listeners[listener], SOUND_MUTE);
        else
        {
            // keep in mind sound XYZ is different to world XYZ. sound +-z = world +-y
            float new_x = sound_turf.x - listener_turf.x;
            float new_z = sound_turf.y - listener_turf.y;

            if (abs(new_x) > Dm.num(x_cutoff) || abs(new_z) > Dm.num(z_cutoff))
                listeners[listener] = Dm.bitor(listeners[listener], SOUND_MUTE);
            else if (Dm.truthy(Dm.bitand(listeners[listener], SOUND_MUTE)))
                unmute_listener(listener, MUTE_RANGE);

            active_song_sound.x = new_x;
            active_song_sound.z = new_z;

            object pref_volume = listener.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_jukebox));
            if (!Dm.truthy(pref_volume))
                listeners[listener] = Dm.bitor(listeners[listener], SOUND_MUTE);
            else
            {
                unmute_listener(listener, MUTE_PREF);
                active_song_sound.volume = Dm.num(volume) * (Dm.num(pref_volume) / 100);
            }
        }

        Dm.output_to(listener, active_song_sound); // SEND_SOUND(listener, active_song_sound) (DIRECT_OUTPUT, rules 12)
    }

    /// When the jukebox moves, we need to update all listeners.
    // tg: code/datums/components/jukebox.dm:366
    [SignalHandler]
    public virtual void on_moved(datum source = null, params object[] __extra)
    {
        update_all();
    }

    /// When the jukebox enters a new area entirely, we need to update the environment to the new area's.
    // tg: code/datums/components/jukebox.dm:371
    [SignalHandler]
    public virtual void on_enter_area(datum source = null, area area_to_register = null)
    {
        set_new_environment(Dm.truthy(area_to_register.sound_environment) ? area_to_register.sound_environment : SOUND_ENVIRONMENT_NONE);
    }

    /// Check for new mobs entering the jukebox's range.
    // tg: code/datums/components/jukebox.dm:376
    [SignalHandler]
    public virtual void check_new_listener(datum source = null, atom_movable entered = null)
    {
        if (active_song_sound == null)
            return;
        if (!ismob(entered))
            return;
        if (Dm.@in(entered, listeners))
            return;
        register_listener((mob)entered);
    }
}

/**
 * Subtype which only plays the music to the mob you pass in via start_music().
 *
 * Multiple mobs can still listen at once, but you must register them all manually via start_music().
 */
// tg: code/datums/components/jukebox.dm:393
public partial class datum_jukebox_single_mob : datum_jukebox
{
    // tg: code/datums/components/jukebox.dm:395
    [DmParams("solo_listener")]
    public override void start_music(params object[] __extra)
    {
        var solo_listener = Dm.arg(__extra, 0) as mob;
        register_listener(solo_listener);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/jukebox.dm:56  var/static/list/connections
    internal DmList datum_jukebox__New__connections = Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_jukebox.check_new_listener)));
    // tg: code/datums/components/jukebox.dm:94  var/static/list/config_songs
    internal DmList datum_jukebox__load_songs_from_config__config_songs;
    // tg: code/datums/components/jukebox.dm:107  var/static/logged_to_admins = FALSE
    internal float datum_jukebox__load_songs_from_config__logged_to_admins;
}
