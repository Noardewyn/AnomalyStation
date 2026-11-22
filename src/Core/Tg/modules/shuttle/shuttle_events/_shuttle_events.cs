// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/shuttle_events/_shuttle_events.dm, turbulence.dm, misc.dm, carp.dm, projectile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D299, DV238: the emergency shuttle's flight events. An event whose spawns are not ported yet (the fake bomb, fireballs) or that needs a ghost poll (player_controlled) is not
// selected (obj_docking_port_mobile_emergency.setup_shuttle_events): it joins as soon as what it spawns turns live.

// tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:2
public partial class datum_shuttle_event : datum
{
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:18
    [DmParams("port")]
    public override async DmTask New(params object[] __extra)
    {
        port = Dm.arg<obj_docking_port_mobile>(__extra, 0);
    }

    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:23
    public virtual void start_up_event(float evacuation_duration = 0)
    {
        if (Dm.eq(port.launch_status, ENDGAME_LAUNCHED))
        {
            active = TRUE; //if added during endgame, instant activate
            activate();
        }
        else
        {
            activate_at = Dm.world.time + evacuation_duration * activation_fraction;
        }
    }

    ///We got activated
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:31
    public virtual object activate() => null;

    ///Process with the SShutle subsystem. Return SHUTTLE_EVENT_CLEAR to self-destruct
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:35
    public virtual object event_process()
    {
        object dot = TRUE;

        if (!Dm.truthy(active))
        {
            if (Dm.world.time < Dm.num(activate_at))
                return FALSE;
            active = TRUE;
            dot = activate();
        }
        return dot;
    }
}

///Spawns objects, mobs, whatever with all the necessary code to make it hit and/or miss the shuttle
// tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:46
public partial class datum_shuttle_event_simple_spawner : datum_shuttle_event
{
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:64
    public override void start_up_event(float evacuation_duration = 0)
    {
        base.start_up_event(evacuation_duration);

        generate_spawning_turfs((DmList)port.return_coords(), spawning_flags, port.preferred_direction);
    }

    ///Bounding coords are list(x0, y0, x1, y1) where x0 and y0 are top-left
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:70
    public virtual void generate_spawning_turfs(DmList bounding_coords = null, float spawning_behaviour = 0, object direction = null)
    {
        spawning_turfs_hit = Dm.list(); //turfs that will drift its contents to miss the shuttle
        spawning_turfs_miss = Dm.list(); //turfs that will drift its contents to hit the shuttle
        float x0 = Dm.num(bounding_coords[1]), y0 = Dm.num(bounding_coords[2]), x1 = Dm.num(bounding_coords[3]), y1 = Dm.num(bounding_coords[4]);
        // Bounding coords sticky to either the top right or bottom left corner of the template, depending on proximity to docking port
        // If we sticky to the bottom right corner, then [1] and [2] will be the bottom right corner, so we need to invert it
        if (x0 > x1)
            (x0, y0, x1, y1) = (x1, y1, x0, y0);

        float dir = Dm.num(direction);
        //step: once we get a corner (lets say top right), in which direction do we 'walk' to get the full side? corner: top left or bottom right. offset: bounding_coords is ONLY the shuttle, not the space around it
        (float stepX, float stepY, float cornerX, float cornerY, float offsetX, float offsetY) =
            dir == NORTH ? (1f, 0f, x0, y0, 0f, (float)SHUTTLE_TRANSIT_BORDER) //we're travelling north (so people get pushed south)
            : dir == SOUTH ? (-1f, 0f, x1, y1, 0f, (float)-SHUTTLE_TRANSIT_BORDER)
            : dir == EAST ? (0f, 1f, x1, y1, (float)SHUTTLE_TRANSIT_BORDER, 0f)
            : (0f, -1f, x0, y0, (float)-SHUTTLE_TRANSIT_BORDER, 0f);

        if (Dm.truthy(Dm.bitand(spawning_behaviour, SHUTTLE_EVENT_HIT_SHUTTLE)))
        {
            ///so we get either the horizontal width or vertical width, which would both equal the amount of spawn tiles
            float tile_amount = Math.Abs(x0 - x1); // tg's (direction == NORTH || SOUTH) is always true, so this is always the horizontal width
            for (float i = 0; i <= tile_amount; i++)
                Dm.append(ref spawning_turfs_hit, locate(cornerX + stepX * i + offsetX, cornerY + stepY * i + offsetY, port.z));
        }
        if (Dm.truthy(Dm.bitand(spawning_behaviour, SHUTTLE_EVENT_MISS_SHUTTLE)))
        {
            float deltaX = x1 - x0, deltaY = y0 - y1;
            for (float i = 1; i <= SHUTTLE_TRANSIT_BORDER; i++)
            {
                //Get the corner tile, and move away from the shuttle and towards the cordon
                Dm.append(ref spawning_turfs_miss, locate(cornerX - stepX * i + offsetX, cornerY - stepY * i + offsetY, port.z));
                //Get the corner tile, but jump over the shuttle and then continue unto the cordon
                Dm.append(ref spawning_turfs_miss, locate(cornerX + deltaX * stepX + stepX * i + offsetX, cornerY + deltaY * stepY + stepY * i + offsetY, port.z));
            }
        }
    }

    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:119
    public override object event_process()
    {
        object dot = base.event_process();

        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(LAZYLEN(spawning_list)))
        {
            if (Dm.truthy(self_destruct_when_empty))
                return SHUTTLE_EVENT_CLEAR;
            return dot;
        }

