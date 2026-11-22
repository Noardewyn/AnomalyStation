using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package SG1 on the converted MetaStation: its scanner gates boot live, wired, with their passive scanline, and with no
/// runtime from the files.
/// </summary>
public class ScannerGateSG1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ScannerGateSG1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_scanner_gates_boot_live()
    {
        using (_meta.World.Enter())
        {
            var gates = Dm.each<obj_machinery_scanner_gate>().ToList();
            int inert = gates.Count(g => InertSeam.IsInert(g));
            int unwired = gates.Count(g => g.wires is not datum_wires_scanner_gate);
            int lineless = gates.Count(g => g.scanline == null);
            _output.WriteLine($"Meta: {gates.Count} scanner gates ({inert} inert, {unwired} unwired, {lineless} without a scanline); modes: "
                + string.Join(", ", gates.Select(g => g.scangate_mode + (Dm.truthy(g.locked) ? " (locked)" : ""))));
            Assert.True(gates.Count >= 2, "too few scanner gates");
            Assert.Equal(0, inert);
            Assert.Equal(0, unwired);
            Assert.Equal(0, lineless);
            var runtimes = _meta.Log.Runtimes.Where(e => (e.ToString() ?? "").Contains("scanner_gate", StringComparison.Ordinal)).ToList();
            Assert.Empty(runtimes);
        }
    }
}
