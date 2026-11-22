// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the PDA messenger): the registered messengers sorted by job and by name, a chat and a message. A
// message's photo (photo_name) is always null: the camera's photos are not ported. The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    /// A list of all active and visible messengers
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:2  GLOBAL_LIST_EMPTY_TYPED(pda_messengers, ...)
    public DmList pda_messengers;

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:2  (GLOBAL_MANAGED(pda_messengers, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpda_messengers()
    {
        pda_messengers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "pda_messengers");
    }

    /// A list of all active and visible messengers
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:4  GLOBAL_LIST_EMPTY_TYPED(pda_messengers_by_job, ...)
    public DmList pda_messengers_by_job;

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:4  (GLOBAL_MANAGED(pda_messengers_by_job, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpda_messengers_by_job()
    {
        pda_messengers_by_job = Dm.list();
        Dm.append(ref gvars_datum_init_order, "pda_messengers_by_job");
    }

    /// A list of all active and visible messengers
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:6  GLOBAL_LIST_EMPTY_TYPED(pda_messengers_by_name, ...)
    public DmList pda_messengers_by_name;

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:6  (GLOBAL_MANAGED(pda_messengers_by_name, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpda_messengers_by_name()
    {
        pda_messengers_by_name = Dm.list();
        Dm.append(ref gvars_datum_init_order, "pda_messengers_by_name");
    }
}

public static partial class GlobalProcs
{
    /// Registers an NTMessenger instance to the list of pda_messengers.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:9
    public static void add_messenger(datum_computer_file_program_messenger messenger = null)
    {
        obj_item_modular_computer messenger_device = messenger.computer;
        // a bunch of empty PDAs are normally allocated, we don't want that clutter
        if (!Dm.truthy(messenger_device.saved_identification) || !Dm.truthy(messenger_device.saved_job))
            return;

        if (messenger == null)
            return;

        object messenger_ref = REF(messenger);
        if (Dm.@in(messenger_ref, GLOB.pda_messengers))
            return;

        GLOB.pda_messengers[messenger_ref] = messenger;
        binary_insert_messenger(messenger, GLOB.pda_messengers_by_job, by_job: true);
        binary_insert_messenger(messenger, GLOB.pda_messengers_by_name, by_job: false);
    }

    // BINARY_INSERT_PROC_COMPARE(messenger, LIST, /datum/computer_file/program/messenger, messenger, compare_job or compare_name,
    // COMPARE_KEY) (__HELPERS/_lists.dm:177), expanded inline (rules 11.1) once for both lists
    private static void binary_insert_messenger(datum_computer_file_program_messenger input, DmList list, bool by_job)
    {
        float cttl = list.len;
        if (cttl == 0)
        {
            Dm.append(ref list, input);
            return;
        }
        float left = 1;
        float right = cttl;
        float mid = Dm.num(Dm.shr(left + right, 1));
        datum_computer_file_program_messenger item;
        while (left < right)
        {
            item = list[mid] as datum_computer_file_program_messenger;
            if ((by_job ? item.compare_job(input) : item.compare_name(input)) <= 0)
                left = mid + 1;
            else
                right = mid;
            mid = Dm.num(Dm.shr(left + right, 1));
        }
        item = list[mid] as datum_computer_file_program_messenger;
        mid = (by_job ? item.compare_job(input) : item.compare_name(input)) > 0 ? mid : mid + 1;
        list.Insert(mid, input);
    }

    /// Unregisters an NTMessenger instance from the pda_messengers table.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:28
    public static void remove_messenger(datum_computer_file_program_messenger messenger = null)
    {
        if (messenger == null)
            return;

        object messenger_ref = REF(messenger);
        if (!Dm.@in(messenger_ref, GLOB.pda_messengers))
            return;

        Dm.remove(ref GLOB.pda_messengers, messenger_ref);
        Dm.remove(ref GLOB.pda_messengers_by_job, messenger);
        Dm.remove(ref GLOB.pda_messengers_by_name, messenger);
    }

    /// Get the display name of a messenger instance
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:42
    public static object get_messenger_name(datum_computer_file_program_messenger messenger = null)
    {
        if (messenger == null)
            return null;
        obj_item_modular_computer computer = messenger.computer;
        if (computer == null)
            return null;
        return STRINGIFY_PDA_TARGET(computer.saved_identification, computer.saved_job);
    }
}

/**
 * Chat log data type, stores information about the recipient,
 * the messages themselves and other metadata.
 */
// tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:54
public partial class datum_pda_chat : datum
{
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:73
    [DmParams("recipient")]
    public override async DmTask New(params object[] __extra)
    {
        datum_computer_file_program_messenger recipient_arg = Dm.arg(__extra, 0) as datum_computer_file_program_messenger;
        recipient = WEAKREF(recipient_arg) as datum_weakref;
        can_reply = recipient_arg != null ? TRUE : FALSE;
    }

    /// Adds a message to the chat log and optionally shows the chat in recents.
    /// Call this instead of adding to messages directly.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:79
    public virtual datum_pda_message add_message(datum_pda_message message = null, object show_in_recents = null)
    {
        show_in_recents ??= TRUE;
        Dm.append(ref messages, message);
        if (!Dm.truthy(visible_in_recents) && Dm.truthy(show_in_recents))
            visible_in_recents = TRUE;
        return message;
    }

    /// Returns this datum as an associative list, used for ui_data calls.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:86
    public virtual DmList get_ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList recipient_data = Dm.list();

        recipient_data["name"] = get_recipient_name();
        recipient_data["job"] = get_recipient_job();
        recipient_data["ref"] = recipient?.reference;

        data["ref"] = REF(this);
        data["recipient"] = recipient_data;

        DmList messages_data = Dm.list();
        foreach (datum_pda_message message in Dm.each<datum_pda_message>(messages))
            Dm.append(ref messages_data, Dm.list(message.get_ui_data(user)));
        data["messages"] = messages_data;
        data["message_draft"] = message_draft;

        data["visible"] = visible_in_recents;
        data["can_reply"] = can_reply;
        data["unread_messages"] = unread_messages;

        return data;
    }

    /// Returns the messenger's name, caches the name in case the recipient becomes invalid later.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:111
    public virtual object get_recipient_name()
    {
        datum_computer_file_program_messenger messenger = recipient?.resolve() as datum_computer_file_program_messenger;
        if (messenger != null && Dm.@in(recipient.reference, GLOB.pda_messengers))
            cached_name = Dm.str(messenger.computer.saved_identification);
        return cached_name;
    }

    /// Returns the messenger's job, caches the job in case the recipient becomes invalid later.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:118
    public virtual object get_recipient_job()
    {
        datum_computer_file_program_messenger messenger = recipient?.resolve() as datum_computer_file_program_messenger;
        if (messenger != null && Dm.@in(recipient.reference, GLOB.pda_messengers))
            cached_job = Dm.str(messenger.computer.saved_job);
        return cached_job;
    }
}

/**
 * Chat message data type, stores data about messages themselves.
 */
// tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:127
public partial class datum_pda_message : datum
{
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:139
    [DmParams("message", "outgoing", "timestamp", "photo_name", "everyone")]
    public override async DmTask New(params object[] __extra)
    {
        message = Dm.arg(__extra, 0);
        outgoing = Dm.arg(__extra, 1);
        timestamp = Dm.arg(__extra, 2);
        photo_name = Dm.arg(__extra, 3);
        everyone = Dm.arg(__extra, 4) ?? FALSE;
    }

    /// Returns an associative list of the message's data, used for ui_data calls.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_data.dm:147
    public virtual DmList get_ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["message"] = message;
        data["outgoing"] = outgoing;
        // photo_name ? SSassets.transport.get_asset_url(photo_name) : null: the photos (the camera) are not ported, so a message has none
        data["photo_path"] = null;
        data["everyone"] = everyone;
        data["timestamp"] = timestamp;
        return data;
    }
}
