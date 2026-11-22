using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// Own-capsule prediction (D103, M2 P4 <c>MotionPredictor</c>, <c>InputClock</c>; docs/ai/specs/locomotion.md section 3): a
/// replay through the same step and view gives the server's bits, a disagreement is smoothed or snapped by its size, the
/// visible error decays over about 100 ms, stale acknowledgements change nothing, and the input clock keeps about one frame
/// queued on the server.
/// </summary>
public class MotionPredictorTests
{
    [Fact]
    public void ASubTileThrustKeepsItsDirectionThroughIdlePredictionAndReplay()
    {
        MotionState start = MotionState.AtTile(10, 10, 1);
        start.Driver = MotionDriver.ZeroG;
        var predictor = new MotionPredictor(start);
        var view = new GridView();
        MotionParams parameters = MotionParams.DefaultRun with { DriftX = 26 };
        predictor.Push(0, 12, 0, MotionInputFlags.None, parameters, view);
        Assert.Equal((short)0, predictor.PredictedParams.DriftX);
        Assert.Equal((short)26, predictor.PredictedParams.DriftY);
        for (int i = 0; i < 10; i++)
            predictor.Push(0, 0, 0, MotionInputFlags.None, predictor.PredictedParams, view);
        MotionState expected = predictor.Predicted;
        Assert.Equal(start.X, expected.X);
        var replay = predictor.Reconcile(start, 0, parameters, view);
        Assert.Equal(CorrectionKind.None, replay.Kind);
        Assert.Equal(expected, predictor.Predicted);
        Assert.Equal((short)26, predictor.PredictedParams.DriftY);
        Assert.Equal(0, predictor.Counters.Mismatches);
    }

    [Fact]
    public void APushFromRestStartsTheDriftTheServerSaidItWouldAndWithoutItPredictsNone()
    {
        MotionState start = MotionState.AtTile(10, 10, 1);
        start.Driver = MotionDriver.ZeroG;
        var view = new GridView();
        // protocol version 51 (DV177): no drift yet; the server's impulse speed starts one along the push
        var predictor = new MotionPredictor(start);
        MotionParams parameters = MotionParams.DefaultRun with { ImpulseSpeed = 38 };
        predictor.Push(12, 0, 0, MotionInputFlags.None, parameters, view);
        Assert.Equal(((short)38, (short)0), (predictor.PredictedParams.DriftX, predictor.PredictedParams.DriftY));
        int pushed = predictor.Predicted.X;
        predictor.Push(0, 0, 0, MotionInputFlags.None, predictor.PredictedParams, view);
        Assert.Equal(pushed + 38, predictor.Predicted.X);
        // without it (beside something to push off) the server's drift stays authoritative
        var still = new MotionPredictor(start);
        still.Push(12, 0, 0, MotionInputFlags.None, MotionParams.DefaultRun, view);
        Assert.Equal(((short)0, (short)0), (still.PredictedParams.DriftX, still.PredictedParams.DriftY));
    }

    // A stand-in server: it applies the frames it received, one per tick, through the same controller.
    private sealed class Server
    {
        public MotionState State;
        public uint LastSeq;

        public Server(MotionState start) => State = start;

        public void Apply(MotionInput frame, MotionParams p, ICollisionView view)
        {
            MotionStep.Step(ref State, in frame, in p, view, new MotionEvents());
            LastSeq = frame.Seq;
        }
    }

