// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/shuttle_move.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file, the move of a shuttle's turfs, areas and contents to a dock (rotated to the dock's direction).
// The navigation computers' hidden turfs (update_hidden_docking_ports) since syndicate N1c (D316: the infiltrator is hidden).
// TURF-IDENTITY (object-model.md 7.1): tg's turf lists name locations, so a turf a stage replaced (CopyOnTop, ScrapeAway) is the
// same list entry for the next stage; the port's lists hold the replaced objects, so each stage reads its turfs through
// turf.dm_identity.

// tg: code/modules/shuttle/mobile_port/shuttle_move.dm:3
public partial class obj_docking_port_mobile : obj_docking_port
{
    /// This is the main proc. Despite what the name suggests,
    /// it instantly moves our mobile port to stationary port `new_dock`.
    // tg: code/modules/shuttle/mobile_port/shuttle_move.dm:3
    public virtual async DmTask<object> initiate_docking(obj_docking_port_stationary new_dock = null, object movement_direction = null, object force = null)
    {
        force ??= FALSE;
        // Crashing this ship with NO SURVIVORS

        // A dock the map lacks (multiz_debug's emergency_home, which the emergency shuttle's arrival asks for by id) is tg's
        // DOCKING_NULL_DESTINATION, where tg's new_dock.get_docked() would runtime; the caller waits and tries again (setTimer(20))
        if (new_dock == null)
        {
            remove_ripples();
            return DOCKING_NULL_DESTINATION;
        }

        if (new_dock.get_docked() == this)
        {
            remove_ripples();
            return DOCKING_SUCCESS;
        }

        if (!Dm.truthy(force))
        {
            if (!Dm.truthy(check_dock(new_dock)))
            {
                remove_ripples();
                return DOCKING_BLOCKED;
            }
            if (!Dm.truthy(canMove()))
            {
                remove_ripples();
                return DOCKING_IMMOBILIZED;
            }
        }

        var old_dock = get_docked() as obj_docking_port_stationary;

        // The area that gets placed under shuttle turfs that do not have their own area to place down
        Type fallback_area_type = typeof(area_space); // SHUTTLE_DEFAULT_UNDERLYING_AREA (__DEFINES/shuttles.dm)

        if (old_dock != null) //Dock overwrites
            fallback_area_type = old_dock.area_type;

        /**************************************************************************************************************
            Both lists are associative with a turf:bitflag structure. (new_turfs bitflag space unused currently)
            The bitflag contains the data for what inhabitants of that coordinate should be moved to the new location
            The bitflags can be found in __DEFINES/shuttles.dm
        */
        DmList old_turfs = return_ordered_turfs(x, y, z, dir);
        DmList new_turfs = return_ordered_turfs(new_dock.x, new_dock.y, new_dock.z, new_dock.dir);
        await CHECK_TICK_ASYNC();
        /**************************************************************************************************************/

        // The fallback area is the area for shuttle turfs that have no area underneath them
        // If it no longer/has never existed it will be created
        var fallback_area = GLOB.areas_by_type[fallback_area_type] as area;
        if (fallback_area == null)
            fallback_area = (area)Dm.@new(fallback_area_type, (object)null);

        float rotation = 0;
        if (!Dm.eq(new_dock.dir, dir)) //Even when the dirs are the same rotation is coming out as not 0 for some reason
        {
            rotation = Dm.num(dir2angle(new_dock.dir)) - Dm.num(dir2angle(dir));
            if ((rotation % 90) != 0)
                rotation += (rotation % 90); //diagonal rotations not allowed, round up
            rotation = SIMPLIFY_DEGREES(rotation);
        }

        movement_direction ??= REVERSE_DIR(preferred_direction);
        if (!Dm.truthy(movement_direction))
            movement_direction = REVERSE_DIR(preferred_direction);

        DmList moved_atoms = Dm.list(); //Everything not a turf that gets moved in the shuttle
        DmList areas_to_move = Dm.list(); //unique assoc list of areas on turfs being moved
        DmList underlying_areas = Dm.list(); //unique assoc list of areas beneath turfs being moved

        object dot = await preflight_check(old_turfs, new_turfs, areas_to_move, underlying_areas, rotation);
        if (Dm.truthy(dot))
        {
            remove_ripples();
            return dot;
        }

        /*******************************************Hiding turfs if necessary*******************************************/
        // TODO: Move this somewhere sane
        DmList new_hidden_turfs = null;
        if (Dm.truthy(hidden))
        {
            new_hidden_turfs = Dm.list();
            for (int i = 1; i <= old_turfs.len; i++)
            {
                await CHECK_TICK_ASYNC();
                var oldT = old_turfs[i] as turf;
                if (Dm.truthy(Dm.bitand(old_turfs[oldT], MOVE_TURF)))
                    Dm.append(ref new_hidden_turfs, new_turfs[i]);
            }
            SSshuttle.update_hidden_docking_ports(null, new_hidden_turfs);
        }
        /***************************************************************************************************************/

        if (!Dm.truthy(force))
        {
            if (!Dm.truthy(check_dock(new_dock)))
            {
                remove_ripples();
                return DOCKING_BLOCKED;
            }
            if (!Dm.truthy(canMove()))
            {
                remove_ripples();
                return DOCKING_IMMOBILIZED;
            }
        }

        // Moving to the new location will trample the ripples there at the exact
        // same time any mobs there are trampled, to avoid any discrepancy where
        // the ripples go away before it is safe.
        // D301: the places, as the move replaces the hull's turf objects
        List<(float x, float y, float z)> left_tiles = streak_tiles(old_turfs), reached_tiles = streak_tiles(new_turfs);

        await takeoff(old_turfs, new_turfs, moved_atoms, rotation, movement_direction, old_dock, fallback_area);

        await CHECK_TICK_ASYNC();

        await cleanup_runway(new_dock, old_turfs, new_turfs, areas_to_move, underlying_areas, moved_atoms, rotation, movement_direction, fallback_area);

        await CHECK_TICK_ASYNC();

        /*******************************************Unhiding turfs if necessary******************************************/
        if (new_hidden_turfs != null)
        {
            // TURF-IDENTITY: the move replaced the hidden turfs (CopyOnTop): their entries and images follow their places, as tg's turf
            // references name locations
            for (int i = 1; i <= new_hidden_turfs.len; i++)
            {
                var was = new_hidden_turfs[i] as turf;
                var now = (turf)turf.dm_identity(was);
                if (now == was)
                    continue;
                var info = SSshuttle.hidden_shuttle_turfs[was] as DmList;
                SSshuttle.hidden_shuttle_turfs.Remove(was);
                if (info != null)
                {
                    SSshuttle.hidden_shuttle_turfs[now] = info;
                    (info[1] as image).loc = now;
                }
                new_hidden_turfs[i] = now;
            }
            SSshuttle.update_hidden_docking_ports(hidden_turfs, null);
            hidden_turfs = new_hidden_turfs;
        }
        /****************************************************************************************************************/

        await check_poddoors();
        new_dock.last_dock_time = Dm.world.time;
        await setDir(new_dock.dir);

        // remove any stragglers just in case, and clear the list
        remove_ripples();
        if (old_dock != null && old_dock is not obj_docking_port_stationary_transit)
            place_streaks<obj_effect_temp_visual_circle_wave_orange>(left_tiles);
        if (new_dock is not obj_docking_port_stationary_transit)
            place_streaks<obj_effect_temp_visual_circle_wave_star_blast>(reached_tiles);
        return DOCKING_SUCCESS;
    }

