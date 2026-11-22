using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package TP1 on the converted MetaStation: the teleporter rooms' consoles, stations and hubs boot live and linked to each
/// other, the hand teleporters and the teleport beacons are live, with no runtime from TP1's files.
/// </summary>
public class TeleporterTP1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public TeleporterTP1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "teleporter.cs", "teleportation.cs", "flypeople.cs" };

    [MetaFact]
    public void Metas_teleporters_boot_live_and_linked()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, unlinked = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            for (int z = 1; z <= world.maxz; z++)
            {
                for (int y = 1; y <= 255; y++)
                {
                    for (int x = 1; x <= 255; x++)
                    {
                        if (locate(x, y, z) is not turf t)
                            continue;
                        foreach (atom thing in t.dm_held)
                        {
                            string kind = thing switch
                            {
                                obj_machinery_computer_teleporter => "teleporter console",
                                obj_machinery_teleport_station => "teleporter station",
                                obj_machinery_teleport_hub => "teleporter hub",
                                obj_item_hand_tele => "hand teleporter",
                                _ => null,
                            };
                            if (kind == null)
                                continue;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                            {
                                inert++;
                                inertKinds.Add(thing.GetType().Name);
                            }
                            if (thing is obj_machinery_teleport_station station && (station.teleporter_hub == null || station.teleporter_console == null))
                                unlinked++;
                        }
                    }
                }
            }
            int beacons = Dm.each<obj_item_beacon>(GLOB.teleportbeacons).Count();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {beacons} teleport beacons; {inert} inert ({string.Join(", ", inertKinds)}), {unlinked} unlinked stations; TP1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("teleporter console") >= 1, "no teleporter console");
            Assert.True(counts.GetValueOrDefault("teleporter station") >= 1, "no teleporter station");
            Assert.True(counts.GetValueOrDefault("teleporter hub") >= 1, "no teleporter hub");
            Assert.Equal(0, inert);
            Assert.Equal(0, unlinked);
            Assert.Empty(runtimes);
        }
    }
}
