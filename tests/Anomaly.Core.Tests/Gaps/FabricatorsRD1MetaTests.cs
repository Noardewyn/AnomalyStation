using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RD1 on the converted MetaStation: Meta's protolathes, circuit imprinters and departmental techfabs and its autolathe boot
/// live, each fabricator linked to the station's techweb with the designs its department and build type allow, with no runtime from the
/// fabricators' files.
/// </summary>
public class FabricatorsRD1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public FabricatorsRD1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "_production.cs", "protolathe.cs", "circuit_imprinter.cs", "autolathe.cs", "rdmachines.cs", "r_n_d.cs", "material_insert.cs",
    };

    [MetaFact]
    public void Metas_fabricators_and_autolathe_boot_live()
    {
        using (_meta.World.Enter())
        {
            var fabricators = new List<obj_machinery_rnd_production>();
            var autolathes = new List<obj_machinery_autolathe>();
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is obj_machinery_rnd_production fab)
                            fabricators.Add(fab);
                        else if (thing is obj_machinery_autolathe lathe)
                            autolathes.Add(lathe);
                    }
                }
            }
            foreach (obj_machinery_rnd_production fab in fabricators)
                _output.WriteLine($"{fab.GetType().Name} at {fab.x},{fab.y}: {Dm.length(fab.cached_designs)} designs, web {fab.stored_research?.GetType().Name}, silo {(fab.materials?.silo != null ? "linked" : "pending")}");
            foreach (obj_machinery_autolathe lathe in autolathes)
                _output.WriteLine($"autolathe at {lathe.x},{lathe.y}: {Dm.length(lathe.stored_research?.researched_designs)} designs");
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");

            Assert.Equal(9, fabricators.Count);
            Assert.Single(autolathes);
            Assert.All(fabricators.Cast<atom>().Concat(autolathes), thing => Assert.False(InertSeam.IsInert(thing), thing.GetType().Name + " is inert"));
            Assert.All(fabricators, fab =>
            {
                Assert.IsType<datum_techweb_science>(fab.stored_research);
                Assert.True(Dm.length(fab.cached_designs) > 0, fab.GetType().Name + " offers nothing");
                Assert.NotNull(fab.materials);
            });
            Assert.All(autolathes, lathe => Assert.IsType<datum_techweb_autounlocking_autolathe>(lathe.stored_research));
            Assert.Empty(runtimes);
        }
    }
}
