// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/ambience.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D296): SSambience plays each listening client its area's ambience every so often (CHANNEL_AMBIENCE), and
// refresh_looping_ambience keeps the area's buzz looping on CHANNEL_BUZZ while the area's APC powers its environment. The var
// block is generated (D111).

/// The subsystem used to play ambience to users every now and then, makes them real excited.
// tg: code/controllers/subsystem/ambience.dm:2 SUBSYSTEM_DEF(ambience)
public partial class datum_controller_subsystem_ambience : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(ambience) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSambience)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/ambience.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSambience != this)
        {
            if (SSambience is datum_controller_subsystem_ambience)
            {
                Recover();
                qdel(SSambience);
            }
            SSambience = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/ambience.dm:14
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(resumed))
            currentrun = ambience_listening_clients.Copy();
        DmList cached_clients = currentrun;

        while (Dm.truthy(cached_clients.len))
        {
            client client_iterator = cached_clients[cached_clients.len] as client;
            cached_clients.len--;

            //Check to see if the client exists
            if (client_iterator == null)
            {
                Dm.remove(ref ambience_listening_clients, client_iterator);
                Dm.remove(ref client_old_areas, client_iterator);
                continue;
            }

            // skip them this tick if they're on the lobby screen or somehow dont have a mob??
            mob client_mob = client_iterator?.mob;
            if (client_mob == null || Dm.truthy(isnewplayer(client_mob)))
                continue;

            if (HAS_TRAIT(client_mob, TRAIT_DEAF)) //WHAT? I CAN'T HEAR YOU
                continue;

            //Check to see if the client-mob is in a valid area
            area current_area = get_area(client_mob) as area;
            if (current_area == null) //Something's gone horribly wrong
            {
                stack_trace(Dm.text("[] has somehow ended up in nullspace. WTF did you do", key_name(client_mob)), "code/controllers/subsystem/ambience.dm", 41);
                remove_ambience_client(client_iterator);
                continue;
            }

            if (Dm.num(ambience_listening_clients[client_iterator]) > world.time)
            {
                // DEVIATION(DV229): a forced area's track waits for the playing one's end instead of cutting it
                if (!(Dm.truthy(current_area.forced_ambience) && (client_old_areas?[client_iterator] != current_area)
                    && Dm.num(port_track_ends?[client_iterator]) <= world.time && prob(5)))
                    continue;
            }

            //Run play_ambience() on the client-mob and set a cooldown
            ambience_listening_clients[client_iterator] = world.time + current_area.play_ambience(client_mob);

            //We REALLY don't want runtimes in SSambience
            if (client_iterator != null)
                client_old_areas[client_iterator] = current_area;

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    /// <summary>DV229 (port only): when each listening client's last track ends.</summary>
    public DmList port_track_ends;

    // tg: code/controllers/subsystem/ambience.dm:74
    public virtual void remove_ambience_client(client to_remove = null)
    {
        if (port_track_ends != null)
            Dm.remove(ref port_track_ends, to_remove);
        Dm.remove(ref ambience_listening_clients, to_remove);
        Dm.remove(ref client_old_areas, to_remove);
        Dm.remove(ref currentrun, to_remove);
    }
}

// tg: code/controllers/subsystem/ambience.dm:59  (the /area procs of this file, rules 1.9 "interleaved types")
public partial class area : atom
{
    ///Attempts to play an ambient sound to a mob, returning the cooldown in deciseconds
    // tg: code/controllers/subsystem/ambience.dm:59
    public virtual float play_ambience(mob M = null, object override_sound = null, float? volume = null)
    {
        volume ??= 27;
        object new_sound = Dm.truthy(override_sound) ? override_sound : pick(ambientsounds);
        if (!Dm.truthy(new_sound)) // Dont try to play a sound if we dont have any.
            return 1 * MINUTES;
        /// volume modifier for ambience as set by the player in preferences.
        float volume_modifier = Dm.num(M.client?.prefs.read_preference(typeof(datum_preference_numeric_volume_sound_ambience_volume))) / 100;
        sound played = sound(new_sound, repeat: 0, wait: 0, volume: volume * volume_modifier, channel: CHANNEL_AMBIENCE);
        Dm.output_to(M, played); // SEND_SOUND(M, new_sound) (DIRECT_OUTPUT, rules 12)

        float sound_length = Dm.num(SSsounds.get_sound_length(played.file));
        if (M.client != null && SSambience != null) // DV229: the track's end, which a forced area's next track waits for
            (SSambience.port_track_ends ??= Dm.list())[M.client] = world.time + sound_length;
        if (!Dm.truthy(sound_length))
        {
            // This will cause sounds to cut into eachother if the sound is longer then the min_ambience_cooldown
            stack_trace(Dm.text("play_ambience failed to get soundlength from [] with a file of [].", played, played.file), "code/controllers/subsystem/ambience.dm", 70);
        }
        return sound_length + rand(min_ambience_cooldown, max_ambience_cooldown);
    }
}

