using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The circuits on the converted MetaStation: science's loaded circuits, two of each (tg's hello_world and speech_relay presets) boot live
/// with their speech and hear components, the relay's speech wired from its hear component, each with its cell; no runtime comes
/// from the circuits' components, shells or presets.
/// </summary>
public class CircuitShellMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public CircuitShellMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "/wiremod/", "shell.cs", "boris_circuit_container", "shuttle_move_deferred_checks" };

    [MetaFact]
    public void Metas_loaded_circuits_boot_live_with_their_components()
    {
        using (_meta.World.Enter())
        {
            var circuits = new List<obj_item_integrated_circuit>();
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is obj_item_integrated_circuit circuit)
                            circuits.Add(circuit);
                    }
                }
            }
            foreach (obj_item_integrated_circuit circuit in circuits)
                _output.WriteLine($"{circuit.GetType().Name} at {circuit.x},{circuit.y}: {string.Join(", ", Dm.each<obj_item_circuit_component>(circuit.attached_components).Select(c => c.GetType().Name))}");
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");

            Assert.Equal(2, circuits.OfType<obj_item_integrated_circuit_loaded_hello_world>().Count());
            Assert.Equal(2, circuits.OfType<obj_item_integrated_circuit_loaded_speech_relay>().Count());
            Assert.All(circuits, c => Assert.False(InertSeam.IsInert(c), c.GetType().Name + " is inert"));
            Assert.All(circuits, c => Assert.NotNull(c.cell));
            foreach (var hello in circuits.OfType<obj_item_integrated_circuit_loaded_hello_world>())
            {
                var helloSpeech = Assert.Single(Dm.each<obj_item_circuit_component>(hello.attached_components).OfType<obj_item_circuit_component_speech>());
                Assert.Equal("Hello World", Dm.str(helloSpeech.message.value));
            }
            foreach (var relay in circuits.OfType<obj_item_integrated_circuit_loaded_speech_relay>())
            {
                var relayHear = Assert.Single(Dm.each<obj_item_circuit_component>(relay.attached_components).OfType<obj_item_circuit_component_hear>());
                var relaySpeech = Assert.Single(Dm.each<obj_item_circuit_component>(relay.attached_components).OfType<obj_item_circuit_component_speech>());
                Assert.Contains(relayHear.message_port, Dm.each<datum_port_output>(relaySpeech.message.connected_ports));
            }
            Assert.Empty(runtimes);
        }
    }
}
