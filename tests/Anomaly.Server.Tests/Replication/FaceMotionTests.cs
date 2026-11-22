using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// Face blocking in motion (locomotion.md section 4, M3): the server's controller over <c>LocomotionSeam</c>'s view and a
/// predictor over the replica's view, both stepping the same inputs on a hand-made world with border windows and a windoor,
/// end every tick in the same <see cref="MotionState"/> (D103: bit-equal).
/// </summary>
public class FaceMotionTests
{
    private static obj Border(V2Run run, int x, int y, float dir, string name)
    {
        return run.In(() =>
        {
            var o = Dm.@new<obj>(run.Turf(x, y));
            o.flags_1 = (o.flags_1 ?? 0) + ON_BORDER_1;
            o.density = 1;
            o.anchored = 1; // a built window is anchored: an unanchored pane is pushed by the walker's bump
            o.dir = dir;
            o.name = name;
            return o;
        });
    }

    private const ushort North = 0, East = 0x4000, South = 0x8000, West = 0xC000; // the yaw (a compass bearing); the wish vector is world-space: MoveX east, MoveY north

    private static bool Same(in MotionState a, in MotionState b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.Yaw == b.Yaw && a.Posture == b.Posture && a.Driver == b.Driver && a.PressTimer == b.PressTimer && a.Slide == b.Slide
        && a.VelX == b.VelX && a.VelY == b.VelY;

    /// <summary>Walks one human along a route; returns the tiles it reached. The predictor is stepped with the replica's view each tick.</summary>
    private static (List<(int X, int Y)> Tiles, int Ticks) Drive(V2Run run, SessionView observer, mob_living human, sbyte mx, sbyte my, int ticks, ushort yaw)
    {
        var events = new MotionEvents();
        var path = new List<(int X, int Y)>();
        uint seq = 0;
        // Tg's move delay (3 ticks between two crossings) is the server's gate, not the controller's: a leg starts after it has run out,
        // and the legs are short enough that two crossings of one leg are far enough apart.
        for (int i = 0; i < 4; i++)
        {
            run.In(() => LocomotionSeam.SetFrame(human, new MotionInput(++seq, 0, 0, yaw, MotionInputFlags.None)));
            run.Step();
        }

        Assert.True(LocomotionSeam.TryGetState(run.World.World, human.dm_id, out MotionState predicted, out _));
        for (int i = 0; i < ticks; i++)
        {
            Assert.True(LocomotionSeam.TryGetState(run.World.World, human.dm_id, out _, out MotionParams parameters));
            var input = new MotionInput(++seq, mx, my, yaw, MotionInputFlags.None);
            run.In(() => LocomotionSeam.SetFrame(human, input));
            run.Step();
            events.Clear();
            MotionStep.Step(ref predicted, input, parameters, observer.Replica.CollisionView, events, human.dm_id);
            Assert.True(LocomotionSeam.TryGetState(run.World.World, human.dm_id, out MotionState server, out _));
            Assert.True(Same(server, predicted), $"tick {i}: server {server.X},{server.Y} predictor {predicted.X},{predicted.Y}");
            (int, int) tile = (MotionMath.TileOf(server.X), MotionMath.TileOf(server.Y));
            if (path.Count == 0 || path[^1] != tile) path.Add(tile);
        }

        return (path, ticks);
    }

    private static (V2Run Run, SessionView Observer, mob_living_carbon_human Human) Fixture()
    {
        V2Run run = V2Run.Quiet(size: 16, cellSweep: 100000);
        run.In(() =>
        {
            LocomotionSeam.Install(run.World.World);
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_run_delay), 1.5f));
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_walk_delay), 4f));
        });
        // Border windows before the join: a pane facing west on (8,5) (the west face of tile 8,5 is blocked: entering from tile 7 is refused),
        // a pane facing east on (11,5), a pane facing south on (4,9) and a windoor-like one (a name, dir north) on (4,12).
        Border(run, 8, 5, Dm.WEST, "window");
        Border(run, 11, 5, Dm.EAST, "window");
        Border(run, 4, 9, Dm.SOUTH, "window");
        Border(run, 4, 12, Dm.NORTH, "windoor");
        run.Step();
        mob_living_carbon_human human = run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(5, 5)));
        SessionView session = run.Connect("Walker", human);
        run.In(() => LocomotionSeam.Register(human));
        run.Step();
        return (run, session, human);
    }

    [Fact]
    public void AWindowFacingTheWalkerStopsItOnBothSidesAndThePredictorAgreesEveryTick()
    {
        var (run, observer, human) = Fixture();
        // East along y=5 from x=5: the pane of (8,5) blocks its west face, so the walker stops on tile 7.
        var (path, _) = Drive(run, observer, human, 127, 0, 40, East);
        Assert.Equal((7, 5), path[^1]);
        Assert.DoesNotContain((8, 5), path);
        // The face is the invariant the contracts pin: blocked from tile 7 toward 8, from tile 8 toward 7.
        ICollisionView view = LocomotionSeam.CollisionView(run.World.World);
        Assert.True(TileFaces.IsFaceBlocked(view, 7, 5, 1, MotionDir.East));
        Assert.True(TileFaces.IsFaceBlocked(view, 8, 5, 1, MotionDir.West));
        Assert.True(TileFaces.IsFaceBlocked(observer.Replica.CollisionView, 7, 5, 1, MotionDir.East));
        Assert.False(TileFaces.IsFaceBlocked(view, 7, 5, 1, MotionDir.North));
        Assert.False(LocomotionSeam.IsSolid(run.World.World, 8, 5, 1)); // a face, not the tile
    }

    [Fact]
    public void AWalkerGoesAroundThePaneAndAlongItsFaceUnhindered()
    {
        var (run, observer, human) = Fixture();
        // (5,5) east to (7,5), where the pane of (8,5) faces west; north to row 6, east along row 6 past the pane's tile (its north
        // face is free and the pane is on row 5), then back south along column 12 past the pane of (11,5), which faces east
        // (a plain obj stands in for the window, and tg's own CanAllowThrough for entering a pane's tile is P4's).
        Drive(run, observer, human, 127, 0, 20, East);
        var (up, _) = Drive(run, observer, human, 0, 127, 4, North);
        int row = up[^1].Y;
        Assert.True(row > 5);
        var (along, _) = Drive(run, observer, human, 127, 0, 20, East);
        Assert.Contains((8, row), along);
        Assert.Contains((12, row), along);
        Assert.Equal((14, row), along[^1]); // the room's east wall is column 16 (a 16 tile room); the capsule stops on tile 14
    }

    [Fact]
    public void ADeltaThatBuildsAWindowIsAFaceTheNextTickOnTheServerAndInTheReplica()
    {
        var (run, observer, human) = Fixture();
        var (before, _) = Drive(run, observer, human, 0, 127, 6, North);
        int tileY = before[^1].Y;
        int x = before[^1].X;
        Assert.Equal(TileEdges.None, observer.Replica.FacesOf(x, tileY + 3, 1));
        Border(run, x, tileY + 3, Dm.SOUTH, "window"); // built after the join: a CellFlagsChanged
        run.Step();
        Assert.Equal(TileEdges.South, observer.Replica.FacesOf(x, tileY + 3, 1));
        Assert.Contains(Wire.DecodeDelta(run.LastFrame).CellFlagsChanged, c => c.X == x && c.Y == tileY + 3 && c.Flags == CellFlags.FaceSouth);
        var (after, _) = Drive(run, observer, human, 0, 127, 20, North);
        Assert.Equal((x, tileY + 2), after[^1]);
        Assert.DoesNotContain((x, tileY + 3), after);
    }

    [Fact]
    public void ARandomWalkAcrossTheFixtureNeverSeparatesTheServerFromThePredictor()
    {
        var (run, observer, human) = Fixture();
        var rng = new Random(2026);
        for (int leg = 0; leg < 30; leg++)
        {
            sbyte mx = (sbyte)rng.Next(-1, 2), my = (sbyte)rng.Next(-1, 2);
            ushort yaw = (ushort)rng.Next(65536);
            Drive(run, observer, human, mx, my, rng.Next(2, 14), yaw);
        }
    }
}
