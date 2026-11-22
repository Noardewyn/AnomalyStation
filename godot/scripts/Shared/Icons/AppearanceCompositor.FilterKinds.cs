using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

// Owner: M6 slice 7b (D156, protocol version 26). The filter types of BYOND that version 25 did not draw (BYOND ref "Filter effects":
// the blurs, bloom, drop shadow, layering, rays, ripple, wave; OpenDream's greyscale) and the colour filter's other colour spaces,
// drawn on the CPU over premultiplied float pictures. The reference gives each filter's parameters and what it draws but not BYOND's
// kernels, so these are this client's (D156): a blur of size s is a Gaussian of standard deviation s / 2 (BYOND: "roughly pixels",
// sizes adding as sqrt(A^2 + B^2)); the rays' pattern is a hash of offset (the "time") and the ray's cell, eased between whole times;
// HCY's chroma is max - min. The ripple and the wave follow the reference's equations. "Centre" is the icon square's middle (BYOND: the
// image's centre), x and y are right and up.
public sealed partial class AppearanceCompositor
{
    private const int MaxFilterGrow = 48;

    // A picture as premultiplied floats 0..1, rows from the top (as Raster).
    private sealed class Plane
    {
        public readonly int W, H;
        public readonly float[] R, G, B, A;

        public Plane(int w, int h)
        {
            W = w;
            H = h;
            R = new float[w * h];
            G = new float[w * h];
            B = new float[w * h];
            A = new float[w * h];
        }

