// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/aux_base.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The auxiliary base: its management console (the AuxBaseConsole window: the base's drop to a landing zone, a blind drop to a random spot
// of the mining level, its turrets), the landing field designator a miner deploys on Lavaland to set the landing zone, the base's mobile
// port (it scrapes away the rock it lands on), the station's public mining dock and the mining shuttle beacon, which marks a landing
// spot for the mining shuttle beside the landed base and turns the base's console into a mining shuttle console.

///Mining Base////

// tg: code/modules/mining/aux_base.dm:14
public partial class obj_machinery_computer_auxiliary_base : obj_machinery_computer
{
    // tg: code/modules/mining/aux_base.dm:3-8  #define ZONE_SET 0 ... BAD_LAYER 5 (file-local)
    internal const float ZONE_SET = 0;
    internal const float BAD_ZLEVEL = 1;
    internal const float BAD_AREA = 2;
    internal const float BAD_COORDS = 3;
    internal const float BAD_TURF = 4;
    internal const float BAD_LAYER = 5;

    /// ID of the currently selected destination of the attached base
    // tg: code/modules/mining/aux_base.dm:33  (a docking port's id, a text: the converter typed it as a path)
    public object destination;

    // tg: code/modules/mining/aux_base.dm:15  (the generated constructor, kept here: the hand-declared var above puts the class in hand mode)
    public obj_machinery_computer_auxiliary_base()
    {
        name = "auxiliary base management console";
        desc = "Allows a deployable expedition base to be dropped from the station to a designated mining location. It can also interface with the mining shuttle at the landing site if a mobile beacon is also deployed.";
        icon = Dm.resource("icons/obj/machines/wallmounts.dmi");
        icon_state = "pod_off";
        icon_keyboard = null;
        icon_screen = "pod_on";
        circuit = typeof(obj_item_circuitboard_computer_auxiliary_base);
        density = 0;
    }

