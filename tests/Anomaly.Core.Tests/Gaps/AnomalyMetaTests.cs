using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Science's anomaly lab on the converted MetaStation: the anomaly refinery, the raw cores (the random ones become cores of a kind),
/// the gizmos (the spawner's pick and the tucker tubes), the ecto sniffer and the orbital pads with their consoles (the station's and Lavaland's base's) boot live; each
/// console found its pad; the refinery takes a raw core and names the radius it needs; and nothing of them throws over the ticks.
/// </summary>
public class AnomalyMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public AnomalyMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    private static readonly string[] Files =
    {
        "anomaly_refinery", "raw_anomaly", "anomaly_core", "research/gizmo", "gizmodes", "moving_randomly", "ecto_sniffer", "mechlaunchpad",
        "quantum_pad", "launchpad_control",
    };

    [MetaFact]
    public void Metas_anomaly_lab_boots_live()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        using (_meta.World.Enter())
        {
            var refineries = Dm.each<obj_machinery_research_anomaly_refinery>().ToList();
            var raw = Dm.each<obj_item_raw_anomaly_core>().ToList();
            var gizmos = Dm.each<obj_machinery_gizmo>().ToList();
            var sniffers = Dm.each<obj_machinery_ecto_sniffer>().ToList();
            var pads = Dm.each<obj_machinery_mechpad>().ToList();
            var consoles = Dm.each<obj_machinery_computer_mechpad>().ToList();
            _output.WriteLine($"Meta's anomaly lab: {refineries.Count} refineries, {raw.Count} raw cores ({string.Join(", ", raw.Select(c => c.GetType().Name))}), "
                + $"{gizmos.Count} gizmos ({string.Join(", ", gizmos.Select(g => g.GetType().Name + " '" + Dm.str(g.name) + "'"))}), {sniffers.Count} sniffers, "
                + $"{pads.Count} orbital pads, {consoles.Count} pad consoles");
            Assert.Single(refineries);
            Assert.Equal(3, raw.Count);
            Assert.DoesNotContain(raw, c => c is obj_item_raw_anomaly_core_random);
            Assert.Equal(2, gizmos.Count);
            Assert.Contains(gizmos, g => g is obj_machinery_gizmo_toggle_tucker_tubes);
            Assert.Single(sniffers);
            Assert.Equal(2, pads.Count); // the station's and Lavaland's base's
            Assert.Equal(2, consoles.Count);
            foreach (atom thing in refineries.Cast<atom>().Concat(raw).Concat(gizmos).Concat(sniffers).Concat(pads).Concat(consoles))
                Assert.False(InertSeam.IsInert(thing), thing.GetType().Name + " is inert");
            foreach (obj_machinery_gizmo gizmo in gizmos)
            {
                Assert.NotNull(gizmo.controller_value);
                Assert.IsType<datum_wires_gizmo>(gizmo.wires);
            }
            foreach (obj_machinery_computer_mechpad console in consoles)
            {
                Assert.NotNull(console.connected_mechpad);
                Assert.Contains(console.connected_mechpad, pads);
                Assert.Equal(console.id, console.connected_mechpad.id);
            }

            // a scientist puts a raw core in the refinery
            var refinery = refineries[0];
            var scientist = Dm.@new<mob_living_carbon_human>(get_step(refinery, SOUTH));
            var core = raw[0];
            Waiting.NoSleep(scientist.put_in_active_hand(core));
            Waiting.NoSleep(refinery.item_interaction(scientist, core));
            Assert.Same(core, refinery.inserted_core);
            Assert.Equal(4f, Dm.num(refinery.get_required_radius(core.anomaly_type)));
        }
        Step(100);
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore).Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
        var bootRuntimes = _meta.Log.Runtimes.Take(runtimesBefore).Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
        foreach (var e in bootRuntimes)
            _output.WriteLine("boot runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(bootRuntimes);
    }
}
