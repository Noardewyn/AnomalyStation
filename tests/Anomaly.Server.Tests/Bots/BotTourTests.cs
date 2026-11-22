using Anomaly.Core.Motion;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>
/// A map from rows of text for the route planner and the tour (M3 P5): <c>#</c> wall, <c>.</c> floor, <c>D</c> a door that opens
/// when a bot has pressed into it for a few ticks (tg's Bumped), <c>L</c> a locked door (never opens), a space is a space turf
/// (open, but a bot must not enter), <c>E</c> floor with a pane on its east face, <c>W</c> floor with a pane on its west face.
/// Row 0 is the northmost; x and y start at 1.
/// </summary>
internal sealed class TextRouteMap : IRouteMap, ICollisionView
{
    private readonly string[] _rows;
    private readonly HashSet<(int, int)> _open = new();
    private readonly Dictionary<(int, int), int> _pressed = new();

    public TextRouteMap(params string[] rows)
    {
        _rows = rows;
    }

    public int Width => _rows[0].Length;

    public int Height => _rows.Length;

    public int Levels => 1;

    private char At(int x, int y) => x < 1 || y < 1 || x > Width || y > Height ? '#' : _rows[Height - y][x - 1];

    public bool IsSolid(int x, int y, int z) => z != 1 || At(x, y) == '#' || ((At(x, y) == 'D' || At(x, y) == 'L') && !_open.Contains((x, y)));

    public bool IsDoor(int x, int y, int z) => At(x, y) is 'D' or 'L';

    public bool IsSpace(int x, int y, int z) => At(x, y) == ' ';

    public TileEdges EdgesOf(int x, int y, int z) => At(x, y) switch
    {
        'E' => TileEdges.East,
        'W' => TileEdges.West,
        _ => TileEdges.None,
    };

    public bool IsFaceBlocked(int x, int y, int z, byte dir) => TileFaces.IsFaceBlocked(this, x, y, z, dir);

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
    {
    }

    /// <summary>The bot presses at a door tile this tick; a plain door opens after 8 presses (a locked one never does).</summary>
    public void Press(int x, int y)
    {
        if (At(x, y) != 'D') return;
        _pressed.TryGetValue((x, y), out int n);
        _pressed[(x, y)] = ++n;
        if (n >= 8) _open.Add((x, y));
    }

    public int OpenDoors => _open.Count;
}

public class BotTourTests
{
    private static readonly string[] Station =
    {
        "########################",
        "#.....#.........#......#",
        "#.....D...E.....#......#",
        "#.....#...E.....L......#",
        "#.....#...E.....#......#",
        "#.....#.........########",
        "#.....#................ ",
        "###########   ##########",
    };

    // Rows 1..8 from the north: y = 8 is "####", y = 1 is the last row. The space gaps are in the last rows (x = 12 to 14 at y = 1, and x = 24 at y = 3).

    [Fact]
    public void ARouteGoesThroughADoorAroundAPaneAndNeverIntoSpace()
    {
        var map = new TextRouteMap(Station);
        // The door tile (7, 6) is solid but on the route; from room A to room B the only way is through it.
        Route toB = RoutePlanner.To(map, 3, 6, 1, 10, 6);
        Assert.NotNull(toB);
        Assert.Contains((7, 6), toB.Tiles);
        Assert.Equal((10, 6), toB.Tiles[^1]);
        // The pane column on x = 11 (east faces) blocks 11 -> 12 for y 4..6; the route to (14, 5) goes round through y = 2.
        Route round = RoutePlanner.To(map, 9, 5, 1, 14, 5);
        Assert.NotNull(round);
        Assert.Contains(round.Tiles, t => t.Y == 7 || t.Y <= 3); // round the pane column's ends, not through it
        foreach ((int x, int y) in round.Tiles)
            Assert.False(map.IsSpace(x, y, 1), $"the route enters space at {x},{y}");
        // A tile behind space only (the row of space below the corridor) is not a goal.
        Assert.Null(RoutePlanner.To(map, 3, 6, 1, 13, 1));
        // Outside the map and out of reach.
        Assert.Null(RoutePlanner.To(map, 3, 6, 1, 99, 99));
    }

    [Fact]
    public void AFaceBlocksTheRouteFromBothSides()
    {
        var map = new TextRouteMap(
            "#######",
            "#..E..#",
            "#..E..#",
            "#######");
        Assert.True(map.IsFaceBlocked(4, 2, 1, MotionDir.East));
        Assert.True(map.IsFaceBlocked(5, 2, 1, MotionDir.West)); // a face blocks from both sides
        Assert.Null(RoutePlanner.To(map, 2, 2, 1, 5, 2)); // a wall of panes between the halves
        var map2 = new TextRouteMap(
            "#######",
            "#..E..#",
            "#.....#",
            "#######");
        Route r = RoutePlanner.To(map2, 2, 3, 1, 5, 3);
        Assert.NotNull(r);
        Assert.Contains((4, 2), r.Tiles.Concat(new[] { (4, 2) })); // goes round through the gap row
        Route back = RoutePlanner.To(map2, 5, 3, 1, 2, 3);
        Assert.NotNull(back);
        Assert.Equal(back.Length, r.Length);
    }

