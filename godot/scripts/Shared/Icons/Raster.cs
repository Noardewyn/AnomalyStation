namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7. Engine-free: no Godot API here, so the files of this folder are compiled into the client and, linked, into
// tests/Anomaly.Server.Tests (Client/Render/). The icon pipeline of D104: tg's .dmi pictures are decoded, cut, blended and
// coloured on the CPU the way BYOND's icon() procs do it (GAGS, tg's greyscale clothes) and the way its renderer draws an
// appearance (the compositor), and the client only uploads the finished picture as a texture.

/// <summary>BYOND's <c>icon.Blend</c> modes (<c>ICON_*</c> defines; the values are BYOND's).</summary>
public enum IconBlend : byte
{
    /// <summary>Add the colour components, clamped; the alphas multiply (transparent where either is).</summary>
    Add = 0,

    /// <summary>Subtract them, clamped; the alphas multiply.</summary>
    Subtract = 1,

    /// <summary>Multiply them (a colour operand is a flat picture of that colour).</summary>
    Multiply = 2,

    /// <summary>Put the new picture over the old one with alpha blending.</summary>
    Overlay = 3,

    /// <summary>Keep the bits both have.</summary>
    And = 4,

    /// <summary>Combine the bits of both.</summary>
    Or = 5,

    /// <summary>Put the new picture under the old one.</summary>
    Underlay = 6,
}

/// <summary>
/// A picture of 8-bit RGBA pixels (straight, not premultiplied alpha), rows from the top. The unit of everything the icon
/// pipeline moves; a frame of an icon state, a GAGS layer and a composed appearance are all rasters.
/// </summary>
public sealed class Raster
{
    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>Four bytes per pixel (R, G, B, A), row after row from the top.</summary>
    public byte[] Pixels { get; }

