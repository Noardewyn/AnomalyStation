using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RD3 on the converted MetaStation: the science department's experi-scanners, experimental destructive scanner and
/// destructive analyzer boot live, each scanner with its experiment handler linked to the station's techweb, the analyzer linked to
/// it too, the operating computers with their autopsy handlers, and no runtime from the experiments' files.
/// </summary>
public class ExperimentsRD3MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ExperimentsRD3MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "experisci", "experiment_handler", "destructive_analyzer", "destructive_scanner", "handheld_scanner", "itembound", "operating_computer",
    };

    [MetaFact]
    public void Metas_experiment_devices_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, unlinked = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in Dm.each<atom>(locate(x, y, 1)))
                    {
                        Count(thing);
                        foreach (atom inner in Dm.each<atom>(thing)) // an experi-scanner in a closet or on a rack's shelf
                            Count(inner);
                    }
                }
            }

            void Count(atom thing)
            {
                string kind = thing switch
                {
                    obj_item_experi_scanner => "experi-scanner",
                    obj_machinery_destructive_scanner => "destructive scanner",
                    obj_machinery_rnd_destructive_analyzer => "destructive analyzer",
                    obj_machinery_computer_operating => "operating computer",
                    _ => null,
                };
                if (kind == null)
                    return;
                counts[kind] = counts.GetValueOrDefault(kind) + 1;
                if (InertSeam.IsInert(thing))
                {
                    inert++;
                    inertKinds.Add(thing.GetType().Name);
                }
                bool linked = thing switch
                {
                    obj_machinery_rnd_destructive_analyzer analyzer => analyzer.stored_research != null,
                    _ => (thing.GetComponent(typeof(datum_component_experiment_handler)) as datum_component_experiment_handler)?.linked_web != null,
                };
                if (!linked)
                    unlinked++;
            }

            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)) + $"; {inert} inert ({string.Join(", ", inertKinds)}), {unlinked} unlinked; experiment runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("experi-scanner") >= 3, "Meta's experi-scanners are missing");
            Assert.True(counts.GetValueOrDefault("destructive scanner") >= 1, "no destructive scanner");
            Assert.True(counts.GetValueOrDefault("destructive analyzer") >= 1, "no destructive analyzer");
            Assert.Equal(0, inert);
            Assert.Equal(0, unlinked);
            Assert.Empty(runtimes);
        }
    }
}
