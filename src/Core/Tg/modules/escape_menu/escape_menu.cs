// Translated from tgstation (snapshot 1d99bd7f): code/modules/escape_menu/escape_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice G4 (D282): the whole file but the suicide icon (tg's suicide is not ported, so the page has no Suicide button and no
// icon for it), the hidden verb reset_held_keys_verb (the port's client has no stuck keys to reset: its keys are the client's own) and
// the time dilation (SStime_track is not translated, DV205).
// DEVIATION(DV44): tg's page is the tgui-escape-menu bundle in a BYOND browser control; the port's client draws the menu natively from
// the same messages ("init", "state") and sends the same "action" messages.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/escape_menu/escape_menu.dm:18  GLOBAL_LIST_EMPTY(escape_menus)
    public DmList escape_menus;

    // tg: code/modules/escape_menu/escape_menu.dm:18  (GLOBAL_MANAGED(escape_menus, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalescape_menus()
    {
        escape_menus = Dm.list();
        Dm.append(ref gvars_datum_init_order, "escape_menus");
    }
}

public partial class client : datum
{
    // tg: code/modules/escape_menu/escape_menu.dm:22
    public virtual async DmTask initialize_escape_menu()
    {
        // set waitfor = FALSE: the caller detaches it
        await sleep(3 * SECONDS);
        // generate_escape_menu_suicide_icon(): the suicide icon (not ported, above)
        escape_menu = Dm.@new<datum_escape_menu>(this);
    }
}

// tg: code/modules/escape_menu/escape_menu.dm:31
[DmPath("/datum/escape_menu")]
public partial class datum_escape_menu : datum
{
    // tg: code/modules/escape_menu/escape_menu.dm:38
    public override async DmTask New(params object[] __extra)
    {
        client client = Dm.arg<client>(__extra, 0);
        this.client = client;
        window = Dm.@new<datum_tgui_window>(client, "escape_menu");
        window.is_browser = TRUE;
        window.initialize(
            strict_mode: TRUE,
            inline_css: Dm.file("tgui/public/tgui-escape-menu.bundle.css"),
            inline_js: Dm.file("tgui/public/tgui-escape-menu.bundle.js"));
        // window.send_asset(the escape menu's font, sounds and icons): DEVIATION(DV44), the client has them (tg's Pixellari and
        // Grand9K fonts, sound/misc/escape_menu)
        window.subscribe(this, nameof(on_message));

        RegisterSignal(client, COMSIG_QDELETING, nameof(on_client_qdel));
        RegisterSignal(client, COMSIG_CLIENT_MOB_LOGIN, nameof(on_client_mob_login));
        RegisterSignal(client, COMSIG_CLIENT_VERB_ADDED, nameof(on_verb_change)); // RegisterSignals(client, list(...), ...)
        RegisterSignal(client, COMSIG_CLIENT_VERB_REMOVED, nameof(on_verb_change));
        RegisterSignal(SSdcs, COMSIG_GLOB_STATION_NAME_CHANGED, nameof(on_station_name_changed));
        RegisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING, nameof(on_round_start));

