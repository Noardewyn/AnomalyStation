using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// DV263, the movement feel (docs/ai/specs/locomotion.md section 2): a walking capsule gathers its speed over the feel's ticks, loses it
/// over its decel ticks, goes slower backward and sideways, turns within a quarter turn at its speed, brakes a reversal, stops at a
/// wall and keeps its speed along one; and a prediction through the same step replays the server's bits with the velocity.
/// </summary>
public class MoveFeelTests
{
    private const ushort East = 16384;
    private static readonly MoveFeel Ramp = new(4, 3, 0f, 0f);
    private static readonly MotionParams Feel = Run with { Feel = MoveFeel.Default };
    private static readonly int Full = (int)MathF.Floor(Run.TilesPerTick * MotionMath.VelocityPerTile + 0.5f);

    private static int Speed(in MotionState s) => (int)MathF.Round(MathF.Sqrt((float)((long)s.VelX * s.VelX + (long)s.VelY * s.VelY)));

    [Fact]
    public void TheFacingIsTheCompassBearingsSineAndCosine()
    {
        for (int yaw = 0; yaw < 65536; yaw++)
        {
            double a = yaw * (2 * Math.PI / 65536);
            (float e, float n) = MotionMath.Facing((ushort)yaw);
            Assert.InRange(e - Math.Sin(a), -1e-5, 1e-5);
            Assert.InRange(n - Math.Cos(a), -1e-5, 1e-5);
        }
    }

    [Fact]
    public void FullSpeedComesInTheAccelTicksAndThenStepsAsWithoutAFeel()
    {
        var view = new GridView(40, 20);
        MotionState s = MotionState.AtTile(3, 10, 1);
        var p = Run with { Feel = Ramp };
        int x = s.X;
        for (int tick = 1; tick <= 4; tick++)
        {
            Step(ref s, Frame(127, 0, yaw: East), p, view);
            Assert.True(s.X - x < 85 || tick == 4, $"tick {tick} already stepped a whole run step");
            x = s.X;
            Assert.Equal(Math.Min(Full, (Full + 3) / 4 * tick), s.VelX); // a quarter of full speed a tick, rounded up
        }
        Assert.Equal(Full, s.VelX);
        MotionState plain = s;
        plain.Stop();
        Step(ref s, Frame(127, 0, yaw: East), p, view);
        Step(ref plain, Frame(127, 0, yaw: East), Run, view);
        Assert.Equal(plain.X, s.X); // at full speed a step is tg's
    }

    [Fact]
    public void ReleasingTheKeysStopsInTheDecelTicks()
    {
        var view = new GridView(40, 20);
        MotionState s = MotionState.AtTile(3, 10, 1);
        var p = Run with { Feel = Ramp };
        for (int i = 0; i < 6; i++)
            Step(ref s, Frame(127, 0, yaw: East), p, view);
        int x = s.X;
        for (int tick = 1; tick <= 3; tick++)
        {
            Step(ref s, Frame(0, 0, yaw: East), p, view);
            Assert.True(tick == 3 ? s.VelX == 0 : s.VelX > 0, $"tick {tick}: velocity {s.VelX}");
            Assert.True(s.X >= x);
            x = s.X;
        }
        Step(ref s, Frame(0, 0, yaw: East), p, view);
        Assert.Equal(x, s.X);
    }

    [Theory]
    [InlineData(0, 127, 1.0)]     // forward
    [InlineData(0, -127, 0.49)]    // straight back
    [InlineData(127, 0, 0.63)]     // sideways
    [InlineData(-127, 0, 0.63)]
    public void BackwardAndSidewaysAreSlowerThanForward(int moveX, int moveY, double share)
    {
        var view = new GridView(40, 40);
        MotionState s = MotionState.AtTile(20, 20, 1);
        for (int i = 0; i < 8; i++)
            Step(ref s, Frame((sbyte)moveX, (sbyte)moveY, yaw: 0), Feel, view);
        Assert.InRange(Speed(s), Full * share - 2, Full * share + 2);
        // the direction is the wish's: the slow-down scales, it does not turn
        Assert.Equal(Math.Sign(moveX), Math.Sign(s.VelX));
        Assert.Equal(Math.Sign(moveY), Math.Sign(s.VelY));
    }

