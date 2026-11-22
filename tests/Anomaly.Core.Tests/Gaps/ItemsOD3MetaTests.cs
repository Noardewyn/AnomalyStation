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
/// Gaps package OD3 on the converted MetaStation with its Lavaland level: Lavaland's mapped and generated ash flora boots live with
/// its variant sprites, with no runtime from OD3's files.
/// </summary>
public class ItemsOD3MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ItemsOD3MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "lavaland/ash_flora", "research/part_replacer", "others/rped", "lavalandruin_code/elephantgraveyard", "grenades/flashbang",
    };

    [MetaFact]
    public void Lavalands_ash_flora_boots_live()
    {
        using (_meta.World.Enter())
        {
            var flora = Dm.each<obj_structure_flora_ash>().Where(f => !QDELETED(f) && f.loc != null).ToList();
            var kinds = flora.GroupBy(f => f.GetType().Name).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key).ToList();
            int inert = flora.Count(f => InertSeam.IsInert(f));
            // the variant sprite Initialize picks (l_mushroom1-4 and the like), never the bare base name
            var unpicked = flora.Where(f => f.icon_state == Dm.str(Dm.initial(f, (atom p) => p.base_icon_state))).Select(f => f.GetType().Name).Distinct().ToList();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"ash flora {flora.Count}: " + string.Join(", ", kinds) + $"; {inert} inert; unpicked {unpicked.Count}; OD3 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(flora.Count >= 6, "too few ash flora (Lavaland.dmm maps six)");
            Assert.Equal(0, inert);
            Assert.Empty(unpicked);
            Assert.Empty(runtimes);
        }
    }
}
