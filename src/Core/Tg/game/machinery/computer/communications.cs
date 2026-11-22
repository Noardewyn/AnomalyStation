// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/communications.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G5 (D272): the communications console: the login, the emergency shuttle's call and recall, the alert level, the priority
// announcement, the emergency maintenance access, the messages, the status display page and the messages to CentCom. The emag came
// with G1a (D316). Gaps CM1 (D333): the rest of the file but what is listed below: the shuttle purchases (the catalogue page, the
// purchase access, the departmental shuttles' lock), the nuke codes' request, the captain's spare ID safe codes, the syndicate console
// and the console hacks. Not here: the battlecruiser card's branch of emag_act (the Starfury, not-translated.md 139's neighbour rows),
// the cross-sector messages (other servers: send2otherserver, the cross_server config; sendToOtherSector stays Deferred and its
// procs live-partial stubs), the admins' reject link (Topic) and the ninja's drain (ninjadrain_act, the space ninja).

// tg: code/game/machinery/computer/communications.dm:20  var/static/battlecruiser_called; COOLDOWN_DECLARE(static/...); var/static/last_status_display
public sealed partial class TgState
{
    internal float communications_important_action_cooldown;
    internal float communications_emergency_access_cooldown;
    internal DmList communications_last_status_display;
}

// tg: code/game/machinery/computer/communications.dm:10
public partial class obj_machinery_computer_communications : obj_machinery_computer
{
    // tg: code/game/machinery/computer/communications.dm:1  #define IMPORTANT_ACTION_COOLDOWN (60 SECONDS), EMERGENCY_ACCESS_COOLDOWN (30 SECONDS)
    private const float IMPORTANT_ACTION_COOLDOWN = 600;
    private const float EMERGENCY_ACCESS_COOLDOWN = 300;
    // tg: :4  the UI's states
    private const string STATE_BUYING_SHUTTLE = "buying_shuttle";
    private const string STATE_CHANGING_STATUS = "changing_status";
    private const string STATE_MAIN = "main";
    private const string STATE_MESSAGES = "messages";

    private static ref float important_action_cooldown => ref TgState.Current.communications_important_action_cooldown;
    private static ref float emergency_access_cooldown => ref TgState.Current.communications_emergency_access_cooldown;
    private static ref DmList last_status_display => ref TgState.Current.communications_last_status_display;

    // tg: code/game/machinery/computer/communications.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // All maps should have at least 1 comms console
        // REGISTER_REQUIRED_MAP_ITEM(1, INFINITY): the unit tests' map item check (UNIT_TESTS)

