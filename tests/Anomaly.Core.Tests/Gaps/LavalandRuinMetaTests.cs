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
/// Lavaland's ruins on the converted MetaStation: SSmapping seeded the Lavaland level with tg's budget, the guaranteed ruins among
/// them, each ruin loaded on the unexplored wastes with its areas and its things, and none of the ruins' code throws over some
/// ticks.
/// </summary>
public class LavalandRuinMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public LavalandRuinMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
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
        "mapping/ruins", "datums/ruins", "lavalandruin_code", "objects_and_mobs/", "ashwalker", "mining_roles", "ruin_defender", "step_triggers",
        "mission_code/Cabin", "vehicles/atv", "machinery/other/miner", "mapping_helpers", "turfs/open/sand", "turfs/open/ice", "turfs/open/grass",
        "turfs/open/ashplanet", "misc_walls", "mineral_walls", "bombable_turf", "debuffs/cursed",
    };

    [MetaFact]
    public void Lavaland_is_seeded_with_its_ruins_on_tgs_budget_and_they_run_without_an_error()
    {
        using (_meta.World.Enter())
        {
            var lavaland = Dm.each(SSmapping.levels_by_trait(ZTRAIT_LAVA_RUINS)).Select(z => (int)Dm.num(z)).ToList();
            Assert.Single(lavaland);
            var landmarks = Dm.each<obj_effect_landmark_ruin>(GLOB.ruin_landmarks).ToList();
            foreach (var landmark in landmarks)
                _output.WriteLine($"{landmark.ruin_template.name} at {landmark.x},{landmark.y},{landmark.z} (cost {landmark.ruin_template.cost})");
            Assert.NotEmpty(landmarks);
            Assert.All(landmarks, l => Assert.Equal(lavaland[0], (int)l.z));
            Assert.Contains(landmarks, l => l.ruin_template is datum_map_template_ruin_lavaland_hierophant);
            Assert.Contains(landmarks, l => l.ruin_template is datum_map_template_ruin_lavaland_blood_drunk_miner);
            Assert.Contains(landmarks, l => l.ruin_template is datum_map_template_ruin_lavaland_elite_tumor);
            float budget = Dm.num(config.Get(typeof(datum_config_entry_number_lavaland_budget)));
            Assert.Equal(60f, budget);
            float picked = landmarks.Where(l => !Dm.truthy(l.ruin_template.always_place)).Sum(l => l.ruin_template.cost);
            Assert.True(picked <= budget, "the picked ruins overspent the budget: " + picked);
            Assert.Equal(0f, SSmapping.reserved_ruins.len);
            // the ruins' maps loaded with their own areas
            int ruinAreas = Dm.each<turf>(block(1, 1, lavaland[0], Dm.world.maxx, Dm.world.maxy, lavaland[0])).Select(t => t.loc).OfType<area_ruin>().Distinct().Count();
            _output.WriteLine($"{ruinAreas} ruin areas on Lavaland");
            Assert.True(ruinAreas >= 5, "the ruins' areas are on Lavaland");
            Assert.True(Dm.each<mob_living_simple_animal_hostile_megafauna_hierophant>().Any(m => m.z == lavaland[0]), "the hierophant is in its arena");
            // the ruins' live types are live on the level
            var inert = Dm.each<atom>().Where(a => a.z == lavaland[0] && InertSeam.IsInert(a)).Select(a => a.GetType().Name).Distinct().OrderBy(n => n).ToList();
            _output.WriteLine("inert on Lavaland: " + string.Join(", ", inert));
        }
        Step(200);
        // the boot's (the seeding and the ruins' loading) and the ticks'
        var runtimes = _meta.Log.Runtimes.Where(e =>
        {
            string text = e.ToString() ?? "";
            return Files.Any(f => text.Contains(f, StringComparison.Ordinal));
        }).ToList();
        foreach (var e in runtimes.Take(5))
            _output.WriteLine(e.ToString());
        Assert.Empty(runtimes);
    }
}
