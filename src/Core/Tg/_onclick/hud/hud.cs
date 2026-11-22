// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P3 (D107): the /datum/hud subset that makes the M2 screen objects (the health doll, the health indicator, the
// fullscreens and alerts it shows) and puts them into client.screen through tg's show_hud; the replicator sends
// client.screen privately. The HUD features of later milestones are Dm.Deferred at the statement with their milestone:
// the inventory slots and hands (M4a), screentips (M9); the action palette, the action groups and the buttons came with
// M4c slice 3 (D146), except fitting them into a changed view (the view, M9); the plane master groups and
// controllers, parallax and the starlight and darkness overlays are never ported as such (lighting and planes, rules 13,
// D24, DV12).

// tg: code/_onclick/hud/hud.dm:23
[DmPath("/datum/hud")]
public partial class datum_hud : datum
{
    // tg: code/_onclick/hud/hud.dm:24
    public mob mymob;
    /// Used for the HUD toggle (F12)
    // tg: code/_onclick/hud/hud.dm:26
    public float hud_shown = TRUE;
    /// Current displayed version of the HUD
    // tg: code/_onclick/hud/hud.dm:28
    public float hud_version = HUD_STYLE_STANDARD;
    /// Equipped item inventory
    // tg: code/_onclick/hud/hud.dm:30
    public float inventory_shown = FALSE;
    /// This is to hide the buttons that can be used via hotkeys. (hotkeybuttons list of buttons)
    // tg: code/_onclick/hud/hud.dm:32
    public float hotkey_ui_hidden = FALSE;

    /// Assoc list of key => "plane master groups"
    /// This is normally just the main window, but it'll occasionally contain things like spyglasses windows
    // tg: code/_onclick/hud/hud.dm:36 (list(): dm_init_vars below)
    public DmList master_groups;
    ///Assoc list of controller groups, associated with key string group name with value of the plane master controller ref
    // tg: code/_onclick/hud/hud.dm:38 (list(): dm_init_vars below)
    public DmList plane_master_controllers;

    /// UI for screentips that appear when you mouse over things
    /// Stored directly as it is used in very hot MouseEntered code
    // tg: code/_onclick/hud/hud.dm:42
    public atom_movable_screen_screentip screentip_text = null;

    /// Whether or not screentips are enabled.
    /// This is updated by the preference for cheaper reads than would be
    /// had with a proc call, especially on one of the hottest procs in the
    /// game (MouseEntered).
    // tg: code/_onclick/hud/hud.dm:48
    public string screentips_enabled = SCREENTIP_PREFERENCE_ENABLED;
    /// Whether to use text or images for click hints.
    /// Same behavior as `screentips_enabled`--very hot, updated when the preference is updated.
    // tg: code/_onclick/hud/hud.dm:51
    public float screentip_images = TRUE;
    /// If this client is being shown atmos debug overlays or not
    // tg: code/_onclick/hud/hud.dm:53
    public float atmos_debug_overlays = FALSE;

    ///Boolean on whether they need at least a healths or healdoll, for unit tests.
    // tg: code/_onclick/hud/hud.dm:56
    public float needs_health_indicator = TRUE;
    /// The color to use for the screentips.
    /// This is updated by the preference for cheaper reads than would be
    /// had with a proc call, especially on one of the hottest procs in the game (MouseEntered).
    // tg: code/_onclick/hud/hud.dm:60
    public object screentip_color = null;
    // tg: code/_onclick/hud/hud.dm:61
    public datum_action_group_palette palette_actions = null;
    // tg: code/_onclick/hud/hud.dm:62
    public datum_action_group_listed listed_actions = null;
    // tg: code/_onclick/hud/hud.dm:63
    public DmList floating_actions = null;

    /// Subtypes can override this to force a specific UI style
    // tg: code/_onclick/hud/hud.dm:66
    public object ui_style = null;
    /// Assoc list of all screen objects we hold by their key
    // tg: code/_onclick/hud/hud.dm:68 (list(): dm_init_vars below)
    public DmList screen_objects;
    /// List of screen objects by their screen group
    // tg: code/_onclick/hud/hud.dm:70 (var/list/screen_groups[SCREEN_GROUP_AMT]: dm_init_vars below)
    public DmList screen_groups;

    /// List of typepaths of /datum/inventory_slot which will be used to automatically create inventory slot UI elements
    /// If assigned a typepath instead of a list, it will instead use all valid subtypes of said typepath
    /// Can be changed in initialize_screen_objects() to add/remove slots
    // tg: code/_onclick/hud/hud.dm:75  var/list/datum/inventory_slot/default_inventory_slots = null; object, not DmList
    // (rules 3.5, a "-" cell): subtypes store a typepath in it (human.dm:2, drones.dm:2), create_inventory_slots
    // (hud.dm:497) tells the two apart with ispath
    public object default_inventory_slots = null;
    /// Alist of slot_id -> inventory_slot datum of our slots
    /// These are singletons, do not modify individual slots!
    // tg: code/_onclick/hud/hud.dm:78 (alist(): dm_init_vars below)
    public DmList inventory_slots;

    /// List of weakrefs to objects that we add to our screen that we don't expect to DO anything
    /// They typically use * in their render target. They exist solely so we can reuse them,
    /// and avoid needing to make changes to all idk 300 consumers if we want to change the appearance
    // tg: code/_onclick/hud/hud.dm:83 (list(): dm_init_vars below)
    public DmList asset_refs_for_reuse;

