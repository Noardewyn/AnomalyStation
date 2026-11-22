// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/colors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M6 slice 7b, D156: colour filters reach the client): filter_appearance_recursive (a coloured chair's armrest,
// chair.dm:107); blend_cutoff_colors (M7 slice T3: the lighting cutoffs of the eyes and the glasses); color_transition_filter
// (engineering E12a); apply_matrix_to_color (a tinted fixture and floodlight); random_colour (gaps P5: the chem dispenser's hallucinated colours); flash_color (gaps MO2: the springlock's red flash, and the crayon's,
// the reverse bear trap's and the plasma fist's). The rest of colors.dm comes with its callers.

public static partial class GlobalProcs
{
    /// Given a color in the format of "#RRGGBB" or "#RRGGBBAA", gives back a 4 entry list with the number values of each
    // tg: code/__HELPERS/colors.dm:21  (silicons SI4d: the sprite editor)
    public static DmList split_color(object color = null)
    {
        DmList output = rgb2num(Dm.str(color));
        if (Dm.length(output) == 3)
            output.Add(255);
        return output;
    }

    /// Blend two colors using the normal blend mode of the CSS compositing algorithm
    // tg: code/__HELPERS/colors.dm:271  (silicons SI4d: the sprite editor)
    public static object blend_color(object backdrop = null, object source = null)
    {
        backdrop ??= "#00000000";
        // #define ALPHA_COMPOSE(src_a, comp_a, back_ch, src_ch) ((1 - src_a / comp_a) * back_ch + (src_a / comp_a) * src_ch)
        static float ALPHA_COMPOSE(float src_a, float comp_a, float back_ch, float src_ch) => (1 - src_a / comp_a) * back_ch + (src_a / comp_a) * src_ch;
        DmList rgb_source = split_color(source);
        float source_alpha = Dm.num(rgb_source[4]);
        if (source_alpha == 0)
            return backdrop;
        if (source_alpha == 255)
            return source;
        DmList rgb_backdrop = split_color(backdrop);
        float backdrop_alpha = Dm.num(rgb_backdrop[4]) / 255;
        source_alpha /= 255;
        float output_alpha = source_alpha + backdrop_alpha - source_alpha * backdrop_alpha;
        return rgb(
            ALPHA_COMPOSE(source_alpha, output_alpha, Dm.num(rgb_backdrop[1]), Dm.num(rgb_source[1])),
            ALPHA_COMPOSE(source_alpha, output_alpha, Dm.num(rgb_backdrop[2]), Dm.num(rgb_source[2])),
            ALPHA_COMPOSE(source_alpha, output_alpha, Dm.num(rgb_backdrop[3]), Dm.num(rgb_source[3])),
            output_alpha * 255
        );
    }

    // tg: code/__HELPERS/colors.dm:3  (gaps J3: the spray can)
    public static object is_color_dark(object color = null, float? threshold = null)
    {
        threshold ??= 25;
        DmList hsl = rgb2num(Dm.str(color), COLORSPACE_HSL) as DmList;
        return Dm.num(hsl[3]) < threshold ? TRUE : FALSE;
    }

    /// Recursively applies a filter to a passed in static appearance, returns the modified appearance
    // tg: code/__HELPERS/colors.dm:184
    public static object apply_matrix_to_color(object color = null, DmList matrix = null, float? colorspace = null)
    {
        colorspace ??= COLORSPACE_HSL;
        if (islist(color))
        {
            DmList components = (DmList)color;
            color = Dm.rgb(Dm.num(components[1]), Dm.num(components[2]), Dm.num(components[3]), Dm.num(components[4]));
        }
        object rgb_color = Dm.rgb2num((string)color, (float)colorspace);
        // Pad alpha if we're lacking it
        if (Dm.length(rgb_color) < 4)
            Dm.append(ref rgb_color, 255);
        DmList num_color = (DmList)rgb_color;

        // Do we have a constants row?
        bool has_constants = false;
        // Do we have an alpha row/parameters?
        bool has_alpha = false;

