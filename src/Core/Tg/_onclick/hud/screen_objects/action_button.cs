// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/action_button.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3 (D146): the whole file: the action button, the mob's button procs, the hud's button icons, the palette,
// its scroll buttons and the landing. Left out, each with a row in not-translated.md:
// - dragging a button to rearrange it (MouseDrag :79, mouse_drop_dragged :108, save_position :144): BYOND's MouseDrag and
//   MouseDrop have no C# roots and the host has no drag action (a later milestone, with the drag and drop of items);
// - the saved button positions of the preferences (load_position and dump_save read client.prefs, null until the
//   preferences, M9): a button always takes its action's default position.
// The var blocks are generated.

// tg: code/_onclick/hud/screen_objects/action_button.dm:1
public partial class atom_movable_screen_movable_action_button : atom_movable_screen_movable
{
    /// GAP(types): tg's untyped `var/id` (action_button.dm:18) is generated as a Type (V3: some subtype stores a typepath in
    /// a var named id), but the action button keeps a bitflag in it (SetId, action.dm:389). The number lives here.
    public object id_value;

    // tg: code/_onclick/hud/screen_objects/action_button.dm:27
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(our_hud))
        {
            mob viewer = our_hud.mymob;
            Waiting.NoSleep(our_hud.hide_action(this));
            if (viewer?.client != null)
                Dm.remove(viewer.client.screen, this);
            Dm.remove(ref linked_action.viewers, our_hud);
            Waiting.NoSleep(viewer.update_action_buttons());
            Dm.remove(ref our_hud.screen_objects, hud_key);
            if (our_hud.screen_groups != null && Dm.truthy(our_hud.screen_groups[hud_group_key]))
                Dm.remove_at(our_hud.screen_groups, hud_group_key, this);
            our_hud = null;
        }
        linked_action = null;
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:41
    public virtual object can_use(mob user = null)
    {
        if (isobserver(user))
        {
            mob_dead_observer dead_mob = user as mob_dead_observer;
            if (Dm.truthy(allow_observer_click))
                return TRUE;
            if (Dm.truthy(dead_mob.observetarget)) // Observers can only click on action buttons if they're not observing something
                return FALSE;
        }

        if (Dm.truthy(linked_action))
        {
            if (Dm.truthy(linked_action.viewers[user.hud_used]))
                return TRUE;
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:56
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob user = usr;
        if (!Dm.truthy(can_use(user)))
            return FALSE;

        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)))
        {
            if (linked_action != null)
                await linked_action.begin_creating_bind(this, user);
            return TRUE;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
        {
            datum_hud our_hud = user.hud_used;
            await our_hud.position_action(this, SCRN_OBJ_DEFAULT);
            return TRUE;
        }
        if (user.next_click > world.time)
            return null;
        user.next_click = world.time + 1;
        float trigger_flags = 0;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            trigger_flags = Dm.bitor(trigger_flags, TRIGGER_SECONDARY_ACTION);
        await linked_action.Trigger(user, trigger_flags: trigger_flags);
        return TRUE;
    }

    // MouseDrag (:79) and mouse_drop_dragged (:108): dragging a button to another place or group. BYOND's MouseDrag and
    // MouseDrop have no C# roots and the host sends no drag (a later milestone, with item drag and drop); not declared.

    // tg: code/_onclick/hud/screen_objects/action_button.dm:99
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (!QDELETED(this))
            openToolTip(usr, this, @params, title: name, content: desc, theme: actiontooltipstyle);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:104
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        closeToolTip(usr);
        base.MouseExited(location, control, @params);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:144
    public virtual void save_position()
    {
        // Reached only from mouse_drop_dragged (:121, :127, :134, :141), which is not declared (dragging, see above).
        Dm.NotTranslated("code/_onclick/hud/screen_objects/action_button.dm:144", "save_position: the dragged button's saved position (dragging and preferences, M10+)");
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:159
    public virtual async DmTask load_position()
    {
        mob user = our_hud.mymob;
        if (!Dm.truthy(user))
            return;
        // user.client?.prefs?.action_buttons_screen_locs["[name]_[id]"]: the preferences are null until M9 (client/New's
        // prefs are a deferred leaf), so the button takes SCRN_OBJ_DEFAULT as tg does for a button with no saved place.
        object position_info = user.client?.prefs?.action_buttons_screen_locs?[Dm.text("[]_[]", name, id_value)] is var saved && Dm.truthy(saved) ? saved : SCRN_OBJ_DEFAULT;
        await user.hud_used.position_action(this, position_info);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:166
    public virtual void dump_save()
    {
        mob user = our_hud.mymob;
        if (!Dm.truthy(user?.client))
            return;
        Dm.remove(ref user.client.prefs.action_buttons_screen_locs, Dm.text("[]_[]", name, id_value));
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:172
    public virtual void update_keybind_maptext(object key = null)
    {
        cut_overlay(keybind_maptext);
        if (!Dm.truthy(key))
            return;
        keybind_maptext = Dm.@new<mutable_appearance>();
        keybind_maptext.maptext = MAPTEXT(Dm.text("<span style='text-align: right'>[]</span>", key));
        keybind_maptext.transform = ((matrix)keybind_maptext.transform).Translate(-4, Dm.length(key) > 1 ? -6 : 2); //with modifiers, its placed lower so cooldown is visible
        add_overlay(keybind_maptext);
    }
}

/**
 * This is a silly proc used in hud code code to determine what icon and icon state we should be using
 * for hud elements (such as action buttons) that don't have their own icon and icon state set.
 *
 * It returns a list, which is pretty much just a struct of info
 */
// tg: code/_onclick/hud/screen_objects/action_button.dm:187
public partial class datum_hud : datum
{
    // tg: code/_onclick/hud/screen_objects/action_button.dm:187
    public virtual object get_action_buttons_icons()
    {
        DmList dot = null;
        dot = Dm.list();
        dot["bg_icon"] = ui_style;
        dot["bg_state"] = "template";
        dot["bg_state_active"] = "template_active";
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/action_button.dm:200
public partial class mob : atom_movable
{
    /**
     * Updates all action buttons this mob has.
     *
     * Arguments:
     * * update_flags - Which flags of the action should we update
     * * force - Force buttons update even if the given button icon state has not changed
     */
    // tg: code/_onclick/hud/screen_objects/action_button.dm:200
    public virtual async DmTask update_mob_action_buttons(float? update_flags = null, float? force = null)
    {
        update_flags ??= ALL;
        force ??= FALSE;
        foreach (datum_action current_action in Dm.each_cast<datum_action>(actions))
            await current_action.build_all_button_icons(update_flags, force);
    }

    /**
     * This proc handles adding all of the mob's actions to their screen
     *
     * If you just need to update existing buttons, use [/mob/proc/update_mob_action_buttons]!
     *
     * Arguments:
     * * update_flags - reload_screen - bool, if TRUE, this proc will add the button to the screen of the passed mob as well
     */
    // tg: code/_onclick/hud/screen_objects/action_button.dm:212
    public virtual async DmTask update_action_buttons(float? reload_screen = null)
    {
        reload_screen ??= FALSE;
        if (!Dm.truthy(hud_used) || !Dm.truthy(client))
            return;

        if (hud_used.hud_version != HUD_STYLE_STANDARD)
            return;

        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
        {
            atom_movable_screen_movable_action_button button = action.viewers[hud_used] as atom_movable_screen_movable_action_button;
            await action.build_all_button_icons();
            if (Dm.truthy(reload_screen))
                Dm.append(client.screen, button);
        }

        if (Dm.truthy(reload_screen))
            hud_used.update_our_owner();
        // This holds the logic for the palette buttons
        hud_used.palette_actions.refresh_actions();
    }

    /**
     * Show (most) of the another mob's action buttons to this mob
     *
     * Used for observers viewing another mob's screen
     */
    // tg: code/_onclick/hud/screen_objects/action_button.dm:235
    public virtual async DmTask show_other_mob_action_buttons(mob take_from = null)
    {
        if (!Dm.truthy(hud_used) || !Dm.truthy(client))
            return;

        foreach (datum_action action in Dm.each_cast<datum_action>(take_from.actions))
        {
            if (!Dm.truthy(action.show_to_observers) || !Dm.truthy(action.owner_has_control))
                continue;
            await action.GiveAction(this);
        }
        RegisterSignal(take_from, COMSIG_MOB_GRANTED_ACTION, nameof(on_observing_action_granted));
        RegisterSignal(take_from, COMSIG_MOB_REMOVED_ACTION, nameof(on_observing_action_removed));
    }

    /**
     * Hide another mob's action buttons from this mob
     *
     * Used for observers viewing another mob's screen
     */
    // tg: code/_onclick/hud/screen_objects/action_button.dm:251
    public virtual void hide_other_mob_action_buttons(mob take_from = null)
    {
        foreach (datum_action action in Dm.each_cast<datum_action>(take_from.actions))
            action.HideFrom(this);
        UnregisterSignal(take_from, Dm.list(COMSIG_MOB_GRANTED_ACTION, COMSIG_MOB_REMOVED_ACTION));
    }

    /// Signal proc for [COMSIG_MOB_GRANTED_ACTION] - If we're viewing another mob's action buttons,
    /// we need to update with any newly added buttons granted to the mob.
    // tg: code/_onclick/hud/screen_objects/action_button.dm:258
    [SignalHandler]
    public virtual void on_observing_action_granted(mob_living source = null, datum_action action = null)
    {
        if (!Dm.truthy(action.show_to_observers) || !Dm.truthy(action.owner_has_control))
            return;
        Waiting.NoSleep(action.GiveAction(this));
    }

    /// Signal proc for [COMSIG_MOB_REMOVED_ACTION] - If we're viewing another mob's action buttons,
    /// we need to update with any removed buttons from the mob.
    // tg: code/_onclick/hud/screen_objects/action_button.dm:267
    [SignalHandler]
    public virtual void on_observing_action_removed(mob_living source = null, datum_action action = null)
    {
        action.HideFrom(this);
    }
}

// tg: code/_onclick/hud/screen_objects/action_button.dm:272
public partial class atom_movable_screen_button_palette : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/action_button.dm:283
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(our_hud))
        {
            client shown_to = our_hud.mymob?.canon_client;
            if (shown_to != null)
                Dm.remove(shown_to.screen, this);
            Dm.remove(ref our_hud.screen_objects, hud_key);
            if (our_hud.screen_groups != null && Dm.truthy(our_hud.screen_groups[hud_group_key]))
                Dm.remove_at(our_hud.screen_groups, hud_group_key, this);
            our_hud = null;
        }
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:291
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hud hud_owner = Dm.arg<datum_hud>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_appearance();
        set_hud(hud_owner);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:296
    public virtual void set_hud(datum_hud our_hud = null)
    {
        this.our_hud = our_hud;
        refresh_owner();
        disable_landing(); // If our hud already has elements, don't force hide us
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:301
    public override object update_name(float? updates = null)
    {
        object dot = null;
        dot = base.update_name(updates);
        if (Dm.truthy(expanded))
            name = I18n.T("Hide Buttons");
        else
            name = I18n.T("Show Buttons");
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:308
    public virtual void refresh_owner()
    {
        mob viewer = our_hud.mymob;
        if (Dm.truthy(viewer.client))
            Dm.combine(viewer.client.screen, this);

        DmList settings = our_hud.get_action_buttons_icons() as DmList;
        string ui_icon = Dm.text("[]", settings["bg_icon"]);
        DmList ui_segments = splittext(ui_icon, ".");
        DmList ui_paths = splittext(ui_segments[1], "/");
        object ui_name = ui_paths[Dm.length(ui_paths)];

        icon_state = Dm.text("[]_palette", ui_name);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:321
    public virtual void activate_landing()
    {
        // Reveal ourselves to the user
        invisibility = INVISIBILITY_NONE;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:325
    public virtual void disable_landing()
    {
        // If we have no elements in the palette, hide your ugly self please
        if (!Dm.truthy(Dm.length(our_hud.palette_actions?.actions)) && !Dm.truthy(Dm.length(our_hud.floating_actions)))
            invisibility = INVISIBILITY_ABSTRACT;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:330
    public virtual void update_state()
    {
        if (Dm.truthy(Dm.length(our_hud.floating_actions)))
            activate_landing();
        else
            disable_landing();
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:336
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (QDELETED(this))
            return;
        show_tooltip(@params);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:342
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        closeToolTip(usr);
        base.MouseExited(location, control, @params);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:346
    public virtual void show_tooltip(object @params = null)
    {
        openToolTip(usr, this, @params, title: name, content: desc);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:352
    public virtual async DmTask play_item_added()
    {
        await color_for_now(GLOB.palette_added_matrix);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:355
    public virtual async DmTask play_item_removed()
    {
        await color_for_now(GLOB.palette_removed_matrix);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:358
    public virtual async DmTask color_for_now(DmList color = null)
    {
        if (Dm.truthy(color_timer_id))
            return;
        await add_atom_colour(color, TEMPORARY_COLOUR_PRIORITY); //We unfortunately cannot animate matrix colors. Curse you lummy it would be ~~non~~trivial to interpolate between the two valuessssssssss
        color_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(remove_color), color), 2 * SECONDS, file: "code/_onclick/hud/screen_objects/action_button.dm", line: 362);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:364
    public virtual async DmTask remove_color(DmList to_remove = null)
    {
        color_timer_id = null;
        await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, to_remove);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:368
    public virtual object can_use(mob user = null)
    {
        if (isobserver(user))
        {
            mob_dead_observer O = user as mob_dead_observer;
            return Dm.num(!Dm.truthy(O.observetarget));
        }
        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:374
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob user = usr;
        if (!Dm.truthy(can_use(user)))
            return null;

        DmList modifiers = params2list(@params);

        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)))
        {
            foreach (datum_action action in Dm.each_cast<datum_action>(user.actions)) // Reset action positions to default
            {
                foreach (datum_hud hud in Dm.each_cast<datum_hud>(action.viewers))
                {
                    atom_movable_screen_movable_action_button button = action.viewers[hud] as atom_movable_screen_movable_action_button;
                    await hud.position_action(button, SCRN_OBJ_DEFAULT);
                }
            }
            to_chat(user, span_notice(I18n.T("Action button positions have been reset.")));
            return TRUE;
        }

        set_expanded(Dm.num(!Dm.truthy(expanded)));
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:390
    public virtual void clicked_while_open(datum source = null, atom target = null, atom location = null, object control = null, object @params = null, mob user = null)
    {
        if (istype(target, typeof(atom_movable_screen_movable_action_button)) || istype(target, typeof(atom_movable_screen_palette_scroll)) || target == this) // If you're clicking on an action button, or us, you can live
            return;
        set_expanded(FALSE);
        if (Dm.truthy(source))
            UnregisterSignal(source, COMSIG_CLIENT_CLICK);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:397
    public virtual void set_expanded(object new_expanded = null)
    {
        datum_action_group our_group = our_hud.palette_actions;
        if (!Dm.truthy(Dm.length(our_group.actions))) //Looks dumb, trust me lad
            new_expanded = FALSE;
        if (Dm.eq(expanded, new_expanded))
            return;

        expanded = Dm.num(new_expanded);
        our_group.refresh_actions();
        update_appearance();

        mob user = usr;
        if (!Dm.truthy(user.client))
            return;

        if (Dm.truthy(expanded))
            RegisterSignal(user.client, COMSIG_CLIENT_CLICK, nameof(clicked_while_open));
        else
            UnregisterSignal(user.client, COMSIG_CLIENT_CLICK);

        closeToolTip(usr); //Our tooltips are now invalid, can't seem to update them in one frame, so here, just close them
    }
}

// tg: code/_onclick/hud/screen_objects/action_button.dm:349-350
public partial class datum_controller_global_vars
{
    // tg: code/_onclick/hud/screen_objects/action_button.dm:349  GLOBAL_LIST_INIT(palette_added_matrix, list(...))
    public DmList palette_added_matrix;

    // tg: code/_onclick/hud/screen_objects/action_button.dm:349  (GLOBAL_MANAGED(palette_added_matrix, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpalette_added_matrix()
    {
        palette_added_matrix = Dm.list(0.4f, 0.5f, 0.2f, 0, 0, 1.4f, 0, 0, 0, 0.4f, 0.6f, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        Dm.append(ref gvars_datum_init_order, "palette_added_matrix");
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:350  GLOBAL_LIST_INIT(palette_removed_matrix, list(...))
    public DmList palette_removed_matrix;

    // tg: code/_onclick/hud/screen_objects/action_button.dm:350  (GLOBAL_MANAGED(palette_removed_matrix, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpalette_removed_matrix()
    {
        palette_removed_matrix = Dm.list(1.4f, 0, 0, 0, 0.7f, 0.4f, 0, 0, 0.4f, 0, 0.6f, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        Dm.append(ref gvars_datum_init_order, "palette_removed_matrix");
    }
}

// tg: code/_onclick/hud/screen_objects/action_button.dm:418
public partial class atom_movable_screen_palette_scroll : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/action_button.dm:427
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hud hud_owner = Dm.arg<datum_hud>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        set_hud(hud_owner);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:431
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(our_hud))
        {
            client shown_to = our_hud.mymob?.canon_client;
            if (shown_to != null)
                Dm.remove(shown_to.screen, this);
            Dm.remove(ref our_hud.screen_objects, hud_key);
            if (our_hud.screen_groups != null && Dm.truthy(our_hud.screen_groups[hud_group_key]))
                Dm.remove_at(our_hud.screen_groups, hud_group_key, this);
            our_hud = null;
        }
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:439
    public virtual object can_use(mob user = null)
    {
        if (isobserver(user))
        {
            mob_dead_observer O = user as mob_dead_observer;
            return Dm.num(!Dm.truthy(O.observetarget));
        }
        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:445
    public virtual void set_hud(datum_hud our_hud = null)
    {
        this.our_hud = our_hud;
        refresh_owner();
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:449
    public virtual void refresh_owner()
    {
        mob viewer = our_hud.mymob;
        if (Dm.truthy(viewer.client))
            Dm.combine(viewer.client.screen, this);

        DmList settings = our_hud.get_action_buttons_icons() as DmList;
        icon = settings["bg_icon"];
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:457
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!Dm.truthy(can_use(usr)))
            return null;
        our_hud.palette_actions.scroll(scroll_direction);
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:462
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (QDELETED(this))
            return;
        openToolTip(usr, this, @params, title: name, content: desc);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:468
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        closeToolTip(usr);
        base.MouseExited(location, control, @params);
    }
}

/// Exists so you have a place to put your buttons when you move them around
// tg: code/_onclick/hud/screen_objects/action_button.dm:485
public partial class atom_movable_screen_action_landing : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/action_button.dm:494
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(owner))
        {
            owner.landing = null;
            client shown_to = owner?.owner?.mymob?.canon_client;
            if (shown_to != null)
                Dm.remove(shown_to.screen, this);
            owner.refresh_actions();
            owner = null;
        }
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:502
    public virtual void set_owner(datum_action_group owner = null)
    {
        this.owner = owner;
        refresh_owner();
    }

    // tg: code/_onclick/hud/screen_objects/action_button.dm:506
    public virtual void refresh_owner()
    {
        datum_hud our_hud = owner.owner;
        mob viewer = our_hud.mymob;
        if (Dm.truthy(viewer.client))
            Dm.combine(viewer.client.screen, this);

        DmList settings = our_hud.get_action_buttons_icons() as DmList;
        icon = settings["bg_icon"];
    }

    /// Reacts to having a button dropped on it
    // tg: code/_onclick/hud/screen_objects/action_button.dm:516
    public virtual async DmTask hit_by(atom_movable_screen_movable_action_button button = null)
    {
        datum_hud our_hud = owner.owner;
        await our_hud.position_action(button, owner.location);
    }
}
