using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P4d (D298): the rest of cargo in the booted toy room of <see cref="CargoEconomyTests"/>. The express console drops a
/// pod in its landing zone or on its printed beacon and waits out its railgun; the materials market turns sheets into a stock block
/// and takes a private or budget buy order for the next shuttle; the produce console sends the chef's groceries on the shuttle, a
/// cooking voucher buys free and its express drops a pod; the mining console pays in mining points; the department order app links
/// to a head's silver ID, orders for free with its cooldown and a head overrides it; the shipping app prints a barcode; the black
/// market's telepad sells a local good, ships a purchase to the uplink and restocks the markets for credits.
/// </summary>
public class MarketTests
{
    private static float Budget(CargoEconomyTests.Cargo c) => c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);

    private static obj_item_card_id Card(CargoEconomyTests.Cargo c, Type trim, float credits = 0)
    {
        if (c.R.In(() => c.Bob.wear_id) == null)
            VendingTests.Funds(c.R, c.Bob, credits);
        else
            c.R.In(() => ((obj_item_card_id)c.Bob.wear_id).registered_account.adjust_money(credits));
        return c.R.In(() =>
        {
            var card = (obj_item_card_id)c.Bob.wear_id;
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
            return card;
        });
    }

    private static string Open(CargoEconomyTests.Cargo c, atom console, string @interface, out JsonElement data) => Open(c, console, @interface, out data, out _);

    private static string Open(CargoEconomyTests.Cargo c, atom console, string @interface, out JsonElement data, out JsonElement statics)
    {
        Take(c.Bob);
        Click(c.R, c.Bob, console);
        JsonElement payload = LastUpdate(Take(c.Bob), out string window);
        Assert.Equal(@interface, InterfaceOf(payload));
        data = payload.GetProperty("data");
        statics = payload.GetProperty("static_data");
        return window;
    }

    /// <summary>CentCom's holding area for pods in transit (tg's supplypod_temp_holding), which the toy room lacks: its corner turf.</summary>
    private static void HoldingArea(CargoEconomyTests.Cargo c) => c.R.In(() =>
    {
        var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
            ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
        set_turf_to_area(locate(1, 1, 1), lane);
    });

    private static List<T> InRoom<T>(CargoEconomyTests.Cargo c) where T : atom => c.R.In(() =>
        Dm.each<turf>(block(1, 1, 1, 11, 9, 1)).SelectMany(t => Dm.each<T>(t.contents)).ToList());

    private static object Act(CargoEconomyTests.Cargo c, datum target, string action, DmList @params = null)
    {
        var ui = c.R.In(() => Dm.@new<datum_tgui>(c.Bob, target, "Test", "test"));
        return c.R.In(() => Waiting.NoSleep(target.ui_act(action, @params ?? Dm.list(), ui, null)));
    }

    [Fact]
    public void The_express_console_drops_a_pod_in_its_landing_zone_and_on_its_beacon()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9801);
        HoldingArea(c);
        c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(10000));
        // no departmental payout during the test: SSeconomy's fire lands where the MC's rand staggered it, which moves with every new
        // subsystem, and its payout would show in the budget checks below
        c.R.In(() => SSeconomy.can_fire = FALSE);
        // the express console lands its pods on floors (tg's /turf/open/floor): the toy room's is indestructible plating. The floors
        // and the beacon below lie beyond the pod's landing blast (light 2, flame 3) from Bob at (4, 4) and the console at (4, 5): a
        // pod beside them can hurt Bob too much to order again
        c.R.In(() =>
        {
            foreach (turf t in Dm.each<turf>(block(1, 9, 1, 2, 9, 1)))
                Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_iron)));
        });
        var console = Place<obj_machinery_computer_cargo_express>(c.R, 4, 5);
        Assert.Same(c.R.In(() => get_area(console)), c.R.In(() => console.landingzone)); // no cargo bay here: the console's own area
        string window = Open(c, console, "CargoExpress", out _);
        string internals = c.R.In(() => Dm.text("[]", typeof(datum_supply_pack_emergency_internals)));
        float cost = c.R.In(() => ((datum_supply_pack)SSshuttle.supply_packs[typeof(datum_supply_pack_emergency_internals)]).get_cost());

        float budget = Budget(c);
        GapsHarness.Act(c.R, c.Bob, window, "add", ("id", internals));
        Assert.True(budget - cost == Budget(c), CargoEconomyTests.Diag(c));
        var zones = InRoom<obj_effect_pod_landingzone>(c);
        Assert.Single(zones);
        Assert.True(c.R.In(() => get_area(zones[0]) == get_area(console)), "the pod lands in the landing zone");

        // the railgun recalibrates for five seconds
        // (each check takes its baseline right before its action: SSeconomy's departmental payout may fall between them, its first
        // fire staggered by the MC's rand)
        budget = Budget(c);
        GapsHarness.Act(c.R, c.Bob, window, "add", ("id", internals));
        Assert.Equal(budget, Budget(c));

        // a beacon printed for the budget, moved away and chosen: the next pod lands on it
        budget = Budget(c);
        GapsHarness.Act(c.R, c.Bob, window, "printBeacon");
        Assert.Equal(budget - BEACON_COST, Budget(c));
        var beacon = c.R.In(() => console.beacon);
        Assert.NotNull(beacon);
        Assert.Same(console, c.R.In(() => beacon.express_console));
        Assert.StartsWith("Supply Pod Beacon #", c.R.In(() => Dm.str(beacon.name)));
        c.R.In(() => Waiting.NoSleep(beacon.forceMove(locate(7, 8, 1))));
        GapsHarness.Act(c.R, c.Bob, window, "LZBeacon");
        Assert.Equal(TRUE, c.R.In(() => console.using_beacon));
        c.R.Step(6 * Second);
        budget = Budget(c);
        GapsHarness.Act(c.R, c.Bob, window, "add", ("id", internals));
        Assert.True(budget - cost == Budget(c), $"budget {budget} -> {Budget(c)}, cost {cost}: " + CargoEconomyTests.Diag(c));
        Assert.Contains(InRoom<obj_effect_pod_landingzone>(c), zone => c.R.In(() => zone.loc == locate(7, 8, 1)));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_materials_market_makes_a_stock_block_and_takes_buy_orders()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9802);
        var market = Place<obj_machinery_materials_market>(c.R, 4, 5);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 5000);

        // sheets become a stock block of their market value
        float price = c.R.In(() => Dm.num(SSstock_market.materials_prices[typeof(datum_material_iron)]));
        Assert.True(price > 0, "iron trades on the market");
        var sheets = c.R.In(() =>
        {
            var s = Dm.@new<obj_item_stack_sheet_iron>(c.Bob.loc, 10);
            Waiting.NoSleep(c.Bob.put_in_active_hand(s));
            return s;
        });
        c.R.In(() => Waiting.NoSleep(market.item_interaction(c.Bob, sheets)));
        Assert.True(c.R.In(() => QDELETED(sheets)), "the sheets went into the market");
        var block = Assert.Single(InRoom<obj_item_stock_block>(c));
        Assert.Equal(price, c.R.In(() => block.export_value));
        Assert.True(c.Feed.Any("You have created a stock block worth"), CargoEconomyTests.Diag(c));

        // an assistant buys privately: the order waits for the next shuttle, a second buy joins it, clear drops it
        string window = Open(c, market, "MatMarket", out JsonElement data);
        Assert.Equal(1, data.GetProperty("orderingPrive").GetDouble());
        string iron = c.R.In(() => Dm.str(Dm.initial(typeof(datum_material_iron), (datum_material p) => p.name)));
        GapsHarness.Act(c.R, c.Bob, window, "buy", ("quantity", 5f), ("material", iron));
        var order = c.R.In(() => SSshuttle.shopping_list.len == 1 ? (datum_supply_order)SSshuttle.shopping_list[1] : null);
        Assert.NotNull(order);
        Assert.Same(mine, c.R.In(() => order.paying_account));
        Assert.Equal("Galactic Materials Market", c.R.In(() => Dm.str(order.orderer_rank)));
        GapsHarness.Act(c.R, c.Bob, window, "buy", ("quantity", 5f), ("material", iron));
        Assert.Equal(1, c.R.In(() => SSshuttle.shopping_list.len));
        Assert.Equal(10f, c.R.In(() => Dm.num(order.pack.contains[typeof(obj_item_stack_sheet_iron)])));
        GapsHarness.Act(c.R, c.Bob, window, "toggle_budget"); // no cargo access: still private
        Assert.Equal(TRUE, c.R.In(() => market.ordering_private));
        GapsHarness.Act(c.R, c.Bob, window, "clear");
        Assert.Equal(0, c.R.In(() => SSshuttle.shopping_list.len));

        // a cargo technician orders on the budget
        Card(c, typeof(datum_id_trim_job_cargo_technician), 5000);
        GapsHarness.Act(c.R, c.Bob, window, "toggle_budget");
        Assert.Equal(FALSE, c.R.In(() => market.ordering_private));
        GapsHarness.Act(c.R, c.Bob, window, "buy", ("quantity", 1f), ("material", iron));
        Assert.Null(c.R.In(() => ((datum_supply_order)SSshuttle.shopping_list[1]).paying_account));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_produce_console_sends_the_groceries_on_the_shuttle()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9803);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 1000);
        var console = Place<obj_machinery_computer_order_console_cook>(c.R, 4, 5);
        string window = Open(c, console, "ProduceConsole", out JsonElement data, out JsonElement statics);
        string potato = statics.GetProperty("order_datums").EnumerateArray().First(item => item.GetProperty("name").GetString() == "Potato").GetProperty("ref").GetString();

        GapsHarness.Act(c.R, c.Bob, window, "cart_set", ("target", potato), ("amt", 25f));
        GapsHarness.Act(c.R, c.Bob, window, "purchase");
        Assert.Equal(1000 - c.R.In(() => round(250 * 0.65f)), c.R.In(() => mine.account_balance));
        Assert.Equal(25f, c.R.In(() => Dm.num(SSshuttle.chef_groceries[Dm.locate_ref(potato)])));
        Assert.Equal(0, c.R.In(() => console.grocery_list.len));
        Assert.True(c.R.In(() => console.order_cooldown > world.time), "the console cools down");

        // the shuttle brings them in the kitchen's freezer crate
        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));
        var crate = Assert.Single(InRoom<obj_structure_closet_crate_freezer>(c));
        Assert.Equal("kitchen produce freezer", c.R.In(() => Dm.str(crate.name)));
        Assert.Equal(25, c.R.In(() => Dm.each<obj_item_food_grown_potato>(crate.contents).Count()));
        Assert.Equal(0, c.R.In(() => SSshuttle.chef_groceries.len));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_cooking_voucher_buys_free_and_express_drops_a_pod()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9804);
        HoldingArea(c);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 1000);
        var console = Place<obj_machinery_computer_order_console_cook>(c.R, 4, 5);
        var voucher = c.R.In(() =>
        {
            var v = Dm.@new<obj_item_cooking_voucher>(c.Bob.loc);
            Waiting.NoSleep(c.Bob.put_in_active_hand(v));
            return v;
        });
        c.R.In(() => Waiting.NoSleep(console.item_interaction(c.Bob, voucher)));
        Assert.Equal(10f, c.R.In(() => Dm.num(console.free_uses)));
        Assert.True(c.R.In(() => QDELETED(voucher)), "the voucher is spent");

        string window = Open(c, console, "ProduceConsole", out JsonElement data, out JsonElement statics);
        Assert.Equal(10, data.GetProperty("free_uses").GetDouble());
        string potato = statics.GetProperty("order_datums").EnumerateArray().First(item => item.GetProperty("name").GetString() == "Potato").GetProperty("ref").GetString();
        GapsHarness.Act(c.R, c.Bob, window, "add_one", ("target", potato));
        GapsHarness.Act(c.R, c.Bob, window, "add_one", ("target", potato));
        GapsHarness.Act(c.R, c.Bob, window, "remove_one", ("target", potato));
        Assert.Equal(1f, c.R.In(() => Dm.num(console.grocery_list[Dm.locate_ref(potato)])));
        GapsHarness.Act(c.R, c.Bob, window, "cart_set", ("target", potato), ("amt", 4f));
        Assert.Equal(0f, c.R.In(() => console.get_total_cost())); // within the free uses
        GapsHarness.Act(c.R, c.Bob, window, "express");
        Assert.Equal(1000f, c.R.In(() => mine.account_balance));
        Assert.Equal(6f, c.R.In(() => Dm.num(console.free_uses)));
        Assert.Contains(InRoom<obj_effect_pod_landingzone>(c), zone => c.R.In(() => zone.loc == c.Bob.loc));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_mining_console_pays_in_mining_points()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9805);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 100);
        c.R.In(() => mine.mining_points = 1000);
        var console = Place<obj_machinery_computer_order_console_mining>(c.R, 4, 5);
        string window = Open(c, console, "ProduceConsole", out JsonElement data, out JsonElement statics);
        Assert.Equal(1000, data.GetProperty("points").GetDouble());
        Assert.Equal("mp", statics.GetProperty("credit_type").GetString());
        string jaunter = statics.GetProperty("order_datums").EnumerateArray().First(item => item.GetProperty("name").GetString() == c.R.In(() => Dm.str(Dm.initial(typeof(obj_item_wormhole_jaunter), (atom p) => p.name)))).GetProperty("ref").GetString();

        GapsHarness.Act(c.R, c.Bob, window, "cart_set", ("target", jaunter), ("amt", 1f));
        GapsHarness.Act(c.R, c.Bob, window, "purchase");
        Assert.Equal(1000 - c.R.In(() => round(650 * 0.65f)), c.R.In(() => mine.mining_points));
        var order = c.R.In(() => (datum_supply_order)SSshuttle.shopping_list[1]);
        Assert.Same(mine, c.R.In(() => order.paying_account));
        Assert.Equal("Mining Vendor", c.R.In(() => Dm.str(order.orderer_rank)));

        // the shuttle ships it without charging the credits
        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));
        Assert.Equal(100f, c.R.In(() => mine.account_balance));
        Assert.Contains(InRoom<obj_structure_closet_crate>(c), crate => c.R.In(() => Dm.each<obj_item_wormhole_jaunter>(crate.contents).Any()));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_department_order_app_orders_for_free_with_a_cooldown()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9806);
        var console = Place<obj_machinery_modular_computer_preset_cargochat_service>(c.R, 4, 5);
        var app = c.R.In(() => (datum_computer_file_program_department_order)console.cpu.find_file_by_name("dept_order"));
        Assert.Equal(typeof(datum_job_department_service), c.R.In(() => app.linked_department));
        Assert.StartsWith("service ", c.R.In(() => Dm.str(console.name)));
        // the first pack the app offers (its static data: the department's cargo groups, what it can see)
        string pack = c.R.In(() =>
        {
            var data = (DmList)Waiting.NoSleep(app.ui_static_data(c.Bob));
            foreach (DmList group in Dm.each_cast<DmList>((DmList)data["supplies"]))
            {
                foreach (DmList entry in Dm.each_cast<DmList>((DmList)group["packs"]))
                    return Dm.str(entry["id"]);
            }
            return null;
        });
        Assert.NotNull(pack);

        // an assistant cannot use it
        Card(c, typeof(datum_id_trim_job_assistant));
        Act(c, app, "order", c.R.In(() => Dm.list(Dm.kv("id", pack))));
        Assert.Equal(0, c.R.In(() => SSshuttle.shopping_list.len));

        // a cook orders for free; the next order waits for the cooldown
        Card(c, typeof(datum_id_trim_job_cook));
        Act(c, app, "order", c.R.In(() => Dm.list(Dm.kv("id", pack))));
        var order = c.R.In(() => app.department_order);
        Assert.NotNull(order);
        Assert.Equal("Departmental Order", c.R.In(() => Dm.str(order.reason)));
        Assert.Null(c.R.In(() => order.paying_account));
        DmList data = c.R.In(() => Waiting.NoSleep(app.ui_data(c.Bob)) as DmList);
        Assert.NotNull(c.R.In(() => data["time_left"]));
        Act(c, app, "order", c.R.In(() => Dm.list(Dm.kv("id", pack))));
        Assert.Equal(1, c.R.In(() => SSshuttle.shopping_list.len));

        // the cook cannot override; the head of personnel can, and the cooldown resets
        Act(c, app, "override_order");
        Assert.Equal(1, c.R.In(() => SSshuttle.shopping_list.len));
        var silver = c.R.In(() =>
        {
            var card = Dm.@new<obj_item_card_id_advanced_silver>(c.Bob.loc);
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, typeof(datum_id_trim_job_head_of_personnel)));
            Waiting.NoSleep(console.cpu.insert_id(card, null)); // the console reads its inserted ID first
            return card;
        });
        Act(c, app, "override_order");
        Assert.Equal(0, c.R.In(() => SSshuttle.shopping_list.len));
        Assert.Null(c.R.In(() => app.department_order));
        Act(c, app, "order", c.R.In(() => Dm.list(Dm.kv("id", pack))));
        Assert.Equal(1, c.R.In(() => SSshuttle.shopping_list.len));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void An_unlinked_department_console_links_to_a_heads_silver_id()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9807);
        var console = Place<obj_machinery_modular_computer_preset_cargochat>(c.R, 4, 5);
        var app = c.R.In(() => (datum_computer_file_program_department_order)console.cpu.find_file_by_name("dept_order"));
        Assert.Null(c.R.In(() => app.linked_department));

        // a plain ID links nothing
        var plain = Card(c, typeof(datum_id_trim_job_cook));
        c.R.In(() => Waiting.NoSleep(console.cpu.insert_id(plain, null)));
        Act(c, app, "link");
        Assert.Null(c.R.In(() => app.linked_department));
        Assert.True(c.Feed.Any("balloon: no department found!"), CargoEconomyTests.Diag(c));

        var silver = c.R.In(() =>
        {
            var card = Dm.@new<obj_item_card_id_advanced_silver>(c.Bob.loc);
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, typeof(datum_id_trim_job_head_of_personnel)));
            Waiting.NoSleep(console.cpu.insert_id(card, null));
            return card;
        });
        Assert.Same(silver, c.R.In(() => console.cpu.stored_id));
        Act(c, app, "link");
        Assert.Equal(typeof(datum_job_department_service), c.R.In(() => app.linked_department));
        Assert.True(c.Feed.Any("balloon: linked"), CargoEconomyTests.Diag(c));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_shipping_app_prints_a_barcode_for_the_selected_account()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9808);
        var console = Place<obj_machinery_modular_computer_preset_cargochat_cargo>(c.R, 4, 5);
        var app = c.R.In(() => (datum_computer_file_program_shipping)console.cpu.find_file_by_name("shipping"));
        Assert.NotNull(app);
        var card = Card(c, typeof(datum_id_trim_job_cargo_technician));
        c.R.In(() => console.cpu.stored_paper = 5);

        Assert.Equal(FALSE, Act(c, app, "print")); // no ID inside
        c.R.In(() => Waiting.NoSleep(console.cpu.insert_id(card, null)));
        Act(c, app, "print"); // no account chosen yet
        Assert.Empty(InRoom<obj_item_barcode>(c));
        Act(c, app, "selectid");
        Assert.Same(c.R.In(() => card.registered_account), c.R.In(() => app.payments_acc));
        Act(c, app, "print");
        var barcode = Assert.Single(InRoom<obj_item_barcode>(c));
        Assert.Same(c.R.In(() => card.registered_account), c.R.In(() => barcode.payments_acc));
        Assert.Equal(c.R.In(() => app.cut_multiplier), c.R.In(() => barcode.cut_multiplier));
        Assert.Equal(4f, c.R.In(() => console.cpu.stored_paper));
        Act(c, app, "resetid");
        Assert.Null(c.R.In(() => app.payments_acc));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_black_market_sells_a_local_good_through_the_telepad()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9809);
        var blackmarket = c.R.In(() => SSmarket.markets[typeof(datum_market_blackmarket)] as datum_market);
        Assert.NotNull(blackmarket);
        Assert.True(c.R.In(() => blackmarket.categories.len) > 0, "the market stocked its items at init");
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 1000);
        var pad = Place<obj_machinery_ltsrbt>(c.R, 5, 4);
        Assert.Contains(pad, c.R.In(() => Dm.each<obj_machinery_ltsrbt>(SSmarket.telepads).ToList()));

        // open, load a pen, price it and place it on the market for the fee
        var pen = c.R.In(() =>
        {
            Waiting.NoSleep(pad.attack_hand(c.Bob));
            var p = Dm.@new<obj_item_pen>(c.Bob.loc);
            Waiting.NoSleep(c.Bob.put_in_active_hand(p));
            Waiting.NoSleep(pad.item_interaction(c.Bob, p));
            return p;
        });
        Assert.Same(pen, c.R.In(() => pad.occupant));
        Assert.Equal(FALSE, c.R.In(() => pad.state_open));
        Act(c, pad, "change_price", c.R.In(() => Dm.list(Dm.kv("value", 100f))));
        Act(c, pad, "change_name", c.R.In(() => Dm.list(Dm.kv("value", "lucky pen"))));
        c.R.In(() => Waiting.NoSleep(pad.place_on_market(c.Bob)));
        float fee = PAYCHECK_LOWER * 1.2f;
        Assert.Equal(1000 - fee, c.R.In(() => mine.account_balance));
        Assert.Null(c.R.In(() => pen.loc));
        var good = c.R.In(() => Dm.each(blackmarket.available_items["Local Goods"] as DmList).Select(id => ((DmList)blackmarket.available_items["Local Goods"])[id]).OfType<datum_market_item_local_good>().Single());
        Assert.Equal("lucky pen", c.R.In(() => Dm.str(good.name)));
        Assert.EndsWith("Seller: " + c.R.In(() => Dm.str(mine.account_holder)), c.R.In(() => Dm.str(good.desc)));

        // Bob buys it back with the uplink, shipped through the telepad: it lands at the uplink
        var uplink = c.R.In(() =>
        {
            var u = Dm.@new<obj_item_market_uplink_blackmarket>(c.Bob.loc);
            Waiting.NoSleep(c.Bob.put_in_active_hand(u));
            Waiting.NoSleep(u.ui_interact(c.Bob));
            return u;
        });
        JsonElement payload = LastUpdate(Take(c.Bob), out string window);
        Assert.Equal("BlackMarketUplink", InterfaceOf(payload));
        string identifier = c.R.In(() => Dm.str(good.identifier));
        GapsHarness.Act(c.R, c.Bob, window, "set_category", ("category", "Local Goods"));
        GapsHarness.Act(c.R, c.Bob, window, "select", ("item", identifier));
        GapsHarness.Act(c.R, c.Bob, window, "buy", ("method", SHIPPING_METHOD_LTSRBT));
        float tax = c.R.In(() => round(100 * (1 - MARKET_WITHHOLDING_TAX)));
        Assert.Equal(1000 - fee - 100 - 40 + tax, c.R.In(() => mine.account_balance));
        for (int i = 0; i < 60 && c.R.In(() => pen.loc != c.Bob.loc); i++)
            c.R.Step(Second);
        Assert.True(c.R.In(() => pen.loc == c.Bob.loc), "the pen reaches the uplink: " + CargoEconomyTests.Diag(c));
        Assert.True(c.R.In(() => HAS_TRAIT(pen, TRAIT_CONTRABAND)), "the black market's goods are contraband");
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void Credits_on_the_telepad_restock_the_markets_and_double_the_price()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9810);
        var pad = Place<obj_machinery_ltsrbt>(c.R, 5, 4);
        float restock = CARGO_CRATE_VALUE * 3.375f;
        var chip = c.R.In(() =>
        {
            var h = Dm.@new<obj_item_holochip>(c.Bob.loc, restock * 2);
            Waiting.NoSleep(c.Bob.put_in_active_hand(h));
            return h;
        });
        c.R.In(() => Waiting.NoSleep(pad.item_interaction(c.Bob, chip)));
        Assert.Equal(restock, c.R.In(() => chip.credits));
        Assert.True(c.Feed.Any("Markets restocked!"), CargoEconomyTests.Diag(c));
        // the second restock costs twice as much
        c.R.In(() => Waiting.NoSleep(pad.item_interaction(c.Bob, chip)));
        Assert.Equal(restock, c.R.In(() => chip.credits));
        Assert.True(c.Feed.Any("Insufficient credits!"), CargoEconomyTests.Diag(c));
        c.R.AssertNoRuntimes();
    }

    // The stale-stub sweep after the economy and cargo (gaps P4d)

    [Fact]
    public void A_forced_fax_to_an_area_without_a_fax_machine_drops_one_in_a_supply_pod()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9811);
        HoldingArea(c);
        // the shuttle's iron floors are an /area/shuttle/supply: no fax machine there
        var paper = c.R.In(() => Dm.@new<obj_item_paper>(c.Bob.loc));
        object sent = c.R.In(() => Waiting.NoSleep(send_fax_to_area(paper, typeof(area_shuttle_supply), "Central Command", TRUE)));
        Assert.Equal(TRUE, sent);
        var zone = Assert.Single(InRoom<obj_effect_pod_landingzone>(c));
        Assert.True(c.R.In(() => get_area(zone) is area_shuttle_supply), "the pod lands in the area");
        Assert.True(c.R.In(() => Dm.each<obj_machinery_fax>(zone.pod.contents).Any()), "the pod carries a fax machine");
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_captured_crewman_goes_on_the_black_market_until_he_comes_home()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9812);
        c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(5000));
        float budget = Budget(c);
        var item = c.R.In(() => Waiting.NoSleep(c.Bob.process_capture(1000f, 3000f)) as datum_market_item_hostage);
        Assert.NotNull(item);
        Assert.Equal(budget - 1000, Budget(c)); // the ransom
        var blackmarket = c.R.In(() => (datum_market)SSmarket.markets[typeof(datum_market_blackmarket)]);
        Assert.Contains("Hostages", c.R.In(() => Dm.each(blackmarket.categories).Select(Dm.str).ToList()));
        Assert.Equal(c.R.In(() => Dm.str(c.Bob.real_name)), c.R.In(() => Dm.str(item.name)));
        Assert.Equal(3000f, c.R.In(() => Dm.num(item.price)));
        Assert.Contains("DISCLAIMER", c.R.In(() => Dm.str(item.desc)));
        Assert.True(c.R.In(() => HAS_TRAIT(c.Bob.mind, TRAIT_HAS_BEEN_KIDNAPPED)), "the mind remembers the kidnapping");
        // returned home by the contract, the offer expires
        c.R.In(() => c.Bob.SEND_SIGNAL(COMSIG_LIVING_RETURN_FROM_CAPTURE, Dm.list(c.Bob, c.Bob.loc)));
        Assert.True(c.R.In(() => QDELETED(item)), "the offer is gone");
        Assert.DoesNotContain("Hostages", c.R.In(() => Dm.each(blackmarket.categories).Select(Dm.str).ToList()));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_round_end_report_has_the_station_economic_summary()
    {
        CargoEconomyTests.Cargo c = CargoEconomyTests.Room(9813);
        VendingTests.Funds(c.R, c.Bob, 99999);
        string report = c.R.In(() => SSticker.market_report());
        Assert.Contains("Station Economic Summary:", report);
        Assert.Contains("Service did not earn any credits...", report); // no restaurant venues
        Assert.Contains("The most affluent crew member at shift end was <b>" + c.R.In(() => Dm.str(c.Bob.real_name)), report);
        Assert.Contains("pieces of mail", report);
        Assert.Contains("Station Economic Summary:", c.R.In(() => Dm.str(SSticker.build_roundend_report())));
        c.R.AssertNoRuntimes();
    }
}
