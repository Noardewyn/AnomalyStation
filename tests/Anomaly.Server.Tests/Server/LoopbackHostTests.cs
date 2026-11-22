using System.IO;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// The host over the real codec, the real replicator, the real locomotion seam and the loopback transport: Hello in the
/// connection request, the join sequence and the Deltas decoded by the protocol's own decoder and applied to a
/// <see cref="ReplicaWorld"/>, MotionInput frames that move the capsule and come back in the private block, refusals and kicks
/// with their DisconnectInfo, strikes, a client that vanishes (the transport's timeout), and record then replay of a loopback
/// session. The client layer is the real one (tg's client/New and /mob/Login on the human avatar).
/// </summary>
public partial class LoopbackHostTests
{
    private sealed class Player
    {
        public readonly LoopbackClientTransport Transport;
        public readonly ReplicaWorld Replica = new();
        public readonly List<ServerText> Texts = new();
        public readonly List<long> DeltaTicks = new();
        public Welcome? Welcome;
        public DisconnectInfo? Gone;
        public TransportDisconnectKind? GoneKind;
        public uint LastInputSeq;
        public OwnBlock LastOwn;
        private readonly List<MotionInput> _frames = new();
        private uint _frameBase; // D331: the seq before the first of _frames

        public Player(LoopbackNetwork network, int port, string name, SessionToken token = default, ushort protocol = ProtocolVersion.Current, string login = null)
        {
            Transport = network.CreateClient();
            Transport.Received += (channel, message) =>
            {
                Assert.True(Codec.TryPeekId(message, out MessageId id));
                switch (id)
                {
                    case MessageId.Welcome:
                        Assert.Equal(Channel.State, channel);
                        Assert.True(Codec.TryDecode(message, out Welcome w, out _));
                        Welcome = w;
                        break;
                    case MessageId.ServerText:
                        Assert.Equal(Channel.Control, channel);
                        Assert.True(Codec.TryDecode(message, out ServerText t, out _));
                        Texts.Add(t);
                        break;
                    case MessageId.SayModal: // version 38: the say window's props, 3 s after the login
                        Assert.Equal(Channel.Control, channel);
                        Assert.True(Codec.TryDecode(message, out SayModalMessage _, out _));
                        break;
                    case MessageId.Runechat:
                        Assert.Equal(Channel.Control, channel);
                        Assert.True(Codec.TryDecode(message, out RunechatMessage _, out _));
                        break;
                    case MessageId.Motion: // version 42: the capsule mobs' positions, unreliable
                        Assert.Equal(Channel.Input, channel);
                        Assert.True(Replica.TryApply(message, out _, out ProtocolError motionError), motionError.ToString());
                        break;
                    default:
                        Assert.Equal(Channel.State, channel);
                        Assert.True(Replica.TryApply(message, out MessageId applied, out ProtocolError error), error.ToString());
                        if (applied == MessageId.Delta)
                        {
                            Assert.True(Codec.TryDecode(message, out Delta d, out _));
                            DeltaTicks.Add(d.Tick);
                            LastInputSeq = d.LastInputSeq;
                            LastOwn = d.Own;
                        }
                        break;
                }
            };
            Transport.Disconnected += info =>
            {
                GoneKind = info.Kind;
                if (info.Data is { Length: > 0 } && Codec.TryDecode(info.Data, out DisconnectInfo d, out _)) Gone = d;
            };
            Transport.Connect("127.0.0.1", port, Codec.Encode(new Hello(protocol, name, token, login)));
        }

        /// <summary>A frame of the next seq on the Input channel, in a message that repeats the last four frames (scope 2d).</summary>
        public MotionInput Motion(sbyte moveX, sbyte moveY, ushort yaw = 0)
        {
            // D331: past the seqs the server's repeats stood in for, as the client's prediction (the redundant frames stay consecutive)
            uint next = (uint)_frames.Count + 1;
            if (LastInputSeq >= _frameBase + next)
            {
                _frames.Clear();
                _frameBase = LastInputSeq;
            }
            var frame = new MotionInput(_frameBase + (uint)_frames.Count + 1, moveX, moveY, yaw, MotionInputFlags.None);
            _frames.Add(frame);
            var message = new MotionInputMessage();
            for (int i = Math.Max(0, _frames.Count - Limits.MaxInputFramesPerMessage); i < _frames.Count; i++) message.Frames.Add(_frames[i]);
            Transport.Send(Channel.Input, Codec.Encode(message));
            return frame;
        }

        public void Action(ActionKind kind) => Transport.Send(Channel.Control, Codec.Encode(new ActionMessage((uint)_frames.Count, kind, 0, ClickButton.None, ClickModifiers.None, null)));
    }

