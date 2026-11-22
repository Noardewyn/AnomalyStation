// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/client_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (it holds only the /client var block), translated by the M1 contracts step; owner in M1: P1.

/**
 * Client datum
 *
 * A datum that is created whenever a user joins a BYOND world, one will exist for every active connected
 * player
 *
 * when they first connect, this client object is created and [/client/New] is called
 *
 * When they disconnect, this client object is deleted and [/client/Del] is called
 *
 * All client topic calls go through [/client/Topic] first, so a lot of our specialised
 * topic handling starts here
 */
// tg: code/modules/client/client_defines.dm:14
// tg: code/modules/client/client_defines.dm:36 parent_type = /datum (the C# base class, rules 2.3: tg's comment at :16-35
// explains why tg re-parents the builtin /client)
// The [DmPath] of a BYOND builtin type and its builtin vars are on its engine part, Builtin/client.builtin.cs.
public partial class client : datum
{
    // tg's overrides of BYOND's /client defaults (rules 10.2, "Builtin types"), in DM declaration order.
    private partial void dm_tg_vars()
    {
        // hides the byond verb panel as we use our own custom version (tg doc comment)
        // tg: code/modules/client/client_defines.dm:41
        show_verb_panel = FALSE;
        // comment out the line below when debugging locally to enable the options & messages menu
        // tg: code/modules/client/client_defines.dm:82
        control_freak = 1;
        // tg: code/modules/client/client_defines.dm:100
        preload_rsc = PRELOAD_RSC;
    }

        ////////////////
        //ADMIN THINGS//
        ////////////////
    ///Contains admin info. Null if client is not an admin.
    // tg: code/modules/client/client_defines.dm:43
    public datum_admins holder = null;
    ///Needs to implement InterceptClickOn(user,params,atom) proc
    // tg: code/modules/client/client_defines.dm:45
    public datum click_intercept = null;
    ///Time when the click was intercepted
    // tg: code/modules/client/client_defines.dm:47
    public float click_intercept_time = 0;

    ///Used to cache this client's bans to save on DB queries
    // tg: code/modules/client/client_defines.dm:50
    public object ban_cache = null;
    ///If we are currently building this client's ban cache, this var stores the timeofday we started at
    // tg: code/modules/client/client_defines.dm:52
    public float ban_cache_start = 0;
    ///Contains the last message sent by this client - used to protect against copy-paste spamming.
    // tg: code/modules/client/client_defines.dm:54
    public string last_message = "";
    ///contins a number of how many times a message identical to last_message was sent.
    // tg: code/modules/client/client_defines.dm:56
    public float last_message_count = 0;
    ///How many messages sent in the last 10 seconds
    // tg: code/modules/client/client_defines.dm:58
    public float total_message_count = 0;
    ///Next tick to reset the total message counter
    // tg: code/modules/client/client_defines.dm:60
    public float total_count_reset = 0;
    ///Internal counter for clients sending external (IRC/Discord) relay messages via ahelp to prevent spamming. Set to a number every time an admin reply is sent, decremented for every client send.
    // tg: code/modules/client/client_defines.dm:62
    public float externalreplyamount = 0;
    ///Tracks say() usage for ic/dchat while slowmode is enabled
    // tg: code/modules/client/client_defines.dm:64  COOLDOWN_DECLARE(say_slowmode)
    // (__DEFINES/cooldowns.dm: var/say_slowmode = 0)
    public float say_slowmode = 0;
    /// The last urgent ahelp that this player sent
    // tg: code/modules/client/client_defines.dm:66  COOLDOWN_DECLARE(urgent_ahelp_cooldown)
    public float urgent_ahelp_cooldown = 0;

