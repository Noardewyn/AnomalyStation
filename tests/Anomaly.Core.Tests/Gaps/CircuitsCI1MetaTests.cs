using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package CI1 on the converted MetaStation: Meta's compact remotes and controllers (with their shell and its component), circuit
/// multitools, component printers (linked to the station's techweb with its circuit designs) and module duplicators boot live, with no
/// runtime from the circuits' files. Meta's two loaded circuits stay inert (CI2's speech and hear components, not-translated section 231).
/// </summary>
public class CircuitsCI1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public CircuitsCI1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "/wiremod/", "shell.cs", "circuit_component.cs", "wiremod_composite.cs", "clock_component.cs", "wire_bundle_component.cs",
    };

    [MetaFact]
    public void Metas_circuit_shells_tools_and_printers_boot_live()
    {
        using (_meta.World.Enter())
        {
            var things = new List<atom>();
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is obj_item_compact_remote or obj_item_controller or obj_item_multitool_circuit or obj_machinery_component_printer
                            or obj_machinery_module_duplicator or obj_item_integrated_circuit)
                            things.Add(thing);
                    }
                }
            }
            foreach (var group in things.GroupBy(t => t.GetType().Name))
                _output.WriteLine($"{group.Key}: {group.Count()}");
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");

            Assert.Equal(4, things.OfType<obj_item_compact_remote>().Count());
            Assert.Equal(3, things.OfType<obj_item_controller>().Count());
            Assert.Equal(6, things.OfType<obj_item_multitool_circuit>().Count());
            Assert.Equal(2, things.OfType<obj_machinery_component_printer>().Count());
            Assert.Equal(2, things.OfType<obj_machinery_module_duplicator>().Count());
            Assert.All(things.Where(t => t is not obj_item_integrated_circuit), thing => Assert.False(InertSeam.IsInert(thing), thing.GetType().Name + " is inert"));
            Assert.All(things.OfType<obj_item>().Where(t => t is obj_item_compact_remote or obj_item_controller), shell =>
            {
                var component = shell.GetComponent(typeof(datum_component_shell)) as datum_component_shell;
                Assert.NotNull(component);
                Assert.Equal(1f, Dm.length(component.unremovable_circuit_components));
            });
            Assert.All(things.OfType<obj_machinery_component_printer>(), printer =>
            {
                Assert.NotNull(printer.techweb);
                Assert.True(Dm.length(printer.current_unlocked_designs) > 20, "the printer offers " + Dm.length(printer.current_unlocked_designs));
                Assert.NotNull(printer.materials);
            });
            // the loaded circuits wait for CI2's speech and hear components
            Assert.All(things.OfType<obj_item_integrated_circuit>(), circuit => Assert.True(InertSeam.IsInert(circuit), circuit.GetType().Name + " is live"));
            Assert.Empty(runtimes);
        }
    }
}
