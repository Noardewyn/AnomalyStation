using System.IO;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.T2;

[DmPath("/obj/t2_loot_a")] public class obj_t2_loot_a : obj { }

[DmPath("/obj/t2_loot_b")] public class obj_t2_loot_b : obj { }

[DmPath("/obj/t2_loot_c")] public class obj_t2_loot_c : obj { }

[DmPath("/obj/t2_loot_d")] public class obj_t2_loot_d : obj { }

/// <summary>A random spawner whose loot, count and options the tests set before it initializes (rules 15 fixtures).</summary>
[DmPath("/obj/effect/spawner/random/t2_probe")]
public class obj_effect_spawner_random_t2_probe : obj_effect_spawner_random
{
    public static Action<obj_effect_spawner_random_t2_probe> Setup;

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        Setup?.Invoke(this);
    }
}

/// <summary>
/// M3 T2 phase B: the spawners against tg's source: unit-level rules of random/_random.dm:1-103 on probe loot, the window spawners'
/// spawn_list read out of tg's structure.dm, the maintenance spawners' loot table read out of tg's maintenance_loot.dm, and the
/// self-deletion (INITIALIZE_HINT_QDEL, spawners.dm).
/// </summary>
public class SpawnerTests : IClassFixture<MetaWorldFixture>
{
    // Setup is a static hook of the probe spawner: the tests of this class run one at a time (xUnit runs a class serially)
    private readonly ITestOutputHelper _output;
    private readonly MetaWorldFixture _meta;

    public SpawnerTests(ITestOutputHelper output, MetaWorldFixture meta)
    {
        _output = output;
        _meta = meta;
    }

    private static string Tg(string relative)
    {
        string clone = MapFixtures.TgClone();
        return clone == null ? null : Path.Combine(clone, relative);
    }

    private static List<string> Spawn(Action<obj_effect_spawner_random_t2_probe> setup, int x = 5, int y = 5, ulong seed = 21, int size = 11, Action<TgToyRoom> before = null)
    {
        var room = TgToyRoom.Create(size, size, seed: seed);
        using (room.World.Enter())
        {
            before?.Invoke(room);
            obj_effect_spawner_random_t2_probe.Setup = setup;
            try
            {
                var spawner = Dm.@new<obj_effect_spawner_random_t2_probe>(locate(x, y, 1));
                Assert.True(QDELETED(spawner), "a random spawner deletes itself (spawners.dm: INITIALIZE_HINT_QDEL)");
                Assert.Empty(room.RuntimeText);
            }
            finally
            {
                obj_effect_spawner_random_t2_probe.Setup = null;
            }
            return Loot(room);
        }
    }

    private static List<string> Loot(TgToyRoom room)
        => Enumerable.Range(1, room.Width).SelectMany(x => Enumerable.Range(1, room.Height).Select(y => (x, y)))
            .SelectMany(p => locate(p.x, p.y, 1).contents.Items().Cast<atom_movable>().Where(m => m is obj_t2_loot_a or obj_t2_loot_b or obj_t2_loot_c or obj_t2_loot_d).Select(m => $"{DmTypes.PathOf(m.GetType())}@{p.x},{p.y}"))
            .OrderBy(s => s, StringComparer.Ordinal).ToList();

    private static DmList Entries(params Type[] types) => Dm.list(types.Cast<object>().ToArray());

