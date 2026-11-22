// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/newscaster/newspaper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6b (D326): the whole file, the newspaper: the issue the newscaster prints with the channels and the wanted issue of the
// moment of printing (the censorship by its times), its Newspaper window's pages, a scribble on a page, peek holes cut with something
// sharp, and reading it with both hands hides the reader's face and name behind it. The field of view cone the read paper narrows
// (`add_fov_trait`, living_fov.dm) is the 2D client's: the 3D view is the client's (as the panic attack's, not-translated section 150).
// The context tips and the suicide are live-partial rows. The var block is generated (D111).

/**
 * Newspapers
 * A static version of the newscaster, that won't update as new stories are added.
 * Can be scribbed upon to add extra text for future readers.
 */
// tg: code/game/machinery/newscaster/newspaper.dm:6
public partial class obj_item_newspaper : obj_item
{
    // tg: code/game/machinery/newscaster/newspaper.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): the screentips (D264)
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wielded))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwielded)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        RegisterSignal(this, COMSIG_ATOM_IGNITED_BY_ITEM, nameof(close_paper_ui));
        creation_time = GLOB.news_network.last_action;
        foreach (datum_feed_channel iterated_feed_channel in Dm.each<datum_feed_channel>(GLOB.news_network.network_channels))
            Dm.append(ref news_content, iterated_feed_channel);

        if (!Dm.truthy(GLOB.news_network.wanted_issue.active))
            return dot;
        saved_wanted_criminal = GLOB.news_network.wanted_issue.criminal;
        saved_wanted_body = GLOB.news_network.wanted_issue.body;
        if (GLOB.news_network.wanted_issue.img != null)
            saved_wanted_icon = GLOB.news_network.wanted_issue.img;
        return dot;
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:69
    [SignalHandler]
    public virtual void close_paper_ui()
    {
        SStgui.close_uis(this);
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:85
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER) || Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) || Dm.truthy(tool.sharpness))
        {
            if (Dm.truthy(punctured))
            {
                balloon_alert(user, I18n.T("already has holes!"));
                return ITEM_INTERACT_BLOCKING;
            }

            string used_verb = I18n.T("cutting out");
            if (!Dm.eq(tool.sharpness, SHARP_EDGED) || Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER))
                used_verb = I18n.T("puncturing");

            balloon_alert(user, I18n.T("[] peekholes...", used_verb));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                return ITEM_INTERACT_BLOCKING;
            }

            playsound(this, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);
            punctured = TRUE;
            // User has additional arms or something, I dunno
            if (loc is mob_living owner)
            {
                // owner.remove_fov_trait(REF(src), FOV_REVERSE_270_DEGRESS): the 2D field of view cone (the 3D view is the client's)
                owner.update_appearance(UPDATE_OVERLAYS);
            }
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(await user.can_write(tool, TRUE)))
            return NONE;

        if (Dm.eq(scribble_page, current_page))
        {
            user.balloon_alert(user, I18n.T("already scribbled!"));
            return ITEM_INTERACT_BLOCKING;
        }

        object new_scribble_text = await tgui_input_text(user, I18n.T("What do you want to scribble?"), I18n.T("Write something"), max_length: MAX_MESSAGE_LEN);
        if (new_scribble_text == null)
            return ITEM_INTERACT_BLOCKING;

        add_fingerprint(user);
        user.balloon_alert(user, I18n.T("scribbling..."));
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.balloon_alert(user, I18n.T("scribbled!"));
        scribble_page = current_page;
        scribble_text = new_scribble_text;
        return ITEM_INTERACT_SUCCESS;
    }

    ///Checks the creation time of the newspaper and compares it to list to see if the list is meant to be censored at the time of printing.
    // tg: code/game/machinery/newscaster/newspaper.dm:133
    public virtual object censored_check(DmList times_censored = null)
    {
        if (!Dm.truthy(times_censored.len))
            return FALSE;
        for (int i = (int)times_censored.len; i > 0; i--)
        {
            float num = Math.Abs(Dm.num(times_censored[i]));
            if (Dm.num(creation_time) <= num)
                continue;
            else
            {
                if (Dm.num(times_censored[i]) > 0)
                    return TRUE;
                else
                    return FALSE;
            }
        }
        return FALSE;
    }

    /// Called when you start reading the paper with both hands
    // tg: code/game/machinery/newscaster/newspaper.dm:148
    public virtual void on_wielded(obj_item source = null, mob_living user = null)
    {
        ADD_TRAIT(user, TRAIT_FACE_COVERED, REF(this));
        RegisterSignal(user, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(holder_updated_overlays));
        RegisterSignal(user, COMSIG_HUMAN_GET_VISIBLE_NAME, nameof(holder_checked_name));
        user.update_appearance(UPDATE_OVERLAYS);
        // if (!punctured) user.add_fov_trait(REF(src), FOV_REVERSE_270_DEGRESS): the 2D field of view cone (the 3D view is the client's)
        if (user is mob_living_carbon_human as_human)
            as_human.update_visible_name();
    }

    /// Called when you stop doing that
    // tg: code/game/machinery/newscaster/newspaper.dm:160
    public virtual void on_unwielded(obj_item source = null, mob_living user = null)
    {
        REMOVE_TRAIT(user, TRAIT_FACE_COVERED, REF(this));
        UnregisterSignal(user, Dm.list(COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_HUMAN_GET_VISIBLE_NAME));
        user.update_appearance(UPDATE_OVERLAYS);
        // if (!punctured) user.remove_fov_trait(REF(src), FOV_REVERSE_270_DEGRESS): the 2D field of view cone (the 3D view is the client's)
        if (user is mob_living_carbon_human as_human)
            as_human.update_visible_name();
    }

    /// Called when we're being read and overlays are updated, we should show a big newspaper over the reader
    // tg: code/game/machinery/newscaster/newspaper.dm:171
    [SignalHandler]
    public virtual void holder_updated_overlays(atom reader = null, DmList overlays = null)
    {
        Dm.append(ref overlays, mutable_appearance(icon, Dm.text("newspaper_held_over[]", Dm.truthy(punctured) ? "_holed" : ""), ABOVE_MOB_LAYER));
        Dm.append(ref overlays, mutable_appearance(icon, Dm.text("newspaper_held_under[]", Dm.truthy(punctured) ? "_holed" : ""), BELOW_MOB_LAYER));
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:176
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(punctured))
            Dm.append(ref dot, span_notice(I18n.T("It has a pair of small peek holes punctured near the top.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("You can cut out some peek holes using something [] or []...", span_bolditalic(I18n.T("sharp")), span_bolditalic(I18n.T("pointy")))));
        return dot;
    }

    /// Called when someone tries to figure out what our identity is, but they can't see it because of the newspaper
    // tg: code/game/machinery/newscaster/newspaper.dm:184
    [SignalHandler]
    public virtual void holder_checked_name(mob_living_carbon_human source = null, DmList identity = null)
    {
        float newspaper_obscurity_priority = 100; // how powerful obscuring your appearance with a newspaper is
        if (Dm.truthy(identity[VISIBLE_NAME_FORCED]))
        {
            if (Dm.num(identity[VISIBLE_NAME_FORCED]) > newspaper_obscurity_priority) // the other set forced name is forcier than breaking news
                return;
            else if (Dm.num(identity[VISIBLE_NAME_FORCED]) == newspaper_obscurity_priority)
                stack_trace(Dm.text("A name-setting signal operation ([]) has a priority collision with [].", identity[VISIBLE_NAME_FACE], this), "code/game/machinery/newscaster/newspaper.dm", 192);
            else
                identity[VISIBLE_NAME_FORCED] = newspaper_obscurity_priority;
        }
        identity[VISIBLE_NAME_FACE] = "";
        identity[VISIBLE_NAME_ID] = "";
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:198
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE)))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (Dm.truthy(ui))
            return null;
        ui = Dm.@new<datum_tgui>(user, this, "Newspaper", name);
        await ui.open();
        return null;
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:207
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "next_page":
                //We're at the very end, nowhere else to go.
                if (current_page == news_content.len + 1)
                    return TRUE;
                current_page++;
                break;
            case "prev_page":
                //We haven't started yet, nowhere else to go.
                if (!Dm.truthy(current_page))
                    return TRUE;
                current_page--;
                break;
            default:
                return TRUE;
        }
        SStgui.update_uis(this);
        playsound(this, SFX_PAGE_TURN, 50, TRUE);
        return TRUE;
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:229
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList channels = Dm.list();
        data["channels"] = channels;
        foreach (datum_feed_channel news_channels in Dm.each_cast<datum_feed_channel>(news_content))
        {
            Dm.append(ref channels, Dm.list(Dm.list(
                Dm.kv("name", news_channels.channel_name),
                Dm.kv("page_number", news_content.Find(news_channels)))));
        }
        return data;
    }

    // tg: code/game/machinery/newscaster/newspaper.dm:239
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["current_page"] = current_page;
        data["scribble_message"] = Dm.eq(scribble_page, current_page) ? scribble_text : null;
        // if(saved_wanted_icon) user << browse_rsc(saved_wanted_icon, "wanted_photo.png"): no picture has an image here (D104)
        data["wanted_criminal"] = saved_wanted_criminal;
        data["wanted_body"] = saved_wanted_body;
        data["wanted_photo"] = saved_wanted_icon != null ? "wanted_photo.png" : null;

        DmList channel_data = Dm.list();
        if (!Dm.truthy(current_page) || current_page == news_content.len + 1)
        {
            channel_data["channel_name"] = null;
            channel_data["author_name"] = null;
            channel_data["is_censored"] = null;
            channel_data["channel_messages"] = Dm.list();
            data["channel_data"] = Dm.list(channel_data);
            return data;
        }
        if (news_content[(int)current_page] is datum_feed_channel current_channel)
        {
            channel_data["channel_name"] = current_channel.channel_name;
            channel_data["author_name"] = current_channel.return_author(censored_check(current_channel.author_censor_time));
            channel_data["is_censored"] = censored_check(current_channel.D_class_censor_time);
            DmList channel_messages = Dm.list();
            channel_data["channel_messages"] = channel_messages;
            foreach (datum_feed_message feed_messages in Dm.each_cast<datum_feed_message>(current_channel.messages))
            {
                if (Dm.num(feed_messages.creation_time) > Dm.num(creation_time))
                    continue;
                data["channel_has_messages"] = TRUE;
                bool has_image = false;
                if (feed_messages.img != null)
                {
                    has_image = true;
                    // user << browse_rsc(feed_messages.img, "tmp_photo[feed_messages.message_id].png"): no picture has an image here (D104)
                }
                Dm.append(ref channel_messages, Dm.list(Dm.list(
                    Dm.kv("message", Dm.text("-[]", feed_messages.return_body(censored_check(feed_messages.body_censor_time)))),
                    Dm.kv("photo", has_image ? Dm.text("tmp_photo[].png", feed_messages.message_id) : null),
                    Dm.kv("author", feed_messages.return_author(censored_check(feed_messages.author_censor_time))))));
            }
        }
        data["channel_data"] = Dm.list(channel_data);
        return data;
    }
}
