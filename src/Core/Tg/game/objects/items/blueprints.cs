// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/blueprints.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (engineering E14): the station blueprints, the cyborgs' schematics, the golems' land claim and the cerulean prints,
// with tg's Blueprints window: create or expand an area from a sealed room (__HELPERS/areas.dm create_area), rename the area, the
// shuttle docking toggle, the wire colour legend (GLOB.wire_color_directory) and the structural view (the roundstart images of the
// pipes, cables and disposal pipes around the viewer in their client.images, turf.dm add_blueprints). Only the cerulean prints'
// HSL colour filter on the renamed area's turfs is deferred (xenobiology). The var block is generated.

/**
 * Blueprints
 * Used to see the wires of machines on the station, the roundstart layout of pipes/cables/tubes,
 * as well as allowing you to rename existing areas and create new ones.
 * Used by the station, cyborgs, and golems.
 */
// tg: code/game/objects/items/blueprints.dm:19
public partial class obj_item_blueprints : obj_item
{
    // tg: code/game/objects/items/blueprints.dm:1-11  the file-local #defines (#undef'd at :257-262, rules 11.4)
    ///The area is a "Station" area, showing no special text.
    private const float AREA_STATION = 1;
    ///The area is in outdoors (lavaland/icemoon/jungle/space), therefore unclaimed territories.
    private const float AREA_OUTDOORS = 2;
    ///The area is special (shuttles/centcom), therefore can't be claimed.
    private const float AREA_SPECIAL = 3;
    ///The blueprints are currently reading the list of all wire datums.
    private const string LEGEND_VIEWING_LIST = "watching_list";
    ///The blueprints are on the main page.
    private const string LEGEND_OFF = "off";

    // tg: code/game/objects/items/blueprints.dm:197  (proc-level var/static: rules 10.6, TgState fields)
    private static ref DmList get_area_type__special_areas => ref TgState.Current.obj_item_blueprints__get_area_type__special_areas;

    // tg: code/game/objects/items/blueprints.dm:46
    public override object Destroy(float? force = null)
    {
        clear_viewer();
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/blueprints.dm:50
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        clear_viewer();
        legend_viewing = LEGEND_OFF;
        return dot;
    }

    // tg: code/game/objects/items/blueprints.dm:55
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Blueprints", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/blueprints.dm:61
    public override object ui_state(mob user = null)
    {
        return GLOB.inventory_state;
    }