    // tg: code/modules/mining/aux_base.dm:22
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        req_one_access = Dm.list("aux_base", "command"); // tg: code/modules/mining/aux_base.dm:22
    }

    // tg: code/modules/mining/aux_base.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "NT_AUX"));
        return dot;
    }

    // tg: code/modules/mining/aux_base.dm:45
    public override object Destroy(float? force = null) // Shouldn't be destroyable... but just in case
    {
        LAZYCLEARLIST(turrets);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/aux_base.dm:49
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AuxBaseConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/mining/aux_base.dm:56
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList options = params2list(possible_destinations);
        var M = SSshuttle.getShuttle(shuttleId) as obj_docking_port_mobile;
        data["type"] = Dm.eq(shuttleId, "colony_drop") ? "base" : "shuttle";
        data["docked_location"] = M != null ? M.get_status_text_tgui() : "Unknown";
        data["locations"] = Dm.list();
        data["locked"] = FALSE;
        data["timer_str"] = M != null ? M.getTimerStr() : "00:00";
        data["destination"] = destination;
        data["blind_drop"] = blind_drop_ready;
        data["turrets"] = Dm.list();
        foreach (datum_weakref turret_ref in Dm.each<datum_weakref>(turrets?.Copy()))
        {
            var base_turret = turret_ref.resolve() as obj_machinery_porta_turret_aux_base;
            if (base_turret == null) // null or invalid in turrets list? axe it
            {
                LAZYREMOVE(ref turrets, turret_ref);
                continue;
            }

            float turret_integrity = Math.Max((Dm.num(base_turret.get_integrity()) - base_turret.integrity_failure * base_turret.max_integrity) / (base_turret.max_integrity - base_turret.integrity_failure * max_integrity) * 100, 0);
            string turret_status;
            if (Dm.truthy(Dm.bitand(base_turret.machine_stat, BROKEN)))
                turret_status = "ERROR";
            else if (!Dm.truthy(base_turret.on))
                turret_status = "Disabled";
            else if (Dm.truthy(base_turret.raised))
                turret_status = "Firing";
            else
                turret_status = "All Clear";
            DmList turret_data = Dm.list(
                Dm.kv("name", base_turret.name),
                Dm.kv("integrity", turret_integrity),
                Dm.kv("status", turret_status),
                Dm.kv("direction", dir2text(get_dir(this, base_turret))),
                Dm.kv("distance", get_dist(this, base_turret)),
                Dm.kv("ref", REF(base_turret)));
            var turret_list = (DmList)data["turrets"];
            Dm.append(ref turret_list, Dm.list(turret_data));
        }
        if (M == null)
        {
            data["status"] = "Missing";
            return data;
        }
        if (Dm.eq(M.mode, SHUTTLE_IGNITING))
            data["status"] = "Igniting";
        else if (Dm.eq(M.mode, SHUTTLE_IDLE))
            data["status"] = "Idle";
        else if (Dm.eq(M.mode, SHUTTLE_RECHARGING))
            data["status"] = "Recharging";
        else
            data["status"] = "In Transit";
        foreach (obj_docking_port_stationary S in Dm.each<obj_docking_port_stationary>(SSshuttle.stationary_docking_ports))
        {
            if (!Dm.truthy(options.Find(S.port_destinations)))
                continue;
            if (!Dm.truthy(M.check_dock(S, silent: TRUE)))
                continue;
            DmList location_data = Dm.list(
                Dm.kv("id", S.shuttle_id),
                Dm.kv("name", S.name));
            var locations = (DmList)data["locations"];
            Dm.append(ref locations, Dm.list(location_data));
        }
        if (length(data["locations"]) == 1)
        {
            foreach (DmList location in Dm.each<DmList>(data["locations"]))
            {
                destination = location["id"];
                data["destination"] = destination;
            }
        }
        if (!Dm.truthy(length(data["locations"])))
        {
            data["locked"] = TRUE;
            data["status"] = "Locked";
        }
        return data;
    }

    /**
     * Checks if we are allowed to launch the base
     *
     * Arguments:
     * * user - The mob trying to initiate the launch
     */
    // tg: code/modules/mining/aux_base.dm:130
    public virtual object launch_check(mob user = null)
    {
        if (!Dm.truthy(is_station_level(z)) && Dm.eq(shuttleId, "colony_drop"))
        {
            to_chat(user, span_warning(I18n.T("You can't move the base again!")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mining/aux_base.dm:136
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await allowed__proc(usr)))
        {
            to_chat(usr, span_danger(I18n.T("Access denied.")));
            return null;
        }

        switch (Dm.str(action))
        {
            case "move":
            {
                if (!Dm.truthy(launch_check(usr)))
                    return null;
                object shuttle_error = await SSshuttle.moveShuttle(shuttleId, @params["shuttle_id"], 1);
                if (Dm.truthy(launch_warning))
                {
                    await say(I18n.T("Launch sequence activated! Prepare for drop!!"), spans: Dm.list(SPAN_DANGER));
                    playsound(loc, Dm.resource("sound/machines/warning-buzzer.ogg"), 70, FALSE);
                    launch_warning = FALSE;
                    blind_drop_ready = FALSE;
                    log_shuttle(Dm.text("[] has launched the auxiliary base.", key_name(usr)));
                    return TRUE;
                }
                else if (!Dm.truthy(shuttle_error))
                {
                    await say(I18n.T("Shuttle request uploaded. Please stand away from the doors."));
                }
                else
                {
                    await say(I18n.T("Shuttle interface failed."));
                }
                break;
            }
            case "random":
            {
                if (Dm.truthy(possible_destinations))
                    return null;
                usr.changeNext_move(CLICK_CD_RAPID); //Anti-spam
                DmList all_mining_turfs = Dm.list();
                foreach (object z_level in Dm.each(SSmapping.levels_by_trait(ZTRAIT_MINING)))
                    Dm.append(ref all_mining_turfs, Z_TURFS(Dm.num(z_level)));
                var LZ = pick(all_mining_turfs) as turf; //Pick a random mining Z-level turf
                if (!ismineralturf(LZ) && !isasteroidturf(LZ))
                {
                    //Find a suitable mining turf. Reduces chance of landing in a bad area
                    to_chat(usr, span_warning(I18n.T("Landing zone scan failed. Please try again.")));
                    return null;
                }
                if (!Dm.eq(await set_landing_zone(LZ, usr), ZONE_SET))
                {
                    to_chat(usr, span_warning(I18n.T("Landing zone unsuitable. Please recalculate.")));
                    return null;
                }
                blind_drop_ready = FALSE;
                return TRUE;
            }
            case "set_destination":
            {
                object target_destination = @params["destination"];
                if (!Dm.truthy(target_destination))
                    return null;
                destination = target_destination;
                return TRUE;
            }
            case "turrets_power":
            {
                foreach (datum_weakref turret_ref in Dm.each<datum_weakref>(turrets?.Copy()))
                {
                    var base_turret = turret_ref.resolve() as obj_machinery_porta_turret_aux_base;
                    if (base_turret == null) // null or invalid in turrets list
                    {
                        LAZYREMOVE(ref turrets, turret_ref);
                        continue;
                    }

                    base_turret.toggle_on();
                }
                return TRUE;
            }
            case "single_turret_power":
            {
                var base_turret = Dm.locate_ref(@params["single_turret_power"]) as obj_machinery_porta_turret_aux_base;
                if (base_turret == null || !Dm.@in(WEAKREF(base_turret), turrets))
                    return null;

                base_turret.toggle_on();
                return TRUE;
            }
        }
        return null;
    }

    // tg: code/modules/mining/aux_base.dm:200
    public virtual void set_mining_mode()
    {
        if (Dm.truthy(is_mining_level(z))) //The console switches to controlling the mining shuttle once landed.
        {
            req_one_access = Dm.list();
            shuttleId = "mining"; //The base can only be dropped once, so this gives the console a new purpose.
            possible_destinations = "mining_home;mining_away;landing_zone_dock;mining_public";
        }
    }

    // tg: code/modules/mining/aux_base.dm:206  var/static/list/disallowed_turf_types (rules 10.6)
    private static DmList set_landing_zone__disallowed_turf_types => TgState.Current.obj_machinery_computer_auxiliary_base__set_landing_zone__disallowed_turf_types ??= zebra_typecacheof(Dm.list(
        Dm.kv(typeof(turf_closed), TRUE),
        Dm.kv(typeof(turf_open_lava), TRUE),
        Dm.kv(typeof(turf_open_indestructible), TRUE),
        Dm.kv(typeof(turf_closed_mineral), FALSE))) as DmList;

    // tg: code/modules/mining/aux_base.dm:206
    public virtual async DmTask<object> set_landing_zone(turf T = null, mob user = null, object no_restrictions = null)
    {
        var base_dock = Dm.locate<obj_docking_port_mobile_auxiliary_base>(SSshuttle.mobile_docking_ports);
        if (base_dock == null) //Not all maps have an Aux base. This object is useless in that case.
        {
            to_chat(user, span_warning(I18n.T("This station is not equipped with an auxiliary base. Please contact your Nanotrasen contractor.")));
            return null;
        }
        if (!Dm.truthy(no_restrictions))
        {
            DmList disallowed_turf_types = set_landing_zone__disallowed_turf_types;

            if (!Dm.truthy(is_mining_level(T.z)))
                return BAD_ZLEVEL;

            DmList colony_turfs = base_dock.return_ordered_turfs(T.x, T.y, T.z, base_dock.dir);
            for (int i = 1; i <= colony_turfs.len; i++)
            {
                // CHECK_TICK (__DEFINES/_tick.dm:21), awaited in a suspending proc
                if (TICK_CHECK())
                    await stoplag();
                var place = colony_turfs[i] as turf;
                if (place == null)
                    return BAD_COORDS;
                if (place.loc is area_icemoon_surface)
                    return BAD_LAYER;
                if (!(place.loc is area_lavaland_surface) && !(place.loc is area_icemoon_underground))
                    return BAD_AREA;
                if (Dm.truthy(disallowed_turf_types[place.type]))
                    return BAD_TURF;
            }
        }

        var A = get_area(T) as area;

        var landing_zone = Dm.@new<obj_docking_port_stationary>(T);
        landing_zone.shuttle_id = Dm.text("colony_drop([])", REF(this));
        landing_zone.port_destinations = Dm.text("colony_drop([])", REF(this));
        landing_zone.name = Dm.text("Landing Zone ([], [])", T.x, T.y);
        landing_zone.dwidth = base_dock.dwidth;
        landing_zone.dheight = base_dock.dheight;
        landing_zone.width = base_dock.width;
        landing_zone.height = base_dock.height;
        await landing_zone.setDir(base_dock.dir);
        landing_zone.area_type = A.type;

        possible_destinations = Dm.text("[][];", possible_destinations, landing_zone.shuttle_id);

        //Serves as a nice mechanic to people get ready for the launch.
        minor_announce(I18n.T("Auxiliary base landing zone coordinates locked in for []. Launch command now available!", A));
        to_chat(user, span_notice(I18n.T("Landing zone set.")));
        return ZONE_SET;
    }
}

