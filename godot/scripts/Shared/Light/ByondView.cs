namespace Anomaly.Client.Shared.Light;

// Owner: M3 P7. Engine-free.

/// <summary>
/// BYOND's <c>view(range, center)</c> over opaque cells, the visibility tg's <c>impacted_corners</c>
/// (<c>lighting_source.dm:465-513</c>) takes its turfs from. BYOND's own code is closed; the rule ported here is the one
/// Lummox Jr. describes in the BYOND forum post 2130277 as OpenDream implements it (OpenDream
/// <c>OpenDreamShared/Dream/ViewAlgorithm.cs</c>, MIT, pinned 1362abc5), steps 2 to 5 and 10: a diagonal shadow loop outward
/// in square rings, then a straight shadow loop outward in diamond rings, then the eye is visible. The window is the square
/// <c>(2r+1) x (2r+1)</c> around the eye (BYOND's view box is a square, the falloff rounds it off). Step 9 of that algorithm
/// (it also marks the wall tiles that bound the view) only changes opaque tiles' visibility, and <c>impacted_corners</c>
/// skips opaque turfs, so it is not needed here; steps 6 to 8 (luminosity) hold trivially: tg gives every turf luminosity 1
/// (<c>lighting_object.dm</c> <c>Initialize</c>) and the source turf the light's range.
/// </summary>
public sealed class ByondView
{
    // The window is stored with a one-cell border (stride w + 2) so the neighbour checks need no bounds tests; absent cells
    // (outside the map, and the border) hold Absent in both planes, which no distance ever equals.
    private const short Absent = -2;

    private short[] _vis = Array.Empty<short>();
    private short[] _vis2 = Array.Empty<short>();
    private bool[] _opaque = Array.Empty<bool>();
    private int _w;
    private int _stride;

    /// <summary>The side of the last computed window.</summary>
    public int Side => _w;

    /// <summary>
    /// Computes the visibility of the window of radius <paramref name="range"/> around (<paramref name="cx"/>,
    /// <paramref name="cy"/>) on level <paramref name="z"/>; read it with <see cref="IsVisible"/>. A cell outside the map is
    /// absent (never visible, never blocks).
    /// </summary>
    public void Compute(ILightCells cells, int cx, int cy, int z, int range)
    {
        int w = 2 * range + 1;
        int stride = w + 2;
        int n = stride * stride;
        if (_vis.Length < n)
        {
            _vis = new short[n];
            _vis2 = new short[n];
            _opaque = new bool[n];
        }

        Array.Fill(_vis, Absent, 0, n);
        Array.Fill(_vis2, Absent, 0, n);
        _w = w;
        _stride = stride;
        for (int x = 0; x < w; x++)
        {
            int tx = cx - range + x;
            bool columnPresent = tx >= 1 && tx <= cells.MaxX;
            for (int y = 0; y < w; y++)
            {
                int ty = cy - range + y;
                int i = (x + 1) * stride + y + 1;
                bool present = columnPresent && ty >= 1 && ty <= cells.MaxY;
                if (!present)
                {
                    _opaque[i] = false;
                    continue;
                }

                _vis[i] = 0;
                _vis2[i] = 0;
                _opaque[i] = cells.IsOpaque(tx, ty, z);
            }
        }

        int eye = range;

        // Step 3, the diagonal shadow loop (square rings; Vis2).
        for (int d = 0; d < range + 1; d++)
        {
            int lowerX = eye - d - 1;
            int upperX = eye + d + 1;
            for (int x = Math.Max(lowerX, 0); x <= Math.Min(upperX, w - 1); x++)
            {
                int y = eye - d - 1;
                int upperY = eye + d + 1;
                bool skipMiddle = x != lowerX && x != upperX;
                if (y < 0)
                    y = skipMiddle ? upperY : 0;
                while (y <= upperY && y < w)
                {
                    int i = (x + 1) * stride + y + 1;
                    if (_vis2[i] != Absent && NeighborIs(_vis2, i, d))
                        _vis2[i] = (short)(_opaque[i] ? -1 : d + 1);
                    if (skipMiddle && y != upperY)
                        y = upperY;
                    else
                        y++;
                }
            }
        }

        // Step 4, the straight shadow loop (diamond rings; Vis).
        for (int d = 0; d < 2 * range + 2; d++)
        {
            int lowerX = Math.Max(eye - d - 1, 0);
            int upperX = Math.Min(eye + d + 1, w - 1);
            for (int x = lowerX; x <= upperX; x++)
            {
                int offsetY = d + 1 - Math.Abs(eye - x);
                UpdateStraight(x, eye - offsetY, d);
                if (offsetY != 0)
                    UpdateStraight(x, eye + offsetY, d);
            }
        }

        // Step 5.
        int eyeIndex = (eye + 1) * stride + eye + 1;
        if (_vis[eyeIndex] != Absent)
            _vis[eyeIndex] = 1;
    }

    /// <summary>Whether the window cell (<paramref name="x"/>, <paramref name="y"/>), 0-based from the window's south-west corner, is in <c>view()</c> (step 10: <c>Vis != 0</c>; false for cells outside the map).</summary>
    public bool IsVisible(int x, int y)
    {
        short v = _vis[(x + 1) * _stride + y + 1];
        return v != 0 && v != Absent;
    }

    /// <summary>Whether the window cell is an opaque turf (read while computing; false outside the map).</summary>
    public bool IsOpaque(int x, int y) => _opaque[(x + 1) * _stride + y + 1];

    private void UpdateStraight(int x, int y, int d)
    {
        if (y < 0 || y >= _w)
            return;
        int i = (x + 1) * _stride + y + 1;
        if (_vis[i] == Absent || !NeighborIs(_vis, i, d))
            return;
        if (_opaque[i])
            _vis[i] = -1;
        else if (_vis2[i] != 0)
            _vis[i] = (short)(d + 1);
    }

    private bool NeighborIs(short[] plane, int i, int d)
    {
        int s = _stride;
        return plane[i - s - 1] == d || plane[i - s] == d || plane[i - s + 1] == d
            || plane[i - 1] == d || plane[i + 1] == d
            || plane[i + s - 1] == d || plane[i + s] == d || plane[i + s + 1] == d;
    }
}