// tg: code/controllers/subsystem/ambience.dm:79
public partial class area_station_maintenance : area_station
{
    ///A list of rare sound effects to fuck with players. No, it does not contain actual minecraft sounds anymore.
    // tg: code/controllers/subsystem/ambience.dm:82  var/static/list/minecraft_cave_noises (rules 10.6: a TgState field, below)
    public static ref DmList minecraft_cave_noises => ref TgState.Current.area_station_maintenance__minecraft_cave_noises;

    // tg: code/controllers/subsystem/ambience.dm:95
    public override float play_ambience(mob M = null, object override_sound = null, float? volume = null)
    {
        if (!Dm.truthy(M.has_light_nearby()) && prob(0.5f))
            return base.play_ambience(M, pick(minecraft_cave_noises));
        return base.play_ambience(M, override_sound, volume);
    }
}

// tg: code/controllers/subsystem/ambience.dm:104
public partial class mob : atom_movable
{
    /**
     * Ambience buzz handling called by either area/Enter() or refresh_looping_ambience()
     */
    // tg: code/controllers/subsystem/ambience.dm:104
    public virtual void update_ambience_area(area new_area = null)
    {
        object old_tracked_area = ambience_tracked_area;
        if (Dm.truthy(old_tracked_area))
        {
            UnregisterSignal(old_tracked_area as datum, COMSIG_AREA_POWER_CHANGE);
            ambience_tracked_area = null;
        }
        if (!Dm.truthy(client))
            return;
        if (new_area != null)
        {
            ambience_tracked_area = new_area;
            RegisterSignal(ambience_tracked_area, COMSIG_AREA_POWER_CHANGE, nameof(refresh_looping_ambience), TRUE);
        }

        refresh_looping_ambience();
    }

    // tg: code/controllers/subsystem/ambience.dm:118
    [SignalHandler]
    public virtual void refresh_looping_ambience(params object[] __extra)
    {
        if (!Dm.truthy(client) || Dm.truthy(isobserver(client.mob))) // If a tree falls in the woods. sadboysuss: Don't refresh for ghosts, it sounds bad
            return;

        // my_area?, prefs?: a mob in nullspace (tg runtimes there) and the tests' clients without preferences get no buzz
        area my_area = get_area(this) as area;
        object sound_to_use = my_area?.ambient_buzz;
        float volume_modifier = Dm.num(client.prefs?.read_preference(typeof(datum_preference_numeric_volume_sound_ship_ambience_volume)));

        if (!Dm.truthy(sound_to_use) || !Dm.truthy(client.prefs?.read_preference(typeof(datum_preference_numeric_volume_sound_ship_ambience_volume))))
        {
            Dm.output_to(this, sound(null, repeat: 0, wait: 0, channel: CHANNEL_BUZZ)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            client.current_ambient_sound = null;
            return;
        }

        if (HAS_TRAIT(this, TRAIT_DEAF)) // Can the mob hear?
        {
            Dm.output_to(this, sound(null, repeat: 0, wait: 0, channel: CHANNEL_BUZZ)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            client.current_ambient_sound = null;
            return;
        }

        //Station ambience is dependent on a functioning and charged APC with environment power enabled.
        if (!Dm.truthy(is_mining_level(my_area.z)) && (my_area.apc == null || !Dm.truthy(my_area.apc.operating)
            || (!Dm.truthy(my_area.apc.cell?.charge) && Dm.truthy(my_area.requires_power)) || !Dm.truthy(my_area.power_environ)))
        {
            Dm.output_to(this, sound(null, repeat: 0, wait: 0, channel: CHANNEL_BUZZ)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            client.current_ambient_sound = null;
            return;
        }
        else
        {
            if (Dm.eq(sound_to_use, client.current_ambient_sound)) // Don't reset current loops
                return;

            client.current_ambient_sound = sound_to_use;
            Dm.output_to(this, sound(my_area.ambient_buzz, repeat: 1, wait: 0, volume: my_area.ambient_buzz_vol * (volume_modifier / 100), channel: CHANNEL_BUZZ)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/ambience.dm:2  SUBSYSTEM_DEF(ambience): GLOBAL_REAL(SSambience, /datum/controller/subsystem/ambience)
    internal datum_controller_subsystem_ambience SSambience;

    // tg: code/controllers/subsystem/ambience.dm:82  var/static/list/minecraft_cave_noises
    internal DmList area_station_maintenance__minecraft_cave_noises = Dm.list(
        Dm.resource("sound/machines/airlock/airlock.ogg"),
        Dm.resource("sound/effects/snap.ogg"),
        Dm.resource("sound/effects/footstep/clownstep1.ogg"),
        Dm.resource("sound/effects/footstep/clownstep2.ogg"),
        Dm.resource("sound/items/tools/welder.ogg"),
        Dm.resource("sound/items/tools/welder2.ogg"),
        Dm.resource("sound/items/tools/crowbar.ogg"),
        Dm.resource("sound/items/deconstruct.ogg"),
        Dm.resource("sound/ambience/misc/source_holehit3.ogg"),
        Dm.resource("sound/ambience/misc/cavesound3.ogg")
    );
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_ambience SSambience => ref TgState.Current.SSambience;
}