        Dm.append(ref GLOB.shuttle_caller_list, this);
        return dot;
    }

    /// Are we NOT a silicon, AND we're logged in as the captain?
    // tg: code/game/machinery/computer/communications.dm:98
    public virtual object authenticated_as_non_silicon_captain(mob user = null)
    {
        if (HAS_SILICON_ACCESS(user))
            return FALSE;
        return Dm.@in(ACCESS_CAPTAIN, authorize_access) ? TRUE : FALSE;
    }

    /// Are we a silicon, OR we're logged in as the captain?
    // tg: code/game/machinery/computer/communications.dm:104
    public virtual object authenticated_as_silicon_or_captain(mob user = null)
    {
        if (HAS_SILICON_ACCESS(user))
            return TRUE;
        return Dm.@in(ACCESS_CAPTAIN, authorize_access) ? TRUE : FALSE;
    }

    /// Are we a silicon, OR logged in?
    // tg: code/game/machinery/computer/communications.dm:110
    public virtual object authenticated__proc(mob user = null)
    {
        if (HAS_SILICON_ACCESS(user))
            return TRUE;
        return authenticated;
    }

    // tg: code/game/machinery/computer/communications.dm:115
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_card_id) // isidcard (__DEFINES/is_helpers.dm)
            return NONE;
        await attack_hand(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/computer/communications.dm:121
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (emag_card is obj_item_card_emag_battlecruiser)
        {
            // tg :122-135: the battlecruiser card (a traitor's) checks the user, marks battlecruiser_called, uses its charge and
            // summons the Starfury (summon_battlecruiser) with an alert sound. The card stays inert with the Starfury, its map and the
            // battlecruiser team (not-translated.md section 134): no live code hands this card over.
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (Dm.truthy(authenticated))
            authorize_access = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
        balloon_alert(user, "routing circuits scrambled");
        playsound(this, "sound/machines/terminal/terminal_alert.ogg", 50, FALSE);
        return TRUE;
    }

    // tg: code/game/machinery/computer/communications.dm:147
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        DmList approved_states = Dm.list(STATE_BUYING_SHUTTLE, STATE_CHANGING_STATUS, STATE_MAIN, STATE_MESSAGES);

        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(has_communication()))
            return dot;

        mob user = ui.user;
        dot = TRUE;

        string act = Dm.str(action);
        switch (act)
        {
            case "answerMessage":
            {
                if (!Dm.truthy(authenticated__proc(user)))
                    return dot;

                object answer_index = @params["answer"];
                object message_index = @params["message"];

                // If either of these aren't numbers, then bad voodoo.
                if (!isnum(answer_index) || !isnum(message_index))
                {
                    message_admins(Dm.text("[] provided an invalid index type when replying to a message on []. This should not happen. Please check with a maintainer and/or consult tgui logs.", key_name_admin(user), this));
                    throw Dm.CRASH("Non-numeric index provided when answering comms console message.", "code/game/machinery/computer/communications.dm", 171);
                }

                if (!Dm.truthy(answer_index) || !Dm.truthy(message_index) || Dm.num(answer_index) < 1 || Dm.num(message_index) < 1)
                    return dot;
                var message = (datum_comm_message)messages[message_index];
                if (Dm.truthy(message.answered))
                    return dot;
                message.answered = Dm.num(answer_index);
                message.answer_callback?.InvokeAsync();
                break;
            }
            case "callShuttle":
            {
                if (!Dm.truthy(authenticated__proc(user)) || Dm.truthy(syndicate))
                    return dot;
                object reason = trim(@params["reason"], MAX_MESSAGE_LEN);
                if (length(reason) < CALL_SHUTTLE_REASON_LENGTH)
                    return dot;
                SSshuttle.requestEvac(user, reason);
                post_status("shuttle");
                break;
            }
            case "changeSecurityLevel":
            {
                if (!Dm.truthy(authenticated_as_silicon_or_captain(user)))
                    return dot;

                // Check if they have
                if (!HAS_SILICON_ACCESS(user))
                {
                    var held_item = user.get_active_held_item() as obj_item;
                    var id_card = held_item?.GetID() as obj_item_card_id;
                    if (id_card == null)
                    {
                        to_chat(user, span_warning("You need to swipe your ID!"));
                        playsound(this, "sound/machines/terminal/terminal_prompt_deny.ogg", 50, FALSE);
                        return dot;
                    }
                    if (!Dm.@in(ACCESS_CAPTAIN, id_card.access))
                    {
                        to_chat(user, span_warning("You are not authorized to do this!"));
                        playsound(this, "sound/machines/terminal/terminal_prompt_deny.ogg", 50, FALSE);
                        return dot;
                    }
                }

                object new_sec_level = SSsecurity_level.text_level_to_number(@params["newSecurityLevel"]);
                if (!Dm.eq(new_sec_level, SEC_LEVEL_GREEN) && !Dm.eq(new_sec_level, SEC_LEVEL_BLUE))
                    return dot;
                if (Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_DELTA)
                {
                    to_chat(user, span_warning("Central Command has placed a lock on the alert level due to a doomsday!"));
                    return dot;
                }
                if (Dm.eq(SSsecurity_level.get_current_level_as_number(), new_sec_level))
                    return dot;

                SSsecurity_level.set_level(new_sec_level);

                to_chat(user, span_notice("Authorization confirmed. Modifying security level."));
                playsound(this, "sound/machines/terminal/terminal_prompt_confirm.ogg", 50, FALSE);

                // Only notify people if an actual change happened
                user.log_message(Dm.text("changed the security level to [] with [].", @params["newSecurityLevel"], this), LOG_GAME);
                message_admins(Dm.text("[] has changed the security level to [] with [] at [].", key_name_admin(user), @params["newSecurityLevel"], this, AREACOORD(user)));
                deadchat_broadcast(I18n.T(" has changed the security level to [] with [] at [].", @params["newSecurityLevel"], this, span_name(Dm.text("[]", get_area_name(user, TRUE)))), span_name(Dm.text("[]", user.real_name)), user, message_type: DEADCHAT_ANNOUNCEMENT);

                alert_level_tick += 1;
                break;
            }
            case "deleteMessage":
            {
                if (!Dm.truthy(authenticated__proc(user)))
                    return dot;
                object message_index = text2num(@params["message"]);
                if (!Dm.truthy(message_index))
                    return dot;
                // LAZYREMOVE(messages, LAZYACCESS(messages, message_index)) (__HELPERS/_lists.dm), expanded inline
                object removing = LAZYACCESS(messages, message_index);
                if (messages != null)
                {
                    Dm.remove(ref messages, removing);
                    if (messages.len == 0)
                        messages = null;
                }
                break;
            }
            case "makePriorityAnnouncement":
            {
                if (!Dm.truthy(authenticated_as_silicon_or_captain(user)) && !Dm.truthy(syndicate))
                    return dot;
                await make_announcement(user as mob_living);
                break;
            }
            case "messageAssociates":
            {
                if (!Dm.truthy(authenticated_as_non_silicon_captain(user)))
                    return dot;
                if (!(important_action_cooldown <= Dm.world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
                    return dot;

                playsound(this, "sound/machines/terminal/terminal_prompt_confirm.ogg", 50, FALSE);
                object message = trim(html_encode(@params["message"]), MAX_MESSAGE_LEN);

                bool emagged = Dm.truthy(Dm.bitand(obj_flags, EMAGGED));
                if (emagged)
                {
                    message_syndicate(message, user);
                    to_chat(user, span_danger("SYSERR @l(19833)of(transmit.dm): !@$ MESSAGE TRANSMITTED TO SYNDICATE COMMAND."));
                }
                else if (Dm.truthy(syndicate))
                {
                    message_syndicate(message, user);
                    to_chat(user, span_danger("Message transmitted to Syndicate Command."));
                }
                else
                {
                    message_centcom(message, user);
                    to_chat(user, span_notice("Message transmitted to Central Command."));
                }

                object associates = emagged || Dm.truthy(syndicate) ? I18n.T("the Syndicate") : I18n.T("CentCom");
                // tg: :258  user.log_talk(message, LOG_SAY, tag = "message to [associates]"): the say log (the admin logs, M10+)
                deadchat_broadcast(I18n.T(" has messaged [], \"[]\" at [].", associates, message, span_name(Dm.text("[]", get_area_name(user, TRUE)))), span_name(Dm.text("[]", user.real_name)), user, message_type: DEADCHAT_ANNOUNCEMENT);
                important_action_cooldown = Dm.world.time + IMPORTANT_ACTION_COOLDOWN; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
                break;
            }
            case "purchaseShuttle":
            {
                object can_buy_shuttles_or_fail_reason = can_buy_shuttles(user);
                if (!Dm.eq(can_buy_shuttles_or_fail_reason, TRUE))
                {
                    if (!Dm.eq(can_buy_shuttles_or_fail_reason, FALSE))
                        to_chat(user, span_alert(Dm.text("[]", can_buy_shuttles_or_fail_reason)));
                    return dot;
                }
                DmList shuttles = assoc_to_values(SSmapping.shuttle_templates) as DmList;
                var shuttle = locate_ref_in(@params["shuttle"], shuttles) as datum_map_template_shuttle;
                if (shuttle == null)
                    return dot;
                if (!Dm.truthy(can_purchase_this_shuttle(shuttle)))
                    return dot;
                if (shuttle is datum_map_template_shuttle_emergency_departmental dept_shuttle)
                {
                    if (dept_shuttle.department_type != null)
                    {
                        float max_crew_count = 0;
                        foreach (object dept_type in Dm.each(SSjob.joinable_departments_by_type))
                        {
                            float crew_count = Dm.num(get_department_employee_count(dept_type));
                            if (crew_count > max_crew_count)
                                max_crew_count = crew_count;
                        }

                        float crew_in_department = Dm.num(get_department_employee_count(dept_shuttle.department_type));
                        if (crew_in_department <= 0 || crew_in_department < max_crew_count)
                        {
                            to_chat(user, span_alert(I18n.T("This shuttle can be buyed only if this department has most employees.")));
                            return dot;
                        }
                    }
                }
                if (!Dm.truthy(shuttle.prerequisites_met()))
                {
                    to_chat(user, span_alert(I18n.T("You have not met the requirements for purchasing this shuttle.")));
                    return dot;
                }
                datum_bank_account bank_account = SSeconomy.get_dep_account(ACCOUNT_CAR);
                if (bank_account.account_balance < shuttle.credit_cost)
                    return dot;
                SSshuttle.shuttle_purchased = SHUTTLEPURCHASE_PURCHASED;
                foreach (datum_round_event_control_shuttle_insurance insurance_event in Dm.each<datum_round_event_control_shuttle_insurance>(SSevents.control))
                    insurance_event.weight *= 20;
                await SSshuttle.unload_preview();
                SSshuttle.existing_shuttle = SSshuttle.emergency;
                await SSshuttle.action_load(shuttle, replace: TRUE);
                bank_account.adjust_money(-shuttle.credit_cost);

                object purchaser_name = Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? scramble_message_replace_chars("AUTHENTICATION FAILURE: CVE-2018-17107", 60) : user.real_name;
                minor_announce(I18n.T("[] has purchased [] for [] [].[]", purchaser_name, shuttle.name, shuttle.credit_cost, MONEY_NAME, Dm.truthy(shuttle.extra_desc) ? Dm.text(" []", shuttle.extra_desc) : ""), I18n.T("Shuttle Purchase"));

                message_admins(Dm.text("[] purchased [].", key_name_admin(user), shuttle.name));
                log_shuttle(Dm.text("[] has purchased [].", key_name(user), shuttle.name));
                SSblackbox.record_feedback("text", "shuttle_purchase", 1, shuttle.name);
                this.state = STATE_MAIN;
                break;
            }
            case "recallShuttle":
            {
                // AIs cannot recall the shuttle
                if (!Dm.truthy(authenticated__proc(user)) || HAS_SILICON_ACCESS(user) || Dm.truthy(syndicate))
                    return dot;
                await SSshuttle.cancel_evac(user);
                break;
            }
            case "requestNukeCodes":
            {
                if (!Dm.truthy(authenticated_as_non_silicon_captain(user)))
                    return dot;
                if (!(important_action_cooldown <= Dm.world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
                    return dot;
                object reason = trim(html_encode(@params["reason"]), MAX_MESSAGE_LEN);
                nuke_request(reason, user);
                to_chat(user, span_notice(I18n.T("Request sent.")));
                user.log_message(Dm.text("has requested the nuclear codes from CentCom with reason \"[]\"", reason), LOG_SAY);
                priority_announce(I18n.T("The codes for the on-station nuclear self-destruct have been requested by []. Confirmation or denial of this request will be sent shortly.", user), I18n.T("Nuclear Self-Destruct Codes Requested"), (SSstation.announcer as datum_centcom_announcer)?.get_rand_report_sound());
                playsound(this, "sound/machines/terminal/terminal_prompt.ogg", 50, FALSE);
                important_action_cooldown = Dm.world.time + IMPORTANT_ACTION_COOLDOWN; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
                break;
            }
            case "restoreBackupRoutingData":
            {
                if (!Dm.truthy(authenticated_as_non_silicon_captain(user)))
                    return dot;
                if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                    return dot;
                to_chat(user, span_notice("Backup routing data restored."));
                playsound(this, "sound/machines/terminal/terminal_prompt_confirm.ogg", 50, FALSE);
                obj_flags = Dm.bitand(obj_flags, Dm.bitnot(EMAGGED));
                break;
            }
            case "sendToOtherSector":
                Dm.Deferred("code/game/machinery/computer/communications.dm:331", "ui_act sendToOtherSector: the cross-server messages (other servers)");
                break;
            case "setState":
            {
                if (!Dm.truthy(authenticated__proc(user)))
                    return dot;
                if (!Dm.@in(@params["state"], approved_states))
                    return dot;
                if (Dm.eq(this.state, STATE_BUYING_SHUTTLE) && !Dm.eq(can_buy_shuttles(user), TRUE))
                    return dot;
                set_state(usr as mob, @params["state"]);
                break;
            }
            case "setStatusMessage":
            {
                if (!Dm.truthy(authenticated__proc(user)))
                    return dot;
                object line_one = reject_bad_text(@params["upperText"] ?? "", MAX_STATUS_LINE_LENGTH);
                object line_two = reject_bad_text(@params["lowerText"] ?? "", MAX_STATUS_LINE_LENGTH);
                post_status("message", line_one, line_two);
                last_status_display = Dm.list(line_one, line_two);
                break;
            }
            case "setStatusPicture":
            {
                if (!Dm.truthy(authenticated__proc(user)))
                    return dot;
                object picture = @params["picture"];
                if (!Dm.@in(picture, GLOB.status_display_approved_pictures))
                    return dot;
                if (Dm.@in(picture, GLOB.status_display_state_pictures))
                {
                    post_status(picture);
                }
                else
                {
                    if (Dm.eq(picture, "currentalert")) // You cannot set Code Blue display during Code Red and similiar
                        post_status("alert", SSsecurity_level?.current_security_level?.status_display_icon_state ?? "greenalert");
                    else
                        post_status("alert", picture);
                }
                break;
            }
            case "toggleAuthentication":
            {
                // Log out if we're logged in
                if (Dm.truthy(authorize_name))
                {
                    authenticated = FALSE;
                    authorize_access = null;
                    authorize_name = null;
                    playsound(this, "sound/machines/terminal/terminal_off.ogg", 50, FALSE);
                    return dot;
                }

                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    authenticated = TRUE;
                    authorize_access = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
                    authorize_name = "Unknown";
                    to_chat(user, span_warning(Dm.text("[] lets out a quiet alarm as its login is overridden.", this)));
                    playsound(this, "sound/machines/terminal/terminal_alert.ogg", 25, FALSE);
                }
                else if (isliving(user))
                {
                    var L = (mob_living)user;
                    var id_card = L.get_idcard(hand_first: TRUE) as obj_item_card_id;
                    if (Dm.truthy(check_access(id_card)))
                    {
                        authenticated = TRUE;
                        authorize_access = id_card.access.Copy();
                        authorize_name = Dm.text("[] - []", id_card.registered_name, id_card.assignment);
                    }
                }

                this.state = STATE_MAIN;
                playsound(this, "sound/machines/terminal/terminal_on.ogg", 50, FALSE);
                await imprint_gps("Encrypted Communications Channel");
                break;
            }
            case "toggleEmergencyAccess":
            {
                if (Dm.truthy(emergency_access_cooldown__proc(user))) //if were in cooldown, dont allow the following code
                    return dot;
                if (!Dm.truthy(authenticated_as_silicon_or_captain(user)))
                    return dot;
                if (Dm.truthy(GLOB.emergency_access))
                {
                    await revoke_maint_all_access();
                    user.log_message("disabled emergency maintenance access.", LOG_GAME);
                    message_admins(Dm.text("[] disabled emergency maintenance access.", key_name_admin(user)));
                    deadchat_broadcast(I18n.T(" disabled emergency maintenance access at [].", span_name(Dm.text("[]", get_area_name(user, TRUE)))), span_name(Dm.text("[]", user.real_name)), user, message_type: DEADCHAT_ANNOUNCEMENT);
                }
                else
                {
                    await make_maint_all_access();
                    user.log_message("enabled emergency maintenance access.", LOG_GAME);
                    message_admins(Dm.text("[] enabled emergency maintenance access.", key_name_admin(user)));
                    deadchat_broadcast(I18n.T(" enabled emergency maintenance access at [].", span_name(Dm.text("[]", get_area_name(user, TRUE)))), span_name(Dm.text("[]", user.real_name)), user, message_type: DEADCHAT_ANNOUNCEMENT);
                }
                break;
            }
            // Request codes for the Captain's Spare ID safe.
            case "requestSafeCodes":
            {
                if (Dm.truthy(SSjob.assigned_captain))
                {
                    to_chat(user, span_warning(I18n.T("There is already an assigned Captain or Acting Captain on deck!")));
                    return dot;
                }

                if (Dm.truthy(SSjob.safe_code_timer_id))
                {
                    to_chat(user, span_warning(I18n.T("The safe code has already been requested and is being delivered to your station!")));
                    return dot;
                }

                if (Dm.truthy(SSjob.safe_code_requested))
                {
                    to_chat(user, span_warning(I18n.T("The safe code has already been requested and delivered to your station!")));
                    return dot;
                }

                if (!Dm.truthy(SSid_access.spare_id_safe_code))
                {
                    to_chat(user, span_warning(I18n.T("There is no safe code to deliver to your station!")));
                    return dot;
                }

                turf pod_location = get_turf(this);

                SSjob.safe_code_request_loc = pod_location;
                SSjob.safe_code_requested = TRUE;
                SSjob.safe_code_timer_id = _addtimer(Dm.@new<datum_callback>(SSjob, nameof(datum_controller_subsystem_job.send_spare_id_safe_code), pod_location), 1200, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/game/machinery/computer/communications.dm", line: 471); // 120 SECONDS
                minor_announce(I18n.T("Due to staff shortages, your station has been approved for delivery of access codes to secure the Captain's Spare ID. Delivery via drop pod at []. ETA 120 seconds.", get_area(pod_location)));
                break;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/computer/communications.dm:474  (rules 2.4: the proc shares the static cooldown var's name)
    public virtual object emergency_access_cooldown__proc(mob user = null)
    {
        if (toggle_uses == toggle_max_uses) //you have used up free uses already, do it one more time and start a cooldown
        {
            to_chat(user, span_warning(Dm.text("This was your last free use without cooldown, you will not be able to use this again for [].", DisplayTimeText(EMERGENCY_ACCESS_COOLDOWN))));
            emergency_access_cooldown = Dm.world.time + EMERGENCY_ACCESS_COOLDOWN; // COOLDOWN_START
            ++toggle_uses; //add a use so that this if() is false the next time you try this button
            return FALSE;
        }

        if (!(emergency_access_cooldown <= Dm.world.time)) // !COOLDOWN_FINISHED
        {
            string time_left = DisplayTimeText(emergency_access_cooldown - Dm.world.time, 1); // COOLDOWN_TIMELEFT
            to_chat(user, span_warning(Dm.text("Emergency Access is still in cooldown for []!", time_left)));
            return TRUE; //dont use the button, we are in cooldown
        }
        else if ((Dm.num(last_toggled) + EMERGENCY_ACCESS_COOLDOWN) < Dm.world.time)
        {
            toggle_uses = 0; //either cooldown is done, or we just havent touched it in 30 seconds, either way reset uses
        }

        ++toggle_uses; //add a use
        last_toggled = Dm.world.time;
        return FALSE; //if we are not in cooldown, allow using the button
    }

    // tg: code/game/machinery/computer/communications.dm:511
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("authenticated", FALSE),
            Dm.kv("emagged", FALSE),
            Dm.kv("syndicate", syndicate));

        string ui_state = HAS_SILICON_ACCESS(user) ? cyborg_state : state;

        object has_connection = has_communication();
        data["hasConnection"] = has_connection;

        if (!Dm.truthy(SSjob.assigned_captain) && !Dm.truthy(SSjob.safe_code_requested) && Dm.truthy(SSid_access?.spare_id_safe_code) && Dm.truthy(has_connection))
        {
            data["canRequestSafeCode"] = TRUE;
            data["safeCodeDeliveryWait"] = 0f;
        }
        else
        {
            data["canRequestSafeCode"] = FALSE;
            if (Dm.truthy(SSjob.safe_code_timer_id) && Dm.truthy(has_connection))
            {
                data["safeCodeDeliveryWait"] = timeleft(SSjob.safe_code_timer_id);
                data["safeCodeDeliveryArea"] = get_area(SSjob.safe_code_request_loc);
            }
            else
            {
                data["safeCodeDeliveryWait"] = 0f;
                data["safeCodeDeliveryArea"] = null;
            }
        }

        if (Dm.truthy(authenticated) || HAS_SILICON_ACCESS(user))
        {
            data["authenticated"] = TRUE;
            data["canLogOut"] = !HAS_SILICON_ACCESS(user) ? TRUE : FALSE;
            data["page"] = ui_state;

            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(syndicate))
                data["emagged"] = TRUE;

            if (ui_state == STATE_MAIN)
            {
                data["canBuyShuttles"] = can_buy_shuttles(user);
                data["canMakeAnnouncement"] = FALSE;
                data["canMessageAssociates"] = FALSE;
                data["canRecallShuttles"] = !HAS_SILICON_ACCESS(user) ? TRUE : FALSE;
                data["canRequestNuke"] = FALSE;
                data["canSendToSectors"] = FALSE;
                data["canSetAlertLevel"] = FALSE;
                data["canToggleEmergencyAccess"] = FALSE;
                data["importantActionReady"] = important_action_cooldown <= Dm.world.time ? TRUE : FALSE; // COOLDOWN_FINISHED
                data["shuttleCalled"] = FALSE;
                data["shuttleLastCalled"] = FALSE;
                data["aprilFools"] = check_holidays(APRIL_FOOLS);
                data["alertLevel"] = SSsecurity_level.get_current_level_as_text();
                data["authorizeName"] = authorize_name;
                data["canLogOut"] = !HAS_SILICON_ACCESS(user) ? TRUE : FALSE;
                data["shuttleCanEvacOrFailReason"] = SSshuttle.canEvac();
                if (Dm.truthy(syndicate))
                    data["shuttleCanEvacOrFailReason"] = "You cannot summon the shuttle from this console!";

                if (Dm.truthy(authenticated_as_non_silicon_captain(user)))
                {
                    data["canMessageAssociates"] = TRUE;
                    data["canRequestNuke"] = TRUE;
                }

                // tg: :568-579  can_send_messages_to_other_sectors and the sectors (CONFIG_GET(keyed_list/cross_server)): the cross-server
                // messages (no other server; the procs are live-partial stubs), so no sector is offered

                if (Dm.truthy(authenticated_as_silicon_or_captain(user)))
                {
                    data["canToggleEmergencyAccess"] = TRUE;
                    data["emergencyAccess"] = GLOB.emergency_access;

                    data["alertLevelTick"] = alert_level_tick;
                    data["canMakeAnnouncement"] = TRUE;
                    data["canSetAlertLevel"] = HAS_SILICON_ACCESS(user) ? "NO_SWIPE_NEEDED" : "SWIPE_NEEDED";
                }
                else if (Dm.truthy(syndicate))
                {
                    data["canMakeAnnouncement"] = TRUE;
                }

                if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IDLE) && !Dm.eq(SSshuttle.emergency.mode, SHUTTLE_RECALL))
                {
                    data["shuttleCalled"] = TRUE;
                    data["shuttleRecallable"] = Dm.truthy(await SSshuttle.can_recall(user)) || Dm.truthy(syndicate) ? TRUE : FALSE;
                }

                if (Dm.truthy(SSshuttle.emergencyCallAmount))
                {
                    data["shuttleCalledPreviously"] = TRUE;
                    if (SSshuttle.emergency_last_call_loc != null)
                        data["shuttleLastCalled"] = format_text(SSshuttle.emergency_last_call_loc.name);
                }
            }
            else if (ui_state == STATE_MESSAGES)
            {
                DmList listed = Dm.list();
                data["messages"] = listed;

                if (messages != null)
                {
                    foreach (datum_comm_message message in Dm.each<datum_comm_message>(messages))
                    {
                        Dm.append(ref listed, Dm.list(Dm.list(
                            Dm.kv("answered", message.answered),
                            Dm.kv("content", message.content),
                            Dm.kv("title", message.title),
                            Dm.kv("possibleAnswers", message.possible_answers))));
                    }
                }
            }
            else if (ui_state == STATE_BUYING_SHUTTLE)
            {
                datum_bank_account bank_account = SSeconomy.get_dep_account(ACCOUNT_CAR);
                DmList shuttles = Dm.list();

                foreach (object shuttle_id in Dm.each(SSmapping.shuttle_templates))
                {
                    var shuttle_template = (datum_map_template_shuttle)SSmapping.shuttle_templates[shuttle_id];

                    if (shuttle_template.credit_cost == INFINITY)
                        continue;

                    if (!Dm.truthy(can_purchase_this_shuttle(shuttle_template)))
                        continue;

                    bool department_locked = false;
                    string department_name_string = "";

                    if (shuttle_template is datum_map_template_shuttle_emergency_departmental dept_shuttle)
                    {
                        department_name_string = dept_shuttle.department_name;

                        if (dept_shuttle.department_type != null)
                        {
                            float crew_in_department = Dm.num(get_department_employee_count(dept_shuttle.department_type));
                            if (crew_in_department <= 0)
                            {
                                department_locked = true;
                            }
                            else
                            {
                                foreach (object other_id in Dm.each(SSmapping.shuttle_templates))
                                {
                                    var other_shuttle = SSmapping.shuttle_templates[other_id] as datum_map_template_shuttle_emergency_departmental;
                                    if (other_shuttle == null || other_shuttle == dept_shuttle || other_shuttle.department_type == null)
                                        continue;
                                    // tg passes the other department's name where its type is looked up: the count is null, read as 0
                                    object crew_in_other_department = get_department_employee_count(other_shuttle.department_name);
                                    if (crew_in_department < Dm.num(crew_in_other_department))
                                    {
                                        department_locked = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    Dm.append(ref shuttles, Dm.list(Dm.list(
                        Dm.kv("name", shuttle_template.name),
                        Dm.kv("description", shuttle_template.description),
                        Dm.kv("occupancy_limit", shuttle_template.occupancy_limit),
                        Dm.kv("creditCost", shuttle_template.credit_cost),
                        Dm.kv("initial_cost", Dm.initial(shuttle_template, (datum_map_template_shuttle p) => p.credit_cost)),
                        Dm.kv("emagOnly", shuttle_template.emag_only),
                        Dm.kv("prerequisites", shuttle_template.prerequisites),
                        Dm.kv("ref", REF(shuttle_template)),
                        Dm.kv("department_locked", department_locked ? TRUE : FALSE),
                        Dm.kv("department_name", department_name_string))));
                }

                data["budget"] = bank_account.account_balance;
                data["shuttles"] = shuttles;
            }
            else if (ui_state == STATE_CHANGING_STATUS)
            {
                data["upperText"] = last_status_display != null ? last_status_display[1] : "";
                data["lowerText"] = last_status_display != null ? last_status_display[2] : "";
            }
        }

        return data;
    }

    // tg: code/game/machinery/computer/communications.dm:666
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "CommunicationsConsole");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/communications.dm:673
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("callShuttleReasonMinLength", CALL_SHUTTLE_REASON_LENGTH),
            Dm.kv("maxStatusLineLength", MAX_STATUS_LINE_LENGTH),
            Dm.kv("maxMessageLength", MAX_MESSAGE_LEN),
            Dm.kv("displayed_currency_full_name", Dm.text(" []", MONEY_NAME)),
            Dm.kv("displayed_currency_name", Dm.text(" []", MONEY_SYMBOL)));
    }

    /// Returns whether or not the communications console can communicate with the station
    // tg: code/game/machinery/computer/communications.dm:705
    public virtual object has_communication()
    {
        turf current_turf = get_turf(this);
        float z_level = current_turf.z;
        if (Dm.truthy(syndicate))
            return TRUE;
        return Dm.truthy(is_station_level(z_level)) || Dm.truthy(is_centcom_level(z_level)) ? TRUE : FALSE;
    }

    // tg: code/game/machinery/computer/communications.dm:712
    public virtual void set_state(mob user = null, object new_state = null)
    {
        if (HAS_SILICON_ACCESS(user))
            cyborg_state = Dm.str(new_state);
        else
            state = Dm.str(new_state);
    }

    /// Returns TRUE if the user can buy shuttles.
    /// If they cannot, returns FALSE or a string detailing why.
    // tg: code/game/machinery/computer/communications.dm:720
    public virtual object can_buy_shuttles(mob user = null)
    {
        if (!Dm.truthy(SSmapping.current_map?.allow_custom_shuttles)) // a host-built room has no map config: no shuttles to buy
            return FALSE;
        if (HAS_SILICON_ACCESS(user))
            return FALSE;

        bool has_access = false;

        foreach (object access in Dm.each(SSshuttle.has_purchase_shuttle_access))
        {
            if (Dm.@in(access, authorize_access))
            {
                has_access = true;
                break;
            }
        }

        if (!has_access)
            return FALSE;

        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_RECALL) && !Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IDLE))
            return I18n.T("The shuttle is already in transit.");
        if (Dm.eq(SSshuttle.shuttle_purchased, SHUTTLEPURCHASE_PURCHASED))
            return I18n.T("A replacement shuttle has already been purchased.");
        if (Dm.eq(SSshuttle.shuttle_purchased, SHUTTLEPURCHASE_FORCED))
            return I18n.T("Due to unforseen circumstances, shuttle purchasing is no longer available.");
        return TRUE;
    }

    /// Returns whether we are authorized to buy this specific shuttle.
    /// Does not handle prerequisite checks, as those should still *show*.
    // tg: code/game/machinery/computer/communications.dm:746
    public virtual object can_purchase_this_shuttle(datum_map_template_shuttle shuttle_template = null)
    {
        if (shuttle_template.who_can_purchase == null)
            return FALSE;

        if (Dm.truthy(shuttle_template.emag_only))
            return Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? TRUE : FALSE;

        foreach (object access in Dm.each(authorize_access))
        {
            if (Dm.@in(access, shuttle_template.who_can_purchase))
                return TRUE;
        }

        return FALSE;
    }

    /// Used in checks of dept-locked shuttles.
    /// Determines number of employees in department.
    // tg: code/game/machinery/computer/communications.dm:761  (target_department_type is object: a department's typepath, or
    // its name where ui_data passes one, translation-rules Appendix A)
    public virtual object get_department_employee_count(object target_department_type = null)
    {
        if (!Dm.truthy(target_department_type))
            return null;
        var current_department = SSjob.joinable_departments_by_type[target_department_type] as datum_job_department;
        if (current_department == null)
            return null;
        float total_crew_count = 0;
        foreach (datum_job current_job in Dm.each<datum_job>(current_department.department_jobs))
            total_crew_count += Dm.num(current_job.current_positions);
        return total_crew_count;
    }

    // tg: code/game/machinery/computer/communications.dm:778
    public virtual async DmTask make_announcement(mob_living user = null)
    {
        bool is_ai = HAS_SILICON_ACCESS(user);
        if (!Dm.truthy(GLOB.communications_controller.can_announce(user, is_ai ? TRUE : FALSE)))
        {
            to_chat(user, span_alert("Intercomms recharging. Please stand by."));
            return;
        }
        object input = await tgui_input_text(user, "Message to announce to the station crew", "Announcement", max_length: MAX_MESSAGE_LEN);
        if (!Dm.truthy(input) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return;
        if (Dm.truthy(await user.try_speak(input)))
        {
            //Adds slurs and so on. Someone should make this use languages too.
            var input_data = (DmList)user.treat_message(input);
            input = input_data["message"];
        }
        else
        {
            //No cheating, mime/random mute guy!
            input = "...";
            user.visible_message(
                span_notice(Dm.text("[] holds down []'s announcement button, leaving the mic on in awkward silence.", user, this)),
                span_notice("You leave the mic on in awkward silence..."),
                span_hear("You hear an awkward silence, somehow."),
                vision_distance: 4);
        }

        DmList players = get_communication_players();
        await GLOB.communications_controller.make_announcement(user, is_ai ? TRUE : FALSE, input, Dm.truthy(syndicate) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? TRUE : FALSE, players);
        deadchat_broadcast(I18n.T(" made a priority announcement from [].", span_name(Dm.text("[]", get_area_name(user, TRUE)))), span_name(Dm.text("[]", user.real_name)), user, message_type: DEADCHAT_ANNOUNCEMENT);
    }

    // tg: code/game/machinery/computer/communications.dm:804
    public virtual DmList get_communication_players()
    {
        return GLOB.player_list;
    }

    // tg: code/game/machinery/computer/communications.dm:807
    public virtual void post_status(object command = null, object data1 = null, object data2 = null)
    {
        var frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS) as datum_radio_frequency;

        if (frequency == null)
            return;

        var status_signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("command", command)));
        if (Dm.eq(command, "message"))
        {
            status_signal.data["top_text"] = data1;
            status_signal.data["bottom_text"] = data2;
            log_game(Dm.text("[] has changed the station status display message to \"[] []\" []", key_name(usr), data1, data2, loc_name(usr)));
        }
        else if (Dm.eq(command, "alert"))
        {
            status_signal.data["picture_state"] = data1;
            log_game(Dm.text("[] has changed the station status display message to \"[]\" []", key_name(usr), data1, loc_name(usr)));
        }

        frequency.post_signal(this, status_signal);
    }

    // tg: code/game/machinery/computer/communications.dm:828
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.shuttle_caller_list, this);
        SSshuttle?.autoEvac(); // a host-built room without the Master has no SSshuttle
        return base.Destroy(force);
    }

    /// Override the cooldown for special actions
    /// Used in places such as CentCom messaging back so that the crew can answer right away
    // tg: code/game/machinery/computer/communications.dm:835
    public virtual void override_cooldown()
    {
        important_action_cooldown = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm), inline
    }

    // tg: code/game/machinery/computer/communications.dm:838
    public virtual void add_message(datum_comm_message new_message = null)
    {
        // LAZYADD(messages, new_message) (__HELPERS/_lists.dm), expanded inline
        messages ??= Dm.list();
        Dm.append(ref messages, new_message);
    }

    // tg: code/game/machinery/computer/communications.dm:841-851  the hack results and their ghost thresholds
    private const string HACK_PIRATE = "Pirates";
    private const string HACK_FUGITIVES = "Fugitives";
    private const string HACK_SLEEPER = "Sleeper Agents";
    private const float MIN_GHOSTS_FOR_PIRATES = 4;
    private const float MIN_GHOSTS_FOR_FUGITIVES = 6;

    /// Begin the process of hacking into the comms console to call in a threat.
    // tg: code/game/machinery/computer/communications.dm:854  (its only caller is the space ninja's hacker module, not ported)
    public virtual async DmTask<object> try_hack_console(mob_living hacker = null, float? duration = null)
    {
        duration ??= 300; // 30 SECONDS
        if (!Dm.truthy(can_hack(hacker, feedback: TRUE)))
            return FALSE;

        AI_notify_hack();
        if (!Dm.truthy(await do_after(hacker, duration, this, extra_checks: Dm.@new<datum_callback>(this, nameof(can_hack), hacker))))
            return FALSE;

        await hack_console(hacker);
        return TRUE;
    }

    /// Checks if this console is hackable. Used as a callback during try_hack_console's doafter as well.
    // tg: code/game/machinery/computer/communications.dm:866
    public virtual object can_hack(mob_living hacker = null, float? feedback = null)
    {
        feedback ??= FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            if (Dm.truthy(feedback) && hacker != null)
                balloon_alert(hacker, I18n.T("can't hack!"));
            return FALSE;
        }
        var console_area = get_area(this) as area;
        if (console_area == null || !Dm.truthy(Dm.bitand(console_area.area_flags, VALID_TERRITORY)))
        {
            if (Dm.truthy(feedback) && hacker != null)
                balloon_alert(hacker, I18n.T("signal too weak!"));
            return FALSE;
        }
        return TRUE;
    }

    /**
     * The communications console hack,
     * called by certain antagonist actions.
     *
     * Brings in additional threats to the round.
     *
     * hacker - the mob that caused the hack
     */
    // tg: code/game/machinery/computer/communications.dm:886
    public virtual async DmTask hack_console(mob_living hacker = null)
    {
        // All hack results we'll choose from.
        DmList hack_options = Dm.list(HACK_SLEEPER);

        // If we have a certain amount of ghosts, we'll add some more !!fun!! options to the list
        float num_ghosts = Dm.num(length(GLOB.current_observers_list)) + Dm.num(length(GLOB.dead_player_list));

        // Pirates / Fugitives have enough lead in time that there's no point summoning them if the shuttle is called
        // Both of these events also summon space ships and so cannot run on planetary maps
        if (datum_controller_subsystem_shuttle.EMERGENCY_IDLE_OR_RECALLED() && !Dm.truthy(SSmapping.is_planetary()))
        {
            // Pirates require ghosts for the pirates obviously
            if (num_ghosts >= MIN_GHOSTS_FOR_PIRATES)
                Dm.append(ref hack_options, HACK_PIRATE);
            // Fugitives require ghosts for both fugitives and hunters (Please no waldo)
            if (num_ghosts >= MIN_GHOSTS_FOR_FUGITIVES)
                Dm.append(ref hack_options, HACK_FUGITIVES);
        }

        object picked_option = pick(hack_options);
        message_admins(Dm.text("[] hacked a [] located at [], resulting in: []!", key_name_admin(hacker), name, AREACOORD(this), picked_option));
        hacker.log_message(Dm.text("hacked a communications console, resulting in: [].", picked_option), LOG_GAME, log_globally: TRUE);
        if (Dm.eq(picked_option, HACK_PIRATE)) // Triggers pirates, which the crew may be able to pay off to prevent
        {
            DmList pirate_rulesets = Dm.list(
                typeof(datum_dynamic_ruleset_midround_pirates),
                typeof(datum_dynamic_ruleset_midround_pirates_heavy));
            await SSdynamic.force_run_midround(pick(pirate_rulesets));
        }
        else if (Dm.eq(picked_option, HACK_FUGITIVES)) // Triggers fugitives, which can cause confusion / chaos as the crew decides which side help
        {
            priority_announce(
                I18n.T("Attention crew: sector monitoring reports a jump-trace from an unidentified vessel destined for your system. Prepare for probable contact."),
                I18n.T("[] High-Priority Update", command_name()));
            await SSdynamic.force_run_midround(typeof(datum_dynamic_ruleset_midround_from_ghosts_fugitives));
        }
        else if (Dm.eq(picked_option, HACK_SLEEPER)) // Trigger one or multiple sleeper agents with the crew (or for latejoining crew)
        {
            priority_announce(
                I18n.T("Attention crew, it appears that someone on your station has hijacked your telecommunications and broadcasted an unknown signal."),
                I18n.T("[] High-Priority Update", command_name()));
            float max_number_of_sleepers = clamp(round(Dm.num(length(GLOB.alive_player_list)) / 40), 1, 3);
            if (!Dm.truthy(await SSdynamic.force_run_midround(typeof(datum_dynamic_ruleset_midround_from_living_traitor), forced_max_cap: max_number_of_sleepers)))
                await SSdynamic.queue_ruleset(typeof(datum_dynamic_ruleset_latejoin_traitor));
        }
    }
}

