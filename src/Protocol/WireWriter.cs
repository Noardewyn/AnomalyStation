using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Anomaly.Protocol;

// Owner: M1 P2. Contract (signatures and the encoding rules below) by the M1 contracts step; the bodies are P2's.

/// <summary>
/// The encoder's cursor over a caller's buffer (scope 2e "Framing and serialization": no NetDataWriter, no serializer
/// library). Encoding rules, shared with <see cref="WireReader"/>:
/// <list type="bullet">
/// <item><description>fixed-width integers little-endian (<c>BinaryPrimitives</c>);</description></item>
/// <item><description>varints LEB128, 7 bits per byte, low group first, at most 10 bytes; signed varints zigzag-encoded first;</description></item>
/// <item><description><c>f32</c> IEEE 754 little-endian; NaN and the infinities are refused (<see cref="ProtocolError.BadValue"/>);</description></item>
/// <item><description>a string as a varint of its UTF-8 byte length plus one (0 is null, 1 is empty), then the bytes, within a byte cap;</description></item>
/// <item><description>a count as a varint within a cap.</description></item>
/// </list>
/// Writing past the end of the buffer, or a value over its cap, throws <see cref="ProtocolException"/>
/// (<see cref="ProtocolError.BufferTooSmall"/>, <see cref="ProtocolError.BadString"/>, <see cref="ProtocolError.CountTooLarge"/>).
/// It never allocates.
/// </summary>
public ref struct WireWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public WireWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>Bytes written so far.</summary>
    public readonly int Position => _position;

    /// <summary>Bytes still free in the buffer.</summary>
    public readonly int Remaining => _buffer.Length - _position;

    /// <summary>The bytes written so far.</summary>
    public readonly ReadOnlySpan<byte> Written => _buffer.Slice(0, _position);

    /// <summary>The number of bytes <see cref="WriteVarUInt"/> uses for <paramref name="value"/> (1 to 10).</summary>
    public static int VarUIntSize(ulong value) => (63 - BitOperations.LeadingZeroCount(value | 1)) / 7 + 1;

    /// <summary>The number of bytes <see cref="WriteVarInt"/> uses for <paramref name="value"/> (1 to 10).</summary>
    public static int VarIntSize(long value) => VarUIntSize(ZigZag(value));

    /// <summary>The first byte of a message.</summary>
    public void WriteMessageId(MessageId id) => WriteByte((byte)id);

    public void WriteByte(byte value)
    {
        Ensure(1);
        _buffer[_position++] = value;
    }

    public void WriteUInt16(ushort value)
    {
        Ensure(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.Slice(_position), value);
        _position += 2;
    }

    public void WriteUInt32(uint value)
    {
        Ensure(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.Slice(_position), value);
        _position += 4;
    }

    public void WriteUInt64(ulong value)
    {
        Ensure(8);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Slice(_position), value);
        _position += 8;
    }

    /// <summary>An unsigned LEB128 varint.</summary>
    public void WriteVarUInt(ulong value)
    {
        Ensure(VarUIntSize(value));
        while (value >= 0x80)
        {
            _buffer[_position++] = (byte)(value | 0x80);
            value >>= 7;
        }

        _buffer[_position++] = (byte)value;
    }

    /// <summary>A zigzag-encoded signed varint.</summary>
    public void WriteVarInt(long value) => WriteVarUInt(ZigZag(value));

    /// <summary>An IEEE 754 single, little-endian; NaN and the infinities throw (<see cref="ProtocolError.BadValue"/>).</summary>
    public void WriteSingle(float value)
    {
        if (!float.IsFinite(value))
            throw new ProtocolException(ProtocolError.BadValue, "a NaN or infinite float cannot be sent");
        Ensure(4);
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.Slice(_position), value);
        _position += 4;
    }

    /// <summary>Raw bytes (a token, the magic, an encoded body).</summary>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        Ensure(value.Length);
        value.CopyTo(_buffer.Slice(_position));
        _position += value.Length;
    }

    /// <summary>A capped UTF-8 string: length + 1 as a varint (0 for null), then the bytes; more than <paramref name="maxBytes"/> bytes throws.</summary>
    public void WriteString(string value, int maxBytes)
    {
        if (value == null)
        {
            WriteByte(0);
            return;
        }

        // Every UTF-16 char takes at least one UTF-8 byte, so a string with more chars than the cap is over it.
        if (value.Length > maxBytes)
            throw StringTooLong(maxBytes);
        int length = Encoding.UTF8.GetByteCount(value);
        if (length > maxBytes)
            throw StringTooLong(maxBytes);
        Ensure(VarUIntSize((ulong)length + 1) + length);
        WriteVarUInt((ulong)length + 1);
        Encoding.UTF8.GetBytes(value, _buffer.Slice(_position, length));
        _position += length;
    }

    /// <summary>A list count as a varint; a negative count or one above <paramref name="max"/> throws.</summary>
    public void WriteCount(int count, int max)
    {
        if (count < 0 || count > max)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a list of " + count + " items is over its cap of " + max);
        WriteVarUInt((ulong)count);
    }

    /// <summary>A <see cref="SessionToken"/>: <see cref="Limits.TokenBytes"/> bytes, <c>Low</c> then <c>High</c>, little-endian.</summary>
    public void WriteToken(SessionToken token)
    {
        Ensure(Limits.TokenBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Slice(_position), token.Low);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Slice(_position + 8), token.High);
        _position += Limits.TokenBytes;
    }

    private static ulong ZigZag(long value) => (ulong)((value << 1) ^ (value >> 63));

    private readonly void Ensure(int count)
    {
        if (count > _buffer.Length - _position)
            throw TooSmall();
    }

    private static ProtocolException TooSmall() => new(ProtocolError.BufferTooSmall, "the destination buffer is too small");

    private static ProtocolException StringTooLong(int maxBytes) => new(ProtocolError.BadString, "a string is over its cap of " + maxBytes + " bytes");
}
