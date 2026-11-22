// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/shuttle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 and G4 (D272): the docking ports' bookkeeping, the round-start templates (setup_shuttles, action_load,
// load_template), the timers (fire), the transit docks, the emergency shuttle's call, recall and auto-call and the hostile
// environments. Gaps CM1: the shuttle purchases' access (init_has_purchase_shuttle_access). Not here: the supply packs and the
// cargo's orders, trade blockades (the cargo console, after M10), the shuttle manipulator's previews (admin tools, after M10), Recover (the Master's restart, slice H). The navigation
// computers' dock overlap and hidden turfs since syndicate N1c (D316).

// tg: code/controllers/subsystem/shuttle.dm:12 SUBSYSTEM_DEF(shuttle)
[DmPath("/datum/controller/subsystem/shuttle")]
public partial class datum_controller_subsystem_shuttle : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(shuttle) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSshuttle) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/shuttle.dm:12
    public override async DmTask New(params object[] __extra)
    {
        if (SSshuttle != this)
        {
            if (SSshuttle is datum_controller_subsystem_shuttle)
            {
                Recover();
                qdel(SSshuttle);
            }
            SSshuttle = this;
        }
        // a world without a selected map (the host-built test and toy rooms) has no shuttle to move: SS_NO_FIRE, as SSmapping
        World world = World.CurrentOrNull;
        if (world == null || MapSeam.Selected(world) == null)
            ss_flags = Dm.bitor(ss_flags, SS_NO_FIRE);
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/shuttle.dm:158
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        order_number = rand(1, 9000);

        // tg: :161-188  the supply packs (subtypesof(/datum/supply_pack)), below
        // a world without a selected map has no reserved level (SSmapping keeps SS_NO_INIT), and a map without a dock (the small
        // test fixtures) has no shuttle to fly to CentCom: no CentCom docks to stand in
        // tg: code/controllers/subsystem/shuttle.dm:159
        order_number = rand(1, 9000);

        DmList pack_processing = subtypesof(typeof(datum_supply_pack));
        while (Dm.truthy(length(pack_processing)))
        {
            object next = pack_processing[length(pack_processing)];
            pack_processing.len--;
            var pack = next is Type path ? (datum_supply_pack)Dm.@new(path) : (datum_supply_pack)next;

            DmList generated_packs = pack.generate_supply_packs();
            if (generated_packs != null)
            {
                Dm.append(ref pack_processing, generated_packs);
                continue;
            }

            //we have to create the pack before checking if it has 'contains' because generate_supply_packs manually sets it, therefore we cant check initial.
            if (pack.contains == null)
                continue;

            //Adds access requirements to the end of each description.
            if (Dm.truthy(pack.access) && Dm.truthy(pack.access_view))
            {
                if (Dm.eq(pack.access, pack.access_view))
                    pack.desc += Dm.text(" Requires [] access to open or purchase.", SSid_access.get_access_desc(pack.access));
                else
                    pack.desc += Dm.text(" Requires [] access to open, or [] access to purchase.", SSid_access.get_access_desc(pack.access), SSid_access.get_access_desc(pack.access_view));
            }
            else if (Dm.truthy(pack.access))
            {
                pack.desc += Dm.text(" Requires [] access to open.", SSid_access.get_access_desc(pack.access));
            }
            else if (Dm.truthy(pack.access_view))
            {
                pack.desc += Dm.text(" Requires [] access to purchase.", SSid_access.get_access_desc(pack.access_view));
            }

            supply_packs[pack.id] = pack;
        }

        if (MapSeam.Selected(World.Current) != null && stationary_docking_ports.len > 0)
            await dm_centcom_docks();
        await setup_shuttles(stationary_docking_ports);
        has_purchase_shuttle_access = init_has_purchase_shuttle_access() as DmList;

        if (arrivals == null)
            log_mapping("No /obj/docking_port/mobile/arrivals placed on the map!");
        if (emergency == null)
            log_mapping("No /obj/docking_port/mobile/emergency placed on the map!");
        if (backup_shuttle == null)
            log_mapping("No /obj/docking_port/mobile/emergency/backup placed on the map!");
        if (supply == null)
            log_mapping("No /obj/docking_port/mobile/supply placed on the map!");
        return SS_INIT_SUCCESS;
    }

    // DEVIATION(DV190): CentCom's docks (_maps/map_files/generic/CentCom.dmm, which D108 does not load) with tg's ids, names,
    // directions, sizes, json keys and templates, the backup shuttle's first so that the emergency shuttle registers last
    // (emergency.dm register: the last one is SSshuttle.emergency). Each stands in its own reservation of the reserved level.
    private static readonly System.Collections.Immutable.ImmutableArray<(string Id, string Name, float Dir, float Width, float Height, float Dwidth, string JsonKey, Type Template)> CentcomDocks =
    [
        ("backup_away", "Backup Shuttle Dock", EAST, 8, 8, 2, null, typeof(datum_map_template_shuttle_emergency_backup)),
        ("emergency_away", "CentCom Emergency Shuttle Dock", EAST, 50, 50, 25, "emergency", null),
        ("cargo_away", "CentCom", WEST, 20, 8, 8, "cargo", null),
        ("ferry_away", "CentCom Ferry Dock", WEST, 21, 30, 10, "ferry", null),
        ("pod_away", "recovery ship", EAST, 5, 7, 2, null, null),
        ("pod_2_away", "recovery ship", EAST, 3, 5, 1, null, null),
        ("pod_3_away", "recovery ship", EAST, 3, 5, 1, null, null),
        ("pod_4_away", "recovery ship", EAST, 3, 5, 1, null, null),
        ("pod_5_away", "recovery ship", WEST, 3, 5, 1, null, null),
        ("pod_6_away", "recovery ship", WEST, 3, 5, 1, null, null),
        ("pod_7_away", "recovery ship", WEST, 3, 5, 1, null, null),
        ("pod_8_away", "recovery ship", WEST, 3, 5, 1, null, null),
        // DEVIATION(DV236): Lavaland's docks (_maps/map_files/Mining/Lavaland.dmm): no lavaland is loaded, so the mining and labour shuttles land in space
        ("laborcamp_away", "labor camp", NORTH, 9, 5, 2, null, null),
        ("mining_away", "lavaland mine", SOUTH, 7, 10, 3, null, null),
        ("lavaland_common_away", "Mining base public dock", EAST, 7, 7, 3, null, null),
    ];

    /// DEVIATION(DV190): makes the stand-ins of CentCom's docks (CentcomDocks), each a stationary port in the middle of a reservation of
    /// space on the reserved level, its vars set as CentCom.dmm sets them (the map preloader) before its Initialize registers it.
    public virtual async DmTask dm_centcom_docks()
    {
        foreach (var dock in CentcomDocks)
        {
            if (getDock(dock.Id) != null)
                continue; // D299: the CentCom level has its real docks
            // the dock's rectangle around its port (return_coords at 0,0): the reservation holds it with a tile of space around
            float cos = dock.Dir == WEST ? 0 : dock.Dir == SOUTH ? -1 : dock.Dir == EAST ? 0 : 1;
            float sin = dock.Dir == WEST ? 1 : dock.Dir == SOUTH ? 0 : dock.Dir == EAST ? -1 : 0;
            // obj_docking_port.return_coords with dheight 0 (no CentCom dock has one)
            float x0 = -dock.Dwidth * cos, y0 = -dock.Dwidth * sin;
            float x1 = (-dock.Dwidth + dock.Width - 1) * cos - (dock.Height - 1) * sin;
            float y1 = (-dock.Dwidth + dock.Width - 1) * sin + (dock.Height - 1) * cos;
            float minX = Math.Min(x0, x1), minY = Math.Min(y0, y1);
            float spanX = Math.Abs(x1 - x0) + 1, spanY = Math.Abs(y1 - y0) + 1;
            var reservation = await SSmapping.request_turf_block_reservation(spanX + 2, spanY + 2, 1) as datum_turf_reservation;
            if (reservation == null)
                throw Dm.CRASH(Dm.text("no reservation for CentCom's dock []", dock.Id), "DV190", 0);
            var bottom_left = (turf)reservation.bottom_left_turfs[1];
            turf midpoint = locate(bottom_left.x + 1 - minX, bottom_left.y + 1 - minY, bottom_left.z);

            DmList attributes = Dm.list(
                Dm.kv("dir", dock.Dir), Dm.kv("dwidth", dock.Dwidth), Dm.kv("height", dock.Height), Dm.kv("name", dock.Name),
                Dm.kv("shuttle_id", dock.Id), Dm.kv("width", dock.Width));
            if (dock.JsonKey != null)
                attributes["json_key"] = dock.JsonKey;
            if (dock.Template != null)
                attributes["roundstart_template"] = dock.Template;
            Dm.world.preloader_setup(attributes, typeof(obj_docking_port_stationary));
            Dm.@new<obj_docking_port_stationary>(midpoint);
        }
    }

    // tg: code/controllers/subsystem/shuttle.dm:203
    public virtual async DmTask setup_shuttles(DmList stationary = null)
    {
        foreach (obj_docking_port_stationary port in Dm.each<obj_docking_port_stationary>(stationary.Copy()))
        {
            await port.load_roundstart();
            // CHECK_TICK (__DEFINES/_tick.dm:21), awaited in a suspending proc
            if (TICK_CHECK())
                await stoplag();
        }
    }

    // tg: code/controllers/subsystem/shuttle.dm:208
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        foreach (object thing in Dm.each(mobile_docking_ports.Copy()))
        {
            if (thing == null)
            {
                mobile_docking_ports.Remove(thing);
                continue;
            }
            var port = (obj_docking_port_mobile)thing;
            await port.check();
        }
        foreach (obj_docking_port_stationary_transit T in Dm.each<obj_docking_port_stationary_transit>(transit_docking_ports.Copy()))
        {
            if (T.owner == null)
                qdel(T, force: TRUE);
            // This next one removes transit docks/zones that aren't
            // immediately being used. This will mean that the zone creation
            // code will be running a lot.

            // If we're below the soft reservation threshold, don't clear the old space
            // We're better off holding onto it for now
            if (transit_utilized < SOFT_TRANSIT_RESERVATION_THRESHOLD)
                continue;
            obj_docking_port_mobile owner = T.owner;
            if (owner != null)
            {
                bool idle = Dm.eq(owner.mode, SHUTTLE_IDLE);
                bool not_centcom_evac = Dm.eq(owner.launch_status, NOLAUNCH);
                bool not_in_use = T.get_docked() == null;
                if (idle && not_centcom_evac && not_in_use)
                    qdel(T, force: TRUE);
            }
        }
        CheckAutoEvac();

        if (!Dm.truthy(SSmapping.clearing_reserved_turfs))
        {
            while (Dm.truthy(transit_requesters.len))
            {
                object requester = popleft(transit_requesters);
                object success = null;
                // Do not try and generate any transit if we're using more then our max already
                if (transit_utilized < MAX_TRANSIT_TILE_COUNT)
                    success = await generate_transit_dock((obj_docking_port_mobile)requester);
                if (!Dm.truthy(success)) // BACK OF THE QUEUE
                {
                    transit_request_failures[requester] = Dm.num(transit_request_failures[requester]) + 1;
                    if (Dm.num(transit_request_failures[requester]) < MAX_TRANSIT_REQUEST_RETRIES)
                    {
                        Dm.append(ref transit_requesters, requester);
                    }
                    else
                    {
                        var M = (obj_docking_port_mobile)requester;
                        M.transit_failure();
                    }
                }
                if (Dm.truthy((Dm.world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    break;
            }
        }
    }

    // tg: shuttle.dm:1-6  #define MAX_TRANSIT_REQUEST_RETRIES 10, MAX_TRANSIT_TILE_COUNT (150 ** 2), SOFT_TRANSIT_RESERVATION_THRESHOLD (100 ** 2)
    private const float MAX_TRANSIT_REQUEST_RETRIES = 10;
    private const float MAX_TRANSIT_TILE_COUNT = 150 * 150;
    private const float SOFT_TRANSIT_RESERVATION_THRESHOLD = 100 * 100;

    // tg: code/controllers/subsystem/shuttle.dm:253
    public virtual void CheckAutoEvac()
    {
        if (Dm.truthy(emergency_no_escape) || Dm.truthy(admin_emergency_no_recall) || Dm.truthy(emergency_no_recall) || emergency == null || !Dm.truthy(SSticker.HasRoundStarted()))
            return;

        float threshold = Dm.num(config.Get(typeof(datum_config_entry_number_emergency_shuttle_autocall_threshold))) /* CONFIG_GET(number/emergency_shuttle_autocall_threshold) */;
        if (!Dm.truthy(threshold))
            return;

        float alive = 0;
        foreach (mob M in Dm.each<mob>(GLOB.player_list))
        {
            if (!Dm.eq(M.stat, DEAD))
                ++alive;
        }

        float total = GLOB.joined_player_list.len;
        if (total <= 0)
            return; //no players no autoevac

        if (alive / total <= threshold)
        {
            string msg = "Automatically dispatching emergency shuttle due to crew death.";
            message_admins(msg);
            log_shuttle(Dm.text("[] Alive: [], Roundstart: [], Threshold: []", msg, alive, total, threshold));
            emergency_no_recall = TRUE;
            priority_announce(
                text: "Catastrophic casualties detected: crisis shuttle protocols activated - jamming recall signals across all frequencies.",
                title: "Emergency Shuttle Dispatched",
                sound: ANNOUNCER_SHUTTLECALLED,
                sender_override: "Emergency Shuttle Uplink Alert",
                color_override: "orange");
            if (EMERGENCY_IDLE_OR_RECALLED() || Dm.num(emergency.timeLeft(1)) > emergency_call_time * ALERT_COEFF_AUTOEVAC_CRITICAL)
                emergency.request(null, set_coefficient: ALERT_COEFF_AUTOEVAC_CRITICAL);
        }
    }

    // #define EMERGENCY_IDLE_OR_RECALLED (SSshuttle.emergency && ((SSshuttle.emergency.mode == SHUTTLE_IDLE) || (SSshuttle.emergency.mode == SHUTTLE_RECALL))) (__DEFINES/shuttles.dm)
    public static bool EMERGENCY_IDLE_OR_RECALLED()
        => SSshuttle.emergency != null && (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IDLE) || Dm.eq(SSshuttle.emergency.mode, SHUTTLE_RECALL));

    // #define EMERGENCY_ESCAPED_OR_ENDGAMED (SSshuttle.emergency && ((SSshuttle.emergency.mode == SHUTTLE_ESCAPE) || (SSshuttle.emergency.mode == SHUTTLE_ENDGAME))) (__DEFINES/shuttles.dm)
    public static bool EMERGENCY_ESCAPED_OR_ENDGAMED()
        => SSshuttle?.emergency != null && (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE) || Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME));

    // #define EMERGENCY_AT_LEAST_DOCKED (SSshuttle.emergency && SSshuttle.emergency.mode != SHUTTLE_IDLE && SSshuttle.emergency.mode != SHUTTLE_RECALL && SSshuttle.emergency.mode != SHUTTLE_CALL) (__DEFINES/shuttles.dm:16)
    public static bool EMERGENCY_AT_LEAST_DOCKED()
        => SSshuttle.emergency != null && !Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IDLE) && !Dm.eq(SSshuttle.emergency.mode, SHUTTLE_RECALL) && !Dm.eq(SSshuttle.emergency.mode, SHUTTLE_CALL);

    // #define EMERGENCY_PAST_POINT_OF_NO_RETURN ((SSshuttle.emergency && SSshuttle.emergency.mode == SHUTTLE_CALL && !SSshuttle.past_restriction_point()) || EMERGENCY_AT_LEAST_DOCKED) (__DEFINES/shuttles.dm:17; syndicate A3: Dynamic's midrounds and latejoins)
    public static bool EMERGENCY_PAST_POINT_OF_NO_RETURN()
        => (SSshuttle.emergency != null && Dm.eq(SSshuttle.emergency.mode, SHUTTLE_CALL) && !Dm.truthy(SSshuttle.past_restriction_point())) || EMERGENCY_AT_LEAST_DOCKED();

    // tg: code/controllers/subsystem/shuttle.dm:286
    public virtual void block_recall(object lockout_timer = null)
    {
        if (lockout_timer == null)
            throw Dm.CRASH("Emergency shuttle block was called, but missing a value for the lockout duration", "code/controllers/subsystem/shuttle.dm", 288);
        if (Dm.truthy(admin_emergency_no_recall))
        {
            priority_announce(
                text: Dm.text("Emergency shuttle uplink interference detected, shuttle call disabled while the system reinitializes. Estimated restore in [].", DisplayTimeText(lockout_timer, round_seconds_to: 60)),
                title: "Uplink Interference",
                sound: Dm.resource("sound/announcer/announcement/announce_dig.ogg"),
                sender_override: "Emergency Shuttle Uplink Alert",
                color_override: "grey");
            _addtimer(Dm.@new<datum_callback>(this, nameof(unblock_recall)), Dm.num(lockout_timer), file: "code/controllers/subsystem/shuttle.dm", line: 297);
            return;
        }
        emergency_no_recall = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(unblock_recall)), Dm.num(lockout_timer), file: "code/controllers/subsystem/shuttle.dm", line: 300);
    }

    // tg: code/controllers/subsystem/shuttle.dm:302
    public virtual void unblock_recall()
    {
        if (Dm.truthy(admin_emergency_no_recall))
        {
            priority_announce(
                text: "Emergency shuttle uplink services are now back online.",
                title: "Uplink Restored",
                sound: Dm.resource("sound/announcer/announcement/announce_dig.ogg"),
                sender_override: "Emergency Shuttle Uplink Alert",
                color_override: "green");
            return;
        }
        emergency_no_recall = FALSE;
    }

    // tg: code/controllers/subsystem/shuttle.dm:314
    public virtual object getShuttle(object id = null)
    {
        foreach (obj_docking_port_mobile M in Dm.each<obj_docking_port_mobile>(mobile_docking_ports))
        {
            if (Dm.eq(M.shuttle_id, id))
                return M;
        }
        warning(Dm.text("couldn't find shuttle with id: []", id));
        return null;
    }

    // tg: code/controllers/subsystem/shuttle.dm:320
    public virtual object getDock(object id = null)
    {
        foreach (obj_docking_port_stationary S in Dm.each<obj_docking_port_stationary>(stationary_docking_ports))
        {
            if (Dm.eq(S.shuttle_id, id))
                return S;
        }
        warning(Dm.text("couldn't find dock with id: []", id));
        return null;
    }

    /// Check if we can call the evac shuttle.
    /// Returns TRUE if we can. Otherwise, returns a string detailing the problem.
    // tg: code/controllers/subsystem/shuttle.dm:328
    public virtual object canEvac()
    {
        float shuttle_refuel_delay = Dm.num(config.Get(typeof(datum_config_entry_number_shuttle_refuel_delay))) /* CONFIG_GET(number/shuttle_refuel_delay) */;
        if (Dm.world.time - Dm.num(SSticker.round_start_time) < shuttle_refuel_delay)
            return Dm.text("The emergency shuttle is refueling. Please wait [] before attempting to call.", DisplayTimeText(shuttle_refuel_delay - (Dm.world.time - Dm.num(SSticker.round_start_time))));

        if (Dm.eq(emergency.mode, SHUTTLE_RECALL)) return "The emergency shuttle may not be called while returning to CentCom.";
        if (Dm.eq(emergency.mode, SHUTTLE_CALL)) return "The emergency shuttle is already on its way.";
        if (Dm.eq(emergency.mode, SHUTTLE_DOCKED)) return "The emergency shuttle is already here.";
        if (Dm.eq(emergency.mode, SHUTTLE_IGNITING)) return "The emergency shuttle is firing its engines to leave.";
        if (Dm.eq(emergency.mode, SHUTTLE_ESCAPE)) return "The emergency shuttle is moving away to a safe distance.";
        if (Dm.eq(emergency.mode, SHUTTLE_STRANDED)) return "The emergency shuttle has been disabled by CentCom.";

        return TRUE;
    }

    // tg: code/controllers/subsystem/shuttle.dm:349
    public virtual object check_backup_emergency_shuttle()
    {
        if (emergency != null)
            return TRUE;

        warning("check_backup_emergency_shuttle(): There is no emergency shuttle, but the shuttle was called. Using the backup shuttle instead.");

        if (backup_shuttle == null)
            throw Dm.CRASH("check_backup_emergency_shuttle(): There is no emergency shuttle, or backup shuttle! The game will be unresolvable. This is possibly a mapping error, more likely a bug with the shuttle manipulation system, or badminry. It is possible to manually resolve this problem by loading an emergency shuttle template manually, and then calling register() on the mobile docking port. Good luck.", "code/controllers/subsystem/shuttle.dm", 357);
        emergency = backup_shuttle;

        return TRUE;
    }

    /**
     * Calls the emergency shuttle.
     *
     * Arguments:
     * * user - The mob that called the shuttle.
     * * call_reason - The reason the shuttle was called, which should be non-html-encoded text.
     */
    // tg: code/controllers/subsystem/shuttle.dm:375
    public virtual void requestEvac(mob user = null, object call_reason = null)
    {
        if (!Dm.truthy(check_backup_emergency_shuttle()))
            return;

        object can_evac_or_fail_reason = SSshuttle.canEvac();
        if (!Dm.eq(can_evac_or_fail_reason, TRUE))
        {
            to_chat(user, span_alert(Dm.text("[]", can_evac_or_fail_reason)));
            return;
        }

        if (length(trim(call_reason)) < CALL_SHUTTLE_REASON_LENGTH && Dm.num(SSsecurity_level.get_current_level_as_number()) > SEC_LEVEL_GREEN)
        {
            to_chat(user, span_alert("You must provide a reason."));
            return;
        }

        var signal_origin = get_area(user) as area;
        call_evac_shuttle(call_reason, signal_origin);

        log_shuttle(Dm.text("[] has called the emergency shuttle.", key_name(user)));
        // tg: deadchat_broadcast(" has called the shuttle at [signal_origin.name].", ...): the dead chat (beyond D151's ghosts)
        if (Dm.truthy(call_reason))
        {
            SSblackbox.record_feedback("text", "shuttle_reason", 1, Dm.text("[]", call_reason));
            log_shuttle(Dm.text("Shuttle call reason: []", call_reason));
            SSticker.emergency_reason = call_reason;
        }
        message_admins(Dm.text("[] has called the shuttle.", key_name_admin(user))); // tg: + the TRIGGER CENTCOM RECALL link (the admin panel's topic, M10+)
    }

    /// Call the emergency shuttle.
    /// If you are doing this on behalf of a player, use requestEvac instead.
    /// `signal_origin` is fluff occasionally provided to players.
    // tg: code/controllers/subsystem/shuttle.dm:402
    public virtual void call_evac_shuttle(object call_reason = null, object signal_origin = null)
    {
        if (!Dm.truthy(check_backup_emergency_shuttle()))
            return;

        call_reason = trim(html_encode(call_reason));

        string emergency_reason = Dm.text("\n\nNature of emergency:\n[]", call_reason);

        emergency.request(
            null,
            signal_origin: signal_origin as area,
            reason: html_decode(emergency_reason),
            red_alert: Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED ? TRUE : FALSE);

        var frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS) as datum_radio_frequency;

        if (frequency != null)
        {
            // Start processing shuttle-mode displays to display the timer
            var status_signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("command", "update")));
            frequency.post_signal(null, status_signal); // tg's source is SSshuttle, a datum the signal's obj source cannot hold; the displays read the data only
        }
    }

    /// Does a fluffy CentCom recall of the emergency shuttle with additional message as desired.
    /// Returns TRUE on success, FALSE otherwise.
    // tg: code/controllers/subsystem/shuttle.dm:425
    public virtual async DmTask<object> centcom_recall(mob user = null, object old_timer = null, object admiral_message = null)
    {
        if (!Dm.eq(emergency.timer, old_timer))
            return FALSE;

        if (!Dm.truthy(await cancel_evac(user, hide_origin: TRUE)))
            return FALSE; //feedback handled in cancel_evac()

        if (!Dm.truthy(admiral_message))
            admiral_message = pick(GLOB.admiral_messages);
        string intercepttext = Dm.text("<font size = 3><b>Nanotrasen Update</b>: Request For Shuttle.</font><hr>To whom it may concern:<br><br>We have taken note of the situation upon [] and have come to the conclusion that it does not warrant the abandonment of the station.<br>If you do not agree with our opinion we suggest that you open a direct line with us and explain the nature of your crisis.<br><br><i>This message has been automatically generated based upon readings from long range diagnostic tools. To assure the quality of your request every finalized report is reviewed by an on-call rear admiral.<br><b>Rear Admiral's Notes:</b> []", station_name(), admiral_message);
        print_command_report(intercepttext, announce: TRUE);

        return TRUE;
    }

    // Called when an emergency shuttle mobile docking port is
    // destroyed, which will only happen with admin intervention
    // tg: code/controllers/subsystem/shuttle.dm:451
    public virtual void emergencyDeregister()
    {
        // When a new emergency shuttle is created, it will override the
        // backup shuttle.
        this.emergency = this.backup_shuttle;
    }

    /// Actually work on canceling the emergency shuttle recall. Returns TRUE if successful, FALSE otherwise.
    /// If hide_origin is TRUE, the recaller's area will not be revealed in announcements (used by admin tools)
    // tg: code/controllers/subsystem/shuttle.dm:458
    public virtual async DmTask<object> cancel_evac(mob user = null, object hide_origin = null)
    {
        hide_origin ??= FALSE;
        if (!Dm.truthy(await can_recall(user)))
            return FALSE;

        var signal_origin = Dm.truthy(hide_origin) ? null : get_area(user) as area;
        emergency.cancel(signal_origin);
        log_shuttle(Dm.text("[] has recalled the shuttle.", key_name(user)));
        message_admins(Dm.text("[] has recalled the shuttle.", key_name_admin(user)));
        if (!Dm.truthy(hide_origin))
            Dm.Deferred("code/controllers/subsystem/shuttle.dm:467", "cancel_evac: deadchat_broadcast, the dead chat (beyond D151's ghosts)");
        return TRUE;
    }

    /// Can this user recall the emergency shuttle? Returns TRUE if they can, otherwise returns FALSE.
    // tg: code/controllers/subsystem/shuttle.dm:471
    public virtual async DmTask<object> can_recall(mob user = null)
    {
        if (emergency == null || !Dm.eq(emergency.mode, SHUTTLE_CALL))
            return FALSE;

        bool is_admin = user.client?.holder != null;
        if (is_admin)
            return await admin_recall(user);

        if (Dm.truthy(admin_emergency_no_recall) || Dm.truthy(emergency_no_recall))
            return FALSE;

        return past_restriction_point();
    }

    /// Are we past the restriction point (i.e. more than half of the shuttle timer has elapsed) for recalling the shuttle? Returns TRUE if we are, FALSE otherwise.
    // tg: code/controllers/subsystem/shuttle.dm:485
    public virtual object past_restriction_point()
    {
        float security_num = Dm.num(SSsecurity_level.get_current_level_as_number());
        if (security_num == SEC_LEVEL_GREEN)
        {
            if (Dm.num(emergency.timeLeft(1)) < emergency_call_time)
                return FALSE;
        }
        else if (security_num == SEC_LEVEL_BLUE)
        {
            if (Dm.num(emergency.timeLeft(1)) < emergency_call_time * 0.5f)
                return FALSE;
        }
        else
        {
            if (Dm.num(emergency.timeLeft(1)) < emergency_call_time * 0.25f)
                return FALSE;
        }

        return TRUE;
    }

    /// Handle admin level overrides of recalling the shuttle. We assume that the user passed is an admin.
    /// If a special state exists, we prompt the admin to confirm they want to override the wishes of other admins/game code. Returns TRUE if they elect to do so, FALSE otherwise.
    // tg: code/controllers/subsystem/shuttle.dm:502
    public virtual async DmTask<object> admin_recall(mob user = null)
    {
        if (Dm.truthy(admin_emergency_no_recall))
        {
            object admin_no_recall_alert = await tgui_alert(
                user,
                "An administrator has disabled the emergency shuttle recall function, are you sure you want to proceed with the recall?",
                "Admin Level Recall Confirmation",
                Dm.list("Yes", "No"));
            if (Dm.eq(admin_no_recall_alert, "Yes"))
            {
                admin_emergency_no_recall = FALSE;
                return TRUE;
            }
            return FALSE;
        }

        if (Dm.truthy(emergency_no_recall))
        {
            object general_no_recall_alert = await tgui_alert(
                user,
                "The emergency shuttle recall function is currently disabled by game code, are you sure you want to proceed with the recall?",
                "Recall Confirmation",
                Dm.list("Yes", "No"));
            if (Dm.eq(general_no_recall_alert, "Yes"))
            {
                // we will not unset emergency_no_recall here, as it's game code enforced and I want it to stay active, admins can transiently override it through this though.
                // they can always edit the variable on SSshuttle if desperate.
                return TRUE;
            }
            return FALSE;
        }

        return TRUE; // we assume that they've seen other warnings at earlier points if they got here from a verb or something like that
    }

    // tg: code/controllers/subsystem/shuttle.dm:530
    public virtual void autoEvac()
    {
        if (!Dm.truthy(SSticker.IsRoundInProgress()) || Dm.truthy(supermatter_cascade))
            return;

        bool callShuttle = true;

        foreach (object thing in Dm.each(GLOB.shuttle_caller_list))
        {
            if (thing is mob_living_silicon_ai AI) // silicons SI4
            {
                if (AI.deployed_shell != null && AI.deployed_shell.client == null)
                    continue;
                if (IS_UNCONSCIOUS_OR_CRIT(AI) || AI.client == null)
                    continue;
            }
            else if (thing is obj_machinery_computer_communications C)
            {
                if (Dm.truthy(Dm.bitand(C.machine_stat, BROKEN)))
                    continue;
            }

            turf T = get_turf(thing);
            if (T != null && Dm.truthy(is_station_level(T.z)))
            {
                callShuttle = false;
                break;
            }
        }

        if (callShuttle)
        {
            if (EMERGENCY_IDLE_OR_RECALLED())
            {
                emergency.request(null, set_coefficient: ALERT_COEFF_AUTOEVAC_NORMAL);
                log_shuttle("There is no means of calling the emergency shuttle anymore. Shuttle automatically called.");
                message_admins("All the communications consoles were destroyed and all AIs are inactive. Shuttle called.");
            }
        }
    }

    // tg: code/controllers/subsystem/shuttle.dm:559
    public virtual void registerHostileEnvironment(datum bad = null)
    {
        hostile_environments[bad] = TRUE;
        checkHostileEnvironment();
    }

    // tg: code/controllers/subsystem/shuttle.dm:563
    public virtual void clearHostileEnvironment(datum bad = null)
    {
        Dm.remove(ref hostile_environments, bad);
        checkHostileEnvironment();
    }

    // tg: code/controllers/subsystem/shuttle.dm:591
    public virtual void checkHostileEnvironment()
    {
        foreach (datum hostile_environment_source in Dm.each<datum>(hostile_environments.Copy()))
        {
            if (QDELETED(hostile_environment_source))
                Dm.remove(ref hostile_environments, hostile_environment_source);
        }
        emergency_no_escape = hostile_environments.len;

        if (Dm.truthy(emergency_no_escape) && Dm.eq(emergency.mode, SHUTTLE_IGNITING))
        {
            emergency.mode = SHUTTLE_STRANDED;
            emergency.timer = null;
            emergency.sound_played = FALSE;
            priority_announce(
                text: "Departure has been postponed indefinitely pending conflict resolution.",
                title: "Hostile Environment Detected",
                sound: Dm.resource("sound/announcer/notice/notice1.ogg"),
                sender_override: "Emergency Shuttle Uplink Alert",
                color_override: "grey");
        }
        if (!Dm.truthy(emergency_no_escape) && (Dm.eq(emergency.mode, SHUTTLE_STRANDED) || Dm.eq(emergency.mode, SHUTTLE_DOCKED)))
        {
            emergency.mode = SHUTTLE_DOCKED;
            emergency.setTimer(emergency_dock_time);
            priority_announce(
                text: Dm.text("You have [] to board the emergency shuttle.", DisplayTimeText(emergency_dock_time)),
                title: "Hostile Environment Resolved",
                sound: Dm.resource("sound/announcer/announcement/announce_dig.ogg"),
                sender_override: "Emergency Shuttle Uplink Alert",
                color_override: "green");
        }
    }

    //try to move/request to dock_home if possible, otherwise dock_away. Mainly used for admin buttons
    // tg: code/controllers/subsystem/shuttle.dm:620
    public virtual async DmTask<object> toggleShuttle(object shuttle_id = null, object dock_home = null, object dock_away = null, object timed = null)
    {
        var shuttle_port = getShuttle(shuttle_id) as obj_docking_port_mobile;
        if (shuttle_port == null)
            return DOCKING_BLOCKED;
        var docked_at = shuttle_port.get_docked() as obj_docking_port_stationary;
        object destination = dock_home;
        if (docked_at != null && Dm.eq(docked_at.shuttle_id, dock_home))
            destination = dock_away;
        if (Dm.truthy(timed))
        {
            if (Dm.truthy(shuttle_port.request(getDock(destination) as obj_docking_port_stationary)))
                return DOCKING_IMMOBILIZED;
        }
        else
        {
            if (!Dm.eq(await shuttle_port.initiate_docking(getDock(destination) as obj_docking_port_stationary), DOCKING_SUCCESS))
                return DOCKING_IMMOBILIZED;
        }
        return DOCKING_SUCCESS; //dock successful
    }

    /**
     * Moves a shuttle to a new location
     *
     * Arguments:
     * * shuttle_id - The ID of the shuttle (mobile docking port) to move
     * * dock_id - The ID of the destination (stationary docking port) to move to
     * * timed - If true, have the shuttle follow normal spool-up, jump, dock process. If false, immediately move to the new location.
     */
    // tg: code/controllers/subsystem/shuttle.dm:644
    public virtual async DmTask<object> moveShuttle(object shuttle_id = null, object dock_id = null, object timed = null)
    {
        var shuttle_port = getShuttle(shuttle_id) as obj_docking_port_mobile;
        var docking_target = getDock(dock_id) as obj_docking_port_stationary;

        if (shuttle_port == null)
            return DOCKING_NULL_SOURCE;
        if (Dm.truthy(timed))
        {
            if (Dm.truthy(shuttle_port.request(docking_target)))
                return DOCKING_IMMOBILIZED;
        }
        else
        {
            if (!Dm.eq(await shuttle_port.initiate_docking(docking_target), DOCKING_SUCCESS))
                return DOCKING_IMMOBILIZED;
        }
        return DOCKING_SUCCESS; //dock successful
    }

    // tg: code/controllers/subsystem/shuttle.dm:658
    public virtual void request_transit_dock(obj_docking_port_mobile M = null)
    {
        if (M == null)
            throw Dm.CRASH(Dm.text("[] is not a mobile docking port", M), "code/controllers/subsystem/shuttle.dm", 660);

        if (M.assigned_transit != null)
            return;
        if (!Dm.@in(M, transit_requesters))
            Dm.append(ref transit_requesters, M);
    }

    // tg: code/controllers/subsystem/shuttle.dm:668
    public virtual async DmTask<object> generate_transit_dock(obj_docking_port_mobile M = null)
    {
        // First, determine the size of the needed zone
        // Because of shuttle rotation, the "width" of the shuttle is not
        // always x.
        float travel_dir = M.preferred_direction;
        // Remember, the direction is the direction we appear to be
        // coming from
        float dock_angle = Dm.num(dir2angle(M.preferred_direction)) + Dm.num(dir2angle(M.port_direction)) + 180;
        float dock_dir = Dm.num(angle2dir(dock_angle));

        float transit_width = SHUTTLE_TRANSIT_BORDER * 2;
        float transit_height = SHUTTLE_TRANSIT_BORDER * 2;

        // Shuttles travelling on their side have their dimensions swapped
        // from our perspective
        if (dock_dir == NORTH || dock_dir == SOUTH)
        {
            transit_width += M.width;
            transit_height += M.height;
        }
        else if (dock_dir == EAST || dock_dir == WEST)
        {
            transit_width += M.height;
            transit_height += M.width;
        }

        Type transit_path = typeof(turf_open_space_transit);
        if (travel_dir == NORTH)
            transit_path = typeof(turf_open_space_transit_north);
        else if (travel_dir == SOUTH)
            transit_path = typeof(turf_open_space_transit_south);
        else if (travel_dir == EAST)
            transit_path = typeof(turf_open_space_transit_east);
        else if (travel_dir == WEST)
            transit_path = typeof(turf_open_space_transit_west);

        var proposal = await SSmapping.request_turf_block_reservation(
            transit_width,
            transit_height,
            z_size: 1, //if this is changed the turf uncontain code below has to be updated to support multiple zs
            reservation_type: typeof(datum_turf_reservation_transit),
            turf_type_override: transit_path) as datum_turf_reservation;

        if (proposal == null)
            return FALSE;

        var bottomleft = (turf)proposal.bottom_left_turfs[1];
        // Then create a transit docking port in the middle
        DmList coords = M.return_coords(0f, 0f, dock_dir);
        /*  0------2
        *   |      |
        *   |      |
        *   |  x   |
        *   3------1
        */

        float x0 = Dm.num(coords[1]);
        float y0 = Dm.num(coords[2]);
        float x1 = Dm.num(coords[3]);
        float y1 = Dm.num(coords[4]);
        // Then we want the point closest to -infinity,-infinity
        float x2 = Math.Min(x0, x1);
        float y2 = Math.Min(y0, y1);

        // Then invert the numbers
        float transit_x = bottomleft.x + SHUTTLE_TRANSIT_BORDER + Math.Abs(x2);
        float transit_y = bottomleft.y + SHUTTLE_TRANSIT_BORDER + Math.Abs(y2);

        turf midpoint = locate(transit_x, transit_y, bottomleft.z);
        if (midpoint == null)
        {
            qdel(proposal);
            return FALSE;
        }

        var old_area = (area)midpoint.loc;
        datum_controller_subsystem_mapping.ListAssertLen(old_area.turfs_to_uncontain_by_zlevel, bottomleft.z);
        Dm.append_at(old_area.turfs_to_uncontain_by_zlevel, bottomleft.z, proposal.reserved_turfs);

        var new_area = Dm.@new<area_shuttle_transit>();
        new_area.set_parallax_movedir(travel_dir);
        new_area.contents = proposal.reserved_turfs;
        datum_controller_subsystem_mapping.ListAssertLen(new_area.turfs_by_zlevel, bottomleft.z);
        new_area.turfs_by_zlevel[bottomleft.z] = proposal.reserved_turfs;

        var new_transit_dock = Dm.@new<obj_docking_port_stationary_transit>(midpoint);
        new_transit_dock.reserved_area = proposal;
        new_transit_dock.name = Dm.text("Transit for []/[]", M.shuttle_id, M.name);
        new_transit_dock.owner = M;
        new_transit_dock.assigned_area = new_area;

        // Add 180, because ports point inwards, rather than outwards
        Waiting.NoSleep(new_transit_dock.setDir(angle2dir(dock_angle)));

        // Proposals use 2 extra hidden tiles of space, from the cordons that surround them
        transit_utilized += (proposal.width + 2) * (proposal.height + 2);
        M.assigned_transit = new_transit_dock;
        RegisterSignal(proposal, COMSIG_QDELETING, nameof(transit_space_clearing));

        return new_transit_dock;
    }

    /// Gotta manage our space brother
    // tg: code/controllers/subsystem/shuttle.dm:772
    public virtual void transit_space_clearing(datum_turf_reservation source = null)
    {
        transit_utilized -= (source.width + 2) * (source.height + 2);
    }

    // tg: code/controllers/subsystem/shuttle.dm:837
    public virtual object is_in_shuttle_bounds(atom A = null)
    {
        object current = get_area(A);
        if (current is area_shuttle && current is not area_shuttle_transit)
            return TRUE;
        foreach (obj_docking_port_mobile M in Dm.each<obj_docking_port_mobile>(mobile_docking_ports))
        {
            if (Dm.truthy(M.is_in_shuttle_bounds(A)))
                return TRUE;
        }
        return null;
    }

    // tg: code/controllers/subsystem/shuttle.dm:845
    public virtual object get_containing_shuttle(atom A = null)
    {
        DmList mobile_docking_ports_cache = mobile_docking_ports;
        for (float i = 1; i <= mobile_docking_ports_cache.len; i++)
        {
            var port = (obj_docking_port)mobile_docking_ports_cache[i];
            if (Dm.truthy(port.is_in_shuttle_bounds(A)))
                return port;
        }
        return null;
    }

    // tg: code/controllers/subsystem/shuttle.dm:852
    public virtual DmList get_containing_dock(atom A = null)
    {
        DmList dot = Dm.list();
        DmList stationary_docking_ports_cache = stationary_docking_ports;
        for (float i = 1; i <= stationary_docking_ports_cache.len; i++)
        {
            var port = (obj_docking_port)stationary_docking_ports_cache[i];
            if (Dm.truthy(port.is_in_shuttle_bounds(A)))
                Dm.append(ref dot, port);
        }
        return dot;
    }

    // tg: code/controllers/subsystem/shuttle.dm:860
    public virtual DmList get_dock_overlap(float x0 = 0, float y0 = 0, float x1 = 0, float y1 = 0, float z = 0)
    {
        DmList dot = Dm.list();
        DmList stationary_docking_ports_cache = stationary_docking_ports;
        for (float i = 1; i <= stationary_docking_ports_cache.len; i++)
        {
            var port = stationary_docking_ports_cache[i] as obj_docking_port;
            if (port == null || port.z != z)
                continue;
            DmList bounds = port.return_coords();
            DmList overlap = get_overlap(x0, y0, x1, y1, Dm.num(bounds[1]), Dm.num(bounds[2]), Dm.num(bounds[3]), Dm.num(bounds[4]));
            var xs = overlap[1] as DmList;
            var ys = overlap[2] as DmList;
            if (Dm.truthy(xs.len) && Dm.truthy(ys.len))
                dot[port] = overlap;
        }
        return dot;
    }

    // tg: code/controllers/subsystem/shuttle.dm:874
    public virtual void update_hidden_docking_ports(DmList remove_turfs = null, DmList add_turfs = null)
    {
        DmList remove_images = Dm.list();
        DmList add_images = Dm.list();

        if (remove_turfs != null)
        {
            foreach (object T in Dm.each(remove_turfs))
            {
                var L = hidden_shuttle_turfs[T] as DmList;
                if (L != null)
                    Dm.append(ref remove_images, L[1]);
            }
            Dm.remove(ref hidden_shuttle_turfs, remove_turfs);
        }

        if (add_turfs != null)
        {
            foreach (turf T in Dm.each<turf>(add_turfs))
            {
                image I;
                if (Dm.truthy(remove_images.len))
                {
                    //we can just reuse any images we are about to delete instead of making new ones
                    I = remove_images[1] as image;
                    remove_images.Cut(1, 2);
                    I.loc = T;
                }
                else
                {
                    I = image(loc: T);
                    Dm.append(ref add_images, I);
                }
                I.appearance = T.appearance;
                I.@override = TRUE;
                hidden_shuttle_turfs[T] = Dm.list(I, T.type);
            }
        }

        Dm.remove(ref hidden_shuttle_turf_images, remove_images);
        Dm.append(ref hidden_shuttle_turf_images, add_images);

        foreach (obj_machinery_computer_camera_advanced_shuttle_docker docking_computer in Dm.each<obj_machinery_computer_camera_advanced_shuttle_docker>(
            SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_camera_advanced_shuttle_docker))))
            docking_computer.update_hidden_docking_ports(remove_images, add_images);
    }

    /**
     * Loads a shuttle template and sends it to a given destination port, optionally replacing the existing shuttle
     *
     * Arguments:
     * * loading_template - The shuttle template to load
     * * destination_port - The station docking port to send the shuttle to once loaded
     * * replace - Whether to replace the shuttle or create a new one
    */
    // tg: code/controllers/subsystem/shuttle.dm:918
    public virtual async DmTask<object> action_load(datum_map_template_shuttle loading_template = null, obj_docking_port_stationary destination_port = null, object replace = null)
    {
        replace ??= FALSE;
        // Check for an existing preview
        if (preview_shuttle != null && (loading_template != preview_template))
        {
            await preview_shuttle.jumpToNullSpace();
            preview_shuttle = null;
            preview_template = null;
            // QDEL_NULL(preview_reservation) (__DEFINES/qdel.dm), expanded inline
            qdel(preview_reservation);
            preview_reservation = null;
        }

        if (preview_shuttle == null)
        {
            await load_template(loading_template);
            preview_template = loading_template;
        }

        // get the existing shuttle information, if any
        object timer = 0f;
        string mode = SHUTTLE_IDLE;
        obj_docking_port_stationary dest_dock = null;

        if (destination_port != null)
        {
            dest_dock = destination_port;
        }
        else if (existing_shuttle != null && Dm.truthy(replace))
        {
            timer = existing_shuttle.timer;
            mode = existing_shuttle.mode;
            dest_dock = existing_shuttle.get_docked() as obj_docking_port_stationary;
        }

        if (dest_dock == null)
            dest_dock = await generate_transit_dock(preview_shuttle) as obj_docking_port_stationary;

        if (dest_dock == null)
            throw Dm.CRASH(Dm.text("No dock found for preview shuttle ([]), aborting.", preview_template.name), "code/controllers/subsystem/shuttle.dm", 946);

        object result = preview_shuttle.canDock(dest_dock);
        // truthy value means that it cannot dock for some reason
        // but we can ignore the someone else docked error because we'll
        // be moving into their place shortly
        if (!Dm.eq(result, SHUTTLE_CAN_DOCK) && !Dm.eq(result, SHUTTLE_SOMEONE_ELSE_DOCKED))
            throw Dm.CRASH(Dm.text("Template shuttle [] cannot dock at [] ([]).", preview_shuttle, dest_dock, result), "code/controllers/subsystem/shuttle.dm", 953);

        if (existing_shuttle != null && Dm.truthy(replace))
            await existing_shuttle.jumpToNullSpace();

        preview_shuttle.register(replace);
        DmList force_memory = preview_shuttle.movement_force;
        preview_shuttle.movement_force = Dm.list(Dm.kv("KNOCKDOWN", 0f), Dm.kv("THROW", 0f));
        preview_shuttle.mode = SHUTTLE_PREARRIVAL; //No idle shuttle moving. Transit dock get removed if shuttle moves too long.
        await preview_shuttle.initiate_docking(dest_dock);
        preview_shuttle.movement_force = force_memory;

        object dot = preview_shuttle;

        // Shuttle state involves a mode and a timer based on world.time, so
        // plugging the existing shuttles old values in works fine.
        preview_shuttle.timer = timer;
        preview_shuttle.mode = mode;

        await preview_shuttle.postregister(Dm.truthy(replace) ? TRUE : FALSE);

        // TODO indicate to the user that success happened, rather than just
        // blanking the modification tab
        preview_shuttle = null;
        preview_template = null;
        existing_shuttle = null;
        selected = null;
        // QDEL_NULL(preview_reservation) (__DEFINES/qdel.dm), expanded inline
        qdel(preview_reservation);
        preview_reservation = null;
        return dot;
    }

    /**
     * Loads a shuttle template into the transit Z level, usually referred to elsewhere in the code as a shuttle preview.
     * Does not register the shuttle so it can't be used yet, that's handled in action_load()
     *
     * Arguments:
     * * loading_template - The shuttle template to load
     */
    // tg: code/controllers/subsystem/shuttle.dm:989
    public virtual async DmTask<object> load_template(datum_map_template_shuttle loading_template = null)
    {
        // Load shuttle template to a fresh block reservation.
        preview_reservation = await SSmapping.request_turf_block_reservation(
            loading_template.width,
            loading_template.height,
            1,
            reservation_type: typeof(datum_turf_reservation_transit)) as datum_turf_reservation;
        if (preview_reservation == null)
            throw Dm.CRASH("failed to reserve an area for shuttle template loading", "code/controllers/subsystem/shuttle.dm", 999);
        var bottom_left = (turf)preview_reservation.bottom_left_turfs[1];
        await loading_template.load(bottom_left, FALSE, FALSE); // centered = FALSE, register = FALSE
        bottom_left = (turf)turf.dm_identity(bottom_left); // TURF-IDENTITY: tg's bottom_left names the corner, whose turf the template replaced

        object affected = loading_template.get_affected_turfs(bottom_left, centered: FALSE);

        float found = 0;
        // Search the turfs for docking ports
        // - We need to find the mobile docking port because that is the heart of
        //   the shuttle.
        // - We need to check that no additional ports have slipped in from the
        //   template, because that causes unintended behaviour.
        foreach (turf affected_turfs in Dm.each<turf>(affected))
        {
            foreach (obj_docking_port port in Dm.each<obj_docking_port>((turf)turf.dm_identity(affected_turfs))) // TURF-IDENTITY: the template's turfs replaced the block's
            {
                if (port is obj_docking_port_mobile mobile)
                {
                    found++;
                    if (found > 1)
                    {
                        qdel(port, force: TRUE);
                        log_mapping(Dm.text("Shuttle Template [] has multiple mobile docking ports.", loading_template.mappath));
                    }
                    else
                    {
                        preview_shuttle = mobile;
                    }
                }
                if (port is obj_docking_port_stationary)
                    log_mapping(Dm.text("Shuttle Template [] has a stationary docking port.", loading_template.mappath));
            }
        }
        if (!Dm.truthy(found))
        {
            string msg = Dm.text("load_template(): Shuttle Template [] has no mobile docking port. Aborting import.", loading_template.mappath);
            foreach (turf T0 in Dm.each<turf>(affected))
                await ((turf)turf.dm_identity(T0)).empty();

            message_admins(msg);
            warning(msg);
            return FALSE;
        }
        //Everything fine
        await loading_template.post_load(preview_shuttle);
        return TRUE;
    }

    // tg: code/controllers/subsystem/shuttle.dm:1209  (gaps CM1: the communications console's shuttle purchases)
    public virtual object init_has_purchase_shuttle_access()
    {
        DmList has_purchase_shuttle_access = Dm.list();

        foreach (object shuttle_id in Dm.each(SSmapping.shuttle_templates))
        {
            var shuttle_template = (datum_map_template_shuttle)SSmapping.shuttle_templates[shuttle_id];
            if (shuttle_template.who_can_purchase != null)
                Dm.combine(ref has_purchase_shuttle_access, shuttle_template.who_can_purchase);
        }

        return has_purchase_shuttle_access;
    }

    /**
     * Removes the preview_shuttle from the transit Z-level
     */
    // tg: code/controllers/subsystem/shuttle.dm:1038  (gaps E3, D327: the shuttle catastrophe calls it)
    public virtual async DmTask unload_preview()
    {
        if (preview_shuttle != null)
            await preview_shuttle.jumpToNullSpace();
        preview_shuttle = null;
        if (preview_reservation != null)
        {
            // QDEL_NULL(preview_reservation) (__DEFINES/qdel.dm), expanded inline
            qdel(preview_reservation);
            preview_reservation = null;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/shuttle.dm:12  SUBSYSTEM_DEF(shuttle): GLOBAL_REAL(SSshuttle, /datum/controller/subsystem/shuttle)
    internal datum_controller_subsystem_shuttle SSshuttle;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_shuttle SSshuttle => ref TgState.Current.SSshuttle;
}
