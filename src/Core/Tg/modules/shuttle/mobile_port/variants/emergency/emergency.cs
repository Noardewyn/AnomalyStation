// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/emergency/emergency.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G4 (D272): the whole file but the database's shuttle name (ShuttleDBStuff, M10+),
// the heart poll and the map vote at the departure (after M10). Syndicate N1b (D316): the hijacked shuttle's dock at the syndicate base.

// tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:1
[DmPath("/obj/docking_port/mobile/emergency")]
public partial class obj_docking_port_mobile_emergency : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        setup_shuttle_events();
        RegisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED, nameof(on_security_level_changed));
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:15
    public virtual void on_security_level_changed(datum_controller_subsystem_security_level source = null)
    {
        if (this != SSshuttle.emergency)
            return;
        if (Dm.eq(mode, SHUTTLE_CALL) || Dm.eq(mode, SHUTTLE_RECALL))
            alert_coeff_change(source.current_security_level.shuttle_call_time_mod);
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:22
    public override object canDock(obj_docking_port_stationary stationary_dock = null)
    {
        return SHUTTLE_CAN_DOCK; //If the emergency shuttle can't move, the whole game breaks, so it will force itself to land even if it has to crush a few departments in the process
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:25
    public override object register(object replace = null, object custom = null)
    {
        object dot = base.register(replace, custom);
        SSshuttle.emergency = this;
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:29
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(force))
        {
            // This'll make the shuttle subsystem use the backup shuttle.
            if (this == SSshuttle.emergency)
            {
                // If we're the selected emergency shuttle
                SSshuttle.emergencyDeregister();
            }
        }

        return base.Destroy(force);
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:38
    public override object request(obj_docking_port_stationary destination_port = null, area signal_origin = null, object reason = null, object red_alert = null, object set_coefficient = null)
    {
        if (!isnum(set_coefficient))
            set_coefficient = SSsecurity_level.current_security_level.shuttle_call_time_mod;
        alert_coeff = Dm.num(set_coefficient);
        float call_time = SSshuttle.emergency_call_time * alert_coeff * engine_coeff;
        // The shuttle can not normally be called while "recalling", so
        // if this proc is called, it's via admin fiat
        if (Dm.eq(mode, SHUTTLE_RECALL) || Dm.eq(mode, SHUTTLE_IDLE) || Dm.eq(mode, SHUTTLE_CALL))
        {
            mode = SHUTTLE_CALL;
            setTimer(call_time);
        }
        else
        {
            return null;
        }

        SSshuttle.emergencyCallAmount++;

        if (prob(70))
            SSshuttle.emergency_last_call_loc = signal_origin;
        else
            SSshuttle.emergency_last_call_loc = null;
        priority_announce(
            text: Dm.text("The emergency shuttle has been called. []It will arrive in [] minute\\s.[][][]",
                Dm.truthy(red_alert) ? "Red Alert state confirmed: Dispatching priority shuttle. " : "",
                timeLeft(60 * 10), // 60 SECONDS
                reason,
                SSshuttle.emergency_last_call_loc != null ? "\n\nCall signal traced. Results can be viewed on any communications console." : "",
                Dm.truthy(SSshuttle.admin_emergency_no_recall) ? "\n\nWarning: Shuttle recall subroutines disabled; Recall not possible." : ""),
            title: "Emergency Shuttle Dispatched",
            sound: ANNOUNCER_SHUTTLECALLED,
            sender_override: "Emergency Shuttle Uplink Alert",
            color_override: "orange");
        return null;
    }

    /// This proc will assume you have done all of the necessary checks to see if the shuttle can be recalled, it will always recall when invoked.
    /// signal_origin is an optional parameter that will log where the recall signal was sent from
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:68
    public override object cancel(area signal_origin = null)
    {
        if (!Dm.eq(mode, SHUTTLE_CALL))
            return null;

        invertTimer();
        mode = SHUTTLE_RECALL;

        if (prob(70))
            SSshuttle.emergency_last_call_loc = signal_origin;
        else
            SSshuttle.emergency_last_call_loc = null;

        priority_announce(
            text: Dm.text("The emergency shuttle has been recalled.[]", SSshuttle.emergency_last_call_loc != null ? " Recall signal traced. Results can be viewed on any communications console." : ""),
            title: "Emergency Shuttle Recalled",
            sound: ANNOUNCER_SHUTTLERECALLED,
            sender_override: "Emergency Shuttle Uplink Alert",
            color_override: "orange");

        SSticker.emergency_reason = null;
        return null;
    }

    /**
     * Proc that handles checking if the emergency shuttle was successfully hijacked via being the only people present on the shuttle for the elimination hijack or highlander objective
     */
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:100
    public virtual object elimination_hijack(object filter_by_human = null, object solo_hijack = null)
    {
        filter_by_human ??= TRUE;
        solo_hijack ??= FALSE;
        bool has_people = false;
        float hijacker_count = 0;
        foreach (mob_living player in Dm.each<mob_living>(GLOB.player_list))
        {
            if (player.mind != null)
            {
                if (player.stat != DEAD)
                {
                    if (issilicon(player) && Dm.truthy(filter_by_human)) //Borgs are technically dead anyways
                        continue;
                    if (isanimal_or_basicmob(player) && Dm.truthy(filter_by_human)) //animals don't count
                        continue;
                    if (isbrain(player)) //also technically dead
                        continue;
                    if (Dm.truthy(shuttle_areas[get_area(player)]))
                    {
                        has_people = true;
                        area location = get_area(player.mind.current) as area;
                        //Non-antag present. Can't hijack.
                        if (!Dm.truthy(player.mind.has_antag_datum(typeof(datum_antagonist))) && !istype(location, typeof(area_shuttle_escape_brig)))
                            return FALSE;
                        //Antag present, doesn't stop but let's see if we actually want to hijack
                        bool prevent = false;
                        foreach (datum_antagonist A in Dm.each<datum_antagonist>(player.mind.antag_datums))
                        {
                            if (A.can_elimination_hijack == ELIMINATION_ENABLED)
                            {
                                hijacker_count += 1;
                                prevent = false;
                                break; //If we have both prevent and hijacker antags assume we want to hijack.
                            }
                            else if (A.can_elimination_hijack == ELIMINATION_PREVENT)
                                prevent = true;
                        }
                        if (prevent)
                            return FALSE;
                    }
                }
            }
        }

        //has people AND either there's only one hijacker or there's any but solo_hijack is disabled
        return Dm.num(has_people && ((hijacker_count == 1) || (hijacker_count > 0 && !Dm.truthy(solo_hijack))));
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:133
    public virtual object is_hijacked()
    {
        return Dm.eq(hijack_status, HIJACK_COMPLETED) ? TRUE : FALSE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:146
    public override async DmTask check()
    {
        if (!Dm.truthy(timer))
            return;
        float time_left = Dm.num(timeLeft(1));

        // The emergency shuttle doesn't work like others so this
        // ripple check is slightly different
        if (!Dm.truthy(ripples.len) && (time_left <= SHUTTLE_RIPPLE_TIME) && (Dm.eq(mode, SHUTTLE_CALL) || Dm.eq(mode, SHUTTLE_ESCAPE)))
        {
            obj_docking_port_stationary destination_dock = null;
            if (Dm.eq(mode, SHUTTLE_CALL))
                destination_dock = SSshuttle.getDock("emergency_home") as obj_docking_port_stationary;
            else if (Dm.eq(mode, SHUTTLE_ESCAPE))
                destination_dock = SSshuttle.getDock("emergency_away") as obj_docking_port_stationary;
            create_ripples(destination_dock);
        }

        if (Dm.eq(mode, SHUTTLE_RECALL))
        {
            if (time_left <= 0)
            {
                mode = SHUTTLE_IDLE;
                timer = 0f;
            }
        }
        else if (Dm.eq(mode, SHUTTLE_CALL))
        {
            if (time_left <= 0)
            {
                //move emergency shuttle to station
                if (!Dm.eq(await initiate_docking(SSshuttle.getDock("emergency_home") as obj_docking_port_stationary), DOCKING_SUCCESS))
                {
                    setTimer(20);
                    return;
                }
                mode = SHUTTLE_DOCKED;
                setTimer(SSshuttle.emergency_dock_time);
                // tg: send2adminchat("Server", "The Emergency Shuttle has docked with the station."): the external admin chat (not ported)
                priority_announce(
                    text: Dm.text("[] has docked with the station. You have [] to board the emergency shuttle.", SSshuttle.emergency, DisplayTimeText(SSshuttle.emergency_dock_time)),
                    title: "Emergency Shuttle Arrival",
                    sound: ANNOUNCER_SHUTTLEDOCK,
                    sender_override: "Emergency Shuttle Uplink Alert",
                    color_override: "orange");
                // tg: ShuttleDBStuff(), the round's shuttle name in the database (M10+); addtimer(announce_shuttle_events, 20 SECONDS): notify_ghosts is not ported
            }
        }
        else if (Dm.eq(mode, SHUTTLE_DOCKED))
        {
            if (time_left <= ENGINE_START_TIME)
            {
                mode = SHUTTLE_IGNITING;
                SSshuttle.checkHostileEnvironment();
                if (Dm.eq(mode, SHUTTLE_STRANDED))
                    return;
                foreach (obj_docking_port_mobile M in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports))
                {
                    if (Dm.eq(M.launch_status, UNLAUNCHED)) //Pods will not launch from the mine/planet, and other ships won't launch unless we tell them to.
                        M.check_transit_zone();
                }
            }
        }
        else if (Dm.eq(mode, SHUTTLE_IGNITING))
        {
            bool success = true;
            SSshuttle.checkHostileEnvironment();
            if (Dm.eq(mode, SHUTTLE_STRANDED))
                return;

            success &= Dm.eq(check_transit_zone(), TRANSIT_READY);
            foreach (obj_docking_port_mobile M in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports))
            {
                if (Dm.eq(M.launch_status, UNLAUNCHED))
                    success &= Dm.eq(M.check_transit_zone(), TRANSIT_READY);
            }
            if (!success)
                setTimer(ENGINE_START_TIME);

            if (time_left <= 50 && !Dm.truthy(sound_played)) //4 seconds left:REV UP THOSE ENGINES BOYS. - should sync up with the launch
            {
                sound_played = 1; //Only rev them up once.
                DmList areas = Dm.list();
                foreach (area_shuttle_escape E in Dm.each<area_shuttle_escape>(GLOB.areas))
                    Dm.append(ref areas, E);
                hyperspace_sound(HYPERSPACE_WARMUP, areas);
            }

            if (time_left <= 0 && !Dm.truthy(SSshuttle.emergency_no_escape))
            {
                //move each escape pod (or applicable spaceship) to its corresponding transit dock
                foreach (obj_docking_port_mobile M in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports.Copy()))
                    await M.on_emergency_launch();

                //now move the actual emergency shuttle to its transit dock
                DmList areas = Dm.list();
                foreach (area_shuttle_escape E in Dm.each<area_shuttle_escape>(GLOB.areas))
                    Dm.append(ref areas, E);
                hyperspace_sound(HYPERSPACE_LAUNCH, areas);
                await enterTransit();

                //Tell the events we're starting, so they can time their spawns or do some other stuff
                //Tell the events we're starting, so they can time their spawns or do some other stuff
                foreach (datum_shuttle_event @event in Dm.each<datum_shuttle_event>(event_list))
                    @event.start_up_event(Dm.num(SSshuttle.emergency_escape_time) * Dm.num(engine_coeff));

                mode = SHUTTLE_ESCAPE;
                launch_status = ENDGAME_LAUNCHED;
                setTimer(SSshuttle.emergency_escape_time * engine_coeff);
                priority_announce(
                    text: Dm.text("The emergency shuttle has left the station. Estimate [] minutes until the shuttle docks at [].", timeLeft(60 * 10), command_name()),
                    title: "Emergency Shuttle Departure",
                    sender_override: "Emergency Shuttle Uplink Alert",
                    color_override: "orange");
                SSticker.poll_hearts(); // INVOKE_ASYNC(SSticker, TYPE_PROC_REF(/datum/controller/subsystem/ticker, poll_hearts)): it does not sleep (gaps R5)
                // tg: :245  INVOKE_ASYNC(SSvote, initiate_vote, /datum/vote/map_vote, "Map Rotation", forced = TRUE): the map vote is not ported
                // (no map rotation, not-translated.md 142)

                if (!Dm.truthy(is_reserved_level(z)))
                    throw Dm.CRASH("Emergency shuttle did not move to transit z-level!", "code/modules/shuttle/mobile_port/variants/emergency/emergency.dm", 248);
            }
        }
        else if (Dm.eq(mode, SHUTTLE_STRANDED) || Dm.eq(mode, SHUTTLE_DISABLED))
        {
            SSshuttle.checkHostileEnvironment();
        }
        else if (Dm.eq(mode, SHUTTLE_ESCAPE))
        {
            if (Dm.truthy(sound_played) && time_left <= HYPERSPACE_END_TIME)
            {
                DmList areas = Dm.list();
                foreach (area_shuttle_escape E in Dm.each<area_shuttle_escape>(GLOB.areas))
                    Dm.append(ref areas, E);
                hyperspace_sound(HYPERSPACE_END, areas);
            }
            if (time_left <= PARALLAX_LOOP_TIME)
            {
                bool area_parallax = false;
                foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(shuttle_areas))
                {
                    if (Dm.truthy(shuttle_area.parallax_movedir))
                    {
                        area_parallax = true;
                        break;
                    }
                }
                if (area_parallax)
                {
                    parallax_slowdown();
                    foreach (obj_docking_port_mobile M in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports))
                    {
                        if (Dm.eq(M.launch_status, ENDGAME_LAUNCHED))
                        {
                            if (M is obj_docking_port_mobile_pod)
                                M.parallax_slowdown();
                        }
                    }
                }
            }

            process_events();

            if (time_left <= 0)
            {
                //move each escape pod to its corresponding escape dock
                foreach (obj_docking_port_mobile port in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports.Copy()))
                    await port.on_emergency_dock();

                // now move the actual emergency shuttle to centcom
                // unless the shuttle is "hijacked"
                object destination_dock = "emergency_away";
                if (Dm.truthy(is_hijacked()) || Dm.truthy(elimination_hijack()))
                {
                    // just double check
                    await SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE);
                    destination_dock = "emergency_syndicate";
                    minor_announce(I18n.T("Corruption detected in shuttle navigation protocols. Please contact your supervisor."), I18n.T("SYSTEM ERROR:"),
                        sound_override: Dm.resource("sound/announcer/announcement/announce_syndi.ogg"));
                }

                await dock_id(destination_dock);
                mode = SHUTTLE_ENDGAME;
                timer = 0f;
            }
        }
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:297
    public override void transit_failure()
    {
        base.transit_failure();
        message_admins("Moving emergency shuttle directly to centcom dock to prevent deadlock.");

        mode = SHUTTLE_ESCAPE;
        launch_status = ENDGAME_LAUNCHED;
        setTimer(SSshuttle.emergency_escape_time);
        priority_announce(
            text: Dm.text("The emergency shuttle is preparing for direct jump. Estimate [] minutes until the shuttle docks at [].", timeLeft(60 * 10), command_name()),
            title: "Emergency Shuttle Transit Failure",
            sender_override: "Emergency Shuttle Uplink Alert",
            color_override: "orange");
    }

    // DEVIATION(DV238): an event that polls ghosts, or spawns what is still inert, would leave dead objects in hyperspace
    private static bool event_can_run(Type @event)
    {
        if (typeof(datum_shuttle_event_simple_spawner_player_controlled).IsAssignableFrom(@event))
            return false;
        if (Dm.@new(@event) is not datum_shuttle_event_simple_spawner spawner)
            return true;
        foreach (Type spawned in Dm.each<Type>(spawner.spawning_list))
        {
            if (spawned.IsDefined(typeof(DmInertAttribute), false))
                return false;
        }
        return true;
    }

    ///Generate a list of events to run during the departure
    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency.dm:312
    public virtual void setup_shuttle_events()
    {
        DmList names = Dm.list();
        foreach (Type @event in Dm.each<Type>(subtypesof(typeof(datum_shuttle_event))))
        {
            if (!prob(Dm.num(Dm.initial(@event, "event_probability"))))
                continue;
            if (!event_can_run(@event)) // DEVIATION(DV238)
                continue;
            add_shuttle_event(@event);
            Dm.append(ref names, Dm.initial(@event, "name"));
        }
        if (Dm.truthy(LAZYLEN(names)))
            log_game(Dm.text("[] has selected the following shuttle events: [].", capitalize(name), english_list(names)));
    }
}
