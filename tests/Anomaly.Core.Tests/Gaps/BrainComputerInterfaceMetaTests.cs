using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The brain-computer interface and the USB ports on the converted MetaStation: its two BCI implanters boot live, closed and empty,
/// with their board; the station's machines that tg gives a USB port (the pumps, valves, meters, air and fire alarms, light switches,
/// status displays, conveyor switches, vitals displays, consoles) carry it with their circuit components' types; no runtime comes from
/// the BCI, the USB port or the machines' circuit components.
/// </summary>
public class BrainComputerInterfaceMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public BrainComputerInterfaceMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "brain_computer_interface", "usb_port", "/wiremod/shell/implant", "/wiremod/components/bci/", "/wiremod/components/usb/", "air_alarm_circuit", "computer_circuit" };

    [MetaFact]
    public void Metas_bci_implanters_and_usb_ports_boot_live()
    {
        using (_meta.World.Enter())
        {
            var implanters = new List<obj_machinery_bci_implanter>();
            var ported = new Dictionary<string, int>();
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is obj_machinery_bci_implanter implanter)
                            implanters.Add(implanter);
                        if (thing.GetComponent(typeof(datum_component_usb_port)) is datum_component_usb_port)
                        {
                            string name = thing.GetType().Name;
                            ported[name] = ported.TryGetValue(name, out int n) ? n + 1 : 1;
                        }
                    }
                }
            }
            foreach (var (name, count) in ported.OrderBy(p => p.Key))
                _output.WriteLine($"{name}: {count} with a USB port");
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");

            Assert.Equal(2, implanters.Count);
            Assert.All(implanters, i =>
            {
                Assert.False(InertSeam.IsInert(i));
                Assert.False(Dm.truthy(i.state_open));
                Assert.Null(i.occupant);
                Assert.Null(i.bci_to_implant);
                Assert.Equal("bci_implanter", i.icon_state);
                Assert.IsType<obj_item_circuitboard_machine_bci_implanter>(i.circuit_value);
                Assert.True(Dm.truthy(i.occupant_typecache[typeof(mob_living_carbon_human)]));
            });
            Assert.True(ported.Keys.Any(k => k.StartsWith(nameof(obj_machinery_airalarm), StringComparison.Ordinal)), "no air alarm with a USB port");
            Assert.True(ported.Keys.Any(k => k.StartsWith(nameof(obj_machinery_firealarm), StringComparison.Ordinal)), "no fire alarm with a USB port");
            Assert.True(ported.Keys.Any(k => k.StartsWith(nameof(obj_machinery_light_switch), StringComparison.Ordinal)), "no light switch with a USB port");
            Assert.True(ported.Keys.Any(k => k.StartsWith(nameof(obj_machinery_atmospherics_components_binary_pump), StringComparison.Ordinal)), "no pump with a USB port");
            Assert.True(ported.Keys.Any(k => k.StartsWith(nameof(obj_machinery_status_display_evac), StringComparison.Ordinal)), "no evac display with a USB port");
            Assert.True(ported.Keys.Any(k => k.StartsWith(nameof(obj_machinery_vitals_reader), StringComparison.Ordinal)), "no vitals display with a USB port");
            Assert.Empty(runtimes);
        }
    }
}
