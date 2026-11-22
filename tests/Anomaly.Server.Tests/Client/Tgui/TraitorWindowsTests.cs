using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate package A2 (D316): the client's AntagInfoTraitor and Uplink windows without the engine. AntagInfoTraitor shows the employer's
/// theme, intro, objectives, allegiances and thoughts, the uplink's code and failsafe, and the code words in blue and red (tg's text when
/// there are none, no uplink section when none was given). The Uplink opens on the primary objectives (tg's directives), its Market lists
/// only what the uplink's flag, role and species may buy, by category, with the sales' own category; a buy sends tg's path or the sale's
/// ref; an item over the budget or out of stock is disabled; the search, the raw TC button, Lock and the locked shop.
/// </summary>
public class TraitorWindowsTests
{
    private static (TguiContext Context, UiNode Tree) Render(TguiInterface view, string name, string json, TguiLocalState local, List<(string Action, string Params)> acts)
    {
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context));
    }

    private const string TraitorInfo = "{\"has_codewords\":1,\"phrases\":\"captain, robust toolbox\",\"responses\":\"bee, medbay\",\"theme\":\"syndicate\","
        + "\"code\":\"123 Alpha\",\"failsafe_code\":\"456 Bravo\",\"intro\":\"You are the Gorlex Marauders agent.\",\"allies\":\"Nobody.\",\"goal\":\"Wreck it.\","
        + "\"has_uplink\":1,\"given_uplink\":1,\"uplink_intro\":\"Your uplink.\",\"uplink_unlock_info\":\"Simply enter the code into the ring tone selection.\","
        + "\"objectives\":[{\"count\":1,\"name\":\"Objective\",\"explanation\":\"Steal a fire axe.\",\"complete\":0},{\"count\":2,\"name\":\"Objective\",\"explanation\":\"Escape on the shuttle or an escape pod alive and without being in custody.\",\"complete\":0}]}";

    [Fact]
    public void AntagInfoTraitor_shows_the_employer_the_uplink_code_and_the_code_words()
    {
        var acts = new List<(string, string)>();
        var (context, tree) = Render(AntagInfoTraitor.Render, "AntagInfoTraitor", TraitorInfo, new TguiLocalState(), acts);
        Assert.Equal((620.0, 580.0, "syndicate"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));
        List<UiNode> nodes = tree.Walk().ToList();
        Assert.Equal("25px", nodes.Single(n => n.Text == "You are the Gorlex Marauders agent.").FontSize as string);
        Assert.Equal(new[] { "#1: Steal a fire axe.", "#2: Escape on the shuttle or an escape pod alive and without being in custody." },
            nodes.Where(n => n.Text != null && n.Text.StartsWith('#')).Select(n => n.Text));
        Assert.Equal("yellow", nodes.Single(n => n.Text == "Your allegiances:").Color);
        Assert.Contains(nodes, n => n.Text == "Wreck it.");
        Assert.Equal("lightblue", nodes.Single(n => n.Text == "Code: 123 Alpha").Color);
        Assert.Equal("red", nodes.Single(n => n.Text == "Failsafe: 456 Bravo").Color);
        Assert.Contains(nodes, n => n.Text == "Simply enter the code into the ring tone selection.");
        Assert.Equal(("blue", true), (nodes.Single(n => n.Text == "captain, robust toolbox").Color, nodes.Single(n => n.Text == "captain, robust toolbox").Bold));
        Assert.Equal("red", nodes.Single(n => n.Text == "bee, medbay").Color);
        Assert.Contains("AntagInfoTraitor", TguiInterfaces.Names);

        // no code words and no uplink given: tg's caution, and no uplink section
        string bare = TraitorInfo.Replace("\"has_codewords\":1", "\"has_codewords\":0").Replace("\"given_uplink\":1", "\"given_uplink\":0");
        nodes = Render(AntagInfoTraitor.Render, "AntagInfoTraitor", bare, new TguiLocalState(), acts).Tree.Walk().ToList();
        Assert.Contains(nodes, n => n.Text != null && n.Text.StartsWith("You have not been supplied with codewords.", StringComparison.Ordinal));
        Assert.DoesNotContain(nodes, n => n.Title == "Uplink");
    }

    private static string UplinkData(double telecrystals, bool shopLocked = false) =>
        "{\"telecrystals\":" + telecrystals + ",\"joined_population\":3,\"lockable\":1,\"uplink_flag\":1,\"assigned_role\":\"Assistant\",\"assigned_species\":\"human\",\"debug\":0,"
        + "\"primary_objectives\":[{\"task_name\":\"DIRECTIVE ALPHA\",\"task_text\":\"Steal a fire axe.\"}],\"can_renegotiate\":1,"
        + "\"extra_purchasable\":[{\"id\":\"/datum/uplink_item/dangerous/revolver\",\"name\":\"Syndicate Revolver (50% off!)\",\"icon\":\"icons/obj/weapons/guns/ballistic.dmi\",\"icon_state\":\"revolver\",\"cost\":6,"
        + "\"desc\":\"A revolver. Normally costs 13 TC.\",\"category\":\"Discounted Gear\",\"purchasable_from\":16777215,\"restricted\":0,\"limited_stock\":-1,\"restricted_roles\":[],\"restricted_species\":[],\"population_minimum\":0,\"ref\":\"[0x21000a1]\"}],"
        + "\"extra_purchasable_stock\":[],\"current_stock\":{\"uplink_shared_stock_kits\":1,\"uplink_shared_stock_surplus\":1,\"/datum/uplink_item/explosives/c4\":0},"
        + "\"shop_locked\":" + (shopLocked ? 1 : 0) + ",\"purchased_items\":0,"
        + "\"uplink_json\":{\"categories\":[\"Bundles\",\"Explosives\",\"Stealthy Weapons\",\"Job\"],\"items\":["
        + Item("/datum/uplink_item/explosives/c4", "Composition C-4", 1, "Explosives", "[]", "/datum/uplink_item/explosives/c4", 0)
        + "," + Item("/datum/uplink_item/explosives/x4", "Composition X-4", 2, "Explosives", "[]", "/datum/uplink_item/explosives/x4", 0)
        + "," + Item("/datum/uplink_item/stealthy_weapons/edagger", "Energy Dagger", 2, "Stealthy Weapons", "[]", "/datum/uplink_item/stealthy_weapons/edagger", 25)
        + "," + Item("/datum/uplink_item/role_restricted/clownpin", "Ultra Hilarious Firing Pin", 4, "Job", "[\"Clown\"]", "/datum/uplink_item/role_restricted/clownpin", 0)
        + "]}}";

    private static string Item(string id, string name, double cost, string category, string roles, string stockKey, double population) =>
        "{\"id\":\"" + id + "\",\"name\":\"" + name + "\",\"icon\":\"icons/obj/weapons/grenade.dmi\",\"icon_state\":\"plastic-explosive0\",\"cost\":" + cost + ",\"desc\":\"" + name + " desc\","
        + "\"category\":\"" + category + "\",\"purchasable_from\":1,\"restricted\":0,\"limited_stock\":-1,\"stock_key\":\"" + stockKey + "\",\"restricted_roles\":" + roles + ","
        + "\"restricted_species\":[],\"population_minimum\":" + population + ",\"cost_override_string\":\"\",\"lock_other_purchases\":0}";

    [Fact]
    public void The_uplink_opens_on_the_directives_and_its_market_sells_by_category()
    {
        var acts = new List<(string Action, string Params)>();
        var local = new TguiLocalState();
        var (context, tree) = Render(Uplink.Render, "Uplink", UplinkData(5), local, acts);
        Assert.Equal((700.0, 600.0, "syndicate"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));
        List<UiNode> nodes = tree.Walk().ToList();
        Assert.True(nodes.Single(n => n.Key == "tab:objectives").Selected);
        Assert.Contains(nodes, n => n.Text == "WELCOME, AGENT." && n.Color == "green");
        Assert.Contains(nodes, n => n.Text == "DIRECTIVE ALPHA");
        Assert.Contains(nodes, n => n.Text == "Steal a fire axe.");
        nodes.Single(n => n.Key == "renegotiate" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("renegotiate_objectives", acts[^1].Action);

        // the Market: the first category with items the flag and role allow (Bundles has none here; the clown's pin is not listed)
        nodes.Single(n => n.Key == "tab:market").OnClick();
        nodes = Render(Uplink.Render, "Uplink", UplinkData(5), local, acts).Tree.Walk().ToList();
        Assert.Equal(new[] { "Explosives", "Stealthy Weapons", "Discounted Gear" }, nodes.Where(n => n.Kind == UiKind.Tab && n.Key.StartsWith("category:", StringComparison.Ordinal)).Select(n => n.Text));
        Assert.Equal("5 TC", nodes.Single(n => n.Key == "buy_raw_tc" && n.Kind == UiKind.Button).Text);
        Assert.Contains(nodes, n => n.Title == "Composition X-4");
        Assert.DoesNotContain(nodes, n => n.Title == "Ultra Hilarious Firing Pin");
        // C-4's stock is used up: disabled; X-4 buys by its path
        UiNode c4 = nodes.Single(n => n.Kind == UiKind.Section && n.Title == "Composition C-4").Buttons;
        Assert.Equal(("1 TC", true), (c4.Text, c4.Disabled));
        UiNode x4 = nodes.Single(n => n.Kind == UiKind.Section && n.Title == "Composition X-4").Buttons;
        Assert.False(x4.Disabled);
        x4.OnClick();
        Assert.Equal(("buy", "{\"path\":\"/datum/uplink_item/explosives/x4\"}"), acts[^1]);

        // the sale: its own category, bought by its ref; over the budget it is disabled
        nodes.Single(n => n.Key == "category:Discounted Gear").OnClick();
        nodes = Render(Uplink.Render, "Uplink", UplinkData(5), local, acts).Tree.Walk().ToList();
        UiNode sale = nodes.Single(n => n.Kind == UiKind.Section && n.Title == "Syndicate Revolver (50% off!)").Buttons;
        Assert.Equal(("6 TC", true), (sale.Text, sale.Disabled));
        nodes = Render(Uplink.Render, "Uplink", UplinkData(10), local, acts).Tree.Walk().ToList();
        nodes.Single(n => n.Kind == UiKind.Section && n.Title == "Syndicate Revolver (50% off!)").Buttons.OnClick();
        Assert.Equal(("buy", "{\"ref\":\"[0x21000a1]\"}"), acts[^1]);

        // the search finds across categories, in the compact rows; a population lock shows its tooltip
        nodes.Single(n => n.Key == "search" && n.Kind == UiKind.Input).OnInput("dagger");
        nodes = Render(Uplink.Render, "Uplink", UplinkData(5), local, acts).Tree.Walk().ToList();
        UiNode dagger = nodes.Single(n => n.Key == "name" && n.Kind == UiKind.Box && n.Text == "Energy Dagger");
        Assert.Equal(("lock", true), (dagger.Icon, dagger.Tooltip.Contains("fewer than 25 people", StringComparison.Ordinal)));
        Assert.DoesNotContain(nodes, n => n.Text == "Composition C-4");

        // raw TC and Lock
        nodes.Single(n => n.Key == "buy_raw_tc" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("buy_raw_tc", acts[^1].Action);
        nodes.Single(n => n.Key == "lock" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("lock", acts[^1].Action);
        Assert.Contains("Uplink", TguiInterfaces.Names);

        // a locked shop: tg's SHOP LOCKED over the market
        nodes = Render(Uplink.Render, "Uplink", UplinkData(5, shopLocked: true), local, acts).Tree.Walk().ToList();
        Assert.Contains(nodes, n => n.Text == "SHOP LOCKED" && n.Color == "red");
    }
}
