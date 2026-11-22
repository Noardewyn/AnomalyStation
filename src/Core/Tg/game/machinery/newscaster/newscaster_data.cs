// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/newscaster/newscaster_data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6b (D326, after gaps P4a-P4c): the whole file, the news network: its channels, feed messages, comments and the wanted
// issue, the censorship's times, and the bounty board's station requests. A cross-sector channel or article goes to no other server
// (send2otherserver: the port runs one server, no linked servers in its config, as a tg server without them). A photo's picture has
// no image here (the camera's picture, not-translated section 107), so articles keep the caption and no photo file. The var blocks are
// generated (D111).

// tg: code/game/machinery/newscaster/newscaster_data.dm:1-5 (interleaved globals, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/newscaster/newscaster_data.dm:1  GLOBAL_DATUM_INIT(news_network, /datum/feed_network, new)
    public datum_feed_network news_network;

    // tg: code/game/machinery/newscaster/newscaster_data.dm:1  (GLOBAL_MANAGED(news_network, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnews_network()
    {
        news_network = Dm.@new<datum_feed_network>();
        Dm.append(ref gvars_datum_init_order, "news_network");
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:2  GLOBAL_LIST_EMPTY(allCasters)
    public DmList allCasters;

    // tg: code/game/machinery/newscaster/newscaster_data.dm:2  (GLOBAL_MANAGED(allCasters, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalallCasters()
    {
        allCasters = Dm.list();
        Dm.append(ref gvars_datum_init_order, "allCasters");
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:4  GLOBAL_LIST_EMPTY(allbountyboards)
    public DmList allbountyboards;

    // tg: code/game/machinery/newscaster/newscaster_data.dm:4  (GLOBAL_MANAGED(allbountyboards, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalallbountyboards()
    {
        allbountyboards = Dm.list();
        Dm.append(ref gvars_datum_init_order, "allbountyboards");
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:5  GLOBAL_LIST_EMPTY(request_list)
    public DmList request_list;

    // tg: code/game/machinery/newscaster/newscaster_data.dm:5  (GLOBAL_MANAGED(request_list, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrequest_list()
    {
        request_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "request_list");
    }
}

// tg: code/game/machinery/newscaster/newscaster_data.dm:15
public partial class datum_feed_message : datum
{
    // tg: code/game/machinery/newscaster/newscaster_data.dm:49
    public virtual string return_author(object censor = null)
    {
        if (Dm.eq(censor, -1))
            censor = author_censor;
        string txt = Dm.text("[]", GLOB.news_network.redacted_text);
        if (!Dm.truthy(censor))
            txt = author;
        return txt;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:57
    public virtual string return_body(object censor = null)
    {
        if (Dm.eq(censor, -1))
            censor = body_censor;
        string txt = Dm.text("[]", GLOB.news_network.redacted_text);
        if (!Dm.truthy(censor))
            txt = body;
        return txt;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:65
    public virtual void toggle_censor_author()
    {
        if (Dm.truthy(author_censor))
            author_censor_time.Add(Dm.num(GLOB.news_network.last_action) * -1);
        else
            author_censor_time.Add(Dm.num(GLOB.news_network.last_action));
        author_censor = Dm.truthy(author_censor) ? FALSE : TRUE;
        GLOB.news_network.last_action = Dm.num(GLOB.news_network.last_action) + 1;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:73
    public virtual void toggle_censor_body()
    {
        if (Dm.truthy(body_censor))
            body_censor_time.Add(Dm.num(GLOB.news_network.last_action) * -1);
        else
            body_censor_time.Add(Dm.num(GLOB.news_network.last_action));
        body_censor = Dm.truthy(body_censor) ? FALSE : TRUE;
        GLOB.news_network.last_action = Dm.num(GLOB.news_network.last_action) + 1;
    }
}

// tg: code/game/machinery/newscaster/newscaster_data.dm:81
public partial class datum_feed_channel : datum
{
    // tg: code/game/machinery/newscaster/newscaster_data.dm:109
    public virtual string return_author(object censor = null)
    {
        if (Dm.eq(censor, -1))
            censor = author_censor;
        string txt = Dm.text("[]", GLOB.news_network.redacted_text);
        if (!Dm.truthy(censor))
            txt = author;
        return txt;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:117
    public virtual void toggle_censor_D_class()
    {
        if (Dm.truthy(censored))
            D_class_censor_time.Add(Dm.num(GLOB.news_network.last_action) * -1);
        else
            D_class_censor_time.Add(Dm.num(GLOB.news_network.last_action));
        censored = Dm.truthy(censored) ? FALSE : TRUE;
        GLOB.news_network.last_action = Dm.num(GLOB.news_network.last_action) + 1;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:125
    public virtual void toggle_censor_author()
    {
        if (Dm.truthy(author_censor))
            author_censor_time.Add(Dm.num(GLOB.news_network.last_action) * -1);
        else
            author_censor_time.Add(Dm.num(GLOB.news_network.last_action));
        author_censor = Dm.truthy(author_censor) ? FALSE : TRUE;
        GLOB.news_network.last_action = Dm.num(GLOB.news_network.last_action) + 1;
    }
}

// tg: code/game/machinery/newscaster/newscaster_data.dm:149
public partial class datum_feed_network : datum
{
    // tg: code/game/machinery/newscaster/newscaster_data.dm:167
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        create_feed_channel(NEWSCASTER_STATION_ANNOUNCEMENTS, "SS13", I18n.T("Company news, staff announcements, and all the latest information. Have a secure shift!"), locked: TRUE);
        create_feed_channel(NEWSCASTER_SPACE_BETTING, "NtOS", I18n.T("News from the SpaceBet PDA App! Download now and make your own bets!"), locked: TRUE);
        wanted_issue = Dm.@new<datum_wanted_message>();
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:172
    public virtual void add_feed_channel(datum_feed_channel new_channel = null)
    {
        Dm.append(ref network_channels, new_channel);
        network_channels_by_id[Dm.text("[]", new_channel.channel_id)] = new_channel;
        network_channels_by_name[Dm.text("[]", new_channel.channel_name)] = new_channel;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:177
    public virtual datum_feed_channel create_feed_channel(object channel_name = null, object author = null, object desc = null, object locked = null, float adminChannel = FALSE, object author_ckey = null, float cross_sector = FALSE, object cross_sector_delay = null, float receiving_cross_sector = FALSE)
    {
        var new_channel = Dm.@new<datum_feed_channel>();
        new_channel.channel_name = Dm.str(channel_name);
        new_channel.author = Dm.str(author);
        new_channel.channel_desc = Dm.str(desc);
        new_channel.locked = Dm.truthy(locked) ? TRUE : FALSE;
        new_channel.is_admin_channel = adminChannel;
        new_channel.receiving_cross_sector = receiving_cross_sector;
        new_channel.channel_id = next_channel_id;
        next_channel_id++;

        add_feed_channel(new_channel);

        if (!Dm.truthy(cross_sector))
            return new_channel;
        new_channel.cross_sector = TRUE;
        // tg: :193-199  send2otherserver(html_decode(station_name()), channel_name, "create_news_channel", additional_data = list(author,
        // author_ckey, desc, delay)): the port runs one server and links none (tg's sends nothing without linked servers in its config)
        return new_channel;
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:201
    public virtual datum_feed_message submit_article(object msg = null, object author = null, object channel_name = null, datum_picture picture = null, float adminMessage = FALSE, float allow_comments = TRUE, float update_alert = TRUE, mob author_mob = null)
    {
        var chosen_channel = network_channels_by_name[channel_name] as datum_feed_channel;
        if (chosen_channel == null)
        {
            stack_trace(Dm.text("Article submitted to non-existent newscaster channel: []", channel_name), "code/game/machinery/newscaster/newscaster_data.dm", 204);
            return null;
        }

        var new_article = Dm.@new<datum_feed_message>();
        new_article.author = Dm.str(author);
        new_article.body = Dm.str(msg);
        new_article.time_stamp = Dm.text("[]", round_timestamp());
        new_article.is_admin_message = adminMessage;
        new_article.locked = Dm.truthy(allow_comments) ? FALSE : TRUE;

        message_count++;
        last_action = Dm.num(last_action) + 1;
        new_article.creation_time = last_action;
        new_article.message_id = message_count;

        if (picture != null)
        {
            new_article.img = picture.picture_image;
            new_article.caption = Dm.str(picture.caption);
            // new_article.photo_file = save_photo(picture.picture_image): a picture has no image to save here (section 107)
            if (picture.picture_image != null)
                new_article.photo_file = save_photo(picture.picture_image);
        }

        Dm.append(ref chosen_channel.messages, new_article);
        new_article.parent_id = chosen_channel.channel_id;

        if (Dm.truthy(chosen_channel.cross_sector))
        {
            // Newscaster articles could be huge, and usefulness of first 50 symbols is dubious
            // tg: :229-235  message_admins(...) and send2otherserver(..., "create_news_article", ...): the admins' notice (admin tooling) and
            // the other servers (none linked, as above)
        }

        if (Dm.truthy(update_alert))
        {
            foreach (obj_machinery_newscaster caster in Dm.each_cast<obj_machinery_newscaster>(GLOB.allCasters))
                caster.news_alert(channel_name);
        }
        return new_article;
    }

    ///Submits a comment on the news network
    // tg: code/game/machinery/newscaster/newscaster_data.dm:243
    public virtual void submit_comment(mob user = null, object comment_text = null, object newscaster_username = null, datum_feed_message current_message = null)
    {
        var new_feed_comment = Dm.@new<datum_feed_comment>();
        new_feed_comment.author = Dm.str(newscaster_username);
        new_feed_comment.body = Dm.str(comment_text);
        new_feed_comment.time_stamp = round_timestamp();
        GLOB.news_network.last_action = Dm.num(GLOB.news_network.last_action) + 1;
        Dm.append(ref current_message.comments, new_feed_comment);
        if (user != null)
            user.log_message(Dm.text("(as []) commented on message [] -- []", newscaster_username, current_message.return_body(-1), current_message.body), LOG_COMMENT);
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:253
    public virtual void submit_wanted(object criminal = null, object body = null, object scanned_user = null, datum_picture picture = null, float adminMsg = FALSE, float newMessage = FALSE)
    {
        wanted_issue.active = TRUE;
        wanted_issue.criminal = criminal;
        wanted_issue.body = body;
        wanted_issue.scanned_user = scanned_user;
        wanted_issue.is_admin_msg = adminMsg;
        if (picture != null)
        {
            wanted_issue.img = picture.picture_image;
            if (picture.picture_image != null) // a picture has no image to save here (section 107)
                wanted_issue.photo_file = save_photo(picture.picture_image);
        }
        if (Dm.truthy(newMessage))
        {
            foreach (obj_machinery_newscaster N in Dm.each_cast<obj_machinery_newscaster>(GLOB.allCasters))
                N.news_alert();
        }
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:266
    public virtual void delete_wanted()
    {
        wanted_issue.active = FALSE;
        wanted_issue.criminal = null;
        wanted_issue.body = null;
        wanted_issue.scanned_user = null;
        wanted_issue.img = null;
        foreach (obj_machinery_newscaster updated_newscaster in Dm.each_cast<obj_machinery_newscaster>(GLOB.allCasters))
            updated_newscaster.update_appearance();
    }

    // tg: code/game/machinery/newscaster/newscaster_data.dm:275
    public virtual object save_photo(icon photo = null)
    {
        // var/photo_file = copytext_char(md5("\icon[photo]"), 1, 6), and the photo saved as a PNG in the round's log folder
        // (fcopy of a cleaned icon): the server renders no icons (D104), so no picture reaches here with an image to save
        Dm.Deferred("code/game/machinery/newscaster/newscaster_data.dm:275", "save_photo: the photo's PNG in the round's log folder (the server renders no icons, D104)");
        return null;
    }
}

//**************************
//	 Bounty Board Datums
//**************************

/**
 * A combined all in one datum that stores everything about the request, the requester's account, as well as the requestee's account
 * All of this is passed to the Request Console UI in order to present in organized way.
 */
// tg: code/game/machinery/newscaster/newscaster_data.dm:293
public partial class datum_station_request : datum
{
    // tg: code/game/machinery/newscaster/newscaster_data.dm:307
    [DmParams("owned", "newvalue", "newdescription", "reqnum", "own_account")]
    public override async DmTask New(params object[] __extra)
    {
        object owned = Dm.arg(__extra, 0);
        object newvalue = Dm.arg(__extra, 1);
        object newdescription = Dm.arg(__extra, 2);
        object reqnum = Dm.arg(__extra, 3);
        object own_account = Dm.arg(__extra, 4);
        await base.New();
        owner = owned;
        value = newvalue;
        description = newdescription;
        req_number = reqnum;
        if (own_account is datum_bank_account account)
            owner_account = account;
    }
}
