// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/filters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2: layering_filter (a material's texture filter, atom_materials.dm:364). M6 slice 7 (D155, filters reach the client):
// the other filter makers, the wibbly filters and convert_list_to_filter. Not translated: GLOB.master_filter_info (filters.dm:4-170),
// the data of the admin filter editor (filterrific, admin tooling M10); nothing else reads it.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/filters.dm:176
    public static object alpha_mask_filter(object x = null, object y = null, object icon = null, object render_source = null, object flags = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "alpha"));
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (icon != null)
            dot["icon"] = icon;
        if (render_source != null)
            dot["render_source"] = render_source;
        if (flags != null)
            dot["flags"] = flags;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:189
    public static object angular_blur_filter(object x = null, object y = null, object size = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "angular_blur"));
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (size != null)
            dot["size"] = size;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:198
    public static object color_matrix_filter(object in_matrix = null, object space = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "color"));
        dot["color"] = in_matrix;
        if (space != null)
            dot["space"] = space;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:204
    public static object displacement_map_filter(object icon = null, object render_source = null, object x = null, object y = null, object size = null)
    {
        size ??= ICON_SIZE_ALL;
        DmList dot = Dm.list(Dm.kv("type", "displace"));
        if (icon != null)
            dot["icon"] = icon;
        if (render_source != null)
            dot["render_source"] = render_source;
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (size != null)
            dot["size"] = size;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:217
    public static object drop_shadow_filter(object x = null, object y = null, object size = null, object offset = null, object color = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "drop_shadow"));
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (size != null)
            dot["size"] = size;
        if (offset != null)
            dot["offset"] = offset;
        if (color != null)
            dot["color"] = color;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:230
    public static object gauss_blur_filter(object size = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "blur"));
        if (size != null)
            dot["size"] = size;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:235
    public static object bloom_filter(object threshold = null, object size = null, object offset = null, object alpha = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "bloom"));
        if (threshold != null)
            dot["threshold"] = threshold;
        if (size != null)
            dot["size"] = size;
        if (offset != null)
            dot["offset"] = offset;
        if (alpha != null)
            dot["alpha"] = alpha;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:265
    public static object motion_blur_filter(object x = null, object y = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "motion_blur"));
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:272
    public static object outline_filter(object size = null, object color = null, object flags = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "outline"));
        if (size != null)
            dot["size"] = size;
        if (color != null)
            dot["color"] = color;
        if (flags != null)
            dot["flags"] = flags;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:281
    public static object radial_blur_filter(object size = null, object x = null, object y = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "radial_blur"));
        if (size != null)
            dot["size"] = size;
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:290
    public static object rays_filter(object size = null, object color = null, object offset = null, object density = null, object threshold = null, object factor = null, object x = null, object y = null, object flags = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "rays"));
        if (size != null)
            dot["size"] = size;
        if (color != null)
            dot["color"] = color;
        if (offset != null)
            dot["offset"] = offset;
        if (density != null)
            dot["density"] = density;
        if (threshold != null)
            dot["threshold"] = threshold;
        if (factor != null)
            dot["factor"] = factor;
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (flags != null)
            dot["flags"] = flags;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:311
    public static object ripple_filter(object radius = null, object size = null, object falloff = null, object repeat = null, object x = null, object y = null, object flags = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "ripple"));
        if (radius != null)
            dot["radius"] = radius;
        if (size != null)
            dot["size"] = size;
        if (falloff != null)
            dot["falloff"] = falloff;
        if (repeat != null)
            dot["repeat"] = repeat;
        if (flags != null)
            dot["flags"] = flags;
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:328
    public static object wave_filter(object x = null, object y = null, object size = null, object offset = null, object flags = null)
    {
        DmList dot = Dm.list(Dm.kv("type", "wave"));
        if (size != null)
            dot["size"] = size;
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (offset != null)
            dot["offset"] = offset;
        if (flags != null)
            dot["flags"] = flags;
        return dot;
    }
    // tg: code/__HELPERS/filters.dm:246
    public static object layering_filter(object icon = null, object render_source = null, object x = null, object y = null, object flags = null, object color = null, object transform = null, object blend_mode = null)
    {
        DmList dot = null;
        dot = Dm.list(Dm.kv("type", "layer"));
        if (icon != null)
            dot["icon"] = icon;
        if (render_source != null)
            dot["render_source"] = render_source;
        if (x != null)
            dot["x"] = x;
        if (y != null)
            dot["y"] = y;
        if (color != null)
            dot["color"] = color;
        if (flags != null)
            dot["flags"] = flags;
        if (transform != null)
            dot["transform"] = transform;
        if (blend_mode != null)
            dot["blend_mode"] = blend_mode;
        return dot;
    }

    // tg: code/__HELPERS/filters.dm:341
    public static void apply_wibbly_filters(atom in_atom = null, object length = null)
    {
        for (float i = 1; i <= 7; i++)
        {
            //This is a very baffling and strange way of doing this but I am just preserving old functionality
            float X;
            float Y;
            float rsq;
            do
            {
                X = 60 * rand() - 30;
                Y = 60 * rand() - 30;
                rsq = X * X + Y * Y;
            }
            while (rsq < 100 || rsq > 900); // Yeah let's just loop infinitely due to bad luck what's the worst that could happen?
            float random_roll = rand();
            in_atom.add_filter(Dm.text("wibbly-[]", i), 5, wave_filter(x: X, y: Y, size: rand() * 2.5f + 0.5f, offset: random_roll) as DmList);
            object filter = in_atom.get_filter(Dm.text("wibbly-[]", i));
            Dm.animate(filter, Dm.named("offset", random_roll), Dm.named("time", 0), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
            Dm.animate(Dm.named("offset", random_roll - 1), Dm.named("time", rand() * 20 + 10));
        }
    }

    // tg: code/__HELPERS/filters.dm:341  the same proc on an /image (M8 slice G: the hallucinated anomaly's image; rules 11.1, one overload per C# type of the receiver)
    public static void apply_wibbly_filters(image in_atom, object length = null)
    {
        for (float i = 1; i <= 7; i++)
        {
            //This is a very baffling and strange way of doing this but I am just preserving old functionality
            float X;
            float Y;
            float rsq;
            do
            {
                X = 60 * rand() - 30;
                Y = 60 * rand() - 30;
                rsq = X * X + Y * Y;
            }
            while (rsq < 100 || rsq > 900); // Yeah let's just loop infinitely due to bad luck what's the worst that could happen?
            float random_roll = rand();
            in_atom.add_filter(Dm.text("wibbly-[]", i), 5, wave_filter(x: X, y: Y, size: rand() * 2.5f + 0.5f, offset: random_roll) as DmList);
            object filter = in_atom.get_filter(Dm.text("wibbly-[]", i));
            Dm.animate(filter, Dm.named("offset", random_roll), Dm.named("time", 0), Dm.named("loop", -1), Dm.named("flags", ANIMATION_PARALLEL));
            Dm.animate(Dm.named("offset", random_roll - 1), Dm.named("time", rand() * 20 + 10));
        }
    }

    // tg: code/__HELPERS/filters.dm:358
    public static void remove_wibbly_filters(atom in_atom = null, object remove_duration = null)
    {
        remove_duration ??= 0;
        if (QDELETED(in_atom))
            return;
        object filter;
        for (float i = 1; i <= 7; i++)
        {
            filter = in_atom.get_filter(Dm.text("wibbly-[]", i));
            if (Dm.eq(remove_duration, 0))
            {
                Dm.animate(filter);
                in_atom.remove_filter(Dm.text("wibbly-[]", i));
                continue;
            }
            Dm.animate(filter, Dm.named("x", 0), Dm.named("y", 0), Dm.named("size", 0), Dm.named("offset", 0), Dm.named("time", remove_duration));
            _addtimer(Dm.@new<datum_callback>(in_atom, nameof(datum.remove_filter), Dm.text("wibbly-[]", i)), Dm.num(remove_duration), file: "code/__HELPERS/filters.dm", line: 369);
        }
    }

    // tg: code/__HELPERS/filters.dm:371
    public static object convert_list_to_filter(DmList list_filter = null)
    {
        DmList arguments = list_filter.Copy();
        Dm.remove(ref arguments, "priority");
        return Dm.filter(Dm.arglist(arguments));
    }
}
