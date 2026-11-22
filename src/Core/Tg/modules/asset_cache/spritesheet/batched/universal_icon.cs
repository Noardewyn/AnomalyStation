// Translated from tgstation (snapshot 1d99bd7f): code/modules/asset_cache/spritesheet/batched/universal_icon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice D4, lobby.md 8: the preferences' pictures): gags_to_universal_icon and get_display_icon_for; syndicate A3 (D316):
// get_flat_uni_icon (the antagonists' pictures; its appearance is any atom, image or appearance, Appendix A). A
// /datum/universal_icon (uni_icon, __DEFINES/assets.dm:16) is the port's /icon: its transforms (blend_color, blend_icon, crop, scale)
// are the /icon operations the client draws (D104, protocol.md 34.3), and the server sends no spritesheets (DV44).

public static partial class GlobalProcs
{
    // tg: code/modules/asset_cache/spritesheet/batched/universal_icon.dm:376
    public static icon gags_to_universal_icon(Type path = null)
    {
        object config = Dm.initial(path, "greyscale_config");
        object colors = Dm.initial(path, "greyscale_colors");
        if (!ispath(path, typeof(atom)) || !Dm.truthy(config) || !Dm.truthy(colors))
            throw Dm.CRASH(Dm.text("gags_to_universal_icon() received an invalid path of \"[]\"!", path), "code/modules/asset_cache/spritesheet/batched/universal_icon.dm", 379);
        object state = Dm.initial(path, "post_init_icon_state");
        // SSgreyscale.GetColoredIconByTypeUniversalIcon(config, colors, state): the GAGS icon of those colours, limited to the state
        return Dm.icon(SSgreyscale.GetColoredIconByType(config, colors), Dm.truthy(state) ? state : Dm.initial(path, "icon_state"));
    }

    /// Gets the relevant universal icon for an atom, when displayed in TGUI. (see: icon_state_preview)
    /// Supports GAGS items and colored items.
    // tg: code/modules/asset_cache/spritesheet/batched/universal_icon.dm:387
    public static icon get_display_icon_for(Type atom_path = null)
    {
        if (!ispath(atom_path, typeof(atom)))
            return null;
        object icon_file = Dm.initial(atom_path, "icon");
        object icon_state = Dm.initial(atom_path, "icon_state");
        if (Dm.truthy(Dm.initial(atom_path, "greyscale_config")) && Dm.truthy(Dm.initial(atom_path, "greyscale_colors")))
            return gags_to_universal_icon(atom_path);
        if (ispath(atom_path, typeof(obj)) && Dm.truthy(Dm.initial(atom_path, "icon_state_preview")))
            icon_state = Dm.initial(atom_path, "icon_state_preview");
        icon made = Dm.icon(icon_file, icon_state);
        // uni_icon(..., color = atom_path::color): a colour other than white multiplies the icon (universal_icon.dm:20)
        if (Dm.initial(atom_path, "color") is string color && !Dm.eq(uppertext(color), "#FFFFFF"))
            made.Blend(color, ICON_MULTIPLY);
        return made;
    }

    /// getFlatIcon for [/datum/universal_icon]s
    /// Still fairly slow for complex appearances due to filesystem operations. Try to avoid using it
    // tg: code/modules/asset_cache/spritesheet/batched/universal_icon.dm:402
    public static object get_flat_uni_icon(object appearance = null, object defdir = null, object deficon = null, object defstate = null, object defblend = null, float? start = null, object parentcolor = null)
    {
        start ??= TRUE;
        IDmAppearance look = appearance as IDmAppearance;
        icon flat = Dm.icon(Dm.resource("icons/blanks/32x32.dmi"), "nothing"); // uni_icon('icons/blanks/32x32.dmi', "nothing")

        if (look == null || look.alpha <= 0)
            return flat;

        if (Dm.truthy(start))
        {
            if (!Dm.truthy(deficon))
                deficon = look.icon;
            if (!Dm.truthy(defstate))
                defstate = look.icon_state;
            if (!Dm.truthy(defblend))
                defblend = look.blend_mode;
        }

        bool should_display = true;
        object curicon = Dm.truthy(look.icon) ? look.icon : deficon;
        object curstate = Dm.truthy(look.icon_state) ? look.icon_state : defstate;
        // if(!isfile(curicon) || !length(string_curicon)): the 'runtime' icons written to a temporary file for IconForge (:443-452); the
        // port's /icon is the client's recipe (D104), which needs no file
        if (!Dm.truthy(icon_exists(curicon, curstate)))
        {
            if (Dm.@in("", icon_states_fast(curicon))) // BYOND defaulting functionality
                curstate = "";
            else
                should_display = false;
        }

