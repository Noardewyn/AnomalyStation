using Anomaly.Protocol;

namespace Anomaly.Server;

/// <summary>
/// The protocol codec as the server host uses it: the messages a server decodes (Hello in a connection request, InputFrame)
/// and encodes (Welcome, the Delta header, ServerText, DisconnectInfo). <see cref="ProtocolCodec"/> forwards to
/// <c>Anomaly.Protocol.Codec</c> (M1 P2); the interface exists so that the host's validation, strikes and session logic are
/// tested on their own (the host tests use a small codec of their own, tests/Anomaly.Server.Tests/Server). Every decoder obeys
/// <c>Codec</c>'s contract: it never throws, and a decoded value satisfies every cap of <see cref="Limits"/>.
/// </summary>
public interface IServerCodec
{
    /// <summary>The id of a message without decoding it; false for an empty message or an unknown id.</summary>
    bool TryPeekId(ReadOnlySpan<byte> message, out MessageId id);

    bool TryDecode(ReadOnlySpan<byte> message, out Hello value, out ProtocolError error);

    bool TryDecode(ReadOnlySpan<byte> message, out InputFrame value, out ProtocolError error);

    byte[] Encode(in Welcome message);

    byte[] Encode(in ServerText message);

    byte[] Encode(in DisconnectInfo message);

    /// <summary>One connection's Delta: its header followed by the tick's shared <paramref name="body"/> (<c>Codec.EncodeDelta</c>).</summary>
    int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, Span<byte> destination);

    /// <summary>The largest per-connection header <see cref="EncodeDelta"/> writes before the body.</summary>
    int MaxDeltaHeaderBytes { get; }

    // ---- protocol version 2 (M2 contracts; ProtocolCodec forwards to M2 P5's Codec, the host's use is M2 P6's) ----

    /// <summary>Version 2: a <see cref="MotionInputMessage"/> from the Input channel.</summary>
    bool TryDecode(ReadOnlySpan<byte> message, out MotionInputMessage value, out ProtocolError error) => Codec.TryDecode(message, out value, out error);

    /// <summary>Version 2: an <see cref="ActionMessage"/> from the Control channel.</summary>
    bool TryDecode(ReadOnlySpan<byte> message, out ActionMessage value, out ProtocolError error) => Codec.TryDecode(message, out value, out error);

    /// <summary>Version 2: one connection's Delta: its header, the shared body and its encoded private block.</summary>
    int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, ReadOnlySpan<byte> ownBlock, Span<byte> destination)
        => Codec.EncodeDelta(tick, lastInputSeq, ownEntity, body, ownBlock, destination);
}

/// <summary>The real codec (<c>Anomaly.Protocol.Codec</c>, M1 P2).</summary>
public sealed class ProtocolCodec : IServerCodec
{
    /// <summary>The one instance (the codec is stateless).</summary>
    public static readonly ProtocolCodec Instance = new();

    private ProtocolCodec()
    {
    }

    public bool TryPeekId(ReadOnlySpan<byte> message, out MessageId id) => Codec.TryPeekId(message, out id);

    public bool TryDecode(ReadOnlySpan<byte> message, out Hello value, out ProtocolError error) => Codec.TryDecode(message, out value, out error);

    public bool TryDecode(ReadOnlySpan<byte> message, out InputFrame value, out ProtocolError error) => Codec.TryDecode(message, out value, out error);

    public byte[] Encode(in Welcome message) => Codec.Encode(message);

    public byte[] Encode(in ServerText message) => Codec.Encode(message);

    public byte[] Encode(in DisconnectInfo message) => Codec.Encode(message);

    public int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, Span<byte> destination)
        => Codec.EncodeDelta(tick, lastInputSeq, ownEntity, body, destination);

    public int MaxDeltaHeaderBytes => Codec.MaxDeltaHeaderBytes;
}
