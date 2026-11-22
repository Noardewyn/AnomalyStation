using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Protocol;

namespace Anomaly.Server.Tests.Net;

/// <summary>
/// The whole stack of this package without sockets: a scripted server table, the codec's join sequence and per-tick Deltas
/// (the body encoded once, a header per connection) sent through the loopback's State channel under latency and loss, and
/// client replicas that must end up equal to the table.
/// </summary>
public class LoopbackReplicationTests
{
    private sealed class Client
    {
        public readonly LoopbackClientTransport Transport;
        public readonly ReplicaWorld Replica = new();
        public Welcome Welcome;

        public Client(LoopbackNetwork network, int port)
        {
            Transport = network.CreateClient();
            Transport.Received += (channel, message) =>
            {
                if (Codec.TryPeekId(message, out MessageId id) && id == MessageId.Welcome)
                {
                    Assert.True(Codec.TryDecode(message, out Welcome, out _));
                    return;
                }

                Assert.Equal(Channel.State, channel);
                Assert.True(Replica.TryApply(message, out _, out ProtocolError error), error.ToString());
            };
            Transport.Connect("127.0.0.1", port, Codec.Encode(new Hello(ProtocolVersion.Current, "Bot" + port, SessionToken.None)));
        }
    }

    [Theory]
    [InlineData(0, 0, 0.0, 1)]
    [InlineData(0, 4, 0.3, 2)]
    [InlineData(2, 6, 0.5, 3)]
    public void LateJoinersAndEarlyOnesEndUpEqualToTheServerTable(int minLatency, int maxLatency, double loss, int seed)
    {
        var network = new LoopbackNetwork(new LoopbackOptions { Seed = (ulong)seed, MinLatencySteps = minLatency, MaxLatencySteps = maxLatency, LossRate = loss, ReorderRate = 0.4 });
        LoopbackServerTransport server = network.CreateServer();
        var script = new ScriptedServer(900 + seed);
        script.Populate(40);
        var connections = new List<IConnection>();
        server.ConnectionRequested += request => request.Accept();
        server.Connected += connection =>
        {
            // The join sequence: Welcome, MapGrid, Snapshot, in this order on the State channel.
            connections.Add(connection);
            connection.Send(Channel.State, Codec.Encode(new Welcome(connection.Id, new SessionToken((ulong)connection.Id, 1), script.Tick, 20, script.OwnEntity)));
            connection.Send(Channel.State, Codec.Encode(script.Map()));
            connection.Send(Channel.State, Codec.Encode(script.Snapshot()));
        };
        server.Start("127.0.0.1", 0);

        var clients = new List<Client>();
        void Tick()
        {
            server.Poll();
            Delta delta = script.Step();
            byte[] body = Codec.EncodeDeltaBody(delta);
            var buffer = new byte[Codec.MaxDeltaHeaderBytes + body.Length];
            foreach (IConnection connection in connections.ToArray())
            {
                // A connection that joined in this tick's poll got a Snapshot of the previous tick, so this Delta is the
                // next one for it, exactly as the contract says (a Snapshot of T is followed by the Delta of T + 1).
                int length = Codec.EncodeDelta(delta.Tick, delta.LastInputSeq, connection.Id, body, buffer);
                connection.Send(Channel.State, buffer.AsSpan(0, length));
            }

            foreach (Client c in clients)
                c.Transport.Poll();
            network.Advance();
        }

        clients.Add(new Client(network, server.Port));
        for (int i = 0; i < 300; i++)
        {
            Tick();
            if (i == 100)
                clients.Add(new Client(network, server.Port));
            if (i == 200)
                clients.Add(new Client(network, server.Port));
        }

        // Stop the script and let the network drain, then every replica must equal the table.
        for (int i = 0; i < 100; i++)
        {
            server.Poll();
            foreach (Client c in clients)
                c.Transport.Poll();
            network.Advance();
        }

        Assert.Equal(3, clients.Count);
        foreach (Client c in clients)
        {
            Assert.True(c.Replica.HasSnapshot);
            Assert.False(c.Replica.IsDesynced);
            Assert.Equal(script.Tick, c.Replica.Tick);
            Assert.Equal(script.Table.Count, c.Replica.Count);
            using IEnumerator<ReplicaEntity> e = c.Replica.Entities.GetEnumerator();
            foreach (EntityState state in script.Table.Values)
            {
                Assert.True(e.MoveNext());
                Assert.Equal(state, e.Current.State);
            }
        }
    }
}