    public Raster(int width, int height)
    {
        if (width < 0 || height < 0 || (long)width * height * 4 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "a raster of " + width + " x " + height);
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public Raster(int width, int height, byte[] pixels)
    {
        if (pixels == null || pixels.Length != width * height * 4)
            throw new ArgumentException("the pixel array does not match " + width + " x " + height, nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>A copy mirrored left to right (<paramref name="horizontal"/>) and/or top to bottom (<paramref name="vertical"/>).</summary>
    public Raster Flipped(bool horizontal, bool vertical)
    {
        var flipped = new Raster(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            int fromY = vertical ? Height - 1 - y : y;
            for (int x = 0; x < Width; x++)
            {
                int fromX = horizontal ? Width - 1 - x : x;
                Buffer.BlockCopy(Pixels, (fromY * Width + fromX) * 4, flipped.Pixels, (y * Width + x) * 4, 4);
            }
        }
        return flipped;
    }

    /// <summary>A flat picture of one colour (<c>0xRRGGBBAA</c>).</summary>
    public static Raster Solid(int width, int height, uint rgba)
    {
        var raster = new Raster(width, height);
        byte r = (byte)(rgba >> 24), g = (byte)(rgba >> 16), b = (byte)(rgba >> 8), a = (byte)rgba;
        for (int i = 0; i < raster.Pixels.Length; i += 4)
        {
            raster.Pixels[i] = r;
            raster.Pixels[i + 1] = g;
            raster.Pixels[i + 2] = b;
            raster.Pixels[i + 3] = a;
        }
        return raster;
    }

    /// <summary>An independent copy.</summary>
    public Raster Clone() => new(Width, Height, (byte[])Pixels.Clone());

    /// <summary>Gives each see-through pixel (alpha under a half) the colour of the nearest solid one, by a breadth-first walk out from the solid pixels. Alpha is left as it is.</summary>
    public void FillTransparent()
    {
        var seen = new bool[Width * Height];
        var queue = new Queue<int>();
        for (int i = 0; i < seen.Length; i++)
        {
            if (Pixels[i * 4 + 3] >= 128)
            {
                seen[i] = true;
                queue.Enqueue(i);
            }
        }
        while (queue.Count > 0)
        {
            int at = queue.Dequeue(), x = at % Width;
            foreach (int next in new[] { x > 0 ? at - 1 : -1, x < Width - 1 ? at + 1 : -1, at - Width, at + Width })
            {
                if (next < 0 || next >= seen.Length || seen[next])
                    continue;
                seen[next] = true;
                Array.Copy(Pixels, at * 4, Pixels, next * 4, 3);
                queue.Enqueue(next);
            }
        }
    }

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>) as <c>0xRRGGBBAA</c> (y from the top).</summary>
    public uint Get(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return ((uint)Pixels[i] << 24) | ((uint)Pixels[i + 1] << 16) | ((uint)Pixels[i + 2] << 8) | Pixels[i + 3];
    }

    /// <summary>Sets the pixel at (<paramref name="x"/>, <paramref name="y"/>) from <c>0xRRGGBBAA</c>.</summary>
    public void Set(int x, int y, uint rgba)
    {
        int i = (y * Width + x) * 4;
        Pixels[i] = (byte)(rgba >> 24);
        Pixels[i + 1] = (byte)(rgba >> 16);
        Pixels[i + 2] = (byte)(rgba >> 8);
        Pixels[i + 3] = (byte)rgba;
    }

    /// <summary>The rectangle (<paramref name="x"/>, <paramref name="y"/>, <paramref name="width"/>, <paramref name="height"/>) as a new raster; pixels outside this one are transparent.</summary>
    public Raster Crop(int x, int y, int width, int height)
    {
        var result = new Raster(width, height);
        for (int row = 0; row < height; row++)
        {
            int sy = y + row;
            if (sy < 0 || sy >= Height)
                continue;
            int from = Math.Max(0, -x), to = Math.Min(width, Width - x);
            if (to <= from)
                continue;
            Buffer.BlockCopy(Pixels, (sy * Width + x + from) * 4, result.Pixels, (row * width + from) * 4, (to - from) * 4);
        }
        return result;
    }

    /// <summary>True when no pixel has any alpha.</summary>
    public bool IsEmpty()
    {
        for (int i = 3; i < Pixels.Length; i += 4)
        {
            if (Pixels[i] != 0)
                return false;
        }
        return true;
    }

    /// <summary>The share of a picture's drawn pixels that are see-through for it to be drawn blended (<see cref="IsTranslucent"/>).</summary>
    public const double TranslucentShare = 0.25;

    /// <summary>
    /// True when at least <see cref="TranslucentShare"/> of the drawn pixels (alpha above 0) are partly see-through (alpha below 255): a
    /// picture tg means to be seen through, as BYOND blends it (the extinguisher's water puff, alpha 11 to 153; the ion trail's sparkles,
    /// 200). The client draws such a picture blended; a sprite with a few soft edge pixels keeps the opaque pass and its alpha scissor.
    /// </summary>
    public bool IsTranslucent()
    {
        int drawn = 0, partial = 0;
        for (int i = 3; i < Pixels.Length; i += 4)
        {
            byte a = Pixels[i];
            if (a == 0)
                continue;
            drawn++;
            if (a < 255)
                partial++;
        }
        return drawn > 0 && partial >= drawn * TranslucentShare;
    }

    /// <summary>
    /// BYOND <c>icon.Blend(icon, blend, x, y)</c>: blends <paramref name="source"/> onto this picture with its upper left corner at
    /// (<paramref name="dx"/>, <paramref name="dy"/>), clipped to this picture. Overlay and underlay use alpha; add, subtract,
    /// multiply, and and or work on the four channels.
    /// </summary>
    public void Blend(Raster source, IconBlend mode, int dx = 0, int dy = 0)
    {
        for (int sy = 0; sy < source.Height; sy++)
        {
            int y = sy + dy;
            if (y < 0 || y >= Height)
                continue;
            for (int sx = 0; sx < source.Width; sx++)
            {
                int x = sx + dx;
                if (x < 0 || x >= Width)
                    continue;
                int s = (sy * source.Width + sx) * 4, d = (y * Width + x) * 4;
                BlendPixel(Pixels, d, source.Pixels, s, mode);
            }
        }
    }

    /// <summary>BYOND <c>icon.Blend(color, ICON_MULTIPLY)</c> style tint: every channel times the colour's channel (<c>0xRRGGBBAA</c>).</summary>
    public void Multiply(uint rgba)
    {
        int r = (int)(rgba >> 24) & 0xFF, g = (int)(rgba >> 16) & 0xFF, b = (int)(rgba >> 8) & 0xFF, a = (int)rgba & 0xFF;
        if (r == 255 && g == 255 && b == 255 && a == 255)
            return;
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            Pixels[i] = Mul(Pixels[i], r);
            Pixels[i + 1] = Mul(Pixels[i + 1], g);
            Pixels[i + 2] = Mul(Pixels[i + 2], b);
            Pixels[i + 3] = Mul(Pixels[i + 3], a);
        }
    }

    /// <summary>
    /// BYOND <c>icon.MapColors</c> and a colour matrix (<see cref="ColorMatrix"/>): every pixel's channels become the matrix applied to them.
    /// </summary>
    public void Map(ColorMatrix matrix)
    {
        if (matrix.IsIdentity)
            return;
        for (int i = 0; i < Pixels.Length; i += 4)
            matrix.Apply(Pixels, i);
    }

    /// <summary>
    /// Syndicate G4c: BYOND's <c>SwapColor</c>: every pixel of <paramref name="from"/> (0xRRGGBBAA; its alpha compared only when
    /// <paramref name="matchAlpha"/>, else any visible pixel of its colour) becomes <paramref name="to"/>.
    /// </summary>
    public void SwapColor(uint from, bool matchAlpha, uint to)
    {
        byte fr = (byte)(from >> 24), fg = (byte)(from >> 16), fb = (byte)(from >> 8), fa = (byte)from;
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            if (Pixels[i] != fr || Pixels[i + 1] != fg || Pixels[i + 2] != fb)
                continue;
            if (matchAlpha ? Pixels[i + 3] != fa : Pixels[i + 3] == 0)
                continue;
            Pixels[i] = (byte)(to >> 24);
            Pixels[i + 1] = (byte)(to >> 16);
            Pixels[i + 2] = (byte)(to >> 8);
            Pixels[i + 3] = (byte)to;
        }
    }

