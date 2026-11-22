// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/requests_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G3 (D291): the file's global lists and the console's init and teardown (its department and name, the department lists,
// the RETA registration, the wall mount), its light, screen overlays, examine, panel and wiring tools and ID or stamp
// authentication, so Meta's request console helpers fill it as tg's do. Gaps SC1 (D333): the rest of the file, whole: the
// RequestsConsole window (ui_interact, ui_act, ui_data, ui_static_data), the department messages over the subspace relays
// (send_message, create_message), the emergencies with their RETA grants, the station announcements, and the two announcement
// config entries with procs (rc_new_message has only vars, generated). The wall frame is vars only (generated). The var blocks
// are generated but for `area` (see below).

public partial class datum_controller_global_vars
{
    // tg: code/game/machinery/requests_console.dm:1  GLOBAL_LIST_EMPTY(req_console_assistance)
    public DmList req_console_assistance;

    // tg: code/game/machinery/requests_console.dm:1  (GLOBAL_MANAGED(req_console_assistance, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreq_console_assistance()
    {
        req_console_assistance = Dm.list();
        Dm.append(ref gvars_datum_init_order, "req_console_assistance");
    }

    // tg: code/game/machinery/requests_console.dm:2  GLOBAL_LIST_EMPTY(req_console_supplies)
    public DmList req_console_supplies;

    // tg: code/game/machinery/requests_console.dm:2  (GLOBAL_MANAGED(req_console_supplies, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreq_console_supplies()
    {
        req_console_supplies = Dm.list();
        Dm.append(ref gvars_datum_init_order, "req_console_supplies");
    }

    // tg: code/game/machinery/requests_console.dm:3  GLOBAL_LIST_EMPTY(req_console_information)
    public DmList req_console_information;

    // tg: code/game/machinery/requests_console.dm:3  (GLOBAL_MANAGED(req_console_information, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreq_console_information()
    {
        req_console_information = Dm.list();
        Dm.append(ref gvars_datum_init_order, "req_console_information");
    }

    // tg: code/game/machinery/requests_console.dm:4  GLOBAL_LIST_EMPTY(req_console_all)
    public DmList req_console_all;

    // tg: code/game/machinery/requests_console.dm:4  (GLOBAL_MANAGED(req_console_all, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreq_console_all()
    {
        req_console_all = Dm.list();
        Dm.append(ref gvars_datum_init_order, "req_console_all");
    }

    // tg: code/game/machinery/requests_console.dm:5  GLOBAL_LIST_EMPTY(req_console_ckey_departments)
    public DmList req_console_ckey_departments;

    // tg: code/game/machinery/requests_console.dm:5  (GLOBAL_MANAGED(req_console_ckey_departments, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreq_console_ckey_departments()
    {
        req_console_ckey_departments = Dm.list();
        Dm.append(ref gvars_datum_init_order, "req_console_ckey_departments");
    }
}

// tg: code/game/machinery/requests_console.dm:13
public partial class obj_machinery_requests_console : obj_machinery
{
    // GAP(types): tg's `var/area/area` (:23) would be generated as a Type (rules 3.2 V3, as the APC's, apc_main.cs), which
    // the console's area instance does not fit: the hand-written member keeps tg's declared type and the generator leaves it
    // out. A hand-written var makes the class's var block hand-written (types-converter.md 3.2, "Hand"): the constructor with
    // tg's overrides (:13-22) is here; the other vars stay generated.

    /// Reference to our area
    // tg: code/game/machinery/requests_console.dm:23  (hand-written: see the GAP(types) above)
    public area area;

    // tg: code/game/machinery/requests_console.dm:13
    public obj_machinery_requests_console()
    {
        name = "requests console";
        desc = "A console intended to send requests to different departments on the station.";
        icon = Dm.resource("icons/obj/machines/wallmounts.dmi");
        icon_state = "req_comp_off";
        base_icon_state = "req_comp";
        active_power_usage = BASE_MACHINE_ACTIVE_CONSUMPTION * 0.15f;
        max_integrity = 300;
        armor_type = typeof(datum_armor_machinery_requests_console);
    }

