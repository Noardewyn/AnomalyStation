using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package TL1 on the converted MetaStation: its wall telescreens and entertainment monitors boot live on their walls, the
/// monitors with their speakers and blank screens, the telescreens on their networks, with no runtime from the files.
/// </summary>
public class TelescreensTL1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public TelescreensTL1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_telescreens_and_entertainment_monitors_boot_live()
    {
        using (_meta.World.Enter())
        {
            var screens = Dm.each<obj_machinery_computer_security_telescreen>().ToList();
            var tvs = screens.OfType<obj_machinery_computer_security_telescreen_entertainment>().ToList();
            var walls = screens.Except(tvs).ToList();
            int inert = screens.Count(s => InertSeam.IsInert(s));
            int speakerless = tvs.Count(t => t.speakers == null || !ReferenceEquals(t.speakers.loc, t));
            int netless = walls.Count(s => Dm.length(s.network) == 0);
            _output.WriteLine($"Meta: {walls.Count} telescreens ({netless} without a network), {tvs.Count} entertainment monitors ({speakerless} without speakers), {inert} inert; "
                + string.Join(", ", walls.Select(s => s.GetType().Name.Replace("obj_machinery_computer_security_telescreen_", "")).Distinct().OrderBy(n => n, StringComparer.Ordinal)));
            Assert.True(walls.Count >= 5, "too few telescreens");
            Assert.True(tvs.Count >= 5, "too few entertainment monitors");
            Assert.Equal(0, inert);
            Assert.Equal(0, speakerless);
            Assert.Equal(0, netless);
            Assert.All(tvs, t => Assert.Equal("entertainment_blank", Dm.str(t.icon_screen)));
            var runtimes = _meta.Log.Runtimes.Where(e =>
            {
                string text = e.ToString() ?? "";
                return text.Contains("telescreen", StringComparison.Ordinal) || text.Contains("broadcast_camera", StringComparison.Ordinal);
            }).ToList();
            Assert.Empty(runtimes);
        }
    }
}
