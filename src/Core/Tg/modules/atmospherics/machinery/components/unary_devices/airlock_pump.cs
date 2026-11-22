// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 5: the whole file. The cycle (airlock_act, start_cycle, stop_cycle, emergency_stop, process_atmos with
// fill_tile and siphon_tile, check_turfs), the links (set_links, find_density, get_adjacent_airlocks, is_valid_airlock,
// unlink_airlock, break_all_links), the pictures (update_icon_nopipes, update_overlays), the init (atmos_init, Initialize,
// post_machine_initialize), the power (set_on, on_set_is_operational), the docking procs (on_dock_request, safe_dock,
// undock: unreachable while no shuttle docks, D108) and the rest (on_deconstruction, can_unwrench, relaymove). Deferred
// leaves: the spoken lines (say, M9) and the hearers' messages of the power change (audible_message, M9). DEVIATION(DV34):
// the gas the pump moves is reported to the airflow seam, like a vent's. The var blocks are generated.

/**
 * The pump looks up for the airlocks automatically based on airlock_pump_distance_limit and airlock_group_distance_limit values.
 * When placed, the dir value (direction where the pipes are coming from) is considered as a direction towards the station (internal). The opposite direction is external.
 * The airlock then tries to find airlocks or walls towards these directions until airlock_pump_distance_limit number of tiles reached.
 * When it finds a valid object, then it tries to find airlocks, in directions perpendicular to the found tiles.
 * And then adds them to the corresponding group (external/internal) until airlock_group_distance_limit number of tiles reached
 *
 * Example scheme of a valid configuration:
 * A-----W
 * A-----A
 * W--P--A
 * W-----W
 * A-----W
 *
 * Where:
 * A - airlocks
 * W - walls
 * P - pump
 */