        /////////
        //OTHER//
        /////////
    ///Player preferences datum for the client
    // tg: code/modules/client/client_defines.dm:72
    public datum_preferences prefs = null;
    ///Move delay of controlled mob, any keypresses inside this period will persist until the next proper move
    // tg: code/modules/client/client_defines.dm:74
    public float move_delay = 0;
    ///The visual delay to use for the current client.Move(), mostly used for making a client based move look like it came from some other slower source
    // tg: code/modules/client/client_defines.dm:76
    public float visual_delay = 0;

        ////////////
        //SECURITY//
        ////////////
    // (control_freak = 1, client_defines.dm:82: an override of a builtin var, in dm_tg_vars above)

        ////////////////////////////////////
        //things that require the database//
        ////////////////////////////////////
    ///Used to determine how old the account is - in days.
    // tg: code/modules/client/client_defines.dm:88
    public float player_age = -1;
    ///Date that this account was first seen in the server
    // tg: code/modules/client/client_defines.dm:90
    public object player_join_date = null;
    ///So admins know why it isn't working - Used to determine what other accounts previously logged in from this ip
    // tg: code/modules/client/client_defines.dm:92
    public string related_accounts_ip = "Requires database";
    ///So admins know why it isn't working - Used to determine what other accounts previously logged in from this computer id
    // tg: code/modules/client/client_defines.dm:94
    public string related_accounts_cid = "Requires database";
    ///Date of byond account creation in ISO 8601 format
    // tg: code/modules/client/client_defines.dm:96
    public object account_join_date = null;
    ///Age of byond account in days
    // tg: code/modules/client/client_defines.dm:98
    public float account_age = -1;

    // (preload_rsc = PRELOAD_RSC, client_defines.dm:100: an override of a builtin var, in dm_tg_vars above)

    // tg: code/modules/client/client_defines.dm:102
    public atom_movable_screen_click_catcher @void;

    ///used to make a special mouse cursor, this one for mouse up icon
    // tg: code/modules/client/client_defines.dm:105
    public object mouse_up_icon = null;
    ///used to make a special mouse cursor, this one for mouse up icon
    // tg: code/modules/client/client_defines.dm:107
    public object mouse_down_icon = null;
    ///used to override the mouse cursor so it doesnt get reset
    // tg: code/modules/client/client_defines.dm:109
    public object mouse_override_icon = null;

    ///Used for ip intel checking to identify evaders, disabled because of issues with traffic
    // tg: code/modules/client/client_defines.dm:112
    public string ip_intel = "Disabled";

    ///datum that controls the displaying and hiding of tooltips
    // tg: code/modules/client/client_defines.dm:115
    public datum_tooltip tooltips;

    ///Last ping of the client
    // tg: code/modules/client/client_defines.dm:118
    public float lastping = 0;
    ///Average ping of the client
    // tg: code/modules/client/client_defines.dm:120
    public float avgping = 0;
    ///world.time they connected
    // tg: code/modules/client/client_defines.dm:122
    public object connection_time;
    ///world.realtime they connected
    // tg: code/modules/client/client_defines.dm:124
    public object connection_realtime;
    ///world.timeofday they connected
    // tg: code/modules/client/client_defines.dm:126
    public object connection_timeofday;

    ///Used for limiting the rate of topic sends by the client to avoid abuse
    // tg: code/modules/client/client_defines.dm:129
    public DmList topiclimiter;
    ///Used for limiting the rate of clicks sends by the client to avoid abuse
    // tg: code/modules/client/client_defines.dm:131
    public DmList clicklimiter;

    ///lazy list of all credit object bound to this client
    // tg: code/modules/client/client_defines.dm:134
    public DmList credits;

    ///these persist between logins/logouts during the same round.
    // tg: code/modules/client/client_defines.dm:137
    public datum_persistent_client persistent_client;

    ///Should only be a key-value list of north/south/east/west = atom/movable/screen.
    // tg: code/modules/client/client_defines.dm:140
    public DmList char_render_holders;