        object curdir = !Dm.truthy(look.dir) || Dm.eq(look.dir, SOUTH) ? defdir : look.dir;
        object base_icon_dir = null; //We'll use this to get the icon state to display if not null BUT NOT pass it to overlays as the dir we have

        if (should_display)
        {
            // if(curdir != SOUTH) the state's dir count from icon_metadata (:466-479): the server reads no .dmi (D104); a layer's dir is the
            // recipe's direction pick (gaps SP1), which the client takes from a state of fewer dirs as its first
            DmList icon_dimensions = get_icon_dimensions(curicon);
            object icon_width = icon_dimensions["width"];
            object icon_height = icon_dimensions["height"];
            if (!Dm.eq(icon_width, 32) || !Dm.eq(icon_height, 32))
                flat.Scale(icon_width, icon_height);
        }

        if (!Dm.truthy(base_icon_dir))
            base_icon_dir = curdir;

        object curblend = Dm.truthy(look.blend_mode) ? look.blend_mode : defblend;

        DmList appearance_overlays = look.overlays, appearance_underlays = look.underlays;
        if (Dm.truthy(Dm.length(appearance_overlays)) || Dm.truthy(Dm.length(appearance_underlays)))
        {
            // Layers will be a sorted list of icons/overlays, based on the order in which they are displayed
            DmList layers = Dm.list();
            image copy = null;
            if (should_display)
            {
                // Add the atom's icon itself, without pixel_x/y offsets.
                copy = Dm.image(icon: curicon, icon_state: curstate, layer: look.layer, dir: base_icon_dir);
                copy.color = look.color;
                copy.alpha = look.alpha;
                copy.blend_mode = Dm.num(curblend);
                layers[copy] = look.layer;
            }

            // PROCESS_OVERLAYS_OR_UNDERLAYS(flat, appearance.underlays, 0) and (flat, appearance.overlays, 1) (:405-430)
            if (!PROCESS_OVERLAYS_OR_UNDERLAYS(look, layers, appearance_underlays, 0) || !PROCESS_OVERLAYS_OR_UNDERLAYS(look, layers, appearance_overlays, 1))
                return flat;

            icon add; // Icon of overlay being added

            DmList flat_dimensions = get_icon_dimensions(flat);
            float flatX1 = 1;
            float flatX2 = Dm.num(flat_dimensions["width"]);
            float flatY1 = 1;
            float flatY2 = Dm.num(flat_dimensions["height"]);

            float addX1 = 0;
            float addX2 = 0;
            float addY1 = 0;
            float addY2 = 0;

            if (Dm.truthy(look.color))
            {
                if (islist(look.color))
                    map_colors_inferred(flat, look.color as DmList);
                else
                    flat.Blend(look.color, ICON_MULTIPLY);
            }

            if (Dm.truthy(parentcolor) && !Dm.truthy(Dm.bitand(look.appearance_flags, RESET_COLOR)))
            {
                if (islist(parentcolor))
                    map_colors_inferred(flat, parentcolor as DmList);
                else
                    flat.Blend(parentcolor, ICON_MULTIPLY);
            }

            object next_parentcolor = Dm.truthy(look.color) ? look.color : parentcolor;

            foreach (IDmAppearance layer_image in Dm.each_cast<IDmAppearance>(layers))
            {
                if (layer_image.alpha == 0)
                    continue;

                if (layer_image == copy && Dm.truthy(Dm.length(Dm.text("[]", layer_image.icon)))) // 'layer_image' is an /image based on the object being flattened, and isn't a 'runtime' icon.
                {
                    curblend = BLEND_OVERLAY;
                    add = Dm.icon(layer_image.icon, layer_image.icon_state, base_icon_dir); // uni_icon(layer_image.icon, layer_image.icon_state, base_icon_dir)
                    if (Dm.truthy(look.color))
                    {
                        if (islist(look.color))
                            map_colors_inferred(add, look.color as DmList);
                        else
                            add.Blend(look.color, ICON_MULTIPLY);
                    }
                }
                else // 'layer_image' is an appearance object.
                    add = get_flat_uni_icon(layer_image, curdir, curicon, curstate, curblend, FALSE, next_parentcolor) as icon;
                if (add == null || !Dm.truthy(Dm.length(Dm.text("[]", add.dm_file)))) // !length(add.icon_file)
                    continue;

                // Find the new dimensions of the flat icon to fit the added overlay
                DmList add_dimensions = get_icon_dimensions(add);
                addX1 = Dm.min(flatX1, layer_image.pixel_x + layer_image.pixel_w + 1);
                addX2 = Dm.max(flatX2, layer_image.pixel_x + layer_image.pixel_w + Dm.num(add_dimensions["width"])); // assuming 32x32
                addY1 = Dm.min(flatY1, layer_image.pixel_y + layer_image.pixel_z + 1);
                addY2 = Dm.max(flatY2, layer_image.pixel_y + layer_image.pixel_z + Dm.num(add_dimensions["height"]));

                // tg's own test, kept: the flat icon grows only when all four edges move
                if (addX1 != flatX1
                    && addX2 != flatX2
                    && addY1 != flatY1
                    && addY2 != flatY2)
                {
                    // Resize the flattened icon so the new icon fits
                    flat.Crop(
                        addX1 - flatX1 + 1,
                        addY1 - flatY1 + 1,
                        addX2 - flatX1 + 1,
                        addY2 - flatY1 + 1
                    );

                    flatX1 = addX1;
                    flatX2 = addX2;
                    flatY1 = addY1;
                    flatY2 = addY2;
                }

                // Blend the overlay into the flattened icon
                flat.Blend(add, blendMode2iconMode(curblend), layer_image.pixel_x + layer_image.pixel_w + 2 - flatX1, layer_image.pixel_y + layer_image.pixel_z + 2 - flatY1);
            }

            if (look.alpha < 255)
                flat.Blend(Dm.rgb(255, 255, 255, look.alpha), ICON_MULTIPLY);

            return flat;
        }
        else if (should_display) // There's no overlays.
        {
            icon final_icon = Dm.icon(curicon, curstate, base_icon_dir); // uni_icon(curicon, curstate, base_icon_dir)

            if (look.alpha < 255)
                final_icon.Blend(Dm.rgb(255, 255, 255, look.alpha), ICON_MULTIPLY);

            if (Dm.truthy(look.color))
            {
                if (islist(look.color))
                    map_colors_inferred(final_icon, look.color as DmList);
                else
                    final_icon.Blend(look.color, ICON_MULTIPLY);
            }

            return final_icon;
        }
        return null;
    }

