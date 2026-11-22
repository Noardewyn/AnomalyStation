using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package S2: the client's Vending window (Vending.tsx) without the engine. It reads tg's static data and data of a vending machine
/// (the records with their categories, the stock, the buyer's account, the ad) the way the TSX does: a free vendor shows FREE and no buyer;
/// a paying one the buyer's ID, cash and job discount, and greys out what the buyer cannot buy; the categories, the search and the layout
/// toggle choose what is shown; a product sends vend, a colourable one's palette select_colors.
/// </summary>
public class VendingWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, "Vending", "Getmore Chocolate Corp", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, Vending.Render(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static string[] Products(UiNode tree) => tree.Walk().Where(n => n.Kind == UiKind.ImageButton).Select(n => n.Key).ToArray();

    // tg's static data and data of a snack machine (ui_data.dm), merged as the window reads them
    private static string Snack(string free, string user, string extended = "0", string ad = "") =>
        "{\"all_products_free\":" + free + ",\"department\":\"SRV\",\"jobDiscount\":0.5,\"displayed_currency_icon\":\"coins\",\"displayed_currency_name\":\" cr\","
        + (ad.Length > 0 ? "\"ad\":\"" + ad + "\"," : "")
        + "\"product_records\":[{\"path\":\"food-candy\",\"name\":\"candy\",\"price\":30,\"ref\":\"[0x21]\",\"colorable\":0,\"icon\":\"icons/obj/food/food.dmi\",\"icon_state\":\"candy\",\"category\":\"Products\"},"
        + "{\"path\":\"food-chips\",\"name\":\"chips\",\"price\":30,\"ref\":\"[0x22]\",\"colorable\":0,\"icon\":\"icons/obj/food/food.dmi\",\"icon_state\":\"chips\",\"category\":\"Products\"},"
        + "{\"path\":\"clothing-shoes-sneakers\",\"name\":\"sneakers\",\"price\":30,\"ref\":\"[0x25]\",\"colorable\":1,\"icon\":\"gags:/datum/greyscale_config/sneakers|#545454#ffffff\",\"icon_state\":\"sneakers\",\"category\":\"Products\"}],"
        + "\"coin_records\":[{\"path\":\"food-pistachios\",\"name\":\"pistachios\",\"price\":50,\"ref\":\"[0x23]\",\"colorable\":0,\"icon\":\"icons/obj/food/food.dmi\",\"icon_state\":\"pistachios\",\"category\":\"Premium\",\"premium\":1}],"
        + "\"hidden_records\":[{\"path\":\"food-syndicake\",\"name\":\"syndi-cakes\",\"price\":50,\"ref\":\"[0x24]\",\"colorable\":0,\"icon\":\"icons/obj/food/food.dmi\",\"icon_state\":\"syndi_cakes\",\"category\":\"Contraband\",\"premium\":1}],"
        + "\"categories\":{\"Products\":{\"icon\":\"cart-shopping\"},\"Premium\":{\"icon\":\"coins\"},\"Contraband\":{\"icon\":\"mask\"}},"
        + "\"user\":" + user + ",\"stock\":{\"food-candy\":{\"amount\":6,\"free\":0},\"food-chips\":{\"amount\":0,\"free\":0},\"clothing-shoes-sneakers\":{\"amount\":2,\"free\":0},"
        + "\"food-pistachios\":{\"amount\":3,\"free\":0},\"food-syndicake\":{\"amount\":6,\"free\":0}},\"extended_inventory\":" + extended + "}";

    [Fact]
    public void AFreeVendorShowsItsProductsAsAGridOfFreeCardsAndVendsOnAClick()
    {
        var (context, tree, acts, local) = Render(Snack("1", "null", ad: "Have a snack."));
        Assert.Equal((431.0, 635.0), (context.WindowWidth, context.WindowHeight));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "user"); // a free vendor asks for no ID
        Assert.Equal("Have a snack.", ByKey(tree, "ad").AllText());

        // the first category's products, as cards: the picture, FREE and the stock under it, the name
        Assert.Equal(new[] { "product:food-candy", "product:food-chips", "product:clothing-shoes-sneakers" }, Products(tree));
        UiNode candy = ByKey(tree, "product:food-candy");
        Assert.Equal(("icons/obj/food/food.dmi", "candy", "Candy", false, false), (candy.DmIcon, candy.DmIconState, candy.Tooltip, candy.Fluid, candy.Disabled));
        Assert.Equal("FREE x6 Candy", candy.AllText());
        Assert.True(ByKey(tree, "product:food-chips").Disabled); // sold out
        candy.OnClick();
        // a colourable product has the palette (the GAGS picture is the client's own composition)
        UiNode sneakers = ByKey(tree, "product:clothing-shoes-sneakers");
        Assert.StartsWith("gags:", sneakers.DmIcon);
        Assert.Equal("palette", sneakers.Buttons.Icon);
        sneakers.Buttons.OnClick();

        // the categories: Products and Premium, the contraband hidden until the vendor is hacked
        Assert.Equal(new[] { "category:Products", "category:Premium" }, tree.Walk().Where(n => n.Key?.StartsWith("category:", StringComparison.Ordinal) == true).Select(n => n.Key));
        UiNode premium = ByKey(tree, "category:Premium");
        Assert.Equal(("coins", "yellow", false), (premium.Icon, premium.Color, premium.Selected));
        premium.OnClick();
        tree = Vending.Render(new TguiContext(1, "Vending", "Getmore Chocolate Corp", 2, UiValue.FromJson(Snack("1", "null")), (a, p) => acts.Add((a, p?.ToJson())), local));
        Assert.Equal(new[] { "product:food-pistachios" }, Products(tree));
        ByKey(tree, "product:food-pistachios").OnClick();

        Assert.Equal(new (string, string)[]
        {
            ("vend", "{\"ref\":\"[0x21]\",\"premium\":false}"), ("select_colors", "{\"ref\":\"[0x25]\"}"), ("vend", "{\"ref\":\"[0x23]\",\"premium\":true}"),
        }, acts);
    }

    [Fact]
    public void AHackedVendorShowsItsContrabandAndTheSearchAndTheListLayoutChooseWhatIsShown()
    {
        var local = new TguiLocalState();
        var (_, tree, acts, _) = Render(Snack("1", "null", extended: "1"), local);
        UiNode contraband = ByKey(tree, "category:Contraband");
        Assert.Equal(("mask", "red"), (contraband.Icon, contraband.Color));

        // two letters search every category, and the category row goes
        ByKey(tree, "search").OnInput("ca");
        tree = Render(Snack("1", "null", extended: "1"), local).Tree;
        Assert.Equal(new[] { "product:food-candy", "product:food-syndicake" }, Products(tree));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "categories");

        // the layout: no preference (tg's null layout) shows the grid and the toggle offers the grid; its first click sets the grid (tg's
        // own: anything but grid becomes grid), the second lists the products, one per row
        ByKey(tree, "search").OnInput("");
        tree = Render(Snack("1", "null", extended: "1"), local, acts).Tree;
        UiNode toggle = ByKey(tree, "layout");
        Assert.Equal(("border-all", "View as Grid"), (toggle.Icon, toggle.Tooltip));
        toggle.OnClick();
        tree = Render(Snack("1", "null", extended: "1"), local, acts).Tree;
        Assert.False(ByKey(tree, "product:food-candy").Fluid);
        Assert.Equal(("list", "View as List"), (ByKey(tree, "layout").Icon, ByKey(tree, "layout").Tooltip));
        ByKey(tree, "layout").OnClick();
        tree = Render(Snack("1", "null", extended: "1"), local, acts).Tree;
        UiNode candy = ByKey(tree, "product:food-candy");
        Assert.True(candy.Fluid);
        Assert.Equal("Candy 6 left FREE", candy.AllText());
        Assert.Equal(new[] { ("change_ui_state", "{\"new_state\":\"grid\"}"), ("change_ui_state", "{\"new_state\":\"list\"}") }, acts.ToArray());
    }

    [Fact]
    public void APayingVendorShowsTheBuyersIdAndCashTheJobDiscountAndGreysOutWhatHeCannotAfford()
    {
        const string cook = "{\"name\":\"Bob\",\"cash\":40,\"is_captain_card\":0,\"job\":\"Cook\",\"department\":\"SRV\"}";
        var (_, tree, _, _) = Render(Snack("0", cook));
        Assert.Equal(("blue", "Bob | Cook"), (ByKey(tree, "user").Color, ByKey(tree, "user").AllText()));
        Assert.Contains("40 cr", ByKey(tree, "buttons").AllText());
        // the cook's own department: half price, 15 cr
        Assert.Equal("15 cr x6 Candy", ByKey(tree, "product:food-candy").AllText());
        Assert.False(ByKey(tree, "product:food-candy").Disabled);

        // no account on the ID: tg's notice, and nothing can be bought
        var (_, noId, _, _) = Render(Snack("0", "null"));
        Assert.Equal((null, "No ID detected! Contact the Head of Personnel."), (ByKey(noId, "user").Color, ByKey(noId, "user").AllText()));
        Assert.True(ByKey(noId, "product:food-candy").Disabled);

        // a buyer of another department pays the full 30 cr, more than his 20
        var (_, poor, _, _) = Render(Snack("0", "{\"name\":\"\",\"cash\":20,\"is_captain_card\":0,\"job\":\"Assistant\",\"department\":\"CIV\"}"));
        Assert.Equal("Unknown | Assistant", ByKey(poor, "user").AllText());
        Assert.Equal("30 cr x6 Candy", ByKey(poor, "product:food-candy").AllText());
        Assert.True(ByKey(poor, "product:food-candy").Disabled);
    }

    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(string json, TguiLocalState local, List<(string, string)> acts)
    {
        var context = new TguiContext(1, "Vending", "Getmore Chocolate Corp", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, Vending.Render(context), acts, local);
    }
}
