// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/login.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer). The HUD, parallax, screen, verb, weather and stat panel leaves that Login
// reaches are Dm.Deferred (docs/ai/specs/not-translated.md): the 3D client draws its own interface.

public partial class mob : atom_movable
{
    /**
     * Run when a client is put in this mob or reconnets to byond and their client was on this mob.
     * Anything that sleeps can result in the client reference being dropped, due to byond using that sleep to handle a client disconnect.
     * You can save a lot of headache if you make Login use SHOULD_NOT_SLEEP, but that would require quite a bit of refactoring how Login code works.
     *
     * Things it does:
     * * Adds player to player_list
     * * sets lastKnownIP
     * * sets computer_id
     * * logs the login
     * * tells the world to update its status (for player count)
     * * create mob huds for the mob if needed
     * * reset next_move to 1
     * * Set statobj to our mob
     * * NOT the parent call. The only unique thing it does is a very obtuse move op, see the comment lower down
     * * if the client exists set the perspective to the mob loc
     * * call on_log on the loc (sigh)
     * * reload the huds for the mob
     * * reload all full screen huds attached to this mob
     * * load any global alternate apperances
     * * sync the mind datum via sync_mind()
     * * call any client login callbacks that exist
     * * grant any actions the mob has to the client
     * * calls [auto_deadmin_on_login](mob.html#proc/auto_deadmin_on_login)
     * * send signal COMSIG_MOB_CLIENT_LOGIN
     * * attaches the ash listener element so clients can hear weather
     * client can be deleted mid-execution of this proc, chiefly on parent calls, with lag
     */
    // tg: code/modules/mob/login.dm:29
    public virtual async DmTask<object> Login()
    {
        if (!Dm.truthy(client))
            return FALSE;

        canon_client = client;
        client.persistent_client.set_mob(this);

        add_to_player_list();
        lastKnownIP = client.address;
        computer_id = client.computer_id;
        log_access(Dm.text("Mob Login: [] was assigned to a [] ([])", key_name(this), type, tag));
        world.update_status();
        client.clear_screen(); //remove hud items just in case
        client.images = Dm.list();
        client.set_right_click_menu_mode(shift_to_open_context_menu);
        // Draws to the default map
        // tg: :45-46  client.eye_parallax = client.create_parallax(""); client.eye_parallax.set_perspective(src)
        Dm.Deferred("code/modules/mob/login.dm:45", "Login: the parallax of the default map (/atom/movable/screen/parallax_home; 2D presentation, not drawn by the 3D client)");

        if (!Dm.truthy(hud_used))
        {
            await create_mob_hud(); // creating a hud will add it to the client's screen, which can process a disconnect
            if (!Dm.truthy(client))
                return FALSE;
        }

        if (Dm.truthy(hud_used))
        {
            await hud_used.show_hud(hud_used.hud_version); // see above, this can process a disconnect
            if (!Dm.truthy(client))
                return FALSE;
            // tg: :58
            await hud_used.update_ui_style(ui_style2icon(client.prefs?.read_preference(typeof(datum_preference_choiced_ui_style))));
        }

        next_move = 1;

        client.statobj = this;

        // DO NOT CALL PARENT HERE
        // BYOND's internal implementation of login does two things
        // 1: Set statobj to the mob being logged into (We got this covered)
        // 2: And I quote "If the mob has no location, place it near (1,1,1) if possible"
        // See, near is doing an agressive amount of legwork there
        // What it actually does is takes the area that (1,1,1) is in, and loops through all those turfs
        // If you successfully move into one, it stops
        // Because we want Move() to mean standard movements rather then just what byond treats it as (ALL moves)
        // We don't allow moves from nullspace -> somewhere. This means the loop has to iterate all the turfs in (1,1,1)'s area
        // For us, (1,1,1) is a space tile. This means roughly 200,000! calls to Move()
        // You do not want this

        if (!Dm.truthy(client))
            return FALSE;

        // tg passes client, which enable_client_mobs_in_contents does not declare: dropped (rules 7.3)
        enable_client_mobs_in_contents();

        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_LOGIN]))
            _SendSignal(COMSIG_MOB_LOGIN, Dm.list(this));

        if (key != client.key)
            key = client.key;
        await reset_perspective(loc);

        if (Dm.truthy(loc))
            loc.on_log(TRUE);

        reload_huds();

        reload_fullscreen(); // Reload any fullscreen overlays this mob has.

        // tg: :94  add_click_catcher()
        Dm.Deferred("code/modules/mob/login.dm:94", "Login: add_click_catcher() (the screen's click catcher; the 3D client clicks by ray, M2)");

        await sync_mind();

        //Reload alternate appearances
        foreach (datum_atom_hud_alternate_appearance alt_hud in Dm.each_cast<datum_atom_hud_alternate_appearance>(GLOB.active_alternate_appearances))
        {
            alt_hud.check_hud(this);
        }

        // tg: :102  update_client_colour()
        update_client_colour();
        update_mouse_pointer();
        // tg: :104
        update_ambience_area(get_area(this) as area);

        if (HAS_TRAIT(this, TRAIT_DEAF))
        {
            stop_sound_channel(CHANNEL_AMBIENCE);
        }

        if (Dm.truthy(client))
        {
            // tg: :111  client.view_size?.resetToDefault() // Resets the client.view in case it was changed.
            client.view_size?.resetToDefault();

            foreach (object A in Dm.each(persistent_client.player_actions))
            {
                // GAP(lead): /datum/action and its Grant (datums/actions/action.dm) are not declared (actions, M2).
                Dm.NotTranslated("code/modules/mob/login.dm:114", "Login: A.Grant(src) for the player's actions (actions, M2)");
            }

            foreach (datum_callback CB in Dm.each_cast<datum_callback>(persistent_client.post_login_callbacks))
                await CB.Invoke();

            log_played_names(
                client.ckey,
                Dm.list(
                    Dm.kv(Dm.text("[]", name), tag),
                    Dm.kv(Dm.text("[]", real_name), tag)));
            auto_deadmin_on_login();

            //Check if they should have a stat panel, after they deadmined.
            // tg: :129  client.set_stat_panel()
            Dm.Deferred("code/modules/mob/login.dm:129", "Login: client.set_stat_panel() (the stat panel, M10+)");

            //Update the chat panel's job/character info for conditional highlights.
            // tg: :132  client.tgui_panel?.send_player_info()
            Dm.Deferred("code/modules/mob/login.dm:132", "Login: client.tgui_panel?.send_player_info() (the tgui chat panel, M10+)");
        }

        log_message(Dm.text("Client [] has taken ownership of mob []([])", key_name(this), this, this.type), LOG_OWNERSHIP);
        log_mob_tag(Dm.text("TAG: [] NEW OWNER: []", tag, key_name(this)));
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_CLIENT_LOGIN]))
            _SendSignal(COMSIG_MOB_CLIENT_LOGIN, Dm.list(this, client));
        if (Dm.truthy(client._listen_lookup?[COMSIG_CLIENT_MOB_LOGIN]))
            client._SendSignal(COMSIG_CLIENT_MOB_LOGIN, Dm.list(client, this));
        client.init_verbs();

        // tg: :140-143  the storms' sound listeners (gaps E2a)
        await AddElement(typeof(datum_element_weather_listener), typeof(datum_weather_particle_ash_storm), ZTRAIT_ASHSTORM, GLOB.ash_storm_sounds);
        await AddElement(typeof(datum_element_weather_listener), typeof(datum_weather_particle_rain_storm), ZTRAIT_RAINSTORM, GLOB.rain_storm_sounds);
        await AddElement(typeof(datum_element_weather_listener), typeof(datum_weather_sand_storm), ZTRAIT_SANDSTORM, GLOB.sand_storm_sounds);
        await AddElement(typeof(datum_element_weather_listener), typeof(datum_weather_snow_storm), ZTRAIT_SNOWSTORM, GLOB.snowstorm_sounds);

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_MOB_LOGGED_IN, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_MOB_LOGGED_IN]))
            SSdcs._SendSignal(COMSIG_GLOB_MOB_LOGGED_IN, Dm.list(SSdcs, this));

        if (Dm.truthy(SSticker.IsPostgame()))
            client.screen.Add(SSticker.reboot_hud);

        return TRUE;
    }

    /**
     * Checks if the attached client is an admin and may deadmin them
     *
     * Configs:
     * * flag/auto_deadmin_always
     * * client.prefs?.toggles & DEADMIN_ALWAYS
     * * User is antag and flag/auto_deadmin_antagonists or client.prefs?.toggles & DEADMIN_ANTAGONIST
     * * or if their job demands a deadminning SSjob.handle_auto_deadmin_roles()
     *
     * Called from [login](mob.html#proc/Login)
     */
    // tg: code/modules/mob/login.dm:164
    public virtual object auto_deadmin_on_login() //return true if they're not an admin at the end.
    {
        if (!Dm.truthy(client?.holder))
            return TRUE;
        int toggles = (int)Dm.num(client.prefs?.toggles);
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_always))) || (toggles & DEADMIN_ALWAYS) != 0)
            return client.holder.auto_deadmin();
        if (Dm.truthy(mind?.has_antag_datum(typeof(datum_antagonist))) && (Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_antagonists))) || (toggles & DEADMIN_ANTAGONIST) != 0))
            return client.holder.auto_deadmin();
        if (Dm.truthy(job))
            return SSjob.handle_auto_deadmin_roles(client, job);
        return null;
    }
}
