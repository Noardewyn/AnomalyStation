using System.Linq;

namespace Anomaly.Client.Shared.Icons;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The owner (2026-10-28, D285): a thing in the own hands is its tg picture made solid the way Minecraft draws a held item: the picture on a
// front and a back face a few pixels apart, and a side face along every edge between a drawn pixel and an empty one, in that pixel's colour
// (first made for the 3D human on branch exp/3d-player-model).

/// <summary>A picture made solid: a mesh in the picture's own plane (x right, y up, z out of the front), its texture the cropped picture.</summary>
public sealed class ExtrudedSprite
{
    /// <summary>Vertex positions, three floats each.</summary>
    public float[] Positions { get; init; }

    /// <summary>Vertex normals, three floats each.</summary>
    public float[] Normals { get; init; }

    /// <summary>Texture coordinates into <see cref="Texture"/>, two floats each (0, 0 top left).</summary>
    public float[] Uvs { get; init; }

    /// <summary>Triangles, three vertex indices each.</summary>
    public int[] Indices { get; init; }

    /// <summary>The picture cropped to its pixels (the faint ones included).</summary>
    public Raster Texture { get; init; }

    /// <summary>How many side faces the outline has (one per edge between a drawn pixel and an empty one).</summary>
    public int SideFaces { get; init; }

    /// <summary>The crop's extent in pixels (width, height): how big the thing is.</summary>
    public int DrawnWidth => Texture.Width;

    public int DrawnHeight => Texture.Height;
}

/// <summary>Makes an <see cref="ExtrudedSprite"/> of a picture.</summary>
public static class SpriteExtrusion
{
    /// <summary>A pixel is drawn from this alpha up (0 to 255); the material cuts at the same level.</summary>
    public const byte AlphaCut = 128;

    /// <summary>
    /// The solid of <paramref name="picture"/> (RGBA rows from the top): the drawn pixels, placed so that the point (<paramref name="gripX"/>,
    /// <paramref name="gripYFromTop"/>) of the picture, in pixels, is the origin; <paramref name="pixel"/> is a pixel's size in the mesh's units and
    /// <paramref name="thicknessPixels"/> the solid's depth in pixels. Null when the picture is empty.
    /// </summary>
    public static ExtrudedSprite Build(Raster picture, double gripX, double gripYFromTop, float pixel, float thicknessPixels = 1f)
    {
        int w = picture.Width, h = picture.Height;
        byte[] px = picture.Pixels;
        bool Drawn(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[(y * w + x) * 4 + 3] >= AlphaCut;
        // the front and the back keep the faint pixels too (tg's glow outline, a flame's edge: the view model draws them as a glow); only the drawn ones get sides
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[(y * w + x) * 4 + 3] == 0)
                    continue;
                x0 = Math.Min(x0, x);
                y0 = Math.Min(y0, y);
                x1 = Math.Max(x1, x);
                y1 = Math.Max(y1, y);
            }
        }
        if (x1 < 0)
            return null;
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        var crop = new byte[cw * ch * 4];
        for (int y = 0; y < ch; y++)
            Array.Copy(px, ((y + y0) * w + x0) * 4, crop, y * cw * 4, cw * 4);

        var pos = new List<float>();
        var nrm = new List<float>();
        var uv = new List<float>();
        var idx = new List<int>();
        float half = thicknessPixels * pixel / 2f;
        float X(double x) => (float)((x - gripX) * pixel);
        float Y(double y) => (float)((gripYFromTop - y) * pixel);
        float U(double x) => (float)((x - x0) / cw);
        float V(double y) => (float)((y - y0) / ch);

        // a quad of four corners (counter-clockwise seen from where the normal points) and their texture coordinates
        void Quad((float, float, float) a, (float, float, float) b, (float, float, float) c, (float, float, float) d, (float, float, float) n,
            (float, float) ta, (float, float) tb, (float, float) tc, (float, float) td)
        {
            int start = pos.Count / 3;
            foreach (var (p, t) in new[] { (a, ta), (b, tb), (c, tc), (d, td) })
            {
                pos.Add(p.Item1);
                pos.Add(p.Item2);
                pos.Add(p.Item3);
                nrm.Add(n.Item1);
                nrm.Add(n.Item2);
                nrm.Add(n.Item3);
                uv.Add(t.Item1);
                uv.Add(t.Item2);
            }
            idx.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        // The front and the back: the whole crop, its empty pixels cut by the material's alpha.
        float l = X(x0), r = X(x1 + 1), t = Y(y0), b = Y(y1 + 1);
        Quad((l, b, half), (r, b, half), (r, t, half), (l, t, half), (0, 0, 1), (0, 1), (1, 1), (1, 0), (0, 0));
        Quad((r, b, -half), (l, b, -half), (l, t, -half), (r, t, -half), (0, 0, -1), (1, 1), (0, 1), (0, 0), (1, 0));

        // The sides: every edge of a drawn pixel that has no drawn neighbour, in the pixel's own colour (its centre's coordinates).
        int sides = 0;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!Drawn(x, y))
                    continue;
                var c = (U(x + 0.5), V(y + 0.5));
                float xl = X(x), xr = X(x + 1), yt = Y(y), yb = Y(y + 1);
                if (!Drawn(x, y - 1))
                {
                    Quad((xl, yt, half), (xr, yt, half), (xr, yt, -half), (xl, yt, -half), (0, 1, 0), c, c, c, c);
                    sides++;
                }
                if (!Drawn(x, y + 1))
                {
                    Quad((xr, yb, half), (xl, yb, half), (xl, yb, -half), (xr, yb, -half), (0, -1, 0), c, c, c, c);
                    sides++;
                }
                if (!Drawn(x - 1, y))
                {
                    Quad((xl, yb, half), (xl, yt, half), (xl, yt, -half), (xl, yb, -half), (-1, 0, 0), c, c, c, c);
                    sides++;
                }
                if (!Drawn(x + 1, y))
                {
                    Quad((xr, yt, half), (xr, yb, half), (xr, yb, -half), (xr, yt, -half), (1, 0, 0), c, c, c, c);
                    sides++;
                }
            }
        }
        return new ExtrudedSprite
        {
            Positions = pos.ToArray(),
            Normals = nrm.ToArray(),
            Uvs = uv.ToArray(),
            Indices = idx.ToArray(),
            Texture = new Raster(cw, ch, crop),
            SideFaces = sides,
        };
    }
}

