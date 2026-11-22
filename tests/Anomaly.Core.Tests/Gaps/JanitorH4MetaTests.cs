using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H4 on the converted MetaStation: the janitor's closet boots live, its janitorial cart with its bucket, the janicart
/// with its riding element, the mop buckets, the wet floor signs and the janicart's key, with no runtime from the janitor's files.
/// </summary>
public class JanitorH4MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public JanitorH4MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "janitor.cs", "pimpin_ride", "ridden.cs", "vehicle_key", "riding_vehicle", "wetfloor", "vacuum.cs", "cleaning.cs", "mop.cs",
    };

    [MetaFact]
    public void Metas_janitors_things_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, dry = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        string kind = thing switch
                        {
                            obj_structure_mop_bucket_janitorialcart => "janitorial cart",
                            obj_structure_mop_bucket => "mop bucket",
                            obj_vehicle_ridden_janicart => "janicart",
                            obj_item_clothing_suit_caution => "wet floor sign",
                            obj_item_key_janitor => "janicart key",
                            obj_item_mop => "mop",
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
                        if (thing is obj_structure_mop_bucket && thing.reagents is not datum_reagents)
                            dry++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)) + $"; {inert} inert ({string.Join(", ", inertKinds)}), {dry} buckets without a holder; janitor runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("janitorial cart") >= 1, "no janitorial cart");
            Assert.True(counts.GetValueOrDefault("janicart") >= 1, "no janicart");
            Assert.Equal(0, inert);
            Assert.Equal(0, dry);
            Assert.Empty(runtimes);
        }
    }
}