        public static Plane Of(Raster image, int grow)
        {
            var p = new Plane(image.Width + 2 * grow, image.Height + 2 * grow);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    uint c = image.Get(x, y);
                    float a = (c & 255) / 255f;
                    int i = (y + grow) * p.W + x + grow;
                    p.A[i] = a;
                    p.R[i] = (c >> 24) / 255f * a;
                    p.G[i] = ((c >> 16) & 255) / 255f * a;
                    p.B[i] = ((c >> 8) & 255) / 255f * a;
                }
            }
            return p;
        }

        public Plane Blank() => new(W, H);

        public Plane Copy()
        {
            var p = new Plane(W, H);
            Array.Copy(R, p.R, R.Length);
            Array.Copy(G, p.G, G.Length);
            Array.Copy(B, p.B, B.Length);
            Array.Copy(A, p.A, A.Length);
            return p;
        }

        public Raster ToRaster()
        {
            var image = new Raster(W, H);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float a = Math.Clamp(A[i], 0f, 1f);
                    if (a <= 0f)
                        continue;
                    uint r = Byte(R[i] / a), g = Byte(G[i] / a), b = Byte(B[i] / a);
                    image.Set(x, y, (r << 24) | (g << 16) | (b << 8) | Byte(a));
                }
            }
            return image;
        }

        // Bilinear sample at a point in pixel units (pixel centres at +0.5); transparent outside.
        public void Sample(float x, float y, out float r, out float g, out float b, out float a)
        {
            x -= 0.5f;
            y -= 0.5f;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            r = g = b = a = 0f;
            Add(x0, y0, (1 - fx) * (1 - fy), ref r, ref g, ref b, ref a);
            Add(x0 + 1, y0, fx * (1 - fy), ref r, ref g, ref b, ref a);
            Add(x0, y0 + 1, (1 - fx) * fy, ref r, ref g, ref b, ref a);
            Add(x0 + 1, y0 + 1, fx * fy, ref r, ref g, ref b, ref a);
        }

        private void Add(int x, int y, float w, ref float r, ref float g, ref float b, ref float a)
        {
            if (w <= 0f || x < 0 || y < 0 || x >= W || y >= H)
                return;
            int i = y * W + x;
            r += R[i] * w;
            g += G[i] * w;
            b += B[i] * w;
            a += A[i] * w;
        }
    }

    private static uint Byte(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    private static ComposedRaster Result(ComposedRaster picture, Plane plane, int grow)
        => new() { Image = plane.ToRaster(), OriginX = picture.OriginX + grow, OriginY = picture.OriginY + grow, IsAnimated = picture.IsAnimated };

    // The centre of an effect in plane pixels (x right, y down from the top), moved by the filter's x and y (right and up).
    private static (float X, float Y) Centre(ComposedRaster picture, Plane plane, int grow, float dx, float dy)
    {
        float cx = picture.OriginX + grow + DefaultIconSize / 2f + dx;
        float cyUp = picture.OriginY + grow + DefaultIconSize / 2f + dy;
        return (cx, plane.H - cyUp);
    }

    private static int BlurReach(float size) => Math.Min(MaxFilterGrow, (int)MathF.Ceiling(Math.Abs(size) * 1.5f));

    // ---- blurs ----

    // A separable Gaussian of standard deviation size / 2 over the four channels.
    private static Plane GaussianBlur(Plane p, float size)
    {
        float sigma = Math.Abs(size) / 2f;
        int reach = BlurReach(size);
        if (sigma <= 0.01f || reach == 0)
            return p;
        float[] kernel = Kernel(sigma, reach);
        Plane across = p.Blank(), down = p.Blank();
        Convolve(p, across, kernel, reach, 1, 0);
        Convolve(across, down, kernel, reach, 0, 1);
        return down;
    }

    private static float[] Kernel(float sigma, int reach)
    {
        var k = new float[2 * reach + 1];
        float sum = 0f;
        for (int i = -reach; i <= reach; i++)
            sum += k[i + reach] = MathF.Exp(-i * i / (2f * sigma * sigma));
        for (int i = 0; i < k.Length; i++)
            k[i] /= sum;
        return k;
    }

    private static void Convolve(Plane from, Plane into, float[] kernel, int reach, int stepX, int stepY)
    {
        for (int y = 0; y < from.H; y++)
        {
            for (int x = 0; x < from.W; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int k = -reach; k <= reach; k++)
                {
                    int sx = x + k * stepX, sy = y + k * stepY;
                    if (sx < 0 || sy < 0 || sx >= from.W || sy >= from.H)
                        continue;
                    float w = kernel[k + reach];
                    int j = sy * from.W + sx;
                    r += from.R[j] * w;
                    g += from.G[j] * w;
                    b += from.B[j] * w;
                    a += from.A[j] * w;
                }
                int i = y * from.W + x;
                into.R[i] = r;
                into.G[i] = g;
                into.B[i] = b;
                into.A[i] = a;
            }
        }
    }

    // BYOND "blur": a Gaussian of size pixels; the picture grows by the blur's reach.
    private static ComposedRaster Blur(ComposedRaster picture, FilterEntry f)
    {
        int grow = BlurReach(f.Size);
        if (grow == 0)
            return picture;
        return Result(picture, GaussianBlur(Plane.Of(picture.Image, grow), f.Size), grow);
    }

    // A Gaussian along a direction at every pixel: (dirX, dirY) per pixel in plane units and the standard deviation in pixels.
    private delegate void BlurAt(float x, float y, out float dirX, out float dirY, out float sigma);

    private static Plane DirectionalBlur(Plane p, BlurAt at)
    {
        Plane result = p.Blank();
        for (int y = 0; y < p.H; y++)
        {
            for (int x = 0; x < p.W; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                at(px, py, out float dx, out float dy, out float sigma);
                int i = y * p.W + x;
                if (sigma < 0.05f)
                {
                    result.R[i] = p.R[i];
                    result.G[i] = p.G[i];
                    result.B[i] = p.B[i];
                    result.A[i] = p.A[i];
                    continue;
                }
                int samples = Math.Clamp((int)MathF.Ceiling(sigma * 3f), 1, MaxFilterGrow);
                float tr = 0, tg = 0, tb = 0, ta = 0, total = 0;
                for (int k = -samples; k <= samples; k++)
                {
                    float t = k * (sigma * 3f / samples);
                    float w = MathF.Exp(-t * t / (2f * sigma * sigma));
                    p.Sample(px + dx * t, py + dy * t, out float r, out float g, out float b, out float a);
                    tr += r * w;
                    tg += g * w;
                    tb += b * w;
                    ta += a * w;
                    total += w;
                }
                result.R[i] = tr / total;
                result.G[i] = tg / total;
                result.B[i] = tb / total;
                result.A[i] = ta / total;
            }
        }
        return result;
    }

    // BYOND "motion_blur": a Gaussian along x, y (up is up), its size their length.
    private static ComposedRaster MotionBlur(ComposedRaster picture, FilterEntry f)
    {
        float length = MathF.Sqrt(f.X * f.X + f.Y * f.Y);
        int grow = BlurReach(length);
        if (grow == 0)
            return picture;
        float ux = f.X / length, uy = -f.Y / length, sigma = length / 2f;
        Plane blurred = DirectionalBlur(Plane.Of(picture.Image, grow), (float x, float y, out float dx, out float dy, out float s) =>
        {
            dx = ux;
            dy = uy;
            s = sigma;
        });
        return Result(picture, blurred, grow);
    }

    // BYOND "angular_blur": along the circle round the centre; size is degrees of blur, offset the radius left sharp (a pixel past it
    // blurs as if offset pixels nearer).
    private static ComposedRaster AngularBlur(ComposedRaster picture, FilterEntry f)
    {
        if (Math.Abs(f.Size) < 0.001f)
            return picture;
        Plane p = Plane.Of(picture.Image, 0);
        (float cx, float cy) = Centre(picture, p, 0, f.X, f.Y);
        float radians = Math.Abs(f.Size) * MathF.PI / 180f / 2f;
        Plane blurred = DirectionalBlur(p, (float x, float y, out float dx, out float dy, out float sigma) =>
        {
            float vx = x - cx, vy = y - cy, d = MathF.Sqrt(vx * vx + vy * vy);
            float reach = MathF.Max(0f, d - f.Offset);
            if (d < 0.001f || reach <= 0f)
            {
                dx = dy = sigma = 0f;
                return;
            }
            dx = -vy / d; // the tangent
            dy = vx / d;
            sigma = reach * radians;
        });
        return Result(picture, blurred, 0);
    }

    // BYOND "radial_blur": outward from the centre, size per pixel of distance past offset.
    private static ComposedRaster RadialBlur(ComposedRaster picture, FilterEntry f)
    {
        if (Math.Abs(f.Size) < 0.00001f)
            return picture;
        Plane p = Plane.Of(picture.Image, 0);
        (float cx, float cy) = Centre(picture, p, 0, f.X, f.Y);
        Plane blurred = DirectionalBlur(p, (float x, float y, out float dx, out float dy, out float sigma) =>
        {
            float vx = x - cx, vy = y - cy, d = MathF.Sqrt(vx * vx + vy * vy);
            float reach = MathF.Max(0f, d - f.Offset);
            if (d < 0.001f || reach <= 0f)
            {
                dx = dy = sigma = 0f;
                return;
            }
            dx = vx / d;
            dy = vy / d;
            sigma = Math.Abs(f.Size) * reach / 2f;
        });
        return Result(picture, blurred, 0);
    }

    // ---- shapes, glows and shadows ----

    // The alpha grown by a radius (as the outline grows: round), in one colour.
    private static Plane Grown(Plane p, float radius, float r, float g, float b, float a)
    {
        Plane result = p.Blank();
        int reach = (int)MathF.Ceiling(Math.Max(0f, radius));
        for (int y = 0; y < p.H; y++)
        {
            for (int x = 0; x < p.W; x++)
            {
                float near = 0f;
                for (int oy = -reach; oy <= reach && near < 1f; oy++)
                {
                    for (int ox = -reach; ox <= reach; ox++)
                    {
                        if (ox * ox + oy * oy > radius * radius + 0.001f)
                            continue;
                        int sx = x + ox, sy = y + oy;
                        if (sx < 0 || sy < 0 || sx >= p.W || sy >= p.H)
                            continue;
                        near = MathF.Max(near, p.A[sy * p.W + sx]);
                    }
                }
                int i = y * p.W + x;
                float alpha = near * a;
                result.A[i] = alpha;
                result.R[i] = r * alpha;
                result.G[i] = g * alpha;
                result.B[i] = b * alpha;
            }
        }
        return result;
    }

    private static void Channels(uint rgba, out float r, out float g, out float b, out float a)
    {
        r = (rgba >> 24) / 255f;
        g = ((rgba >> 16) & 255) / 255f;
        b = ((rgba >> 8) & 255) / 255f;
        a = (rgba & 255) / 255f;
    }

    // Source over destination, premultiplied, the source moved by whole plane pixels.
    private static void Over(Plane top, Plane under, int dx = 0, int dy = 0)
    {
        for (int y = 0; y < under.H; y++)
        {
            for (int x = 0; x < under.W; x++)
            {
                int sx = x - dx, sy = y - dy;
                if (sx < 0 || sy < 0 || sx >= top.W || sy >= top.H)
                    continue;
                int i = y * under.W + x, j = sy * top.W + sx;
                float k = 1f - top.A[j];
                under.R[i] = top.R[j] + under.R[i] * k;
                under.G[i] = top.G[j] + under.G[i] * k;
                under.B[i] = top.B[j] + under.B[i] * k;
                under.A[i] = top.A[j] + under.A[i] * k;
            }
        }
    }

    // BYOND "drop_shadow": the shape grown by offset, blurred by size, in the colour, moved by x, y, under the picture; a negative size
    // draws it inside the picture instead (an inset shadow).
    private static ComposedRaster DropShadow(ComposedRaster picture, FilterEntry f)
    {
        Channels(f.Rgba, out float r, out float g, out float b, out float a);
        if (a <= 0f)
            return picture;
        bool inset = f.Size < 0f;
        int grow = inset ? 0 : Math.Min(MaxFilterGrow, (int)MathF.Ceiling(Math.Max(0f, f.Offset) + BlurReach(f.Size) + Math.Max(Math.Abs(f.X), Math.Abs(f.Y))));
        Plane image = Plane.Of(picture.Image, grow);
        int dx = (int)MathF.Round(f.X), dy = -(int)MathF.Round(f.Y);
        if (!inset)
        {
            Plane shadow = GaussianBlur(Grown(image, Math.Max(0f, f.Offset), r, g, b, a), f.Size);
            var moved = shadow.Blank();
            Over(shadow, moved, dx, dy);
            Over(image, moved);
            return Result(picture, moved, grow);
        }
        // inset: the outside of the shape, grown inward, blurred, kept where the picture is and drawn over it
        Plane outside = image.Blank();
        for (int i = 0; i < outside.A.Length; i++)
            outside.A[i] = 1f - image.A[i];
        Plane spread = GaussianBlur(Grown(outside, Math.Max(0f, f.Offset), r, g, b, a), f.Size);
        Plane inner = image.Blank();
        Over(spread, inner, dx, dy);
        for (int i = 0; i < inner.A.Length; i++)
        {
            float keep = image.A[i];
            inner.R[i] *= keep;
            inner.G[i] *= keep;
            inner.B[i] *= keep;
            inner.A[i] *= keep;
        }
        Plane result = image.Copy();
        Over(inner, result);
        return Result(picture, result, 0);
    }

    // BYOND "bloom": the pixels with any channel above the threshold's spread (grown by offset, blurred by size) and added, times alpha.
    private static ComposedRaster Bloom(ComposedRaster picture, FilterEntry f)
    {
        Channels(f.ThresholdRgba, out float tr, out float tg, out float tb, out _);
        int grow = Math.Min(MaxFilterGrow, (int)MathF.Ceiling(Math.Max(0f, f.Offset)) + BlurReach(f.Size));
        Plane image = Plane.Of(picture.Image, grow);
        Plane bright = image.Blank();
        for (int i = 0; i < image.A.Length; i++)
        {
            float a = image.A[i];
            if (a <= 0f)
                continue;
            float r = image.R[i] / a, g = image.G[i] / a, b = image.B[i] / a;
            if (r > tr || g > tg || b > tb)
            {
                bright.R[i] = image.R[i];
                bright.G[i] = image.G[i];
                bright.B[i] = image.B[i];
                bright.A[i] = a;
            }
        }
        if (f.Offset > 0f)
            bright = GrownColour(bright, f.Offset);
        Plane glow = GaussianBlur(bright, f.Size);
        float strength = Math.Clamp(f.Alpha, 0f, 255f) / 255f;
        for (int i = 0; i < image.A.Length; i++)
        {
            image.R[i] += glow.R[i] * strength;
            image.G[i] += glow.G[i] * strength;
            image.B[i] += glow.B[i] * strength;
            image.A[i] = Math.Min(1f, image.A[i] + glow.A[i] * strength);
        }
        return Result(picture, image, grow);
    }

    // Each pixel takes the brightest (most opaque) neighbour within the radius, colours kept.
    private static Plane GrownColour(Plane p, float radius)
    {
        Plane result = p.Blank();
        int reach = (int)MathF.Ceiling(radius);
        for (int y = 0; y < p.H; y++)
        {
            for (int x = 0; x < p.W; x++)
            {
                int best = -1;
                for (int oy = -reach; oy <= reach; oy++)
                {
                    for (int ox = -reach; ox <= reach; ox++)
                    {
                        if (ox * ox + oy * oy > radius * radius + 0.001f)
                            continue;
                        int sx = x + ox, sy = y + oy;
                        if (sx < 0 || sy < 0 || sx >= p.W || sy >= p.H)
                            continue;
                        int j = sy * p.W + sx;
                        if (best < 0 || p.A[j] > p.A[best])
                            best = j;
                    }
                }
                int i = y * p.W + x;
                if (best >= 0)
                {
                    result.R[i] = p.R[best];
                    result.G[i] = p.G[best];
                    result.B[i] = p.B[best];
                    result.A[i] = p.A[best];
                }
            }
        }
        return result;
    }

    // ---- layering ----

    // BYOND "layer": a second picture (its icon square on this one's, moved by x and y), its colour and transform (about its centre,
    // bilinear), over the picture (FILTER_OVERLAY) or under it (FILTER_UNDERLAY); blend_mode applies to the top one.
    private static ComposedRaster Layer(ComposedRaster picture, ComposedRaster second, FilterEntry f)
    {
        Raster image = second.Image;
        if (f.ColorMatrix != null)
        {
            image = image.Clone();
            image.Map(ColorMatrix.FromList(f.ColorMatrix));
        }
        // the second picture's pixels in this picture's frame (bottom-up), after its transform about its icon square's centre
        float a = 1, b = 0, c = 0, d = 0, e = 1, ff = 0;
        if (f.Transform is { Length: 6 } t)
            (a, b, c, d, e, ff) = (t[0], t[1], t[2], t[3], t[4], t[5]);
        float det = a * e - b * d;
        if (MathF.Abs(det) < 1e-6f)
            return picture;
        // where the transformed second picture can reach: its corners
        float scx = second.OriginX + DefaultIconSize / 2f, scy = second.OriginY + DefaultIconSize / 2f;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach ((float px, float py) in new[] { (0f, 0f), (image.Width, 0f), (0f, image.Height), (image.Width, image.Height) })
        {
            float vx = px - scx, vy = py - scy;
            float tx = a * vx + b * vy + c, ty = d * vx + e * vy + ff;
            float ox = tx + picture.OriginX + DefaultIconSize / 2f + f.X, oy = ty + picture.OriginY + DefaultIconSize / 2f + f.Y;
            minX = MathF.Min(minX, ox);
            minY = MathF.Min(minY, oy);
            maxX = MathF.Max(maxX, ox);
            maxY = MathF.Max(maxY, oy);
        }
        int left = Math.Max(0, Math.Min(MaxFilterGrow, (int)MathF.Ceiling(-minX))), bottom = Math.Max(0, Math.Min(MaxFilterGrow, (int)MathF.Ceiling(-minY)));
        int right = Math.Max(0, Math.Min(MaxFilterGrow, (int)MathF.Ceiling(maxX - picture.Image.Width)));
        int top = Math.Max(0, Math.Min(MaxFilterGrow, (int)MathF.Ceiling(maxY - picture.Image.Height)));
        int width = picture.Image.Width + left + right, height = picture.Image.Height + bottom + top;
        var placed = new Raster(width, height);
        var source = Plane.Of(image, 0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // this pixel's centre relative to the second picture's centre in the result's frame, then the inverse transform
                float ux = x + 0.5f - left - picture.OriginX - DefaultIconSize / 2f - f.X - c;
                float uy = (height - y - 0.5f) - bottom - picture.OriginY - DefaultIconSize / 2f - f.Y - ff;
                float vx = (e * ux - b * uy) / det, vy = (-d * ux + a * uy) / det;
                float sx = vx + scx, syUp = vy + scy;
                source.Sample(sx, image.Height - syUp, out float r, out float g, out float bl, out float al);
                if (al <= 0.002f)
                    continue;
                placed.Set(x, y, (Byte(r / al) << 24) | (Byte(g / al) << 16) | (Byte(bl / al) << 8) | Byte(al));
            }
        }
        var result = new Raster(width, height);
        int blend = f.BlendMode is >= 1 and <= 5 ? f.BlendMode : 1;
        if ((f.Flags & FilterFlags.Underlay) != 0)
        {
            Join(result, placed, 0, 0, 1);
            Join(result, picture.Image, left, top, blend);
        }
        else
        {
            Join(result, picture.Image, left, top, 1);
            Join(result, placed, 0, 0, blend);
        }
        return new ComposedRaster { Image = result, OriginX = picture.OriginX + left, OriginY = picture.OriginY + bottom, IsAnimated = picture.IsAnimated || second.IsAnimated };
    }

    // Draws a picture onto another at (dx, dy) from the top left with BYOND's blend_mode (DrawPixel).
    private static void Join(Raster below, Raster top, int dx, int dy, int blend)
    {
        for (int y = 0; y < top.Height; y++)
        {
            int ty = y + dy;
            if (ty < 0 || ty >= below.Height)
                continue;
            for (int x = 0; x < top.Width; x++)
            {
                int tx = x + dx;
                if (tx < 0 || tx >= below.Width)
                    continue;
                DrawPixel(below.Pixels, (ty * below.Width + tx) * 4, top.Pixels, (y * top.Width + x) * 4, blend);
            }
        }
    }

    // ---- rays ----

    // BYOND "rays": rays from the centre (x, y) up to size long, their alpha falling linearly outward, in the colour; offset is the time
    // (a whole step is a new set; repeats after 1000), density the number of ray cells round the circle, threshold culls weak rays
    // (the rest rescaled to 0..1), factor ties length to strength. Flags: over, under (an image's default) or alone.
    private static ComposedRaster Rays(ComposedRaster picture, FilterEntry f)
    {
        Channels(f.Rgba, out float r, out float g, out float b, out float a);
        float size = Math.Max(0f, f.Size);
        float reachX = Math.Abs(f.X) + size - DefaultIconSize / 2f, reachY = Math.Abs(f.Y) + size - DefaultIconSize / 2f;
        int grow = Math.Clamp((int)MathF.Ceiling(Math.Max(reachX, reachY)), 0, MaxFilterGrow);
        Plane image = Plane.Of(picture.Image, grow);
        (float cx, float cy) = Centre(picture, image, grow, f.X, f.Y);
        Plane rays = image.Blank();
        int cells = Math.Max(1, (int)f.Density);
        float time = ((f.Offset % 1000f) + 1000f) % 1000f;
        int t0 = (int)MathF.Floor(time);
        float ease = time - t0;
        ease = ease * ease * (3f - 2f * ease);
        float threshold = Math.Clamp(f.Threshold, 0f, 0.999f), factor = Math.Clamp(f.Factor, 0f, 1f);
        for (int y = 0; y < image.H; y++)
        {
            for (int x = 0; x < image.W; x++)
            {
                float vx = x + 0.5f - cx, vy = cy - (y + 0.5f), d = MathF.Sqrt(vx * vx + vy * vy);
                if (size <= 0f || d >= size)
                    continue;
                float turn = (MathF.Atan2(vy, vx) / (2f * MathF.PI) + 1f) % 1f * cells;
                int cell = (int)MathF.Floor(turn) % cells;
                float across = turn - MathF.Floor(turn);
                float strength = Lerp(Hash(cell, t0), Hash(cell, t0 + 1), ease) * MathF.Pow(MathF.Sin(MathF.PI * across), 2f);
                strength = (strength - threshold) / (1f - threshold);
                if (strength <= 0f)
                    continue;
                float length = size * (1f - factor + factor * strength);
                if (d >= length)
                    continue;
                float alpha = a * strength * (1f - d / length);
                int i = y * image.W + x;
                rays.A[i] = alpha;
                rays.R[i] = r * alpha;
                rays.G[i] = g * alpha;
                rays.B[i] = b * alpha;
            }
        }
        bool over = (f.Flags & FilterFlags.Overlay) != 0, under = (f.Flags & FilterFlags.Underlay) != 0;
        if (!over && !under)
            return Result(picture, rays, grow); // alone: the rays erase the picture
        if (under) // an image's default (both flags) draws them beneath it
        {
            Over(image, rays);
            return Result(picture, rays, grow);
        }
        Over(rays, image);
        return Result(picture, image, grow);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // A number 0..1 from a ray cell and a whole time (the same pair gives the same number).
    private static float Hash(int cell, int time)
    {
        uint h = (uint)(cell * 374761393 + (time % 1000) * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0xFFFFFF;
    }

    // ---- distortions ----

    // Each pixel taken from where a displacement sends it (dx right, dy up, in pixels); unbounded, the picture grows by the reach.
    private delegate void Pull(float vx, float vy, out float dx, out float dy);

    private static ComposedRaster Distort(ComposedRaster picture, FilterEntry f, bool bounded, float reach, Pull pull)
    {
        int grow = bounded ? 0 : Math.Min(MaxFilterGrow, (int)MathF.Ceiling(Math.Abs(reach)));
        Plane image = Plane.Of(picture.Image, grow);
        (float cx, float cy) = Centre(picture, image, grow, f.X, f.Y);
        Plane result = image.Blank();
        for (int y = 0; y < image.H; y++)
        {
            for (int x = 0; x < image.W; x++)
            {
                float vx = x + 0.5f - cx, vy = cy - (y + 0.5f);
                pull(vx, vy, out float dx, out float dy);
                float sx = x + 0.5f + dx, sy = y + 0.5f - dy;
                if (bounded && (sx < grow || sy < grow || sx > image.W - grow || sy > image.H - grow))
                    continue;
                image.Sample(sx, sy, out float r, out float g, out float b, out float a);
                int i = y * image.W + x;
                result.R[i] = r;
                result.G[i] = g;
                result.B[i] = b;
                result.A[i] = a;
            }
        }
        return Result(picture, result, grow);
    }

    // BYOND "ripple": size x sin(2 pi r') / (2.5 x falloff x r'^2 + 1), r' = (radius - distance) / repeat, along the radius, inside
    // the ripple's outer radius.
    private static ComposedRaster Ripple(ComposedRaster picture, FilterEntry f)
    {
        if (Math.Abs(f.Size) < 0.001f || f.Repeat == 0f)
            return picture;
        bool bounded = (f.Flags & FilterFlags.WaveBounded) != 0;
        return Distort(picture, f, bounded, f.Size, (float vx, float vy, out float dx, out float dy) =>
        {
            float d = MathF.Sqrt(vx * vx + vy * vy);
            float rr = (f.Radius - d) / f.Repeat;
            if (d < 0.001f || rr < 0f)
            {
                dx = dy = 0f;
                return;
            }
            float amount = f.Size * MathF.Sin(2f * MathF.PI * rr) / (2.5f * f.Falloff * rr * rr + 1f);
            dx = vx / d * amount;
            dy = vy / d * amount;
        });
    }

    // BYOND "wave": size x sin(2 pi (d - offset)), d the periods from the centre along x, y (the period their length); along the wave,
    // or across it with WAVE_SIDEWAYS.
    private static ComposedRaster Wave(ComposedRaster picture, FilterEntry f)
    {
        float period = MathF.Sqrt(f.X * f.X + f.Y * f.Y);
        if (Math.Abs(f.Size) < 0.001f || period < 0.001f)
            return picture;
        float ux = f.X / period, uy = f.Y / period;
        bool sideways = (f.Flags & FilterFlags.WaveSideways) != 0, bounded = (f.Flags & FilterFlags.WaveBounded) != 0;
        var centred = new FilterEntry { X = 0, Y = 0 }; // the wave runs from the image's centre
        return Distort(picture, centred, bounded, f.Size, (float vx, float vy, out float dx, out float dy) =>
        {
            float along = (vx * ux + vy * uy) / period;
            float amount = f.Size * MathF.Sin(2f * MathF.PI * (along - f.Offset));
            dx = (sideways ? -uy : ux) * amount;
            dy = (sideways ? ux : uy) * amount;
        });
    }

    // ---- colour ----

    // OpenDream's "greyscale": the picture's luminance (0.299, 0.587, 0.114), alpha kept.
    private static ComposedRaster Greyscale(ComposedRaster picture)
    {
        Raster image = picture.Image.Clone();
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                uint c = image.Get(x, y);
                uint grey = Byte(((c >> 24) * 0.299f + ((c >> 16) & 255) * 0.587f + ((c >> 8) & 255) * 0.114f) / 255f);
                image.Set(x, y, (grey << 24) | (grey << 16) | (grey << 8) | (c & 255));
            }
        }
        return new ComposedRaster { Image = image, OriginX = picture.OriginX, OriginY = picture.OriginY, IsAnimated = picture.IsAnimated };
    }

    // BYOND "color" in another colour space (FILTER_COLOR_HSV 1, HSL 2, HCY 3): each pixel converted to the space (hue 0..1 of a turn,
    // the other components 0..1), the matrix applied, converted back (BYOND ref "Color matrix", "Other color spaces").
    private static ComposedRaster SpaceMatrix(ComposedRaster picture, float[] m, byte space)
    {
        Raster image = picture.Image.Clone();
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                uint c = image.Get(x, y);
                float a = (c & 255) / 255f;
                if (a <= 0f)
                    continue;
                float r = (c >> 24) / 255f, g = ((c >> 16) & 255) / 255f, b = ((c >> 8) & 255) / 255f;
                (float p, float q, float s) = ToSpace(r, g, b, space);
                float np = p * m[0] + q * m[4] + s * m[8] + a * m[12] + m[16];
                float nq = p * m[1] + q * m[5] + s * m[9] + a * m[13] + m[17];
                float ns = p * m[2] + q * m[6] + s * m[10] + a * m[14] + m[18];
                float na = p * m[3] + q * m[7] + s * m[11] + a * m[15] + m[19];
                (r, g, b) = FromSpace(np, nq, ns, space);
                image.Set(x, y, (Byte(r) << 24) | (Byte(g) << 16) | (Byte(b) << 8) | Byte(na));
            }
        }
        return new ComposedRaster { Image = image, OriginX = picture.OriginX, OriginY = picture.OriginY, IsAnimated = picture.IsAnimated };
    }

    private static float HueOf(float r, float g, float b, float max, float chroma)
    {
        if (chroma <= 0f) return 0f;
        float h = max == r ? (g - b) / chroma % 6f : max == g ? (b - r) / chroma + 2f : (r - g) / chroma + 4f;
        return (h / 6f + 1f) % 1f;
    }

    internal static (float, float, float) ToSpace(float r, float g, float b, byte space)
    {
        float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b)), chroma = max - min;
        float h = HueOf(r, g, b, max, chroma);
        switch (space)
        {
            case 1: // HSV
                return (h, max <= 0f ? 0f : chroma / max, max);
            case 2: // HSL
            {
                float l = (max + min) / 2f;
                float s = chroma <= 0f ? 0f : chroma / (1f - Math.Abs(2f * l - 1f));
                return (h, s, l);
            }
            default: // HCY
                return (h, chroma, 0.299f * r + 0.587f * g + 0.114f * b);
        }
    }

    internal static (float, float, float) FromSpace(float h, float p, float q, byte space)
    {
        h = (h % 1f + 1f) % 1f;
        p = Math.Clamp(p, 0f, 1f);
        q = Math.Clamp(q, 0f, 1f);
        float chroma, m;
        switch (space)
        {
            case 1: // HSV: p saturation, q value
                chroma = q * p;
                m = q - chroma;
                break;
            case 2: // HSL: p saturation, q luminance
                chroma = (1f - Math.Abs(2f * q - 1f)) * p;
                m = q - chroma / 2f;
                break;
            default: // HCY: p chroma, q luma
            {
                chroma = p;
                (float hr, float hg, float hb) = HueRgb(h, chroma);
                m = q - (0.299f * hr + 0.587f * hg + 0.114f * hb);
                return (Math.Clamp(hr + m, 0f, 1f), Math.Clamp(hg + m, 0f, 1f), Math.Clamp(hb + m, 0f, 1f));
            }
        }
        (float r, float g, float b) = HueRgb(h, chroma);
        return (Math.Clamp(r + m, 0f, 1f), Math.Clamp(g + m, 0f, 1f), Math.Clamp(b + m, 0f, 1f));
    }

    // The colour of a hue (0..1) with a chroma, its smallest channel 0.
    private static (float, float, float) HueRgb(float h, float chroma)
    {
        float hh = h * 6f, x = chroma * (1f - Math.Abs(hh % 2f - 1f));
        return (int)hh switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };
    }
}
