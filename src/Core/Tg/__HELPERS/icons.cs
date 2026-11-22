// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only center_image (M7x slice D: the shotguns' 64x64 inhands), GrayScale and ColorTone (M7 husks: the husk's limbs, _bodyparts.dm:1488), icon_exists and BlendRGB (M5 slice 2: the materials' colours; its rgb_gradient is not in the shim) and get_icon_dimensions (M8y slice A: the buckled alert's icon), generate_icon_alpha_mask and copy_appearance_filter_overlays (syndicate campaign D316: the greenscreen display) and get_small_overlay (silicons SI2) and get_flat_human_icon (gaps B1: the imaginary friend's picture, DV440) and GetColors (gaps H2: the custom food's filling colour) translated (M2 F1: update_worn_undersuit asks whether a species' uniform file has the
// jumpsuit's state). The icon procs of the file (blending, colouring, flattening, the icon state caches,
// the dimension lookups) render icons on the server, which the port leaves to the client (D104): they come with the
// presentation work that needs them, or not at all.

public static partial class GlobalProcs
{
    /**
     * Centers a (usually larger) image around the middle of the tile: offsets it so that its bottom left corner is shifted by half of the
     * size it has over a tile, so an inhand sprite of 64x64 or 96x96 stands on the holder's middle.
     *
     * Arguments:
     * * image_to_center: the image to offset
     * * x_dimension, y_dimension: the sprite's size in pixels (0 for "none": nothing is returned)
     */
    // tg: code/__HELPERS/icons.dm:1144
    public static image center_image(image image_to_center = null, float? x_dimension = null, float? y_dimension = null)
    {
        x_dimension ??= 0;
        y_dimension ??= 0;
        if (image_to_center == null)
            return null;

        if (x_dimension == 0 || y_dimension == 0)
            return null;

        if ((x_dimension == ICON_SIZE_X) && (y_dimension == ICON_SIZE_Y))
            return image_to_center;

        //Offset the image so that its bottom left corner is shifted this many pixels
        //This makes it infinitely easier to draw larger inhands/images larger than world.iconsize
        //but still use them in game
        float x_offset = -((x_dimension.Value / ICON_SIZE_X) - 1) * (ICON_SIZE_X * 0.5f);
        float y_offset = -((y_dimension.Value / ICON_SIZE_Y) - 1) * (ICON_SIZE_Y * 0.5f);

        //Correct values under icon_size
        if (x_dimension < ICON_SIZE_X)
            x_offset *= -1;
        if (y_dimension < ICON_SIZE_Y)
            y_offset *= -1;

        image_to_center.pixel_w = x_offset;
        image_to_center.pixel_z = y_offset;

        return image_to_center;
    }

    // tg: code/__HELPERS/icons.dm:1214
    public static object icon_exists(object file = null, object state = null)
    {
        if (file == null || state == null)
            return FALSE; //This is common enough that it shouldn't panic, imo.

        // if(isnull(GLOB.icon_states_cache_lookup[file])) compile_icon_states_cache(file); return
        // !isnull(GLOB.icon_states_cache_lookup[file][state]) (:1218-1220): a .dmi's states come from rust-g's icon metadata
        // (icon_metadata, :1234); the server reads no .dmi (D104), so it reads the committed icon state table generated from
        // tg's icons (silicons SI4, IconStates; as the body-part masks, D179). A dynamic icon is asked by its file.
        return IconStates.Has(IconFilePath(file), Dm.str(state)) ? TRUE : FALSE;
    }

    // tg: code/__HELPERS/icons.dm:1223
    public static DmList icon_states_fast(object file = null)
    {
        if (file == null)
            return null;
        // GLOB.icon_states_cache[file], which compile_icon_states_cache fills from rust-g's icon metadata (:1230): the committed icon state
        // table, as icon_exists; a file it does not hold has no states
        IReadOnlyCollection<string> states = IconStates.States(IconFilePath(file));
        return states == null ? Dm.list() : Dm.list(System.Linq.Enumerable.ToArray<object>(states));
    }