    // tg: code/game/objects/items/blueprints.dm:64
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        object sw__1 = get_area_type(user);
        if (Dm.eq(sw__1, AREA_OUTDOORS))
            data["area_notice"] = I18n.T("You are in unclaimed territory.");
        else if (Dm.eq(sw__1, AREA_SPECIAL))
            data["area_notice"] = I18n.T("This area has no notes.");
        else
        {
            area current_area = get_area(user) as area;
            data["area_notice"] = I18n.T("You are now in \\the []", current_area.name);
        }
        area area_inside_of = get_area(user) as area;
        data["area_name"] = html_encode(area_inside_of.name);
        data["area_allows_shuttle_docking"] = area_inside_of.allow_shuttle_docking;
        data["legend"] = legend_viewing;
        data["viewing"] = viewing != null ? TRUE : FALSE;
        data["wire_data"] = Dm.list();
        if (legend_viewing != LEGEND_VIEWING_LIST && legend_viewing != LEGEND_OFF)
        {
            foreach (object device in Dm.each(GLOB.wire_color_directory))
            {
                if (Dm.text("[]", device) != legend_viewing)
                    continue;
                data["wires_name"] = GLOB.wire_name_directory[device];
                DmList device_colors = GLOB.wire_color_directory[device] as DmList;
                foreach (object individual_color in Dm.each(device_colors))
                {
                    object wire_name = device_colors[individual_color];
                    if (Dm.truthy(findtext(wire_name, WIRE_DUD_PREFIX))) //don't show duds
                        continue;
                    Dm.append(data["wire_data"] as DmList, Dm.list(Dm.list(
                        Dm.kv("color", individual_color),
                        Dm.kv("message", wire_name))));
                }
            }
        }
        return data;
    }

    // tg: code/game/objects/items/blueprints.dm:95
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["legend_viewing_list"] = LEGEND_VIEWING_LIST;
        data["legend_off"] = LEGEND_OFF;
        data["fluff_notice"] = fluffnotice;
        data["station_name"] = station_name();
        data["wire_devices"] = Dm.list();
        foreach (object wireset in Dm.each(GLOB.wire_color_directory))
        {
            Dm.append(data["wire_devices"] as DmList, Dm.list(Dm.list(
                Dm.kv("name", GLOB.wire_name_directory[wireset]),
                Dm.kv("ref", wireset))));
        }
        return data;
    }

    // tg: code/game/objects/items/blueprints.dm:109
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(Dm.bitor(Dm.bitor(NEED_LITERACY, NEED_DEXTERITY), NEED_HANDS), ALLOW_RESTING))))
            return TRUE;

        switch (action)
        {
            case "create_area":
                if (Dm.truthy(in_use))
                    return dot;
                in_use = TRUE;
                await create_area(user, new_area_type);
                in_use = FALSE;
                break;
            case "edit_area":
                if (!Dm.eq(get_area_type(user), AREA_STATION))
                    return dot;
                if (Dm.truthy(in_use))
                    return dot;
                in_use = TRUE;
                await edit_area(user);
                in_use = FALSE;
                break;
            case "toggle_allow_shuttle_docking":
            {
                if (!Dm.eq(get_area_type(user), AREA_STATION))
                    return dot;
                area area = get_area(this) as area;
                area.allow_shuttle_docking = Dm.truthy(area.allow_shuttle_docking) ? FALSE : TRUE;
                break;
            }
            case "exit_legend":
                legend_viewing = LEGEND_OFF;
                break;
            case "view_legend":
                legend_viewing = LEGEND_VIEWING_LIST;
                break;
            case "view_wireset":
            {
                object setting_wireset = @params["view_wireset"];
                foreach (object device in Dm.each(GLOB.wire_color_directory))
                {
                    if (Dm.eq(Dm.text("[]", device), setting_wireset)) //I know... don't change it...
                    {
                        legend_viewing = Dm.str(setting_wireset);
                        return TRUE;
                    }
                }
                break;
            }
            case "view_blueprints":
                playsound(this, Dm.resource("sound/items/paper_flip.ogg"), 40, TRUE);
                user.balloon_alert_to_viewers(I18n.T("flips blueprints over"));
                set_viewer(user);
                break;
            case "hide_blueprints":
                playsound(this, Dm.resource("sound/items/paper_flip.ogg"), 40, TRUE);
                user.balloon_alert_to_viewers(I18n.T("flips blueprints over"));
                clear_viewer();
                break;
            case "refresh":
                playsound(this, Dm.resource("sound/items/paper_flip.ogg"), 40, TRUE);
                clear_viewer();
                set_viewer(user);
                break;
        }
        return TRUE;
    }

    /**
     * Sets the user's client as the person viewing blueprint data, and builds blueprint data
     * around the user.
     * Args:
     * - user: The person who's client we're giving images to.
     */
    // tg: code/game/objects/items/blueprints.dm:168
    public virtual void set_viewer(mob user = null)
    {
        if (user == null || user.client == null)
            return;
        if (viewing != null)
            clear_viewer();
        viewing = user.client;
        showing = get_blueprint_data(get_turf(viewing.eye ?? user) as turf, viewing.view) as DmList;
        Dm.combine(viewing.images, showing);
    }

    /**
     * Clears the client we're showig images to and deletes the images of blueprint data
     * we made to show them.
     */
    // tg: code/game/objects/items/blueprints.dm:181
    public virtual void clear_viewer()
    {
        if (viewing != null)
        {
            Dm.remove(viewing.images, showing);
            viewing = null;
        }
        showing.Cut();
    }

    /**
     * Gets the area type the user is currently standing in.
     * Returns: AREA_STATION, AREA_OUTDOORS, or AREA_SPECIAL
     * Args:
     * - user: The person we're getting the area of to check if it's a special area.
     */
    // tg: code/game/objects/items/blueprints.dm:193
    public virtual object get_area_type(mob user = null)
    {
        area area_checking = get_area(user) as area;
        if (Dm.truthy(area_checking.outdoors))
            return AREA_OUTDOORS;
        get_area_type__special_areas ??= typecacheof(Dm.list(
            typeof(area_shuttle),
            typeof(area_centcom),
            typeof(area_centcom_asteroid),
            typeof(area_centcom_tdome),
            typeof(area_centcom_wizard_station),
            typeof(area_misc_hilbertshotel),
            typeof(area_misc_hilbertshotelstorage))) as DmList;
        if (Dm.@in(area_checking.type, get_area_type__special_areas))
            return AREA_SPECIAL;
        return AREA_STATION;
    }

    /**
     * edit_area
     * Takes input from the player and renames the area the blueprints are currently in.
     */
    // tg: code/game/objects/items/blueprints.dm:214
    public virtual async DmTask<object> edit_area(mob user = null)
    {
        area area_editing = get_area(this) as area;
        string prevname = Dm.text("[]", area_editing.name);
        object new_name = await tgui_input_text(user, I18n.T("New area name"), I18n.T("Area Creation"), max_length: MAX_NAME_LEN);
        if (new_name == null || !Dm.truthy(Dm.length(new_name)) || Dm.eq(new_name, prevname))
            return null;

        rename_area(area_editing, new_name);
        user.balloon_alert(user, I18n.T("area renamed to []", new_name));
        user.log_message(Dm.text("has renamed [] to []", prevname, new_name), LOG_GAME);
        return TRUE;
    }
}

///Slime blueprints - Makes areas colored and compatible with xenobiology camera consoles, one time use.
// tg: code/game/objects/items/blueprints.dm:240
public partial class obj_item_blueprints_slime : obj_item_blueprints
{
    // tg: code/game/objects/items/blueprints.dm:246
    public override async DmTask<object> edit_area(mob user = null)
    {
        object dot = await base.edit_area(user);
        area area = get_area(this) as area;
        // :249-253: the turfs of the area lose their washable colour and take color_transition_filter("#2956B2") at FIXED_COLOUR_PRIORITY
        Dm.Deferred("code/game/objects/items/blueprints.dm:249", "edit_area: the cerulean prints' HSL colour filter on the area's turfs (color_transition_filter; xenobiology)");
        Dm.combine(ref area.area_flags, XENOBIOLOGY_COMPATIBLE);
        qdel(this);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/blueprints.dm:197  /obj/item/blueprints/get_area_type's var/static/list/special_areas
    internal DmList obj_item_blueprints__get_area_type__special_areas;
}
