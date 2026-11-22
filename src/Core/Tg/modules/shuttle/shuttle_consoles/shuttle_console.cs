// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/shuttle_consoles/shuttle_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D299: the ordinary shuttle console; gaps V1: the head revolutionary's one-time warning.

// tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:9
public partial class obj_machinery_computer_shuttle : obj_machinery_computer
{
    private const string SHUTTLE_CONSOLE_ACCESSDENIED = "accessdenied";
    private const string SHUTTLE_CONSOLE_ENDGAME = "endgame";
    private const string SHUTTLE_CONSOLE_RECHARGING = "recharging";
    private const string SHUTTLE_CONSOLE_INTRANSIT = "intransit";
    private const string SHUTTLE_CONSOLE_DESTINVALID = "destinvalid";
    private const string SHUTTLE_CONSOLE_SUCCESS = "success";
    private const string SHUTTLE_CONSOLE_ERROR = "error";

    /// Head revolutionaries who have been warned against leaving the station (gaps V1)
    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:31  var/static/list/dumb_rev_heads = list() (a type's static, rules 10.6)
    internal static ref DmList dumb_rev_heads => ref TgState.Current.obj_machinery_computer_shuttle__dumb_rev_heads;

    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:39
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_nav_computer_icon), Dm.resource("icons/effects/nav_computer_indicators.dmi"), "computer", FALSE));
        Waiting.NoSleep(connect_to_shuttle(mapload, SSshuttle?.get_containing_shuttle(this) as obj_docking_port_mobile));
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:44
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        // IS_HEAD_REVOLUTIONARY(user) (__DEFINES/antagonists.dm:326), expanded inline (gaps V1)
        if (Dm.truthy(is_station_level(user.z)) && user.mind != null && Dm.truthy(user.mind.has_antag_datum(typeof(datum_antagonist_rev_head))) && !Dm.@in(user.mind, dumb_rev_heads)) //Rev heads will get a one-time warning that they shouldn't leave
        {
            to_chat(user, span_warning(I18n.T("You get a feeling that leaving the station might be a REALLY dumb idea...")));
            Dm.append(ref dumb_rev_heads, user.mind);
            return dot;
        }
        if (HAS_TRAIT(user, TRAIT_FORBID_MINING_SHUTTLE_CONSOLE_OUTSIDE_STATION) && !Dm.truthy(is_station_level(user.z))) //Free golems and other mobs with this trait will not be able to use the shuttle from outside the station Z
        {
            to_chat(user, span_warning("You get the feeling you shouldn't mess with this."));
            return dot;
        }
        if (!Dm.truthy(user.can_read(this, reading_check_flags: READING_CHECK_LITERACY))) //Illiterate mobs which aren't otherwise blocked from using computers will send the shuttle to a random valid destination
        {
            to_chat(user, span_warning("You start mashing buttons at random!"));
            if (Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
            {
                DmList dest_list = get_valid_destinations();
                if (!Dm.truthy(dest_list.len)) //No valid destinations
                {
                    to_chat(user, span_warning("The console shows a flashing error message, but you can't comprehend it."));
                    return dot;
                }
                var random_destination = (DmList)pick(dest_list);
                if (Dm.eq(await send_shuttle(random_destination["id"], user), SHUTTLE_CONSOLE_SUCCESS))
                    return dot;
                to_chat(user, span_warning("The console shows a flashing error message, but you can't comprehend it."));
            }
            return dot;
        }
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ShuttleConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:73
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        var mobile_docking_port = SSshuttle.getShuttle(shuttleId) as obj_docking_port_mobile;
        data["docked_location"] = mobile_docking_port != null ? mobile_docking_port.get_status_text_tgui() : "Unknown";
        data["locations"] = Dm.list();
        data["locked"] = locked;
        data["authorization_required"] = admin_controlled;
        data["timer_str"] = mobile_docking_port != null ? mobile_docking_port.getTimerStr() : "00:00";
        data["destination"] = destination;
        if (mobile_docking_port == null)
        {
            data["status"] = "Missing";
            return data;
        }
        if (Dm.truthy(admin_controlled))
            data["status"] = "Unauthorized Access";
        else if (Dm.truthy(locked))
            data["status"] = "Locked";
        else if (Dm.eq(mobile_docking_port.mode, SHUTTLE_IGNITING))
            data["status"] = "Igniting";
        else if (Dm.eq(mobile_docking_port.mode, SHUTTLE_IDLE))
            data["status"] = "Idle";
        else if (Dm.eq(mobile_docking_port.mode, SHUTTLE_RECHARGING))
            data["status"] = "Recharging";
        else
            data["status"] = "In Transit";
        DmList locations = get_valid_destinations();
        data["locations"] = locations;
        if (length(locations) == 1)
        {
            foreach (DmList location in Dm.each<DmList>(locations))
            {
                destination = location["id"];
                data["destination"] = destination;
            }
        }
        if (!Dm.truthy(length(locations)))
        {
            data["locked"] = TRUE;
            data["status"] = "Locked";
        }
        if (!Dm.truthy(mobile_docking_port.canMove()))
        {
            data["locked"] = TRUE;
            data["status"] = "Immobile";
        }
        return data;
    }

    /**
     * Checks if we are allowed to launch the shuttle
     *
     * Arguments:
     * * user - The mob trying to initiate the launch
     */
    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:118
    public virtual async DmTask<object> launch_check(mob user = null)
    {
        if (!Dm.truthy(await allowed__proc(user))) //Normally this is already checked via interaction code but some cases may skip that so check it explicitly here (illiterates launching randomly)
            return FALSE;
        return TRUE;
    }

    /**
     * Returns a list of currently valid destinations for this shuttle console,
     * taking into account its list of allowed destinations, their current state, and the shuttle's current location
    **/
    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:127
    public virtual DmList get_valid_destinations()
    {
        DmList destination_list = params2list(possible_destinations);
        var mobile_docking_port = (obj_docking_port_mobile)SSshuttle.getShuttle(shuttleId);
        obj_docking_port_stationary current_destination = mobile_docking_port.destination;
        DmList valid_destinations = Dm.list();
        foreach (obj_docking_port_stationary stationary_docking_port in Dm.each<obj_docking_port_stationary>(SSshuttle.stationary_docking_ports))
        {
            if (!Dm.@in(stationary_docking_port.port_destinations, destination_list))
                continue;
            if (!Dm.truthy(mobile_docking_port.check_dock(stationary_docking_port, silent: TRUE)))
                continue;
            if (stationary_docking_port == current_destination)
                continue;
            Dm.append(ref valid_destinations, Dm.list(Dm.list(Dm.kv("id", stationary_docking_port.shuttle_id), Dm.kv("name", stationary_docking_port.name))));
        }
        return valid_destinations;
    }

    /**
     * Attempts to send the linked shuttle to dest_id, checking various sanity checks to see if it can move or not
     *
     * Arguments:
     * * dest_id - The ID of the stationary docking port to send the shuttle to
     * * user - The mob that used the console
     */
    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:153
    public virtual async DmTask<object> send_shuttle(object dest_id = null, mob user = null)
    {
        if (!Dm.truthy(await launch_check(user)))
            return SHUTTLE_CONSOLE_ACCESSDENIED;
        var shuttle_port = (obj_docking_port_mobile)SSshuttle.getShuttle(shuttleId);
        if (Dm.eq(shuttle_port.launch_status, ENDGAME_LAUNCHED))
            return SHUTTLE_CONSOLE_ENDGAME;
        if (Dm.truthy(no_destination_swap))
        {
            if (Dm.eq(shuttle_port.mode, SHUTTLE_RECHARGING))
                return SHUTTLE_CONSOLE_RECHARGING;
            if (!Dm.eq(shuttle_port.mode, SHUTTLE_IDLE))
                return SHUTTLE_CONSOLE_INTRANSIT;
        }
        //check to see if the dest_id passed from tgui is actually a valid destination
        bool validdest = false;
        foreach (DmList dest_data in Dm.each<DmList>(get_valid_destinations()))
        {
            if (Dm.eq(dest_data["id"], dest_id))
            {
                validdest = true; //Found our destination, we can skip ahead now
                break;
            }
        }
        if (!validdest) //Didn't find our destination in the list of valid destinations, something bad happening
        {
            if (user?.client != null)
            {
                log_admin(Dm.text("Warning: possible href exploit by [] - Attempted to dock [] to illegal target location \"[]\"", key_name(user), this, dest_id));
                message_admins(Dm.text("Warning: possible href exploit by [] - Attempted to dock [] to illegal target location \"[]\"", key_name(user), this, dest_id));
            }
            else
            {
                stack_trace(Dm.text("[] tried to send the shuttle [] to the target location [], but the target location was not found in the list of valid destinations.", user, this, dest_id), "code/modules/shuttle/shuttle_consoles/shuttle_console.dm", 176);
            }
            return SHUTTLE_CONSOLE_DESTINVALID;
        }
        if (Dm.eq(await SSshuttle.moveShuttle(shuttleId, dest_id, TRUE), DOCKING_SUCCESS))
        {
            await say("Shuttle departing. Please stand away from the doors.");
            log_shuttle(Dm.text("[] has sent shuttle \"[]\" towards \"[]\", using [].", key_name(user), shuttleId, dest_id, this));
            return SHUTTLE_CONSOLE_SUCCESS;
        }
        return SHUTTLE_CONSOLE_ERROR;
    }

    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:186
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = usr;
        if (!Dm.truthy(await allowed__proc(user)))
        {
            to_chat(user, span_danger("Access denied."));
            return null;
        }

        switch (Dm.str(action))
        {
            case "move":
                switch (Dm.str(await send_shuttle(@params["shuttle_id"], user))) //Try to send the shuttle, tell the user what happened
                {
                    case SHUTTLE_CONSOLE_ACCESSDENIED:
                        to_chat(user, span_warning("Access denied."));
                        return null;
                    case SHUTTLE_CONSOLE_ENDGAME:
                        to_chat(user, span_warning("You've already escaped. Never going back to that place again!"));
                        return null;
                    case SHUTTLE_CONSOLE_RECHARGING:
                        to_chat(user, span_warning("Shuttle engines are not ready for use."));
                        return null;
                    case SHUTTLE_CONSOLE_INTRANSIT:
                        to_chat(user, span_warning("Shuttle already in transit."));
                        return null;
                    case SHUTTLE_CONSOLE_DESTINVALID:
                        to_chat(user, span_warning("Invalid destination."));
                        return null;
                    case SHUTTLE_CONSOLE_ERROR:
                        to_chat(user, span_warning("Unable to comply."));
                        return null;
                    case SHUTTLE_CONSOLE_SUCCESS:
                        return TRUE; //No chat message here because the send_shuttle proc makes the console itself speak
                }
                break;
            case "set_destination":
                object target_destination = @params["destination"];
                if (Dm.truthy(target_destination))
                {
                    destination = target_destination;
                    return TRUE;
                }
                break;
            case "request":
                if (!(request_cooldown <= Dm.world.time)) // COOLDOWN_FINISHED, inline (rules 11.1)
                {
                    to_chat(user, span_warning("CentCom is still processing last authorization request!"));
                    return null;
                }
                request_cooldown = Dm.world.time + 1 * MINUTES; // COOLDOWN_START, inline
                to_chat(user, span_notice("Your request has been received by CentCom."));
                message_admins(Dm.text("SHUTTLE: [] is requesting to move or unlock the shuttle [].", key_name(user), shuttleId));
                return TRUE;
        }
        return null;
    }

    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:231
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        req_access = Dm.list();
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, "id checking system fried");
        return TRUE;
    }

    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:239
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        if (!Dm.truthy(mapload))
            return null;
        if (port == null)
            return null;
        //Remove old custom port id and ";;"
        float find_old = findtextEx(possible_destinations, Dm.text("[]_custom", shuttleId));
        if (Dm.truthy(find_old))
            possible_destinations = replacetext(replacetextEx(possible_destinations, Dm.text("[]_custom", shuttleId), ""), ";;", ";");
        shuttleId = port.shuttle_id;
        possible_destinations += Dm.text(";[]_custom", port.shuttle_id);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/shuttle/shuttle_consoles/shuttle_console.dm:31  /obj/machinery/computer/shuttle's var/static/list/dumb_rev_heads = list()
    internal DmList obj_machinery_computer_shuttle__dumb_rev_heads = Dm.list();
}
