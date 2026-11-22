using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H1 on the converted MetaStation: its sinks, showers, toilets, urinals, puddles, mirrors, dressers, washing machines
/// and recycler boot live, with their water (those with a reclaimer), their reflection and their urinal cakes, and with no runtime from
/// their files.
/// </summary>
public class HouseholdH1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public HouseholdH1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "water_structures", "shower", "washing_machine", "mirror", "dresser", "recycler", "reflection", "omen",
    };

    [MetaFact]
    public void Metas_household_things_boot_live_with_their_water_and_their_reflection()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int dry = 0, inert = 0, noReflection = 0, cakes = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        string kind = thing switch
                        {
                            obj_structure_sink => "sink",
                            obj_machinery_shower => "shower",
                            obj_structure_toilet => "toilet",
                            obj_structure_urinal => "urinal",
                            obj_structure_water_source => "water source",
                            obj_structure_mirror => "mirror",
                            obj_structure_dresser => "dresser",
                            obj_machinery_washing_machine => "washing machine",
                            obj_machinery_recycler => "recycler",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                            inert++;
                        // a thing with a water reclaimer starts full; tg's greyscale toilets (the brig's) have none and start dry
                        bool reclaims = thing switch
                        {
                            obj_structure_sink sink => Dm.truthy(sink.has_water_reclaimer),
                            obj_machinery_shower shower => Dm.truthy(shower.has_water_reclaimer),
                            obj_structure_toilet toilet => Dm.truthy(toilet.has_water_reclaimer),
                            obj_structure_water_source => true,
                            _ => false,
                        };
                        if (reclaims && ((thing.reagents as datum_reagents)?.total_volume ?? 0) <= 0)
                            dry++;
                        if (thing is obj_structure_mirror && thing.GetComponent(typeof(datum_component_reflection)) == null)
                            noReflection++;
                        if (thing is obj_structure_urinal urinal && urinal.hidden_item is obj_item_food_urinalcake)
                            cakes++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)) + $"; {dry} dry, {inert} inert, {noReflection} mirrors without a reflection, {cakes} urinal cakes; household runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("sink") >= 20, "too few sinks");
            Assert.True(counts.GetValueOrDefault("shower") >= 20, "too few showers");
            Assert.True(counts.GetValueOrDefault("toilet") >= 5, "too few toilets");
            Assert.True(counts.GetValueOrDefault("mirror") >= 5, "too few mirrors");
            Assert.Equal(0, inert);
            Assert.Equal(0, dry);
            Assert.Equal(0, noReflection);
            Assert.Equal(counts.GetValueOrDefault("urinal"), cakes); // every mapped urinal hides its cake
            Assert.Empty(runtimes);
        }
    }
}