/// A vent, scrubber and a sensor in a single device meant specifically for cycling airlocks. Ideal for airlocks of up to 3x3 tiles in size to avoid wind and timing out.
// tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:21
public partial class obj_machinery_atmospherics_components_unary_airlock_pump : obj_machinery_atmospherics_components_unary
{
    private const string File = "code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm";

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:84
    public override void update_icon_nopipes(params object[] __extra)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational) || !Dm.truthy(powered__proc()))
            icon_state = "vent_off";
        else
            icon_state = Dm.truthy(pump_direction) ? "vent_out" : "vent_in";
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:91
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(underfloor_state))
            return dot;

        atom distro_node = nodes[1] as atom;
        atom waste_node = nodes[2] as atom;
        Dm.append(ref dot, get_pipe_image(
            iconfile: icon,
            iconstate: Dm.text("pipe_[]", nodes[1] == null ? "exposed" : "intact"),
            direction: dir,
            color: Dm.truthy(distro_node?.color) ? Dm.text("[]", distro_node.color) : COLOR_BLUE, // default to blue to indicate input
            piping_layer: 4));
        Dm.append(ref dot, get_pipe_image(
            iconfile: icon,
            iconstate: Dm.text("pipe_[]", nodes[2] == null ? "exposed" : "intact"),
            direction: dir,
            color: Dm.truthy(waste_node?.color) ? Dm.text("[]", waste_node.color) : COLOR_RED, // default to red to indicate output
            piping_layer: 2));

        Dm.append(ref dot, get_pipe_image(icon, "vent_cap", dir, piping_layer: 4));
        Dm.append(ref dot, get_pipe_image(icon, "vent_cap", dir, piping_layer: 2));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:114
    public override void atmos_init(DmList node_connects = null)
    {
        foreach (obj_machinery_atmospherics target in Dm.each<obj_machinery_atmospherics>(get_step(this, dir)))
        {
            // DEVIATION(DV31), as the base atmos_init: an inert device has no gas mixtures and is no node
            if (InertSeam.IsInert(target))
                continue;
            if (Dm.truthy(connection_check(target, 4)) && !Dm.truthy(nodes[1]))
                nodes[1] = target; // Distro
            if (Dm.truthy(connection_check(target, 2)) && !Dm.truthy(nodes[2]))
                nodes[2] = target; // Waste
        }
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:123
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            can_unwrench = FALSE;
        datum_gas_mixture distro_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture waste_air = airs[2] as datum_gas_mixture;
        distro_air.volume = 1000;
        waste_air.volume = 1000;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:132
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        await set_links();
        // If we are on docked shuttle - setup docking variables
        // Example - 'build your own shuttle' evac vessel
        turf local_turf = get_turf(this);
        if (!Dm.truthy(cycling_set_up) || !isshuttleturf(local_turf))
            return;

        float tile_air_pressure;
        foreach (obj_machinery_door_airlock external_airlock in Dm.each<obj_machinery_door_airlock>(external_airlocks))
        {
            area current_area = get_area(external_airlock) as area;
            foreach (obj_machinery_door_airlock other_airlock in Dm.each<obj_machinery_door_airlock>(orange(2, external_airlock))) // does not include src, extended because some escape pods have 1 plating turf exposed to space
            {
                if (get_area(other_airlock) != current_area) // does not include double-wide airlocks unless actually docked
                {
                    // Cycle linking is only disabled if we are actually adjacent to another airlock
                    external_airlock.shuttledocked = TRUE;
                    other_airlock.shuttledocked = TRUE;
                    if (other_airlock.cycle_pump != null)
                        Waiting.Detach(other_airlock.cycle_pump.on_dock_request(internal_pressure_target)); // Only case when airlock pumps speaking to each other directly
                    // Save external airlocks turf in case our own docking purpouses
                    local_turf = get_turf(other_airlock);
                }
            }
        }

        if (local_turf != null)
        {
            local_turf = get_step(local_turf, REVERSE_DIR(dir));
            tile_air_pressure = 0;
            if (local_turf != null)
                tile_air_pressure = max(0, Dm.num((await local_turf.return_air() as datum_gas_mixture).return_pressure()));
            await on_dock_request(tile_air_pressure);
        }
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:161
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        object dot = await base.on_deconstruction(disassembled);
        if (Dm.truthy(cycling_set_up))
            break_all_links();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:167
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (!Dm.truthy(dot))
        {
            to_chat(user, span_warning(I18n.T("You cannot unwrench [], it is secured firmly in place!", this)));
            return FALSE;
        }
        if (Dm.truthy(dot) && Dm.truthy(on))
        {
            to_chat(user, span_warning(I18n.T("You cannot unwrench [], wait for the cycle completion!", this)));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:176
    public override void set_on(object active = null)
    {
        base.set_on(active);
        if (Dm.truthy(active))
            update_use_power(ACTIVE_POWER_USE);
        else
            update_use_power(IDLE_POWER_USE);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:183
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on))
            return dot.Value;
        if (!Dm.truthy(is_operational))
        {
            stack_trace("Airlock pump on but not operational, it should've been cancelled by on_set_is_operational", File, 187);
            await stop_cycle(null, unbolt_only: TRUE);
            return dot.Value;
        }

        turf location = get_turf(loc);
        if (isclosedturf(location))
            return dot.Value;

        if (check_turfs_cooldown <= world.time) // COOLDOWN_FINISHED(src, check_turfs_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            check_turfs();
            check_turfs_cooldown = world.time + (2 * SECONDS); // COOLDOWN_START(src, check_turfs_cooldown, 2 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        }

        datum_gas_mixture distro_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture tile_air = await loc.return_air() as datum_gas_mixture;
        float tile_air_pressure = Dm.num(tile_air.return_pressure());

        if (Dm.num(pump_direction) == ATMOS_DIRECTION_RELEASING) //distro node -> tile
        {
            float pressure_delta = Dm.num(cycle_pressure_target) - tile_air_pressure;
            if (pressure_delta <= allowed_pressure_error && Dm.truthy(await stop_cycle("Pressurization complete.")))
                return dot.Value; //Internal target pressure reached

            float available_moles = Dm.num(distro_air.total_moles());
            float total_tiles = Dm.length(adjacent_turfs) + 1;
            float split_moles = QUANTIZE(available_moles / total_tiles);

            fill_tile(loc as turf, split_moles, pressure_delta);
            foreach (turf tile in Dm.each<turf>(adjacent_turfs))
                fill_tile(tile, split_moles, pressure_delta);
        }
        else //tile -> waste node
        {
            float pressure_delta = tile_air_pressure - Dm.num(cycle_pressure_target);
            if (pressure_delta <= allowed_pressure_error && Dm.truthy(await stop_cycle("Decompression complete.")))
                return dot.Value; //External target pressure reached

            siphon_tile(loc as turf);
            foreach (turf tile in Dm.each<turf>(adjacent_turfs))
                siphon_tile(tile);
        }
        return dot.Value;
    }

    /// Fill a tile with air from the distro node
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:226
    public virtual void fill_tile(turf tile = null, object moles = null, object pressure_delta = null)
    {
        datum_pipeline distro_pipe = parents[1] as datum_pipeline;
        datum_gas_mixture distro_air = airs[1] as datum_gas_mixture;
        datum_gas_mixture tile_air = Waiting.NoSleep(tile.return_air()) as datum_gas_mixture;
        float transfer_moles = (volume_rate / Dm.num(tile_air.volume)) * (Dm.num(pressure_delta) * Dm.num(tile_air.volume)) / (distro_air.temperature * R_IDEAL_GAS_EQUATION);
        moles = min(Dm.num(moles), transfer_moles);

        datum_gas_mixture removed_air = distro_air.remove(moles) as datum_gas_mixture;

        if (removed_air == null)
            return; //No air in distro

        AirflowSeam.VentOn(tile, removed_air, tile_air, outward: true); // DEVIATION(DV34): the clients' streams out of the pump
        Waiting.NoSleep(tile.assume_air(removed_air));
        distro_pipe.update = TRUE;
    }

    /// Siphon air from the tile to the waste node within the volume rate limit
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:243
    public virtual void siphon_tile(turf tile = null)
    {
        datum_pipeline waste_pipe = parents[2] as datum_pipeline;
        datum_gas_mixture waste_air = airs[2] as datum_gas_mixture;
        datum_gas_mixture tile_air = Waiting.NoSleep(tile.return_air()) as datum_gas_mixture;

        float transfer_moles = Dm.num(tile_air.total_moles()) * (volume_rate / Dm.num(tile_air.volume));
        datum_gas_mixture removed_air = tile.remove_air(transfer_moles) as datum_gas_mixture;

        if (removed_air == null)
            return; //No air on the tile

        AirflowSeam.VentOn(tile, removed_air, tile_air, outward: false); // DEVIATION(DV34): the clients' streams into the pump
        waste_air.merge(removed_air);
        waste_pipe.update = TRUE;
    }

    /// Proc for triggering cycle by clicking on a bolted airlock that has a pump assigned
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:259
    public virtual async DmTask airlock_act(obj_machinery_door_airlock airlock = null)
    {
        if (Dm.truthy(on))
        {
            await airlock.run_animation(DOOR_DENY_ANIMATION); // Already cycling
            return;
        }
        if (!Dm.truthy(cycling_set_up))
        {
            await airlock.say(I18n.T("Airlock pair not found."));
            return;
        }
        if (Dm.@in(airlock, external_airlocks))
        {
            // If it's not null - we shuttledocked
            // (it may be 0. Maybe badmin set internal pressure to 0 as well, who knows)
            if (docked_side_pressure != null)
            {
                // Space-faced airlock detection
                turf external_tile = get_step(airlock, REVERSE_DIR(dir));
                // Map edge or space turf
                if (external_tile == null || is_space_or_openspace(external_tile))
                {
                    await airlock.run_animation(DOOR_DENY_ANIMATION);
                    return;
                }
                float tile_air_pressure = max(0, Dm.num((await external_tile.return_air() as datum_gas_mixture).return_pressure()));
                float pressure_delta = Dm.num(docked_side_pressure) - tile_air_pressure;
                if (pressure_delta > 0 ? (pressure_delta > allowed_pressure_error * 10) : (pressure_delta * -1 > allowed_pressure_error * 10))
                {
                    // Disabled to avoid airlocks close-open spam
                    await airlock.run_animation(DOOR_DENY_ANIMATION);
                    return;
                }
            }

            await start_cycle(ATMOS_DIRECTION_SIPHONING, airlock);
        }
        else if (Dm.@in(airlock, internal_airlocks))
            await start_cycle(ATMOS_DIRECTION_RELEASING, airlock);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:287
    public virtual async DmTask emergency_stop()
    {
        if (!Dm.truthy(on) || Dm.truthy(airlocks_animating) || !Dm.truthy(is_operational))
            return; // Already done

        await stop_cycle("Cycling timed out, bolts unlocked.", unbolt_only: TRUE);
    }

    ///Start decompression or pressurization cycle depending on the passed direction
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:294
    public virtual async DmTask<object> start_cycle(object cycle_direction = null, obj_machinery_door_airlock source_airlock = null)
    {
        if (Dm.truthy(on) || !Dm.truthy(cycling_set_up) || Dm.truthy(airlocks_animating) || !Dm.truthy(powered__proc()))
            return FALSE;

        pump_direction = Dm.num(cycle_direction);

        foreach (obj_machinery_door_airlock airlock in Dm.each<obj_machinery_door_airlock>(Dm.add(internal_airlocks, external_airlocks)))
            Waiting.Detach(airlock.secure_close());

        airlocks_animating = TRUE;
        await stoplag(1 * SECONDS); // Wait for closing animation
        airlocks_animating = FALSE;

        set_on(TRUE);
        emergency_stop_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(emergency_stop)), cycle_timeout, Dm.bitor(Dm.bitor(Dm.bitor(TIMER_STOPPABLE, TIMER_UNIQUE), TIMER_OVERRIDE), TIMER_DELETE_ME), file: File, line: 308);

        turf local_turf = get_turf(this);
        float tile_air_pressure = max(0, Dm.num((await local_turf.return_air() as datum_gas_mixture).return_pressure()));

        if (Dm.num(pump_direction) == ATMOS_DIRECTION_RELEASING)
        {
            cycle_pressure_target = internal_pressure_target;
            float pressure_delta = Dm.num(cycle_pressure_target) - tile_air_pressure;
            if (pressure_delta <= allowed_pressure_error)
            {
                await stop_cycle("Pressure nominal, cycle skipped.");
                return TRUE;
            }

            datum_gas_mixture distro_air = airs[1] as datum_gas_mixture;
            if (Dm.num(distro_air.return_pressure()) < min_distro_pressure)
            {
                await stop_cycle("Low pipe pressure, cycle skipped. Proceed with caution.", unbolt_only: TRUE);
                return TRUE;
            }

            if (source_airlock == null)
                source_airlock = internal_airlocks[1] as obj_machinery_door_airlock;
            if (Dm.truthy(is_cycling_audible))
            {
                await source_airlock.say(I18n.T("Pressurizing airlock."));
            }
        }
        else
        {
            cycle_pressure_target = docked_side_pressure != null ? docked_side_pressure : external_pressure_target;
            float pressure_delta = tile_air_pressure - Dm.num(cycle_pressure_target);
            if (pressure_delta <= allowed_pressure_error)
            {
                await stop_cycle("Pressure nominal, cycle skipped.");
                return TRUE;
            }

            if (source_airlock == null)
                source_airlock = external_airlocks[1] as obj_machinery_door_airlock;
            if (Dm.truthy(is_cycling_audible))
            {
                await source_airlock.say(I18n.T("Decompressing airlock."));
            }
        }

        return TRUE;
    }

    ///Complete/Abort cycle with the passed message
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:344
    public virtual async DmTask<object> stop_cycle(object message = null, float? unbolt_only = null)
    {
        unbolt_only ??= FALSE;
        if (!Dm.truthy(on))
            return FALSE;
        set_on(FALSE);
        deltimer(emergency_stop_timer);

        // In case we can open both sides safe_dock will do it for us
        // it also handles its own messages. If we can't - procceed
        if (docked_side_pressure != null && Dm.truthy(await safe_dock(unbolt_only)))
            return TRUE;

        DmList unlocked_airlocks = Dm.num(pump_direction) == ATMOS_DIRECTION_RELEASING ? internal_airlocks : external_airlocks;
        foreach (obj_machinery_door_airlock airlock in Dm.each<obj_machinery_door_airlock>(unlocked_airlocks))
        {
            airlock.unbolt();
            if (Dm.truthy(open_airlock_on_cycle) && !Dm.truthy(unbolt_only))
                Waiting.Detach(airlock.secure_open()); //Can unbolt, but without audio
        }

        airlocks_animating = TRUE;
        await stoplag(1 * SECONDS); // Wait for opening animation
        airlocks_animating = FALSE;

        if (Dm.truthy(message) && Dm.truthy(is_cycling_audible))
            await ((obj_machinery_door_airlock)unlocked_airlocks[1]).say(message);

        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:371
    public virtual async DmTask on_dock_request(float? requester_pressure = null)
    {
        requester_pressure ??= 0;
        if (docked_side_pressure != null)
            return;

        docked_side_pressure = requester_pressure.Value;

        if (!Dm.truthy(powered__proc()) || !Dm.truthy(cycling_set_up))
            return;

        // We just finishing previous cycle
        if (Dm.truthy(airlocks_animating))
        {
            await say(I18n.T("Docking request queued."));
            await stoplag(1.1f * SECONDS); // Wait for opening animation
            if (Dm.truthy(airlocks_animating)) // Should (almost) never happened
            {
                await say(I18n.T("ERROR: D11. Please re-initiate docking sequence."));
                return;
            }
        }

        if (Dm.truthy(on))
        {
            // You can't go there, there is a shuttle now
            if (Dm.num(pump_direction) == ATMOS_DIRECTION_SIPHONING)
            {
                await stop_cycle("Cycling sequence overriden by docking sequence.", TRUE);
                await start_cycle(ATMOS_DIRECTION_RELEASING);
            }
            // If cycling inside, docking will be handled by stop_cycle proc
            return;
        }

        // Check if we need cycle in
        turf local_turf = get_turf(this);
        float tile_air_pressure = max(0, Dm.num((await local_turf.return_air() as datum_gas_mixture).return_pressure()));
        float pressure_delta = internal_pressure_target - tile_air_pressure;
        if (pressure_delta <= allowed_pressure_error)
        {
            // We fine
            await safe_dock();
        }
        else
        {
            obj_machinery_door_airlock source_airlock = pick(internal_airlocks) as obj_machinery_door_airlock;
            await source_airlock.say(I18n.T("Docking sequence initiated"));
            await start_cycle(ATMOS_DIRECTION_RELEASING);
        }
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:409
    public virtual async DmTask<object> safe_dock(float? unbolt_only = null)
    {
        unbolt_only ??= FALSE;
        float pressure_delta = internal_pressure_target - Dm.num(docked_side_pressure);
        // Docked vessel has pressure higher then our internal
        if ((pressure_delta + allowed_pressure_error) < 0)
            return FALSE;
        // Pressure is too different, its unsafe to open both sides
        else if (pressure_delta > allowed_pressure_error * 10)
            return FALSE;
        // No power handles by stop_cycle pretty good
        else if (!Dm.truthy(powered__proc()))
            return FALSE;

        turf local_turf = get_turf(this);
        float tile_air_pressure = max(0, Dm.num((await local_turf.return_air() as datum_gas_mixture).return_pressure()));
        pressure_delta = internal_pressure_target - tile_air_pressure;
        // Chamber is not pressurised
        if (pressure_delta > allowed_pressure_error)
            return FALSE;

        foreach (obj_machinery_door_airlock airlock in Dm.each<obj_machinery_door_airlock>(Dm.add(external_airlocks, internal_airlocks)))
        {
            if (Dm.@in(airlock, external_airlocks))
            {
                airlock.air_tight = TRUE;
                local_turf = get_step(airlock, REVERSE_DIR(dir));
                // Map edge or space turf
                if (local_turf == null || is_space_or_openspace(local_turf))
                    continue;

                tile_air_pressure = max(0, Dm.num((await local_turf.return_air() as datum_gas_mixture).return_pressure()));
                pressure_delta = Dm.num(docked_side_pressure) - tile_air_pressure;
                // Do not open airlocks leading in space
                // If docked entity now has pressure lower or higher then was declared on docking
                // We will keep airlocks closed until redocking or fixing atmos
                if (pressure_delta > 0 ? (pressure_delta > allowed_pressure_error * 10) : (pressure_delta * -1 > allowed_pressure_error * 10))
                    continue;
            }

            airlock.unbolt();
            if (Dm.truthy(open_airlock_on_cycle) && !Dm.truthy(unbolt_only))
                Waiting.Detach(airlock.secure_open());
        }

        airlocks_animating = TRUE;
        await stoplag(1 * SECONDS); // Wait for closing animation
        airlocks_animating = FALSE;
        update_appearance(UPDATE_ICON);
        await say(I18n.T("Docking complete."));
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:456
    public virtual async DmTask undock()
    {
        if (docked_side_pressure == null)
            return;
        docked_side_pressure = null;
        if (!Dm.truthy(powered__proc()))
            return;

        foreach (obj_machinery_door_airlock airlock in Dm.each<obj_machinery_door_airlock>(external_airlocks))
            Waiting.Detach(airlock.secure_close(TRUE));

        await say(I18n.T("Docking connection terminated."));
        airlocks_animating = TRUE;
        await stoplag(1 * SECONDS); // Wait for closing animation
        airlocks_animating = FALSE;
    }

    ///Update adjacent_turfs with atmospherically adjacent tiles
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:473
    public virtual void check_turfs()
    {
        adjacent_turfs.Cut();
        turf local_turf = get_turf(this);
        adjacent_turfs = local_turf.get_atmos_adjacent_turfs(alldir: TRUE) as DmList;
    }

    ///Find airlocks and link up with them
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:480
    public virtual async DmTask set_links()
    {
        float perpendicular_dirs = Dm.truthy(NSCOMPONENT(dir)) ? Dm.bitor(WEST, EAST) : Dm.bitor(NORTH, SOUTH);
        turf internal_airlocks_origin = find_density(get_turf(this), dir) as turf;
        turf external_airlocks_origin = find_density(get_turf(this), REVERSE_DIR(dir)) as turf;
        internal_airlocks = get_adjacent_airlocks(internal_airlocks_origin, perpendicular_dirs) as DmList;
        external_airlocks = get_adjacent_airlocks(external_airlocks_origin, perpendicular_dirs) as DmList;

        // This is support for awkwardly shaped airlocks that cycle on a group ID
        if (!Dm.truthy(length(internal_airlocks)))
        {
            foreach (obj_machinery_door_airlock external_airlock in Dm.each_cast<obj_machinery_door_airlock>(external_airlocks))
                Dm.combine(ref internal_airlocks, external_airlock.close_others);
            // For double-wide airlocks, so we don't end up with doors in both lists
            Dm.remove(ref internal_airlocks, external_airlocks);
        }
        if (!Dm.truthy(length(external_airlocks)))
        {
            foreach (obj_machinery_door_airlock internal_airlock in Dm.each_cast<obj_machinery_door_airlock>(internal_airlocks))
                Dm.combine(ref external_airlocks, internal_airlock.close_others);
            Dm.remove(ref external_airlocks, internal_airlocks);
        }

        if (!Dm.truthy(length(external_airlocks)) || !Dm.truthy(length(internal_airlocks)))
        {
            if (!Dm.truthy(can_unwrench)) //maploaded pump
                throw Dm.CRASH(Dm.text("[] couldn't find airlocks to cycle with!", type), File, 500);
            internal_airlocks.Cut();
            external_airlocks.Cut();
            await say(I18n.T("Cycling setup failed. No opposite airlocks found."));
            return;
        }

        foreach (obj_machinery_door_airlock airlock in Dm.each_cast<obj_machinery_door_airlock>(Dm.add(internal_airlocks, external_airlocks)))
        {
            airlock.set_cycle_pump(this);
            RegisterSignal(airlock, COMSIG_QDELETING, nameof(unlink_airlock));
            if (Dm.@in(airlock, external_airlocks))
                Waiting.Detach(airlock.secure_close());
            else if (Dm.truthy(open_airlock_on_cycle))
                Waiting.Detach(airlock.secure_open());
        }

        cycling_set_up = TRUE;
        if (Dm.truthy(can_unwrench))
        {
            await say(I18n.T("Cycling setup complete."));
        }
    }

    ///Get the turf of the first found airlock or an airtight structure (walls) within the allowed range
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:520
    public virtual object find_density(turf origin = null, object direction = null, object max_distance = null)
    {
        max_distance ??= airlock_pump_distance_limit;
        turf next_turf = origin;
        float limit = max(1, Dm.num(max_distance));
        while (Dm.truthy(limit))
        {
            limit--;
            next_turf = get_step(next_turf, Dm.num(direction));
            obj_machinery_door_airlock found_airlock = Dm.locate<obj_machinery_door_airlock>(next_turf);
            if (Dm.truthy(is_valid_airlock(found_airlock)))
                return found_airlock.loc;
            if (!Dm.truthy(next_turf.can_atmos_pass))
                return next_turf;
        }
        return null;
    }

    ///Find airlocks adjacent to the central one, lined up along the provided directions
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:534
    public virtual object get_adjacent_airlocks(object origin_turf = null, object directions = null)
    {
        DmList airlocks = Dm.list();

        obj_machinery_door_airlock origin_airlock = Dm.locate<obj_machinery_door_airlock>(origin_turf);
        if (Dm.truthy(is_valid_airlock(origin_airlock)))
            airlocks.Add(origin_airlock);

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(direction, directions)))
                continue;
            turf next_turf = origin_turf as turf;
            float limit = max(0, airlock_group_distance_limit);
            while (Dm.truthy(limit))
            {
                limit--;
                next_turf = get_step(next_turf, Dm.num(direction));
                obj_machinery_door_airlock found_airlock = Dm.locate<obj_machinery_door_airlock>(next_turf);
                if (Dm.truthy(is_valid_airlock(found_airlock)))
                    airlocks.Add(found_airlock);
                else
                    limit = 0;
            }
        }

        return airlocks;
    }

    ///Whether the passed airlock can be linked with
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:559
    public virtual object is_valid_airlock(obj_machinery_door_airlock airlock = null)
    {
        if (airlock == null)
            return FALSE;
        if (airlock.cycle_pump != null)
            return FALSE; // Already linked
        if (Dm.truthy(can_unwrench) && !istype(airlock, valid_airlock_typepath))
            return FALSE; // Invalid airlock type and the pump is not mapspawn
        return TRUE;
    }

    ///Find airlocks and link up with them
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:570
    [SignalHandler]
    public virtual void unlink_airlock(object airlock = null)
    {
        UnregisterSignal(airlock as datum, COMSIG_QDELETING);

        if (Dm.@in(airlock, internal_airlocks))
            internal_airlocks.Remove(airlock);
        if (Dm.@in(airlock, external_airlocks))
            external_airlocks.Remove(airlock);

        if (!Dm.truthy(internal_airlocks.len) || !Dm.truthy(external_airlocks.len))
            break_all_links();
    }

    ///Break the cycling setup
    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:585
    public virtual void break_all_links()
    {
        foreach (obj_machinery_door_airlock airlock in Dm.each_cast<obj_machinery_door_airlock>(Dm.add(internal_airlocks, external_airlocks)))
        {
            UnregisterSignal(airlock, COMSIG_QDELETING);
            airlock.unbolt();
        }

        deltimer(emergency_stop_timer);
        external_airlocks = Dm.list();
        internal_airlocks = Dm.list();
        cycling_set_up = FALSE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:595
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(initialize_directions, direction)))
            return await base.relaymove(user, direction);
        if (Dm.truthy(Dm.bitand(Dm.bitor(NORTH, EAST), direction)))
            user.ventcrawl_layer = clamp(user.ventcrawl_layer + 2, PIPING_LAYER_DEFAULT - 1, PIPING_LAYER_DEFAULT + 1);
        if (Dm.truthy(Dm.bitand(Dm.bitor(SOUTH, WEST), direction)))
            user.ventcrawl_layer = clamp(user.ventcrawl_layer - 2, PIPING_LAYER_DEFAULT - 1, PIPING_LAYER_DEFAULT + 1);
        to_chat(user, I18n.T("You align yourself with the [] output.", user.ventcrawl_layer == 2 ? "1st" : "2nd")); // "[...]\th": BYOND's ordinal suffix of the number before it
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/airlock_pump.dm:604
    public override async DmTask on_set_is_operational(object was_operational = null)
    {
        if (Dm.truthy(was_operational) && !Dm.truthy(is_operational))
        {
            // unbolt all the doors but don't open them
            foreach (obj_machinery_door_airlock airlock in Dm.each_cast<obj_machinery_door_airlock>(Dm.add(internal_airlocks, external_airlocks)))
                airlock.unbolt();
            audible_message(span_notice(I18n.T("[] whirrs as [] lose[] power, disengaging airlock bolts.", this, p_they(), p_s())));
            deltimer(emergency_stop_timer);
            set_on(FALSE);
        }

        else if (!Dm.truthy(was_operational) && Dm.truthy(is_operational))
        {
            // upon regaining power, re-bolt relevant airlocks
            foreach (obj_machinery_door_airlock airlock in Dm.each_cast<obj_machinery_door_airlock>(external_airlocks))
                Waiting.Detach(airlock.secure_close());
            foreach (obj_machinery_door_airlock airlock in Dm.each_cast<obj_machinery_door_airlock>(internal_airlocks))
            {
                if (Dm.truthy(open_airlock_on_cycle))
                    Waiting.Detach(airlock.secure_open());
            }
            audible_message(span_notice(I18n.T("[] whirrs as [] regain[] power, re-engaging airlock bolts.", this, p_they(), p_s())));
        }
    }
}
