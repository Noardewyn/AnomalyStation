using Anomaly.Core.DmShim;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// Standing players are solid (D51, D105) at every speed the bridge allows: tg's slowdown can be below the run delay (the
/// minimum is one tick, a tile per tick) and MOVE_SPEED_SCALE goes up to 4 (D106), so a step can be longer than the
/// distance at which two bodies touch (2 x 0.175 tile = 90 subunits). A capsule must still stop at the body, not be
/// carried past its centre and pushed out on the far side.
/// </summary>
public class FastMoverTests
{
    private sealed class Field : ICollisionView
    {
        public readonly List<CapsuleObstacle> Capsules = new();
        public bool IsSolid(int x, int y, int z) => x < 0 || y < 0 || x > 20 || y > 20;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
        {
            foreach (CapsuleObstacle c in Capsules.OrderBy(c => c.Id))
            {
                if (c.Id != exclude && Math.Abs(c.X - x) <= range && Math.Abs(c.Y - y) <= range)
                    into.Add(c);
            }
        }
    }

    private const int C = 256;

    [Theory]
    [InlineData(0.25f)]   // 64 subunits a tick
    [InlineData(0.34f)]   // 87: just under the touch distance
    [InlineData(0.36f)]   // 92: just over it
    [InlineData(0.5f)]    // slowdown 1.0 ds
    [InlineData(0.75f)]
    [InlineData(1f)]      // slowdown 0.5 ds or less: a tile a tick
    [InlineData(1.34f)]   // the run speed at scale 4
    public void AHeadOnWalkerStopsAtTheBodyAtEverySpeed(float tilesPerTick)
    {
        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) })
        {
            var field = new Field();
            field.Capsules.Add(new CapsuleObstacle(7, 10 * C, 10 * C, MotionMath.DefaultRadius));
            var s = MotionState.AtTile(10 - 5 * dx, 10 - 5 * dy, 1);
            var p = new MotionParams(tilesPerTick, MotionMath.DefaultRadius, true, 3);
            var frame = new MotionInput(1, (sbyte)(127 * dx), (sbyte)(127 * dy), 0, MotionInputFlags.None);
            for (int tick = 0; tick < 60; tick++)
            {
                MotionStep.Step(ref s, frame, p, field, new MotionEvents(), 1);
                long px = s.X - 10 * C, py = s.Y - 10 * C;
                Assert.True(px * dx + py * dy <= 0, $"speed {tilesPerTick}, direction ({dx},{dy}), tick {tick}: the walker is past the body's centre at ({px},{py})");
                Assert.True(px * px + py * py >= 90L * 90L - 2 * 90, $"speed {tilesPerTick}, direction ({dx},{dy}), tick {tick}: inside the body at ({px},{py})");
            }
            long fx = s.X - 10 * C, fy = s.Y - 10 * C;
            Assert.True(fx * fx + fy * fy < 120L * 120L, $"speed {tilesPerTick}, direction ({dx},{dy}): it came to rest {Math.Sqrt(fx * fx + fy * fy):0} from the body's centre");
        }
    }

    [Fact]
    public void AFastWalkerAtTwoBodiesTouchingEachOtherNeverSlipsBetweenThem()
    {
        var field = new Field();
        // two bodies side by side across the walker's path, closer than their own touch distance: no way between them
        field.Capsules.Add(new CapsuleObstacle(7, 10 * C, 10 * C - 40, MotionMath.DefaultRadius));
        field.Capsules.Add(new CapsuleObstacle(8, 10 * C, 10 * C + 40, MotionMath.DefaultRadius));
        foreach (float tilesPerTick in new[] { 0.3f, 0.5f, 1f, 1.34f })
        {
            var s = MotionState.AtTile(5, 10, 1);
            var p = new MotionParams(tilesPerTick, MotionMath.DefaultRadius, true, 3);
            var frame = new MotionInput(1, 127, 0, 0, MotionInputFlags.None);
            for (int tick = 0; tick < 30; tick++)
            {
                MotionStep.Step(ref s, frame, p, field, new MotionEvents(), 1);
                foreach (CapsuleObstacle body in field.Capsules)
                {
                    long dx = s.X - body.X, dy = s.Y - body.Y;
                    Assert.True(dx * dx + dy * dy >= 90L * 90L - 2 * 90, $"speed {tilesPerTick}, tick {tick}: inside body {body.Id} at ({dx},{dy})");
                }
                // the gap between the bodies is the band |y - 10 * C| < 40 east of their near edge: never entered past x - 40
                if (Math.Abs(s.Y - 10 * C) < 40)
                    Assert.True(s.X < 10 * C - 60, $"speed {tilesPerTick}, tick {tick}: in the gap at ({s.X},{s.Y})");
            }
        }
    }

    [Fact]
    public void AContactIsStillReportedWhenTheStepIsCutIntoPieces()
    {
        var field = new Field();
        field.Capsules.Add(new CapsuleObstacle(7, 10 * C, 10 * C, MotionMath.DefaultRadius));
        var s = MotionState.AtTile(8, 10, 1);
        var p = new MotionParams(1f, MotionMath.DefaultRadius, true, 3);
        var frame = new MotionInput(1, 127, 0, 0, MotionInputFlags.None);
        var events = new MotionEvents();
        var contacts = new List<long>();
        for (int tick = 0; tick < 8; tick++)
        {
            events.Clear();
            MotionStep.Step(ref s, frame, p, field, events, 1);
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == MotionEventKind.Contact)
                    contacts.Add(events[i].OtherId);
            }
        }
        Assert.NotEmpty(contacts);
        Assert.All(contacts, id => Assert.Equal(7, id));
    }

    [Fact]
    public void AtTheDefaultRunSpeedTheStepIsNotCutAndTheResultIsTheOldOneByHand()
    {
        // 85 subunits is under the touch distance, so nothing changes for the default speeds (the recorded run of the motion
        // tests pins the bits): a walker 150 short of a body's centre steps 85, overlaps it by 25 and is pushed back along
        // the line of the centres to the touch distance, 90 from it (x = 2560 - 90)
        var field = new Field();
        field.Capsules.Add(new CapsuleObstacle(7, 10 * C, 10 * C, MotionMath.DefaultRadius));
        var s = MotionState.AtTile(10, 10, 1);
        s.X -= 150;
        var events = new MotionEvents();
        MotionStep.Step(ref s, new MotionInput(1, 127, 0, 0, MotionInputFlags.None), MotionParams.DefaultRun, field, events, 1);
        Assert.Equal(10 * C - 90, s.X);
        Assert.Equal(10 * C, s.Y);
        // the push-out ends in tile 10 (2432 and up): one crossing, then the contact
        Assert.Equal(2, events.Count);
        Assert.Equal(new MotionEvent(MotionEventKind.Crossed, 9, 10, 10, 10, MotionDir.East, 0), events[0]);
        Assert.Equal(MotionEventKind.Contact, events[1].Kind);
        Assert.Equal(7, events[1].OtherId);
    }

    [Fact]
    public void TheBridgeKeepsAFastPlayerBehindAStandingPlayer()
    {
        // the whole chain: tg's slowdown of 0.5 ds (a tile per tick) and the largest speed scale
        foreach ((float slowdown, float scale) in new[] { (0.5f, 1f), (1f, 1f), (1.5f, 2f), (1.5f, 4f) })
        {
            var r = new T2Room(401);
            LocomotionSeam.SetSpeedScale(r.World, scale);
            mob_living_carbon_human b = r.Dummy(10, 5, name: "other");
            mob_living_carbon_human a = r.Join(4, 5, "Alice", slowdown, "mover");
            for (int tick = 0; tick < 30; tick++)
            {
                r.Go(a, 127, 0, T2Room.Yaw(EAST));
                Assert.True(r.State(a).X <= r.State(b).X, $"slowdown {slowdown}, scale {scale}, tick {tick}: the player is past the other one ({r.State(a).X} > {r.State(b).X})");
                r.AssertOnLoc(a, $"slowdown {slowdown}, scale {scale}, tick {tick}");
            }
            Assert.InRange(r.State(b).X - r.State(a).X, 90, 92);
            r.AssertNoRuntimes();
        }
    }
}
