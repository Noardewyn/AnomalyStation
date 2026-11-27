using System.Linq;
using System.Globalization;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.Tgui;

// M7 slice D172: tg's tgui sprite sheets that an interface names by class (tgui-core's asset prop, ['pipes32x32', 'north-pipe']). The
// server sends no assets (DV44); the client draws the class from its own copy of the .dmi files the sheet is made of, as tg's
// asset datum inserts them. The Godot host (Hud/Tgui/TguiHost.cs) reads the picture; this half only resolves the class.

/// <summary>
/// A sprite sheet class resolved to tg's .dmi files: the files to look in (the last inserted first), the icon state and the BYOND dir; E13: an
/// icon blended under or over it (uni_icon's blend_icon with ICON_UNDERLAY or ICON_OVERLAY, the RCD's windows and airlocks).
/// </summary>
public readonly record struct SpritesheetIcon(IReadOnlyList<string> Files, string State, byte Dir, SpritesheetLayer? Underlay = null, SpritesheetLayer? Overlay = null);

/// <summary>An icon file and state blended into a sprite sheet class's picture (E13).</summary>
public readonly record struct SpritesheetLayer(string File, string State);

public static class TguiSpritesheets
{
    /// <summary>tg's "pipes" sheet at 32x32 (asset_cache/assets/pipes.dm), the RPD's pipe previews.</summary>
    public const string Pipes32 = "pipes32x32";

    // tg: code/modules/asset_cache/assets/pipes.dm:6  the four files, each inserted with every state in GLOB.alldirs; a later file's
    // class replaces an earlier one's (batched_spritesheet.dm insert_icon), so they are searched last first
    private static readonly string[] PipesFiles =
    {
        "icons/obj/pipes_n_cables/hydrochem/fluid_ducts.dmi",
        "icons/obj/pipes_n_cables/transit_tube.dmi",
        "icons/obj/pipes_n_cables/disposal.dmi",
        "icons/obj/pipes_n_cables/pipe_item.dmi",
    };

    /// <summary>tg's "tcomms" sheet at 32x32 (asset_cache/assets/tcomms.dm), the telecomms monitor's machine pictures (M9 slice D2).</summary>
    public const string Tcomms32 = "tcomms32x32";

    // tg: code/modules/asset_cache/assets/tcomms.dm:4  each telecomms machine's icon_state of its icon (/obj/machinery/telecomms: one file)
    private static readonly string[] TcommsFiles = { "icons/obj/machines/telecomms.dmi" };

    // tg's dir2text names (__HELPERS/type2type.dm), which insert_all_icons puts before the state ("[dir2text(direction)]-")
    private static readonly Dictionary<string, byte> Dirs = new(StringComparer.Ordinal)
    {
        ["north"] = 1,
        ["south"] = 2,
        ["east"] = 4,
        ["west"] = 8,
        ["northeast"] = 5,
        ["southeast"] = 6,
        ["northwest"] = 9,
        ["southwest"] = 10,
    };

    /// <summary>E13: tg's "rcd-tgui" sheet at 32x32 (asset_cache/assets/rcd.dm), the RCD's design pictures by design name.</summary>
    public const string Rcd32 = "rcd-tgui32x32";

    /// <summary>E13: tg's "rtd" sheet at 32x32 (asset_cache/assets/rtd.dm), the RTD's tiles by icon file, state and dir.</summary>
    public const string Rtd32 = "rtd32x32";

    /// <summary>E13: tg's "rdd" sheet at 32x32 (asset_cache/assets/rdd.dm), the RDD's decorations by design name.</summary>
    public const string Rdd32 = "rdd32x32";

    /// <summary>E15: tg's "plumbing-tgui" sheet at 32x32 (asset_cache/assets/plumbing.dm), the plumbing constructor's designs by icon state.</summary>
    public const string Plumbing32 = "plumbing-tgui32x32";

    /// <summary>E15: tg's "chemmaster" sheet at 32x32 (asset_cache/assets/chemmaster.dm), the containers by their sanitized type path.</summary>
    public const string Chemmaster32 = "chemmaster32x32";

    /// <summary>Gaps P2: tg's "design" sheet at 32x32 (asset_cache/assets/research_designs.dm), the research designs' pictures by design id (the biogenerator's).</summary>
    public const string Design32 = "design32x32";

