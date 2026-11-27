using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The fish: a fish out of water flops and dies (its death message and its dead sprite), a fish case keeps it alive, feeding
/// sates it and grows it, a fish that reproduces on its own breeds in a tank, a fish grows into another fish or into a mob, an
/// evolution's conditions, the traits on a fish and on a grown mob, butchering into fillets and eating a fish, the export price,
/// the trophy mount, the fish encyclopedia's window, and a sweep over every fish (their sizes and weights, death and deletion).
/// </summary>
public class FishTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int w = 13, int h = 9)
    {
        var r = new AudioRoom(w, h, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    /// <summary>A crate standing in for an aquarium (the aquariums come later): the fish inside count as in an aquarium.</summary>
    private static obj_structure_closet_crate Tank(AudioRoom r, int x, int y)
    {
        var tank = r.In(() => Dm.@new<obj_structure_closet_crate>(locate(x, y, 1)));
        r.Step(2);
        r.In(() => ADD_TRAIT(tank, TRAIT_IS_AQUARIUM, "fish test"));
        return tank;
    }

    [Fact]
    public void A_fish_out_of_water_flops_and_dies()
    {
        var (r, feed, _) = Room(14601);
        var fish = r.In(() => Dm.@new<obj_item_fish_zipzap>(locate(7, 4, 1)));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(FISH_ALIVE, fish.status);
            Assert.True(HAS_TRAIT(fish, TRAIT_FISH_FLOPPING), "a fish on the floor flops");
        });
        bool dead = false;
        for (int i = 0; i < 180 && !dead; i++)
        {
            r.Step(Second);
            dead = r.In(() => fish.status == FISH_DEAD);
        }
        Assert.True(dead, "the zipzap died out of water");
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(fish, TRAIT_FISH_FLOPPING), "a dead fish stops flopping");
            Assert.Equal("zipzap_dead", fish.icon_state);
        });
        Assert.True(feed.Any("dies"), "the death message");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_fish_case_keeps_its_fish_alive()
    {
        var (r, _, _) = Room(14602);
        var fishcase = r.In(() => Dm.@new<obj_item_storage_fish_case>(locate(7, 4, 1)));
        var fish = r.In(() => Dm.@new<obj_item_fish_goldfish>(locate(7, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(fish.forceMove(fishcase));
            Assert.True(HAS_TRAIT(fish, TRAIT_FISH_STASIS), "the case puts the fish in stasis");
        });
        r.Step(120 * Second);
        r.In(() =>
        {
            Assert.Equal(FISH_ALIVE, fish.status);
            Assert.True(Dm.num(fish.get_health_percentage()) >= 1, "the fish in stasis is not hurt");
            // the case grows to the fish's size and back
            var item = (obj_item)fishcase;
            Assert.True(item.w_class >= fish.w_class);
            Waiting.NoSleep(fish.forceMove(fishcase.loc));
            Assert.False(HAS_TRAIT(fish, TRAIT_FISH_STASIS), "out of the case the stasis ends");
            Assert.Equal(WEIGHT_CLASS_SMALL, item.w_class);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Feeding_sates_a_fish_and_grows_it()
    {
        var (r, _, _) = Room(14603);
        var fish = r.In(() => Dm.@new<obj_item_fish_goldfish>(locate(7, 4, 1)));
        var can = r.In(() => Dm.@new<obj_item_reagent_containers_cup_fish_feed>(locate(7, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            fish.last_feeding = 0; // the fish should be hungry
            Assert.True(Dm.num(fish.get_hunger()) > 0, "the fish is hungry");
            Waiting.NoSleep(fish.feed((datum_reagents)can.reagents));
            Assert.Equal(0f, Dm.num(fish.get_hunger()));

            // fed when its hunger peaks, it grows
            fish.last_feeding = world.time - fish.feeding_frequency * FISH_GROWTH_PEAK;
            float old_size = Dm.num(fish.size), old_weight = Dm.num(fish.weight);
            Waiting.NoSleep(((datum_reagents)can.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 2.5f));
            Waiting.NoSleep(fish.feed((datum_reagents)can.reagents));
            Assert.True(Dm.num(fish.size) > old_size, "the fish grew longer");
            Assert.True(Dm.num(fish.weight) > old_weight, "the fish grew heavier");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_fish_that_reproduces_alone_breeds_in_a_tank()
    {
        var (r, _, _) = Room(14604);
        r.World.Rng.Reseed(14604, 1);
        var tank = Tank(r, 7, 4);
        var fish = r.In(() =>
        {
            var f = Dm.@new<obj_item_fish_jumpercable>(tank);
            f.last_feeding = world.time;
            f.breeding_wait = 0; // a new fish waits before its first breeding
            return f;
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(fish, TRAIT_FISH_SELF_REPRODUCE), "the jumpercable reproduces on its own");
            Assert.True(Dm.truthy(fish.ready_to_reproduce()));
            var jr = Waiting.NoSleep(fish.try_to_reproduce()) as obj_item_fish_jumpercable;
            Assert.NotNull(jr);
            Assert.Same(tank, jr.loc);
            Assert.True(Dm.num(fish.breeding_wait) > world.time, "the parent waits before it breeds again");
            Assert.False(Dm.truthy(fish.ready_to_reproduce()));
            // a fish that does not reproduce alone finds no partner without an aquarium's candidates
            var goldfish = Dm.@new<obj_item_fish_goldfish>(tank);
            Assert.False(Dm.truthy(Waiting.NoSleep(goldfish.try_to_reproduce())));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_evolution_needs_its_condition()
    {
        var (r, _, _) = Room(14605);
        r.In(() =>
        {
            var clownfish = Dm.@new<obj_item_fish_clownfish>(null);
            var lube = (datum_fish_evolution)GLOB.fish_evolutions[typeof(datum_fish_evolution_lubefish)];
            Assert.Contains(typeof(datum_fish_evolution_lubefish), Dm.each<Type>(clownfish.evolution_types));
            Assert.False(Dm.truthy(Waiting.NoSleep(lube.check_conditions(clownfish, null, null))), "an unfed clownfish does not turn lubefish");
            // fed space lube, it can
            var reagents = Dm.@new<datum_reagents>(10);
            Waiting.NoSleep(reagents.add_reagent(typeof(datum_reagent_lube), 5));
            var lubeReagent = (datum_reagent)reagents.has_reagent(typeof(datum_reagent_lube));
            Assert.True(Dm.truthy(Waiting.NoSleep(lubeReagent.used_on_fish(clownfish))));
            Assert.True(HAS_TRAIT(clownfish, TRAIT_FISH_FED_LUBE));
            Assert.True(Dm.truthy(Waiting.NoSleep(lube.check_conditions(clownfish, null, null))), "a clownfish fed lube can turn lubefish");
            // its offspring by that evolution is a lubefish with the evolution's traits
            var jr = Waiting.NoSleep(clownfish.create_offspring(lube.new_fish_type, evolution: lube)) as obj_item_fish;
            Assert.IsType<obj_item_fish_clownfish_lube>(jr);
            foreach (Type trait in Dm.each<Type>(lube.new_traits))
                Assert.Contains(trait, Dm.each<Type>(jr.fish_traits));
            Assert.False(string.IsNullOrEmpty(Dm.str(lube.get_evolution_tooltip())), "the evolution explains its conditions");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_chasm_crab_grows_into_a_lobstrosity()
    {
        var (r, _, _) = Room(14606);
        r.World.Rng.Reseed(14606, 1);
        var crab = r.In(() => Dm.@new<obj_item_fish_chasm_crab>(locate(7, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            crab.last_feeding = world.time;
            var growth = (datum_component_fish_growth)Dm.each<object>(crab.GetComponents(typeof(datum_component_fish_growth))).First();
            Assert.Equal(crab.lob_type, growth.result_type);
            growth.maturation = 100;
            growth.on_fish_life(crab, 1);
            Assert.True(QDELETED(crab), "the crab is gone once grown");
            var lobster = Dm.each<mob_living_basic_mining_lobstrosity_juvenile>(locate(7, 4, 1).contents).FirstOrDefault();
            Assert.NotNull(lobster);
            // every fish trait applied to the grown mob does not error out
            foreach (object trait_type in Dm.each(GLOB.fish_traits))
                Waiting.NoSleep(((datum_fish_trait)GLOB.fish_traits[trait_type]).apply_to_mob(lobster));
        });
        r.Step(5 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_fryish_grows_into_a_fritterish()
    {
        var (r, _, _) = Room(14607);
        var tank = Tank(r, 7, 4);
        var fry = r.In(() =>
        {
            var f = Dm.@new<obj_item_fish_fryish>(tank);
            f.last_feeding = world.time;
            // the aquarium's safe water and temperature, which its own component sets on its fish
            f.fish_flags = Dm.bitor(f.fish_flags, FISH_FLAG_SAFE_TEMPERATURE | FISH_FLAG_SAFE_FLUID);
            return f;
        });
        r.Step(2);
        r.In(() =>
        {
            var growth = (datum_component_fish_growth)Dm.each<object>(fry.GetComponents(typeof(datum_component_fish_growth))).First();
            Assert.NotNull(growth);
            growth.maturation = 100;
            growth.on_fish_life(fry, 1);
            Assert.True(QDELETED(fry));
            var grown = Dm.each<obj_item_fish_fryish_fritterish>(tank.contents).FirstOrDefault();
            Assert.NotNull(grown);
            Assert.Contains(grown.variant, new[] { "fritterish", "bernard", "matthew" });
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_traits_change_the_fish()
    {
        var (r, _, bob) = Room(14608);
        r.In(() =>
        {
            // amphibious: the mastodon breathes the room's air on the floor
            var amphibious = Dm.@new<obj_item_fish_mastodon>(locate(8, 6, 1));
            Assert.True(HAS_TRAIT(amphibious, TRAIT_FISH_AMPHIBIOUS));
            Assert.True(Dm.truthy(Waiting.NoSleep(amphibious.proper_environment())), "an amphibious fish lives in the room's air");
            var goldfish = Dm.@new<obj_item_fish_goldfish>(locate(8, 6, 1));
            Assert.False(Dm.truthy(Waiting.NoSleep(goldfish.proper_environment())), "a goldfish does not");
            // heavy: the slowdown of a heavy fish
            var pike = Dm.@new<obj_item_fish_pike>(locate(8, 6, 1));
            Assert.True(HAS_TRAIT(pike, TRAIT_FISH_SHOULD_TWOHANDED), "a pike wants two hands");
            // the toxic fish carries its toxin to whoever eats it
            var puffer = Dm.@new<obj_item_fish_pufferfish>(locate(8, 6, 1));
            Assert.True(Dm.truthy(((datum_reagents)puffer.reagents).has_reagent(typeof(datum_reagent_toxin_tetrodotoxin))), "the pufferfish holds its toxin");
        });
        // revival: a dead fish with the revival trait comes back
        var crab = r.In(() => Dm.@new<obj_item_fish_mastodon>(locate(9, 6, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Contains(typeof(datum_fish_trait_revival), Dm.each<Type>(crab.fish_traits));
            Waiting.NoSleep(crab.set_status(FISH_DEAD));
        });
        bool revived = false;
        for (int i = 0; i < 200 && !revived; i++)
        {
            r.Step(Second);
            revived = r.In(() => crab.status == FISH_ALIVE);
        }
        Assert.True(revived, "the mastodon came back to life");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cook_cuts_a_fish_into_fillets()
    {
        var (r, feed, bob) = Room(14609);
        var table = r.In(() => Dm.@new<obj_structure_table>(locate(5, 4, 1)));
        var fish = r.In(() => Dm.@new<obj_item_fish_goldfish>(locate(5, 4, 1)));
        r.Step(2);
        int expected = r.In(() => (int)max(round(fish.num_fillets * Dm.num(fish.size) / FISH_FILLET_NUMBER_SIZE_DIVISOR, 1), 1));
        Hold<obj_item_knife_kitchen>(r, bob);
        Click(r, bob, fish);
        r.Step((int)(0.5f * expected * Second) + 2 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(fish), "the fish was cut up");
            Assert.Equal(expected, Dm.each<obj_item_food_fishmeat>(locate(5, 4, 1).contents).Count());
            Assert.Contains(Dm.each<obj_item_food_fishmeat>(locate(5, 4, 1).contents), f => f.name.Contains("goldfish", StringComparison.Ordinal));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_raw_fish_is_eaten_bite_by_bite()
    {
        var (r, _, bob) = Room(14610);
        var fish = Hold<obj_item_fish_goldfish>(r, bob);
        r.In(() =>
        {
            var edible = (datum_component_edible)fish.GetComponent(typeof(datum_component_edible));
            Assert.NotNull(edible);
            Assert.True(Dm.truthy(Dm.bitand(edible.foodtypes, RAW)), "a raw fish is raw food");
            edible.eat_time = 0;
        });
        Click(r, bob, bob);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bob.has_reagent(typeof(datum_reagent_consumable_nutriment_protein))), "eating the fish gave protein");
            Assert.True(QDELETED(fish) || fish.status == FISH_DEAD || fish.bites_amount > 0, "the fish was bitten");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fish_have_their_export_prices()
    {
        var (r, _, _) = Room(14611);
        r.In(() =>
        {
            var goldfish = Dm.@new<obj_item_fish_goldfish>(null);
            var sacabambaspis = Dm.@new<obj_item_fish_sacabambaspis>(null);
            var tadpole = Dm.@new<obj_item_fish_tadpole>(null);
            var export = Dm.@new<datum_export_fish>();
            float gold = Dm.num(export.get_base_cost(goldfish));
            Assert.True(gold > 0, "a goldfish sells");
            Assert.Equal(Dm.num(goldfish.get_export_price(30)), gold);
            Assert.Equal(2f, Dm.num(export.get_base_cost(tadpole)));
            Assert.True(Dm.num(sacabambaspis.get_export_price(30)) > 0);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_fish_is_mounted_on_the_wall_and_taken_down()
    {
        var (r, feed, bob) = Room(14612);
        var mount = r.In(() => Dm.@new<obj_structure_fish_mount>(locate(5, 4, 1)));
        r.Step(2);
        var fish = Hold<obj_item_fish_goldfish>(r, bob);
        Click(r, bob, mount);
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Same(fish, mount.mounted_fish);
            Assert.Same(mount, fish.loc);
            Assert.True(Dm.@in(fish, mount.vis_contents), "the fish is shown on the mount");
            Assert.True(HAS_TRAIT(mount, TRAIT_STOP_FISH_FLOPPING));
            Assert.Equal(bob.name, Dm.str(fish.catcher_name));
            Assert.True(Dm.truthy(fish.catch_date));
            Assert.True(Dm.truthy(fish.anchored));
        });
        Assert.True(feed.Any("fish mounted"));
        // a right click takes it down into the hand
        r.In(() => Waiting.Detach(mount.attack_hand_secondary(bob, Dm.list())));
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Null(mount.mounted_fish);
            Assert.Same(bob, fish.loc);
            Assert.False(Dm.truthy(fish.anchored));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_bar_mount_starts_with_a_dead_trophy()
    {
        var (r, _, _) = Room(14613);
        r.World.Rng.Reseed(14613, 1);
        var mount = r.In(() => Dm.@new<obj_structure_fish_mount_bar>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(mount.mounted_fish);
            Assert.Equal(FISH_DEAD, mount.mounted_fish.status);
            Assert.True(Dm.truthy(mount.persistence_loaded_fish));
            Assert.True(HAS_TRAIT(mount.mounted_fish, TRAIT_FISH_LOW_PRICE));
            Assert.False(mount.mounted_fish is obj_item_fish_holo || mount.mounted_fish is obj_item_fish_starfish_chrystarfish);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fish_encyclopedia_lists_the_fish()
    {
        var (r, _, bob) = Room(14614);
        r.In(() =>
        {
            Assert.NotNull(SSfishing);
            Assert.True(Dm.truthy(SSfishing.initialized), "the Master started the fishing subsystem");
            Assert.Equal(typeof(obj_item_fish_goldfish), SSfishing.catchable_fish["goldfish"]);
        });
        r.Step(2);
        var book = Hold<obj_item_book_manual_fish_catalog>(r, bob);
        Take(bob);
        r.In(() => Waiting.NoSleep(book.ui_interact(bob)));
        r.Step(4);
        var payload = LastFullUpdate(Take(bob), out _);
        Assert.Equal("FishCatalog", InterfaceOf(payload));
        var data = payload.GetProperty("static_data");
        Assert.Equal("Aquatech Ltd.", data.GetProperty("sponsored_by").GetString());
        var fish = data.GetProperty("fish_info").EnumerateArray().ToList();
        Assert.True(fish.Count > 50, "the encyclopedia lists the fish");
        var gold = fish.First(f => f.GetProperty("name").GetString() == "goldfish");
        Assert.Equal("Freshwater", gold.GetProperty("fluid").GetString());
        Assert.Equal("iconsobjaquariumfishdmigoldfish", gold.GetProperty("icon").GetString());
        Assert.Contains("★", gold.GetProperty("beauty").GetString());
        // the fish sources name where it bites: the fish-porter's aquarium dimension among them
        Assert.Contains("Aquarium dimension (Fishing portal generator)", gold.GetProperty("fishing_tips").GetProperty("spots").GetString());
        Assert.True(gold.GetProperty("fishing_tips").GetProperty("traits").GetArrayLength() > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_fish_lives_dies_and_is_deleted()
    {
        var failures = new List<string>();
        AudioRoom r = null;
        mob_living_carbon_human bob = null;
        int i = 0;
        foreach (Type type in FishTypes())
        {
            if (i++ % 20 == 0)
            {
                (r, _, bob) = Room(14615, 15, 9);
                r.World.Rng.Reseed(14615, i);
            }
            try
            {
                var fish = r.In(() => Dm.@new(type, locate(7, 4, 1)) as obj_item_fish);
                r.Step(Second);
                r.In(() =>
                {
                    if (!QDELETED(fish))
                    {
                        Waiting.NoSleep(fish.randomize_size_and_weight());
                        Assert.True(Dm.num(fish.size) > 0 && Dm.num(fish.weight) > 0, "a size and a weight");
                        Waiting.NoSleep(fish.examine(bob));
                    }
                });
                r.Step(Second);
                r.In(() =>
                {
                    if (!QDELETED(fish))
                        Waiting.NoSleep(fish.set_status(FISH_DEAD));
                });
                r.Step(Second);
                r.In(() => qdel(fish));
                r.Step(2);
            }
            catch (Exception e)
            {
                failures.Add(type.Name + ": " + e.Message);
            }
            if (r.Room.Runtimes.Count > 0)
            {
                failures.Add(type.Name + ": " + string.Join(" | ", r.Room.RuntimeText));
                r.Room.Runtimes.Clear();
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static List<Type> FishTypes()
    {
        var r = new AudioRoom(5, 5, seed: 14616);
        return r.In(() => Dm.each<Type>(subtypesof(typeof(obj_item_fish))).ToList());
    }
}
