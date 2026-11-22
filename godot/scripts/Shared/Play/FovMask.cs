namespace Anomaly.Client.Shared;

// Owner: third-person play (the owner, 2026-10-16). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The own mob's line of sight for the third-person view, as Space Station 14 draws it: the camera hangs above and behind the
/// mob and would see over the walls, so what the mob itself cannot see is shaded out. The rule is a straight line on the tile
/// plane from the eye to the point: the point is seen unless the line passes through an opaque cell (tg's <c>IS_OPAQUE_TURF</c>,
/// replicated as <c>CellFlags.Opaque</c>: walls, closed airlocks) other than the eye's own cell and the point's own cell (so a
/// wall's face and top towards the eye are seen, its far side is not). The shader (<c>shaders/fov_shadow.gdshader</c>) runs the
/// same walk per pixel over <see cref="Opaque"/>; this class runs it per cell for the things that are hidden whole (the moving
/// entities, the chunks with nothing in sight) and for the clicks (nothing out of sight is picked).
/// <para>
/// Coordinates are tg's tiles as continuous numbers: the cell (x, y) covers x - 0.5 to x + 0.5 (a pose's X of 3.0 is the middle
/// of tile 3, as <see cref="TileSpace.WorldX"/> has it). The window is the square of <see cref="Radius"/> cells around the eye's
/// cell; everything outside it is out of sight.
/// </para>
/// </summary>
public sealed class FovMask
{
    /// <summary>Cells from the eye's cell to the window's edge.</summary>
    public int Radius { get; }

    /// <summary>The window's side, <c>2 * Radius + 1</c>.</summary>
    public int Side { get; }

    /// <summary>
    /// One byte per window cell, row by row from the south-west corner (index <c>j * Side + i</c> is the cell
    /// <c>(OriginX + i, OriginY + j)</c>): 255 opaque (a cell outside the map too), 0 clear. The shader's texture.
    /// </summary>
    public byte[] Opaque { get; }

    /// <summary>
    /// The shader's texture, laid out as <see cref="Opaque"/>: 0 a clear cell, <see cref="OpaqueSeen"/> an opaque cell in sight (its faces and
    /// top are drawn whole: a pixel on the border of two walls is not shadowed by its neighbour), <see cref="OpaqueHidden"/> one out of sight.
    /// </summary>
    public byte[] Texels { get; }

    public const byte OpaqueSeen = 170, OpaqueHidden = 255;

    private readonly bool[] _visible;
    private float _eyeX = float.NaN, _eyeY = float.NaN;
    private bool _opaqueChanged = true;

    public FovMask(int radius = 30)
    {
        Radius = radius;
        Side = 2 * radius + 1;
        Opaque = new byte[Side * Side];
        Texels = new byte[Side * Side];
        _visible = new bool[Side * Side];
    }

    /// <summary>The window's south-west cell.</summary>
    public int OriginX { get; private set; }

    public int OriginY { get; private set; }

    /// <summary>The level the mask is for.</summary>
    public int Z { get; private set; }

    /// <summary>The eye, in tiles.</summary>
    public float EyeX => _eyeX;

    public float EyeY => _eyeY;

    /// <summary>Counts the times the cells' sight was worked out again (a reader redoes what it derives from it).</summary>
    public int Version { get; private set; }

    /// <summary>Set by <see cref="Update"/> when <see cref="Opaque"/> or the window moved (the texture is uploaded again); cleared by the reader.</summary>
    public bool TextureDirty { get; set; } = true;

