using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Silicons SI1 (D326): the client's law windows without the engine: StateLawUi.tsx lists the laws with their checkboxes and the
/// confirm button, LawRack.tsx shows the core slot welded, the empty slots that take a held module, the screws a screwdriver
/// turns, and the link controls of a core rack.
/// </summary>
public class SiliconLawWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<string> Acts) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<string>();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add(a + (p == null ? "" : ":" + string.Join(",", p.Fields.Select(f => f.Key + "=" + (f.Value.Text ?? f.Value.AsNum().ToString(System.Globalization.CultureInfo.InvariantCulture)))))), local ?? new TguiLocalState());
        return (context, view(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void The_state_laws_window_lists_the_laws_and_states_them_after_a_confirm()
    {
        var local = new TguiLocalState();
        string json = "{\"locked\":0,\"stated_laws\":[\"Protect the bar.\"],\"all_laws\":[{\"text\":\"Hack.\",\"number\":\"!@#$\",\"type\":\"hacked\"},"
            + "{\"text\":\"You may not injure a human being.\",\"number\":1,\"type\":\"core\"},{\"text\":\"Protect the bar.\",\"number\":2,\"type\":\"supplied\"}]}";
        var (context, tree, acts) = Render(StateLawUi.Render, "StateLawUi", json, local);
        Assert.Equal((400.0, 3 * 55 + 150.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("State Laws", context.WindowTitle);
        Assert.Contains(tree.Walk(), n => n.Text == "1. You may not injure a human being." && n.Color == "blue");
        Assert.Contains(tree.Walk(), n => n.Text == "!@#$. Hack." && n.Color == "red");
        Assert.False(ByKey(tree, "check1").Checkbox);
        Assert.True(ByKey(tree, "check2").Checkbox);
        ByKey(tree, "check1").OnClick();
        ByKey(tree, "state_laws").OnClick(); // Button.Confirm: the first click asks
        Assert.Equal(new[] { "toggle_stated:law=You may not injure a human being." }, acts);
        var (_, tree2, acts2) = Render(StateLawUi.Render, "StateLawUi", json, local);
        Assert.Equal("Confirm?", ByKey(tree2, "state_laws").Text);
        ByKey(tree2, "state_laws").OnClick();
        Assert.Equal(new[] { "state_laws" }, acts2);
        var (_, locked, _) = Render(StateLawUi.Render, "StateLawUi", "{\"locked\":1,\"stated_laws\":[],\"all_laws\":[]}");
        Assert.Equal(("On Cooldown", true), (ByKey(locked, "state_laws").Text, ByKey(locked, "state_laws").Disabled));
        Assert.Contains(locked.Walk(), n => n.Text == "No laws available!");
    }

    [Fact]
    public void The_rack_window_shows_the_welded_core_takes_a_module_and_turns_screws()
    {
        string slots = "[{\"empty\":0,\"name\":\"unidentified welded module\",\"security\":2,\"ioned\":0},"
            + "{\"empty\":1,\"name\":\"Empty\",\"security\":0,\"ioned\":0},{\"empty\":0,\"name\":\"'Freeform' AI Module\",\"security\":1,\"ioned\":0}]";
        var (context, tree, acts) = Render(LawRack.Render, "LawRack", "{\"slots\":" + slots + ",\"holding_module\":1,\"holding_screwdriver\":0,"
            + "\"holding_welder\":0,\"holding_multitool\":0,\"has_core_slot\":1,\"depowered\":0,\"refresh_cooldown\":0,"
            + "\"linkable_silicons\":[],\"linked_rack_module_count\":0,\"linked_mobs\":[]}");
        Assert.Equal((400.0, 3 * 40 + 200.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Module Rack", context.WindowTitle);
        Assert.Equal(("Welded", true), (ByKey(tree, "weld0").Text, ByKey(tree, "weld0").Disabled));
        Assert.Equal("#4183CD", ByKey(tree, "slot0").Background);
        Assert.Equal(("Empty", false), (ByKey(tree, "module1").Text, ByKey(tree, "module1").Disabled));
        Assert.True(ByKey(tree, "module2").Disabled); // screwed in
        Assert.True(ByKey(tree, "screwL2").Disabled); // no screwdriver in hand
        Assert.Equal("You need a screwdriver to unscrew this module.", ByKey(tree, "screwL2").Tooltip);
        Assert.Contains(tree.Walk(), n => n.Text == "0 other module(s) are being loaded remotely.");
        Assert.Contains(tree.Walk(), n => n.DmIconState == "ai_bsod"); // no linked silicon
        Assert.Contains(tree.Walk(), n => n.Text == "N/A");
        Assert.True(ByKey(tree, "new_connection").Disabled);
        ByKey(tree, "module1").OnClick();
        ByKey(tree, "refresh").OnClick();
        Assert.Equal(new[] { "insert_module:slot=2", "refresh" }, acts);

        var (_, screwing, screwActs) = Render(LawRack.Render, "LawRack", "{\"slots\":" + slots + ",\"holding_module\":0,\"holding_screwdriver\":1,"
            + "\"holding_welder\":0,\"holding_multitool\":0,\"has_core_slot\":1,\"depowered\":1,\"refresh_cooldown\":250,\"linked_mobs\":[\"AI\"]}");
        Assert.False(ByKey(screwing, "screwR2").Disabled);
        Assert.True(ByKey(screwing, "screwL0").Disabled); // welded
        Assert.Equal(("No power!", true), (ByKey(screwing, "refresh").Tooltip, ByKey(screwing, "refresh").Disabled));
        Assert.Contains(screwing.Walk(), n => n.DmIconState == "off");
        ByKey(screwing, "screwR2").OnClick();
        Assert.Equal(new[] { "screw_module:slot=3" }, screwActs);
    }
}
