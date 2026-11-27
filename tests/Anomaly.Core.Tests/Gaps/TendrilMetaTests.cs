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
/// Lavaland on the converted MetaStation: the cave generator placed its necropolis tendrils, each live with its eerie GPS signal,
/// its abilities and its necropolis floor about it, the fauna live around them, and none of them throws over some ticks.
/// </summary>
public class TendrilMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public TendrilMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
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
        "lavaland/tendril", "mining_mobs/", "necropolis_chests", "mining_loot/", "cain_and_abel", "structures/spawner", "icemoon/cave_entrance",
        "mapgen/CaveGenerator", "mapgen/biomes",
    };

    [MetaFact]
    public void Lavalands_caves_hold_live_tendrils_among_their_fauna_and_they_run_without_an_error()
    {
        int runtimesBefore = _meta.Log.Runtimes.Count;
        Step(40);
        using (_meta.World.Enter())
        {
            var tendrils = Dm.each<mob_living_basic_mining_tendril>().Where(m => m.z > 0).ToList();
            var fauna = Dm.each<mob_living_basic_mining>().Where(m => m.z > 0 && m is not mob_living_basic_mining_tendril).ToList();
            _output.WriteLine($"Lavaland: {tendrils.Count} tendrils, {fauna.Count} mining fauna");
            Assert.NotEmpty(tendrils);
            Assert.All(tendrils, t => Assert.False(InertSeam.IsInert(t), "a tendril is inert"));
            Assert.Equal(tendrils.Count, Dm.each<mob_living_basic_mining_tendril>(GLOB.tendrils).Count());
            Assert.All(tendrils, t => Assert.NotNull(t.GetComponent(typeof(datum_component_gps))));
            Assert.All(tendrils, t => Assert.True(Dm.each<datum_action_cooldown>(t.actions).Count() >= 4, "a tendril has its lash, chaser, spikes and stab"));
            Assert.All(tendrils, t => Assert.True(t.infected_turfs.len > 0, "a tendril turned no floor"));
            // the fauna about the tendrils (the biomes place them around)
            Assert.NotEmpty(fauna);
            Assert.True(tendrils.Any(t => fauna.Any(f => f.z == t.z && get_dist(f, t) <= 30)), "fauna lives near the tendrils");
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