    // The object-creating defaults of the var block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/_onclick/hud/hud.dm:36
        master_groups = Dm.list();
        // tg: code/_onclick/hud/hud.dm:38
        plane_master_controllers = Dm.list();
        // tg: code/_onclick/hud/hud.dm:68
        screen_objects = Dm.list();
        // tg: code/_onclick/hud/hud.dm:70
        screen_groups = Dm.list_n(SCREEN_GROUP_AMT);
        // tg: code/_onclick/hud/hud.dm:78
        inventory_slots = Dm.alist();
        // tg: code/_onclick/hud/hud.dm:83
        asset_refs_for_reuse = Dm.list();
    }
}

// tg: code/_onclick/hud/hud.dm:8
public partial class datum_controller_global_vars
{
    // The default UI style is the first one in the list
    // tg: code/_onclick/hud/hud.dm:8  GLOBAL_LIST_INIT(available_ui_styles, list(...))
    public DmList available_ui_styles;

    // tg: code/_onclick/hud/hud.dm:8  (GLOBAL_MANAGED(available_ui_styles, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalavailable_ui_styles()
    {
        available_ui_styles = Dm.list(
            Dm.kv("Midnight", Dm.resource("icons/hud/screen_midnight.dmi")),
            Dm.kv("Retro", Dm.resource("icons/hud/screen_retro.dmi")),
            Dm.kv("Plasmafire", Dm.resource("icons/hud/screen_plasmafire.dmi")),
            Dm.kv("Slimecore", Dm.resource("icons/hud/screen_slimecore.dmi")),
            Dm.kv("Operative", Dm.resource("icons/hud/screen_operative.dmi")),
            Dm.kv("Clockwork", Dm.resource("icons/hud/screen_clockwork.dmi")),
            Dm.kv("Glass", Dm.resource("icons/hud/screen_glass.dmi")),
            Dm.kv("Trasen-Knox", Dm.resource("icons/hud/screen_trasenknox.dmi")),
            Dm.kv("Detective", Dm.resource("icons/hud/screen_detective.dmi"))
        );
        Dm.append(ref gvars_datum_init_order, "available_ui_styles");
    }
}

// tg: code/_onclick/hud/hud.dm:20 (global procs)
public static partial class GlobalProcs
{
    // tg: code/_onclick/hud/hud.dm:20
    public static object ui_style2icon(object ui_style = null)
    {
        return (GLOB.available_ui_styles[ui_style] is var or__1 && Dm.truthy(or__1) ? or__1 : GLOB.available_ui_styles[GLOB.available_ui_styles[1]]);
    }
}

// tg: code/_onclick/hud/hud.dm:85
public partial class datum_hud : datum
{
    // tg: code/_onclick/hud/hud.dm:85
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        mob owner = Dm.arg<mob>(__extra, 0);
        mymob = owner;

        if (!Dm.truthy(ui_style))
        {
            // will fall back to the default if any of these are null
            ui_style = ui_style2icon(owner.client?.prefs?.read_preference(typeof(datum_preference_choiced_ui_style)));
        }

        await add_screen_object(typeof(atom_movable_screen_button_palette), HUD_MOB_TOGGLE_PALETTE);
        await add_screen_object(typeof(atom_movable_screen_palette_scroll_down), HUD_MOB_PALETTE_DOWN);
        await add_screen_object(typeof(atom_movable_screen_palette_scroll_up), HUD_MOB_PALETTE_UP);

        // var/datum/plane_master_group/main/main_group = new(PLANE_GROUP_MAIN); main_group.attach_to(src) (:96-97): the
        // plane masters are BYOND's 2D render planes, which the 3D client does not use (lighting and planes are not ported,
        // rules 13, D24, DV12); master_groups stays empty.
        Dm.Deferred("code/_onclick/hud/hud.dm:96", "New: the main plane master group (2D render planes; never as such: rules 13, D24)");

        datum_preferences preferences = owner?.client?.prefs;
        screentip_color = preferences?.read_preference(typeof(datum_preference_color_screentip_color));
        screentips_enabled = preferences?.read_preference(typeof(datum_preference_choiced_enable_screentips)) as string;
        screentip_images = Dm.num(preferences?.read_preference(typeof(datum_preference_toggle_screentip_images)));
        screentip_text = await add_screen_object(typeof(atom_movable_screen_screentip), HUD_MOB_SCREENTIP) as atom_movable_screen_screentip;

        // The plane master controllers: their plane masters are not ported (rules 13), the game controller's filters reach the
        // client (plane_master_controller.cs, protocol version 29).
        foreach (Type mytype in Dm.each_cast<Type>(subtypesof(typeof(atom_movable_plane_master_controller))))
        {
            atom_movable_plane_master_controller controller_instance = Dm.@new(mytype, null, this) as atom_movable_plane_master_controller;
            plane_master_controllers[controller_instance.name] = controller_instance;
        }

        // owner.overlay_fullscreen("see_through_darkness", /atom/movable/screen/fullscreen/see_through_darkness) (:109): a
        // lighting-plane fullscreen (the 3D client renders light itself, D24, DV12).
        Dm.Deferred("code/_onclick/hud/hud.dm:109", "New: the see_through_darkness lighting fullscreen (lighting is the client's, D24, DV12)");

        // Register onto the global spacelight appearances
        // So they can be render targeted by anything in the world
        // for(var/obj/starlight_appearance/starlight as anything in GLOB.starlight_objects) register_reuse(starlight) (:113-114):
        // starlight is lighting (D24; GLOB.starlight_objects is not declared).
        Dm.Deferred("code/_onclick/hud/hud.dm:113", "New: the starlight appearances (lighting, D24)");

