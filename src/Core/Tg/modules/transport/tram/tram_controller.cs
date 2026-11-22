// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but the screentips (add_context) and the variable editor's hook (vv_edit_var),
// live-partial.txt. The emissive overlays are lighting (DEVIATION DV12). The arrival jingle's /datum/threed_sound (which re-mixes
// a playing sound's volume as its listeners walk) is not made: the 3D client places every sound at its source itself.

/**
 * Tram specific variant of the generic linear transport controller.
 *
 * Hierarchy
 * The sstransport subsystem manages a list of controllers,
 * A controller manages a list of transport modules (individual tiles) which together make up a transport unit (in this case a tram)
 */
// tg: code/modules/transport/tram/tram_controller.dm:8
public partial class datum_transport_controller_linear_tram : datum_transport_controller_linear
{
    /// controller_status as the bit field it is (tg reads it with &)
    internal int status_bits => (int)controller_status;

    /**
     * Make sure all modules have matching speed limiter vars, pull save data from persistence
     *
     * We track a few stats about it, and keep a small historical record on the
     * information plate inside the tram.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:124
    [DmParams("transport_module")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_tram_speed(tram_max_speed);
        base_internal_movement_delay = internal_movement_delay;
        tram_history = SSpersistence.load_tram_history(specific_transport_id) as DmList;
        datum_tram_mfg_info previous_tram = peek(tram_history) as datum_tram_mfg_info;
        if (previous_tram != null && Dm.truthy(previous_tram.active))
            tram_registration = pop(tram_history) as datum_tram_mfg_info;
        else
            tram_registration = Dm.@new<datum_tram_mfg_info>(specific_transport_id);

        check_starting_landmark();
    }

    // tg: code/modules/transport/tram/tram_controller.dm:157
    public override object Destroy(float? force = null)
    {
        paired_cabinet = null;
        set_status_code(SYSTEM_FAULT, TRUE);
        tram_registration.active = FALSE;
        SSblackbox.record_feedback("amount", "tram_destroyed", 1);
        SSpersistence.save_tram_history(specific_transport_id);
        TramRides.Gone(this); // DEVIATION(DV403): no ride of a destroyed tram is drawn on
        return base.Destroy(force);
    }

    /**
     * Register transport modules to the controller
     *
     * Spreads out searching neighbouring tiles for additional transport modules, to combine into one full tram.
     * We register to every module's signal that it's collided with something, be it mob, structure, etc.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:171
    public override async DmTask add_transport_modules(obj_structure_transport_linear new_transport_module = null)
    {
        await base.add_transport_modules(new_transport_module);
        RegisterSignal(new_transport_module, COMSIG_MOVABLE_BUMP, nameof(gracefully_break));
    }

    /**
     * The mapper should have placed the tram at one of the stations, the controller will search for a landmark within
     * its control area and set it as its idle position.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:179
    public override void check_for_landmarks(obj_structure_transport_linear new_transport_module = null)
    {
        base.check_for_landmarks(new_transport_module);
        foreach (turf platform_loc in Dm.each<turf>(new_transport_module.locs))
        {
            obj_effect_landmark_transport_nav_beacon_tram_platform initial_destination = Dm.locate<obj_effect_landmark_transport_nav_beacon_tram_platform>(platform_loc);
            obj_effect_landmark_transport_nav_beacon_tram_nav beacon = Dm.locate<obj_effect_landmark_transport_nav_beacon_tram_nav>(platform_loc);

            if (initial_destination != null)
            {
                idle_platform = initial_destination;
                destination_platform = initial_destination;
            }

            if (beacon != null)
                nav_beacon = beacon;
        }
    }

    /**
     * Verify tram is in a valid starting location, start the subsystem.
     *
     * Throw an error if someone mapped a tram with no landmarks available for it to register.
     * The processing subsystem starts off because not all maps have elevators/transports.
     * Now that the tram is aware of its surroundings, we start the subsystem.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:198
    public virtual object check_starting_landmark()
    {
        if (idle_platform == null || nav_beacon == null)
            throw Dm.CRASH("a tram lift_master was initialized without the required landmarks to give it direction!", "code/modules/transport/tram/tram_controller.dm", 200);

        SStransport.can_fire = TRUE;

        return TRUE;
    }

    /**
     * The tram explodes if it hits a few types of objects.
     *
     * Signal for when the tram runs into a field of which it cannot go through.
     * Stops the train's travel fully, sends a message, and destroys the train.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:214
    public virtual void gracefully_break(atom bumped_atom = null)
    {
        travel_remaining = 0;
        bumped_atom.visible_message(span_userdanger(I18n.T("[] crashes into the field violently!", bumped_atom)));
        foreach (obj_structure_transport_linear_tram transport_module in Dm.each<obj_structure_transport_linear_tram>(transport_modules))
        {
            transport_module.set_travelling(FALSE);
            foreach (object explosive_target in Dm.each(transport_module.transport_contents))
            {
                if (iseffect(explosive_target))
                    continue;

                if (isliving(explosive_target))
                    Waiting.Detach(explosion(explosive_target as atom, devastation_range: rand(0, 1), heavy_impact_range: 2, light_impact_range: 3)); //50% chance of gib
                else if (prob(9))
                    Waiting.Detach(explosion(explosive_target as atom, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3));

                Waiting.Detach(explosion(transport_module, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3));
                qdel(transport_module);
            }

            send_transport_status_update();
        }
        TramRides.End(this); // DEVIATION(DV403): the ride stops here
    }

    /**
     * Calculate the journey details to the requested platform
     *
     * These will eventually be passed to the transport modules as args telling them where to move.
     * We do some sanity checking in case of discrepencany between where the subsystem thinks the
     * tram is and where the tram actually is. (For example, moving the landmarks after round start.)
     */
    // tg: code/modules/transport/tram/tram_controller.dm:245
    public virtual object calculate_route(obj_effect_landmark_transport_nav_beacon_tram destination = null)
    {
        if (destination == idle_platform)
            return FALSE;

        destination_platform = destination as obj_effect_landmark_transport_nav_beacon_tram_platform;
        travel_direction = Dm.num(get_dir(nav_beacon, destination_platform));
        travel_remaining = Dm.num(get_dist(nav_beacon, destination_platform));
        travel_trip_length = travel_remaining;
        log_transport(Dm.text("TC: [] trip calculation: src: [], [], [] dst: [] [], [], [] = Dir [] Dist [].", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z, destination_platform, destination_platform.x, destination_platform.y, destination_platform.z, travel_direction, travel_remaining));
        return TRUE;
    }