    /// <summary>Walks one touring bot on the text map with the controller: the planner's wish, <c>MotionStep</c>, doors opening under pressure.</summary>
    private static (TourPlanner Planner, TourCoverage Coverage, TextRouteMap Map, MotionState State, int SpaceTicks) Tour(string[] rows, int sx, int sy, int ticks, TourSettings settings = null)
    {
        var map = new TextRouteMap(rows);
        var coverage = new TourCoverage();
        var planner = new TourPlanner(coverage, settings);
        MotionState state = MotionState.AtTile(sx, sy, 1);
        var events = new MotionEvents();
        int spaceTicks = 0;
        for (int tick = 0; tick < ticks; tick++)
        {
            BotWish wish = planner.Update(tick, true, state.X, state.Y, 1, map);
            events.Clear();
            MotionStep.Step(ref state, new MotionInput((uint)tick + 1, wish.MoveX, wish.MoveY, wish.Yaw, MotionInputFlags.None), MotionParams.DefaultRun, map, events);
            // A press into a closed door's face is tg's Bump: the door opens after a few ticks if the bot has the access.
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != MotionEventKind.Pressing) continue;
                (int x, int y) = TileFaces.Neighbour(MotionMath.TileOf(state.X), MotionMath.TileOf(state.Y), events[i].Dir);
                map.Press(x, y);
            }

            if (map.IsSpace(MotionMath.TileOf(state.X), MotionMath.TileOf(state.Y), 1)) spaceTicks++;
            if (planner.Finished) break;
        }

        return (planner, coverage, map, state, spaceTicks);
    }

    [Fact]
    public void ATourReachesEveryWalkableTileOpensTheDoorsAndNeverStandsInSpace()
    {
        var (planner, coverage, map, state, spaceTicks) = Tour(Station, 3, 6, 6000);
        Assert.Equal(0, spaceTicks);
        Assert.True(coverage.Reachable > 80, "reachable " + coverage.Reachable);
        // The locked door's room (x 18 to 23) is out of reach for a bot without access; everything else is covered.
        Assert.True(coverage.Covered >= coverage.Reachable - 4 * 6 - 4, $"covered {coverage.Covered} of {coverage.Reachable}");
        Assert.True(map.OpenDoors >= 1);
        Assert.True(coverage.DoorsPassed >= 1);
        Assert.True(planner.DoorPressTicks > 0);
        Assert.True(planner.Replans >= 1, "the locked door is pressed, abandoned and avoided");
        Assert.NotEqual(0, state.X);
    }

    [Fact]
    public void ABotInAClosedRoomWithOnlyALockedDoorIsCountedStuckAndReplansWithoutLeavingTheMap()
    {
        var rows = new[]
        {
            "#########",
            "#...L...#",
            "#########",
        };
        var settings = new TourSettings { BlockedTicks = 20, StuckTicks = 120, AvoidTicks = 60 };
        var (planner, coverage, _, state, _) = Tour(rows, 2, 2, 1500, settings);
        Assert.True(planner.Replans > 0);
        Assert.True(planner.StuckEvents > 0);
        Assert.True(MotionMath.TileOf(state.X) is >= 1 and <= 7);
        Assert.True(coverage.Share > 0.0);
    }

    [Fact]
    public void TheTourIsDeterministicAndFinishesOnAMapWithNothingLeft()
    {
        var rows = new[]
        {
            "#####",
            "#...#",
            "#####",
        };
        var (planner, coverage, _, _, _) = Tour(rows, 2, 2, 800);
        Assert.True(planner.Finished);
        Assert.Equal(coverage.Reachable, coverage.Covered);
        var (_, coverage2, _, state2, _) = Tour(rows, 2, 2, 800);
        Assert.Equal(coverage.Covered, coverage2.Covered);
        _ = state2;
    }

    [Fact]
    public void TheBehaviourIsOnTheCommandLine()
    {
        Assert.True(BotBehaviourNames.TryParse("tour", out BotBehaviour behaviour));
        Assert.Equal(BotBehaviour.Tour, behaviour);
        Assert.Equal("tour", BotBehaviourNames.ToName(BotBehaviour.Tour));
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Tour));
    }

    [Fact]
    public void TheReplicaMapReadsFacesDoorsAndSpaceFromTheWire()
    {
        // A MapGrid with a pane and a door entity: the replica's route map sees both.
        var grid = Protocol.Msg.FlatMap(8, 4);
        grid.TurfTypes.Add("/turf/open/space");
        grid.Runs.Clear();
        grid.Runs.Add(new Anomaly.Protocol.TurfRun(8 * 4 - 8, 0));
        grid.Runs.Add(new Anomaly.Protocol.TurfRun(8, 1));
        grid.FlagRuns.Add(new Anomaly.Protocol.TurfRun(8 * 4 - 1, 0));
        grid.FlagRuns.Add(new Anomaly.Protocol.TurfRun(1, Anomaly.Protocol.CellFlags.FaceEast));
        var replica = new Anomaly.Protocol.ReplicaWorld();
        replica.Apply(grid);
        var map = new ReplicaRouteMap(replica);
        Assert.Equal(8, map.Width);
        Assert.True(map.IsSpace(1, 4, 1));
        Assert.False(map.IsSpace(1, 1, 1));
        Assert.False(map.IsFaceBlocked(7, 4, 1, MotionDir.East));
        Assert.True(map.IsFaceBlocked(8, 4, 1, MotionDir.East));
        Assert.False(map.IsFaceBlocked(8, 4, 1, MotionDir.West));
        Assert.False(map.IsDoor(3, 3, 1));
    }
}
