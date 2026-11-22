using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>
/// M2 P8: own-capsule prediction against a server over the loopback with latency and loss (scope 2l "Prediction headless"):
/// walking alone gives no correction at all, contact and shove corrections converge within three ticks, the client and the
/// server stay bit-equal. The server is the client's scripted one (<see cref="FakeServer"/>), which runs the real controller
/// with the real parameters; the integration step runs the same against the real host.
/// </summary>
public class HeadlessPredictionTests
{
    private static LoopbackOptions Net(int latencySteps, double loss, ulong seed = 5)
        => new() { Seed = seed, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps, LossRate = loss };

    // A walk that turns: along the room, into the partition wall, around, through the doorway.
    private static void WalkAbout(HeadlessWorld world, int seconds)
    {
        MoveKeys[] plan = { MoveKeys.Forward, MoveKeys.Forward | MoveKeys.Right, MoveKeys.Right, MoveKeys.Back | MoveKeys.Right, MoveKeys.Back, MoveKeys.Left, MoveKeys.Forward | MoveKeys.Left };
        double[] yaws = { 0, 0.3, 1.2, -0.8, 2.0, -1.5, 0.0 };
        for (int s = 0; s < seconds; s++)
        {
            world.Keys = plan[s % plan.Length];
            world.Yaw = yaws[s % yaws.Length];
            world.Run(20);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void WalkingAloneGivesZeroCorrectionsAtAnyLatency(int latencySteps)
    {
        using var world = new HeadlessWorld(network: Net(latencySteps, 0.0));
        WalkAbout(world, 30);
        PredictionCounters c = world.Session.Prediction.Counters;
        Assert.True(c.Reconciliations > 400, "reconciliations " + c.Reconciliations);
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Snaps);
        // Bit-equal: the replayed state equals the old prediction in every bit (the same step, the same view).
        Assert.Equal(0, c.Mismatches);
        // And the camera follows: stopped, the prediction equals the server's state.
        world.Keys = MoveKeys.None;
        world.Run(20);
        Assert.Equal(world.Server.PlayerState.X, world.Session.Prediction.Predicted.X);
        Assert.Equal(world.Server.PlayerState.Y, world.Session.Prediction.Predicted.Y);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void With2PercentLossACorrectionNeedsAServerSideLossEvent(int latencySteps)
    {
        // Redundancy covers a lost Input message: the next one repeats its frames. A correction can only come from the server's
        // buffer running dry (two messages in a row lost: the server repeats the last frame) or overflowing (a burst: the oldest
        // frame is dropped), and then the server has stepped other than the frames said. Every correction must be explained so.
        int zero = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            using var world = new HeadlessWorld(network: Net(latencySteps, 0.02, seed));
            WalkAbout(world, 40);
            PredictionCounters c = world.Session.Prediction.Counters;
            Assert.True(world.Network.DroppedUnreliable > 5, "the loss ran");
            Assert.True(c.Corrections <= world.Server.Starved + world.Server.Dropped,
                $"seed {seed}: {c.Corrections} corrections but the server starved {world.Server.Starved} and dropped {world.Server.Dropped}");
            Assert.Equal(0, c.Snaps);
            Assert.True(c.MaxErrorTiles < 1f);
            if (c.Corrections == 0)
                zero++;
            world.Keys = MoveKeys.None;
            world.Run(20);
            Assert.Equal(world.Server.PlayerState.X, world.Session.Prediction.Predicted.X);
        }
        Assert.True(zero >= 6, "most runs of 40 s are untouched by the loss: " + zero + " of 12");
    }

    [Fact]
    public void ABodyThatStandsStillIsBumpedWithoutACorrection()
    {
        using var world = new HeadlessWorld(new FakeServerOptions { Bots = 1, StillBots = true, ScriptedEvents = false }, Net(2, 0.0));
        // A body three tiles in front of the player; the player walks into it and leans on it.
        PlayerState(world, out int px, out int py);
        world.Server.Place(2, px, py + 3);
        world.Run(10);
        world.Keys = MoveKeys.Forward;
        world.Run(80);
        PredictionCounters c = world.Session.Prediction.Counters;
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Mismatches);
        world.Server.TryGetState(2, out MotionState bot);
        MotionState me = world.Server.PlayerState;
        double dx = (me.X - bot.X) / 256.0, dy = (me.Y - bot.Y) / 256.0;
        Assert.True(Math.Sqrt(dx * dx + dy * dy) >= 2 * MotionMath.DefaultRadius - 0.01, "the capsules touch, they do not overlap");
        Assert.True(Math.Abs(dy) < 0.5, "it stopped at the body: " + dy);
    }

