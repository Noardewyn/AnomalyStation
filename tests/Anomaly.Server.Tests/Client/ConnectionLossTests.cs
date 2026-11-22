using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>
/// M3 I1c: what the client does when the server closes the session or the connection drops. An interactive run goes back to the connect screen with
/// the server's words on it (the name and the address stay filled in, the reconnect token stays kept); a scripted run (the soak, the smoke, a
/// screenshot, the bench, a headless run) keeps its exit code 1. Both log the reason.
/// </summary>
public class ConnectionLossTests
{
    /// <summary>The words the owner's client got after a server stall (2026-10-09): the rate limit closed the session.</summary>
    private const string FloodText = "Too many commands: the rate limit was exceeded.";

    private sealed class Transport : IClientTransport
    {
        public ClientTransportState State { get; private set; } = ClientTransportState.Idle;
        public int RoundTripMs => 0;
        public long BytesSent => 0;
        public long BytesReceived => 0;
        public event Action Connected;
        public event ClientReceivedHandler Received;
        public event Action<TransportDisconnect> Disconnected;
        public void Connect(string host, int port, ReadOnlySpan<byte> hello) => State = ClientTransportState.Connecting;
        public void Poll() { }
        public void Send(Channel channel, ReadOnlySpan<byte> message) { }
        public void Disconnect() { }
        public void Dispose() { }
        public void Join(MapGrid map, Snapshot snapshot)
        {
            State = ClientTransportState.Connected;
            Connected?.Invoke();
            Received?.Invoke(Channel.State, Codec.Encode(new Welcome(1, new SessionToken(7, 9), 10, 20, 5)));
            Received?.Invoke(Channel.State, Codec.Encode(map));
            Received?.Invoke(Channel.State, Codec.Encode(snapshot));
        }
        public void End(TransportDisconnect end)
        {
            State = ClientTransportState.Disconnected;
            Disconnected?.Invoke(end);
        }
    }

    /// <summary>A client that joined and was then closed by the server with <paramref name="reason"/> and <paramref name="text"/>.</summary>
    private static GameClient ClosedByTheServer(DisconnectReason reason, string text)
    {
        var transport = new Transport();
        var client = new GameClient(transport, "127.0.0.1", 7777, "Alice");
        client.Connect();
        var map = new MapGrid { Tick = 10, Width = 4, Height = 4, Levels = 1 };
        map.TurfTypes.Add("/turf/open/indestructible");
        map.Runs.Add(new TurfRun(16, 0));
        var snapshot = new Snapshot { Tick = 10, LastInputSeq = 0, OwnEntity = 5 };
        snapshot.Types.Add("/mob");
        snapshot.Entities.Add(new EntityState(5, 0, 2, 2, 1, 2, "Alice", 0f, 10));
        transport.Join(map, snapshot);
        Assert.Equal(FeedPhase.Playing, client.Phase);
        transport.End(new TransportDisconnect(TransportDisconnectKind.RemoteClose, Codec.Encode(new DisconnectInfo(reason, text))));
        Assert.Equal(FeedPhase.Closed, client.Phase);
        return client;
    }

    [Fact]
    public void AnInteractiveRunGoesBackToTheConnectScreenWithTheServersWords()
    {
        GameClient client = ClosedByTheServer(DisconnectReason.Flooding, FloodText);
        foreach (string[] arguments in new[] { Array.Empty<string>(), new[] { "--connect=127.0.0.1:7777", "--name=Alice" }, new[] { "--connect=lan-box:7777", "--names" } })
        {
            ConnectionLoss loss = ConnectionLoss.Of(ClientArgs.Parse(arguments), headless: false, client.CloseReason, duringGame: true);
            Assert.False(loss.Quit);
            Assert.Contains("too many messages", loss.Notice);
            Assert.Contains(FloodText, loss.Notice);
            Assert.StartsWith("Disconnected by the server", loss.Notice);
            Assert.EndsWith(".", loss.Notice);
            Assert.Contains(FloodText, loss.LogLine);
            Assert.Contains("connect screen", loss.LogLine);
        }
    }