        Dm.append(ref GLOB.escape_menus, this);
        send_init();
    }

    // tg: code/modules/escape_menu/escape_menu.dm:61
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.escape_menus, this);
        STOP_PROCESSING(SSescape_menu, this);
        window?.unsubscribe(this);
        window = null;
        client = null;
        return base.Destroy(force);
    }

    // tg: code/modules/escape_menu/escape_menu.dm:69
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        send_update(Dm.list(
            Dm.kv("serverTime", server_timestamp(format: "hh:mm:ss")),
            Dm.kv("shiftTime", SSticker.round_start_time == 0 ? "Pre-Game" : round_timestamp()),
            // "timeDilation": SStime_track (DV205)
            Dm.kv("admins", build_admin_list()),
            Dm.kv("players", build_player_list()),
            Dm.kv("ignoredOffline", build_ignored_offline())));
        return null;
    }

    // tg: code/modules/escape_menu/escape_menu.dm:79
    public virtual void on_client_qdel()
    {
        qdel(this);
    }

    // tg: code/modules/escape_menu/escape_menu.dm:83
    public virtual void on_client_mob_login()
    {
        send_update(Dm.list(Dm.kv("canLeaveBody", isliving(client?.mob) ? TRUE : FALSE)));
    }

    // tg: code/modules/escape_menu/escape_menu.dm:89
    public virtual void on_verb_change(client source = null, DmList verbs_changed = null)
    {
        if (Dm.@in(client.ADMINHELP_VERB, verbs_changed))
            send_update(Dm.list(Dm.kv("canAdminHelp", Dm.@in(client.ADMINHELP_VERB, this.client?.verbs) ? TRUE : FALSE)));
    }

    // tg: code/modules/escape_menu/escape_menu.dm:96
    public virtual void on_station_name_changed()
    {
        send_update(Dm.list(Dm.kv("stationName", station_name())));
    }

    // tg: code/modules/escape_menu/escape_menu.dm:102
    public virtual void on_round_start()
    {
        send_update(Dm.list(Dm.kv("shiftTime", round_timestamp())));
    }

    // tg: code/modules/escape_menu/escape_menu.dm:109
    public virtual void send_init()
    {
        DmList resources = Dm.list();
        // the config's githuburl, forumurl, rulesurl, wikiurl and configurl resources (Report Bug, Github, Forums, Rules, Wiki,
        // Config): their verbs open web pages the port does not have (interface.cs). The changelog (D282) is the lobby's alone (D286:
        // its corner button sends resource_changelog), so the menu lists none

        window.send_message("init", Dm.list(
            Dm.kv("stationName", station_name()),
            Dm.kv("roundId", GLOB.round_id ?? "Unset"),
            Dm.kv("serverTime", server_timestamp(format: "hh:mm:ss")),
            Dm.kv("shiftTime", SSticker.round_start_time == 0 ? "Pre-Game" : round_timestamp()),
            Dm.kv("mapName", SSmapping.current_map?.map_name ?? "Loading..."),
            Dm.kv("mapFeedbackLink", SSmapping.current_map?.feedback_link),
            Dm.kv("mapWebmap", SSmapping.current_map?.mapping_url),
            Dm.kv("canLeaveBody", isliving(client?.mob) ? TRUE : FALSE),
            Dm.kv("canAdminHelp", Dm.@in(client.ADMINHELP_VERB, this.client?.verbs) ? TRUE : FALSE),
            Dm.kv("canSeeNotes", config.Get(typeof(datum_config_entry_flag_see_own_notes))),
            Dm.kv("hasTicketNotification", this.client?.current_ticket != null && !Dm.truthy(this.client.current_ticket.player_replied) ? TRUE : FALSE),
            Dm.kv("admins", build_admin_list()),
            Dm.kv("players", build_player_list()),
            Dm.kv("ignoredOffline", build_ignored_offline()),
            Dm.kv("resources", resources)));
    }

    // tg: code/modules/escape_menu/escape_menu.dm:155
    public virtual DmList build_admin_list()
    {
        DmList result = Dm.list();
        foreach (client admin in Dm.each<client>(GLOB.admins))
        {
            Dm.append(ref result, Dm.list(Dm.list(
                Dm.kv("ckey", admin.ckey),
                Dm.kv("displayName", admin.holder?.fakekey ?? admin.ckey),
                Dm.kv("rank", admin.holder?.rank_names()),
                Dm.kv("feedbackLink", null), // admin.holder?.feedback_link(): the admins' feedback links (the database)
                Dm.kv("ping", round(admin.avgping, 1)),
                Dm.kv("ignored", Dm.@in(admin.ckey, client?.prefs?.ignoring) ? TRUE : FALSE),
                Dm.kv("isSelf", Dm.eq(admin.ckey, client?.ckey) ? TRUE : FALSE))));
        }
        return result;
    }

    // tg: code/modules/escape_menu/escape_menu.dm:169
    public virtual DmList build_player_list()
    {
        DmList result = Dm.list();
        foreach (client player in Dm.each<client>(GLOB.clients))
        {
            if (Dm.@in(player, GLOB.admins))
                continue; // GLOB.clients - GLOB.admins
            Dm.append(ref result, Dm.list(Dm.list(
                Dm.kv("ckey", player.ckey),
                Dm.kv("displayName", player.ckey),
                Dm.kv("ping", round(player.avgping, 1)),
                Dm.kv("ignored", Dm.@in(player.ckey, client?.prefs?.ignoring) ? TRUE : FALSE),
                Dm.kv("isSelf", Dm.eq(player.ckey, client?.ckey) ? TRUE : FALSE))));
        }
        return result;
    }

    // tg: code/modules/escape_menu/escape_menu.dm:181
    public virtual DmList build_ignored_offline()
    {
        DmList result = Dm.list();
        if (Dm.truthy(client?.prefs?.ignoring))
        {
            foreach (object ignored_key in Dm.each<object>(client.prefs.ignoring))
            {
                if (!Dm.@in(ignored_key, GLOB.directory))
                    Dm.append(ref result, ignored_key);
            }
        }
        return result;
    }

    // tg: code/modules/escape_menu/escape_menu.dm:189
    public virtual void send_update(DmList data = null)
    {
        window.send_message("state", data);
    }

    // tg: code/modules/escape_menu/escape_menu.dm:192
    public virtual async DmTask<object> on_message(object type = null, object payload = null, object href_list = null)
    {
        if (Dm.eq(type, "ready"))
        {
            send_init();
            return TRUE;
        }

        if (!Dm.eq(type, "action"))
            return FALSE;

        DmList payload_list = payload as DmList;
        object action = payload_list?["action"];
        if (Dm.eq(action, "opened"))
        {
            // the BYOND version warning (byond_build < 1680): no BYOND client
            START_PROCESSING(SSescape_menu, this);
            // DEVIATION(DV218): tg sends the ticket's notice with the page's init only (a BYOND page loads once); the menu opened
            // shows it as it is now, so Admin Help blinks while an admin's reply waits
            send_update(Dm.list(Dm.kv("hasTicketNotification", client?.current_ticket != null && !Dm.truthy(client.current_ticket.player_replied) ? TRUE : FALSE)));
        }
        else if (Dm.eq(action, "closed"))
            STOP_PROCESSING(SSescape_menu, this);
        else if (Dm.eq(action, "character") || Dm.eq(action, "settings"))
        {
            datum_preferences prefs = client?.prefs;
            if (prefs == null)
                return TRUE;
            prefs.current_window = Dm.eq(action, "character") ? PREFERENCE_TAB_CHARACTER_PREFERENCES : PREFERENCE_TAB_GAME_PREFERENCES;
            await prefs.update_static_data(client.mob);
            await prefs.ui_interact(client.mob);
        }
        else if (Dm.eq(action, "create_ticket"))
        {
            if (!Dm.@in(client.ADMINHELP_VERB, this.client?.verbs))
                return TRUE;
            this.client?.adminhelp();
        }
        else if (Dm.eq(action, "view_ticket"))
            this.client?.view_latest_ticket();
        else if (Dm.eq(action, "pray"))
            Dm.NotTranslated("code/modules/escape_menu/escape_menu.dm:225", "on_message: pray, the pray keybinding");
        else if (Dm.eq(action, "see_notes"))
        {
            if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_see_own_notes))))
            {
                to_chat(client.mob, span_notice("Seeing notes has been disabled on this server."));
                return TRUE;
            }
            Dm.NotTranslated("code/modules/escape_menu/escape_menu.dm:231", "on_message: browse_messages, the admin notes (the database)");
        }
        else if (Dm.eq(action, "ghost"))
        {
            mob_living living_user = client?.mob as mob_living;
            living_user?.ghost();
        }
        else if (Dm.eq(action, "suicide"))
            return TRUE; // human_user?.suicide(): tg's suicide is not ported (the page has no Suicide button)
        else if (Dm.eq(action, "quit"))
        {
            // winset(usr, null, list("command"=".quit")): DEVIATION(DV44), the client quits itself
        }
        else if (Dm.eq(action, "resource_changelog"))
            client?.changelog();
        else if (Dm.eq(action, "admin_notice"))
            Dm.NotTranslated("code/modules/escape_menu/escape_menu.dm:255", "on_message: admin_notice, the admin notice verb");
        else if (Dm.eq(action, "toggle_ignore"))
        {
            object ckey = payload_list["ckey"];
            if (!Dm.truthy(ckey) || Dm.eq(ckey, client?.ckey))
                return TRUE;
            if (Dm.@in(ckey, client?.prefs?.ignoring))
                Dm.remove(ref client.prefs.ignoring, ckey);
            else
                LAZYADD(ref client.prefs.ignoring, ckey);
            client.prefs.save_preferences();
            to_chat(client, span_notice(Dm.text("[] has been []ignored in OOC.", ckey, Dm.@in(ckey, client.prefs.ignoring) ? "" : "un")));
        }
        // the other resources (resource_bug, _github, _forums, _rules, _wiki, _config) are not sent (send_init)

        return TRUE;
    }
}