        float sw__1 = Dm.length(matrix);
        if (sw__1 == 9)
        {
            has_constants = false;
            has_alpha = false;
        }
        else if (sw__1 == 12)
        {
            has_constants = true;
            has_alpha = false;
        }
        else if (sw__1 == 16)
        {
            has_constants = false;
            has_alpha = true;
        }
        else if (sw__1 == 20)
        {
            has_constants = true;
            has_alpha = true;
        }
        else
            throw Dm.CRASH(Dm.text("Matrix of invalid length [] was passed into apply_matrix_to_color!", Dm.length(matrix)), "code/__HELPERS/colors.dm", 211);

        DmList new_color = Dm.list(0, 0, 0, 0);
        float row_length = 3;
        if (has_alpha)
            row_length = 4;
        else
            new_color[4] = 255;

        float rows = Dm.length(matrix) / row_length;
        for (float row_index = 1; row_index <= rows; row_index++)
        {
            for (float row_elem = 1; row_elem <= row_length; row_elem++)
            {
                float elem = Dm.num(matrix[(row_index - 1) * row_length + row_elem]);
                if (!has_constants || row_index != rows)
                {
                    new_color[row_index] = Dm.num(new_color[row_index]) + Dm.num(num_color[row_elem]) * elem;
                    continue;
                }

                // Constant values at the end of the list (if we have such)
                if (colorspace != COLORSPACE_HSV && colorspace != COLORSPACE_HCY && colorspace != COLORSPACE_HSL)
                {
                    new_color[row_elem] = Dm.num(new_color[row_elem]) + elem * 255;
                    continue;
                }

                // HSV/HSL/HCY have non-255 maximums for their values
                float multiplier = 255;
                // Hue goes from 0 to 360
                if (row_elem == 1)
                    multiplier = 360;
                // Value, luminance, chroma, etc go from 0 to 100
                else if (row_elem >= 2 && row_elem <= 3)
                    multiplier = 100;
                // Alpha still goes from 0 to 255
                else if (row_elem == 4)
                    multiplier = 255;
                new_color[row_elem] = Dm.num(new_color[row_elem]) + elem * multiplier;
            }
        }

        return Dm.rgb(Dm.num(new_color[1]), Dm.num(new_color[2]), Dm.num(new_color[3]), Dm.num(new_color[4]), colorspace);
    }

    // tg: code/__HELPERS/colors.dm:250
    public static mutable_appearance filter_appearance_recursive(mutable_appearance filter = null, object filter_to_apply = null)
    {
        mutable_appearance modify = Dm.@new<mutable_appearance>(filter);
        // BYOND's filters list of an appearance without filters is empty; the port keeps null for none
        DmList existing_filters = modify.filters is DmList present ? present.Copy() : Dm.list();
        modify.filters = Dm.add(Dm.list(filter_to_apply), existing_filters);

        // Ideally this should be recursive to check for KEEP_APART elements that need this applied to it
        // and RESET_COLOR flags but this is much simpler, and hopefully we don't have that point of layering here
        if (Dm.truthy(Dm.bitand(modify.appearance_flags, KEEP_TOGETHER)))
            return modify;

        for (float overlay_index = 1; overlay_index <= length(modify.overlays); overlay_index++)
            modify.overlays[overlay_index] = filter_appearance_recursive(modify.overlays[overlay_index] as mutable_appearance, filter_to_apply);

        for (float underlay_index = 1; underlay_index <= length(modify.underlays); underlay_index++)
            modify.underlays[underlay_index] = filter_appearance_recursive(modify.underlays[underlay_index] as mutable_appearance, filter_to_apply);

        return modify;
    }

    /* Generates an HSL color transition matrix filter which nicely paints an object
     * without making it a deep fried blob of color
     * saturation_behavior determines how we handle color saturation:
     * SATURATION_MULTIPLY - Multiply pixel's saturation by color's saturation. Paints accents while keeping dim areas dim.
     * SATURATION_OVERRIDE- Affects original lightness/saturation to ensure that pale objects still get doused in color
     */
    // tg: code/__HELPERS/colors.dm:162  (engineering E12a: the hyper-noblium crystal's pressure-proofed clothing)
    public static object color_transition_filter(object new_color = null, string saturation_behavior = SATURATION_MULTIPLY)
    {
        if (islist(new_color))
        {
            DmList rgb_list = (DmList)new_color;
            new_color = Dm.rgb(Dm.num(rgb_list[1]), Dm.num(rgb_list[2]), Dm.num(rgb_list[3]));
        }
        DmList hsl = Dm.rgb2num((string)new_color, COLORSPACE_HSL);
        float hue = Dm.num(hsl[1]) / 360;
        float saturation = Dm.num(hsl[2]) / 100;
        float added_saturation = 0;
        float deducted_light = 0;
        if (Dm.eq(saturation_behavior, SATURATION_OVERRIDE))
        {
            added_saturation = saturation * 0.75f;
            deducted_light = saturation * 0.5f;
            saturation = min(saturation, 1 - added_saturation);
        }

