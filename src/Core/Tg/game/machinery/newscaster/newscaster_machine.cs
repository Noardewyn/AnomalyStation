// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/newscaster/newscaster_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6b (D326, after gaps P4a-P4c): the whole file, the newscaster: its PhysicalNewscaster window (the channels and their
// stories with comments and censorship, a new channel, a story, the wanted issue, the printed newspaper, the bounty board's requests
// paid between bank accounts), its alerts, screen overlays and light, the paper it is loaded with, its welding repair, wrenching off
// the wall, damage sounds and breaking, the pAI's newscaster, the wall frame. The photos' pictures go to the window as nothing
// (`browse_rsc` of a picture's image: the server renders no icons, D104); the screen's glow on the emissive plane is not created
// (DV12). The admins' approval notice of a cross-sector channel is admin tooling (no linked servers either). The var blocks are
// generated (D111).

// tg: code/game/machinery/newscaster/newscaster_machine.dm:3
public partial class obj_machinery_newscaster : obj_machinery
{
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:1  (file-local, #undef at the end)
    private const float ALERT_DELAY = 50 * SECONDS;

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.allCasters, this);
        Dm.append(ref GLOB.allbountyboards, this);
        update_appearance();
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:83
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.allCasters, this);
        Dm.remove(ref GLOB.allbountyboards, this);
        current_channel = null;
        current_image = null;
        active_request = null;
        current_user = null;
        newscaster_username = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:93
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }
        Waiting.NoSleep(set_light(1.5f, 0.7f, "#34D352")); // green light
        return dot;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:100
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            string state = Dm.text("[]_[]", base_icon_state, Dm.truthy(GLOB.news_network.wanted_issue.active) ? "wanted" : "normal");
            Dm.append(ref dot, mutable_appearance(icon, state));
            // DEVIATION(DV12): . += emissive_appearance(icon, state, src, alpha = src.alpha), the screen's glow, is not created

            if (Dm.truthy(GLOB.news_network.wanted_issue.active) && Dm.truthy(alert))
            {
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_alert", base_icon_state)));
                // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]_alert", src, alpha = src.alpha)
            }
        }

        float hp_percent = Dm.num(atom_integrity) * 100 / Dm.num(max_integrity);
        if (hp_percent >= 75)
            return dot;
        if (hp_percent >= 50)
            Dm.append(ref dot, "crack1"); // DEVIATION(DV12): . += emissive_blocker(icon, "crack1", src, alpha = src.alpha)
        else if (hp_percent >= 25)
            Dm.append(ref dot, "crack2"); // DEVIATION(DV12): . += emissive_blocker(icon, "crack2", src, alpha = src.alpha)
        else
            Dm.append(ref dot, "crack3"); // DEVIATION(DV12): . += emissive_blocker(icon, "crack3", src, alpha = src.alpha)
        return dot;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:127
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PhysicalNewscaster", name);
            await ui.open();
        }
        alert = FALSE; //We're checking our messages!
        await update_icon();
        return dot;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:136
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList message_list = Dm.list();

        //Code displaying name and Job Information, taken from the player mob's ID card if one exists.
        obj_item_card_id card = null;
        if (issilicon(user))
            newscaster_username = user.name;
        else if (user is mob_living living_user)
        {
            card = living_user.get_idcard(hand_first: TRUE) as obj_item_card_id;
            newscaster_username = card?.registered_account?.account_holder;
        }

        var user_data = Dm.list();
        data["user"] = user_data;
        if (card != null)
        {
            current_user = card.registered_account;
            user_data["name"] = card.registered_account.account_holder;
            if (card.registered_account.account_job != null)
            {
                user_data["job"] = card.registered_account.account_job.title;
                user_data["department"] = card.registered_account.account_job.paycheck_department;
            }
            else
            {
                user_data["job"] = "No Job";
                user_data["department"] = DEPARTMENT_UNASSIGNED;
            }
        }
        else if (user is mob_living_silicon silicon_user)
        {
            user_data["name"] = silicon_user.name;
            user_data["job"] = silicon_user.job;
            user_data["department"] = "N/A";
        }
        else
        {
            user_data["name"] = user.name;
            user_data["job"] = "N/A";
            user_data["department"] = "N/A";
        }

        DmList card_access = card?.GetAccess() as DmList;
        data["admin_mode"] = Dm.@in(admin_access, card_access) ? TRUE : FALSE;
        data["security_mode"] = Dm.@in(security_access, card_access) ? TRUE : FALSE;
        data["photo_data"] = current_image != null ? TRUE : FALSE;
        data["creating_channel"] = creating_channel;
        data["awaiting_approval"] = awaiting_approval;
        data["creating_comment"] = creating_comment;
        data["viewing_wanted"] = viewing_wanted;

        //Here is all the UI_data sent about the current wanted issue, as well as making a new one in the UI.
        data["making_wanted_issue"] = Dm.truthy(GLOB.news_network.wanted_issue?.active) ? FALSE : TRUE;
        data["criminal_name"] = criminal_name;
        data["crime_description"] = crime_description;
        DmList wanted_info = Dm.list();
        if (GLOB.news_network.wanted_issue != null)
        {
            bool has_wanted_issue = GLOB.news_network.wanted_issue.img != null;
            // if(has_wanted_issue) user << browse_rsc(GLOB.news_network.wanted_issue.img, "wanted_photo.png"): no picture has an image
            // here (D104)
            wanted_info = Dm.list(Dm.list(
                Dm.kv("active", GLOB.news_network.wanted_issue.active),
                Dm.kv("criminal", GLOB.news_network.wanted_issue.criminal),
                Dm.kv("crime", GLOB.news_network.wanted_issue.body),
                Dm.kv("author", GLOB.news_network.wanted_issue.scanned_user),
                Dm.kv("image", has_wanted_issue ? "wanted_photo.png" : null)));
        }

        //Code breaking down the channels that have been made on-station thus far. ha
        //Then, breaks down the messages that have been made on those channels.
        if (current_channel != null)
        {
            foreach (datum_feed_message feed_message in Dm.each_cast<datum_feed_message>(current_channel.messages))
            {
                object photo_id = null;
                DmList comment_list = null;
                if (feed_message.img != null)
                {
                    // user << browse_rsc(feed_message.img, "tmp_photo[feed_message.message_id].png"): no picture has an image here (D104)
                    photo_id = Dm.text("tmp_photo[].png", feed_message.message_id);
                }
                foreach (datum_feed_comment comment_message in Dm.each_cast<datum_feed_comment>(feed_message.comments))
                {
                    comment_list ??= Dm.list();
                    Dm.append(ref comment_list, Dm.list(Dm.list(
                        Dm.kv("auth", comment_message.author),
                        Dm.kv("body", comment_message.body),
                        Dm.kv("time", comment_message.time_stamp))));
                }
                Dm.append(ref message_list, Dm.list(Dm.list(
                    Dm.kv("auth", feed_message.author),
                    Dm.kv("body", feed_message.body),
                    Dm.kv("time", feed_message.time_stamp),
                    Dm.kv("channel_num", feed_message.parent_id),
                    Dm.kv("censored_message", feed_message.body_censor),
                    Dm.kv("censored_author", feed_message.author_censor),
                    Dm.kv("ID", feed_message.message_id),
                    Dm.kv("photo", photo_id),
                    Dm.kv("comments", comment_list))));
            }
        }

        data["viewing_channel"] = current_channel?.channel_id;
        data["paper"] = paper_remaining;
        //Here we display all the information about the current channel.
        data["channelName"] = current_channel?.channel_name;
        data["channelAuthor"] = current_channel?.author;

        if (current_channel == null)
        {
            data["channelAuthor"] = "Nanotrasen Inc";
            data["channelDesc"] = I18n.T("Welcome to Newscaster Net. Interface & News networks Operational.");
            data["channelLocked"] = TRUE;
            data["receivingCrossSector"] = FALSE;
        }
        else
        {
            data["channelDesc"] = current_channel.channel_desc;
            data["channelLocked"] = current_channel.locked;
            data["channelCensored"] = current_channel.censored;
            data["receivingCrossSector"] = current_channel.receiving_cross_sector;
        }

        //We send all the information about all messages in existence.
        data["messages"] = message_list;
        data["wanted"] = wanted_info;

        DmList formatted_requests = Dm.list();
        DmList formatted_applicants = Dm.list();
        foreach (datum_station_request request in Dm.each_cast<datum_station_request>(GLOB.request_list))
        {
            Dm.append(ref formatted_requests, Dm.list(Dm.list(Dm.kv("owner", request.owner), Dm.kv("value", request.value), Dm.kv("description", request.description), Dm.kv("acc_number", request.req_number))));
            if (request.applicants != null)
            {
                foreach (datum_bank_account applicant_bank_account in Dm.each_cast<datum_bank_account>(request.applicants))
                    Dm.append(ref formatted_applicants, Dm.list(Dm.list(Dm.kv("name", applicant_bank_account.account_holder), Dm.kv("request_id", request.owner_account.account_id), Dm.kv("requestee_id", applicant_bank_account.account_id))));
            }
        }
        data["requests"] = formatted_requests;
        data["applicants"] = formatted_applicants;
        data["bountyValue"] = bounty_value;
        data["bountyText"] = bounty_text;

        return data;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:259
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList channel_list = Dm.list();
        foreach (datum_feed_channel channel in Dm.each_cast<datum_feed_channel>(GLOB.news_network.network_channels))
        {
            Dm.append(ref channel_list, Dm.list(Dm.list(
                Dm.kv("name", channel.channel_name),
                Dm.kv("author", channel.author),
                Dm.kv("censored", channel.censored),
                Dm.kv("locked", channel.locked),
                Dm.kv("ID", channel.channel_id))));
        }

        data["channels"] = channel_list;
        return data;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:275
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;
        object current_ref_num = @params["request"];
        object current_app_num = @params["applicant"];
        datum_bank_account request_target = null;
        if (Dm.truthy(current_ref_num))
        {
            foreach (datum_station_request iterated_station_request in Dm.each_cast<datum_station_request>(GLOB.request_list))
            {
                if (Dm.eq(iterated_station_request.req_number, current_ref_num))
                {
                    active_request = iterated_station_request;
                    break;
                }
            }
        }
        if (active_request != null)
        {
            foreach (datum_bank_account iterated_bank_account in Dm.each_cast<datum_bank_account>(active_request.applicants))
            {
                if (Dm.eq(iterated_bank_account.account_id, current_app_num))
                {
                    request_target = iterated_bank_account;
                    break;
                }
            }
        }

        switch (Dm.str(action))
        {
            case "setChannel":
            {
                object selected_channel_id = @params["channel"];
                if (selected_channel_id == null)
                    return TRUE;
                var potential_channel = GLOB.news_network.network_channels_by_id[Dm.text("[]", selected_channel_id)] as datum_feed_channel;
                if (potential_channel == null)
                    return TRUE;
                current_channel = potential_channel;
                break;
            }

            case "createStory":
            {
                if (current_channel == null)
                {
                    balloon_alert(user, I18n.T("select a channel first!"));
                    return TRUE;
                }
                object current_channel_id = @params["current"];
                await create_story(user, channel_id: current_channel_id);
                break;
            }

            case "togglePhoto":
                await toggle_photo(user);
                return TRUE;

            case "startCreateChannel":
                await start_create_channel(user);
                return TRUE;

            case "setChannelName":
            {
                object pre_channel_name = reject_bad_text(@params["channeltext"], max_length: MAX_NAME_LEN);
                if (!Dm.truthy(pre_channel_name))
                    return TRUE;
                channel_name = pre_channel_name;
                break;
            }

            case "setChannelDesc":
            {
                object pre_channel_desc = reject_bad_text(@params["channeldesc"], max_length: MAX_BROADCAST_LEN);
                if (!Dm.truthy(pre_channel_desc))
                    return TRUE;
                channel_desc = pre_channel_desc;
                break;
            }

            case "createChannel":
            {
                object locked = @params["lockedmode"];
                object cross_sector = @params["cross_sector"];
                await create_channel(user, locked, cross_sector);
                return TRUE;
            }

            case "cancelCreation":
                creating_channel = FALSE;
                creating_comment = FALSE;
                awaiting_approval = FALSE;
                viewing_wanted = FALSE;
                criminal_name = null;
                crime_description = null;
                return TRUE;

            case "storyCensor":
            {
                obj_item_card_id id_card = null;
                if (user is mob_living living_user)
                    id_card = living_user.get_idcard(hand_first: TRUE) as obj_item_card_id;
                if (!Dm.@in(admin_access, id_card?.GetAccess() as DmList))
                {
                    await say(I18n.T("Clearance not found."));
                    return TRUE;
                }
                object questionable_message = @params["messageID"];
                foreach (datum_feed_message iterated_feed_message in Dm.each_cast<datum_feed_message>(current_channel.messages))
                {
                    if (Dm.eq(iterated_feed_message.message_id, questionable_message))
                    {
                        iterated_feed_message.toggle_censor_body();
                        break;
                    }
                }
                break;
            }

            case "authorCensor":
            {
                obj_item_card_id id_card = null;
                if (user is mob_living living_user)
                    id_card = living_user.get_idcard(hand_first: TRUE) as obj_item_card_id;
                if (!Dm.@in(admin_access, id_card?.GetAccess() as DmList))
                {
                    await say(I18n.T("Clearance not found."));
                    return TRUE;
                }
                object questionable_message = @params["messageID"];
                foreach (datum_feed_message iterated_feed_message in Dm.each<datum_feed_message>(current_channel.messages))
                {
                    if (Dm.eq(iterated_feed_message.message_id, questionable_message))
                    {
                        iterated_feed_message.toggle_censor_author();
                        break;
                    }
                }
                break;
            }

            case "channelDNotice":
            {
                obj_item_card_id id_card = null;
                if (user is mob_living living_user)
                    id_card = living_user.get_idcard(hand_first: TRUE) as obj_item_card_id;
                if (!Dm.@in(admin_access, id_card?.GetAccess() as DmList))
                {
                    await say(I18n.T("Clearance not found."));
                    return TRUE;
                }
                object selected_channel_id = @params["channel"];
                if (selected_channel_id == null)
                    return TRUE;
                var potential_channel = GLOB.news_network.network_channels_by_id[Dm.text("[]", selected_channel_id)] as datum_feed_channel;
                if (potential_channel == null)
                    return TRUE;
                current_channel = potential_channel;
                current_channel.toggle_censor_D_class();
                break;
            }

            case "startComment":
            {
                if (!Dm.truthy(newscaster_username))
                {
                    creating_comment = FALSE;
                    return TRUE;
                }
                creating_comment = TRUE;
                object commentable_message = @params["messageID"];
                if (!Dm.truthy(commentable_message))
                    return TRUE;
                foreach (datum_feed_message iterated_feed_message in Dm.each_cast<datum_feed_message>(current_channel.messages))
                {
                    if (Dm.eq(iterated_feed_message.message_id, commentable_message))
                        current_message = iterated_feed_message;
                }
                return TRUE;
            }

            case "setCommentBody":
            {
                object pre_comment_text = @params["commenttext"];
                if (!Dm.truthy(pre_comment_text))
                    return TRUE;
                comment_text = pre_comment_text;
                return TRUE;
            }

            case "createComment":
                create_comment(user);
                return TRUE;

            case "toggleWanted":
                alert = FALSE;
                viewing_wanted = TRUE;
                update_appearance();
                return TRUE;

            case "setCriminalName":
            {
                object temp_name = await tgui_input_text(user, I18n.T("Write the Criminal's Name"), I18n.T("Warrent Alert Handler"), I18n.T("John Doe"), max_length: MAX_NAME_LEN, multiline: FALSE);
                if (!Dm.truthy(temp_name))
                    return TRUE;
                criminal_name = temp_name;
                return TRUE;
            }

            case "setCrimeData":
            {
                object temp_desc = await tgui_input_text(user, I18n.T("Write the Criminal's Crimes"), I18n.T("Warrent Alert Handler"), I18n.T("Unknown"), max_length: MAX_BROADCAST_LEN, multiline: TRUE);
                if (!Dm.truthy(temp_desc))
                    return TRUE;
                crime_description = temp_desc;
                return TRUE;
            }

            case "submitWantedIssue":
                if (!Dm.truthy(crime_description) || !Dm.truthy(criminal_name))
                    return TRUE;
                GLOB.news_network.submit_wanted(criminal_name, crime_description, newscaster_username, current_image, adminMsg: FALSE, newMessage: TRUE);
                current_image = null;
                return TRUE;

            case "clearWantedIssue":
                clear_wanted_issue(user);
                foreach (obj_machinery_newscaster other_newscaster in Dm.each_cast<obj_machinery_newscaster>(GLOB.allCasters))
                {
                    other_newscaster.update_appearance();
                    return TRUE;
                }
                break;

            case "printNewspaper":
                await print_paper(user);
                return TRUE;

            case "createBounty":
                await create_bounty();
                return TRUE;

            case "apply":
                await apply_to_bounty();
                return TRUE;

            case "payApplicant":
                await pay_applicant(payment_target: request_target);
                return TRUE;

            case "clear":
                if (current_user != null || Dm.truthy(newscaster_username))
                {
                    current_user = null;
                    newscaster_username = null;
                    await say(I18n.T("Account Reset."));
                    return TRUE;
                }
                break;

            case "deleteRequest":
                await delete_bounty_request();
                return TRUE;

            case "bountyVal":
                bounty_value = Dm.num(text2num(@params["bountyval"]));
                if (!Dm.truthy(bounty_value))
                    bounty_value = 1;
                bounty_value = clamp(bounty_value, 1, 1000);
                break;

            case "bountyText":
            {
                object pre_bounty_text = @params["bountytext"];
                if (!Dm.truthy(pre_bounty_text))
                    return null;
                bounty_text = Dm.str(pre_bounty_text);
                break;
            }
        }
        return TRUE;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:489
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        await base.on_set_machine_stat(old_value);
        update_appearance();
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:493
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_paper)
            return NONE;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
            return ITEM_INTERACT_BLOCKING;
        paper_remaining++;
        to_chat(user, span_notice(I18n.T("You insert [] into []! It now holds [] sheet\\s of paper.", tool, this, paper_remaining)));
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    ///returns (machine_stat & broken)
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:504
    public virtual object needs_repair() => Dm.bitand(machine_stat, BROKEN);

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:507
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        object dot = ITEM_INTERACT_SUCCESS;
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            to_chat(user, span_notice(I18n.T("[] does not need repairs.", this)));
            return dot;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return dot;
        user.balloon_alert_to_viewers(I18n.T("started welding..."), I18n.T("started repairing..."));
        audible_message(span_hear(I18n.T("You hear welding.")));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(needs_repair)))))
        {
            user.balloon_alert_to_viewers(I18n.T("stopped welding!"), I18n.T("interrupted the repair!"));
            return dot;
        }
        user.balloon_alert_to_viewers(I18n.T("repaired []", this));
        atom_integrity = max_integrity;
        await set_machine_stat(Dm.bitand(machine_stat, ~BROKEN));
        return dot;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:525
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You start []securing []...", Dm.truthy(anchored) ? "un" : "", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 60, volume: 50)))
            return null;
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            to_chat(user, span_warning(I18n.T("The broken remains of [] fall on the ground.", this)));
            Dm.@new<obj_item_stack_sheet_iron>(loc, 5);
            Dm.@new<obj_item_shard>(loc);
            Dm.@new<obj_item_shard>(loc);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You []secure [].", Dm.truthy(anchored) ? "un" : "", this)));
            Dm.@new<obj_item_wallframe_newscaster>(loc);
        }
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:541
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                    playsound(loc, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 100, TRUE);
                else
                    playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 90, TRUE);
                break;
            case BURN:
                playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:552
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
        Dm.@new<obj_item_shard>(loc);
        Dm.@new<obj_item_shard>(loc);
        return null;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:557
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot))
            playsound(loc, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
        return dot;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:563
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(((mob_living)user).combat_mode))
            to_chat(user, span_warning(I18n.T("The newscaster controls are far too complicated for your tiny brain!")));
        else
            await take_damage(5, BRUTE, MELEE);
        return null;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:569
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        update_appearance();
        return dot;
    }

    /**
     * Sends photo data to build the newscaster article.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:576
    public virtual datum_picture send_photo_data()
    {
        if (current_image == null)
            return null;
        return current_image;
    }

    /**
     * This takes a held photograph, and updates the current_image variable with that of the held photograph's image.
     * *user: The mob who is being checked for a held photo object.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:585
    public virtual async DmTask attach_photo(mob user = null)
    {
        var photo = user.is_holding_item_of_type(typeof(obj_item_photo)) as obj_item_photo;
        if (photo != null)
            current_image = photo.picture;
        if (issilicon(user))
        {
            obj_item_camera_siliconcam targetcam = null;
            if (user is mob_living_silicon_ai ai)
                targetcam = ai.aicamera;
            else if (user is mob_living_silicon_pai pai)
            {
                if (pai.aicamera != null)
                    targetcam = pai.aicamera;
            }
            else if (user is mob_living_silicon_robot R)
            {
                if (R.connected_ai != null)
                    targetcam = R.connected_ai.aicamera;
                else
                    targetcam = R.aicamera;
            }
            else
                to_chat(user, span_warning(I18n.T("You cannot interface with silicon photo uploading!")));
            if (!Dm.truthy(targetcam.stored.len))
            {
                to_chat(user, span_bolddanger(I18n.T("No images saved.")));
                return;
            }
            var selection = await targetcam.selectpicture(user) as datum_picture;
            if (selection != null)
                current_image = selection;
        }
    }

    /**
     * This takes all current feed stories and messages, and prints them onto a newspaper, after checking that the newscaster has been loaded with paper.
     * The newscaster then prints the paper to the floor.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:617
    public virtual async DmTask<object> print_paper(mob user = null)
    {
        if (paper_remaining <= 0)
        {
            balloon_alert_to_viewers(I18n.T("out of paper!"));
            return TRUE;
        }
        SSblackbox.record_feedback("amount", "newspapers_printed", 1);
        var new_newspaper = Dm.@new<obj_item_newspaper>(loc);
        playsound(loc, SFX_PAGE_TURN, 50, TRUE);
        await try_put_in_hand(new_newspaper, user as mob_living);
        paper_remaining--;
        return null;
    }

    /**
     * This clears alerts on the newscaster from a new message being published and updates the newscaster's appearance.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:630
    public virtual void remove_alert()
    {
        alert = FALSE;
        update_appearance();
    }

    /**
     * When a new feed message is made that will alert all newscasters, this causes the newscasters to sent out a spoken message as well as create a sound.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:637
    public virtual void news_alert(object channel = null)
    {
        if (Dm.truthy(channel))
        {
            alert = TRUE;
            Waiting.Detach(say(I18n.T("Breaking news from []!", channel)));
            playsound(this, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 75, TRUE);
            update_appearance();
            _addtimer(Dm.@new<datum_callback>(this, nameof(remove_alert)), ALERT_DELAY, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/machinery/newscaster/newscaster_machine.dm", line: 643);
        }
        else
        {
            Waiting.Detach(say(I18n.T("Attention! Wanted issue distributed!")));
            playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 75, TRUE);
        }
    }

    /**
     * Performs a series of sanity checks before giving the user confirmation to create a new feed_channel using channel_name, and channel_desc.
     * *channel_locked: This variable determines if other users than the author can make comments and new feed_stories on this channel.
     *
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:654
    public virtual async DmTask<object> create_channel(mob user = null, object channel_locked = null, object cross_sector = null)
    {
        if (!Dm.truthy(channel_name))
            return null;
        if (Dm.truthy(cross_sector))
            channel_locked = TRUE;

        if (!Dm.truthy(channel_desc))
            return TRUE;

        if (channel_locked == null)
            return TRUE;

        var potential_channel = GLOB.news_network.network_channels_by_name[channel_name] as datum_feed_channel;
        if (potential_channel != null)
        {
            await tgui_alert(user, I18n.T("ERROR: Feed channel with that name already exists on the Network."), null, Dm.list(I18n.T("Okay")));
            return TRUE;
        }

        DmList hard_filter_result = is_ic_filtered(channel_name);
        if (hard_filter_result != null)
        {
            await tgui_alert(user, I18n.T("Your channel name contains: (\"[]\"), which is not allowed on this server.", hard_filter_result[CHAT_FILTER_INDEX_WORD]));
            return TRUE;
        }

        object choice = await tgui_alert(user, I18n.T("Please confirm feed channel creation"), I18n.T("Network Channel Handler"), Dm.list(I18n.T("Confirm"), I18n.T("Cancel")));
        creating_channel = FALSE;
        if (!Dm.eq(choice, I18n.T("Confirm")))
        {
            await update_static_data(user);
            return null;
        }

        float approval_time = CROSS_SECTOR_CANCEL_TIME;
        DmList soft_filter_result = is_soft_ooc_filtered(channel_name);
        if (soft_filter_result != null)
        {
            if (!Dm.eq(await tgui_alert(user, I18n.T("Your channel name contains \"[]\". \"[]\", Are you sure you want to use it?", soft_filter_result[CHAT_FILTER_INDEX_WORD], soft_filter_result[CHAT_FILTER_INDEX_REASON]), I18n.T("Soft Blocked Word"), Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("Yes")))
                return null;
            message_admins(Dm.text("[] has passed the soft filter for \"[]\". They may be using a disallowed term for a cross-station newscaster channel. Increasing delay time to reject.\n\n Channel name: \"[]\"", ADMIN_LOOKUPFLW(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], Dm.html_encode(channel_name)));
            log_admin_private(Dm.text("[] has passed the soft filter for \"[]\". They may be using a disallowed term for a cross-station newscaster channel. Increasing delay time to reject.\n\n Channel name: \"[]\"", key_name(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], channel_name));
            approval_time = EXTENDED_CROSS_SECTOR_CANCEL_TIME;
        }

        if (!Dm.truthy(cross_sector))
        {
            await finish_channel_creation(user, channel_locked, FALSE);
            return null;
        }

        awaiting_approval = TRUE;
        await update_static_data(user);
        user.log_message(Dm.text("is about to create a cross-sector newscaster channel with the following name: []", channel_name), LOG_GAME);
        // tg: :700-707  to_chat(GLOB.admins, the cross-sector channel's notice with its REJECT link): the admins' notice (admin tooling);
        // the channel is approved when the time runs out, as in tg with no admin to reject it
        channel_approval_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(finish_channel_creation), user, channel_locked, TRUE, approval_time), approval_time, TIMER_STOPPABLE, file: "code/game/machinery/newscaster/newscaster_machine.dm", line: 708);
        return null;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:710
    public virtual async DmTask finish_channel_creation(mob user = null, object channel_locked = null, object cross_sector = null, object cross_sector_delay = null)
    {
        channel_approval_timer = null;
        awaiting_approval = FALSE;
        GLOB.news_network.create_feed_channel(channel_name, newscaster_username, channel_desc, locked: channel_locked, author_ckey: user?.ckey, cross_sector: Dm.truthy(cross_sector) ? TRUE : FALSE, cross_sector_delay: cross_sector_delay);
        SSblackbox.record_feedback("text", "newscaster_channels", 1, Dm.text("[]", channel_name));
        await update_static_data(user);
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:717
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (!Dm.truthy(href_list?["reject_channel_creation"]))
            return await base.Topic(href, href_list);

        if (usr?.client?.holder == null)
        {
            usr?.log_message("tried to reject the creation of a cross-sector newscaster channel without being an admin.", LOG_ADMIN);
            message_admins(Dm.text("[] tried to reject the creation of a cross-sector newscaster channel without being an admin.", key_name(usr)));
            return null;
        }

        if (channel_approval_timer == null)
        {
            to_chat(usr, span_warning(I18n.T("It's too late!")));
            return null;
        }

        deltimer(channel_approval_timer);
        channel_approval_timer = null;

        log_admin(Dm.text("[] has cancelled the creation of a cross-sector newscaster channel.", key_name(usr)));
        message_admins(Dm.text("[] has cancelled the creation of a cross-sector newscaster channel.", key_name(usr)));
        return TRUE;
    }

    /**
     * Constructs a comment to attach to the currently selected feed_message of choice, assuming that a user can be found and that a message body has been written.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:740
    public virtual object create_comment(mob user = null)
    {
        if (!Dm.truthy(comment_text))
        {
            creating_comment = FALSE;
            return TRUE;
        }
        if (!Dm.truthy(newscaster_username))
        {
            creating_comment = FALSE;
            return TRUE;
        }
        GLOB.news_network.submit_comment(user, comment_text, newscaster_username, current_message);
        creating_comment = FALSE;
        return null;
    }

    /**
     * This proc performs checks before enabling the creating_channel var on the newscaster, such as preventing a user from having multiple channels,
     * preventing an un-ID'd user from making a channel, and preventing censored authors from making a channel.
     * Otherwise, sets creating_channel to TRUE.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:755
    public virtual async DmTask<object> start_create_channel(mob user = null)
    {
        //This first block checks for pre-existing reasons to prevent you from making a new channel, like being censored, or if you have a channel already.
        DmList existing_authors = Dm.list();
        foreach (datum_feed_channel iterated_feed_channel in Dm.each_cast<datum_feed_channel>(GLOB.news_network.network_channels))
        {
            if (Dm.truthy(iterated_feed_channel.author_censor))
                Dm.append(ref existing_authors, GLOB.news_network.redacted_text);
            else
                Dm.append(ref existing_authors, iterated_feed_channel.author);
        }
        if (!Dm.truthy(newscaster_username) || Dm.@in(newscaster_username, existing_authors))
        {
            creating_channel = FALSE;
            await tgui_alert(user, I18n.T("ERROR: User cannot be found or already has an owned feed channel."), null, Dm.list(I18n.T("Okay")));
            return TRUE;
        }
        creating_channel = TRUE;
        return TRUE;
    }

    /**
     * Creates a new feed story to the global newscaster network.
     * Verifies that the message is being written to a real feed_channel, then provides a text input for the feed story to be written into.
     * Finally, it submits the message to the network, is logged globally, and clears all message-specific variables from the machine.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:775
    public virtual async DmTask<object> create_story(mob user = null, object channel_id = null)
    {
        var potential_channel = GLOB.news_network.network_channels_by_id[Dm.text("[]", channel_id)] as datum_feed_channel;
        if (potential_channel == null)
            return null;
        current_channel = potential_channel;

        if (Dm.truthy(current_channel.receiving_cross_sector))
            return null;

        object temp_message = await tgui_input_text(user, I18n.T("Write your Feed story"), I18n.T("Network Channel Handler"), feed_channel_message, max_length: MAX_MESSAGE_LEN, multiline: TRUE);
        if (length(temp_message) <= 1)
            return TRUE;

        if (Dm.truthy(temp_message))
            feed_channel_message = temp_message;

        GLOB.news_network.submit_article(Dm.text("<font face=\"[]\">[]</font>", PEN_FONT, parsemarkdown(feed_channel_message, user)), newscaster_username, current_channel.channel_name, send_photo_data(), adminMessage: FALSE, allow_comments: TRUE, author_mob: user);
        SSblackbox.record_feedback("amount", "newscaster_stories", 1);
        feed_channel_message = "";
        current_image = null;
        return null;
    }

    /**
     * Selects a currently held photo from the user's hand and makes it the current_image held by the newscaster.
     * If a photo is still held in the newscaster, it will otherwise clear it from the machine.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:800
    public virtual async DmTask<object> toggle_photo(mob user = null)
    {
        if (current_image != null)
        {
            balloon_alert(user, I18n.T("current photo cleared."));
            current_image = null;
            return TRUE;
        }

        await attach_photo(user);
        if (current_image != null)
            balloon_alert(user, I18n.T("photo selected."));
        else
            balloon_alert(user, I18n.T("no photo identified."));
        return null;
    }

    // tg: code/game/machinery/newscaster/newscaster_machine.dm:812
    public virtual object clear_wanted_issue(mob user = null)
    {
        obj_item_card_id id_card = null;
        if (user is mob_living living_user)
            id_card = living_user.get_idcard(hand_first: TRUE) as obj_item_card_id;
        if (!Dm.@in(security_access, id_card?.GetAccess() as DmList))
        {
            Waiting.Detach(say(I18n.T("Clearance not found.")));
            return TRUE;
        }
        GLOB.news_network.wanted_issue.active = FALSE;
        return TRUE;
    }

    /**
     * This proc removes a station_request from the global list of requests, after checking that the owner of that request is the one who is trying to remove it.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:826
    public virtual async DmTask<object> delete_bounty_request()
    {
        if (active_request == null || current_user == null)
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
            return TRUE;
        }
        if (!Dm.eq(active_request?.owner, current_user.account_holder))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
            return TRUE;
        }
        await say(I18n.T("Deleted current request."));
        GLOB.request_list.Remove(active_request);
        return null;
    }

    /**
     * This creates a new bounty to the global list of bounty requests, alongisde the provided value of the request, and the owner of the request.
     * For more info, see datum/station_request.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:840
    public virtual async DmTask<object> create_bounty()
    {
        if (current_user == null || !Dm.truthy(bounty_text))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
            return TRUE;
        }
        foreach (datum_station_request iterated_station_request in Dm.each_cast<datum_station_request>(GLOB.request_list))
        {
            if (Dm.eq(iterated_station_request.req_number, current_user.account_id))
            {
                await say(I18n.T("Account already has active bounty."));
                return TRUE;
            }
        }
        var curr_request = Dm.@new<datum_station_request>(current_user.account_holder, bounty_value, bounty_text, current_user.account_id, current_user);
        Dm.append(ref GLOB.request_list, Dm.list(curr_request));
        foreach (obj iterated_bounty_board in Dm.each_cast<obj>(GLOB.allbountyboards))
        {
            await iterated_bounty_board.say(I18n.T("New bounty added!"));
            playsound(iterated_bounty_board.loc, Dm.resource("sound/effects/cashregister.ogg"), 30, TRUE);
        }
        return null;
    }

    /**
     * This sorts through the current list of bounties, and confirms that the intended request found is correct.
     * Then, adds the current user to the list of applicants to that bounty.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:857
    public virtual async DmTask<object> apply_to_bounty()
    {
        if (current_user == null)
        {
            await say(I18n.T("No ID detected."));
            return TRUE;
        }
        if (Dm.eq(current_user.account_holder, active_request.owner))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
            return TRUE;
        }
        foreach (object new_apply in Dm.each(active_request?.applicants))
        {
            if (Dm.eq(current_user.account_holder, active_request?.applicants[new_apply]))
            {
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 20, TRUE);
                return TRUE;
            }
        }
        Dm.append(ref active_request.applicants, Dm.list(current_user));
        return null;
    }

    /**
     * This pays out the current request_target the amount held by the active request's assigned value, and then clears the active request from the global list.
     */
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:873
    public virtual async DmTask<object> pay_applicant(datum_bank_account payment_target = null)
    {
        if (current_user == null)
            return TRUE;
        if (!current_user.has_money(active_request.value) || !Dm.eq(current_user.account_holder, active_request.owner))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            return TRUE;
        }
        payment_target.transfer_money(current_user, active_request.value, I18n.T("Bounty Request"));
        await say(I18n.T("Paid out [] [].", active_request.value, MONEY_NAME));
        GLOB.request_list.Remove(active_request);
        qdel(active_request);
        return null;
    }
}

// tg: code/game/machinery/newscaster/newscaster_machine.dm:70
public partial class obj_machinery_newscaster_pai : obj_machinery_newscaster
{
    // tg: code/game/machinery/newscaster/newscaster_machine.dm:70
    public override object ui_state(mob user = null) => GLOB.deep_inventory_state;
}
