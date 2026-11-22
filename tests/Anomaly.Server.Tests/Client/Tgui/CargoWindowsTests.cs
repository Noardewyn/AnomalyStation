using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps P4b (D298): the client's cargo windows without the engine: the supply console's budget, prices (a private purchase's tenth
/// more), Buy Privately, the cart's costs and private and department tags, the requests' accounts and costs, the help tab; the NT
/// Shopping Network app (NtosCargo) with the same content; the Coupon Master app.
/// </summary>
public class CargoWindowsTests
{
    private static (UiNode Tree, TguiContext Context, List<(string Action, string Params)> Acts) Render(Func<TguiContext, UiNode> render, string name, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (render(c), c, acts);
    }

    private const string CargoJson = "{\"department\":\"Cargo\",\"location\":\"CentCom\",\"points\":12500,\"grocery\":0,\"away\":1,\"self_paid\":0,"
        + "\"docked\":1,\"loan\":0,\"loan_dispatched\":0,\"can_send\":1,\"can_approve_requests\":1,\"requestonly\":0,"
        + "\"message\":\"Remember to stamp and send back the supply manifests.\",\"max_order\":20,"
        + "\"displayed_currency_full_name\":\" credits\",\"displayed_currency_name\":\" cr\","
        + "\"supplies\":{\"Emergency\":{\"name\":\"Emergency\",\"packs\":["
        + "{\"name\":\"Internals Crate\",\"cost\":400,\"id\":\"/datum/supply_pack/emergency/internals\",\"desc\":\"Air.\",\"goody\":0,\"access\":0,\"contraband\":0,\"contains\":[]},"
        + "{\"name\":\"Big Crate\",\"cost\":25000,\"id\":\"/datum/supply_pack/emergency/big\",\"desc\":\"Big.\",\"goody\":0,\"access\":\"armory\",\"contraband\":0,\"contains\":[]}]}},"
        + "\"cart\":[{\"cost_type\":\"cr\",\"object\":\"Internals Crate\",\"cost\":880,\"id\":7,\"amount\":2,\"orderer\":\"Jo\",\"paid\":2,\"dep_order\":0,\"can_be_cancelled\":1}],"
        + "\"requests\":[{\"object\":\"Big Crate\",\"cost\":25000,\"orderer\":\"Al\",\"reason\":\"guns &amp; ammo\",\"id\":9,\"account\":\"Security Budget\"}]}";

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheSupplyConsoleShowsTheBudgetPricesAndBuysPrivately()
    {
        var local = new TguiLocalState();
        var (tree, context, acts) = Render(Cargo.Render, "Cargo", CargoJson, local);
        Assert.Equal((800.0, 750.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(TguiFormat.FormatMoney(12500) + " credits", ByKey(tree, "points").Text);
        Assert.Equal("2 items (880 cr)", ByKey(tree, "count").Text.Trim());

        // the catalog: the prices in gold, a dear one in orange, the restricted lock
        UiNode internals = ByKey(tree, "pack:/datum/supply_pack/emergency/internals");
        Assert.Equal("", internals.Color);
        Assert.Equal("400 cr", ByKey(internals, "cost").Text);
        Assert.Null(internals.Walk().FirstOrDefault(n => n.Key == "private"));
        UiNode big = ByKey(tree, "pack:/datum/supply_pack/emergency/big");
        Assert.Equal("orange", big.Color);
        Assert.Equal("Restricted", ByKey(big, "access").Walk().Single(n => n.Kind == UiKind.Icon).Tooltip);
        internals.OnClick();
        ByKey(tree, "toggleprivate").OnClick();
        Assert.Equal(new (string, string)[] { ("add", "{\"id\":\"/datum/supply_pack/emergency/internals\"}"), ("toggleprivate", null) }, acts);

        // bought privately: every pack yellow, the price struck and the tenth more under it
        var (priv, _, _) = Render(Cargo.Render, "Cargo", CargoJson.Replace("\"self_paid\":0", "\"self_paid\":1"), local);
        UiNode privInternals = ByKey(priv, "pack:/datum/supply_pack/emergency/internals");
        Assert.Equal("yellow", privInternals.Color);
        Assert.True(ByKey(privInternals, "cost").Slashed);
        Assert.Equal("440 cr", ByKey(privInternals, "private").Text);
        Assert.Equal(("caution", "check-square-o"), (ByKey(priv, "toggleprivate").Color, ByKey(priv, "toggleprivate").Icon));
    }

    [Fact]
    public void TheCartAndRequestsShowCostsTagsAndAccounts()
    {
        var local = new TguiLocalState();
        var (tree, _, _) = Render(Cargo.Render, "Cargo", CargoJson, local);
        ByKey(tree, "tab:cart").OnClick();
        var (cart, _, acts) = Render(Cargo.Render, "Cargo", CargoJson, local);
        UiNode entry = ByKey(cart, "entry:7");
        Assert.Equal("880 cr", ByKey(entry, "cost").Text);
        Assert.Equal("[Private x 2]", ByKey(entry, "paid").Text);
        ByKey(entry, "count").OnChange(5);
        Assert.Equal(("modify", "{\"order_name\":\"Internals Crate\",\"amount\":5}"), Assert.Single(acts));
        Assert.Equal("Confirm the order", ByKey(cart, "confirmButton").Text);

        ByKey(cart, "tab:active requests").OnClick();
        var (requests, _, _) = Render(Cargo.Render, "Cargo", CargoJson, local);
        UiNode request = ByKey(requests, "request:9");
        Assert.Equal("Security Budget", ByKey(request, "account").Text);
        Assert.Equal(TguiFormat.FormatMoney(25000) + " cr", ByKey(request, "cost").Text);
        Assert.Equal("guns & ammo", ByKey(request, "reason").Text);

        ByKey(requests, "tab:help").OnClick();
        var (help, _, _) = Render(Cargo.Render, "Cargo", CargoJson, local);
        Assert.Contains("When in doubt, ask the QM!", help.AllText());
    }

    [Fact]
    public void TheLoanRowAppearsWithALoan()
    {
        var (tree, _, acts) = Render(Cargo.Render, "Cargo", CargoJson.Replace("\"loan\":0", "\"loan\":1"));
        UiNode loan = ByKey(tree, "loanButton");
        Assert.Equal("Loan Shuttle", loan.Text);
        Assert.False(loan.Disabled);
        loan.OnClick();
        Assert.Equal(("loan", (string)null), Assert.Single(acts));
        var (loaned, _, _) = Render(Cargo.Render, "Cargo", CargoJson.Replace("\"loan\":0,\"loan_dispatched\":0", "\"loan\":1,\"loan_dispatched\":1"));
        Assert.Equal("Loaned to Centcom", ByKey(loaned, "loaned").Text);
    }

    [Fact]
    public void TheShoppingAppShowsTheCargoContentInAComputer()
    {
        string json = CargoJson.Replace("\"department\":\"Cargo\"", "\"department\":\"Civil Budget Requisitions\"").Replace("\"can_send\":1", "\"can_send\":0,\"app_cost\":1")
            .Replace("{\"department\"", "{\"PC_device_theme\":\"ntos\",\"PC_showexitprogram\":1,\"department\"");
        var (tree, context, _) = Render(NtosCargo.Render, "NtosCargo", json);
        Assert.Equal((800.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Civil Budget Requisitions", tree.AllText());
        // the app's prices are always the private tenth more
        UiNode internals = ByKey(tree, "pack:/datum/supply_pack/emergency/internals");
        Assert.Equal("440 cr", ByKey(internals, "private").Text);
    }

    [Fact]
    public void TheCouponMasterRedeemsACodeAndListsTheCoupons()
    {
        var (none, _, _) = Render(NtosCouponMaster.Render, "NtosCouponMaster", "{\"valid_id\":0,\"redeemed_coupons\":[],\"printed_coupons\":[]}");
        Assert.Contains("No valid bank account detected. Insert a valid ID.", none.AllText());

        const string json = "{\"valid_id\":1,\"redeemed_coupons\":[{\"goody\":\"Beekeeping Starter Crate\",\"discount\":50}],\"printed_coupons\":[{\"goody\":\"Medipen Two-Pak\",\"discount\":25}]}";
        var (tree, _, acts) = Render(NtosCouponMaster.Render, "NtosCouponMaster", json);
        Assert.Equal("Beekeeping Starter Crate (50% OFF)", ByKey(ByKey(tree, "redeemed"), "coupon:0").Text);
        Assert.Equal("Medipen Two-Pak (25% OFF)", ByKey(ByKey(tree, "printed"), "coupon:0").Text);
        ByKey(tree, "code").OnCommit("BEEKEEPINGSTARTER50");
        Assert.Equal(("redeem", "{\"code\":\"BEEKEEPINGSTARTER50\"}"), Assert.Single(acts));
    }
}