    [Fact]
    public void AReplayThroughTheSameStepGivesTheServersBitsWithLatency()
    {
        var view = new GridView().Solid(8, 5).Solid(8, 6).Solid(4, 9);
        view.Capsules.Add(new CapsuleObstacle(5, 6 * 256 + 30, 8 * 256, 0.175f));
        MotionState start = MotionState.AtTile(5, 5, 1);
        var predictor = new MotionPredictor(start);
        var server = new Server(start);
        var inFlight = new Queue<MotionInput>();
        var rng = new Random(3);
        const int latency = 4; // ticks from the client's push to the server's step, and the ack comes back with the state
        var acks = new Queue<(MotionState, uint)>();
        for (int tick = 0; tick < 600; tick++)
        {
            MotionInput frame = predictor.Push((sbyte)rng.Next(-128, 128), (sbyte)rng.Next(-128, 128), (ushort)rng.Next(65536), MotionInputFlags.None, Run, view);
            inFlight.Enqueue(frame);
            if (inFlight.Count > latency)
            {
                server.Apply(inFlight.Dequeue(), Run, view);
                acks.Enqueue((server.State, server.LastSeq));
            }
            if (acks.Count > latency)
            {
                (MotionState state, uint seq) = acks.Dequeue();
                Reconciliation r = predictor.Reconcile(state, seq, Run, view);
                Assert.Equal(CorrectionKind.None, r.Kind);
                Assert.Equal(0f, r.ErrorTiles);
            }
        }
        PredictionCounters c = predictor.Counters;
        Assert.True(c.Reconciliations > 500);
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Mismatches);
        Assert.Equal(0, c.Snaps);
        Assert.Equal((0f, 0f), predictor.VisualOffset);
        Assert.InRange(predictor.Pending, latency, 2 * latency + 1);
    }

    [Fact]
    public void AnAckDropsTheAcknowledgedFramesAndASmallDisagreementIsSmoothed()
    {
        var view = new GridView();
        MotionState start = MotionState.AtTile(5, 5, 1);
        var predictor = new MotionPredictor(start);
        for (int i = 0; i < 5; i++)
            predictor.Push(127, 0, 0, MotionInputFlags.None, Run, view);
        Assert.Equal(5, predictor.Pending);
        Assert.Equal(6u, predictor.NextSeq);
        Assert.Equal(5 * 256 + 5 * 85, predictor.Predicted.X);

        // The server says: after frame 2 the capsule stood 30 subunits further south (a contact it saw first).
        MotionState auth = start;
        auth.X += 2 * 85;
        auth.Y -= 30;
        Reconciliation r = predictor.Reconcile(auth, 2, Run, view);
        Assert.Equal(2u, predictor.AckedSeq);
        Assert.Equal(3, predictor.Pending);
        Assert.Equal(3, r.Replayed);
        Assert.Equal(CorrectionKind.Smoothed, r.Kind);
        Assert.Equal(30f / 256f, r.ErrorTiles, 5);
        Assert.Equal(5 * 256 - 30, predictor.Predicted.Y);
        (float ox, float oy) = predictor.VisualOffset;
        Assert.Equal((0f, 30f / 256f), (ox, oy)); // drawn where it was, decaying to the new state
        predictor.Decay(0.05f);
        Assert.Equal(15f / 256f, predictor.VisualOffset.Y, 5);
        predictor.Decay(0.06f);
        Assert.Equal((0f, 0f), predictor.VisualOffset);
        Assert.Equal(1, predictor.Counters.Corrections);
        Assert.Equal(1, predictor.Counters.Mismatches);
    }

    [Fact]
    public void ADifferenceUnderACentimetreIsTakenAsItIs()
    {
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        predictor.Push(127, 0, 0, MotionInputFlags.None, Run, view);
        MotionState auth = predictor.Predicted;
        auth.X += 1; // 1/256 tile = 0.8 cm
        Reconciliation r = predictor.Reconcile(auth, 1, Run, view);
        Assert.Equal(CorrectionKind.None, r.Kind);
        Assert.Equal(auth.X, predictor.Predicted.X);
        Assert.Equal((0f, 0f), predictor.VisualOffset);
        Assert.Equal(0, predictor.Counters.Corrections);
        Assert.Equal(1, predictor.Counters.Mismatches); // the bits differ all the same
    }

    [Fact]
    public void MoreThanATileSnaps()
    {
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        predictor.Push(127, 0, 0, MotionInputFlags.None, Run, view);
        Reconciliation r = predictor.Reconcile(MotionState.AtTile(9, 5, 1), 1, Run, view);
        Assert.Equal(CorrectionKind.Snapped, r.Kind);
        Assert.Equal((0f, 0f), predictor.VisualOffset);
        Assert.Equal(1, predictor.Counters.Snaps);
        Assert.Equal(9 * 256, predictor.Predicted.X);
        Reconciliation level = predictor.Reconcile(MotionState.AtTile(9, 5, 2), 1, Run, view);
        Assert.Equal(CorrectionKind.Snapped, level.Kind); // another level is always a snap
    }

    [Fact]
    public void ALevelUpOrDownTheSameColumnIsTheServersCarryNotAMisprediction()
    {
        // DV422: a flyer's or a ghost's level key (tg's zMove) only the server makes; a teleport to another level elsewhere still counts
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        predictor.Push(0, 0, 0, MotionInputFlags.None, Run, view);
        Reconciliation up = predictor.Reconcile(MotionState.AtTile(5, 5, 2), 1, Run, view);
        Assert.Equal((0f, 2), (up.ErrorTiles, predictor.Predicted.Z)); // no error shown: no camera shake
        Assert.Equal((0L, 0L, 1L), (predictor.Counters.Snaps, predictor.Counters.Mismatches, predictor.Counters.Carried));
        Reconciliation far = predictor.Reconcile(MotionState.AtTile(20, 5, 1), 1, Run, view);
        Assert.True(far.ErrorTiles > 1f);
        Assert.Equal(1L, predictor.Counters.Snaps);
    }

    [Fact]
    public void AShoveTheServerSentIsReplayedAsTheSameSlide()
    {
        var view = new GridView();
        MotionState start = MotionState.AtTile(5, 5, 1);
        var predictor = new MotionPredictor(start);
        var server = new Server(start);
        for (uint i = 0; i < 3; i++)
            predictor.Push(0, 0, 0, MotionInputFlags.None, Run, view);
        server.Apply(new MotionInput(1, 0, 0, 0, MotionInputFlags.None), Run, view);
        MotionStep.Displace(ref server.State, 6, 5, 1, 4); // tg moved the mob a tile east after frame 1
        Reconciliation r = predictor.Reconcile(server.State, 1, Run, view);
        Assert.Equal(CorrectionKind.Smoothed, r.Kind); // the slide's first two shares: half a tile
        server.Apply(new MotionInput(2, 0, 0, 0, MotionInputFlags.None), Run, view);
        server.Apply(new MotionInput(3, 0, 0, 0, MotionInputFlags.None), Run, view);
        Assert.Equal(server.State, predictor.Predicted); // the replay played the same slide
    }

    [Fact]
    public void ADiagonalPullTheServerSentIsSmoothedNotSnapped()
    {
        // the bed one lies on is pulled diagonally: tg moves the mob 1.4 tiles, which the capsule slides; more than a tile, still a glide
        var view = new GridView();
        MotionState start = MotionState.AtTile(5, 5, 1);
        var predictor = new MotionPredictor(start);
        MotionParams still = Run with { CanMove = false };
        for (uint i = 0; i < 6; i++)
            predictor.Push(0, 0, 0, MotionInputFlags.None, still, view);
        var server = new Server(start);
        server.Apply(new MotionInput(1, 0, 0, 0, MotionInputFlags.None), still, view);
        MotionStep.Displace(ref server.State, 6, 6, 1, 3);
        Reconciliation r = predictor.Reconcile(server.State, 1, still, view);
        Assert.True(r.ErrorTiles > MotionPredictor.SnapAboveTiles);
        Assert.Equal(CorrectionKind.Smoothed, r.Kind);
        Assert.Equal(0, predictor.Counters.Snaps);
        // a body that cannot move itself is moved by the server alone: no misprediction, a carried reconciliation
        Assert.Equal(0, predictor.Counters.Corrections);
        Assert.Equal(0, predictor.Counters.Mismatches);
        Assert.Equal(1, predictor.Counters.Carried);
    }

    [Fact]
    public void AStepOffAFloorIntoSpaceTurnsTheDriverAndTheSpeedWhereTheServerWould()
    {
        // weightless north of y 6 (the mask's rows dy >= 1 around (5, 5)); the jetpack's full speed there
        ulong mask = 0;
        for (int dy = 1; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
                mask |= 1UL << ((dy + 3) * 7 + dx + 3);
        MotionParams floor = Run with { GravityMask = mask, GravityX = 5, GravityY = 5, OtherTilesPerTick = 0.5f, OtherCanMove = true }; // a lit jetpack steers there
        MotionState s = MotionState.AtTile(5, 5, 1);
        Assert.Equal(Run.TilesPerTick, MotionPredictor.InGravityOf(ref s, floor).TilesPerTick);
        Assert.Equal(MotionDriver.Grounded, s.Driver);
        MotionState space = MotionState.AtTile(5, 6, 1);
        Assert.Equal(0.5f, MotionPredictor.InGravityOf(ref space, floor).TilesPerTick);
        Assert.Equal(MotionDriver.ZeroG, space.Driver);
        MotionState far = MotionState.AtTile(5, 9, 1);
        Assert.Equal(Run.TilesPerTick, MotionPredictor.InGravityOf(ref far, floor).TilesPerTick); // beyond the mask: as the centre
        // made in space, the same mask turns the floor back: grounded at the space speed's other
        MotionParams inSpace = floor with { GravityY = 6, GravityMask = mask >> 7, TilesPerTick = 0.5f, OtherTilesPerTick = Run.TilesPerTick };
        MotionState back = MotionState.AtTile(5, 5, 1);
        back.Driver = MotionDriver.ZeroG;
        Assert.Equal(Run.TilesPerTick, MotionPredictor.InGravityOf(ref back, inSpace).TilesPerTick);
        Assert.Equal(MotionDriver.Grounded, back.Driver);

        // walking north off the floor: the prediction matches a server that turns the driver and the speed at the tile's start
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        var server = new Server(MotionState.AtTile(5, 5, 1));
        for (uint seq = 1; seq <= 12; seq++)
        {
            predictor.Push(0, 127, 0, MotionInputFlags.None, floor, view);
            MotionParams p = MotionPredictor.InGravityOf(ref server.State, floor);
            server.Apply(new MotionInput(seq, 0, 127, 0, MotionInputFlags.None), p, view);
        }
        Assert.Equal(MotionDriver.ZeroG, predictor.Predicted.Driver);
        Assert.Equal(CorrectionKind.None, predictor.Reconcile(server.State, 12, floor, view).Kind);
    }

    [Fact]
    public void AnOlderAcknowledgementIsIgnoredAndTheSameOneIsReconciledAgain()
    {
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        for (int i = 0; i < 4; i++) predictor.Push(0, 127, 0, MotionInputFlags.None, Run, view);
        MotionState after3 = MotionState.AtTile(5, 5, 1);
        after3.Y += 3 * 85;
        predictor.Reconcile(after3, 3, Run, view);
        MotionState predicted = predictor.Predicted;
        Reconciliation stale = predictor.Reconcile(MotionState.AtTile(1, 1, 1), 2, Run, view);
        Assert.Equal(0, stale.Replayed);
        Assert.Equal(predicted, predictor.Predicted);
        Assert.Equal(3u, predictor.AckedSeq);
        // The same ack with a state that moved (the server repeated a frame): replayed again.
        MotionState repeated = after3;
        repeated.Y += 85;
        Reconciliation again = predictor.Reconcile(repeated, 3, Run, view);
        Assert.Equal(1, again.Replayed);
        Assert.Equal(CorrectionKind.Smoothed, again.Kind);
    }

    [Fact]
    public void AFullRingDropsItsOldestFrame()
    {
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1), capacity: 4);
        for (int i = 0; i < 6; i++) predictor.Push(0, 0, 0, MotionInputFlags.None, Run, view);
        Assert.Equal(4, predictor.Capacity);
        Assert.Equal(4, predictor.Pending);
        Assert.Equal(2, predictor.Counters.Overflows);
    }

    [Fact]
    public void TheRecentFramesAreTheNewestFourNewestLast()
    {
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        Span<MotionInput> buffer = stackalloc MotionInput[MotionPredictor.Redundancy];
        predictor.Push(1, 0, 0, MotionInputFlags.None, Run, view);
        Assert.Equal(1, predictor.CopyRecentFrames(buffer));
        for (int i = 0; i < 5; i++) predictor.Push(1, 0, 0, MotionInputFlags.None, Run, view);
        Assert.Equal(4, predictor.CopyRecentFrames(buffer));
        Assert.Equal(new uint[] { 3, 4, 5, 6 }, buffer.ToArray().Select(f => f.Seq).ToArray());
        predictor.Reconcile(predictor.Predicted, 6, Run, view); // acknowledged frames are still repeated (cheap, harmless)
        Assert.Equal(2, predictor.CopyRecentFrames(buffer, max: 2));
        Assert.Equal(new uint[] { 5, 6 }, buffer[..2].ToArray().Select(f => f.Seq).ToArray());
    }

    [Fact]
    public void AResetDropsThePendingFramesAndKeepsTheSeq()
    {
        var view = new GridView();
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        predictor.Push(127, 0, 0, MotionInputFlags.None, Run, view);
        predictor.Push(127, 0, 0, MotionInputFlags.None, Run, view);
        predictor.Reset(MotionState.AtTile(2, 2, 1));
        Assert.Equal(0, predictor.Pending);
        Assert.Equal(3u, predictor.NextSeq);
        Assert.Equal(2 * 256, predictor.Predicted.X);
    }

    [Fact]
    public void TheInputClockSendsOneFramePerTickAndNudgesItsRateByTheServersQueue()
    {
        var clock = new InputClock();
        Assert.Equal(1, clock.TargetDepth);
        Assert.Equal(1.0, clock.Rate);
        int frames = 0;
        for (int i = 0; i < 32; i++) frames += clock.Advance(1.0 / 32); // exact binary fractions
        Assert.Equal(20, frames); // one second, 20 ticks
        for (int i = 0; i < 100; i++) clock.ObserveBufferDepth(3); // too many queued: slow down
        Assert.InRange(clock.Rate, 1 - InputClock.MaxNudge, 0.99);
        for (int i = 0; i < 200; i++) clock.ObserveBufferDepth(0); // starving: speed up
        Assert.InRange(clock.Rate, 1.01, 1 + InputClock.MaxNudge);
        Assert.Equal(InputClock.MaxFramesPerAdvance, clock.Advance(5.0)); // a long hitch does not flood the server
        Assert.Equal(0, clock.Advance(0));
    }

    [Theory]
    [InlineData(0, MotionDir.North, MotionDir.North)]
    [InlineData(8192 + 900, MotionDir.North, MotionDir.North)]   // past 45 degrees but within the hysteresis
    [InlineData(8192 + 920, MotionDir.North, MotionDir.East)]    // past the hysteresis
    [InlineData(65536 - 8192 - 900, MotionDir.North, MotionDir.North)]
    [InlineData(65536 - 8192 - 920, MotionDir.North, MotionDir.West)]
    [InlineData(32768, MotionDir.East, MotionDir.South)]
    [InlineData(8192, 0, MotionDir.East)]                         // no current facing: the nearest, clockwise on the boundary
    [InlineData(16384 + 8192 + 500, MotionDir.East, MotionDir.East)]
    public void TheFacingFollowsTheYawWithFiveDegreesOfHysteresis(int yaw, byte current, byte expected)
    {
        Assert.Equal(expected, MotionFacing.DirOf((ushort)yaw, current));
    }

    [Fact]
    public void TheYawOfADirectionIsItsCompassBearing()
    {
        Assert.Equal(0, MotionFacing.YawOf(MotionDir.North));
        Assert.Equal(16384, MotionFacing.YawOf(MotionDir.East));
        Assert.Equal(32768, MotionFacing.YawOf(MotionDir.South));
        Assert.Equal(49152, MotionFacing.YawOf(MotionDir.West));
        foreach (byte d in new[] { MotionDir.North, MotionDir.South, MotionDir.East, MotionDir.West })
            Assert.Equal(d, MotionFacing.Nearest(MotionFacing.YawOf(d)));
    }
}
