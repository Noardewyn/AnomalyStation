// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/chatroom/conversation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4b, D327): an NTNRC chat channel: its messages, its clients (online, offline, muted) and its operator. The
// var block is generated (D111) but for `id`, a number (the types tool reads the static counter's type).

// tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:3
public partial class datum_ntnet_conversation : datum
{
    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:1  #define MAX_CHANNELS 1000 (the file's define)
    private const float MAX_CHANNELS = 1000;

    ///ID using the UID.
    public object id; // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:14 (hand-declared: a number)

    ///A static UID to ensure no conversations are the same.
    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:12  var/static/ntnrc_uid = 0
    private static float ntnrc_uid
    {
        get => TgState.Current.datum_ntnet_conversation__ntnrc_uid;
        set => TgState.Current.datum_ntnet_conversation__ntnrc_uid = value;
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:30
    public override async DmTask New(params object[] __extra)
    {
        object title = Dm.arg(__extra, 0);
        object strong = Dm.arg(__extra, 1) ?? FALSE;
        this.title = title as string;
        this.strong = Dm.num(strong);

        id = ntnrc_uid;
        ntnrc_uid++;
        if (Dm.num(id) > MAX_CHANNELS)
        {
            qdel(this);
            return;
        }
        SSmodular_computers.chat_channels.Add(this);
        await base.New();
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:42
    public override object Destroy(float? force = null)
    {
        SSmodular_computers.chat_channels.Remove(this);
        foreach (datum_computer_file_program_chatclient chatterbox in Dm.each<datum_computer_file_program_chatclient>(Dm.bitor(active_clients, offline_clients)))
            purge_client(chatterbox);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:48
    public virtual void add_message(object message = null, object username = null)
    {
        message = Dm.text("[] []: []", round_timestamp(format: "hh:mm"), username, message);
        messages[Dm.text("[]", next_message_id)] = message;
        next_message_id++;
        trim_message_list();
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:54
    public virtual void add_status_message(object message = null)
    {
        message = Dm.text("[] -!- []", round_timestamp(format: "hh:mm"), message);
        messages[Dm.text("[]", next_message_id)] = message;
        next_message_id++;
        trim_message_list();
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:60
    public virtual void trim_message_list()
    {
        if (messages.len <= 50)
            return;
        messages = messages.Copy(messages.len - 50, 0);
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:65
    public virtual void add_client(datum_computer_file_program_chatclient new_user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (new_user == null)
            return;
        Dm.combine(ref new_user.conversations, this);
        active_clients.Add(new_user);
        if (!Dm.truthy(silent))
            add_status_message(Dm.text("[] has joined the channel.", new_user.username));
        // No operator, so we assume the channel was empty. Assign this user as operator, without the message, since you're the creator.
        if (channel_operator == null)
            changeop(new_user, silent: TRUE);
    }

    //Clear all of our references to a client, used for client deletion
    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:77
    public virtual void purge_client(datum_computer_file_program_chatclient forget = null)
    {
        remove_client(forget);
        Dm.remove(ref forget.conversations, this);
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:81
    public virtual void remove_client(datum_computer_file_program_chatclient leaving = null)
    {
        if (leaving == null)
            return;
        if (Dm.@in(leaving, active_clients))
        {
            active_clients.Remove(leaving);
            add_status_message(Dm.text("[] has left the channel.", leaving.username));
        }
        Dm.remove(ref muted_clients, leaving);
        Dm.remove(ref offline_clients, leaving);

        // Channel operator left, pick new operator
        if (leaving == channel_operator)
        {
            channel_operator = null;
            if (active_clients.len > 0)
            {
                datum_computer_file_program_chatclient newop = pick(active_clients) as datum_computer_file_program_chatclient;
                changeop(newop);
            }
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:97
    public virtual void go_offline(datum_computer_file_program_chatclient offline = null)
    {
        if (offline == null || !Dm.@in(offline, active_clients))
            return;
        active_clients.Remove(offline);
        offline_clients.Add(offline);
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:103
    public virtual void mute_user(datum_computer_file_program_chatclient op = null, datum_computer_file_program_chatclient muted = null)
    {
        if (!Dm.truthy(op.netadmin_mode) && channel_operator != op) //sanity even if the person shouldn't be able to see the mute button
            return;
        if (Dm.@in(muted, muted_clients))
        {
            muted_clients.Remove(muted);
            muted.computer.alert_call(muted, Dm.text("You have been unmuted from []!", title), Dm.resource("sound/machines/ping.ogg"));
        }
        else
        {
            muted_clients.Add(muted);
            muted.computer.alert_call(muted, Dm.text("You have been muted from []!", title));
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:113
    public virtual void ping_user(datum_computer_file_program_chatclient pinger = null, datum_computer_file_program_chatclient pinged = null)
    {
        if (Dm.@in(pinger, muted_clients)) //oh my god fuck off
            return;
        add_status_message(Dm.text("[] pinged [].", pinger.username, pinged.username));
        pinged.computer.alert_call(pinged, Dm.text("You have been pinged in [] by []!", title, pinger.username), Dm.resource("sound/machines/ping.ogg"));
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:119
    public virtual void changeop(datum_computer_file_program_chatclient newop = null, float? silent = null)
    {
        silent ??= FALSE;
        if (newop == null)
            throw Dm.CRASH(Dm.text("[] is attempting to add [] as the operator, but it isn't a chat client.", this, newop), "code/modules/modular_computers/file_system/programs/chatroom/conversation.dm", 121);
        channel_operator = newop;
        if (!Dm.truthy(silent))
            add_status_message(Dm.text("Channel operator status transferred to [].", newop.username));
    }

    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:126
    public virtual object change_title(object newtitle = null, datum_computer_file_program_chatclient renamer = null)
    {
        if (channel_operator != renamer || Dm.truthy(strong)) // Not Authorised or channel cannot be editted
            return FALSE;
        add_status_message(Dm.text("[] has changed channel title from [] to []", renamer.username, title, newtitle));
        title = newtitle as string;
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/chatroom/conversation.dm:12  the conversation's var/static/ntnrc_uid (rules 10.6)
    internal float datum_ntnet_conversation__ntnrc_uid;
}
