// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file but the types the port does not have live (the mech bay's recharge port, the auxiliary base's
// console, the megafauna and the boss mobs, the navigation beacons: their stubs stay deferred; the ladders came with multi-z, D325) and the achievement of a crushed
// vehicle's rider (the awards are not ported). The [DmPath] of /obj/docking_port/stationary is here: this is the earliest .dme
// file that declares it (rules 2.3).

/*
All ShuttleMove procs go here
*/

/************************************Base procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:9
public partial class turf : atom
{
    // Called on every turf in the shuttle region, returns a bitflag for allowed movements of that turf
    // returns the new move_mode (based on the old)
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:9
    public virtual object fromShuttleMove(turf newT = null, object move_mode = null)
    {
        object dot = move_mode;
        if (Dm.truthy(Dm.bitand(move_mode, MOVE_AREA)) && Dm.truthy(isshuttleturf(this)))
            dot = Dm.bitor(dot, Dm.bitor(MOVE_TURF, MOVE_CONTENTS));
        if (Dm.truthy(_listen_lookup?[COMSIG_SHUTTLE_TURF_SHOULD_MOVE_SPECIAL]) && Dm.truthy(_SendSignal(COMSIG_SHUTTLE_TURF_SHOULD_MOVE_SPECIAL, Dm.list(this, newT, move_mode))))
            dot = Dm.bitor(dot, MOVE_SPECIAL);
        return dot;
    }

    // Called from the new turf before anything has been moved
    // Only gets called if fromShuttleMove returns true first
    // returns the new move_mode (based on the old)
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:19
    public virtual async DmTask<object> toShuttleMove(turf oldT = null, object move_mode = null, obj_docking_port_mobile shuttle = null)
    {
        object dot = move_mode;
        if (!Dm.truthy(Dm.bitand(dot, MOVE_TURF)))
            return dot;

        object shuttle_dir = shuttle.dir;
        foreach (atom_movable thing in Dm.each<atom_movable>(contents))
        {
            if (Dm.truthy(Dm.bitand(thing.resistance_flags, SHUTTLE_CRUSH_PROOF)))
                continue;
            if (isliving(thing))
            {
                var living_thing = (mob_living)thing;
                if (Dm.truthy(living_thing.incorporeal_move)) // Don't crush incorporeal things
                    continue;
                // tg: if (isvehicle(living_thing.buckled)) living_thing.client?.give_award(/datum/award/achievement/misc/no_parking, living_thing): the awards are not ported
                if (living_thing.buckled != null)
                    await living_thing.buckled.unbuckle_mob(living_thing, force: TRUE);
                if (living_thing.pulledby != null)
                    await living_thing.pulledby.stop_pulling();
                await living_thing.stop_pulling();
                living_thing.visible_message(span_warning(Dm.text("[] slams into []!", shuttle, living_thing)));
                SSblackbox.record_feedback("tally", "shuttle_gib", 1, living_thing.type);
                log_shuttle(Dm.text("[] was shuttle gibbed by [].", key_name(living_thing), shuttle));
                living_thing.investigate_log(Dm.text("has been gibbed by [].", shuttle), INVESTIGATE_DEATHS);
                await living_thing.gib(DROP_ALL_REMAINS);
            }
            else if (!ismob(thing)) //non-living mobs shouldn't be affected by shuttles, which is why this is an else
            {
                if (!Dm.truthy(thing.anchored))
                    await step(thing, Dm.num(shuttle_dir));
                else
                    qdel(thing);
            }
        }
        return dot;
    }

    // Called on the old turf to move the turf data
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:49
    public virtual async DmTask<object> onShuttleMove(turf new_turf = null, DmList movement_force = null, object move_dir = null, object ignore_area_change = null)
    {
        ignore_area_change ??= FALSE;
        if (new_turf == this) // In case of in place shuttle rotation shenanigans.
            return null;
        // Destination turf changes.
        // Baseturfs is definitely a list or this proc wouldnt be called.
        object shuttle_depth = depth_to_find_baseturf(typeof(turf_baseturf_skipover_shuttle));

        if (!Dm.truthy(shuttle_depth))
            throw Dm.CRASH(Dm.text("A turf queued to move via shuttle somehow had no skipover in baseturfs. []([]):[]", this, type, loc), "code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm", 57);
        new_turf = (turf)await new_turf.CopyOnTop(this, 1, Dm.num(shuttle_depth), TRUE, Dm.truthy(ignore_area_change) ? CHANGETURF_NO_AREA_CHANGE : NONE); // Don't automatically change space area to nearspace if we'll override it later
        // TURF-IDENTITY: tg's new_turf names its location, whose turf CopyOnTop just replaced
        new_turf.blocks_air = TRUE;
        new_turf.air_update_turf(TRUE, FALSE);
        blocks_air = TRUE;
        air_update_turf(TRUE, TRUE);
        if (isopenturf(new_turf))
        {
            var new_open = (turf_open)new_turf;
            new_open.copy_air_with_tile(this as turf_open); // tg passes the old turf, closed or open; the proc reads an open one's air
        }
        CeilingSystem.Of(World.Current).ShuttleArrived(this, new_turf); // DEVIATION(DV183): the roof moves with the turf
        // SEND_SIGNAL(src, COMSIG_TURF_ON_SHUTTLE_MOVE, new_turf) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_ON_SHUTTLE_MOVE]))
            _SendSignal(COMSIG_TURF_ON_SHUTTLE_MOVE, Dm.list(this, new_turf));

        return TRUE;
    }

    // Called on the new turf after everything has been moved
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:71
    public virtual async DmTask<object> afterShuttleMove(turf oldT = null, object rotation = null)
    {
        //Dealing with the turf we left behind
        await oldT.TransferComponents(this);

        SSexplosions.wipe_turf(this);
        object shuttle_depth = depth_to_find_baseturf(typeof(turf_baseturf_skipover_shuttle));

        (int oldX, int oldY, int oldZ) = ((int)oldT.x, (int)oldT.y, (int)oldT.z); // TURF-IDENTITY: the scrape replaces oldT
        if (Dm.truthy(shuttle_depth))
            await oldT.ScrapeAway(Dm.num(shuttle_depth));
        CeilingSystem.Of(World.Current).ShuttleLeft(oldX, oldY, oldZ); // DEVIATION(DV183)

        if (Dm.truthy(rotation))
            await shuttleRotate(rotation, @params: ALL); //see shuttle_rotate.dm

        // if we have a lighting object that needs to be updated
        // tg: lighting_object?.needs_update ... lighting_object.update(): the lighting object is the turf's mark (DV85), nothing to redraw

        // SEND_SIGNAL(src, COMSIG_TURF_AFTER_SHUTTLE_MOVE, oldT) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_AFTER_SHUTTLE_MOVE]))
            _SendSignal(COMSIG_TURF_AFTER_SHUTTLE_MOVE, Dm.list(this, oldT));

        return TRUE;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:94
    public virtual void lateShuttleMove(turf oldT = null)
    {
        blocks_air = Dm.initial(this, (turf p) => p.blocks_air);
        air_update_turf(TRUE, blocks_air);
        oldT.blocks_air = Dm.initial(oldT, (turf p) => p.blocks_air);
        oldT.air_update_turf(TRUE, oldT.blocks_air);
    }
}

/////////////////////////////////////////////////////////////////////////////////////

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:105
public partial class atom_movable : atom
{
    // Return the move_move (based on the old), without any side effects.
    // This is for checking what would be moved if src is on a shuttle being moved.
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:105
    public virtual object hypotheticalShuttleMove(object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        return move_mode;
    }

    // Called on every atom in shuttle turf contents before anything has been moved
    // returns the new move_mode (based on the old)
    // WARNING: Do not leave turf contents in beforeShuttleMove or dock() will runtime
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:111
    public virtual async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        await DmTask.CompletedTask;
        // SEND_SIGNAL(src, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, newT, rotation, move_mode, moving_dock) || move_mode
        object sent = Dm.truthy(_listen_lookup?[COMSIG_ATOM_BEFORE_SHUTTLE_MOVE]) ? _SendSignal(COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, Dm.list(this, newT, rotation, move_mode, moving_dock)) : null;
        return Dm.truthy(sent) ? sent : move_mode;
    }

    /// Called on atoms to move the atom to the new location
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:116
    public virtual async DmTask<object> onShuttleMove(turf newT = null, turf oldT = null, DmList movement_force = null, object move_dir = null, obj_docking_port_stationary old_dock = null, obj_docking_port_mobile moving_dock = null)
    {
        if (newT == oldT) // In case of in place shuttle rotation shenanigans.
            return null;

        if (loc != oldT) // This is for multi tile objects
            return null;

        await abstract_move(newT);

        return TRUE;
    }

    // Called on atoms after everything has been moved
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:128
    public virtual async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        if (light != null)
            update_light();
        if (Dm.truthy(rotation))
            await shuttleRotate(rotation, @params: ALL);

        // SEND_SIGNAL(src, COMSIG_ATOM_AFTER_SHUTTLE_MOVE, oldT) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_AFTER_SHUTTLE_MOVE]))
            _SendSignal(COMSIG_ATOM_AFTER_SHUTTLE_MOVE, Dm.list(this, oldT));

        return TRUE;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:140
    public virtual async DmTask lateShuttleMove(turf oldT = null, DmList movement_force = null, object move_dir = null)
    {
        if (movement_force == null || Dm.truthy(anchored))
            return;
        float throw_force = Dm.num(movement_force["THROW"]);
        if (!Dm.truthy(throw_force))
            return;
        var target = get_edge_target_turf(this, move_dir) as turf;
        float range = throw_force * 10;
        range = CEILING(rand(range - (range * 0.1f), range + (range * 0.1f)), 10) / 10;
        float speed = range / 5;
        await safe_throw_at(target, range, speed, force: MOVE_FORCE_EXTREMELY_STRONG);
    }
}

/////////////////////////////////////////////////////////////////////////////////////

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:156
public partial class area : atom
{
    // Called on areas before anything has been moved
    // returns the new move_mode (based on the old)
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:156
    public virtual object beforeShuttleMove(DmList shuttle_areas = null)
    {
        if (!Dm.truthy(shuttle_areas[this]))
            return NONE;
        return MOVE_AREA;
    }

    // Called on areas to move their turf between areas
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:162
    public virtual object onShuttleMove(turf oldT = null, turf newT = null, obj_docking_port_mobile shuttle = null, area fallback_area = null)
    {
        if (newT == oldT) // In case of in place shuttle rotation shenanigans.
            return TRUE;

        var underlying_area = shuttle.underlying_areas_by_turf[oldT] as area;
        oldT.change_area(this, underlying_area ?? fallback_area);
        Dm.remove(shuttle.underlying_areas_by_turf, oldT);
        //The old turf has now been given back to the area that turf originaly belonged to

        var old_dest_area = (area)newT.loc;
        set_parallax_movedir(old_dest_area.parallax_movedir);
        newT.change_area(old_dest_area, this);
        shuttle.underlying_areas_by_turf[newT] = old_dest_area;
        return TRUE;
    }

    // Called on areas after everything has been moved
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:178
    public virtual object afterShuttleMove(object new_parallax_dir = null)
    {
        set_parallax_movedir(new_parallax_dir);
        return TRUE;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:182
    public virtual void lateShuttleMove()
    {
    }
}

/************************************Machinery move procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:298
public partial class obj_machinery_navbeacon : obj_machinery
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:298
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        Dm.remove_at(GLOB.navbeacons, Dm.text("[]", z), this);
        Dm.remove(ref GLOB.deliverybeacons, this);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:303
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);

        if (Dm.truthy(codes[NAVBEACON_PATROL_MODE]))
        {
            if (!Dm.truthy(GLOB.navbeacons[Dm.text("[]", z)]))
                GLOB.navbeacons[Dm.text("[]", z)] = Dm.list();
            Dm.append_at(GLOB.navbeacons, Dm.text("[]", z), this); //Register with the patrol list!
        }
        if (Dm.truthy(codes[NAVBEACON_DELIVERY_MODE]))
        {
            Dm.append(ref GLOB.deliverybeacons, this);
            Dm.append(ref GLOB.deliverybeacontags, location);
        }
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:191
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:191
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);

        if (cycle_pump != null)
            Waiting.Detach(cycle_pump.undock()); // INVOKE_ASYNC

        foreach (obj_machinery_door_airlock other_airlock in Dm.each<obj_machinery_door_airlock>(range(2, this))) // includes src, extended because some escape pods have 1 plating turf exposed to space
        {
            other_airlock.shuttledocked = FALSE;
            other_airlock.air_tight = TRUE;
            if (other_airlock.cycle_pump != null)
            {
                Waiting.Detach(other_airlock.cycle_pump.undock()); // INVOKE_ASYNC
                continue;
            }
            Waiting.Detach(other_airlock.close(FALSE, TRUE)); // INVOKE_ASYNC: force crush
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:205
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        object current_area = get_area(this);
        turf local_turf = null;
        float tile_air_pressure;
        foreach (obj_machinery_door_airlock other_airlock in Dm.each<obj_machinery_door_airlock>(orange(2, this))) // does not include src, extended because some escape pods have 1 plating turf exposed to space
        {
            if (get_area(other_airlock) != current_area) // does not include double-wide airlocks unless actually docked
            {
                // Cycle linking is only disabled if we are actually adjacent to another airlock
                shuttledocked = TRUE;
                other_airlock.shuttledocked = TRUE;
                if (other_airlock.cycle_pump != null)
                {
                    local_turf = get_step(this, REVERSE_DIR(Dm.num(other_airlock.cycle_pump.dir)));
                    tile_air_pressure = 0;
                    if (local_turf != null)
                        tile_air_pressure = Math.Max(0, Dm.num(((datum_gas_mixture)await local_turf.return_air()).return_pressure()));
                    Waiting.Detach(other_airlock.cycle_pump.on_dock_request(tile_air_pressure)); // INVOKE_ASYNC
                }
                // Save external airlocks turf in case our own docking purpouses
                local_turf = get_turf(other_airlock);
            }
        }

        if (cycle_pump != null)
        {
            tile_air_pressure = 0;
            if (local_turf != null)
            {
                local_turf = get_step(local_turf, REVERSE_DIR(Dm.num(cycle_pump.dir)));
                if (local_turf != null)
                    tile_air_pressure = Math.Max(0, Dm.num(((datum_gas_mixture)await local_turf.return_air()).return_pressure()));
                Waiting.Detach(cycle_pump.on_dock_request(tile_air_pressure)); // INVOKE_ASYNC
            }
            else
            {
                // In case, somebody decides to build an airlock on evac shuttle, we count CentComs blastdoors as valid docking airlock
                local_turf = get_step(this, REVERSE_DIR(Dm.num(cycle_pump.dir)));
                if (local_turf != null)
                {
                    foreach (obj_machinery_door_poddoor_shuttledock centcom_airlock in Dm.each<obj_machinery_door_poddoor_shuttledock>(local_turf))
                    {
                        // For some reason on docking moment those tiles are vacuum, and pump denies safe_dock attempt
                        // To fix this we're lying, that external pressure is nominal
                        Waiting.Detach(cycle_pump.on_dock_request(ONE_ATMOSPHERE)); // INVOKE_ASYNC
                        break;
                    }
                }
            }
        }
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:242
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:242
    public override object hypotheticalShuttleMove(object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = base.hypotheticalShuttleMove(rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:247
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
        {
            dot = Dm.bitor(dot, MOVE_CONTENTS);
            SScameras.remove_camera_from_chunk(this);
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:253
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        SScameras.add_camera_to_chunk(this);
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:261
public partial class obj_machinery_computer_auxiliary_base : obj_machinery_computer
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:261
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        if (Dm.truthy(is_mining_level(z))) //Avoids double logging and landing on other Z-levels due to badminnery
            SSblackbox.record_feedback("associative", "colonies_dropped", 1, Dm.list(Dm.kv("x", x), Dm.kv("y", y), Dm.kv("z", z)));
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:266
public partial class obj_machinery_atmospherics : obj_machinery
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:266
    public override async DmTask lateShuttleMove(turf oldT = null, DmList movement_force = null, object move_dir = null)
    {
        await base.lateShuttleMove(oldT, movement_force, move_dir);
        if (pipe_vision_img != null)
            pipe_vision_img.loc = loc;

        bool missing_nodes = false;
        for (float i = 1; i <= device_type; i++)
        {
            if (nodes[i] != null)
            {
                var node = (obj_machinery_atmospherics)nodes[i];
                bool connected = false;
                foreach (object D in Dm.each(GLOB.cardinals))
                {
                    if (Dm.@in(node, get_step(this, Dm.num(D))))
                    {
                        connected = true;
                        break;
                    }
                }

                if (!connected)
                    nullify_node(i);
            }

            if (nodes[i] == null)
                missing_nodes = true;
        }

        if (missing_nodes)
        {
            atmos_init();
            foreach (obj_machinery_atmospherics A in Dm.each<obj_machinery_atmospherics>(pipeline_expansion()))
            {
                A.atmos_init();
                if (Dm.truthy(A.return_pipenet()))
                    A.add_member(this);
            }
            SSair.add_to_rebuild_queue(this);
        }
        else
        {
            // atmos_init() calls update_appearance(), so we don't need to call it
            update_appearance();
        }
    }
}

/************************************Mob move procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:316
public partial class mob : atom_movable
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:316
    public override async DmTask<object> onShuttleMove(turf newT = null, turf oldT = null, DmList movement_force = null, object move_dir = null, obj_docking_port_stationary old_dock = null, obj_docking_port_mobile moving_dock = null)
    {
        if (HAS_TRAIT(this, TRAIT_BLOCK_SHUTTLE_MOVEMENT))
            return null;
        return await base.onShuttleMove(newT, oldT, movement_force, move_dir, old_dock, moving_dock);
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:321
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        if (HAS_TRAIT(this, TRAIT_BLOCK_SHUTTLE_MOVEMENT))
            return null;
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        if (client != null && movement_force != null)
        {
            float shake_force = Math.Max(Dm.num(movement_force["THROW"]), Dm.num(movement_force["KNOCKDOWN"]));
            if (buckled != null)
                shake_force *= 0.25f;
            shake_camera(this, shake_force, 1);
        }
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:331
public partial class mob_living : mob
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:331
    public override async DmTask lateShuttleMove(turf oldT = null, DmList movement_force = null, object move_dir = null)
    {
        float knockdown = Dm.num(movement_force?["KNOCKDOWN"]);
        if (buckled != null && get_area(this) is area_shuttle_arrival)
        {
            //if we're on the arrival shuttle, unbuckle so that new player's don't get stuck in there
            await buckled.user_unbuckle_mob(this, this);
            return;
        }
        if (knockdown > 0)
        {
            if (buckled != null || HAS_TRAIT(this, TRAIT_NEGATES_GRAVITY))
            {
                await Immobilize(knockdown * 0.5f);
                return;
            }
            await Paralyze(knockdown);
        }
        await base.lateShuttleMove(oldT, movement_force, move_dir);
    }
}

/************************************Structure move procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:355
public partial class obj_structure_grille : obj_structure
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:355
    public override object hypotheticalShuttleMove(object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = base.hypotheticalShuttleMove(rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:360
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:365
public partial class obj_structure_lattice : obj_structure
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:365
    public override object hypotheticalShuttleMove(object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = base.hypotheticalShuttleMove(rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:370
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        if (Dm.truthy(Dm.bitand(dot, MOVE_AREA)))
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:375
public partial class obj_structure_cable : obj_structure
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:375
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        await cut_cable_from_powernet(FALSE);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:379
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        connect_cable(TRUE);
        propagate_if_no_network();
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:394 (D325: the ladders are live)
public partial class obj_structure_ladder : obj_structure
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:394
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        if (!Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            disconnect();
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:399
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        if (!Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            LateInitialize();
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:404
    public override async DmTask<object> onShuttleMove(turf newT = null, turf oldT = null, DmList movement_force = null, object move_dir = null, obj_docking_port_stationary old_dock = null, obj_docking_port_mobile moving_dock = null)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            // simply don't be moved
            return FALSE;
        return await base.onShuttleMove(newT, oldT, movement_force, move_dir, old_dock, moving_dock);
    }
}

/************************************Misc move procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:415
public partial class obj_docking_port_mobile : obj_docking_port
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:415
    public override object hypotheticalShuttleMove(object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = base.hypotheticalShuttleMove(rotation, move_mode, moving_dock);
        if (moving_dock == this)
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:420
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        if (moving_dock == this)
            dot = Dm.bitor(dot, MOVE_CONTENTS);
        return dot;
    }
}

// Never move the stationary docking port, otherwise things get WEIRD
// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:426
[DmPath("/obj/docking_port/stationary")]
public partial class obj_docking_port_stationary : obj_docking_port
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:426
    public override async DmTask<object> onShuttleMove(turf newT = null, turf oldT = null, DmList movement_force = null, object move_dir = null, obj_docking_port_stationary old_dock = null, obj_docking_port_mobile moving_dock = null)
    {
        return FALSE;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:448
public partial class obj_docking_port_stationary_public_mining_dock : obj_docking_port_stationary
{
    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:448
    public override async DmTask<object> onShuttleMove(turf newT = null, turf oldT = null, DmList movement_force = null, object move_dir = null, obj_docking_port_stationary old_dock = null, obj_docking_port_mobile moving_dock = null)
    {
        shuttle_id = "mining_public"; //It will not move with the base, but will become enabled as a docking point.
        return null;
    }
}