// tg: code/game/machinery/computer/communications.dm:62
public partial class obj_machinery_computer_communications_syndicate : obj_machinery_computer_communications
{
    // tg: code/game/machinery/computer/communications.dm:71
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/machinery/computer/communications.dm:74
    public override object can_buy_shuttles(mob user = null)
    {
        return FALSE;
    }

    // tg: code/game/machinery/computer/communications.dm:77
    public override object can_send_messages_to_other_sectors(mob user = null)
    {
        return FALSE;
    }

    // tg: code/game/machinery/computer/communications.dm:80
    public override object authenticated_as_silicon_or_captain(mob user = null)
    {
        return FALSE;
    }

    // tg: code/game/machinery/computer/communications.dm:83
    public override DmList get_communication_players()
    {
        DmList targets = Dm.list();
        foreach (mob target in Dm.each<mob>(GLOB.player_list))
        {
            if (Dm.eq(target.stat, DEAD) || Dm.eq(target.z, z) || Dm.truthy(target.mind?.has_antag_datum(typeof(datum_antagonist_battlecruiser))))
                Dm.append(ref targets, target);
        }
        return targets;
    }
}

// tg: code/game/machinery/computer/communications.dm:938
public partial class datum_comm_message : datum
{
    // tg: code/game/machinery/computer/communications.dm:945
    [DmParams("new_title", "new_content", "new_possible_answers")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New();
        object new_title = Dm.arg(__extra, 0);
        object new_content = Dm.arg(__extra, 1);
        object new_possible_answers = Dm.arg(__extra, 2);
        if (Dm.truthy(new_title))
            title = new_title;
        if (Dm.truthy(new_content))
            content = new_content;
        if (Dm.truthy(new_possible_answers))
            possible_answers = new_possible_answers as DmList;
    }

    // tg: code/game/machinery/computer/communications.dm:954
    public override object Destroy(float? force = null)
    {
        answer_callback = null;
        return base.Destroy(force);
    }
}