    /// <summary>
    /// The mask for an eye at (<paramref name="eyeX"/>, <paramref name="eyeY"/>) on level <paramref name="z"/>; the cells' sight is
    /// worked out again only when the eye moved by more than a few hundredths of a tile or an opaque cell changed.
    /// </summary>
    public void Update(Func<int, int, int, bool> isOpaque, float eyeX, float eyeY, int z)
    {
        int cx = Cell(eyeX), cy = Cell(eyeY);
        int ox = cx - Radius, oy = cy - Radius;
        bool moved = ox != OriginX || oy != OriginY || z != Z;
        OriginX = ox;
        OriginY = oy;
        Z = z;
        bool changed = moved;
        for (int j = 0; j < Side; j++)
        {
            for (int i = 0; i < Side; i++)
            {
                byte b = isOpaque(ox + i, oy + j, z) ? (byte)255 : (byte)0;
                int k = j * Side + i;
                if (Opaque[k] != b)
                {
                    Opaque[k] = b;
                    changed = true;
                }
            }
        }
        if (!changed && !_opaqueChanged && MathF.Abs(eyeX - _eyeX) < 0.03f && MathF.Abs(eyeY - _eyeY) < 0.03f)
            return;
        _opaqueChanged = false;
        Version++;
        _eyeX = eyeX;
        _eyeY = eyeY;
        // A cell is in sight when its middle or a point near one of its corners is; an opaque cell also when a point of one of its faces is
        // (a wall seen along its row shows only its face).
        const float In = 0.38f, Face = 0.48f;
        for (int j = 0; j < Side; j++)
        {
            for (int i = 0; i < Side; i++)
            {
                float x = ox + i, y = oy + j;
                int k = j * Side + i;
                bool seen = Sees(x, y) || Sees(x - In, y - In) || Sees(x + In, y - In) || Sees(x - In, y + In) || Sees(x + In, y + In);
                if (!seen && Opaque[k] != 0)
                    seen = Sees(x - Face, y) || Sees(x + Face, y) || Sees(x, y - Face) || Sees(x, y + Face);
                _visible[k] = seen;
                Texels[k] = Opaque[k] == 0 ? (byte)0 : seen ? OpaqueSeen : OpaqueHidden;
            }
        }
        TextureDirty = true;
    }

    /// <summary>Whether the cell (<paramref name="x"/>, <paramref name="y"/>) on <paramref name="z"/> has anything in sight (false outside the window or on another level).</summary>
    public bool CellVisible(int x, int y, int z)
    {
        if (z != Z || float.IsNaN(_eyeX))
            return false;
        int i = x - OriginX, j = y - OriginY;
        return i >= 0 && j >= 0 && i < Side && j < Side && _visible[j * Side + i];
    }

    /// <summary>Whether the thing at the tile position (<paramref name="x"/>, <paramref name="y"/>) is in sight (its cell's rule).</summary>
    public bool PointVisible(float x, float y, int z) => CellVisible(Cell(x), Cell(y), z);

    /// <summary>
    /// The line from the eye to the point (<paramref name="x"/>, <paramref name="y"/>) in tiles: false when it passes an opaque cell
    /// other than the eye's and the point's own, or the point is outside the window. The shader's walk, step for step.
    /// </summary>
    public bool Sees(float x, float y)
    {
        int tx = Cell(x), ty = Cell(y);
        if (tx < OriginX || ty < OriginY || tx >= OriginX + Side || ty >= OriginY + Side)
            return false;
        // Amanatides and Woo's grid walk in the "u" space where the cell (x, y) is [x, x + 1): u = tile + 0.5.
        float ux = _eyeX + 0.5f, uy = _eyeY + 0.5f;
        float dx = x + 0.5f - ux, dy = y + 0.5f - uy;
        int cx = (int)MathF.Floor(ux), cy = (int)MathF.Floor(uy);
        int stepX = dx > 0 ? 1 : -1, stepY = dy > 0 ? 1 : -1;
        float tDeltaX = dx != 0 ? MathF.Abs(1f / dx) : float.PositiveInfinity;
        float tDeltaY = dy != 0 ? MathF.Abs(1f / dy) : float.PositiveInfinity;
        float tMaxX = dx != 0 ? (dx > 0 ? cx + 1 - ux : ux - cx) * tDeltaX : float.PositiveInfinity;
        float tMaxY = dy != 0 ? (dy > 0 ? cy + 1 - uy : uy - cy) * tDeltaY : float.PositiveInfinity;
        for (int n = 0; n < 4 * Side; n++)
        {
            // The point is in this cell, or the line ends before the next border (a point on a border): nothing more to cross.
            if ((cx == tx && cy == ty) || MathF.Min(tMaxX, tMaxY) >= 1f)
                return true;
            if (tMaxX < tMaxY)
            {
                cx += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                cy += stepY;
                tMaxY += tDeltaY;
            }
            if (cx == tx && cy == ty)
                return true;
            int i = cx - OriginX, j = cy - OriginY;
            if (i < 0 || j < 0 || i >= Side || j >= Side || Opaque[j * Side + i] != 0)
                return false;
        }
        return true;
    }

    /// <summary>The cell a tile position falls in.</summary>
    public static int Cell(float tile) => (int)MathF.Floor(tile + 0.5f);
}
