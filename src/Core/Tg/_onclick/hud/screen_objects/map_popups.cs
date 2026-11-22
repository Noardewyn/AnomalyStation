// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/map_popups.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M10 slice D, D268; the popup windows silicons SI4, D326): the screen objects of a secondary map (the camera console's)
// and the client's map registry. A popup window (create_popup's BYOND skin clone) is a floating window of the 3D client with a second 3D
// view of its map: the server lists it in client.dm_popups and the replicator sends the list (DEVIATION(DV387)).

// tg: code/_onclick/hud/screen_objects/map_popups.dm:19
public partial class atom_movable_screen : atom_movable
{
    /**
     * Sets screen_loc of this screen object, in form of point coordinates,
     * with optional pixel offset (px, py).
     *
     * If applicable, "assigned_map" has to be assigned before this proc call.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:19
    public virtual void set_position(object x = null, object y = null, object px = null, object py = null)
    {
        px ??= 0;
        py ??= 0;
        if (Dm.truthy(assigned_map))
            screen_loc = Dm.text("[]:[]:[],[]:[]", assigned_map, x, px, y, py);
        else
            screen_loc = Dm.text("[]:[],[]:[]", x, px, y, py);
    }

    /**
     * Sets screen_loc to fill a rectangular area of the map.
     *
     * If applicable, "assigned_map" has to be assigned before this proc call.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:30
    public virtual void fill_rect(object x1 = null, object y1 = null, object x2 = null, object y2 = null)
    {
        if (Dm.truthy(assigned_map))
            screen_loc = Dm.text("[]:[],[] to [],[]", assigned_map, x1, y1, x2, y2);
        else
            screen_loc = Dm.text("[],[] to [],[]", x1, y1, x2, y2);
    }
}

public partial class client
{
    /**
     * Registers screen obj with the client, which makes it visible on the
     * assigned map, and becomes a part of the assigned map's lifecycle.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:40
    public virtual void register_map_obj(atom_movable_screen screen_obj = null)
    {
        if (!Dm.truthy(screen_obj.assigned_map))
            throw Dm.CRASH(Dm.text("Can't register [] without 'assigned_map' property.", screen_obj), "code/_onclick/hud/screen_objects/map_popups.dm", 42);
        if (!Dm.truthy(screen_maps[screen_obj.assigned_map]))
            screen_maps[screen_obj.assigned_map] = Dm.list();
        // NOTE: Possibly an expensive operation
        DmList screen_map = screen_maps[screen_obj.assigned_map] as DmList;
        Dm.combine(ref screen_map, screen_obj);
        DmList on_screen = screen; // a list: changed in place
        Dm.combine(ref on_screen, screen_obj);
    }

    /**
     * Clears the map of registered screen objects.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:53
    public virtual object clear_map(object map_name = null)
    {
        if (!Dm.truthy(map_name) || !Dm.truthy(screen_maps[map_name]))
            return FALSE;
        foreach (atom_movable_screen screen_obj in Dm.each<atom_movable_screen>((screen_maps[map_name] as DmList).Copy()))
        {
            Dm.remove_at(screen_maps, map_name, screen_obj);
            DmList on_screen = screen; // a list: changed in place
            Dm.remove(ref on_screen, screen_obj);
            if (Dm.truthy(screen_obj.del_on_map_removal))
                qdel(screen_obj);
        }
        Dm.remove(ref screen_maps, map_name);
        return null;
    }

    /**
     * Clears all the maps of registered screen objects.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:66
    public virtual void clear_all_maps()
    {
        foreach (object map_name in Dm.each(screen_maps.Copy()))
            clear_map(map_name);
    }

    /**
     * Creates a popup window with a basic map element in it, without any
     * further initialization.
     *
     * Ratio is how many pixels by how many pixels (keep it simple).
     *
     * Returns a map name.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:78
    public virtual string create_popup(object name = null, object title = null, object ratiox = null, object ratioy = null)
    {
        ratiox ??= 100;
        ratioy ??= 100;
        // DEVIATION(DV387): winclone(src, "popupwindow", name), the window's title, size and on-close command, winshow and the map
        // element's winset are one record the client opens as a floating window (its close runs handle_popup_close, as on-close)
        dm_popups[Dm.str(name)] = Dm.list(Dm.str(title), Dm.num(ratiox), Dm.num(ratioy));
        dm_popups_serial++;
        return Dm.text("[]_map", name);
    }

    /**
     * Create the popup, and get it ready for generic use by giving
     * it a background.
     *
     * Width and height are multiplied by 64 by default.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:103
    public virtual object setup_popup(object popup_name = null, object width = null, object height = null, object tilesize = null, object title = null, object bg_icon = null)
    {
        width ??= 9;
        height ??= 9;
        tilesize ??= 2;
        if (!Dm.truthy(popup_name))
            return null;
        clear_map(Dm.text("[]_map", popup_name));
        float x_value = ICON_SIZE_X * Dm.num(tilesize) * Dm.num(width);
        float y_value = ICON_SIZE_Y * Dm.num(tilesize) * Dm.num(height);
        string map_name = create_popup(popup_name, title, x_value, y_value);

        atom_movable_screen_background background = Dm.@new<atom_movable_screen_background>();
        background.assigned_map = map_name;
        background.fill_rect(1, 1, width, height);
        if (Dm.truthy(bg_icon))
            background.icon_state = Dm.str(bg_icon);
        register_map_obj(background);

        return map_name;
    }

    /**
     * Closes a popup.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:124
    public virtual void close_popup(object popup = null)
    {
        // winshow(src, popup, 0): DEVIATION(DV387), the record leaves the client's list
        if (Dm.truthy(dm_popups[Dm.str(popup)]))
        {
            Dm.remove(ref dm_popups, Dm.str(popup));
            dm_popups_serial++;
        }
        handle_popup_close(popup);
    }

    /**
     * When the popup closes in any way (player or proc call) it calls this.
     */
    // tg: code/_onclick/hud/screen_objects/map_popups.dm:131  GAME_VERB_NATIVE(/client, handle_popup_close, "handle popup close", null, window_id as text):
    // the verb (the client's window, ClientSeam.ClosePopup) and its body
    public virtual void handle_popup_close(object window_id = null)
    {
        __gvb_handle_popup_close(window_id);
    }

    // tg: code/_onclick/hud/screen_objects/map_popups.dm:131  the body
    public virtual void __gvb_handle_popup_close(object window_id = null)
    {
        clear_map(Dm.text("[]_map", window_id));
        if (Dm.truthy(_listen_lookup?[COMSIG_POPUP_CLEARED]))
            _SendSignal(COMSIG_POPUP_CLEARED, Dm.list(this, window_id));
    }
}