    /**
     * Handles moving the tram
     *
     * Called by the subsystem, the controller tells the individual tram parts where to actually go and has extra safety checks
     * incase multiple inputs get through, preventing conflicting directions and the tram literally ripping itself apart.
     * All of the actual movement is handled by SStransport.
     *
     * If we're this far all the PRE_DEPARTURE checks should have passed, so we leave the PRE_DEPARTURE status and actually move.
     * We send a signal to anything registered that cares about the physical movement of the tram.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:270
    public virtual void dispatch_transport(obj_effect_landmark_transport_nav_beacon_tram destination_platform = null)
    {
        log_transport(Dm.text("TC: [] starting departure.", specific_transport_id));
        set_status_code(PRE_DEPARTURE, FALSE);
        if ((status_bits & EMERGENCY_STOP) != 0)
        {
            set_status_code(EMERGENCY_STOP, FALSE);
            playsound(paired_cabinet, Dm.resource("sound/machines/synth/synth_yes.ogg"), 40, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
            Waiting.Detach(paired_cabinet.say(I18n.T("Controller reset.")));
        }
        nav_beacon.tram_loop.start();
        foreach (obj_structure_transport_linear_tram transport_module in Dm.each<obj_structure_transport_linear_tram>(transport_modules)) //only thing everyone needs to know is the new location.
        {
            if (Dm.truthy(transport_module.travelling)) //wee woo wee woo there was a double action queued. damn multi tile structs
                return; //we don't care to undo cover_locked controls, though, as that will resolve itself
            if (Dm.eq(malf_active, TRANSPORT_LOCAL_WARNING))
            {
                if (Dm.truthy(transport_module.check_for_humans()))
                {
                    throw_chance *= 1.75f;
                    malf_active = TRANSPORT_LOCAL_FAULT;
                    _addtimer(Dm.@new<datum_callback>(this, nameof(announce_malf_event)), 1 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 285);
                }
            }
            transport_module.verify_transport_contents();
            transport_module.internal_movement_delay = Dm.num(internal_movement_delay);
            transport_module.glide_size_override = DELAY_TO_GLIDE_SIZE(Dm.num(internal_movement_delay));
            transport_module.set_travelling(TRUE);
        }

        // DEVIATION(DV403): the first step waits for the trip's announcement to reach the clients (tg: the movement delay)
        scheduled_move = world.time + Math.Max(Dm.num(internal_movement_delay), TramRides.LeadDeciseconds);

        START_PROCESSING(SStransport, this);
        TramRides.Start(this); // DEVIATION(DV403): the clients are told the whole ride now, so they draw it moving evenly
    }

    /**
     * Tram processing loop
     *
     * Moves the tram to its set destination.
     * When it arrives at its destination perform callback to the post-arrival procs like controls and lights.
     * We update the odometer and kill the process until we need to move again.
     *
     * If the status is EMERGENCY_STOP the tram should immediately come to a stop regardless of the travel_remaining.
     * Some extra things happen in an emergency stop (throwing the passengers) and when reset will run a
     * recovery procedure to head to the nearest platform and sync logical and physical location data
     * (idle_platform and nav_beacon) once the issue is resolved.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:306
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (paired_cabinet == null)
            set_status_code(SYSTEM_FAULT, TRUE);

        if ((status_bits & SYSTEM_FAULT) != 0 || (status_bits & EMERGENCY_STOP) != 0)
        {
            halt_and_catch_fire();
            return PROCESS_KILL;
        }

        if (!Dm.truthy(travel_remaining))
        {
            if (!Dm.truthy(controller_operational) || Dm.eq(malf_active, TRANSPORT_LOCAL_FAULT))
                degraded_stop();
            else
                normal_stop();

            return PROCESS_KILL;
        }
        else if (world.time >= scheduled_move)
        {
            float start_time = TICK_USAGE();
            travel_remaining--;

            Waiting.NoSleep(move_transport_horizontally(travel_direction));
            TramRides.Stepped(this); // DEVIATION(DV403): a step off the announced schedule is announced again

            float duration = (TICK_USAGE_REAL() - start_time) * Dm.num(world.tick_lag); // TICK_USAGE_TO_MS(start_time)
            current_load = duration;
            current_speed = Dm.num(((obj_structure_transport_linear)transport_modules[1]).glide_size);
            if (Dm.truthy(recovery_mode))
            {
                if (duration <= (SStransport.max_time / 2))
                    recovery_clear_count++;
                else
                    recovery_clear_count = 0;

                if (recovery_clear_count >= SStransport.max_cheap_moves)
                {
                    set_tram_speed(tram_max_speed);
                    recovery_mode = FALSE;
                    recovery_clear_count = 0;
                    log_transport(Dm.text("TC: [] removing speed limiter, performance issue resolved. Last tick was []ms.", specific_transport_id, duration));
                }
            }
            else if (duration > SStransport.max_time)
            {
                recovery_activate_count++;
                if (recovery_activate_count >= SStransport.max_exceeding_moves)
                {
                    message_admins(Dm.text("The tram at [] is taking [] ms which is more than [] ms per movement for [] ticks. Reducing its movement speed until it recovers. If this continues to be a problem you can reset the tram contents to its original state, and clear added objects on the Debug tab.", "(the tram)", duration, SStransport.max_time, recovery_activate_count));
                    log_transport(Dm.text("TC: [] activating speed limiter due to poor performance.  Last tick was []ms.", specific_transport_id, duration));
                    base_internal_movement_delay = internal_movement_delay; // update base in case it's been edited
                    internal_movement_delay = Dm.num(base_internal_movement_delay) * 2; //halves its speed
                    recovery_mode = TRUE;
                    recovery_activate_count = 0;
                }
            }
            else
            {
                recovery_activate_count = Dm.max(recovery_activate_count - 1, 0);
            }

            if (travel_remaining < 39 && announce_cooldown <= world.time) // COOLDOWN_FINISHED(src, announce_cooldown)
            {
                make_announcement(I18n.T("The next station is: [].", destination_platform));
                announce_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START(src, announce_cooldown, 4 SECONDS)
            }

            scheduled_move = world.time + Dm.num(internal_movement_delay);
        }
        return null;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:358
    public virtual void set_tram_speed(object new_speed = null)
    {
        internal_movement_delay = round(clamp(50 / Dm.num(new_speed), 0.5f, 5), 0.1f);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:361
    public virtual void make_announcement(object broadcast = null)
    {
        if (Dm.truthy(SStts.tts_enabled))
            nav_beacon.voice = SStts.tram_voice;
        else
            playsound(nav_beacon, Dm.resource("sound/machines/tram/info_chime.ogg"), 100, vary: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_exponent: 1.4f);

        Waiting.Detach(nav_beacon.say(broadcast));
    }

    /**
     * Tram stops normally, performs post-trip actions and updates the tram registration.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:372
    public virtual void normal_stop()
    {
        TramRides.End(this); // DEVIATION(DV403): arrived
        cycle_doors(CYCLE_OPEN);
        log_transport(Dm.text("TC: [] trip completed. Info: nav_pos ([], [], []) idle_pos ([], [], []).", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z, destination_platform.x, destination_platform.y, destination_platform.z));
        nav_beacon.tram_loop.stop();
        _addtimer(Dm.@new<datum_callback>(this, nameof(unlock_controls)), 2 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 376);
        _addtimer(Dm.@new<datum_callback>(this, nameof(platform_arrival_jingle)), 2.5f * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 377);
        if ((status_bits & SYSTEM_FAULT) != 0 && nav_beacon.loc == destination_platform.loc) //position matches between controller and tram, we're back on track
        {
            set_status_code(SYSTEM_FAULT, FALSE);
            playsound(paired_cabinet, Dm.resource("sound/machines/synth/synth_yes.ogg"), 40, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
            Waiting.Detach(paired_cabinet.say(I18n.T("Controller reset.")));
            log_transport(Dm.text("TC: [] position data successfully reset.", specific_transport_id));
        }
        idle_platform = destination_platform;

        tram_registration.distance_travelled += (travel_trip_length - travel_remaining);
        travel_trip_length = 0;
        current_speed = 0;
        current_load = 0;
        set_tram_speed(tram_max_speed);
    }

    /**
     * Tram comes to an in-station degraded stop, throwing the players. Caused by power loss or tram malfunction event.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:396
    public virtual void degraded_stop()
    {
        TramRides.End(this); // DEVIATION(DV403)
        crash_fx();
        log_transport(Dm.text("TC: [] trip completed with a degraded status. Info: TS-[] nav_pos ([], [], []) idle_pos ([], [], []).", specific_transport_id, controller_status, nav_beacon.x, nav_beacon.y, nav_beacon.z, destination_platform.x, destination_platform.y, destination_platform.z));
        nav_beacon.tram_loop.stop();
        _addtimer(Dm.@new<datum_callback>(this, nameof(unlock_controls)), 4 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 400);
        if ((status_bits & SYSTEM_FAULT) != 0)
        {
            set_status_code(SYSTEM_FAULT, FALSE);
            playsound(paired_cabinet, Dm.resource("sound/machines/synth/synth_yes.ogg"), 40, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
            Waiting.Detach(paired_cabinet.say(I18n.T("Controller reset.")));
            log_transport(Dm.text("TC: [] position data successfully reset. ", specific_transport_id));
        }
        if (Dm.eq(malf_active, TRANSPORT_LOCAL_FAULT))
        {
            set_status_code(SYSTEM_FAULT, TRUE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(cycle_doors), CYCLE_OPEN), 2 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 408);
            malf_active = TRANSPORT_SYSTEM_NORMAL;
            throw_chance = Dm.num(Dm.initial(this, (datum_transport_controller_linear p) => p.throw_chance));
            playsound(paired_cabinet, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 60, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
            Waiting.Detach(paired_cabinet.say(I18n.T("Controller error. Please contact your engineering department.")));
        }
        idle_platform = destination_platform;
        tram_registration.distance_travelled += (travel_trip_length - travel_remaining);
        travel_trip_length = 0;
        current_speed = 0;
        current_load = 0;
        set_tram_speed(tram_max_speed);
        float throw_direction = travel_direction;
        foreach (obj_structure_transport_linear_tram module in Dm.each_cast<obj_structure_transport_linear_tram>(transport_modules))
            Waiting.Detach(module.estop_throw(throw_direction));
    }

    /**
     * Tram comes to an emergency stop without completing its trip. Caused by emergency stop button or some catastrophic tram failure.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:426
    public virtual void halt_and_catch_fire()
    {
        TramRides.End(this); // DEVIATION(DV403)
        if ((status_bits & SYSTEM_FAULT) != 0)
        {
            if (paired_cabinet != null)
            {
                playsound(paired_cabinet, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 60, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
                Waiting.Detach(paired_cabinet.say(I18n.T("Controller error. Please contact your engineering department.")));
            }
            log_transport(Dm.text("TC: [] Transport Controller failed!", specific_transport_id));
        }

        if (Dm.truthy(travel_remaining))
        {
            travel_remaining = 0;
            crash_fx();
            float throw_direction = travel_direction;
            foreach (obj_structure_transport_linear_tram module in Dm.each_cast<obj_structure_transport_linear_tram>(transport_modules))
                Waiting.Detach(module.estop_throw(throw_direction));
        }
        nav_beacon.tram_loop.stop();
        _addtimer(Dm.@new<datum_callback>(this, nameof(unlock_controls)), 4 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 442);
        _addtimer(Dm.@new<datum_callback>(this, nameof(cycle_doors), CYCLE_OPEN), 2 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 443);
        idle_platform = null;
        log_transport(Dm.text("TC: [] Transport Controller needs new position data from the tram.", specific_transport_id));
        tram_registration.distance_travelled += (travel_trip_length - travel_remaining);
        travel_trip_length = 0;
        current_speed = 0;
        current_load = 0;
    }

    /**
     * Performs a reset of the tram's position data by finding a predetermined reference landmark, then driving to it.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:454
    public virtual void reset_position()
    {
        malf_active = TRANSPORT_SYSTEM_NORMAL;
        if (idle_platform != null)
        {
            if (get_turf(idle_platform) == get_turf(nav_beacon))
            {
                set_status_code(SYSTEM_FAULT, FALSE);
                set_status_code(EMERGENCY_STOP, FALSE);
                playsound(paired_cabinet, Dm.resource("sound/machines/synth/synth_yes.ogg"), 40, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
                Waiting.Detach(paired_cabinet.say(I18n.T("Controller reset.")));
                log_transport(Dm.text("TC: [] Transport Controller reset was requested, but the tram nav data seems correct. Info: nav_pos ([], [], []) idle_pos ([], [], []).", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z, idle_platform.x, idle_platform.y, idle_platform.z));
                return;
            }
        }

        log_transport(Dm.text("TC: [] performing Transport Controller reset. Locating closest reset beacon to ([], [], [])", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z));
        object tram_velocity_sign;
        if (Dm.truthy(Dm.bitand(travel_direction, Dm.bitor(NORTH, SOUTH))))
            tram_velocity_sign = Dm.truthy(Dm.bitand(travel_direction, NORTH)) ? OUTBOUND : INBOUND;
        else
            tram_velocity_sign = Dm.truthy(Dm.bitand(travel_direction, EAST)) ? OUTBOUND : INBOUND;

        obj_effect_landmark_transport_nav_beacon_tram reset_beacon = closest_nav_in_travel_dir(nav_beacon, tram_velocity_sign, specific_transport_id) as obj_effect_landmark_transport_nav_beacon_tram;

        if (reset_beacon == null)
        {
            playsound(paired_cabinet, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 60, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
            Waiting.Detach(paired_cabinet.say(I18n.T("Controller reset failed. Contact manufacturer."))); // If you screwed up the tram this bad, I don't even
            log_transport(Dm.text("TC: [] non-recoverable error! Tram is at ([], [], [] []) and can't find a reset beacon.", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z, Dm.truthy(tram_velocity_sign) ? "OUTBOUND" : "INBOUND"));
            message_admins(Dm.text("Tram ID [] is in a non-recoverable error state at ([], [], []). If it's causing problems, delete the controller datum from the 'Reset Tram' proc in the Debug tab.", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z));
            return;
        }

        travel_direction = Dm.num(get_dir(nav_beacon, reset_beacon));
        travel_remaining = Dm.num(get_dist(nav_beacon, reset_beacon));
        travel_trip_length = travel_remaining;
        destination_platform = reset_beacon as obj_effect_landmark_transport_nav_beacon_tram_platform;
        internal_movement_delay = 1.5f;
        playsound(paired_cabinet, Dm.resource("sound/machines/ping.ogg"), 40, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        Waiting.Detach(paired_cabinet.say(I18n.T("Peforming controller reset... Navigating to reset point.")));
        log_transport(Dm.text("TC: [] trip calculation: src: [], [], [] dst: [] [], [], [] = Dir [] Dist [].", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z, destination_platform, reset_beacon.x, reset_beacon.y, reset_beacon.z, travel_direction, travel_remaining));
        cycle_doors(CYCLE_CLOSED);
        set_active(TRUE);
        set_status_code(CONTROLS_LOCKED, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(dispatch_transport), reset_beacon), 3 * SECONDS, file: "code/modules/transport/tram/tram_controller.dm", line: 497);
        log_transport(Dm.text("TC: [] trying to reset at [].", specific_transport_id, destination_platform));
    }

    // tg: code/modules/transport/tram/tram_controller.dm:500
    public virtual void estop()
    {
        playsound(paired_cabinet, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 60, vary: FALSE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        Waiting.Detach(paired_cabinet.say(I18n.T("Emergency stop activated!")));
        set_status_code(EMERGENCY_STOP, TRUE);
        log_transport(Dm.text("TC: [] requested emergency stop.", specific_transport_id));
    }

    /**
     * Tram crash sound and visuals
     */
    // tg: code/modules/transport/tram/tram_controller.dm:509
    public virtual void crash_fx()
    {
        playsound(source: nav_beacon, soundin: Dm.resource("sound/vehicles/car_crash.ogg"), vol: 100, vary: FALSE, falloff_distance: DEFAULT_TRAM_LENGTH);
        nav_beacon.audible_message(span_userdanger(I18n.T("You hear metal grinding as the tram comes to a sudden, complete stop!")));
        foreach (mob_living tram_passenger in Dm.each<mob_living>(range(DEFAULT_TRAM_LENGTH - 2, nav_beacon)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(tram_passenger))
                continue;
            shake_camera(M: tram_passenger, duration: 0.2f * SECONDS, strength: 3);
        }
    }

