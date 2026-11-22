using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package S2: the vending machines (code/modules/vending) in a booted toy room. A click opens tg's "Vending" window with the
/// records, their categories and the stock; "vend" spawns the product in the buyer's hand, takes it from the stock and thanks the buyer
/// with tg's vend sound, charging the buyer's ID card account the price (gaps P4a: the payment component); a refill canister restocks an open vendor; a tilted vendor crushes and pins a player, a witness remembers it, and it is righted by hand; the
/// contraband wire opens the hidden stock; every vendor Meta maps builds its stock and dispenses each of its products.
/// </summary>
public class VendingTests
{
    private const int X = 5, Y = 4;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        // the toy room has no APC: free power, as use_energy gives shuttles
        r.In(() => (get_area(locate(X, Y, 1)) as area).requires_power = FALSE);
        Funds(r, bob, 500);
        return (r, bob);
    }

    // An assistant's ID card in the ID slot with a bank account holding the credits, as a crewman's (gaps P4a: the vendors charge it)
    internal static datum_bank_account Funds(AudioRoom r, mob_living_carbon_human who, float credits) => r.In(() =>
    {
        if (who.w_uniform == null) // the ID slot hangs on a jumpsuit
            Waiting.NoSleep(who.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(who.loc), ITEM_SLOT_ICLOTHING));
        var card = Dm.@new<obj_item_card_id_advanced>(who.loc);
        Waiting.NoSleep(who.equip_to_slot_or_del(card, ITEM_SLOT_ID));
        Assert.Same(card, who.wear_id);
        var account = Dm.@new<datum_bank_account>(who.real_name, SSjob.get_job_type(typeof(datum_job_assistant)), 1, TRUE);
        account.adjust_money(credits);
        card.set_account(account);
        return account;
    });

    private static T Vendor<T>(AudioRoom r) where T : obj_machinery_vending => Place<T>(r, X, Y);

    private static datum_data_vending_product Record(AudioRoom r, obj_machinery_vending vendor, Type path) => r.In(() =>
        Dm.each_cast<datum_data_vending_product>(Dm.add(Dm.add(vendor.product_records, vendor.coin_records), vendor.hidden_records))
            .First(record => Dm.eq(record.product_path, path)));

    private static string OpenWindow(AudioRoom r, mob who, atom vendor, out JsonElement data, out JsonElement staticData)
    {
        Take(who);
        Click(r, who, vendor);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal("Vending", InterfaceOf(payload));
        data = payload.GetProperty("data");
        staticData = payload.GetProperty("static_data");
        return window;
    }

    private static string OpenWindow(AudioRoom r, mob who, atom vendor) => OpenWindow(r, who, vendor, out _, out _);

    [Fact]
    public void AClickOpensTheWindowAndVendPutsTheProductInTheHandFromTheStock()
    {
        var (r, bob) = Room(9901);
        GapsHarness.Feed feed = Listen(r);
        var vendor = Vendor<obj_machinery_vending_snack>(r);
        // its board gave it its refill canister, which filled the records (RefreshParts, restock) and is empty now
        r.In(() =>
        {
            Assert.IsType<obj_item_circuitboard_machine_vendor>(vendor.circuit_value);
            var canister = Dm.locate<obj_item_vending_refill_snack>(vendor.component_parts);
            Assert.NotNull(canister);
            Assert.Equal(0f, Dm.num(canister.get_part_rating()));
            Assert.False(Dm.truthy(vendor.all_products_free)); // a station vendor charges (the payment component, gaps P4a)
            Assert.NotNull(vendor.GetComponent(typeof(datum_component_payment)));
            Assert.Equal("snack", vendor.icon_state);
        });

        string window = OpenWindow(r, bob, vendor, out JsonElement data, out JsonElement staticData);
        Assert.Equal(0, staticData.GetProperty("all_products_free").GetInt32());
        JsonElement candyRecord = staticData.GetProperty("product_records").EnumerateArray().First(p => p.GetProperty("path").GetString() == "food-candy");
        Assert.Equal(("candy", "Products"), (candyRecord.GetProperty("name").GetString(), candyRecord.GetProperty("category").GetString()));
        Assert.Equal(("icons/obj/food/food.dmi", "candy"), (candyRecord.GetProperty("icon").GetString(), candyRecord.GetProperty("icon_state").GetString()));
        Assert.Equal(6, data.GetProperty("stock").GetProperty("food-candy").GetProperty("amount").GetInt32());
        // the premium and contraband records come with their categories; the contraband is hidden until the vendor is hacked
        Assert.True(staticData.GetProperty("categories").TryGetProperty("Premium", out _));
        Assert.True(staticData.GetProperty("categories").TryGetProperty("Contraband", out _));
        Assert.Equal(0, data.GetProperty("extended_inventory").GetInt32());

        datum_bank_account account = r.In(() => (bob.get_idcard(TRUE) as obj_item_card_id).registered_account);
        float price = r.In(() => Dm.num(Record(r, vendor, typeof(obj_item_food_candy)).price));
        Assert.True(price > 0);
        // the balance right before the vend: SSeconomy's payday may already have paid the assistant (its first fire is staggered by
        // the MC's rand, so where it falls moves with every new subsystem)
        float before = r.In(() => account.account_balance);
        Act(r, bob, window, "vend", ("ref", candyRecord.GetProperty("ref").GetString()), ("premium", FALSE));
        // the assistant's civilian paycheck department is not the vendor's (service), so no discount
        Assert.Equal(before - price, r.In(() => account.account_balance));
        Assert.Equal(round(price * VENDING_CREDITS_COLLECTION_AMOUNT), r.In(() => vendor.credits_contained));
        JsonElement after = Data(r, bob, out _);
        Assert.Equal(5, after.GetProperty("stock").GetProperty("food-candy").GetProperty("amount").GetInt32());
        r.In(() =>
        {
            Assert.IsType<obj_item_food_candy>(bob.get_active_held_item());
            Assert.Equal(5f, Dm.num(Record(r, vendor, typeof(obj_item_food_candy)).amount));
        });
        Assert.True(feed.Any("You take candy out of the slot."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(feed.Any("Thank you for shopping with"), "the vendor thanks the buyer");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/machine_vend.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCoffeeMachineFlicksItsVendStateAndASoldOutProductIsDenied()
    {
        var (r, bob) = Room(9902);
        var flicks = new List<(object Target, object State)>();
        var vendor = Vendor<obj_machinery_vending_coffee>(r);
        datum_data_vending_product coffee = Record(r, vendor, typeof(obj_item_reagent_containers_cup_glass_coffee));
        string window = OpenWindow(r, bob, vendor);
        PresentationSinks.Of(r.World).Appearance = new FlickRecorder(flicks);

        Act(r, bob, window, "vend", ("ref", r.In(() => (string)REF(coffee))), ("premium", FALSE));
        Assert.Contains(flicks, f => ReferenceEquals(f.Target, vendor) && Dm.eq(f.State, "coffee-vend"));
        r.In(() => Assert.IsType<obj_item_reagent_containers_cup_glass_coffee>(bob.get_active_held_item()));

        // sold out: tg's "Sold out of" and no product
        r.In(() => coffee.amount = 0);
        r.In(() => qdel(bob.get_active_held_item()));
        Act(r, bob, window, "vend", ("ref", r.In(() => (string)REF(coffee))), ("premium", FALSE));
        r.In(() => Assert.Null(bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARefillCanisterRestocksAnOpenVendor()
    {
        var (r, bob) = Room(9903);
        GapsHarness.Feed feed = Listen(r);
        var vendor = Vendor<obj_machinery_vending_snack>(r);
        datum_data_vending_product chips = Record(r, vendor, typeof(obj_item_food_chips));
        r.In(() => chips.amount = 1);

        // a closed panel refuses the canister
        var canister = WallTests.Hold<obj_item_vending_refill_snack>(r, bob);
        Assert.EndsWith("Getmore Chocolate Corp restocking unit", r.In(() => (string)canister.name)); // after tg's \improper marker
        Click(r, bob, vendor);
        Assert.True(feed.Any("You should probably unscrew the service panel first!"));
        Assert.Equal(1f, r.In(() => Dm.num(chips.amount)));

        r.Step(Second); // the click cooldown
        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, vendor);
        Assert.Equal(1f, r.In(() => Dm.num(vendor.panel_open)));
        Assert.Contains("panel2", r.In(() => Dm.each(vendor.overlays).Select(o => o is IDmAppearance a ? a.icon_state : Dm.str(o)).ToList()));

        r.Step(Second);
        canister = WallTests.Hold<obj_item_vending_refill_snack>(r, bob);
        Click(r, bob, vendor);
        Assert.True(feed.Any("You loaded 5 items in"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal(6f, r.In(() => Dm.num(chips.amount)));
        // the canister took the vendor's whole product list and gave five chips of it
        Assert.Equal(1f, r.In(() => Dm.num(canister.products[typeof(obj_item_food_chips)])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATiltedVendorCrushesAndPinsAPlayerAndIsRightedByHand()
    {
        var (r, bob) = Room(9904);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        var vendor = Vendor<obj_machinery_vending_cola>(r);
        float before = r.In(() => Dm.num(bob.get_brute_loss()));

        object flags = r.In(() => Waiting.NoSleep(vendor.tilt(bob, forced_crit: VENDOR_CRUSH_CRIT_PIN)));
        Assert.True(Dm.truthy(Dm.bitand(flags, SUCCESSFULLY_FELL_OVER)) && Dm.truthy(Dm.bitand(flags, SUCCESSFULLY_CRUSHED_MOB)));
        r.In(() =>
        {
            Assert.Equal(TRUE, vendor.tilted);
            Assert.Same(get_turf(bob), get_turf(vendor)); // it fell onto Bob's tile
            Assert.Same(vendor, bob.buckled); // pinned underneath
            Assert.True(Dm.num(bob.get_brute_loss()) > before, "the vendor crushes Bob");
            Assert.Equal(ABOVE_MOB_LAYER, vendor.layer);
            var turned = (matrix)vendor.transform;
            Assert.True(Math.Abs(Math.Abs(turned.b) - 1) < 0.001f, "the vendor lies turned by a quarter");
            Assert.NotNull(alice.mind.memories[typeof(datum_memory_witness_vendor_crush)]); // a witness remembers it (tilting.dm:236)
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/bang.ogg");

        // Alice rights it: five seconds of work, then Bob is free and the vendor stands again
        Click(r, alice, vendor);
        r.Step(5 * Second + 4);
        r.In(() =>
        {
            Assert.Equal(FALSE, vendor.tilted);
            Assert.Null(bob.buckled);
            Assert.Equal(Dm.initial(vendor, (atom p) => p.layer), vendor.layer);
            var upright = (matrix)vendor.transform;
            Assert.True(Math.Abs(upright.b) < 0.001f && Math.Abs(upright.a - 1) < 0.001f, "the vendor stands upright");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheContrabandWireOpensTheHiddenStock()
    {
        var (r, bob) = Room(9905);
        var vendor = Vendor<obj_machinery_vending_snack>(r);
        datum_data_vending_product syndicake = Record(r, vendor, typeof(obj_item_food_syndicake));
        string syndicakeRef = r.In(() => (string)REF(syndicake));
        string window = OpenWindow(r, bob, vendor);

        Act(r, bob, window, "vend", ("ref", syndicakeRef), ("premium", TRUE));
        r.In(() => Assert.Null(bob.get_active_held_item())); // hidden: nothing comes out
        Assert.Equal(6f, r.In(() => Dm.num(syndicake.amount)));

        r.In(() => Waiting.NoSleep(vendor.wires.on_pulse(WIRE_CONTRABAND)));
        JsonElement data = Data(r, bob, out _, steps: 25);
        Assert.Equal(1, data.GetProperty("extended_inventory").GetInt32());
        Act(r, bob, window, "vend", ("ref", syndicakeRef), ("premium", TRUE));
        r.In(() =>
        {
            obj_item cake = bob.get_active_held_item() as obj_item;
            Assert.IsType<obj_item_food_syndicake>(cake);
            Assert.True(HAS_TRAIT(cake, TRAIT_CONTRABAND));
            Assert.Equal(5f, Dm.num(syndicake.amount));
        });
        r.AssertNoRuntimes();
    }

    /// <summary>The vending machines MetaStation maps (and the snack and cola machines its random spawners pick from).</summary>
    public static readonly Type[] MetaVendors =
    {
        typeof(obj_machinery_vending_assist), typeof(obj_machinery_vending_autodrobe), typeof(obj_machinery_vending_boozeomat),
        typeof(obj_machinery_vending_cart), typeof(obj_machinery_vending_cigarette), typeof(obj_machinery_vending_clothing),
        typeof(obj_machinery_vending_coffee), typeof(obj_machinery_vending_cola), typeof(obj_machinery_vending_cola_red),
        typeof(obj_machinery_vending_cola_blue), typeof(obj_machinery_vending_cola_black), typeof(obj_machinery_vending_cola_space_up),
        typeof(obj_machinery_vending_cola_starkist), typeof(obj_machinery_vending_cola_sodie), typeof(obj_machinery_vending_cola_pwr_game),
        typeof(obj_machinery_vending_cola_shamblers), typeof(obj_machinery_vending_cytopro), typeof(obj_machinery_vending_dinnerware),
        typeof(obj_machinery_vending_drugs), typeof(obj_machinery_vending_engivend), typeof(obj_machinery_vending_games),
        typeof(obj_machinery_vending_hydronutrients), typeof(obj_machinery_vending_hydroseeds), typeof(obj_machinery_vending_medical),
        typeof(obj_machinery_vending_modularpc), typeof(obj_machinery_vending_security), typeof(obj_machinery_vending_sustenance),
        typeof(obj_machinery_vending_tool), typeof(obj_machinery_vending_snack), typeof(obj_machinery_vending_snack_blue),
        typeof(obj_machinery_vending_snack_orange), typeof(obj_machinery_vending_snack_green), typeof(obj_machinery_vending_snack_teal),
        typeof(obj_machinery_vending_hotdog),
        typeof(obj_machinery_vending_wardrobe_atmos_wardrobe), typeof(obj_machinery_vending_wardrobe_bar_wardrobe),
        typeof(obj_machinery_vending_wardrobe_cargo_wardrobe), typeof(obj_machinery_vending_wardrobe_chap_wardrobe),
        typeof(obj_machinery_vending_wardrobe_chef_wardrobe), typeof(obj_machinery_vending_wardrobe_chem_wardrobe),
        typeof(obj_machinery_vending_wardrobe_coroner_wardrobe), typeof(obj_machinery_vending_wardrobe_curator_wardrobe),
        typeof(obj_machinery_vending_wardrobe_det_wardrobe), typeof(obj_machinery_vending_wardrobe_engi_wardrobe),
        typeof(obj_machinery_vending_wardrobe_gene_wardrobe), typeof(obj_machinery_vending_wardrobe_hydro_wardrobe),
        typeof(obj_machinery_vending_wardrobe_jani_wardrobe), typeof(obj_machinery_vending_wardrobe_law_wardrobe),
        typeof(obj_machinery_vending_wardrobe_medi_wardrobe), typeof(obj_machinery_vending_wardrobe_robo_wardrobe),
        typeof(obj_machinery_vending_wardrobe_science_wardrobe), typeof(obj_machinery_vending_wardrobe_sec_wardrobe),
        typeof(obj_machinery_vending_wardrobe_viro_wardrobe),
    };

    [Fact]
    public void EveryMetaVendorStocksAndDispensesEachOfItsProducts()
    {
        var (r, bob) = Room(9906);
        foreach (Type type in MetaVendors)
        {
            Assert.False(InertSeam.IsInert(r.In(() => Dm.@new(type, locate(X, Y, 1)) as atom) ?? throw new InvalidOperationException(type.Name)), type.Name);
            r.Step(2);
            r.In(() =>
            {
                var vendor = (obj_machinery_vending)Dm.locate_type(type, locate(X, Y, 1));
                Assert.True(vendor.product_records.len > 0, type.Name + " has no products");
                Waiting.NoSleep(vendor.ui_static_data(bob));
                foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(Dm.add(Dm.add(vendor.product_records, vendor.coin_records), vendor.hidden_records)))
                {
                    Assert.True(Dm.num(record.amount) > 0, type.Name + ": " + record.product_path + " is out of stock");
                    var made = Waiting.NoSleep(vendor.dispense(record, locate(X + 1, Y, 1), silent: TRUE)) as atom;
                    Assert.True(made != null && ReferenceEquals(made.GetType(), record.product_path), type.Name + ": " + record.product_path);
                    qdel(made);
                }
                qdel(vendor);
            });
            r.Step();
        }
        r.AssertNoRuntimes();
    }

    private sealed class FlickRecorder : IAppearanceSink
    {
        private readonly List<(object Target, object State)> _flicks;

        public FlickRecorder(List<(object Target, object State)> flicks) => _flicks = flicks;

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) { }

        public void Flick(object target, object state) => _flicks.Add((target, state));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }
}