    // #define PROCESS_OVERLAYS_OR_UNDERLAYS(flat, process, base_layer) (universal_icon.dm:405-430; it returns from get_flat_uni_icon, rules
    // 11.1 (c): false here makes the caller return flat). Sorts the appearances of process into layers by their layer. An overlay kept
    // as text or as an icon (not an appearance, Appearance.cs) is skipped: shortcut: the port's overlays hold those as they are added,
    // where BYOND makes an appearance of them; upgrade when a preview picture needs one.
    private static bool PROCESS_OVERLAYS_OR_UNDERLAYS(IDmAppearance appearance, DmList layers, DmList process, float base_layer)
    {
        for (int i = 1; i <= Dm.length(process); i++)
        {
            if (process[i] is not IDmAppearance current)
                continue;
            if (current.plane != FLOAT_PLANE && current.plane != appearance.plane)
                continue;
            float current_layer = current.layer;
            if (current_layer < 0)
            {
                if (current_layer <= -1000)
                    return false;
                current_layer = base_layer + appearance.layer + current_layer / 1000;
            }
            /* If we are using topdown rendering, chop that part off so things layer together as expected */
            if ((current_layer >= TOPDOWN_LAYER && current_layer < EFFECTS_LAYER) || current_layer > TOPDOWN_LAYER + EFFECTS_LAYER)
                current_layer -= TOPDOWN_LAYER;
            for (int index_to_compare_to = 1; index_to_compare_to <= Dm.length(layers); index_to_compare_to++)
            {
                object compare_to = layers[index_to_compare_to];
                if (current_layer < Dm.num(layers[compare_to]))
                {
                    layers.Insert(index_to_compare_to, current);
                    break;
                }
            }
            layers[current] = current_layer;
        }
        return true;
    }

    // /datum/universal_icon/proc/map_colors_inferred (universal_icon.dm:95) on the port's universal icon, the /icon: tg's first test is
    // always true (num_args <= 20 || num_args >= 16), so every matrix goes to map_colors_rgba, its 20 places in order (kept)
    private static void map_colors_inferred(icon target, DmList color_args)
    {
        var rgba = new object[20];
        for (int i = 0; i < 20; i++)
            rgba[i] = i < Dm.length(color_args) ? color_args[i + 1] : 0f;
        target.MapColors(rgba);
    }
}