    /**
     * Handles unlocking the tram controls for use after moving
     *
     * More safety checks to make sure the tram has actually docked properly
     * at a location before users are allowed to interact with the tram console again.
     * Tram finds its location at this point before fully unlocking controls to the user.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:525
    public virtual void unlock_controls()
    {
        controls_lock(FALSE);
        foreach (obj_structure_transport_linear_tram transport_module in Dm.each_cast<obj_structure_transport_linear_tram>(transport_modules)) //only thing everyone needs to know is the new location.
            transport_module.set_travelling(FALSE);
        set_active(FALSE);
    }

    /**
     * Sets the active status for the controller and sends a signal to listeners.
     *
     * The main signal used by most components, it has the active status, the bitfield of the controller's status, its direction, and set destination.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:539
    public virtual void set_active(object new_status = null)
    {
        if (Dm.eq(controller_active, new_status))
            return;

        controller_active = Dm.num(new_status);
        send_transport_status_update();
        log_transport(Dm.text("TC: [] controller state [].", specific_transport_id, Dm.truthy(controller_active) ? "READY > PROCESSING" : "PROCESSING > READY"));
    }

    /**
     * Sets the controller status bitfield
     *
     * This status var is used by various components like lights, crossing signals, signs
     * Sent via signal the listening components will perform required actions based on
     * the status codes.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:558
    public virtual void set_status_code(object code = null, object value = null)
    {
        if (!Dm.eq(code, DOORS_READY))
            log_transport(Dm.text("TC: [] status change [][].", specific_transport_id, Dm.truthy(value) ? "+" : "-", code));

        if (Dm.truthy(value))
            controller_status = Dm.num(Dm.bitor(controller_status, code));
        else
            controller_status = Dm.num(Dm.bitand(controller_status, ~(int)Dm.num(code)));
        send_transport_status_update();
    }

    // tg: code/modules/transport/tram/tram_controller.dm:567
    public virtual void send_transport_status_update()
    {
        SStransport.SEND_SIGNAL(COMSIG_TRANSPORT_UPDATED, Dm.list(SStransport, this, controller_active, controller_status, travel_direction, destination_platform));
    }

    /**
     * Part of the pre-departure list, checks the status of the doors on the tram
     *
     * Checks if all doors are closed, and updates the status code accordingly.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:577
    public virtual void update_status()
    {
        foreach (obj_machinery_door_airlock_tram door in Dm.each<obj_machinery_door_airlock_tram>(SStransport.doors))
        {
            if (!Dm.eq(door.transport_linked_id, specific_transport_id))
                continue;
            if (Dm.truthy(door.crushing_in_progress))
            {
                log_transport(Dm.text("TC: [] door [] failed crush status check.", specific_transport_id, door.id_tag));
                set_status_code(DOORS_READY, FALSE);
                return;
            }
        }

        set_status_code(DOORS_READY, TRUE);
    }

    /**
     * Cycle all the doors on the tram.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:591
    public virtual void cycle_doors(object door_status = null, object rapid = null)
    {
        if (Dm.eq(door_status, CYCLE_OPEN))
        {
            foreach (obj_machinery_door_airlock_tram door in Dm.each<obj_machinery_door_airlock_tram>(SStransport.doors))
            {
                if (Dm.eq(door.transport_linked_id, specific_transport_id))
                    Waiting.Detach(door.open(rapid == null ? null : Dm.num(rapid))); // INVOKE_ASYNC
            }
        }
        else if (Dm.eq(door_status, CYCLE_CLOSED))
        {
            foreach (obj_machinery_door_airlock_tram door in Dm.each<obj_machinery_door_airlock_tram>(SStransport.doors))
            {
                if (Dm.eq(door.transport_linked_id, specific_transport_id))
                    Waiting.Detach(door.close(rapid == null ? null : Dm.num(rapid))); // INVOKE_ASYNC
            }
        }
    }

    // tg: code/modules/transport/tram/tram_controller.dm:603
    public virtual void notify_controller(obj_machinery_transport_tram_controller new_cabinet = null)
    {
        paired_cabinet = new_cabinet;
        RegisterSignal(new_cabinet, COMSIG_MACHINERY_POWER_LOST, nameof(power_lost));
        RegisterSignal(new_cabinet, COMSIG_MACHINERY_POWER_RESTORED, nameof(power_restored));
        RegisterSignal(new_cabinet, COMSIG_QDELETING, nameof(on_cabinet_qdel));
        log_transport(Dm.text("TC: [] is now paired with [].", specific_transport_id, new_cabinet));
        if ((status_bits & SYSTEM_FAULT) != 0)
        {
            set_status_code(SYSTEM_FAULT, FALSE);
            reset_position();
        }
    }

    // tg: code/modules/transport/tram/tram_controller.dm:614
    public virtual void set_home_controller(obj_machinery_transport_tram_controller_tcomms tcomms_unit = null)
    {
        home_controller = tcomms_unit;
        RegisterSignal(tcomms_unit, COMSIG_MACHINERY_POWER_LOST, nameof(home_power_lost));
        RegisterSignal(tcomms_unit, COMSIG_MACHINERY_POWER_RESTORED, nameof(home_power_restored));
        RegisterSignal(tcomms_unit, COMSIG_QDELETING, nameof(on_home_qdel));
        log_transport(Dm.text("TC: [] is now paired with home controller [].", specific_transport_id, tcomms_unit));
        if ((status_bits & COMM_ERROR) != 0)
            set_status_code(COMM_ERROR, FALSE);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:624
    public virtual void on_cabinet_qdel()
    {
        paired_cabinet = null;
        log_transport(Dm.text("TC: [] received QDEL from controller cabinet.", specific_transport_id));
        set_status_code(SYSTEM_FAULT, TRUE);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:629
    public virtual void on_home_qdel()
    {
        home_controller = null;
        log_transport(Dm.text("TC: [] received QDEL from controller cabinet.", specific_transport_id));
        set_status_code(COMM_ERROR, TRUE);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:634
    public virtual void home_power_lost() => set_status_code(COMM_ERROR, TRUE);

    // tg: code/modules/transport/tram/tram_controller.dm:637
    public virtual void home_power_restored() => set_status_code(COMM_ERROR, FALSE);

    /**
     * Tram malfunction random event. Set comm error, requiring engineering or AI intervention.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:643
    public virtual void start_malf_event()
    {
        malf_active = TRANSPORT_LOCAL_WARNING;
        paired_cabinet.update_appearance();
        throw_chance *= 1.25f;
        log_transport(Dm.text("TC: [] starting Tram Malfunction event.", specific_transport_id));
    }

    /**
     * Remove effects of tram malfunction event.
     *
     * If engineers didn't already repair the tram by the end of the event,
     * automagically reset it remotely.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:655
    public virtual void end_malf_event()
    {
        if (!Dm.truthy(malf_active))
            return;
        malf_active = TRANSPORT_SYSTEM_NORMAL;
        paired_cabinet.update_appearance();
        throw_chance = Dm.num(Dm.initial(this, (datum_transport_controller_linear p) => p.throw_chance));
        log_transport(Dm.text("TC: [] ending Tram Malfunction event.", specific_transport_id));
    }

    // tg: code/modules/transport/tram/tram_controller.dm:663
    public virtual void announce_malf_event()
    {
        priority_announce(I18n.T("Our automated control system has lost contact with the tram's onboard computer. Please stand by, engineering has been dispatched to the tram to perform a reset."), Dm.text("[] Engineering Division", command_name()));
    }

    // tg: code/modules/transport/tram/tram_controller.dm:666
    public virtual void register_collision(float? points = null)
    {
        points ??= 1;
        tram_registration.collisions += points.Value;
        SStransport.SEND_SIGNAL(COMSIG_TRAM_COLLISION, Dm.list(SStransport, SSpersistence.tram_hits_this_round));
    }

    // tg: code/modules/transport/tram/tram_controller.dm:670
    public virtual void power_lost()
    {
        set_operational(FALSE);
        log_transport(Dm.text("TC: [] power lost.", specific_transport_id));
    }

    // tg: code/modules/transport/tram/tram_controller.dm:674
    public virtual void power_restored()
    {
        set_operational(TRUE);
        log_transport(Dm.text("TC: [] power restored.", specific_transport_id));
        cycle_doors(CYCLE_OPEN);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:679
    public virtual void set_operational(object new_value = null)
    {
        if (!Dm.eq(controller_operational, new_value))
            controller_operational = Dm.num(new_value);

        send_transport_status_update();
    }

    /**
     * Returns the closest tram nav beacon to an atom
     *
     * Creates a list of nav beacons in the requested direction
     * and returns the closest to be passed to the industrial_lift
     */
    // tg: code/modules/transport/tram/tram_controller.dm:695
    public virtual object closest_nav_in_travel_dir(atom origin = null, object travel_dir = null, object beacon_type = null)
    {
        if (origin == null || !Dm.truthy(origin.z))
            return FALSE;

        DmList inbound_candidates = Dm.list();
        DmList outbound_candidates = Dm.list();

        foreach (object candidate in Dm.each(SStransport.nav_beacons[beacon_type]))
        {
            if (candidate is not obj_effect_landmark_transport_nav_beacon_tram candidate_beacon)
                continue;
            if (!Dm.eq(candidate_beacon.z, origin.z) || !Dm.eq(candidate_beacon.z, nav_beacon.z))
                continue;

            float nav_dir = Dm.num(nav_beacon.dir);
            if (nav_dir == EAST || nav_dir == WEST)
            {
                if (!Dm.eq(candidate_beacon.y, nav_beacon.y))
                    continue;
                else if (candidate_beacon.x < nav_beacon.x)
                    Dm.append(ref inbound_candidates, candidate_beacon);
                else
                    Dm.append(ref outbound_candidates, candidate_beacon);
            }
            else if (nav_dir == NORTH || nav_dir == SOUTH)
            {
                if (!Dm.eq(candidate_beacon.x, nav_beacon.x))
                    continue;
                else if (candidate_beacon.y < nav_beacon.y)
                    Dm.append(ref inbound_candidates, candidate_beacon);
                else
                    Dm.append(ref outbound_candidates, candidate_beacon);
            }
        }

        if (Dm.eq(travel_dir, INBOUND))
        {
            object selected = get_closest_atom(typeof(obj_effect_landmark_transport_nav_beacon_tram), inbound_candidates, origin);
            if (Dm.truthy(selected))
                return selected;
            stack_trace(Dm.text("No inbound beacon candidate found for []. Cancelling dispatch.", origin), "code/modules/transport/tram/tram_controller.dm", 731);
            return FALSE;
        }
        else if (Dm.eq(travel_dir, OUTBOUND))
        {
            object selected = get_closest_atom(typeof(obj_effect_landmark_transport_nav_beacon_tram), outbound_candidates, origin);
            if (Dm.truthy(selected))
                return selected;
            stack_trace(Dm.text("No outbound beacon candidate found for []. Cancelling dispatch.", origin), "code/modules/transport/tram/tram_controller.dm", 738);
            return FALSE;
        }
        else
        {
            stack_trace(Dm.text("Tram receieved invalid travel direction []. Cancelling dispatch.", travel_dir), "code/modules/transport/tram/tram_controller.dm", 742);
        }

        return FALSE;
    }