    // The icon file an icon_exists or icon_states_fast argument names: a resource's path, a path's text, a dynamic icon's file. A GAGS
    // icon (DV19: "gags:<config type>|<colours>", which the client composes) has its config's states: the config's json file.
    private static string IconFilePath(object file)
    {
        string path = file switch
        {
            DmResource resource => resource.Path,
            string text => text,
            DmShim.icon dynamic => (dynamic.dm_file as DmResource)?.Path ?? dynamic.dm_file as string,
            _ => null,
        };
        if (path == null || !path.StartsWith("gags:", StringComparison.Ordinal))
            return path;
        int bar = path.IndexOf('|');
        Type config = text2path(bar < 0 ? path[5..] : path[5..bar]) as Type;
        return config == null || !typeof(datum_greyscale_config).IsAssignableFrom(config)
            ? null
            : (Dm.initial(config, (datum_greyscale_config c) => c.json_config) as DmResource)?.Path;
    }
}

// tg's procs on the builtin /icon, as extension methods of the shim's icon (as tg's /world procs are of DmWorld, rules 1.5): the
// operations they call are kept for the client (D104, protocol.md section 34.3).
public static partial class GlobalProcs
{
    // Convert to grayscale
    // tg: code/__HELPERS/icons.dm:210
    public static void GrayScale(this icon src)
    {
        src.MapColors(0.3f, 0.3f, 0.3f, 0.59f, 0.59f, 0.59f, 0.11f, 0.11f, 0.11f, 0, 0, 0);
    }

    // tg: code/__HELPERS/icons.dm:213
    public static void ColorTone(this icon src, object tone = null)
    {
        src.GrayScale();

        DmList TONE = Dm.rgb2num(tone as string);
        float gray = round(Dm.num(TONE[1]) * 0.3f + Dm.num(TONE[2]) * 0.59f + Dm.num(TONE[3]) * 0.11f, 1);

        icon upper = Dm.truthy(255 - gray) ? Dm.icon(src) : null; // new(src): BYOND's new /icon(src) is icon(src), a copy

        if (Dm.truthy(gray))
        {
            src.MapColors(255 / gray, 0, 0, 0, 255 / gray, 0, 0, 0, 255 / gray, 0, 0, 0);
            src.Blend(tone, ICON_MULTIPLY);
        }
        else
            src.SetIntensity(0);
        if (Dm.truthy(255 - gray))
        {
            upper.Blend(rgb(gray, gray, gray), ICON_SUBTRACT);
            upper.MapColors((255 - Dm.num(TONE[1])) / (255 - gray), 0, 0, 0, 0, (255 - Dm.num(TONE[2])) / (255 - gray), 0, 0, 0, 0, (255 - Dm.num(TONE[3])) / (255 - gray), 0, 0, 0, 0, 0, 0, 0, 0, 1);
            src.Blend(upper, ICON_ADD);
        }
    }
}

public static partial class GlobalProcs
{
    /*
        Smooth blend between RGB colors

        amount=0 is the first color
        amount=1 is the second color
        amount=0.5 is directly between the two colors

        amount<0 or amount>1 are allowed
     */
    // tg: code/__HELPERS/icons.dm:311
    public static object BlendRGB(object rgb1 = null, object rgb2 = null, object amount = null)
    {
        // return rgb_gradient(amount, 0, rgb1, 1, rgb2, "loop"): rgb_gradient(index, args...) is BYOND's gradient(args, index)
        // (__DEFINES/gradient.dm:2), which the shim does not have; M6 slice 1 (the blood decals' drying colour) writes the
        // two-stop form inline: in "loop" mode the index wraps into [0, 1), then each RGB channel (and the alpha, when either
        // colour has one) goes linearly from the first colour to the second (BYOND ref /proc/gradient; flagged for the BYOND
        // oracle: whether an index of exactly 1 wraps to the first colour).
        DmList first = Dm.rgb2num(rgb1 as string);
        DmList second = Dm.rgb2num(rgb2 as string);
        if (first == null || second == null)
            return null;
        float index = Dm.num(amount);
        index -= MathF.Floor(index);
        float Channel(int i) => Dm.num(first[i]) + (Dm.num(second[i]) - Dm.num(first[i])) * index;
        if (Dm.length(first) < 4 && Dm.length(second) < 4)
            return Dm.rgb(MathF.Round(Channel(1)), MathF.Round(Channel(2)), MathF.Round(Channel(3)));
        float alpha1 = Dm.length(first) >= 4 ? Dm.num(first[4]) : 255;
        float alpha2 = Dm.length(second) >= 4 ? Dm.num(second[4]) : 255;
        return Dm.rgb(MathF.Round(Channel(1)), MathF.Round(Channel(2)), MathF.Round(Channel(3)), MathF.Round(alpha1 + (alpha2 - alpha1) * index));
    }
}

