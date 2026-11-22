using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H2 (D333): the kitchen machines and the cooking they do, in a booted toy room with Bob as a player: the microwave warms
/// a donk pocket, the oven bakes dough into bread on its tray, the griddle grills a slab into a steak, the processor makes meatballs,
/// a knife slices bread on a table and a rolling pin flattens dough, flour and water make dough, the deep fryer fries, the grill smokes
/// a slab, the gibber turns a monkey into its meat (the mob meat material), a rod puts butter on a stick, a can opens, the donut box
/// fills with live donuts, a quality fillet carries its quality and the range's burners cook meatball soup in its pot.
/// </summary>
public class KitchenH2Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static void Empty(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        obj_item held = who.get_active_held_item() as obj_item;
        if (held != null)
            qdel(held);
    });

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second); // tg's click cooldown (changeNext_move) of the click before
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    /// <summary>An atom as the map loader makes it: Initialize with mapload (as BrigTimerTests' Mapped).</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        T made = (T)Dm.@new(typeof(T), locate(x, y, 1));
        SSatoms.initialized = previous;
        // the map loader's late loaders (tg's InitializeAtoms runs them after the map's Initialize): the machines' power_change
        DmList late = SSatoms.late_loaders;
        SSatoms.late_loaders = Dm.list();
        foreach (atom lateLoader in Dm.each_cast<atom>(late))
            lateLoader.LateInitialize();
        return made;
    });

    private static List<T> At<T>(AudioRoom r, atom where) where T : class => r.In(() => Dm.each<T>(where).ToList());

    private static List<T> At<T>(AudioRoom r, int x, int y) where T : class => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    private static string Chat(Feed feed) => string.Join(" | ", feed.Chat.Select(c => c.Html));

    [Fact]
    public void TheMicrowaveWarmsADonkPocket()
    {
        var (r, feed, bob) = Room(7201);
        var microwave = Place<obj_machinery_microwave>(r, 5, 4);
        var pocket = Hold<obj_item_food_donkpocket>(r, bob);

        Use(r, bob, microwave);
        Assert.Same(microwave, r.In(() => pocket.loc));
        Assert.True(feed.Any("You add"), Chat(feed));

        RightClick(r, bob, microwave); // start the cook cycle
        r.In(() => Assert.True(Dm.truthy(microwave.operating), "the microwave did not start: " + Chat(feed)));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("microwave") == true); // tg's microwave hum (the looping sound)
        r.Step(12 * Second); // ten cycles of a second
        r.In(() => Assert.False(Dm.truthy(microwave.operating)));
        var warm = At<obj_item_food_donkpocket_warm>(r, 5, 4);
        Assert.Single(warm);
        Assert.True(r.In(() => QDELETED(pocket)));
        Assert.True(r.In(() => Dm.truthy(((datum_reagents)warm[0].reagents).has_reagent(typeof(datum_reagent_medicine_omnizine)))), "the warm pocket got no omnizine");
        r.In(() => Assert.Equal(1f, microwave.dirty));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOvenBakesDoughIntoBreadOnItsTray()
    {
        var (r, feed, bob) = Room(7202);
        var oven = Mapped<obj_machinery_oven>(r, 5, 4);
        var tray = r.In(() => oven.used_tray);
        Assert.NotNull(tray);

        Use(r, bob, oven); // open it
        r.In(() => Assert.True(Dm.truthy(oven.open)));
        var dough = Hold<obj_item_food_dough>(r, bob);
        Use(r, bob, tray);
        Assert.Same(tray, r.In(() => dough.loc));
        Use(r, bob, oven); // close it: the tray bakes
        r.In(() => Assert.False(Dm.truthy(oven.open)));
        r.In(() => Assert.Equal("oven_on", oven.icon_state));
        r.Step(50 * Second); // tg's 30 to 45 seconds
        var bread = At<obj_item_food_bread_plain>(r, tray);
        Assert.Single(bread);
        Assert.True(r.In(() => QDELETED(dough)));
        Assert.True(feed.Any("You smell something great"), Chat(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGriddleGrillsASlabIntoASteak()
    {
        var (r, feed, bob) = Room(7203);
        var griddle = Place<obj_machinery_griddle>(r, 5, 4);
        var slab = Hold<obj_item_food_meat_slab>(r, bob);
        Use(r, bob, griddle);
        Assert.Same(griddle, r.In(() => slab.loc));
        Use(r, bob, griddle); // turn it on
        r.In(() => Assert.True(Dm.truthy(griddle.on)));
        r.In(() => Assert.EndsWith("_on", griddle.icon_state));
        r.Step(95 * Second); // tg's 30 to 90 seconds
        var steaks = At<obj_item_food_meat_steak_plain>(r, griddle);
        Assert.Single(steaks);
        Assert.True(r.In(() => Dm.@in(steaks[0], griddle.griddled_objects)), "the steak is not on the griddle");
        Assert.True(feed.Any("turns into"), Chat(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheProcessorMakesMeatballsFromASlab()
    {
        var (r, feed, bob) = Room(7204);
        var processor = Place<obj_machinery_processor>(r, 5, 4);
        var slab = Hold<obj_item_food_meat_slab>(r, bob);
        Use(r, bob, processor);
        Assert.Same(processor, r.In(() => slab.loc));
        Use(r, bob, processor); // turn it on
        Assert.True(feed.Any("You turn on"), Chat(feed));
        r.Step(5 * Second);
        var meatballs = At<obj_item_food_raw_meatball>(r, 5, 4);
        Assert.Equal(3, meatballs.Count); // MEATSLAB_PROCESSED_AMOUNT
        Assert.True(feed.Any("finishes processing"), Chat(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AKnifeSlicesBreadOnATableAndARollingPinFlattensDough()
    {
        var (r, feed, bob) = Room(7205);
        Place<obj_structure_table>(r, 5, 4);
        var bread = r.In(() => Dm.@new<obj_item_food_bread_plain>(locate(5, 4, 1)));
        Hold<obj_item_knife_kitchen>(r, bob);
        Use(r, bob, bread);
        Assert.True(feed.Any("You start working on"), Chat(feed));
        r.Step(5 * Second);
        Assert.Equal(5, At<obj_item_food_breadslice_plain>(r, 5, 4).Count);
        Assert.True(r.In(() => QDELETED(bread)));

        Empty(r, bob);
        var dough = r.In(() => Dm.@new<obj_item_food_dough>(locate(5, 4, 1)));
        Hold<obj_item_kitchen_rollingpin>(r, bob);
        Use(r, bob, dough);
        r.Step(5 * Second);
        Assert.Single(At<obj_item_food_flatdough>(r, 5, 4));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FlourAndWaterMakeDough()
    {
        var (r, _, _) = Room(7206);
        var beaker = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(5, 4, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_consumable_flour), 15));
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 10));
        });
        r.Step(Second);
        Assert.Single(At<obj_item_food_dough>(r, 5, 4));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDeepFryerFriesWhatItIsGiven()
    {
        var (r, feed, bob) = Room(7207);
        var fryer = Place<obj_machinery_deepfryer>(r, 5, 4);
        var slab = Hold<obj_item_food_meat_slab>(r, bob);
        Use(r, bob, fryer);
        Assert.Same(fryer, r.In(() => slab.loc));
        r.In(() => Assert.Equal("fryer_on", fryer.icon_state));
        r.Step(55 * Second); // FRYING_TIME_PERFECT is 50 seconds
        Assert.True(feed.Any("dings"), Chat(feed));
        Use(r, bob, fryer); // take it out: tg's eject nulls the fryer's slot on the way out, so the fried thing lands on the floor
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => slab.loc as atom));
        r.In(() => Assert.StartsWith("deep-fried", slab.name));
        r.In(() => Assert.True(HAS_TRAIT(slab, TRAIT_FOOD_FRIED)));
        r.In(() => Assert.Equal("fryer_off", fryer.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGrillSmokesASlabOverWood()
    {
        var (r, feed, bob) = Room(7208);
        var grill = Place<obj_machinery_grill>(r, 5, 4);
        var wood = Hold<obj_item_stack_sheet_mineral_wood>(r, bob);
        Use(r, bob, grill);
        r.In(() => Assert.True(grill.grill_fuel > 0, "no fuel: " + Chat(feed)));
        r.In(() => Assert.Equal("grill_on", grill.icon_state));
        r.In(() => grill.grill_fuel = 2000); // a plank burns for a few seconds; enough fuel for the slab's whole grilling time
        var slab = Hold<obj_item_food_meat_slab>(r, bob);
        Use(r, bob, grill);
        Assert.Same(grill, r.In(() => slab.loc));
        r.In(() => Assert.Equal("grill", grill.icon_state));
        r.Step(95 * Second); // until the slab's own grilling time (30 to 90 seconds)
        r.In(() => Assert.True(HAS_TRAIT(slab, TRAIT_FOOD_BBQ_GRILLED), "not grilled: " + slab.name));
        r.In(() => Assert.NotNull(slab.GetComponent(typeof(datum_component_sizzle)))); // the grill marks
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGibberTurnsAMonkeyIntoItsMeat()
    {
        var (r, feed, bob) = Room(7209);
        var gibber = Place<obj_machinery_gibber>(r, 5, 4);
        var monkey = r.In(() => Dm.@new<mob_living_carbon_human_species_monkey>(locate(6, 4, 1)));
        r.Step(Second);
        r.In(() =>
        {
            Waiting.NoSleep(monkey.death()); // a dead monkey: a live one's AI climbs back out of the gibber (relaymove)
            Waiting.NoSleep(monkey.forceMove(gibber));
            gibber.set_occupant(monkey);
        });
        Use(r, bob, gibber);
        r.In(() => Assert.True(Dm.truthy(gibber.operating), "the gibber did not start: " + Chat(feed)));
        Assert.True(feed.Any("squelchy grinding"), Chat(feed));
        r.Step(10 * Second);
        var meats = r.In(() =>
        {
            var found = new List<obj_item_food_meat>();
            for (int x = 1; x <= 13; x++)
                for (int y = 1; y <= 9; y++)
                    found.AddRange(Dm.each<obj_item_food_meat>(locate(x, y, 1)).Where(m => !QDELETED(m)));
            return found;
        });
        Assert.NotEmpty(meats);
        r.In(() =>
        {
            foreach (obj_item_food_meat meat in meats)
            {
                Assert.Contains("'s", meat.name); // "<the monkey's name>'s meat"
                bool mobMeat = Dm.each(meat.custom_materials).Any(m => m is datum_material_meat_mob_meat);
                Assert.True(mobMeat, "no mob meat material on " + meat.name);
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMeatMaterialMakesThingsEdibleAndBloody()
    {
        var (r, _, _) = Room(7210);
        var chair = r.In(() => Dm.@new<obj_structure_chair_greyscale>(locate(6, 4, 1))); // the material chair takes its material's effects
        r.In(() => Waiting.NoSleep(chair.set_custom_materials(Dm.list(Dm.kv(Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_meat))), SHEET_MATERIAL_AMOUNT)))));
        r.In(() => Assert.True(IS_EDIBLE(chair)));
        r.In(() => Assert.NotNull(chair.GetComponent(typeof(datum_component_bloody_spreader))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARodPutsButterOnAStickAndACanOpens()
    {
        var (r, feed, bob) = Room(7211);
        var butter = r.In(() => Dm.@new<obj_item_food_butter>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Assert.False(InertSeam.IsInert(butter)));
        var rods = Hold<obj_item_stack_rods>(r, bob);
        r.In(() => rods.amount = 3);
        Use(r, bob, butter);
        Assert.True(feed.Any("You stick the rod into the stick of butter."), Chat(feed));
        r.In(() => Assert.True(QDELETED(butter)));
        r.In(() => Assert.Equal(2f, rods.amount));
        Assert.Single(At<obj_item_food_butter_on_a_stick>(r, 5, 4).Concat(At<obj_item_food_butter_on_a_stick>(r, bob)).ToList());

        Empty(r, bob);
        var can = Hold<obj_item_food_canned_beans>(r, bob);
        r.In(() => Assert.False(Dm.truthy(can.is_drainable())));
        r.In(() => Waiting.NoSleep(can.attack_self(bob)));
        Assert.True(feed.Any("You pull back the tab"), Chat(feed));
        r.In(() => Assert.True(Dm.truthy(can.is_drainable())));
        r.In(() => Assert.Equal("beans_open", can.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDonutBoxHoldsSixDonutsAndQualityFishCooksIntoQualityFood()
    {
        var (r, _, _) = Room(7212);
        var box = r.In(() => Dm.@new<obj_item_storage_fancy_donut_box>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(box));
            var donuts = Dm.each<obj_item_food_donut>(box).ToList();
            Assert.Equal(6, donuts.Count);
            Assert.All(donuts, d => Assert.False(InertSeam.IsInert(d)));
            Assert.Equal("donutbox_inner", box.icon_state);
            Assert.Equal(1f, Dm.num(((datum_storage)box.atom_storage).can_hold?[typeof(obj_item_food_donut_plain)]));
        });

        var salmon = r.In(() => Dm.@new<obj_item_food_fishmeat_salmon>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(salmon._listen_lookup?[COMSIG_ITEM_BAKED]), "the quality ingredient element is not on the salmon fillet"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRangeCooksMeatballSoupInItsPot()
    {
        var (r, feed, bob) = Room(7213);
        var range = Mapped<obj_machinery_oven_range>(r, 5, 4); // a mapped range comes with its soup pot on the burners
        r.Step(2);
        var stove = r.In(() => range.GetComponent(typeof(datum_component_stove)) as datum_component_stove);
        Assert.NotNull(stove);
        var pot = r.In(() => stove.container as obj_item_reagent_containers_cup_soup_pot);
        Assert.NotNull(pot);
        foreach (Type ingredient in new[] { typeof(obj_item_food_meatball), typeof(obj_item_food_grown_carrotlike_carrot), typeof(obj_item_food_grown_potato) })
        {
            var held = r.In(() => (obj_item)Dm.@new(ingredient, bob.loc));
            r.Step(2);
            r.In(() => Waiting.NoSleep(bob.put_in_hands(held)));
            r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(pot.item_interaction(bob, held))));
        }
        r.In(() => Waiting.NoSleep(((datum_reagents)pot.reagents).add_reagent(typeof(datum_reagent_water), 50)));
        r.In(() => stove.turn_on());
        r.Step(60 * Second);
        r.In(() => Assert.True(Dm.truthy(((datum_reagents)pot.reagents).has_reagent(typeof(datum_reagent_consumable_nutriment_soup_meatball_soup))),
            "no meatball soup: " + string.Join(", ", Dm.each<datum_reagent>(((datum_reagents)pot.reagents).reagent_list).Select(x => x.name))
            + $"; {((datum_reagents)pot.reagents).chem_temp} K, burners on {stove.on}, range stat {range.machine_stat}, ingredients {Dm.length(pot.added_ingredients)}; " + Chat(feed)));
        Assert.True(feed.Any("steaming pot of soup"), Chat(feed));
        r.AssertNoRuntimes();
    }
}