    /// Plays the arrival jingle associated with the platform
    // tg: code/modules/transport/tram/tram_controller.dm:747
    public virtual void platform_arrival_jingle()
    {
        object our_channel = SSsounds.random_available_channel();
        sound jingle = Dm.sound(idle_platform.arrival_sound, FALSE, 0, Dm.num(our_channel), 60);
        playsound(idle_platform, jingle, 60, FALSE, extrarange: 7); // tg passes extrarange 0 positionally and 7 by name: the name wins
        // the /datum/threed_sound that tg makes over the hearers is not made (the file header)
    }

    /**
     * Moves the tram when hit by an immovable rod
     *
     * Tells the individual tram parts where to actually go and has an extra safety checks
     * incase multiple inputs get through, preventing conflicting directions and the tram
     * literally ripping itself apart. all of the actual movement is handled by SStramprocess
     */
    // tg: code/modules/transport/tram/tram_controller.dm:782
    public virtual object rod_collision(obj_effect_immovablerod collided_rod = null)
    {
        log_transport(Dm.text("TC: [] hit an immovable rod.", specific_transport_id));
        if (!Dm.truthy(controller_operational))
            return null;
        object rod_velocity_sign;
        // Determine inbound or outbound
        if (Dm.truthy(Dm.bitand(collided_rod.dir, Dm.bitor(NORTH, SOUTH))))
            rod_velocity_sign = Dm.truthy(Dm.bitand(collided_rod.dir, NORTH)) ? OUTBOUND : INBOUND;
        else
            rod_velocity_sign = Dm.truthy(Dm.bitand(collided_rod.dir, EAST)) ? OUTBOUND : INBOUND;

        obj_effect_landmark_transport_nav_beacon_tram_nav push_destination = closest_nav_in_travel_dir(origin: nav_beacon, travel_dir: rod_velocity_sign, beacon_type: IMMOVABLE_ROD_DESTINATIONS) as obj_effect_landmark_transport_nav_beacon_tram_nav;
        if (push_destination == null)
            return null;
        travel_direction = Dm.num(get_dir(nav_beacon, push_destination));
        travel_remaining = Dm.num(get_dist(nav_beacon, push_destination));
        travel_trip_length = travel_remaining;
        destination_platform = null; // tg assigns the nav beacon to its platform-typed var: the push has no platform to stop at
        log_transport(Dm.text("TC: [] collided at ([], [], []) towards [] ([], [], []) Dir [] Dist [].", specific_transport_id, nav_beacon.x, nav_beacon.y, nav_beacon.z, push_destination, push_destination.x, push_destination.y, push_destination.z, travel_direction, travel_remaining));
        // Don't bother processing crossing signals, where this tram's going there are no signals
        priority_announce(I18n.T("In a turn of rather peculiar events, it appears that [] has struck an immovable rod. (Don't ask us where it came from.) This has led to a station brakes failure on one of the tram platforms.\n\nOur diligent team of engineers have been informed and they're rushing over - although not quite at the speed of our recently flying tram.\n\nSo while we all look in awe at the universe's mysterious sense of humour, please stand clear of the tracks and remember to stand behind the yellow line.", GLOB.station_name), "Braking News");
        set_active(TRUE);
        set_status_code(CONTROLS_LOCKED, TRUE);
        dispatch_transport(destination_platform: push_destination);
        return push_destination;
    }
}

