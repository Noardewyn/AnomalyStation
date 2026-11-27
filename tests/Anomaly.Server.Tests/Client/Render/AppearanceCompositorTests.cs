using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>The appearance compositor against BYOND's rules (hand-made 4 x 4 icons so every pixel can be named).</summary>
public class AppearanceCompositorTests
{
    private const uint Red = 0xFF0000FF, Green = 0x00FF00FF, Blue = 0x0000FFFF, White = 0xFFFFFFFF;
    private const byte South = ByondDir.South, North = ByondDir.North, East = ByondDir.East, West = ByondDir.West;

    private readonly TestTables _tables = new();
    private readonly IconLibrary _library = new(new MemoryIconFiles());
    private readonly FakeIcon _icon = new();

    public AppearanceCompositorTests()
    {
        _library.Register("a.dmi", _icon);
    }

    private ComposedRaster Compose(TableRef root, byte dir = South, double seconds = 0)
    {
        var compositor = new AppearanceCompositor(_library);
        Assert.True(compositor.TryCompose(_tables, root, dir, seconds, out ComposedRaster composed));
        return composed;
    }

    private TableRef Add(string state, Action<AppearanceEntry> configure = null) => _tables.Add("a.dmi", state, configure);

    private static void Overlay(TestTables tables, AppearanceEntry parent, params TableRef[] children) => parent.Overlays.AddRange(children);

    [Fact]
    public void AnAppearanceShowsTheFrameOfItsDirection()
    {
        _icon.State("man", 4, 1, Raster.Solid(4, 4, Red), Raster.Solid(4, 4, Green), Raster.Solid(4, 4, Blue), Raster.Solid(4, 4, White));
        TableRef man = Add("man");
        Assert.Equal(Red, Compose(man, South).Image.Get(1, 1));
        Assert.Equal(Green, Compose(man, North).Image.Get(1, 1));
        Assert.Equal(Blue, Compose(man, East).Image.Get(1, 1));
        Assert.Equal(White, Compose(man, West).Image.Get(1, 1));
        ComposedRaster plain = Compose(man);
        Assert.Equal((4, 4, 0, 0), (plain.Image.Width, plain.Image.Height, plain.OriginX, plain.OriginY));
    }

    [Fact]
    public void AStateTheIconLacksDrawsNothingAndIsNoPlaceholder()
    {
        // The space play test (2026-10-15): tg's trail particles blank themselves with icon_state "" after their flick (effects_trail.dm:72);
        // effects.dmi has no default state, so BYOND draws nothing. A missing icon file keeps the placeholder (the icons are not converted).
        _icon.Solid("ion_fade", White, 4);
        var compositor = new AppearanceCompositor(_library);
        TableRef blank = Add("");
        TableRef fade = Add("ion_fade");
        Assert.False(compositor.TryCompose(_tables, blank, South, 0, out _));
        Assert.True(compositor.IsBlank(_tables, blank, South));
        Assert.False(compositor.IsBlank(_tables, fade, South));
        TableRef missingFile = _tables.Add("missing.dmi", "ion_fade");
        Assert.False(compositor.TryCompose(_tables, missingFile, South, 0, out _));
        Assert.False(compositor.IsBlank(_tables, missingFile, South));
        // With a default state the missing one shows it (BYOND), so it is not blank.
        _icon.Solid("", Red, 4);
        var again = new AppearanceCompositor(_library);
        Assert.Equal(Red, Compose(blank).Image.Get(0, 0));
        Assert.False(again.IsBlank(_tables, blank, South));
    }

    [Fact]
    public void APictureMostlyPartlyTransparentIsTranslucent()
    {
        // tg's water puff: alpha 11 to 153 (the opaque pass's scissor at 128 cut it away whole); a sprite with a few soft edges is not.
        var puff = Raster.Solid(4, 4, 0xAACCFF40);
        Assert.True(puff.IsTranslucent());
        var sprite = Raster.Solid(4, 4, Red);
        sprite.Set(0, 0, 0xFF000080);
        sprite.Set(1, 0, 0xFF000080);
        Assert.False(sprite.IsTranslucent());
        Assert.False(new Raster(4, 4).IsTranslucent());
    }