        RegisterSignal(SSmapping, COMSIG_PLANE_OFFSET_INCREASE, nameof(on_plane_increase));
        RegisterSignal(mymob, COMSIG_MOB_LOGIN, nameof(client_refresh));
        RegisterSignal(mymob, COMSIG_MOB_LOGOUT, nameof(clear_client));
        RegisterSignal(mymob, COMSIG_MOB_SIGHT_CHANGE, nameof(update_sightflags));
        RegisterSignal(mymob, COMSIG_VIEWDATA_UPDATE, nameof(on_viewdata_update));

        await initialize_screen_objects();
        await create_inventory_slots();
        update_locked_slots();

        update_sightflags(mymob, mymob.sight, NONE);
    }

    // tg: code/_onclick/hud/hud.dm:128
    public override object Destroy(float? force = null)
    {
        if (mymob.hud_used == this)
            mymob.hud_used = null;

        QDEL_NULL(ref palette_actions);
        QDEL_NULL(ref listed_actions);
        QDEL_LIST(floating_actions);
        screentip_text = null;
        screen_groups = null;
        inventory_slots.Cut();
        QDEL_LIST_ASSOC_VAL(screen_objects);
        QDEL_LIST_ASSOC_VAL(master_groups);
        QDEL_LIST_ASSOC_VAL(plane_master_controllers);
        mymob = null;

        return base.Destroy(force);
    }

    /// Creates and registers a managed screen object
    // tg: code/_onclick/hud/hud.dm:146
    public virtual async DmTask<object> add_screen_object(object new_object = null, object hud_key = null, float? group_key = null, object ui_icon = null, object ui_loc = null, float? update_screen = null)
    {
        group_key ??= HUD_GROUP_STATIC;
        update_screen ??= FALSE;
        if (ispath(new_object))
            new_object = Dm.@new((Type)new_object, null, this, ui_loc);

        if (hud_key == null)
            hud_key = REF(new_object);

        if (ui_icon != null)
            ((atom)new_object).icon = ui_icon;

        if (ui_loc != null)
            ((atom_movable)new_object).screen_loc = (string)ui_loc;

        ((atom_movable_screen)new_object).hud_key = hud_key;
        if (Dm.truthy(screen_objects[hud_key]))
            throw Dm.CRASH(Dm.text("Attempted to add a new [] screen object to the [] hud while an object with the same key [] is already present!", new_object, this, hud_key), "code/_onclick/hud/hud.dm", 161);

        screen_objects[hud_key] = new_object;

        if (Dm.truthy(group_key))
        {
            // LAZYADD(screen_groups[group_key], new_object): the list element through a temp (the macro assigns it, rules 11.1)
            DmList t__1 = screen_groups[Dm.num(group_key)] as DmList;
            LAZYADD(ref t__1, new_object);
            screen_groups[Dm.num(group_key)] = t__1;
            ((atom_movable_screen)new_object).hud_group_key = group_key;
        }

        if (Dm.truthy(update_screen))
            await show_hud(hud_version);
        return new_object;
    }

    /// Removes a screen object and refreshes the hud. Can just be passed a key.
    // tg: code/_onclick/hud/hud.dm:174
    public virtual async DmTask remove_screen_object(object to_remove = null, float? update = null)
    {
        update ??= TRUE;
        if (!(to_remove is atom_movable_screen))
        {
            to_remove = screen_objects[to_remove];
            if (!Dm.truthy(to_remove))
                return;
        }
        qdel(to_remove);
        if (Dm.truthy(update) && !QDELETED(this))
            await show_hud(hud_version);
    }

    /// Proc for children to spawn their screen object in
    // tg: code/_onclick/hud/hud.dm:184
    public virtual async DmTask initialize_screen_objects()
    {
        return;
    }

    // tg: code/_onclick/hud/hud.dm:187
    [SignalHandler]
    public virtual void client_refresh(datum source = null)
    {
        client client = mymob.canon_client;
        RegisterSignal(client, COMSIG_CLIENT_SET_EYE, nameof(on_eye_change));
        on_eye_change(null, null, client.eye);
    }

    // tg: code/_onclick/hud/hud.dm:193
    [SignalHandler]
    public virtual void clear_client(datum source = null)
    {
        if (Dm.truthy(mymob.canon_client))
            UnregisterSignal(mymob.canon_client, COMSIG_CLIENT_SET_EYE);
    }

    // tg: code/_onclick/hud/hud.dm:198
    [SignalHandler]
    public virtual void on_viewdata_update(datum source = null, object view = null)
    {
        view_audit_buttons();
    }

    // tg: code/_onclick/hud/hud.dm:203
    [SignalHandler]
    public virtual void on_eye_change(datum source = null, atom old_eye = null, atom new_eye = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_HUD_EYE_CHANGED]))
            this._SendSignal(COMSIG_HUD_EYE_CHANGED, Dm.list(this, old_eye, new_eye));

        if (Dm.truthy(old_eye))
            UnregisterSignal(old_eye, COMSIG_MOVABLE_Z_CHANGED);
        if (Dm.truthy(new_eye))
        {
            // By the time logout runs, the client's eye has already changed
            // There's just no log of the old eye, so we need to override
            // :sadkirby:
            RegisterSignal(new_eye, COMSIG_MOVABLE_Z_CHANGED, nameof(eye_z_changed), @override: TRUE);
        }
        eye_z_changed(new_eye);
    }

    // tg: code/_onclick/hud/hud.dm:216
    [SignalHandler]
    public virtual void update_sightflags(datum source = null, object new_sight = null, object old_sight = null)
    {
        // If neither the old and new flags can see turfs but not objects, don't transform the turfs
        // This is to ensure parallax works when you can't see holder objects
        if (Dm.eq(should_sight_scale(new_sight), should_sight_scale(old_sight)))
            return;

        foreach (object group_key in Dm.each(master_groups))
        {
            // group.refresh_planes_offset() (:225): GAP: /datum/plane_master_group is not declared; master_groups stays
            // empty in the port (the plane masters are deferred in New).
            Dm.NotTranslated("code/_onclick/hud/hud.dm:225", "update_sightflags: the plane master groups' offsets (2D render planes, rules 13)");
        }
    }

    // tg: code/_onclick/hud/hud.dm:227
    public virtual object should_use_scale()
    {
        return should_sight_scale(mymob.sight);
    }

    // tg: code/_onclick/hud/hud.dm:230
    public virtual object should_sight_scale(object sight_flags = null)
    {
        return Dm.num(!Dm.eq(Dm.bitand(sight_flags, Dm.bitor(SEE_TURFS, SEE_OBJS)), SEE_TURFS));
    }

    // tg: code/_onclick/hud/hud.dm:233
    [SignalHandler]
    public virtual void eye_z_changed(atom eye = null)
    {
        turf eye_turf = get_turf(eye);
        if (!Dm.truthy(eye_turf))
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_HUD_Z_CHANGED]))
            this._SendSignal(COMSIG_HUD_Z_CHANGED, Dm.list(this, eye_turf.z));
    }

    // tg: code/_onclick/hud/hud.dm:240
    [SignalHandler]
    public virtual void on_plane_increase(datum source = null, object old_max_offset = null, object new_max_offset = null)
    {
        // register_reuse(GLOB.starlight_objects[i + 1]) and build_plane_groups (:242-244): the starlight and the plane
        // masters of new z layers (lighting and render planes, never as such: rules 13, D24); a second z level is M3.
        Dm.NotTranslated("code/_onclick/hud/hud.dm:242", "on_plane_increase: the starlight and plane masters of new z layers (lighting and render planes, rules 13)");
    }

    /// Creates the required plane masters to fill out new z layers (because each "level" of multiz gets its own plane master set)
    // tg: code/_onclick/hud/hud.dm:247
    public virtual void build_plane_groups(object starting_offset = null, object ending_offset = null)
    {
        foreach (object group_key in Dm.each(master_groups))
            Dm.NotTranslated("code/_onclick/hud/hud.dm:249", "build_plane_groups: /datum/plane_master_group (2D render planes, rules 13)");
    }

    /// Returns the plane master that matches the input plane from the passed in group
    // tg: code/_onclick/hud/hud.dm:253
    public virtual object get_plane_master(object plane = null, string group_key = null)
    {
        Dm.NotTranslated("code/_onclick/hud/hud.dm:253", "get_plane_master: /datum/plane_master_group (2D render planes, rules 13)");
        return null;
    }

    /// Returns a list of all plane masters that match the input true plane, drawn from the passed in group (ignores z layer offsets)
    // tg: code/_onclick/hud/hud.dm:259
    public virtual object get_true_plane_masters(object true_plane = null, string group_key = null)
    {
        group_key ??= PLANE_GROUP_MAIN;
        DmList masters = Dm.list();
        // for(var/plane in TRUE_PLANE_TO_OFFSETS(true_plane)) masters += get_plane_master(plane, group_key) (:261-262): the
        // plane master groups are never made in the port (2D render planes, rules 13; New defers them), so there is no
        // plane master to find and the list stays empty (what /mob/sync_lighting_plane_cutoff and the other callers loop over).
        Dm.Deferred("code/_onclick/hud/hud.dm:261", "get_true_plane_masters: the plane masters of a true plane (2D render planes, never as such: rules 13)");
        return masters;
    }

    /// Returns all the planes belonging to the passed in group key
    // tg: code/_onclick/hud/hud.dm:266
    public virtual object get_planes_from(object group_key = null)
    {
        Dm.NotTranslated("code/_onclick/hud/hud.dm:266", "get_planes_from: /datum/plane_master_group (2D render planes, rules 13)");
        return null;
    }

    /// Returns the corresponding plane group datum if one exists
    // tg: code/_onclick/hud/hud.dm:271
    public virtual object get_plane_group(object key = null)
    {
        return master_groups[key];
    }

    /**
     * Shows this hud's hud to some mob
     *
     * Arguments
     * * version - denotes which style should be displayed. blank or 0 means "next version"
     * * viewmob - what mob to show the hud to. Can be this hud's mob, can be another mob, can be null (will use this hud's mob if so)
     */
    // tg: code/_onclick/hud/hud.dm:295
    public virtual async DmTask<object> show_hud(float? version = null, mob viewmob = null)
    {
        version ??= 0;
        if (!ismob(mymob))
            return FALSE;

        mob screenmob = (viewmob is var or__1 && Dm.truthy(or__1) ? or__1 : mymob);
        if (!Dm.truthy(screenmob.client))
            return FALSE;

        // This code is the absolute fucking worst, I want it to go die in a fire
        // Seriously, why
        // I'm sorry
        screenmob.client.clear_screen();
        // screenmob.client.apply_clickcatcher() (:307): the click catcher of the 2D map (the 3D client clicks by ray, M2 P4/P8)
        Dm.Deferred("code/_onclick/hud/hud.dm:307", "show_hud: apply_clickcatcher (the 2D map's click catcher; the 3D client clicks by ray)");
        // screenmob.client.apply_parallax() (:308): the 2D parallax background (never as such: the 3D client draws space)
        Dm.Deferred("code/_onclick/hud/hud.dm:308", "show_hud: apply_parallax (the 2D parallax background; the 3D client draws space)");

        float display_hud_version = Dm.num(version);
        if (!Dm.truthy(display_hud_version)) // If 0 or blank, display the next hud version
            display_hud_version = hud_version + 1;

        // If the requested version number is greater than the available versions, reset back to the first version
        if (display_hud_version > HUD_VERSIONS)
            display_hud_version = HUD_STYLE_STANDARD;

        DmList group_static = screen_groups[HUD_GROUP_STATIC] as DmList;
        DmList group_toggleable = screen_groups[HUD_GROUP_TOGGLEABLE_INVENTORY] as DmList;
        DmList group_hotkeys = screen_groups[HUD_GROUP_HOTKEYS] as DmList;
        DmList group_info = screen_groups[HUD_GROUP_INFO] as DmList;
        DmList group_screen = screen_groups[HUD_GROUP_SCREEN_OVERLAYS] as DmList;
        DmList group_storage = screen_groups[HUD_GROUP_STORAGE] as DmList;

        // Screen overlays get added regardless of the HUD state
        if (Dm.truthy(length(group_screen)))
            Dm.append(screenmob.client.screen, group_screen);

        atom_movable_screen_button_palette palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        atom_movable_screen_combattoggle action_intent = screen_objects[HUD_MOB_INTENTS] as atom_movable_screen_combattoggle;

        float sw__1 = display_hud_version;
        if (sw__1 == HUD_STYLE_STANDARD)
        {
            hud_shown = TRUE;

            if (Dm.truthy(length(group_static)))
                Dm.append(screenmob.client.screen, group_static);
            if (Dm.truthy(length(group_toggleable)) && Dm.truthy(screenmob.hud_used) && Dm.truthy(screenmob.hud_used.inventory_shown))
                Dm.append(screenmob.client.screen, group_toggleable);
            if (Dm.truthy(length(group_hotkeys)) && !Dm.truthy(hotkey_ui_hidden))
                Dm.append(screenmob.client.screen, group_hotkeys);
            if (Dm.truthy(length(group_info)))
                Dm.append(screenmob.client.screen, group_info);
            // Do not show open storages to viewers, they get their own storage UIs
            if (Dm.truthy(length(group_storage)) && viewmob == mymob)
                Dm.append(screenmob.client.screen, group_storage);

            Dm.append(screenmob.client.screen, palette);
            //Restore intent selection to the original position
            if (Dm.truthy(action_intent))
                action_intent.screen_loc = Dm.str(action_intent.default_screen_location);
        }
        else if (sw__1 == HUD_STYLE_REDUCED)
        {
            hud_shown = FALSE;

            if (Dm.truthy(length(group_info)))
                Dm.append(screenmob.client.screen, group_info);

            // Hands are apart of the static group but still should be presetn in the reduced mode
            for (float i = 1, i__end = length(mymob.held_items); i <= i__end; i++)
            {
                atom_movable_screen hand = screen_objects[HUD_KEY_HAND_SLOT(i)] as atom_movable_screen;
                if (Dm.truthy(hand))
                    Dm.append(screenmob.client.screen, hand);
            }

            if (Dm.truthy(action_intent))
            {
                //move this to the alternative position, where zone_select usually is.
                action_intent.screen_loc = ui_acti_alt;
            }
        }
        else if (sw__1 == HUD_STYLE_NOHUD)
        {
            hud_shown = FALSE;
        }

        hud_version = display_hud_version;
        inventory_update(screenmob);
        // Gives all of the actions the screenmob owes to their hud
        await screenmob.update_action_buttons(TRUE);
        // Handles alerts - the things on the right side of the screen
        reorganize_alerts(screenmob);
        screenmob.reload_fullscreen();

        update_reuse(screenmob);

        // ensure observers get an accurate and up-to-date view
        if (!Dm.truthy(viewmob))
        {
            await plane_masters_update();
            foreach (object M in Dm.each(mymob.observers))
                await show_hud(hud_version, M as mob);
        }
        else if (Dm.truthy(viewmob.hud_used))
        {
            viewmob.hide_other_mob_action_buttons(mymob);
            await viewmob.hud_used.plane_masters_update();
            await viewmob.show_other_mob_action_buttons(mymob);
        }

        if (Dm.truthy(screenmob._listen_lookup?[COMSIG_MOB_HUD_REFRESHED]))
            screenmob._SendSignal(COMSIG_MOB_HUD_REFRESHED, Dm.list(screenmob, this));
        return TRUE;
    }

    // tg: code/_onclick/hud/hud.dm:395
    public virtual async DmTask plane_masters_update()
    {
        foreach (object group_key in Dm.each(master_groups))
        {
            // Plane masters are always shown to OUR mob, never to observers
            // group.refresh_hud() (:399): /datum/plane_master_group is not declared (render planes, rules 13)
            Dm.NotTranslated("code/_onclick/hud/hud.dm:399", "plane_masters_update: /datum/plane_master_group (2D render planes, rules 13)");
        }
    }

    // tg: code/_onclick/hud/hud.dm:408
    public virtual void inventory_update(mob viewer = null)
    {
        if (mymob == null)
            return;

        foreach (object slot_id in Dm.each(inventory_slots))
        {
            datum_inventory_slot slot = inventory_slots[slot_id] as datum_inventory_slot;
            if (slot != null)
                slot.update_inventory_slot(this, mymob);
        }
    }

    // tg: code/_onclick/hud/hud.dm:417
    public virtual void update_inventory_slot(object slot_id = null, params object[] __extra)
    {
        if (mymob == null)
            return;
        datum_inventory_slot slot = inventory_slots[slot_id] as datum_inventory_slot;
        if (slot != null)
        {
            // var/list/slot_args = list(src, mymob) + args.Copy(2); slot.update_inventory_slot(arglist(slot_args)): the
            // arguments after slot_id are this call's own extra arguments, passed on as they are
            slot.update_inventory_slot(this, mymob, __extra);
        }
    }

    // tg: code/_onclick/hud/hud.dm:425
    public virtual async DmTask update_ui_style(object new_ui_style = null)
    {
        // do nothing if overridden by a subtype or already on that style
        if (Dm.truthy(Dm.initial(this, (datum_hud p) => p.ui_style)) || Dm.eq(ui_style, new_ui_style))
            return;

        foreach (object hud_key in Dm.each(screen_objects))
        {
            atom item = screen_objects[hud_key] as atom;
            if (Dm.eq(item?.icon, ui_style))
                item.icon = new_ui_style;
        }

        ui_style = new_ui_style;
        await build_hand_slots(update_hud: TRUE);
    }

    // tg: code/_onclick/hud/hud.dm:438
    public virtual void register_reuse(atom_movable_screen reuse = null)
    {
        Dm.append(ref asset_refs_for_reuse, WEAKREF(reuse));
        if (mymob?.client != null)
            Dm.append(mymob.client.screen, reuse);
    }

    // tg: code/_onclick/hud/hud.dm:442
    public virtual void unregister_reuse(atom_movable_screen reuse = null)
    {
        Dm.remove(ref asset_refs_for_reuse, WEAKREF(reuse));
        if (mymob?.client != null)
            Dm.remove(mymob.client.screen, reuse);
    }

    // tg: code/_onclick/hud/hud.dm:446
    public virtual void update_reuse(mob show_to = null)
    {
        foreach (datum_weakref screen_ref in Dm.each_cast<datum_weakref>(asset_refs_for_reuse))
        {
            atom_movable_screen reuse = screen_ref.resolve() as atom_movable_screen;
            if (reuse == null)
            {
                Dm.remove(ref asset_refs_for_reuse, screen_ref);
                continue;
            }
            if (show_to.client != null)
                Dm.append(show_to.client.screen, reuse);
        }
    }

    /// Rebuilds our mob's hand slot screen elements
    // tg: code/_onclick/hud/hud.dm:455
    public virtual async DmTask build_hand_slots(float? update_hud = null)
    {
        update_hud ??= FALSE;
        // Clean up existing hand slot objects
        foreach (atom_movable_screen_inventory_hand hand in Dm.each<atom_movable_screen_inventory_hand>(screen_groups[HUD_GROUP_STATIC]))
            await remove_screen_object(hand, FALSE);

        for (float i = 1, i__end = length(mymob.held_items); i <= i__end; i++)
        {
            atom_movable_screen_inventory_hand hand_box = await add_screen_object(typeof(atom_movable_screen_inventory_hand), HUD_KEY_HAND_SLOT(i), HUD_GROUP_STATIC, ui_style, ui_hand_position(i)) as atom_movable_screen_inventory_hand;
            hand_box.name = Dm.str(mymob.get_held_index_name(i));
            hand_box.icon_state = Dm.text("hand_[]", mymob.held_index_to_dir(i));
            hand_box.held_index = i;
            hand_box.update_appearance();
        }

        float num_of_swaps = 0;
        foreach (atom_movable_screen_swap_hand swap_hands in Dm.each<atom_movable_screen_swap_hand>(screen_groups[HUD_GROUP_STATIC]))
            num_of_swaps += 1;

        float hand_num = 1;
        foreach (atom_movable_screen_swap_hand swap_hands in Dm.each<atom_movable_screen_swap_hand>(screen_groups[HUD_GROUP_STATIC]))
        {
            float hand_ind = RIGHT_HANDS;
            if (num_of_swaps > 1)
                hand_ind = IS_RIGHT_INDEX(hand_num) ? LEFT_HANDS : RIGHT_HANDS;
            swap_hands.screen_loc = ui_swaphand_position(mymob, hand_ind);
            hand_num += 1;
        }

        hand_num = 1;

        foreach (atom_movable_screen_drop swap_hands in Dm.each<atom_movable_screen_drop>(screen_groups[HUD_GROUP_STATIC]))
        {
            float hand_ind = LEFT_HANDS;
            if (num_of_swaps > 1)
                hand_ind = IS_LEFT_INDEX(hand_num) ? LEFT_HANDS : RIGHT_HANDS;
            swap_hands.screen_loc = ui_swaphand_position(mymob, hand_ind);
            hand_num += 1;
        }

        if (Dm.truthy(update_hud) && mymob?.hud_used == this)
            await show_hud(hud_version);
    }

    /// Handles dimming inventory slots that a mob can't equip items to in their current state
    // tg: code/_onclick/hud/hud.dm:492
    public virtual void update_locked_slots()
    {
        return;
    }

    /// Creates inventory slot screen elements based on our assigned default_inventory_slots
    // tg: code/_onclick/hud/hud.dm:496
    public virtual async DmTask create_inventory_slots()
    {
        object created_paths = default_inventory_slots;
        if (ispath(created_paths))
            created_paths = valid_subtypesof((Type)created_paths);

        foreach (object slot_type in Dm.each(created_paths))
        {
            datum_inventory_slot inv_slot = GLOB.inventory_slot_datums[slot_type] as datum_inventory_slot;
            if (!Dm.truthy(inv_slot))
            {
                stack_trace(Dm.text("[] attempted to use an invalid inventory slot: []", this, slot_type), "code/_onclick/hud/hud.dm", 504);
                continue;
            }
            await inv_slot.create_element(this);
            inventory_slots[inv_slot.slot_id] = inv_slot;
        }

        // Lets add an abstract "hands" slot to ourselves for native handling
        inventory_slots[ITEM_SLOT_HANDS] = GLOB.inventory_slot_datums[typeof(datum_inventory_slot_hands)];
        inventory_update();
    }

    // tg: code/__DEFINES/hud.dm:165  #define HUD_KEY_ITEM_SLOT(slot) "item_slot:[slot]"; :169 HUD_KEY_HAND_SLOT(slot)
    // "hand_slot:[slot]". GAP(lead): the macros have no Defines member yet: local copies (I1 cleanup).
    internal static string HUD_KEY_ITEM_SLOT(object slot) => Dm.text("item_slot:[]", slot);
    internal static string HUD_KEY_HAND_SLOT(object slot) => Dm.text("hand_slot:[]", slot);

    // tg: code/__HELPERS/hud.dm:1  /proc/ui_hand_position(i) //values based on old hand ui positions (CENTER:-/+16,SOUTH:5)
    // GAP(lead): __HELPERS/hud.dm has no GlobalProcs members yet: local copies (I1 cleanup).
    internal static string ui_hand_position(float i)
    {
        // DEVIATION(DV212): the left hand's box is left of the middle and the right hand's right of it (tg mirrors them, as on a body facing you)
        float x_off = IS_LEFT_INDEX(i) ? -1 : 0;
        float y_off = Dm.round((i - 1) / 2);
        return Dm.text("CENTER+[]:16,SOUTH+[]:5", x_off, y_off);
    }

    // tg: code/__HELPERS/hud.dm:6  /proc/ui_swaphand_position(mob/M, which = LEFT_HANDS) //values based on old swaphand ui positions (CENTER: +/-16,SOUTH+1:5)
    internal static string ui_swaphand_position(mob M, float which = LEFT_HANDS)
    {
        object x_off = (which == LEFT_HANDS) ? -1 : null;
        float y_off = Dm.round((Dm.num(M.held_items.len) - 1) / 2);
        return Dm.text("CENTER[]:16,SOUTH+[]:5", x_off, y_off + 1);
    }

    // tg: code/_onclick/hud/hud.dm:513
    public virtual async DmTask position_action(atom_movable_screen_movable_action_button button = null, object position = null)
    {
        // This is kinda a hack, I'm sorry.
        // Basically, FLOATING is never a valid position to pass into this proc. It exists as a generic marker for manually positioned buttons
        // Not as a position to target
        if (Dm.eq(position, SCRN_OBJ_FLOATING))
            return;

        if (button.location != SCRN_OBJ_DEFAULT)
            await hide_action(button);

        if (Dm.eq(position, SCRN_OBJ_DEFAULT)) // Reset to the default
        {
            button.dump_save(); // Nuke any existing saves
            await position_action(button, button.linked_action.default_button_position);
            return;
        }
        else if (Dm.eq(position, SCRN_OBJ_IN_LIST))
            await listed_actions.insert_action(button);
        else if (Dm.eq(position, SCRN_OBJ_IN_PALETTE))
            await palette_actions.insert_action(button);
        else if (Dm.eq(position, SCRN_OBJ_INSERT_FIRST))
        {
            await listed_actions.insert_action(button, index: 1);
            position = SCRN_OBJ_IN_LIST;
        }
        else // If we don't have it as a define, this is a screen_loc, and we should be floating
        {
            Dm.append(floating_actions, button);
            button.screen_loc = position as string;
            position = SCRN_OBJ_FLOATING;
            atom_movable_screen_button_palette toggle_palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
            toggle_palette.update_state();
        }

        button.location = position as string;
    }

    // tg: code/_onclick/hud/hud.dm:544
    public virtual async DmTask position_action_relative(atom_movable_screen_movable_action_button button = null, atom_movable_screen_movable_action_button relative_to = null)
    {
        if (button.location != SCRN_OBJ_DEFAULT)
            await hide_action(button);

        string sw__1 = relative_to.location;
        if (sw__1 == SCRN_OBJ_IN_LIST)
            await listed_actions.insert_action(button, listed_actions.index_of(relative_to));
        else if (sw__1 == SCRN_OBJ_IN_PALETTE)
            await palette_actions.insert_action(button, palette_actions.index_of(relative_to));
        else if (sw__1 == SCRN_OBJ_FLOATING) // If we don't have it as a define, this is a screen_loc, and we should be floating
        {
            Dm.append(floating_actions, button);
            client our_client = mymob.canon_client;
            if (!Dm.truthy(our_client))
            {
                await position_action(button, button.linked_action.default_button_position);
                return;
            }

            // Asks for a location adjacent to our button that won't overflow the map
            // button.screen_loc = get_valid_screen_location(relative_to.screen_loc, ICON_SIZE_ALL, our_client.view_size.getView())
            // (:561): reached only by dragging a button onto a floating one (mouse_drop_dragged, not declared: dragging).
            Dm.NotTranslated("code/_onclick/hud/hud.dm:561", "position_action_relative: a floating button's place next to another (get_valid_screen_location; dragging, a later milestone)");
            atom_movable_screen_button_palette toggle_palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
            toggle_palette.update_state();
        }

        button.location = relative_to.location;
    }

    /// Removes the passed in action from its current position on the screen
    // tg: code/_onclick/hud/hud.dm:570
    public virtual async DmTask hide_action(atom_movable_screen_movable_action_button button = null)
    {
        string sw__1 = button.location;
        if (sw__1 == SCRN_OBJ_DEFAULT) // Invalid
            throw Dm.CRASH("We just tried to hide an action buttion that somehow has the default position as its location, you done fucked up", "code/_onclick/hud/hud.dm", 573);
        else if (sw__1 == SCRN_OBJ_FLOATING)
        {
            Dm.remove(floating_actions, button);
            atom_movable_screen_button_palette toggle_palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
            toggle_palette.update_state();
        }
        else if (sw__1 == SCRN_OBJ_IN_LIST)
            await listed_actions.remove_action(button);
        else if (sw__1 == SCRN_OBJ_IN_PALETTE)
            await palette_actions.remove_action(button);

        button.screen_loc = null;
    }

    /// Generates visual landings for all groups that the button is not a memeber of
    // tg: code/_onclick/hud/hud.dm:589
    public virtual void generate_landings(atom_movable_screen_movable_action_button button = null)
    {
        listed_actions.generate_landing();
        palette_actions.generate_landing();
        atom_movable_screen_button_palette toggle_palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        toggle_palette.activate_landing();
    }

    /// Clears all currently visible landings
    // tg: code/_onclick/hud/hud.dm:596
    public virtual void hide_landings()
    {
        listed_actions.clear_landing();
        palette_actions.clear_landing();
        atom_movable_screen_button_palette toggle_palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        toggle_palette.disable_landing();
    }

    // Updates any existing "owned" visuals, ensures they continue to be visible
    // tg: code/_onclick/hud/hud.dm:603
    public virtual void update_our_owner()
    {
        atom_movable_screen_button_palette toggle_palette = screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
        atom_movable_screen_palette_scroll palette_down = screen_objects[HUD_MOB_PALETTE_DOWN] as atom_movable_screen_palette_scroll;
        atom_movable_screen_palette_scroll palette_up = screen_objects[HUD_MOB_PALETTE_UP] as atom_movable_screen_palette_scroll;

        toggle_palette.refresh_owner();
        palette_down.refresh_owner();
        palette_up.refresh_owner();

        listed_actions.update_landing();
        palette_actions.update_landing();
    }

    /// Ensures all of our buttons are properly within the bounds of our client's view, moves them if they're not
    // tg: code/_onclick/hud/hud.dm:616
    public virtual void view_audit_buttons()
    {
        object our_view = mymob?.canon_client?.view;
        if (!Dm.truthy(our_view))
            return;
        // listed_actions.check_against_view(), palette_actions.check_against_view() and the floating buttons' offsets
        // (:620-625): fitting the buttons into a changed client view (view_to_pixels, screen_loc_to_offset,
        // offset_to_screen_loc are not declared); the view is a fixed 15 x 15 until the view (M9).
        Dm.Deferred("code/_onclick/hud/hud.dm:620", "view_audit_buttons: the action groups' and floating buttons' view bounds (the view, M10+)");
    }

    /// Generates and fills new action groups with our mob's current actions
    // tg: code/_onclick/hud/hud.dm:628
    public virtual async DmTask build_action_groups()
    {
        listed_actions = await Dm.new_async(typeof(datum_action_group_listed), this) as datum_action_group_listed;
        palette_actions = await Dm.new_async(typeof(datum_action_group_palette), this) as datum_action_group_palette;
        floating_actions = Dm.list();
        foreach (datum_action action in Dm.each_cast<datum_action>(mymob.actions))
        {
            atom_movable_screen_movable_action_button button = action.viewers[this] as atom_movable_screen_movable_action_button;
            if (!Dm.truthy(button))
                await action.ShowTo(mymob);
            else
                await position_action(button, button.location);
        }
    }
}

