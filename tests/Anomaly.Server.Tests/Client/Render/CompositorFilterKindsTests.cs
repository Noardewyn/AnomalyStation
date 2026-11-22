using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// M6 slice 7b (D156, protocol version 26): the compositor draws the rest of BYOND's filters on a hand-made 32 x 32 picture (a 4 x 4 red
/// square in the middle): the blurs spread it (a motion blur along its vector only, an angular and a radial blur not at the centre), a
/// drop shadow lies under it where moved, a layer goes over or under it, a wave and a ripple move its pixels, rays are drawn under it,
/// a bloom adds a glow, greyscale and an HSL matrix change its colour, an outline's alpha scales its line; and an animated filter
/// (FilterPlayback) is drawn at the value the animation has at that time.
/// </summary>
public class CompositorFilterKindsTests
{
    private const uint Red = 0xFF0000FF, Blue = 0x0000FFFF;

    private readonly TestTables _tables = new();
    private readonly IconLibrary _library = new(new MemoryIconFiles());

    public CompositorFilterKindsTests()
    {
        var icon = new FakeIcon();
        _library.Register("a.dmi", icon);
        var square = new Raster(32, 32);
        for (int y = 14; y < 18; y++)
            for (int x = 14; x < 18; x++)
                square.Set(x, y, Red);
        icon.State("square", 1, 1, square);
        var corner = new Raster(32, 32);
        corner.Set(0, 31, Blue); // the icon square's lower left pixel
        icon.State("corner", 1, 1, corner);
    }

    private ComposedRaster Compose(params FilterEntry[] filters)
    {
        TableRef root = _tables.Add("a.dmi", "square", e => e.Filters = filters.ToList());
        var compositor = new AppearanceCompositor(_library);
        Assert.True(compositor.TryCompose(_tables, root, ByondDir.South, 0, out ComposedRaster composed));
        return composed;
    }

    // The pixel at column x, row y from the bottom of the icon square (outside the picture: transparent).
    private static uint At(ComposedRaster p, int x, int y)
    {
        int px = p.OriginX + x, py = p.Image.Height - 1 - (p.OriginY + y);
        return px < 0 || py < 0 || px >= p.Image.Width || py >= p.Image.Height ? 0u : p.Image.Get(px, py);
    }

    private static int Alpha(ComposedRaster p, int x, int y) => (int)(At(p, x, y) & 255);

    [Fact]
    public void A_blur_spreads_the_square_and_grows_the_picture()
    {
        ComposedRaster p = Compose(new FilterEntry { Kind = FilterKind.Blur, Size = 2 });
        Assert.True(p.Image.Width > 32);
        Assert.True(Alpha(p, 12, 16) > 0, "beside the square");
        Assert.True(Alpha(p, 15, 15) < 255, "the square's own pixels lose some alpha");
        Assert.Equal(0, Alpha(p, 2, 2));
    }

    [Fact]
    public void A_motion_blur_spreads_along_its_vector_only()
    {
        ComposedRaster p = Compose(new FilterEntry { Kind = FilterKind.MotionBlur, X = 0, Y = 3 });
        Assert.True(Alpha(p, 15, 19) > 0, "above");
        Assert.True(Alpha(p, 15, 12) > 0, "below");
        Assert.Equal(0, Alpha(p, 12, 16)); // not to the side
    }

