using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package LC1 on the converted MetaStation (with Lavaland): the brig's labour camp teleporter, its console and the item
/// reclaimer boot live and find each other and the camp's beacon; the prisoner management consoles and the prison's labour
/// machines boot live; no runtime from the files.
/// </summary>
public class LaborCampLC1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public LaborCampLC1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_labour_camp_machines_boot_live_and_linked()
    {
        using (_meta.World.Enter())
        {
            var consoles = Dm.each<obj_machinery_computer_prisoner_gulag_teleporter_computer>().ToList();
            var teleporters = Dm.each<obj_machinery_gulag_teleporter>().ToList();
            var reclaimers = Dm.each<obj_machinery_gulag_item_reclaimer>().ToList();
            var managers = Dm.each<obj_machinery_computer_prisoner_management>().ToList();
            var beacons = Dm.each<obj_structure_gulag_beacon>().ToList();
            var presses = Dm.each<obj_machinery_plate_press>().ToList();
            var porters = Dm.each<obj_machinery_produceporter>().ToList();
            var all = consoles.Cast<atom>().Concat(teleporters).Concat(reclaimers).Concat(managers).Concat(presses).Concat(porters).ToList();
            int inert = all.Count(a => InertSeam.IsInert(a));
            _output.WriteLine($"Meta: {consoles.Count} teleporter consoles, {teleporters.Count} teleporters, {reclaimers.Count} reclaimers, {managers.Count} management consoles, "
                + $"{beacons.Count} beacons, {presses.Count} plate presses, {porters.Count} produceporters; {inert} inert; "
                + string.Join("; ", consoles.Select(c => $"console: teleporter {(c.teleporter != null ? "found" : "none")}, beacon {(c.beacon != null ? "found" : "none")}")));
            Assert.NotEmpty(consoles);
            Assert.NotEmpty(teleporters);
            Assert.NotEmpty(reclaimers);
            Assert.NotEmpty(managers);
            Assert.Equal(0, inert);
            Assert.All(consoles, c => Assert.NotNull(c.teleporter));
            if (beacons.Count > 0)
                Assert.All(consoles, c => Assert.NotNull(c.beacon));
            Assert.All(teleporters, t => Assert.NotNull(t.linked_reclaimer));
            var runtimes = _meta.Log.Runtimes.Where(e =>
            {
                string text = e.ToString() ?? "";
                return text.Contains("gulag", StringComparison.Ordinal) || text.Contains("prisoner", StringComparison.Ordinal) || text.Contains("prisonlabor", StringComparison.Ordinal);
            }).ToList();
            Assert.Empty(runtimes);
        }
    }
}
