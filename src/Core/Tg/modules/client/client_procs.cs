// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/client_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the GLOB lists of builds (:5-13), is_localhost, client/New, client/Del, client/Destroy, set_eye,
// update_special_keybinds, set_right_click_menu_mode, update_ambience_pref, clear_screen, get_parallax, create_parallax and (M10x slice G6) is_afk translated (the
// client layer, M1 P1, scope 2b). Topic and the topic limiter (with the file-local defines LIMITER_SIZE, CURRENT_SECOND,
// SECOND_COUNT, CURRENT_MINUTE, MINUTE_COUNT and ADMINSWARNED_AT, :14-19, which only they use), the message and upload
// limits, the database and ban procs, the asset, verb, stat panel, admin and click procs come with their modules
// (M9-M10). The leaves of New and Destroy that the M1 path reaches and nothing in M1 reads are Dm.Deferred
// (docs/ai/specs/not-translated.md); the rest is Dm.NotTranslated.

public partial class datum_controller_global_vars
{
    ////////////
    //SECURITY//
    ////////////

    // tg: code/modules/client/client_procs.dm:5  GLOBAL_LIST_INIT(blacklisted_builds, list(...))
    public DmList blacklisted_builds;

    // tg: code/modules/client/client_procs.dm:5  (GLOBAL_MANAGED(blacklisted_builds, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblacklisted_builds()
    {
        blacklisted_builds = Dm.list(
            Dm.kv("1622", "Bug breaking rendering can lead to wallhacks."));
        Dm.append(ref gvars_datum_init_order, "blacklisted_builds");
    }

    // tg: code/modules/client/client_procs.dm:8  GLOBAL_LIST_INIT(unrecommended_builds, list(...))
    public DmList unrecommended_builds;

    // tg: code/modules/client/client_procs.dm:8  (GLOBAL_MANAGED(unrecommended_builds, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalunrecommended_builds()
    {
        unrecommended_builds = Dm.list(
            Dm.kv("1670", "Bug breaking in-world text rendering."),
            Dm.kv("1671", "Bug breaking in-world text rendering."),
            Dm.kv("1675", "Frequent crashing."),
            Dm.kv("1676", "Frequent crashing."));
        Dm.append(ref gvars_datum_init_order, "unrecommended_builds");
    }
}

// The [DmPath] of /client, a BYOND builtin type, is on its engine part Builtin/client.builtin.cs (rules 2.3).
public partial class client : datum
{
    // tg: code/modules/client/client_procs.dm:173
    public virtual object is_localhost()
    {
        return Dm.num(Dm.@in(address, is_localhost__localhost_addresses));
    }

    // tg: code/modules/client/client_procs.dm:174  var/static/localhost_addresses = list("127.0.0.1", "::1", null)
    // (rules 10.6: a proc-level static, its TgState field below)
    private static ref DmList is_localhost__localhost_addresses => ref TgState.Current.client__is_localhost__localhost_addresses;

    //checks if a client is afk
    //3000 frames = 5 minutes
    // tg: code/modules/client/client_procs.dm:958  (M10x slice G6: the admin counts of the admin help)
    public virtual object is_afk(object duration = null)
    {
        duration ??= config.Get(typeof(datum_config_entry_number_inactivity_period)); // CONFIG_GET(number/inactivity_period)
        if (inactivity > Dm.num(duration))
            return inactivity;
        return FALSE;
    }

