// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/painter/paintable_decals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (engineering E14: the decal painter's categories, GLOB.paintable_decals) but the sprite sheet: the asset
// /datum/asset/spritesheet_batched/decals and the categories' generate_*_spritesheet_icon procs, which build its preview pictures
// with uni_icon, are not run (live-partial.txt E14): the server sends no assets and the client draws the sheet's classes from its
// own copy of the .dmi files (DV44, the client's TguiSpritesheets). The decal datums (/datum/paintable_decal) are var-only, read
// through `::`. The var blocks are generated.

/**
 * ## Paintable Decal Category
 *
 * Holds a bunch of information the decal painter uses to determine what it can and can't paint, and how to paint it.
 */
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:6
public partial class datum_paintable_decal_category : datum
{
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:153  (proc-level var/static: rules 10.6, TgState fields)
    private static ref regex get_decal_info__rgba_regex => ref TgState.Current.datum_paintable_decal_category__get_decal_info__rgba_regex;

    /// Returns a key for the spritesheet icon, used to avoid duplicates
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:32
    public virtual object spritesheet_key(object dir = null, object state = null, object color = null)
    {
        return Dm.text("[]_[]_[]", state, dir, replacetext(color, "#", ""));
    }

    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:39, :54, :63  generate_all_spritesheet_icons,
    // generate_independent_decal_spritesheet_icons, generate_colored_decal_spritesheet_icon: live-partial (the sprite sheet, DV44).

    /// Constructs and returns this category's UI data
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:80
    public virtual object get_ui_data()
    {
        if (Dm.truthy(cached_category_data))
            return cached_category_data.Copy();

        cached_category_data = Dm.list();

        cached_category_data["decal_list"] = Dm.list();
        cached_category_data["color_list"] = Dm.list();
        cached_category_data["dir_list"] = Dm.list();

        foreach (Type decal_type in Dm.each_cast<Type>(subtypesof(paintable_decal_type)))
        {
            object name = Dm.initial(decal_type, (datum_paintable_decal p) => p.name);
            object state = Dm.initial(decal_type, (datum_paintable_decal p) => p.icon_state);
            if (!Dm.truthy(name) || !Dm.truthy(state))
                continue;

            Dm.append(cached_category_data["decal_list"] as DmList, Dm.list(Dm.list(
                Dm.kv("name", name),
                Dm.kv("icon_state", state),
                Dm.kv("directional", Dm.initial(decal_type, (datum_paintable_decal p) => p.directional)))));
        }

        foreach (object color in Dm.each(possible_colors))
        {
            Dm.append(cached_category_data["color_list"] as DmList, Dm.list(Dm.list(
                Dm.kv("name", color),
                Dm.kv("color", possible_colors[color]))));
        }

        foreach (object dirname in Dm.each(dir_list))
        {
            Dm.append(cached_category_data["dir_list"] as DmList, Dm.list(Dm.list(
                Dm.kv("name", dirname),
                Dm.kv("dir", dir_list[dirname]))));
        }

        return cached_category_data.Copy();
    }

    /// Checks if the passed icon state is one of this category's decals
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:119
    public virtual object is_state_valid(object state = null)
    {
        // Ui data has all icon states so let's just piggyback off of that
        foreach (DmList decal_data in Dm.each_cast<DmList>((get_ui_data() as DmList)["decal_list"]))
        {
            if (Dm.eq(decal_data["icon_state"], state))
                return TRUE;
        }
        return FALSE;
    }

    /// Checks if the passed direction is one of this category's directions
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:128
    public virtual object is_dir_valid(object dir = null)
    {
        foreach (object dirname in Dm.each(dir_list))
        {
            if (Dm.eq(dir_list[dirname], dir))
                return TRUE;
        }
        return FALSE;
    }

    /// Checks if the passed color is one of this category's colors
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:136
    public virtual object is_color_valid(object color = null)
    {
        foreach (object colorname in Dm.each(possible_colors))
        {
            if (Dm.eq(possible_colors[colorname], color))
                return TRUE;
        }
        return FALSE;
    }