    [Fact]
    public void FloatLayersSortByValueAndTieKeepsListOrder()
    {
        _icon.Solid("base", White, 4).Solid("g", Green, 4).Solid("b", Blue, 4);
        // tg's -BODYPARTS_LAYER style: the more negative index is lower. Green at -2 is below blue at -1.
        TableRef g = Add("g", e => e.Layer = -2), b = Add("b", e => e.Layer = -1);
        TableRef rootGb = Add("base", e => e.Overlays.AddRange(new[] { g, b }));
        TableRef rootBg = Add("base", e => e.Overlays.AddRange(new[] { b, g }));
        Assert.Equal(Blue, Compose(rootGb).Image.Get(0, 0));
        Assert.Equal(Blue, Compose(rootBg).Image.Get(0, 0)); // list order does not matter when the layers differ

        TableRef g1 = Add("g", e => e.Layer = -1), b1 = Add("b", e => e.Layer = -1);
        Assert.Equal(Blue, Compose(Add("base", e => e.Overlays.AddRange(new[] { g1, b1 }))).Image.Get(0, 0));
        Assert.Equal(Green, Compose(Add("base", e => e.Overlays.AddRange(new[] { b1, g1 }))).Image.Get(0, 0)); // equal layers: the later one is on top
    }

    [Fact]
    public void AnOverlayOnItsParentsLayerKeepsListOrderWithTheTopFloat()
    {
        // tg's fishing HUD: the rod's frame (FLOAT_LAYER) first, then the completion bar on the HUD's own layer, drawn over the frame
        _icon.Solid("base", White, 4).Solid("g", Green, 4).Solid("b", Blue, 4);
        TableRef frame = Add("g", e => e.Layer = -1);
        TableRef bar = Add("b", e => e.Layer = 3);
        Assert.Equal(Blue, Compose(Add("base", e => { e.Layer = 3; e.Overlays.AddRange(new[] { frame, bar }); })).Image.Get(0, 0));
        Assert.Equal(Green, Compose(Add("base", e => { e.Layer = 3; e.Overlays.AddRange(new[] { bar, frame }); })).Image.Get(0, 0));
        // a lower float stays under it whatever the order
        TableRef low = Add("g", e => e.Layer = -2);
        Assert.Equal(Blue, Compose(Add("base", e => { e.Layer = 3; e.Overlays.AddRange(new[] { bar, low }); })).Image.Get(0, 0));
    }

    [Fact]
    public void OfTwoChildrenOnOneLayerTheFirstIsDrawnWholeBeforeTheSecond()
    {
        // tg's fishing HUD: the bait with its bars (its own overlays), then the fish, both on the HUD's layer: the fish is over the bars
        _icon.Solid("base", White, 4).Solid("g", Green, 4).Solid("b", Blue, 4).Solid("r", Red, 4);
        TableRef bars = Add("g", e => e.Layer = -1);
        TableRef bait = Add("b", e => { e.Layer = 3; e.Overlays.Add(bars); });
        TableRef fish = Add("r", e => e.Layer = 3);
        Assert.Equal(Red, Compose(Add("base", e => { e.Layer = 3; e.Overlays.AddRange(new[] { bait, fish }); })).Image.Get(0, 0));
        Assert.Equal(Green, Compose(Add("base", e => { e.Layer = 3; e.Overlays.AddRange(new[] { fish, bait }); })).Image.Get(0, 0));
    }

