using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Icons;

// Owner: M6 slice 7 (D155, protocol version 25). BYOND's filters on a composed picture (BYOND ref "filters"; OpenDream's shaders in
// OpenDreamClient/Resources/Shaders): an alpha mask, an outline, a displacement map and a colour matrix, applied in the appearance's
// order to the picture of the appearance with its children. A mask, a map or a second picture is an icon state (frame 0, facing south)
// or a render target's picture; it is placed with its icon square on the picture's, moved by the filter's x and y. Version 26 (D156):
// the other types (AppearanceCompositor.FilterKinds.cs) and the outline's alpha (tg's, the health doll's pulse).
public sealed partial class AppearanceCompositor
{
    // The pictures of the render targets met during one composition, by name (a filter's render source looks them up).
    private ComposedRaster ApplyFilters(IAppearanceSource tables, IReadOnlyList<FilterEntry> filters, ComposedRaster picture, IReadOnlyDictionary<string, ComposedRaster> targets)
    {
        foreach (FilterEntry f in filters)
        {
            switch (f.Kind)
            {
                case FilterKind.Alpha:
                {
                    ComposedRaster mask = Source(tables, f, targets);
                    if (mask != null)
                        picture = AlphaMask(picture, mask, f);
                    break;
                }
                case FilterKind.Displace:
                {
                    ComposedRaster map = Source(tables, f, targets);
                    if (map != null)
                        picture = Displace(picture, map, f);
                    break;
                }
                case FilterKind.Outline:
                    picture = Outline(picture, f);
                    break;
                case FilterKind.Color:
                    if (f.ColorMatrix is { Length: 20 } && f.Space != 0)
                    {
                        picture = SpaceMatrix(picture, f.ColorMatrix, f.Space);
                    }
                    else if (f.ColorMatrix is { Length: 20 })
                    {
                        Raster mapped = picture.Image.Clone();
                        mapped.Map(ColorMatrix.FromList(f.ColorMatrix));
                        picture = new ComposedRaster { Image = mapped, OriginX = picture.OriginX, OriginY = picture.OriginY, IsAnimated = picture.IsAnimated };
                    }
                    break;
                case FilterKind.Layer:
                {
                    ComposedRaster second = Source(tables, f, targets);
                    if (second != null)
                        picture = Layer(picture, second, f);
                    break;
                }
                case FilterKind.Blur:
                    picture = Blur(picture, f);
                    break;
                case FilterKind.MotionBlur:
                    picture = MotionBlur(picture, f);
                    break;
                case FilterKind.AngularBlur:
                    picture = AngularBlur(picture, f);
                    break;
                case FilterKind.RadialBlur:
                    picture = RadialBlur(picture, f);
                    break;
                case FilterKind.DropShadow:
                    picture = DropShadow(picture, f);
                    break;
                case FilterKind.Bloom:
                    picture = Bloom(picture, f);
                    break;
                case FilterKind.Rays:
                    picture = Rays(picture, f);
                    break;
                case FilterKind.Ripple:
                    picture = Ripple(picture, f);
                    break;
                case FilterKind.Wave:
                    picture = Wave(picture, f);
                    break;
                case FilterKind.Greyscale:
                    picture = Greyscale(picture);
                    break;
            }
        }
        return picture;
    }

    // The mask or map of a filter: its render source's picture, else its icon's state.
    private ComposedRaster Source(IAppearanceSource tables, FilterEntry f, IReadOnlyDictionary<string, ComposedRaster> targets)
    {
        if (!f.RenderSource.IsNone)
        {
            string name = tables.ResolveString(f.RenderSource);
            return name != null && targets.TryGetValue(name, out ComposedRaster target) ? target : null;
        }
        if (f.Icon.IsNone)
            return null; // a mask, a map or a second picture of nothing: the picture stays as it is
        string icon = tables.ResolveResource(f.Icon);
        if (icon == null || !_library.TryGetIconNow(icon, out IIcon sheet, out _) || !sheet.TryGetShown(tables.ResolveString(f.IconState) ?? string.Empty, out IIconState state))
            return null;
        return new ComposedRaster { Image = state.Get(ByondDir.Index(ByondDir.South, state.Dirs), 0), OriginX = 0, OriginY = 0 };
    }

    // The source's pixel under the picture's pixel (column x, row y from the bottom), with the filter's offset; 0 outside.
    private static uint SourceAt(ComposedRaster picture, ComposedRaster source, int x, int y, float dx, float dy)
    {
        int sx = x - picture.OriginX + source.OriginX - (int)MathF.Round(dx);
        int sy = y - picture.OriginY + source.OriginY - (int)MathF.Round(dy);
        if (sx < 0 || sy < 0 || sx >= source.Image.Width || sy >= source.Image.Height)
            return 0;
        return source.Image.Get(sx, source.Image.Height - 1 - sy);
    }

