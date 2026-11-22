// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4b, D327): the NTNRC chat client, with its NtosNetChat window: channels to create, join, leave, rename,
// protect with a password or delete, messages, pings and mutes, the screen name, the network administrator's mode and the channel's
// log saved as a file. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:11
public partial class datum_computer_file_program_chatclient : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:1-9  the file's defines
    private const float USERNAME_SIZE = 32;
    private const float CHANNELNAME_SIZE = 18;
    private const float MESSAGE_SIZE = 2048;
    private const float PING_COOLDOWN_TIME = 3 * SECONDS;
    private const float STATUS_ONLINE = 3;
    private const float STATUS_AWAY = 2;
    private const float STATUS_OFFLINE = 1;

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:37
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        if (!Dm.truthy(username))
            username = Dm.text("DefaultUser[]", rand(100, 999));
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:42
    public override object Destroy(float? force = null)
    {
        foreach (datum_ntnet_conversation discussion in Dm.each<datum_ntnet_conversation>(conversations.Copy()))
            discussion.purge_client(this);
        conversations.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:48
    public virtual datum_ntnet_conversation create_new_channel(object channel_title = null, float? strong = null)
    {
        strong ??= FALSE;
        datum_ntnet_conversation new_converstaion = Dm.@new<datum_ntnet_conversation>(channel_title, strong);
        new_converstaion.add_client(this);
        new_converstaion.title = channel_title as string;
        active_channel = new_converstaion.id;
        return new_converstaion;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:55
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        datum_ntnet_conversation channel = SSmodular_computers.get_chat_channel_by_id(active_channel);
        bool authed = false;
        if (channel != null && (channel.channel_operator == this || Dm.truthy(netadmin_mode)))
            authed = true;

        switch (action)
        {
            case "PRG_speak":
            {
                if (channel == null || active_channel == null)
                    return dot;
                if (Dm.@in(this, channel.muted_clients)) // Make sure we aren't muted
                    return dot;
                object message = reject_bad_chattext(@params["message"], MESSAGE_SIZE);
                if (!Dm.truthy(message))
                    return dot;
                if (Dm.truthy(channel.password) && (!Dm.@in(this, channel.active_clients) && !Dm.@in(this, channel.offline_clients)))
                {
                    if (Dm.eq(channel.password, message))
                    {
                        channel.add_client(this);
                        return TRUE;
                    }
                }

                channel.add_message(message, username);
                mob_living user = usr as mob_living;
                user.log_talk(message, LOG_CHAT, tag: Dm.text("as [] to channel []", username, channel.title));
                return TRUE;
            }
            case "PRG_joinchannel":
            {
                object new_target = text2num(@params["id"]);
                if (new_target == null || Dm.eq(new_target, active_channel))
                    return dot;

                if (Dm.truthy(netadmin_mode))
                {
                    active_channel = new_target; // Bypasses normal leave/join and passwords. Technically makes the user invisible to others.
                    return TRUE;
                }

                active_channel = new_target;
                channel = SSmodular_computers.get_chat_channel_by_id(new_target);
                if ((!Dm.@in(this, channel.active_clients) && !Dm.@in(this, channel.offline_clients)) && !Dm.truthy(channel.password))
                    channel.add_client(this);
                return TRUE;
            }
            case "PRG_leavechannel":
                if (channel != null)
                {
                    channel.remove_client(this);
                    active_channel = null;
                    return TRUE;
                }
                break;
            case "PRG_newchannel":
            {
                object channel_title = reject_bad_chattext(@params["new_channel_name"], CHANNELNAME_SIZE);
                if (!Dm.truthy(channel_title))
                    return dot;
                create_new_channel(channel_title);
                return TRUE;
            }
            case "PRG_toggleadmin":
            {
                if (Dm.truthy(netadmin_mode))
                {
                    netadmin_mode = FALSE;
                    channel?.add_client(this);
                    return TRUE;
                }
                mob_living user = usr as mob_living;
                if (Dm.truthy(can_run(user, TRUE, Dm.list(ACCESS_NETWORK))))
                {
                    foreach (datum_ntnet_conversation channels in Dm.each<datum_ntnet_conversation>(SSmodular_computers.chat_channels))
                        channels.remove_client(this);
                    netadmin_mode = TRUE;
                    return TRUE;
                }
                break;
            }
            case "PRG_changename":
            {
                object newname = reject_bad_chattext(@params["new_name"], USERNAME_SIZE);
                newname = replacetext(newname, " ", "_");
                if (!Dm.truthy(newname) || Dm.eq(newname, username))
                    return dot;
                foreach (datum_ntnet_conversation anychannel in Dm.each<datum_ntnet_conversation>(SSmodular_computers.chat_channels))
                {
                    if (Dm.@in(this, anychannel.active_clients))
                        anychannel.add_status_message(Dm.text("[] is now known as [].", username, newname));
                }
                username = newname;
                return TRUE;
            }
            case "PRG_savelog":
            {
                if (channel == null)
                    return dot;
                object logname = trim(@params["log_name"], MAX_MESSAGE_LEN);
                if (!Dm.truthy(length(logname)) || !Dm.truthy(filter_filename_pda(logname)))
                    return dot;
                datum_computer_file_data_text logfile = Dm.@new<datum_computer_file_data_text>();
                // Now we will generate HTML-compliant file that can actually be viewed/printed.
                logfile.filename = logname as string;
                logfile.stored_text = Dm.text("\\[b\\]Logfile dump from NTNRC channel []\\[/b\\]\\[BR\\]", channel.title);
                foreach (object message_id in Dm.each(channel.messages))
                    logfile.stored_text = Dm.text("[][]\\[BR\\]", logfile.stored_text, channel.messages[message_id]);
                logfile.stored_text = Dm.text("[]\\[b\\]Logfile dump completed.\\[/b\\]", logfile.stored_text);
                logfile.calculate_size();
                if (computer == null || !Dm.truthy(computer.store_file(logfile)))
                {
                    if (computer == null)
                    {
                        // This program shouldn't even be runnable without computer.
                        throw Dm.CRASH("Var computer is null!", "code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm", 143);
                    }
                    computer.visible_message(span_warning(I18n.T("\\The [] shows an \"I/O Error - Hard drive may be full. Please free some space and try again. Required space: []GQ\" warning.", computer, logfile.size)));
                }
                return TRUE;
            }
            case "PRG_renamechannel":
            {
                if (!authed)
                    return dot;
                object newname = reject_bad_chattext(@params["new_name"], CHANNELNAME_SIZE);
                if (!Dm.truthy(newname) || channel == null)
                    return dot;
                channel.add_status_message(Dm.text("Channel renamed from [] to [] by operator.", channel.title, newname));
                channel.title = newname as string;
                return TRUE;
            }
            case "PRG_deletechannel":
                if (authed)
                {
                    qdel(channel);
                    active_channel = null;
                    return TRUE;
                }
                break;
            case "PRG_setpassword":
            {
                if (!authed)
                    return dot;
                object new_password = sanitize(@params["new_password"]);
                if (!authed)
                    return dot;
                channel.password = new_password;
                return TRUE;
            }
            case "PRG_mute_user":
            {
                if (!authed)
                    return dot;
                // locate(params["ref"]) in channel.active_clients + channel.offline_clients
                datum_computer_file_program_chatclient muted = Dm.locate_ref(@params["ref"]) as datum_computer_file_program_chatclient;
                if (muted != null && !Dm.@in(muted, Dm.add(channel.active_clients, channel.offline_clients)))
                    muted = null;
                channel.mute_user(this, muted);
                return TRUE;
            }
            case "PRG_ping_user":
            {
                // COOLDOWN_FINISHED(src, ping_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
                if (!(ping_cooldown <= world.time))
                    return dot;
                if (Dm.@in(this, channel.muted_clients))
                    return dot;
                // locate(params["ref"]) in channel.active_clients + channel.offline_clients
                datum_computer_file_program_chatclient pinged = Dm.locate_ref(@params["ref"]) as datum_computer_file_program_chatclient;
                if (pinged != null && !Dm.@in(pinged, Dm.add(channel.active_clients, channel.offline_clients)))
                    pinged = null;
                channel.ping_user(this, pinged);
                // COOLDOWN_START(src, ping_cooldown, PING_COOLDOWN_TIME) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
                ping_cooldown = world.time + PING_COOLDOWN_TIME;
                return TRUE;
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:184
    public override object process_tick(object seconds_per_tick = null)
    {
        object dot = base.process_tick(seconds_per_tick);

        if (!Dm.@in(this, computer.idle_threads))
            return dot;

        datum_ntnet_conversation watched_channel = SSmodular_computers.get_chat_channel_by_id(active_channel);
        if (watched_channel == null) // If we're not in a channel, no need for a message notification header.
        {
            ui_header = null;
            return dot;
        }
        if (!Dm.truthy(length(watched_channel.messages))) // But if there's no messages, we do still wait for a message.
        {
            ui_header = "ntnrc_idle.gif";
            return dot;
        }

        object last_message_id_found = watched_channel.messages[watched_channel.messages.len];
        if (Dm.eq(last_message_id_found, last_message_id))
        {
            ui_header = "ntnrc_idle.gif";
            return dot;
        }
        ui_header = "ntnrc_new.gif";
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:204
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        if (!Dm.truthy(dot))
            return dot;
        foreach (datum_ntnet_conversation channel in Dm.each<datum_ntnet_conversation>(SSmodular_computers.chat_channels))
        {
            if (Dm.@in(this, channel.offline_clients))
            {
                channel.offline_clients.Remove(this);
                channel.active_clients.Add(this);
            }
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:213
    public override object kill_program(mob user = null)
    {
        foreach (datum_ntnet_conversation channel in Dm.each<datum_ntnet_conversation>(SSmodular_computers.chat_channels))
            channel.go_offline(this);
        active_channel = null;
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:219
    public override object background_program(mob user = null)
    {
        object dot = base.background_program(user);
        datum_ntnet_conversation open_channel = SSmodular_computers.get_chat_channel_by_id(active_channel);
        if (open_channel == null || !Dm.truthy(length(open_channel.messages)))
        {
            last_message_id = null;
            ui_header = null;
            return dot;
        }

        last_message_id = open_channel.messages[open_channel.messages.len];
        ui_header = "ntnrc_idle.gif";
        return dot;
    }

    /// Converts active/idle/closed to a numerical status for sorting clients by.
    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:231
    public virtual float get_numerical_status()
    {
        if (this == computer.active_program)
            return STATUS_ONLINE;
        if (Dm.@in(this, computer.idle_threads))
            return STATUS_AWAY;
        return STATUS_OFFLINE;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:238
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["selfref"] = REF(this); //used to verify who is you, as usernames can be copied.
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/ntnrc_client.dm:243
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList all_channels = Dm.list();
        foreach (datum_ntnet_conversation conversations in Dm.each<datum_ntnet_conversation>(SSmodular_computers.chat_channels))
        {
            if (Dm.truthy(conversations.title))
            {
                all_channels.Add(Dm.list(Dm.list(
                    Dm.kv("chan", conversations.title),
                    Dm.kv("id", conversations.id))));
            }
        }
        data["all_channels"] = all_channels;
        data["active_channel"] = active_channel;

        datum_ntnet_conversation channel = SSmodular_computers.get_chat_channel_by_id(active_channel);
        bool authed = false;
        data["clients"] = Dm.list();
        data["messages"] = Dm.list();
        if (channel != null)
        {
            data["title"] = channel.title;
            if (!Dm.truthy(channel.password) || Dm.truthy(netadmin_mode))
                authed = true;
            DmList clients = Dm.list();
            foreach (datum_computer_file_program_chatclient channel_client in Dm.each<datum_computer_file_program_chatclient>(Dm.add(channel.active_clients, channel.offline_clients)))
            {
                if (channel_client == this)
                    authed = true;
                clients.Add(Dm.list(Dm.list(
                    Dm.kv("name", channel_client.username),
                    Dm.kv("online", channel_client == channel_client.computer.active_program ? TRUE : FALSE),
                    Dm.kv("away", Dm.@in(channel_client, channel_client.computer.idle_threads) ? TRUE : FALSE),
                    Dm.kv("muted", Dm.@in(channel_client, channel.muted_clients) ? TRUE : FALSE),
                    Dm.kv("operator", channel.channel_operator == channel_client ? TRUE : FALSE),
                    Dm.kv("status", channel_client.get_numerical_status()),
                    Dm.kv("ref", REF(channel_client)))));
            }
            //no fishing for ui data allowed
            if (authed)
            {
                data["strong"] = channel.strong;
                data["clients"] = clients;
                DmList messages = Dm.list();
                for (int i = (int)channel.messages.len; i >= 1; i--)
                {
                    object message_id = channel.messages[i];
                    messages.Add(Dm.list(Dm.list(
                        Dm.kv("key", message_id),
                        Dm.kv("msg", channel.messages[message_id]))));
                }
                data["messages"] = messages;
                data["is_operator"] = channel.channel_operator == this || Dm.truthy(netadmin_mode) ? TRUE : FALSE;
            }
        }

        data["username"] = username;
        data["adminmode"] = netadmin_mode;
        data["can_admin"] = can_run(user, FALSE, Dm.list(ACCESS_NETWORK));
        data["authed"] = authed ? TRUE : FALSE;
        return data;
    }
}