    /// <summary>D321: tg's "mecha_equipment" sheet at 32x32 (asset_cache/assets/mecha.dm), the exosuit modules' and the ore box's pictures by icon state.</summary>
    public const string MechaEquipment32 = "mecha_equipment32x32";

    // tg: code/modules/asset_cache/assets/mecha.dm:4  insert_all_icons("", ...) of the two files, the later one's class replacing the earlier's
    private static readonly string[] MechaEquipmentFiles = { "icons/obj/ore.dmi", "icons/obj/devices/mecha_equipment.dmi" };

    /// <summary>Whether <paramref name="sheet"/> is a sprite sheet this table knows (its classes are resolved by <see cref="TryResolve"/>).</summary>
    public static bool IsSheet(string sheet) => sheet == Pipes32 || sheet == Tcomms32 || sheet == Rcd32 || sheet == Rtd32 || sheet == Rdd32 || sheet == Plumbing32 || sheet == Chemmaster32 || sheet == Design32 || sheet == MechaEquipment32;

    /// <summary>The files, state and dir of a class of <paramref name="sheet"/> (<c>"north-pipe"</c>, <c>"hub"</c>); false for an unknown sheet or class.</summary>
    public static bool TryResolve(string sheet, string cls, out SpritesheetIcon icon)
    {
        icon = default;
        if (string.IsNullOrEmpty(cls))
            return false;
        if (sheet == MechaEquipment32)
        {
            icon = new SpritesheetIcon(MechaEquipmentFiles, cls, 2); // insert_all_icons' SOUTH
            return true;
        }
        if (sheet == Tcomms32)
        {
            icon = new SpritesheetIcon(TcommsFiles, cls, 2); // uni_icon's default dir: SOUTH
            return true;
        }
        if (sheet == Rcd32 || sheet == Rtd32 || sheet == Rdd32 || sheet == Plumbing32 || sheet == Chemmaster32 || sheet == Design32)
            return RapidDeviceSheets.Classes(sheet).TryGetValue(cls, out icon);
        if (sheet != Pipes32)
            return false;
        int dash = cls.IndexOf('-');
        if (dash <= 0 || !Dirs.TryGetValue(cls[..dash], out byte dir))
            return false;
        icon = new SpritesheetIcon(PipesFiles, cls[(dash + 1)..], dir);
        return true;
    }

    // ---- engineering E14: tg's "paintable_decals" sheet at 32x32 (the decal painter's previews) ----

    /// <summary>tg's "paintable_decals" sheet at 32x32 (paintable_decals.dm:267), the decal painter's previews: a floor tile with the decal on it.</summary>
    public const string Decals32 = "paintable_decals32x32";

