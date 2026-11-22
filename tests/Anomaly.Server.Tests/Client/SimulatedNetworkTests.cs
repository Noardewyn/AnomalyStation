using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: <c>--sim-latency</c> and <c>--sim-loss</c>, a decorator over a transport.</summary>
public class SimulatedNetworkTests
{
    private sealed class Pair : IDisposable
    {
        public double Now;
        public readonly LoopbackNetwork Network = new(new LoopbackOptions { Seed = 3 });
        public readonly LoopbackServerTransport Server;
        public readonly SimulatedNetworkTransport Client;
        public readonly List<(double At, Channel Channel, string Text)> AtServer = new();
        public readonly List<(double At, Channel Channel, string Text)> AtClient = new();
        private IConnection _connection;

        public Pair(double roundTripMs, double loss = 0)
        {
            Server = Network.CreateServer();
            Server.ConnectionRequested += r => _connection = r.Accept();
            Server.Received += (c, ch, m) => AtServer.Add((Now, ch, System.Text.Encoding.ASCII.GetString(m)));
            Server.Start("127.0.0.1", 0);
            Client = new SimulatedNetworkTransport(Network.CreateClient(), () => Now, roundTripMs, loss, seed: 4);
            Client.Received += (ch, m) => AtClient.Add((Now, ch, System.Text.Encoding.ASCII.GetString(m)));
            Client.Connect("127.0.0.1", Server.Port, Codec.Encode(new Hello(ProtocolVersion.Current, "T", SessionToken.None)));
            for (int i = 0; i < 5; i++)
                Step();
        }

        // 10 ms steps; the network moves a step every 50 ms of fake time.
        public void Step(int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                Now += 0.01;
                if (Math.Round(Now * 100) % 5 == 0)
                    Network.Advance();
                Server.Poll();
                Client.Poll();
            }
        }

        public void ToClient(Channel channel, string text) => _connection.Send(channel, System.Text.Encoding.ASCII.GetBytes(text));

        public void Dispose() => Client.Dispose();
    }

    [Fact]
    public void WithNoSimulationEverythingPassesStraightThrough()
    {
        using var pair = new Pair(0);
        pair.Client.Send(Channel.Control, System.Text.Encoding.ASCII.GetBytes("a"));
        pair.Step(10);
        Assert.Single(pair.AtServer);
        pair.ToClient(Channel.State, "b");
        pair.Step(10);
        Assert.Single(pair.AtClient);
    }

    [Fact]
    public void EachDirectionIsHeldBackByHalfTheRoundTrip()
    {
        using var pair = new Pair(200);
        double sentAt = pair.Now;
        pair.Client.Send(Channel.Control, System.Text.Encoding.ASCII.GetBytes("up"));
        pair.Step(60);
        Assert.Single(pair.AtServer);
        double up = pair.AtServer[0].At - sentAt;
        Assert.InRange(up, 0.10, 0.16); // 100 ms plus the network's own step
        double downFrom = pair.Now;
        pair.ToClient(Channel.State, "down");
        pair.Step(60);
        Assert.Single(pair.AtClient);
        Assert.InRange(pair.AtClient[0].At - downFrom, 0.10, 0.16);
        Assert.InRange(pair.Client.RoundTripMs, 200, 400);
    }

    [Fact]
    public void MessagesKeepTheirOrder()
    {
        using var pair = new Pair(100);
        for (int i = 0; i < 20; i++)
        {
            pair.Client.Send(Channel.Control, System.Text.Encoding.ASCII.GetBytes("m" + i));
            pair.Step(2);
        }
        pair.Step(50);
        Assert.Equal(Enumerable.Range(0, 20).Select(i => "m" + i), pair.AtServer.Select(x => x.Text));
    }

    [Fact]
    public void OnlyTheInputChannelLosesMessages()
    {
        using var pair = new Pair(0, loss: 0.3);
        for (int i = 0; i < 400; i++)
        {
            pair.Client.Send(Channel.Input, System.Text.Encoding.ASCII.GetBytes("i"));
            pair.Client.Send(Channel.Control, System.Text.Encoding.ASCII.GetBytes("c"));
            pair.Step(1);
        }
        pair.Step(20);
        int input = pair.AtServer.Count(x => x.Channel == Channel.Input);
        int control = pair.AtServer.Count(x => x.Channel == Channel.Control);
        Assert.Equal(400, control);
        Assert.InRange(input, 230, 330);
        Assert.Equal(400 - input, pair.Client.DroppedInput);
    }

    [Fact]
    public void ABadSettingIsRefused()
    {
        var net = new LoopbackNetwork();
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedNetworkTransport(net.CreateClient(), () => 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedNetworkTransport(net.CreateClient(), () => 0, 10, 1.5));
    }
}
