using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps R2: the client's StationTraitsPanel window (tg's StationTraitsPanel.tsx) without the engine. The View tab lists the round's
/// traits with tg's Revert confirm button (disabled with tg's tooltips when the trait cannot be reverted or the round has started);
/// the Edit tab plans the next round's traits: the dropdown of every trait by name, Add and Delete send the whole planned list,
/// with tg's two empty states (none planned, and none to run).
/// </summary>
public class StationTraitsPanelWindowTests
{
    private const string Valid = "\"valid_station_traits\":[{\"name\":\"Wallets!\",\"path\":\"/datum/station_trait/wallets\"},{\"name\":\"Blackout\",\"path\":\"/datum/station_trait/blackout\"},{\"name\":\"Hangover\",\"path\":\"/datum/station_trait/hangover\"}]";

    private static (TguiContext Context, UiNode Tree) Render(string json, TguiLocalState local, List<(string Action, string Params)> acts)
    {
        var context = new TguiContext(1, "StationTraitsPanel", "panel", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, StationTraitsPanel.Render(context));
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void TheViewTabListsTheRoundsTraitsWithTgsRevertConfirm()
    {
        var acts = new List<(string, string)>();
        var local = new TguiLocalState();
        string json = "{\"too_late_to_revert\":0,\"current_traits\":[{\"name\":\"Blackout\",\"can_revert\":1,\"ref\":\"[0x101]\"},{\"name\":\"Filled up maintenance\",\"can_revert\":0,\"ref\":\"[0x102]\"}]," + Valid + "}";
        var (context, tree) = Render(json, local, acts);
        Assert.Equal((500.0, 500.0, "Modify Station Traits"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.True(ByKey(tree, "view").Selected);
        Assert.False(ByKey(tree, "edit").Selected);
        Assert.Contains(tree.Walk(), n => n.Text == "Blackout");
        Assert.Contains(tree.Walk(), n => n.Text == "Filled up maintenance");

        UiNode locked = ByKey(tree, "revert:[0x102]");
        Assert.True(locked.Disabled);
        Assert.Equal("This trait is not revertable.", locked.Tooltip);
        UiNode revert = ByKey(tree, "revert:[0x101]");
        Assert.False(revert.Disabled);
        Assert.Equal(("Revert", "times"), (revert.Text, revert.Icon));
        revert.OnClick(); // Button.Confirm: the first click asks
        Assert.Empty(acts);
        (_, tree) = Render(json, local, acts);
        Assert.Equal("Confirm?", ByKey(tree, "revert:[0x101]").Text);
        ByKey(tree, "revert:[0x101]").OnClick();
        Assert.Equal(new (string, string)[] { ("revert", "{\"ref\":\"[0x101]\"}") }, acts);

        // once the round has started every Revert is locked with tg's other tooltip
        (_, tree) = Render(json.Replace("\"too_late_to_revert\":0", "\"too_late_to_revert\":1"), new TguiLocalState(), acts);
        Assert.True(ByKey(tree, "revert:[0x101]").Disabled);
        Assert.Equal("It's too late to revert station traits, the round has already started.", ByKey(tree, "revert:[0x101]").Tooltip);
        (_, tree) = Render("{\"too_late_to_revert\":0,\"current_traits\":[]," + Valid + "}", new TguiLocalState(), acts);
        Assert.Contains(tree.Walk(), n => n.Text == "There are no active station traits.");
    }

    [Fact]
    public void TheEditTabPlansTheNextRoundsTraitsWithTgsActs()
    {
        var acts = new List<(string, string)>();
        var local = new TguiLocalState();
        string none = "{\"too_late_to_revert\":0,\"current_traits\":[]," + Valid + "}";
        var (_, tree) = Render(none, local, acts);
        ByKey(tree, "edit").OnClick();
        (_, tree) = Render(none, local, acts);
        Assert.True(ByKey(tree, "edit").Selected);

        // nothing planned: tg's offer to prevent the traits
        Assert.Contains(tree.Walk(), n => n.Text == "No future station traits are planned.");
        ByKey(tree, "prevent_traits").OnClick();
        Assert.Equal(("setup_future_traits", "{\"station_traits\":[]}"), acts[^1]);

        // the dropdown: every trait by name, sorted; Add sends the pick
        UiNode dropdown = ByKey(tree, "select_trait");
        Assert.Equal(new[] { "Blackout", "Hangover", "Wallets!" }, dropdown.Options);
        Assert.Equal("Select trait to add...", dropdown.Tooltip);
        ByKey(tree, "add").OnClick(); // nothing picked: no act
        Assert.Single(acts);
        dropdown.OnSelected("Wallets!");
        (_, tree) = Render(none, local, acts);
        ByKey(tree, "add").OnClick();
        Assert.Equal(("setup_future_traits", "{\"station_traits\":[\"/datum/station_trait/wallets\"]}"), acts[^1]);

        // planned traits: the new pick goes first, a planned one is not added twice, Delete sends the others
        string planned = "{\"too_late_to_revert\":0,\"current_traits\":[],\"future_station_traits\":[{\"name\":\"Wallets!\",\"path\":\"/datum/station_trait/wallets\"},{\"name\":\"Blackout\",\"path\":\"/datum/station_trait/blackout\"}]," + Valid + "}";
        local.Set("selectedTrait", "Hangover");
        (_, tree) = Render(planned, local, acts);
        ByKey(tree, "add").OnClick();
        Assert.Equal(("setup_future_traits", "{\"station_traits\":[\"/datum/station_trait/hangover\",\"/datum/station_trait/wallets\",\"/datum/station_trait/blackout\"]}"), acts[^1]);
        local.Set("selectedTrait", "Blackout");
        (_, tree) = Render(planned, local, acts);
        int count = acts.Count;
        ByKey(tree, "add").OnClick();
        Assert.Equal(count, acts.Count);
        ByKey(tree, "delete:/datum/station_trait/wallets").OnClick();
        Assert.Equal(("setup_future_traits", "{\"station_traits\":[\"/datum/station_trait/blackout\"]}"), acts[^1]);

        // an empty plan: no traits next round, or run them normally
        (_, tree) = Render("{\"too_late_to_revert\":0,\"current_traits\":[],\"future_station_traits\":[]," + Valid + "}", local, acts);
        Assert.Contains(tree.Walk(), n => n.Text == "No station traits will run next round.");
        ByKey(tree, "clear_future_traits").OnClick();
        Assert.Equal(("clear_future_traits", null), acts[^1]);
    }

    [Fact]
    public void TheWindowIsRegisteredUnderItsInterfaceName()
    {
        Assert.Equal((TguiInterface)StationTraitsPanel.Render, TguiInterfaces.Find("StationTraitsPanel"));
    }
}