    // D301, not tg's: every fourth tile of a hull's footprint, where the client draws a shuttle's streak
    private static List<(float x, float y, float z)> streak_tiles(DmList turfs)
    {
        var tiles = new List<(float x, float y, float z)>();
        foreach (turf t in Dm.each<turf>(turfs))
        {
            if (t.x % 4 == 0 && t.y % 4 == 0)
                tiles.Add((t.x, t.y, t.z));
        }
        return tiles;
    }

    // D301: tg types nothing else spawns, without their picture: orange marks a departure, star_blast an arrival; the client tells them apart (ShuttleFx)
    private void place_streaks<T>(List<(float x, float y, float z)> tiles) where T : obj_effect_temp_visual
    {
        foreach ((float tile_x, float tile_y, float tile_z) in tiles)
        {
            T mark = Dm.@new<T>(locate(tile_x, tile_y, tile_z));
            mark.icon = null;
            mark.icon_state = null;
            mark.dir = preferred_direction;
        }
    }

    // CHECK_TICK (__DEFINES/_tick.dm:21), awaited in a suspending proc
    private static async DmTask CHECK_TICK_ASYNC()
    {
        if (TICK_CHECK())
            await stoplag();
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move.dm:107
    public virtual async DmTask<object> preflight_check(DmList old_turfs = null, DmList new_turfs = null, DmList areas_to_move = null, DmList underlying_areas = null, object rotation = null)
    {
        for (float i = 1; i <= length(old_turfs); i++)
        {
            await CHECK_TICK_ASYNC();
            var oldT = old_turfs[i] as turf;
            var newT = new_turfs[i] as turf;
            if (newT == null)
                return DOCKING_NULL_DESTINATION;
            if (oldT == null)
                return DOCKING_NULL_SOURCE;

            var old_area = (area)oldT.loc;
            object move_mode = old_area.beforeShuttleMove(shuttle_areas); //areas

            foreach (atom_movable moving_atom in Dm.each<atom_movable>(oldT.contents))
            {
                await CHECK_TICK_ASYNC();
                if (moving_atom.loc != oldT) //fix for multi-tile objects
                    continue;
                move_mode = await moving_atom.beforeShuttleMove(newT, rotation, move_mode, this); //atoms
            }

            move_mode = oldT.fromShuttleMove(newT, move_mode); //turfs
            move_mode = await newT.toShuttleMove(oldT, move_mode, this); //turfs

            if (Dm.truthy(Dm.bitand(move_mode, MOVE_AREA)))
            {
                var underlying_area = underlying_areas_by_turf[oldT] as area;
                if (underlying_area != null)
                    underlying_areas[underlying_area] = TRUE;
                areas_to_move[old_area] = TRUE;
            }

            old_turfs[oldT] = move_mode;
        }
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move.dm:137
    public virtual async DmTask takeoff(DmList old_turfs = null, DmList new_turfs = null, DmList moved_atoms = null, object rotation = null, object movement_direction = null, obj_docking_port_stationary old_dock = null, area fallback_area = null)
    {
        for (float i = 1; i <= old_turfs.len; i++)
        {
            var oldT = (turf)old_turfs[i];
            var newT = (turf)turf.dm_identity((turf)new_turfs[i]);
            object move_mode = old_turfs[oldT];

            if (Dm.truthy(Dm.bitand(move_mode, MOVE_TURF)))
            {
                await oldT.onShuttleMove(newT, movement_force, movement_direction, Dm.bitand(move_mode, MOVE_AREA)); //turfs
                newT = (turf)turf.dm_identity(newT); // TURF-IDENTITY: CopyOnTop replaced the destination's turf
                new_turfs[i] = newT;
            }

            if (Dm.truthy(Dm.bitand(move_mode, MOVE_AREA)))
            {
                var shuttle_area = (area)oldT.loc;
                shuttle_area.onShuttleMove(oldT, newT, this, fallback_area); //areas
            }

            if (Dm.truthy(Dm.bitand(move_mode, MOVE_CONTENTS)))
            {
                foreach (atom_movable moving_atom in Dm.each<atom_movable>(oldT.contents.Copy()))
                {
                    if (moving_atom.loc != oldT) //fix for multi-tile objects
                        continue;
                    await moving_atom.onShuttleMove(newT, oldT, movement_force, movement_direction, old_dock, this); //atoms
                    moved_atoms[moving_atom] = oldT;
                }
            }

            if (Dm.truthy(Dm.bitand(move_mode, MOVE_SPECIAL)))
            {
                // SEND_SIGNAL(oldT, COMSIG_SHUTTLE_TURF_ON_MOVE_SPECIAL, newT, movement_force, movement_direction, old_dock, src) (rules 11.1 (e))
                if (Dm.truthy(oldT._listen_lookup?[COMSIG_SHUTTLE_TURF_ON_MOVE_SPECIAL]))
                    oldT._SendSignal(COMSIG_SHUTTLE_TURF_ON_MOVE_SPECIAL, Dm.list(oldT, newT, movement_force, movement_direction, old_dock, this));
            }
        }
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move.dm:162
    public virtual async DmTask cleanup_runway(obj_docking_port_stationary new_dock = null, DmList old_turfs = null, DmList new_turfs = null, DmList areas_to_move = null, DmList underlying_areas = null, DmList moved_atoms = null, object rotation = null, object movement_direction = null, area fallback_area = null)
    {
        fallback_area.afterShuttleMove(0);
        for (float i = 1; i <= underlying_areas.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var underlying_area = (area)underlying_areas[i];
            underlying_area.afterShuttleMove(0);
        }

        // Parallax handling
        // This needs to be done before the atom after move
        object new_parallax_dir = NONE;
        if (new_dock is obj_docking_port_stationary_transit)
            new_parallax_dir = preferred_direction;
        for (float i = 1; i <= areas_to_move.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var internal_area = (area)areas_to_move[i];
            internal_area.afterShuttleMove(new_parallax_dir); //areas
        }

        for (float i = 1; i <= old_turfs.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var old_turf = (turf)old_turfs[i];
            object old_move_mode = old_turfs[old_turf];
            var old_ceiling = get_step_multiz(old_turf, UP) as turf;
            if (old_ceiling != null) // check if a ceiling was generated previously
            {
                // remove old ceiling
                if (old_ceiling is turf_open_floor_engine_hull_ceiling)
                    await old_ceiling.ScrapeAway();
                else
                    await old_ceiling.remove_baseturfs_from_typecache(Dm.list(Dm.kv(typeof(turf_open_floor_engine_hull_ceiling), TRUE)));
            }
            if (!Dm.truthy(Dm.bitand(old_move_mode, MOVE_TURF)))
                continue;
            var new_turf = (turf)turf.dm_identity((turf)new_turfs[i]);
            await new_turf.afterShuttleMove((turf)turf.dm_identity(old_turf), rotation); //turfs
            new_turf = (turf)turf.dm_identity(new_turf);
            var new_ceiling = get_step_multiz(new_turf, UP) as turf; // check if a ceiling is needed
            if (new_ceiling != null)
            {
                // generate ceiling
                if (!(new_ceiling is turf_open_floor_engine_hull_ceiling || Dm.truthy(new_ceiling.depth_to_find_baseturf(typeof(turf_open_floor_engine_hull_ceiling)))))
                {
                    if (new_ceiling is turf_open_openspace || new_ceiling is turf_open_space_openspace)
                    {
                        await new_ceiling.place_on_top(typeof(turf_open_floor_engine_hull_ceiling));
                    }
                    else
                    {
                        await new_ceiling.stack_ontop_of_baseturf(typeof(turf_open_openspace), typeof(turf_open_floor_engine_hull_ceiling));
                        await new_ceiling.stack_ontop_of_baseturf(typeof(turf_open_space_openspace), typeof(turf_open_floor_engine_hull_ceiling));
                    }
                }
            }
        }
        for (float i = 1; i <= moved_atoms.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var moved_object = (atom_movable)moved_atoms[i];
            if (QDELETED(moved_object))
                continue;
            var oldT = (turf)turf.dm_identity((turf)moved_atoms[moved_object]);
            await moved_object.afterShuttleMove(oldT, movement_force, dir, preferred_direction, movement_direction, rotation); //atoms
        }

        // lateShuttleMove (There had better be a really good reason for additional stages beyond this)

        fallback_area.lateShuttleMove();

        for (float i = 1; i <= areas_to_move.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var internal_area = (area)areas_to_move[i];
            internal_area.lateShuttleMove();
        }

        for (float i = 1; i <= underlying_areas.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var underlying_area = (area)underlying_areas[i];
            underlying_area.lateShuttleMove();
        }

        for (float i = 1; i <= old_turfs.len; i++)
        {
            await CHECK_TICK_ASYNC();
            if (!Dm.truthy(Dm.bitand(old_turfs[old_turfs[i]], Dm.bitor(MOVE_CONTENTS, MOVE_TURF))))
                continue;
            var oldT = (turf)turf.dm_identity((turf)old_turfs[i]);
            var newT = (turf)turf.dm_identity((turf)new_turfs[i]);
            newT.lateShuttleMove(oldT);
        }

        for (float i = 1; i <= moved_atoms.len; i++)
        {
            await CHECK_TICK_ASYNC();
            var moved_object = (atom_movable)moved_atoms[i];
            if (QDELETED(moved_object))
                continue;
            var oldT = (turf)turf.dm_identity((turf)moved_atoms[moved_object]);
            await moved_object.lateShuttleMove(oldT, movement_force, movement_direction);
        }
    }
}
