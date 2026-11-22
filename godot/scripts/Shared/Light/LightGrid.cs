using System.Runtime.InteropServices;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Light;

// Owner: M3 P7. Engine-free. docs/ai/specs/light-grid.md sections 2 to 4.

/// <summary>
/// tg's corner light for one level, computed from the sources' replicated state (D24, D114): per corner the sum of every
/// source's <c>falloff * light_power * colour</c> (<c>lighting_source.dm</c> <c>update_corners</c>, <c>APPLY_CORNER</c>
/// <c>:191-213</c>), the corners being those of the non-opaque turfs in the source's <c>view()</c>
/// (<c>impacted_corners</c>, <see cref="ByondView"/>). A source is added, changed or removed on its own and an opaque cell
/// changing recomputes only the sources whose view box holds it; sums are kept in fixed point (2^-16) so adding and
/// removing is exact and the incremental result equals a full recompute bit for bit.
/// <para>
/// Texel (i, j) is the corner at the south-west of tile (i + 1, j + 1) (tg's corner at <c>(i + 0.5, j + 0.5)</c>), the
/// texture of <c>docs/ai/specs/light-grid.md</c> section 7: <see cref="Texels"/> holds it as RGBA16F.
/// </para>
/// </summary>
public sealed class LightGrid
{
    private const float Fixed = 65536f;

    private sealed class Source
    {
        public long Key;
        public int X, Y;
        public LightState State;
        public float Lr, Lg, Lb;
        public int Bound;
        public int[] Corners = Array.Empty<int>();
        public float[] Effect = Array.Empty<float>();
        public int Count;
        public float AppliedLr, AppliedLg, AppliedLb;
    }

    private readonly ILightCells _cells;
    private readonly int _z;
    private readonly int _texW;
    private readonly int _texH;
    private readonly int[] _sum;
    private readonly Dictionary<long, Source> _sources = new();
    private readonly Dictionary<(float, int, float, float, int, float, float), float[]> _sheets = new();
    private readonly ByondView _view = new();
    private readonly byte[] _texels;
    private int _dirtyX0 = int.MaxValue, _dirtyY0 = int.MaxValue, _dirtyX1 = -1, _dirtyY1 = -1;

    /// <summary>A grid for level <paramref name="z"/> of <paramref name="cells"/>; all dark until sources are added.</summary>
    public LightGrid(ILightCells cells, int z)
    {
        _cells = cells;
        _z = z;
        _texW = cells.MaxX + 1;
        _texH = cells.MaxY + 1;
        _sum = new int[_texW * _texH * 3];
        _texels = new byte[_texW * _texH * 8];
        MarkDirty(0, 0, _texW - 1, _texH - 1);
    }

    /// <summary>The level this grid lights.</summary>
    public int Z => _z;

    /// <summary>The texture width in texels (<c>maxx + 1</c>).</summary>
    public int Width => _texW;

    /// <summary>The texture height in texels (<c>maxy + 1</c>).</summary>
    public int Height => _texH;

    /// <summary>The number of live sources.</summary>
    public int SourceCount => _sources.Count;

    /// <summary>The texture's bytes, RGBA16F row-major (row j is y; up to date after <see cref="Flush"/>).</summary>
    public byte[] Texels => _texels;

    /// <summary>
    /// Adds the source <paramref name="key"/> at tile (<paramref name="x"/>, <paramref name="y"/>), or changes it, or removes
    /// it when <paramref name="state"/> has no light, is off, has no power, or is none of tg's light systems
    /// (<see cref="TgLightMath.OnGrid"/>: the corner light and the overlay lights, DV61). Recomputes only this source.
    /// </summary>
    public void SetSource(long key, int x, int y, in LightState state)
    {
        bool live = !state.IsNone && state.On && state.Power != 0f && TgLightMath.OnGrid(state.System);
        if (!live)
        {
            RemoveSource(key);
            return;
        }

        if (!_sources.TryGetValue(key, out Source s))
        {
            s = new Source { Key = key };
            _sources[key] = s;
        }
        else if (s.X == x && s.Y == y && s.State == state)
        {
            return;
        }

        s.X = x;
        s.Y = y;
        s.State = state;
        s.Lr = ((state.Rgba >> 24) & 255) / 255f;
        s.Lg = ((state.Rgba >> 16) & 255) / 255f;
        s.Lb = ((state.Rgba >> 8) & 255) / 255f;
        Recompute(s);
    }

    /// <summary>Removes a source; false when there was none.</summary>
    public bool RemoveSource(long key)
    {
        if (!_sources.Remove(key, out Source s))
            return false;
        Unapply(s);
        return true;
    }

    /// <summary>A cell's opacity changed: recomputes the sources whose view box holds it.</summary>
    public void CellOpacityChanged(int x, int y)
    {
        foreach (Source s in _sources.Values)
        {
            if (Math.Abs(s.X - x) <= s.Bound && Math.Abs(s.Y - y) <= s.Bound)
                Recompute(s);
        }
    }

