using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>BYOND's icon.Blend modes and the colour operations on rasters, against hand-computed pixels.</summary>
public class RasterTests
{
    private static Raster One(uint rgba) => Raster.Solid(1, 1, rgba);

    private static uint Blend(uint bottom, uint top, IconBlend mode)
    {
        Raster r = One(bottom);
        r.Blend(One(top), mode);
        return r.Get(0, 0);
    }

    [Fact]
    public void OverlayIsAlphaOverWithStraightColours()
    {
        Assert.Equal(0x10203040u, Blend(0x10203040, 0x00000000, IconBlend.Overlay)); // a clear top changes nothing
        Assert.Equal(0xAABBCCFFu, Blend(0x10203040, 0xAABBCCFF, IconBlend.Overlay)); // an opaque top replaces
        Assert.Equal(0xAABBCC80u, Blend(0x00000000, 0xAABBCC80, IconBlend.Overlay)); // over nothing, the top as it is
        // Half-transparent white over opaque black: mid grey, still opaque.
        Assert.Equal(0x808080FFu, Blend(0x000000FF, 0xFFFFFF80, IconBlend.Overlay));
        // Two half-transparent pixels: alpha 128 + 128 * (1 - 128/255) = 192, colour weighted by what each contributes.
        uint over = Blend(0x000000FF & 0xFFFFFF00 | 0x80, 0xFFFFFF80, IconBlend.Overlay);
        Assert.Equal(0xC0u, over & 0xFF);
        Assert.InRange((over >> 24) & 0xFF, 168u, 172u); // 255 * 128 / 192 = 170
    }

    [Fact]
    public void UnderlayPutsTheNewPictureBelow()
    {
        Assert.Equal(0xAABBCCFFu, Blend(0x00000000, 0xAABBCCFF, IconBlend.Underlay)); // nothing above: the new picture shows
        Assert.Equal(0x102030FFu, Blend(0x102030FF, 0xAABBCCFF, IconBlend.Underlay)); // an opaque bottom hides it
    }

    [Fact]
    public void AddSubtractAndMultiplyWorkOnAllFourChannels()
    {
        // add and subtract work on the colours; the alphas multiply, transparent where either is (BYOND, M7 husks): 128 * 128 / 255 = 64
        Assert.Equal(0xFF70FF40u, Blend(0xF0402080, 0x2030FF80, IconBlend.Add));
        Assert.Equal(0xD0100040u, Blend(0xF0402080, 0x2030FF80, IconBlend.Subtract));
        // 255 * 128 / 255 = 128; 128 * 128 / 255 = 64 (rounded).
        Assert.Equal(0x80808040u, Blend(0xFF80FF80, 0x80FF8080, IconBlend.Multiply));
    }

    [Fact]
    public void MultiplyByAColourTintsAndKeepsAlphaWhenTheColourIsOpaque()
    {
        Raster r = One(0xFF8040C0);
        r.Multiply(0x80FF00FF);
        Assert.Equal(0x808000C0u, r.Get(0, 0));
    }

    [Fact]
    public void AndOrWorkOnBits()
    {
        Assert.Equal(0xF0F0F0F0u & 0x3C3C3C3C, Blend(0xF0F0F0F0, 0x3C3C3C3C, IconBlend.And));
        Assert.Equal(0xF0F0F0F0u | 0x3C3C3C3C, Blend(0xF0F0F0F0, 0x3C3C3C3C, IconBlend.Or));
    }

    [Fact]
    public void BlendOffsetsAndClipsToTheDestination()
    {
        Raster bottom = Raster.Solid(4, 4, 0x000000FF);
        bottom.Blend(Raster.Solid(3, 3, 0xFFFFFFFF), IconBlend.Overlay, 2, 3);
        RasterAssert.Pixel(bottom, 1, 3, 0x000000FF);
        RasterAssert.Pixel(bottom, 2, 3, 0xFFFFFFFF);
        RasterAssert.Pixel(bottom, 3, 3, 0xFFFFFFFF);
        RasterAssert.Pixel(bottom, 3, 2, 0x000000FF);
    }

    [Fact]
    public void ColourMatrixMapsChannelsWithBYONDsRowOrder()
    {
        // BYOND: rows are the input channels red, green, blue, alpha, then the constants; columns the output channels.
        // This one swaps red and blue, halves alpha and adds 0.2 to green.
        var list = new float[]
        {
            0, 0, 1, 0, // red in goes to blue out
            0, 1, 0, 0,
            1, 0, 0, 0, // blue in goes to red out
            0, 0, 0, 0.5f,
            0, 0.2f, 0, 0,
        };
        Raster r = One(0xFF000080);
        r.Map(ColorMatrix.FromList(list));
        // in: r=255 g=0 b=0 a=128 -> r=0, g=0.2*255=51, b=255, a=64
        Assert.Equal(0x0033FF40u, r.Get(0, 0));
    }

    [Fact]
    public void ComposedColourStepsApplyInOrder()
    {
        ColorMatrix halfRed = ColorMatrix.Tint(0x80FFFFFF);
        ColorMatrix toGreen = ColorMatrix.FromList(new float[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 });
        Raster a = One(0xFF0000FF), b = One(0xFF0000FF);
        a.Map(halfRed);
        a.Map(toGreen);
        b.Map(halfRed.Then(toGreen));
        Assert.Equal(a.Get(0, 0), b.Get(0, 0));
        Assert.True(ColorMatrix.Tint(0xFFFFFFFF).IsIdentity);
        Assert.True(ColorMatrix.FromList(new float[3]).IsIdentity);
    }

    [Fact]
    public void CropOutsideTheSourceIsTransparent()
    {
        Raster r = Raster.Solid(2, 2, 0xFFFFFFFF).Crop(1, 1, 2, 2);
        RasterAssert.Pixel(r, 0, 0, 0xFFFFFFFF);
        RasterAssert.Pixel(r, 1, 0, 0);
        RasterAssert.Pixel(r, 0, 1, 0);
    }
}
