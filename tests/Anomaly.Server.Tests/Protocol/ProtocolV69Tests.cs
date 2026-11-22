using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 69 (DV347, docs/ai/specs/protocol.md section 70): the own motion's parameters end with the running conveyor belts around the
/// mob (<see cref="BeltField"/>), which round-trip; a field without a speed, a speed without a field's place or a tile code past west is refused.
/// The capsule step rides a belt: along it at its speed with keys or not, toward its middle line with none, and not at all off it.
/// </summary>
public class ProtocolV69Tests
{
    private static BeltField Row(int x, int y, short speed) => new BeltField(0, 0, 0, 0, x, y, speed)
        .With(0, 0, MotionDir.East).With(1, 0, MotionDir.East).With(2, 0, MotionDir.North).With(-4, -4, MotionDir.West).With(4, 4, MotionDir.South);

    private static Delta With(MotionParams p) => new() { Tick = 4, Own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(40, 40, 1), Params = p } };

    [Fact]
    public void TheBeltFieldRoundTripsAndABadOneIsRefused()
    {
        Assert.True(ProtocolVersion.Current >= 69);
        BeltField row = Row(40, 40, 64);
        Assert.Equal(MotionDir.East, row.DirAt(41, 40));
        Assert.Equal(MotionDir.North, row.DirAt(42, 40));
        Assert.Equal(MotionDir.West, row.DirAt(36, 36));
        Assert.Equal(MotionDir.South, row.DirAt(44, 44));
        Assert.Equal(MotionDir.None, row.DirAt(45, 40)); // outside the 9 x 9 tiles
        foreach (BeltField belt in new[] { default, row })
        {
            MotionParams p = MotionParams.DefaultRun with { Belt = belt };
            Assert.True(Codec.TryDecode(Codec.Encode(With(p)), out Delta back, out ProtocolError error), error.ToString());
            Assert.Equal(p, back.Own.Params);
        }
        foreach (BeltField bad in new[] { row with { Speed = 0 }, row with { W0 = row.W0 | 5 }, row with { W3 = row.W3 | (1UL << 60) }, row with { Speed = -1 } })
            Assert.Throws<ProtocolException>(() => Codec.Encode(With(MotionParams.DefaultRun with { Belt = bad })));
    }

    [Fact]
    public void A_capsule_on_a_running_belt_rides_it_and_off_it_stands()
    {
        var view = new FlatView();
        MotionParams p = MotionParams.DefaultRun with { Belt = Row(40, 40, 64) };
        // idle on the east belt a little south of its line: a quarter tile east a tick, and up toward the line at half that
        MotionState s = MotionState.AtTile(40, 40, 1);
        s.Y -= 40;
        MotionStep.Step(ref s, MotionInput.Idle(1, 0), in p, view, null);
        Assert.Equal((40 * 256 + 64, 40 * 256 - 40 + 32), (s.X, s.Y));
        // a key held across the belt is not held back toward the line
        MotionState walking = MotionState.AtTile(40, 40, 1);
        MotionState alone = walking;
        var north = new MotionInput(1, 0, MotionInput.MaxMove, 0, MotionInputFlags.None);
        MotionStep.Step(ref walking, in north, in p, view, null);
        MotionStep.Step(ref alone, in north, MotionParams.DefaultRun, view, null);
        Assert.Equal((alone.X + 64, alone.Y), (walking.X, walking.Y));
        // off the belts (and with no field) nothing moves
        MotionState off = MotionState.AtTile(40, 43, 1);
        MotionStep.Step(ref off, MotionInput.Idle(1, 0), in p, view, null);
        Assert.Equal((40 * 256, 43 * 256), (off.X, off.Y));
    }

    // A rider a belt holds against a wall gathers its own speed there (DV263's stop would take it back every tick) and walks off against the belt.
    [Fact]
    public void A_rider_a_belt_pins_to_a_wall_runs_off_against_it()
    {
        var view = new FlatView { WallX = 41 };
        short speed = 32; // a tile per 0.2 s at MOVE_SPEED_SCALE 0.5
        MotionParams p = MotionParams.DefaultRun with
        {
            TilesPerTick = 1f / 6f, // running at the same scale
            Feel = new MoveFeel(4, 3, 0f, 0f),
            Belt = new BeltField(0, 0, 0, 0, 40, 40, speed).With(0, 0, MotionDir.East),
        };
        MotionState s = MotionState.AtTile(40, 40, 1);
        for (int i = 0; i < 10; i++)
            MotionStep.Step(ref s, MotionInput.Idle(1, 0), in p, view, null);
        int pinned = s.X;
        Assert.True(pinned > 40 * 256, "the belt carried the capsule to the wall");
        var west = new MotionInput(1, -MotionInput.MaxMove, 0, 0, MotionInputFlags.None);
        for (int i = 0; i < 10; i++)
            MotionStep.Step(ref s, in west, in p, view, null);
        Assert.True(s.X <= pinned - 6 * (43 - speed), $"the runner left the wall against the belt (from {pinned} to {s.X})");
    }

    private sealed class FlatView : ICollisionView
    {
        public int WallX { get; init; } = int.MaxValue;
        public bool IsSolid(int x, int y, int z) => z != 1 || x == WallX;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) { }
    }
}