    // tg: code/game/machinery/requests_console.dm:63
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }
        Waiting.NoSleep(set_light(1.5f, 0.7f, "#34D352")); //green light
        return dot;
    }

    // tg: code/game/machinery/requests_console.dm:70
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(open))
            Dm.append(ref dot, span_notice(I18n.T("It looks like you can pry open the panel with <b>a crowbar</b>.")));
        else
            Dm.append(ref dot, span_warning(I18n.T("The panel was pried open, you can close it with <b>a crowbar</b>.")));

        if (Dm.truthy(hack_state))
            Dm.append(ref dot, span_warning(I18n.T("The console seems to have been tampered with!")));
        return dot;
    }

    // tg: code/game/machinery/requests_console.dm:80
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(open))
            Dm.append(ref dot, mutable_appearance(icon, "req_comp_open"));

        if (Dm.truthy(open) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;

        string screen_state;

        if (Dm.truthy(emergency) || Dm.eq(new_message_priority, REQ_EXTREME_MESSAGE_PRIORITY))
            screen_state = Dm.text("[]3", base_icon_state);
        else if (Dm.eq(new_message_priority, REQ_HIGH_MESSAGE_PRIORITY))
            screen_state = Dm.text("[]2", base_icon_state);
        else if (Dm.eq(new_message_priority, REQ_NORMAL_MESSAGE_PRIORITY))
            screen_state = Dm.text("[]1", base_icon_state);
        else
            screen_state = Dm.text("[]0", base_icon_state);

        Dm.append(ref dot, mutable_appearance(icon, screen_state));
        // DEVIATION(DV12): . += emissive_appearance(icon, screen_state, src, alpha = src.alpha), the screen's glow on the emissive
        // plane, is not created
        return dot;
    }

    // tg: code/game/machinery/requests_console.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // Init by checking our area, stolen from APC code
        area = get_area(loc) as area;

        // Naming and department sets
        if (Dm.truthy(auto_name)) // If autonaming, just pick department and name from the area code.
        {
            department = Dm.text("[]", get_area_name(area, TRUE));
            name = I18n.T("\\improper [] requests console", department);
        }
        else
        {
            if (!Dm.truthy(department) && !Dm.eq(name, "requests console")) // if we have a map-set name, let's default that for the department.
                department = name;
            else if (!Dm.truthy(department)) // if we have no department and no name, we'll have to be Unknown.
            {
                department = "Unknown";
                name = I18n.T("\\improper [] requests console", department);
            }
            else
                name = I18n.T("\\improper [] requests console", department); // and if we have a 'department', our name should reflect that.
        }

        Dm.append(ref GLOB.req_console_all, this);

        GLOB.req_console_ckey_departments[ckey(department)] = department; // and then we set ourselves a listed name

        // Register this console for RETA UI updates - code/modules/reta/reta_system.dm
        object dept_key = reta_get_user_department_by_name(department);
        if (Dm.truthy(dept_key))
        {
            DmList origin_consoles = GLOB.reta_consoles_by_origin[dept_key] as DmList;
            LAZYADD(ref origin_consoles, this);
            GLOB.reta_consoles_by_origin[dept_key] = origin_consoles;
        }

        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/requests_console.dm:134
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(messages);
        Dm.remove(ref GLOB.req_console_all, this);

        // Remove from RETA console registry
        object dept_key = reta_get_user_department_by_name(department);
        if (Dm.truthy(dept_key))
        {
            DmList origin_consoles = GLOB.reta_consoles_by_origin[dept_key] as DmList;
            LAZYREMOVE(ref origin_consoles, this);
            GLOB.reta_consoles_by_origin[dept_key] = origin_consoles;
        }

        return base.Destroy(force);
    }

    // tg: code/game/machinery/requests_console.dm:7-11 (file-local, #undef at :649-652)
    internal const string REQ_EMERGENCY_SECURITY = "Security";
    internal const string REQ_EMERGENCY_ENGINEERING = "Engineering";
    internal const string REQ_EMERGENCY_MEDICAL = "Medical";
    internal const float ANNOUNCEMENT_COOLDOWN_TIME = 30 * SECONDS;

    // tg: code/game/machinery/requests_console.dm:145
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(open))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RequestsConsole");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/requests_console.dm:154
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob usr = World.Current.usr as mob;

        switch (Dm.str(action))
        {
            case "clear_message_status":
                has_mail_send_error = FALSE;
                foreach (obj_machinery_requests_console console in Dm.each<obj_machinery_requests_console>(GLOB.req_console_all))
                {
                    if (Dm.eq(console.department, department))
                    {
                        console.new_message_priority = REQ_NO_NEW_MESSAGE;
                        console.update_appearance();
                    }
                }
                return TRUE;
            case "clear_authentication":
                message_stamped_by = "";
                message_verified_by = "";
                announcement_authenticated = FALSE;
                return TRUE;
            case "toggle_silent":
                silent = Dm.truthy(silent) ? FALSE : TRUE;
                return TRUE;
            case "set_emergency":
                return await ui_act_set_emergency(@params, usr);
            case "send_announcement":
            {
                if (!(announcement_cooldown <= world.time)) // COOLDOWN_FINISHED
                {
                    to_chat(usr, span_alert(I18n.T("Intercomms recharging. Please stand by.")));
                    return dot;
                }
                if (!Dm.truthy(can_send_announcements))
                    return dot;
                if (!Dm.truthy(announcement_authenticated)) // || isAdminGhostAI(usr): no admin ghosts yet (GAP(lead), is_helpers.cs)
                    return dot;

                object message = reject_bad_text(trim(html_encode(@params["message"]), MAX_MESSAGE_LEN), ascii_only: FALSE);
                if (!Dm.truthy(message))
                {
                    to_chat(usr, span_alert(I18n.T("Invalid message.")));
                    return dot;
                }
                if (usr is mob_living L)
                    message = (L.treat_message(message) as DmList)?["message"];

                minor_announce(message, Dm.text("[] Announcement:", department), html_encode: FALSE, sound_override: Dm.resource("sound/announcer/announcement/announce_dig.ogg"));
                GLOB.news_network.submit_article(message, department, NEWSCASTER_STATION_ANNOUNCEMENTS, null);
                usr.log_talk(message, LOG_SAY, tag: Dm.text("station announcement from []", this));
                message_admins(Dm.text("[] has made a station announcement from [] at [].", ADMIN_LOOKUPFLW(usr), this, AREACOORD(usr)));
                deadchat_broadcast(Dm.text(" made a station announcement from [].", span_name(Dm.text("[]", get_area_name(usr, TRUE)))), span_name(Dm.text("[]", usr.real_name)), usr, message_type: DEADCHAT_ANNOUNCEMENT);

                announcement_cooldown = world.time + ANNOUNCEMENT_COOLDOWN_TIME; // COOLDOWN_START
                announcement_authenticated = FALSE;
                return TRUE;
            }
            case "quick_reply":
            {
                object recipient = @params["reply_recipient"];

                object reply_message = reject_bad_text(await tgui_input_text(usr, I18n.T("Write a quick reply to []", recipient), I18n.T("Awaiting Input")), ascii_only: FALSE);
                if (QDELETED(ui) || !Dm.eq(ui.status, UI_INTERACTIVE))
                    return dot;
                if (!Dm.truthy(reply_message))
                {
                    has_mail_send_error = TRUE;
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, TRUE);
                    return TRUE;
                }

                send_message(recipient, reply_message, REQ_NORMAL_MESSAGE_PRIORITY, REPLY_REQUEST);
                return TRUE;
            }
            case "send_message":
            {
                object recipient = @params["recipient"];
                if (!Dm.truthy(recipient))
                    return dot;
                object priority = @params["priority"];
                if (!Dm.truthy(priority))
                    return dot;
                object message = reject_bad_text(trim(html_encode(@params["message"]), MAX_MESSAGE_LEN), ascii_only: FALSE);
                if (!Dm.truthy(message))
                {
                    to_chat(usr, span_alert(I18n.T("Invalid message.")));
                    has_mail_send_error = TRUE;
                    return TRUE;
                }
                object request_type = @params["request_type"];
                if (!Dm.truthy(request_type))
                    return dot;
                send_message(recipient, message, priority, request_type);
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/requests_console.dm:173-317  (ui_act's "set_emergency" branch, split out of the switch)
    private async DmTask<object> ui_act_set_emergency(DmList @params, mob usr)
    {
        if (Dm.truthy(emergency))
            return null;

        // Check for RETA eligibility
        object emergency_type = @params["emergency"];
        object origin_dept = reta_get_user_department_by_name(department);
        string target_dept = null;

        switch (Dm.str(emergency_type))
        {
            case REQ_EMERGENCY_SECURITY:
                target_dept = "Security";
                break;
            case REQ_EMERGENCY_ENGINEERING:
                target_dept = "Engineering";
                break;
            case REQ_EMERGENCY_MEDICAL:
                target_dept = "Medical";
                break;
        }

        // Check if user can call this emergency (prevent self-calls) RETA
        object user_dept = reta_get_user_department(usr);
        if (Dm.eq(user_dept, target_dept)) // && !isAdminGhostAI(usr): no admin ghosts yet (GAP(lead), is_helpers.cs)
        {
            to_chat(usr, span_alert(I18n.T("You cannot call your own department for emergency assistance.")));
            return null;
        }

        // Check cooldown RETA
        if (Dm.truthy(origin_dept) && target_dept != null && reta_on_cooldown(origin_dept, target_dept))
        {
            to_chat(usr, span_alert(I18n.T("Emergency calls to [] are on cooldown.", target_dept)));
            return null;
        }

        emergency = emergency_type;

        // Enhanced announcement with caller info
        object caller_info = "";
        // Aghosts have no IDs, but they hold authorithy so instead calling them Unknowns just drop caller info completely
        if (usr != null && isliving(usr))
        {
            caller_info = I18n.T("(Identification not provided)");
            var caller_mob = (mob_living)usr;
            var ID = caller_mob.get_idcard() as obj_item_card_id;
            if (ID != null)
                caller_info = I18n.T("(Called by [], [])", ID.registered_name, ID.assignment);
            // Centcom still refuses to provide IDs to their silicons
            else if (issilicon(caller_mob))
            {
                caller_info = I18n.T("(Called by [], [])", caller_mob.name, caller_mob.job);
                // Cyborgs do not have their job var set, and this is wrong
                if (iscyborg(caller_mob) && caller_mob.mind?.assigned_role != null)
                    caller_info = I18n.T("(Called by [], [])", caller_mob.name, caller_mob.mind.assigned_role.title);
            }
            // If someone swiped their ID before
            else if (Dm.truthy(message_verified_by))
            {
                caller_info = I18n.T("(Last authentication: [])", message_verified_by);
                message_stamped_by = "";
                message_verified_by = "";
            }
        }

        // Grant RETA if conditions are met
        if (Dm.truthy(origin_dept) && target_dept != null && Dm.truthy(config.Get(typeof(datum_config_entry_flag_reta_enabled))))
        {
            // Set cooldown
            object cooldown_ds = config.Get(typeof(datum_config_entry_number_reta_dept_cooldown_ds));
            if (!Dm.truthy(cooldown_ds))
                cooldown_ds = 150;
            reta_set_cooldown(origin_dept, target_dept, cooldown_ds);

            // Find responders and grant access to their ID cards
            object duration_ds = config.Get(typeof(datum_config_entry_number_reta_duration_ds));
            if (!Dm.truthy(duration_ds))
                duration_ds = 3000;
            object granted_count = reta_find_and_grant_access(target_dept, origin_dept, duration_ds);

            // Track this call for multiple department analysis
            reta_track_call(origin_dept, target_dept);

            switch (Dm.str(emergency_type))
            {
                case REQ_EMERGENCY_SECURITY:
                    await aas_config_announce(typeof(datum_aas_config_entry_rc_emergency), Dm.list(Dm.kv("LOCATION", department), Dm.kv("CALLER", caller_info), Dm.kv("RETARESPONDERS", granted_count)), this, Dm.list(RADIO_CHANNEL_SECURITY), "Security");
                    break;
                case REQ_EMERGENCY_ENGINEERING:
                    await aas_config_announce(typeof(datum_aas_config_entry_rc_emergency), Dm.list(Dm.kv("LOCATION", department), Dm.kv("CALLER", caller_info), Dm.kv("RETARESPONDERS", granted_count)), this, Dm.list(RADIO_CHANNEL_ENGINEERING), "Engineering");
                    break;
                case REQ_EMERGENCY_MEDICAL:
                    await aas_config_announce(typeof(datum_aas_config_entry_rc_emergency), Dm.list(Dm.kv("LOCATION", department), Dm.kv("CALLER", caller_info), Dm.kv("RETARESPONDERS", granted_count)), this, Dm.list(RADIO_CHANNEL_MEDICAL), "Medical");
                    break;
            }

            // Send confirmation to the calling department about the RETA activation
            DmList target_channels = Dm.list();
            switch (Dm.str(origin_dept))
            {
                case "Security":
                    Dm.append(ref target_channels, RADIO_CHANNEL_SECURITY);
                    break;
                case "Engineering":
                    Dm.append(ref target_channels, RADIO_CHANNEL_ENGINEERING);
                    break;
                case "Medical":
                    Dm.append(ref target_channels, RADIO_CHANNEL_MEDICAL);
                    break;
                case "Science":
                    Dm.append(ref target_channels, RADIO_CHANNEL_SCIENCE);
                    break;
                case "Service":
                    Dm.append(ref target_channels, RADIO_CHANNEL_SERVICE);
                    break;
                case "Command":
                    Dm.append(ref target_channels, RADIO_CHANNEL_COMMAND);
                    break;
                case "Cargo":
                    Dm.append(ref target_channels, RADIO_CHANNEL_SUPPLY);
                    break;
                case "Mining":
                    Dm.append(ref target_channels, RADIO_CHANNEL_SUPPLY);
                    break;
            }

            // Do not announce if RETA failed to activate
            if (Dm.truthy(granted_count))
                await aas_config_announce(typeof(datum_aas_config_entry_rc_reta_announcement), Dm.list(Dm.kv("GRANTEE", target_dept), Dm.kv("CALLER", caller_info)), this, target_channels);
            // Log RETA activity
            log_game(Dm.text("RETA: [] called [] emergency, granted access to [] responder IDs for [] seconds", origin_dept, target_dept, granted_count, Dm.num(duration_ds) / 10));

            // Push UI updates to consoles in the same origin department
            reta_push_ui_updates(origin_dept, target_dept);
            // Making sure our emergency is synced with RETA timers
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_emergency)), Dm.num(duration_ds), file: "code/game/machinery/requests_console.dm", line: 302);
        }
        else
        {
            // Normal emergency call without RETA
            switch (Dm.str(emergency_type))
            {
                case REQ_EMERGENCY_SECURITY:
                    await aas_config_announce(typeof(datum_aas_config_entry_rc_emergency), Dm.list(Dm.kv("LOCATION", department), Dm.kv("CALLER", caller_info)), null, Dm.list(RADIO_CHANNEL_SECURITY), "Security");
                    break;
                case REQ_EMERGENCY_ENGINEERING:
                    await aas_config_announce(typeof(datum_aas_config_entry_rc_emergency), Dm.list(Dm.kv("LOCATION", department), Dm.kv("CALLER", caller_info)), null, Dm.list(RADIO_CHANNEL_ENGINEERING), "Engineering");
                    break;
                case REQ_EMERGENCY_MEDICAL:
                    await aas_config_announce(typeof(datum_aas_config_entry_rc_emergency), Dm.list(Dm.kv("LOCATION", department), Dm.kv("CALLER", caller_info)), null, Dm.list(RADIO_CHANNEL_MEDICAL), "Medical");
                    break;
            }
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_emergency)), 5 * MINUTES, file: "code/game/machinery/requests_console.dm", line: 312);
        }

        update_appearance();
        return TRUE;
    }

    ///Sends the message from the request console
    // tg: code/game/machinery/requests_console.dm:349
    public virtual void send_message(object recipient, object message, object priority, object request_type)
    {
        object radio_channel = null;
        // They all naming them wrong, all the time... I'll probably rewrite this later in separate PR.
        // Automatically from areas or via mapping helpers. (ther is no "Cargobay Request Console" in any map)
        switch (Dm.str(ckey(recipient)))
        {
            case "bridge":
                radio_channel = RADIO_CHANNEL_COMMAND;
                break;
            case "medbay":
                radio_channel = RADIO_CHANNEL_MEDICAL;
                break;
            case "science":
                radio_channel = RADIO_CHANNEL_SCIENCE;
                break;
            case "engineering":
                radio_channel = RADIO_CHANNEL_ENGINEERING;
                break;
            case "security":
                radio_channel = RADIO_CHANNEL_SECURITY;
                break;
            case "cargobay":
            case "mining":
                radio_channel = RADIO_CHANNEL_SUPPLY;
                break;
        }

        var signal = Dm.@new<datum_signal_subspace_messaging_rc>(this, Dm.list(
            Dm.kv("sender_department", department),
            Dm.kv("recipient_department", recipient),
            Dm.kv("message", message),
            Dm.kv("verified", message_verified_by),
            Dm.kv("stamped", message_stamped_by),
            Dm.kv("priority", priority),
            Dm.kv("notify_channel", radio_channel),
            Dm.kv("request_type", request_type)));
        signal.send_to_receivers();

        has_mail_send_error = Dm.truthy(signal.data["done"]) ? FALSE : TRUE;

        if (!Dm.truthy(silent))
        {
            if (Dm.truthy(has_mail_send_error))
                playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, TRUE);
            else
                playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 50, TRUE);
        }

        message_stamped_by = "";
        message_verified_by = "";
    }

    // tg: code/game/machinery/requests_console.dm:390
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["is_admin_ghost_ai"] = FALSE; // isAdminGhostAI(user): no admin ghosts yet (GAP(lead), is_helpers.cs)
        data["can_send_announcements"] = can_send_announcements;
        data["department"] = department;
        data["emergency"] = emergency;
        data["hack_state"] = hack_state;
        data["new_message_priority"] = new_message_priority;
        data["silent"] = silent;
        data["has_mail_send_error"] = has_mail_send_error;
        data["authentication_data"] = Dm.list(
            Dm.kv("message_verified_by", message_verified_by),
            Dm.kv("message_stamped_by", message_stamped_by),
            Dm.kv("announcement_authenticated", announcement_authenticated));
        data["messages"] = Dm.list();
        foreach (datum_request_message message in Dm.each<datum_request_message>(messages))
            Dm.append_at(data, "messages", Dm.list(message.message_ui_data()));

        // Add RETA data
        data["reta_enabled"] = config.Get(typeof(datum_config_entry_flag_reta_enabled));
        object origin_dept = reta_get_user_department_by_name(department);
        object user_dept = reta_get_user_department(user);

        DmList cooldowns = Dm.list();
        data["reta_cooldowns"] = cooldowns;
        if (Dm.truthy(origin_dept))
        {
            cooldowns["Security"] = reta_on_cooldown(origin_dept, "Security") ? TRUE : FALSE;
            cooldowns["Engineering"] = reta_on_cooldown(origin_dept, "Engineering") ? TRUE : FALSE;
            cooldowns["Medical"] = reta_on_cooldown(origin_dept, "Medical") ? TRUE : FALSE;
        }

        data["reta_user_dept"] = user_dept;

        return data;
    }

    // tg: code/game/machinery/requests_console.dm:425
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["assistance_consoles"] = Dm.sub(GLOB.req_console_assistance, department);
        data["supply_consoles"] = Dm.sub(GLOB.req_console_supplies, department);
        data["information_consoles"] = Dm.sub(GLOB.req_console_information, department);

        return data;
    }

    // tg: code/game/machinery/requests_console.dm:434
    public override object say_mod__proc(object input = null, DmList message_mods = null)
    {
        message_mods ??= Dm.list();
        if (Dm.truthy(spantext_char(input, "!", -3)))
            return I18n.T("blares");
        else
            return base.say_mod__proc(input, message_mods);
    }

    /// Turns the emergency console back to its normal sprite once the emergency has timed out
    // tg: code/game/machinery/requests_console.dm:441
    public virtual void clear_emergency()
    {
        emergency = null;
        update_appearance();
    }

    /// Updates the UI for all viewers
    // tg: code/game/machinery/requests_console.dm:446
    public virtual void ui_update()
    {
        SStgui.update_uis(this);
    }

    /// From message_server.dm: Console.create_message(data)
    // tg: code/game/machinery/requests_console.dm:450
    public virtual async DmTask create_message(DmList data)
    {
        var new_message = Dm.@new<datum_request_message>(data);

        switch (Dm.num(new_message.priority))
        {
            case REQ_NORMAL_MESSAGE_PRIORITY:
                if (new_message_priority < REQ_NORMAL_MESSAGE_PRIORITY)
                {
                    new_message_priority = REQ_NORMAL_MESSAGE_PRIORITY;
                    update_appearance();
                }
                break;

            case REQ_HIGH_MESSAGE_PRIORITY:
                if (new_message_priority < REQ_HIGH_MESSAGE_PRIORITY)
                {
                    new_message_priority = REQ_HIGH_MESSAGE_PRIORITY;
                    update_appearance();
                }
                break;

            case REQ_EXTREME_MESSAGE_PRIORITY:
                silent = FALSE;
                if (new_message_priority < REQ_EXTREME_MESSAGE_PRIORITY)
                {
                    new_message_priority = REQ_EXTREME_MESSAGE_PRIORITY;
                    update_appearance();
                }
                break;
        }

        messages.Insert(1, new_message); //reverse order

        SStgui.update_uis(this);

        if (!Dm.truthy(silent))
        {
            playsound(this, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 50, TRUE);
            await say(new_message.get_alert());
        }

        if (Dm.truthy(new_message.radio_channel))
        {
            object authentication = null;
            string announcement_line = "Unauthenticated";
            if (Dm.truthy(new_message.message_verified_by))
            {
                authentication = new_message.message_verified_by;
                announcement_line = "Verified with ID";
            }
            else if (Dm.truthy(new_message.message_stamped_by))
            {
                authentication = new_message.message_stamped_by;
                announcement_line = "Stamped with stamp";
            }

            await aas_config_announce(typeof(datum_aas_config_entry_rc_new_message), Dm.list(
                Dm.kv("AUTHENTICATION", authentication),
                Dm.kv("SENDER", new_message.sender_department),
                Dm.kv("RECEIVER", department),
                Dm.kv("MESSAGE", new_message.content)
                ), null, Dm.list(new_message.radio_channel), announcement_line, Dm.eq(new_message.priority, REQ_EXTREME_MESSAGE_PRIORITY) ? TRUE : FALSE);
        }
    }

    // tg: code/game/machinery/requests_console.dm:496
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 50);
        if (Dm.truthy(open))
        {
            to_chat(user, span_notice(I18n.T("You close the maintenance panel.")));
            open = FALSE;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You open the maintenance panel.")));
            open = TRUE;
        }
        update_appearance();
        return TRUE;
    }

    // tg: code/game/machinery/requests_console.dm:507
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(open))
        {
            hack_state = Dm.num(!Dm.truthy(hack_state));
            if (Dm.truthy(hack_state))
                to_chat(user, span_notice(I18n.T("You modify the wiring.")));
            else
                to_chat(user, span_notice(I18n.T("You reset the wiring.")));
            update_appearance();
            tool.play_tool_sound(this, 50);
        }
        else
            to_chat(user, span_warning(I18n.T("You must open the maintenance panel first!")));
        return TRUE;
    }

    // tg: code/game/machinery/requests_console.dm:520
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item_card_id card = tool.GetID() as obj_item_card_id;
        if (card != null)
        {
            message_verified_by = Dm.text("[] ([])", card.registered_name, card.assignment);
            announcement_authenticated = Dm.num(Dm.@in(ACCESS_RC_ANNOUNCE, card.access));
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }
        if (istype(tool, typeof(obj_item_stamp)))
        {
            message_stamped_by = tool.name;
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/machinery/requests_console.dm:533
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_wallframe_requests_console>(loc);
        return null;
    }
}

