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
/// Lavaland's generated caves on the converted MetaStation: the biomes spawned the common fauna (goliaths, legions, watchers,
/// brimdemons, lobstrosities, bileworms, goldgrubs and the raptors' spawners), each live and with its respawner, and none of
/// them throws over some ticks of hunting. The mining base keeps its raptor ranch: the chicks and younglings, their troughs
/// and the RaptorDexes. (The ice moon is not loaded with MetaStation, so its mobs are covered by IceMoonMobTests alone.)
/// </summary>
public class LavalandFaunaMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public LavalandFaunaMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
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
        "basic/lavaland/mining", "basic/lavaland/goliath", "basic/lavaland/watcher", "basic/lavaland/legion", "basic/lavaland/hivelord",
        "basic/lavaland/basilisk", "basic/lavaland/brimdemon", "basic/lavaland/bileworm", "basic/lavaland/lobstrosity", "basic/lavaland/node_drone",
        "monster_organs", "kinetic_crusher", "mining_mob_respawner", "mapgen/biomes",
        "basic/lavaland/goldgrub", "basic/lavaland/gutlunchers", "basic/lavaland/raptor", "basic/lavaland/mook", "riding_minigame", "raptordex",
    };

    [MetaFact]
    public void Lavalands_caves_hold_the_live_fauna_and_it_hunts_without_an_error()
    {
        int runtimesBefore = _meta.Log.Runtimes.Count;
        // the respawners' first mobs come up out of the ground: each waits inside its unburrow or fly-down effect for its 2.5 s
        // animation (mining_mob_respawner.dm:139-163) before it stands on its turf
        Step(100);
        using (_meta.World.Enter())
        {
            var fauna = Dm.each<mob_living_basic_mining>().Where(m => m.z > 0).ToList();
            var respawners = Dm.each<obj_effect_mining_mob_respawner>().ToList();
            var byType = fauna.GroupBy(m => m.GetType().Name).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count());
            int inert = fauna.Count(m => InertSeam.IsInert(m));
            _output.WriteLine($"Lavaland: {fauna.Count} mining mobs ({string.Join(", ", byType)}), {inert} inert; {respawners.Count} respawners");
            Assert.NotEmpty(fauna);
            Assert.Contains(fauna, m => m is mob_living_basic_mining_goliath);
            Assert.Contains(fauna, m => m is mob_living_basic_mining_legion);
            Assert.Contains(fauna, m => m is mob_living_basic_mining_watcher);
            Assert.Equal(0, fauna.Count(m => !InertSeam.IsInert(m) && m.ai_controller == null && m is not mob_living_basic_mining_legion_brood));
            Assert.NotEmpty(respawners);
            Assert.All(respawners, s => Assert.DoesNotContain(SPAWN_MEGAFAUNA, Dm.each<object>(s.valid_mobs)));

            Assert.Contains(fauna, m => m is mob_living_basic_mining_goldgrub);
            var raptors = Dm.each<mob_living_basic_raptor>().Where(m => m.z > 0).ToList();
            _output.WriteLine($"Raptors: {raptors.Count} ({string.Join(", ", raptors.GroupBy(m => m.growth_stage).Select(g => g.Key + " " + g.Count()))})");
            Assert.NotEmpty(raptors);
            Assert.DoesNotContain(raptors, m => InertSeam.IsInert(m) || m.ai_controller == null || m.raptor_color is not datum_raptor_color);
            // the mining base's ranch (Lavaland.dmm): three younglings and a chick, four troughs and three RaptorDexes
            Assert.Contains(raptors, m => m.growth_stage == RAPTOR_YOUNG);
            Assert.Contains(raptors, m => m.growth_stage == RAPTOR_BABY);
            Assert.Equal(4, Dm.each<obj_structure_ore_container_food_trough_raptor_trough>().Count(t => t.z > 0 && !InertSeam.IsInert(t)));
            Assert.Equal(3, Dm.each<obj_item_raptor_dex>().Count(d => get_turf(d) != null && !InertSeam.IsInert(d)));
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