public partial class mob_living : mob
{
    // tg: code/modules/mob/dead/observer/observer.dm:307  GAME_VERB_DESC(/mob/living, ghost, "Ghost", "Relinquish your life and enter
    // the land of the dead.", "OOC"): the body
    public virtual async DmTask __gvb_ghost(DmList structured_args = null)
    {
        if (stat != STABLE && stat != DEAD)
            succumb();
        if (stat == DEAD)
        {
            if (!HAS_TRAIT(this, TRAIT_CORPSELOCKED)) //corpse-locked have to confirm with the alert below
            {
                ghostize(TRUE);
                return;
            }
        }
        object response = await tgui_alert(usr, "Are you sure you want to ghost? You won't be able to re-enter your body!", "Confirm Ghost Observe", Dm.list("Ghost", "Stay in Body"));
        if (!Dm.eq(response, "Ghost"))
            return; //didn't want to ghost after-all
        ghostize(FALSE); // FALSE parameter is so we can never re-enter our body. U ded.
    }

    // tg: code/modules/mob/dead/observer/observer.dm:307  the verb proc (SSverbs.invoke_verb: a caller without a client does nothing)
    public virtual void ghost(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_ghost(Dm.list()));
    }
}

public partial class mob_eye : mob
{
    // tg: code/modules/mob/dead/observer/observer.dm:321  GAME_VERB_DESC(/mob/eye, ghost, ...): the body
    public virtual async DmTask __gvb_ghost(DmList structured_args = null)
    {
        object response = await tgui_alert(usr, "Are you sure you want to ghost? If you ghost whilst still alive you cannot re-enter your body!", "Confirm Ghost Observe", Dm.list("Ghost", "Stay in Body"));
        if (!Dm.eq(response, "Ghost"))
            return;
        ghostize(FALSE);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:321  the verb proc
    public virtual void ghost(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_ghost(Dm.list()));
    }
}
