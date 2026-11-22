using System.IO;
using System.Text.Json;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// The transit tube kit (godot/assets/models/transit_tubes): every tube, station and dispenser state of transit_tube.dmi, turned by the catalog's rule
/// for each of tg's four dirs, has its ports (connections.json) exactly where tg's init_tube_dirs connects it, so neighbouring models meet; a station's
/// opening cover is on the side its boarding_dir is entered from.
/// </summary>
public sealed class TransitTubeModelTests
{
    private const string Icon = "icons/obj/pipes_n_cables/transit_tube.dmi";
    private const byte N = 1, S = 2, E = 4, W = 8, NE = N | E, NW = N | W, SE = S | E, SW = S | W;

    private static string Root
    {
        get
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "godot", "assets", "models");
                if (File.Exists(Path.Combine(candidate, ModelCatalog.FileName)))
                    return candidate;
            }
            throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
        }
    }

    // tg's init_tube_dirs (transit_tube.dm, station.dm) for NORTH, SOUTH, EAST, WEST
    public static IEnumerable<object[]> Tubes() => new[]
    {
        new object[] { "straight", "tube_straight", new[] { new[] { N, S }, new[] { N, S }, new[] { E, W }, new[] { E, W } } },
        new object[] { "crossing", "tube_straight_crossing", new[] { new[] { N, S }, new[] { N, S }, new[] { E, W }, new[] { E, W } } },
        new object[] { "diagonal", "tube_diagonal", new[] { new[] { NE, SW }, new[] { NE, SW }, new[] { NW, SE }, new[] { NW, SE } } },
        new object[] { "diagonal_crossing", "tube_diagonal_crossing", new[] { new[] { NE, SW }, new[] { NE, SW }, new[] { NW, SE }, new[] { NW, SE } } },
        new object[] { "curved0", "tube_curve_left", new[] { new[] { N, SW }, new[] { S, NE }, new[] { E, NW }, new[] { SE, W } } },
        new object[] { "curved1", "tube_curve_right", new[] { new[] { N, SE }, new[] { S, NW }, new[] { E, SW }, new[] { NE, W } } },
        new object[] { "junction0", "tube_junction", new[] { new[] { N, SE, SW }, new[] { S, NW, NE }, new[] { E, SW, NW }, new[] { W, NE, SE } } },
        new object[] { "junction1", "tube_junction", new[] { new[] { N, SW, SE }, new[] { S, NE, NW }, new[] { E, NW, SW }, new[] { W, SE, NE } } },
        new object[] { "closed_station0", "tube_station", new[] { new[] { E, W }, new[] { E, W }, new[] { N, S }, new[] { N, S } } },
        new object[] { "open_station1", "tube_station", new[] { new[] { E, W }, new[] { E, W }, new[] { N, S }, new[] { N, S } } },
        new object[] { "open_dispenser0", "tube_station_dispenser", new[] { new[] { E, W }, new[] { E, W }, new[] { N, S }, new[] { N, S } } },
        new object[] { "closed_terminus0", "tube_terminus", new[] { new[] { E }, new[] { W }, new[] { S }, new[] { N } } },
        new object[] { "open_terminusdispenser1", "tube_terminus_dispenser", new[] { new[] { E }, new[] { W }, new[] { S }, new[] { N } } },
    };

    private static readonly byte[] Dirs = { N, S, E, W };

    [Theory]
    [MemberData(nameof(Tubes))]
    public void TheTurnedModelsPortsAreTgsTubeDirs(string state, string model, byte[][] tubeDirs)
    {
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));
        ModelEntry entry = catalog.Find(Icon, state);
        Assert.NotNull(entry);
        using JsonDocument connections = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "transit_tubes", "connections.json")));
        byte[] ports = connections.RootElement.GetProperty(model).EnumerateArray().Select(p => Parse(p.GetProperty("direction").GetString())).ToArray();
        for (int i = 0; i < 4; i++)
        {
            float yaw = ModelRules.Choose(entry, state, Dirs[i], 0).Select(p => p.Yaw).Distinct().Single();
            Assert.Equal(tubeDirs[i].OrderBy(d => d), ports.Select(p => Turn(p, yaw)).OrderBy(d => d));
        }
    }

    // station.dm: station0's and terminus0's boarding_dir is REVERSE_DIR(dir) (a mob walking that way comes from the dir's side), the flipped one's is dir
    [Theory]
    [InlineData("opening_station0", false)]
    [InlineData("opening_station1", true)]
    [InlineData("opening_terminus0", false)]
    [InlineData("opening_terminus1", true)]
    public void AStationsRollingCoverIsOnTheSideItIsBoardedFrom(string state, bool flipped)
    {
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));
        ModelEntry entry = catalog.Find(Icon, state);
        string rolling = entry.Parts.Single(p => p.Value.Pivot && p.Value.Degrees != 0f).Value.Node;
        byte unturned = rolling == "Boarding_Door_back" ? N : S; // Boarding_back at -Z (north), Boarding_front at +Z
        foreach (byte dir in Dirs)
        {
            float yaw = ModelRules.Choose(entry, state, dir, 0).First().Yaw;
            byte boarding = flipped ? dir : Reverse(dir);
            Assert.Equal(Reverse(boarding), Turn(unturned, yaw));
        }
    }

    private static byte Reverse(byte dir) => (byte)(((dir & 0b0101) << 1) | ((dir & 0b1010) >> 1));

    // a BYOND dir turned by a yaw (counter-clockwise seen from above, Godot's +Y)
    private static byte Turn(byte dir, float yaw)
    {
        float x = ((dir & E) != 0 ? 1 : 0) - ((dir & W) != 0 ? 1 : 0), y = ((dir & N) != 0 ? 1 : 0) - ((dir & S) != 0 ? 1 : 0);
        float tx = x * MathF.Cos(yaw) - y * MathF.Sin(yaw), ty = x * MathF.Sin(yaw) + y * MathF.Cos(yaw);
        return (byte)((ty > 0.5f ? N : 0) | (ty < -0.5f ? S : 0) | (tx > 0.5f ? E : 0) | (tx < -0.5f ? W : 0));
    }

    private static byte Parse(string name) => name switch
    {
        "N" => N, "S" => S, "E" => E, "W" => W, "NE" => NE, "NW" => NW, "SE" => SE, "SW" => SW,
        _ => throw new ArgumentException(name),
    };
}