    [Fact]
    public void An_angular_blur_leaves_the_centre_and_a_radial_blur_spreads_outward()
    {
        // a pixel far from the centre: the square moved to the right by a layer of nothing would be too much; use the corner
        TableRef root = _tables.Add("a.dmi", "corner", e => e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.AngularBlur, Size = 20 } });
        var compositor = new AppearanceCompositor(_library);
        Assert.True(compositor.TryCompose(_tables, root, ByondDir.South, 0, out ComposedRaster spun));
        Assert.True(Alpha(spun, 0, 0) < 255, "the corner pixel smears along its circle");
        Assert.True(Alpha(spun, 1, 0) > 0 || Alpha(spun, 0, 1) > 0, "to its neighbours on the circle");

        ComposedRaster centred = Compose(new FilterEntry { Kind = FilterKind.AngularBlur, Size = 20, Offset = 8 });
        Assert.Equal(Red, At(centred, 15, 15)); // inside the radius left sharp

        ComposedRaster zoomed = Compose(new FilterEntry { Kind = FilterKind.RadialBlur, Size = 0.3f });
        Assert.True(Alpha(zoomed, 18, 18) > 0 || Alpha(zoomed, 13, 13) > 0, "the square's corners spread outward");
    }

    [Fact]
    public void A_drop_shadow_lies_under_the_square_where_it_is_moved()
    {
        ComposedRaster p = Compose(new FilterEntry { Kind = FilterKind.DropShadow, X = 3, Y = -3, Size = 0.01f, Rgba = 0x000000FF });
        Assert.Equal(Red, At(p, 15, 15)); // the square over its shadow
        uint shadow = At(p, 19, 13);
        Assert.True((shadow & 255) > 200 && (shadow >> 8) == 0, "black below and to the right");
        Assert.Equal(0, Alpha(p, 12, 19));
    }

    [Fact]
    public void A_layer_goes_over_by_default_and_under_with_FILTER_UNDERLAY()
    {
        TableRef layerIcon = _tables.Res("a.dmi");
        TableRef corner = _tables.Str("corner");
        ComposedRaster over = Compose(new FilterEntry { Kind = FilterKind.Layer, Icon = layerIcon, IconState = corner, X = 15, Y = 15, Flags = FilterFlags.Overlay });
        Assert.Equal(Blue, At(over, 15, 15)); // the corner pixel moved onto the square
        ComposedRaster under = Compose(new FilterEntry { Kind = FilterKind.Layer, Icon = layerIcon, IconState = corner, X = 15, Y = 15, Flags = FilterFlags.Underlay });
        Assert.Equal(Red, At(under, 15, 15));
        ComposedRaster beside = Compose(new FilterEntry { Kind = FilterKind.Layer, Icon = layerIcon, IconState = corner, X = 3, Y = 0 });
        Assert.Equal(Blue, At(beside, 3, 0));
    }

    [Fact]
    public void A_wave_and_a_ripple_move_the_pixels()
    {
        ComposedRaster plain = Compose();
        ComposedRaster waved = Compose(new FilterEntry { Kind = FilterKind.Wave, X = 8, Y = 0, Size = 2, Offset = 0.25f });
        Assert.False(waved.Image.Pixels.AsSpan().SequenceEqual(plain.Image.Pixels));
        ComposedRaster rippled = Compose(new FilterEntry { Kind = FilterKind.Ripple, Size = 2, Repeat = 4, Radius = 6, Falloff = 1 });
        Assert.False(rippled.Image.Pixels.AsSpan().SequenceEqual(plain.Image.Pixels));
        ComposedRaster bounded = Compose(new FilterEntry { Kind = FilterKind.Wave, X = 8, Y = 0, Size = 2, Flags = FilterFlags.WaveBounded });
        Assert.Equal(32, bounded.Image.Width); // WAVE_BOUNDED keeps the picture's bounds
    }

    [Fact]
    public void Rays_are_drawn_under_an_image_by_default()
    {
        ComposedRaster p = Compose(new FilterEntry { Kind = FilterKind.Rays, Size = 14, Rgba = 0xFFFFFFFF, Density = 6, Threshold = 0, Flags = FilterFlags.Overlay | FilterFlags.Underlay });
        Assert.Equal(Red, At(p, 15, 15)); // the square stays on top
        int lit = 0;
        for (int y = 2; y < 30; y++)
            for (int x = 2; x < 30; x++)
                if (!(x is >= 14 and < 18 && y is >= 14 and < 18) && Alpha(p, x, y) > 0) lit++;
        Assert.True(lit > 20, "rays round the square: " + lit);
        ComposedRaster later = Compose(new FilterEntry { Kind = FilterKind.Rays, Size = 14, Rgba = 0xFFFFFFFF, Density = 6, Threshold = 0, Offset = 7, Flags = 3 });
        Assert.False(later.Image.Pixels.AsSpan().SequenceEqual(p.Image.Pixels)); // another time, other rays
    }

    [Fact]
    public void A_bloom_adds_a_glow_round_the_bright_pixels()
    {
        ComposedRaster p = Compose(new FilterEntry { Kind = FilterKind.Bloom, ThresholdRgba = 0x808080FF, Size = 2, Offset = 1 });
        Assert.True(Alpha(p, 12, 16) > 0, "the glow beside the square");
        ComposedRaster none = Compose(new FilterEntry { Kind = FilterKind.Bloom, ThresholdRgba = 0xFFFFFFFF, Size = 2, Offset = 1 });
        Assert.Equal(0, Alpha(none, 12, 16)); // nothing is brighter than white
    }

    [Fact]
    public void Greyscale_and_an_HSL_matrix_change_the_colour()
    {
        uint grey = At(Compose(new FilterEntry { Kind = FilterKind.Greyscale }), 15, 15);
        Assert.Equal(grey >> 24, (grey >> 16) & 255);
        Assert.Equal(76u, grey >> 24); // 0.299 of red

        // HSL: hue + 1/3 of a turn, the rest kept: red becomes green
        var m = new float[20];
        m[0] = 1; m[5] = 1; m[10] = 1; m[15] = 1; m[16] = 1f / 3f;
        uint shifted = At(Compose(new FilterEntry { Kind = FilterKind.Color, ColorMatrix = m, Space = 2 }), 15, 15);
        Assert.True((shifted >> 16 & 255) > 200 && (shifted >> 24) < 40, "green: " + shifted.ToString("X8"));
    }

    [Fact]
    public void An_outlines_alpha_scales_its_line_and_an_animation_draws_it_as_it_plays()
    {
        ComposedRaster full = Compose(new FilterEntry { Kind = FilterKind.Outline, Size = 1, Rgba = 0xFF0033FF });
        ComposedRaster faint = Compose(new FilterEntry { Kind = FilterKind.Outline, Size = 1, Rgba = 0xFF0033FF, Alpha = 100 });
        ComposedRaster hidden = Compose(new FilterEntry { Kind = FilterKind.Outline, Size = 1, Rgba = 0xFF0033FF, Alpha = 0 });
        Assert.Equal(255, Alpha(full, 13, 15));
        Assert.Equal(100, Alpha(faint, 13, 15));
        Assert.Equal(0, Alpha(hidden, 13, 15));

        // the health doll's pulse: alpha 0 to 200 over 1.5 s, back over 1.5 s, for ever; 20 ticks a second
        var outline = new List<FilterEntry> { new() { Kind = FilterKind.Outline, Size = 1, Rgba = 0xFF0033FF, Alpha = 0 } };
        TableRef root = _tables.Add("a.dmi", "square", e => e.Filters = outline);
        var animation = new EntityAnimation { Tracks = new[] { ProtocolV26Pulse(100) } };
        var compositor = new AppearanceCompositor(_library);
        int AlphaAt(double tick)
        {
            Assert.True(FilterPlayback.TryEvaluate(_tables, root, animation, tick, 20, out Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> looks));
            Assert.True(compositor.TryCompose(_tables, root, ByondDir.South, 0, out ComposedRaster now, false, false, looks));
            return Alpha(now, 13, 15);
        }
        Assert.Equal(0, AlphaAt(100));
        Assert.InRange(AlphaAt(115), 95, 105); // half way up
        Assert.Equal(200, AlphaAt(130)); // the top
        Assert.InRange(AlphaAt(145), 95, 105);
        Assert.Equal(200, AlphaAt(190)); // the next round's top
        Assert.True(compositor.TryCompose(_tables, root, ByondDir.South, 0, out ComposedRaster rest));
        Assert.Equal(0, Alpha(rest, 13, 15)); // without the animation: the appearance's value
    }

    private static AnimationTrack ProtocolV26Pulse(long start) => Anomaly.Server.Tests.Protocol.ProtocolV26Tests.Pulse(start);
}
