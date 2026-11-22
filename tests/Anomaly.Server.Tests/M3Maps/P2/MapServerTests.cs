using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.M3Maps.P2;

/// <summary>
/// M3 P2: the server with a converted map (ServerConfig.Map, MAP): the world loads it through SSmapping, players join at
/// its spawn points, and the command log records the map for the replay. The fixture map is tests/fixtures/maps/p2load.
/// </summary>
public class MapServerTests
{
    private static string FixtureMaps()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tests", "fixtures", "maps", "p2load");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("tests/fixtures/maps/p2load not found");
    }

    private static ServerConfig MapConfig(ulong seed = 7)
        => new() { Seed = seed, Console = false, StatusEveryTicks = 1000, Map = "p2load", MapsFolder = FixtureMaps() };

    [Fact]
    public void A_map_world_spawns_players_at_its_late_join_turfs()
    {
        using var server = TestServer.Start(MapConfig(), strict: false);
        Assert.True(server.Host.MapLoaded);
        Assert.Null(server.Host.Room);
        Assert.Equal(new[] { new SpawnPoint(3, 3, 1), new SpawnPoint(5, 4, 1) }, server.Host.SpawnPoints);
        server.Join("Alice");
        (int x, int y) = server.PositionOf("Alice");
        Assert.Contains((x, y), new[] { (3, 3), (5, 4) });
        using (server.World.Enter())
            Assert.Equal("P2 Load", GlobalProcs.SSmapping.current_map.map_name);
    }

    [Fact]
    public void The_command_log_records_the_map_and_its_replay_loads_it()
    {
        ServerConfig config = MapConfig();
        CommandLog log = CommandLog.Parse(CommandLog.For(config).ToText(), out string error);
        Assert.Null(error);
        Assert.Equal("p2load", log.Map);
        Assert.Contains("\nmap p2load\n", CommandLog.For(config).ToText());
        Assert.DoesNotContain("\nmap ", CommandLog.For(TestServer.DefaultConfig()).ToText());
        ServerConfig replay = log.ReplayConfig(config);
        Assert.Equal(("p2load", config.MapsFolder), (replay.Map, replay.MapsFolder));
        Assert.Null(CommandLog.Parse(CommandLog.For(config).ToText().Replace("map p2load", "map ../x"), out error));
        Assert.Contains("not a map json name", error);
    }

    [Fact]
    public void The_maps_folder_is_found_from_a_relative_name()
    {
        string folder = FixtureMaps();
        Assert.Equal(folder, ServerHost.ResolveMapsFolder(folder, "p2load"));
        Assert.Equal(Path.GetFullPath("no-such-maps-folder"), ServerHost.ResolveMapsFolder("no-such-maps-folder", "p2load"));
    }
}
