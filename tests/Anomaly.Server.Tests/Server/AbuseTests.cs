using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// Validation and limits (scope 2e, D40), all on the host thread before anything is queued: refused Hellos, strikes (3 in
/// 10 s disconnect), the token buckets of MotionInput and of the actions (per type), the inbound size caps, forged seqs, and
/// the commands only the Input and Control channels may carry. Rejected input never reaches the world: the state hash is
/// the one of a run without it, and the replay log holds none of it. The version-2 commands have their own tests in
/// <c>MotionInputTests</c> and <c>ActionTests</c>; this class is the abuse side.
/// </summary>
public class AbuseTests
{
    [Theory]
    [InlineData("", DisconnectReason.InvalidName)]
    [InlineData("A name that is much too long for tg, over 42 chars", DisconnectReason.InvalidName)]
    [InlineData("Bad\nName", DisconnectReason.InvalidName)]
    [InlineData("Bell\u0007", DisconnectReason.InvalidName)]
    [InlineData("Right‮left", DisconnectReason.InvalidName)]
    [InlineData("Zero​width", DisconnectReason.InvalidName)]
    [InlineData(" padded", DisconnectReason.InvalidName)]
    [InlineData("!!!", DisconnectReason.InvalidName)]
    public void BadNamesAreRefusedBeforeAConnectionExists(string name, DisconnectReason expected)
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect(name);
        server.Tick();
        Assert.Equal(expected, remote.Rejected?.Reason);
        Assert.Null(remote.Connection);
        Assert.Empty(server.Host.Sessions);
    }

    [Fact]
    public void ANameOf42CharactersIsAccepted()
    {
        string name = new string('a', Limits.MaxNameChars);
        Assert.True(ServerHost.ValidateName(name, out _));
        Assert.False(ServerHost.ValidateName(name + "a", out string why));
        Assert.Contains("42", why);
        Assert.True(ServerHost.ValidateName("Jane Doe-Smith", out _));
        Assert.True(ServerHost.ValidateName("Ünïcødé 名前", out _));
    }

    [Fact]
    public void AWrongProtocolVersionIsRefused()
    {
        using var server = TestServer.Start();
        FakeRemote remote = server.Connect("Alice", protocol: ProtocolVersion.Current + 1);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }

    [Fact]
    public void ACommandBeforeHelloIsABadHello()
    {
        using var server = TestServer.Start();
        // A connection request whose data is an InputFrame (a command before any Hello), garbage, nothing, or too long.
        FakeRemote command = server.Transport.Request(FakeCodec.EncodeInput(1, HeldDirs.North));
        FakeRemote garbage = server.Transport.Request(new byte[] { 0xFF, 1, 2, 3 });
        FakeRemote empty = server.Transport.Request(Array.Empty<byte>());
        FakeRemote huge = server.Transport.Request(new byte[Limits.MaxInboundMessageBytes + 1]);
        server.Tick();
        Assert.All(new[] { command, garbage, empty, huge }, r => Assert.Equal(DisconnectReason.BadHello, r.Rejected?.Reason));
        Assert.Empty(server.Host.Sessions);
        Assert.Equal(4, server.Host.RejectedRequests);
    }

    [Fact]
    public void ThreeMalformedMessagesWithinTenSecondsDisconnect()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Send(new byte[] { 0xEE });                         // an unknown id
        alice.Send(new byte[] { (byte)MessageId.InputFrame, 1 }); // a truncated InputFrame
        server.Tick();
        Assert.True(alice.IsConnected);
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
        alice.Send(FakeCodec.EncodeHello("Alice"));              // a second Hello
        server.Tick();
        Assert.Equal(DisconnectReason.ProtocolViolation, alice.Closed?.Reason);
        Assert.Contains("Hello is not a client command", alice.Closed?.Text ?? "");
        server.Tick();
        Assert.Equal(SessionState.Closed, server.Host.SessionOf(1).State);
    }

    [Fact]
    public void StrikesFurtherApartThanTheWindowDoNotDisconnect()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        for (int i = 0; i < 5; i++)
        {
            alice.Send(new byte[] { 0xEE });
            server.Tick(Limits.StrikeWindowTicks / 2 + 1);
        }
        Assert.True(alice.IsConnected);
        Assert.Equal(5, server.Host.SessionOf(1).Strikes);
    }

    [Fact]
    public void AnOversizedMessageIsRefusedUnreadAndIsAStrike()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        var oversized = new byte[Limits.MaxInboundMessageBytes + 1];
        FakeCodec.EncodeMotion(1, 127, 0).CopyTo(oversized, 0);
        alice.Send(oversized, Channel.Input);
        alice.Send(FakeCodec.EncodeMotion(1, 0, 0), Channel.State); // the right message on the wrong channel
        server.Tick();
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind is CommandKind.Motion or CommandKind.Action);
    }

    [Fact]
    public void AMotionInputOverItsOwnCapAndAnActionOverItsAreStrikes()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        // Under the 256 bytes of every inbound message but over the cap of MotionInput (64) and of Action.
        var longMotion = new byte[Limits.MaxMotionInputMessageBytes + 1];
        FakeCodec.EncodeMotion(1, 127, 0).CopyTo(longMotion, 0);
        alice.Send(longMotion, Channel.Input);
        var longAction = new byte[Limits.MaxActionMessageBytes + 1];
        FakeCodec.EncodeAction(0, ActionKind.Rest).CopyTo(longAction, 0);
        alice.Send(longAction);
        server.Tick();
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind is CommandKind.Motion or CommandKind.Action);
    }

    [Fact]
    public void ACommandBeforeTheWelcomeIsAStrike()
    {
        using var server = TestServer.Start();
        server.Clients.LoginSleepTicks = 10;
        FakeRemote slow = server.Connect("Slow");
        server.Tick(); // accepted and joined; client/New sleeping
        slow.Motion(127, 0);
        slow.Action(ActionKind.Rest);
        server.Tick();
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind is CommandKind.Motion or CommandKind.Action);
    }

    [Fact]
    public void TheVersion1InputFrameAndWrongChannelsAreStrikes()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Send(FakeCodec.EncodeInput(1, HeldDirs.North));                     // the version-1 InputFrame on Control
        alice.Send(FakeCodec.EncodeMotion(1, 127, 0), Channel.Control);           // a MotionInput on the reliable channel
        alice.Send(FakeCodec.EncodeAction(0, ActionKind.Rest), Channel.Input);    // an Action on the unreliable one
        server.Tick();
        Assert.Equal(DisconnectReason.ProtocolViolation, alice.Closed?.Reason);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind is CommandKind.Input or CommandKind.Motion or CommandKind.Action);
    }

    [Fact]
    public void AMalformedMotionInputOrActionIsAStrike()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Send(new byte[] { (byte)MessageId.MotionInput }, Channel.Input);                // cut off after its id
        alice.Send(new byte[] { (byte)MessageId.MotionInput, 9, 0, 0, 0, 0 }, Channel.Input); // a frame count over the cap
        server.Tick();
        Assert.Equal(2, server.Host.SessionOf(1).Strikes);
        alice.Send(new byte[] { (byte)MessageId.Action, 1, 2, 3 });                           // a truncated Action
        server.Tick();
        Assert.Equal(DisconnectReason.ProtocolViolation, alice.Closed?.Reason);
    }

    [Fact]
    public void AForgedSeqIsAStrikeAndKeepsNothingOfTheMessage()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick();
        Session s = server.Host.SessionOf(1);
        Assert.Equal(1u, s.Jitter.NewestSeq);
        // Seq 1 + MaxInputSeqLead + 1 is more than a minute of frames ahead of the last taken one.
        alice.Send(FakeCodec.EncodeMotion((uint)(s.Jitter.LastTakenSeq + Limits.MaxInputSeqLead + 1), 127, 0), Channel.Input);
        server.Tick();
        Assert.Equal(1, s.Strikes);
        Assert.Equal(s.Jitter.LastTakenSeq, s.Jitter.NewestSeq); // nothing of it kept: the newest is the seq the repeat stood in for (D331)
        // An action tagged with a forged frame seq is a strike too.
        alice.Action(ActionKind.Rest, frameSeq: (uint)(s.Jitter.LastTakenSeq + Limits.MaxInputSeqLead + 1));
        server.Tick();
        Assert.Equal(2, s.Strikes);
        Assert.Equal(0, s.PendingActionCount);
    }

    [Fact]
    public void AThousandMotionMessagesPerSecondAreCutByTheBucketAndDisconnectAsFlooding()
    {
        using var server = TestServer.Start();
        FakeRemote flooder = server.Join("Flooder");
        FakeRemote honest = server.Join("Honest");
        uint seq = 0;
        for (int t = 0; t < 20; t++)
        {
            for (int i = 0; i < 50; i++) // 1000 per second at 20 ticks per second
                flooder.Send(FakeCodec.EncodeMotion(++seq, (sbyte)(i % 2 == 0 ? 100 : -100), 0), Channel.Input);
            honest.Motion((sbyte)(t % 2 == 0 ? 100 : 0), 0);
            server.Tick();
            if (t == 0) Assert.Equal(35, server.Host.SessionOf(1).RateLimited); // the bucket: 15 of the first 50 pass
        }
        Session s = server.Host.SessionOf(1);
        Assert.False(flooder.IsConnected);
        Assert.Equal(DisconnectReason.Flooding, flooder.Closed?.Reason);
        Assert.Equal(0, s.Strikes);
        Assert.True(s.RateLimited >= ServerHost.FloodDropLimit);
        Assert.True(honest.IsConnected);
        Assert.Equal(0, server.Host.SessionOf(2).RateLimited);
    }

    [Fact]
    public void ActionsOverTheirBucketsAreDroppedPerKindAndAFloodDisconnects()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        // Ten rests in one poll: the toggle bucket (4, burst 4) lets four through; the other six are dropped, no strikes.
        for (int i = 0; i < 10; i++) alice.Action(ActionKind.Rest);
        server.Tick(2);
        Session s = server.Host.SessionOf(1);
        Assert.Equal(4, server.Clients.Actions.Count(a => a.Action == "rest"));
        Assert.Equal(6, s.RateLimited);
        Assert.Equal(0, s.Strikes);
        // The kinds have their own buckets: a click is not charged to the toggles' (the overall bucket of 10 had 6 tokens left).
        long target = server.MobOf("Alice").dm_id;
        alice.Action(ActionKind.Click, target, ClickButton.Left);
        server.Tick(2);
        Assert.Single(server.Clients.Clicks);
        // A script that keeps going is cut off.
        for (int t = 0; t < 40 && alice.IsConnected; t++)
        {
            for (int i = 0; i < 30; i++) alice.Action(ActionKind.ToggleCombatMode);
            server.Tick();
        }
        Assert.Equal(DisconnectReason.Flooding, alice.Closed?.Reason);
        Assert.Equal(0, s.Strikes);
    }

    [Fact]
    public void RejectedInputNeverChangesTheWorld()
    {
        // Run A: two players, one walks; a third client is refused in every way a Hello can be, and the second player sends
        // two malformed messages (two strikes: still connected). Run B: the same players and the same valid inputs only.
        ulong a = Run(abuse: true, out CommandLog logA);
        ulong b = Run(abuse: false, out CommandLog logB);
        Assert.Equal(b, a);
        Assert.Equal(logB.Commands, logA.Commands);

        static ulong Run(bool abuse, out CommandLog log)
        {
            using var server = TestServer.Start();
            FakeRemote walker = server.Join("Walker");
            FakeRemote other = server.Join("Other");
            for (int t = 0; t < 30; t++)
            {
                if (t % 10 == 0) walker.Motion(t % 20 == 0 ? (sbyte)100 : (sbyte)0, t % 20 == 0 ? (sbyte)0 : (sbyte)100);
                if (abuse && t == 3)
                {
                    server.Connect("Walker");                                     // name in use
                    server.Connect("Mallory", protocol: 999);                     // wrong version
                    server.Connect("Bad\u0001Name");                              // control character
                    server.Transport.Request(FakeCodec.EncodeMotion(1, 127, 0));  // a command before Hello
                    other.Send(new byte[] { 0x42 });                             // unknown id
                    other.Send(new byte[Limits.MaxInboundMessageBytes + 10]);    // oversized
                }
                server.Tick();
            }
            Assert.True(other.IsConnected);
            log = server.Host.Log;
            return server.Hash();
        }
    }
}
