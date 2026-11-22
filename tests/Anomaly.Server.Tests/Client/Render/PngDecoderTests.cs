using System.IO;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

public class PngDecoderTests
{
    // A 13 x 9 picture (odd sizes make the Adam7 passes uneven) with distinct colours and some transparency.
    private static Raster Picture()
    {
        var r = new Raster(13, 9);
        for (int y = 0; y < 9; y++)
        {
            for (int x = 0; x < 13; x++)
                r.Set(x, y, (uint)((x * 19 + 7) & 255) << 24 | (uint)((y * 27 + 3) & 255) << 16 | (uint)(((x + y) * 11) & 255) << 8 | (uint)((x + y) % 3 == 0 ? 0 : 255 - ((x * y) & 127)));
        }
        return r;
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void EveryRgbaFilterAndInterlaceDecodesToTheSamePixels(int filter, bool interlaced)
    {
        Raster source = Picture();
        Raster decoded = PngDecoder.Decode(PngEncoder.Encode(source, 6, 8, filter, interlaced));
        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Fact]
    public void ColourTypesWithoutAlphaAreOpaqueAndGreyIsReplicated()
    {
        Raster source = Picture();
        Raster rgb = PngDecoder.Decode(PngEncoder.Encode(source, 2, 8, 1, false));
        Assert.Equal(source.Get(5, 4) | 0xFF, rgb.Get(5, 4));
        Raster grey = PngDecoder.Decode(PngEncoder.Encode(source, 0, 8, 2, true));
        uint g = (source.Get(5, 4) >> 16) & 255;
        Assert.Equal(g << 24 | g << 16 | g << 8 | 0xFF, grey.Get(5, 4));
        Raster greyAlpha = PngDecoder.Decode(PngEncoder.Encode(source, 4, 8, 3, false));
        Assert.Equal(g << 24 | g << 16 | g << 8 | (source.Get(5, 4) & 255), greyAlpha.Get(5, 4));
    }

    [Fact]
    public void PalettesOfEveryDepthKeepTheirColoursAndTransparency()
    {
        var source = new Raster(7, 5);
        uint[] colours = { 0xFF0000FF, 0x00FF00FF, 0x0000FF80, 0x11223300 };
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
                source.Set(x, y, colours[(x + y) % colours.Length]);
        }
        foreach (int depth in new[] { 2, 4, 8 })
        {
            foreach (bool interlaced in new[] { false, true })
            {
                Raster decoded = PngDecoder.Decode(PngEncoder.Encode(source, 3, depth, 4, interlaced));
                Assert.Equal(source.Pixels, decoded.Pixels);
            }
        }
    }

    [Fact]
    public void SixteenBitSamplesKeepTheirHighByte()
    {
        Raster source = Picture();
        Raster decoded = PngDecoder.Decode(PngEncoder.Encode(source, 6, 16, 0, false));
        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Fact]
    public void OneBitGreyIsBlackAndWhite()
    {
        var source = new Raster(9, 2);
        for (int x = 0; x < 9; x++)
        {
            source.Set(x, 0, x % 2 == 0 ? 0xFFFFFFFF : 0x000000FF);
            source.Set(x, 1, x % 2 == 0 ? 0x000000FF : 0xFFFFFFFF);
        }
        Raster decoded = PngDecoder.Decode(PngEncoder.Encode(source, 0, 1, 0, false));
        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
    public void NonsenseIsRejected(byte[] bytes)
        => Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(bytes));

    [Fact]
    public void ATruncatedFileIsRejectedNotReadPastItsEnd()
    {
        byte[] png = PngEncoder.Encode(Picture(), 6, 8, 4, false);
        Assert.ThrowsAny<Exception>(() => PngDecoder.Decode(png.AsSpan(0, png.Length / 2).ToArray()));
    }

    [RealIconsFact]
    public void EveryRealIconSheetDecodes()
    {
        int decoded = 0;
        foreach (string file in Directory.EnumerateFiles(Path.Combine(RealIcons.Root, "icons"), "*.png", SearchOption.AllDirectories))
        {
            Raster raster = PngDecoder.Decode(File.ReadAllBytes(file));
            Assert.True(raster.Width > 0 && raster.Height > 0, file);
            decoded++;
        }
        Assert.True(decoded > 1000, decoded + " sheets");
    }
}