    [Fact]
    public void UnderlaysAreBelowTheAppearanceAndAbsoluteLayersCompareWithTheParents()
    {
        var holed = Raster.Solid(4, 4, Red);
        holed.Set(0, 0, 0);
        _icon.State("holed", 1, 1, holed).Solid("g", Green, 4).Solid("b", Blue, 4);
        TableRef g = Add("g");
        TableRef withUnder = Add("holed", e => e.Underlays.Add(g));
        ComposedRaster under = Compose(withUnder);
        Assert.Equal(Green, under.Image.Get(0, 0)); // seen through the hole
        Assert.Equal(Red, under.Image.Get(1, 1));   // hidden elsewhere

        // An overlay on an absolute layer below its parent's layer is drawn below it.
        TableRef low = Add("b", e => e.Layer = 2);
        ComposedRaster below = Compose(Add("holed", e => { e.Layer = 4; e.Overlays.Add(low); }));
        Assert.Equal(Blue, below.Image.Get(0, 0));
        Assert.Equal(Red, below.Image.Get(1, 1));
        TableRef high = Add("b", e => e.Layer = 6);
        Assert.Equal(Blue, Compose(Add("holed", e => { e.Layer = 4; e.Overlays.Add(high); })).Image.Get(1, 1));

        // An overlay on a lower plane is below its parent whatever its layer (a rock's wall-plane copy under its smoothed game-plane
        // picture, _closed.dm:31); FLOAT_PLANE takes the parent's plane, so it is not lower.
        TableRef wallPlane = Add("b", e => { e.Layer = 6; e.Plane = -5; });
        Assert.Equal(Red, Compose(Add("holed", e => { e.Plane = -4; e.Overlays.Add(wallPlane); })).Image.Get(1, 1));
        TableRef floatPlane = Add("b", e => { e.Layer = 6; e.Plane = -32767; });
        Assert.Equal(Blue, Compose(Add("holed", e => { e.Plane = -4; e.Overlays.Add(floatPlane); })).Image.Get(1, 1));
    }

    [Fact]
    public void AnOverlaysOwnOverlaysStayWithIt()
    {
        _icon.Solid("base", White, 4).Solid("g", Green, 4).Solid("b", Blue, 4).Solid("r", Red, 4);
        TableRef r = Add("r", e => e.Layer = -3);           // a child of the blue overlay, floating at -3
        TableRef b = Add("b", e => { e.Layer = -1; e.Overlays.Add(r); });
        TableRef g = Add("g", e => e.Layer = -2);           // a sibling of blue between
        // Level by level: blue's whole subtree is above green although red (-3) alone would sort below green (-2).
        Assert.Equal(Red, Compose(Add("base", e => e.Overlays.AddRange(new[] { g, b }))).Image.Get(0, 0));
    }

    [Fact]
    public void ColourAndAlphaAreInheritedUnlessReset()
    {
        _icon.Solid("base", White, 4).Solid("child", White, 4);
        TableRef plain = Add("child");
        TableRef reset = Add("child", e => e.AppearanceFlags = AppearanceFlag.ResetColor | AppearanceFlag.ResetAlpha);
        ComposedRaster tinted = Compose(Add("base", e => { e.Rgba = Red; e.Overlays.Add(plain); }));
        Assert.Equal(Red, tinted.Image.Get(2, 2));
        ComposedRaster resetColour = Compose(Add("base", e => { e.Rgba = Red; e.Overlays.Add(reset); }));
        Assert.Equal(White, resetColour.Image.Get(2, 2));
        ComposedRaster half = Compose(Add("base", e => { e.Alpha = 128; e.Overlays.Add(plain); }));
        // Both halves: alpha 128 over alpha 128 is about 192.
        Assert.InRange(half.Image.Get(2, 2) & 0xFF, 190u, 194u);
        // A child that resets alpha is opaque again and covers the half transparent parent.
        Assert.Equal(255u, Compose(Add("base", e => { e.Alpha = 128; e.Overlays.Add(reset); })).Image.Get(2, 2) & 0xFF);
    }

    [Fact]
    public void ARootComposedOpaqueDropsItsOwnAlphaForAVisualWhoseChainEndsTransparent()
    {
        // M4v slice 3: tg's pickup and attack images end their animate() chain at alpha 0, which the appearance holds; the client
        // composes the root opaque and plays the chain's alpha over it (EntityLayer, protocol.md section 19.3).
        _icon.Solid("base", White, 4).Solid("child", White, 4);
        TableRef half = Add("child", e => e.Alpha = 128);
        TableRef faded = Add("base", e => { e.Alpha = 0; e.Overlays.Add(half); });
        var compositor = new AppearanceCompositor(_library);
        Assert.True(compositor.TryCompose(_tables, faded, South, 0, out ComposedRaster opaque, rootOpaque: true));
        Assert.Equal(255u, opaque.Image.Get(2, 2) & 0xFF); // the child's own alpha still blends over the opaque base
        if (compositor.TryCompose(_tables, faded, South, 0, out ComposedRaster plain))
            Assert.InRange(plain.Image.Get(2, 2) & 0xFF, 0u, 1u); // the ordinary picture keeps the root's alpha (both cached apart)
    }

