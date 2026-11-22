using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// D315 (the owner, 2026-11-03): every player's client can show a minimap of the level its mob is on. A host seam, not tg code: it runs
// tg's load_z (minimap.dm) for one z-level on a datum that is never registered (no id, no New, so the world's state is untouched) and
// reads the picture tg's icon would hold: its DrawBox calls and its Crop. With it, each drawn cell's area (the name tg's hover reads,
// map_position_to_name) and the area's department, the code of its tacmap_color (DEVIATION(DV266)).

/// <summary>A z-level's minimap, tg's <c>/datum/minimap</c> picture as plain values.</summary>
/// <param name="Z">The z-level.</param>
/// <param name="MinX">The world x of the picture's first column (tg's <c>min_x</c>).</param>
/// <param name="MinY">The world y of its first row (tg's <c>min_y</c>).</param>
/// <param name="Width">Columns.</param>
/// <param name="Height">Rows.</param>
/// <param name="Pixels">Each cell's colour as <c>0xRRGGBBAA</c>, x fastest, then y; 0 where nothing is drawn.</param>
/// <param name="Areas">Each cell's area, 1 + its index in <paramref name="AreaNames"/>; 0 where nothing is drawn.</param>
/// <param name="AreaNames">The areas' names.</param>
/// <param name="AreaCodes">The areas' departments ("CMD", "SEC", ...; "" for an area of no department colour).</param>
public sealed record MinimapPicture(int Z, int MinX, int MinY, int Width, int Height, uint[] Pixels, int[] Areas, List<string> AreaNames, List<string> AreaCodes);

/// <summary>Makes a z-level's minimap with tg's <c>load_z</c>.</summary>
public static class MinimapSeam
{
    /// <summary>The minimap of <paramref name="z"/>, or null for a level with nothing tg draws (space, reserved levels).</summary>
    public static MinimapPicture Build(World world, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        using (world.Enter())
        {
            var minimap = new datum_minimap();
            minimap.dm_init_vars();
            minimap.load_z(z);
            (int x1, int y1, int x2, int y2) = (1, 1, 0, 0);
            foreach ((int function, object with, int x, int y) in minimap.base_map.dm_ops)
                if (function == icon.CropFunction && with is ValueTuple<int, int> corner)
                    (x1, y1, x2, y2) = (x, y, corner.Item1, corner.Item2);
            int width = x2 - x1 + 1, height = y2 - y1 + 1;
            if (width < 1 || height < 1)
                return null;

            var pixels = new uint[width * height];
            var areas = new int[width * height];
            var names = new List<string>();
            var codes = new List<string>();
            var index = new Dictionary<area, int>(ReferenceEqualityComparer.Instance);
            foreach ((int function, object with, int x, int y) in minimap.base_map.dm_ops)
            {
                // load_z draws one cell per box
                if (function != icon.DrawBoxFunction || with is not ValueTuple<string, int, int> box)
                    continue;
                int cell = (y - y1) * width + (x - x1);
                pixels[cell] = Rgba(box.Item1);
                if (locate(x, y, z)?.loc is not area place)
                    continue;
                if (!index.TryGetValue(place, out int a))
                {
                    names.Add(place.name ?? "");
                    codes.Add(Department(place.tacmap_color));
                    index[place] = a = names.Count;
                }
                areas[cell] = a;
            }
            return new MinimapPicture(z, x1, y1, width, height, pixels, areas, names, codes);
        }
    }

    // The department of a tg area colour (minimap.dm's TACMAP_AREA_*, which tg's station areas set as their tacmap_color).
    private static string Department(string tacmapColor) => tacmapColor?.ToUpperInvariant() switch
    {
        TACMAP_AREA_COMMAND => "CMD",
        TACMAP_AREA_CARGO => "CRG",
        TACMAP_AREA_ENGINEERING => "ENG",
        TACMAP_AREA_MEDICAL => "MED",
        TACMAP_AREA_SCIENCE => "SCI",
        TACMAP_AREA_SECURITY => "SEC",
        TACMAP_AREA_SERVICE => "SRV",
        TACMAP_AREA_MAINTENANCE => "MNT",
        _ => "",
    };

    private static uint Rgba(string colour)
    {
        if (colour == null)
            return 0;
        DmList c = Dm.rgb2num(colour);
        float alpha = c.Count > 3 ? Dm.num(c[4]) : 255;
        return (uint)Dm.num(c[1]) << 24 | (uint)Dm.num(c[2]) << 16 | (uint)Dm.num(c[3]) << 8 | (uint)alpha;
    }
}