    /// <summary>A cell's base light (or its turf's space-ness) changed: marks the four corners around it.</summary>
    public void CellBaseLightChanged(int x, int y) => MarkDirty(x - 1, y - 1, x, y);

    /// <summary>Recomputes every source from scratch against the current cells (the full recompute the incremental result is compared with).</summary>
    public void RecomputeAll()
    {
        Array.Clear(_sum);
        foreach (Source s in _sources.Values)
        {
            s.Count = 0;
            s.AppliedLr = s.AppliedLg = s.AppliedLb = 0f;
            Recompute(s);
        }

        MarkDirty(0, 0, _texW - 1, _texH - 1);
    }

    /// <summary>Marks the whole texture dirty (the next <see cref="Flush"/> rewrites it all: a level became the active one).</summary>
    public void MarkAllDirty() => MarkDirty(0, 0, _texW - 1, _texH - 1);

    /// <summary>The raw fixed-point sums of corner (<paramref name="i"/>, <paramref name="j"/>) in units of 2^-16 (for tests).</summary>
    public (int R, int G, int B) RawAt(int i, int j)
    {
        int o = (j * _texW + i) * 3;
        return (_sum[o], _sum[o + 1], _sum[o + 2]);
    }

    /// <summary>The corner's displayed light: tg's normalised, rounded sums (<see cref="TgLightMath.Normalize"/>) plus the area base light, never below 0; <paramref name="baseShare"/> is the base light's strength (0 to 1).</summary>
    public (float R, float G, float B) LightAt(int i, int j, out float baseShare)
    {
        int o = (j * _texW + i) * 3;
        TgLightMath.Normalize(_sum[o] / Fixed, _sum[o + 1] / Fixed, _sum[o + 2] / Fixed, out float r, out float g, out float b);
        r = MathF.Max(r, 0f);
        g = MathF.Max(g, 0f);
        b = MathF.Max(b, 0f);
        BaseAtCorner(i, j, out float br, out float bg, out float bb, out baseShare);
        return (r + br, g + bg, b + bb);
    }

    /// <summary>
    /// Writes the dirty corners into <see cref="Texels"/> and returns their rectangle (inclusive texel bounds) or false when
    /// nothing changed since the last call.
    /// </summary>
    public bool Flush(out int x0, out int y0, out int x1, out int y1)
    {
        x0 = _dirtyX0;
        y0 = _dirtyY0;
        x1 = _dirtyX1;
        y1 = _dirtyY1;
        if (x1 < 0)
            return false;
        Span<Half> px = MemoryMarshal.Cast<byte, Half>(new Span<byte>(_texels));
        for (int j = y0; j <= y1; j++)
        {
            for (int i = x0; i <= x1; i++)
            {
                (float r, float g, float b) = LightAt(i, j, out float a);
                int o = (j * _texW + i) * 4;
                px[o] = (Half)r;
                px[o + 1] = (Half)g;
                px[o + 2] = (Half)b;
                px[o + 3] = (Half)a;
            }
        }

        _dirtyX0 = _dirtyY0 = int.MaxValue;
        _dirtyX1 = _dirtyY1 = -1;
        return true;
    }

    // The area's base light at a corner: the mean of the four tiles around it (tg draws the base light per tile flat and
    // its corners blend; the corner is where a bilinear read lands), with space turfs and tiles outside the map counting 0
    // (DV22: no base light on /turf/open/space cells).
    private void BaseAtCorner(int i, int j, out float r, out float g, out float b, out float share)
    {
        r = g = b = share = 0f;
        for (int dx = 1; dx <= 2; dx++)
        {
            for (int dy = 1; dy <= 2; dy++)
            {
                int tx = i + dx - 1;
                int ty = j + dy - 1;
                if (tx < 1 || ty < 1 || tx > _cells.MaxX || ty > _cells.MaxY)
                    continue;
                uint bl = _cells.BaseLight(tx, ty, _z);
                // DEVIATION(DV22): no base light on /turf/open/space cells (tg's /area/space is fullbright, areas/misc.dm:7-9).
                if (bl == 0u || _cells.IsSpaceTurf(tx, ty, _z))
                    continue;
                float alpha = (bl & 255u) / 255f;
                r += ((bl >> 24) & 255u) / 255f * alpha;
                g += ((bl >> 16) & 255u) / 255f * alpha;
                b += ((bl >> 8) & 255u) / 255f * alpha;
                share += alpha;
            }
        }

        r *= 0.25f;
        g *= 0.25f;
        b *= 0.25f;
        share *= 0.25f;
    }

