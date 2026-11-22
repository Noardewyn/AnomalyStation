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
/// Gaps package ST3 on the converted MetaStation: its water coolers, serving dishes, trash and laundry carts, trash bins, gold crate,
/// wall clocks and calendars boot live (the coolers with their jugs and water), with no runtime from ST3's files.
/// </summary>
public class CratesST3MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public CratesST3MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "abandoned_crates", "crates/bins", "crates/critter", "crates/cardboard", "closets/infinite", "signs_interactive",
        "reagent_dispenser", "crates_lockers/crates.dm", "spawn_glitter",
    };

    [MetaFact]
    public void Metas_closets_crates_and_dispensers_left_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, jugless = 0;
            for (int z = 1; z <= world.maxz; z++)
            {
                for (int y = 1; y <= world.maxy; y++)
                {
                    for (int x = 1; x <= world.maxx; x++)
                    {
                        turf t = locate(x, y, z);
                        if (t == null)
                            continue;
                        foreach (atom thing in t.dm_held)
                        {
                            string kind = thing switch
                            {
                                obj_structure_reagent_dispensers_water_cooler => "water cooler",
                                obj_structure_reagent_dispensers_servingdish => "serving dish",
                                obj_structure_closet_crate_trashcart => "trash cart",
                                obj_structure_closet_crate_bin => "trash bin",
                                obj_structure_closet_crate_goldcrate_stocked => "gold crate",
                                obj_structure_sign_clock => "clock",
                                obj_structure_sign_calendar => "calendar",
                                _ => null,
                            };
                            if (kind == null)
                                continue;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                            if (thing is obj_structure_reagent_dispensers_water_cooler { our_jug: null })
                                jugless++;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {jugless} jugless coolers; ST3 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("water cooler") >= 3, "too few water coolers");
            Assert.True(counts.GetValueOrDefault("serving dish") >= 2, "too few serving dishes");
            Assert.True(counts.GetValueOrDefault("trash cart") >= 2, "too few trash carts");
            Assert.True(counts.GetValueOrDefault("clock") >= 3, "too few clocks");
            foreach (string one in new[] { "gold crate", "calendar" })
                Assert.True(counts.GetValueOrDefault(one) >= 1, "no " + one);
            Assert.Equal(0, inert);
            Assert.Equal(0, jugless);
            Assert.Empty(runtimes);
        }
    }
}
