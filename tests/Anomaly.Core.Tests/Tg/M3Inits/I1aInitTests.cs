using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Inits;

/// <summary>A maintenance loot spawner fixture (rules 15) that records what it makes: three picks from GLOB.maintenance_loot.</summary>
[DmPath("/obj/effect/spawner/random/maintenance/test_i1a_recording")]
public class test_i1a_recording_maint_spawner : obj_effect_spawner_random_maintenance
{
    public readonly List<Type> Made = new();

    public test_i1a_recording_maint_spawner()
    {
        spawn_loot_count = 3;
    }

    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        Made.Add(type_path_to_make as Type);
        return await base.make_item(spawn_loc, type_path_to_make);
    }
}

// M3 I1a: the var typing by kinds (types-converter.md 3.5), the random posters, the maintenance loot.
public class I1aInitTests
{
    private static TgToyRoom Room(ulong seed = 21) => TgToyRoom.Create(9, 9, seed: seed);

    // ---- task 1: var typing ----

    [Fact]
    public void MapTextIdsFitTheVarsTheyEdit()
    {
        var room = Room();
        using (room.World.Enter())
        {
            // Meta's edits (MetaStation.dmm): the preloader's "expected a number, got text" before the typing rule
            world.preloader_setup(Dm.list(Dm.kv("id", "aftport")), typeof(obj_machinery_power_solar_control));
            var solar = Dm.@new<obj_machinery_power_solar_control>(locate(4, 4, 1));
            world.preloader_setup(Dm.list(Dm.kv("id", "crematoriumChapel")), typeof(obj_structure_bodycontainer_crematorium));
            var crematorium = Dm.@new<obj_structure_bodycontainer_crematorium>(locate(5, 4, 1));
            Assert.Empty(room.RuntimeText);
            Assert.Equal("aftport", solar.id);
            Assert.Equal("crematoriumChapel", crematorium.id);
            // the defaults stay tg's numbers (solar.dm:422, morgue.dm:430)
            Assert.True(Dm.eq(new obj_machinery_power_solar_control().id, 0));
            Assert.True(Dm.eq(new obj_structure_bodycontainer_crematorium().id, 1));
        }
    }

    [Fact]
    public void AMailSortingJunctionReadsTextTagsFromTheMapEditor()
    {
        var room = TgToyRoom.Boot(9, 9, game: false, mapExtras: _ =>
        {
            turf floor = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            // pipe_sorting.dm:26: "sortType is to be set in map editor", a number or a list separated by ;
            world.preloader_setup(Dm.list(Dm.kv("sortType", "1;2")), typeof(obj_structure_disposalpipe_sorting_mail));
            Dm.@new<obj_structure_disposalpipe_sorting_mail>(floor);
        });
        using (room.World.Enter())
        {
            var junction = Dm.locate<obj_structure_disposalpipe_sorting_mail>(locate(4, 4, 1));
            Assert.NotNull(junction);
            Assert.Equal("1;2", junction.sortType);
            Assert.Equal(2, junction.sortTypes.Count); // pipe_sorting.dm:42-47: the istext branch
            Assert.True(Dm.eq(junction.sortTypes[1], 1));
            Assert.True(Dm.eq(junction.sortTypes[2], 2));
        }
    }

