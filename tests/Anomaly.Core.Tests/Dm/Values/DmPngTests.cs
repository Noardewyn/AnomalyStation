using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The PNG decoder on hostile bytes: a header's claimed size is refused with an <see cref="InvalidDataException"/> when it is not positive,
/// cannot be held, or is more than the image data can inflate to, before anything is allocated for it; a negative chunk length is refused
/// instead of looping; real images, the most compressible included, still decode to their pixels.
/// </summary>
public class DmPngTests
{
    // a 1 x 1 PNG whose header claims another size (the CRC is left stale: the decoder reads none)
    private static byte[] Claimed(uint width, uint height)
    {
        byte[] png = DmPng.Encode(1, 1, new[] { 0x808080FFu });
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), height);
        return png;
    }

    // the decoder refuses the bytes with InvalidDataException (not OutOfMemory or Overflow) and allocates under a megabyte meanwhile
    private static void Refused(byte[] png)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<InvalidDataException>(() => DmPng.Decode(png));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024 * 1024, allocated + " bytes allocated");
    }

    [Theory]
    [InlineData(15000u, 15000u)]
    [InlineData(0u, 0u)]
    [InlineData(0u, 5u)]
    [InlineData(5u, 0u)]
    [InlineData(2u, 1u)] // one more pixel than the data holds
    [InlineData(0x7FFFFFFFu, 0x7FFFFFFFu)]
    [InlineData(0x7FFFFFFFu, 1u)]
    [InlineData(0x80000000u, 0x80000000u)]
    [InlineData(0xFFFFFFFFu, 0xFFFFFFFFu)]
    public void APngHeaderClaimingASizeItsDataCannotFillIsRefusedWithoutAllocatingIt(uint width, uint height)
        => Refused(Claimed(width, height));

    [Fact]
    public void APngCutShortInItsHeaderIsRefused()
    {
        byte[] whole = DmPng.Encode(1, 1, new[] { 0x808080FFu });
        Refused(whole[..23]);
        Refused(whole[..24]);
        Refused(whole[..30]);
    }

    // Decode on a pool thread, failing after 10 seconds instead of freezing the run: a chunk loop that never advances is a synchronous CPU
    // loop, which xunit's Timeout (async tests only) would not stop; the bounded Wait returns on this thread whatever the pool thread does.
    private static Exception DecodeWithin(byte[] png)
    {
        Task<Exception> run = Task.Run(() =>
        {
            try
            {
                DmPng.Decode(png);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        });
        Assert.True(run.Wait(TimeSpan.FromSeconds(10)), "the decoder did not return in 10 seconds: a chunk length that never advances loops forever");
        return run.Result;
    }

    private static readonly byte[] OnePixel = DmPng.Encode(1, 1, new[] { 0x808080FFu });

    // the 1 x 1 PNG with a tEXt chunk (one the decoder skips) of that length field right after IHDR, followed by four bytes and the rest
    private static byte[] TextAfterHeader(uint length)
    {
        var text = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(text, length);
        "tEXt"u8.CopyTo(text.AsSpan(4));
        return [.. OnePixel[..33], .. text, .. OnePixel[33..]];
    }

    // the 1 x 1 PNG with its IEND replaced by a tEXt chunk of that length field and 16 payload bytes plus a CRC, the last bytes of the file
    private static byte[] TextAtTheEnd(uint length)
    {
        var text = new byte[8 + 16 + 4];
        BinaryPrimitives.WriteUInt32BigEndian(text, length);
        "tEXt"u8.CopyTo(text.AsSpan(4));
        return [.. OnePixel[..^12], .. text];
    }

    [Theory]
    [InlineData(0xFFFFFFF4u)] // -12: the next chunk would start where this one did
    [InlineData(0x80000000u)]
    [InlineData(0xFFFFFFFFu)]
    public void AnAncillaryPngChunkWithANegativeLengthIsRefusedRatherThanLoopingForever(uint length)
    {
        Exception e = DecodeWithin(TextAfterHeader(length));
        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("a PNG chunk runs past the file", e.Message);
    }

    [Fact]
    public void AnImageDataChunkWithANegativeLengthIsRefused()
    {
        byte[] png = OnePixel.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(33), 0xFFFFFFF4u); // the IDAT's length, read as -12
        Exception e = DecodeWithin(png);
        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("a PNG chunk runs past the file", e.Message);
    }

    [Fact]
    public void APngChunkEndingExactlyAtTheFilesEndIsReadAndOneBytePastItIsRefused()
    {
        Exception e = DecodeWithin(TextAtTheEnd(16));
        Assert.Null(e);
        Assert.Equal(new[] { 0x808080FFu }, DmPng.Decode(TextAtTheEnd(16)).Rgba);
        e = DecodeWithin(TextAtTheEnd(17));
        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("a PNG chunk runs past the file", e.Message);
    }

    [Fact]
    public void RealPngsStillDecodeToTheirPixelsTheMostCompressibleIncluded()
    {
        var mixed = new uint[37 * 23];
        for (int i = 0; i < mixed.Length; i++)
            mixed[i] = (uint)(i * 2654435761u) | 0x10u;
        (int w, int h, uint[] rgba) = DmPng.Decode(DmPng.Encode(37, 23, mixed));
        Assert.Equal((37, 23), (w, h));
        Assert.Equal(mixed, rgba);
        // all zeros inflates the most per compressed byte deflate can: the bound must still take it
        foreach (int side in new[] { 1, 16, 448, 1024 })
        {
            var zeros = new uint[side * side];
            (w, h, rgba) = DmPng.Decode(DmPng.Encode(side, side, zeros));
            Assert.Equal((side, side), (w, h));
            Assert.Equal(zeros, rgba);
        }
    }
}
