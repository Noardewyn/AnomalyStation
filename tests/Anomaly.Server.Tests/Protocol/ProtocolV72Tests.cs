using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 72 (DV357): the own motion's parameters end with the space wind around the mob (a <see cref="BeltField"/> of wind
/// directions and its push speed) and a gravity pull's tile and speed, which round-trip; a pull's tile without a speed is refused. The
/// capsule step is carried by both, keys or not, and a walker's own wish is added, so it leans against a wind weaker than its speed.
/// </summary>
public class ProtocolV72Tests
{
    private static Delta With(MotionParams p) => new() { Tick = 4, Own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(40, 40, 1), Params = p } };

    private static BeltField SouthWind(short speed) => new BeltField(0, 0, 0, 0, 40, 40, speed).With(0, 0, MotionDir.South).With(0, 1, MotionDir.South).With(0, -1, MotionDir.South);

    [Fact]
    public void TheWindAndThePullRoundTripAndABadOneIsRefused()
    {
        Assert.True(ProtocolVersion.Current >= 72);
        foreach (MotionParams p in new[]
        {
            MotionParams.DefaultRun,
            MotionParams.DefaultRun with { Wind = SouthWind(51) },
            MotionParams.DefaultRun with { PullX = 45, PullY = 38, PullSpeed = 26 },
            MotionParams.DefaultRun with { Wind = SouthWind(102), PullX = 1, PullY = 1, PullSpeed = 1, Belt = SouthWind(10) },
        })
        {
            Assert.True(Codec.TryDecode(Codec.Encode(With(p)), out Delta back, out ProtocolError error), error.ToString());
            Assert.Equal(p, back.Own.Params);
        }
        foreach (MotionParams bad in new[]
        {
            MotionParams.DefaultRun with { PullX = 45, PullY = 38 },
            MotionParams.DefaultRun with { Wind = SouthWind(0) },
            MotionParams.DefaultRun with { Wind = SouthWind(-1) },
            MotionParams.DefaultRun with { PullX = 45, PullY = 38, PullSpeed = -1 },
        })
            Assert.Throws<ProtocolException>(() => Codec.Encode(With(bad)));
    }

    [Fact]
    public void The_wind_carries_an_idle_capsule_and_a_walker_leans_against_it()
    {
        var view = new FlatView();
        MotionParams p = MotionParams.DefaultRun with { Wind = SouthWind(51) }; // a fifth of a tile a tick
        MotionState idle = MotionState.AtTile(40, 40, 1);
        MotionStep.Step(ref idle, MotionInput.Idle(1, 0), in p, view, null);
        Assert.Equal((40 * 256, 40 * 256 - 51), (idle.X, idle.Y));
        // running north against it: its own speed less the wind's
        var north = new MotionInput(1, 0, MotionInput.MaxMove, 0, MotionInputFlags.None);
        MotionState walking = MotionState.AtTile(40, 40, 1), alone = walking;
        MotionStep.Step(ref walking, in north, in p, view, null);
        MotionStep.Step(ref alone, in north, MotionParams.DefaultRun, view, null);
        Assert.Equal(alone.Y - 51, walking.Y);
        Assert.True(walking.Y > 40 * 256, "the runner gains on a wind weaker than its speed");
        // a calm tile in the field does not blow
        MotionState calm = MotionState.AtTile(42, 40, 1);
        MotionStep.Step(ref calm, MotionInput.Idle(1, 0), in p, view, null);
        Assert.Equal((42 * 256, 40 * 256), (calm.X, calm.Y));
    }

    [Fact]
    public void A_pull_draws_the_capsule_to_its_tile_centre_and_stops_there()
    {
        var view = new FlatView();
        MotionParams p = MotionParams.DefaultRun with { PullX = 43, PullY = 44, PullSpeed = 26 };
        MotionState s = MotionState.AtTile(40, 40, 1);
        MotionStep.Step(ref s, MotionInput.Idle(1, 0), in p, view, null);
        Assert.Equal((40 * 256 + 26 * 3 / 5, 40 * 256 + 26 * 4 / 5), (s.X, s.Y)); // along the 3-4-5 line
        for (int i = 0; i < 100; i++)
            MotionStep.Step(ref s, MotionInput.Idle(1, 0), in p, view, null);
        Assert.Equal((43 * 256, 44 * 256), (s.X, s.Y));
    }

    // A walker the wind pins to a wall gathers its own speed there (DV263's stop would take it back every tick) and runs off against it.
    [Fact]
    public void A_walker_the_wind_pins_to_a_wall_runs_off_against_it()
    {
        var view = new FlatView { WallX = 41 };
        short speed = 32;
        MotionParams p = MotionParams.DefaultRun with
        {
            TilesPerTick = 1f / 6f,
            Feel = new MoveFeel(4, 3, 0f, 0f),
            Wind = new BeltField(0, 0, 0, 0, 40, 40, speed).With(0, 0, MotionDir.East),
        };
        MotionState s = MotionState.AtTile(40, 40, 1);
        for (int i = 0; i < 10; i++)
            MotionStep.Step(ref s, MotionInput.Idle(1, 0), in p, view, null);
        int pinned = s.X;
        Assert.True(pinned > 40 * 256, "the wind carried the capsule to the wall");
        var west = new MotionInput(1, -MotionInput.MaxMove, 0, 0, MotionInputFlags.None);
        for (int i = 0; i < 10; i++)
            MotionStep.Step(ref s, in west, in p, view, null);
        Assert.True(s.X <= pinned - 6 * (43 - speed), $"the runner left the wall against the wind (from {pinned} to {s.X})");
    }

    private sealed class FlatView : ICollisionView
    {
        public int WallX { get; init; } = int.MaxValue;
        public bool IsSolid(int x, int y, int z) => z != 1 || x == WallX;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) { }
    }
}
