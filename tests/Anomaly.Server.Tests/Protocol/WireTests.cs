using System.Buffers.Binary;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>The primitives of <see cref="WireWriter"/> and <see cref="WireReader"/> (docs/ai/specs/protocol.md section 2).</summary>
public class WireTests
{
    private static byte[] VarUInt(ulong value)
    {
        Span<byte> buffer = stackalloc byte[10];
        var w = new WireWriter(buffer);
        w.WriteVarUInt(value);
        Assert.Equal(WireWriter.VarUIntSize(value), w.Position);
        return w.Written.ToArray();
    }

    private static byte[] VarInt(long value)
    {
        Span<byte> buffer = stackalloc byte[10];
        var w = new WireWriter(buffer);
        w.WriteVarInt(value);
        Assert.Equal(WireWriter.VarIntSize(value), w.Position);
        return w.Written.ToArray();
    }

    private static ProtocolError ErrorOf(Action action)
    {
        ProtocolException e = Assert.Throws<ProtocolException>(action);
        return e.Error;
    }

    [Theory]
    [InlineData(0UL, new byte[] { 0x00 })]
    [InlineData(1UL, new byte[] { 0x01 })]
    [InlineData(127UL, new byte[] { 0x7F })]
    [InlineData(128UL, new byte[] { 0x80, 0x01 })]
    [InlineData(300UL, new byte[] { 0xAC, 0x02 })]
    [InlineData(16383UL, new byte[] { 0xFF, 0x7F })]
    [InlineData(16384UL, new byte[] { 0x80, 0x80, 0x01 })]
    [InlineData(ulong.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
    public void VarUIntHasTheLeb128Bytes(ulong value, byte[] expected)
    {
        Assert.Equal(expected, VarUInt(value));
        var r = new WireReader(expected);
        Assert.Equal(value, r.ReadVarUInt());
        Assert.True(r.End);
    }

    [Theory]
    [InlineData(0L, new byte[] { 0x00 })]
    [InlineData(-1L, new byte[] { 0x01 })]
    [InlineData(1L, new byte[] { 0x02 })]
    [InlineData(-2L, new byte[] { 0x03 })]
    [InlineData(63L, new byte[] { 0x7E })]
    [InlineData(-64L, new byte[] { 0x7F })]
    [InlineData(64L, new byte[] { 0x80, 0x01 })]
    public void VarIntIsZigZag(long value, byte[] expected)
    {
        Assert.Equal(expected, VarInt(value));
        var r = new WireReader(expected);
        Assert.Equal(value, r.ReadVarInt());
    }

    [Fact]
    public void VarintsRoundTripAtTheEdgesAndForSeededRandomValues()
    {
        var rng = new Random(20261007);
        var unsigned = new List<ulong> { 0, 1, 127, 128, 255, 256, uint.MaxValue, (ulong)uint.MaxValue + 1, long.MaxValue, (ulong)long.MaxValue + 1, ulong.MaxValue };
        var signed = new List<long> { 0, 1, -1, 63, -64, 64, -65, int.MinValue, int.MaxValue, long.MinValue, long.MinValue + 1, long.MaxValue };
        for (int i = 0; i < 5000; i++)
        {
            ulong u = (ulong)rng.NextInt64() << rng.Next(0, 2) ^ (ulong)rng.NextInt64();
            unsigned.Add(u >> rng.Next(0, 64));
            signed.Add(rng.NextInt64(long.MinValue, long.MaxValue) >> rng.Next(0, 63));
        }

        foreach (ulong value in unsigned)
        {
            var r = new WireReader(VarUInt(value));
            Assert.Equal(value, r.ReadVarUInt());
            Assert.True(r.End);
        }

        foreach (long value in signed)
        {
            var r = new WireReader(VarInt(value));
            Assert.Equal(value, r.ReadVarInt());
            Assert.True(r.End);
        }
    }

    [Fact]
    public void AVarintMustBeCanonicalShortAndWithinTenBytes()
    {
        Assert.Equal(ProtocolError.BadVarint, ErrorOf(() => new WireReader(new byte[] { 0x80, 0x00 }).ReadVarUInt()));
        Assert.Equal(ProtocolError.BadVarint, ErrorOf(() => new WireReader(new byte[] { 0xFF, 0x80, 0x00 }).ReadVarUInt()));
        // Eleven bytes, and a tenth byte above 1 (bit 64).
        Assert.Equal(ProtocolError.BadVarint, ErrorOf(() => new WireReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 }).ReadVarUInt()));
        Assert.Equal(ProtocolError.BadVarint, ErrorOf(() => new WireReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 }).ReadVarUInt()));
        Assert.Equal(ProtocolError.BadVarint, ErrorOf(() => new WireReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x81, 0x00 }).ReadVarUInt()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[] { 0x80 }).ReadVarUInt()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(ReadOnlySpan<byte>.Empty).ReadVarUInt()));
    }

    [Fact]
    public void FixedWidthIntegersAreLittleEndian()
    {
        Span<byte> buffer = stackalloc byte[16];
        var w = new WireWriter(buffer);
        w.WriteByte(0xAB);
        w.WriteUInt16(0x1234);
        w.WriteUInt32(0xDEADBEEF);
        w.WriteUInt64(0x0102030405060708UL);
        Assert.Equal(new byte[] { 0xAB, 0x34, 0x12, 0xEF, 0xBE, 0xAD, 0xDE, 8, 7, 6, 5, 4, 3, 2, 1 }, w.Written.ToArray());
        var r = new WireReader(w.Written);
        Assert.Equal(0xAB, r.ReadByte());
        Assert.Equal(0x1234, r.ReadUInt16());
        Assert.Equal(0xDEADBEEFu, r.ReadUInt32());
        Assert.Equal(0x0102030405060708UL, r.ReadUInt64());
        r.ExpectEnd();
    }

    [Fact]
    public void ASingleIsLittleEndianIeee754AndRefusesNaNAndInfinity()
    {
        Span<byte> buffer = stackalloc byte[8];
        var w = new WireWriter(buffer);
        w.WriteSingle(1.5f);
        Assert.Equal(new byte[] { 0x00, 0x00, 0xC0, 0x3F }, w.Written.ToArray());
        foreach (float value in new[] { 0f, -0f, 1f, -1.25f, float.Epsilon, float.MaxValue, float.MinValue, 10.666667f })
        {
            var w2 = new WireWriter(buffer);
            w2.WriteSingle(value);
            var r = new WireReader(w2.Written);
            Assert.Equal(BitConverter.SingleToInt32Bits(value), BitConverter.SingleToInt32Bits(r.ReadSingle()));
        }

        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Equal(ProtocolError.BadValue, ErrorOf(() =>
            {
                Span<byte> b = stackalloc byte[4];
                new WireWriter(b).WriteSingle(bad);
            }));
            byte[] raw = new byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(raw, bad);
            Assert.Equal(ProtocolError.BadValue, ErrorOf(() => new WireReader(raw).ReadSingle()));
        }

        // A NaN with a payload, and a negative quiet NaN.
        Assert.Equal(ProtocolError.BadValue, ErrorOf(() => new WireReader(new byte[] { 0x01, 0x00, 0xC0, 0x7F }).ReadSingle()));
        Assert.Equal(ProtocolError.BadValue, ErrorOf(() => new WireReader(new byte[] { 0x00, 0x00, 0xC0, 0xFF }).ReadSingle()));
    }

    [Fact]
    public void AStringIsLengthPlusOneThenUtf8WithNullAndEmptyDistinct()
    {
        Span<byte> buffer = stackalloc byte[64];
        var w = new WireWriter(buffer);
        w.WriteString(null, 10);
        w.WriteString("", 10);
        w.WriteString("hi", 10);
        w.WriteString("\u00E9\u20AC\U0001F600", 10);
        Assert.Equal(new byte[] { 0, 1, 3, (byte)'h', (byte)'i', 10, 0xC3, 0xA9, 0xE2, 0x82, 0xAC, 0xF0, 0x9F, 0x98, 0x80 }, w.Written.ToArray());
        var r = new WireReader(w.Written);
        Assert.Null(r.ReadString(10));
        Assert.Equal(string.Empty, r.ReadString(10));
        Assert.Equal("hi", r.ReadString(10));
        Assert.Equal("\u00E9\u20AC\U0001F600", r.ReadString(10));
        r.ExpectEnd();
    }

    [Fact]
    public void AStringAtItsCapPassesAndOneByteOverFailsOnBothSides()
    {
        byte[] buffer = new byte[600];
        string atCap = new('x', 512);
        string over = new('x', 513);
        var w = new WireWriter(buffer);
        w.WriteString(atCap, 512);
        Assert.Equal(512 + 2, w.Position);
        Assert.Equal(atCap, new WireReader(w.Written).ReadString(512));
        byte[] encoded = w.Written.ToArray();
        Assert.Equal(ProtocolError.BadString, ErrorOf(() => new WireWriter(new byte[600]).WriteString(over, 512)));
        Assert.Equal(ProtocolError.BadString, ErrorOf(() => new WireReader(encoded).ReadString(511)));

        // Multi-byte: 128 four-byte characters are exactly 512 bytes; 129 are over.
        string emoji = string.Concat(Enumerable.Repeat("\U0001F600", 128));
        var w2 = new WireWriter(new byte[600]);
        w2.WriteString(emoji, 512);
        Assert.Equal(emoji, new WireReader(w2.Written).ReadString(512));
        Assert.Equal(ProtocolError.BadString, ErrorOf(() => new WireWriter(new byte[600]).WriteString(emoji + "\U0001F600", 512)));
    }

    [Fact]
    public void AStringReaderRefusesBadLengthsAndBadUtf8WithoutAllocating()
    {
        // A claimed length over the cap, and one over the bytes left.
        Assert.Equal(ProtocolError.BadString, ErrorOf(() => new WireReader(new byte[] { 0xFF, 0xFF, 0x03 }).ReadString(512)));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[] { 0x0A, 1, 2, 3 }).ReadString(512)));
        Assert.Equal(ProtocolError.BadVarint, ErrorOf(() => new WireReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }).ReadString(512)));
        // Invalid UTF-8: an overlong NUL, a lone continuation byte, a surrogate, a truncated sequence, a byte over U+10FFFF.
        foreach (byte[] bad in new[]
                 {
                     new byte[] { 3, 0xC0, 0x80 },
                     new byte[] { 2, 0x80 },
                     new byte[] { 4, 0xED, 0xA0, 0x80 },
                     new byte[] { 3, 0xE2, 0x82 },
                     new byte[] { 5, 0xF4, 0x90, 0x80, 0x80 },
                     new byte[] { 2, 0xFF },
                 })
        {
            Assert.Equal(ProtocolError.BadString, ErrorOf(() => new WireReader(bad).ReadString(64)));
        }
    }

    [Fact]
    public void ACountIsCappedAndMustFitTheBytesLeft()
    {
        var w = new WireWriter(new byte[32]);
        w.WriteCount(3, 10);
        Assert.Equal(ProtocolError.CountTooLarge, ErrorOf(() => new WireWriter(new byte[32]).WriteCount(11, 10)));
        Assert.Equal(ProtocolError.CountTooLarge, ErrorOf(() => new WireWriter(new byte[32]).WriteCount(-1, 10)));
        // 3 items of 2 bytes need 6 bytes left; 5 are not enough.
        Assert.Equal(ProtocolError.CountTooLarge, ErrorOf(() => new WireReader(new byte[] { 3, 0, 0, 0, 0, 0 }).ReadCount(10, 2)));
        Assert.Equal(3, new WireReader(new byte[] { 3, 0, 0, 0, 0, 0, 0 }).ReadCount(10, 2));
        // Over the cap even when the bytes are there.
        Assert.Equal(ProtocolError.CountTooLarge, ErrorOf(() => new WireReader(new byte[] { 11, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ReadCount(10, 1)));
        // A forged huge count (2^63) is refused before any multiplication can overflow.
        Assert.Equal(ProtocolError.CountTooLarge, ErrorOf(() => new WireReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 }).ReadCount(int.MaxValue, int.MaxValue)));
    }

    [Fact]
    public void ATokenIsSixteenBytesLowThenHigh()
    {
        var token = new SessionToken(0x0807060504030201UL, 0x100F0E0D0C0B0A09UL);
        var w = new WireWriter(new byte[32]);
        w.WriteToken(token);
        Assert.Equal(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray(), w.Written.ToArray());
        Assert.Equal(token, new WireReader(w.Written).ReadToken());
        Assert.True(SessionToken.None.IsNone);
        Assert.False(token.IsNone);
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[15]).ReadToken()));
    }

    [Fact]
    public void TheWriterRefusesToPassItsBufferAndTheReaderToPassItsEnd()
    {
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(Span<byte>.Empty).WriteByte(1)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[1]).WriteUInt16(1)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[3]).WriteUInt32(1)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[7]).WriteUInt64(1)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[1]).WriteVarUInt(128)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[3]).WriteSingle(1f)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[15]).WriteToken(default)));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[3]).WriteBytes(new byte[4])));
        Assert.Equal(ProtocolError.BufferTooSmall, ErrorOf(() => new WireWriter(new byte[3]).WriteString("abc", 10)));
        var w = new WireWriter(new byte[4]);
        w.WriteByte(1);
        Assert.Equal(3, w.Remaining);
        Assert.Equal(1, w.Position);

        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(ReadOnlySpan<byte>.Empty).ReadByte()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[1]).ReadUInt16()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[3]).ReadUInt32()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[7]).ReadUInt64()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[3]).ReadSingle()));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[3]).ReadBytes(4)));
        Assert.Equal(ProtocolError.Truncated, ErrorOf(() => new WireReader(new byte[3]).ReadBytes(-1)));
        Assert.Equal(ProtocolError.TrailingBytes, ErrorOf(() => new WireReader(new byte[1]).ExpectEnd()));
        new WireReader(ReadOnlySpan<byte>.Empty).ExpectEnd();
    }

    [Fact]
    public void TheMessageIdIsTheFirstByteAndOnlyKnownIdsPass()
    {
        for (int id = 0; id < 256; id++)
        {
            byte value = (byte)id;
            bool known = id >= 1 && id <= 35; // version 100: Hotbar = 34, Storages = 35; version 99: PhotoRequest = 32, PhotoPart = 33; version 92: GunSight = 31; version 84: Weather = 30; version 79: RuntimeResource = 29; version 70: BuildPreview = 28; version 38: SayModal = 19, Runechat = 20; version 40: RadioChannels = 21; version 42: Motion = 22; version 53: BugReport = 23; version 54: HeldMotion = 24; version 63: Tts = 25; version 67: Minimap = 26; version 68: MinimapMobs = 27
            Assert.Equal(known, WireReader.IsKnownMessageId(value));
            if (known)
                Assert.Equal((MessageId)id, new WireReader(new[] { value }).ReadMessageId());
            else
                Assert.Equal(ProtocolError.UnknownMessage, ErrorOf(() => new WireReader(new[] { value }).ReadMessageId()));
        }
    }

    [Fact]
    public void ReadBytesIsAViewNotACopy()
    {
        byte[] message = { 1, 2, 3, 4 };
        var r = new WireReader(message);
        ReadOnlySpan<byte> two = r.ReadBytes(2);
        Assert.Equal(2, r.Position);
        Assert.Equal(2, r.Remaining);
        Assert.Equal(new byte[] { 1, 2 }, two.ToArray());
        message[0] = 9;
        Assert.Equal(9, two[0]);
    }
}
