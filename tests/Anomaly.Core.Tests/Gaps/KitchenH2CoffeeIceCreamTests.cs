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
/// Gaps package H2 (D333), the coffee and ice cream part, in a booted toy room with Bob as a player: the coffeemaker's radial (brewing
/// from the cartridge into the pot, the steam, taking a cup), the impressa's dried beans, the ice cream vat's scoop on a waffle cone
/// (the flavour's name, reagents and overlay) and its cones mode, and the food cart unpacking into its stand and packing back.
/// </summary>
public class KitchenH2CoffeeIceCreamTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, bob);
    }

    /// <summary>An atom as the map loader makes it: Initialize with mapload (as BrigTimerTests' Mapped).</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y) where T : atom
    {
        T made = r.In(() =>
        {
            float previous = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            T thing = (T)Dm.@new(typeof(T), locate(x, y, 1));
            SSatoms.initialized = previous;
            // the map loader's late loaders (tg's InitializeAtoms runs them after the map's Initialize): the machines' power_change
            DmList late = SSatoms.late_loaders;
            SSatoms.late_loaders = Dm.list();
            foreach (atom lateLoader in Dm.each_cast<atom>(late))
                lateLoader.LateInitialize();
            return thing;
        });
        r.Step(Second);
        return made;
    }

    private static void Hold(AudioRoom r, mob_living_carbon_human bob, obj_item item) => r.In(() => Waiting.NoSleep(bob.put_in_active_hand(item)));

    private static float Amount(AudioRoom r, atom holder, Type reagent) => r.In(() => Dm.num(((datum_reagents)holder.reagents).get_reagent_amount(reagent)));

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second); // tg's click cooldown of the click before
        Click(r, who, target, modifiers);
    }

    private static void PickRadial(AudioRoom r, mob_living_carbon_human bob, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu");
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(bob.client, id, index));
        r.Step(6); // show_radial_menu's wait loop sees the choice at its next check
    }

    [Fact]
    public void TheCoffeemakerBrewsTheCartridgeIntoItsPotAndHandsOutACup()
    {
        var (r, bob) = Room(9801);
        var feed = Listen(r);
        var maker = Mapped<obj_machinery_coffeemaker>(r, 5, 4);
        Assert.NotNull(r.In(() => maker.coffeepot));
        Assert.NotNull(r.In(() => maker.cartridge));
        var overlays = Overlays(r, maker);
        Assert.Contains("coffeemaker_pot_empty", overlays);
        Assert.Contains("coffeemaker_cartidge", overlays);

        Use(r, bob, maker);
        PickRadial(r, bob, "Brew");
        Assert.True(r.In(() => Dm.truthy(maker.brewing)), "not brewing: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + "; stat " + r.In(() => maker.machine_stat) + "; " + string.Join("\n", r.Room.RuntimeText));
        Assert.Equal(120f, Amount(r, maker.coffeepot, typeof(datum_reagent_consumable_coffee)));
        Assert.Equal(3f, r.In(() => maker.cartridge.charges));
        Assert.Contains("coffeemaker_pot_full", Overlays(r, maker));
        r.Step(20 * Second + 5); // brew_time at speed 1
        Assert.Equal(0f, r.In(() => maker.brewing));

        // a cup from the cup holder
        Use(r, bob, maker);
        PickRadial(r, bob, "Take Cup");
        Assert.IsType<obj_item_reagent_containers_cup_glass_coffee_cup>(r.In(() => bob.get_active_held_item()));
        Assert.Equal(14f, r.In(() => maker.coffee_cups));

        // the right click hands the pot back
        r.In(() => qdel(bob.get_active_held_item()));
        r.Step(Second);
        Click(r, bob, maker, "right=1;button=right");
        r.Step(2);
        Assert.Null(r.In(() => maker.coffeepot));
        Assert.IsType<obj_item_reagent_containers_cup_coffeepot>(r.In(() => bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImpressaGrindsDriedBeansFromItsPack()
    {
        var (r, bob) = Room(9802);
        var feed = Listen(r);
        var maker = Mapped<obj_machinery_coffeemaker_impressa>(r, 5, 4);
        Assert.Null(r.In(() => maker.cartridge));
        var pack = r.In(() => Dm.@new<obj_item_storage_box_coffeepack>(bob.loc));
        r.Step(2);
        Assert.Equal(5, r.In(() => Dm.each<obj_item_food_grown_coffee>(pack.contents).Count(b => HAS_TRAIT(b, TRAIT_DRIED))));
        Hold(r, bob, pack);
        Use(r, bob, maker);
        Assert.Equal(5f, r.In(() => maker.coffee_amount));
        Assert.Contains("grinder_half", Overlays(r, maker));
        r.In(() => qdel(pack)); // an empty hand opens the coffeemaker's radial

        Use(r, bob, maker);
        PickRadial(r, bob, "Brew");
        Assert.True(r.In(() => maker.coffee_amount) == 4f, "not brewed: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + "; stat " + r.In(() => maker.machine_stat) + "; " + string.Join("\n", r.Room.RuntimeText));
        Assert.True(Amount(r, maker.coffeepot, typeof(datum_reagent_consumable_coffee)) > 0, "no coffee brewed from the beans");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIceCreamVatScoopsOntoAConeAndMakesCones()
    {
        var (r, bob) = Room(9803);
        var vat = Place<obj_machinery_icecream_vat>(r, 5, 4);
        Assert.Equal(6f, Amount(r, vat, typeof(datum_reagent_consumable_vanilla)));

        var cone = r.In(() => Dm.@new<obj_item_food_icecream>(bob.loc));
        r.Step(2);
        Assert.NotNull(r.In(() => cone.GetComponent(typeof(datum_component_ice_cream_holder))));
        Hold(r, bob, cone);
        Use(r, bob, vat);
        var holder = r.In(() => (datum_component_ice_cream_holder)cone.GetComponent(typeof(datum_component_ice_cream_holder)));
        Assert.Equal(new[] { "vanilla" }, r.In(() => Dm.each(holder.scoops).Select(Dm.str).ToArray()));
        Assert.Equal("vanilla ice cream", r.In(() => cone.name));
        Assert.Equal(5f, Amount(r, vat, typeof(datum_reagent_consumable_vanilla))); // one unit of each ingredient
        Assert.True(Amount(r, cone, typeof(datum_reagent_consumable_sugar)) >= 10, "no sweetener in the scoop");
        Assert.Contains("icecream_custom", Overlays(r, cone));
        Assert.Equal(typeof(datum_status_effect_food_chilling), r.In(() => cone.crafted_food_buff));

        // the right click swaps to cones; a click makes one from flour and sugar
        r.In(() => qdel(cone));
        r.Step(Second);
        Click(r, bob, vat, "right=1;button=right");
        r.Step(2);
        Assert.Equal("cones", r.In(() => vat.vat_mode));
        float flour = Amount(r, vat, typeof(datum_reagent_consumable_flour));
        r.In(() => Waiting.NoSleep(vat.make_cone(bob, typeof(obj_item_food_icecream), Dm.list(typeof(datum_reagent_consumable_flour), typeof(datum_reagent_consumable_sugar)))));
        r.Step(2);
        Assert.Equal(flour - 1, Amount(r, vat, typeof(datum_reagent_consumable_flour)));
        Assert.IsType<obj_item_food_icecream>(r.In(() => bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFoodCartUnpacksIntoItsStandAndPacksBack()
    {
        var (r, bob) = Room(9804);
        var cart = Place<obj_machinery_food_cart>(r, 6, 5);
        // tg's own runtime: a cart made after the map load queues its packed table's smoothing (structures.dm:24), and SSicon_smooth's
        // fire smooths it inside the cart without a z (icon_smoothing.dm:138's CRASH); a mapped cart's table is skipped at the
        // subsystem's Initialize (icon_smooth.dm:54)
        r.Step(2);
        r.Room.Runtimes.RemoveAll(e => e.Message.Contains("/obj/structure/table/reinforced called smooth_icon() without being on a z-level", StringComparison.Ordinal));
        r.In(() => Waiting.NoSleep(cart.unpack(bob)));
        r.Step(Second); // the unpacked tables smooth with their neighbours before they are packed again (tg's smooth_icon needs a z)
        Assert.Equal(1f, r.In(() => cart.unpacked));
        Assert.Equal(1f, r.In(() => cart.anchored));
        Assert.Same(r.In(() => locate(7, 4, 1)), r.In(() => cart.cart_table.loc));
        Assert.Same(r.In(() => locate(8, 4, 1)), r.In(() => cart.cart_griddle.loc));
        Assert.Same(r.In(() => locate(6, 4, 1)), r.In(() => cart.cart_smartfridge.loc));

        r.In(() => Waiting.NoSleep(cart.pack_up()));
        r.Step(2);
        Assert.Equal(0f, r.In(() => cart.unpacked));
        Assert.Same(cart, r.In(() => cart.cart_griddle.loc));
        r.AssertNoRuntimes();
    }
}