    [Fact]
    public void Count_picks_without_repeats_all_loot_and_chance_zero_follow_tgs_loop()
    {
        // spawn_loot_count = 3, spawn_loot_double = FALSE: each pick leaves the list (:63), so three different entries
        List<string> three = Spawn(s =>
        {
            s.loot = Entries(typeof(obj_t2_loot_a), typeof(obj_t2_loot_b), typeof(obj_t2_loot_c), typeof(obj_t2_loot_d));
            s.spawn_loot_count = 3;
            s.spawn_loot_double = 0f;
        });
        Assert.Equal(3, three.Count);
        Assert.Equal(3, three.Select(l => l.Split('@')[0]).Distinct().Count());
        Assert.All(three, l => Assert.EndsWith("@5,5", l)); // no scatter: the spawner's own turf

        // spawn_all_loot: every entry once, whatever the count (:48-50)
        List<string> all = Spawn(s =>
        {
            s.loot = Entries(typeof(obj_t2_loot_a), typeof(obj_t2_loot_b), typeof(obj_t2_loot_c), typeof(obj_t2_loot_d));
            s.spawn_all_loot = 1f;
        });
        Assert.Equal(new[] { "/obj/t2_loot_a", "/obj/t2_loot_b", "/obj/t2_loot_c", "/obj/t2_loot_d" }, all.Select(l => l.Split('@')[0]).OrderBy(x => x, StringComparer.Ordinal));

        // spawn_loot_chance = 0: `if(!prob(spawn_loot_chance)) return` (:41): nothing, and still deleted
        Assert.Empty(Spawn(s =>
        {
            s.loot = Entries(typeof(obj_t2_loot_a));
            s.spawn_loot_chance = 0f;
        }));

        // no loot: nothing, no runtime
        Assert.Empty(Spawn(s => s.loot = null));

        // with repeats allowed (the default) a one-entry list gives the count
        Assert.Equal(5, Spawn(s =>
        {
            s.loot = Entries(typeof(obj_t2_loot_a));
            s.spawn_loot_count = 5;
        }).Count);
    }

    [Fact]
    public void Weights_and_nested_lists_are_picked_as_pick_weight_recursive_does()
    {
        // a weighted list picks by weight: 99 to 1 over many spawners (the world RNG is seeded, so this does not flake)
        var room = TgToyRoom.Create(21, 21, seed: 5);
        int a = 0, b = 0;
        using (room.World.Enter())
        {
            obj_effect_spawner_random_t2_probe.Setup = s => s.loot = Dm.list(Dm.kv(typeof(obj_t2_loot_a), 99), Dm.kv(typeof(obj_t2_loot_b), 1));
            try
            {
                for (int x = 1; x <= 20; x++)
                    for (int y = 1; y <= 20; y++)
                        Dm.@new<obj_effect_spawner_random_t2_probe>(locate(x, y, 1));
            }
            finally
            {
                obj_effect_spawner_random_t2_probe.Setup = null;
            }
            foreach (string l in Loot(room))
            {
                if (l.StartsWith("/obj/t2_loot_a", StringComparison.Ordinal)) a++;
                else b++;
            }
        }
        Assert.Equal(400, a + b);
        Assert.InRange(a, 380, 400);
        Assert.True(a > 20 * b, $"{a} of the heavy entry, {b} of the light one");

        // a nested list is a group: the pick descends into it (pick_weight_recursive), the group's own entries are the result
        List<string> nested = Spawn(s => s.loot = Dm.list(Dm.kv(Dm.list(Dm.kv(typeof(obj_t2_loot_c), 1), Dm.kv(typeof(obj_t2_loot_d), 1)), 1)));
        Assert.Single(nested);
        Assert.Matches(@"^/obj/t2_loot_[cd]@5,5$", nested[0]);
    }

    [Fact]
    public void Scatter_puts_the_loot_on_distinct_open_turfs_in_view_within_the_radius()
    {
        List<string> scattered = Spawn(s =>
        {
            s.loot = Entries(typeof(obj_t2_loot_a));
            s.spawn_loot_count = 5;
            s.spawn_scatter_radius = 1;
        }, before: room =>
        {
            Dm.@new<turf_closed_wall>(locate(6, 5, 1)); // a closed turf in the radius takes none (:127)
        });
        Assert.Equal(5, scattered.Count);
        var places = scattered.Select(l => l.Split('@')[1]).ToList();
        Assert.Equal(places.Count, places.Distinct().Count()); // pick_n_take: a turf at most once
        foreach (string p in places)
        {
            int[] xy = p.Split(',').Select(int.Parse).ToArray();
            Assert.InRange(xy[0], 4, 6);
            Assert.InRange(xy[1], 4, 6);
            Assert.NotEqual("6,5", p);
        }
        // more loot than turfs: it stops with the turfs (`spawn_locations.len`, :71)
        List<string> many = Spawn(s =>
        {
            s.loot = Entries(typeof(obj_t2_loot_a));
            s.spawn_loot_count = 20;
            s.spawn_scatter_radius = 1;
        }, before: room => Dm.@new<turf_closed_wall>(locate(6, 5, 1)));
        Assert.Equal(8, many.Count); // the 3 x 3 square without the wall
    }

