// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the PDA messenger): the messenger app, its contacts by job or name, its chats, its NtosMessenger
// window, sending a message through telecomms (the message server), receiving it with the PDA's ring and the chat line with its
// (Reply) link, the quick reply and the mass message of the captain's and the lawyer's PDAs. Left out with a comment or a marked
// leaf: the photos (the image files and their assets: the camera), the silicons' photo and their AI tracking link, the talk log
// and the bomber log with the shell circuit's sender in its tag (the logs, M10). The virus disks' send and the rigged messages (the
// Detomatix disk, its explode choice) came with syndicate G3d (D316). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:2  #define TEMP_IMAGE_PATH(ref),
// MAX_PDA_MESSAGE_LEN 1024, PDA_MESSAGE_TIMESTAMP_FORMAT "hh:mm" (file-local)
// tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:8
public partial class datum_computer_file_program_messenger : datum_computer_file_program
{
    private const float MAX_PDA_MESSAGE_LEN = 1024;
    private const string PDA_MESSAGE_TIMESTAMP_FORMAT = "hh:mm";

    // DEVIATION(DV171): the port's chat type of the messenger's lines (tg's to_chat of them has none, so they are untyped info): the
    // client's Messages tab shows them (chat.md 3)
    private const string MESSAGE_TYPE_PDA = "pda";

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:52
    [SignalHandler]
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        RegisterSignal(computer, COMSIG_MODULAR_COMPUTER_FILE_STORE, nameof(check_new_photo));
        RegisterSignal(computer, COMSIG_MODULAR_COMPUTER_FILE_DELETE, nameof(check_image_removed));
        RegisterSignal(computer, COMSIG_MODULAR_PDA_IMPRINT_UPDATED, nameof(on_imprint_added));
        RegisterSignal(computer, COMSIG_MODULAR_PDA_IMPRINT_RESET, nameof(on_imprint_reset));
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:59
    [SignalHandler]
    public virtual void check_new_photo(object sender = null, datum_computer_file storing_image = null)
    {
        if (!(storing_image is datum_computer_file_image))
            return;
        update_pictures_for_all();
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:65
    [SignalHandler]
    public virtual void check_image_removed(object sender = null, datum_computer_file image_removed = null)
    {
        if (image_removed is datum_computer_file_image image && Dm.eq(selected_image, image.image_name))
            selected_image = null;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:70
    [SignalHandler]
    public virtual void on_imprint_added(object sender = null)
    {
        add_messenger(this);
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:74
    [SignalHandler]
    public virtual void on_imprint_reset(object sender = null)
    {
        remove_messenger(this);
        LAZYNULL(ref saved_chats);
        selected_image = null;
        viewing_messages_of = null;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:81
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(computer))
            stack_trace(Dm.text("Attempted to qdel messenger of [] without qdeling computer, this will cause problems later", computer), "code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm", 83);
        remove_messenger(this);
        return base.Destroy(force);
    }

    /// Gets the list of available messengers
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:88
    public virtual DmList get_messengers()
    {
        DmList dictionary = Dm.list();

        DmList messengers_sorted = Dm.truthy(sort_by_job) ? GLOB.pda_messengers_by_job : GLOB.pda_messengers_by_name;

        foreach (object entry in Dm.each(messengers_sorted))
        {
            if (!(entry is datum_computer_file_program_messenger messenger) || messenger.computer == null)
                continue;
            if (messenger == this || Dm.truthy(messenger.invisible))
                continue;

            DmList data = Dm.list();
            data["name"] = messenger.computer.saved_identification;
            data["job"] = messenger.computer.saved_job;
            data["ref"] = REF(messenger);

            dictionary[data["ref"]] = data;
        }

        return dictionary;
    }

    /// Checks if the person can send an everyone message
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:109
    public virtual bool can_send_everyone_message()
    {
        // COOLDOWN_FINISHED(src, last_text) && COOLDOWN_FINISHED(src, last_text_everyone) (__DEFINES/cooldowns.dm:113), inline
        return last_text <= Dm.num(world.time) && last_text_everyone <= Dm.num(world.time);
    }

    /// Gets all currently relevant image asset keys
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:113
    public virtual DmList get_picture_assets()
    {
        DmList data = Dm.list();

        foreach (datum_computer_file_image image_file in Dm.each<datum_computer_file_image>(computer.stored_files))
            Dm.combine(ref data, image_file.image_name);

        if (Dm.@in(viewing_messages_of, saved_chats))
        {
            datum_pda_chat chat = LAZYACCESS(saved_chats, viewing_messages_of) as datum_pda_chat;
            foreach (datum_pda_message message in Dm.each<datum_pda_message>(chat.messages))
            {
                if (message.photo_name == null)
                    continue;
                Dm.combine(ref data, message.photo_name);
            }
        }

        if (selected_image != null)
            Dm.combine(ref data, selected_image);

        return data;
    }

    /// Sends new datum/picture assets to everyone
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:132
    public virtual void update_pictures_for_all()
    {
        // SSassets.transport.send_assets(window.user, get_picture_assets()) for each open window: the asset transport is the client's
        // (DV44), and no photo exists (the camera)
    }

    /// Set the ringtone if possible. Also handles encoding.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:142
    public virtual object set_ringtone(object new_ringtone = null, mob user = null)
    {
        new_ringtone = trim(html_encode(new_ringtone), MESSENGER_RINGTONE_MAX_LENGTH);
        if (!Dm.truthy(new_ringtone))
            return FALSE;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(computer._listen_lookup?[COMSIG_TABLET_CHANGE_ID]) ? NONE : computer._SendSignal(COMSIG_TABLET_CHANGE_ID, Dm.list(computer, user, new_ringtone)), COMPONENT_STOP_RINGTONE_CHANGE)))
            return FALSE;

