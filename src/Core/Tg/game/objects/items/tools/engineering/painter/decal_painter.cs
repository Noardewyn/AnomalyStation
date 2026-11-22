// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/painter/decal_painter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (engineering E14): the decal painter paints the chosen decal of GLOB.paintable_decals on a floor through
// /datum/element/decal (the turf's overlay, drawn with the floor tile), with tg's DecalPainter window; the extreme painter; the
// cyborg's painter (its cell charge since silicons SI2). The var blocks are generated.

// tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:1
public partial class obj_item_airlock_painter_decal : obj_item_airlock_painter
{
    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_category(GLOB.paintable_decals[1] as datum_paintable_decal_category);
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:26
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isfloorturf(interacting_with) && Dm.truthy(use_paint(user)))
        {
            await paint_floor(interacting_with as turf_open_floor);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    /**
     * Actually add current decal to the floor.
     *
     * Responsible for actually adding the element to the turf for maximum flexibility.area
     * Can be overridden for different decal behaviors.
     * Arguments:
     * * target - The turf being painted to
    */
    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:40
    public virtual async DmTask paint_floor(turf_open_floor target = null)
    {
        DmList decal_data = current_category.get_decal_info(
            state: selected_decal_icon_state,
            color: selected_color,
            dir: selected_dir) as DmList;

        await target.AddElement(
            typeof(datum_element_decal),
            Dm.named("_icon", Dm.resource("icons/turf/decals.dmi")),
            Dm.named("_icon_state", decal_data[DECAL_INFO_ICON_STATE]),
            Dm.named("_dir", decal_data[DECAL_INFO_DIR]),
            Dm.named("_alpha", decal_data[DECAL_INFO_ALPHA]),
            Dm.named("_color", decal_data[DECAL_INFO_COLOR]),
            Dm.named("_cleanable", FALSE));
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:57
    public virtual object set_category(datum_paintable_decal_category category = null)
    {
        current_category = category;
        selected_color = Dm.str(category.possible_colors?[category.possible_colors[1]]);
        selected_decal_icon_state = Dm.str((((category.get_ui_data() as DmList)["decal_list"] as DmList)[1] as DmList)["icon_state"]);
        return TRUE;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:63
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DecalPainter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:69
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // . += get_asset_datum(/datum/asset/spritesheet_batched/decals): the asset transport is the client's (DV44); the client
        // draws the "paintable_decals" sheet's pictures from its own copy of tg's .dmi files (TguiSpritesheets).
        return await base.ui_assets(user);
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:73
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["categories"] = Dm.list();
        foreach (datum_paintable_decal_category category in Dm.each_cast<datum_paintable_decal_category>(GLOB.paintable_decals))
        {
            DmList category_data = category.get_ui_data() as DmList;
            Dm.append(data["categories"] as DmList, Dm.list(Dm.list(
                Dm.kv("category", category.category),
                Dm.kv("decal_list", category_data["decal_list"]),
                Dm.kv("color_list", category_data["color_list"]),
                Dm.kv("dir_list", category_data["dir_list"]))));
        }

        // var/datum/asset/spritesheet_batched/icon_assets = get_asset_datum(/datum/asset/spritesheet_batched/decals): its name is
        // "paintable_decals" (paintable_decals.dm:268); the asset itself is the client's (DV44)
        data["icon_prefix"] = Dm.text("[]32x32", "paintable_decals");

        return data;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:91
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["current_decal"] = selected_decal_icon_state;
        data["current_color"] = selected_color;
        data["current_dir"] = selected_dir;
        data["current_custom_color"] = selected_custom_color;
        data["active_category"] = current_category.category;
        return data;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:100
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "select_category":
            {
                object new_category = @params["category"];
                foreach (datum_paintable_decal_category category in Dm.each_cast<datum_paintable_decal_category>(GLOB.paintable_decals))
                {
                    if (!Dm.eq(category.category, new_category))
                        continue;

                    set_category(category);
                    return TRUE;
                }
                break;
            }

            case "select_decal":
            {
                object new_state = @params["decal"];
                object new_dir = text2num(@params["dir"]);
                if (Dm.truthy(current_category.is_state_valid(new_state)))
                    selected_decal_icon_state = Dm.str(new_state);
                if (Dm.truthy(current_category.is_dir_valid(new_dir)))
                    selected_dir = Dm.num(new_dir);
                return TRUE;
            }

            case "select_color":
            {
                object new_color = @params["color"];
                if (Dm.truthy(current_category.is_color_valid(new_color)))
                    selected_color = Dm.str(new_color);
                return TRUE;
            }

            case "pick_custom_color":
                if (Dm.@in("Custom", current_category.possible_colors))
                    await pick_painting_tool_color(usr, selected_custom_color);
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:135
    public override void set_painting_tool_color(object chosen_color = null)
    {
        base.set_painting_tool_color(chosen_color);
        selected_custom_color = chosen_color;
        selected_color = Dm.str(chosen_color);
    }
}

// tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:145
public partial class obj_item_airlock_painter_decal_cyborg : obj_item_airlock_painter_decal
{
    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:149
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        mob_living_silicon_robot cyborg = user as mob_living_silicon_robot;
        if (!iscyborg(user) || cyborg.cell__obj == null)
            return dot;
        if (cyborg.cell__obj != null && Dm.num(cyborg.cell__obj.charge) > 0)
            await cyborg.cell__obj.use(0.025f * STANDARD_CELL_CHARGE);
        else if (Dm.num(cyborg.cell__obj.charge) <= 0)
        {
            balloon_alert(user, I18n.T("not enough energy!"));
            return dot;
        }
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/painter/decal_painter.dm:160
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        return CLICK_ACTION_BLOCKING;
    }
}