// tg: code/game/machinery/requests_console.dm:551
public partial class datum_aas_config_entry_rc_emergency : datum_aas_config_entry
{
    // tg: code/game/machinery/requests_console.dm:564
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // If RETA enabled change config lines to include RETA info
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_reta_enabled))))
        {
            // Non sec/engi/med personnel may be called by CC or AI (I hope) for anomaly removal and etc. Mostly admin triggered calls
            announcement_lines_map = Dm.list(
                Dm.kv("RETA Granted", "- RETA door access granted to responders"),
                Dm.kv("RETA Failed", "- no RETA access provided"),
                Dm.kv("Security", "SECURITY EMERGENCY in %LOCATION %CALLER %RETA!!!"),
                Dm.kv("Engineering", "ENGINEERING EMERGENCY in %LOCATION %CALLER %RETA!!!"),
                Dm.kv("Medical", "MEDICAL EMERGENCY in %LOCATION %CALLER, %RETA!!!"),
                Dm.kv("Science", "Science personnel was requested in %LOCATION %CALLER %RETA."),
                Dm.kv("Service", "Service personnel was requested in %LOCATION %CALLER %RETA."),
                Dm.kv("Command", "Command personnel was requested in %LOCATION %CALLER %RETA."),
                Dm.kv("Cargo", "Cargo personnel was requested in %LOCATION %CALLER %RETA."),
                Dm.kv("Mining", "Miners were requested in %LOCATION %CALLER %RETA."));
            vars_and_tooltips_map = Dm.list(
                Dm.kv("LOCATION", "will be replaced with the department name"),
                Dm.kv("CALLER", "with caller name and job if applicable"),
                Dm.kv("RETA", "with RETA Granted or RETA Failed lines depending on RETA system report"));
        }
    }

    // tg: code/game/machinery/requests_console.dm:587
    public override object compile_announce(DmList variables_map = null, object announcement_line = null)
    {
        if (!Dm.truthy(variables_map["CALLER"]))
            variables_map["CALLER"] = "(Caller placeholder)";

        variables_map["RETA"] = Dm.truthy(variables_map["RETARESPONDERS"]) ? announcement_lines_map["RETA Granted"] : announcement_lines_map["RETA Failed"];
        object dot = base.compile_announce(variables_map, announcement_line);
        // In case - someone expands RETA departments, but forgets to add announcement lines for them
        if (!Dm.truthy(announcement_lines_map[announcement_line]))
            dot = Dm.text("ERROR: UNKNOWN DEPARTMENT \\[[]\\] CALLED IN [] []. PLEASE REPORT THIS TO NT TECH SUPPORT.", announcement_line, Dm.truthy(variables_map["LOCATION"]) ? variables_map["LOCATION"] : "[NO DATA]", variables_map["CALLER"]);

        DmList exploded_string = splittext_char(dot, "(Caller placeholder)") as DmList;
        DmList trimed_message = Dm.list();
        foreach (object each in Dm.each(exploded_string))
        {
            object line = trim(each);
            if (Dm.truthy(line))
                Dm.append(ref trimed_message, line);
        }
        // Rebuild the string without empty lines
        return trimed_message.Join(" ");
    }
}

// tg: code/game/machinery/requests_console.dm:612
public partial class datum_aas_config_entry_rc_reta_announcement : datum_aas_config_entry
{
    // tg: code/game/machinery/requests_console.dm:617
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // If RETA disabled - we should be down
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_reta_enabled))))
        {
            announcement_lines_map["Message"] = "RETA system is disabled.";
            enabled = FALSE;
            modifiable = FALSE;
        }
    }

    // tg: code/game/machinery/requests_console.dm:625
    public override object compile_announce(DmList variables_map = null, object announcement_line = null)
    {
        if (!Dm.truthy(variables_map["CALLER"]))
            variables_map["CALLER"] = "(Caller placeholder)";
        DmList exploded_string = splittext_char(base.compile_announce(variables_map, announcement_line), "(Caller placeholder)") as DmList;
        DmList trimed_message = Dm.list();
        foreach (object each in Dm.each(exploded_string))
        {
            object line = trim(each);
            if (Dm.truthy(line))
                Dm.append(ref trimed_message, line);
        }
        // Rebuild the string without empty lines
        return trimed_message.Join(" ");
    }
}