    // BYOND "alpha": the picture's alpha times the mask's (MASK_INVERSE: times its inverse); MASK_SWAP draws the mask's colours
    // through the picture's alpha instead.
    private static ComposedRaster AlphaMask(ComposedRaster picture, ComposedRaster mask, FilterEntry f)
    {
        Raster image = picture.Image, result = image.Clone();
        bool inverse = (f.Flags & FilterFlags.MaskInverse) != 0, swap = (f.Flags & FilterFlags.MaskSwap) != 0;
        for (int y = 0; y < image.Height; y++)
        {
            int row = image.Height - 1 - y;
            for (int x = 0; x < image.Width; x++)
            {
                uint m = SourceAt(picture, mask, x, y, f.X, f.Y);
                int maskAlpha = (int)(m & 255);
                if (inverse)
                    maskAlpha = 255 - maskAlpha;
                uint p = image.Get(x, row);
                int alpha = Raster.Mul((byte)(p & 255), maskAlpha);
                result.Set(x, row, swap ? (m & 0xFFFFFF00u) | (uint)alpha : (p & 0xFFFFFF00u) | (uint)alpha);
            }
        }
        return new ComposedRaster { Image = result, OriginX = picture.OriginX, OriginY = picture.OriginY, IsAnimated = picture.IsAnimated };
    }

    // BYOND "displace": each pixel is taken from where the map points (red: x, green: y; 128 is no move), up to size pixels, as far as
    // the map is opaque. High green moves the picture up (OpenDream's displacement.swsl): tg's height masks hold 0 to squash a monkey down.
    private static ComposedRaster Displace(ComposedRaster picture, ComposedRaster map, FilterEntry f)
    {
        Raster image = picture.Image, result = new Raster(image.Width, image.Height);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                uint m = SourceAt(picture, map, x, y, f.X, f.Y);
                float weight = (m & 255) / 255f;
                float mx = ((int)(m >> 24) - 128) / 127f * f.Size * weight;
                float my = ((int)((m >> 16) & 255) - 128) / 127f * f.Size * weight;
                int fromX = x + (int)MathF.Round(mx), fromY = y - (int)MathF.Round(my);
                if (fromX < 0 || fromY < 0 || fromX >= image.Width || fromY >= image.Height)
                    continue;
                result.Set(x, image.Height - 1 - y, image.Get(fromX, image.Height - 1 - fromY));
            }
        }
        return new ComposedRaster { Image = result, OriginX = picture.OriginX, OriginY = picture.OriginY, IsAnimated = picture.IsAnimated };
    }

    // BYOND "outline": a line of size pixels round the drawn pixels, under them, in the filter's colour (OUTLINE_SQUARE 2: square
    // corners). The picture grows by the line's width on every side.
    private static ComposedRaster Outline(ComposedRaster picture, FilterEntry f)
    {
        float size = Math.Clamp(f.Size, 0f, 16f);
        int grow = (int)MathF.Ceiling(size);
        if (grow == 0)
            return picture;
        bool square = (f.Flags & FilterFlags.OutlineSquare) != 0;
        Raster image = picture.Image;
        var result = new Raster(image.Width + 2 * grow, image.Height + 2 * grow);
        uint color = f.Rgba & 0xFFFFFF00u;
        // tg's alpha on an outline (D156): the colour's alpha times it, the health doll's pulse from 0 to 200
        int colorAlpha = Raster.Mul((byte)(f.Rgba & 255), (int)MathF.Round(Math.Clamp(f.Alpha, 0f, 255f)));
        if (colorAlpha == 0)
            return picture;
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                int near = 0;
                for (int oy = -grow; oy <= grow && near < 255; oy++)
                {
                    for (int ox = -grow; ox <= grow; ox++)
                    {
                        if (!square && ox * ox + oy * oy > size * size)
                            continue;
                        int sx = x - grow + ox, sy = y - grow + oy;
                        if (sx < 0 || sy < 0 || sx >= image.Width || sy >= image.Height)
                            continue;
                        near = Math.Max(near, (int)(image.Get(sx, sy) & 255));
                    }
                }
                if (near > 0)
                    result.Set(x, y, color | (uint)Raster.Mul((byte)colorAlpha, near));
            }
        }
        result.Blend(image, IconBlend.Overlay, grow, grow);
        return new ComposedRaster { Image = result, OriginX = picture.OriginX + grow, OriginY = picture.OriginY + grow, IsAnimated = picture.IsAnimated };
    }
}
