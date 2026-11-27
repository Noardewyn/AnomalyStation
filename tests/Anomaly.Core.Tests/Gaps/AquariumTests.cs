using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
/// The aquariums: a fish put into an aquarium swims in it (its visual in the aquarium's vis_contents, the assisted mode setting the
/// fluid and temperature it needs), the Aquarium window's fluid, temperature and modes, feeding by hand and from the feed storage,
/// breeding, stasis, the fish tank's limits and weight, breaking and fixing the glass, the upgrade kits, the fish analyzer and its
/// window, admiring an aquarium, the athletic fishing gloves, the fishing boxes and a sweep over the newly live types.
/// </summary>
public class AquariumTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int w = 13, int h = 9)
    {
        var r = new AudioRoom(w, h, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => r.World.Rng.Reseed(seed, 1));
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    private static datum_component_aquarium Comp(AudioRoom r, atom aquarium)
        => r.In(() => aquarium.GetComponent(typeof(datum_component_aquarium)) as datum_component_aquarium);

    private static List<obj_item_fish> Fishes(AudioRoom r, atom aquarium)
        => r.In(() => Dm.each_cast<atom>(aquarium.contents).OfType<obj_item_fish>().ToList());

    /// <summary>Puts the fish in the aquarium the way a player does: the fish in hand, a click on the aquarium.</summary>
    private static T Insert<T>(AudioRoom r, mob_living_carbon_human bob, atom aquarium) where T : obj_item_fish
    {
        T fish = Hold<T>(r, bob);
        Click(r, bob, aquarium);
        r.In(() => Assert.True(fish.loc == aquarium, "the fish went into the aquarium"));
        r.Step(Second); // the click cooldown
        return fish;
    }

    private static List<string> OverlayStates(atom a) => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList();

    private static JsonElement OpenWindow(AudioRoom r, mob_living_carbon_human bob, atom aquarium, out string window)
    {
        Take(bob);
        Click(r, bob, aquarium, "alt=1"); // the control panel
        r.In(() => Assert.True(HAS_TRAIT(aquarium, TRAIT_AQUARIUM_PANEL_OPEN), "alt-click opens the panel"));
        r.Step(Second); // the click cooldown
        Click(r, bob, aquarium);
        r.Step(4);
        JsonElement payload = LastFullUpdate(Take(bob), out window);
        Assert.Equal("Aquarium", InterfaceOf(payload));
        return payload;
    }

    [Fact]
    public void A_fish_put_in_an_aquarium_swims_in_it()
    {
        var (r, feed, bob) = Room(14801);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        var comp = Comp(r, aquarium);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(aquarium));
            Assert.True(HAS_TRAIT(aquarium, TRAIT_IS_AQUARIUM));
            Assert.True(HAS_TRAIT(aquarium, TRAIT_FISHING_SPOT), "an aquarium is a fishing spot of its own fish");
            Assert.Equal("aquarium_base", aquarium.icon_state);
            Assert.Equal(AQUARIUM_MODE_AUTO, comp.current_mode);
        });
        var guppy = Insert<obj_item_fish_clownfish>(r, bob, aquarium);
        Assert.True(feed.Any("added to aquarium"));
        r.In(() =>
        {
            Assert.Contains(guppy, Dm.each_cast<atom>(comp.tracked_fish_by_type[typeof(obj_item_fish_clownfish)]));
            // the assisted mode picks the clownfish's salt water and the middle of its temperatures
            Assert.Equal(AQUARIUM_FLUID_SALTWATER, comp.fluid_type);
            Assert.True(ISINRANGE(comp.fluid_temp, guppy.required_temperature_min, guppy.required_temperature_max));
            Assert.True(Dm.truthy(Dm.bitand(guppy.fish_flags, FISH_FLAG_SAFE_FLUID)) && Dm.truthy(Dm.bitand(guppy.fish_flags, FISH_FLAG_SAFE_TEMPERATURE)));
            Assert.True(Dm.truthy(Waiting.NoSleep(guppy.proper_environment())), "the clownfish breathes in the aquarium");
            // its visual is in the aquarium's vis_contents, over the water and under the borders
            var visual = Dm.each_cast<atom>(aquarium.vis_contents).OfType<obj_effect_aquarium>().Single();
            Assert.Equal("clownfish_small", visual.icon_state);
            Assert.True(visual.layer > aquarium.layer + AQUARIUM_GLASS_LAYER && visual.layer < aquarium.layer + AQUARIUM_BORDERS_LAYER, "layer " + visual.layer);
            Assert.True(visual.pixel_w >= -16 && visual.pixel_w <= 16, "the fish swims inside the tank");
            // the glass, the water and the borders are drawn over the aquarium
            var states = OverlayStates(aquarium);
            Assert.Contains("aquarium_glass_water", states);
            Assert.Contains("aquarium_borders", states);
        });
        // taking it out takes its visual away
        r.In(() => Waiting.NoSleep(guppy.forceMove(bob.loc)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Empty(Dm.each_cast<atom>(aquarium.vis_contents).OfType<obj_effect_aquarium>());
            Assert.Null(comp.tracked_fish_by_type);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_window_sets_the_fluid_the_temperature_and_the_mode()
    {
        var (r, _, bob) = Room(14802);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        var goldfish = Insert<obj_item_fish_goldfish>(r, bob, aquarium);
        var comp = Comp(r, aquarium);
        JsonElement payload = OpenWindow(r, bob, aquarium, out string window);
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(new[] { "Manual", "Assisted", "Stasis" }, payload.GetProperty("static_data").GetProperty("aquariumModes").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(1, data.GetProperty("lockedFluidTemp").GetInt32());
        Assert.Equal("GOLDFISH", data.GetProperty("fishData")[0].GetProperty("fish_name").GetString());

        Act(r, bob, window, "change_mode", ("new_mode", AQUARIUM_MODE_MANUAL));
        Act(r, bob, window, "fluid", ("fluid", AQUARIUM_FLUID_AIR));
        r.In(() =>
        {
            Assert.Equal(AQUARIUM_FLUID_AIR, comp.fluid_type);
            Assert.False(Dm.truthy(Dm.bitand(goldfish.fish_flags, FISH_FLAG_SAFE_FLUID)), "a goldfish drowns in air");
            Assert.False(Dm.truthy(Waiting.NoSleep(goldfish.proper_environment())));
            Assert.Contains("aquarium_glass_air", OverlayStates(aquarium));
            // the fish's visual sinks to the bottom
            var visual = Dm.each_cast<atom>(aquarium.vis_contents).OfType<obj_effect_aquarium>().Single();
            Assert.Equal(AQUARIUM_FLUID_AIR, visual.fluid_type);
        });
        Act(r, bob, window, "temperature", ("temperature", 500f));
        r.In(() => Assert.Equal(MAX_AQUARIUM_TEMP, comp.fluid_temp)); // clamped to the aquarium's range
        Act(r, bob, window, "feeding_interval", ("feeding_interval", 5f));
        r.In(() => Assert.Equal(5 * MINUTES, comp.feeding_interval));
        Act(r, bob, window, "change_mode", ("new_mode", AQUARIUM_MODE_SAFE));
        r.In(() => Assert.True(HAS_TRAIT_FROM(aquarium, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT), "stasis"));
        JsonElement after = Data(r, bob, out _);
        Assert.Equal("Stasis", after.GetProperty("currentMode").GetString());

        // the window takes the fish out
        string fishRef = after.GetProperty("fishData")[0].GetProperty("fish_ref").GetString();
        Act(r, bob, window, "remove_item", ("item_reference", fishRef));
        r.In(() => Assert.Same(aquarium.loc, goldfish.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Feeding_by_hand_and_from_the_feed_storage()
    {
        var (r, feed, bob) = Room(14803);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        var goldfish = Insert<obj_item_fish_goldfish>(r, bob, aquarium);
        r.In(() => goldfish.last_feeding = 0f);
        Hold<obj_item_reagent_containers_cup_fish_feed>(r, bob);
        Click(r, bob, aquarium);
        Assert.True(feed.Any("fed the fish"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        float fedAt = r.In(() => Dm.num(goldfish.last_feeding));
        Assert.True(fedAt > 0, "the goldfish ate");

        // the feed storage feeds the fish at its interval
        var comp = Comp(r, aquarium);
        r.In(() =>
        {
            Waiting.NoSleep(((datum_reagents)aquarium.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 2));
            goldfish.last_feeding = 0f;
            comp.last_feeding = world.time - comp.feeding_interval - 10; // the interval has passed
        });
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.True(Dm.num(goldfish.last_feeding) > 0, "the storage fed the goldfish");
            Assert.True(Dm.num(((datum_reagents)aquarium.reagents).total_volume) < 2, "the feed was used");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Two_guppies_breed_in_an_aquarium()
    {
        var (r, _, bob) = Room(14804);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        var first = Insert<obj_item_fish_guppy>(r, bob, aquarium);
        var second = Insert<obj_item_fish_guppy>(r, bob, aquarium);
        r.In(() =>
        {
            foreach (var fish in new[] { first, second })
            {
                fish.breeding_wait = 0f;
                fish.last_feeding = world.time;
            }
        });
        r.Step(6 * Second);
        var fishes = Fishes(r, aquarium);
        Assert.True(fishes.Count >= 3, "the guppies bred: " + fishes.Count);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fish_tank_limits_its_fish_and_grows_heavy()
    {
        var (r, feed, bob) = Room(14805);
        var tank = Place<obj_item_fish_tank>(r, 5, 4);
        r.In(() => Assert.Equal("fish_tank", tank.icon_state));
        // a fish too big for it
        var pike = Hold<obj_item_fish_pike>(r, bob);
        r.In(() => pike.size = 150f);
        Click(r, bob, tank);
        Assert.True(feed.Any("fish is too big!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.In(() => Assert.NotSame(tank, pike.loc));
        r.In(() => qdel(pike));
        // heavy fish slow its carrier
        var goldfish = Insert<obj_item_fish_goldfish>(r, bob, tank);
        r.In(() =>
        {
            Assert.Equal(Dm.num(goldfish.size), tank.current_summed_size);
            Waiting.NoSleep(tank.change_size_weight(0f, 6000f));
            Assert.True(tank.slowdown > 0, "a heavy tank slows");
            Assert.True(tank.force > 5, "and hits harder");
            Waiting.NoSleep(tank.change_size_weight(500f, 0f));
            Assert.True(HAS_TRAIT_FROM(tank, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, INNATE_TRAIT), "a crowded tank stops growth");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_broken_aquarium_spills_its_fish_and_two_glass_sheets_fix_it()
    {
        var (r, feed, bob) = Room(14806);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        var guppy = Insert<obj_item_fish_guppy>(r, bob, aquarium);
        r.In(() => Waiting.NoSleep(aquarium.atom_break()));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(aquarium.broken));
            Assert.IsAssignableFrom<turf>(guppy.loc);
            Assert.Contains("aquarium_glass_cracks", OverlayStates(aquarium));
        });
        var glass = Hold<obj_item_stack_sheet_glass>(r, bob);
        r.In(() => glass.amount = 5f);
        Click(r, bob, aquarium);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(aquarium.broken), string.Join(" | ", feed.Chat.Select(c => c.Html)));
            Assert.Equal(3f, glass.amount);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_bioelectricity_kit_turns_an_aquarium_into_a_generator_with_its_fish()
    {
        var (r, _, bob) = Room(14807);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        var zipzap = Insert<obj_item_fish_zipzap>(r, bob, aquarium);
        r.In(() => Waiting.NoSleep(((datum_reagents)aquarium.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 2)));
        Hold<obj_item_aquarium_upgrade_bioelec_gen>(r, bob);
        Click(r, bob, aquarium);
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(aquarium), "the old aquarium is gone");
            var generator = Dm.each<obj_structure_aquarium_bioelec_gen>().Single();
            Assert.Same(generator, zipzap.loc);
            Assert.True(HAS_TRAIT(generator, TRAIT_BIOELECTRIC_GENERATOR));
            Assert.True(Dm.num(((datum_reagents)generator.reagents).total_volume) > 0, "the feed storage came along");
            Assert.True(HAS_TRAIT(generator, TRAIT_IS_AQUARIUM));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fish_analyzer_reads_a_fish_and_every_fish_of_an_aquarium()
    {
        var (r, _, bob) = Room(14808);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        Insert<obj_item_fish_goldfish>(r, bob, aquarium);
        Insert<obj_item_fish_guppy>(r, bob, aquarium);
        var analyzer = Hold<obj_item_fish_analyzer>(r, bob);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(analyzer));
            Assert.Equal("fish_analyzer", analyzer.icon_state);
            Assert.NotNull(analyzer.case_color);
        });
        Take(bob);
        Click(r, bob, aquarium);
        JsonElement payload = LastFullUpdate(Take(bob), out _);
        Assert.Equal("FishAnalyzer", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(0, data.GetProperty("fish_scanned").GetInt32());
        var names = data.GetProperty("fish_list").EnumerateArray().Select(f => f.GetProperty("fish_name").GetString()).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "goldfish", "guppy" }, names);
        JsonElement goldfish = data.GetProperty("fish_list").EnumerateArray().First(f => f.GetProperty("fish_name").GetString() == "goldfish");
        Assert.Equal("Nutriment", goldfish.GetProperty("fish_food").GetString());
        Assert.Equal(100f, goldfish.GetProperty("fish_health").GetSingle());

        // a fish on the floor alone
        var loose = r.In(() => Dm.@new<obj_item_fish_guppy>(locate(5, 4, 1)));
        r.Step(2);
        Click(r, bob, loose);
        r.Step(4);
        JsonElement one = LastUpdate(Take(bob), out _).GetProperty("data");
        Assert.Equal(1, one.GetProperty("fish_scanned").GetInt32());
        Assert.Single(one.GetProperty("fish_list").EnumerateArray());
        r.In(() => Assert.Same(loose, analyzer.scanned_object));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Admiring_an_aquarium_with_live_fish_lifts_the_mood()
    {
        var (r, feed, bob) = Room(14809);
        var aquarium = Place<obj_structure_aquarium>(r, 5, 4);
        Insert<obj_item_fish_goldfish>(r, bob, aquarium);
        Click(r, bob, aquarium);
        Assert.True(feed.Any("admiring aquarium..."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.Step(6 * Second);
        r.In(() => Assert.IsType<datum_mood_event_aquarium_positive>(bob.mob_mood?.mood_events["aquarium"]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_athletic_gloves_fish_bare_handed_and_work_the_wearer_out()
    {
        const float BITING_PHASE = 2, MINIGAME_PHASE = 3;
        var (r, _, bob) = Room(14810);
        var gloves = r.In(() => Dm.@new<obj_item_clothing_gloves_fishing>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(gloves, ITEM_SLOT_GLOVES)))));
        var toilet = Place<obj_structure_toilet>(r, 5, 4);
        r.In(() => Waiting.NoSleep(toilet.AddComponent(typeof(datum_component_fishing_spot), GLOB.preset_fish_sources[typeof(datum_fish_source_toilet)])));
        Click(r, bob, toilet);
        datum_fishing_challenge challenge = null;
        for (int i = 0; i < 4 * Second && challenge == null; i++)
        {
            r.Step();
            challenge = r.In(() => GLOB.fishing_challenges_by_user[bob] as datum_fishing_challenge);
        }
        Assert.True(challenge != null, "the gloves cast");
        r.In(() => Assert.IsType<obj_item_fishing_rod_mob_fisher_athletic>(challenge.used_rod));
        bool biting = false;
        for (int i = 0; i < 40 * Second && !biting; i++)
        {
            r.Step();
            biting = r.In(() => challenge.phase == BITING_PHASE);
        }
        Assert.True(biting, "nothing bit");
        Click(r, bob, toilet);
        r.Step(2);
        r.In(() => Assert.Equal(MINIGAME_PHASE, challenge.phase));
        r.In(() => Assert.Same(challenge, gloves.challenge));
        float before = r.In(() => Dm.num(bob.get_stamina_loss()));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.num(bob.get_stamina_loss()) > before, "the workout tires");
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_exercised)));
            qdel(challenge);
        });
        r.Step(2);
        r.In(() => Assert.Null(gloves.challenge));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_fishing_boxes_and_the_super_toolbox_hold_their_gear()
    {
        var (r, _, _) = Room(14811);
        var toolbox = Place<obj_item_storage_toolbox_fishing_master>(r, 6, 4);
        var lures = Place<obj_item_storage_box_fishing_lures>(r, 7, 4);
        var props = Place<obj_item_storage_box_aquarium_props>(r, 8, 4);
        var kit = Place<obj_item_storage_box_fish_revival_kit>(r, 9, 4);
        r.In(() =>
        {
            var inside = Dm.each_cast<atom>(toolbox.contents).ToList();
            Assert.Contains(inside, a => a is obj_item_fishing_rod_telescopic_master);
            Assert.Contains(inside, a => a is obj_item_fish_analyzer);
            Assert.Contains(inside, a => a is obj_item_aquarium_kit);
            var hooks = inside.OfType<obj_item_storage_box_fishing_hooks_master>().Single();
            Assert.Equal(5, Dm.each_cast<atom>(hooks.contents).Count());
            Assert.Equal(4, Dm.each_cast<atom>(inside.OfType<obj_item_storage_box_fishing_lines_master>().Single().contents).Count());
            Assert.Equal(Dm.each_cast<Type>(subtypesof(typeof(obj_item_fishing_lure))).Count() + 1, Dm.each_cast<atom>(lures.contents).Count());
            Assert.Equal(Dm.each_cast<Type>(subtypesof(typeof(obj_item_aquarium_prop))).Count(), Dm.each_cast<atom>(props.contents).Count());
            Assert.Contains(Dm.each_cast<atom>(kit.contents), a => a is obj_item_lazarus_injector);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lawyers_tank_and_the_prefilled_aquarium_keep_their_fish_in_stasis()
    {
        var (r, _, _) = Room(14812);
        var tank = Place<obj_item_fish_tank_lawyer>(r, 6, 4);
        var prefilled = Place<obj_structure_aquarium_prefilled>(r, 8, 4);
        r.Step(10 * Second);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT_FROM(tank, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT), "the lawyer's tank is in stasis");
            var goldfish = Dm.each_cast<atom>(tank.contents).OfType<obj_item_fish_goldfish>().Single();
            Assert.Equal(FISH_ALIVE, goldfish.status);
            Assert.Equal(2, Dm.each_cast<atom>(tank.contents).OfType<obj_item_aquarium_prop>().Count());
            Assert.Equal(3, Dm.each_cast<atom>(prefilled.contents).OfType<obj_item_fish>().Count());
            Assert.Equal(5, Dm.each_cast<atom>(prefilled.vis_contents).OfType<obj_effect_aquarium>().Count()); // three fish and two props
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_newly_live_aquarium_type_is_made_and_deleted()
    {
        var (r, _, _) = Room(14813);
        Type[] roots =
        [
            typeof(obj_structure_aquarium), typeof(obj_item_fish_tank), typeof(obj_item_aquarium_prop), typeof(obj_item_aquarium_upgrade),
            typeof(obj_item_aquarium_kit), typeof(obj_item_fish_analyzer), typeof(obj_item_clothing_gloves_fishing),
            typeof(obj_item_fishing_rod_mob_fisher_athletic), typeof(obj_item_storage_box_fishing_hooks), typeof(obj_item_storage_box_fishing_lines),
            typeof(obj_item_storage_box_fish_revival_kit), typeof(obj_item_storage_box_fishing_lures), typeof(obj_item_storage_box_aquarium_props),
            typeof(obj_item_storage_toolbox_fishing_master), typeof(obj_item_clothing_head_helmet_perceptomatrix),
        ];
        var types = roots.SelectMany(t => r.In(() => Dm.each_cast<Type>(typesof(t)).ToList())).Distinct().ToList();
        foreach (Type type in types)
        {
            atom made = r.In(() => Dm.@new(type, locate(8, 4, 1)) as atom);
            r.Step(2);
            r.In(() => Assert.False(InertSeam.IsInert(made), type.Name + " is live"));
            r.In(() => qdel(made));
            r.Step(2);
        }
        r.AssertNoRuntimes();
    }
}
