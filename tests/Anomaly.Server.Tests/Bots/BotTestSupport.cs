using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>
/// A client transport that does what a test tells it to and records what the bot sends. Like the real transports it raises
/// its events only inside <see cref="Poll"/>, in the order they were queued.
/// </summary>
internal sealed class FakeClientTransport : IClientTransport
{
    private readonly Queue<Action> _events = new();

    public record SentMessage(Channel Channel, byte[] Bytes, bool Connected);

    public List<SentMessage> Sent { get; } = new();

    public List<(string Host, int Port, byte[] Hello)> Connects { get; } = new();

    public int DisconnectCalls { get; private set; }

    /// <summary>True: <see cref="Disconnect"/> never produces a <c>Disconnected</c> event (a transport that loses it).</summary>
    public bool SwallowDisconnect { get; set; }

    /// <summary>When set, <see cref="Connect"/> throws it.</summary>
    public Exception ConnectThrows { get; set; }

    /// <summary>When set, <see cref="Poll"/> throws it (a transport that misbehaves).</summary>
    public Exception PollThrows { get; set; }

    public int Polls { get; private set; }

    public bool Disposed { get; private set; }

    public ClientTransportState State { get; private set; }

    public int RoundTripMs { get; set; }

    public long BytesSent { get; private set; }

    public long BytesReceived { get; private set; }

    public event Action Connected;

    public event ClientReceivedHandler Received;

    public event Action<TransportDisconnect> Disconnected;

    public void Connect(string host, int port, ReadOnlySpan<byte> hello)
    {
        if (ConnectThrows != null) throw ConnectThrows;
        Connects.Add((host, port, hello.ToArray()));
        State = ClientTransportState.Connecting;
    }

    public void Poll()
    {
        Polls++;
        if (PollThrows != null) throw PollThrows;
        while (_events.Count > 0) _events.Dequeue()();
    }

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        Sent.Add(new SentMessage(channel, message.ToArray(), State == ClientTransportState.Connected));
        BytesSent += message.Length;
    }

    public void Disconnect()
    {
        DisconnectCalls++;
        if (State == ClientTransportState.Connected || State == ClientTransportState.Connecting)
        {
            State = ClientTransportState.Disconnected;
            if (!SwallowDisconnect)
            {
                _events.Enqueue(() => Disconnected?.Invoke(new TransportDisconnect(TransportDisconnectKind.LocalClose, Array.Empty<byte>())));
            }
        }
    }

    public void Dispose()
    {
        Disposed = true;
    }

    // ---- what the test makes the "server" do ----

    /// <summary>The server accepted the request: <c>Connected</c> at the next poll.</summary>
    public void Accept()
    {
        State = ClientTransportState.Connected;
        _events.Enqueue(() => Connected?.Invoke());
    }

    /// <summary>A message from the server at the next poll.</summary>
    public void Deliver(Channel channel, byte[] message, bool evenWhenEnded = false)
    {
        // As on a real transport, nothing arrives once the connection has ended.
        if (!evenWhenEnded && (State == ClientTransportState.Disconnected || State == ClientTransportState.Idle)) return;
        BytesReceived += message.Length;
        _events.Enqueue(() => Received?.Invoke(channel, message));
    }

    /// <summary>The server refuses the connection request with a reason.</summary>
    public void Reject(DisconnectReason reason, string text = null)
    {
        State = ClientTransportState.Disconnected;
        byte[] data = Codec.Encode(new DisconnectInfo(reason, text));
        _events.Enqueue(() => Disconnected?.Invoke(new TransportDisconnect(TransportDisconnectKind.Rejected, data)));
    }

    /// <summary>The server closes a connection with a reason.</summary>
    public void Close(DisconnectReason reason, string text = null)
    {
        State = ClientTransportState.Disconnected;
        byte[] data = Codec.Encode(new DisconnectInfo(reason, text));
        _events.Enqueue(() => Disconnected?.Invoke(new TransportDisconnect(TransportDisconnectKind.RemoteClose, data)));
    }

    /// <summary>The connection could not be made.</summary>
    public void Fail(TransportDisconnectKind kind)
    {
        State = ClientTransportState.Disconnected;
        _events.Enqueue(() => Disconnected?.Invoke(new TransportDisconnect(kind, Array.Empty<byte>())));
    }

    // ---- what the test reads back ----

    /// <summary>The MotionInput messages the bot sent (on the Input channel), decoded; messages that do not decode are skipped.</summary>
    public List<MotionInputMessage> MotionMessages()
    {
        var messages = new List<MotionInputMessage>();
        foreach (SentMessage m in Sent)
        {
            if (m.Channel == Channel.Input && Codec.TryDecode(m.Bytes, out MotionInputMessage message, out _)) messages.Add(message);
        }
        return messages;
    }

    /// <summary>The Action messages the bot sent (on the Control channel), decoded.</summary>
    public List<ActionMessage> Actions()
    {
        var actions = new List<ActionMessage>();
        foreach (SentMessage m in Sent)
        {
            if (m.Channel == Channel.Control && Codec.TryDecode(m.Bytes, out ActionMessage action, out _)) actions.Add(action);
        }
        return actions;
    }

    /// <summary>The Hello of connection attempt <paramref name="index"/>.</summary>
    public Hello HelloOf(int index)
    {
        Assert.True(Codec.TryDecode(Connects[index].Hello, out Hello hello, out ProtocolError error), "the Hello does not decode: " + error);
        return hello;
    }
}

/// <summary>Small maps for the tests: a walled room, optionally with pillars.</summary>
internal static class TestMaps
{
    public const string Wall = "/turf/closed/wall";
    public const string Floor = "/turf/open/floor/plasteel";

    /// <summary>A <paramref name="width"/> x <paramref name="height"/> room: wall on the rim and on <paramref name="pillars"/> (BYOND x, y), floor elsewhere.</summary>
    public static MapGrid Room(int width, int height, params (int X, int Y)[] pillars)
    {
        var grid = new MapGrid { Tick = 0, Width = width, Height = height, Levels = 1 };
        grid.TurfTypes.Add(Wall);
        grid.TurfTypes.Add(Floor);
        var pillarSet = new HashSet<(int, int)>(pillars);
        int runType = -1;
        int runCount = 0;
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
            {
                bool wall = x == 1 || y == 1 || x == width || y == height || pillarSet.Contains((x, y));
                int type = wall ? 0 : 1;
                if (type == runType)
                {
                    runCount++;
                    continue;
                }
                if (runCount > 0) grid.Runs.Add(new TurfRun(runCount, runType));
                runType = type;
                runCount = 1;
            }
        }
        grid.Runs.Add(new TurfRun(runCount, runType));
        return grid;
    }
}
