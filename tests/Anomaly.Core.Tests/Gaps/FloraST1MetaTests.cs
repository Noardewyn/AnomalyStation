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
/// Gaps package ST1 on the converted MetaStation: Meta's mapped flora (the garden's bushes, grass and rocks) and potted plants boot
/// live, the random ones with a look picked from tg's states (none keeps the mapping icon "random_plant"), with no runtime from
/// ST1's files. Lavaland's ash flora (another file, ash_flora.dm) is ItemsOD3MetaTests' (gaps OD3).
/// </summary>
public class FloraST1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public FloraST1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "/flora.cs", "kirbyplants.cs", "see_through_maps.cs" };

    [MetaFact]
    public void Metas_flora_and_potted_plants_boot_live_with_their_looks()
    {
        using (_meta.World.Enter())
        {
            var flora = Dm.each<obj_structure_flora>().Where(f => f is not obj_structure_flora_ash).ToList();
            var plants = Dm.each<obj_item_kirbyplants>().ToList();
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            foreach (atom thing in flora.Cast<atom>().Concat(plants))
            {
                if (InertSeam.IsInert(thing))
                    inertKinds.Add(thing.GetType().Name);
            }
            var placeholders = plants.Where(p => p.icon_state == "random_plant" || (p.icon as DmResource)?.Path == "icons/obj/fluff/flora/_flora.dmi").ToList();
            var unsynced = plants.Where(p => !Dm.truthy(p.dead) && p.icon_state != Dm.str(p.base_icon_state)).ToList();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {flora.Count} flora ({string.Join(", ", flora.GroupBy(f => f.GetType().Name).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key))})");
            _output.WriteLine($"Meta: {plants.Count} potted plants ({string.Join(", ", plants.GroupBy(p => p.icon_state).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key))}); "
                + $"inert {inertKinds.Count} ({string.Join(", ", inertKinds)}); {placeholders.Count} with the mapping icon; ST1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(flora.Count >= 1, "no flora");
            Assert.True(plants.Count >= 1, "no potted plant");
            Assert.Empty(inertKinds);
            Assert.Empty(placeholders);
            Assert.Empty(unsynced);
            Assert.Empty(runtimes);
        }
    }
}