        ringtone = Dm.str(new_ringtone);
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:153
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        // SSassets.transport.send_assets(user, get_picture_assets()): the asset transport is the client's (DV44)
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:157
    public override object ui_state(mob user = null)
    {
        if (issilicon(user))
            return GLOB.deep_inventory_state;
        return GLOB.default_state;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:162
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        if (Dm.eq(action, "PDA_ringSet"))
        {
            mob_living user = usr as mob_living;
            object new_ringtone = await tgui_input_text(user, I18n.T("Enter a new ringtone"), I18n.T("Ringtone"), ringtone, max_length: MAX_MESSAGE_LEN, encode: FALSE);
            if (!Dm.truthy(await computer.can_interact(user)))
            {
                computer.balloon_alert(user, I18n.T("can't reach!"));
                return FALSE;
            }
            return set_ringtone(new_ringtone, user);
        }

        if (Dm.eq(action, "PDA_toggleAlerts"))
        {
            alert_silenced = !Dm.truthy(alert_silenced) ? TRUE : FALSE;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_toggleSendingAndReceiving"))
        {
            sending_and_receiving = !Dm.truthy(sending_and_receiving) ? TRUE : FALSE;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_viewMessages"))
        {
            if (Dm.@in(viewing_messages_of, saved_chats))
            {
                datum_pda_chat chat = LAZYACCESS(saved_chats, viewing_messages_of) as datum_pda_chat;
                chat.unread_messages = 0;
            }

            viewing_messages_of = @params["ref"];

            if (Dm.@in(viewing_messages_of, saved_chats))
            {
                datum_pda_chat chat = LAZYACCESS(saved_chats, viewing_messages_of) as datum_pda_chat;
                chat.visible_in_recents = TRUE;
            }

            selected_image = null;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_closeMessages"))
        {
            object target = @params["ref"];

            if (!Dm.@in(target, saved_chats))
                return FALSE;

            datum_pda_chat chat = LAZYACCESS(saved_chats, target) as datum_pda_chat;
            chat.visible_in_recents = FALSE;
            if (Dm.eq(viewing_messages_of, target))
                viewing_messages_of = null;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_clearMessages"))
        {
            object chat_ref = @params["ref"];

            if (Dm.@in(chat_ref, saved_chats))
                LAZYREMOVE(ref saved_chats, chat_ref);
            else if (chat_ref == null)
                LAZYNULL(ref saved_chats);

            viewing_messages_of = null;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_changeSortStyle"))
        {
            sort_by_job = !Dm.truthy(sort_by_job) ? TRUE : FALSE;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_sendEveryone"))
        {
            if (!Dm.truthy(sending_and_receiving))
            {
                to_chat(usr, span_notice(I18n.T("ERROR: This device has sending disabled.")));
                return FALSE;
            }

            if (!Dm.truthy(spam_mode))
            {
                to_chat(usr, span_notice(I18n.T("ERROR: This device does not have mass-messaging perms.")));
                return FALSE;
            }

            if (!can_send_everyone_message())
                return FALSE;

            return await send_message_to_all(usr as mob_living, @params["message"]);
        }

        if (Dm.eq(action, "PDA_saveMessageDraft"))
        {
            object target_chat_ref = @params["ref"];
            object message_draft = @params["message"];

            if (!Dm.@in(target_chat_ref, saved_chats))
                return FALSE;

            datum_pda_chat chat = LAZYACCESS(saved_chats, target_chat_ref) as datum_pda_chat;

            chat.message_draft = Dm.str(message_draft);

            return TRUE;
        }

        if (Dm.eq(action, "PDA_clearUnreads"))
        {
            object target_chat_ref = @params["ref"];

            if (!Dm.@in(target_chat_ref, saved_chats))
                return FALSE;

            datum_pda_chat chat = LAZYACCESS(saved_chats, target_chat_ref) as datum_pda_chat;
            chat.unread_messages = 0;

            return TRUE;
        }

        if (Dm.eq(action, "PDA_sendMessage"))
        {
            if (!Dm.truthy(sending_and_receiving))
            {
                to_chat(usr, span_notice(I18n.T("ERROR: This device has sending disabled.")));
                return FALSE;
            }

            // target ref, can either be a chat in saved_chats
            // or a messenger ref in GLOB.pda_messengers
            object target_ref = @params["ref"];

            object target = null;

            if (Dm.@in(target_ref, saved_chats))
                target = LAZYACCESS(saved_chats, target_ref);
            else if (Dm.@in(target_ref, GLOB.pda_messengers))
                target = GLOB.pda_messengers[target_ref];
            else
                return FALSE;

            if (Dm.truthy(sending_virus))
            {
                if (computer.inserted_disk is not obj_item_disk_computer_virus disk)
                    return FALSE;

                datum_computer_file_program_messenger target_messenger = null;

                if (target is datum_pda_chat target_chat)
                {
                    target_messenger = target_chat.recipient?.resolve() as datum_computer_file_program_messenger;
                    if (target_messenger == null)
                    {
                        to_chat(usr, span_notice(I18n.T("ERROR: Recipient no longer exists.")));
                        return FALSE;
                    }
                }
                else if (target is datum_computer_file_program_messenger target_app)
                    target_messenger = target_app;

                return await disk.send_virus(computer as obj_item_modular_computer_pda, target_messenger.computer as obj_item_modular_computer_pda, usr as mob_living, @params["message"]);
            }

            return await send_message(usr, @params["message"], Dm.list(target));
        }

        if (Dm.eq(action, "PDA_clearPhoto"))
        {
            selected_image = null;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_toggleVirus"))
        {
            sending_virus = !Dm.truthy(sending_virus) ? TRUE : FALSE;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_selectPhoto"))
        {
            if (issilicon(usr))
                return FALSE;

            object photo_uid = text2num(@params["uid"]);

            if (!(computer.find_file_by_uid(photo_uid) is datum_computer_file_image selected_image_file))
                return FALSE;

            selected_image = selected_image_file.image_name;
            return TRUE;
        }

        if (Dm.eq(action, "PDA_siliconSelectPhoto"))
        {
            // the silicon's camera (user.aicamera.selectpicture) and the temporary image asset (:321-332): the silicons, the camera
            if (!issilicon(usr))
                return FALSE;
            Dm.NotTranslated("code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:324", "ui_act: PDA_siliconSelectPhoto, a silicon's camera picture (the silicons and the camera)");
            return FALSE;
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:334
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList static_data = Dm.list();

        static_data["can_spam"] = spam_mode;
        static_data["is_silicon"] = issilicon(user) ? TRUE : FALSE;
        static_data["remote_silicon"] = (isAI(user) || iscyborg(user)) && !(computer is obj_item_modular_computer_pda_silicon) ? TRUE : FALSE; //Silicon is accessing a PDA on the ground, not their internal one. Avoiding pAIs in this check.
        static_data["alert_able"] = alert_able;

        return static_data;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:344
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList chats_data = Dm.list();
        foreach (object chat_ref in Dm.each(saved_chats))
        {
            datum_pda_chat chat = saved_chats[chat_ref] as datum_pda_chat;
            DmList chat_data = chat.get_ui_data(user);
            chats_data[chat_ref] = chat_data;
        }

        DmList messengers = get_messengers();

        data["owner"] = Dm.@in(REF(this), GLOB.pda_messengers) ? Dm.list(
            Dm.kv("name", computer.saved_identification),
            Dm.kv("job", computer.saved_job),
            Dm.kv("ref", REF(this))) : null;
        data["saved_chats"] = Dm.truthy(chats_data) ? chats_data : Dm.list();
        data["messengers"] = messengers;
        data["sort_by_job"] = sort_by_job;
        data["alert_silenced"] = alert_silenced;
        data["sending_and_receiving"] = sending_and_receiving;
        data["open_chat"] = viewing_messages_of;

        // silicons handle selecting photos a bit differently for now
        if (!issilicon(user))
        {
            DmList stored_photos = Dm.list();
            // for(var/datum/computer_file/image/image_file in computer.stored_files) ... (:370-374): the image files (the camera): none
            data["stored_photos"] = stored_photos;
        }
        data["selected_photo_path"] = null; // selected_image's asset url: the photos (the camera)
        data["on_spam_cooldown"] = !can_send_everyone_message() ? TRUE : FALSE;

        if (computer.inserted_disk is obj_item_disk_computer_virus)
        {
            data["virus_attach"] = TRUE;
            data["sending_virus"] = sending_virus;
        }
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:385
    public override async DmTask<object> ui_assets(mob user = null)
    {
        object dot = await base.ui_assets(user);
        // . += get_asset_datum(/datum/asset/spritesheet_batched/chat): the asset transport is the client's (DV44)
        return dot;
    }

    //////////////////////
    // MESSAGE HANDLING //
    //////////////////////

    /// Brings up the quick reply prompt to send a message.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:394
    public virtual async DmTask quick_reply_prompt(mob_living user = null, datum_pda_chat chat = null)
    {
        if (chat == null)
            return;
        datum_computer_file_program_messenger target = chat.recipient?.resolve() as datum_computer_file_program_messenger;
        if (target == null || target.computer == null)
        {
            to_chat(user, span_notice(I18n.T("ERROR: Recipient no longer exists.")));
            chat.recipient = null;
            chat.can_reply = FALSE;
            return;
        }
        object target_name = target.computer.saved_identification;
        object input_message = await tgui_input_text(user, I18n.T("Enter []", Dm.truthy(mime_mode) ? "emojis" : "a message"), Dm.text("NT Messaging[]", Dm.truthy(target_name) ? Dm.text(" ([])", target_name) : ""), max_length: MAX_MESSAGE_LEN, encode: FALSE);
        await send_message(user, input_message, Dm.list(chat));
    }

    /// Helper proc that sends a message to everyone
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:408
    public virtual async DmTask<object> send_message_to_all(mob_living user = null, object message = null)
    {
        DmList chats = Dm.list();
        DmList messenger_targets = Dm.list();

        foreach (object mc in Dm.each(get_messengers()))
            Dm.append(ref messenger_targets, mc);

        foreach (object chatref in Dm.each(saved_chats))
        {
            datum_pda_chat chat = saved_chats[chatref] as datum_pda_chat;
            if (!Dm.@in(chat.recipient?.reference, messenger_targets)) // if its in messenger_targets, it's valid
                continue;
            Dm.remove(ref messenger_targets, chat.recipient.reference);
            Dm.append(ref chats, chat);
        }

        foreach (object missing_messenger in Dm.each(messenger_targets))
        {
            datum_pda_chat new_chat = create_chat(missing_messenger);
            Dm.append(ref chats, new_chat);
        }

        if (Dm.truthy(await send_message(user, message, chats, everyone: TRUE)))
            last_text_everyone = Dm.num(world.time) + 2 * MINUTES; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        return null;
    }

    /// Creates a chat and adds it to saved_chats. Supports fake users. Returns the newly created chat.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:430
    public virtual datum_pda_chat create_chat(object recipient_ref = null, object name = null, object job = null)
    {
        datum_computer_file_program_messenger recipient = null;

        if (name == null && job == null)
        {
            if (!Dm.@in(recipient_ref, GLOB.pda_messengers))
                throw Dm.CRASH("tried to create a chat with a messenger that isn't registered", "code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm", 435);
            recipient = GLOB.pda_messengers[recipient_ref] as datum_computer_file_program_messenger;
        }

        datum_pda_chat new_chat = Dm.@new<datum_pda_chat>(recipient);

        // this is a chat with a "fake user" (automated or forged message)
        if (recipient == null)
        {
            new_chat.cached_name = Dm.str(name);
            new_chat.cached_job = Dm.str(job);
            new_chat.can_reply = FALSE;
        }

        LAZYSET(ref saved_chats, REF(new_chat), new_chat);

        return new_chat;
    }

    /// Gets the chat by the recipient, either by their name or messenger ref
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:451
    public virtual datum_pda_chat find_chat_by_recipient(object recipient = null, object fake_user = null)
    {
        foreach (object chat_ref in Dm.each(saved_chats))
        {
            datum_pda_chat chat = saved_chats[chat_ref] as datum_pda_chat;
            if (Dm.truthy(fake_user) && Dm.eq(chat.cached_name, recipient))
                return chat;
            else if (Dm.eq(chat.recipient?.reference, recipient))
                return chat;
        }
        return null;
    }

    /// Returns a message input, sanitized and checked against the filter
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:461
    public virtual async DmTask<object> sanitize_pda_message(object message = null, mob sender = null)
    {
        message = sanitize(trim(message, MAX_PDA_MESSAGE_LEN));

        if (Dm.truthy(mime_mode))
            message = emoji_sanitize(message);

        // check message against filter
        if (sender != null && !Dm.truthy(await check_pda_message_against_filter(message, sender)))
            return null;

        return emoji_parse(message);
    }

    /// Sends a message to targets via PDA. When sending to everyone, set `everyone` to true so the message is formatted accordingly
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:474
    public virtual async DmTask<object> send_message(atom source = null, object message = null, DmList targets = null, object everyone = null)
    {
        everyone ??= FALSE;
        mob_living sender = source as mob_living;
        message = await sanitize_pda_message(message, sender);
        if (!Dm.truthy(message))
            return FALSE;

        // upgrade the image asset to a permanent key (:483-488): the temporary image is the silicons' camera picture (the camera)
        object photo_asset_key = selected_image;

        // our sender targets
        DmList target_messengers = Dm.list();
        DmList target_chats = Dm.list();

        bool should_alert = Dm.length(targets) == 1 && sender != null;

        // filter out invalid targets
        foreach (object target in Dm.each(targets))
        {
            datum_pda_chat target_chat = null;
            datum_computer_file_program_messenger target_messenger = null;

            if (target is datum_pda_chat chat)
            {
                target_chat = chat;

                if (!Dm.truthy(target_chat.can_reply))
                {
                    if (should_alert)
                        to_chat(sender, span_notice(I18n.T("ERROR: Recipient has receiving disabled.")));
                    continue;
                }

                target_messenger = target_chat.recipient?.resolve() as datum_computer_file_program_messenger;

                if (target_messenger == null)
                {
                    if (should_alert)
                        to_chat(sender, span_notice(I18n.T("ERROR: Recipient no longer exists.")));
                    target_chat.can_reply = FALSE;
                    target_chat.recipient = null;
                    continue;
                }

                if (!Dm.truthy(target_messenger.sending_and_receiving))
                {
                    if (should_alert)
                        to_chat(sender, span_notice(I18n.T("ERROR: Recipient has receiving disabled.")));
                    continue;
                }
            }
            else if (target is datum_computer_file_program_messenger messenger)
            {
                target_messenger = messenger;

                if (!Dm.truthy(target_messenger.sending_and_receiving))
                {
                    if (should_alert)
                        to_chat(sender, span_notice(I18n.T("ERROR: Recipient has receiving disabled.")));
                    continue;
                }

                target_chat = find_chat_by_recipient(REF(target));

                if (target_chat == null)
                    target_chat = create_chat(REF(target));
            }
            else
            {
                stack_trace(Dm.text("invalid target []", target), "code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm", 537);
                continue;
            }

            Dm.append(ref target_chats, target_chat);
            Dm.append(ref target_messengers, target_messenger);
        }

        if (!Dm.truthy(await send_message_signal(source, message, target_messengers, photo_asset_key, everyone)))
            return FALSE;

        // Log it in our logs
        datum_pda_message message_datum = Dm.@new<datum_pda_message>(message, TRUE, round_timestamp(PDA_MESSAGE_TIMESTAMP_FORMAT), photo_asset_key, everyone);
        foreach (datum_pda_chat target_chat in Dm.each<datum_pda_chat>(target_chats))
        {
            target_chat.add_message(message_datum, show_in_recents: !Dm.truthy(everyone) ? TRUE : FALSE);
            target_chat.unread_messages = 0;
        }

        // send new pictures to everyone
        if (photo_asset_key != null)
            update_pictures_for_all();

        // switch our chat screen after sending a message, but do it only if it's not to everyone
        if (!Dm.truthy(everyone))
            viewing_messages_of = REF(target_chats[1]);

        return TRUE;
    }

    /// Sends a rigged message that explodes when the recipient tries to reply or look at it.
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:563
    public virtual async DmTask<object> send_rigged_message(mob sender = null, object message = null, DmList targets = null, object fake_name = null, object fake_job = null, object attach_fake_photo = null)
    {
        message = await sanitize_pda_message(message, sender);

        if (!Dm.truthy(message))
            return FALSE;

        object fake_photo = Dm.truthy(attach_fake_photo) ? ">:3c" : null;

        return await send_message_signal(sender, message, targets, fake_photo, FALSE, TRUE, fake_name, fake_job);
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:573
    public virtual async DmTask<object> send_message_signal(atom source = null, object message = null, DmList targets = null, object photo_path = null, object everyone = null, object rigged = null, object fake_name = null, object fake_job = null)
    {
        everyone ??= FALSE;
        rigged ??= FALSE;
        mob sender = null;
        if (source is mob source_mob)
        {
            sender = source_mob;
            if (!Dm.truthy(await sender.can_perform_action(computer, ALLOW_RESTING | ALLOW_PAI)))
                return FALSE;
        }

        if (!(last_text <= Dm.num(world.time))) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return FALSE;

        if (Dm.length(targets) == 0)
            return FALSE;

        // check for jammers
        if (Dm.truthy(is_within_radio_jammer_range(computer)) && !Dm.truthy(rigged))
        {
            // different message so people know it's a radio jammer
            if (sender != null)
                to_chat(sender, span_notice(I18n.T("ERROR: Network unavailable, please try again later.")));
            if (Dm.truthy(alert_able) && !Dm.truthy(alert_silenced))
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 15, TRUE);
            return FALSE;
        }

        // used for logging
        DmList stringified_targets = Dm.list();

        foreach (datum_computer_file_program_messenger messenger in Dm.each<datum_computer_file_program_messenger>(targets))
            Dm.append(ref stringified_targets, get_messenger_name(messenger));

        float sent_prob = 0;
        if (source is mob_living_carbon_human oldie)
            sent_prob = Dm.num(Dm.pow(0.025f * Dm.num(oldie.age), 3)); // 25 y/o = ~0.25% chance, 85 y/o = ~10% chance
        if (computer != null && Dm.prob(sent_prob))
            message = Dm.text("[] []", message, computer.get_messenger_ending());

        datum_signal_subspace_messaging_tablet_message signal = Dm.@new<datum_signal_subspace_messaging_tablet_message>(computer, Dm.list(
            Dm.kv("ref", REF(this)),
            Dm.kv("message", message),
            Dm.kv("targets", targets),
            Dm.kv("rigged", rigged),
            Dm.kv("everyone", everyone),
            Dm.kv("photo", photo_path),
            Dm.kv("automated", FALSE)));
        if (Dm.truthy(rigged)) //Will skip the message server and go straight to the hub so it can't be cheesed by disabling the message server machine
        {
            signal.data["fakename"] = fake_name;
            signal.data["fakejob"] = fake_job;
            signal.server_type = typeof(obj_machinery_telecomms_hub);
            signal.data["reject"] = FALSE; // Do not refuse the message
        }

        signal.send_to_receivers();

        // If it didn't reach, note that fact
        if (!Dm.truthy(signal.data["done"]))
        {
            if (sender != null)
                to_chat(sender, span_notice(I18n.T("ERROR: Server is not responding.")));
            if (Dm.truthy(alert_able) && !Dm.truthy(alert_silenced))
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 15, TRUE);
            return FALSE;
        }

        // the shell circuit's creator for the talk log's tag (:633-636) and the talk log (source.log_talk(message, LOG_PDA, ...), :639) and the
        // bomber log of a rigged message (:640-641): the logs (M10)

        // Show it to ghosts
        string ghost_message = span_game_say(Dm.text("[] []PDA Message --> []: \"[]\"", span_name(Dm.str(signal.format_sender())), Dm.truthy(rigged) ? Dm.text("(as []) Rigged ", span_name(Dm.str(fake_name))) : "", span_name(Dm.text("[]", signal.format_target())), signal.format_message()));
        DmList message_listeners = Dm.add(GLOB.dead_player_list, GLOB.current_observers_list) as DmList;
        foreach (mob listener in Dm.each<mob>(message_listeners))
        {
            if (!Dm.truthy(Dm.bitand(get_chat_toggles(listener), CHAT_GHOSTPDA)))
                continue;
            to_chat(listener, Dm.text("[] []", FOLLOW_LINK(listener, source), ghost_message), type: MESSAGE_TYPE_PDA);
        }

        if (sender != null)
            to_chat(sender, span_info(I18n.T("PDA message sent to []: \"[]\"", signal.format_target(), message)), type: MESSAGE_TYPE_PDA);

        if (Dm.truthy(alert_able) && !Dm.truthy(alert_silenced))
            computer.send_sound();

        last_text = Dm.num(world.time) + 1 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline

        if (Dm.truthy(computer._listen_lookup?[COMSIG_MODULAR_PDA_MESSAGE_SENT]))
            computer._SendSignal(COMSIG_MODULAR_PDA_MESSAGE_SENT, Dm.list(computer, source, signal));

        selected_image = null;
        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:664
    public virtual void receive_message(datum_signal_subspace_messaging_tablet_message signal = null)
    {
        datum_pda_chat chat = null;

        object is_rigged = signal.data["rigged"];
        object is_automated = signal.data["automated"];
        bool is_fake_user = Dm.truthy(is_rigged) || Dm.truthy(is_automated) || signal.data["ref"] == null;
        object fake_name = is_fake_user ? signal.data["fakename"] : null;
        object fake_job = is_fake_user ? signal.data["fakejob"] : null;

        object sender_ref = signal.data["ref"];

        // don't create a new chat for rigged messages, make it a one off notif
        if (!Dm.truthy(is_rigged))
        {
            datum_pda_message message = Dm.@new<datum_pda_message>(signal.data["message"], FALSE, round_timestamp(PDA_MESSAGE_TIMESTAMP_FORMAT), signal.data["photo"], signal.data["everyone"]);

            chat = find_chat_by_recipient(is_fake_user ? fake_name : sender_ref, is_fake_user ? TRUE : FALSE);
            if (chat == null)
                chat = create_chat(!is_fake_user ? sender_ref : null, fake_name, fake_job);
            chat.add_message(message);
            chat.unread_messages++;

            // the recipient (us) currently has a chat with the sender open, so update their ui
            if (viewing_messages_of != null && Dm.eq(viewing_messages_of, sender_ref))
                viewing_messages_of = REF(chat);
        }

        DmList receievers = Dm.list();
        // if(computer.inserted_pai && computer.inserted_pai.pai) receievers += computer.inserted_pai.pai: the pAI mob (pAIs)
        if (computer.loc is mob_living living_holder)
            Dm.append(ref receievers, living_holder);

        // chat is null for rigged messages, we do in fact have to nullcheck
        datum_computer_file_program_messenger sender_messenger = chat?.recipient?.resolve() as datum_computer_file_program_messenger;

        object sender_title = is_fake_user ? STRINGIFY_PDA_TARGET(fake_name, fake_job) : get_messenger_name(sender_messenger);
        object sender_name = is_fake_user ? fake_name : sender_messenger.computer.saved_identification;

        if (Dm.truthy(computer._listen_lookup?[COMSIG_MODULAR_PDA_MESSAGE_RECEIVED]))
            computer._SendSignal(COMSIG_MODULAR_PDA_MESSAGE_RECEIVED, Dm.list(computer, signal, Dm.truthy(fake_job) ? fake_job : sender_messenger?.computer.saved_job, sender_name));

        foreach (mob_living messaged_mob in Dm.each<mob_living>(receievers))
        {
            if (IS_UNCONSCIOUS(messaged_mob))
                continue;
            if (!Dm.truthy(messaged_mob.is_literate()))
                continue;
            string reply_href = Dm.truthy(signal.data["rigged"]) ? "explode" : "message";
            string photo_href = Dm.truthy(signal.data["rigged"]) ? "explode" : "open";
            string reply;
            if (Dm.truthy(is_automated))
                reply = "[Automated Message]";
            else
                reply = Dm.text("(<a href='byond://?src=[];choice=[];skiprefresh=1;target=[]'>Reply</a>)", REF(this), reply_href, REF(chat));

            // if (isAI(messaged_mob)) the sender title's AI tracking link (:717-718): the AI (a later milestone)

            string inbound_message = Dm.text("[]", signal.format_message());

            string photo_message = Dm.truthy(signal.data["photo"]) ? Dm.text(" (<a href='byond://?src=[];choice=[];skiprefresh=1;target=[]'>Photo Attached</a>)", REF(this), photo_href, REF(chat)) : "";
            // [icon2html(computer, messaged_mob)]: the chat icons (as at the other icon2html sites)
            to_chat(messaged_mob, span_infoplain(I18n.T("<b>PDA message from [], </b>\"[]\"[] []", sender_title, inbound_message, photo_message, reply)), type: MESSAGE_TYPE_PDA);

            if (Dm.truthy(computer._listen_lookup?[COMSIG_COMPUTER_RECEIVED_MESSAGE]))
                computer._SendSignal(COMSIG_COMPUTER_RECEIVED_MESSAGE, Dm.list(computer, sender_title, inbound_message, photo_message));
        }

        if (Dm.truthy(alert_able) && (!Dm.truthy(alert_silenced) || Dm.truthy(is_rigged)))
            computer.ring(ringtone, receievers);

        SStgui.update_uis(computer);
        update_pictures_for_all();
    }

    /// topic call that answers to people pressing "(Reply)" in chat
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:735
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);

        if (QDELETED(this))
            return null;
        if (!Dm.truthy(await usr.can_perform_action(computer, FORBID_TELEKINESIS_REACH | ALLOW_RESTING | ALLOW_PAI)))
            return null;

        // send an activation message and open the messenger
        if (!(Dm.truthy(computer.enabled) || Dm.truthy(await computer.turn_on(usr, open_ui: FALSE))))
            return null;
        if (!(computer.active_program == this || Dm.truthy(await computer.open_program(usr, this, open_ui: FALSE))))
            return null;

        object target_href = href_list["target"];

        if (Dm.eq(href_list["choice"], "message"))
        {
            if (!Dm.@in(target_href, saved_chats))
                return null;
            await quick_reply_prompt(usr as mob_living, LAZYACCESS(saved_chats, target_href) as datum_pda_chat);
        }
        else if (Dm.eq(href_list["choice"], "open"))
        {
            if (Dm.@in(target_href, saved_chats))
                viewing_messages_of = target_href;
            await computer.update_tablet_open_uis(usr);
        }
        else if (Dm.eq(href_list["choice"], "explode"))
        {
            if (!HAS_TRAIT(computer, TRAIT_PDA_CAN_EXPLODE))
                return null;

            obj_item_modular_computer_pda comp = computer as obj_item_modular_computer_pda;
            await comp.explode(usr, from_message_menu: TRUE);
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:769
    public virtual float compare_name(datum_computer_file_program_messenger rhs = null)
    {
        return sorttext(rhs.computer?.saved_identification, computer?.saved_identification);
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_program.dm:772
    public virtual float compare_job(datum_computer_file_program_messenger rhs = null)
    {
        return sorttext(rhs.computer?.saved_job, computer?.saved_job);
    }
}