// tg: code/modules/transport/tram/tram_controller.dm:73
public partial class datum_tram_mfg_info : datum
{
    /**
     * Assign registration details to a new tram.
     *
     * When a new tram is created, we give it a builder's plate with the date it was created.
     * We track a few stats about it, and keep a small historical record on the
     * information plate inside the tram.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:93
    [DmParams("specific_transport_id")]
    public override async DmTask New(params object[] __extra)
    {
        object specific_transport_id = Dm.arg<object>(__extra, 0);
        if (Dm.truthy(GLOB.round_id))
            serial_number = Dm.text("LT306TG[]", add_leading(GLOB.round_id, 6, "0"));
        else
            serial_number = Dm.text("LT306TG[]", rand(000000, 999999));

        mfg_date = Dm.text("[]-[]", CURRENT_STATION_YEAR, time2text(Dm.num(world.timeofday), "MM-DD", NO_TIMEZONE));
        install_location = specific_transport_id;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:102
    public virtual void load_from_json(DmList json_data = null)
    {
        serial_number = json_data["serial_number"];
        active = Dm.num(json_data["active"]);
        mfg_date = json_data["mfg_date"];
        install_location = json_data["install_location"];
        distance_travelled = Dm.num(json_data["distance_travelled"]);
        collisions = Dm.num(json_data["collisions"]);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:110
    public virtual object export_to_json()
    {
        DmList new_data = Dm.list();
        new_data["serial_number"] = serial_number;
        new_data["active"] = active;
        new_data["mfg_date"] = mfg_date;
        new_data["install_location"] = install_location;
        new_data["distance_travelled"] = distance_travelled;
        new_data["collisions"] = collisions;
        return new_data;
    }
}

/**
 * The physical cabinet on the tram. Acts as the interface between players and the controller datum.
 */
// tg: code/modules/transport/tram/tram_controller.dm:808
public partial class obj_machinery_transport_tram_controller : obj_machinery_transport
{
    // tg: code/modules/transport/tram/tram_controller.dm:837
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WRENCH, TOOL_ACT_SECONDARY));
        return dot;
    }

    /**
     * Mapped or built tram cabinet isn't located on a transport module.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:847
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        SStransport.hello(this, name, id_tag);
        find_controller();
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_controller.dm:853
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        await set_machine_stat(Dm.bitor(machine_stat, BROKEN));
        return await base.atom_break(damage_flag);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:878
    public override object update_current_power_usage()
    {
        return null; // We get power from area rectifiers
    }

    // tg: code/modules/transport/tram/tram_controller.dm:881
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(has_cover))
        {
            Dm.append(ref dot, span_notice(I18n.T("The door appears to be [].", Dm.truthy(cover_locked) ? I18n.T("locked. Swipe an ID card to unlock") : I18n.T("unlocked. Swipe an ID card to lock"))));
            if (Dm.truthy(panel_open))
            {
                Dm.append(ref dot, span_notice(I18n.T("It is secured to the tram wall with []", EXAMINE_HINT("bolts."))));
                Dm.append(ref dot, span_notice(I18n.T("The maintenance panel can be closed with a []", EXAMINE_HINT("screwdriver."))));
            }
            else
            {
                Dm.append(ref dot, span_notice(I18n.T("The maintenance panel can be opened with a []", EXAMINE_HINT("screwdriver."))));
            }
        }

        if (Dm.truthy(cover_open) || !Dm.truthy(has_cover))
        {
            Dm.append(ref dot, span_notice(I18n.T("The [] resets the tram controller if a problem occurs or needs to be restarted.", EXAMINE_HINT("yellow reset button"))));
            Dm.append(ref dot, span_notice(I18n.T("The [] immediately stops the tram, requiring a reset afterwards.", EXAMINE_HINT("red stop button"))));
            Dm.append(ref dot, span_notice(I18n.T("The cabinet can be closed with a []", EXAMINE_HINT("Right-click."))));
        }
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("The cabinet can be opened with a []", EXAMINE_HINT("Left-click."))));
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:900
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(has_cover))
            return NONE;
        if (tool is not obj_item_card_id)
            return NONE;
        if (Dm.truthy(cover_open))
            return ITEM_INTERACT_BLOCKING;
        return Dm.truthy(try_toggle_lock(user, tool as obj_item_card)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:909
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(cover_open) || !Dm.truthy(has_cover))
            return dot;

        if (Dm.truthy(cover_locked))
        {
            obj_item_card_id id_card = (user as mob_living)?.get_idcard(TRUE) as obj_item_card_id;
            if (id_card == null)
            {
                balloon_alert(user, I18n.T("access denied!"));
                return dot;
            }

            try_toggle_lock(user as mob_living, id_card);
            return dot;
        }

        toggle_door();
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:926
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        if (!Dm.truthy(has_cover))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (!Dm.truthy(cover_open))
        {
            obj_item_card_id id_card = (user as mob_living)?.get_idcard(TRUE) as obj_item_card_id;
            if (id_card == null)
            {
                balloon_alert(user, I18n.T("access denied!"));
                return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
            }

            try_toggle_lock(user as mob_living, id_card);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        toggle_door();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:944
    public virtual void toggle_door()
    {
        if (!Dm.truthy(cover_open))
            playsound(loc, Dm.resource("sound/machines/closet/closet_open.ogg"), 35, TRUE, -3);
        else
            playsound(loc, Dm.resource("sound/machines/closet/closet_close.ogg"), 50, TRUE, -3);
        cover_open = Dm.num(!Dm.truthy(cover_open));
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_controller.dm:952
    public virtual object try_toggle_lock(mob_living user = null, obj_item_card id_card = null, object @params = null)
    {
        if (id_card == null)
            id_card = user.get_idcard(TRUE) as obj_item_card;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("access controller damaged!"));
            return FALSE;
        }

        if (Dm.truthy(check_access(id_card)))
        {
            cover_locked = Dm.num(!Dm.truthy(cover_locked));
            balloon_alert(user, I18n.T("controls []", Dm.truthy(cover_locked) ? I18n.T("locked") : I18n.T("unlocked")));
            update_appearance();
            return TRUE;
        }

        balloon_alert(user, I18n.T("access denied!"));
        return FALSE;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:968
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act_secondary(user, tool);
        if (!Dm.truthy(has_cover))
            return dot;

        if (Dm.truthy(panel_open) && Dm.truthy(cover_open))
        {
            balloon_alert(user, I18n.T("unsecuring..."));
            tool.play_tool_sound(this);
            if (!Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
                return dot;
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, vary: TRUE);
            balloon_alert(user, I18n.T("unsecured"));
            await deconstruct(TRUE);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:983
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act_secondary(user, tool);
        if (!Dm.truthy(cover_open))
            return dot;

        tool.play_tool_sound(this);
        panel_open = Dm.num(!Dm.truthy(panel_open));
        balloon_alert(user, Dm.truthy(panel_open) ? I18n.T("mounting bolts exposed") : I18n.T("mounting bolts hidden"));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:993
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        atom drop_location = await find_obstruction_free_location(1, this) as atom;

        if (Dm.truthy(disassembled))
        {
            Dm.@new<obj_item_wallframe_tram>(drop_location);
        }
        else
        {
            Dm.@new<obj_item_stack_sheet_mineral_titanium>(drop_location, 2);
            Dm.@new<obj_item_stack_sheet_iron>(drop_location);
        }
        return null;
    }

    /**
     * Update the blinky lights based on the controller status, allowing to quickly check without opening up the cabinet.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:1005
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(has_cover))
        {
            if (!Dm.truthy(cover_open))
            {
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-closed", base_icon_state)));
                if (Dm.truthy(cover_locked))
                    Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-locked", base_icon_state)));
            }
            else
            {
                mutable_appearance controller_door = mutable_appearance(icon, Dm.text("[]-open", base_icon_state)) as mutable_appearance;
                controller_door.pixel_w = -3;
                Dm.append(ref dot, controller_door);
            }
        }

        // the emissive_appearance copies of each light are lighting (DV12)
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-estop", base_icon_state)));
            return dot;
        }

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-power", base_icon_state)));

        if (controller_datum == null)
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-fatal", base_icon_state)));
            return dot;
        }

        if ((controller_datum.status_bits & EMERGENCY_STOP) != 0)
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-estop", base_icon_state)));
            return dot;
        }

        if ((controller_datum.status_bits & SYSTEM_FAULT) != 0 || !Dm.eq(controller_datum.malf_active, TRANSPORT_SYSTEM_NORMAL))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-fault", base_icon_state)));
            return dot;
        }

        if ((controller_datum.status_bits & DOORS_READY) == 0)
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-doors", base_icon_state)));

        if (Dm.truthy(controller_datum.controller_active))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-active", base_icon_state)));

        if ((controller_datum.status_bits & COMM_ERROR) != 0)
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-comms", base_icon_state)));
        else
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-normal", base_icon_state)));
        return dot;
    }

    /**
     * Find the controller associated with the transport module the cabinet is sitting on.
     */
    // tg: code/modules/transport/tram/tram_controller.dm:1061
    public virtual void find_controller()
    {
        obj_structure_transport_linear_tram tram_structure = locate_transport<obj_structure_transport_linear_tram>(loc);
        if (tram_structure == null)
            return;

        controller_datum = tram_structure.transport_controller_datum as datum_transport_controller_linear_tram;
        if (controller_datum == null)
            return;

        controller_datum.notify_controller(this);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(sync_controller));
    }

    /**
     * Since the machinery obj is a dumb terminal for the controller datum, sync the display with the status bitfield of the tram
     */
    // tg: code/modules/transport/tram/tram_controller.dm:1090
    public virtual void sync_controller(datum source = null, object controller = null, object controller_status = null, object travel_direction = null, object destination_platform = null)
    {
        Waiting.NoSleep(use_energy(active_power_usage));
        if (controller != controller_datum)
            return;
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1096
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("already fried!"));
            return FALSE;
        }
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        cover_locked = FALSE;
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, I18n.T("access controller shorted"));
        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1107
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (HAS_SILICON_ACCESS(user) && ((controller_datum.status_bits & SYSTEM_FAULT) != 0 || (controller_datum.status_bits & COMM_ERROR) != 0 || !Dm.truthy(is_operational)))
        {
            to_chat(user, span_warning(I18n.T("An error code flashes: Communications fault! The [] is not responding to remote inputs!", this)));
            return UI_CLOSE;
        }

        return await base.ui_status(user, state);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1114
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);

        if (!Dm.truthy(cover_open) && !HAS_SILICON_ACCESS(user) && !isobserver(user))
            return null;

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return null;

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "TramController");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1128
    public override async DmTask<object> ui_data(mob user = null)
    {
        int status = controller_datum.status_bits;
        DmList data = Dm.list(
            Dm.kv("transportId", controller_datum.specific_transport_id),
            Dm.kv("controllerActive", controller_datum.controller_active),
            Dm.kv("controllerOperational", controller_datum.controller_operational),
            Dm.kv("travelDirection", controller_datum.travel_direction),
            Dm.kv("destinationPlatform", controller_datum.destination_platform),
            Dm.kv("idlePlatform", controller_datum.idle_platform),
            Dm.kv("recoveryMode", controller_datum.recovery_mode),
            Dm.kv("currentSpeed", controller_datum.current_speed),
            Dm.kv("currentLoad", controller_datum.current_load),
            Dm.kv("statusSF", (status & SYSTEM_FAULT) != 0 || !Dm.eq(controller_datum.malf_active, TRANSPORT_SYSTEM_NORMAL) ? TRUE : (status & SYSTEM_FAULT)),
            Dm.kv("statusCE", status & COMM_ERROR),
            Dm.kv("statusES", status & EMERGENCY_STOP),
            Dm.kv("statusPD", status & PRE_DEPARTURE),
            Dm.kv("statusDR", status & DOORS_READY),
            Dm.kv("statusCL", status & CONTROLS_LOCKED),
            Dm.kv("statusBS", status & BYPASS_SENSORS));

        return data;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1150
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["destinations"] = SStransport.detailed_destination_list(controller_datum.specific_transport_id);

        return data;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1156
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (manual_command_cooldown > world.time) // !COOLDOWN_FINISHED(src, manual_command_cooldown)
            return dot;

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            visible_message(span_warning(I18n.T("The button doesn't appear to do anything, the []'s power failure status is flashing!", this)), vision_distance: COMBAT_MESSAGE_RANGE);
            return dot;
        }

        switch (action as string)
        {
            case "dispatch":
            {
                obj_effect_landmark_transport_nav_beacon_tram_platform destination_platform = null;
                foreach (object candidate in Dm.each(SStransport.nav_beacons[controller_datum.specific_transport_id]))
                {
                    if (candidate is obj_effect_landmark_transport_nav_beacon_tram_platform destination && Dm.eq(destination.name, @params["tripDestination"]))
                    {
                        destination_platform = destination;
                        break;
                    }
                }

                if (destination_platform == null)
                    return FALSE;

                SEND_SIGNAL(COMSIG_TRANSPORT_REQUEST, Dm.list(this, controller_datum.specific_transport_id, destination_platform.platform_code));
                update_appearance();
                break;
            }
            case "estop":
                controller_datum.estop();
                break;
            case "reset":
                controller_datum.reset_position();
                break;
            case "dclose":
                controller_datum.cycle_doors(CYCLE_CLOSED);
                break;
            case "dopen":
                controller_datum.cycle_doors(CYCLE_OPEN);
                break;
            case "togglesensors":
                if ((controller_datum.status_bits & BYPASS_SENSORS) != 0)
                    controller_datum.set_status_code(BYPASS_SENSORS, FALSE);
                else
                    controller_datum.set_status_code(BYPASS_SENSORS, TRUE);
                break;
        }

        manual_command_cooldown = world.time + 2 * SECONDS; // COOLDOWN_START(src, manual_command_cooldown, 2 SECONDS)
        return dot;
    }
}

