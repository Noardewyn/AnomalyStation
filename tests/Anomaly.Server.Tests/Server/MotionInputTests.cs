using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// The input path of M2 (scope 2b, 2d; ServerHost.FormInputs, ApplyMotion): the avatar is tg's human registered as a capsule
/// mob, a MotionInput frame goes through the session's jitter buffer, one frame per tick becomes a recorded Motion command
/// for the locomotion seam, an unchanged repeat is no command, silence goes idle, and the private block gets the capsule's
/// state and the buffer's depth. The locomotion seam is the stand-in of <see cref="FakeLocomotion"/> here; the real seam
/// runs in LoopbackHostTests and the integration tests.
/// </summary>
public class MotionInputTests
{
    [Fact]
    public void TheHostInstallsTheLocomotionPhaseAndSetsTheSpeedScaleAtBoot()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, MoveSpeedScale = 0.5f, StatusEveryTicks = 1000 };
        using var server = TestServer.Start(config);
        Assert.True(server.Locomotion.Installed);
        Assert.Equal(0.5f, server.Locomotion.SpeedScale);
        // An empty world has no tg world to move capsules in.
        var empty = new FakeLocomotion();
        using var bare = new ServerHost(new ServerConfig { Empty = true, Ticks = 3, Console = false }, locomotion: empty);
        bare.Start();
        Assert.False(empty.Installed);
    }

    [Fact]
    public void TheTicksSSmobsFiredInGoToTheirOwnHistogram()
    {
        using var server = TestServer.Start();
        datum_controller_subsystem_mobs mobs;
        using (server.World.Enter())
            mobs = Anomaly.Core.DmShim.Dm.each<datum_controller_subsystem_mobs>(Anomaly.Core.Tg.GlobalProcs.Master.subsystems).Single();
        // From just after one of SSmobs' own fires (its 2 s wait follows; the Master's stagger moves the first fire as subsystems come and go):
        // the host only reads times_fired after each step, so a bump stands for a fire.
        for (int i = 0; i < 60 && server.Host.Metrics.RunMobsStep.Count == 0; i++)
            server.Tick();
        long counted = server.Host.Metrics.RunMobsStep.Count;
        Assert.Equal(1, counted);
        server.Tick(3);
        Assert.Equal(counted, server.Host.Metrics.RunMobsStep.Count);
        mobs.times_fired += 1;
        server.Tick();
        Assert.Equal(counted + 1, server.Host.Metrics.RunMobsStep.Count);
        server.Tick(5);
        Assert.Equal(counted + 1, server.Host.Metrics.RunMobsStep.Count);
        mobs.times_fired += 1;
        server.Tick();
        Assert.Equal(counted + 2, server.Host.Metrics.RunMobsStep.Count);
    }

    [Fact]
    public void TheAvatarIsAHumanRegisteredAfterItsClientAttached()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        mob avatar = server.MobOf("Alice");
        Assert.IsType<mob_living_carbon_human>(avatar);
        Assert.True(server.Locomotion.IsRegistered(avatar));
        Assert.Single(server.Locomotion.Registrations);
        Assert.Same(avatar, avatar.client.FakeMobOf());
        // A reconnect finds the mob that kept its key: still registered, not registered twice.
        SessionToken token = alice.Welcome.Value.Token;
        alice.Drop();
        server.Tick(2);
        server.Join("Alice", token);
        Assert.Single(server.Locomotion.Registrations);
        Assert.Same(avatar, server.MobOf("Alice"));
    }

    [Fact]
    public void AFrameBecomesTheSeamsFrameInTheTickAfterItArrived()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        mob avatar = server.MobOf("Alice");
        server.Locomotion.Frames.Clear();
        long sent = server.Host.Tick;
        MotionInput frame = alice.Motion(100, -20, 12345);
        server.Tick();
        var applied = Assert.Single(server.Locomotion.Frames);
        Assert.Equal((sent + 1, avatar.dm_id, frame), (applied.Tick, applied.Mob, applied.Frame));
        Assert.Equal(1u, server.Host.SessionOf(1).LastAppliedInputSeq);
        var command = Assert.Single(server.Host.Log.Commands, c => c.Kind == CommandKind.Motion);
        Assert.Equal((sent + 1, 1, 1u, frame), (command.ApplyTick, command.Session, command.Seq, command.Motion));
        // usr was the player's mob in the command (the seam sets it for its own step).
        Assert.Empty(server.Err.ToString());
    }

    [Fact]
    public void OneFrameIsTakenPerTickInSeqOrderAndTheRedundancyIsIgnored()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Locomotion.Frames.Clear();
        var history = new List<MotionInput>();
        // Three frames sent in one poll, each message repeating the frames before it (up to four): 1, 1-2, 1-3.
        for (int i = 1; i <= 3; i++)
        {
            history.Add(new MotionInput((uint)i, (sbyte)(i * 10), 0, 0, MotionInputFlags.None));
            alice.Send(FakeCodec.EncodeMotion(history.ToArray()), Channel.Input);
        }
        server.Tick();
        Assert.Equal(new[] { 1u }, server.Locomotion.Frames.Select(f => f.Frame.Seq));
        Assert.Equal(2, server.Host.SessionOf(1).Jitter.Depth);
        server.Tick(2);
        Assert.Equal(new[] { 1u, 2u, 3u }, server.Locomotion.Frames.Select(f => f.Frame.Seq));
        Assert.Equal(new[] { 0L, 1L, 2L }, server.Locomotion.Frames.Select(f => f.Tick - server.Locomotion.Frames[0].Tick));
        Assert.Equal(3u, server.Host.SessionOf(1).LastAppliedInputSeq);
    }

    [Fact]
    public void AnUnchangedRepeatIsNoCommandButItsSeqIsAcknowledged()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Locomotion.Frames.Clear();
        alice.Motion(100, 0, 1000);
        server.Tick();
        // The same wish and yaw in new frames: the seam keeps its last frame, so no command, but lastInputSeq follows.
        alice.Motion(100, 0, 1000);
        server.Tick();
        alice.Motion(100, 0, 1000);
        server.Tick();
        server.Tick(3); // nothing new arrives: the last frame repeats, standing in for seqs 4 to 6 (D331)
        Assert.Single(server.Locomotion.Frames);
        Assert.Single(server.Host.Log.Commands, c => c.Kind == CommandKind.Motion);
        Session s = server.Host.SessionOf(1);
        Assert.Equal(6u, s.LastAppliedInputSeq);
        Assert.Equal((3L, 3L), (s.Jitter.Repeats, s.Jitter.StandIns));
        // seq 4 arriving late was stood in for: ignored
        alice.Motion(-100, 0, 2000);
        server.Tick();
        Assert.Single(server.Locomotion.Frames);
        // A changed wish, or a turned head, is a new command.
        alice.SkipTo(s.LastAppliedInputSeq);
        alice.Motion(100, 0, 2000);
        server.Tick();
        alice.Motion(-100, 0, 2000);
        server.Tick();
        Assert.Equal(3, server.Locomotion.Frames.Count);
        Assert.Equal(new uint[] { 1, 8, 9 }, server.Locomotion.Frames.Select(f => f.Frame.Seq));
    }

    [Fact]
    public void AClientThatWentSilentStopsAfterHalfASecond()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Locomotion.Frames.Clear();
        alice.Motion(127, 0, 4000);
        server.Tick();
        server.Tick(ActionLimits.MaxRepeatedFrameTicks);
        Assert.Single(server.Locomotion.Frames); // still repeating, still running
        server.Tick();
        Assert.Equal(2, server.Locomotion.Frames.Count);
        MotionInput idle = server.Locomotion.Frames[1].Frame;
        // the repeats stood in for the next seqs until then (D331); the idle frame keeps the last one: no wish, the yaw kept
        uint last = 1u + (uint)ActionLimits.MaxRepeatedFrameTicks;
        Assert.Equal((last, (sbyte)0, (sbyte)0, (ushort)4000), (idle.Seq, idle.MoveX, idle.MoveY, idle.Yaw));
        server.Tick(10);
        Assert.Equal(2, server.Locomotion.Frames.Count); // idle once, not every tick
        Assert.Equal(last, server.Host.SessionOf(1).LastAppliedInputSeq);
        // A new frame starts it again.
        alice.SkipTo(last);
        alice.Motion(127, 0, 4000);
        server.Tick();
        Assert.Equal(3, server.Locomotion.Frames.Count);
    }

    [Fact]
    public void MoreThanFourFramesQueuedDropTheOldest()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Locomotion.Frames.Clear();
        for (int i = 0; i < Limits.MaxJitterBufferFrames + 2; i++) alice.Motion((sbyte)(10 + i), 0);
        server.Tick();
        Session s = server.Host.SessionOf(1);
        Assert.Equal(2, s.Jitter.Dropped); // two above the cap (Limits.MaxJitterBufferFrames, 8 since D331)
        Assert.Equal(new[] { 3u }, server.Locomotion.Frames.Select(f => f.Frame.Seq)); // 1 and 2 were dropped
        Assert.Equal(3u, s.LastAppliedInputSeq);
    }

    [Fact]
    public void ThePrivateBlockGetsTheCapsuleStateTheBufferDepthAndTheAckedSeq()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        mob avatar = server.MobOf("Alice");
        var state = MotionState.AtTile(5, 6, 1, 777);
        var parameters = new MotionParams(0.25f, 0.175f, true, 3);
        server.Locomotion.States[avatar.dm_id] = (state, parameters);
        for (int i = 0; i < 3; i++) alice.Motion(50, 0);
        server.Replicator.Privates.Clear();
        server.Tick();
        var (tick, inputs) = server.Replicator.Privates.Single(p => p.Inputs.Session == 1);
        Assert.Equal(server.Host.Tick, tick);
        Assert.Equal((1, avatar.dm_id, 1u, true), (inputs.Session, inputs.OwnEntity, inputs.LastInputSeq, inputs.HasMotion));
        Assert.Equal((state.X, state.Y, state.Yaw), (inputs.Motion.X, inputs.Motion.Y, inputs.Motion.Yaw));
        Assert.Equal(parameters, inputs.Params);
        Assert.Equal(2, inputs.InputBufferDepth); // three queued, one taken
        Assert.Equal(avatar.client.dm_id, inputs.ClientId);

        // The Delta carries the block after the shared body (the fake codec's layout).
        var delta = FakeCodec.DecodeDelta(alice.Messages(MessageId.Delta).Last());
        Assert.Equal(new byte[] { 1, 2, 1 }, delta.OwnBlock);
        Assert.Equal(1u, delta.LastInputSeq);
        Assert.Equal(BitConverter.GetBytes(delta.Tick), delta.Body);
    }

    [Fact]
    public void AMobThatIsNoCapsuleHasNoMotionInItsBlock()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        server.Replicator.Privates.Clear();
        server.Tick();
        Assert.False(server.Replicator.Privates.Single().Inputs.HasMotion); // TryGetState found no state
    }

    [Fact]
    public void ALeavingSessionsMobStandsStillAndStaysACapsule()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        mob avatar = server.MobOf("Alice");
        alice.Motion(127, 0, 9000);
        server.Tick(2);
        server.Locomotion.Frames.Clear();
        alice.Drop();
        server.Tick(2);
        var idle = Assert.Single(server.Locomotion.Frames);
        Assert.Equal((avatar.dm_id, (sbyte)0, (sbyte)0, (ushort)9000), (idle.Mob, idle.Frame.MoveX, idle.Frame.MoveY, idle.Frame.Yaw));
        Assert.True(server.Locomotion.IsRegistered(avatar)); // SSD: the body stays solid (D105)
    }

    [Fact]
    public void AFrameFromASessionThatIsNotJoinedYetIsNotQueued()
    {
        using var server = TestServer.Start();
        server.Clients.LoginSleepTicks = 6;
        FakeRemote slow = server.Connect("Slow");
        server.Tick(2);
        slow.Motion(127, 0);
        server.Tick();
        Assert.Equal(1, server.Host.SessionOf(1).Strikes);
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind == CommandKind.Motion);
    }

    [Fact]
    public void TwoRunsOfTheSameInputsGiveTheSameMotionCommandsAndHash()
    {
        ulong Run(out CommandLog log)
        {
            using var server = TestServer.Start();
            FakeRemote alice = server.Join("Alice");
            for (int t = 0; t < 30; t++)
            {
                if (t % 3 == 0)
                {
                    alice.SkipTo(server.Host.SessionOf(1).LastAppliedInputSeq); // D331: past the seqs the repeats stood in for, as the client's prediction
                    alice.Motion((sbyte)(t % 2 == 0 ? 100 : -100), (sbyte)(t % 5), (ushort)(t * 1000));
                }
                server.Tick();
            }
            log = server.Host.Log;
            return server.Hash();
        }
        ulong a = Run(out CommandLog first);
        ulong b = Run(out CommandLog second);
        Assert.Equal(a, b);
        Assert.Equal(first.Commands, second.Commands);
        Assert.True(first.Commands.Count(c => c.Kind == CommandKind.Motion) >= 10);
    }
}

internal static class ClientFixtureExtensions
{
    /// <summary>The mob a fixture client controls (FakeClients attaches the mob and the client both ways).</summary>
    public static mob FakeMobOf(this client c) => ((client_test_m1_server_client)c).FakeMob;
}