    [Fact]
    public void TheDecalElementKeepsItsIconStateInTgsVar()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var decal = Dm.@new<obj_effect_turf_decal_tile_blue>(locate(4, 4, 1));
            Assert.Empty(room.RuntimeText);
            var elements = new List<datum_element_decal>();
            foreach (object key in Dm.each(SSdcs.elements_by_type))
                if (SSdcs.elements_by_type[key] is datum_element_decal e) elements.Add(e);
            Assert.Contains(elements, e => e.base_icon_state is string s && s == decal.icon_state); // _decal.dm:82
        }
    }

    // ---- task 2: posters ----

    private static List<string> Posters(ulong seed, int count)
    {
        var room = Room(seed);
        var picked = new List<string>();
        using (room.World.Enter())
        {
            for (int i = 0; i < count; i++)
            {
                var poster = Dm.@new<obj_structure_sign_poster_random>(locate(2 + i % 6, 2 + i / 6, 1));
                Type cut = poster.cutdown_type as Type;
                Assert.NotNull(cut);
                Assert.NotEqual(typeof(obj_structure_sign_poster_random), cut);
                Assert.True(cut.IsSubclassOf(typeof(obj_structure_sign_poster)));
                // poster.dm:197: an icon state, not never_random, not pixel shifted; the blacklist (:313-316)
                Assert.False(cut.IsSubclassOf(typeof(obj_structure_sign_poster_traitor)) || cut == typeof(obj_structure_sign_poster_traitor));
                Assert.Equal(Dm.initial(cut, (obj_structure_sign_poster p) => p.icon_state), poster.icon_state);
                Assert.NotEqual("random_anything", poster.icon_state);
                Assert.False(Dm.truthy(Dm.initial(cut, (obj_structure_sign_poster p) => p.never_random)));
                if (!Dm.truthy(poster.ruined))
                    Assert.Equal("poster - " + poster.original_name, poster.name); // poster.dm:166-168
                picked.Add(DmTypes.PathOf(cut));
            }
            Assert.Empty(room.RuntimeText);
        }
        return picked;
    }

    [Fact]
    public void ARandomPosterBecomesARealPosterDeterministicallyPerSeed()
    {
        List<string> first = Posters(7, 12);
        Assert.Equal(first, Posters(7, 12));
        Assert.NotEqual(first, Posters(8, 12)); // the world RNG picks (pick(approved_types), poster.dm:201)
        Assert.True(first.Distinct().Count() > 1);
    }

    // ---- task 4: cleanable decals ----

    [Fact]
    public void DirtOnATiledFloorSmoothsAndASecondDirtMergesIntoIt()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var floor = Dm.@new<turf_open_floor_iron>(locate(4, 4, 1));
            Assert.True(Dm.truthy(floor.tiled_turf));
            var dirt = Dm.@new<obj_effect_decal_cleanable_dirt>(floor);
            Assert.False(InertSeam.IsInert(dirt));
            Assert.Equal("dirt-0", dirt.icon_state); // mess.dm:89-93: the tiled dirt smooths
            Assert.True(Dm.eq(dirt.icon, Dm.resource("icons/effects/dirt.dmi")));
            Assert.True(Dm.truthy(Dm.bitand(dirt.smoothing_flags, SMOOTH_BITMASK)));
            Assert.True(HAS_TRAIT(dirt, TRAIT_MOPABLE)); // cleanable.dm:31-32
            var second = Dm.@new<obj_effect_decal_cleanable_dirt>(floor);
            Assert.True(QDELETED(second)); // cleanable.dm:25-29: merged into the first (INITIALIZE_HINT_QDEL)
            Assert.False(QDELETED(dirt));

            var plating = Dm.@new<turf_open_floor_wood>(locate(5, 4, 1)); // tiled_turf = FALSE (fancy_floor.dm:19)
            var flat = Dm.@new<obj_effect_decal_cleanable_dirt>(plating);
            Assert.StartsWith("dirt-flat-", flat.icon_state); // mess.dm:84: not tiled, no smoothing
            var dust = Dm.@new<obj_effect_decal_cleanable_dirt_dust>(locate(6, 4, 1));
            Assert.Equal("dust", dust.icon_state); // mess.dm:111
            var web = Dm.@new<obj_effect_decal_cleanable_cobweb>(locate(6, 5, 1));
            Assert.False(HAS_TRAIT(web, TRAIT_MOPABLE)); // is_mopped = FALSE (mess.dm:167)
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void ADecalOnAClosedTurfDeletesItself()
    {
        var room = Room();
        using (room.World.Enter())
        {
            var dirt = Dm.@new<obj_effect_decal_cleanable_dirt>(locate(1, 1, 1)); // the room's rim is a closed turf
            Assert.True(QDELETED(dirt)); // decal.dm:13-17: NeverShouldHaveComeHere
        }
    }

    // ---- task 3: maintenance loot ----

    private static HashSet<Type> LootLeaves(DmList table, HashSet<Type> into = null)
    {
        into ??= new HashSet<Type>();
        foreach (object item in Dm.each(table))
        {
            if (item is DmList inner) LootLeaves(inner, into);
            else into.Add((Type)item);
        }
        return into;
    }

    private static (List<Type> Made, bool Deleted, int Left) Maint(ulong seed)
    {
        var room = Room(seed);
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            var spawner = Dm.@new<test_i1a_recording_maint_spawner>(at);
            Assert.Same(GLOB.maintenance_loot, spawner.loot); // maintenance.dm:28-29
            HashSet<Type> leaves = LootLeaves(GLOB.maintenance_loot);
            Assert.All(spawner.Made, t => Assert.Contains(t, leaves)); // only tg's loot table
            return (spawner.Made, QDELETED(spawner), Dm.each_cast<atom>(at).Count(a => a is obj_effect_spawner_random_maintenance));
        }
    }

    [Fact]
    public void AMaintenanceSpawnerMakesTgsLootDeterministicallyAndDeletesItself()
    {
        var first = Maint(11);
        Assert.Equal(3, first.Made.Count);
        Assert.True(first.Deleted); // INITIALIZE_HINT_QDEL (misc.dm:34)
        Assert.Equal(0, first.Left);
        Assert.Equal(first.Made, Maint(11).Made);
        Assert.Contains(Enumerable.Range(12, 8).Select(s => Maint((ulong)s).Made), other => !other.SequenceEqual(first.Made));
    }

    [Fact]
    public void TheMaintenanceTablesAreTgs()
    {
        var room = Room();
        using (room.World.Enter())
        {
            // maintenance_loot.dm:418-424: the five pools and their weights
            DmList pools = GLOB.maintenance_loot;
            Assert.Equal(5, pools.Count);
            Assert.True(Dm.eq(pools[GLOB.trash_loot], 4500));
            Assert.True(Dm.eq(pools[GLOB.common_loot], 4500));
            Assert.True(Dm.eq(pools[GLOB.uncommon_loot], 900));
            Assert.True(Dm.eq(pools[GLOB.rarity_loot], 99));
            Assert.True(Dm.eq(pools[GLOB.oddity_loot], 1));
            Assert.Equal(GLOB.maintenance_loot.Count, GLOB.dumpster_loot.Count); // :427, a copy
            Assert.NotSame(GLOB.maintenance_loot, GLOB.dumpster_loot);
            Assert.Equal(271, LeafEntries(GLOB.maintenance_loot)); // every typepath of the five pools (:17-407)
            Assert.Contains(typeof(obj_item_trash_can), LootLeaves(GLOB.trash_loot)); // :19
            Assert.Contains(typeof(obj_item_toy_cards_deck_tarot_haunted), LootLeaves(GLOB.oddity_loot)); // :406
            Assert.Equal(9, GLOB.ratking_trash.Count);
            Assert.Equal(4, GLOB.ratking_coins.Count);
        }
    }

    private static int LeafEntries(DmList table)
    {
        var all = new List<Type>();
        void Walk(DmList l)
        {
            foreach (object item in Dm.each(l))
            {
                if (item is DmList inner) Walk(inner);
                else all.Add((Type)item);
            }
        }
        Walk(table);
        return all.Count;
    }
}
