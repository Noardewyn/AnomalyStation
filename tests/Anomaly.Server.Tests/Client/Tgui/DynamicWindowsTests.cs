using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate package A3 (D316): the client's DynamicAdmin, DynamicTester and the preferences menu's Antagonists page without the engine:
/// tg's status rows, tabs and acts of the Dynamic panel, the tester's odds, and the Antagonists page's tg order, pictures, predicted
/// choices and set_antags.
/// </summary>
public class DynamicWindowsTests
{
    private static (TguiContext Context, Func<UiNode> Render, List<(string Action, string Params)> Acts) Make(TguiInterface view, string name, string json)
    {
        var acts = new List<(string, string)>();
        var local = new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, () => view(context), acts);
    }

    private const string AdminJson = "{\"current_tier\":{\"number\":2,\"name\":\"Low-Medium Chaos\"},"
        + "\"ruleset_count\":{\"roundstart\":0,\"light_midround\":2,\"heavy_midround\":1,\"latejoin\":1},"
        + "\"full_config\":{\"Low Chaos\":{\"weight\":8,\"min_pop\":0}},\"config_even_enabled\":1,"
        + "\"queued_rulesets\":[{\"name\":\"Traitor\",\"id\":\"Latejoin Traitor\",\"typepath\":\"/datum/dynamic_ruleset/latejoin/traitor\",\"selected_players\":[],\"admin_disabled\":0,\"index\":1}],"
        + "\"active_rulesets\":[{\"name\":\"Traitors\",\"id\":\"Roundstart Traitor\",\"typepath\":\"/datum/dynamic_ruleset/roundstart/traitor\",\"selected_players\":[{\"key\":\"Alice\"},{\"key\":\"Bob\"},{\"key\":\"Carol\"}],\"admin_disabled\":0,\"hidden\":0,\"index\":1}],"
        + "\"all_rulesets\":{\"roundstart\":[{\"name\":\"Traitors\",\"id\":\"Roundstart Traitor\",\"typepath\":\"/datum/dynamic_ruleset/roundstart/traitor\",\"selected_players\":[],\"admin_disabled\":0},"
        + "{\"name\":\"Changelings\",\"id\":\"Roundstart Changeling\",\"typepath\":\"/datum/dynamic_ruleset/roundstart/changeling\",\"selected_players\":[],\"admin_disabled\":1}],"
        + "\"light_midround\":[{\"name\":\"Traitor\",\"id\":\"Midround Traitor\",\"typepath\":\"/datum/dynamic_ruleset/midround/from_living/traitor\",\"selected_players\":[],\"admin_disabled\":0}]},"
        + "\"time_until_lights\":12000,\"time_until_heavies\":0,\"time_until_latejoins\":0,\"time_until_next_midround\":0,\"time_until_next_latejoin\":6000,"
        + "\"failed_latejoins\":2,\"light_midround_chance\":100,\"heavy_midround_chance\":60,\"latejoin_chance\":20,\"roundstarted\":1,"
        + "\"light_chance_maxxed\":0,\"heavy_chance_maxxed\":1,\"latejoin_chance_maxxed\":0,\"next_dynamic_tick\":1500,\"antag_events_enabled\":1}";

    [Fact]
    public void The_Dynamic_panel_shows_tgs_status_and_sends_its_acts()
    {
        var (context, render, acts) = Make(DynamicAdmin.Render, "DynamicAdmin", AdminJson);
        UiNode tree = render();
        Assert.Equal((500.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Dynamic Admin Panel", context.WindowTitle);
        Assert.Equal(new[] { "Status", "Rulesets", "Config" }, tree.Walk().Where(n => n.Kind == UiKind.Tab).Select(n => n.Text));
        Assert.Contains(tree.Walk(), n => n.Text == "2 (Low-Medium Chaos)");
        Assert.DoesNotContain(tree.Walk(), n => n.Text == "(Change)"); // the round has started
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Light Midround Ruleset Count");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Light Midround Start");
        Assert.Contains(tree.Walk(), n => n.Text == "0h 20m 0s");
        Assert.Contains(tree.Walk(), n => n.Text == "Next dynamic tick (0h 2m 30s)"); // the heavies have started, no cooldown
        Assert.Contains(tree.Walk(), n => n.Text == "60%");
        Assert.Contains(tree.Walk(), n => n.Text == "20% (2 failed attempts)");
        Assert.Equal("Reset", tree.Walk().Single(n => n.Key == "max_heavy_chance").Text);
        // the roundstart count is locked after the start; the others take plus and zero
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "add:roundstart");
        tree.Walk().Single(n => n.Key == "add:latejoin").OnClick();
        tree.Walk().Single(n => n.Key == "light_start_now").OnClick();
        tree.Walk().Single(n => n.Key == "antag_events").OnClick();
        Assert.Equal(new[] { "add_ruleset_category_count", "light_start_now", "toggle_antag_events" }, acts.Select(a => a.Action));
        Assert.Contains("\"ruleset_category\":\"latejoin\"", acts[0].Params, StringComparison.Ordinal);

        // the Rulesets tab: the queue, the active rulesets with their players, every ruleset with its queue or execute button
        tree.Walk().Single(n => n.Kind == UiKind.Tab && n.Text == "Rulesets").OnClick();
        tree = render();
        Assert.Equal((800.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "Traitor (Latejoin Traitor)");
        Assert.Contains(tree.Walk(), n => n.Text == "Selected: Alice, Bob, and Carol");
        Assert.Equal(new[] { "Roundstart", "Light Midround" }, tree.Walk().Where(n => n.Key == "title").Select(n => n.Children[0].Text));
        UiNode changelings = tree.Walk().Single(n => n.Key == "ruleset:Roundstart Changeling");
        Assert.True(changelings.Walk().Single(n => n.Key == "queue").Disabled); // the round has started
        Assert.Equal("Round already started!", changelings.Walk().Single(n => n.Key == "queue").Tooltip);
        acts.Clear();
        tree.Walk().Single(n => n.Key == "ruleset:Midround Traitor").Walk().Single(n => n.Key == "execute").OnClick();
        tree.Walk().Single(n => n.Key == "remove").OnClick();
        Assert.Equal(new[] { "execute_ruleset", "remove_queued_ruleset" }, acts.Select(a => a.Action));
        Assert.Contains("/datum/dynamic_ruleset/midround/from_living/traitor", acts[0].Params, StringComparison.Ordinal);
        Assert.False(tree.Walk().Single(n => n.Key == "enable_all").Disabled); // one is disabled
        tree.Walk().Single(n => n.Key == "search").OnInput("chang");
        tree = render();
        Assert.Single(tree.Walk(), n => n.Key?.StartsWith("ruleset:", StringComparison.Ordinal) == true);

        // the Config tab: the shown table's entries as JSON
        tree.Walk().Single(n => n.Kind == UiKind.Tab && n.Text == "Config").OnClick();
        tree = render();
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "weight" && n.Children[0].Text == "8");
    }

    [Fact]
    public void Without_a_tier_the_panel_offers_to_set_one_and_without_a_config_no_config_tab()
    {
        var (_, render, acts) = Make(DynamicAdmin.Render, "DynamicAdmin", "{\"ruleset_count\":{},\"full_config\":[],\"config_even_enabled\":0,\"queued_rulesets\":[],\"active_rulesets\":[],\"all_rulesets\":{},\"roundstarted\":0}");
        UiNode tree = render();
        Assert.Equal(new[] { "Status", "Rulesets" }, tree.Walk().Where(n => n.Kind == UiKind.Tab).Select(n => n.Text));
        tree.Walk().Single(n => n.Text == "(Click to set)").OnClick();
        Assert.Equal("set_tier", Assert.Single(acts).Action);
    }

    [Fact]
    public void The_tester_shows_each_rulesets_odds_by_tab()
    {
        var (context, render, acts) = Make(DynamicTester.Render, "DynamicTester", "{\"tier\":2,\"num_players\":10,"
            + "\"roundstart_ruleset_report\":[{\"name\":\"Traitors\",\"weight\":10,\"max_candidates\":1,\"min_candidates\":1,\"comment\":\"\"},{\"name\":\"Changelings\",\"weight\":0,\"max_candidates\":1,\"min_candidates\":1,\"comment\":\"\"},"
            + "{\"name\":\"Revolution\",\"weight\":5,\"max_candidates\":1,\"min_candidates\":1,\"comment\":\" (Assuming 3 heads of staff)\"}],"
            + "\"light_midround_ruleset_report\":[{\"name\":\"Traitor\",\"weight\":10,\"max_candidates\":1,\"min_candidates\":1}],\"heavy_midround_ruleset_report\":[]}");
        UiNode tree = render();
        Assert.Equal("Dynamic Weight Tester", context.WindowTitle);
        Assert.Equal(new[] { "Changelings", "Revolution", "Traitors" }, tree.Walk().Where(n => n.Key == "name").Select(n => n.Text));
        Assert.Equal(new[] { "0%", "33%", "67%" }, tree.Walk().Where(n => n.Key == "odds").Select(n => n.Text));
        Assert.Equal(" (Assuming 3 heads of staff)", tree.Walk().Single(n => n.Text == "Revolution").Tooltip);
        tree.Walk().Single(n => n.Key == "num_players").OnChange(25);
        Assert.Equal("set_num_players", acts.Single().Action);
        tree.Walk().Single(n => n.Kind == UiKind.Tab && n.Text == "LightMidrounds").OnClick();
        tree = render();
        Assert.Equal(new[] { "100%" }, tree.Walk().Where(n => n.Key == "odds").Select(n => n.Text));
    }

    [Fact]
    public void The_antagonists_page_lists_tgs_antagonists_and_predicts_the_choices()
    {
        var (context, render, acts) = Make(PreferencesMenu.Render, "PreferencesMenu", "{\"window\":0,\"character_profiles\":[\"Ada\"],\"active_slot\":1,"
            + "\"selected_antags\":[\"traitor\"],\"antag_bans\":[\"wizard\"],"
            + "\"antagonist_icons\":{\"traitor\":[\"iconops:icons/blanks/32x32.dmi|nothing|0|103,1,1,s,115,115\",\"nothing\"]}}");
        context.Local.Set("page", "Antagonists");
        UiNode tree = render();
        Assert.False(tree.Walk().Single(n => n.Key == "page:Antagonists").Children[0].Disabled);
        Assert.Equal(new[] { "Roundstart", "Midround", "Latejoin" }, tree.Walk().Where(n => n.Key?.StartsWith("section:", StringComparison.Ordinal) == true).Select(n => n.Title));
        // tg's order: the traitors' priority -1 first, then by name
        UiNode roundstart = tree.Walk().Single(n => n.Key == "section:Roundstart");
        string[] keys = roundstart.Walk().Where(n => n.Key?.StartsWith("antag:", StringComparison.Ordinal) == true).Select(n => n.Key[6..]).ToArray();
        Assert.Equal("traitor", keys[0]);
        Assert.Equal(new[] { "traitor", "bloodbrother", "bloodworm", "changeling", "clownoperative", "cultist", "headrevolutionary", "heretic", "malfai", "operative", "spy", "wizard" }, keys);
        Assert.Equal(37, PreferencesMenu.Antagonists.Length);

        UiNode traitor = roundstart.Walk().Single(n => n.Key == "antag:traitor").Walk().Single(n => n.Key == "icon");
        Assert.Equal("green", traitor.Color);
        Assert.Equal(3, traitor.Walk().Single(n => n.Key == "picture").Size);
        Assert.StartsWith("An unpaid debt.", traitor.Tooltip, StringComparison.Ordinal);
        UiNode wizard = roundstart.Walk().Single(n => n.Key == "antag:wizard").Walk().Single(n => n.Key == "icon");
        Assert.Equal(("grey", true, "You are banned from Wizard."), (wizard.Color, wizard.Slashed, wizard.Tooltip));
        wizard.OnClick(); // a ban takes no click
        Assert.Empty(acts);

        // a click on the infiltrator turns it on at once (predicted) and tells the server
        UiNode latejoin = tree.Walk().Single(n => n.Key == "section:Latejoin");
        latejoin.Walk().Single(n => n.Key == "antag:syndicateinfiltrator").Walk().Single(n => n.Key == "icon").OnClick();
        Assert.Equal(("set_antags", "{\"antags\":[\"syndicateinfiltrator\"],\"toggled\":true}"), Assert.Single(acts));
        tree = render();
        Assert.Equal("green", tree.Walk().Single(n => n.Key == "section:Latejoin").Walk().Single(n => n.Key == "antag:syndicateinfiltrator").Walk().Single(n => n.Key == "icon").Color);
        // Disable All of the roundstart section
        roundstart = tree.Walk().Single(n => n.Key == "section:Roundstart");
        roundstart.Buttons.Walk().Single(n => n.Text == "Disable All").OnClick();
        Assert.StartsWith("{\"antags\":[\"traitor\",\"bloodbrother\"", acts[^1].Params, StringComparison.Ordinal);
        Assert.EndsWith("\"toggled\":false}", acts[^1].Params, StringComparison.Ordinal);
        tree = render();
        Assert.Equal("red", tree.Walk().Single(n => n.Key == "antag:traitor").Walk().Single(n => n.Key == "icon").Color);
    }
}
