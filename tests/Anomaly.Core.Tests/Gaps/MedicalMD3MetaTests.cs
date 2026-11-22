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
/// Gaps package MD3 on the converted MetaStation: Meta's lazarus injector boots live and loaded (MetaStation maps no limb grower
/// and no organ harvester; their boards are printed), with no runtime from MD3's files.
/// </summary>
public class MedicalMD3MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MedicalMD3MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "limbgrower.cs", "harvester.cs", "lazarus_injector.cs" };

    [MetaFact]
    public void Metas_lazarus_injector_boots_live_and_loaded()
    {
        using (_meta.World.Enter())
        {
            var injectors = Dm.each<obj_item_lazarus_injector>().ToList();
            int inert = injectors.Count(i => InertSeam.IsInert(i));
            int spent = injectors.Count(i => !Dm.truthy(i.loaded));
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {injectors.Count} lazarus injectors; {inert} inert, {spent} spent; MD3 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(injectors.Count >= 1, "no lazarus injector");
            Assert.Equal(0, inert);
            Assert.Equal(0, spent);
            Assert.Empty(runtimes);
        }
    }
}