    private sealed class Rig : IDisposable
    {
        public readonly LoopbackNetwork Network = new(new LoopbackOptions { Seed = 5, TimeoutSteps = 40 });
        public readonly LoopbackServerTransport Server;
        public readonly StringWriter Out = new();
        public readonly StringWriter Err = new();
        public readonly ServerHost Host;
        public readonly List<Player> Players = new();

        public Rig(TextWriter record = null, bool devCommands = false, ServerConfig config = null)
        {
            Server = Network.CreateServer();
            Host = new ServerHost(config ?? TestServer.DefaultConfig(devCommands: devCommands), Server, new Replicator(), ProtocolCodec.Instance, null, Out, Err,
                recordTo: record, strictNotTranslated: false);
            Host.Start();
            while (!Host.JoinsOpen) Tick();
        }

        public void Tick(int n = 1)
        {
            for (int i = 0; i < n && !Host.Finished; i++)
            {
                Host.RunTick();
                Network.Advance();
                foreach (Player p in Players) p.Transport.Poll();
            }
        }

        public Player Connect(string name, SessionToken token = default, ushort protocol = ProtocolVersion.Current, string login = null)
        {
            var p = new Player(Network, Server.Port, name, token, protocol, login);
            Players.Add(p);
            return p;
        }

        public Player Join(string name, SessionToken token = default, string login = null)
        {
            Player p = Connect(name, token, login: login);
            for (int i = 0; i < 20 && p.Welcome == null; i++) Tick();
            Assert.True(p.Welcome != null, name + " got no Welcome: " + p.Gone);
            return p;
        }

        public void Dispose() => Host.Dispose();
    }

    [Fact]
    public void CeilingEditsReachTwoClientsAndALateJoinerWithoutChangingTheFloor()
    {
        using var rig = new Rig(devCommands: true);
        Player alice = rig.Join("Alice"), bob = rig.Join("Bob");
        string floor = alice.Replica.TurfTypeAt(6, 7, 1);
        alice.Transport.Send(Channel.Control, Codec.Encode(new ActionMessage(0, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, "ceiling /turf/open/floor/glass 6 7 1")));
        rig.Tick(5);
        Assert.True(alice.Replica.CeilingAt(6, 7, 1).IsGlass);
        Assert.Equal(alice.Replica.CeilingAt(6, 7, 1), bob.Replica.CeilingAt(6, 7, 1));
        Player carol = rig.Join("Carol");
        Assert.Equal(alice.Replica.CeilingAt(6, 7, 1), carol.Replica.CeilingAt(6, 7, 1));
        Assert.Equal(floor, carol.Replica.TurfTypeAt(6, 7, 1));
        alice.Transport.Send(Channel.Control, Codec.Encode(new ActionMessage(0, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, "ceiling none 6 7 1")));
        rig.Tick(5);
        foreach (Player p in rig.Players) Assert.False(p.Replica.CeilingAt(6, 7, 1).Exists);
        Assert.Equal(0, rig.Host.World.RuntimeErrorCount);
    }

    [Fact]
    public void TheJoinSequenceAndTheDeltasDecodeAndApply()
    {
        using var rig = new Rig();
        Player alice = rig.Join("Alice");
        Welcome w = alice.Welcome.Value;
        Assert.Equal(1, w.Session);
        Assert.Equal(rig.Host.AvatarOf("Alice"), w.OwnEntity);
        Assert.Equal(TestServer.RoomSize, alice.Replica.Map.Width);
        Assert.Equal(w.Tick, alice.Replica.Tick);

        // The join Snapshot carries the private block: the own capsule at its tile's centre.
        Assert.True(alice.Replica.Own.HasMotion);
        int startX = alice.Replica.Own.Motion.X;

        alice.Motion(MotionInput.MaxMove, 0);
        rig.Tick(8);
        MotionInput stop = alice.Motion(0, 0); // after the seqs the repeats stood in for (D331)
        rig.Tick(6);
        Assert.Equal(Enumerable.Range(1, alice.DeltaTicks.Count).Select(i => w.Tick + i), alice.DeltaTicks);
        Assert.True(stop.Seq > 2u);
        Assert.True(alice.LastInputSeq >= stop.Seq); // the silent ticks after it stand in for the next seqs
        Assert.Equal(new[] { (CommandKind.Motion, 1u), (CommandKind.Motion, stop.Seq) },
            rig.Host.Log.Commands.Where(c => c.Kind == CommandKind.Motion).Select(c => (c.Kind, c.Seq)));
        // The capsule ran east (sideways to its facing: 63 % of 1/3 tile a tick, D314) for the 8 ticks the frame held (the seam repeats a mob's last frame).
        Assert.True(alice.Replica.Own.Motion.X > startX + 128, $"the capsule did not run east: {startX} -> {alice.Replica.Own.Motion.X}; err {rig.Err}");
        Assert.Equal(alice.LastInputSeq, alice.Replica.Own.AckedSeq);
        Assert.DoesNotContain("ProtocolException", rig.Err.ToString());
    }

