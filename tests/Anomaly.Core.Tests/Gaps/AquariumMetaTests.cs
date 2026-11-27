using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The aquariums on the converted MetaStation: the lawyer's fish tank boots live with its props and its goldfish in stasis, which
/// stays alive over the ticks; the mapped aquarium kit and fish feed are live; and nothing in the aquariums' files throws.
/// </summary>
public class AquariumMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public AquariumMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_fish_tank_keeps_its_goldfish_alive()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        obj_item_fish_tank_lawyer tank;
        obj_item_fish fish;
        using (_meta.World.Enter())
        {
            var tanks = Dm.each<obj_item_fish_tank>().ToList();
            _output.WriteLine($"Meta: {tanks.Count} fish tanks, {Dm.each<obj_structure_aquarium>().Count()} aquariums");
            tank = tanks.OfType<obj_item_fish_tank_lawyer>().Single();
            Assert.False(InertSeam.IsInert(tank));
            Assert.True(HAS_TRAIT(tank, TRAIT_IS_AQUARIUM));
            Assert.True(HAS_TRAIT_FROM(tank, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT), "the lawyer's tank starts in stasis");
            fish = Dm.each_cast<atom>(tank.contents).OfType<obj_item_fish>().Single();
            Assert.Equal(FISH_ALIVE, fish.status);
            Assert.Equal(2, Dm.each_cast<atom>(tank.contents).OfType<obj_item_aquarium_prop>().Count());
            Assert.Equal(3, Dm.each_cast<atom>(tank.vis_contents).OfType<obj_effect_aquarium>().Count());
            Assert.True(Dm.num(((datum_reagents)tank.reagents).total_volume) > 0, "its feed storage holds the goldfish's food");
            Assert.All(Dm.each<obj_item_aquarium_kit>().ToList(), kit => Assert.False(InertSeam.IsInert(kit)));
            Assert.NotEmpty(Dm.each<obj_item_reagent_containers_cup_fish_feed>().ToList());
        }
        Step(60 * 20);
        using (_meta.World.Enter())
        {
            Assert.Equal(FISH_ALIVE, fish.status);
            Assert.Same(tank, fish.loc);
            Assert.True(Dm.num(fish.get_health_percentage()) >= 0.99f, "the goldfish is healthy: " + fish.get_health_percentage());
        }

        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => (e.ToString() ?? "").Contains("aquarium", StringComparison.OrdinalIgnoreCase) || (e.ToString() ?? "").Contains("fish", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
