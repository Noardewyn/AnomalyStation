using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// The test room (M1 scope 2a, M2 scope 2k): 48 x 48 by default, every floor tile reachable, spread spawn points, tg's
/// <c>initial_gas_mix</c> on the floors; the <c>rooms</c> layout (four rooms of about 10 x 10 with doorways, a 1-tile and a
/// 2-tile corridor, a dead end, pillars) and M1's <c>classic</c> one.
/// </summary>
public class TestRoomTests
{
    [Theory]
    [InlineData(48, 48, TestRoomLayout.Classic)]
    [InlineData(64, 64, TestRoomLayout.Classic)]
    [InlineData(24, 24, TestRoomLayout.Classic)]
    [InlineData(14, 30, TestRoomLayout.Classic)]
    [InlineData(5, 5, TestRoomLayout.Classic)]
    [InlineData(3, 3, TestRoomLayout.Classic)]
    [InlineData(48, 48, TestRoomLayout.Rooms)]
    [InlineData(64, 64, TestRoomLayout.Rooms)]
    [InlineData(44, 44, TestRoomLayout.Rooms)]
    [InlineData(40, 40, TestRoomLayout.Rooms)]
    [InlineData(32, 32, TestRoomLayout.Rooms)]
    [InlineData(24, 24, TestRoomLayout.Rooms)]
    [InlineData(16, 16, TestRoomLayout.Rooms)]
    [InlineData(14, 14, TestRoomLayout.Rooms)]
    [InlineData(14, 30, TestRoomLayout.Rooms)]
    [InlineData(64, 20, TestRoomLayout.Rooms)]
    [InlineData(48, 30, TestRoomLayout.Rooms)]
    [InlineData(13, 13, TestRoomLayout.Rooms)]
    [InlineData(5, 5, TestRoomLayout.Rooms)]
    [InlineData(3, 3, TestRoomLayout.Rooms)]
    public void TheRimIsWallAndEveryFloorTileIsReachable(int width, int height, TestRoomLayout layout)
    {
        bool[,] wall = new bool[width + 1, height + 1];
        int floor = 0;
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
            {
                wall[x, y] = TestRoom.IsWall(width, height, x, y, layout);
                if (x == 1 || y == 1 || x == width || y == height) Assert.True(wall[x, y], $"rim ({x},{y})");
                else if (!wall[x, y]) floor++;
            }
        }
        // A flood fill over the floor (4-neighbours, as tg's cardinal steps) from (2, 2) reaches every floor tile.
        var seen = new bool[width + 1, height + 1];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((2, 2));
        seen[2, 2] = true;
        int reached = 0;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            reached++;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (!wall[nx, ny] && !seen[nx, ny]) { seen[nx, ny] = true; queue.Enqueue((nx, ny)); }
            }
        }
        Assert.Equal(floor, reached);
    }

    [Fact]
    public void A48ClassicRoomHasWallRunsAndPillars()
    {
        int interior = 0;
        for (int y = 2; y < 48; y++)
            for (int x = 2; x < 48; x++)
                if (TestRoom.IsWall(48, 48, x, y, TestRoomLayout.Classic)) interior++;
        Assert.InRange(interior, 40, 120);
        for (int x = 9; x <= 19; x++) Assert.True(TestRoom.IsWall(48, 48, x, 16, TestRoomLayout.Classic)); // a run
        Assert.True(TestRoom.IsWall(48, 48, 24, 24, TestRoomLayout.Classic));      // the centre pillar
        Assert.False(TestRoom.IsWall(48, 48, 2, 2, TestRoomLayout.Classic));
        Assert.False(TestRoom.IsWall(13, 13, 6, 6, TestRoomLayout.Classic)); // below FeaturedSize: no interior walls
    }

    [Theory]
    [InlineData(TestRoomLayout.Classic)]
    [InlineData(TestRoomLayout.Rooms)]
    public void TheBuiltRoomMatchesTheLayoutAndHasSpreadSpawnPoints(TestRoomLayout layout)
    {
        const int size = 48;
        TestRoom room = null;
        var world = new World(3);
        // tg's strings/ files, as the server mounts them: M10 slice F's ticker reads the lobby music and the station names
        if (TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tg)
            TgStrings.Mount(DmWorldState.Of(world).Files, tg);
        TgWorld.Boot(world, _ => room = TestRoom.Build(size, size, layout));
        using (world.Enter())
        {
            Assert.Equal(size, (int)Dm.world.maxx);
            Assert.Equal(size, (int)Dm.world.maxy);
            Assert.Equal(1, (int)Dm.world.maxz);
            for (int y = 1; y <= size; y++)
            {
                for (int x = 1; x <= size; x++)
                {
                    turf t = locate(x, y, 1);
                    Assert.Equal(TestRoom.IsWall(size, size, x, y, layout), t is turf_closed_indestructible);
                    if (!TestRoom.IsWall(size, size, x, y, layout)) Assert.IsType<turf_open_indestructible>(t);
                    Assert.Same(room.Area, t.loc);
                }
            }
        }

        IReadOnlyList<SpawnPoint> spawns = room.SpawnPoints;
        Assert.InRange(spawns.Count, 100, 250); // enough for the 150-bot run's first joins, each on its own tile (bots wrap round)
        Assert.Equal(spawns.Count, spawns.Distinct().Count());
        foreach (SpawnPoint p in spawns)
        {
            Assert.Equal(1, p.Z);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Assert.False(TestRoom.IsWall(size, size, p.X + dx, p.Y + dy, layout), $"spawn ({p.X},{p.Y}) touches a wall");
        }
        // Consecutive joins land far apart.
        for (int i = 0; i + 1 < 20; i++)
        {
            int distance = Math.Max(Math.Abs(spawns[i].X - spawns[i + 1].X), Math.Abs(spawns[i].Y - spawns[i + 1].Y));
            Assert.True(distance >= 6, $"spawns {i} and {i + 1} are {distance} tiles apart");
        }

        // The Master initializes the room's atoms as in a real boot.
        world.Step(30);
        Assert.Equal(0, world.RuntimeErrorCount);
    }

    [Fact]
    public void TheRoomsLayoutHasFourRoomsDoorwaysTwoCorridorsADeadEndAndPillars()
    {
        const int w = 48, h = 48;
        bool Wall(int x, int y) => TestRoom.IsWall(w, h, x, y, TestRoomLayout.Rooms);
        // Four rooms of 10 x 10 floor in the corners, walled on their inner sides.
        foreach ((int x0, int y0) in new[] { (2, 2), (38, 2), (2, 38), (38, 38) })
        {
            int free = 0;
            for (int y = y0; y < y0 + 10; y++)
                for (int x = x0; x < x0 + 10; x++)
                    if (!Wall(x, y)) free++;
            Assert.InRange(free, 98, 100); // 10 x 10, minus the room's own pillar
        }
        Assert.True(Wall(12, 5) && Wall(37, 5) && Wall(5, 12) && Wall(5, 37));  // the inner walls ...
        Assert.False(Wall(12, 7) || Wall(37, 7) || Wall(7, 12) || Wall(7, 37));  // ... with a one-tile doorway in the middle of each
        Assert.True(Wall(12, 6) && Wall(12, 8));                                 // exactly one tile wide

        // The 1-tile corridor: floor between two wall rows over its whole span, open at both ends.
        int cy = h / 2;
        for (int x = 16; x <= 33; x++)
        {
            Assert.True(Wall(x, cy - 5) && Wall(x, cy - 3), $"corridor walls at x {x}");
            Assert.False(Wall(x, cy - 4), $"corridor floor at x {x}");
        }
        Assert.False(Wall(15, cy - 4) || Wall(34, cy - 4));

        // The 2-tile corridor: two floor rows between wall rows, closed at its east end (the dead-end wall face).
        for (int x = 16; x <= 32; x++)
        {
            Assert.True(Wall(x, cy + 3) && Wall(x, cy + 6));
            Assert.False(Wall(x, cy + 4) || Wall(x, cy + 5));
        }
        Assert.True(Wall(33, cy + 4) && Wall(33, cy + 5));
        Assert.False(Wall(15, cy + 4) || Wall(15, cy + 5));

        // Pillars in the hall: single tiles with floor around them.
        int pillars = 0;
        for (int y = 14; y <= 35; y++)
            for (int x = 14; x <= 35; x++)
                if (Wall(x, y) && !Wall(x - 1, y) && !Wall(x + 1, y) && !Wall(x, y - 1) && !Wall(x, y + 1)) pillars++;
        Assert.Equal(6, pillars);
    }

    [Fact]
    public void ASmallRoomsLayoutHasRoomsAndDoorwaysOnly()
    {
        // 24 x 24: rooms of 6 x 6 with a pillar each, no corridors (the hall is too small), every room joined to the hall.
        bool Wall(int x, int y) => TestRoom.IsWall(24, 24, x, y, TestRoomLayout.Rooms);
        Assert.True(Wall(8, 3) && Wall(3, 8));
        Assert.False(Wall(8, 5) || Wall(5, 8));
        for (int x = 9; x <= 16; x++)
            Assert.False(Wall(x, 12), "the hall's middle row is open");
    }

    [Fact]
    public void TheServersRoomIsRingedBySpaceAndADeletedRimWallVentsIntoIt()
    {
        // the owner (2026-10-13): a deleted rim wall let no air out, as nothing lay beyond the map's edge
        const int size = 24, space = TestRoom.ServerSpace;
        TestRoom room = null;
        var world = new World(5);
        // tg's strings/ files, as the server mounts them (gaps P3: SSatoms' setupGenetics makes the speech mutations, which read them)
        if (TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tg)
            TgStrings.Mount(DmWorldState.Of(world).Files, tg);
        TgWorld.Boot(world, _ => room = TestRoom.Build(size, size, space: space));
        using (world.Enter())
        {
            Assert.Equal(size + 2 * space, Dm.num(Dm.world.maxx));
            Assert.Equal(size + 2 * space, Dm.num(Dm.world.maxy));
            for (int x = 1; x <= size + 2 * space; x++)
            {
                Assert.IsType<turf_open_space>(locate(x, 1, 1));
                Assert.IsType<area_space>(((turf)locate(x, space, 1)).loc);
            }
            Assert.IsType<turf_closed_indestructible>(locate(1 + space, 1 + space, 1)); // the room's corner
            Assert.IsType<area_misc_testroom>(((turf)locate(2 + space, 2 + space, 1)).loc);
            foreach (SpawnPoint p in room.SpawnPoints)
            {
                Assert.InRange(p.X, 2 + space, size - 1 + space);
                Assert.IsType<turf_open_indestructible>(locate(p.X, p.Y, 1));
            }
        }
        using (world.Enter())
            Assert.Contains("scraped away", DevCommands.Delete(locate(1 + space, 10 + space, 1)));
        world.Step(40);
        using (world.Enter())
        {
            Assert.IsAssignableFrom<turf_open>(locate(1 + space, 10 + space, 1));
            float inside = Dm.num(((turf_open)locate(2 + space, 10 + space, 1)).air.return_pressure());
            Assert.True(inside < 70f, "the room vents through the hole into the space around it: " + inside);
        }
    }

    [Fact]
    public void TheFloorsKeepTgsInitialGasMixAndTheWallsBlockAir()
    {
        TestRoom room = null;
        var world = new World(3);
        TgWorld.Boot(world, _ => room = TestRoom.Build(24, 24));
        using (world.Enter())
        {
            int floors = 0;
            for (int y = 1; y <= 24; y++)
            {
                for (int x = 1; x <= 24; x++)
                {
                    turf t = locate(x, y, 1);
                    if (t is turf_open open)
                    {
                        Assert.Equal(Defines.OPENTURF_DEFAULT_ATMOS, open.initial_gas_mix);
                        Assert.False(Dm.truthy(open.blocks_air));
                        floors++;
                    }
                    else
                    {
                        Assert.True(Dm.truthy(t.blocks_air), "a wall blocks air");
                    }
                }
            }
            Assert.True(floors > 300);
        }

        // Another mix is a parameter (for the tests and a future gas map).
        var other = new World(4);
        TgWorld.Boot(other, _ => TestRoom.Build(24, 24, TestRoomLayout.Rooms, Defines.AIRLESS_ATMOS));
        using (other.Enter())
            Assert.Equal(Defines.AIRLESS_ATMOS, ((turf_open)locate(4, 4, 1)).initial_gas_mix);
    }

    [Fact]
    public void TheSpawnOrderIsFixed()
    {
        static IReadOnlyList<SpawnPoint> Build()
        {
            TestRoom room = null;
            TgWorld.Boot(new World(1), _ => room = TestRoom.Build(24, 24));
            return room.SpawnPoints;
        }
        Assert.Equal(Build(), Build());
    }

    [Fact]
    public void SizesOutsideTheLimitsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TgWorld.Boot(new World(1), _ => TestRoom.Build(65, 48)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TgWorld.Boot(new World(1), _ => TestRoom.Build(2, 48)));
    }
}
