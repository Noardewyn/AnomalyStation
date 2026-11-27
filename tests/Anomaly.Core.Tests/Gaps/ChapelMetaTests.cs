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
/// The chapel on the converted MetaStation: the altar of the gods and the chaplain's old shard boot live, the altar takes a sect and
/// shows it, and some seconds go by with no runtime from the chapel's files.
/// </summary>
public class ChapelMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ChapelMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "religion_sects.cs", "rites.cs", "religion_structures.cs", "religious_tool.cs", "soulstone.cs", "shade.cs", "dreaming.cs",
        "religion.cs", "bibles.cs",
    };

    [MetaFact]
    public void Metas_altar_takes_a_sect_and_the_chapel_runs_without_an_error()
    {
        obj_structure_altar_of_gods altar;
        using (_meta.World.Enter())
        {
            var altars = Dm.each<obj_structure_altar_of_gods>(Dm.world).ToList();
            var shards = Dm.each<obj_item_soulstone_anybody_chaplain>(Dm.world).ToList();
            _output.WriteLine($"Meta: {altars.Count} altars of the gods, {shards.Count} chaplain's shards");
            altar = Assert.Single(altars);
            Assert.False(InertSeam.IsInert(altar));
            Assert.NotNull(altar.GetComponent(typeof(datum_component_religious_tool)));
            Assert.Contains(Dm.each<object>(GLOB.chaplain_altars), a => a == altar);
            Assert.All(shards, s => Assert.False(InertSeam.IsInert(s)));
            Assert.NotEmpty(shards);
            Assert.Null(GLOB.religious_sect);

            Waiting.NoSleep(set_new_religious_sect(typeof(datum_religion_sect_mechanical)));
            Assert.IsType<datum_religion_sect_mechanical>(GLOB.religious_sect);
            Assert.Equal(Dm.str(GLOB.religious_sect.altar_icon_state), altar.icon_state);
        }
        for (int tick = 0; tick < 200; tick++)
            _meta.World.Step();
        using (_meta.World.Enter())
        {
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Empty(runtimes);
        }
    }
}