    /// <summary>Multiplies the alpha by <paramref name="alpha"/> (0 to 255 as 0 to 1).</summary>
    public void MultiplyAlpha(int alpha)
    {
        if (alpha >= 255)
            return;
        for (int i = 3; i < Pixels.Length; i += 4)
            Pixels[i] = Mul(Pixels[i], alpha);
    }

    internal static byte Mul(int a, int b) => (byte)((a * b + 127) / 255);

    internal static void BlendPixel(byte[] dst, int d, byte[] src, int s, IconBlend mode)
    {
        int sr = src[s], sg = src[s + 1], sb = src[s + 2], sa = src[s + 3];
        int dr = dst[d], dg = dst[d + 1], db = dst[d + 2], da = dst[d + 3];
        switch (mode)
        {
            case IconBlend.Overlay:
                Over(dst, d, sr, sg, sb, sa, dr, dg, db, da);
                break;
            case IconBlend.Underlay:
                Over(dst, d, dr, dg, db, da, sr, sg, sb, sa);
                break;
            case IconBlend.Add:
                dst[d] = (byte)Math.Min(255, dr + sr);
                dst[d + 1] = (byte)Math.Min(255, dg + sg);
                dst[d + 2] = (byte)Math.Min(255, db + sb);
                dst[d + 3] = Mul(da, sa); // BYOND: transparent where either is (ref /icon/proc/Blend; OpenDream's IconBlendsColor)
                break;
            case IconBlend.Subtract:
                dst[d] = (byte)Math.Max(0, dr - sr);
                dst[d + 1] = (byte)Math.Max(0, dg - sg);
                dst[d + 2] = (byte)Math.Max(0, db - sb);
                dst[d + 3] = Mul(da, sa);
                break;
            case IconBlend.Multiply:
                dst[d] = Mul(dr, sr);
                dst[d + 1] = Mul(dg, sg);
                dst[d + 2] = Mul(db, sb);
                dst[d + 3] = Mul(da, sa);
                break;
            case IconBlend.And:
                dst[d] = (byte)(dr & sr);
                dst[d + 1] = (byte)(dg & sg);
                dst[d + 2] = (byte)(db & sb);
                dst[d + 3] = (byte)(da & sa);
                break;
            case IconBlend.Or:
                dst[d] = (byte)(dr | sr);
                dst[d + 1] = (byte)(dg | sg);
                dst[d + 2] = (byte)(db | sb);
                dst[d + 3] = (byte)(da | sa);
                break;
        }
    }

    // "Top over bottom" with straight alpha: the Porter-Duff over operator, rounded to the nearest integer.
    private static void Over(byte[] dst, int d, int tr, int tg, int tb, int ta, int br, int bg, int bb, int ba)
    {
        if (ta == 255 || ba == 0)
        {
            dst[d] = (byte)tr;
            dst[d + 1] = (byte)tg;
            dst[d + 2] = (byte)tb;
            dst[d + 3] = (byte)ta;
            return;
        }
        if (ta == 0)
        {
            dst[d] = (byte)br;
            dst[d + 1] = (byte)bg;
            dst[d + 2] = (byte)bb;
            dst[d + 3] = (byte)ba;
            return;
        }
        // The bottom's contribution is weighted by what the top lets through.
        int through = ba * (255 - ta);
        int outA255 = ta * 255 + through;          // alpha scaled by 255
        int outA = (outA255 + 127) / 255;
        dst[d] = (byte)((tr * ta * 255 + br * through + outA255 / 2) / outA255);
        dst[d + 1] = (byte)((tg * ta * 255 + bg * through + outA255 / 2) / outA255);
        dst[d + 2] = (byte)((tb * ta * 255 + bb * through + outA255 / 2) / outA255);
        dst[d + 3] = (byte)outA;
    }
}