/// <summary>
/// BYOND's <c>icon.Width()</c> and <c>Height()</c> of an icon file for the server, which renders no icons: tg's own sizes from
/// the files' DMI headers (Generated/Types/IconSizes.g.cs, written by the types tool); a file not listed is one tile, 32 x 32
/// (BYOND's default and tg's world.icon_size). An /icon object reads its file's size (every state of a file has its size).
/// </summary>
public static partial class IconSizes
{
    public static (int Width, int Height) Of(object file)
    {
        string path = file switch
        {
            DmResource r => r.Path,
            icon i => (i.dm_file as DmResource)?.Path ?? i.dm_file as string,
            string s => s,
            _ => null,
        };
        return path != null && Sizes.TryGetValue(path, out var size) ? size : (ICON_SIZE_X, ICON_SIZE_Y);
    }
}

public static partial class GlobalProcs
{
    /// Returns a list containing the width and height of an icon file
    // tg: code/__HELPERS/icons.dm:1288  (the sizes come from the files' DMI headers, IconSizes; GLOB.icon_dimensions's cache is the table itself)
    public static DmList get_icon_dimensions(object icon_path = null)
    {
        (int width, int height) = IconSizes.Of(icon_path);
        return Dm.list(Dm.kv("width", width), Dm.kv("height", height));
    }

    // tg: code/__HELPERS/icons.dm:1266
    public static float get_size_in_tiles(obj target = null)
    {
        // var/icon/size_check = icon(target.icon, target.icon_state); size_check.Width(): the file's width (IconSizes)
        float size = IconSizes.Of(target.icon).Width / (float)ICON_SIZE_X;

        return size;
    }
}

public partial class obj
{
    /**
     * Updates the bounds of a rotated object
     * This ensures that the bounds are always correct,
     * even if the object is rotated after init.
     */
    // tg: code/__HELPERS/icons.dm:1277
    public virtual void set_bounds()
    {
        float size = get_size_in_tiles(this);

        if (Dm.eq(dir, NORTH) || Dm.eq(dir, SOUTH))
        {
            bound_width = size * ICON_SIZE_X;
            bound_height = ICON_SIZE_Y;
        }
        else
        {
            bound_width = ICON_SIZE_X;
            bound_height = size * ICON_SIZE_Y;
        }
    }
}

public partial class atom
{
    ///Flickers an overlay on an atom
    // tg: code/__HELPERS/icons.dm:1172  (gaps E2c: the portal storm's flashes)
    public virtual async DmTask flick_overlay_static(object overlay_image = null, object duration = null)
    {
        // set waitfor = FALSE
        if (!Dm.truthy(overlay_image))
            return;
        add_overlay(overlay_image);
        await sleep(Dm.num(duration));
        cut_overlay(overlay_image);
    }

    /// Perform a shake on an atom, resets its position afterwards
    // tg: code/__HELPERS/icons.dm:1181
    public virtual void Shake(float? pixelshiftx = null, float? pixelshifty = null, float? duration = null, float? shake_interval = null)
    {
        pixelshiftx ??= 2;
        pixelshifty ??= 2;
        duration ??= 2.5f * SECONDS;
        shake_interval ??= 0.02f * SECONDS;
        float initialpixelx = pixel_x;
        float initialpixely = pixel_y;
        // tg's first step takes initialpixelx for the y base too (kept)
        Dm.animate(this, Dm.named("pixel_x", initialpixelx + rand(-pixelshiftx.Value, pixelshiftx.Value)), Dm.named("pixel_y", initialpixelx + rand(-pixelshifty.Value, pixelshifty.Value)), Dm.named("time", shake_interval.Value), Dm.named("flags", ANIMATION_PARALLEL)); // Start at 3 because we already applied one, and need another to reset
        for (float i = 3, i__end = duration.Value / shake_interval.Value; i <= i__end; i++)
            Dm.animate(Dm.named("pixel_x", initialpixelx + rand(-pixelshiftx.Value, pixelshiftx.Value)), Dm.named("pixel_y", initialpixely + rand(-pixelshifty.Value, pixelshifty.Value)), Dm.named("time", shake_interval.Value));
        Dm.animate(Dm.named("pixel_x", initialpixelx), Dm.named("pixel_y", initialpixely), Dm.named("time", shake_interval.Value));
    }
    /**
     * Generates an alpha mask of the given icon and icon_state.
     * Returns an `/icon` that is the alpha mask of the provided icon and icon_state.
     */
    // tg: code/__HELPERS/icons.dm:621
    public static icon generate_icon_alpha_mask(object icon_to_mask = null, object icon_state_to_mask = null)
    {
        icon mask_icon = Dm.icon(icon_to_mask, icon_state_to_mask);
        // we invert the mask by using none of the original colors; the fourth group of numbers is the alpha values of each of the
        // original colors, which we multiply by 255 and subtract 255 from for the matching pixels, starting with white everywhere
        mask_icon.MapColors(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 255f, 255f, 255f, -255f, 1f, 1f, 1f, 1f);
        return mask_icon;
    }

