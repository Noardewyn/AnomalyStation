// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/arrivals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file but the text-to-speech voice of its requests console (SStts, not ported) and vv_edit_var (the
// admin's var editing) and NukeDiskCheck's disks (the nuclear disk is the antagonists'). The arrivals shuttle's chairs are the late joiners' spawns (LateInitialize: SSjob.latejoin_trackers).

// tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:1
[DmPath("/obj/docking_port/mobile/arrivals")]
public partial class obj_docking_port_mobile_arrivals : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        preferred_direction = dir;
        return INITIALIZE_HINT_LATELOAD; //for latejoin list
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:27
    public override object register(object replace = null, object custom = null)
    {
        base.register(replace, custom);
        if (SSshuttle.arrivals != null)
            log_mapping("More than one arrivals docking_port placed on map! Ignoring duplicates.");
        SSshuttle.arrivals = this;
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:33
    public override void LateInitialize(params object[] __extra)
    {
        areas = Dm.list();

        DmList new_latejoin = Dm.list();
        foreach (area_shuttle_arrival arrival_area in Dm.each<area_shuttle_arrival>(GLOB.areas))
        {
            foreach (DmList zlevel_turfs in Dm.each<DmList>(arrival_area.get_zlevel_turf_lists()))
            {
                foreach (turf arrival_turf in Dm.each<turf>(zlevel_turfs))
                {
                    foreach (obj_structure_chair shuttle_chair in Dm.each<obj_structure_chair>(arrival_turf))
                        Dm.append(ref new_latejoin, shuttle_chair);
                    if (console == null)
                    {
                        console = locate<obj_machinery_requests_console>(arrival_turf);
                        if (Dm.truthy(SStts?.tts_enabled) && console != null)
                            console.voice = SStts.tram_voice;
                        if (console != null)
                            RegisterSignal(console, COMSIG_QDELETING, nameof(find_console));
                    }
                }
            }
            Dm.append(ref areas, arrival_area);
        }

        if (Dm.truthy(SSjob.latejoin_trackers.len))
            log_mapping("Map contains predefined latejoin spawn points and an arrivals shuttle. Using the arrivals shuttle.");

        if (!Dm.truthy(new_latejoin.len))
        {
            log_mapping("Arrivals shuttle contains no chairs for spawn points. Reverting to latejoin landmarks.");
            if (!Dm.truthy(SSjob.latejoin_trackers.len))
                log_mapping("No latejoin landmarks exist. Players will spawn unbuckled on the shuttle.");
            return;
        }

        SSjob.latejoin_trackers = new_latejoin;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:60
    public override async DmTask check()
    {
        await base.check();

        if (Dm.truthy(perma_docked))
        {
            if (!Dm.eq(mode, SHUTTLE_CALL))
            {
                sound_played = FALSE;
                mode = SHUTTLE_IDLE;
            }
            else
            {
                SendToStation();
            }
            return;
        }

        if (Dm.truthy(damaged))
        {
            if (!Dm.truthy(CheckTurfsPressure()))
            {
                damaged = FALSE;
                if (console != null)
                    await console.say("Repairs complete, launching soon.");
            }
            return;
        }

        //If this proc is high on the profiler add a cooldown to the stuff after this line

        else if (Dm.truthy(CheckTurfsPressure()))
        {
            damaged = TRUE;
            if (console != null)
                await console.say("Alert, hull breach detected!");
            await aas_config_announce(typeof(datum_aas_config_entry_arrivals_broken), Dm.list(), command_span: TRUE);
            if (!Dm.eq(mode, SHUTTLE_CALL))
            {
                sound_played = FALSE;
                mode = SHUTTLE_IDLE;
            }
            else
            {
                SendToStation();
            }
            return;
        }

        bool found_awake = Dm.truthy(PersonCheck()) || Dm.truthy(NukeDiskCheck());
        if (Dm.eq(mode, SHUTTLE_CALL))
        {
            if (found_awake)
                SendToStation();
        }
        else if (Dm.eq(mode, SHUTTLE_IGNITING))
        {
            if (found_awake && !Dm.truthy(force_depart))
            {
                mode = SHUTTLE_IDLE;
                sound_played = FALSE;
            }
            else if (!Dm.truthy(sound_played))
            {
                hyperspace_sound(HYPERSPACE_WARMUP, areas);
                sound_played = TRUE;
            }
        }
        else if (!found_awake)
        {
            await Launch(FALSE);
        }
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:106
    public virtual void find_console(datum source = null, object force = null)
    {
        //clear ref to old deleted console
        console = null;

        //find new console
        foreach (area_shuttle_arrival arrival_area in Dm.each<area_shuttle_arrival>(areas))
        {
            foreach (DmList zlevel_turfs in Dm.each<DmList>(arrival_area.get_zlevel_turf_lists()))
            {
                foreach (turf arrival_turf in Dm.each<turf>(zlevel_turfs))
                {
                    var target = locate<obj_machinery_requests_console>(arrival_turf);
                    if (!QDELETED(target))
                    {
                        console = target;
                        RegisterSignal(console, COMSIG_QDELETING, nameof(find_console));
                        return;
                    }
                }
            }
        }
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:124
    public virtual object CheckTurfsPressure()
    {
        foreach (object I in Dm.each(SSjob.latejoin_trackers))
        {
            var T = get_turf(I) as turf_open;
            float pressure = Dm.num(T.air.return_pressure());
            if (pressure < HAZARD_LOW_PRESSURE || pressure > HAZARD_HIGH_PRESSURE) //simple safety check
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:132
    public virtual object PersonCheck()
    {
        foreach (mob player in Dm.each<mob>(GLOB.player_list))
        {
            if (Dm.@in(get_area(player), areas) && !Dm.eq(player.stat, DEAD) && !HAS_TRAIT(player, TRAIT_BLOCK_SHUTTLE_MOVEMENT))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:138
    public virtual object NukeDiskCheck()
    {
        // tg: each /obj/item/disk/nuclear of SSpoints_of_interest.real_nuclear_disks in the shuttle's areas: the nuclear disk is the
        // antagonists' (not ported), so no disk keeps the shuttle docked
        return FALSE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:144
    public virtual void SendToStation()
    {
        // DEVIATION(DV241): the owner's flight is twice tg's dock window (55 ds)
        float dockTime = Dm.num(config.Get(typeof(datum_config_entry_number_arrivals_shuttle_dock_window))) * 2; // CONFIG_GET(number/arrivals_shuttle_dock_window)
        if (Dm.eq(mode, SHUTTLE_CALL) && Dm.num(timeLeft(1)) > dockTime)
        {
            if (console != null)
                Waiting.Detach(console.say(Dm.truthy(damaged) ? "Initiating emergency docking for repairs!" : Dm.text("Now approaching: [].", station_name())));
            hyperspace_sound(HYPERSPACE_LAUNCH, areas); //for the new guy
            setTimer(dockTime);
        }
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:152
    public override async DmTask<object> initiate_docking(obj_docking_port_stationary new_dock = null, object movement_direction = null, object force = null)
    {
        bool docked = new_dock == assigned_transit;
        sound_played = FALSE;
        if (docked) //about to launch
        {
            if (!Dm.truthy(force_depart))
            {
                string cancel_reason = null;
                if (Dm.truthy(PersonCheck()))
                    cancel_reason = "lifeform dectected on board";
                else if (Dm.truthy(NukeDiskCheck()))
                    cancel_reason = "critical station device detected on board";
                if (cancel_reason != null)
                {
                    mode = SHUTTLE_IDLE;
                    if (console != null)
                        await console.say(Dm.text("Launch cancelled, [].", cancel_reason));
                    return null;
                }
            }
            force_depart = FALSE;
        }
        object dot = await base.initiate_docking(new_dock, movement_direction, force);
        if (!Dm.truthy(dot) && !docked && !Dm.truthy(damaged))
        {
            if (console != null)
                await console.say("Welcome to your new life, employees!");
            foreach (datum_callback C in Dm.each<datum_callback>(queued_announces))
                await C.Invoke();
            // LAZYCLEARLIST(queued_announces) (__HELPERS/_lists.dm), expanded inline
            queued_announces?.Cut();
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:177
    public override void check_effects()
    {
        base.check_effects();
        if (Dm.eq(mode, SHUTTLE_CALL) && !Dm.truthy(sound_played) && Dm.num(timeLeft(1)) <= HYPERSPACE_END_TIME)
        {
            sound_played = TRUE;
            hyperspace_sound(HYPERSPACE_END, areas);
        }
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:183
    public override object canDock(obj_docking_port_stationary stationary_dock = null)
    {
        object dot = base.canDock(stationary_dock);
        if (Dm.eq(dot, SHUTTLE_ALREADY_DOCKED))
            dot = SHUTTLE_CAN_DOCK;
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:188
    public virtual async DmTask Launch(object pickingup = null)
    {
        if (Dm.truthy(pickingup))
            force_depart = TRUE;
        if (Dm.eq(mode, SHUTTLE_IDLE))
        {
            if (console != null)
                await console.say(Dm.truthy(pickingup) ? "Departing immediately for new employee pickup." : "Shuttle departing.");
            obj_docking_port_stationary target = target_dock;
            if (QDELETED(target))
                target = SSshuttle.getDock("arrival_stationary") as obj_docking_port_stationary;
            request(target); //we will intentionally never return SHUTTLE_ALREADY_DOCKED
        }
    }

    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:199
    public virtual async DmTask RequireUndocked(mob user = null)
    {
        if (Dm.eq(mode, SHUTTLE_CALL) || Dm.truthy(damaged))
            return;

        await Launch(TRUE);

        to_chat(user, span_notice("Calling your shuttle. One moment..."));
        while (!Dm.eq(mode, SHUTTLE_CALL) && !Dm.truthy(damaged))
            await stoplag();
    }

    /**
     * Queues an announcement arrival.
     *
     * Arguments:
     * * mob - The arriving mob.
     * * rank - The job of the arriving mob.
     */
    // tg: code/modules/shuttle/mobile_port/variants/arrivals.dm:216
    public virtual void QueueAnnounce(object mob = null, object rank = null)
    {
        if (!Dm.eq(mode, SHUTTLE_CALL))
        {
            Waiting.Detach(announce_arrival(mob as mob_living_carbon_human, rank));
        }
        else
        {
            // LAZYADD(queued_announces, CALLBACK(GLOBAL_PROC, GLOBAL_PROC_REF(announce_arrival), mob, rank)) (__HELPERS/_lists.dm), expanded inline
            queued_announces ??= Dm.list();
            Dm.append(ref queued_announces, Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.announce_arrival), mob, rank));
        }
    }
}
