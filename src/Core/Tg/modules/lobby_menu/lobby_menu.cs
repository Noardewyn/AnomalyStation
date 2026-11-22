// Translated from tgstation (snapshot 1d99bd7f): code/modules/lobby_menu/lobby_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F: the whole file but the admin's transparency verb (ADMIN_VERB toggle_lobby_transparency: admin verbs, M10+) and the
// polls (the database). Gaps R2: the station traits' sign-up buttons.
// DEVIATION(DV44): tg's page is the tgui-lobby bundle in a BYOND browser control that the skin swaps in for the map; the port's client
// draws the lobby natively from the same messages ("init", "state"), and the skin's swap (winset of the map/lobby selector) is the
// window opening (the "init" message) and closing (TguiOutputKind.Close).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/lobby_menu/lobby_menu.dm:1  GLOBAL_LIST_EMPTY(lobby_menus)
    public DmList lobby_menus;

    // tg: code/modules/lobby_menu/lobby_menu.dm:1  (GLOBAL_MANAGED(lobby_menus, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGloballobby_menus()
    {
        lobby_menus = Dm.list();
        Dm.append(ref gvars_datum_init_order, "lobby_menus");
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:2  GLOBAL_VAR(lobby_background_transparent)
    public object lobby_background_transparent;

    // tg: code/modules/lobby_menu/lobby_menu.dm:2  (GLOBAL_MANAGED(lobby_background_transparent, null), __DEFINES/__globals.dm:5)
    public virtual void InitGloballobby_background_transparent()
    {
        Dm.append(ref gvars_datum_init_order, "lobby_background_transparent");
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:5  GLOBAL_VAR(lobby_title_asset_registered)
    public object lobby_title_asset_registered;

    // tg: code/modules/lobby_menu/lobby_menu.dm:5  (GLOBAL_MANAGED(lobby_title_asset_registered, null), __DEFINES/__globals.dm:5)
    public virtual void InitGloballobby_title_asset_registered()
    {
        Dm.append(ref gvars_datum_init_order, "lobby_title_asset_registered");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/lobby_menu/lobby_menu.dm:4  #define LOBBY_TITLE_ASSET_NAME "lobby_title_screen.png" (file-local, #undef at :402)
    private const string LOBBY_TITLE_ASSET_NAME = "lobby_title_screen.png";

    /// DEVIATION(DV44): the title screen the page shows, as the icon file the client draws (SStitle's default, title.dm:33: SStitle and
    /// its config's title screens are not translated; the asset transport is the client's).
    internal const string LobbyTitleIcon = "icons/runtime/default_title.dmi";

    // tg: code/modules/lobby_menu/lobby_menu.dm:7
    public static void register_lobby_title_asset()
    {
        if (Dm.truthy(GLOB.lobby_title_asset_registered))
            return;
        GLOB.lobby_title_asset_registered = TRUE;
        // UNTIL(SStitle.icon); SSassets.transport.register_asset(LOBBY_TITLE_ASSET_NAME, SStitle.icon): DEVIATION(DV44), the client
        // has the title icon (LobbyTitleIcon)
    }
}

public partial class client : datum
{
    // tg: code/modules/lobby_menu/lobby_menu.dm:16
    public virtual void initialize_lobby_menu()
    {
        // set waitfor = FALSE: nothing below sleeps once the title asset is the client's (DV44)
        register_lobby_title_asset();
        // Wait for the title screen asset to be available: UNTIL(SSassets.transport.get_asset_url(LOBBY_TITLE_ASSET_NAME)), DV44
        lobby_menu = Dm.@new<datum_lobby_menu>(this);
    }
}

// tg: code/modules/lobby_menu/lobby_menu.dm:33
[DmPath("/datum/lobby_menu")]
public partial class datum_lobby_menu : datum
{
    // tg: code/modules/lobby_menu/lobby_menu.dm:39
    public override async DmTask New(params object[] __extra)
    {
        client client = Dm.arg<client>(__extra, 0);
        this.client = client;
        window = Dm.@new<datum_tgui_window>(client, "lobby_menu");
        window.is_browser = TRUE;

        object transparent = Dm.truthy(GLOB.lobby_background_transparent) && !Dm.truthy(client.prefs?.read_preference(typeof(datum_preference_toggle_disable_lobby_transparency))) ? TRUE : FALSE;
        create_browser(transparent);
        initialize_browser();
        window.subscribe(this, nameof(on_message));

        RegisterSignal(client, COMSIG_QDELETING, nameof(on_client_qdel));
        RegisterSignal(client, COMSIG_CLIENT_MOB_LOGIN, nameof(on_client_mob_login));
        RegisterSignal(SSticker, COMSIG_TICKER_ENTER_PREGAME, nameof(on_ticker_pregame));
        RegisterSignal(SSticker, COMSIG_TICKER_ENTER_SETTING_UP, nameof(on_ticker_setting_up));
        RegisterSignal(SSticker, COMSIG_TICKER_ERROR_SETTING_UP, nameof(on_ticker_error_setting_up));
        RegisterSignal(SSticker, COMSIG_TICKER_ROUND_STARTING, nameof(on_round_start));
        RegisterSignals(SSdcs, Dm.list(COMSIG_GLOB_LOBBY_TRAIT_ADDED, COMSIG_GLOB_LOBBY_TRAIT_REMOVED), nameof(on_traits_changed));

        Dm.append(ref GLOB.lobby_menus, this);
        update_visibility();
        // DEVIATION(DV44): an init opens the client's lobby window, so a client that logs back into its body (tg's skin keeps the map
        // screen) gets none
        if (client?.mob is mob_dead_new_player)
            send_init();
    }

    /// Creates the lobby_menu browser element in the appropriate parent window.
    /// When transparent, it lives in map_screen overlaying the map.
    /// When opaque, it lives in lobby_screen (swapped via the CHILD selector).
    // tg: code/modules/lobby_menu/lobby_menu.dm:63
    public virtual void create_browser(object transparent = null)
    {
        // winset(client, "lobby_menu", ...): the skin's browser element. DEVIATION(DV44): the client draws the lobby itself.
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:87
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.lobby_menus, this);
        STOP_PROCESSING(SSlobby_menu, this);
        window?.unsubscribe(this);
        window = null;
        client = null;
        return base.Destroy(force);
    }

    /// Loads the bundle, sends assets, and pushes initial state into the browser.
    // tg: code/modules/lobby_menu/lobby_menu.dm:96
    public virtual void initialize_browser()
    {
        window.initialize(
            strict_mode: TRUE,
            inline_css: Dm.file("tgui/public/tgui-lobby.bundle.css"),
            inline_js: Dm.file("tgui/public/tgui-lobby.bundle.js"));
        // window.send_asset(the lobby's font, sounds and icons); SSassets.transport.send_assets(client, LOBBY_TITLE_ASSET_NAME):
        // DEVIATION(DV44), the client has them (tg's icons/hud/lobby, sound/ui, the fonts)
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:118
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        send_update(Dm.list(
            Dm.kv("countdown", get_countdown_text()),
            Dm.kv("playerCount", length(GLOB.clients)),
            Dm.kv("readyCount", SSticker.totalPlayersReady),
            Dm.kv("adminReadyCount", SSticker.total_admins_ready),
            Dm.kv("adminCount", length(GLOB.admins)),
            Dm.kv("shiftTime", SSticker.round_start_time == 0 ? "Pre-Game" : round_timestamp())));
        return null;
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:128
    public virtual void on_client_qdel()
    {
        qdel(this);
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:132
    public virtual void on_client_mob_login()
    {
        update_visibility();
    }

    /// Swaps between the lobby screen and the map screen based on whether the client's mob is a new_player
    // tg: code/modules/lobby_menu/lobby_menu.dm:137
    public virtual void update_visibility()
    {
        bool should_show = client?.mob is mob_dead_new_player && !Dm.truthy(client.interviewee);
        // winset(client, SKIN_MAP_LOBBY_SELECTOR, ...) (and "lobby_menu" is-visible when transparent): DEVIATION(DV44), the client
        // shows the lobby while the window is open; hiding it closes the window, and send_init below opens it again.
        if (should_show)
        {
            START_PROCESSING(SSlobby_menu, this);
            send_init();
        }
        else
        {
            STOP_PROCESSING(SSlobby_menu, this);
            client?.dm_tgui_write(new TguiOutput(Dm.str(window.id), TguiOutputKind.Close, null));
        }
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:152
    public virtual void on_ticker_pregame()
    {
        send_update(Dm.list(
            Dm.kv("gamePhase", "pregame"),
            Dm.kv("canObserve", TRUE)));
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:159
    public virtual void on_ticker_setting_up()
    {
        send_update(Dm.list(
            Dm.kv("gamePhase", "setting_up"),
            Dm.kv("canReady", FALSE),
            Dm.kv("canJoin", TRUE)));
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:167
    public virtual void on_ticker_error_setting_up()
    {
        send_update(Dm.list(
            Dm.kv("gamePhase", "pregame"),
            Dm.kv("canReady", TRUE),
            Dm.kv("canJoin", FALSE)));
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:175
    public virtual void on_round_start()
    {
        send_update(Dm.list(
            Dm.kv("gamePhase", "playing"),
            Dm.kv("canReady", FALSE),
            Dm.kv("canJoin", TRUE)));
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:183
    public virtual void on_traits_changed()
    {
        send_update(Dm.list(Dm.kv("stationTraits", get_station_traits())));
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:187
    public virtual object get_game_phase()
    {
        if (Dm.eq(SSticker.current_state, GAME_STATE_STARTUP))
            return "startup";
        if (Dm.eq(SSticker.current_state, GAME_STATE_PREGAME))
            return "pregame";
        if (Dm.eq(SSticker.current_state, GAME_STATE_SETTING_UP))
            return "setting_up";
        if (Dm.eq(SSticker.current_state, GAME_STATE_PLAYING))
            return "playing";
        return "postgame";
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:199
    public virtual object get_countdown_text()
    {
        object time_remaining = SSticker.GetTimeLeft();
        if (Dm.gt(time_remaining, 0))
            return Dm.text("[]s", round(Dm.num(time_remaining) / 10));
        if (Dm.eq(time_remaining, -10))
            return "DELAYED";
        return "SOON";
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:207
    public virtual DmList get_station_traits()
    {
        DmList result = Dm.list();
        mob_dead_new_player player = client?.mob as mob_dead_new_player;
        foreach (datum_station_trait trait in Dm.each_cast<datum_station_trait>(GLOB.lobby_station_traits))
        {
            if (!Dm.truthy(trait.can_display_lobby_button(client)))
                continue;
            Dm.append(ref result, Dm.list(Dm.list(
                Dm.kv("ref", REF(trait)),
                Dm.kv("name", trait.name),
                Dm.kv("description", trait.get_lobby_description()),
                Dm.kv("iconState", trait.get_lobby_icon_state(player)),
                Dm.kv("overlays", trait.get_lobby_overlay_states(player)))));
        }
        return result;
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:222
    public virtual void send_init()
    {
        mob_dead_new_player player = client?.mob as mob_dead_new_player;
        object game_phase = get_game_phase();

        window.send_message("init", Dm.list(
            Dm.kv("titleImageUrl", LobbyTitleIcon), // DEVIATION(DV44): SSassets.transport.get_asset_url(LOBBY_TITLE_ASSET_NAME)
            Dm.kv("gamePhase", game_phase),
            Dm.kv("isReady", player != null && Dm.eq(player.ready, PLAYER_READY_TO_PLAY) ? TRUE : FALSE),
            Dm.kv("canReady", Dm.eq(game_phase, "pregame") || Dm.eq(game_phase, "startup") ? TRUE : FALSE),
            Dm.kv("canJoin", Dm.eq(game_phase, "setting_up") || Dm.eq(game_phase, "playing") ? TRUE : FALSE),
            Dm.kv("canObserve", Dm.num(SSticker.current_state) > GAME_STATE_STARTUP ? TRUE : FALSE),
            // GAP(lead): SSearly_assets (the asset transport) is not declared, so the atoms' initialization alone
            Dm.kv("assetsReady", Dm.eq(SSatoms.initialized, INITIALIZATION_INNEW_REGULAR) ? TRUE : FALSE),
            Dm.kv("countdown", get_countdown_text()),
            Dm.kv("playerCount", length(GLOB.clients)),
            Dm.kv("readyCount", SSticker.totalPlayersReady),
            Dm.kv("adminReadyCount", SSticker.total_admins_ready),
            Dm.kv("adminCount", length(GLOB.admins)),
            Dm.kv("mapName", SSmapping.current_map?.map_name ?? "Loading..."),
            Dm.kv("shiftTime", SSticker.round_start_time == 0 ? "Pre-Game" : round_timestamp()),
            Dm.kv("isAdmin", client?.holder != null ? TRUE : FALSE),
            Dm.kv("isLocalhost", client?.is_localhost()),
            Dm.kv("stationTraits", get_station_traits()),
            Dm.kv("hasNewPoll", FALSE),
            Dm.kv("canPoll", FALSE), // !is_guest_key(client?.key) && SSdbcore.Connect(): the polls are the database's (M10+)
            Dm.kv("overflowJob", null),
            Dm.kv("transparent", GLOB.lobby_background_transparent)));

        // check_new_polls(): the database's polls (M10+)

        // the signals of SSearly_assets and SSatoms' post-initialize (:252-259): DEVIATION(DV44), the client's assets are ready when
        // it has joined, so assetsReady is the atoms' initialization only
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:300
    public virtual void send_update(DmList data = null)
    {
        window.send_message("state", data);
    }

    // tg: code/modules/lobby_menu/lobby_menu.dm:303
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
        mob_dead_new_player player = client?.mob as mob_dead_new_player;
        if (player == null)
            return TRUE;
        if (Dm.truthy(client.interviewee))
            return TRUE;

        if (Dm.eq(action, "ready_toggle"))
        {
            if (Dm.eq(player.ready, PLAYER_NOT_READY))
            {
                player.auto_deadmin_on_ready_or_latejoin();
                player.ready = PLAYER_READY_TO_PLAY;
            }
            else
                player.ready = PLAYER_NOT_READY;
            send_update(Dm.list(Dm.kv("isReady", Dm.eq(player.ready, PLAYER_READY_TO_PLAY) ? TRUE : FALSE)));
        }
        else if (Dm.eq(action, "join"))
        {
            if (!Dm.truthy(SSticker?.IsRoundInProgress()))
            {
                to_chat(player, span_boldwarning("The round is either not ready, or has already finished..."));
                return TRUE;
            }

            object relevant_cap;
            object hard_popcap = config.Get(typeof(datum_config_entry_number_hard_popcap));
            object extreme_popcap = config.Get(typeof(datum_config_entry_number_extreme_popcap));
            if (Dm.truthy(hard_popcap) && Dm.truthy(extreme_popcap))
                relevant_cap = Dm.min(Dm.num(hard_popcap), Dm.num(extreme_popcap));
            else
                relevant_cap = Dm.max(Dm.num(hard_popcap), Dm.num(extreme_popcap));

            if (Dm.truthy(SSticker.queued_players.len) || (Dm.truthy(relevant_cap) && Dm.ge(living_player_count(), relevant_cap) && !Dm.@in(ckey(player.key), GLOB.admin_datums)))
            {
                to_chat(player, span_danger(Dm.str(config.Get(typeof(datum_config_entry_string_hard_popcap_message)))));
                float queue_position = SSticker.queued_players.Find(player);
                if (queue_position == 1)
                    to_chat(player, span_notice("You are next in line to join the game. You will be notified when a slot opens up."));
                else if (Dm.truthy(queue_position))
                    to_chat(player, span_notice(Dm.text("There are [] players in front of you in the queue to join the game.", queue_position - 1)));
                else
                {
                    Dm.append(ref SSticker.queued_players, player);
                    to_chat(player, span_notice(Dm.text("You have been added to the queue to join the game. Your position in queue is [].", SSticker.queued_players.len)));
                }
                return TRUE;
            }

            player.auto_deadmin_on_ready_or_latejoin();

            if (Dm.truthy(payload_list["ctrlClick"]))
            {
                to_chat(player, span_warning("Opening emergency fallback late join menu! If THIS doesn't show, ahelp immediately!"));
                await GLOB.latejoin_menu.fallback_ui(player);
            }
            else
                await GLOB.latejoin_menu.ui_interact(player);
        }
        else if (Dm.eq(action, "observe"))
            await player.make_me_an_observer();
        else if (Dm.eq(action, "character_setup"))
        {
            datum_preferences prefs = client.prefs;
            prefs.current_window = PREFERENCE_TAB_CHARACTER_PREFERENCES;
            await prefs.update_static_data(player);
            await prefs.ui_interact(player);
        }
        else if (Dm.eq(action, "settings"))
        {
            datum_preferences prefs = client.prefs;
            prefs.current_window = PREFERENCE_TAB_GAME_PREFERENCES;
            await prefs.update_static_data(player);
            await prefs.ui_interact(player);
        }
        else if (Dm.eq(action, "changelog"))
            client.changelog();
        else if (Dm.eq(action, "crew_manifest"))
            await player.ViewManifest();
        else if (Dm.eq(action, "poll"))
            Dm.NotTranslated("code/modules/lobby_menu/lobby_menu.dm:379", "on_message: handle_player_polling, the polls (the database)");
        else if (Dm.eq(action, "start_now"))
        {
            if (!Dm.truthy(client.is_localhost()) || !Dm.truthy(check_rights_for(client, R_SERVER)))
                return TRUE;
            SSticker.start_immediately = TRUE;
            if (Dm.eq(SSticker.current_state, GAME_STATE_STARTUP))
                to_chat(player, span_admin("The server is still setting up, but the round will be started as soon as possible."));
        }
        else if (Dm.eq(action, "sign_up"))
        {
            object trait_ref = payload_list?["ref"];
            if (!Dm.truthy(trait_ref))
                return TRUE;
            var trait = locate_ref(trait_ref) as datum_station_trait;
            if (!istype(trait, typeof(datum_station_trait)) || !Dm.@in(trait, GLOB.lobby_station_traits))
                return TRUE;
            object feedback = await trait.on_lobby_button_click(player);
            send_update(Dm.list(
                Dm.kv("stationTraits", get_station_traits()),
                Dm.kv("traitFeedback", feedback)));
        }

        return TRUE;
    }
}