    /**
     * Copies the passed /appearance, returns a /mutable_appearance
     *
     * Filters out certain overlays from the copy, depending on their planes
     * Prevents stuff like lighting from being copied to the new appearance
     */
    // tg: code/__HELPERS/icons.dm:1368
    public static mutable_appearance copy_appearance_filter_overlays(object appearance_to_copy = null)
    {
        mutable_appearance copy = Dm.@new<mutable_appearance>(appearance_to_copy);
        DmList plane_whitelist = Dm.list(FLOAT_PLANE, GAME_PLANE, FLOOR_PLANE); // var/static: a constant list, made per call

        /// Ideally we'd have knowledge what we're removing but i'd have to be done on target appearance retrieval
        DmList overlays_to_keep = Dm.list();
        foreach (object special_overlay in Dm.each(copy.overlays))
        {
            if (special_overlay == null)
                continue;
            mutable_appearance real = Dm.@new<mutable_appearance>();
            real.appearance = special_overlay;
            if (Dm.@in(PLANE_TO_TRUE(real.plane), plane_whitelist))
                Dm.append(overlays_to_keep, real);
        }
        copy.overlays = overlays_to_keep;

        DmList underlays_to_keep = Dm.list();
        foreach (object special_underlay in Dm.each(copy.underlays))
        {
            if (special_underlay == null)
                continue;
            mutable_appearance real = Dm.@new<mutable_appearance>();
            real.appearance = special_underlay;
            if (Dm.@in(PLANE_TO_TRUE(real.plane), plane_whitelist))
                Dm.append(underlays_to_keep, real);
        }
        copy.underlays = underlays_to_keep;

        return copy;
    }
}

// tg: code/__HELPERS/icons.dm:1326 (silicons SI2: the ghosts' alert icon)
public static partial class GlobalProcs
{
    /// Fikou's fix for making toast alerts look nice - resets offsets, transforms to fit
    // tg: code/__HELPERS/icons.dm:1326
    public static object get_small_overlay(atom source = null)
    {
        mutable_appearance alert_overlay = Dm.@new<mutable_appearance>(source);
        alert_overlay.pixel_x = 0;
        alert_overlay.pixel_y = 0;
        alert_overlay.pixel_z = 0;
        alert_overlay.pixel_w = 0;

        float scale = 1;
        DmList icon_dimensions = get_icon_dimensions(source.icon);
        float width = Dm.num(icon_dimensions["width"]);
        float height = Dm.num(icon_dimensions["height"]);

        if (width > ICON_SIZE_X)
            alert_overlay.pixel_w = -(ICON_SIZE_X / 2f) * ((width - ICON_SIZE_X) / ICON_SIZE_X);
        if (height > ICON_SIZE_Y)
            alert_overlay.pixel_z = -(ICON_SIZE_Y / 2f) * ((height - ICON_SIZE_Y) / ICON_SIZE_Y);
        if (width > ICON_SIZE_X || height > ICON_SIZE_Y)
        {
            if (width >= height)
                scale = ICON_SIZE_X / width;
            else
                scale = ICON_SIZE_Y / height;
        }
        alert_overlay.transform = (alert_overlay.transform as matrix).Scale(scale);

        return alert_overlay;
    }

