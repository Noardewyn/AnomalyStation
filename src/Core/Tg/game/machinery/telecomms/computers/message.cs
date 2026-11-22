// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/computers/message.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice E (the PDA messages' server): the message monitor console (message.dm:1-283): its link to the messaging server, the
// decryption key login, the PDA message logs, the server's switch, the key change, the forged message and the malfunctioning
// silicons' hack, with its MessageMonitor window, and emag_act (G1a, D316: the emag prints a monitor key paper). Left out with a comment:
// the forged message's talk log (the logs, M10). The monitor decryption key paper
// (/obj/item/paper/monitorkey) came with the paperwork (gaps S3). The var block is
// generated (D111).

// tg: code/game/machinery/telecomms/computers/message.dm:6  #define MSG_MON_SCREEN_MAIN 0, _LOGS 1, _REQUEST_LOGS 2, _HACKED 3 (file-local)
// tg: code/game/machinery/telecomms/computers/message.dm:11
public partial class obj_machinery_computer_message_monitor : obj_machinery_computer
{
    private const float MSG_MON_SCREEN_MAIN = 0;
    private const float MSG_MON_SCREEN_LOGS = 1;
    private const float MSG_MON_SCREEN_REQUEST_LOGS = 2;
    private const float MSG_MON_SCREEN_HACKED = 3;

