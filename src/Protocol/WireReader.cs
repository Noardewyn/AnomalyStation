using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace Anomaly.Protocol;

// Owner: M1 P2. Contract by the M1 contracts step; the bodies are P2's.

/// <summary>
/// The decoder's cursor over a received message (the encoding rules are on <see cref="WireWriter"/>). Every read checks
/// the bytes left and its cap before it allocates anything, and every failure is a <see cref="ProtocolException"/>: a
/// fuzzed input (random bytes, truncations, bit flips, oversized lengths and counts, NaN) never raises anything else and
/// never makes a decoder allocate past the caps of <see cref="Limits"/> (scope 2j "Codec").
/// <para>
/// Varints must be canonical (the shortest form): a redundant zero group is <see cref="ProtocolError.BadVarint"/>, so a
/// value has exactly one encoding.
/// </para>
/// </summary>
public ref struct WireReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public WireReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>Bytes read so far.</summary>
    public readonly int Position => _position;

    /// <summary>Bytes left.</summary>
    public readonly int Remaining => _buffer.Length - _position;

    /// <summary>True when every byte has been read.</summary>
    public readonly bool End => _position >= _buffer.Length;

    /// <summary>The first byte of a message; a value outside <see cref="MessageId"/> throws (<see cref="ProtocolError.UnknownMessage"/>).</summary>
    public MessageId ReadMessageId()
    {
        byte value = ReadByte();
        if (!IsKnownMessageId(value))
            throw new ProtocolException(ProtocolError.UnknownMessage, "unknown message id " + value);
        return (MessageId)value;
    }

    /// <summary>True for the ids a message may start with (<see cref="MessageId.Hello"/> through <see cref="MessageId.Motion"/>, version 42).</summary>
    public static bool IsKnownMessageId(byte value) => value >= (byte)MessageId.Hello && value <= (byte)MessageId.GunSight;

    /// <summary>Version 34: the next byte, not consumed (a mouse action's target kind, 0 for none).</summary>
    public readonly byte PeekByte()
    {
        if (_position >= _buffer.Length)
            throw Truncated();
        return _buffer[_position];
    }

    public byte ReadByte()
    {
        if (_position >= _buffer.Length)
            throw Truncated();
        return _buffer[_position++];
    }

    public ushort ReadUInt16()
    {
        Need(2);
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position));
        _position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        Need(4);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_position));
        _position += 4;
        return value;
    }

    public ulong ReadUInt64()
    {
        Need(8);
        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position));
        _position += 8;
        return value;
    }

    /// <summary>An unsigned LEB128 varint; more than 10 bytes or an overflow throws (<see cref="ProtocolError.BadVarint"/>).</summary>
    public ulong ReadVarUInt()
    {
        ulong result = 0;
        for (int shift = 0; shift < 70; shift += 7)
        {
            if (_position >= _buffer.Length)
                throw Truncated();
            byte b = _buffer[_position++];
            if (shift == 63 && b > 1)
                throw new ProtocolException(ProtocolError.BadVarint, "a varint overflows 64 bits");
            result |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80)
            {
                if (b == 0 && shift > 0)
                    throw new ProtocolException(ProtocolError.BadVarint, "a varint is not in its shortest form");
                return result;
            }
        }

        throw new ProtocolException(ProtocolError.BadVarint, "a varint is longer than 10 bytes");
    }

    /// <summary>A zigzag-encoded signed varint.</summary>
    public long ReadVarInt()
    {
        ulong value = ReadVarUInt();
        return (long)(value >> 1) ^ -(long)(value & 1);
    }

    /// <summary>An IEEE 754 single; NaN and the infinities throw (<see cref="ProtocolError.BadValue"/>).</summary>
    public float ReadSingle()
    {
        Need(4);
        float value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.Slice(_position));
        _position += 4;
        if (!float.IsFinite(value))
            throw new ProtocolException(ProtocolError.BadValue, "a NaN or infinite float");
        return value;
    }

    /// <summary>The next <paramref name="count"/> raw bytes (a view into the message, not a copy).</summary>
    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count < 0 || count > _buffer.Length - _position)
            throw Truncated();
        ReadOnlySpan<byte> value = _buffer.Slice(_position, count);
        _position += count;
        return value;
    }

    /// <summary>A capped UTF-8 string (null for the length 0); a length over <paramref name="maxBytes"/> or invalid UTF-8 throws (<see cref="ProtocolError.BadString"/>).</summary>
    public string ReadString(int maxBytes)
    {
        ulong lengthPlusOne = ReadVarUInt();
        if (lengthPlusOne == 0)
            return null;
        ulong length = lengthPlusOne - 1;
        if (length > (ulong)maxBytes)
            throw new ProtocolException(ProtocolError.BadString, "a string of " + length + " bytes is over its cap of " + maxBytes);
        if (length > (ulong)(_buffer.Length - _position))
            throw Truncated();
        if (length == 0)
            return string.Empty;
        ReadOnlySpan<byte> bytes = _buffer.Slice(_position, (int)length);
        if (!Utf8.IsValid(bytes))
            throw new ProtocolException(ProtocolError.BadString, "a string is not valid UTF-8");
        _position += (int)length;
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// A list count: above <paramref name="max"/>, or more items than the bytes left can hold at
    /// <paramref name="minBytesPerItem"/> bytes each, throws (<see cref="ProtocolError.CountTooLarge"/>), so a forged
    /// count never makes the caller allocate.
    /// </summary>
    public int ReadCount(int max, int minBytesPerItem)
    {
        ulong count = ReadVarUInt();
        if (count > (ulong)max)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a list of " + count + " items is over its cap of " + max);
        if ((long)count * Math.Max(minBytesPerItem, 0) > _buffer.Length - _position)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a list of " + count + " items does not fit the bytes left");
        return (int)count;
    }

    /// <summary>A <see cref="SessionToken"/> (<see cref="Limits.TokenBytes"/> bytes).</summary>
    public SessionToken ReadToken()
    {
        Need(Limits.TokenBytes);
        var token = new SessionToken(
            BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position)),
            BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position + 8)));
        _position += Limits.TokenBytes;
        return token;
    }

    /// <summary>Throws <see cref="ProtocolError.TrailingBytes"/> unless every byte has been read.</summary>
    public readonly void ExpectEnd()
    {
        if (_position < _buffer.Length)
            throw new ProtocolException(ProtocolError.TrailingBytes, (_buffer.Length - _position) + " bytes follow the message");
    }

    private readonly void Need(int count)
    {
        if (count > _buffer.Length - _position)
            throw Truncated();
    }

    private static ProtocolException Truncated() => new(ProtocolError.Truncated, "the message ends early");
}