// tg: code/_onclick/hud/hud.dm:275
public partial class mob : atom_movable
{
    ///Creates the mob's visible HUD, returns FALSE if it can't, TRUE if it did.
    // tg: code/_onclick/hud/hud.dm:275
    public virtual async DmTask<object> create_mob_hud()
    {
        if (!Dm.truthy(client) || Dm.truthy(hud_used))
            return FALSE;
        await set_hud_used(await Dm.new_async(hud_type, this) as datum_hud);
        update_sight();
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_HUD_CREATED]))
            this._SendSignal(COMSIG_MOB_HUD_CREATED, Dm.list(this));
        return TRUE;
    }

    // tg: code/_onclick/hud/hud.dm:283
    public virtual async DmTask set_hud_used(datum_hud new_hud = null)
    {
        hud_used = new_hud;
        await new_hud.build_action_groups();
    }
}

// tg: code/_onclick/hud/hud.dm:401
[DmPath("/datum/hud/human")]
public partial class datum_hud_human : datum_hud
{
    // tg: code/_onclick/hud/hud.dm:401
    public override async DmTask<object> show_hud(float? version = null, mob viewmob = null)
    {
        version ??= 0;
        object dot = null;
        dot = await base.show_hud(version, viewmob);
        if (!Dm.truthy(dot))
            return dot;
        mob screenmob = (viewmob is var or__1 && Dm.truthy(or__1) ? or__1 : mymob);
        inventory_update(screenmob);
        return dot;
    }
}
