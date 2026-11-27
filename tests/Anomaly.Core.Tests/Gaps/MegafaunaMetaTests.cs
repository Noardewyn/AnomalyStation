using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Lavaland on the converted MetaStation: the cave generator placed its megafauna (the ash drake, Bubblegum, the colossus), the
/// map holds the legion behind the necropolis gate, each live with its GPS signal, the menacing vents' bosses waiting, and none of
/// them throws over some ticks.
/// </summary>
public class MegafaunaMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MegafaunaMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
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
        "simple_animal/", "hostile/megafauna", "basic/boss", "actions/mobs", "mining_loot/megafauna", "trophies_megafauna", "boss_vent",
        "mapgen/CaveGenerator", "mapgen/biomes",
    };

    [MetaFact]
    public void Lavalands_caves_hold_live_megafauna_and_they_run_without_an_error()
    {
        int runtimesBefore = _meta.Log.Runtimes.Count;
        Step(40);
        using (_meta.World.Enter())
        {
            var megafauna = Dm.each<mob_living_simple_animal_hostile_megafauna>().Where(m => m.z > 0).ToList();
            var vents = Dm.each<obj_structure_ore_vent_boss>().ToList();
            var byType = megafauna.GroupBy(m => m.GetType().Name).Select(g => g.Key + " " + g.Count());
            _output.WriteLine($"Lavaland: {megafauna.Count} megafauna ({string.Join(", ", byType)}); {vents.Count} menacing vents");
            Assert.NotEmpty(megafauna);
            Assert.All(megafauna, m => Assert.False(InertSeam.IsInert(m), m.GetType().Name + " is inert"));
            // the caves' kinds, the legion the map holds in the necropolis behind its gate (Lavaland.dmm), and the hierophant of its
            // arena when the ruins' seeding rolls that ruin (lavaland_surface_hierophant.dmm)
            Assert.All(megafauna, m => Assert.True(m is mob_living_simple_animal_hostile_megafauna_dragon
                || m is mob_living_simple_animal_hostile_megafauna_bubblegum || m is mob_living_simple_animal_hostile_megafauna_colossus
                || m is mob_living_simple_animal_hostile_megafauna_legion || m is mob_living_simple_animal_hostile_megafauna_hierophant,
                m.GetType().Name + " is not one Lavaland places"));
            Assert.Equal(1, megafauna.Count(m => m is mob_living_simple_animal_hostile_megafauna_legion));
            Assert.NotNull(GLOB.necropolis_gate);
            // at most one Bubblegum: the generator takes it off its list once placed (CaveGenerator.dm:382)
            Assert.True(megafauna.Count(m => m is mob_living_simple_animal_hostile_megafauna_bubblegum) <= 1, "one Bubblegum at most");
            Assert.All(megafauna, m => Assert.NotNull(m.GetComponent(typeof(datum_component_gps))));
            Assert.All(vents, v => Assert.NotNull(v.summoned_boss));
        }
        Step(100);
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore).Where(e =>
        {
            string text = e.ToString() ?? "";
            return Files.Any(f => text.Contains(f, StringComparison.Ordinal));
        }).ToList();
        foreach (var e in runtimes.Take(5))
            _output.WriteLine(e.ToString());
        Assert.Empty(runtimes);
    }
}