// tg: code/modules/transport/tram/tram_controller.dm:834
public partial class obj_machinery_transport_tram_controller_hilbert : obj_machinery_transport_tram_controller
{
    // tg: code/modules/transport/tram/tram_controller.dm:1075
    public override void find_controller()
    {
        foreach (datum_transport_controller_linear_tram tram in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(tram.specific_transport_id, configured_transport_id))
            {
                controller_datum = tram;
                break;
            }
        }

        if (controller_datum == null)
            return;

        controller_datum.notify_controller(this);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(sync_controller));
    }
}

/// Controller that sits in the telecoms room
// tg: code/modules/transport/tram/tram_controller.dm:1202
public partial class obj_machinery_transport_tram_controller_tcomms : obj_machinery_transport_tram_controller
{
    /// Handles the machine being affected by an EMP, causing signal failure.
    // tg: code/modules/transport/tram/tram_controller.dm:1214
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(100 / Dm.num(severity)) && !Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            await set_machine_stat(Dm.bitor(machine_stat, EMPED));
            controller_datum.set_status_code(COMM_ERROR, TRUE);
            float duration = (300 * SECONDS) / Dm.num(severity);
            _addtimer(Dm.@new<datum_callback>(this, nameof(de_emp)), rand(duration - 2 * SECONDS, duration + 2 * SECONDS), file: "code/modules/transport/tram/tram_controller.dm", line: 1223);
        }
        return dot;
    }

    /// Handles the machine stopping being affected by an EMP.
    // tg: code/modules/transport/tram/tram_controller.dm:1226
    public virtual async DmTask de_emp()
    {
        await set_machine_stat(Dm.bitand(machine_stat, ~EMPED));
        controller_datum.set_status_code(COMM_ERROR, FALSE);
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1230
    public override void find_controller()
    {
        link_tram();
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1234
    public override void link_tram()
    {
        base.link_tram();
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        controller_datum = tram;
        if (controller_datum == null)
            return;
        controller_datum.set_home_controller(this);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(sync_controller));
    }
}

// tg: code/modules/transport/tram/tram_controller.dm:1242
public partial class obj_item_wallframe_tram : obj_item_wallframe
{
    // tg: code/modules/transport/tram/tram_controller.dm:1250
    public override object find_support_structure(atom structure = null)
    {
        return structure as obj_structure_tram;
    }

    // tg: code/modules/transport/tram/tram_controller.dm:1253
    public override async DmTask<object> try_build(atom on_tram = null, mob user = null)
    {
        turf tram_turf = get_turf(user);
        obj_structure_thermoplastic tram_floor = Dm.locate<obj_structure_thermoplastic>(tram_turf);
        if (tram_floor == null)
        {
            balloon_alert(user, I18n.T("needs tram!"));
            return FALSE;
        }

        return await base.try_build(on_tram, user);
    }
}