// tg: code/modules/mining/aux_base.dm:257
public partial class obj_item_assault_pod_mining : obj_item_assault_pod
{
    // tg: code/modules/mining/aux_base.dm:270
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(setting))
            return null;

        to_chat(user, span_notice(I18n.T("You begin setting the landing zone parameters...")));
        setting = TRUE;
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: user))) //You get a few seconds to cancel if you do not want to drop there.
        {
            setting = FALSE;
            return null;
        }
        setting = FALSE;

        turf T = get_turf(user);
        obj_machinery_computer_auxiliary_base AB = null;

        foreach (obj_machinery_computer_auxiliary_base A in Dm.each<obj_machinery_computer_auxiliary_base>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_auxiliary_base))))
        {
            if (Dm.truthy(is_station_level(A.z)))
            {
                AB = A;
                break;
            }
        }
        if (AB == null)
        {
            to_chat(user, span_warning(I18n.T("No auxiliary base console detected.")));
            return null;
        }

        object result = await AB.set_landing_zone(T, user, no_restrictions);
        if (result == null)
            return null;
        switch (Dm.num(result))
        {
            case obj_machinery_computer_auxiliary_base.ZONE_SET:
                qdel(this);
                break;
            case obj_machinery_computer_auxiliary_base.BAD_ZLEVEL:
                to_chat(user, span_warning(I18n.T("This uplink can only be used in a designed mining zone.")));
                break;
            case obj_machinery_computer_auxiliary_base.BAD_AREA:
                to_chat(user, span_warning(I18n.T("Unable to acquire a targeting lock. Find an area clear of structures or entirely within one.")));
                break;
            case obj_machinery_computer_auxiliary_base.BAD_COORDS:
                to_chat(user, span_warning(I18n.T("Location is too close to the edge of the station's scanning range. Move several paces away and try again.")));
                break;
            case obj_machinery_computer_auxiliary_base.BAD_TURF:
                to_chat(user, span_warning(I18n.T("The landing zone contains turfs unsuitable for a base. Make sure you've removed all walls and dangerous terrain from the landing zone.")));
                break;
            case obj_machinery_computer_auxiliary_base.BAD_LAYER:
                to_chat(user, span_warning(I18n.T("This area is not hazardous enough to justify an auxiliary base. Try again on a deeper layer.")));
                break;
        }
        return null;
    }
}

