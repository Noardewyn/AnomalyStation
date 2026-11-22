namespace Anomaly.Net;

// Owner: M1 P2 (an addition to the contracts: the per-channel counters the metrics of P3 and the soak report read).

/// <summary>
/// Per-channel traffic counters, implemented by every connection and client transport of this assembly next to the totals of
/// <see cref="IConnection"/> and <see cref="IClientTransport"/> (test with <c>conn is IChannelCounters</c>). Payload bytes
/// are the protocol messages' bytes; the wire bytes are what the transport really moved (LiteNetLib: its statistics, headers
/// and acks included; the loopback: equal to the payload).
/// </summary>
public interface IChannelCounters
{
    /// <summary>Payload bytes sent on <paramref name="channel"/>.</summary>
    long BytesSentOn(Channel channel);

    /// <summary>Payload bytes received on <paramref name="channel"/>.</summary>
    long BytesReceivedOn(Channel channel);

    /// <summary>Messages sent on <paramref name="channel"/>.</summary>
    long MessagesSentOn(Channel channel);

    /// <summary>Messages received on <paramref name="channel"/>.</summary>
    long MessagesReceivedOn(Channel channel);

    /// <summary>Bytes the transport sent including its own headers and acks (0 where it does not measure).</summary>
    long WireBytesSent { get; }

    /// <summary>Bytes the transport received including its own headers and acks.</summary>
    long WireBytesReceived { get; }
}

/// <summary>The counters behind <see cref="IChannelCounters"/>: plain arrays, written on the thread that sends or polls.</summary>
internal sealed class ChannelTally
{
    private readonly long[] _bytesSent = new long[Channels.Count];
    private readonly long[] _bytesReceived = new long[Channels.Count];
    private readonly long[] _messagesSent = new long[Channels.Count];
    private readonly long[] _messagesReceived = new long[Channels.Count];

    public long BytesSent { get; private set; }

    public long BytesReceived { get; private set; }

    public long MessagesSent { get; private set; }

    public long MessagesReceived { get; private set; }

    public void Sent(Channel channel, int bytes)
    {
        _bytesSent[(int)channel] += bytes;
        _messagesSent[(int)channel]++;
        BytesSent += bytes;
        MessagesSent++;
    }

    public void Received(Channel channel, int bytes)
    {
        _bytesReceived[(int)channel] += bytes;
        _messagesReceived[(int)channel]++;
        BytesReceived += bytes;
        MessagesReceived++;
    }

    public long BytesSentOn(Channel channel) => Channels.IsValid(channel) ? _bytesSent[(int)channel] : 0;

    public long BytesReceivedOn(Channel channel) => Channels.IsValid(channel) ? _bytesReceived[(int)channel] : 0;

    public long MessagesSentOn(Channel channel) => Channels.IsValid(channel) ? _messagesSent[(int)channel] : 0;

    public long MessagesReceivedOn(Channel channel) => Channels.IsValid(channel) ? _messagesReceived[(int)channel] : 0;
}
