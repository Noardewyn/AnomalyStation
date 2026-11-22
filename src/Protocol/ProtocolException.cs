namespace Anomaly.Protocol;

// Owner: M1 P2 (contract: M1 contracts step).

/// <summary>What was wrong with a message (<see cref="ProtocolException.Error"/>, the out value of every <c>Codec.TryDecode</c>).</summary>
public enum ProtocolError : byte
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>The message ended before a value was complete.</summary>
    Truncated,

    /// <summary>The message is longer than its cap (<see cref="Limits.MaxInboundMessageBytes"/>, <see cref="Limits.MaxOutboundMessageBytes"/>).</summary>
    TooLong,

    /// <summary>A varint longer than 10 bytes, or one that overflows its type.</summary>
    BadVarint,

    /// <summary>A string that is not valid UTF-8 or is longer than its byte cap.</summary>
    BadString,

    /// <summary>A count above its cap, or one the bytes left in the message cannot hold.</summary>
    CountTooLarge,

    /// <summary>A value out of its range: NaN or an infinity, an unknown enum value, a direction mask with bits beyond the four cardinal ones, a coordinate outside the map, a type index outside the table, a change mask with bits a change cannot hold.</summary>
    BadValue,

    /// <summary>The first byte is no <see cref="MessageId"/>.</summary>
    UnknownMessage,

    /// <summary>The first byte is a known id, but not the one being decoded.</summary>
    WrongMessage,

    /// <summary>Bytes left over after the message.</summary>
    TrailingBytes,

    /// <summary><see cref="Hello"/> does not start with <see cref="ProtocolVersion.Magic"/>.</summary>
    BadMagic,

    /// <summary>The destination span of an encoder is too small (an encoder error, never a decoder one).</summary>
    BufferTooSmall,
}

/// <summary>
/// The only exception the codec lets out (scope 2j "Codec": a fuzzed input never raises anything else). Readers and
/// writers throw it; every <c>Codec.TryDecode</c> catches it and returns false with its <see cref="Error"/>. The server
/// host counts a decode failure as a strike; a client treats one as a desync and leaves.
/// </summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(ProtocolError error, string message)
        : base(message)
    {
        Error = error;
    }

    /// <summary>What was wrong.</summary>
    public ProtocolError Error { get; }
}
