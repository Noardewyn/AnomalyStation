using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package H2: the client's SmartVend window (SmartVend.tsx) without the engine. It reads tg's ui_data of a smart fridge (the items
/// grouped by type and name with their picture and amount) the way the TSX does: the items as a grid of cards (the smart fridge
/// layout preference's default) or, after the toggle, a list; the search filters them by name; a click releases one, a right click all of
/// them, the amount box that amount; an empty fridge says so; a dehydrator shows its Dry button instead of the search.
/// </summary>
public class SmartVendWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var context = new TguiContext(1, "SmartVend", "smartfridge", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, SmartVend.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static string[] Items(UiNode tree) => tree.Walk().Where(n => n.Kind == UiKind.ImageButton).Select(n => n.Key).ToArray();

    // tg's ui_data of a food smart fridge (smartfridge.dm:369): the contents keyed "[type]-[name]", sorted by key
    private const string Fridge =
        "{\"contents\":{"
        + "\"/obj/item/food/grown/apple-apple\":{\"path\":\"/obj/item/food/grown/apple-apple\",\"name\":\"Apple\",\"icon\":\"icons/obj/service/hydroponics/harvest.dmi\",\"icon_state\":\"apple\",\"amount\":3},"
        + "\"/obj/item/food/grown/banana-banana\":{\"path\":\"/obj/item/food/grown/banana-banana\",\"name\":\"Banana\",\"icon\":\"icons/obj/service/hydroponics/harvest.dmi\",\"icon_state\":\"banana\",\"amount\":1}"
        + "},\"name\":\"smartfridge\",\"isdryer\":0}";

    [Fact]
    public void AFridgeShowsItsItemsAsCardsAndReleasesOneAllOrTheChosenAmount()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(Fridge, local);
        Assert.Equal((431.0, 575.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Storage", ByKey(tree, "storage").Title);

        // the grid: a card per item, its picture, name and amount
        Assert.Equal(new[] { "tile:/obj/item/food/grown/apple-apple", "tile:/obj/item/food/grown/banana-banana" }, Items(tree));
        UiNode apple = ByKey(tree, "tile:/obj/item/food/grown/apple-apple");
        Assert.Equal(("icons/obj/service/hydroponics/harvest.dmi", "apple", "Apple", false, false), (apple.DmIcon, apple.DmIconState, apple.Tooltip, apple.Fluid, apple.Disabled));
        Assert.Equal("x3 Apple", apple.AllText());
        // more than one: the amount box; a single banana has none
        Assert.Equal((1.0, 3.0), (apple.Buttons.MinValue, apple.Buttons.MaxValue));
        Assert.Null(ByKey(tree, "tile:/obj/item/food/grown/banana-banana").Buttons);

        apple.OnClickWith(new UiClick(false, false, false, false));
        apple.OnClickWith(new UiClick(false, false, false, true));
        apple.Buttons.OnChange(2);
        Assert.Equal(new (string, string)[]
        {
            ("Release", "{\"path\":\"/obj/item/food/grown/apple-apple\",\"amount\":1}"),
            ("Release", "{\"path\":\"/obj/item/food/grown/apple-apple\",\"amount\":3}"),
            ("Release", "{\"path\":\"/obj/item/food/grown/apple-apple\",\"amount\":2}"),
        }, acts);
    }

    [Fact]
    public void TheSearchAndTheListLayoutChooseWhatIsShownAndAnEmptyFridgeSaysSo()
    {
        var local = new TguiLocalState();
        var acts = new List<(string, string)>();
        UiNode tree = Render(Fridge, local, acts).Tree;
        ByKey(tree, "search").OnInput("ban");
        tree = Render(Fridge, local, acts).Tree;
        Assert.Equal(new[] { "tile:/obj/item/food/grown/banana-banana" }, Items(tree));
        ByKey(tree, "search").OnInput("pear");
        tree = Render(Fridge, local, acts).Tree;
        Assert.Equal("Nothing found.", ByKey(tree, "nothing").Text);
        ByKey(tree, "search").OnInput("");

        // the toggle: the grid offers the list, which shows one item per row with its amount box
        tree = Render(Fridge, local, acts).Tree;
        UiNode toggle = ByKey(tree, "layout");
        Assert.Equal(("list", "View as List"), (toggle.Icon, toggle.Tooltip));
        toggle.OnClick();
        tree = Render(Fridge, local, acts).Tree;
        UiNode banana = ByKey(tree, "row:/obj/item/food/grown/banana-banana");
        Assert.True(banana.Fluid);
        Assert.Equal("Banana x1", banana.AllText());
        Assert.True(banana.Walk().Single(n => n.Kind == UiKind.NumberInput).Disabled); // one banana: nothing to choose
        Assert.Equal(("border-all", "View as Grid"), (ByKey(tree, "layout").Icon, ByKey(tree, "layout").Tooltip));
        Assert.Equal(new[] { ("change_ui_state", "{\"new_state\":\"list\"}") }, acts.ToArray());

        // an empty fridge: tg's ui_data sends an empty list
        tree = Render("{\"contents\":[],\"name\":\"smartfridge\",\"isdryer\":0}", new TguiLocalState()).Tree;
        Assert.Equal("Nothing found.", ByKey(tree, "nothing").Text);
    }

    [Fact]
    public void ADehydratorShowsItsDryButtonInsteadOfTheSearch()
    {
        string dryer(string drying) => "{\"contents\":{\"/obj/item/food/grown/grapes-grapes\":{\"path\":\"/obj/item/food/grown/grapes-grapes\",\"name\":\"Bunch Of Grapes\","
            + "\"icon\":\"icons/obj/service/hydroponics/harvest.dmi\",\"icon_state\":\"grapes\",\"amount\":2}},\"name\":\"dehydrator\",\"isdryer\":1,\"drying\":" + drying + "}";
        var acts = new List<(string, string)>();
        UiNode tree = Render(dryer("0"), new TguiLocalState(), acts).Tree;
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "search" || n.Key == "layout");
        UiNode dry = ByKey(tree, "dry");
        Assert.Equal(("tint", "Dry"), (dry.Icon, dry.Text));
        dry.OnClick();
        UiNode stop = ByKey(Render(dryer("1"), new TguiLocalState()).Tree, "dry");
        Assert.Equal(("stop", "Stop drying"), (stop.Icon, stop.Text));
        Assert.Equal(new[] { ("Dry", (string)null) }, acts.ToArray());
    }
}
