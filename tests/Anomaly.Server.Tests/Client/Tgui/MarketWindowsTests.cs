using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps P4d (D298): the client's windows of the rest of cargo without the engine: the express console (its lock, landing location
/// and express catalog), the materials market (prices, stock, the buy buttons and the crash notice), the produce console (shopping,
/// checkout with the free and paid rows, the order-sent dimmer), the department orders app (unlinked, cooldown, the catalog's
/// cooldown estimates), the shipping app, the black market uplink with its shipment selector, and the LTSRBT.
/// </summary>
public class MarketWindowsTests
{
    private static (UiNode Tree, TguiContext Context, List<(string Action, string Params)> Acts) Render(Func<TguiContext, UiNode> render, string name, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (render(c), c, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static UiNode Find(UiNode tree, string key) => tree.Walk().FirstOrDefault(n => n.Key == key);

    private const string ExpressJson = "{\"locked\":0,\"points\":5000,\"usingBeacon\":0,\"using_beacon\":0,\"beaconzone\":\"Cargo Bay\",\"beaconName\":\"Supply Pod Beacon #1\","
        + "\"canBuyBeacon\":1,\"hasBeacon\":1,\"canBeacon\":1,\"beaconError\":0,\"printMsg\":\"Print Beacon for 500 credits (5)\",\"message\":\"Sales are near-instantaneous - please choose carefully.\","
        + "\"self_paid\":0,\"max_order\":20,\"cart\":[],\"requests\":[],\"displayed_currency_full_name\":\" credits\",\"displayed_currency_name\":\" cr\","
        + "\"supplies\":{\"Emergency\":{\"name\":\"Emergency\",\"packs\":["
        + "{\"name\":\"Internals Crate\",\"cost\":400,\"id\":\"/datum/supply_pack/emergency/internals\",\"desc\":\"Air.\",\"goody\":0,\"access\":0,\"contraband\":0,\"contains\":[]}]}}}";

    [Fact]
    public void TheExpressConsoleChoosesItsLandingAndBuysAPod()
    {
        var (tree, context, acts) = Render(CargoExpress.Render, "CargoExpress", ExpressJson);
        Assert.Equal((600.0, 700.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("5000 credits", ByKey(tree, "points").Text);
        Assert.Equal("blue", ByKey(tree, "message").Color);
        Assert.True(ByKey(tree, "cargoBay").Selected);
        Assert.Equal(("Supply Pod Beacon #1", "Cargo Bay"), (ByKey(tree, "beacon").Text, ByKey(tree, "beacon").Tooltip));
        ByKey(tree, "beacon").OnClick();
        ByKey(tree, "printBeacon").OnClick();
        ByKey(tree, "pack:/datum/supply_pack/emergency/internals").OnClick();
        Assert.Equal(new (string, string)[] { ("LZBeacon", null), ("printBeacon", null), ("add", "{\"id\":\"/datum/supply_pack/emergency/internals\"}") }, acts);
        Assert.Null(Find(tree, "toggleprivate")); // the express catalog has no private buying

        // a beacon error turns the notice red; a locked console shows only its lock
        var (error, _, _) = Render(CargoExpress.Render, "CargoExpress", ExpressJson.Replace("\"beaconError\":0", "\"beaconError\":1"));
        Assert.Equal("red", ByKey(error, "message").Color);
        var (locked, _, _) = Render(CargoExpress.Render, "CargoExpress", ExpressJson.Replace("\"locked\":0", "\"locked\":1"));
        Assert.Equal("Swipe a Cargo Technician-level ID card to unlock this interface.", ByKey(locked, "lockText").Text);
        Assert.Null(Find(locked, "cargoBay"));
    }

    private const string MarketJson = "{\"orderingPrive\":1,\"canOrderCargo\":0,\"creditBalance\":1000,\"orderBalance\":0,\"CARGO_CRATE_VALUE\":200,\"updateTime\":200,\"catastrophe\":0,"
        + "\"materials\":["
        + "{\"name\":\"plasma\",\"price\":300,\"rarity\":200,\"min_threshold\":100,\"max_threshold\":900,\"quantity\":10,\"requested\":0,\"elastic\":90,\"trend\":\"up\",\"color\":\"#ba3692\"},"
        + "{\"name\":\"iron\",\"price\":5,\"rarity\":100,\"min_threshold\":2,\"max_threshold\":20,\"quantity\":50,\"requested\":5,\"elastic\":20,\"trend\":\"down\",\"color\":\"\"}]}";

    [Fact]
    public void TheMaterialsMarketShowsPricesStockAndBuys()
    {
        var (tree, context, acts) = Render(MatMarket.Render, "MatMarket", MarketJson);
        Assert.Equal((990.0, 615.0), (context.WindowWidth, context.WindowHeight));
        Assert.Null(Find(tree, "toggle")); // no cargo access: no budget toggle
        Assert.Equal("Balance: " + TguiFormat.FormatMoney(1000) + " cr.", ByKey(tree, "balance").Text);
        Assert.Equal("20 seconds until next update", ByKey(tree, "update").Text);
        // the rarer material last; iron's numbers and colours
        UiNode iron = ByKey(tree, "material:0");
        Assert.Equal("Iron", ByKey(iron, "name").Text);
        Assert.Equal(("Elasticity: 20%", "red"), (ByKey(iron, "elastic").Text, ByKey(iron, "elastic").Color));
        Assert.Equal("50 sheets of iron trading.", ByKey(iron, "trading").Text);
        Assert.Equal(("5 sheets ordered.", "lightblue"), (ByKey(iron, "ordered").Text, ByKey(iron, "ordered").Color));
        Assert.Equal(("Iron is trending down.", "red"), (ByKey(iron, "trend").Text, ByKey(iron, "trend").Color));
        // a private order is a tenth dearer and the first carries the crate: 1000 - 200 < 300 * 5 * 1.1
        UiNode plasma = ByKey(tree, "material:1");
        Assert.False(ByKey(plasma, "buy:1").Disabled);
        Assert.True(ByKey(plasma, "buy:5").Disabled);
        ByKey(iron, "buy:10").OnClick();
        Assert.Equal(("buy", "{\"quantity\":10,\"material\":\"iron\"}"), Assert.Single(acts));

        // with cargo access the budget toggle; a crash covers the market
        var (cargo, _, cargoActs) = Render(MatMarket.Render, "MatMarket", MarketJson.Replace("\"canOrderCargo\":0", "\"canOrderCargo\":1").Replace("\"catastrophe\":0", "\"catastrophe\":1"));
        Assert.Equal("Order via Cargo Budget?", ByKey(cargo, "toggle").Text);
        ByKey(cargo, "toggle").OnClick();
        Assert.Equal(("toggle_budget", null), Assert.Single(cargoActs));
        Assert.Equal("ATTENTION! THE MARKET HAS CRASHED", ByKey(cargo, "l1").Text);
        Assert.True(ByKey(ByKey(cargo, "material:0"), "buy:1").Disabled);
    }

    private const string ProduceJson = "{\"total_cost\":120,\"free_uses\":3,\"off_cooldown\":1,\"item_amts\":[{\"name\":\"Potato\",\"amt\":5},{\"name\":\"Milk\",\"amt\":1}],\"points\":800,"
        + "\"credit_type\":\"credit\",\"express_tooltip\":\"Sends instantly.\",\"purchase_tooltip\":\"Next shuttle.\",\"forced_express\":0,\"cargo_value\":200,\"cargo_cost_multiplier\":0.65,\"express_cost_multiplier\":2,"
        + "\"order_categories\":[\"Veggies\",\"Milk & Eggs\",\"Reagents\"],\"order_datums\":["
        + "{\"name\":\"Potato\",\"desc\":\"A potato.\",\"cat\":\"Veggies\",\"ref\":\"[0x1]\",\"cost\":7,\"icon\":\"icons/obj/service/hydroponics/harvest.dmi\",\"icon_state\":\"potato\",\"max\":50},"
        + "{\"name\":\"Tomato\",\"desc\":\"A tomato.\",\"cat\":\"Veggies\",\"ref\":\"[0x2]\",\"cost\":7,\"icon\":\"icons/obj/service/hydroponics/harvest.dmi\",\"icon_state\":\"tomato\",\"max\":50},"
        + "{\"name\":\"Milk\",\"desc\":\"Milk.\",\"cat\":\"Milk & Eggs\",\"ref\":\"[0x3]\",\"cost\":20,\"icon\":\"icons/obj/food/containers.dmi\",\"icon_state\":\"milk\",\"max\":20}]}";

    [Fact]
    public void TheProduceConsoleShopsSearchesAndChecksOut()
    {
        var local = new TguiLocalState();
        var (tree, context, acts) = Render(ProduceConsole.Render, "ProduceConsole", ProduceJson, local);
        Assert.Equal((500.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("~ Free Products: 0 / 3", ByKey(tree, "free").Text);
        Assert.Equal("coins", ByKey(ByKey(tree, "points"), "coins").Icon);
        // the first category's goods with their pictures, minus, plus and amount
        UiNode potato = ByKey(tree, "item:[0x1]");
        Assert.Equal(("icons/obj/service/hydroponics/harvest.dmi", "potato"), (ByKey(potato, "picture").DmIcon, ByKey(potato, "picture").DmIconState));
        Assert.Equal("Costs 7 credit per order", ByKey(potato, "cost").Tooltip);
        Assert.Equal(5, ByKey(potato, "count").Value);
        Assert.Null(Find(tree, "item:[0x3]"));
        ByKey(potato, "plus").OnClick();
        ByKey(potato, "count").OnChange(9);
        Assert.Equal(new (string, string)[] { ("add_one", "{\"target\":\"[0x1]\"}"), ("cart_set", "{\"target\":\"[0x1]\",\"amt\":9}") }, acts);

        // condensed: no pictures; a search crosses the categories
        ByKey(tree, "condense").OnClick();
        ByKey(tree, "search").OnInput("mil");
        var (searched, _, _) = Render(ProduceConsole.Render, "ProduceConsole", ProduceJson, local);
        Assert.Equal("Expand", ByKey(searched, "condense").Text);
        Assert.Null(Find(ByKey(searched, "item:[0x3]"), "picture"));
        Assert.Null(Find(searched, "item:[0x1]"));

        // checkout: the potatoes split into three free and two paid, milk paid; the purchase total under the crate value
        ByKey(searched, "checkout").OnClick();
        var (checkout, _, checkoutActs) = Render(ProduceConsole.Render, "ProduceConsole", ProduceJson, local);
        UiNode freeRow = ByKey(checkout, "row:0:[0x1]");
        Assert.Equal("Free", ByKey(freeRow, "free").Text);
        Assert.Equal((3.0, 3.0), (ByKey(freeRow, "count").Value, ByKey(freeRow, "count").MaxValue));
        UiNode paidRow = ByKey(checkout, "row:1:[0x1]");
        Assert.Equal((2.0, 47.0), (ByKey(paidRow, "count").Value, ByKey(paidRow, "count").MaxValue));
        Assert.Equal("\"Milk.\"", ByKey(ByKey(checkout, "row:2:[0x3]"), "desc").Text);
        ByKey(paidRow, "count").OnChange(4);
        UiNode purchase = ByKey(checkout, "purchase");
        Assert.Equal(("Purchase: 78 cr", true, "Total must be above or equal to 200"), (purchase.Text, purchase.Disabled, purchase.Tooltip));
        UiNode express = ByKey(checkout, "express");
        Assert.Equal(("Express: 240 cr", false, "Sends instantly."), (express.Text, express.Disabled, express.Tooltip));
        express.OnClick();
        Assert.Equal(new (string, string)[] { ("cart_set", "{\"target\":\"[0x1]\",\"amt\":7}"), ("express", null) }, checkoutActs);

        // on cooldown the order-sent dimmer; forced express hides the purchase button
        var (cooling, _, _) = Render(ProduceConsole.Render, "ProduceConsole", ProduceJson.Replace("\"off_cooldown\":1", "\"off_cooldown\":0").Replace("\"forced_express\":0", "\"forced_express\":1"), local);
        Assert.Equal("Order sent! Machine on cooldown...", ByKey(cooling, "text").Text);
        Assert.Null(Find(cooling, "purchase"));
    }

    [Fact]
    public void AnEmptyCheckoutAsksForAnOrderAndMiningPointsAreText()
    {
        var local = new TguiLocalState();
        string json = ProduceJson.Replace("\"item_amts\":[{\"name\":\"Potato\",\"amt\":5},{\"name\":\"Milk\",\"amt\":1}]", "\"item_amts\":[]")
            .Replace("\"credit_type\":\"credit\"", "\"credit_type\":\"mp\"").Replace("\"free_uses\":3", "\"free_uses\":null");
        var (tree, _, _) = Render(ProduceConsole.Render, "ProduceConsole", json, local);
        Assert.Null(Find(tree, "free"));
        Assert.Equal("mp", ByKey(ByKey(tree, "points"), "credit").Text);
        ByKey(tree, "checkout").OnClick();
        var (checkout, _, _) = Render(ProduceConsole.Render, "ProduceConsole", json, local);
        Assert.Equal("Nothing!", ByKey(checkout, "empty").Text);
        Assert.Equal(("Express: 240 mp", true, "Order at least 1 item"), (ByKey(checkout, "express").Text, ByKey(checkout, "express").Disabled, ByKey(checkout, "express").Tooltip));
    }

    private const string DeptJson = "{\"no_link\":0,\"id_inside\":0,\"time_left\":null,\"can_override\":0,\"PC_device_theme\":\"ntos\","
        + "\"supplies\":[{\"name\":\"Service\",\"packs\":[{\"name\":\"Janitorial Supplies\",\"cost\":1300,\"id\":\"/datum/supply_pack/service/janitor\",\"desc\":\"Mops.\"}]},"
        + "{\"name\":\"Food & Hydroponics\",\"packs\":[{\"name\":\"Food Crate\",\"cost\":300,\"id\":\"/datum/supply_pack/organic/food\",\"desc\":\"Food.\"},{\"name\":\"Big Crate\",\"cost\":9000,\"id\":\"/datum/supply_pack/organic/big\",\"desc\":\"Big.\"}]}]}";

    [Fact]
    public void TheDepartmentOrderAppOrdersLinksAndOverrides()
    {
        var local = new TguiLocalState();
        var (tree, _, acts) = Render(NtosDeptOrder.Render, "NtosDeptOrder", DeptJson, local);
        UiNode janitor = ByKey(tree, "pack:Janitorial Supplies");
        Assert.Equal(("moderate cooldown.", "yellow"), (ByKey(janitor, "estimate").Text, ByKey(janitor, "estimate").Color));
        ByKey(janitor, "orderButton").OnClick();
        Assert.Equal(("order", "{\"id\":\"/datum/supply_pack/service/janitor\"}"), Assert.Single(acts));
        ByKey(tree, "tab:Food & Hydroponics").OnClick();
        var (food, _, _) = Render(NtosDeptOrder.Render, "NtosDeptOrder", DeptJson, local);
        Assert.Equal("short cooldown.", ByKey(ByKey(food, "pack:Food Crate"), "estimate").Text);
        Assert.Equal(("very long cooldown.", "red"), (ByKey(ByKey(food, "pack:Big Crate"), "estimate").Text, ByKey(ByKey(food, "pack:Big Crate"), "estimate").Color));

        // unlinked: the link button waits for an ID; cooling down: the override
        var (unlinked, _, linkActs) = Render(NtosDeptOrder.Render, "NtosDeptOrder", DeptJson.Replace("\"no_link\":0", "\"no_link\":1").Replace("\"id_inside\":0", "\"id_inside\":1"));
        ByKey(unlinked, "link").OnClick();
        Assert.Equal(("link", null), Assert.Single(linkActs));
        var (cooling, _, overrideActs) = Render(NtosDeptOrder.Render, "NtosDeptOrder", DeptJson.Replace("\"time_left\":null", "\"time_left\":\"4 minutes\"").Replace("\"can_override\":0", "\"can_override\":1"));
        Assert.Equal("Ready for another order in 4 minutes...", ByKey(cooling, "timeLeft").Text);
        Assert.Null(Find(cooling, "pack:Janitorial Supplies"));
        ByKey(cooling, "override").OnClick();
        Assert.Equal(("override_order", null), Assert.Single(overrideActs));
        var (shipped, _, _) = Render(NtosDeptOrder.Render, "NtosDeptOrder", DeptJson.Replace("\"time_left\":null", "\"time_left\":\"4 minutes\""));
        Assert.Equal((true, "Crate already shipped! No cancelling now!"), (ByKey(shipped, "override").Disabled, ByKey(shipped, "override").Tooltip));
    }

    [Fact]
    public void TheShippingAppShowsTheUserAndPrints()
    {
        string json = "{\"has_id_slot\":1,\"paperamt\":\"4 / 10\",\"card_owner\":\"Bob's ID Card\",\"current_user\":\"Bob\",\"barcode_split\":20,\"PC_device_theme\":\"ntos\"}";
        var (tree, _, acts) = Render(NtosShipping.Render, "NtosShipping", json);
        Assert.Equal(("Bob", "Bob's ID Card", "4 / 10", "20%"), (ByKey(tree, "user").Text, ByKey(tree, "card").Text, ByKey(tree, "paper").Text, ByKey(tree, "split").Text));
        ByKey(tree, "selectid").OnClick();
        ByKey(tree, "print").OnClick();
        ByKey(tree, "setsplit").OnClick();
        Assert.Equal(new (string, string)[] { ("selectid", null), ("print", null), ("setsplit", null) }, acts);
        var (nobody, _, _) = Render(NtosShipping.Render, "NtosShipping", json.Replace("\"current_user\":\"Bob\"", "\"current_user\":null"));
        Assert.Equal(("N/A", true), (ByKey(nobody, "user").Text, ByKey(nobody, "print").Disabled));
    }

    private const string UplinkJson = "{\"categories\":[\"Local Goods\",\"Tools\"],\"money\":150,\"buying\":0,\"delivery_methods\":[],\"viewing_category\":\"Local Goods\",\"viewing_market\":\"/datum/market/blackmarket\","
        + "\"items\":[{\"id\":\"[0x5]\",\"name\":\"lucky pen\",\"cost\":100,\"amount\":1,\"desc\":\"A pen. - Seller: Bob\",\"html_icon\":null},{\"id\":\"[0x6]\",\"name\":\"gold\",\"cost\":900,\"amount\":2,\"desc\":\"Gold.\",\"html_icon\":null}],"
        + "\"delivery_method_description\":{\"LTSRBT\":\"Long-To-Short-Range-Bluespace-Transceiver.\",\"Launch\":\"Launched at the station.\",\"Teleport\":\"Teleported somewhere.\"},"
        + "\"ltsrbt_built\":0,\"markets\":[{\"id\":\"/datum/market/blackmarket\",\"name\":\"Black Market\"}]}";

    [Fact]
    public void TheBlackMarketUplinkSelectsAndShips()
    {
        var (tree, context, acts) = Render(BlackMarketUplink.Render, "BlackMarketUplink", UplinkJson);
        Assert.Equal((670.0, 480.0, "hackerman"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));
        Assert.Equal("150 cr", ByKey(tree, "money").Text);
        Assert.True(ByKey(tree, "category:Local Goods").Selected);
        UiNode pen = ByKey(tree, "item:lucky pen");
        Assert.Equal(("1 in stock", "100 cr"), (ByKey(pen, "stock").Text, ByKey(pen, "cost").Text));
        Assert.True(ByKey(ByKey(tree, "item:gold"), "buy").Disabled); // dearer than the money
        ByKey(pen, "buy").OnClick();
        ByKey(tree, "category:Tools").OnClick();
        Assert.Equal(new (string, string)[] { ("select", "{\"item\":\"[0x5]\"}"), ("set_category", "{\"category\":\"Tools\"}") }, acts);

        // buying: the shipment selector, the LTSRBT only when one is built
        string buying = UplinkJson.Replace("\"buying\":0", "\"buying\":1").Replace("\"delivery_methods\":[]", "\"delivery_methods\":[{\"name\":\"LTSRBT\",\"price\":40},{\"name\":\"Launch\",\"price\":0},{\"name\":\"Teleport\",\"price\":175}]");
        var (selector, _, buyActs) = Render(BlackMarketUplink.Render, "BlackMarketUplink", buying);
        Assert.Null(Find(selector, "method:LTSRBT"));
        Assert.Equal("Launched at the station.", ByKey(ByKey(selector, "method:Launch"), "description").Text);
        Assert.True(ByKey(ByKey(selector, "method:Teleport"), "buy").Disabled);
        ByKey(ByKey(selector, "method:Launch"), "buy").OnClick();
        ByKey(selector, "cancel").OnClick();
        Assert.Equal(new (string, string)[] { ("buy", "{\"method\":\"Launch\"}"), ("cancel", null) }, buyActs);
        var (built, _, _) = Render(BlackMarketUplink.Render, "BlackMarketUplink", buying.Replace("\"ltsrbt_built\":0", "\"ltsrbt_built\":1"));
        Assert.Equal("40 cr", ByKey(ByKey(built, "method:LTSRBT"), "buy").Text);

    }

    [Fact]
    public void TheLtsrbtNamesPricesAndPlacesOnTheMarket()
    {
        var local = new TguiLocalState();
        string json = "{\"name\":\"pen\",\"desc\":\"A pen.\",\"price\":100,\"min_price\":25,\"max_price\":10000,\"loaded_icon\":null}";
        var (tree, context, acts) = Render(LTSRBT.Render, "LTSRBT", json, local);
        Assert.Equal((300.0, 380.0, "hackerman"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));
        Assert.Equal(("pen", "A pen."), (ByKey(tree, "name").TextValue, ByKey(tree, "desc").TextValue));
        UiNode price = ByKey(tree, "price");
        Assert.Equal((100.0, 25.0, 10000.0, "cr"), (price.Value, price.MinValue, price.MaxValue, price.Unit));
        ByKey(tree, "name").OnCommit("lucky pen");
        price.OnChange(250);
        ByKey(tree, "desc").OnCommit("Lucky.");
        // Place on Market asks for a confirmation first
        ByKey(tree, "place").OnClick();
        var (confirm, _, _) = Render(LTSRBT.Render, "LTSRBT", json, local, acts);
        Assert.Equal("Confirm?", ByKey(confirm, "place").Text);
        ByKey(confirm, "place").OnClick();
        Assert.Equal(new (string, string)[] { ("change_name", "{\"value\":\"lucky pen\"}"), ("change_price", "{\"value\":250}"), ("change_desc", "{\"value\":\"Lucky.\"}"), ("place_on_market", null) }, acts);
    }
}