    [Fact]
    public void AShoveCorrectionConvergesWithinThreeTicks()
    {
        using var world = new HeadlessWorld(new FakeServerOptions { Bots = 1, StillBots = true, ScriptedEvents = false }, Net(2, 0.02));
        PlayerState(world, out int px, out int py);
        world.Server.Place(2, px + 1, py); // beside the player, to its east
        world.Run(20);
        PredictionCounters before = world.Session.Prediction.Counters;
        Assert.Equal(0, before.Corrections);

        // The bot shoves the standing player west: one full tile, slid over the glide time.
        Assert.True(world.Server.Shove(2, FakeServer.OwnId));
        world.Server.TryGetState(FakeServer.OwnId, out MotionState shoved);
        int ticksWithCorrection = 0;
        long firstCorrection = -1, lastCorrection = -1;
        long previous = before.Corrections;
        float worst = 0;
        world.Run(30, tick =>
        {
            PredictionCounters c = world.Session.Prediction.Counters;
            if (c.Corrections > previous)
            {
                if (firstCorrection < 0)
                    firstCorrection = tick;
                lastCorrection = tick;
                ticksWithCorrection++;
            }
            previous = c.Corrections;
            worst = Math.Max(worst, c.MaxErrorTiles);
        });
        Assert.True(firstCorrection > 0, "the client noticed the shove");
        Assert.True(worst > 0.2f, "a good part of a tile: " + worst);
        // The correction is shown once, then the replay agrees with the server again: the counters stop growing.
        PredictionCounters after = world.Session.Prediction.Counters;
        Assert.InRange(after.Corrections - before.Corrections, 1, 3);
        Assert.False(world.Session.Prediction.Predicted.Slide.Active);
        Assert.Equal(world.Server.PlayerState.X, world.Session.Prediction.Predicted.X);
        Assert.Equal(world.Server.PlayerState.Y, world.Session.Prediction.Predicted.Y);
        Assert.True(world.Server.PlayerState.X < shoved.X || world.Server.PlayerState.TileX == px - 1 || world.Server.PlayerState.TileX == px + 0, "it moved west");
        // The visible error has decayed away within the smoothing time.
        (float ox, float oy) = world.Session.Prediction.VisualOffset;
        Assert.Equal(0f, ox, 4);
        Assert.Equal(0f, oy, 4);
        Assert.True(lastCorrection - firstCorrection < 3, "corrections lasted from tick " + firstCorrection + " to " + lastCorrection);
        Assert.True(ticksWithCorrection <= 3, "corrected on " + ticksWithCorrection + " ticks");
    }

    [Fact]
    public void ARightClickOnTheMobUnderTheCrosshairShovesItAndALeftClickHelps()
    {
        using var world = new HeadlessWorld(new FakeServerOptions { Bots = 1, StillBots = true, ScriptedEvents = false }, Net(1, 0));
        PlayerState(world, out int px, out int py);
        world.Server.Place(2, px, py + 1); // one tile north, in reach
        world.Run(10);
        // Looking north (yaw 0) at eye height: the bot is under the crosshair; looking away it is not.
        Assert.True(world.Session.TryPick(0, 0, 1.6f, out long id, out bool inReach));
        Assert.Equal(2, id);
        Assert.True(inReach);
        Assert.False(world.Session.TryPick(1.0, 0, 1.6f, out _, out _));
        Assert.Equal(0, world.Session.PickTarget(Math.PI, 0, 1.6f));

        world.Server.TryGetState(2, out MotionState before);
        Assert.True(world.Session.Click(id, ClickButton.Right, ClickModifiers.None));
        world.Run(25);
        world.Server.TryGetState(2, out MotionState after);
        Assert.Equal(before.TileY + 1, after.TileY); // moved one tile away, north
        Assert.False(world.Session.Click(0, ClickButton.Right, ClickModifiers.None)); // nothing under the crosshair: no message
        // The cooldown (16 ticks) has passed: a left click is the help click and the server's text comes back.
        var texts = new List<string>();
        world.Client.ServerTextReceived += t => texts.Add(t.Html);
        world.Server.Place(2, px, py + 1);
        world.Run(3);
        world.Session.Click(2, ClickButton.Left, ClickModifiers.None);
        world.Run(10);
        Assert.Contains(texts, t => t.Contains("pat Bot 1"));
    }

    [Fact]
    public void ACameraShakeFollowsAShove()
    {
        using var world = new HeadlessWorld(new FakeServerOptions { Bots = 1, StillBots = true, ScriptedEvents = false }, Net(1, 0));
        PlayerState(world, out int px, out int py);
        world.Server.Place(2, px + 1, py);
        world.Run(10);
        Assert.Equal(0, world.Session.CorrectionShakes);
        world.Server.Shove(2, FakeServer.OwnId);
        world.Run(10);
        Assert.True(world.Session.CorrectionShakes >= 1);
    }