    ///Amount of keydowns in the last keysend checking interval
    // tg: code/modules/client/client_defines.dm:143
    public float client_keysend_amount = 0;
    ///World tick time where client_keysend_amount will reset
    // tg: code/modules/client/client_defines.dm:145
    public float next_keysend_reset = 0;
    ///World tick time where keysend_tripped will reset back to false
    // tg: code/modules/client/client_defines.dm:147
    public float next_keysend_trip_reset = 0;
    ///When set to true, user will be autokicked if they trip the keysends in a second limit again
    // tg: code/modules/client/client_defines.dm:149
    public float keysend_tripped = FALSE;
    ///custom movement keys for this client
    // tg: code/modules/client/client_defines.dm:151 (list(): dm_init_vars below)
    public DmList movement_keys;

    ///Autoclick list of two elements, first being the clicked thing, second being the parameters.
    // tg: code/modules/client/client_defines.dm:154 (var/list/atom/selected_target[2]: dm_init_vars below)
    public DmList selected_target;
    ///Used in MouseDrag to preserve the original mouse click parameters
    // tg: code/modules/client/client_defines.dm:156
    public string mouseParams = "";
    ///Used in MouseDrag to preserve the last mouse-entered location. Weakref
    // tg: code/modules/client/client_defines.dm:158
    public datum_weakref mouse_location_ref = null;
    ///Used in MouseDrag to preserve the last mouse-entered object. Weakref
    // tg: code/modules/client/client_defines.dm:160
    public datum_weakref mouse_object_ref;
    //Middle-mouse-button click dragtime control for aimbot exploit detection.
    // tg: code/modules/client/client_defines.dm:162
    public float middragtime = 0;
    //Middle-mouse-button clicked object control for aimbot exploit detection. Weakref
    // tg: code/modules/client/client_defines.dm:164
    public datum_weakref middle_drag_atom_ref;
    //When we started the currently active drag
    // tg: code/modules/client/client_defines.dm:166
    public float drag_start = 0;
    //The params we were passed at the start of the drag, in list form
    // tg: code/modules/client/client_defines.dm:168
    public DmList drag_details;


    /// Messages currently seen by this client
    // tg: code/modules/client/client_defines.dm:172
    public DmList seen_messages;

    /// datum wrapper for client view
    // tg: code/modules/client/client_defines.dm:175
    public datum_view_data view_size;

    /// our current tab
    // tg: code/modules/client/client_defines.dm:178
    public object stat_tab;

    /// list of all tabs
    // tg: code/modules/client/client_defines.dm:181 (list(): dm_init_vars below)
    public DmList panel_tabs;
    ///A lazy list of atoms we've examined in the last RECENT_EXAMINE_MAX_WINDOW (default 2) seconds, so that we will call [/atom/proc/examine_more] instead of [/atom/proc/examine] on them when examining
    // tg: code/modules/client/client_defines.dm:183
    public DmList recent_examines;

    /// List of all the parallax instances we are currently displaying
    // tg: code/modules/client/client_defines.dm:186 (list(): dm_init_vars below)
    public DmList parallax_instances;
    /// The parallax instance tied to our eye
    // tg: code/modules/client/client_defines.dm:188
    public atom_movable_screen_parallax_home eye_parallax;

    ///Are we locking our movement input?
    // tg: code/modules/client/client_defines.dm:191
    public float movement_locked = FALSE;

    /**
     * Assoc list with all the active maps - when a screen obj is added to
     * a map, it's put in here as well.
     *
     * Format: list(<mapname> = list(/atom/movable/screen))
     */
    // tg: code/modules/client/client_defines.dm:199 (list(): dm_init_vars below)
    public DmList screen_maps;

    // List of all asset filenames sent to this client by the asset cache, along with their assoicated md5s
    // tg: code/modules/client/client_defines.dm:202 (list(): dm_init_vars below)
    public DmList sent_assets;
    /// List of all completed blocking send jobs awaiting acknowledgement by send_asset
    // tg: code/modules/client/client_defines.dm:204 (list(): dm_init_vars below)
    public DmList completed_asset_jobs;
    /// Last asset send job id.
    // tg: code/modules/client/client_defines.dm:206
    public float last_asset_job = 0;
    // tg: code/modules/client/client_defines.dm:207
    public float last_completed_asset_job = 0;

