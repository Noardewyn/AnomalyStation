namespace Anomaly.Client.Shared.Light;

// Owner: M3 P7. Engine-free.

/// <summary>
/// What the light grid reads of the map: the size, what blocks light, the area's base light and whether a cell is a space
/// turf. <see cref="ReplicaLightCells"/> implements it over the replica; tests implement it over arrays.
/// </summary>
public interface ILightCells
{
    /// <summary>The map's width in tiles (tg's <c>world.maxx</c>); tiles are 1 to <see cref="MaxX"/>.</summary>
    int MaxX { get; }

    /// <summary>The map's height in tiles (<c>world.maxy</c>).</summary>
    int MaxY { get; }

    /// <summary>tg's <c>IS_OPAQUE_TURF</c> (replicated as <c>CellFlags.Opaque</c>).</summary>
    bool IsOpaque(int x, int y, int z);

    /// <summary>The cell's area base light (<c>base_lighting_color</c> as 0xRRGGBB in the high bytes and <c>base_lighting_alpha</c> in the low byte, <see cref="Anomaly.Protocol.BaseLight"/>'s packing); 0 for none.</summary>
    uint BaseLight(int x, int y, int z);

    /// <summary>Whether the cell's turf is a <c>/turf/open/space</c> type: the base light is not applied there (DV22).</summary>
    bool IsSpaceTurf(int x, int y, int z);

    /// <summary>D288: whether nothing is over the cell (no opaque turf, no metal ceiling): the light of space reaches it (<see cref="SkyLight"/>). Without ceilings, an open space turf.</summary>
    bool IsOpenToSky(int x, int y, int z) => !IsOpaque(x, y, z) && IsSpaceTurf(x, y, z);

    /// <summary>
    /// <see cref="SkyLight.Fill"/> for the levels <paramref name="low"/> up of a multi-z stack at once, <c>levels[i]</c> being level <c>low + i</c>
    /// (the top one the stack's top), in the tiles (<paramref name="x0"/>, <paramref name="y0"/>) to (<paramref name="x1"/>, <paramref name="y1"/>)
    /// (clamped to the map; the default is all of it); this default fills them cell by cell.
    /// </summary>
    void FillSkyStack(byte[][] levels, int low, int x0 = 1, int y0 = 1, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        int w = MaxX + 1;
        for (int i = 0; i < levels.Length; i++)
            for (int y = Math.Max(1, y0); y <= Math.Min(MaxY, y1); y++)
                for (int x = Math.Max(1, x0); x <= Math.Min(MaxX, x1); x++)
                    levels[i][y * w + x] = SkyLight.At(this, x, y, low + i);
    }
}