    // tg: /datum/light_source/proc/update_corners (lighting_source.dm:491-): the corners of the non-opaque visible turfs,
    // each getting falloff * light_power, the old effect taken off first (APPLY_CORNER's OLD).
    private void Recompute(Source s)
    {
        Unapply(s);

        LightState st = s.State;
        int vo = TgLightMath.VisualOffset(st.OffsetX, st.OffsetY);
        int bound = TgLightMath.BoundRange(st.Range, vo);
        int working = TgLightMath.WorkingRange(st.Range, vo);
        s.Bound = Math.Max(bound, working);

        float[] sheet = GetSheet(st, vo, bound, out int sheetSide);
        _view.Compute(_cells, s.X, s.Y, _z, working);
        int side = _view.Side;

        int maxCorners = (side + 1) * (side + 1);
        if (s.Corners.Length < maxCorners)
        {
            s.Corners = new int[maxCorners];
            s.Effect = new float[maxCorners];
        }

        // Which corners the visible turfs reach: a stamp over the window's (side + 1)^2 corners.
        Span<bool> stamp = maxCorners <= 1024 ? stackalloc bool[maxCorners] : new bool[maxCorners];
        for (int x = 0; x < side; x++)
        {
            for (int y = 0; y < side; y++)
            {
                if (!_view.IsVisible(x, y))
                    continue;
                if (_view.IsOpaque(x, y))
                    continue;
                stamp[x * (side + 1) + y] = true;
                stamp[(x + 1) * (side + 1) + y] = true;
                stamp[x * (side + 1) + y + 1] = true;
                stamp[(x + 1) * (side + 1) + y + 1] = true;
            }
        }

        int n = 0;
        for (int x = 0; x <= side; x++)
        {
            for (int y = 0; y <= side; y++)
            {
                if (!stamp[x * (side + 1) + y])
                    continue;
                // Window cell (x, y) is tile (s.X - working + x, ...); its south-west corner is texel (tile - 1).
                int i = s.X - working + x - 1;
                int j = s.Y - working + y - 1;
                if (i < 0 || j < 0 || i >= _texW || j >= _texH)
                    continue;
                // lighting_source.dm:196: LUM_FALLOFF reads the sheet at (corner - source) + the range offset.
                int k = i - s.X + bound + 1;
                int l = j - s.Y + bound + 1;
                if (k < 0 || l < 0 || k >= sheetSide || l >= sheetSide)
                    continue;
                float effect = sheet[k * sheetSide + l] * st.Power;
                if (effect == 0f)
                    continue;
                s.Corners[n] = j * _texW + i;
                s.Effect[n] = effect;
                n++;
            }
        }

        s.Count = n;
        s.AppliedLr = s.Lr;
        s.AppliedLg = s.Lg;
        s.AppliedLb = s.Lb;
        Apply(s, 1);
    }

    private void Unapply(Source s)
    {
        if (s.Count == 0)
            return;
        Apply(s, -1);
        s.Count = 0;
    }

    private void Apply(Source s, int sign)
    {
        int minI = int.MaxValue, minJ = int.MaxValue, maxI = -1, maxJ = -1;
        for (int n = 0; n < s.Count; n++)
        {
            int c = s.Corners[n];
            float e = s.Effect[n];
            int o = c * 3;
            _sum[o] += sign * (int)MathF.Round(e * s.AppliedLr * Fixed);
            _sum[o + 1] += sign * (int)MathF.Round(e * s.AppliedLg * Fixed);
            _sum[o + 2] += sign * (int)MathF.Round(e * s.AppliedLb * Fixed);
            int j = c / _texW;
            int i = c - j * _texW;
            if (i < minI) minI = i;
            if (i > maxI) maxI = i;
            if (j < minJ) minJ = j;
            if (j > maxJ) maxJ = j;
        }

        if (maxI >= 0)
            MarkDirty(minI, minJ, maxI, maxJ);
    }

    private void MarkDirty(int x0, int y0, int x1, int y1)
    {
        x0 = Math.Max(x0, 0);
        y0 = Math.Max(y0, 0);
        x1 = Math.Min(x1, _texW - 1);
        y1 = Math.Min(y1, _texH - 1);
        if (x0 < _dirtyX0) _dirtyX0 = x0;
        if (y0 < _dirtyY0) _dirtyY0 = y0;
        if (x1 > _dirtyX1) _dirtyX1 = x1;
        if (y1 > _dirtyY1) _dirtyY1 = y1;
    }

    // tg: get_sheet / generate_sheet (lighting_source.dm:227-258), cached by the same key. Entry [k * side + l] is
    // falloff_at_coord at x = -bound + offset_x - 0.5 + k, y = -bound + offset_y - 0.5 + l.
    private float[] GetSheet(in LightState st, int vo, int bound, out int side)
    {
        side = 2 * bound + 2;
        float range = MathF.Max(1f, st.Range);
        var key = (range, vo, st.OffsetX, st.OffsetY, (int)st.Dir, st.Angle, st.Height);
        if (_sheets.TryGetValue(key, out float[] sheet))
            return sheet;
        sheet = new float[side * side];
        for (int k = 0; k < side; k++)
        {
            float x = -bound + st.OffsetX - 0.5f + k;
            for (int l = 0; l < side; l++)
            {
                float y = -bound + st.OffsetY - 0.5f + l;
                sheet[k * side + l] = TgLightMath.FalloffAtCoord(x, y, 0f, range, st.Dir, st.Angle, st.Height);
            }
        }

        _sheets[key] = sheet;
        return sheet;
    }
}
