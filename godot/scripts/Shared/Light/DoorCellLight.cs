using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared.Light;

// Owner: M7 (the owner's playtest, 2026-10-21). Engine-free: the client's LightLayer fills the texture, light_grid.gdshaderinc reads it.

/// <summary>
/// The light a point in a closed door's cell takes. tg's closed airlock is opaque: no light passes it, and each side of it is lit by the corners on that side.
/// A wall's faces lie on its tile's edges and so read the corners of the side they face; a door's leaf stands in the middle of its tile, between the corners of
/// both sides, and took half the light of the lit room behind it (its frame and the doorway's floor too). In an opaque cell with a door (its plane known), a
/// point reads the two corners of the tile edge on its side of the door's plane: the coordinate across the plane is moved onto that edge. A transparent cell
/// (an open or a glass door, light passes as in tg) and a wall are read as before.
/// </summary>
public static class DoorCellLight
{
    /// <summary>No move: the four corners of the tile are blended as tg's lighting object blends them.</summary>
    public const byte None = 0;

    /// <summary>The door's plane runs north-south (its face EAST): a point reads the west or the east edge.</summary>
    public const byte SnapX = 1;

    /// <summary>The door's plane runs east-west (its face NORTH): a point reads the south or the north edge.</summary>
    public const byte SnapY = 2;

    /// <summary>The code of a cell from whether it is opaque and its door's face (<see cref="DoorAxis"/>: NORTH, EAST, or 0 for no door).</summary>
    public static byte Code(bool opaque, byte doorFace)
        => !opaque || doorFace == 0 ? None : doorFace == ByondDirConst.North ? SnapY : SnapX;

    /// <summary>
    /// Fills <paramref name="texels"/> (one byte per tile, (maxX + 1) x (maxY + 1), texel (x, y) = tile (x, y), row by row) for level <paramref name="z"/>:
    /// the code times 127, so the shader's <c>round(r * 2)</c> is the code.
    /// </summary>
    public static void Fill(byte[] texels, ILightCells cells, int z, Func<int, int, int, byte> doorFace)
    {
        int w = cells.MaxX + 1;
        Array.Clear(texels);
        for (int y = 1; y <= cells.MaxY; y++)
            for (int x = 1; x <= cells.MaxX; x++)
                if (cells.IsOpaque(x, y, z))
                    texels[y * w + x] = (byte)(Code(true, doorFace(x, y, z)) * 127);
    }

    /// <summary>
    /// The shader's choice (light_grid.gdshaderinc), for tests: the point (<paramref name="px"/>, <paramref name="py"/>) in corner units (the include's
    /// <c>p</c>: tile (x, y) covers x - 1 to x) whose four corners are blended, in a cell with <paramref name="code"/>.
    /// </summary>
    public static (float X, float Y) SamplePoint(float px, float py, byte code)
    {
        if (code == SnapX)
            px = Edge(px);
        else if (code == SnapY)
            py = Edge(py);
        return (px, py);
    }

    // the nearer edge of the tile: its own side of the plane through the tile's middle
    private static float Edge(float p) => MathF.Floor(p) + (p - MathF.Floor(p) >= 0.5f ? 1f : 0f);
}