    [Fact]
    public void ADiagonalBackIsBetweenBackAndSideways()
    {
        var view = new GridView(40, 40);
        MotionState s = MotionState.AtTile(20, 20, 1);
        for (int i = 0; i < 8; i++)
            Step(ref s, Frame(127, -127, yaw: 0), Feel, view);
        Assert.InRange(Speed(s), Full * 0.49, Full * 0.63);
    }

    [Fact]
    public void ATurnWithinAQuarterKeepsTheSpeedAndAReversalBrakesThroughZero()
    {
        var view = new GridView(40, 40);
        MotionState s = MotionState.AtTile(20, 20, 1);
        var p = Run with { Feel = Ramp };
        for (int i = 0; i < 6; i++)
            Step(ref s, Frame(127, 0, yaw: East), p, view);
        Step(ref s, Frame(127, 127, yaw: East), p, view); // half a quarter: at once
        Assert.InRange(Speed(s), Full - 2, Full + 2);
        Assert.Equal(s.VelX, s.VelY, 2.0);
        Step(ref s, Frame(127, 0, yaw: East), p, view);
        Assert.Equal((Full, 0), (s.VelX, s.VelY));

        // west: the velocity brakes at the decel rate through zero, then gathers again
        int ticks = 0;
        while (s.VelX > 0 && ticks < 10)
        {
            Step(ref s, Frame(-127, 0, yaw: East), p, view);
            ticks++;
        }
        Assert.InRange(ticks, 2, 3);
        while (s.VelX != -Full && ticks < 20)
        {
            Step(ref s, Frame(-127, 0, yaw: East), p, view);
            ticks++;
        }
        Assert.Equal(-Full, s.VelX);
        Assert.InRange(ticks, 5, 8);
    }

    [Fact]
    public void AWallAheadStopsTheVelocityAndAWallAlongKeepsIt()
    {
        var view = new GridView(40, 40).Solid(10, 20);
        MotionState s = MotionState.AtTile(7, 20, 1);
        var p = Run with { Feel = Ramp };
        for (int i = 0; i < 20; i++)
            Step(ref s, Frame(127, 0, yaw: East), p, view);
        Assert.Equal(9, s.TileX);
        Assert.Equal(0, s.VelX); // pressed into the wall: no speed kept to burst away with
        Step(ref s, Frame(0, 127, yaw: East), p, view);
        Assert.InRange(s.VelY, 1, Full / 4 + 1); // north from rest, not at once

        // diagonally along a row of walls: the speed along them is a step without the feel
        var row = new GridView(40, 40);
        for (int x = 0; x < 40; x++)
            row.Solid(x, 22);
        MotionState a = MotionState.AtTile(5, 21, 1);
        MotionState b = a;
        for (int i = 0; i < 12; i++)
        {
            int ax = a.X, bx = b.X;
            Step(ref a, Frame(127, 127, yaw: East), p, row);
            Step(ref b, Frame(127, 127, yaw: East), Run, row);
            if (i >= 6)
                Assert.Equal(b.X - bx, a.X - ax);
        }
    }

    [Fact]
    public void ACapsuleThatCannotMoveOrIsDisplacedLosesItsVelocity()
    {
        var view = new GridView(40, 40);
        MotionState s = MotionState.AtTile(20, 20, 1);
        for (int i = 0; i < 6; i++)
            Step(ref s, Frame(127, 0, yaw: East), Feel, view);
        Assert.NotEqual(0, s.VelX);
        MotionState stunned = s;
        Step(ref stunned, Frame(127, 0, yaw: East), Feel with { CanMove = false }, view);
        Assert.Equal((0, 0), (stunned.VelX, stunned.VelY));
        MotionState shoved = s;
        MotionStep.Displace(ref shoved, shoved.TileX, shoved.TileY + 1, 1, 4);
        Assert.Equal((0, 0), (shoved.VelX, shoved.VelY));
        MotionState weightless = s;
        weightless.Driver = MotionDriver.ZeroG;
        Step(ref weightless, Frame(0, 0, yaw: East), Feel, view);
        Assert.Equal((0, 0), (weightless.VelX, weightless.VelY));
    }

