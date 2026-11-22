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
/// Gaps package SK1 (D333): the stacks' leftovers, in a booted toy room with Bob as a player: the tanning (a knife shaves a hide, heat
/// dries a wet hide into leather), the lizard hide's skin colour, the sheets' recipe lists (leather, diamond, cardboard, cloth, bronze),
/// the titanium's shuttle frame rods, the coal lit by a welder, the clown's cardboard box, the wall filling's welding and reskins, the
/// mineral tiles welded back into a sheet, the wired glass tile, the arcade tickets' piles, the bluespace polycrystal broken by hand
/// and the duct tape's repair.
/// </summary>
public class StacksSK1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, params object[] args) where T : obj_item
    {
        T item = r.In(() => Dm.@new(typeof(T), who.loc, args.Length > 0 ? args[0] : null) as T);
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static List<string> Titles(object recipes)
    {
        var titles = new List<string>();
        foreach (object entry in Dm.each(recipes as DmList))
        {
            if (entry is datum_stack_recipe recipe)
                titles.Add(recipe.title);
            else if (entry is datum_stack_recipe_list group)
                titles.Add(group.title);
        }
        return titles;
    }

    [Fact]
    public void AKnifeShavesAHideAndHeatDriesTheWetHideIntoLeather()
    {
        var (r, feed, bob) = Room(9201);
        var hide = r.In(() => Dm.@new<obj_item_stack_sheet_animalhide_corgi>(bob.loc, 3));
        var knife = Hold<obj_item_knife_kitchen>(r, bob);
        r.Step(2);
        r.In(() => Waiting.Detach(hide.item_interaction(bob, knife)));
        r.Step(6 * Second);
        Assert.True(feed.Any("You cut the hair from"));
        var bare = r.In(() => Dm.each(bob.loc.contents).OfType<obj_item_stack_sheet_hairlesshide>().FirstOrDefault());
        Assert.NotNull(bare);
        r.In(() =>
        {
            Assert.True(QDELETED(hide));
            Assert.Equal(3f, Dm.num(bare.amount));
        });

        var wet = r.In(() => Dm.@new<obj_item_stack_sheet_wethide>(locate(6, 4, 1), 2));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(wet.should_atmos_process(null, 300)));
            Assert.True(Dm.truthy(wet.should_atmos_process(null, 600)));
            for (int i = 0; i < 30; i++)
                Waiting.NoSleep(wet.atmos_expose(null, 600));
            var leather = Dm.each(locate(6, 4, 1).contents).OfType<obj_item_stack_sheet_leather>().FirstOrDefault();
            Assert.NotNull(leather);
            Assert.Equal(2f, Dm.num(leather.amount));
            Assert.Equal(30f, wet.wetness); // one hide of the two dried, the wetness starts over for the next
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALizardHideKeepsItsColourAndMergesOnlyWithItsOwn()
    {
        var (r, _, bob) = Room(9202);
        r.In(() =>
        {
            var green = Dm.@new<obj_item_stack_sheet_animalhide_carbon_lizard>(locate(6, 4, 1), 2, FALSE, null, 1, "#7fff7f");
            var red = Dm.@new<obj_item_stack_sheet_animalhide_carbon_lizard>(locate(6, 4, 1), 1, FALSE, null, 1, "#ff7f7f");
            var other_green = Dm.@new<obj_item_stack_sheet_animalhide_carbon_lizard>(locate(6, 4, 1), 1, FALSE, null, 1, "#7fff7f");
            Assert.Equal("#7fff7f", green.skin_color);
            Assert.False(Dm.truthy(green.can_merge(red)));
            Assert.True(Dm.truthy(green.can_merge(other_green)));
            var half = Waiting.NoSleep(green.split_stack(1)) as obj_item_stack_sheet_animalhide_carbon_lizard;
            Assert.NotNull(half);
            Assert.Equal("#7fff7f", half.skin_color);
            // a random lizard hide picks one of tg's eight colours
            var random = Dm.@new<obj_item_stack_sheet_animalhide_carbon_lizard>(locate(6, 4, 1));
            Assert.Matches("^#(7f|ff)(7f|ff)(7f|ff)$", ((string)random.skin_color).ToLowerInvariant());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSheetsOfferTheirRecipes()
    {
        var (r, _, bob) = Room(9203);
        r.In(() =>
        {
            Assert.Contains("wallet", Titles(Dm.@new<obj_item_stack_sheet_leather>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("belts", Titles(Dm.@new<obj_item_stack_sheet_leather>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("diamond door", Titles(Dm.@new<obj_item_stack_sheet_mineral_diamond>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("fancy boxes", Titles(Dm.@new<obj_item_stack_sheet_cardboard>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("bedsheet", Titles(Dm.@new<obj_item_stack_sheet_cloth>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("bronze chair", Titles(Dm.@new<obj_item_stack_sheet_bronze>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("carp mask", Titles(Dm.@new<obj_item_stack_sheet_animalhide_carp>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("snowman", Titles(Dm.@new<obj_item_stack_sheet_mineral_snow>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("bluespace crystal tile", Titles(Dm.@new<obj_item_stack_sheet_bluespace_crystal>(locate(6, 4, 1)).get_main_recipes()));
            Assert.Contains("leather_recipes", Dm.each(GLOB.gvars_datum_init_order).OfType<string>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TitaniumAndFiveRodsMakeShuttleFrameRods()
    {
        var (r, feed, bob) = Room(9204);
        var titanium = r.In(() => Dm.@new<obj_item_stack_sheet_mineral_titanium>(bob.loc, 2));
        var rods = Hold<obj_item_stack_rods>(r, bob, 10);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(titanium.item_interaction(bob, rods)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(titanium.amount));
            Assert.Equal(5f, Dm.num(rods.amount));
            Assert.NotEmpty(Dm.each(range(1, bob)).OfType<obj_item_stack_rods_shuttle>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CoalLitByAWelderBurnsAwayIntoCarbonDioxide()
    {
        var (r, _, bob) = Room(9205);
        var coal = r.In(() => Dm.@new<obj_item_stack_sheet_mineral_coal>(locate(5, 4, 1), 5));
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(welder.welding)));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(coal.item_interaction(bob, welder)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(coal));
            var air = Waiting.NoSleep(locate(5, 4, 1).return_air()) as datum_gas_mixture;
            Assert.True(Dm.truthy(air.has_gas(typeof(datum_gas_carbon_dioxide), 1)));
        });
        r.In(() => Assert.True(Dm.@in(typeof(datum_reagent_carbon), Waiting.NoSleep(Dm.@new<obj_item_stack_sheet_mineral_coal>(locate(6, 4, 1)).grind_results()) as DmList)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClownStampMakesACardboardSheetAClownBox()
    {
        var (r, feed, bob) = Room(9206);
        var cardboard = r.In(() => Dm.@new<obj_item_stack_sheet_cardboard>(bob.loc, 3));
        var stamp = Hold<obj_item_stamp_clown>(r, bob);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(cardboard.item_interaction(bob, stamp)))));
        r.Step(2);
        Assert.True(feed.Any("It's a clown box! Honk!"));
        r.In(() =>
        {
            Assert.Equal(2f, Dm.num(cardboard.amount));
            Assert.NotEmpty(Dm.each(bob.loc.contents).OfType<obj_item_storage_box_clown>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWallFillingWeldsBackIntoTwoSheetsAndOffersItsReskins()
    {
        var (r, feed, bob) = Room(9207);
        var filling = r.In(() => Dm.@new<obj_item_stack_wall_filling_plastitanium>(bob.loc, 3));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(4, filling.wall_reskin_types.Count);
            Assert.True(Dm.@in(typeof(obj_item_stack_wall_filling_plastitanium_survivalpod), filling.wall_reskin_types));
            // plastitanium's throwforce of 2 takes 50 throws to crit a human: tg rates no throw of it (wall_filling.dm:31)
            string text = string.Join("\n", Dm.each(Waiting.NoSleep(filling.examine(bob)) as DmList).Select(x => x?.ToString()));
            Assert.DoesNotContain("throwing weapon", text, StringComparison.Ordinal);
            filling.throwforce = 25; // four throws: "great"
            text = string.Join("\n", Dm.each(Waiting.NoSleep(filling.examine(bob)) as DmList).Select(x => x?.ToString()));
            Assert.Contains("a great throwing weapon", text, StringComparison.Ordinal);
            filling.throwforce = 2;
        });
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(filling.item_interaction(bob, welder)))));
        r.Step(2);
        Assert.True(feed.Any("You shaped"));
        r.In(() =>
        {
            Assert.Equal(2f, Dm.num(filling.amount));
            var sheet = Dm.each(range(1, bob)).OfType<obj_item_stack_sheet_mineral_plastitanium>().FirstOrDefault();
            Assert.NotNull(sheet);
            Assert.Equal(2f, Dm.num(sheet.amount));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FourMineralTilesWeldIntoOneSheet()
    {
        var (r, feed, bob) = Room(9208);
        var tiles = r.In(() => Dm.@new<obj_item_stack_tile_mineral_diamond>(bob.loc, 6));
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(tiles.welder_act(bob, welder)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(2f, Dm.num(tiles.amount));
            Assert.NotEmpty(Dm.each(range(1, bob)).OfType<obj_item_stack_sheet_mineral_diamond>());
            Assert.False(InertSeam.IsInert(tiles));
        });
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(tiles.welder_act(bob, welder)))));
        Assert.True(feed.Any("You need at least four tiles to do this!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWiredGlassTileTakesIronToLightAndIsCutBackIntoCableAndGlass()
    {
        var (r, feed, bob) = Room(9209);
        var wired = r.In(() => Dm.@new<obj_item_stack_light_w>(bob.loc, 3));
        var iron = Hold<obj_item_stack_sheet_iron>(r, bob, 5);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(wired.item_interaction(bob, iron)))));
        r.Step(2);
        Assert.True(feed.Any("You make a light tile."));
        r.In(() =>
        {
            Assert.Equal(2f, Dm.num(wired.amount));
            Assert.Equal(4f, Dm.num(iron.amount));
            Assert.NotEmpty(Dm.each(range(1, bob)).OfType<obj_item_stack_tile_light>());
        });
        var cutters = r.In(() => Dm.@new<obj_item_wirecutters>(bob.loc));
        r.In(() => Waiting.NoSleep(wired.wirecutter_act(bob, cutters)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(wired.amount));
            Assert.NotEmpty(Dm.each(range(1, bob)).OfType<obj_item_stack_cable_coil>());
            Assert.NotEmpty(Dm.each(range(1, bob)).OfType<obj_item_stack_sheet_glass>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ArcadeTicketsPileUpWithTheirCount()
    {
        var (r, _, bob) = Room(9210);
        r.In(() =>
        {
            var tickets = Dm.@new<obj_item_stack_arcadeticket>(locate(6, 4, 1), 1);
            Waiting.NoSleep(tickets.update_icon_state());
            Assert.Equal("arcade-ticket", tickets.icon_state);
            Waiting.NoSleep(tickets.add(3));
            Waiting.NoSleep(tickets.update_icon_state());
            Assert.Equal("arcade-ticket_2", tickets.icon_state);
            Waiting.NoSleep(tickets.add(4));
            Waiting.NoSleep(tickets.update_icon_state());
            Assert.Equal("arcade-ticket_3", tickets.icon_state);
            Waiting.NoSleep(tickets.add(10));
            Waiting.NoSleep(tickets.update_icon_state());
            Assert.Equal("arcade-ticket_4", tickets.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrystalBreaksOffAPolycrystalHeldInTheOtherHand()
    {
        var (r, feed, bob) = Room(9211);
        var poly = Hold<obj_item_stack_sheet_bluespace_crystal>(r, bob, 2);
        r.In(() => bob.swap_hand());
        r.Step(2);
        r.In(() => Assert.Same(poly, bob.get_inactive_held_item()));
        r.In(() => Waiting.NoSleep(poly.attack_hand(bob)));
        r.Step(2);
        Assert.True(feed.Any("You break off a crystal."));
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(poly.amount));
            Assert.IsAssignableFrom<obj_item_stack_ore_bluespace_crystal_refined>(bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void DuctTapeRepairsADamagedThingOnTheRightButton()
    {
        var (r, feed, bob) = Room(9212);
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        r.In(() => Waiting.NoSleep(crate.take_damage(50, BRUTE, MELEE, FALSE)));
        float before = r.In(() => Dm.num(crate.get_integrity()));
        var tape = Hold<obj_item_stack_medical_wrap_sticky_tape_duct>(r, bob, 3);
        r.In(() => Waiting.Detach(tape.interact_with_atom_secondary(crate, bob)));
        r.Step(4 * Second);
        Assert.True(feed.Any("You finish repairing"));
        r.In(() =>
        {
            Assert.True(Dm.num(crate.get_integrity()) > before);
            Assert.Equal(2f, Dm.num(tape.amount));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASheetOfPizzaIsEdibleAndAHauntiumSheetIsHaunted()
    {
        var (r, _, bob) = Room(9213);
        var pizza = r.In(() => Dm.@new<obj_item_stack_sheet_pizza>(bob.loc, 2));
        var hauntium = r.In(() => Dm.@new<obj_item_stack_sheet_hauntium>(bob.loc, 2));
        r.Step(2);
        r.In(() =>
        {
            // the pizza material's edible component (pizza.dm:46): nutriment and oil by the sheets' material amount
            Assert.NotNull(pizza.GetComponent(typeof(datum_component_edible)));
            Assert.True(Dm.truthy(((datum_reagents)pizza.reagents).has_reagent(typeof(datum_reagent_consumable_nutriment))));
            Assert.True(HAS_TRAIT(pizza, TRAIT_ROD_REMOVE_FISHING_DUD));
            // the hauntium material haunts the item (hauntium.dm:23)
            Assert.True(HAS_TRAIT(hauntium, TRAIT_HAUNTED));
            Assert.Contains("huge pizza", Titles(pizza.get_main_recipes()));
        });
        r.AssertNoRuntimes();
    }
}
