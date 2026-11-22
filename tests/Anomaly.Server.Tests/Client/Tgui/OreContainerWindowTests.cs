using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps ST5: the client's OreContainer window (tg's OreContainer.tsx): the stock of an ore container drawn from each ore's own icon,
/// named by the first word of its title-cased name with its amount, filtered by the search on the name, and the Withdraw button's act
/// with the ore's reference; it is registered under its name.
/// </summary>
public class OreContainerWindowTests
{
    private const string Json = "{\"ores\":[" +
        "{\"id\":\"[0x2001]\",\"name\":\"iron ore\",\"amount\":5,\"icon\":\"icons/obj/ore.dmi\",\"icon_state\":\"iron\"}," +
        "{\"id\":\"[0x2002]\",\"name\":\"plasma ore\",\"amount\":12,\"icon\":\"icons/obj/ore.dmi\",\"icon_state\":\"plasma\"}]}";

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheOreContainerWindowListsItsStockAndWithdrawsAnOre()
    {
        Assert.NotNull(TguiInterfaces.Find("OreContainer"));

        var acts = new List<(string Action, string Reference)>();
        var local = new TguiLocalState();
        var context = new TguiContext(1, "OreContainer", "w", 2, UiValue.FromJson(Json),
            (action, args) => acts.Add((action, args?.Get("reference")?.Text)), local);
        UiNode tree = OreContainer.Render(context);
        Assert.Equal((550.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Ore Container", context.WindowTitle);

        UiNode icon = ByKey(tree, "icon:[0x2001]");
        Assert.Equal(("icons/obj/ore.dmi", "iron"), (icon.DmIcon, icon.DmIconState));
        Assert.Equal("Iron", ByKey(tree, "name:[0x2001]").Text);
        Assert.Equal("Amount: 5", ByKey(tree, "amount:[0x2001]").Text);
        Assert.Equal("Plasma", ByKey(tree, "name:[0x2002]").Text);

        UiNode withdraw = ByKey(tree, "withdraw:[0x2002]");
        Assert.Equal("Withdraw", withdraw.Text);
        withdraw.OnClick();
        Assert.Equal(("withdraw", "[0x2002]"), Assert.Single(acts));

        // the search keeps the ores whose names match
        ByKey(tree, "search").OnInput("plas");
        var searched = new TguiContext(1, "OreContainer", "w", 2, UiValue.FromJson(Json), (_, _) => { }, local);
        UiNode filtered = OreContainer.Render(searched);
        Assert.DoesNotContain(filtered.Walk(), n => n.Key == "ore:[0x2001]");
        Assert.Contains(filtered.Walk(), n => n.Key == "ore:[0x2002]");
    }
}