    [Theory]
    [InlineData("--autowalk")]
    [InlineData("--quit-after=30")]
    [InlineData("--screenshot-after=5,/tmp/x.png")]
    [InlineData("--bench=10")]
    [InlineData("--keys=1:W+")]
    [InlineData("--dev-after=1,fullheal Alice")]
    public void AScriptedRunKeepsItsExitCode(string automation)
    {
        GameClient client = ClosedByTheServer(DisconnectReason.Flooding, FloodText);
        var args = ClientArgs.Parse(new[] { "--connect=127.0.0.1:7777", "--name=Alice", automation });
        Assert.True(args.Automated);
        ConnectionLoss loss = ConnectionLoss.Of(args, headless: false, client.CloseReason, duringGame: true);
        Assert.True(loss.Quit);
        Assert.Equal(1, loss.ExitCode);
        Assert.Contains(FloodText, loss.LogLine);
        Assert.Contains("exit 1", loss.LogLine);
    }

    [Fact]
    public void AHeadlessRunHasNoScreenToGoBackTo()
    {
        ConnectionLoss loss = ConnectionLoss.Of(ClientArgs.Parse(new[] { "--connect=127.0.0.1:7777" }), headless: true, "the connection timed out", duringGame: true);
        Assert.True(loss.Quit);
        Assert.Equal(1, loss.ExitCode);
    }

    [Fact]
    public void AConnectThatNeverGotInFollowsTheSameRule()
    {
        string reason = new GameClient(new Transport(), "127.0.0.1", 7777, "Alice").CloseReason ?? "could not connect to 127.0.0.1:7777";
        ConnectionLoss interactive = ConnectionLoss.Of(ClientArgs.Parse(new[] { "--connect=127.0.0.1:7777" }), false, reason, duringGame: false);
        Assert.False(interactive.Quit);
        Assert.Equal("Could not connect to 127.0.0.1:7777.", interactive.Notice);
        Assert.StartsWith("[anomaly] connect failed:", interactive.LogLine);
        ConnectionLoss scripted = ConnectionLoss.Of(ClientArgs.Parse(new[] { "--connect=127.0.0.1:7777", "--autowalk", "--quit-after=8" }), false, reason, duringGame: false);
        Assert.True(scripted.Quit);
        Assert.Equal(1, scripted.ExitCode);
    }

    [Fact]
    public void AnEmptyReasonStillSaysSomethingAndAFullStopIsNotDoubled()
    {
        ConnectionLoss none = ConnectionLoss.Of(ClientArgs.Parse(Array.Empty<string>()), false, null, duringGame: true);
        Assert.Equal("The connection ended.", none.Notice);
        ConnectionLoss stopped = ConnectionLoss.Of(ClientArgs.Parse(Array.Empty<string>()), false, "you were kicked.", duringGame: true);
        Assert.Equal("You were kicked.", stopped.Notice);
    }

    [Fact]
    public void TheKeptTokenSurvivesALostConnectionSoOneClickTakesTheCharacterBack()
    {
        // The connect screen's JoinAttempt stores the token when the game starts and keeps it when the connection ends: a new attempt for the same server and name
        // sends it in its Hello (the server's replacement of the old session is R4's and the server tests').
        string path = Path.Combine(Path.GetTempPath(), "anomaly-i1c-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var tokens = new ReconnectTokens(path);
            Transport first = null;
            var attempt = new JoinAttempt(() => first = new Transport(), tokens, "127.0.0.1", 7777, "Alice");
            var map = new MapGrid { Tick = 10, Width = 4, Height = 4, Levels = 1 };
            map.TurfTypes.Add("/turf/open/indestructible");
            map.Runs.Add(new TurfRun(16, 0));
            var snapshot = new Snapshot { Tick = 10, LastInputSeq = 0, OwnEntity = 5 };
            snapshot.Types.Add("/mob");
            snapshot.Entities.Add(new EntityState(5, 0, 2, 2, 1, 2, "Alice", 0f, 10));
            first.Join(map, snapshot);
            Assert.Equal(FeedPhase.Playing, attempt.Poll(0));
            first.End(new TransportDisconnect(TransportDisconnectKind.RemoteClose, Codec.Encode(new DisconnectInfo(DisconnectReason.Flooding, FloodText))));
            Assert.Equal(FeedPhase.Closed, attempt.Poll(1));
            attempt.Dispose();

            var again = new JoinAttempt(() => new Transport(), new ReconnectTokens(path), "127.0.0.1", 7777, "Alice");
            Assert.True(again.UsedKeptToken);
            Assert.Equal(new SessionToken(7, 9), again.Client.Token);
            again.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