    // tg: code/game/machinery/telecomms/computers/message.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:38
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        //Is the server isn't linked to a server, and there's a server available, default it to the first one in the list.
        if (linked_server == null)
        {
            foreach (obj_machinery_telecomms_message_server message_server in Dm.each<obj_machinery_telecomms_message_server>(GLOB.telecomm_machines))
            {
                set_linked_server(message_server);
                break;
            }
        }
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:46
    public override object Destroy(float? force = null)
    {
        set_linked_server(null);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:50
    public virtual void set_linked_server(obj_machinery_telecomms_message_server new_server = null)
    {
        if (linked_server != null)
            Dm.remove(ref linked_server.listening_computers, this);
        linked_server = new_server;
        if (linked_server != null)
            Dm.append(ref linked_server.listening_computers, this);
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:57
    public virtual bool is_server_responsive()
    {
        return linked_server != null && !Dm.truthy(Dm.bitand(linked_server.machine_stat, NOPOWER | BROKEN));
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:60
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item I = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            //Stops people from just unscrewing the monitor and putting it back to get the console working again.
            to_chat(user, span_warning(I18n.T("It is too hot to mess with!")));
            return TRUE;
        }
        return await base.screwdriver_act(user, I);
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:67
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        if (linked_server == null)
        {
            to_chat(user, span_notice(I18n.T("A no server error appears on the screen.")));
            return FALSE;
        }

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        screen = MSG_MON_SCREEN_HACKED;
        do_sparks(5, FALSE, this);
        var monitor_key_paper = Dm.@new<obj_item_paper_monitorkey>(loc, linked_server);
        // Will help make emagging the console not so easy to get away with.
        monitor_key_paper.add_raw_text("<br><br><font color='red'>£%@%(*$%&(£&?*(%&£/{}</font>");
        float time = 100 * Dm.length(linked_server.decryptkey);
        _addtimer(Dm.@new<datum_callback>(this, nameof(unemag_console)), time, file: "code/game/machinery/telecomms/computers/message.dm", line: 81);
        error_message = "%$&(£: Critical %$$@ Error // !RestArting! <lOadiNg backUp iNput ouTput> - ?pLeaSe wAit!";
        linked_server.toggled = FALSE;
        return TRUE;
    }

    /// Remove the emag effect from the console
    // tg: code/game/machinery/telecomms/computers/message.dm:87
    public virtual void unemag_console()
    {
        screen = MSG_MON_SCREEN_MAIN;
        linked_server.toggled = TRUE;
        error_message = "";
        obj_flags = Dm.num(Dm.bitand(obj_flags, ~EMAGGED));
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:93
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list(
            Dm.kv("screen", screen),
            Dm.kv("error_message", error_message),
            Dm.kv("notice_message", notice_message),
            Dm.kv("success_message", success_message),
            Dm.kv("auth", authenticated),
            Dm.kv("server_status", is_server_responsive() ? TRUE : FALSE));

        if (screen == MSG_MON_SCREEN_MAIN)
        {
            data["password"] = password;
            data["status"] = linked_server?.on;
            // Check is AI or cyborg malf
            data["is_malf"] = user is mob_living_silicon silicon_user && Dm.truthy(silicon_user.hack_software) ? TRUE : FALSE;
        }
        else if (screen == MSG_MON_SCREEN_LOGS)
        {
            DmList message_list = Dm.list();
            foreach (datum_data_tablet_msg pda in Dm.each<datum_data_tablet_msg>(linked_server.pda_msgs))
                Dm.append(ref message_list, Dm.list(Dm.list(Dm.kv("ref", REF(pda)), Dm.kv("sender", pda.sender), Dm.kv("recipient", pda.recipient), Dm.kv("message", pda.message))));
            data["messages"] = message_list;
        }
        else if (screen == MSG_MON_SCREEN_REQUEST_LOGS)
        {
            DmList request_list = Dm.list();
            foreach (datum_data_rc_msg rc in Dm.each<datum_data_rc_msg>(linked_server.rc_msgs))
                Dm.append(ref request_list, Dm.list(Dm.list(Dm.kv("ref", REF(rc)), Dm.kv("message", rc.message), Dm.kv("stamp", rc.stamp), Dm.kv("id_auth", rc.id_auth), Dm.kv("departament", rc.sender_department))));
            data["requests"] = request_list;
        }
        return data;
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:123
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["is_on_station"] = is_on_station() ? TRUE : FALSE;
        return data;
    }

    /// Check if this console is on the station and in a valid area
    // tg: code/game/machinery/telecomms/computers/message.dm:129
    public virtual bool is_on_station()
    {
        if (!Dm.truthy(is_station_level(z)))
            return false;
        if (!(get_area(this) is area_station area) || !Dm.truthy(Dm.bitand(area.area_flags, VALID_TERRITORY)))
            return false;
        return true;
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:137
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        error_message = "";
        success_message = "";
        notice_message = "";

        if (Dm.eq(action, "auth"))
        {
            object authPass = @params["auth_password"];

            if (Dm.truthy(authenticated))
            {
                authenticated = FALSE;
                return TRUE;
            }

            if (!Dm.eq(linked_server.decryptkey, authPass))
            {
                error_message = I18n.T("ALERT: Incorrect decryption key!");
                return TRUE;
            }

            authenticated = TRUE;
            success_message = I18n.T("YOU SUCCESSFULLY LOGGED IN!");

            return TRUE;
        }
        if (Dm.eq(action, "link_server"))
        {
            DmList message_servers = Dm.list();
            foreach (obj_machinery_telecomms_message_server message_server in Dm.each<obj_machinery_telecomms_message_server>(GLOB.telecomm_machines))
                Dm.append(ref message_servers, message_server);

            if (message_servers.len > 1)
            {
                object selected_server = await tgui_input_list(usr, I18n.T("Please select a server"), I18n.T("Server Selection"), message_servers);
                if (QDELETED(this) || !Dm.truthy(is_operational) || !Dm.truthy(await usr.can_interact_with(this)))
                {
                    screen = MSG_MON_SCREEN_MAIN;
                    return TRUE;
                }
                set_linked_server(selected_server as obj_machinery_telecomms_message_server);
                if (linked_server != null)
                    notice_message = I18n.T("NOTICE: Server selected.");
            }
            else if (message_servers.len == 1)
            {
                set_linked_server(message_servers[1] as obj_machinery_telecomms_message_server);
                notice_message = I18n.T("NOTICE: Only Single Server Detected - Server selected.");
            }
            else
                error_message = I18n.T("ALERT: No server detected.");
            screen = MSG_MON_SCREEN_MAIN;
            return TRUE;
        }
        if (Dm.eq(action, "turn_server"))
        {
            if (!is_server_responsive())
            {
                error_message = I18n.T("ALERT: No server detected.");
                return TRUE;
            }

            linked_server.toggled = !Dm.truthy(linked_server.toggled) ? TRUE : FALSE;
            return TRUE;
        }
        if (Dm.eq(action, "view_message_logs"))
        {
            screen = MSG_MON_SCREEN_LOGS;
            return null;
        }
        if (Dm.eq(action, "view_request_logs"))
        {
            screen = MSG_MON_SCREEN_REQUEST_LOGS;
            return TRUE;
        }
        if (Dm.eq(action, "clear_message_logs"))
        {
            linked_server.pda_msgs = Dm.list();
            notice_message = I18n.T("NOTICE: Logs cleared.");
            return TRUE;
        }
        if (Dm.eq(action, "clear_request_logs"))
        {
            linked_server.rc_msgs = Dm.list();
            notice_message = I18n.T("NOTICE: Logs cleared.");
            return TRUE;
        }
        if (Dm.eq(action, "set_key"))
        {
            if (!is_on_station())
                return TRUE;

            object dkey = await tgui_input_text(usr, I18n.T("Please enter the decryption key"), I18n.T("Telecomms Decryption"), max_length: 16);
            if (QDELETED(this) || !Dm.truthy(is_operational) || !Dm.truthy(await usr.can_interact_with(this)))
                return TRUE;
            if (Dm.truthy(dkey) && !Dm.eq(dkey, ""))
            {
                if (Dm.eq(linked_server.decryptkey, dkey))
                {
                    object newkey = await tgui_input_text(usr, I18n.T("Please enter the new key (3 - 16 characters max)"), I18n.T("New Key"), max_length: 16);
                    if (QDELETED(this) || !Dm.truthy(is_operational) || !Dm.truthy(await usr.can_interact_with(this)))
                        return TRUE;
                    if (Dm.length(newkey) <= 3)
                        notice_message = I18n.T("NOTICE: Decryption key too short!");
                    else if (Dm.truthy(newkey) && !Dm.eq(newkey, ""))
                        linked_server.decryptkey = Dm.str(newkey);
                    notice_message = I18n.T("NOTICE: Decryption key set.");
                }
                else
                    error_message = I18n.T("ALERT: Incorrect decryption key!");
            }
            return TRUE;
        }
        if (Dm.eq(action, "return_home"))
        {
            screen = MSG_MON_SCREEN_MAIN;
            return TRUE;
        }
        if (Dm.eq(action, "delete_message"))
        {
            object found = Dm.locate_ref(@params["ref"]);
            Dm.remove(ref linked_server.pda_msgs, Dm.@in(found, linked_server.pda_msgs) ? found : null);
            success_message = I18n.T("Log Deleted!");
            return TRUE;
        }
        if (Dm.eq(action, "delete_request"))
        {
            object found = Dm.locate_ref(@params["ref"]);
            Dm.remove(ref linked_server.rc_msgs, Dm.@in(found, linked_server.rc_msgs) ? found : null);
            success_message = I18n.T("Log Deleted!");
            return TRUE;
        }
        if (Dm.eq(action, "connect_server"))
        {
            if (linked_server != null)
                return TRUE;
            foreach (obj_machinery_telecomms_message_server new_home in Dm.each<obj_machinery_telecomms_message_server>(GLOB.telecomm_machines))
            {
                set_linked_server(new_home);
                break;
            }
            return TRUE;
        }
        if (Dm.eq(action, "send_fake_message"))
        {
            object sender = await tgui_input_text(usr, I18n.T("What is the sender's name?"), I18n.T("Sender"), max_length: MAX_NAME_LEN);
            object job = await tgui_input_text(usr, I18n.T("What is the sender's job?"), I18n.T("Job"), max_length: 60);

            object recipient;
            DmList tablet_to_messenger = Dm.list();
            DmList viewable_tablets = Dm.list();
            foreach (object messenger_ref in Dm.each(GLOB.pda_messengers))
            {
                datum_computer_file_program_messenger message_app = GLOB.pda_messengers[messenger_ref] as datum_computer_file_program_messenger;
                if (message_app == null || Dm.truthy(message_app.invisible))
                    continue;
                if (!Dm.truthy(message_app.computer.saved_identification))
                    continue;
                Dm.append(ref viewable_tablets, message_app.computer);
                tablet_to_messenger[message_app.computer] = message_app;
            }
            if (viewable_tablets.len > 0)
                recipient = await tgui_input_list(usr, I18n.T("Select a tablet from the list"), I18n.T("Tablet Selection"), viewable_tablets);
            else
                recipient = null;

            object message = await tgui_input_text(usr, I18n.T("Please enter your message"), I18n.T("Message"), max_length: MAX_MESSAGE_LEN);
            if (sender == null || Dm.eq(sender, ""))
                sender = "UNKNOWN";

            if (recipient == null)
            {
                notice_message = I18n.T("NOTICE: No recipient selected!");
                return TRUE;
            }
            if (message == null || Dm.eq(message, ""))
            {
                notice_message = I18n.T("NOTICE: No message entered!");
                return TRUE;
            }
            if (QDELETED(this) || !Dm.truthy(is_operational) || !Dm.truthy(await usr.can_interact_with(this)))
                return TRUE;

            datum_signal_subspace_messaging_tablet_message signal = Dm.@new<datum_signal_subspace_messaging_tablet_message>(this, Dm.list(
                Dm.kv("fakename", Dm.text("[]", sender)),
                Dm.kv("fakejob", Dm.text("[]", job)),
                Dm.kv("message", message),
                Dm.kv("targets", Dm.list(tablet_to_messenger[recipient]))));
            // This will log the signal and transmit it to the target
            await linked_server.receive_information(signal, null);
            usr.log_message(Dm.text("(Tablet: [] | []) sent \"[]\" to []", name, usr.real_name, message, signal.format_target()), LOG_PDA);
            return TRUE;
        }
        // Malfunction AI and cyborgs can hack console. This will authenticate the console, but you need to wait password selection
        if (Dm.eq(action, "hack"))
        {
            float time = 10 * SECONDS * Dm.length(linked_server.decryptkey);
            _addtimer(Dm.@new<datum_callback>(this, nameof(unemag_console)), time, file: "code/game/machinery/telecomms/computers/message.dm", line: 247);
            screen = MSG_MON_SCREEN_HACKED;
            error_message = "%$&(£: Critical %$$@ Error // !RestArting! <lOadiNg backUp iNput ouTput> - ?pLeaSe wAit!";
            linked_server.toggled = FALSE;
            authenticated = TRUE;
            return TRUE;
        }
        return TRUE;
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:295
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MessageMonitor", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:302
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // . += get_asset_datum(/datum/asset/spritesheet_batched/chat): the asset transport is the client's (DV44)
        return await base.ui_assets(user);
    }
}

/// Monitor decryption key paper
// tg: code/game/machinery/telecomms/computers/message.dm:312  (gaps S3: the paperwork)
public partial class obj_item_paper_monitorkey : obj_item_paper
{
    // tg: code/game/machinery/telecomms/computers/message.dm:316
    [DmParams("mapload", "server")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_telecomms_message_server server = Dm.arg(__extra, 0) as obj_machinery_telecomms_message_server;
        base.Initialize(mapload, __extra);
        if (server != null)
        {
            print(server);
            return INITIALIZE_HINT_NORMAL;
        }
        return INITIALIZE_HINT_LATELOAD;
    }

    /**
     * Handles printing the monitor key for a given server onto this piece of paper.
     */
    // tg: code/game/machinery/telecomms/computers/message.dm:326
    public virtual void print(obj_machinery_telecomms_message_server server = null)
    {
        add_raw_text(Dm.text("<center><h2>Daily Key Reset</h2></center><br>The new message monitor key is <b>[]</b>.<br>Please keep this a secret and away from the clown.<br>If necessary, change the password to a more secure one.", server.decryptkey));
        add_overlay("paper_words");
        update_appearance();
    }

    // tg: code/game/machinery/telecomms/computers/message.dm:331
    public override void LateInitialize(params object[] __extra)
    {
        foreach (obj_machinery_telecomms_message_server_preset server in Dm.each<obj_machinery_telecomms_message_server_preset>(GLOB.telecomm_machines))
        {
            if (Dm.truthy(server.decryptkey))
            {
                print(server);
                break;
            }
        }
    }
}