        DmList new_matrix = Dm.list(
            0, 0, 0, // Ignore original hue
            0, saturation, 0, // Multiply the saturation by ours
            0, 0, 1 - deducted_light, // If we're highly saturated then remove a bit of lightness to keep some color in
            hue, added_saturation, 0 // And apply our preferred hue and some saturation if we're oversaturated
        );
        return color_matrix_filter(new_matrix, FILTER_COLOR_HSL);
    }

    /**
     * Blends two lighting color cutoffs (3 length lists of 0 to 100 per channel) into the one that lights as both do.
     */
    // tg: code/__HELPERS/colors.dm:95
    public static object blend_cutoff_colors(DmList first_color = null, DmList second_color = null)
    {
        // These runtimes usually mean that either the eye or the glasses have an incorrect color_cutoffs
        if (!(Dm.num(length(first_color)) == 3))
            throw Dm.CRASH(assertion_message("code/__HELPERS/colors.dm", 97, "first_color?.len == 3", Dm.text("First color must be a 3 length list, received []", json_encode(first_color))), "code/__HELPERS/colors.dm", 97);
        if (!(Dm.num(length(second_color)) == 3))
            throw Dm.CRASH(assertion_message("code/__HELPERS/colors.dm", 98, "second_color?.len == 3", Dm.text("Second color must be a 3 length list, received []", json_encode(second_color))), "code/__HELPERS/colors.dm", 98);

        DmList output = Dm.list_n(3);

        // Invert the colors, multiply to "darken" (actually lights), then uninvert to get back to what we want
        for (float i = 1; i <= 3; i++)
            output[i] = (1 - (1 - Dm.num(first_color[i]) / 100) * (1 - Dm.num(second_color[i]) / 100)) * 100;

        return output;
    }

    /**
     * Gets a color for a name, will return the same color for a given string consistently within a round.atom
     *
     * Note that this proc aims to produce pastel-ish colors using the HSL colorspace. These seem to be favorable for displaying on the map.
     *
     * Arguments:
     * * name - The name to generate a color for
     * * sat_shift - A value between 0 and 1 that will be multiplied against the saturation
     * * lum_shift - A value between 0 and 1 that will be multiplied against the luminescence
     */
    // tg: code/__HELPERS/colors.dm:118
    public static object colorize_string(object name = null, float? sat_shift = null, float? lum_shift = null)
    {
        sat_shift ??= 1;
        lum_shift ??= 1;
        // seed to help randomness
        // tg: var/static/rseed = rand(1,26) (rules 10.6: in the world's TgState, drawn on the first call)
        colorize_string__rseed ??= Dm.rand(1, 26);
        float rseed = colorize_string__rseed.Value;

        // get hsl using the selected 6 characters of the md5 hash
        string hash = Dm.copytext(Dm.md5(Dm.add(name, GLOB.round_id)), rseed, rseed + 6);
        float h = Dm.num(hex2num(Dm.copytext(hash, 1, 3))) * (360f / 255);
        float s = Dm.num(Dm.shr(hex2num(Dm.copytext(hash, 3, 5)), 2)) * ((CM_COLOR_SAT_MAX - CM_COLOR_SAT_MIN) / 63) + CM_COLOR_SAT_MIN;
        float l = Dm.num(Dm.shr(hex2num(Dm.copytext(hash, 5, 7)), 2)) * ((CM_COLOR_LUM_MAX - CM_COLOR_LUM_MIN) / 63) + CM_COLOR_LUM_MIN;

