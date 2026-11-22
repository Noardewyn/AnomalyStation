using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps CM1 (D333): the client's CommunicationsConsole window without the engine: the shuttle purchase page (tg's BuyingShuttle.tsx)
/// with the budget, the cards sorted emag-only first and then by their initial cost, each card's price button and its tooltip (the
/// missing money, the department's lock, the Syndicate's notice), the prerequisites and the lock line, the purchase and back acts; and
/// the main page's nuke codes request for a captain.
/// </summary>
public class CommunicationsConsoleWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "CommunicationsConsole", "Communications Console", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, CommunicationsConsole.Render(context), acts);
    }

    private const string Statics = "\"callShuttleReasonMinLength\":12,\"maxStatusLineLength\":40,\"maxMessageLength\":1024,\"displayed_currency_full_name\":\" credits\",\"displayed_currency_name\":\" cr\"";

    private const string Catalogue = "{\"authenticated\":1,\"canLogOut\":1,\"hasConnection\":1,\"emagged\":0,\"syndicate\":0,\"page\":\"buying_shuttle\",\"budget\":1000,"
        + Statics + ",\"shuttles\":["
        + "{\"name\":\"Venture Emergency Shuttle\",\"description\":\"Legroom.\",\"occupancy_limit\":\"45\",\"creditCost\":2000,\"initial_cost\":2000,\"emagOnly\":0,\"prerequisites\":null,\"ref\":\"[0x21000001]\",\"department_locked\":0,\"department_name\":\"\"},"
        + "{\"name\":\"Box Station Emergency Shuttle\",\"description\":\"The gold standard.\",\"occupancy_limit\":\"45\",\"creditCost\":800,\"initial_cost\":800,\"emagOnly\":0,\"prerequisites\":null,\"ref\":\"[0x21000002]\",\"department_locked\":0,\"department_name\":\"\"},"
        + "{\"name\":\"Luxury Shuttle\",\"description\":\"Gold.\",\"occupancy_limit\":\"75\",\"creditCost\":10000,\"initial_cost\":10000,\"emagOnly\":1,\"prerequisites\":null,\"ref\":\"[0x21000003]\",\"department_locked\":0,\"department_name\":\"\"},"
        + "{\"name\":\"Engineering Emergency Shuttle\",\"description\":\"Turbines.\",\"occupancy_limit\":\"40\",\"creditCost\":900,\"initial_cost\":3100,\"emagOnly\":0,\"prerequisites\":null,\"ref\":\"[0x21000004]\",\"department_locked\":1,\"department_name\":\"Engineering\"},"
        + "{\"name\":\"The Ark\",\"description\":\"A zoo.\",\"occupancy_limit\":\"70\",\"creditCost\":5000,\"initial_cost\":5000,\"emagOnly\":0,\"prerequisites\":\"You will need to obtain all samples for the station's DNA Vault goal.\",\"ref\":\"[0x21000005]\",\"department_locked\":0,\"department_name\":\"\"}"
        + "]}";

    [Fact]
    public void The_purchase_page_lists_the_catalogue_with_its_prices_and_locks()
    {
        var (_, tree, acts) = Render(Catalogue);
        Assert.Contains("CommunicationsConsole", TguiInterfaces.Names);
        Assert.Contains(tree.Walk(), n => n.Text == "1000 cr" && n.Bold && n.Color == "good");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "coins");

        // emag-only first, then by the initial cost
        var cards = tree.Walk().Where(n => n.Kind == UiKind.Section && n.Key != null && n.Key.StartsWith("shuttle:", StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { "Luxury Shuttle", "Box Station Emergency Shuttle", "Venture Emergency Shuttle", "Engineering Emergency Shuttle", "The Ark" }, cards.Select(c => c.Title).ToArray());

        UiNode box = cards[1].Buttons;
        Assert.Equal("800  cr", box.Text);
        Assert.False(box.Disabled);
        Assert.Null(box.Tooltip);
        Assert.Contains(cards[1].Walk(), n => n.Text == "The gold standard.");
        Assert.Contains(cards[1].Walk(), n => n.Text == "Occupancy Limit: 45" && n.Color == "teal");

        UiNode venture = cards[2].Buttons;
        Assert.True(venture.Disabled);
        Assert.Equal("You need 1000 more  credits.", venture.Tooltip);

        UiNode luxury = cards[0].Buttons;
        Assert.Equal("Buy", luxury.Text); // not emagged: no price
        Assert.Equal("red", luxury.Color);
        Assert.True(luxury.Disabled);

        UiNode engineering = cards[3].Buttons;
        Assert.Equal("Locked", engineering.Text);
        Assert.Equal("danger", engineering.Color);
        Assert.Equal("Requires Engineering Department to have the highest employees count.", engineering.Tooltip);
        Assert.Contains(cards[3].Walk(), n => n.Text == "This shuttle can only be purchased if Engineering Department has biggest number of employees!" && n.Color == "red");

        Assert.Contains(cards[4].Walk(), n => n.Text == "Prerequisites: You will need to obtain all samples for the station's DNA Vault goal." && n.Color == "violet");

        box.OnClick();
        tree.Walk().Single(n => n.Text == "Back").OnClick();
        Assert.Equal(("purchaseShuttle", "{\"shuttle\":\"[0x21000002]\"}"), acts[0]);
        Assert.Equal(("setState", "{\"state\":\"main\"}"), acts[1]);
    }

    [Fact]
    public void An_emagged_console_prices_the_syndicates_shuttles_with_their_notice()
    {
        var (_, tree, _) = Render(Catalogue.Replace("\"emagged\":0", "\"emagged\":1").Replace("\"budget\":1000", "\"budget\":20000"));
        UiNode luxury = tree.Walk().Single(n => n.Kind == UiKind.Section && n.Title == "Luxury Shuttle").Buttons;
        Assert.Equal("10000  cr", luxury.Text);
        Assert.False(luxury.Disabled);
        Assert.Equal("This shuttle is deemed significantly dangerous to the crew, and is only supplied by the Syndicate.", luxury.Tooltip);
        Assert.Equal("syndicate", Render(Catalogue.Replace("\"emagged\":0", "\"emagged\":1")).Context.WindowTheme);
    }

    [Fact]
    public void The_main_page_offers_the_purchase_and_the_nuke_codes_to_a_captain()
    {
        string main = "{\"authenticated\":1,\"canLogOut\":1,\"hasConnection\":1,\"emagged\":0,\"syndicate\":0,\"page\":\"main\",\"alertLevel\":\"green\","
            + Statics + ",\"canBuyShuttles\":1,\"canMakeAnnouncement\":1,\"canMessageAssociates\":1,\"canRecallShuttles\":1,\"canRequestNuke\":1,"
            + "\"canSendToSectors\":0,\"canSetAlertLevel\":\"SWIPE_NEEDED\",\"canToggleEmergencyAccess\":1,\"importantActionReady\":1,\"shuttleCalled\":0,"
            + "\"shuttleLastCalled\":0,\"shuttleCanEvacOrFailReason\":1,\"authorizeName\":\"Bob - Captain\",\"canRequestSafeCode\":0,\"safeCodeDeliveryWait\":0}";
        var local = new TguiLocalState();
        var (_, tree, acts) = Render(main, local);
        UiNode buy = tree.Walk().Single(n => n.Text == "Purchase Shuttle");
        Assert.False(buy.Disabled);
        buy.OnClick();
        Assert.Equal(("setState", "{\"state\":\"buying_shuttle\"}"), acts[0]);

        tree.Walk().Single(n => n.Text == "Request Nuclear Authentication Codes").OnClick();
        (_, tree, acts) = Render(main, local);
        UiNode reason = tree.Walk().Single(n => n.Kind == UiKind.TextArea && n.Key == "nukeModal:input");
        reason.OnInput("The disk is lost");
        (_, tree, acts) = Render(main, local);
        tree.Walk().Single(n => n.Text == "Request Codes").OnClick();
        Assert.Equal(("requestNukeCodes", "{\"reason\":\"The disk is lost\"}"), acts.Single());

        // a fail reason disables the purchase with its text
        (_, tree, _) = Render(main.Replace("\"canBuyShuttles\":1", "\"canBuyShuttles\":\"A replacement shuttle has already been purchased.\""));
        buy = tree.Walk().Single(n => n.Text == "Purchase Shuttle");
        Assert.True(buy.Disabled);
        Assert.Equal("A replacement shuttle has already been purchased.", buy.Tooltip);
    }
}