/// <summary>
/// A colour matrix as BYOND's <c>color</c> var and <c>icon.MapColors</c> take it, in floats of 0 to 1: output channel
/// <c>o</c> is <c>R*m[R,o] + G*m[G,o] + B*m[B,o] + A*m[A,o] + c[o]</c>. The 20 numbers of a list are the rows red, green,
/// blue, alpha and the constants, each row the output red, green, blue, alpha (BYOND ref "color matrix"; the order that
/// <c>AppearanceEntry.ColorMatrix</c> normalizes to).
/// </summary>
public readonly struct ColorMatrix
{
    private readonly float[] _m; // null: the identity

    /// <summary>The identity: no change.</summary>
    public static ColorMatrix Identity => default;

    private ColorMatrix(float[] m) => _m = m;

    /// <summary>True for the identity.</summary>
    public bool IsIdentity => _m == null;

    /// <summary>
    /// From BYOND's list forms of a colour matrix: 20 numbers (rows red, green, blue, alpha, constants; four columns), 16 (the same
    /// without the constants), 12 (rows red, green, blue and constants, three columns: alpha passes through) or 9 (rows red, green, blue).
    /// Null or any other length gives the identity.
    /// </summary>
    public static ColorMatrix FromList(IReadOnlyList<float> list)
    {
        if (list == null)
            return Identity;
        var m = new float[20];
        switch (list.Count)
        {
            case 20:
                for (int i = 0; i < 20; i++)
                    m[i] = list[i];
                break;
            case 16:
                for (int i = 0; i < 16; i++)
                    m[i] = list[i];
                break;
            case 12:
            case 9:
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                        m[row * 4 + col] = list[row * 3 + col];
                }
                m[15] = 1;
                if (list.Count == 12)
                {
                    for (int col = 0; col < 3; col++)
                        m[16 + col] = list[9 + col];
                }
                break;
            default:
                return Identity;
        }
        return new ColorMatrix(m);
    }

    /// <summary>The matrix of a flat tint (<c>0xRRGGBBAA</c>): every channel times the colour's channel.</summary>
    public static ColorMatrix Tint(uint rgba)
    {
        if (rgba == 0xFFFFFFFF)
            return Identity;
        var m = new float[20];
        m[0] = ((rgba >> 24) & 0xFF) / 255f;
        m[5] = ((rgba >> 16) & 0xFF) / 255f;
        m[10] = ((rgba >> 8) & 0xFF) / 255f;
        m[15] = (rgba & 0xFF) / 255f;
        return new ColorMatrix(m);
    }

    /// <summary>"First this one, then <paramref name="then"/>" (the composition of two colour steps).</summary>
    public ColorMatrix Then(ColorMatrix then)
    {
        if (IsIdentity)
            return then;
        if (then.IsIdentity)
            return this;
        var result = new float[20];
        // Rows 0..3 map an input channel to outputs; row 4 is the constant added after.
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                float sum = 0;
                for (int k = 0; k < 4; k++)
                    sum += _m[row * 4 + k] * then._m[k * 4 + col];
                result[row * 4 + col] = sum;
            }
        }
        for (int col = 0; col < 4; col++)
        {
            float sum = then._m[16 + col];
            for (int k = 0; k < 4; k++)
                sum += _m[16 + k] * then._m[k * 4 + col];
            result[16 + col] = sum;
        }
        return new ColorMatrix(result);
    }

    /// <summary>Applies the matrix to the pixel at <paramref name="index"/> of an RGBA byte array.</summary>
    public void Apply(byte[] pixels, int index)
    {
        if (_m == null)
            return;
        float r = pixels[index] / 255f, g = pixels[index + 1] / 255f, b = pixels[index + 2] / 255f, a = pixels[index + 3] / 255f;
        pixels[index] = Clamp(r * _m[0] + g * _m[4] + b * _m[8] + a * _m[12] + _m[16]);
        pixels[index + 1] = Clamp(r * _m[1] + g * _m[5] + b * _m[9] + a * _m[13] + _m[17]);
        pixels[index + 2] = Clamp(r * _m[2] + g * _m[6] + b * _m[10] + a * _m[14] + _m[18]);
        pixels[index + 3] = Clamp(r * _m[3] + g * _m[7] + b * _m[11] + a * _m[15] + _m[19]);
    }

    private static byte Clamp(float v) => v <= 0 ? (byte)0 : v >= 1 ? (byte)255 : (byte)(v * 255f + 0.5f);
}
