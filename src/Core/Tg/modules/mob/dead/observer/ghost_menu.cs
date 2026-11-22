// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/ghost_menu.dm, with observer.dm's DNR verb,
// stay_dead, boo and possess (the menu's actions)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole ghost_menu.dm, the ghosts' settings window (tgui's GhostMenu), where a ghost signs up as a pAI.
// DEVIATION(DV390): the extra view distance (a 2D client's wider view, client.view_size, which the port's client has not got) has no
// first-person meaning: the action changes nothing and the client's window leaves the control out. SSlag_switch is not declared
// (as footsteps.cs): no measure is on.

// tg: code/modules/mob/dead/observer/ghost_menu.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/dead/observer/ghost_menu.dm:1  GLOBAL_DATUM_INIT(ghost_menu, /datum/ghost_menu, new)
    public datum_ghost_menu ghost_menu;

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:1  (GLOBAL_MANAGED(ghost_menu, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalghost_menu()
    {
        ghost_menu = Dm.@new<datum_ghost_menu>();
        Dm.append(ref gvars_datum_init_order, "ghost_menu");
    }
}

// tg: code/modules/mob/dead/observer/ghost_menu.dm:3
public partial class datum_ghost_menu : datum
{
    // SSlag_switch.measures[DISABLE_GHOST_ZOOM_TRAY] && !user.client?.holder: no lag switch measure is on (see the file comment)
    private static bool zoom_tray_disabled(mob user) => false;

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:5
    public override object ui_state(mob user = null) => GLOB.observer_state;

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:8
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "GhostMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:14
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        var dead_user = (mob_dead_observer)ui.user;
        switch (Dm.str(action))
        {
            case "DNR":
                dead_user.stay_dead();
                return TRUE;
            case "return_to_body":
                dead_user.reenter_corpse();
                return TRUE;
            case "restore_appearance":
                restore_ghost_appearance(dead_user);
                return TRUE;
            case "change_notification":
            {
                object key = @params["key"];
                if (Dm.truthy(key) && GLOB.poll_ignore[key] is DmList ignoring)
                {
                    // GLOB.poll_ignore[key] ^= list(dead_user.ckey)
                    if (Dm.@in(dead_user.ckey, ignoring))
                        ignoring.Remove(dead_user.ckey);
                    else
                        ignoring.Add(dead_user.ckey);
                }
                return TRUE;
            }
            case "turn_all_on":
                foreach (object key in Dm.each(GLOB.poll_ignore))
                    (GLOB.poll_ignore[key] as DmList)?.Remove(dead_user.ckey);
                return TRUE;
            case "turn_all_off":
                foreach (object key in Dm.each(GLOB.poll_ignore))
                {
                    if (GLOB.poll_ignore[key] is DmList ignoring && !Dm.@in(dead_user.ckey, ignoring))
                        ignoring.Add(dead_user.ckey);
                }
                return TRUE;
            case "signup_pai":
                await SSpai.recruit_window(dead_user);
                return TRUE;
            case "tray_scan":
                tray_view(dead_user);
                return TRUE;
            case "darkness":
            {
                object darkness_type = @params["darkness_level"];
                if (darkness_type == null)
                    return FALSE;
                foreach (object lighting_types in Dm.each(GLOB.ghost_lightings))
                {
                    if (!Dm.eq(darkness_type, lighting_types))
                        continue;
                    //our selected one is the one we already have enabled.
                    if (Dm.eq(dead_user.lighting_cutoff, GLOB.ghost_lightings[darkness_type]))
                        return FALSE;
                    toggle_darkness(dead_user, darkness_type);
                    return TRUE;
                }
                break;
            }
            case "toggle_visibility":
            {
                object to_toggle = @params["toggling"];
                if (to_toggle != null && Dm.num(to_toggle) is var flag && !Dm.@in(flag, Dm.list(GHOST_DATA_HUDS, GHOST_VISION, GHOST_HEALTH, GHOST_CHEM, GHOST_GAS))) // ALL_GHOST_FLAGS
                    return null;
                toggle_hud_type(dead_user, Dm.num(to_toggle));
                return TRUE;
            }
            case "crew_manifest":
                await GLOB.manifest.ui_interact(dead_user);
                return TRUE;
            case "view_range":
            {
                object setting_to = Dm.add(text2num(@params["new_view_range"]), GHOST_MIN_VIEW_RANGE);
                if (!isnum(setting_to))
                    return FALSE;
                set_view(dead_user, setting_to);
                return TRUE;
            }
            case "boo":
                if (!Dm.truthy(dead_user.fun_verbs))
                    return FALSE;
                dead_user.boo();
                return TRUE;
            case "possess":
                if (!Dm.truthy(dead_user.fun_verbs))
                    return FALSE;
                await dead_user.possess();
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:89
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        var ghost = (mob_dead_observer)user;
        DmList data = Dm.list();

        data["can_boo"] = ghost.bootime <= world.time ? TRUE : FALSE; // COOLDOWN_FINISHED(user, bootime)
        data["has_fun"] = ghost.fun_verbs;
        data["body_name"] = Dm.truthy(ghost.can_reenter_corpse) && ghost.mind?.current != null ? ghost.mind.current.real_name : FALSE;
        foreach (object level in Dm.each(GLOB.ghost_lightings))
        {
            if (Dm.eq(GLOB.ghost_lightings[level], ghost.lighting_cutoff))
                data["current_darkness"] = level;
        }
        DmList notifications = Dm.list();
        foreach (object key in Dm.each(GLOB.poll_ignore_desc))
        {
            notifications.Add(Dm.list(Dm.list(
                Dm.kv("key", key),
                Dm.kv("enabled", Dm.@in(ghost.ckey, GLOB.poll_ignore[key] as DmList) ? TRUE : FALSE),
                Dm.kv("desc", GLOB.poll_ignore_desc[key]))));
        }
        data["notification_data"] = notifications;

        data["current_extra_view"] = 0; // DV390: no client view size

        data["hud_info"] = Dm.list(
            Dm.list(Dm.kv("name", "Data HUDs"), Dm.kv("enabled", Dm.bitand(ghost.ghost_hud_flags, GHOST_DATA_HUDS)), Dm.kv("flag", GHOST_DATA_HUDS), Dm.kv("tooltip", "Grants you Med/Sec/Diag HUDs.")),
            Dm.list(Dm.kv("name", "Ghost Vision"), Dm.kv("enabled", Dm.bitand(ghost.ghost_hud_flags, GHOST_VISION)), Dm.kv("flag", GHOST_VISION), Dm.kv("tooltip", "Allows you to see ghost-only things (ex: smuggler satchels, countdowns, camera eyes).")),
            Dm.list(Dm.kv("name", "Health Scanner"), Dm.kv("enabled", Dm.bitand(ghost.ghost_hud_flags, GHOST_HEALTH)), Dm.kv("flag", GHOST_HEALTH), Dm.kv("tooltip", "Allows you to perform a health scan by clicking on someone.")),
            Dm.list(Dm.kv("name", "Chemical Scanner"), Dm.kv("enabled", Dm.bitand(ghost.ghost_hud_flags, GHOST_CHEM)), Dm.kv("flag", GHOST_CHEM), Dm.kv("tooltip", "Allows you to perform a chemical scan by clicking on someone.")),
            Dm.list(Dm.kv("name", "Gas Scanner"), Dm.kv("enabled", Dm.bitand(ghost.ghost_hud_flags, GHOST_GAS)), Dm.kv("flag", GHOST_GAS), Dm.kv("tooltip", "Allows you to perform a gas scan by clicking on a tile/atmos machine.")));

        return data;
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:144
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["max_extra_view"] = GHOST_MAX_VIEW_RANGE_DEFAULT - GHOST_MIN_VIEW_RANGE; // a BYOND member's wider range is DV390's
        DmList levels = Dm.list();
        foreach (object level in Dm.each(GLOB.ghost_lightings))
            levels.Add(level);
        data["darkness_levels"] = levels;
        data["lag_switch_on"] = zoom_tray_disabled(user) ? TRUE : FALSE;
        return data;
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:153
    public virtual void tray_view(mob_dead_observer user = null)
    {
        if (zoom_tray_disabled(user))
        {
            to_chat(user, span_notice(I18n.T("That verb is currently globally disabled.")));
            return;
        }
        t_ray_scan(user);
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:159
    public virtual void toggle_darkness(mob_dead_observer user = null, object darkness_type = null)
    {
        user.client.prefs.write_preference(GLOB.preference_entries[typeof(datum_preference_choiced_ghost_lighting)] as datum_preference, darkness_type);
        user.lighting_cutoff = (float?)Dm.num(user.default_lighting_cutoff());
        user.update_sight();
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:164
    public virtual void toggle_hud_type(mob_dead_observer user = null, object hud_type = null)
    {
        user.toggle_ghost_hud_flag(hud_type);
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:167
    public virtual void restore_ghost_appearance(mob_dead_observer user = null)
    {
        user.set_ghost_appearance();
        if (user.client?.prefs == null)
            return;
        object real_name_pref = user.client.prefs.read_preference(typeof(datum_preference_name_real_name));
        user.deadchat_name = real_name_pref;
        if (user.mind != null)
            user.mind.ghostname = real_name_pref;
        user.name = Dm.str(real_name_pref);
    }

    // tg: code/modules/mob/dead/observer/ghost_menu.dm:177
    public virtual object set_view(mob_dead_observer user = null, object new_view = null)
    {
        if (zoom_tray_disabled(user))
        {
            to_chat(user, span_notice(I18n.T("That verb is currently globally disabled.")));
            return TRUE;
        }
        // :181-184 user.client.view_size.setTo(round(new_view, 1) - GHOST_MIN_VIEW_RANGE): DV390, the client has no view size
        return null;
    }
}

public partial class mob_dead_observer
{
    // tg: code/modules/mob/dead/observer/observer.dm:384  GAME_VERB(/mob/dead/observer, do_not_resuscitate, "Do Not Resuscitate", null):
    // the verb's body (the ghost HUD's DNR button calls the verb, screen_objects/ghost.dm:41)
    public virtual async DmTask __gvb_do_not_resuscitate(DmList structured_args = null)
    {
        if (!Dm.truthy(can_reenter_corpse))
        {
            to_chat(usr, span_warning(I18n.T("You're already stuck out of your body!")));
            return;
        }

        object response = await tgui_alert(usr, I18n.T("Are you sure you want to prevent (almost) all means of resuscitation? This cannot be undone."), I18n.T("Are you sure you want to stay dead?"), Dm.list("DNR", "Save Me"));
        if (Dm.eq(response, "DNR"))
            stay_dead();
    }

    // tg: code/modules/mob/dead/observer/observer.dm:384  the verb (as dead_tele's): a detached call of the body after SSverbs.invoke_verb's
    // check (a caller without a client does nothing); the body sleeps in tgui_alert.
    public virtual void do_not_resuscitate(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_do_not_resuscitate(Dm.list()));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:394
    public virtual object stay_dead()
    {
        if (!Dm.truthy(can_reenter_corpse))
        {
            to_chat(usr, span_warning(I18n.T("You're already stuck out of your body!")));
            return FALSE;
        }

        can_reenter_corpse = FALSE;
        var current_mob = mind?.current as mob_living;
        if (current_mob != null)
        {
            // Update med huds
            current_mob.med_hud_set_status();
            current_mob.log_message(Dm.text("had their player ([]) do-not-resuscitate / DNR", key_name(this)), LOG_GAME, color: COLOR_GREEN, log_globally: FALSE);
            if (Dm.truthy(current_mob._listen_lookup?[COMSIG_LIVING_DNR])) // SEND_SIGNAL
                current_mob._SendSignal(COMSIG_LIVING_DNR, Dm.list(current_mob, this));
        }

        log_message(Dm.text("has opted to do-not-resuscitate / DNR from their body ([])", current_mob), LOG_GAME, color: COLOR_GREEN);

        // Disassociates observer mind from the body mind
        mind = null;

        to_chat(this, span_boldnotice(I18n.T("You can no longer be brought back into your body.")));
        return TRUE;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:678
    public virtual void boo()
    {
        if (!(bootime <= world.time)) // COOLDOWN_FINISHED(src, bootime)
            return;
        obj_machinery_light L = null;
        foreach (obj_machinery_light candidate in Dm.each<obj_machinery_light>(view(1, this)))
        {
            L = candidate;
            break;
        }
        if (L != null && Dm.truthy(L.flicker()))
            bootime = world.time + 60 * SECONDS; // COOLDOWN_START(src, bootime, 60 SECONDS)
        //Maybe in the future we can add more <i>spooky</i> code here!
    }

    // tg: code/modules/mob/dead/observer/observer.dm:725
    public virtual async DmTask<object> possess()
    {
        DmList possessible = Dm.list();
        foreach (mob_living L in Dm.each<mob_living>(GLOB.alive_mob_list))
        {
            if (L is mob_living_carbon_human_dummy || get_turf(L) == null) //Haha no.
                continue;
            if (!Dm.@in(L, GLOB.player_list) && L.mind == null)
                possessible.Add(L);
        }

        var target = await tgui_input_list(usr, I18n.T("Your new life begins today!"), I18n.T("Possess Mob"), sort_names(possessible) as DmList) as mob_living;

        if (target == null)
            return FALSE;

        if (ismegafauna(target))
        {
            to_chat(this, span_warning(I18n.T("This creature is too powerful for you to possess!")));
            return FALSE;
        }

        if (Dm.truthy(can_reenter_corpse) && mind?.current != null)
        {
            if (Dm.eq(await tgui_alert(usr, I18n.T("Your soul is still tied to your former life as [], if you go forward there is no going back to that life. Are you sure you wish to continue?", mind.current.name), I18n.T("Move On"), Dm.list("Yes", "No")), "No"))
                return FALSE;
        }
        if (Dm.truthy(target.key))
        {
            to_chat(this, span_warning(I18n.T("Someone has taken this body while you were choosing!")));
            return FALSE;
        }

        target.PossessByPlayer(key);
        target.set_faction(Dm.list(FACTION_NEUTRAL));
        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:34
public partial class atom_movable_screen_ghost_dnr : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:39
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        var dnring = usr as mob_dead_observer;
        dnring.do_not_resuscitate();
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:52
public partial class atom_movable_screen_ghost_settings : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:57
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        flick(Dm.add(icon_state, "_anim"), this);
    }

    // tg: code/_onclick/hud/screen_objects/ghost.dm:62
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await GLOB.ghost_menu.ui_interact(usr);
        return null;
    }
}
