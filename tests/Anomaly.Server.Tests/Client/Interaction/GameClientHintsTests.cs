using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>M4a P7: the real feed (<see cref="GameClient"/>) hands the server's ContextHints (message 11 on the Control channel) to the HUD and refuses a damaged one.</summary>
public class GameClientHintsTests
{
    private sealed class Transport : IClientTransport
    {
        public ClientTransportState State { get; set; } = ClientTransportState.Idle;

        public int RoundTripMs => 1;

        public long BytesSent => 0;

        public long BytesReceived => 0;

        public event Action Connected;

        public event ClientReceivedHandler Received;

        public event Action<TransportDisconnect> Disconnected
        {
            add { }
            remove { }
        }

        public void Connect(string host, int port, ReadOnlySpan<byte> hello) => State = ClientTransportState.Connecting;

        public void Poll()
        {
        }

        public void Send(Channel channel, ReadOnlySpan<byte> message)
        {
        }

        public void Disconnect()
        {
        }

        public void Dispose()
        {
        }

        public void RaiseConnected()
        {
            State = ClientTransportState.Connected;
            Connected?.Invoke();
        }

        public void RaiseReceived(Channel channel, byte[] message) => Received?.Invoke(channel, message);
    }

    private static (GameClient Client, Transport Transport) Joined()
    {
        var transport = new Transport();
        var client = new GameClient(transport, "127.0.0.1", 7777, "Alice");
        client.Connect();
        transport.RaiseConnected();
        var map = new MapGrid { Tick = 10, Width = 4, Height = 4, Levels = 1 };
        map.TurfTypes.Add("/turf/open/indestructible");
        map.Runs.Add(new TurfRun(16, 0));
        var snapshot = new Snapshot { Tick = 10, OwnEntity = 5 };
        snapshot.Types.Add("/mob");
        snapshot.Entities.Add(new EntityState(5, 0, 2, 2, 1, 2, "Alice", 0f, 10));
        transport.RaiseReceived(Channel.State, Codec.Encode(new Welcome(1, new SessionToken(1, 2), 10, 20, 5)));
        transport.RaiseReceived(Channel.State, Codec.Encode(map));
        transport.RaiseReceived(Channel.State, Codec.Encode(snapshot));
        return (client, transport);
    }

    [Fact]
    public void AContextHintsMessageReachesTheEventWithItsLabels()
    {
        (GameClient client, Transport transport) = Joined();
        Assert.Equal(FeedPhase.Playing, client.Phase);
        var received = new List<ContextHintsMessage>();
        client.ContextHintsReceived += received.Add;
        var sent = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 5, Name = "toolbox" };
        sent.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Pick up"));
        sent.Hints.Add(new ContextHint(ClickButton.Right, ClickModifiers.Ctrl, "Open"));
        transport.RaiseReceived(Channel.Control, Codec.Encode(sent));
        ContextHintsMessage got = Assert.Single(received);
        Assert.Equal((TargetKind.Entity, 5L, "toolbox"), (got.TargetKind, got.Target, got.Name));
        Assert.Equal(new[] { "Pick up", "Open" }, got.Hints.Select(h => h.Label));
        Assert.Equal(ClickModifiers.Ctrl, got.Hints[1].Modifiers);
        Assert.Equal(FeedPhase.Playing, client.Phase);
    }

    [Fact]
    public void ADamagedContextHintsMessageEndsTheSession()
    {
        (GameClient client, Transport transport) = Joined();
        transport.RaiseReceived(Channel.Control, new byte[] { (byte)MessageId.ContextHints, 0xFF, 0xFF, 0xFF });
        Assert.Equal(FeedPhase.Closed, client.Phase);
        Assert.Contains("ContextHints", client.CloseReason);
    }
}