    // tg: code/game/objects/items/tools/engineering/painter/paintable_decals.dm:63  the category's preview: its decal of that state, dir and
    // colour (get_decal_info) at its alpha, multiplied by its colour (or by the rainbow for "custom"), over the floor tile; the warning
    // stripes' override (:204) draws the coloured state as it is. The class is spritesheet_key's "[state]_[dir]_[colour without #]" (:32).
    // The category is not in the class: the warning stripes' colours are the only words, and the tile and trimline categories (the only
    // ones with an alpha of their own, /obj/effect/turf_decal/tile's 110) are the only ones whose states start "tile_", "diagonal_" or
    // "trimline".
    /// <summary>The preview picture of a class of <see cref="Decals32"/> (<c>"warningline_4_red"</c>) from tg's .dmi files; false when the class or a file is unknown.</summary>
    public static bool TryDrawDecal(IconLibrary library, string cls, out Raster picture)
    {
        picture = null;
        int c = cls?.LastIndexOf('_') ?? -1;
        int d = c > 0 ? cls.LastIndexOf('_', c - 1) : -1;
        if (d <= 0 || !byte.TryParse(cls.AsSpan(d + 1, c - d - 1), NumberStyles.None, CultureInfo.InvariantCulture, out byte dir))
            return false;
        string state = cls[..d], color = cls[(c + 1)..];
        bool warning = color is "yellow" or "red" or "white";
        int alpha = state.StartsWith("tile_", StringComparison.Ordinal) || state.StartsWith("diagonal_", StringComparison.Ordinal)
            || state.StartsWith("trimline", StringComparison.Ordinal) ? 110 : 255;
        uint? multiply = null;
        if (warning)
            state = color == "yellow" ? state : state + "_" + color; // paintable_decals.dm:213
        else if (color != "custom")
        {
            if (color.Length is not (6 or 8) || !uint.TryParse(color.AsSpan(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                return false;
            multiply = (rgb << 8) | 0xFF;
            if (color.Length == 8) // the RGBA colours (paintable_decals.dm:156)
                alpha = int.Parse(color.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        if (state.EndsWith("__8", StringComparison.Ordinal)) // the 8-dir states turn by 45 degrees (paintable_decals.dm:149)
        {
            state = state[..^3];
            dir = TurnLeft45(dir);
        }
        if (!TryFrame(library, "icons/turf/floors.dmi", "floor", 2, out Raster floor) || !TryFrame(library, "icons/turf/decals.dmi", state, dir, out Raster decal))
            return false;
        if (!warning)
        {
            decal.MultiplyAlpha(alpha);
            if (multiply is uint tint)
                decal.Multiply(tint);
            else if (TryFrame(library, "icons/effects/random_spawners.dmi", "rainbow", 2, out Raster rainbow))
                decal.Blend(rainbow, IconBlend.Multiply);
        }
        floor.Blend(decal, IconBlend.Overlay);
        picture = floor;
        return true;
    }

    // ---- tg's "fish" sheet at 32x32 (the fish encyclopedia's illustrations) ----

    /// <summary>tg's "fish" sheet at 32x32 (asset_cache/assets/fish.dm), the fish encyclopedia's illustrations.</summary>
    public const string Fish32 = "fish32x32";

    // tg: code/modules/asset_cache/assets/fish.dm:9  a fish's class is sanitize_css_class_name("[icon][icon_state]"), the file and the state
    // with all but letters and digits dropped. The fish's three icon files (no sanitized name starts another) are the prefixes; the state is
    // the file's state whose sanitized name is the rest, drawn as uni_icon draws it (its first frame, facing south).
    private static readonly string[] FishFiles = { "icons/obj/aquarium/fish.dmi", "icons/obj/aquarium/rift.dmi", "icons/obj/aquarium/wide.dmi" };

    /// <summary>The picture of a class of <see cref="Fish32"/> (<c>"iconsobjaquariumfishdmigoldfish"</c>) from tg's .dmi files; false when the class or a file is unknown.</summary>
    public static bool TryDrawFish(IconLibrary library, string cls, out Raster picture)
    {
        picture = null;
        if (string.IsNullOrEmpty(cls))
            return false;
        foreach (string file in FishFiles)
        {
            string prefix = SanitizeCssClassName(file);
            if (!cls.StartsWith(prefix, StringComparison.Ordinal) || !library.TryGetIcon(file, out IIcon icon) || icon is not IconSheet sheet)
                continue;
            string rest = cls[prefix.Length..];
            foreach (IconState state in sheet.States)
            {
                if (SanitizeCssClassName(state.Name) == rest)
                    return TryFrame(library, file, state.Name, ByondDir.South, out picture);
            }
            return false;
        }
        return false;
    }

    // tg: code/__HELPERS/text.dm:1217  sanitize_css_class_name: the name without the characters that are not letters or digits
    private static string SanitizeCssClassName(string name) => new(name.Where(char.IsAsciiLetterOrDigit).ToArray());

    // BYOND's turn(dir, 45): one step counterclockwise (north to northwest)
    private static byte TurnLeft45(byte dir) => dir switch
    {
        1 => 9, 9 => 8, 8 => 10, 10 => 2, 2 => 6, 6 => 4, 4 => 5, 5 => 1, _ => dir,
    };

    private static bool TryFrame(IconLibrary library, string file, string state, byte dir, out Raster frame)
    {
        frame = null;
        if (!library.TryGetIcon(file, out IIcon icon) || !icon.TryGetState(state, out IIconState found))
            return false;
        frame = found.Get(ByondDir.Index(dir, found.Dirs), 0).Clone();
        return true;
    }
}