    [Fact]
    public void RefusalsCarryTheirReasonInTheRejectData()
    {
        using var rig = new Rig();
        Player owner = rig.Join("Alice");
        Player wrongVersion = rig.Connect("Bob", protocol: ProtocolVersion.Current + 1);
        Player badName = rig.Connect("Bad\tName");
        Player taken = rig.Connect("ALICE");
        Player wrongToken = rig.Connect("Alice", new SessionToken(1, 2));
        rig.Tick(4);
        Assert.Equal(DisconnectReason.WrongVersion, wrongVersion.Gone?.Reason);
        Assert.Equal(DisconnectReason.InvalidName, badName.Gone?.Reason);
        Assert.Equal(DisconnectReason.NameInUse, taken.Gone?.Reason);
        Assert.Equal(DisconnectReason.BadToken, wrongToken.Gone?.Reason);
        Assert.All(new[] { wrongVersion, badName, taken, wrongToken }, p => Assert.Equal(TransportDisconnectKind.Rejected, p.GoneKind));
        Assert.Null(owner.Gone);
        Assert.Single(rig.Host.Sessions);
    }

    [Fact]
    public void GarbageStrikesAndTheThirdDisconnects()
    {
        using var rig = new Rig();
        Player alice = rig.Join("Alice");
        alice.Transport.Send(Channel.Control, new byte[] { 0xEE });
        alice.Transport.Send(Channel.Control, new byte[] { (byte)MessageId.InputFrame, 1, 2 }); // the version-1 InputFrame: no longer a command
        alice.Transport.Send(Channel.Control, Codec.Encode(new Hello(ProtocolVersion.Current, "Alice", SessionToken.None)));
        rig.Tick(4);
        Assert.Equal(DisconnectReason.ProtocolViolation, alice.Gone?.Reason);
        Assert.Equal(SessionState.Closed, rig.Host.SessionOf(1).State);
        Assert.DoesNotContain(rig.Host.Log.Commands, c => c.Kind is CommandKind.Input or CommandKind.Motion);
    }

    [Fact]
    public void AVanishedClientTimesOutAndLeaves()
    {
        using var rig = new Rig();
        Player alice = rig.Join("Alice");
        alice.Transport.Drop(); // no goodbye: the server learns it from the transport's timeout
        rig.Tick(rig.Network.Options.TimeoutSteps + 5);
        Session s = rig.Host.SessionOf(1);
        Assert.Equal(SessionState.Closed, s.State);
        Assert.Contains("(Timeout)", rig.Out.ToString());
        Assert.Contains("left #1 Alice", rig.Out.ToString());
        Player again = rig.Join("Alice", alice.Welcome.Value.Token);
        Assert.Equal(alice.Welcome.Value.OwnEntity, again.Welcome.Value.OwnEntity);
    }

    [Fact]
    public void AKickArrivesAsKickedWithTheReason()
    {
        using var rig = new Rig();
        Player alice = rig.Join("Alice");
        rig.Host.EnqueueConsoleLine("kick Alice spam");
        rig.Tick(3);
        Assert.Equal(DisconnectReason.Kicked, alice.Gone?.Reason);
        Assert.Contains("spam", alice.Gone?.Text);
    }

    [Fact]
    public void ALoopbackSessionRecordedAndReplayedGivesTheSameHash()
    {
        var file = new StringWriter();
        ulong live;
        long end;
        using (var rig = new Rig(file))
        {
            Player a = rig.Join("Alice");
            Player b = rig.Join("Bob");
            for (int t = 0; t < 60; t++)
            {
                if (t % 4 == 0) a.Motion((sbyte)(t / 4 % 2 == 0 ? 100 : -100), (sbyte)(t / 4 % 3 == 0 ? 60 : 0), (ushort)(t * 700));
                if (t % 9 == 0) b.Motion(t % 18 == 0 ? (sbyte)-127 : (sbyte)0, 0);
                if (t == 20) a.Action(ActionKind.Rest);
                if (t == 30) b.Transport.Disconnect();
                rig.Tick();
            }
            rig.Host.RequestStop();
            rig.Tick(2);
            Assert.True(rig.Host.Finished);
            Assert.Equal(DisconnectReason.ServerShutdown, a.Gone?.Reason);
            live = rig.Host.FinalHash;
            end = rig.Host.Tick;
        }
        CommandLog log = CommandLog.Parse(file.ToString(), out string error);
        Assert.Null(error);
        // The replay runs the same host over the same real locomotion seam, from the log alone.
        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: false);
        replay.Start();
        while (!replay.Finished) replay.RunTick();
        Assert.Equal(end, replay.Tick);
        Assert.Equal(live, replay.FinalHash);
    }
}