    [Fact]
    public void AColourMatrixTurnsTheDrawnColours()
    {
        _icon.Solid("base", Red, 4);
        // red -> blue
        var swap = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 };
        Assert.Equal(Blue, Compose(Add("base", e => e.ColorMatrix = swap)).Image.Get(0, 0));
    }

    [Fact]
    public void PixelOffsetsGrowThePictureAndMoveTheOrigin()
    {
        _icon.Solid("base", White, 4).Solid("dot", Red, 2);
        // A 2 x 2 overlay moved 3 pixels right and 1 up: it sticks out of the 4 x 4 square on the right, not the left.
        ComposedRaster right = Compose(Add("base", e => e.Overlays.Add(Add("dot", d => { d.PixelX = 3; d.PixelY = 1; }))));
        Assert.Equal((5, 4), (right.Image.Width, right.Image.Height));
        Assert.Equal((0, 0), (right.OriginX, right.OriginY));
        Assert.Equal(Red, right.Image.Get(4, 1)); // x 3..4, y 1..2 from the bottom: rows 1..2 from the top of 4
        Assert.Equal(0u, right.Image.Get(4, 3));

        // Moved to the left and down it reaches below and left of the square: the origin moves inside the picture.
        ComposedRaster left = Compose(Add("base", e => e.Overlays.Add(Add("dot", d => { d.PixelX = -1; d.PixelY = -1; }))));
        Assert.Equal((5, 5), (left.Image.Width, left.Image.Height));
        Assert.Equal((1, 1), (left.OriginX, left.OriginY));
        Assert.Equal(Red, left.Image.Get(0, 4)); // the lowest row of the picture
        Assert.Equal(White, left.Image.Get(1 + 3, 4 - 1 - 3)); // the square's top right pixel
    }

    [Fact]
    public void PixelWAndZMoveTheChildAfterTheParentsTransform()
    {
        _icon.State("arrow", 1, 1, Marked(Red)).Solid("dot", Blue, 1);
        // The parent is turned a quarter; pixel_x of the child turns with it, pixel_w does not.
        float[] turn = { 0, 1, 0, -1, 0, 0 };
        TableRef x = Add("dot", d => d.PixelX = 2);
        TableRef w = Add("dot", d => d.PixelW = 2);
        ComposedRaster byX = Compose(Add("arrow", e => { e.Transform = turn; e.Overlays.Add(x); }));
        ComposedRaster byW = Compose(Add("arrow", e => { e.Transform = turn; e.Overlays.Add(w); }));
        // The turned frame carries the pixel_x offset with it (2 to the right becomes 2 down); the pixel_w offset stays 2 to the right.
        Assert.Equal(Blue, byW.Image.Get(2, 0));
        Assert.Equal(Blue, byX.Image.Get(0, 2));
    }

    private static Raster Marked(uint colour)
    {
        // A 4 x 4 picture of white with a coloured top left pixel.
        Raster r = Raster.Solid(4, 4, White);
        r.Set(0, 0, colour);
        return r;
    }

    [Fact]
    public void ATransformTurnsThePictureAboutItsCentre()
    {
        _icon.State("m", 1, 1, Marked(Red));
        // BYOND's M.Turn(90): clockwise, so the top left pixel goes to the top right.
        ComposedRaster turned = Compose(Add("m", e => e.Transform = new float[] { 0, 1, 0, -1, 0, 0 }));
        Assert.Equal((4, 4), (turned.Image.Width, turned.Image.Height));
        Assert.Equal(Red, turned.Image.Get(3, 0));
        Assert.Equal(White, turned.Image.Get(0, 0));
        ComposedRaster half = Compose(Add("m", e => e.Transform = new float[] { 0, -1, 0, 1, 0, 0 })); // counter-clockwise: to the bottom left
        Assert.Equal(Red, half.Image.Get(0, 3));
    }

    [Fact]
    public void ATranslationInTheTransformMovesThePictureBelowItsSquare()
    {
        _icon.Solid("m", White, 4);
        // tg's lying mobs are moved six pixels down: here one pixel, so the picture grows downward and the square sits above its bottom row.
        ComposedRaster moved = Compose(Add("m", e => e.Transform = new float[] { 1, 0, 0, 0, 1, -1 }));
        Assert.Equal((4, 5), (moved.Image.Width, moved.Image.Height));
        Assert.Equal((0, 1), (moved.OriginX, moved.OriginY));
        Assert.Equal(0u, moved.Image.Get(0, 0));
        Assert.Equal(White, moved.Image.Get(0, 4));
    }

    [Fact]
    public void KeepTogetherAppliesAlphaToTheWholeGroup()
    {
        _icon.Solid("base", Red, 4).Solid("child", Green, 4);
        TableRef child = Add("child");
        ComposedRaster together = Compose(Add("base", e => { e.Alpha = 128; e.AppearanceFlags = AppearanceFlag.KeepTogether; e.Overlays.Add(child); }));
        Assert.Equal(0x00FF0080u, together.Image.Get(1, 1)); // green over red inside the group, then half transparent as one picture
        ComposedRaster apart = Compose(Add("base", e => { e.Alpha = 128; e.Overlays.Add(child); }));
        uint pixel = apart.Image.Get(1, 1);
        Assert.InRange(pixel & 0xFF, 190u, 194u);   // two half transparent pictures over each other
        Assert.True(((pixel >> 24) & 0xFF) > 20);   // the red still shows through
    }

    [Fact]
    public void KeepApartChildrenStayOutOfTheGroup()
    {
        _icon.Solid("base", Red, 4).Solid("inside", Green, 4).State("apart", 1, 1, Solid1(Blue));
        TableRef inside = Add("inside"), apart = Add("apart", e => e.AppearanceFlags = AppearanceFlag.KeepApart);
        ComposedRaster picture = Compose(Add("base", e => { e.Alpha = 128; e.AppearanceFlags = AppearanceFlag.KeepTogether; e.Overlays.AddRange(new[] { inside, apart }); }));
        // The group (green over red) is drawn at alpha 128; the kept-apart blue pixel is not inside it.
        Assert.Equal(0x00FF0080u, picture.Image.Get(2, 2));
        // The blue pixel at (0, 0) is drawn on top with the parent's alpha, so it shows blue over the group's green.
        uint apartPixel = picture.Image.Get(0, 0);
        Assert.True(((apartPixel >> 8) & 0xFF) > 0 && (apartPixel & 0xFF) > 128, apartPixel.ToString("X8"));
    }

    private static Raster Solid1(uint colour)
    {
        var r = new Raster(4, 4);
        r.Set(0, 0, colour);
        return r;
    }

    [Fact]
    public void AddAndSubtractBlendChildrenOverWhatIsBelow()
    {
        _icon.Solid("base", 0x404040FF, 4).Solid("light", 0x303030FF, 4);
        TableRef add = Add("light", e => e.BlendMode = 2), sub = Add("light", e => e.BlendMode = 3);
        Assert.Equal(0x707070FFu, Compose(Add("base", e => e.Overlays.Add(add))).Image.Get(0, 0));
        Assert.Equal(0x101010FFu, Compose(Add("base", e => e.Overlays.Add(sub))).Image.Get(0, 0));
    }

    [Fact]
    public void ANestedBlendModeIsInheritedWhenTheChildSaysDefault()
    {
        _icon.Solid("base", 0x404040FF, 4).Solid("light", 0x303030FF, 4).Solid("inner", 0x101010FF, 4);
        // The child adds; its own child has blend 0 so it adds too.
        TableRef inner = Add("inner");
        TableRef child = Add("light", e => { e.BlendMode = 2; e.Overlays.Add(inner); });
        Assert.Equal(0x808080FFu, Compose(Add("base", e => e.Overlays.Add(child))).Image.Get(0, 0));
    }

    [Fact]
    public void AChildWithoutAnIconUsesItsParentsIcon()
    {
        _icon.Solid("base", White, 4).Solid("extra", Green, 4);
        TableRef extra = _tables.Add(null, "extra");
        Assert.Equal(Green, Compose(Add("base", e => e.Overlays.Add(extra))).Image.Get(0, 0));
    }

    [Fact]
    public void AMissingIconOrStateDrawsNothingButTheRestStillDraws()
    {
        _icon.Solid("base", White, 4);
        var compositor = new AppearanceCompositor(_library);
        TableRef missing = _tables.Add("nowhere.dmi", "x"), noState = Add("no such state");
        Assert.False(compositor.TryCompose(_tables, missing, South, 0, out _));
        Assert.False(compositor.TryCompose(_tables, noState, South, 0, out _));
        Assert.True(compositor.TryCompose(_tables, Add("base", e => e.Overlays.AddRange(new[] { missing, noState })), South, 0, out ComposedRaster picture));
        Assert.Equal(White, picture.Image.Get(0, 0));
        Assert.False(compositor.TryCompose(_tables, TableRef.Public(999), South, 0, out _));
    }

    [Fact]
    public void ResultsAreCachedPerAppearanceAndDirection()
    {
        _icon.State("man", 4, 1, Raster.Solid(4, 4, Red), Raster.Solid(4, 4, Green), Raster.Solid(4, 4, Blue), Raster.Solid(4, 4, White));
        TableRef man = Add("man");
        var compositor = new AppearanceCompositor(_library);
        compositor.TryCompose(_tables, man, South, 0, out ComposedRaster a);
        compositor.TryCompose(_tables, man, South, 5, out ComposedRaster b);
        compositor.TryCompose(_tables, man, North, 0, out ComposedRaster c);
        Assert.Same(a, b);
        Assert.NotSame(a, c);
        Assert.Equal(2, compositor.CachedCount);
        compositor.Clear();
        Assert.Equal(0, compositor.CachedCount);
    }

    [Fact]
    public void AnAnimatedAppearanceIsComposedAgainOnlyWhenAFrameChanges()
    {
        _icon.State("blink", 1, 2, Raster.Solid(4, 4, Red), Raster.Solid(4, 4, Green));
        TableRef blink = Add("blink");
        var compositor = new AppearanceCompositor(_library);
        compositor.TryCompose(_tables, blink, South, 0.00, out ComposedRaster first);
        compositor.TryCompose(_tables, blink, South, 0.05, out ComposedRaster same);
        compositor.TryCompose(_tables, blink, South, 0.10, out ComposedRaster second);
        Assert.True(first.IsAnimated);
        Assert.Same(first, same);
        Assert.NotSame(first, second);
        Assert.Equal(Red, first.Image.Get(0, 0));
        Assert.Equal(Green, second.Image.Get(0, 0));
    }

    [Fact]
    public void DirectionsReportsTheWidestStateOfTheTree()
    {
        _icon.State("four", 4, 1, Raster.Solid(4, 4, Red)).State("eight", 8, 1, Raster.Solid(4, 4, Red)).Solid("one", Red, 4);
        var compositor = new AppearanceCompositor(_library);
        Assert.Equal(1, compositor.Directions(_tables, Add("one")));
        Assert.Equal(4, compositor.Directions(_tables, Add("four", e => e.Overlays.Add(Add("one")))));
        Assert.Equal(8, compositor.Directions(_tables, Add("one", e => e.Overlays.Add(Add("eight")))));
    }

    [Fact]
    public void AnOverlayWithItsOwnDirectionKeepsIt()
    {
        _icon.State("man", 4, 1, Raster.Solid(4, 4, Red), Raster.Solid(4, 4, Green), Raster.Solid(4, 4, Blue), Raster.Solid(4, 4, White));
        TableRef north = Add("man", e => e.Dir = North);
        // The root faces east; the overlay is fixed to north.
        Assert.Equal(Green, Compose(Add("man", e => e.Overlays.Add(north)), East).Image.Get(0, 0));
    }

    [RealIconsFact]
    public void ARealFloorTileComposesToItsSprite()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var tables = new TestTables();
        TableRef floor = tables.Add("icons/turf/floors.dmi", "floor");
        var compositor = new AppearanceCompositor(library);
        Assert.True(compositor.TryCompose(tables, floor, South, 0, out ComposedRaster picture));
        Assert.Equal((32, 32), (picture.Image.Width, picture.Image.Height));
        library.TryGetIcon("icons/turf/floors.dmi", out IIcon icon);
        icon.TryGetState("floor", out IIconState state);
        Assert.Equal(state.Get(0, 0).Pixels, picture.Image.Pixels);
    }
}
