using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R3 (D121, the owner's second walk: "some doors and airlocks are turned the wrong way"): a door stands across the passage it closes. The rule scores both axes
/// (<see cref="DoorAxis"/>); these are the cases the lead named, the M2 rooms, and MetaStation itself.
/// </summary>
public sealed class WalkFix2DoorTests : WalkFixBase
{
    private const byte North = ByondDirConst.North, East = ByondDirConst.East;

    public WalkFix2DoorTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ARowOfFirelocksAcrossAThreeWideHallEachStandsAcrossIt()
    {
        // A hall of three tiles' width running east-west with a firelock in each of its three cells at x = 6: the plane of each runs along y (EAST). The old rule
        // saw no wall pair (the cells east and west of each are open floor) and fell back to NORTH: "a row of frames turned along the hall".
        var world = new AsciiWorld(new[]
        {
            "###########",
            ".....F.....",
            ".....F.....",
            ".....F.....",
            "###########",
        });
        foreach (int y in new[] { 2, 3, 4 })
            Assert.Equal(East, world.Piece(6, y, RenderKind.Firelock).Face);
        // A row of firelocks along a hall that runs north-south closes it with planes along x.
        var tall = new AsciiWorld(new[]
        {
            "#.#.#",
            "#...#",
            "#FFF#",
            "#...#",
            "#...#",
        });
        foreach (int x in new[] { 2, 3, 4 })
            Assert.Equal(North, tall.Piece(x, 3, RenderKind.Firelock).Face);
    }

    [Fact]
    public void TheFacesDoNotDependOnTheOrderTheDoorsAreListedIn()
    {
        var world = new AsciiWorld(new[]
        {
            "###########",
            ".....F.....",
            ".....F.....",
            ".....F.....",
            "###########",
        });
        // Planned from the entities in the reverse order, the faces are the same: the rule works on the cells, in a fixed order.
        WorldPlan reversed = ChunkPlanner.Plan(world.Grid, null, world.Entities.AsEnumerable().Reverse().ToList());
        Assert.Equal(
            world.Plan.Chunks.SelectMany(c => c.Placed).OrderBy(p => p.EntityId).Select(p => (p.EntityId, p.Face)),
            reversed.Chunks.SelectMany(c => c.Placed).OrderBy(p => p.EntityId).Select(p => (p.EntityId, p.Face)));
    }

    [Fact]
    public void AnAirlockBetweenTwoDenseTraysStandsAcrossTheWayBetweenThem()
    {
        // Trays east and west of the airlock (a dense machine that is not a wall): the way through is north-south, the plane runs along x.
        var eastWest = new AsciiWorld(new[]
        {
            ".......",
            "..TAT..",
            ".......",
        });
        Assert.Equal(North, eastWest.Piece(4, 2, RenderKind.Airlock).Face);
        // Trays north and south: the way through is east-west, the plane runs along y.
        var northSouth = new AsciiWorld(new[]
        {
            ".......",
            "...T...",
            "...A...",
            "...T...",
            ".......",
        });
        Assert.Equal(East, northSouth.Piece(4, 3, RenderKind.Airlock).Face);
    }

    [Fact]
    public void AnAirlockInAWindowGapStandsInTheGap()
    {
        var eastWest = new AsciiWorld(new[]
        {
            ".......",
            "..WAW..",
            ".......",
        });
        Assert.Equal(North, eastWest.Piece(4, 2, RenderKind.Airlock).Face);
        var northSouth = new AsciiWorld(new[]
        {
            ".......",
            "...W...",
            "...A...",
            "...W...",
            ".......",
        });
        Assert.Equal(East, northSouth.Piece(4, 3, RenderKind.Airlock).Face);
        // A grille beside it blocks as well.
        var grilles = new AsciiWorld(new[]
        {
            ".......",
            "..GAG..",
            ".......",
        });
        Assert.Equal(North, grilles.Piece(4, 2, RenderKind.Airlock).Face);
    }

    [Fact]
    public void ADoubleAirlockStandsTheSameWayOnBothDoors()
    {
        // Two airlocks one behind the other in a north-south passage between walls: both plane along x.
        var passage = new AsciiWorld(new[]
        {
            "#.#",
            "#A#",
            "#A#",
            "#.#",
        });
        Assert.Equal(North, passage.Piece(2, 2, RenderKind.Airlock).Face);
        Assert.Equal(North, passage.Piece(2, 3, RenderKind.Airlock).Face);
        // And in an east-west passage between walls north and south: both along y.
        var corridor = new AsciiWorld(new[]
        {
            "#####",
            "..AA.",
            "#####",
        });
        Assert.Equal(East, corridor.Piece(3, 2, RenderKind.Airlock).Face);
        Assert.Equal(East, corridor.Piece(4, 2, RenderKind.Airlock).Face);
    }

    [Fact]
    public void ADoorInAWallStandsAcrossItAndTheOldRulesCasesStillHold()
    {
        // The M1 and M2 case: walls east and west of the door, the plane runs along x; walls north and south, along y.
        var north = new AsciiWorld(new[]
        {
            "...",
            "#A#",
            "...",
        });
        Assert.Equal(North, north.Piece(2, 2, RenderKind.Airlock).Face);
        var east = new AsciiWorld(new[]
        {
            ".#.",
            ".A.",
            ".#.",
        });
        Assert.Equal(East, east.Piece(2, 2, RenderKind.Airlock).Face);
        // A pod door and a shutter are doors too.
        var pod = new AsciiWorld(new[]
        {
            ".#.",
            ".P.",
            ".#.",
        });
        Assert.Equal(East, pod.Piece(2, 2, RenderKind.PodDoor).Face);
    }

    [Fact]
    public void ADoorInACornerIsDecidedByWhatLiesFurtherAlongTheAxes()
    {
        // All four neighbours open: a tie that the near cells cannot break. Walls two and three cells away east and west make the way north-south: NORTH.
        var narrowEastWest = new AsciiWorld(new[]
        {
            ".......",
            ".......",
            "#..A..#",
            ".......",
            ".......",
        });
        Assert.Equal(North, narrowEastWest.Piece(4, 3, RenderKind.Airlock).Face);
        // The same turned: walls two cells away north and south make the way east-west: EAST.
        var narrowNorthSouth = new AsciiWorld(new[]
        {
            "#######",
            ".......",
            "...A...",
            ".......",
            "#######",
        });
        Assert.Equal(East, narrowNorthSouth.Piece(4, 3, RenderKind.Airlock).Face);
        // A door alone in the open with nothing to decide falls back to NORTH, as before.
        var open = new AsciiWorld(new[]
        {
            ".........",
            ".........",
            ".........",
            "....A....",
            ".........",
            ".........",
            ".........",
        });
        Assert.Equal(North, open.Piece(5, 4, RenderKind.Airlock).Face);
    }

    [Fact]
    public void ATiedDoorBesideADecidedDoorSharesItsAxis()
    {
        // The door at (3, 3) has walls north and south of it: its plane runs along y (EAST). The door at (4, 3) has a wall north, open floor south and east and the
        // door west: a tie of the near scores (2 to 2). The neighbouring door breaks it; without it the far scores tie as well and the door falls back to NORTH.
        var world = new AsciiWorld(new[]
        {
            "......",
            "..##..",
            "..AA..",
            "..#...",
            "......",
        });
        Assert.Equal(East, world.Piece(3, 3, RenderKind.Airlock).Face);
        Assert.Equal(East, world.Piece(4, 3, RenderKind.Airlock).Face);
        // Placed alone (a later spawn that knows no neighbour's face), the same door would stand NORTH.
        Assert.True(ChunkPlanner.TryPlace(new PlanEntity(9, AsciiWorld.Airlock, 4, 3, 1, 2, Anomaly.Protocol.TableRef.Public(9), 0, 0, Anomaly.Protocol.LightState.None, null),
            (x, y, z) => (x, y) is (3, 4) or (4, 4) or (3, 2), (x, y, z) => (x, y) is (3, 4) or (4, 4) or (3, 2) or (3, 3), out Placed alone));
        Assert.Equal(North, alone.Face);
    }

    [Fact]
    public void ADoorsFaceFromTheScoresAndTheTieBreaksAreTestedDirectly()
    {
        // The pure function on a hand-made neighbourhood: a tie from walls north and door west, open east and south.
        CellClass Classify(int x, int y, int z) => (x, y) switch
        {
            (5, 6) => CellClass.Blocking, // north: a wall
            (4, 5) => CellClass.Blocking, // west: the decided door
            _ => CellClass.Open,
        };
        Assert.Equal(0, DoorAxis.Near(5, 5, 1, Classify)); // alongX = west + open south = 2, alongY = north + open east = 2
        byte DecidedWest(int x, int y, int z) => (x, y) == (4, 5) ? East : (byte)0;
        Assert.Equal(East, DoorAxis.Choose(5, 5, 1, Classify, DecidedWest));
        // Without the decided neighbour: the far cells are all open on both axes (a tie), so NORTH.
        Assert.Equal(North, DoorAxis.Choose(5, 5, 1, Classify));
        // The owner's play test (2026-10-16): a door nothing decides takes its own dir's axis (tg's assembly takes its builder's dir, the airlock the assembly's)
        Assert.Equal(East, DoorAxis.Choose(5, 5, 1, Classify, ownDir: 8));
        Assert.Equal(East, DoorAxis.Choose(5, 5, 1, Classify, ownDir: 4));
        Assert.Equal(North, DoorAxis.Choose(5, 5, 1, Classify, ownDir: 2));
        // ... but the walls beside it still decide first
        CellClass Corridor(int x, int y, int z) => x is 4 or 6 ? CellClass.Blocking : CellClass.Open;
        Assert.Equal(DoorAxis.Choose(5, 5, 1, Corridor), DoorAxis.Choose(5, 5, 1, Corridor, ownDir: 8));
        // The batch gives the same answer in whatever order the doors come.
        var doors = new[] { (4, 5, 1), (5, 5, 1) };
        CellClass ClassifyWithWalls(int x, int y, int z) => (x, y) switch
        {
            (4, 6) or (4, 4) => CellClass.Blocking, // the west door has walls north and south of it
            (5, 6) => CellClass.Blocking,
            (4, 5) => CellClass.Blocking,
            _ => CellClass.Open,
        };
        Dictionary<(int X, int Y, int Z), byte> forward = DoorAxis.ResolveAll(doors, ClassifyWithWalls);
        Dictionary<(int X, int Y, int Z), byte> backward = DoorAxis.ResolveAll(doors.Reverse(), ClassifyWithWalls);
        Assert.Equal(East, forward[(4, 5, 1)]);
        Assert.Equal(East, forward[(5, 5, 1)]);
        Assert.Equal(forward.OrderBy(k => k.Key), backward.OrderBy(k => k.Key));
    }

    [Fact]
    public void AWindoorAndABorderFirelockKeepTheirDirFace()
    {
        var world = new AsciiWorld(new[]
        {
            ".....",
            ".....",
            ".....",
        });
        foreach (byte dir in new byte[] { 1, 2, 4, 8 })
        {
            Assert.True(ChunkPlanner.TryPlace(Entity(1, "/obj/machinery/door/window/left", 3, 2, dir), (x, y, z) => x == 2 || x == 4, out Placed windoor));
            Assert.Equal(dir, windoor.Face);
            Assert.True(ChunkPlanner.TryPlace(Entity(2, "/obj/machinery/door/firedoor/border_only", 3, 2, dir), (x, y, z) => x == 2 || x == 4, out Placed border));
            Assert.Equal(dir, border.Face);
        }
        Assert.NotNull(world);
    }

    // ---- MetaStation ----

    [Fact]
    public void EveryDoorOfMetaStandsAcrossItsPassage()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        int doors = 0, decidedByWalls = 0;
        byte[] solid = world.Binding.Grid.SolidBits;
        var doorCells = new HashSet<(int, int)>(world.Map.Entities.Where(e => PropKinds.IsPassageDoor(e.TypePath)).Select(e => (e.X, e.Y)));
        bool Blocked(int x, int y)
        {
            int i = (x - 1) + (y - 1) * world.Map.Width;
            return world.IsWallOrWindow(x, y) || doorCells.Contains((x, y)) || (solid[i >> 3] & (1 << (i & 7))) != 0;
        }
        foreach (Placed p in world.Plan.Chunks.SelectMany(c => c.Placed).Where(p => p.Kind == PlacedKind.Piece && p.Shape is RenderKind.Airlock or RenderKind.PodDoor or RenderKind.Firelock))
        {
            doors++;
            Assert.True(p.Face is North or East, $"({p.X},{p.Y}) {p.Shape} has face {p.Face}");
            // Walls (or windows) at both ends of one axis and nothing blocking both ends of the other: the door spans that axis. This is what the old rule did where it
            // decided at all, and the new one must agree; where both axes are blocked at both ends the door is a tie and takes its neighbour's axis.
            bool ew = world.IsWallOrWindow(p.X + 1, p.Y) && world.IsWallOrWindow(p.X - 1, p.Y);
            bool ns = world.IsWallOrWindow(p.X, p.Y + 1) && world.IsWallOrWindow(p.X, p.Y - 1);
            bool ewBlocked = Blocked(p.X + 1, p.Y) && Blocked(p.X - 1, p.Y), nsBlocked = Blocked(p.X, p.Y + 1) && Blocked(p.X, p.Y - 1);
            if (ew != ns && !(ew ? nsBlocked : ewBlocked))
            {
                decidedByWalls++;
                Assert.True(p.Face == (ew ? North : East), $"({p.X},{p.Y}) {p.Shape} face {p.Face}, walls ew={ew} ns={ns}");
            }
        }
        _output.WriteLine($"{doors} doors, {decidedByWalls} between walls");
        Assert.True(doors > 1000, "Meta has over a thousand doors: " + doors);
        Assert.True(decidedByWalls > 600, "most are in a wall: " + decidedByWalls);
    }

    [Fact]
    public void TheHeavyFirelockLineAcrossTheHallOfMetaStandsAcrossIt()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        // The owner's shot 8 (169.95, 115.37): ten heavy firelocks in a column at x = 173, y 110 to 119, closing a hall that runs east-west. They were turned along the hall.
        for (int y = 110; y <= 119; y++)
        {
            FakeMapEntity door = world.Map.Entities.Single(e => e.X == 173 && e.Y == y && e.TypePath == "/obj/machinery/door/firedoor/heavy");
            Assert.Equal(East, world.Placed[door.Id].Face);
        }
        // Other doors the old rule turned the wrong way: the firelock row at (87, 87..89) and the airlocks it left to the default.
        foreach ((int x, int y) in new[] { (87, 87), (87, 88), (87, 89), (126, 91), (126, 92), (126, 93) })
        {
            FakeMapEntity door = world.Map.Entities.Single(e => e.X == x && e.Y == y && e.TypePath == "/obj/machinery/door/firedoor");
            Assert.Equal(East, world.Placed[door.Id].Face);
        }
    }

    private static PlanEntity Entity(long id, string type, int x, int y, byte dir) => new(id, type, x, y, 1, dir, Anomaly.Protocol.TableRef.Public((uint)id), 0, 0, Anomaly.Protocol.LightState.None, null);
}
