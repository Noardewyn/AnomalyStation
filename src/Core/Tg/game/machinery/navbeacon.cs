// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/navbeacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (2026-10-18 the init and the lists, gaps package C3 the rest: the panel, the deconstruction and the NavBeacon window). The var block is generated.

// tg: code/game/machinery/navbeacon.dm:4
public partial class obj_machinery_navbeacon : obj_machinery
{
    // tg: code/game/machinery/navbeacon.dm:39
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        original_location = location;

        set_codes();

        glob_lists_register(init: TRUE);

        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE));
        return dot;
    }

    // tg: code/game/machinery/navbeacon.dm:50
    public override object Destroy(float? force = null)
    {
        glob_lists_deregister();
        return base.Destroy(force);
    }

    // tg: code/game/machinery/navbeacon.dm:54
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(GLOB.navbeacons[Dm.text("[]", old_turf?.z)]))
            Dm.remove_at(GLOB.navbeacons, Dm.text("[]", old_turf?.z), this);
        if (Dm.truthy(GLOB.navbeacons[Dm.text("[]", new_turf?.z)]))
            Dm.append_at(GLOB.navbeacons, Dm.text("[]", new_turf?.z), this);
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    ///Set the transponder codes assoc list from codes_txt during initialization, or during reset
    // tg: code/game/machinery/navbeacon.dm:70
    public virtual void set_codes()
    {
        codes = Dm.list();

        if (!Dm.truthy(codes_txt))
            return;

        DmList entries = splittext(codes_txt, ";"); // entries are separated by semicolons

        foreach (object entry in Dm.each(entries))
        {
            float index = findtext(entry, "="); // format is "key=value"
            if (Dm.truthy(index))
            {
                string key = copytext(entry, 1, index);
                // tg: entry[index] is the one character "=" (a text index, BYOND 516)
                string val = copytext(entry, index + length(copytext(entry, index, index + 1)));
                codes[key] = val;
            }
            else
                codes[entry] = Dm.text("[]", TRUE);
        }
    }

    ///Removes the nav beacon from the global beacon lists
    // tg: code/game/machinery/navbeacon.dm:89
    public virtual void glob_lists_deregister()
    {
        if (Dm.truthy(GLOB.navbeacons[Dm.text("[]", z)]))
            Dm.remove_at(GLOB.navbeacons, Dm.text("[]", z), this); //Remove from beacon list, if in one.
        Dm.remove(GLOB.deliverybeacons, this);
        Dm.remove(GLOB.deliverybeacontags, location);
    }

    ///Registers the navbeacon to the global beacon lists
    // tg: code/game/machinery/navbeacon.dm:96
    public virtual void glob_lists_register(float? init = null)
    {
        init ??= FALSE;
        if (!Dm.truthy(init))
            glob_lists_deregister();
        if (!Dm.truthy(codes))
            return;
        if (Dm.truthy(codes[NAVBEACON_PATROL_MODE]))
        {
            if (!Dm.truthy(GLOB.navbeacons[Dm.text("[]", z)]))
                GLOB.navbeacons[Dm.text("[]", z)] = Dm.list();
            Dm.append_at(GLOB.navbeacons, Dm.text("[]", z), this); //Register with the patrol list!
        }
        if (Dm.truthy(codes[NAVBEACON_DELIVERY_MODE]))
        {
            Dm.append(GLOB.deliverybeacons, this);
            Dm.append(GLOB.deliverybeacontags, location);
        }
    }

    // tg: code/game/machinery/navbeacon.dm:118
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, panel_open);
        return dot;
    }

    // tg: code/game/machinery/navbeacon.dm:61
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        if (loc is not turf_open_floor floor || !isfloorturf(loc))
            return;
        await floor.remove_tile(null, silent: TRUE, make_tile: TRUE, force_plating: TRUE);
    }

    // tg: code/game/machinery/navbeacon.dm:109
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/navbeacon.dm:112
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open) && Dm.truthy(cover_locked))
        {
            balloon_alert(user, "hatch locked!");
            return ITEM_INTERACT_BLOCKING;
        }
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/navbeacon.dm:122
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        turf our_turf = (turf)loc;
        if (our_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
            return ITEM_INTERACT_BLOCKING; // prevent interaction when T-scanner revealed

        if (!Dm.truthy(tool.GetID()))
            return NONE;
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, "panel open!");
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, "access denied");
            return ITEM_INTERACT_BLOCKING;
        }
        controls_locked = Dm.num(!Dm.truthy(controls_locked));
        balloon_alert(user, Dm.text("controls []", Dm.truthy(controls_locked) ? "locked" : "unlocked"));
        SStgui.update_uis(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/navbeacon.dm:140
    public override async DmTask<object> attack_ai(mob user = null)
    {
        await interact(user);
        return null;
    }

    // tg: code/game/machinery/navbeacon.dm:143
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return null;
    }

    // tg: code/game/machinery/navbeacon.dm:146
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);

        turf our_turf = (turf)loc;
        if (our_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
            return dot; // prevent intraction when T-scanner revealed

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "NavBeacon");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/navbeacon.dm:159
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList controls = Dm.list();

        controls["location"] = location;
        controls["patrol_enabled"] = Dm.truthy(codes[NAVBEACON_PATROL_MODE]) ? TRUE : FALSE;
        controls["patrol_next"] = codes[NAVBEACON_PATROL_NEXT];
        controls["delivery_enabled"] = Dm.truthy(codes[NAVBEACON_DELIVERY_MODE]) ? TRUE : FALSE;
        controls["delivery_direction"] = dir2text(text2num(codes[NAVBEACON_DELIVERY_DIRECTION]));
        controls["cover_locked"] = cover_locked;

        data["locked"] = controls_locked;
        data["siliconUser"] = Dm.num(HAS_SILICON_ACCESS(user));
        data["controls"] = controls;

        return data;
    }

    // tg: code/game/machinery/navbeacon.dm:176
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList static_controls = Dm.list();
        DmList direction_options = TgState.Current.obj_machinery_navbeacon__direction_options ??= Dm.list("none", dir2text(EAST), dir2text(NORTH), dir2text(SOUTH), dir2text(WEST));

        static_controls["direction_options"] = direction_options;
        static_controls["has_codes"] = Dm.truthy(codes_txt) ? TRUE : FALSE;

        data["static_controls"] = static_controls;
        return data;
    }

    // tg: code/game/machinery/navbeacon.dm:187
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = ui.user;

        if (Dm.eq(action, "lock") && Dm.truthy(await allowed__proc(user)))
        {
            controls_locked = Dm.num(!Dm.truthy(controls_locked));
            return TRUE;
        }

        if (Dm.truthy(controls_locked) && !HAS_SILICON_ACCESS(user))
            return dot;

        switch (action as string)
        {
            case "reset_codes":
                glob_lists_deregister();
                location = original_location;
                set_codes();
                glob_lists_register();
                return TRUE;
            case "toggle_cover":
                cover_locked = Dm.num(!Dm.truthy(cover_locked));
                return TRUE;
            case "toggle_patrol":
                toggle_code(NAVBEACON_PATROL_MODE);
                return TRUE;
            case "toggle_delivery":
                toggle_code(NAVBEACON_DELIVERY_MODE);
                return TRUE;
            case "set_location":
            {
                object input_text = await tgui_input_text(user, "Enter the beacon's location tag", "Beacon Location", location, max_length: 20);
                if (!Dm.truthy(input_text) || Dm.eq(location, input_text))
                    return dot;
                glob_lists_deregister();
                location = input_text as string;
                glob_lists_register();
                return TRUE;
            }
            case "set_patrol_next":
            {
                object next_patrol = codes[NAVBEACON_PATROL_NEXT];
                object input_text = await tgui_input_text(user, "Enter the tag of the next patrol location", "Beacon Location", next_patrol, max_length: 20);
                if (!Dm.truthy(input_text) || Dm.eq(location, input_text))
                    return dot;
                codes[NAVBEACON_PATROL_NEXT] = input_text;
                return TRUE;
            }
            case "set_delivery_direction":
                codes[NAVBEACON_DELIVERY_DIRECTION] = Dm.text("[]", text2dir(@params["direction"]));
                return TRUE;
        }
        return dot;
    }

    ///Adds or removes a specific code
    // tg: code/game/machinery/navbeacon.dm:236
    public virtual void toggle_code(object code = null)
    {
        if (Dm.truthy(codes[code]))
            codes.Remove(code);
        else
            codes[code] = Dm.text("[]", TRUE);
        glob_lists_register();
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList obj_machinery_navbeacon__direction_options;
}