        if (prob(spawn_probability_per_process))
        {
            for (float i = 1; i <= spawns_per_spawn; i++)
                spawn_movable(get_type_to_spawn());
        }
        return dot;
    }

    ///Pick a random turf from the valid turfs we got. Overwrite if you need some custom picking
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:134
    public virtual turf get_spawn_turf() => (turf)pick(Dm.add(spawning_turfs_hit, spawning_turfs_miss));

    ///Spawn stuff! if you're not using this, don't use the simple_spawner subtype
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:139
    public virtual void spawn_movable(object spawn_type = null)
    {
        post_spawn((atom_movable)Dm.@new((Type)spawn_type, get_spawn_turf()));
    }

    ///Not technically a getter if remove_from_list_when_spawned=TRUE. Otherwise, this returns the type we're going to spawn and throw at the shuttle
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:143
    public virtual object get_type_to_spawn()
    {
        object dot = pick_weight(spawning_list);
        if (Dm.truthy(remove_from_list_when_spawned)) //if we have this enabled, we decrease the pickweight by 1 till it runs out
        {
            spawning_list[dot] = Dm.num(spawning_list[dot]) - 1;
            if (Dm.num(spawning_list[dot]) < 1)
                spawning_list.Remove(dot);
        }
        return dot;
    }

    ///Do any post-spawn edits you need to do
    // tg: code/modules/shuttle/shuttle_events/_shuttle_events.dm:151
    public virtual void post_spawn(atom_movable spawnee = null)
    {
        ADD_TRAIT(spawnee, TRAIT_FREE_HYPERSPACE_SOFTCORDON_MOVEMENT, INNATE_TRAIT); //Lets us spawn and move further away from the shuttle without being teleported into space
        ADD_TRAIT(spawnee, TRAIT_DEL_ON_SPACE_DUMP, INNATE_TRAIT); //if we hit the cordon, we get deleted. If the shuttle can make you, it can qdel you
    }
}

/// Repeat the "buckle in or fall over" event a couple times
// tg: code/modules/shuttle/shuttle_events/turbulence.dm:2
public partial class datum_shuttle_event_turbulence : datum_shuttle_event
{
    // tg: code/modules/shuttle/shuttle_events/turbulence.dm:16
    public override object activate()
    {
        object dot = base.activate();
        minor_announce("Please note, we are entering an area of subspace turbulence. For your own safety, please fasten your belts and remain seated until the vehicle comes to a complete stop.",
            title: "Emergency Shuttle", alert: TRUE);
        turbulence_cooldown = Dm.world.time + rand(5 * SECONDS, 20 * SECONDS); // Reduced interval after the announcement
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_events/turbulence.dm:22
    public override object event_process()
    {
        object dot = base.event_process();
        if (!Dm.truthy(dot))
            return dot;
        if (!(turbulence_cooldown <= Dm.world.time))
            return dot;
        turbulence_cooldown = Dm.world.time + rand(minimum_interval, maximum_interval);
        shake();
        _addtimer(Dm.@new<datum_callback>(this, nameof(knock_down)), warning_interval, TIMER_DELETE_ME);
        return dot;
    }

    /// Warn players to get buckled
    // tg: code/modules/shuttle/shuttle_events/turbulence.dm:33
    public virtual void shake()
    {
        foreach (mob_living mob in Dm.each<mob_living>(mobs_in_area_type(Dm.list(typeof(area_shuttle_escape)))))
        {
            float shake_intensity = Dm.truthy(mob.buckled) ? 0.25f : 1;
            if (mob.client != null)
                shake_camera(mob, 3 * SECONDS, shake_intensity);
        }
    }

    /// Knock them down
    // tg: code/modules/shuttle/shuttle_events/turbulence.dm:41
    public virtual void knock_down()
    {
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE))
            return; // They docked
        foreach (mob_living mob in Dm.each<mob_living>(mobs_in_area_type(Dm.list(typeof(area_shuttle_escape))))) // Not very efficient but check again in case someone was outdoors
        {
            if (Dm.truthy(mob.buckled))
                continue;
            Waiting.Detach(mob.Paralyze(3 * SECONDS, ignore_canstun: TRUE));
        }
    }
}

// tg: code/modules/shuttle/shuttle_events/misc.dm:30
public partial class datum_shuttle_event_simple_spawner_maintenance : datum_shuttle_event_simple_spawner
{
    // tg: code/modules/shuttle/shuttle_events/misc.dm:37
    public override object get_type_to_spawn()
    {
        object spawn_list = GLOB.maintenance_loot;
        while (spawn_list is DmList list)
            spawn_list = pick_weight(list);
        return spawn_list;
    }

    // tg's spawning_list is empty and event_process would stop at it: LAZYLEN(spawning_list) is false for a maintenance event in tg too, so it spawns nothing there either
}

// tg: code/modules/shuttle/shuttle_events/carp.dm:2
public partial class datum_shuttle_event_simple_spawner_carp : datum_shuttle_event_simple_spawner
{
    // tg: code/modules/shuttle/shuttle_events/carp.dm:14
    public override void post_spawn(atom_movable spawnee = null)
    {
        base.post_spawn(spawnee);
        //Give the carp the goal to migrate in a straight line so they dont just idle in hyperspace
        var carpee = (mob_living_basic_carp)spawnee;
        carpee.migrate_to(Dm.list(WEAKREF((datum)get_edge_target_turf(carpee.loc, angle2dir(Dm.num(dir2angle(port.preferred_direction)) - 180)))));
    }
}

// tg: code/modules/shuttle/shuttle_events/carp.dm:21
public partial class datum_shuttle_event_simple_spawner_carp_friendly : datum_shuttle_event_simple_spawner_carp
{
    // tg: code/modules/shuttle/shuttle_events/carp.dm:35
    public override turf get_spawn_turf() => (turf)(prob(hit_the_shuttle_chance) ? pick(spawning_turfs_hit) : pick(spawning_turfs_miss));
}
