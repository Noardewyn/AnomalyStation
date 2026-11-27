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
/// Fishing on the converted MetaStation: the station's toilets are fishing spots, the water tiles lazy ones; the fish-porter boots
/// live and opens its aquarium portal; the mapped rods and the fishing toolboxes are live; a crewman casts a mapped rod at a
/// toilet and gets a bite; and nothing in fishing's files throws over the ticks.
/// </summary>
public class FishingMetaTests : IClassFixture<MetaWorldFixture>
{
    private const float BITING_PHASE = 2;

    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public FishingMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
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
    public void Metas_toilets_water_and_fish_porter_fish()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        mob_living_carbon_human angler;
        obj_structure_toilet toilet;
        using (_meta.World.Enter())
        {
            var toilets = Dm.each<obj_structure_toilet>().Where(t => Dm.truthy(is_station_level(t.z))).ToList();
            _output.WriteLine($"Meta: {toilets.Count} station toilets");
            Assert.True(toilets.Count >= 10, "Meta's toilets: " + toilets.Count);
            Assert.All(toilets, t => Assert.True(HAS_TRAIT(t, TRAIT_FISHING_SPOT), "a station toilet is a fishing spot"));
            // the hot springs of Lavaland's ruins are still inert (not-translated.md section 251)
            var water = Dm.each<turf_open_water>().Where(t => !InertSeam.IsInert(t)).ToList();
            _output.WriteLine($"Meta: {water.Count} live water tiles");
            Assert.True(water.Count >= 6, "Meta's pools: " + water.Count);
            Assert.All(water, t => Assert.True(HAS_TRAIT(t, TRAIT_FISHING_SPOT), "a water tile is a lazy fishing spot"));

            var porters = Dm.each<obj_machinery_fishing_portal_generator>().ToList();
            _output.WriteLine($"Meta: {porters.Count} fish-porters, {porters.Count(p => Dm.truthy(is_station_level(p.z)))} on the station");
            var porter = porters.Single(p => Dm.truthy(is_station_level(p.z)));
            Assert.False(InertSeam.IsInert(porter));
            var rods = Dm.each<obj_item_fishing_rod>().ToList();
            Assert.True(rods.Count >= 2, "the mapped rods: " + rods.Count);
            Assert.All(rods, rod => Assert.False(InertSeam.IsInert(rod)));
            var toolboxes = Dm.each<obj_item_storage_toolbox_fishing>().ToList();
            Assert.True(toolboxes.Count >= 2, "the fishing toolboxes: " + toolboxes.Count);
            Assert.All(toolboxes, box => Assert.Contains(Dm.each<atom>(box.contents), a => a is obj_item_fishing_rod));

            // the porter opens its aquarium portal for whoever turns it on
            angler = Dm.@new<mob_living_carbon_human>(get_step(porter, SOUTH));
            Waiting.NoSleep(porter.activate(GLOB.preset_fish_sources[typeof(datum_fish_source_portal)] as datum_fish_source, angler));
            Assert.True(HAS_TRAIT(porter, TRAIT_FISHING_SPOT), "the porter's open portal is a fishing spot");
            Waiting.NoSleep(porter.deactivate());

            // a rod cast at the toilet next to the angler
            turf Beside(atom a) => Dm.each(range(1, a)).OfType<turf>()
                .FirstOrDefault(t => t is turf_open && t != a.loc && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(c => Dm.truthy(c.density)));
            toilet = toilets.First(t => Beside(t) != null);
            turf beside = Beside(toilet);
            Waiting.NoSleep(angler.forceMove(beside));
            var rod = Dm.@new<obj_item_fishing_rod>(beside);
            Assert.True(Dm.truthy(Waiting.NoSleep(angler.put_in_hands(rod))));
            Waiting.NoSleep(rod.melee_attack_chain(angler, toilet));
        }
        datum_fishing_challenge challenge = null;
        bool biting = false;
        for (int i = 0; i < 60 * 20 && !biting; i++)
        {
            Step(1);
            using (_meta.World.Enter())
            {
                challenge ??= GLOB.fishing_challenges_by_user[angler] as datum_fishing_challenge;
                biting = challenge != null && challenge.phase == BITING_PHASE;
            }
        }
        Assert.True(challenge != null, "the cast began a challenge");
        Assert.True(biting, "a fish bit");
        using (_meta.World.Enter())
            qdel(challenge);
        Step(20);

        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => (e.ToString() ?? "").Contains("fish", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
