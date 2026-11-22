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
/// Gaps package AS1 on the converted MetaStation: Meta's assemblies (the mapped timers, igniters, proximity sensors, signalers and
/// mousetrap, those in its lockers and boxes, the mousetrap boxes' traps, the electropacks) boot live, the timers idle and unarmed
/// traps unarmed, with no runtime from AS1's files.
/// </summary>
public class AssembliesAS1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public AssembliesAS1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "assembly/holder.cs", "assembly/timer.cs", "assembly/health.cs", "assembly/infrared.cs", "assembly/mousetrap.cs",
        "assembly/voice.cs", "assembly/assembly.cs", "electrified_buckle.cs", "electricchair.cs",
    };

    // the kinds AS1 ports or finishes; the flash (assembly/flash.dm) is another package's
    private static bool Mine(obj_item_assembly a) => a is obj_item_assembly_timer or obj_item_assembly_health or obj_item_assembly_infra
        or obj_item_assembly_mousetrap or obj_item_assembly_voice or obj_item_assembly_igniter or obj_item_assembly_prox_sensor
        or obj_item_assembly_signaler or obj_item_assembly_shock_kit;

    [MetaFact]
    public void Metas_assemblies_boot_live_and_idle()
    {
        for (int tick = 0; tick < 40; tick++) // two seconds: SSobj fires
            _meta.World.Step();
        using (_meta.World.Enter())
        {
            var assemblies = Dm.each<obj_item_assembly>().ToList();
            var holders = Dm.each<obj_item_assembly_holder>().ToList();
            var boxes = Dm.each<obj_item_storage_box_mousetraps>().ToList();
            var packs = Dm.each<obj_item_electropack>().ToList();
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            foreach (atom thing in assemblies.Where(Mine).Cast<atom>().Concat(holders).Concat(boxes).Concat(packs))
            {
                if (InertSeam.IsInert(thing))
                    inertKinds.Add(thing.GetType().Name);
            }
            var timers = assemblies.OfType<obj_item_assembly_timer>().ToList();
            var traps = assemblies.OfType<obj_item_assembly_mousetrap>().ToList();
            var ticking = timers.Where(t => Dm.truthy(t.timing)).ToList();
            var armed = traps.Where(t => Dm.truthy(t.armed) && t is not obj_item_assembly_mousetrap_armed).ToList();
            var emptyBoxes = boxes.Where(b => !Dm.each<obj_item_assembly_mousetrap>(b.contents).Any()).ToList();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {assemblies.Count} assemblies ({string.Join(", ", assemblies.GroupBy(a => a.GetType().Name).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key))})");
            _output.WriteLine($"Meta: {holders.Count} holders, {boxes.Count} mousetrap boxes ({emptyBoxes.Count} empty), {packs.Count} electropacks; inert {inertKinds.Count} ({string.Join(", ", inertKinds)}); "
                + $"{ticking.Count} timers ticking, {armed.Count} traps armed; AS1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(timers.Count >= 5, "too few timers");
            Assert.True(traps.Count >= 1, "no mousetrap");
            Assert.True(boxes.Count >= 1, "no mousetrap box");
            Assert.Empty(inertKinds);
            Assert.Empty(ticking);
            Assert.Empty(armed);
            Assert.Empty(emptyBoxes);
            Assert.Empty(runtimes);
        }
    }
}