    /// A buffer of currently held keys.
    // tg: code/modules/client/client_defines.dm:210 (list(): dm_init_vars below)
    public DmList keys_held;
    /// A buffer for combinations such of modifiers + keys (ex: CtrlD, AltE, ShiftT). Format: `"key"` -> `"combo"` (ex: `"D"` -> `"CtrlD"`)
    // tg: code/modules/client/client_defines.dm:212 (list(): dm_init_vars below)
    public DmList key_combos_held;
    /// The direction we WANT to move, based off our keybinds
    /// Will be udpated to be the actual direction later on
    // tg: code/modules/client/client_defines.dm:215
    public float intended_direction = NONE;
    /*
    ** These next two vars are to apply movement for keypresses and releases made while move delayed.
    ** Because discarding that input makes the game less responsive.
    */
    /// On next move, add this dir to the move that would otherwise be done
    // tg: code/modules/client/client_defines.dm:221
    public object next_move_dir_add;
    /// On next move, subtract this dir from the move that would otherwise be done
    // tg: code/modules/client/client_defines.dm:223
    public object next_move_dir_sub;

    /// If the client is currently under the restrictions of the interview system
    // tg: code/modules/client/client_defines.dm:226
    public float interviewee = FALSE;

    /// Whether or not this client has standard hotkeys enabled
    // tg: code/modules/client/client_defines.dm:229
    public float hotkeys = TRUE;

    /// Whether or not this client has the combo HUD enabled
    // tg: code/modules/client/client_defines.dm:232
    public float combo_hud_enabled = FALSE;

    /// If this client has been fully initialized or not
    // tg: code/modules/client/client_defines.dm:235
    public float fully_created = FALSE;

    /// Does this client have typing indicators enabled?
    // tg: code/modules/client/client_defines.dm:238
    public float typing_indicators = FALSE;

    /// Loot panel for the client
    // tg: code/modules/client/client_defines.dm:241
    public datum_lootpanel loot_panel;

    // tg: code/modules/client/client_defines.dm:243
    public datum_admin_verb_panel admin_verb_panel;

    ///Which ambient sound this client is currently being provided.
    // tg: code/modules/client/client_defines.dm:246
    public object current_ambient_sound;

    /// The DPI scale of the client. 1 is equivalent to 100% window scaling, 2 will be 200% window scaling
    // tg: code/modules/client/client_defines.dm:249
    public object window_scaling;

    // tg: code/modules/client/client_defines.dm:251
    public datum_tgui_window stat_panel;

    /// OOC colour of the clients messages.
    // tg: code/modules/client/client_defines.dm:254
    public object ooc_colour = null;

    // The object-creating defaults of the var block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/modules/client/client_defines.dm:151
        movement_keys = Dm.list();
        // tg: code/modules/client/client_defines.dm:154
        selected_target = Dm.list_n(2);
        // tg: code/modules/client/client_defines.dm:181
        panel_tabs = Dm.list();
        // tg: code/modules/client/client_defines.dm:186
        parallax_instances = Dm.list();
        // tg: code/modules/client/client_defines.dm:199
        screen_maps = Dm.list();
        // tg: code/modules/client/client_defines.dm:202
        sent_assets = Dm.list();
        // tg: code/modules/client/client_defines.dm:204
        completed_asset_jobs = Dm.list();
        // tg: code/modules/client/client_defines.dm:210
        keys_held = Dm.list();
        // tg: code/modules/client/client_defines.dm:212
        key_combos_held = Dm.list();
        // tg: code/modules/tgui/external.dm:160  /client/var/list/tgui_windows = list() (M5 slice 3)
        tgui_windows = Dm.list();
    }
}
