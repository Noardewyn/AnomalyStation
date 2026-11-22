using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Integration;

namespace Anomaly.Server.Tests.M3Maps.R4;

/// <summary>
/// M3 R4, the owner's first walk (the map looked empty): SSmapping loads the station in the Master's init, after the host
/// attached the replicator to the booted world, so the replicator's cell tables were built for a world with no map and a
/// client that joined got one <c>/turf</c> run, no solidity and no turf appearances. The tables are rebuilt when the map's
/// size changes and re-read when joins open: a client that joins gets the map as the world has it.
/// </summary>
public class MapJoinTests
{
    internal static string FixtureMaps()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tests", "fixtures", "maps", "p2load");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("tests/fixtures/maps/p2load not found");
    }

    // noInterest (M10 slice C): every client receives every entity, for a test whose watcher follows events across the station.
    internal static ServerConfig MapConfig(string map, string folder, ulong seed = 7, bool noInterest = false)
        => new() { Seed = seed, Console = false, StatusEveryTicks = 100000, Map = map, MapsFolder = folder, MoveSpeedScale = 1f, NoInterest = noInterest }; // tg's speed, not DV70's

    [Fact]
    public void A_client_that_joins_a_map_world_gets_the_turfs_the_walls_and_the_appearances()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapConfig("p2load", FixtureMaps()), strict: false);
        MapGrid grid = world.Replicator.ReadMapGrid();
        Assert.Equal((7, 6, 2), (grid.Width, grid.Height, grid.Levels)); // the station and tg's first transit level (M10 slice G1)
        Assert.True(grid.Runs.Count > 1, "one turf run: the tables were built before the map loaded");
        Assert.NotEmpty(grid.AppearanceRuns);

        RealHeadlessWorld.Player p = world.Join("Walker");
        ReplicaWorld replica = p.Client.Replica;
        int walls = 0;
        using (world.World.Enter())
        {
            for (int y = 1; y <= 6; y++)
            {
                for (int x = 1; x <= 7; x++)
                {
                    turf t = MapView.TurfAt(world.World, x, y, 1);
                    string path = DmTypes.PathOf(t.GetType());
                    Assert.Equal(path, replica.TurfTypeAt(x, y, 1));
                    bool closed = path.StartsWith("/turf/closed", StringComparison.Ordinal);
                    Assert.Equal(closed, replica.IsSolid(x, y, 1));
                    Assert.NotEqual(0u, replica.TurfAppearance(x, y, 1).Index);
                    if (closed) walls++;
                }
            }
        }
        Assert.Equal(14, walls);
    }

    [MetaServerFact]
    public void A_client_that_joins_Meta_gets_its_walls_and_floors()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = world.Join("Walker");
        ReplicaWorld replica = p.Client.Replica;
        int walls = 0, floors = 0, solidMismatch = 0, typeMismatch = 0;
        using (world.World.Enter())
        {
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    string path = DmTypes.PathOf(MapView.TurfAt(world.World, x, y, 1).GetType());
                    if (path != replica.TurfTypeAt(x, y, 1)) typeMismatch++;
                    if (path.StartsWith("/turf/closed/wall", StringComparison.Ordinal))
                    {
                        walls++;
                        if (!replica.IsSolid(x, y, 1)) solidMismatch++;
                    }
                    else if (path.StartsWith("/turf/open/floor", StringComparison.Ordinal)) floors++;
                }
            }
        }
        Assert.Equal(0, typeMismatch);
        Assert.Equal(0, solidMismatch);
        Assert.True(walls > 3000, "walls " + walls);
        Assert.True(floors > 10000, "floors " + floors);
    }
}