    /**
     * Used by the decal painter to modify the state of the decal based on the... state.
     */
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:146
    public virtual object get_decal_info(object state = null, object color = null, object dir = null)
    {
        // Special case for 8-dir sprites. Rather than add support for both 4-dir and 8-dir,
        // 8-dir are affixed with "__8" at the end of the icon state. Then we handle it in this proc.
        if (copytext(state, -3) == "__8")
        {
            state = splicetext(state, -3, 0, "");
            dir = turn(dir, 45);
        }

        get_decal_info__rgba_regex ??= Dm.@new<regex>(@"(#[0-9a-fA-F]{6})([0-9a-fA-F]{2})");
        object alpha = default_alpha;
        // Special case for RGBA colors
        if (Dm.truthy(get_decal_info__rgba_regex.Find(color)))
        {
            color = get_decal_info__rgba_regex.group[1];
            alpha = text2num(get_decal_info__rgba_regex.group[2], 16);
        }

        return Dm.list(
            Dm.kv(Dm.text("[]", DECAL_INFO_ICON_STATE), state),
            Dm.kv(Dm.text("[]", DECAL_INFO_DIR), dir),
            Dm.kv(Dm.text("[]", DECAL_INFO_COLOR), color),
            Dm.kv(Dm.text("[]", DECAL_INFO_ALPHA), alpha));
    }
}

// Basic tile decals
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:168
public partial class datum_paintable_decal_category_tile : datum_paintable_decal_category
{
}

// Tile trimlines. The var-only categories have a hand-written part so that init_subtypes, which skips the generated-only types
// (DV25, __HELPERS/global_lists.cs), makes them as tg does.
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:190
public partial class datum_paintable_decal_category_tile_trimline : datum_paintable_decal_category_tile
{
}

// Generic warning stripes
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:195
public partial class datum_paintable_decal_category_warning : datum_paintable_decal_category
{
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:204  generate_colored_decal_spritesheet_icon: live-partial (the sprite sheet, DV44).

    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:211
    public override object get_decal_info(object state = null, object color = null, object dir = null)
    {
        // Special case. Default warning stripes are yellow, so don't append anything if passed yellow
        if (Dm.eq(color, "yellow"))
            color = "";

        return Dm.list(
            Dm.kv(Dm.text("[]", DECAL_INFO_ICON_STATE), Dm.text("[][][]", state, Dm.truthy(color) ? "_" : "", color)),
            Dm.kv(Dm.text("[]", DECAL_INFO_DIR), dir),
            Dm.kv(Dm.text("[]", DECAL_INFO_COLOR), color),
            Dm.kv(Dm.text("[]", DECAL_INFO_ALPHA), default_alpha));
    }
}

// Plain colored siding
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:224
public partial class datum_paintable_decal_category_siding : datum_paintable_decal_category
{
}

// Sidings which are not colored / have a specific pattern, texture, etc
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:244
public partial class datum_paintable_decal_category_normal_siding : datum_paintable_decal_category
{
}

// Plating sidings and all color variations
// tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:252
public partial class datum_paintable_decal_category_plating : datum_paintable_decal_category
{
}

public partial class datum_controller_global_vars : datum_controller
{
    /// Global list of all paintable decal categories singletons
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:264  GLOBAL_LIST_INIT(paintable_decals, init_subtypes(/datum/paintable_decal_category))
    public DmList paintable_decals;

    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:264  (GLOBAL_MANAGED(paintable_decals, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpaintable_decals()
    {
        paintable_decals = init_subtypes(typeof(datum_paintable_decal_category));
        Dm.append(ref gvars_datum_init_order, "paintable_decals");
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:153  get_decal_info's var/static/regex/rgba_regex
    internal regex datum_paintable_decal_category__get_decal_info__rgba_regex;
}
