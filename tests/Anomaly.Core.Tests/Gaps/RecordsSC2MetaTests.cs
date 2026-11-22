using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package SC2 (D333) on the converted MetaStation: its medical records consoles and laptops, its security records consoles and
/// its warrant consoles boot live, with no runtime from the records consoles' files.
/// </summary>
public class RecordsSC2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public RecordsSC2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "computer/records/", "computer\\records\\", "warrant.cs", "wanted_poster", "medical_note", "records/record.cs", "records\\record.cs",
    };

    [MetaFact]
    public void Metas_records_consoles_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        string kind = thing switch
                        {
                            obj_machinery_computer_records_medical_laptop => "medical records laptop",
                            obj_machinery_computer_records_medical => "medical records console",
                            obj_machinery_computer_records_security => "security records console",
                            obj_machinery_computer_warrant => "warrant console",
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
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)) + $"; {inert} inert ({string.Join(", ", inertKinds)}); records runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            // tg's MetaStation.dmm: 5 medical records consoles and 7 laptops, 12 security records consoles, 2 warrant consoles
            Assert.Equal(5, counts.GetValueOrDefault("medical records console"));
            Assert.Equal(7, counts.GetValueOrDefault("medical records laptop"));
            Assert.Equal(12, counts.GetValueOrDefault("security records console"));
            Assert.Equal(2, counts.GetValueOrDefault("warrant console"));
            Assert.Equal(0, inert);
            Assert.Empty(runtimes);
        }
    }
}