        // adjust for shifts
        s = Dm.clamp(s * sat_shift.Value, 0, 1);
        l = Dm.clamp(l * lum_shift.Value, 0, 1);

        // convert to rgb
        float h_int = Dm.min(Dm.round(h / 60), 5); // mapping each section of H to 60 degree sections
        float c = (1 - Dm.abs(2 * l - 1)) * s;
        float x = c * (1 - Dm.abs(Dm.mod(h / 60, 2) - 1));
        float m = l - c * 0.5f;
        x = (x + m) * 255;
        c = (c + m) * 255;
        m *= 255;
        object sw = h_int;
        if (Dm.eq(sw, 0))
            return Dm.text("#[][][]", num2hex(c, 2), num2hex(x, 2), num2hex(m, 2));
        else if (Dm.eq(sw, 1))
            return Dm.text("#[][][]", num2hex(x, 2), num2hex(c, 2), num2hex(m, 2));
        else if (Dm.eq(sw, 2))
            return Dm.text("#[][][]", num2hex(m, 2), num2hex(c, 2), num2hex(x, 2));
        else if (Dm.eq(sw, 3))
            return Dm.text("#[][][]", num2hex(m, 2), num2hex(x, 2), num2hex(c, 2));
        else if (Dm.eq(sw, 4))
            return Dm.text("#[][][]", num2hex(x, 2), num2hex(m, 2), num2hex(c, 2));
        else if (Dm.eq(sw, 5))
            return Dm.text("#[][][]", num2hex(c, 2), num2hex(m, 2), num2hex(x, 2));
        return null;
    }

    // tg: code/__HELPERS/colors.dm:120  colorize_string's var/static/rseed
    private static ref float? colorize_string__rseed => ref TgState.Current.GlobalProcs__colorize_string__rseed;
}

public static partial class GlobalProcs
{
    ///Returns a random color picked from a list, has 2 modes (0 and 1), mode 1 doesn't pick white, black or gray
    // tg: code/__HELPERS/colors.dm:28
    public static object random_colour(float? mode = null)
    {
        mode ??= 0;
        switch (mode)
        {
            case 0:
                return pick("white", "black", "gray", "red", "green", "blue", "brown", "yellow", "orange", "darkred",
                    "crimson", "lime", "darkgreen", "cyan", "navy", "teal", "purple", "indigo");
            case 1:
                return pick("red", "green", "blue", "brown", "yellow", "orange", "darkred", "crimson",
                    "lime", "darkgreen", "cyan", "navy", "teal", "purple", "indigo");
            default:
                return "white";
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/colors.dm:52 (file-local, #undef at :70)
    private const string TEMP_COLOR_SOURCE = "temp_flash";

    ///Flash a color on the passed mob
    // tg: code/__HELPERS/colors.dm:55
    public static void flash_color(object mob_or_client = null, object flash_color = null, object flash_time = null)
    {
        flash_color ??= COLOR_CULT_RED;
        flash_time ??= 20;
        mob flashed_mob = null;
        if (ismob(mob_or_client))
            flashed_mob = mob_or_client as mob;
        else if (istype(mob_or_client, typeof(client)))
        {
            client flashed_client = mob_or_client as client;
            flashed_mob = flashed_client.mob;
        }

        if (!istype(flashed_mob, typeof(mob)))
            return;

        datum_client_colour_temp temp_color = Dm.@new<datum_client_colour_temp>(flashed_mob);
        temp_color.color = flash_color;
        temp_color.fade_in = Dm.num(flash_time) * 0.25f;
        temp_color.fade_out = Dm.num(flash_time) * 0.25f;
        // QDEL_IN(temp_color, (flash_time * 0.5) + 1)
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(qdel), (Dm.num(flash_time) * 0.5f) + 1 > GC_FILTER_QUEUE ? WEAKREF(temp_color) : temp_color), (Dm.num(flash_time) * 0.5f) + 1, file: "code/__HELPERS/colors.dm", line: 68);
        flashed_mob.add_client_colour(temp_color, TEMP_COLOR_SOURCE);
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/colors.dm:120  colorize_string's var/static/rseed (null until the first call draws it)
    internal float? GlobalProcs__colorize_string__rseed;
}