/// <summary>Where the hand holds a held picture (Hud/ViewModel).</summary>
public static class HeldItemGrip
{
    /// <summary>How far in from a handle's end the hand holds it, pixels.</summary>
    public const double HandleInset = 1.5;

    /// <summary>
    /// Where the hand holds a held picture, in its pixels (x from the left, y from the top). A small thing is held at
    /// (<paramref name="handX"/>, <paramref name="handYFromTop"/>). A large one (a tool, a weapon) is held by its handle (the owner,
    /// 2026-10-19: tg holds a fire axe under its head; the hand belongs near the handle's end): the left end of the thing's long axis (the lower
    /// one when it stands upright), where tg draws handles, <see cref="HandleInset"/> pixels in, across the middle of the pixels there.
    /// </summary>
    public static (double X, double Y) Grip(Raster picture, double handX, double handYFromTop)
    {
        int w = picture.Width, h = picture.Height;
        var xs = new List<double>();
        var ys = new List<double>();
        int minX = w, maxX = -1, minY = h, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (picture.Pixels[(y * w + x) * 4 + 3] < SpriteExtrusion.AlphaCut)
                    continue;
                xs.Add(x + 0.5);
                ys.Add(y + 0.5);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }
        if (xs.Count == 0 || !IsLarge(maxX - minX + 1, maxY - minY + 1))
            return (handX, handYFromTop);
        double cx = xs.Average(), cy = ys.Average();
        // the long axis: the principal direction of the drawn pixels
        double sxx = 0, syy = 0, sxy = 0;
        for (int i = 0; i < xs.Count; i++)
        {
            double dx = xs[i] - cx, dy = ys[i] - cy;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }
        double angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        double ax = Math.Cos(angle), ay = Math.Sin(angle);
        double lo = double.MaxValue, hi = double.MinValue;
        for (int i = 0; i < xs.Count; i++)
        {
            double t = (xs[i] - cx) * ax + (ys[i] - cy) * ay;
            lo = Math.Min(lo, t);
            hi = Math.Max(hi, t);
        }
        // tg draws a tool's handle to the left (a screwdriver's heavy handle too) and low when it stands upright: that end is the handle's
        bool loIsHandle = Math.Abs(ax) > 0.2 ? ax * lo < ax * hi : ay * lo > ay * hi;
        double end = loIsHandle ? lo + HandleInset : hi - HandleInset;
        double across = 0;
        int near = 0;
        for (int i = 0; i < xs.Count; i++)
        {
            double dx = xs[i] - cx, dy = ys[i] - cy;
            if (Math.Abs(dx * ax + dy * ay - end) > 1.0)
                continue;
            across += -dx * ay + dy * ax;
            near++;
        }
        if (near > 0)
            across /= near;
        return (cx + ax * end - ay * across, cy + ay * end + ax * across);
    }

    /// <summary>A thing at least this long (pixels, either way) is held by its handle; a smaller one by its middle.</summary>
    public const int LargeFrom = 14;

    /// <summary>Whether a held picture of this drawn size is held by its handle.</summary>
    public static bool IsLarge(int drawnWidth, int drawnHeight) => Math.Max(drawnWidth, drawnHeight) >= LargeFrom;
}
