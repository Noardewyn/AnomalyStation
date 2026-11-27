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
/// The food left: the South Bronx Paradise bar's second look, the mystery snack bags taking a flavour of their kind, the ghost burger
/// floating about on its own, the energy cake slice cutting its eater, the popsicle's bites, the envirochow buffing a pet, the squid
/// ink can inking whoever it hits, the death sandwich's disgust, the butterdog's slip, the fortune cookie's fortune, the macaron's
/// colour, the bait's traits and the recipe guides of the crafting window (the chemical reactions and the soups).
/// </summary>
public class FoodLeftTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
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

    [Fact]
    public void The_bronx_bar_shows_its_real_name_on_the_first_look()
    {
        var (r, feed, bob) = Room(14231);
        var bar = Hold<obj_item_food_candy_bronx>(r, bob);
        r.In(() => Waiting.NoSleep(bar.examine(bob)));
        r.In(() =>
        {
            Assert.Contains("South Bronx Parasite bar", bar.name, StringComparison.Ordinal);
            Assert.True(Dm.truthy(bar.revelation));
            Assert.NotNull(bar.GetComponent(typeof(datum_component_edible)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mystery_bag_takes_a_flavour_of_its_kind()
    {
        var (r, _, _) = Room(14232);
        (Type Random, Type Kind)[] bags =
        {
            (typeof(obj_item_food_peanuts_random), typeof(obj_item_food_peanuts)),
            (typeof(obj_item_food_cnds_random), typeof(obj_item_food_cnds)),
            (typeof(obj_item_food_cornchips_random), typeof(obj_item_food_cornchips)),
            (typeof(obj_item_food_sticko_random), typeof(obj_item_food_sticko)),
            (typeof(obj_item_food_shok_roks_random), typeof(obj_item_food_shok_roks)),
        };
        foreach (var (random, kind) in bags)
        {
            var bag = r.In(() => Dm.@new(random, locate(6, 4, 1)) as obj_item_food);
            r.Step(2);
            r.In(() =>
            {
                List<string> flavours = Dm.each<Type>(subtypesof(kind)).Where(t => t != random)
                    .Select(t => Dm.str(Dm.initial(t, "name"))).ToList();
                Assert.Contains(bag.name, flavours);
                qdel(bag);
            });
        }
        r.In(() => Assert.DoesNotContain(Dm.each<Type>(GLOB.safe_peanut_types), t => !Dm.truthy(Dm.initial(t, "safe_for_consumption"))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_ghost_burger_floats_and_wanders_on_its_own()
    {
        var (r, _, _) = Room(14233);
        r.World.Rng.Reseed(14233, 1);
        var burger = r.In(() => Dm.@new<obj_item_food_burger_ghost>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Assert.NotNull(burger.GetComponent(typeof(datum_component_ghost_edible))));
        var start = r.In(() => burger.loc);
        bool moved = false;
        for (int i = 0; i < 60 * Second && !moved; i++)
        {
            r.Step();
            moved = r.In(() => burger.loc != start);
        }
        Assert.True(moved, "the ghost burger moved on its own");
        r.In(() => qdel(burger));
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_energy_cake_slice_cuts_whoever_eats_it()
    {
        var (r, feed, bob) = Room(14234);
        var slice = Hold<obj_item_food_cakeslice_birthday_energy>(r, bob);
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        Click(r, bob, bob);
        r.Step(2 * Second);
        r.In(() => Assert.True(Dm.num(bob.get_brute_loss()) - before >= 18f, "the embedded dagger hurt the eater's head"));
        Assert.True(feed.Any("you accidentally hurt yourself on the embedded energy dagger!"));
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("blade1.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_popsicle_shows_its_bites()
    {
        var (r, _, bob) = Room(14235);
        var popsicle = Hold<obj_item_food_popsicle_creamsicle_orange>(r, bob);
        string overlay = r.In(() => Dm.str(Dm.initial(typeof(obj_item_food_popsicle_creamsicle_orange), "overlay_state")));
        r.In(() =>
        {
            Assert.Equal(((datum_reagents)popsicle.reagents).total_volume / popsicle.bite_states, Dm.num(popsicle.bite_consumption), 3);
            Assert.Contains(Dm.each<object>(popsicle.overlays), o => (o is IDmAppearance x ? x.icon_state : Dm.str(o)) == overlay);
        });
        Click(r, bob, bob);
        r.Step(2 * Second);
        r.In(() =>
        {
            int bites = (int)Dm.num(popsicle.bitecount);
            Assert.True(bites >= 1, "the popsicle was bitten");
            Assert.Contains(Dm.each<object>(popsicle.overlays), o => (o is IDmAppearance x ? x.icon_state : Dm.str(o)) == overlay + "_" + Math.Min(bites, 3));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Envirochow_buffs_a_pet_once_the_can_is_open()
    {
        var (r, feed, bob) = Room(14236);
        var dog = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(locate(5, 4, 1)));
        var can = Hold<obj_item_food_canned_envirochow>(r, bob);
        r.In(() => Assert.Equal(NONE, Waiting.NoSleep(can.interact_with_atom(dog, bob))));
        r.In(() => Waiting.NoSleep(can.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(can.is_drainable()), "the can is open");
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(can.interact_with_atom(dog, bob)));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(can));
            Assert.NotNull(dog.has_status_effect(typeof(datum_status_effect_limited_buff_health_buff)));
            Assert.True(dog.maxHealth > Dm.num(Dm.initial(typeof(mob_living_basic_pet_dog_corgi), "maxHealth")), "the buff raised the pet's health");
        });
        Assert.True(feed.Any("chows down on"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_open_squid_ink_can_inks_whoever_it_hits()
    {
        var (r, feed, bob) = Room(14237);
        var target = r.Dummy(7, 4);
        var can = Hold<obj_item_food_canned_squid_ink>(r, bob);
        r.In(() => Waiting.NoSleep(can.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(can.GetComponent(typeof(datum_component_splat)));
            Waiting.NoSleep(bob.throw_item(target));
        });
        r.Step(Second);
        Assert.True(feed.Any("is inked by"), "the can inked the target");
        r.In(() => Assert.NotEmpty(Dm.each<obj_effect_decal_cleanable_food_squid_ink>(range(1, target))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_death_sandwich_disgusts_whoever_looks_at_it_wrong()
    {
        var (r, feed, bob) = Room(14238);
        var sandwich = Hold<obj_item_food_sandwich_death>(r, bob);
        r.In(() =>
        {
            Assert.Equal(0, Dm.bitand(sandwich.obj_flags, UNIQUE_RENAME));
            Waiting.NoSleep(sandwich.examine(bob));
            Assert.True(Dm.num(bob.disgust) >= 33f);
            Assert.Equal(FOOD_ALLERGIC, sandwich.check_liked(bob));
        });
        Assert.True(feed.Any("a horrible feeling that you've done something wrong."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_butterdog_is_slippery_and_the_moldy_bread_can_be_swabbed()
    {
        var (r, _, _) = Room(14239);
        var dog = r.In(() => Dm.@new<obj_item_food_butterdog>(locate(6, 4, 1)));
        var bread = r.In(() => Dm.@new<obj_item_food_breadslice_moldy_bacteria>(locate(7, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(dog.GetComponent(typeof(datum_component_slippery)));
            DmList samples = Dm.list();
            bread.SEND_SIGNAL(COMSIG_SWAB_FOR_SAMPLES, Dm.list(bread, samples));
            Assert.NotEmpty(Dm.each<object>(samples));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Food_variants_take_their_looks()
    {
        var (r, _, _) = Room(14240);
        var macaron = r.In(() => Dm.@new<obj_item_food_cookie_macaron>(locate(6, 4, 1)));
        var cookie = r.In(() => Dm.@new<obj_item_food_fortunecookie>(locate(6, 4, 1)));
        var bait = r.In(() => Dm.@new<obj_item_food_bait_doughball_synthetic_unconsumable>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Matches("^macaron_[1-4]$", macaron.icon_state);
            Assert.NotNull(cookie.GetComponent(typeof(datum_component_edible)));
            Assert.True(HAS_TRAIT(bait, TRAIT_GREAT_QUALITY_BAIT) || HAS_TRAIT(bait, TRAIT_GOOD_QUALITY_BAIT) || HAS_TRAIT(bait, TRAIT_BASIC_QUALITY_BAIT));
            Assert.True(HAS_TRAIT(bait, TRAIT_BAIT_UNCONSUMABLE));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_recipe_guides_list_the_chemical_reactions_and_the_soups()
    {
        var (r, _, _) = Room(14241);
        r.In(() =>
        {
            var guides = Dm.each<datum_crafting_recipe>(GLOB.cooking_recipes).ToList();
            var soup = guides.OfType<datum_crafting_recipe_food_reaction_soup_meatball_soup>().Single();
            Assert.NotNull(soup.result);
            Assert.NotEmpty(Dm.each<object>(soup.reqs));
            var reactions = guides.OfType<datum_crafting_recipe_food_reaction>().Where(g => g is not datum_crafting_recipe_food_reaction_soup).ToList();
            Assert.NotEmpty(reactions);
            Assert.All(reactions, g => Assert.NotNull(g.result));
            Assert.Contains(reactions, g => Dm.each<object>(g.reqs).Any(q => q is Type t && t.IsSubclassOf(typeof(datum_reagent))));
            Assert.Contains(guides, g => g is datum_crafting_recipe_food_order && Dm.str(g.name).StartsWith("Store Bought ", StringComparison.Ordinal));
            Assert.Contains(guides, g => g is datum_crafting_recipe_food_biogenerator && Dm.str(g.name).StartsWith("Synthetic ", StringComparison.Ordinal));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_newly_live_food_and_restaurant_type_is_created_and_deleted()
    {
        var (r, _, _) = Room(14242);
        Type[] types =
        {
            typeof(obj_item_food_candy_bronx), typeof(obj_item_food_chips), typeof(obj_item_food_peanuts), typeof(obj_item_food_peanuts_random),
            typeof(obj_item_food_cnds), typeof(obj_item_food_cnds_random), typeof(obj_item_food_pistachios), typeof(obj_item_food_cornchips),
            typeof(obj_item_food_cornchips_random), typeof(obj_item_food_hot_shots), typeof(obj_item_food_sticko_random), typeof(obj_item_food_shok_roks_random),
            typeof(obj_item_food_burger_plain), typeof(obj_item_food_burger_human), typeof(obj_item_food_burger_ghost), typeof(obj_item_food_burger_superbite),
            typeof(obj_item_food_burger_cheese), typeof(obj_item_food_burger_crazy), typeof(obj_item_food_bowled_wish), typeof(obj_item_food_bait),
            typeof(obj_item_food_bait_natural), typeof(obj_item_food_bait_doughball_synthetic), typeof(obj_item_food_bait_doughball_synthetic_unconsumable),
            typeof(obj_item_food_appleslice), typeof(obj_item_food_barrelmelonmush), typeof(obj_item_food_barrelmelonslice), typeof(obj_item_food_bread_root),
            typeof(obj_item_food_breadslice_empty), typeof(obj_item_food_breadslice_moldy_bacteria), typeof(obj_item_food_breadslice_root),
            typeof(obj_item_food_butterdog), typeof(obj_item_food_cakeslice_birthday_energy), typeof(obj_item_food_cakeslice_fruit),
            typeof(obj_item_food_candied_pineapple), typeof(obj_item_food_canned_envirochow), typeof(obj_item_food_canned_squid_ink),
            typeof(obj_item_food_carrotfries), typeof(obj_item_food_cheesyfries), typeof(obj_item_food_cookie_macaron), typeof(obj_item_food_fortunecookie),
            typeof(obj_item_food_fries), typeof(obj_item_food_hard_taco_shell), typeof(obj_item_food_holymelonmush), typeof(obj_item_food_holymelonslice),
            typeof(obj_item_food_ink_sac), typeof(obj_item_food_moonfish_eggs), typeof(obj_item_food_muffin_booberry), typeof(obj_item_food_muffin_moffin),
            typeof(obj_item_food_okonomiyaki), typeof(obj_item_food_onigiri), typeof(obj_item_food_pickle), typeof(obj_item_food_pie_plump_pie),
            typeof(obj_item_food_pineappleslice), typeof(obj_item_food_plumphelmetbiscuit), typeof(obj_item_food_popsicle), typeof(obj_item_food_poutine),
            typeof(obj_item_food_rationpack), typeof(obj_item_food_salad_bibimbap), typeof(obj_item_food_salad_egg_fried_rice),
            typeof(obj_item_food_salad_simple_fried_noodles), typeof(obj_item_food_sandwich_death), typeof(obj_item_food_seaweedsheet),
            typeof(obj_item_food_snowcones), typeof(obj_item_food_spaghetti_carbonara), typeof(obj_item_food_spiderlollipop),
            typeof(obj_item_food_swirl_lollipop), typeof(obj_item_food_watermelonmush), typeof(obj_item_food_watermelonslice),
            typeof(obj_effect_decal_cleanable_food_squid_ink), typeof(obj_item_circuitboard_machine_restaurant_portal),
            // the bare portal and seat are left out: tg's own Destroy and Initialize read their venue, which only the subtypes name
            typeof(obj_machinery_restaurant_portal_restaurant), typeof(obj_machinery_restaurant_portal_bar),
            typeof(obj_structure_holosign_robot_seat_restaurant), typeof(obj_structure_holosign_robot_seat_bar),
            typeof(obj_item_holosign_creator_robot_seat), typeof(obj_item_holosign_creator_robot_seat_restaurant), typeof(obj_item_holosign_creator_robot_seat_bar),
        };
        foreach (Type type in types)
        {
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
            var thing = r.In(() => Dm.@new(type, locate(9, 4, 1)) as datum);
            r.Step(2);
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Type[] datums =
        {
            typeof(datum_controller_subsystem_restaurant), typeof(datum_venue), typeof(datum_venue_restaurant), typeof(datum_venue_bar),
            typeof(datum_custom_order), typeof(datum_custom_order_moth_clothing), typeof(datum_custom_order_icecream), typeof(datum_custom_order_reagent),
            typeof(datum_custom_order_reagent_drink), typeof(datum_custom_order_reagent_soup), typeof(datum_customer_data), typeof(mob_living_basic_robot_customer),
            typeof(datum_ai_controller_robot_customer), typeof(datum_element_venue_price), typeof(datum_component_ghost_edible),
            typeof(datum_status_effect_limited_buff), typeof(datum_status_effect_limited_buff_health_buff), typeof(datum_crafting_recipe_food_reaction),
            typeof(datum_crafting_recipe_food_reaction_soup), typeof(datum_crafting_recipe_food_order), typeof(datum_crafting_recipe_food_biogenerator),
        };
        foreach (Type type in datums)
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        foreach (Type customer in Dm.each<Type>(r.In(() => subtypesof(typeof(datum_customer_data)))))
            Assert.False(Attribute.IsDefined(customer, typeof(DmInertAttribute), inherit: false), customer.Name + " is inert");
        r.AssertNoRuntimes();
    }
}
