using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps ST2: the client's Trophycase window (tg's Trophycase.jsx) shows the showpiece's name, picture and description and the
/// historian panel of the curator's key; the Vendatray window (Vendatray.tsx) shows the product, its price, the open and purchase
/// buttons and the tray's registration. Both are registered under their names.
/// </summary>
public class FurnitureST2WindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Has(UiNode tree, string key) => tree.Walk().Any(n => n.Key == key);

    [Fact]
    public void TheTrophycaseWindowShowsTheShowpieceAndTheHistorianPanel()
    {
        Assert.NotNull(TguiInterfaces.Find("Trophycase"));
        // an empty case, the key not inserted
        var (context, empty, acts) = Render("Trophycase", Trophycase.Render,
            "{\"has_showpiece\":0,\"historian_mode\":0,\"holographic_showpiece\":0,\"showpiece_description\":null,\"showpiece_name\":null,\"showpiece_icon\":null}");
        Assert.Equal((300.0, 380.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Under construction.", ByKey(empty, "showpiece_name").Text);
        Assert.Contains("This exhibit is empty. History awaits your contribution!", empty.AllText());
        Assert.True(Has(empty, "landmark"));
        ByKey(empty, "insert_key").OnClick();
        Assert.Equal(new (string, string)[] { ("insert_key", null) }, acts);

        // a new trophy with the key in: its picture is tg's sprite, the description is still to be recorded
        var (_, fresh, freshActs) = Render("Trophycase", Trophycase.Render,
            "{\"has_showpiece\":1,\"historian_mode\":1,\"holographic_showpiece\":0,\"showpiece_description\":null,\"showpiece_name\":\"toolbox\","
            + "\"showpiece_icon\":{\"icon\":\"icons/obj/storage/toolbox.dmi\",\"icon_state\":\"red\"}}");
        Assert.Equal("toolbox", ByKey(fresh, "showpiece_name").Text);
        Assert.Equal(("icons/obj/storage/toolbox.dmi", "red"), (ByKey(fresh, "icon").DmIcon, ByKey(fresh, "icon").DmIconState));
        Assert.Contains("This exhibit is under construction. Get the curator's key to finalize your contribution!", fresh.AllText());
        Assert.Contains("New trophy detected. Please record a description to begin archival.", fresh.AllText());
        Assert.False(ByKey(fresh, "change_message").Disabled);
        ByKey(fresh, "change_message").OnClick();
        ByKey(fresh, "lock").OnClick();
        Assert.Equal(new (string, string)[] { ("change_message", null), ("lock", null) }, freshActs);

        // a recorded one: its description, the recording note
        var (_, recorded, _) = Render("Trophycase", Trophycase.Render,
            "{\"has_showpiece\":1,\"historian_mode\":1,\"holographic_showpiece\":0,\"showpiece_description\":\"Won at the &#39;Cup&#39;\","
            + "\"showpiece_name\":\"toolbox\",\"showpiece_icon\":null}");
        Assert.Equal("Won at the 'Cup'", ByKey(recorded, "text").Text);
        Assert.Contains("Recording has begun.", recorded.AllText());

        // a holographic one: no editing
        var (_, holo, _) = Render("Trophycase", Trophycase.Render,
            "{\"has_showpiece\":1,\"historian_mode\":1,\"holographic_showpiece\":1,\"showpiece_description\":\"A fond memory\","
            + "\"showpiece_name\":\"hologram\",\"showpiece_icon\":null}");
        Assert.True(ByKey(holo, "change_message").Disabled);
        Assert.Equal("A fond memory", ByKey(holo, "hologram_text").Text);
        Assert.Contains("A holographic trophy is already present.", holo.AllText());
    }

    [Fact]
    public void TheVendatrayWindowShowsTheProductPriceAndRegistration()
    {
        Assert.NotNull(TguiInterfaces.Find("Vendatray"));
        // an empty unregistered tray
        var (context, empty, acts) = Render("Vendatray", Vendatray.Render,
            "{\"product_name\":null,\"product_cost\":5,\"tray_open\":0,\"registered\":0,\"owner_name\":null,\"product_icon\":null}");
        Assert.Equal((300.0, 270.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Empty", ByKey(empty, "name").Text);
        Assert.Equal("N/A cr ", ByKey(empty, "cost").Text);
        Assert.False(Has(empty, "image"));
        Assert.True(ByKey(empty, "buy").Disabled);
        Assert.Equal("Closed", ByKey(empty, "open").Text);
        Assert.Contains("Tray is unregistered.", empty.AllText());
        ByKey(empty, "register").OnClick();
        ByKey(empty, "open").OnClick();
        ByKey(empty, "adjust").OnClick();
        Assert.Equal(new (string, string)[] { ("Register", null), ("Open", null), ("Adjust", null) }, acts);

        // a registered tray with a product: its picture, its price, the purchase needs a confirmation
        const string json = "{\"product_name\":\"donut\",\"product_cost\":12,\"tray_open\":1,\"registered\":1,\"owner_name\":\"John Doe\","
            + "\"product_icon\":{\"icon\":\"icons/obj/food/donuts.dmi\",\"icon_state\":\"donut\"}}";
        var local = new TguiLocalState();
        var (_, tray, trayActs) = Render("Vendatray", Vendatray.Render, json, local);
        Assert.Equal("donut", ByKey(tray, "name").Text);
        Assert.Equal("12 cr ", ByKey(tray, "cost").Text);
        Assert.Equal(("icons/obj/food/donuts.dmi", "donut"), (ByKey(tray, "icon").DmIcon, ByKey(tray, "icon").DmIconState));
        Assert.Equal("Open", ByKey(tray, "open").Text);
        Assert.True(ByKey(tray, "open").Selected);
        Assert.Contains("Pays to the account of John Doe.", tray.AllText());
        Assert.False(Has(tray, "register"));
        Assert.False(ByKey(tray, "buy").Disabled);
        ByKey(tray, "buy").OnClick();
        Assert.Empty(trayActs);
        var (_, confirm, confirmActs) = Render("Vendatray", Vendatray.Render, json, local);
        Assert.Equal("Confirm?", ByKey(confirm, "buy").Text);
        ByKey(confirm, "buy").OnClick();
        Assert.Equal(new (string, string)[] { ("Buy", null) }, confirmActs);
    }
}