    /// # If you already have a human and need to get its flat icon, call `get_flat_existing_human_icon()` instead.
    /// For creating consistent icons for human looking simple animals.
    // tg: code/__HELPERS/icons.dm:771  (gaps B1: the imaginary friend's picture)
    // DEVIATION(DV440): the flat icon is the port's flattening of the dressed dummy (get_flat_uni_icon, the client's recipe, D104) facing
    // south: the port's /icon holds one picture, so tg's Insert of a flat picture per direction of showDirs is not made.
    public static async DmTask<object> get_flat_human_icon(object icon_id = null, datum_job job = null, datum_preferences prefs = null, object dummy_key = null, object showDirs = null, object outfit_override = null, float? no_anim = null)
    {
        showDirs ??= GLOB.cardinals;
        no_anim ??= FALSE;
        DmList humanoid_icon_cache = TgState.Current.GlobalProcs__get_flat_human_icon__humanoid_icon_cache ??= Dm.list();
        if (Dm.truthy(icon_id) && Dm.truthy(humanoid_icon_cache[icon_id]))
            return humanoid_icon_cache[icon_id];

        mob_living_carbon_human_dummy body = await generate_or_wait_for_human_dummy(dummy_key) as mob_living_carbon_human_dummy;

        if (prefs != null)
            prefs.apply_prefs_to(body, TRUE);

        object outfit = outfit_override ?? job?.outfit;
        if (job != null)
            await (body.dna.species as datum_species).pre_equip_species_outfit(job, body, TRUE);
        if (Dm.truthy(outfit))
            await body.equipOutfit(outfit, TRUE);

        // var/icon/out_icon = icon('icons/effects/effects.dmi', "nothing"); for(var/direction in showDirs) { var/icon/partial =
        // getFlatIcon(body, defdir = direction, no_anim = no_anim); out_icon.Insert(partial, dir = direction) }: DV440, one picture
        icon out_icon = get_flat_uni_icon(body, SOUTH) as icon;

        humanoid_icon_cache[icon_id] = out_icon;
        if (Dm.truthy(dummy_key))
            await unset_busy_human_dummy(dummy_key);
        else
            qdel(body);
        return out_icon;
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/icons.dm:772  get_flat_human_icon's var/static/list/humanoid_icon_cache (built on the first call)
    internal DmList GlobalProcs__get_flat_human_icon__humanoid_icon_cache;
}

// tg: code/__HELPERS/icons.dm:369 (gaps H2: the custom food's filling colour)
public static partial class GlobalProcs
{
    //Used in the OLD chem colour mixing algorithm
    // tg: code/__HELPERS/icons.dm:369
    public static object GetColors(object hex = null)
    {
        hex = uppertext(hex);
        // No alpha set? Default to full alpha.
        if (Dm.length(hex) == 7)
            hex = Dm.add(hex, "FF");
        float hi1 = text2ascii(hex, 2); // R
        float lo1 = text2ascii(hex, 3); // R
        float hi2 = text2ascii(hex, 4); // G
        float lo2 = text2ascii(hex, 5); // G
        float hi3 = text2ascii(hex, 6); // B
        float lo3 = text2ascii(hex, 7); // B
        float hi4 = text2ascii(hex, 8); // A
        float lo4 = text2ascii(hex, 9); // A
        return Dm.list(Dm.bitor(Dm.shl(hi1 >= 65 ? hi1 - 55 : hi1 - 48, 4), lo1 >= 65 ? lo1 - 55 : lo1 - 48),
            Dm.bitor(Dm.shl(hi2 >= 65 ? hi2 - 55 : hi2 - 48, 4), lo2 >= 65 ? lo2 - 55 : lo2 - 48),
            Dm.bitor(Dm.shl(hi3 >= 65 ? hi3 - 55 : hi3 - 48, 4), lo3 >= 65 ? lo3 - 55 : lo3 - 48),
            Dm.bitor(Dm.shl(hi4 >= 65 ? hi4 - 55 : hi4 - 48, 4), lo4 >= 65 ? lo4 - 55 : lo4 - 48));
    }
}

// tg: code/__HELPERS/icons.dm:631 (gaps SP1: the digitigrade clothing masks)
public static partial class GlobalProcs
{
    /**
     * A simple helper proc to apply a mask to an icon
     * Arguments:
     * * base_icon - the icon which the proc will apply the mask on
     * * mask_to_use - the mask to use from code\__DEFINES\icon_masks.dm
     * * replacement - an icon to overlay after the masking
     */
    // tg: code/__HELPERS/icons.dm:638
    public static object apply_icon_mask(icon base_icon = null, object mask_to_use = null, icon replacement = null)
    {
        if (mask_to_use == null)
            return null;
        icon mask = Dm.icon(Dm.resource("icons/mob/clothing/under/masking_helpers.dmi"), mask_to_use);
        base_icon.Blend(mask, ICON_SUBTRACT);
        if (replacement != null)
            base_icon.Blend(replacement, ICON_OVERLAY);
        return base_icon;
    }
}

