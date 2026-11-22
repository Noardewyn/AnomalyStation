using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps MD3: the client's Limbgrower window (tg's Limbgrower.jsx) shows the reagent capacity and each reagent with its confirmed
/// Remove Reagent act, the design categories as tabs with their designs' reagent costs and Make acts, and the Building... dimmer
/// while it grows. It is registered under its name.
/// </summary>
public class LimbgrowerWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Limbgrower", "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, Limbgrower.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Json = "{\"reagents\":[{\"reagent_name\":\"Synthflesh\",\"reagent_amount\":60,\"reagent_type\":\"/datum/reagent/medicine/c2/synthflesh\"}],"
        + "\"total_reagents\":60,\"max_reagents\":100,\"busy\":0,"
        + "\"categories\":[{\"name\":\"human\",\"designs\":[{\"parent_category\":\"human\",\"name\":\"Left Arm\",\"path\":\"/datum/design/leftarm\","
        + "\"needed_reagents\":[{\"name\":\"Synthflesh\",\"amount\":25}]}]},"
        + "{\"name\":\"lizard\",\"designs\":[{\"parent_category\":\"lizard\",\"name\":\"Lizard Tail\",\"path\":\"/datum/design/lizard_tail\","
        + "\"needed_reagents\":[{\"name\":\"Synthflesh\",\"amount\":20}]}]}]}";

    [Fact]
    public void TheLimbgrowerShowsItsReagentsAndDesignsAndSendsTgsActs()
    {
        Assert.NotNull(TguiInterfaces.Find("Limbgrower"));
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(Json, local);
        Assert.Equal((500.0, 550.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("60 / 100 reagent capacity used.", text);
        Assert.Equal("60u", ByKey(tree, "reagent:Synthflesh").Text);
        Assert.Contains("Synthflesh: 25u", text);
        Assert.DoesNotContain("Lizard Tail", text); // the first tab is open

        // Remove Reagent asks to confirm first
        ByKey(tree, "empty:Synthflesh").OnClick();
        Assert.Empty(acts);
        var (_, confirming, acts2) = Render(Json, local);
        Assert.Equal("Confirm?", ByKey(confirming, "empty:Synthflesh").Text);
        ByKey(confirming, "empty:Synthflesh").OnClick();
        Assert.Equal(new (string, string)[] { ("empty_reagent", "{\"reagent_type\":\"/datum/reagent/medicine/c2/synthflesh\"}") }, acts2);

        // Make sends the design and its tab
        var (_, again, acts3) = Render(Json, local);
        ByKey(again, "make:Left Arm").OnClick();
        Assert.Equal(new (string, string)[] { ("make_limb", "{\"design_path\":\"/datum/design/leftarm\",\"active_tab\":\"human\"}") }, acts3);

        // the lizard tab
        ByKey(again, "tab:lizard").OnClick();
        var (_, lizard, _) = Render(Json, local);
        Assert.Contains("Lizard Tail", lizard.AllText());
        Assert.True(ByKey(lizard, "tab:lizard").Selected);

        // busy: the dimmer
        var (_, busy, _) = Render(Json.Replace("\"busy\":0", "\"busy\":1"), new TguiLocalState());
        Assert.Contains("Building...", busy.AllText());
    }
}