    [Fact]
    public void Split_offsets_follow_tgs_formula()
    {
        // spawn_loot_split: the first item stays, item i >= 1 gets x = 2*(i % 8) + 2*floor(i / 8), y = 2*(i % 8) (:77-81)
        var room = TgToyRoom.Create(11, 11);
        using (room.World.Enter())
        {
            obj_effect_spawner_random_t2_probe.Setup = s =>
            {
                s.loot = Entries(typeof(obj_t2_loot_a), typeof(obj_t2_loot_b), typeof(obj_t2_loot_c), typeof(obj_t2_loot_d));
                s.spawn_loot_count = 12;
                s.spawn_loot_split = 1f;
                s.spawn_loot_double = 1f;
            };
            try
            {
                Dm.@new<obj_effect_spawner_random_t2_probe>(locate(5, 5, 1));
            }
            finally
            {
                obj_effect_spawner_random_t2_probe.Setup = null;
            }
            var items = locate(5, 5, 1).contents.Items().Cast<atom_movable>().Where(m => m is obj_t2_loot_a or obj_t2_loot_b or obj_t2_loot_c or obj_t2_loot_d).ToList();
            Assert.Equal(12, items.Count);
            Assert.Equal(0f, items[0].pixel_x);
            for (int i = 1; i < 12; i++)
            {
                Assert.Equal(2 * (i % 8) + 2 * (i / 8), (int)items[i].pixel_x);
                Assert.Equal(2 * (i % 8), (int)items[i].pixel_y);
            }
        }
    }