    // tg: code/modules/client/client_procs.dm:194  (M9 slice A: speech)
    public virtual object handle_spam_prevention(object message = null, object mute_type = null)
    {
        //Increment message count
        total_message_count += 1;

        //store the total to act on even after a reset
        float cache = total_message_count;

        if (total_count_reset <= world.time)
        {
            total_message_count = 0;
            total_count_reset = world.time + 5 * SECONDS;
        }

        //If they're really going crazy, mute them
        if (cache >= SPAM_TRIGGER_AUTOMUTE * 2)
        {
            total_message_count = 0;
            total_count_reset = 0;
            // tg: :210  cmd_admin_mute(src, mute_type, 1): the admin mutes are the admin tools (M10); the message is suppressed as tg's
            Dm.Deferred("code/modules/client/client_procs.dm:210", "handle_spam_prevention: cmd_admin_mute, the automatic mute of a spammer (the admin tools, M10+)");
            return TRUE;
        }

        //Otherwise just supress the message
        else if (cache >= SPAM_TRIGGER_AUTOMUTE)
            return TRUE;


        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_automute_on))) && !Dm.truthy(holder) && Dm.eq(last_message, message))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_AUTOMUTE_CHECK]) ? NONE : mob._SendSignal(COMSIG_MOB_AUTOMUTE_CHECK, Dm.list(mob, this, last_message, mute_type)), WAIVE_AUTOMUTE_CHECK)))
                return FALSE;

            last_message_count++;
            if (last_message_count >= SPAM_TRIGGER_AUTOMUTE)
            {
                to_chat(this, span_danger(I18n.T("You have exceeded the spam filter limit for identical messages. A mute was automatically applied for the current round. Contact admins to request its removal.")));
                // tg: :225  cmd_admin_mute(src, mute_type, 1): the admin tools (M10)
                Dm.Deferred("code/modules/client/client_procs.dm:225", "handle_spam_prevention: cmd_admin_mute, the automatic mute of a spammer (the admin tools, M10+)");
                return TRUE;
            }
            if (last_message_count >= SPAM_TRIGGER_WARNING)
            {
                //"auto-ban" sends the message that the cold and uncaring gamecode has been designed to quiash you like a bug in short measure should you continue, and it's quite intentional that the user isn't told exactly what that entails.
                to_chat(this, span_userdanger(I18n.T("You are nearing the auto-ban limit for identical messages.")));
                mob.balloon_alert(mob, I18n.T("stop spamming!"));
                return FALSE;
            }
        }
        else
        {
            last_message = Dm.str(message);
            last_message_count = 0;
            return FALSE;
        }
        return null;
    }

    ///////////
    //CONNECT//
    ///////////

    // tg: code/modules/client/client_procs.dm:255
    // Rules 7.1: no `new` (/datum's New(params object[]) has another signature; slots.tsv still writes it).
    public virtual async DmTask<object> New(object TopicData = null)
    {
        object dot = null;
        object tdata = TopicData; //save this for later use
        TopicData = null; //Prevent calls to client.Topic from connect

        if (connection != "seeker" && connection != "web")//Invalid connection type.
            return null;

        Dm.append(ref GLOB.clients, this);
        GLOB.directory[ckey] = this;

        float reconnecting = FALSE;
        if (Dm.truthy(GLOB.persistent_clients_by_ckey[ckey]))
        {
            reconnecting = TRUE;
            persistent_client = GLOB.persistent_clients_by_ckey[ckey] as datum_persistent_client;
        }
        else
            persistent_client = Dm.@new<datum_persistent_client>(ckey);
        persistent_client.set_client(this);

        // tg: :274-280  winset(src, null, list("browser-options" = "find,refresh")); stat_panel = new(src, "statbrowser") and
        // its subscribe(src, PROC_REF(on_stat_panel_message)); tgui_panel = new(src, "browseroutput")
        Dm.Deferred("code/modules/client/client_procs.dm:274", "client/New: the browser options, the stat panel, the tgui chat panel (the BYOND skin and tgui, M10+)");
        tgui_say = Dm.@new<datum_tgui_say>(this, "tgui_say");
        // tg: :284  initialize_commandbar_spy(): the BYOND command bar's spy (the port has no command bar)

        // DEVIATION(DV161): tg's typing_indicator preference sets this in apply_to_client (say_modal/typing.dm:36) when the
        // preferences load; the preferences are not ported (prefs is null), so the toggle's default (on) is applied here.
        typing_indicators = TRUE;

        set_right_click_menu_mode(TRUE);

        GLOB.ahelp_tickets.ClientLogin(this); // tg: :288 (M10x slice G6)
        // tg: :289-290  GLOB.interviews.client_login(src); GLOB.requests.client_login(src)
        Dm.Deferred("code/modules/client/client_procs.dm:288", "client/New: the interviews and requests of the player (admin tooling, M10+)");
        //preferences datum - also holds some persistent data for the client (because we may as well keep these datums to a minimum)
        // tg: :292
        prefs = GLOB.preferences_datums[ckey] as datum_preferences;
        if (prefs != null)
        {
            prefs.parent = this;
            prefs.load_savefile(); // just to make sure we have the latest data
            prefs.apply_all_client_preferences();
        }
        else
        {
            prefs = Dm.@new<datum_preferences>(this);
            GLOB.preferences_datums[ckey] = prefs;
        }
        prefs.last_ip = address; //these are gonna be used for banning
        prefs.last_id = computer_id; //these are gonna be used for banning

        // tg: :303-307  if(fexists(roundend_report_file())) ASSIGN_GAME_VERB(src, /client, show_previous_roundend_report);
        // if(fexists("data/server_last_roundend_report.html")) ASSIGN_GAME_VERB(src, /client, show_servers_last_roundend_report)
        Dm.Deferred("code/modules/client/client_procs.dm:303", "client/New: the verbs that show the last round-end reports (verbs and the round end, M10+)");

        string full_version = Dm.text("[].[]", byond_version, Dm.truthy(byond_build) ? (object)byond_build : "xxx");
        log_access(Dm.text("Login: [] from []-[] || BYOND v[]", key_name(this), Dm.truthy(address) ? address : "localhost", computer_id, full_version));

        float alert_mob_dupe_login = FALSE;
        float alert_admin_multikey = FALSE;
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_log_access))))
        {
            DmList joined_players = Dm.list();
            foreach (object player_ckey in Dm.each(GLOB.joined_player_list))
                joined_players[player_ckey] = 1;

            foreach (object joined_player_ckey in Dm.each(Dm.bitor(GLOB.directory, joined_players)))
            {
                if (!Dm.truthy(joined_player_ckey) || Dm.eq(joined_player_ckey, ckey))
                    continue;

                datum_preferences joined_player_preferences = GLOB.preferences_datums[joined_player_ckey] as datum_preferences;
                if (!Dm.truthy(joined_player_preferences))
                    continue; //this shouldn't happen.

                // GAP(lead): /datum/preferences' last_ip and last_id (modules/client/preferences.dm) are not declared; the
                // preferences are not translated (M9), so GLOB.preferences_datums stays empty and this is never reached.
                Dm.NotTranslated("code/modules/client/client_procs.dm:325", "client/New: the multikey check against another player's preferences (last_ip, last_id; preferences, M10+)");
            }
        }

        dot = await New__d0(TopicData); //calls mob.Login()

        // Admin Verbs need the client's mob to exist. Must be after ..()
        float connecting_admin = FALSE; //because de-admined admins connecting should be treated like admins.
        //Admin Authorisation
        datum_admins admin_datum = GLOB.admin_datums[ckey] as datum_admins;
        if (admin_datum != null)
        {
            admin_datum.associate(this);
            connecting_admin = TRUE;
        }
        else if (Dm.truthy(GLOB.deadmins[ckey]))
        {
            // tg: :377  ASSIGN_GAME_VERB(src, /client, readmin): the admin verbs (not ported; the port's admin commands are the
            // server's console, D275)
            Dm.Deferred("code/modules/client/client_procs.dm:377", "client/New: the readmin verb of a deadmined admin (admin verbs, not ported)");
            connecting_admin = TRUE;
        }
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_autoadmin))))
        {
            if (!Dm.truthy(GLOB.admin_datums[ckey]))
            {
                DmList autoadmin_ranks = ranks_from_rank_name(config.Get(typeof(datum_config_entry_string_autoadmin_rank)));
                if (autoadmin_ranks.len == 0)
                    to_chat(world, "Autoadmin rank not found");
                else
                    Dm.@new<datum_admins>(autoadmin_ranks, ckey);
            }
        }

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_enable_localhost_rank))) && !Dm.truthy(connecting_admin) && Dm.truthy(is_localhost()))
        {
            var localhost_rank = Dm.@new<datum_admin_rank>("!localhost!", RANK_SOURCE_LOCAL, R_EVERYTHING, R_DBRANKS, R_EVERYTHING); //+EVERYTHING -DBRANKS *EVERYTHING
            if (QDELETED(localhost_rank))
            {
                to_chat(world, "Local admin rank creation failed, somehow?");
                return dot;
            }
            Dm.@new<datum_admins>(Dm.list(localhost_rank), ckey, 1, 1);
        }

        // tg: :395-398  if (length(GLOB.stickybanadminexemptions)) { GLOB.stickybanadminexemptions -= ckey;
        // if (!length(GLOB.stickybanadminexemptions)) restore_stickybans() }
        Dm.Deferred("code/modules/client/client_procs.dm:395", "client/New: the sticky ban admin exemptions (GLOB.stickybanadminexemptions, restore_stickybans; bans, M10+)");

        if (!Dm.truthy(byond_build))
        {
            message_admins(span_adminnotice(Dm.text("[] has been detected as spoofing their BYOND version. Connection rejected.", key_name(this))));
            // GAP(lead): add_system_note (modules/admin/sql_message_system.dm) and log_suspicious_login
            // (__HELPERS/logging/admin.dm) are not translated; the port's clients always report a build (client.EngineByondBuild).
            Dm.NotTranslated("code/modules/client/client_procs.dm:401", "client/New: add_system_note and log_suspicious_login for a spoofed BYOND version (admin notes, M10+)");
            qdel(this);
            return dot;
        }

        if (Dm.@in(num2text(byond_build), GLOB.blacklisted_builds))
        {
            log_access(Dm.text("Failed login: [] - blacklisted BYOND version", key));
            to_chat_immediate(this, span_userdanger(I18n.T("Your version of BYOND is blacklisted.")));
            to_chat_immediate(this, span_danger(I18n.T("BYOND build [] ([].[]) has been blacklisted for the following reason: [].", byond_build, byond_version, byond_build, GLOB.blacklisted_builds[num2text(byond_build)])));
            to_chat_immediate(this, span_danger(I18n.T("Please download a new version of BYOND. If [] is the latest, you can go to <a href=\"https://secure.byond.com/download/build\">BYOND's website</a> to download other versions.", byond_build)));
            if (Dm.truthy(connecting_admin))
                to_chat_immediate(this, I18n.T("As an admin, you are being allowed to continue using this version, but please consider changing BYOND versions."));
            else
            {
                qdel(this);
                return dot;
            }
        }

        if (Dm.truthy(SSinput.initialized))
            set_macros();

        // Initialize stat panel
        // tg: :423-433  stat_panel.initialize(inline_html = file("html/statbrowser.html"), ...);
        // addtimer(CALLBACK(src, PROC_REF(check_panel_loaded)), 30 SECONDS); INVOKE_ASYNC(src, PROC_REF(acquire_dpi));
        // tgui_panel.initialize()
        Dm.Deferred("code/modules/client/client_procs.dm:423", "client/New: the stat panel's initialization and its 30 s load check, the DPI query, the tgui chat panel (the BYOND skin and tgui, M10+)");
        Waiting.Detach(tgui_say.initialize());
        // tg: :437
        Waiting.Detach(initialize_escape_menu());
        // tg: :438
        initialize_lobby_menu();

        if (Dm.truthy(alert_mob_dupe_login) && !Dm.truthy(holder))
        {
            // Notify admins if the connecting player's CID is configured to be ignored by stickybans
            // GAP(lead): SSstickyban (controllers/subsystem/stickyban.dm; bans, M10) is not declared: read as null, as the
            // global of a subsystem that does not exist (a stand-in local, rules 2.5).
            object SSstickyban__gap = null;
            if (Dm.truthy(SSstickyban__gap))
            {
                Dm.NotTranslated("code/modules/client/client_procs.dm:442", "client/New: a CID that SSstickyban ignores (bans, M10+)");
            }
            else
            {
                // If the CID is not ignored, notify the player with the pop-up.
                string dupe_login_message = I18n.T("Your ComputerID has already logged in with another key this round, please log out of this one NOW or risk being banned!");
                // Notify admins if the connecting player's CID is a duplicate of another player's CID
                if (Dm.truthy(alert_admin_multikey))
                {
                    dupe_login_message += I18n.T("\nAdmins have been informed.");
                    message_admins(span_danger(Dm.text("<B>MULTIKEYING: </B></span><span class='notice'>[] has a matching CID+IP with another player and is clearly multikeying. They have been warned to leave the server or risk getting banned.", key_name_admin(this))));
                    log_admin_private(Dm.text("MULTIKEYING: [] has a matching CID+IP with another player and is clearly multikeying. They have been warned to leave the server or risk getting banned.", key_name(this)));
                }
                string dupe_login_message__s = dupe_login_message;
                spawn(0.5f * SECONDS, async () => //needs to run during world init, do not convert to add timer
                {
                    await alert(mob, dupe_login_message__s); //players get banned if they don't see this message, do not convert to tgui_alert (or even tg_alert) please.
                    to_chat_immediate(mob, span_danger(dupe_login_message__s));
                });
            }
        }

        connection_time = world.time;
        connection_realtime = world.realtime;
        connection_timeofday = world.timeofday;
        // tg: :462  winset(src, null, "command=\".configure graphics-hwmode on\"")
        Dm.Deferred("code/modules/client/client_procs.dm:462", "client/New: winset .configure graphics-hwmode on (the BYOND skin, not ported)");
        object breaking_version = config.Get(typeof(datum_config_entry_number_client_error_version));
        object breaking_build = config.Get(typeof(datum_config_entry_number_client_error_build));
        object warn_version = config.Get(typeof(datum_config_entry_number_client_warn_version));
        object warn_build = config.Get(typeof(datum_config_entry_number_client_warn_build));

        if (Dm.lt(byond_version, breaking_version) || (Dm.eq(byond_version, breaking_version) && Dm.lt(byond_build, breaking_build))) //Out of date client.
        {
            to_chat_immediate(this, span_danger(I18n.T("<b>Your version of BYOND is too old:</b>")));
            to_chat_immediate(this, config.Get(typeof(datum_config_entry_string_client_error_message)));
            to_chat_immediate(this, I18n.T("Your version: [].[]", byond_version, byond_build));
            to_chat_immediate(this, I18n.T("Required version: [].[] or later", breaking_version, breaking_build));
            to_chat_immediate(this, I18n.T("Visit <a href=\"https://secure.byond.com/download\">BYOND's website</a> to get the latest version of BYOND."));
            if (Dm.truthy(connecting_admin))
                to_chat_immediate(this, I18n.T("Because you are an admin, you are being allowed to walk past this limitation, But it is still STRONGLY suggested you upgrade"));
            else
            {
                qdel(this);
                return dot;
            }
        }
        else if (Dm.lt(byond_version, warn_version) || (Dm.eq(byond_version, warn_version) && Dm.lt(byond_build, warn_build))) //We have words for this client.
        {
            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_client_warn_popup))))
            {
                // GAP(lead): browse() and HTML_SKELETON are the UI seam (Appendix C, planned M9-M10).
                Dm.NotTranslated("code/modules/client/client_procs.dm:483", "client/New: the out-of-date BYOND warning popup (browse, the UI seam, M10+)");
            }
            else
            {
                to_chat(this, span_danger(I18n.T("<b>Your version of byond may be getting out of date:</b>")));
                to_chat(this, config.Get(typeof(datum_config_entry_string_client_warn_message)));
                to_chat(this, I18n.T("Your version: [].[]", byond_version, byond_build));
                to_chat(this, I18n.T("Required version to remove this message: [].[] or later", warn_version, warn_build));
                to_chat(this, I18n.T("Visit <a href=\"https://secure.byond.com/download\">BYOND's website</a> to get the latest version of BYOND."));
            }
        }

        if (connection == "web" && !Dm.truthy(connecting_admin))
        {
            if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_webclient))))
            {
                to_chat_immediate(this, I18n.T("Web client is disabled"));
                qdel(this);
                return dot;
            }
            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_webclient_only_byond_members))))
            {
                // GAP(lead): BYOND's client.IsByondMember() is not in the port (the port's clients connect as "seeker").
                Dm.NotTranslated("code/modules/client/client_procs.dm:504", "client/New: IsByondMember() for a web client (BYOND membership, not ported)");
                return dot;
            }
        }

        // SHIM-GAP: BYOND's world.address (the address of the machine hosting the world, null when it cannot be
        // determined) is not in the facade (Appendix C, planned with the server configuration); read as null (a stand-in
        // local, rules 2.5).
        object world_address__gap = null;
        if ((Dm.eq(world_address__gap, address) || !Dm.truthy(address)) && !Dm.truthy(GLOB.host))
        {
            GLOB.host = key;
            world.update_status();
        }

        if (Dm.truthy(holder))
        {
            // tg: :508-510  add_admin_verbs(), display_admin_memos(src), adminGreet(): the admin verbs, the memos and the login notice (not
            // ported: the port's admin commands are the server's console, D275)
            Dm.Deferred("code/modules/client/client_procs.dm:508", "client/New: add_admin_verbs, display_admin_memos and adminGreet of an admin (admin verbs and memos, not ported)");
        }
        if (Dm.truthy(mob) && Dm.truthy(reconnecting))
        {
            object stealth_admin = mob.client?.holder?.fakekey;
            object announce_leave = mob.client?.prefs?.read_preference(typeof(datum_preference_toggle_broadcast_login_logout));
            if (!Dm.truthy(stealth_admin))
                deadchat_broadcast(I18n.T(" has reconnected."), Dm.text("<b>[][]</b>", mob, mob.get_realname_string()), follow_target: mob, turf_target: get_turf(mob), message_type: DEADCHAT_LOGIN_LOGOUT, admin_only: Dm.truthy(announce_leave) ? FALSE : TRUE);
        }
        // tg: :526  add_verbs_from_config()
        Dm.Deferred("code/modules/client/client_procs.dm:526", "client/New: add_verbs_from_config (the verbs that the configuration enables, verbs, M10+)");

        // This needs to be before the client age from db is updated as it'll be updated by then.
        // tg: :529-536  the lastseen query (SSdbcore.NewQuery) and display_unread_notes(src, time_stamp)
        Dm.Deferred("code/modules/client/client_procs.dm:529", "client/New: the player's last-seen time and unread admin notes (the database: never, rules 13)");

        // tg: :538  var/cached_player_age = set_client_age_from_db(tdata)
        // set_client_age_from_db returns null without a database (`if(!SSdbcore.Connect()) return`, :719), which the port never has.
        object cached_player_age = null; //we have to cache this because other shit may change it and we need its current value now down below.
        Dm.Deferred("code/modules/client/client_procs.dm:538", "client/New: set_client_age_from_db(tdata), the player's age and related accounts from the database (never, rules 13; null without a database, as in tg)");
        if (isnum(cached_player_age) && Dm.eq(cached_player_age, -1)) //first connection
            player_age = 0;
        object nnpa = config.Get(typeof(datum_config_entry_number_notify_new_player_age));
        if (isnum(cached_player_age) && Dm.eq(cached_player_age, -1)) //first connection
        {
            if (Dm.ge(nnpa, 0))
            {
                // GAP(lead): log_admin_private and send2tgs_adminless_only (the first-connection notices) come with the
                // admin tooling (M10); cached_player_age is null without a database.
                Dm.NotTranslated("code/modules/client/client_procs.dm:544", "client/New: the first-connection notices to admins (admin tooling, M10+)");
            }
        }
        else if (isnum(cached_player_age) && Dm.lt(cached_player_age, nnpa))
            message_admins(Dm.text("New user: [] just connected with an age of [] day[]", key_name_admin(this), cached_player_age, player_age == 1 ? "" : "s"));
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_account_age_for_jobs))) && account_age >= 0)
            player_age = account_age;
        if (account_age >= 0 && Dm.lt(account_age, nnpa))
        {
            Dm.NotTranslated("code/modules/client/client_procs.dm:557", "client/New: the new BYOND account notice to admins (account_age comes from the database, never; admin tooling, M10+)");
        }
        // tg: :565-566  scream_about_watchlists(src); validate_key_in_db()
        Dm.Deferred("code/modules/client/client_procs.dm:565", "client/New: scream_about_watchlists and validate_key_in_db (the watchlists and the player table: the database, never, rules 13)");
        // If we aren't already generating a ban cache, fire off a build request
        // This way hopefully any users of request_ban_cache will never need to yield
        // tg: :569-570  if(!ban_cache_start && SSban_cache?.query_started) INVOKE_ASYNC(GLOBAL_PROC, GLOBAL_PROC_REF(build_ban_cache), src)
        Dm.Deferred("code/modules/client/client_procs.dm:569", "client/New: the ban cache build request (SSban_cache; bans, M10+)");

        // tg: :572  send_resources()
        Dm.Deferred("code/modules/client/client_procs.dm:572", "client/New: send_resources (the asset cache, M10+)");

        // tg: :574  apply_clickcatcher()
        Dm.Deferred("code/modules/client/client_procs.dm:574", "client/New: apply_clickcatcher (the screen's click catcher, the HUD, M10+)");

        if (!Dm.eq(prefs.lastchangelog, GLOB.changelog_hash)) //bolds the changelog button on the interface so we know there are updates.
        {
            to_chat(this, span_info(I18n.T("You have unread updates in the changelog.")));
            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_aggressive_changelog))))
                changelog();
        }

        // tg: :581-584  if(ckey in GLOB.clientmessages) { for(var/message in GLOB.clientmessages[ckey]) to_chat(src, message);
        // GLOB.clientmessages.Remove(ckey) }
        Dm.Deferred("code/modules/client/client_procs.dm:581", "client/New: the messages admins left for the player (GLOB.clientmessages; admin tooling, M10+)");

        // tg: :586-588  if(CONFIG_GET(flag/autoconvert_notes)) convert_notes_sql(ckey); display_admin_messages(src)
        Dm.Deferred("code/modules/client/client_procs.dm:586", "client/New: convert_notes_sql and display_admin_messages (admin notes in the database, never, rules 13)");
        // tg: :589-590  if(!winexists(src, "asset_cache_browser")) to_chat(... custom skin ...)
        Dm.Deferred("code/modules/client/client_procs.dm:589", "client/New: winexists(src, \"asset_cache_browser\"), the custom skin warning (the BYOND skin, not ported)");

        // tg: :592
        update_ambience_pref(prefs.read_preference(typeof(datum_preference_numeric_volume_sound_ambience_volume)));
        // tg: :593  check_ip_intel()
        Dm.Deferred("code/modules/client/client_procs.dm:593", "client/New: check_ip_intel (disabled in tg's config)");

        //This is down here because of the browse() calls in tooltip/New()
        // tg: :586-587  if(!tooltips) tooltips = new /datum/tooltip(src) (protocol version 19, DV47: the datum's output goes to the
        // native tooltip)
        if (!Dm.truthy(tooltips))
            tooltips = await Dm.new_async<datum_tooltip>(this);

        // tg: :589  loot_panel = new(src)
        loot_panel = Dm.@new<datum_lootpanel>(this);
        // tg: :591-594  view_size = new(src); view_size.resetFormat(); view_size.setZoomMode(); view_size.apply()
        Dm.Deferred("code/modules/client/client_procs.dm:591", "client/New: the view size datum (/datum/view_data: client.view; the view, M2)");
        Master.UpdateTickRate();
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_CLIENT_CONNECT, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_CLIENT_CONNECT]))
            SSdcs._SendSignal(COMSIG_GLOB_CLIENT_CONNECT, Dm.list(SSdcs, this));
        fully_created = TRUE;
        // tg: :610  set_fullscreen()
        Dm.Deferred("code/modules/client/client_procs.dm:610", "client/New: set_fullscreen (the fullscreen preference and the window, the BYOND skin, M10+)");
        return dot;
    }

    //////////////
    //DISCONNECT//
    //////////////

    // tg: code/modules/client/client_procs.dm:604
    // Rules 7.1: no `new` (the port declares no builtin /datum/Del; slots.tsv still writes it).
    public virtual async DmTask Del()
    {
        if (!Dm.truthy(gc_destroyed))
        {
            gc_destroyed = world.time;
            if (!QDELING(this))
                stack_trace("Client does not purport to be QDELING, this is going to cause bugs in other places!", "code/modules/client/client_procs.dm", 608);

            // Yes this is the same as what's found in qdel(). Yes it does need to be here
            // Get off my back
            if (Dm.truthy(_listen_lookup?[COMSIG_QDELETING]))
                _SendSignal(COMSIG_QDELETING, Dm.list(this, TRUE));
            Destroy(); //Clean up signals and timers.
        }
        await Del__d0();
    }

    // tg: code/modules/client/client_procs.dm:616
    // An override of /datum/Destroy (client's parent_type is /datum). It declares no parameter (rules 7.2: [DmParams()]).
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(mob))
        {
            object stealth_admin = mob.client?.holder?.fakekey;
            object announce_join = mob.client?.prefs?.read_preference(typeof(datum_preference_toggle_broadcast_login_logout));
            if (!Dm.truthy(stealth_admin))
                deadchat_broadcast(I18n.T(" has disconnected."), Dm.text("<b>[][]</b>", mob, mob.get_realname_string()), follow_target: mob, turf_target: get_turf(mob), message_type: DEADCHAT_LOGIN_LOGOUT, admin_only: Dm.truthy(announce_join) ? FALSE : TRUE);
            Waiting.NoSleep(mob.become_uncliented());
        }

        Dm.remove(ref GLOB.clients, this);
        Dm.remove(ref GLOB.directory, ckey);
        if (Dm.truthy(persistent_client))
            persistent_client.set_client(null);
        else
            stack_trace("A client was Del()'d without a persistent_client! This should not be happening.", "code/modules/client/client_procs.dm", 630);

        log_access(Dm.text("Logout: []", key_name(this)));
        GLOB.ahelp_tickets.ClientLogout(this); // tg: :633 (M10x slice G6)
        // tg: :634-636  GLOB.interviews.client_logout(src);
        // GLOB.requests.client_logout(src); SSserver_maint.UpdateHubStatus()
        Dm.Deferred("code/modules/client/client_procs.dm:633", "client/Destroy: the interviews and requests of the player (admin tooling, M10+) and SSserver_maint's hub status (the BYOND hub, not ported)");
        if (Dm.truthy(credits))
            QDEL_LIST(credits);
        if (Dm.truthy(holder))
        {
            holder.owner = null;
            Dm.remove(ref GLOB.admins, this);
            // tg: :642  handle_admin_logout(): the logout notice and the "no admins online" relay to TGS (not ported)
            Dm.Deferred("code/modules/client/client_procs.dm:642", "client/Destroy: handle_admin_logout (the admin logout notice and the TGS relay, not ported)");
        }

        QDEL_LIST_ASSOC_VAL(char_render_holders);

        SSambience?.remove_ambience_client(this); // tg: :646 (null in a world without tg's MC)
        // tg: :647-648  SSmouse_entered.hovers -= src; SSping.currentrun -= src
        Dm.Deferred("code/modules/client/client_procs.dm:647", "client/Destroy: SSmouse_entered's hovers and SSping's run (mouse hover and ping are not declared; M2, M10+)");
        Dm.remove(ref SSsound_tokens.clients_needing_update, this);
        Dm.remove(ref SSsound_tokens.currentrun, this);
        QDEL_NULL(ref view_size);
        QDEL_NULL(ref @void);
        QDEL_NULL(ref tooltips);
        QDEL_NULL(ref loot_panel);
        QDEL_LIST(parallax_instances);
        eye_parallax = null;
        seen_messages = null;
        Master.UpdateTickRate();
        base.Destroy(force); //Even though we're going to be hard deleted there are still some things that want to know the destroy is happening
        return QDEL_HINT_HARDDEL_NOW;
    }

    // tg: code/modules/client/client_procs.dm:1016
    public virtual async DmTask set_eye(object new_eye = null)
    {
        if (Dm.eq(new_eye, eye))
            return;
        atom old_eye = eye;
        eye = new_eye as atom;

        // Draws to the default map
        // tg: :1024-1025  eye_parallax = create_parallax(""); eye_parallax.set_perspective(eye)
        Dm.Deferred("code/modules/client/client_procs.dm:1024", "set_eye: the parallax of the default map follows the eye (/atom/movable/screen/parallax_home; parallax is 2D presentation, not drawn by the 3D client)");
        if (Dm.truthy(_listen_lookup?[COMSIG_CLIENT_SET_EYE]))
            _SendSignal(COMSIG_CLIENT_SET_EYE, Dm.list(this, old_eye, new_eye));
    }

    /**
     * Updates the keybinds for special keys
     *
     * Handles adding macros for the keys that need it
     * And adding movement keys to the clients movement_keys list
     * At the time of writing this, communication(OOC, Say, IC, ASAY) require macros
     * Arguments:
     * * direct_prefs - the preference we're going to get keybinds from
     */
    // tg: code/modules/client/client_procs.dm:1036
    public virtual void update_special_keybinds(datum_preferences direct_prefs = null)
    {
        datum_preferences D = (prefs is var or__1 && Dm.truthy(or__1) ? or__1 : direct_prefs);
        // DEVIATION(DV14): the preferences are not translated (M9: client/New's
        // prefs is a deferred leaf), so D is null and tg would return here with no movement keys; until they are, the
        // keybindings are tg's defaults, GLOB.default_hotkeys as init_keybindings builds them from the keybinding datums
        // (_globalvars/lists/keybindings.dm:2-20), here only the four movement ones (datums/keybinding/movement.dm:5-31; the
        // z-level keys bind no movement_keys entry).
        // M10 slice F: the preferences exist, but their key_bindings stay empty (DV161: the port's client binds its own keys, so tg's
        // deep_copy_list(GLOB.default_hotkeys) is not made); an empty list reads as these defaults.
        DmList key_bindings = D != null && Dm.truthy(length(D.key_bindings)) ? D.key_bindings : Dm.list(
            Dm.kv("North", Dm.list("W", "North")),
            Dm.kv("South", Dm.list("S", "South")),
            Dm.kv("West", Dm.list("A", "West")),
            Dm.kv("East", Dm.list("D", "East")));
        if (!Dm.truthy(key_bindings))
            return;
        movement_keys = Dm.list();
        foreach (object kb_name in Dm.each(key_bindings))
        {
            foreach (object key__1 in Dm.each(key_bindings[kb_name]))
            {
                object sw__1 = kb_name;
                if (Dm.eq(sw__1, "North"))
                    movement_keys[key__1] = NORTH;
                else if (Dm.eq(sw__1, "East"))
                    movement_keys[key__1] = EAST;
                else if (Dm.eq(sw__1, "West"))
                    movement_keys[key__1] = WEST;
                else if (Dm.eq(sw__1, "South"))
                    movement_keys[key__1] = SOUTH;
                else if (Dm.eq(sw__1, ADMIN_CHANNEL))
                {
                    // GAP(lead): tgui_say_create_open_command and winset (the UI seam) come with tgui say (M9).
                    Dm.NotTranslated("code/modules/client/client_procs.dm:1052", "update_special_keybinds: the admin say channel macro (the admin tools, M10+)");
                }
            }
        }
        calculate_move_dir();
    }

    // tg: code/modules/client/client_procs.dm:1090
    public virtual object get_parallax(object map = null)
    {
        foreach (atom_movable_screen_parallax_home instance in Dm.each_cast<atom_movable_screen_parallax_home>(parallax_instances))
        {
            if (Dm.eq(Dm.get_var(instance, "submap"), map)) // GAP(lead, _onclick/hud/parallax/parallax.cs): the var submap of /atom/movable/screen/parallax_home is not declared; read by name
                return instance;
        }
        return null;
    }

    /// Creates a new parallax holder if one does not already exist on the passed in map
    // tg: code/modules/client/client_procs.dm:1096
    public virtual object create_parallax(object map = null)
    {
        atom_movable_screen_parallax_home existing = get_parallax(map) as atom_movable_screen_parallax_home;
        if (Dm.truthy(existing))
            return existing;
        Dm.NotTranslated("code/modules/client/client_procs.dm:1100", "create_parallax: new /atom/movable/screen/parallax_home (parallax screens are 2D presentation; set_eye defers them)");
        return null;
    }

    /// compiles a full list of verbs and sends it to the browser
    ///Gives someone hearted status for OOC, from behavior commendations
    // tg: code/modules/client/client_procs.dm:1121  (gaps R5)
    public virtual void adjust_heart(float? duration = null)
    {
        duration ??= 24 * HOURS;
        float new_duration = world.realtime + duration.Value;
        if (Dm.num(prefs.hearted_until) > new_duration)
            return;
        to_chat(this, span_nicegreen(I18n.T("Someone awarded you a heart!")));
        prefs.hearted_until = new_duration;
        prefs.hearted = TRUE;
        prefs.save_preferences();
    }

    // tg: code/modules/client/client_procs.dm:1131
    public virtual void init_verbs()
    {
        // tg: :1132-1150  if(IsAdminAdvancedProcCall()) return; the verbs of the client, its mob and the mob's contents, by
        // category, into panel_tabs and the stat panel (stat_panel.send_message("init_verbs", ...))
        Dm.Deferred("code/modules/client/client_procs.dm:1132", "init_verbs: the verb list of the stat panel (verbs and the stat panel, M10+)");
    }

    // tg: code/modules/client/client_procs.dm:1180
    public virtual void set_right_click_menu_mode(object shift_only = null)
    {
        // tg: :1181-1188  if(shift_only) { winset(src, SKIN_MAPWINDOW_MAP, "right-click=true"); winset(src,
        // SKIN_DEFAULT_SHIFTUP, "is-disabled=false"); winset(src, SKIN_DEFAULT_SHIFT, "is-disabled=false") } else { the
        // same with right-click=false and is-disabled=true }
        Dm.Deferred("code/modules/client/client_procs.dm:1181", "set_right_click_menu_mode: winset of the map's right-click mode and the Shift macros (the BYOND skin, not ported; the 3D client has its own input)");
    }

    // tg: code/modules/client/client_procs.dm:1190
    public virtual void update_ambience_pref(object value = null)
    {
        if (SSambience == null) // a world without tg's MC (the tests' rooms) has no SSambience to listen to
            return;
        if (Dm.truthy(value))
        {
            if (Dm.num(SSambience.ambience_listening_clients[this]) > world.time)
                return; // If already properly set we don't want to reset the timer.
            SSambience.ambience_listening_clients[this] = world.time + 10 * SECONDS; //Just wait 10 seconds before the next one aight mate? cheers.
        }
        else
            SSambience.remove_ambience_client(this);
    }

    // tg: code/modules/client/client_procs.dm:1218  (syndicate package A3, D316: the antagonists' account age)
    public virtual object get_remaining_days(object days_needed = null)
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_age_restriction_for_jobs)))) // CONFIG_GET(flag/use_age_restriction_for_jobs)
            return 0;

        if (!Dm.isnum(player_age) || player_age < 0)
            return 0;

        if (!Dm.isnum(days_needed))
            return 0;

        return Dm.max(0, Dm.num(days_needed) - player_age);
    }

    /// Clears the client's screen, aside from ones that opt out
    // tg: code/modules/client/client_procs.dm:1257
    public virtual void clear_screen()
    {
        foreach (object @object in Dm.each(screen))
        {
            if (@object is atom_movable_screen)
            {
                atom_movable_screen screen_object = @object as atom_movable_screen;
                // GAP(lead): /atom/movable/screen's clear_with_screen (_onclick/hud/screen_objects.dm) is not declared;
                // read by name (no screen object reaches a client in M1).
                if (!Dm.truthy(Dm.get_var(screen_object, "clear_with_screen")))
                    continue;
            }

            Dm.remove(screen, @object);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/client/client_procs.dm:174  /client/proc/is_localhost(): var/static/localhost_addresses (rules 10.6)
    internal DmList client__is_localhost__localhost_addresses = Dm.list("127.0.0.1", "::1", null);
}
