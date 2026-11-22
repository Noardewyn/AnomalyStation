using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package VI1 on the converted MetaStation: the virology lab's PanD.E.M.I.C. boots live with its icon state, SSdisease is made
/// with its archive of the common diseases, and the files run without a runtime.
/// </summary>
public class DiseasesVI1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public DiseasesVI1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_virology_lab_boots_live()
    {
        using (_meta.World.Enter())
        {
            var pandemics = Dm.each<obj_machinery_computer_pandemic>().ToList();
            _output.WriteLine($"Meta: {pandemics.Count} PanD.E.M.I.C.s ({string.Join(", ", pandemics.Select(p => p.icon_state + " in " + (get_area(p) as area)?.name))}); "
                + $"SSdisease: {(SSdisease == null ? "none" : SSdisease.archive_diseases.len + " archived")}");
            Assert.True(pandemics.Count >= 1, "no PanD.E.M.I.C. on Meta");
            Assert.Equal(0, pandemics.Count(p => InertSeam.IsInert(p)));
            Assert.All(pandemics, p => Assert.StartsWith("pandemic0", p.icon_state));
            Assert.NotNull(SSdisease);
            Assert.True(SSdisease.archive_diseases.len >= 20, "the archive of the common diseases");
            var runtimes = _meta.Log.Runtimes.Where(e =>
            {
                string text = e.ToString() ?? "";
                return text.Contains("disease", StringComparison.OrdinalIgnoreCase) || text.Contains("pandemic", StringComparison.Ordinal)
                    || text.Contains("symptom", StringComparison.Ordinal) || text.Contains("germ_sensitive", StringComparison.Ordinal)
                    || text.Contains("infective", StringComparison.Ordinal);
            }).ToList();
            Assert.Empty(runtimes);
        }
    }
}
