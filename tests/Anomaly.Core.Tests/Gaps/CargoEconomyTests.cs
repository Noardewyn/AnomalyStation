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
/// Gaps package P4b (D298): cargo with the economy in a booted toy room. A supply shuttle stands in its east columns (an
/// /area/shuttle/supply over iron floor) and its buy and sell run as the shuttle's arrival and departure run them: an order is paid
/// from the cargo budget, a private one from its buyer with cargo's handling fee, goodies ship in their buyer's order case (a crate
/// with the crate tax past five), what the shuttle carries back is sold into the budget and a price tag pays its cut; the account
/// lock, the coupons, the Coupon Master, the scanner's export reading, the trade chip, the economy's mail crate and the windows.
/// </summary>
public class CargoEconomyTests
{
    internal sealed class Cargo
    {
        public AudioRoom R;
        public Feed Feed;
        public mob_living_carbon_human Bob;
        public obj_docking_port_mobile_supply Shuttle;
        public area_shuttle_supply Area;
    }

    // the shuttle's columns: x 8..10, y 2..8
    internal static Cargo Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE); // the toy room has no APC
        for (int i = 0; i < 30 && r.In(() => SSeconomy.times_fired) < 1; i++)
            r.Step(Second);
        var (shuttle, supplyArea) = r.In(() =>
        {
            var a = Dm.@new<area_shuttle_supply>();
            a.requires_power = FALSE;
            foreach (turf t in Dm.each<turf>(block(8, 2, 1, 10, 8, 1)))
            {
                t.change_area((area)t.loc, a);
                Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_iron)));
            }
            var port = Dm.@new<obj_docking_port_mobile_supply>(locate(9, 5, 1));
            port.shuttle_areas = Dm.list(Dm.kv(a, TRUE));
            SSshuttle.supply = port;
            return (port, a);
        });
        return new Cargo { R = r, Feed = feed, Bob = bob, Shuttle = shuttle, Area = supplyArea };
    }

    internal static string Diag(Cargo c) => "chat: " + string.Join(" | ", c.Feed.Chat.Select(m => m.Html)) + " runtimes: " + string.Join("\n", c.R.Room.RuntimeText);

    private static float Budget(Cargo c) => c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);

    private static datum_supply_order Order(Cargo c, Type pack, datum_bank_account paying = null) => c.R.In(() =>
    {
        var order = Dm.@new<datum_supply_order>(SSshuttle.supply_packs[pack], "Bob", "Assistant", "bob", "", paying);
        Dm.append(ref SSshuttle.shopping_list, order);
        return order;
    });

    private static List<T> OnShuttle<T>(Cargo c) where T : atom => c.R.In(() =>
        Dm.each<turf>(block(8, 2, 1, 10, 8, 1)).SelectMany(t => Dm.each<T>(t.contents)).ToList());

    [Fact]
    public void An_order_is_paid_from_the_cargo_budget_and_a_private_one_from_its_buyer()
    {
        Cargo c = Room(9601);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 5000);
        c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(10000));
        float budget = Budget(c);

        datum_supply_order cargoOrder = Order(c, typeof(datum_supply_pack_emergency_internals));
        float cargoPrice = c.R.In(() => cargoOrder.get_final_cost());
        datum_supply_order privateOrder = Order(c, typeof(datum_supply_pack_emergency_internals), mine);
        float packCost = c.R.In(() => privateOrder.pack.get_cost());
        float privatePrice = c.R.In(() => privateOrder.get_final_cost());
        Assert.Equal(round(packCost * 1.1f), privatePrice); // a private order costs a tenth more
        float idCargo = c.R.In(() => Dm.num(cargoOrder.id));
        float idPrivate = c.R.In(() => Dm.num(privateOrder.id));

        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));

        Assert.Equal(0, c.R.In(() => SSshuttle.shopping_list.len));
        // cargo paid its own order and got the handling fee of the private one
        Assert.Equal(budget - cargoPrice + (privatePrice - packCost), Budget(c));
        Assert.Equal(5000 - privatePrice, c.R.In(() => mine.account_balance));
        var crates = OnShuttle<obj_structure_closet_crate>(c);
        Assert.Equal(2, crates.Count);
        var cargoCrate = crates.Single(crate => crate.name.EndsWith("#" + idCargo));
        var privateCrate = crates.Single(crate => crate.name.EndsWith("#" + idPrivate));
        string bobName = c.R.In(() => Dm.str(c.Bob.real_name));
        Assert.Contains(" - Purchased by " + bobName, privateCrate.name);
        Assert.NotNull(c.R.In(() => privateCrate.GetComponent(typeof(datum_component_locked_to_account))));
        Assert.Null(c.R.In(() => cargoCrate.GetComponent(typeof(datum_component_locked_to_account))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("has shipped") && m.Html.Contains(privatePrice + " credits have been charged"));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void An_order_the_budget_cannot_pay_waits_on_the_list()
    {
        Cargo c = Room(9602);
        datum_bank_account poor = VendingTests.Funds(c.R, c.Bob, 1);
        datum_supply_order order = Order(c, typeof(datum_supply_pack_emergency_internals), poor);
        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));
        Assert.Equal(1, c.R.In(() => SSshuttle.shopping_list.len)); // can_be_cancelled: the player cancels it, not the shuttle
        Assert.Equal(1f, c.R.In(() => poor.account_balance));
        Assert.Empty(OnShuttle<obj_structure_closet_crate>(c));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("rejected due to lack of funds"));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void Goodies_ship_in_their_buyers_case_and_a_sixth_pays_the_crate_tax()
    {
        Cargo c = Room(9603);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 20000);
        Type goody = typeof(datum_supply_pack_goody_medipen_twopak);
        float goodyPrice = c.R.In(() => round(((datum_supply_pack)SSshuttle.supply_packs[goody]).get_cost()));

        // one goody: a free shipping case only Bob's card opens
        Order(c, goody, mine);
        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));
        var cases = OnShuttle<obj_item_storage_lockbox_order>(c);
        Assert.Single(cases);
        string bobName = c.R.In(() => Dm.str(c.Bob.real_name));
        Assert.Equal("goody case - purchased by " + bobName, cases[0].name);
        Assert.Same(mine, c.R.In(() => cases[0].buyer_account));
        Assert.Equal(20000 - goodyPrice, c.R.In(() => mine.account_balance)); // a goody costs no private markup
        Assert.True(c.R.In(() => Dm.each<obj_item_reagent_containers_hypospray_medipen>(cases[0].contents).Any()), "the case holds the goody");
        var card = c.R.In(() => (obj_item_card_id)c.Bob.wear_id);
        Assert.True(c.R.In(() => Dm.truthy(cases[0].can_unlock(c.Bob, card, 1))));
        var other = c.R.In(() =>
        {
            var o = Dm.@new<obj_item_card_id_advanced>(c.Bob.loc);
            o.set_account(Dm.@new<datum_bank_account>("Eve", SSjob.get_job_type(typeof(datum_job_assistant)), 1, TRUE));
            return o;
        });
        Assert.False(c.R.In(() => Dm.truthy(cases[0].can_unlock(c.Bob, other, 1))));

        // six goodies: past GOODY_FREE_SHIPPING_MAX the sixth pays the crate tax and they ship in an account-locked crate
        c.R.In(() => qdel(cases[0]));
        float before = c.R.In(() => mine.account_balance);
        for (int i = 0; i < 6; i++)
            Order(c, goody, mine);
        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));
        Assert.Equal(before - 6 * goodyPrice - 700, c.R.In(() => mine.account_balance));
        var crate = OnShuttle<obj_structure_closet_crate_secure>(c).Single();
        Assert.Equal("goody crate - purchased by " + bobName, crate.name);
        Assert.NotNull(c.R.In(() => crate.GetComponent(typeof(datum_component_locked_to_account))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Goody order size exceeds free shipping limit"));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_account_lock_opens_only_for_the_buyer()
    {
        Cargo c = Room(9604);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 0);
        mob_living_carbon_human eve = c.R.Join(5, 4, "Eve");
        VendingTests.Funds(c.R, eve, 0);
        var crate = c.R.In(() =>
        {
            var k = Dm.@new<obj_structure_closet_crate>(locate(5, 5, 1));
            Waiting.NoSleep(k.AddComponent(typeof(datum_component_locked_to_account), mine));
            return k;
        });
        Assert.False(c.R.In(() => Dm.truthy(Waiting.NoSleep(crate.open(eve)))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Bank account does not match with buyer!"));
        Assert.Contains(c.R.Sounds.Heard, h => h.File == "sound/machines/buzz/buzz-two.ogg");
        Assert.True(c.R.In(() => Dm.truthy(Waiting.NoSleep(crate.open(c.Bob)))));
        Assert.Null(c.R.In(() => crate.GetComponent(typeof(datum_component_locked_to_account))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("account lock disengages with a soft click"));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_shuttle_sells_what_it_carries_and_a_price_tag_pays_its_cut()
    {
        Cargo c = Room(9605);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 0);
        float budget = Budget(c);
        var (sheets, tagged) = c.R.In(() =>
        {
            // iron's and glass's market prices back at their baselines (5 a sheet): tg lets both fall to 0 (minimum_value_override),
            // and a worthless stack is not exported, so a downward trend (the market's RNG) would drop iron from the sale
            SSstock_market.materials_prices[typeof(datum_material_iron)] = 5;
            SSstock_market.materials_prices[typeof(datum_material_glass)] = 5;
            var s = Dm.@new<obj_item_stack_sheet_iron>(locate(9, 3, 1), 50);
            var t = Dm.@new<obj_item_stack_sheet_glass>(locate(9, 4, 1), 50);
            Waiting.NoSleep(t.AddComponent(typeof(datum_component_pricetag), Dm.list(mine), 0.5f));
            return (s, t);
        });
        c.R.In(() => Waiting.NoSleep(c.Shuttle.sell()));
        Assert.True(c.R.In(() => QDELETED(sheets) && QDELETED(tagged)), "the sold sheets are gone");
        float earned = Budget(c) - budget;
        Assert.True(earned > 0, Diag(c));
        Assert.True(c.R.In(() => mine.account_balance) > 0, "the price tag paid its cut: " + Diag(c));
        string message = c.R.In(() => SSshuttle.centcom_message);
        Assert.Contains("iron", message);
        Assert.Contains("glass", message);
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Sale of") && m.Html.Contains("recorded"));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_coupon_takes_its_discount_off_and_comes_back_when_the_order_is_cancelled()
    {
        Cargo c = Room(9606);
        var console = Place<obj_machinery_computer_cargo>(c.R, 4, 5);
        c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(100000));
        // the gimmick assistant's bee coupon: its pack and discount are set, so its Initialize names it
        var coupon = c.R.In(() => Dm.@new<obj_item_coupon_bee>(c.Bob.loc));
        Type bees = typeof(datum_supply_pack_organic_hydroponics_beekeeping_fullkit);
        string packName = c.R.In(() => ((datum_supply_pack)SSshuttle.supply_packs[bees]).name);
        Assert.Equal("coupon - 70% off " + packName, c.R.In(() => coupon.name));
        c.R.In(() => Waiting.NoSleep(coupon.attack_atom(console, c.Bob)));
        Assert.Same(console, c.R.In(() => coupon.loc));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Coupon for " + packName + " applied!"));

        // an order of the pack takes the coupon
        Assert.Equal(TRUE, c.R.In(() => Waiting.NoSleep(console.add_item(c.Bob, Dm.text("[]", bees)))));
        var order = c.R.In(() => (datum_supply_order)SSshuttle.shopping_list[1]);
        Assert.Same(coupon, c.R.In(() => order.applied_coupon));
        float full = c.R.In(() => order.pack.get_cost());
        Assert.Equal(round(full * (1 - 0.7f)), c.R.In(() => order.get_final_cost()));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Coupon found! 70% off applied!"));

        // cancelled: the coupon drops at the console
        Assert.True(c.R.In(() => Waiting.NoSleep(console.remove_item(order.id))));
        Assert.Same(c.R.In(() => get_turf(console)), c.R.In(() => coupon.loc)); // DV408: kept, where tg deletes it with the order
        Assert.False(c.R.In(() => QDELETED(coupon)));

        // a coupon code's printed coupon (tg names it only when its pack comes on Initialize: it stays "coupon")
        var code = c.R.In(() => Dm.@new<datum_coupon_code>(0.25f, bees, 0));
        var printed = c.R.In(() => code.generate());
        Assert.Equal(0.25f, c.R.In(() => Dm.num(printed.discount_pct_off)));
        Assert.Equal(bees, c.R.In(() => printed.discounted_pack));
        Assert.Equal(TRUE, c.R.In(() => code.printed));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_goody_needs_a_private_purchase_and_a_private_order_charges_on_delivery()
    {
        Cargo c = Room(9607);
        var console = Place<obj_machinery_computer_cargo>(c.R, 4, 5);
        Type goody = typeof(datum_supply_pack_goody_medipen_twopak);
        Assert.Null(c.R.In(() => Waiting.NoSleep(console.add_item(c.Bob, Dm.text("[]", goody)))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Small crates may only be purchased by private accounts."));
        Assert.Equal(0, c.R.In(() => SSshuttle.shopping_list.len));

        c.R.In(() => console.self_paid = TRUE);
        Assert.Null(c.R.In(() => Waiting.NoSleep(console.add_item(c.Bob, Dm.text("[]", goody)))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("No ID card detected."));
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 100);
        Assert.Equal(TRUE, c.R.In(() => Waiting.NoSleep(console.add_item(c.Bob, Dm.text("[]", goody)))));
        Assert.Same(mine, c.R.In(() => ((datum_supply_order)SSshuttle.shopping_list[1]).paying_account));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Order processed. The price will be charged to " + c.R.In(() => Dm.str(c.Bob.real_name))));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_supply_console_window_shows_the_budget_and_toggles_private_buying()
    {
        Cargo c = Room(9608);
        var console = Place<obj_machinery_computer_cargo>(c.R, 4, 5);
        Take(c.Bob);
        Click(c.R, c.Bob, console);
        JsonElement payload = LastUpdate(Take(c.Bob), out string window);
        Assert.Equal("Cargo", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(Budget(c), (float)data.GetProperty("points").GetDouble());
        Assert.Equal(0, data.GetProperty("self_paid").GetDouble());
        Assert.Equal(" credits", payload.GetProperty("static_data").GetProperty("displayed_currency_full_name").GetString());
        Act(c.R, c.Bob, window, "toggleprivate");
        Assert.Equal(TRUE, c.R.In(() => console.self_paid));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_scanner_reads_the_export_value()
    {
        Cargo c = Room(9609);
        var (scanner, sheets) = c.R.In(() =>
        {
            var s = Dm.@new<obj_item_universal_scanner>(c.Bob.loc);
            var st = Dm.@new<obj_item_stack_sheet_iron>(locate(5, 4, 1), 10);
            return (s, st);
        });
        c.R.In(() => Waiting.NoSleep(scanner.export_scan(sheets, c.Bob)));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Scanned") && m.Html.Contains("value: <b>") && m.Html.Contains("credits"));
        Assert.False(c.R.In(() => QDELETED(sheets)), "a dry run sells nothing");
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_economy_mail_crate_takes_the_waiting_mail()
    {
        Cargo c = Room(9610);
        c.R.In(() => SSeconomy.mail_waiting = 3);
        var crate = c.R.In(() => Dm.@new<obj_structure_closet_crate_mail_economy>(locate(5, 5, 1)));
        Assert.Equal(0f, c.R.In(() => SSeconomy.mail_waiting));
        Assert.Equal(3, c.R.In(() => Dm.each<obj_item_mail>(crate.contents).Count()));

        // the shuttle's create_mail brings one when mail waits
        c.R.In(() => SSeconomy.mail_waiting = 2);
        c.R.In(() => Waiting.NoSleep(c.Shuttle.create_mail()));
        Assert.Single(OnShuttle<obj_structure_closet_crate_mail_economy>(c));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_trade_chip_unlocks_its_special_pack()
    {
        Cargo c = Room(9611);
        var console = Place<obj_machinery_computer_cargo>(c.R, 4, 5);
        Type special = c.R.In(() => Dm.each(SSshuttle.supply_packs).Cast<object>()
            .Select(key => (datum_supply_pack)SSshuttle.supply_packs[key])
            .First(p => Dm.truthy(Dm.bitand(p.order_flags, ORDER_SPECIAL))).GetType());
        var chip = c.R.In(() => Dm.@new<obj_item_trade_chip>(c.Bob.loc, special));
        Assert.Contains("trade contract chip- ", c.R.In(() => chip.name));
        Assert.Equal(ITEM_INTERACT_SUCCESS, c.R.In(() => Waiting.NoSleep(console.item_interaction(c.Bob, chip))));
        Assert.True(c.R.In(() => Dm.truthy(Dm.bitand(((datum_supply_pack)SSshuttle.supply_packs[special]).order_flags, ORDER_SPECIAL_ENABLED))));
        Assert.True(c.R.In(() => QDELETED(chip)));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Contract accepted into nanotrasen supply database."));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_materials_order_is_cut_to_what_the_market_holds()
    {
        Cargo c = Room(9612);
        c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(100000));
        var (order, pack) = c.R.In(() =>
        {
            Type iron = typeof(datum_material_iron);
            SSstock_market.materials_quantity[iron] = 10f;
            var p = Dm.@new<datum_supply_pack_custom_minerals>("Bob", 100, Dm.list(Dm.kv(typeof(obj_item_stack_sheet_iron), 30f)));
            var o = Dm.@new<datum_supply_order>(p, "Bob", "Assistant", "bob", "");
            Dm.append(ref SSshuttle.shopping_list, o);
            return (o, p);
        });
        c.R.In(() => Waiting.NoSleep(c.Shuttle.buy()));
        Assert.Equal(10f, c.R.In(() => Dm.num(pack.contains[typeof(obj_item_stack_sheet_iron)])));
        Assert.Equal(0f, c.R.In(() => Dm.num(SSstock_market.materials_quantity[typeof(datum_material_iron)])));
        var crate = OnShuttle<obj_structure_closet_crate>(c).Single();
        Assert.Equal(10, c.R.In(() => Dm.each<obj_item_stack_sheet_iron>(crate.contents).Sum(s => (int)s.amount)));
        Assert.Equal("Bob's Materials Order", c.R.In(() => pack.name));
        c.R.AssertNoRuntimes();
    }

    private static (obj_item_modular_computer_pda Pda, T App) Pda<T>(Cargo c) where T : datum_computer_file_program, new() => c.R.In(() =>
    {
        var pda = Dm.@new<obj_item_modular_computer_pda>(c.Bob.loc);
        Waiting.NoSleep(c.Bob.put_in_active_hand(pda));
        Waiting.NoSleep(pda.insert_id(c.Bob.wear_id as obj_item_card_id, c.Bob));
        var app = Dm.@new<T>();
        pda.store_file(app);
        return (pda, app);
    });

    private static object AppAct(Cargo c, datum_computer_file_program app, string action, DmList @params = null)
    {
        var ui = c.R.In(() => Dm.@new<datum_tgui>(c.Bob, app, app.tgui_id, app.filedesc));
        return c.R.In(() => Waiting.NoSleep(app.ui_act(action, @params ?? Dm.list(), ui, null)));
    }

    [Fact]
    public void The_shopping_app_buys_privately_and_approves_requests_with_a_budget_card()
    {
        Cargo c = Room(9613);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 1000);
        var (pda, app) = Pda<datum_computer_file_program_budgetorders>(c);

        // a crewman's card requests for its department
        DmList data = c.R.In(() => Waiting.NoSleep(app.ui_data(c.Bob)) as DmList);
        Assert.Equal(TRUE, c.R.In(() => Dm.num(data["requestonly"])));
        Assert.Equal(c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CIV).account_balance), c.R.In(() => Dm.num(data["points"])));
        Assert.True(c.R.In(() => ((DmList)data["supplies"]).len) > 5);

        // bought privately: the order goes straight to the cart and a requisition form comes out
        AppAct(c, app, "toggleprivate");
        Assert.Equal(TRUE, c.R.In(() => app.self_paid));
        float paper = c.R.In(() => pda.stored_paper ?? 0);
        string id = c.R.In(() => Dm.text("[]", typeof(datum_supply_pack_emergency_internals)));
        Assert.Equal(TRUE, c.R.In(() => Dm.num(AppAct(c, app, "add", c.R.In(() => Dm.list(Dm.kv("id", id)))))));
        var order = c.R.In(() => (datum_supply_order)SSshuttle.shopping_list[1]);
        Assert.Same(mine, c.R.In(() => order.paying_account));
        Assert.Equal(paper - 1, c.R.In(() => pda.stored_paper ?? 0));
        Assert.True(c.R.In(() => Dm.each<obj_item_paper_requisition>(get_turf(pda)).Any()), "the app printed the requisition form");
        Assert.Contains(c.R.Sounds.Heard, h => h.File == "sound/effects/coin2.ogg");

        // a budget card (ACCESS_BUDGET) buys for its department and approves a request with the department's budget
        c.R.In(() => Dm.append(ref pda.stored_id.access, ACCESS_BUDGET));
        data = c.R.In(() => Waiting.NoSleep(app.ui_data(c.Bob)) as DmList);
        Assert.Equal(FALSE, c.R.In(() => Dm.num(data["requestonly"])));
        Assert.Equal("Civil Budget Requisitions", c.R.In(() => Dm.str(data["department"])));
        var request = c.R.In(() =>
        {
            var o = Dm.@new<datum_supply_order>(SSshuttle.supply_packs[typeof(datum_supply_pack_emergency_internals)], "Eve", "Assistant", "eve", "air");
            Dm.append(ref SSshuttle.request_list, o);
            return o;
        });
        AppAct(c, app, "approve", c.R.In(() => Dm.list(Dm.kv("id", Dm.text("[]", request.id)))));
        Assert.Equal(0, c.R.In(() => SSshuttle.request_list.len));
        Assert.Same(c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CIV)), c.R.In(() => request.paying_account));
        Assert.True(c.R.In(() => Dm.@in(request, SSshuttle.shopping_list)));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_coupon_master_redeems_a_dropped_code_once_and_prints_it()
    {
        Cargo c = Room(9614);
        datum_bank_account mine = VendingTests.Funds(c.R, c.Bob, 0);
        var (pda, app) = Pda<datum_computer_file_program_coupon>(c);
        Assert.NotNull(c.R.In(() => SSmodular_computers.discount_coupons)); // the install starts the drops
        Assert.True(c.R.In(() => HAS_TRAIT(pda, TRAIT_MODPC_HALVED_DOWNLOAD_SPEED)));

        Type bees = typeof(datum_supply_pack_organic_hydroponics_beekeeping_fullkit);
        var code = c.R.In(() =>
        {
            var k = Dm.@new<datum_coupon_code>(0.5f, bees, 0);
            SSmodular_computers.discount_coupons["BEEKEEPING50"] = k;
            return k;
        });
        AppAct(c, app, "redeem", c.R.In(() => Dm.list(Dm.kv("code", "NOPE"))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Invalid coupon code."));
        AppAct(c, app, "redeem", c.R.In(() => Dm.list(Dm.kv("code", "BEEKEEPING50"))));
        Assert.True(c.R.In(() => Dm.@in(code, mine.redeemed_coupons)));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Coupon BEEKEEPING50 redeemed."));
        AppAct(c, app, "redeem", c.R.In(() => Dm.list(Dm.kv("code", "BEEKEEPING50"))));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Coupon BEEKEEPING50 already redeemed."));

        DmList data = c.R.In(() => Waiting.NoSleep(app.ui_data(c.Bob)) as DmList);
        var redeemed = c.R.In(() => (DmList)((DmList)data["redeemed_coupons"])[1]);
        Assert.Equal(50f, c.R.In(() => Dm.num(redeemed["discount"])));

        // the photocopier prints it: a coupon of the pack
        var coupon = c.R.In(() => app.print_coupon(mine) as obj_item_coupon);
        Assert.NotNull(coupon);
        Assert.Equal(bees, c.R.In(() => coupon.discounted_pack));
        data = c.R.In(() => Waiting.NoSleep(app.ui_data(c.Bob)) as DmList);
        Assert.Equal(1, c.R.In(() => ((DmList)data["printed_coupons"]).len));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_coupon_drop_messages_the_pdas_with_the_coupon_master()
    {
        Cargo c = Room(9615);
        VendingTests.Funds(c.R, c.Bob, 0);
        var (pda, app) = Pda<datum_computer_file_program_coupon>(c);
        c.R.In(() => pda.imprint_id("Bob Roe", "Assistant")); // the messenger lists an imprinted PDA
        c.R.In(() => Dm.@new<obj_machinery_announcement_system>(locate(2, 2, 1)));
        Assert.NotNull(c.R.In(() => get_announcement_system()));
        Assert.True(c.R.In(() => Dm.each(GLOB.pda_messengers).Cast<object>().Any(key => (GLOB.pda_messengers[key] as datum_computer_file_program_messenger)?.computer == pda)), "the PDA's messenger is listed");
        c.R.In(() => SSmodular_computers.announce_coupon());
        Assert.Equal(1, c.R.In(() => SSmodular_computers.discount_coupons.len));
        string code = c.R.In(() => Dm.str(SSmodular_computers.discount_coupons[1]));
        Assert.True(c.R.In(() => SSmodular_computers.next_discount) > c.R.In(() => (float)Dm.world.time));
        // the code is the pack's name without its "Pack"/"Crate" and other marks, and the discount in percent
        var coupon = c.R.In(() => (datum_coupon_code)SSmodular_computers.discount_coupons[code]);
        Assert.Matches("^[A-Z0-9]+$", code);
        Assert.EndsWith(c.R.In(() => Dm.text("[]", Dm.num(coupon.discount) * 100)), code);
        Assert.True(c.R.In(() => Dm.@in(coupon.discounted_pack, GLOB.discountable_packs[SUPPLY_PACK_STD_DISCOUNTABLE] as DmList)
            || Dm.@in(coupon.discounted_pack, GLOB.discountable_packs[SUPPLY_PACK_UNCOMMON_DISCOUNTABLE] as DmList)
            || Dm.@in(coupon.discounted_pack, GLOB.discountable_packs[SUPPLY_PACK_RARE_DISCOUNTABLE] as DmList)));
        // (the PDA message itself goes out over telecomms, which the toy room has none of)
        c.R.AssertNoRuntimes();
    }
}