    [Fact]
    public void TheFeelIsOffByDefault()
    {
        var view = new GridView(40, 40);
        MotionState s = MotionState.AtTile(20, 20, 1);
        Step(ref s, Frame(0, -127, yaw: 0), Run, view);
        Assert.Equal(20 * 256 - 85, s.Y); // a whole step backward at once, as tg
        Assert.Equal((0, 0), (s.VelX, s.VelY));
    }

    // A stand-in server: it applies the frames it received, one per tick, through the same controller.
    private sealed class Server(MotionState start)
    {
        public MotionState State = start;
        public uint LastSeq;

        public void Apply(MotionInput frame, MotionParams p, ICollisionView view)
        {
            MotionStep.Step(ref State, in frame, in p, view, new MotionEvents());
            LastSeq = frame.Seq;
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    [InlineData(7, 4)]
    public void AReplayWithTheVelocityGivesTheServersBits(int latency, int seed)
    {
        // walls, a corridor and a standing body: presses, slides along walls, stops and push-outs, with held keys, turns and reversals
        var view = new GridView(30, 30).Solid(12, 10).Solid(12, 11).Solid(8, 14).Solid(15, 8);
        for (int x = 4; x < 20; x++)
            view.Solid(x, 18);
        view.Capsules.Add(new CapsuleObstacle(5, 10 * 256 + 40, 13 * 256, 0.175f));
        MotionState start = MotionState.AtTile(10, 12, 1);
        var predictor = new MotionPredictor(start);
        var server = new Server(start);
        var inFlight = new Queue<MotionInput>();
        var acks = new Queue<(MotionState, uint)>();
        var rng = new Random(seed);
        sbyte mx = 0, my = 0;
        int yaw = 0, hold = 0;
        for (int tick = 0; tick < 2000; tick++)
        {
            if (--hold <= 0)
            {
                hold = rng.Next(1, 16);
                int keys = rng.Next(9); // none, or one of eight directions at full or a part of a tick
                (mx, my) = keys == 0 ? ((sbyte)0, (sbyte)0) : ((sbyte)(Math.Round(Math.Sin(keys * Math.PI / 4) * 127) * (rng.Next(4) == 0 ? 0.5 : 1)),
                    (sbyte)(Math.Round(Math.Cos(keys * Math.PI / 4) * 127) * (rng.Next(4) == 0 ? 0.5 : 1)));
                if (rng.Next(3) == 0)
                    yaw = rng.Next(65536); // a flick of the view
            }
            yaw = (yaw + rng.Next(-600, 601)) & 0xFFFF; // the mouse drifting
            MotionInput frame = predictor.Push(mx, my, (ushort)yaw, MotionInputFlags.None, Feel, view);
            inFlight.Enqueue(frame);
            if (inFlight.Count > latency)
            {
                server.Apply(inFlight.Dequeue(), Feel, view);
                acks.Enqueue((server.State, server.LastSeq));
            }
            if (acks.Count > latency)
            {
                (MotionState state, uint seq) = acks.Dequeue();
                Reconciliation r = predictor.Reconcile(state, seq, Feel, view);
                Assert.Equal(CorrectionKind.None, r.Kind);
            }
        }
        PredictionCounters c = predictor.Counters;
        Assert.True(c.Reconciliations > 1900);
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Mismatches);
        Assert.Equal(0, c.Snaps);
        Assert.True(server.State.X != start.X || server.State.Y != start.Y);
    }
}
