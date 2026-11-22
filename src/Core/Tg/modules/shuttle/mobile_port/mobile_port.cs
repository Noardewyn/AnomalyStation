// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/mobile_port.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file but highlight (#ifdef TESTING).

// tg: code/modules/shuttle/mobile_port/mobile_port.dm:1
public partial class obj_docking_port_mobile : obj_docking_port
{
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:64
    [DmParams("mapload", "areas")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList areas = Dm.arg<DmList>(__extra, 0);
        object dot = base.Initialize(mapload);

        if (!Dm.truthy(shuttle_id))
            shuttle_id = "shuttle";
        if (!Dm.truthy(name))
            name = "shuttle";
        float counter = 1;
        object tmp_id = shuttle_id;
        object tmp_name = name;
        while (Dm.truthy(Check_id(shuttle_id)))
        {
            counter++;
            shuttle_id = Dm.text("[]_[]", tmp_id, counter);
            name = Dm.text("[] []", tmp_name, counter);
        }

        if (areas != null)
        {
            foreach (area area in Dm.each<area>(areas))
                shuttle_areas[area] = TRUE;
        }
        else
        {
            DmList all_turfs = return_ordered_turfs(x, y, z, dir);
            for (float i = 1; i <= all_turfs.len; i++)
            {
                var curT = (turf)all_turfs[i];
                var cur_area = curT.loc as area;
                if (Dm.istype(cur_area, area_type))
                {
                    turf_count++;
                    shuttle_areas[cur_area] = TRUE;
                }
            }
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:95
    public override object Destroy(float? force = null)
    {
        unregister();
        destination = null;
        previous = null;
        foreach (obj_machinery_power_shuttle_engine engine in Dm.each<obj_machinery_power_shuttle_engine>(engine_list.Copy()))
            engine.unsync_ship();
        if (!QDELETED(assigned_transit))
        {
            qdel(assigned_transit, force: TRUE);
            assigned_transit = null;
        }
        shuttle_areas = null;
        remove_ripples();
        return base.Destroy(force);
    }

    /**
     * Calculated and populates the information used for docking and some internal vars.
     * This can also be used to calculate from shuttle_areas so that you can expand/shrink shuttles!
     *
     * Arguments:
     * * loading_from - The template that the shuttle was loaded from, if not given we iterate shuttle_areas to calculate information instead
     */
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:117
    public virtual void calculate_docking_port_information(datum_map_template_shuttle loading_from = null)
    {
        float port_x_offset = Dm.num(loading_from?.port_x_offset);
        float port_y_offset = Dm.num(loading_from?.port_y_offset);
        float width = loading_from?.width ?? 0;
        float height = loading_from?.height ?? 0;
        if (loading_from == null)
        {
            if (!Dm.truthy(length(shuttle_areas)))
                throw Dm.CRASH("Attempted to calculate a docking port's information without a template before it was assigned any areas!", "code/modules/shuttle/mobile_port/mobile_port.dm", 124);
            // no template given, use shuttle_areas to calculate width and height
            float WORLDMAXX_CUTOFF = Dm.world.maxx + 1;
            float WORLDMAXY_CUTOFF = Dm.world.maxx + 1;
            float min_x = WORLDMAXX_CUTOFF;
            float min_y = WORLDMAXY_CUTOFF;
            float max_x = -1;
            float max_y = -1;
            foreach (area shuttle_area in Dm.each<area>(shuttle_areas))
            {
                foreach (DmList zlevel_turfs in Dm.each<DmList>(shuttle_area.get_zlevel_turf_lists()))
                {
                    foreach (turf turf in Dm.each<turf>(zlevel_turfs))
                    {
                        min_x = Math.Min(turf.x, min_x);
                        max_x = Math.Max(turf.x, max_x);
                        min_y = Math.Min(turf.y, min_y);
                        max_y = Math.Max(turf.y, max_y);
                    }
                }
            }

            if (min_x == WORLDMAXX_CUTOFF || max_x == -1)
                throw Dm.CRASH("Failed to locate shuttle boundaries when iterating through shuttle areas, somehow.", "code/modules/shuttle/mobile_port/mobile_port.dm", 140);
            if (min_y == WORLDMAXY_CUTOFF || max_y == -1)
                throw Dm.CRASH("Failed to locate shuttle boundaries when iterating through shuttle areas, somehow.", "code/modules/shuttle/mobile_port/mobile_port.dm", 142);

            width = (max_x - min_x) + 1;
            height = (max_y - min_y) + 1;
            port_x_offset = x - min_x + 1;
            port_y_offset = y - min_y + 1;
        }

        if (dir == EAST || dir == WEST)
        {
            this.width = height;
            this.height = width;
        }
        else
        {
            this.width = width;
            this.height = height;
        }

        if (dir == NORTH)
        {
            dwidth = port_x_offset - 1;
            dheight = port_y_offset - 1;
        }
        else if (dir == EAST)
        {
            dwidth = height - port_y_offset;
            dheight = port_x_offset - 1;
        }
        else if (dir == SOUTH)
        {
            dwidth = width - port_x_offset;
            dheight = height - port_y_offset;
        }
        else if (dir == WEST)
        {
            dwidth = port_y_offset - 1;
            dheight = width - port_x_offset;
        }
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:172
    public override object is_in_shuttle_bounds(atom A = null)
    {
        object dot = base.is_in_shuttle_bounds(A);
        if (Dm.truthy(dot) && !Dm.truthy(shuttle_areas[get_area(A)]))
            return FALSE;
        return dot;
    }

    /**
     * Actions to be taken after shuttle is loaded but before it has been moved out of transit z-level to its final location
     *
     * Arguments:
     * * replace - TRUE if this shuttle is replacing an existing one. FALSE by default.
     * * custom -  TRUE if this shuttle should be added to the custom shuttle list. FALSE by default.
     */
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:184
    public override object register(object replace = null, object custom = null)
    {
        replace ??= FALSE;
        custom ??= FALSE;
        object dot = base.register(replace, custom);
        if (!Dm.truthy(shuttle_id))
            shuttle_id = "shuttle";

        if (!Dm.truthy(name))
            name = "shuttle";

        object counter = SSshuttle.assoc_mobile[shuttle_id];
        if (!Dm.truthy(replace) || !Dm.truthy(counter))
        {
            if (Dm.truthy(counter))
            {
                float next = Dm.num(counter) + 1;
                SSshuttle.assoc_mobile[shuttle_id] = next;
                shuttle_id = Dm.text("[]_[]", shuttle_id, next);
                name = Dm.text("[] []", name, next);
                //Re link machinery to new shuttle id
                Waiting.NoSleep(linkup());
            }
            else
            {
                SSshuttle.assoc_mobile[shuttle_id] = 1f;
            }
        }

        Dm.append(ref SSshuttle.mobile_docking_ports, this);

        if (Dm.truthy(custom))
            Dm.append(ref SSshuttle.custom_shuttles, this);
        return dot;
    }

    /**
     * Actions to be taken after shuttle is loaded and has been moved to its final location
     *
     * Arguments:
     * * replace - TRUE if this shuttle is replacing an existing one. FALSE by default.
     */
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:215  (a DmTask since gaps CM1: the build-your-own shuttle docks in its override)
    public virtual DmTask postregister(float? replace = null)
    {
        return DmTask.CompletedTask;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:218
    public override object unregister()
    {
        object dot = base.unregister();
        Dm.remove(ref SSshuttle.mobile_docking_ports, this);
        Dm.remove(ref SSshuttle.custom_shuttles, this);
        return dot;
    }

    // Called after the shuttle is loaded from template, so we make sure they know it's from mapload.
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:226
    public virtual async DmTask linkup(obj_docking_port_stationary dock = null)
    {
        foreach (area place in Dm.each<area>(shuttle_areas))
        {
            await place.connect_to_shuttle(TRUE, this, dock);
            foreach (atom individual_atoms in Dm.each<atom>(place))
                await individual_atoms.connect_to_shuttle(TRUE, this, dock);
        }
    }

    //this is a hook for custom behaviour. Maybe at some point we could add checks to see if engines are intact
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:233
    public virtual object canMove()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_SHUTTLE_SHOULD_MOVE]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_SHUTTLE_SHOULD_MOVE, Dm.list(this)), BLOCK_SHUTTLE_MOVE)))
            return FALSE;
        return TRUE;
    }

    //this is to check if this shuttle can physically dock at dock stationary_dock
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:240
    public virtual object canDock(obj_docking_port_stationary stationary_dock = null)
    {
        if (stationary_dock == null)
            return SHUTTLE_NOT_A_DOCKING_PORT;

        if (Dm.truthy(stationary_dock.override_can_dock_checks))
            return SHUTTLE_CAN_DOCK;

        if (dwidth > stationary_dock.dwidth)
            return SHUTTLE_DWIDTH_TOO_LARGE;

        if (width - dwidth > stationary_dock.width - stationary_dock.dwidth)
            return SHUTTLE_WIDTH_TOO_LARGE;

        if (dheight > stationary_dock.dheight)
            return SHUTTLE_DHEIGHT_TOO_LARGE;

        if (height - dheight > stationary_dock.height - stationary_dock.dheight)
            return SHUTTLE_HEIGHT_TOO_LARGE;

        //check the dock isn't occupied
        object currently_docked = stationary_dock.get_docked();
        if (currently_docked != null)
        {
            // by someone other than us
            if (currently_docked != this)
                return SHUTTLE_SOMEONE_ELSE_DOCKED;
            else
                // This isn't an error, per se, but we can't let the shuttle code
                // attempt to move us where we currently are, it will get weird.
                return SHUTTLE_ALREADY_DOCKED;
        }

        return SHUTTLE_CAN_DOCK;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:272
    public virtual object check_dock(obj_docking_port_stationary S = null, object silent = null)
    {
        silent ??= FALSE;
        object status = canDock(S);
        if (Dm.eq(status, SHUTTLE_CAN_DOCK))
            return TRUE;
        if (!Dm.eq(status, SHUTTLE_ALREADY_DOCKED) && !Dm.truthy(silent)) // SHUTTLE_ALREADY_DOCKED is no cause for error
            message_admins(Dm.text("Shuttle [] cannot dock at [], error: []", this, S, status));
        // We're already docked there, don't need to do anything.
        // Triggering shuttle movement code in place is weird
        return FALSE;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:283
    public virtual void transit_failure()
    {
        message_admins(Dm.text("Shuttle [] repeatedly failed to create transit zone.", this));
    }

    /**
     * Calls the shuttle to the destination port, respecting its ignition and call timers
     *
     * Arguments:
     * * destination_port - Stationary docking port to move the shuttle to
     */
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:292
    // the emergency shuttle's request adds signal_origin, reason, red_alert and set_coefficient (emergency.dm:38): the slot's superset
    public virtual object request(obj_docking_port_stationary destination_port = null, area signal_origin = null, object reason = null, object red_alert = null, object set_coefficient = null)
    {
        if (!Dm.truthy(check_dock(destination_port)))
            return null; // testing("check_dock failed on request for [src]")

        if (Dm.eq(mode, SHUTTLE_IGNITING) && destination == destination_port)
            return null;

        if (!Dm.truthy(canMove()))
            return null;

        if (Dm.eq(mode, SHUTTLE_CALL))
        {
            if (destination_port == destination)
            {
                if (Dm.num(timeLeft(1)) < callTime * engine_coeff)
                    setTimer(callTime * engine_coeff);
            }
            else
            {
                destination = destination_port;
                setTimer(callTime * engine_coeff);
            }
        }
        else if (Dm.eq(mode, SHUTTLE_RECALL))
        {
            if (destination_port == destination)
            {
                setTimer(callTime * engine_coeff - Dm.num(timeLeft(1)));
            }
            else
            {
                destination = destination_port;
                setTimer(callTime * engine_coeff);
            }
            mode = SHUTTLE_CALL;
        }
        else if (Dm.eq(mode, SHUTTLE_IDLE) || Dm.eq(mode, SHUTTLE_IGNITING))
        {
            destination = destination_port;
            mode = SHUTTLE_IGNITING;
            setTimer(ignitionTime);
        }
        return null;
    }

    //recall the shuttle to where it was previously
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:324
    // the emergency shuttle's cancel takes signal_origin (emergency.dm:68): the slot's superset
    public virtual object cancel(area signal_origin = null)
    {
        if (!Dm.eq(mode, SHUTTLE_CALL))
            return null;

        remove_ripples();

        invertTimer();
        mode = SHUTTLE_RECALL;
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:333
    public virtual async DmTask enterTransit()
    {
        if ((Dm.truthy(SSshuttle.lockdown) && Dm.truthy(is_station_level(z))) || !Dm.truthy(canMove())) //emp went off, no escape
        {
            mode = SHUTTLE_IDLE;
            return;
        }
        previous = null;
        if (destination == null)
        {
            // sent to transit with no destination -> unlimited timer
            timer = float.PositiveInfinity;
        }
        var S0 = get_docked() as obj_docking_port_stationary;
        obj_docking_port_stationary S1 = assigned_transit;
        if (S1 != null)
        {
            if (!Dm.eq(await initiate_docking(S1), DOCKING_SUCCESS))
                warning(Dm.text("shuttle \"[]\" could not enter transit space. Docked at []. Transit dock [].", shuttle_id, S0 != null ? S0.shuttle_id : "null", S1 != null ? S1.shuttle_id : "null"));
            else if (S0 != null)
            {
                if (Dm.truthy(S0.delete_after))
                    qdel(S0, TRUE);
                else
                    previous = S0;
            }
        }
        else
        {
            warning(Dm.text("shuttle \"[]\" could not enter transit space. S0=[] S1=[]", shuttle_id, S0 != null ? S0.shuttle_id : "null", S1 != null ? S1.shuttle_id : "null"));
        }
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:355
    public virtual async DmTask jumpToNullSpace()
    {
        // Destroys the docking port and the shuttle contents.
        // Not in a fancy way, it just ceases.
        var current_dock = get_docked() as obj_docking_port_stationary;

        Type underlying_area_type = typeof(area_space); // SHUTTLE_DEFAULT_UNDERLYING_AREA (__DEFINES/shuttles.dm)
        // If the shuttle is docked to a stationary port, restore its normal
        // "empty" area and turf
        if (current_dock?.area_type != null)
            underlying_area_type = current_dock.area_type;

        DmList old_turfs = return_ordered_turfs(x, y, z, dir);

        var underlying_area = GLOB.areas_by_type[underlying_area_type] as area;
        if (underlying_area == null)
            underlying_area = (area)Dm.@new(underlying_area_type, (object)null);

        for (float i = 1; i <= old_turfs.len; i++)
        {
            var oldT = old_turfs[i] as turf;
            if (oldT == null || !Dm.istype(oldT.loc, area_type))
                continue;
            oldT.change_area((area)oldT.loc, underlying_area);
            await oldT.empty(FALSE); // turf_type FALSE: the turf stays, the shuttle's boundary is scraped away below
            oldT = (turf)turf.dm_identity(oldT); // TURF-IDENTITY: tg's oldT names the location, emptied

            // Here we locate the bottommost shuttle boundary and remove all turfs above it
            object shuttle_tile_depth = oldT.depth_to_find_baseturf(typeof(turf_baseturf_skipover_shuttle));
            if (shuttle_tile_depth != null)
                await oldT.ScrapeAway(Dm.num(shuttle_tile_depth));
        }

        qdel(this, force: TRUE);
    }

    /**
     * Ghosts and marks as escaped (for greentext purposes) all mobs, then deletes the shuttle.
     * Used by the Shuttle Manipulator
     */
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:390
    public virtual async DmTask intoTheSunset()
    {
        // Loop over mobs
        foreach (turf turfs in Dm.each<turf>(return_turfs()))
        {
            foreach (mob_living sunset_mobs in Dm.each<mob_living>(turfs.get_all_contents()))
            {
                // If they have a mind and they're not in the brig, they escaped
                if (sunset_mobs.mind != null && get_area(sunset_mobs) is not area_shuttle_escape_brig)
                    sunset_mobs.mind.force_escaped = TRUE;
                // Ghostize them and put them in nullspace stasis (for stat & possession checks)
                ADD_TRAIT(sunset_mobs, TRAIT_NO_TRANSFORM, REF(this));
                sunset_mobs.ghostize(FALSE);
                Waiting.NoSleep(sunset_mobs.moveToNullspace());
            }
        }

        // Now that mobs are stowed, delete the shuttle
        await jumpToNullSpace();
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:405
    public virtual void create_ripples(obj_docking_port_stationary S1 = null, object animate_time = null)
    {
        // A dock the map lacks has no turfs to ripple: tg's multiz debug map has no emergency_home, which the emergency shuttle's check
        // asks for by id (getDock warns and gives null), and ripple_area would runtime on every check of the call (tg's too)
        if (S1 == null)
            return;
        DmList turfs = ripple_area(S1);
        foreach (object t in Dm.each(turfs))
            Dm.append(ref ripples, Dm.@new<obj_effect_abstract_ripple>(t, animate_time));
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:410
    public virtual void remove_ripples()
    {
        // QDEL_LIST(ripples) (__DEFINES/qdel.dm), expanded inline
        if (ripples != null)
        {
            foreach (object ripple in Dm.each(ripples))
                qdel(ripple);
            ripples.Cut();
        }
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:413
    public virtual DmList ripple_area(obj_docking_port_stationary S1 = null)
    {
        DmList L0 = return_ordered_turfs(x, y, z, dir);
        DmList L1 = return_ordered_turfs(S1.x, S1.y, S1.z, S1.dir);

        DmList ripple_turfs = Dm.list();
        float stop = Math.Min(L0.len, L1.len);
        for (float i = 1; i <= stop; i++)
        {
            var T0 = (turf)L0[i];
            var T1 = (turf)L1[i];
            if (!Dm.istype(T0.loc, area_type) || T0.loc is area_shuttle_transit)
                continue; // not part of the shuttle
            Dm.append(ref ripple_turfs, T1);
        }

        return ripple_turfs;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:428
    public virtual async DmTask check_poddoors()
    {
        foreach (obj_machinery_door_poddoor_shuttledock pod in Dm.each<obj_machinery_door_poddoor_shuttledock>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor_shuttledock))))
            pod.check();
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:432
    public virtual async DmTask<object> dock_id(object id = null)
    {
        var port = SSshuttle.getDock(id) as obj_docking_port_stationary;
        if (port != null)
            return await initiate_docking(port);
        return null;
    }

    //used by shuttle subsystem to check timers
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:440
    public virtual async DmTask check()
    {
        check_effects();
        //process_events() if you were to add events to non-escape shuttles, uncomment this

        if (Dm.eq(mode, SHUTTLE_IGNITING))
            check_transit_zone();

        if (Dm.num(timeLeft(1)) > 0)
            return;
        // If we can't dock or we don't have a transit slot, wait for 20 ds,
        // then try again
        if (Dm.eq(mode, SHUTTLE_CALL) || Dm.eq(mode, SHUTTLE_PREARRIVAL))
        {
            if (Dm.truthy(prearrivalTime) && !Dm.eq(mode, SHUTTLE_PREARRIVAL))
            {
                mode = SHUTTLE_PREARRIVAL;
                setTimer(prearrivalTime);
                return;
            }
            object error = await initiate_docking(destination, preferred_direction);
            if (Dm.truthy(error) && Dm.truthy(Dm.bitand(error, Dm.bitor(DOCKING_NULL_DESTINATION, DOCKING_NULL_SOURCE))))
            {
                string msg = Dm.text("A mobile dock in transit exited initiate_docking() with an error. This is most likely a mapping problem: Error: [],  ([]) ([] -> [])", error, this, previous, destination);
                warning(msg);
                message_admins(msg);
                mode = SHUTTLE_IDLE;
                return;
            }
            else if (Dm.truthy(error))
            {
                setTimer(20);
                return;
            }
            if (Dm.truthy(rechargeTime))
            {
                mode = SHUTTLE_RECHARGING;
                setTimer(rechargeTime);
                return;
            }
        }
        else if (Dm.eq(mode, SHUTTLE_RECALL))
        {
            if (!Dm.eq(await initiate_docking(previous), DOCKING_SUCCESS))
            {
                setTimer(20);
                return;
            }
        }
        else if (Dm.eq(mode, SHUTTLE_IGNITING))
        {
            if (!Dm.eq(check_transit_zone(), TRANSIT_READY))
            {
                setTimer(20);
                return;
            }
            else
            {
                mode = SHUTTLE_CALL;
                setTimer(callTime * engine_coeff);
                await enterTransit();
                return;
            }
        }

        mode = SHUTTLE_IDLE;
        timer = 0f;
        destination = null;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:489
    public virtual void check_effects()
    {
        if (!Dm.truthy(ripples.len))
        {
            if (Dm.eq(mode, SHUTTLE_CALL) || Dm.eq(mode, SHUTTLE_RECALL))
            {
                float tl = Dm.num(timeLeft(1));
                if (tl <= SHUTTLE_RIPPLE_TIME)
                    create_ripples(destination, tl);
            }
        }

        object S0 = get_docked();
        if (S0 is obj_docking_port_stationary_transit && Dm.num(timeLeft(1)) <= PARALLAX_LOOP_TIME)
        {
            foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(shuttle_areas))
            {
                if (Dm.truthy(shuttle_area.parallax_movedir))
                    parallax_slowdown();
            }
        }
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:503
    public virtual void parallax_slowdown()
    {
        foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(shuttle_areas))
            shuttle_area.set_parallax_movedir(NONE);

        assigned_transit?.assigned_area?.set_parallax_movedir(NONE);
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:510
    public virtual object check_transit_zone()
    {
        if (assigned_transit != null)
            return TRANSIT_READY;
        SSshuttle.request_transit_dock(this);
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:516
    public virtual void setTimer(object wait = null)
    {
        timer = Dm.world.time + Dm.num(wait);
        last_timer_length = wait;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:520
    public virtual void modTimer(object multiple = null)
    {
        float time_remaining = Dm.num(timer) - Dm.world.time;
        if (time_remaining < 0 || !Dm.truthy(last_timer_length))
            return;
        time_remaining *= Dm.num(multiple);
        last_timer_length = Dm.num(last_timer_length) * Dm.num(multiple);
        setTimer(time_remaining);
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:528
    public virtual void alert_coeff_change(object new_coeff = null)
    {
        if (new_coeff == null)
            return;

        float time_multiplier = Dm.num(new_coeff) / alert_coeff;
        float time_remaining = Dm.num(timer) - Dm.world.time;
        if (time_remaining < 0 || !Dm.truthy(last_timer_length))
            return;

        time_remaining *= time_multiplier;
        last_timer_length = Dm.num(last_timer_length) * time_multiplier;
        alert_coeff = Dm.num(new_coeff);
        setTimer(time_remaining);
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:542
    public virtual void invertTimer()
    {
        if (!Dm.truthy(last_timer_length))
            return;
        float time_remaining = Dm.num(timer) - Dm.world.time;
        if (time_remaining > 0)
        {
            float time_passed = Dm.num(last_timer_length) - time_remaining;
            setTimer(time_passed);
        }
    }

    //returns timeLeft
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:551
    public virtual object timeLeft(object divisor = null)
    {
        float d = Dm.num(divisor);
        if (d <= 0)
            d = 10;

        float ds_remaining;
        if (!Dm.truthy(timer))
            ds_remaining = callTime * engine_coeff;
        else
            ds_remaining = Math.Max(0, Dm.num(timer) - Dm.world.time);

        return Dm.round(ds_remaining / d, 1);
    }

    // returns 3-letter mode string, used by status screens and mob status panel
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:564
    public virtual object getModeStr()
    {
        if (Dm.eq(mode, SHUTTLE_IGNITING)) return "IGN";
        if (Dm.eq(mode, SHUTTLE_RECALL)) return "RCL";
        if (Dm.eq(mode, SHUTTLE_CALL)) return "ETA";
        if (Dm.eq(mode, SHUTTLE_DOCKED)) return "ETD";
        if (Dm.eq(mode, SHUTTLE_ESCAPE)) return "ESC";
        if (Dm.eq(mode, SHUTTLE_STRANDED)) return "ERR";
        if (Dm.eq(mode, SHUTTLE_RECHARGING)) return "RCH";
        if (Dm.eq(mode, SHUTTLE_PREARRIVAL)) return "LDN";
        if (Dm.eq(mode, SHUTTLE_DISABLED)) return "DIS";
        return "";
    }

    // returns 5-letter timer string, used by status screens and mob status panel
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:587
    public virtual object getTimerStr()
    {
        if (Dm.eq(mode, SHUTTLE_STRANDED) || Dm.eq(mode, SHUTTLE_DISABLED))
            return "--:--";

        float timeleft = Dm.num(timeLeft());
        if (timeleft > 1 * 60 * 60 * 10) // 1 HOURS
            return "--:--";
        else if (timeleft > 0)
            return Dm.text("[]:[]", add_leading(num2text(Dm.mod(timeleft / 60, 60)), 2, "0"), add_leading(num2text(Dm.mod(timeleft, 60)), 2, "0"));
        else
            return "00:00";
    }

    /**
     * Gets shuttle location status in a form of string for tgui interfaces
     */
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:602
    public virtual object get_status_text_tgui()
    {
        var dockedAt = get_docked() as obj_docking_port_stationary;
        object docked_at = Dm.truthy(dockedAt?.name) ? dockedAt.name : "Unknown";
        if (dockedAt is not obj_docking_port_stationary_transit)
            return docked_at;
        if (Dm.num(timeLeft()) > 1 * 60 * 60 * 10) // 1 HOURS
            return "Hyperspace";
        obj_docking_port_stationary dst = Dm.eq(mode, SHUTTLE_RECALL) ? previous : destination;
        return Dm.text("In transit to []", Dm.truthy(dst?.name) ? dst.name : "unknown location");
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:613
    public virtual object getStatusText()
    {
        var dockedAt = get_docked() as obj_docking_port_stationary;
        object docked_at = Dm.truthy(dockedAt?.name) ? dockedAt.name : "unknown";
        if (dockedAt is obj_docking_port_stationary_transit)
        {
            if (Dm.num(timeLeft()) > 1 * 60 * 60 * 10) // 1 HOURS
                return "hyperspace";
            obj_docking_port_stationary dst = Dm.eq(mode, SHUTTLE_RECALL) ? previous : destination;
            return Dm.text("transit towards [] ([])", Dm.truthy(dst?.name) ? dst.name : "unknown location", getTimerStr());
        }
        else if (Dm.eq(mode, SHUTTLE_RECHARGING))
        {
            return Dm.text("[], recharging []", docked_at, getTimerStr());
        }
        return docked_at;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:631
    public virtual object getDbgStatusText()
    {
        var dockedAt = get_docked() as obj_docking_port_stationary;
        object dot = Dm.truthy(dockedAt?.name) ? dockedAt.name : "unknown";
        if (dockedAt is obj_docking_port_stationary_transit)
        {
            obj_docking_port_stationary dst = Dm.eq(mode, SHUTTLE_RECALL) ? previous : destination;
            if (dst != null)
                dot = Dm.text("(transit to) []", Dm.truthy(dst.name) ? dst.name : dst.shuttle_id);
            else
                dot = "(transit to) nowhere";
        }
        else if (dockedAt != null)
        {
            dot = Dm.truthy(dockedAt.name) ? dockedAt.name : dockedAt.shuttle_id;
        }
        else
        {
            dot = "unknown";
        }
        return dot;
    }

    // attempts to locate /obj/machinery/computer/shuttle with matching ID inside the shuttle
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:651
    public virtual object get_control_console()
    {
        foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(shuttle_areas))
        {
            var shuttle_computer = locate<obj_machinery_computer_shuttle>(shuttle_area);
            if (shuttle_computer == null)
                continue;
            if (Dm.eq(shuttle_computer.shuttleId, shuttle_id))
                return shuttle_computer;
        }
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:660
    public virtual void hyperspace_sound(object phase = null, DmList areas = null)
    {
        string selected_sound;
        float p = Dm.num(phase);
        if (p == HYPERSPACE_WARMUP)
            selected_sound = "hyperspace_begin";
        else if (p == HYPERSPACE_LAUNCH)
            selected_sound = "hyperspace_progress";
        else if (p == HYPERSPACE_END)
            selected_sound = "hyperspace_end";
        else
            throw Dm.CRASH(Dm.text("Invalid hyperspace sound phase: []", phase), "code/modules/shuttle/mobile_port/mobile_port.dm", 670);
        // This previously was played from each door at max volume, and was one of the worst things I had ever seen.
        // Now it's instead played from the nearest engine if close, or the first engine in the list if far since it doesn't really matter.
        // Or a door if for some reason the shuttle has no engine, fuck oh hi daniel fuck it
        float range = (engine_coeff * Math.Max(width, height));
        float long_range = range * 2.5f;
        atom distant_source = null;

        if (Dm.truthy(engine_list.len))
        {
            distant_source = (atom)engine_list[1];
        }
        else
        {
            foreach (object our_area in Dm.each(areas))
            {
                distant_source = locate<obj_machinery_door>(our_area);
                if (distant_source != null)
                    break;
            }
        }

        if (distant_source == null)
            return;
        foreach (mob zlevel_mobs in Dm.each<mob>(datum_controller_subsystem_mobs.clients_by_zlevel[z]))
        {
            float dist_far = get_dist(zlevel_mobs, distant_source);
            if (dist_far <= long_range && dist_far > range)
            {
                zlevel_mobs.playsound_local(distant_source, Dm.text("sound/runtime/hyperspace/[]_distance.ogg", selected_sound), 100);
            }
            else if (dist_far <= range)
            {
                atom source = null;
                if (!Dm.truthy(engine_list.len))
                {
                    source = distant_source;
                }
                else
                {
                    float closest_dist = 10000;
                    foreach (obj_machinery_power_shuttle_engine engines in Dm.each<obj_machinery_power_shuttle_engine>(engine_list))
                    {
                        float dist_near = get_dist(zlevel_mobs, engines);
                        if (dist_near < closest_dist)
                        {
                            source = engines;
                            closest_dist = dist_near;
                        }
                    }
                }
                zlevel_mobs.playsound_local(source, Dm.text("sound/runtime/hyperspace/[].ogg", selected_sound), 100);
            }
        }
    }

    // Losing all initial engines should get you 2
    // Adding another set of engines at 0.5 time
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:707
    public virtual void alter_engines(float mod = 0)
    {
        if (mod == 0)
            return;
        float old_coeff = engine_coeff;
        engine_coeff = get_engine_coeff(mod);
        current_engine_power = Math.Max(0, current_engine_power + mod);
        if (Dm.truthy(in_flight()))
        {
            float delta_coeff = engine_coeff / old_coeff;
            modTimer(delta_coeff);
        }
    }

    // Double initial engines to get to 0.5 minimum
    // Lose all initial engines to get to 2
    //For 0 engine shuttles like BYOS 5 engines to get to doublespeed
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:720
    public virtual float get_engine_coeff(float engine_mod = 0)
    {
        float new_value = Math.Max(0, current_engine_power + engine_mod);
        if (new_value == initial_engine_power)
            return 1;
        if (new_value > initial_engine_power)
        {
            float delta = new_value - initial_engine_power;
            float change_per_engine = (1 - ENGINE_COEFF_MIN) / ENGINE_DEFAULT_MAXSPEED_ENGINES; // 5 by default
            if (initial_engine_power > 0)
                change_per_engine = (1 - ENGINE_COEFF_MIN) / initial_engine_power; // or however many it had
            return Math.Clamp(1 - delta * change_per_engine, ENGINE_COEFF_MIN, ENGINE_COEFF_MAX);
        }
        else
        {
            float delta = initial_engine_power - new_value;
            float change_per_engine = 1; //doesn't really matter should not be happening for 0 engine shuttles
            if (initial_engine_power > 0)
                change_per_engine = (ENGINE_COEFF_MAX - 1) / initial_engine_power; //just linear drop to max delay
            return Math.Clamp(1 + delta * change_per_engine, ENGINE_COEFF_MIN, ENGINE_COEFF_MAX);
        }
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:738
    public virtual object in_flight()
    {
        if (Dm.eq(mode, SHUTTLE_CALL) || Dm.eq(mode, SHUTTLE_RECALL) || Dm.eq(mode, SHUTTLE_PREARRIVAL))
            return TRUE;
        if (Dm.eq(mode, SHUTTLE_IDLE) || Dm.eq(mode, SHUTTLE_IGNITING))
            return FALSE;
        return FALSE; // hmm
    }

    //Called when emergency shuttle leaves the station
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:755
    public virtual async DmTask on_emergency_launch()
    {
        if (Dm.eq(launch_status, UNLAUNCHED)) //Pods will not launch from the mine/planet, and other ships won't launch unless we tell them to.
        {
            launch_status = ENDGAME_LAUNCHED;
            await enterTransit();
        }
    }

    //Called when emergency shuttle docks at centcom
    ///Process all the shuttle events for every shuttle tick we get
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:784
    public virtual void process_events()
    {
        DmList removees = null;
        foreach (datum_shuttle_event @event in Dm.each<datum_shuttle_event>(event_list))
        {
            if (Dm.eq(@event.event_process(), SHUTTLE_EVENT_CLEAR)) //if we return SHUTTLE_EVENT_CLEAR, we clean them up
                LAZYADD(ref removees, @event);
        }
        foreach (object item in Dm.each(removees))
            event_list.Remove(item);
    }

    /// Give a typepath of a shuttle event to add to the shuttle. If added during endgame transit, will insta start the event
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:793
    public virtual datum_shuttle_event add_shuttle_event(Type typepath = null)
    {
        var @event = (datum_shuttle_event)Dm.@new(typepath, this);
        Dm.append(ref event_list, @event);
        if (Dm.eq(launch_status, ENDGAME_LAUNCHED))
            @event.start_up_event(0);
        return @event;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:769
    public virtual async DmTask on_emergency_dock()
    {
        // Mapping a new docking point for each ship mappers could potentially want docking with centcom would take up lots of space,
        // just let them keep flying off "into the sunset" for their greentext.
        if (Dm.eq(launch_status, ENDGAME_LAUNCHED))
            launch_status = ENDGAME_TRANSIT;
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/shuttle/mobile_port/mobile_port.dm:746
public partial class obj_docking_port_mobile_emergency : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:746
    public override object in_flight()
    {
        if (Dm.eq(mode, SHUTTLE_ESCAPE))
            return TRUE;
        if (Dm.eq(mode, SHUTTLE_STRANDED) || Dm.eq(mode, SHUTTLE_ENDGAME))
            return FALSE;
        return base.in_flight();
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:765
    public override async DmTask on_emergency_launch()
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:780
    public override async DmTask on_emergency_dock()
    {
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/shuttle/mobile_port/mobile_port.dm:775
public partial class obj_docking_port_mobile_pod : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/mobile_port.dm:775
    public override async DmTask on_emergency_dock()
    {
        if (Dm.eq(launch_status, ENDGAME_LAUNCHED))
        {
            await initiate_docking(SSshuttle.getDock(Dm.text("[]_away", shuttle_id)) as obj_docking_port_stationary); //Escape pods dock at centcom
            mode = SHUTTLE_ENDGAME;
        }
    }
}