// tg: code/modules/mining/aux_base.dm:312
[DmPath("/obj/docking_port/mobile/auxiliary_base")]
public partial class obj_docking_port_mobile_auxiliary_base : obj_docking_port_mobile
{
    // tg: code/modules/mining/aux_base.dm:316
    public override async DmTask takeoff(DmList old_turfs = null, DmList new_turfs = null, DmList moved_atoms = null, object rotation = null, object movement_direction = null, obj_docking_port_stationary old_dock = null, area fallback_area = null)
    {
        foreach (turf place in Dm.each<turf>(new_turfs.Copy()))
        {
            if (ismineralturf(place))
                await place.ScrapeAway();
        }
        await base.takeoff(old_turfs, new_turfs, moved_atoms, rotation, movement_direction, old_dock, fallback_area);
    }
}

// tg: code/modules/mining/aux_base.dm:332
public partial class obj_structure_mining_shuttle_beacon : obj_structure
{
    // tg: code/modules/mining/aux_base.dm:345
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(anchored))
        {
            to_chat(user, span_warning(I18n.T("Landing zone already set.")));
            return dot;
        }

        if (Dm.truthy(anti_spam_cd))
        {
            to_chat(user, span_warning(I18n.T("[] is currently recalibrating. Please wait.", this)));
            return dot;
        }

        anti_spam_cd = 1;
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_cooldown)), 5 * SECONDS, file: "code/modules/mining/aux_base.dm", line: 357);

        turf landing_spot = get_turf(this);

        if (!Dm.truthy(is_mining_level(landing_spot.z)))
        {
            to_chat(user, span_warning(I18n.T("This device is only to be used in a mining zone.")));
            return dot;
        }
        obj_machinery_computer_auxiliary_base aux_base_console = null;
        foreach (obj_machinery_computer_auxiliary_base ABC in Dm.each<obj_machinery_computer_auxiliary_base>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_auxiliary_base))))
        {
            if (get_dist(landing_spot, ABC) <= console_range)
            {
                aux_base_console = ABC;
                break;
            }
        }
        if (aux_base_console == null) //Needs to be near the base to serve as its dock and configure it to control the mining shuttle.
        {
            to_chat(user, span_warning(I18n.T("The auxiliary base's console must be within [] meters in order to interface.", console_range)));
            return dot;
        }

        //Mining shuttles may not be created equal, so we find the map's shuttle dock and size accordingly.
        foreach (object S in Dm.each(SSshuttle.stationary_docking_ports))
        {
            var SM = (obj_docking_port_stationary)S; //SM is declared outside so it can be checked for null
            if (Dm.eq(SM.shuttle_id, "mining_home") || Dm.eq(SM.shuttle_id, "mining_away"))
            {
                var A = get_area(landing_spot) as area;

                Mport = Dm.@new<obj_docking_port_stationary>(landing_spot);
                Mport.shuttle_id = "landing_zone_dock";
                Mport.port_destinations = "landing_zone_dock";
                Mport.name = "auxiliary base landing site";
                Mport.dwidth = SM.dwidth;
                Mport.dheight = SM.dheight;
                Mport.width = SM.width;
                Mport.height = SM.height;
                await Mport.setDir(dir);
                Mport.area_type = A.type;

                break;
            }
        }
        if (Mport == null)
        {
            to_chat(user, span_warning(I18n.T("This station is not equipped with an appropriate mining shuttle. Please contact Nanotrasen Support.")));
            return dot;
        }

        obj_docking_port_mobile mining_shuttle = null;
        DmList landing_turfs = Dm.list(); //List of turfs where the mining shuttle may land.
        foreach (obj_docking_port_mobile MS in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports))
        {
            if (!Dm.eq(MS.shuttle_id, "mining"))
                continue;
            mining_shuttle = MS;
            landing_turfs = mining_shuttle.return_ordered_turfs(x, y, z, dir);
            break;
        }

        if (mining_shuttle == null) //Not having a mining shuttle is a map issue
        {
            to_chat(user, span_warning(I18n.T("No mining shuttle signal detected. Please contact Nanotrasen Support.")));
            SSshuttle.stationary_docking_ports.Remove(Mport);
            qdel(Mport);
            return dot;
        }

        for (int i = 1; i <= landing_turfs.len; i++) //You land NEAR the base, not IN it.
        {
            var L = landing_turfs[i] as turf;
            if (L == null) //This happens at map edges
            {
                to_chat(user, span_warning(I18n.T("Unable to secure a valid docking zone. Please try again in an open area near, but not within the auxiliary mining base.")));
                SSshuttle.stationary_docking_ports.Remove(Mport);
                qdel(Mport);
                return dot;
            }
            if (get_area(L) is area_shuttle_auxiliary_base)
            {
                to_chat(user, span_warning(I18n.T("The mining shuttle must not land within the mining base itself.")));
                SSshuttle.stationary_docking_ports.Remove(Mport);
                qdel(Mport);
                return dot;
            }
        }

        if (!Dm.eq(mining_shuttle.canDock(Mport), SHUTTLE_CAN_DOCK))
        {
            to_chat(user, span_warning(I18n.T("Unable to secure a valid docking zone. Please try again in an open area near, but not within the auxiliary mining base.")));
            SSshuttle.stationary_docking_ports.Remove(Mport);
            qdel(Mport);
            return dot;
        }

        aux_base_console.set_mining_mode(); //Lets the colony park the shuttle there, now that it has a dock.
        to_chat(user, span_notice(I18n.T("Mining shuttle calibration successful! Shuttle interface available at base console.")));
        await set_anchored(TRUE); //Locks in place to mark the landing zone.
        playsound(loc, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);
        log_shuttle(Dm.text("[] has registered the mining shuttle beacon at [].", key_name(usr), COORD(landing_spot)));
        return dot;
    }

    // tg: code/modules/mining/aux_base.dm:437
    public virtual void clear_cooldown()
    {
        anti_spam_cd = 0;
    }

    // tg: code/modules/mining/aux_base.dm:440
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_hand(user); //So borgies can help
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/aux_base.dm:211  set_landing_zone's var/static/list/disallowed_turf_types
    internal DmList obj_machinery_computer_auxiliary_base__set_landing_zone__disallowed_turf_types;
}
