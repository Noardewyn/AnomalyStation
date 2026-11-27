using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Lavaland's ruin seeding (tg's seedRuins): the ruin templates of the mounted maps are preloaded, the cheapest first; the seeding spends
/// the level's budget, always places the guaranteed ruins (the hierophant's arena, a blood-drunk miner, the elite tumours), never two
/// of a ruin that allows no duplicates, only on the unexplored ash, clear of the level's edges, and the same seed seeds the same
/// ruins in the same places. The room's second level is a Lavaland level 120 tiles on a side.
/// </summary>
public class RuinSeedingTests
{
    private readonly ITestOutputHelper _output;

    public RuinSeedingTests(ITestOutputHelper output) => _output = output;

    private const int Size = 120;

    /// The converted ruins (maps/RandomRuins/**.as3dmap) at tg's paths, as the host mounts them (MapSeam).
    private static void MountRuins(World w)
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        string root = Path.Combine(MapFixtures.RepoRoot(), "maps", "RandomRuins");
        foreach (string converted in Directory.GetFiles(root, "*" + MapText.Extension, SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, converted).Replace('\\', '/');
            files.WriteText("_maps/RandomRuins/" + relative[..^MapText.Extension.Length] + ".dmm", File.ReadAllText(converted));
        }
        TgStrings.Mount(files, TgClone.Root, new[] { "strings/modular_maps/syndicatebase.toml" }); // the syndicate base's rooms
    }

    /// A room whose level 2 is Lavaland: ash and rock in the unexplored wastes' area, the ruin templates preloaded.
    /// <paramref name="unexploredFromX"/> leaves the columns west of it outside the wastes.
    private static AudioRoom Room(ulong seed, int unexploredFromX = 1)
    {
        var r = new AudioRoom(Size, Size, seed: seed, levels: 2, configure: MountRuins);
        r.In(() =>
        {
            SSmapping.z_list = Dm.list();
            SSmapping.z_level_to_plane_offset ??= Dm.list();
            SSmapping.z_level_to_lowest_plane_offset ??= Dm.list();
            SSmapping.gravity_by_z_level ??= Dm.list();
            SSmapping.z_level_to_stack ??= Dm.list();
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))), FALSE, contain_turfs: FALSE);
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(2, "Lavaland", Dm.list(Dm.kv(ZTRAIT_MINING, TRUE), Dm.kv(ZTRAIT_LAVA_RUINS, TRUE))), FALSE, contain_turfs: FALSE);
            var wastes = Dm.@new<area_lavaland_surface_outdoors_unexplored>();
            var explored = Dm.@new<area_lavaland_surface_outdoors>();
            foreach (turf t in Dm.each<turf>(block(1, 1, 2, Size, Size, 2)))
            {
                t.change_area((area)t.loc, t.x >= unexploredFromX ? wastes : explored);
                Waiting.NoSleep(t.ChangeTurf((t.x + t.y) % 3 == 0 ? typeof(turf_closed_mineral_volcanic_lava_land_surface) : typeof(turf_open_misc_asteroid_basalt_lava_land_surface)));
            }
            SSmapping.current_map = Dm.@new<datum_map_config>();
            SSmapping.current_map.minetype = MINETYPE_LAVALAND;
            SSmapping.themed_ruins = Dm.list();
            SSmapping.ruins_templates = Dm.list();
            SSmapping.preloadRuinTemplates();
        });
        return r;
    }

    /// No runtimes but tg's own: the pride ruin's door stands on weak lava under a stone tile, and the lava's Initialize burns what
    /// stands on it (`initialize_occupant`, lava.dm:84) before the tile has made the lava safe and before the door has its integrity,
    /// so the door's take_damage CRASHes (atom_defense.dm:22), as in tg; and the food cart's table and the syndicate base's slot machine (below).
    private static void AssertNoRuntimes(AudioRoom r)
    {
        r.Room.Runtimes.RemoveAll(e => e.Message.Contains("cult airlock taking damage while having <= 0 integrity", StringComparison.Ordinal));
        // and the beach's food cart's packed table, nullspaced in the cart, queued for smoothing on its Initialize (structures.dm:23)
        r.Room.Runtimes.RemoveAll(e => e.Message.Contains("/obj/structure/table/reinforced called smooth_icon() without being on a z-level", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    private record Placed(string Name, Type Type, int X, int Y, float Cost, bool Duplicates);

    /// Seeds the Lavaland level with tg's budget and loads the reserved ruins: the ruins placed, by their landmarks.
    private static List<Placed> Seed(AudioRoom r, float budget = 60)
    {
        r.In(() =>
        {
            Waiting.NoSleep(seedRuins(Dm.list(2), budget, Dm.list(typeof(area_lavaland_surface_outdoors_unexplored)), SSmapping.themed_ruins[ZTRAIT_LAVA_RUINS] as DmList,
                clear_below: TRUE, mineral_budget: 15, mineral_budget_update: OREGEN_PRESET_LAVALAND, ruins_type: ZTRAIT_LAVA_RUINS));
            Waiting.NoSleep(SSmapping.load_reserved_ruins());
        });
        r.Step(2);
        return r.In(() => Dm.each<obj_effect_landmark_ruin>(GLOB.ruin_landmarks)
            .Select(l => new Placed(l.ruin_template.name, l.ruin_template.GetType(), (int)l.x, (int)l.y, l.ruin_template.cost, Dm.truthy(l.ruin_template.allow_duplicates)))
            .ToList());
    }

    [Fact]
    public void The_ruin_templates_of_the_mounted_maps_are_preloaded_cheapest_first()
    {
        var r = Room(13601);
        r.In(() =>
        {
            var lava = (DmList)SSmapping.themed_ruins[ZTRAIT_LAVA_RUINS];
            Assert.NotNull(lava);
            var names = Dm.each(lava).Select(Dm.str).ToList();
            foreach (string name in new[] { "Lava-Ruin Hierophant's Arena", "Lava-Ruin Ash Walker Nest", "Lava-Ruin Syndicate Lava Base", "Lava-Ruin Pulsating Tumor", "Lava-Ruin Ancient Puzzle" })
                Assert.Contains(name, names);
            var costs = Dm.each(SSmapping.ruins_templates).Select(k => ((datum_map_template_ruin)SSmapping.ruins_templates[k]).cost).ToList();
            Assert.Equal(costs.OrderBy(c => c).ToList(), costs); // tg sorts the cheapest first (cmp_ruincost_priority)
            Assert.All(Dm.each(lava).Select(k => (datum_map_template_ruin)lava[k]), t => Assert.True(t.width > 0 && t.height > 0, t.name + " has no size"));
        });
        AssertNoRuntimes(r);
    }

    [Fact]
    public void Seeding_spends_the_budget_and_always_places_the_guaranteed_ruins_once_each_clear_of_the_edges()
    {
        var r = Room(13602);
        List<Placed> placed = Seed(r);
        foreach (Placed p in placed)
            _output.WriteLine($"{p.Name} at {p.X},{p.Y} (cost {p.Cost})");
        Assert.NotEmpty(placed);
        Assert.Contains(placed, p => p.Type == typeof(datum_map_template_ruin_lavaland_hierophant));
        Assert.Contains(placed, p => typeof(datum_map_template_ruin_lavaland_blood_drunk_miner).IsAssignableFrom(p.Type));
        Assert.Contains(placed, p => p.Type == typeof(datum_map_template_ruin_lavaland_elite_tumor));
        // the forced ruins come out of the budget too; what was picked after them fits what was left
        float forced = placed.Where(p => p.Type == typeof(datum_map_template_ruin_lavaland_hierophant) || p.Type == typeof(datum_map_template_ruin_lavaland_elite_tumor)
            || typeof(datum_map_template_ruin_lavaland_blood_drunk_miner_random).IsAssignableFrom(p.Type)).Sum(p => p.Cost);
        Assert.True(placed.Sum(p => p.Cost) <= 60 + forced, "the budget was overspent: " + placed.Sum(p => p.Cost));
        foreach (var group in placed.Where(p => !p.Duplicates).GroupBy(p => p.Name))
            Assert.True(group.Count() == 1, group.Key + " was placed twice");
        r.In(() =>
        {
            foreach (obj_effect_landmark_ruin landmark in Dm.each<obj_effect_landmark_ruin>(GLOB.ruin_landmarks))
            {
                var template = landmark.ruin_template;
                Assert.Equal(2f, landmark.z);
                Assert.True(landmark.x - template.width / 2 > TRANSITIONEDGE && landmark.x + template.width / 2 <= Size - TRANSITIONEDGE,
                    template.name + " is too near the level's edge");
                Assert.True(landmark.y - template.height / 2 > TRANSITIONEDGE && landmark.y + template.height / 2 <= Size - TRANSITIONEDGE,
                    template.name + " is too near the level's edge");
            }
            // the ruins' own things came with them: the hierophant's arena holds its hierophant
            Assert.True(Dm.each<mob_living_simple_animal_hostile_megafauna_hierophant>().Any(m => m.z == 2), "the hierophant is in its arena");
            Assert.True(Dm.each<obj_structure_elite_tumor>().Any(m => m.z == 2), "an elite tumour");
            // the active ruins are what was placed
            Assert.Equal(placed.Count, Dm.each(SSmapping.active_ruins).Count());
            Assert.Equal(0f, SSmapping.reserved_ruins.len);
        });
        AssertNoRuntimes(r);
    }

    [Fact]
    public void Ruins_land_only_on_the_unexplored_wastes()
    {
        // the western half is explored ground: no ruin may touch it
        var r = Room(13603, unexploredFromX: Size / 2);
        List<Placed> placed = Seed(r, budget: 30);
        Assert.NotEmpty(placed);
        r.In(() =>
        {
            foreach (obj_effect_landmark_ruin landmark in Dm.each<obj_effect_landmark_ruin>(GLOB.ruin_landmarks))
            {
                var template = landmark.ruin_template;
                float west = landmark.x - Dm.round(template.width / 2);
                Assert.True(west >= Size / 2, template.name + " reaches into the explored ground at x " + west);
            }
        });
    }

    [Fact]
    public void The_same_seed_seeds_the_same_ruins_in_the_same_places()
    {
        List<Placed> first = Seed(Room(13604));
        List<Placed> second = Seed(Room(13604));
        Assert.Equal(first.Select(p => (p.Name, p.X, p.Y)).ToList(), second.Select(p => (p.Name, p.X, p.Y)).ToList());
    }
}