    [Fact]
    public void AMovingBodyMakesSmallCorrectionsAtContactThatConverge()
    {
        // Bots wander and the player runs into them: the client sees a bot where it was a latency ago, so a contact is
        // corrected by up to a step; each correction is gone in a few ticks and none is as big as a tile.
        using var world = new HeadlessWorld(new FakeServerOptions { Bots = 12, Size = 24, ScriptedEvents = false }, Net(2, 0.02));
        WalkAbout(world, 60);
        PredictionCounters c = world.Session.Prediction.Counters;
        Assert.True(c.Reconciliations > 1000);
        Assert.True(c.Snaps == 0, "snaps " + c.Snaps);
        Assert.True(c.MaxErrorTiles < 1f, "largest error " + c.MaxErrorTiles);
        // After the run the prediction agrees with the server again once the player stops and the bots are far.
        world.Keys = MoveKeys.None;
        world.Run(10);
        Assert.Equal(world.Server.PlayerState.X, world.Session.Prediction.Predicted.X);
        Assert.Equal(world.Server.PlayerState.Y, world.Session.Prediction.Predicted.Y);
    }

    [Fact]
    public void TheInputClockKeepsAboutOneFrameInTheServersBuffer()
    {
        using var world = new HeadlessWorld(network: Net(2, 0.02));
        var depths = new List<int>();
        world.Keys = MoveKeys.Forward;
        world.Run(200, tick => depths.Add(world.Session.Prediction.BufferDepth));
        depths.RemoveRange(0, 40);
        Assert.InRange(depths.Average(), 0.3, 2.0);
        Assert.InRange(world.Session.Prediction.ClockRate, 0.95, 1.05);
        Assert.True(depths.All(d => d <= Limits.MaxJitterBufferFrames));
    }

    [Fact]
    public void ActionsAreTaggedWithTheirFrameAndTheServerAppliesThemAfterIt()
    {
        using var world = new HeadlessWorld(network: Net(2, 0));
        world.Run(10);
        uint tag = world.Session.Prediction.NewestSeq;
        world.Session.Rest();
        Assert.False(world.Server.PlayerLying);
        world.Run(2);
        // 100 ms each way: not yet.
        world.Run(6);
        Assert.True(world.Server.PlayerLying);
        Assert.True(world.Server.AppliedSeq >= tag);
        // The posture arrives in the private block, and the camera sinks.
        world.Run(6);
        Assert.Equal(Posture.Lying, world.Session.Prediction.Predicted.Posture);
        world.Session.Rest();
        world.Run(4);
        Assert.True(world.Server.PlayerLying, "getting up takes a second");
        world.Run(30);
        Assert.False(world.Server.PlayerLying);
    }

    [Fact]
    public void LyingTheCapsuleCrawlsAtTgsCrawlSpeedAndStillPredictsExactly()
    {
        using var world = new HeadlessWorld(network: Net(2, 0.02));
        world.Run(10);
        world.Session.Rest();
        world.Run(10);
        world.Keys = MoveKeys.Forward;
        MotionState before = world.Session.Prediction.Predicted;
        world.Run(40);
        MotionState after = world.Session.Prediction.Predicted;
        double tiles = (after.Y - before.Y) / 256.0;
        // Crawling: 1.5 + 4 ds per tile is 1.8 tiles per second; 2 s, less the reaction.
        Assert.InRange(tiles, 2.0, 4.0);
        Assert.Equal(0, world.Session.Prediction.Counters.Corrections);
        Assert.Equal(MotionDriver.Prone, after.Driver);
    }

    [Fact]
    public void WalkRunToggleChangesTheSpeedAfterTheServerSaysSo()
    {
        using var world = new HeadlessWorld(network: Net(1, 0));
        world.Run(10);
        world.Keys = MoveKeys.Forward;
        MotionState a = world.Session.Prediction.Predicted;
        world.Run(10);
        double run = (world.Session.Prediction.Predicted.Y - a.Y) / 256.0;
        world.Keys = MoveKeys.None;
        world.Run(4);
        world.Session.ToggleMoveIntent();
        world.Run(6);
        Assert.InRange(world.Session.Prediction.Params.TilesPerTick, 0.12f, 0.13f);
        world.Keys = MoveKeys.Forward;
        MotionState b = world.Session.Prediction.Predicted;
        world.Run(10);
        double walk = (world.Session.Prediction.Predicted.Y - b.Y) / 256.0;
        Assert.InRange(run, 2.8, 3.5);
        Assert.InRange(walk, 1.0, 1.5);
    }

    private static void PlayerState(HeadlessWorld world, out int x, out int y)
    {
        MotionState s = world.Server.PlayerState;
        x = s.TileX;
        y = s.TileY;
    }
}