    [Fact]
    public void A_maintenance_spawner_on_a_closed_closets_tile_puts_its_loot_inside_the_closet()
    {
        // maintenance.dm:69-72 (the M5 leftovers, 2026-10-16): closet.take_contents() of each closed closet on the spawner's turf;
        // an open one leaves the loot on the floor
        var room = TgToyRoom.Create(11, 11, seed: 23);
        using (room.World.Enter())
        {
            var closed = Dm.@new<obj_structure_closet>(locate(4, 5, 1));
            var open = Dm.@new<obj_structure_closet>(locate(6, 5, 1));
            Waiting.NoSleep(open.open());
            Dm.@new<obj_effect_spawner_random_maintenance_three>(locate(4, 5, 1));
            Dm.@new<obj_effect_spawner_random_maintenance_three>(locate(6, 5, 1));
            // the loot table also holds trash decals (the ash, maintenance_loot.dm:43), which a closet does not take in: three picks
            // are the items inside plus the decals left on the tile
            var closedTile = Dm.each_cast<atom>(locate(4, 5, 1).contents).ToList();
            Assert.Equal(3, Dm.each_cast<atom>(closed.contents).OfType<obj_item>().Count() + closedTile.OfType<obj_effect_decal_cleanable>().Count());
            Assert.Empty(closedTile.OfType<obj_item>());
            Assert.Empty(Dm.each_cast<atom>(open.contents).OfType<obj_item>());
            var floor = Dm.each_cast<atom>(locate(6, 5, 1).contents).ToList();
            Assert.True(floor.Count(a => a is obj_item || a is obj_effect_decal_cleanable) == 3,
                "on the open closet's tile: " + string.Join(", ", floor.Select(a => a.GetType().Name)));
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_loot_and_another_seed_another()
    {
        Action<obj_effect_spawner_random_t2_probe> setup = s =>
        {
            s.loot = Dm.list(Dm.kv(typeof(obj_t2_loot_a), 3), Dm.kv(typeof(obj_t2_loot_b), 3), Dm.kv(typeof(obj_t2_loot_c), 3), Dm.kv(typeof(obj_t2_loot_d), 3));
            s.spawn_loot_count = 8;
        };
        List<string> first = Spawn(setup, seed: 7);
        Assert.Equal(first, Spawn(setup, seed: 7));
        Assert.Contains(Enumerable.Range(8, 6), seed => !first.SequenceEqual(Spawn(setup, seed: (ulong)seed)));
    }

    // ---- tg's own spawn tables, read out of tg's source

    private static string StripComments(string line)
    {
        int c = line.IndexOf("//", StringComparison.Ordinal);
        return c >= 0 ? line.Substring(0, c) : line;
    }

    private static readonly HashSet<string> Defined = new(StringComparer.Ordinal);

    /// <summary>`spawn_list = list(...)` of each /obj/effect/spawner/structure type of structure.dm, and the types with an Initialize of their own.</summary>
    private static (Dictionary<string, List<string>> lists, HashSet<string> overrides) ReadStructureSpawners(string file)
    {
        var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var overrides = new HashSet<string>(StringComparer.Ordinal);
        string current = null;
        Defined.Clear();
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = StripComments(raw);
            if (line.StartsWith("/obj/effect/spawner/structure", StringComparison.Ordinal))
            {
                Match init = Regex.Match(line, @"^(/obj/effect/spawner/structure[A-Za-z0-9_/]*)/Initialize\(");
                if (init.Success) overrides.Add(init.Groups[1].Value);
                else if (!line.Contains('(', StringComparison.Ordinal))
                {
                    current = line.Trim();
                    Defined.Add(current);
                }
                continue;
            }
            if (current != null && line.StartsWith("\tspawn_list", StringComparison.Ordinal))
                lists[current] = Regex.Matches(line, @"/obj/[A-Za-z0-9_/]+").Select(m => m.Value).ToList();
        }
        return (lists, overrides);
    }

    [TgCloneFact]
    public void Window_spawners_give_the_grille_and_the_window_set_of_tgs_spawn_list_and_delete_themselves()
    {
        var (lists, overrides) = ReadStructureSpawners(Tg("code/game/objects/effects/spawners/structure.dm"));
        int tested = 0, hollowInert = 0;
        foreach (Type spawner in DmTypes.Subtypes(typeof(obj_effect_spawner_structure_window), true))
        {
            string path = DmTypes.PathOf(spawner);
            if (!Defined.Contains(path)) continue; // defined in another file (the indestructible ones): not this file's table
            // the list a type uses is its own, else its nearest parent's; a type with an Initialize of its own picks by dir
            string owner = path;
            while (owner != null && !lists.ContainsKey(owner)) owner = owner.Contains('/') ? owner.Substring(0, owner.LastIndexOf('/')) : null;
            if (owner == null) continue;
            if (path.Contains("/hollow", StringComparison.Ordinal))
            {
                // the hollow spawners pick their spawn_list by dir in an Initialize of their own (structure.dm:33-180); they are
                // inert in M3 (P4: no Meta map uses them), so their dir tables are not tested; they must stay inert, not spawn a wrong set
                Assert.True(InertSeam.IsInertType(spawner) || overrides.Contains(path) || lists.ContainsKey(path), path);
                if (InertSeam.IsInertType(spawner)) hollowInert++;
                continue;
            }
            Assert.True(!overrides.Contains(path) || path == "/obj/effect/spawner/structure/window", path);
            var room = TgToyRoom.Create(5, 5);
            using (room.World.Enter())
            {
                turf at = locate(3, 3, 1);
                var made = (obj_effect_spawner_structure_window)Dm.@new(spawner, at);
                Assert.True(QDELETED(made), path + " deletes itself");
                var found = at.contents.Items().Cast<atom_movable>().Select(m => DmTypes.PathOf(m.GetType())).OrderBy(x => x, StringComparer.Ordinal).ToList();
                Assert.True(lists[owner].OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(found), $"{path} (list of {owner}): [{string.Join(", ", found)}], tg's spawn_list is [{string.Join(", ", lists[owner])}]");
                Assert.Equal(RCD_MEMORY_WINDOWGRILLE, at.rcd_memory); // structure.dm:29
                Assert.Empty(room.RuntimeText);
                tested++;
            }
        }
        _output.WriteLine($"{tested} window spawner types tested, {hollowInert} hollow ones inert");
        Assert.True(tested >= 5, $"{tested}");
    }

    [MetaFact]
    public void No_live_spawner_is_left_on_Meta_and_the_ones_left_are_inert()
    {
        using (_meta.World.Enter())
        {
            int inert = 0, live = 0;
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    foreach (atom_movable m in locate(x, y, 1).contents.Items())
                    {
                        if (m is not obj_effect_spawner) continue;
                        if (InertSeam.IsInert(m)) inert++;
                        else live++;
                    }
                }
            }
            _output.WriteLine($"spawners left on Meta: {live} live, {inert} inert");
            Assert.Equal(0, live);
        }
    }

    private static readonly Regex TypePath = new(@"/(?:obj|mob|turf)/[A-Za-z0-9_/]+", RegexOptions.CultureInvariant);

    [TgCloneFact]
    public void Maintenance_spawners_make_only_things_of_tgs_loot_table_and_the_same_seed_makes_the_same()
    {
        // What a maintenance spawner can put on its tile: the paths of tg's loot table, the paths of the random spawners it nests
        // (their own tables, spawners/random/*.dm), and the descendants of the loot_type_path / loot_subtype_path those name.
        var inTable = new HashSet<string>(StringComparer.Ordinal);
        var families = new List<string>();
        var files = new List<string> { Tg("code/_globalvars/lists/maintenance_loot.dm") };
        files.AddRange(Directory.GetFiles(Tg("code/game/objects/effects/spawners/random"), "*.dm"));
        foreach (string file in files)
        {
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = StripComments(raw);
                foreach (Match m in TypePath.Matches(line))
                {
                    inTable.Add(m.Value);
                    if (line.Contains("loot_type_path", StringComparison.Ordinal) || line.Contains("loot_subtype_path", StringComparison.Ordinal)) families.Add(m.Value);
                }
            }
        }
        Assert.True(inTable.Count > 200, $"{inTable.Count} paths in tg's tables");
        bool Allowed(string path) => inTable.Contains(path) || families.Any(f => path.StartsWith(f + "/", StringComparison.Ordinal));

        List<string> Run(ulong seed, out List<string> outside)
        {
            var room = TgToyRoom.Create(25, 25, seed: seed);
            using (room.World.Enter())
            {
                var types = new[] { typeof(obj_effect_spawner_random_maintenance), typeof(obj_effect_spawner_random_maintenance_two), typeof(obj_effect_spawner_random_maintenance_three) };
                var spawners = new List<obj_effect_spawner_random>();
                for (int x = 2; x <= 24; x++)
                    for (int y = 2; y <= 24; y++)
                        spawners.Add((obj_effect_spawner_random)Dm.@new(types[(x + y) % 3], locate(x, y, 1)));
                Assert.All(spawners, s => Assert.True(QDELETED(s)));
                var made = new List<string>();
                outside = new List<string>();
                for (int x = 1; x <= 25; x++)
                {
                    for (int y = 1; y <= 25; y++)
                    {
                        foreach (atom_movable m in locate(x, y, 1).contents.Items())
                        {
                            string path = DmTypes.PathOf(m.GetType());
                            made.Add($"{x},{y} {path}");
                            if (!Allowed(path)) outside.Add(path);
                        }
                    }
                }
                return made;
            }
        }

        List<string> first = Run(3, out List<string> outsideFirst);
        _output.WriteLine($"{first.Count} things from 529 spawners; outside tg's table: {string.Join(", ", outsideFirst.Distinct().Take(10))}");
        Assert.True(first.Count > 200, $"{first.Count}");
        // decals and the like that a loot type spawns beside itself (a `new` in a loot item's Initialize) are not part of the table
        Assert.True(outsideFirst.Count == 0, $"{outsideFirst.Count} of {first.Count} are outside tg's table: {string.Join(", ", outsideFirst.Distinct().Take(10))}");
        Assert.Equal(first, Run(3, out _));
        Assert.NotEqual(first, Run(4, out _));
    }
}
