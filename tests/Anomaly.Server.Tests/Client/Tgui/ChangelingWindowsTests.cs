using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package K1 (D327): the changeling's windows without the engine. AntagInfoChangeling: the true name and hive, the absorbed DNA
/// button, the objectives with the "Evolve New Directives" button, the abilities' guide, the stolen memories' dropdown that picks a
/// story and the "Absorb a victim first!" notices. CellularEmporium: the genetic points and the readapt button, the categories in tg's
/// order, the abilities of the selected one by name, the owned and unaffordable ones disabled, the search over every category and the
/// evolve act with the ability's path. LingMMITalk: the MMI's map view and the message sent and cleared.
/// </summary>
public class ChangelingWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, view(context), acts);
    }

    private const string InfoJson = "{\"true_name\":\"Mr. Alpha\",\"hive_name\":\"Xenomorph-hive\",\"stolen_antag_info\":\"\",\"absorbed_dna\":2,\"can_change_objective\":1,"
        + "\"objectives\":[{\"count\":1,\"name\":\"Objective\",\"explanation\":\"Extract 6 compatible genomes.\",\"complete\":0}],"
        + "\"memories\":[{\"name\":\"Alice's first kiss\",\"story\":\"Alice kissed Bob.\"},{\"name\":\"Alice's fall\",\"story\":\"Alice fell down.\"}]}";

    [Fact]
    public void The_info_window_shows_the_name_hive_objectives_guide_and_memories()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(AntagInfoChangeling.Render, "AntagInfoChangeling", InfoJson, local);
        Assert.Equal((720.0, 750.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "You are Mr. Alpha from the");
        UiNode hive = tree.Walk().Single(n => n.Text == " Xenomorph-hive");
        Assert.Equal(("yellow", true), (hive.Color, hive.Bold));
        Assert.Equal("2", tree.Walk().Single(n => n.Key == "dna").Text);
        Assert.Contains(tree.Walk(), n => n.Text == "#1: Extract 6 compatible genomes.");
        UiNode evolve = tree.Walk().Single(n => n.Text == "Evolve New Directives");
        evolve.OnClick();
        Assert.Equal("change_objectives", Assert.Single(acts).Action);
        // the guide's paragraphs keep tg's styled words; the hivemind's names the alias
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Html && n.TextValue.Contains("color:red;font-weight:bold'> Absorb DNA</span>"));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Html && n.TextValue.Contains("Mr. Alpha</span>, by starting a message with"));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Color == "danger");

        // the first memory is shown; picking the other shows its story
        UiNode dropdown = tree.Walk().Single(n => n.Kind == UiKind.Dropdown);
        Assert.Equal(new[] { "Alice's first kiss", "Alice's fall" }, dropdown.Options);
        Assert.Contains(tree.Walk(), n => n.Text == "Alice kissed Bob.");
        dropdown.OnSelected("Alice's fall");
        var (_, again, _) = Render(AntagInfoChangeling.Render, "AntagInfoChangeling", InfoJson, local);
        Assert.Contains(again.Walk(), n => n.Text == "Alice fell down.");
        // no stolen antagonist information yet
        Assert.Single(again.Walk(), n => n.Text == "Absorb a victim first!");
        Assert.Contains("AntagInfoChangeling", TguiInterfaces.Names);
    }

    [Fact]
    public void The_info_window_asks_for_a_victim_without_memories()
    {
        var (_, tree, _) = Render(AntagInfoChangeling.Render, "AntagInfoChangeling",
            "{\"true_name\":\"Ms. Beta\",\"hive_name\":\"Gorlex-hive\",\"stolen_antag_info\":\"Alice's antagonist memories: none.\",\"absorbed_dna\":1,\"can_change_objective\":0,\"objectives\":[],\"memories\":[]}");
        Assert.Single(tree.Walk(), n => n.Text == "Absorb a victim first!");
        Assert.Contains(tree.Walk(), n => n.Text == "Alice's antagonist memories: none.");
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Dropdown);
        Assert.DoesNotContain(tree.Walk(), n => n.Text == "Evolve New Directives");
    }

    private static string Ability(string name, string path, int cost, string category, int absorbs = 0) =>
        "{\"name\":\"" + name + "\",\"desc\":\"" + name + " desc.\",\"icon\":\"" + name.ToLowerInvariant().Replace(' ', '_') + "\",\"helptext\":\"\",\"path\":\"" + path
        + "\",\"genetic_point_required\":" + cost + ",\"absorbs_required\":" + absorbs + ",\"dna_required\":0,\"category\":\"" + category + "\"}";

    private static readonly string EmporiumJson = "{\"abilities\":["
        + Ability("Organic Shield", "/datum/action/changeling/weapon/shield", 1, "combat") + ","
        + Ability("Arm Blade", "/datum/action/changeling/weapon/arm_blade", 2, "combat") + ","
        + Ability("Spread Infestation", "/datum/action/changeling/spiders", 1, "utility", 3) + ","
        + Ability("Lesser Form", "/datum/action/changeling/lesserform", 1, "stealth") + ","
        + Ability("Mute Sting", "/datum/action/changeling/sting/mute", 2, "stings")
        + "],\"can_readapt\":0,\"genetic_points_count\":1,\"owned_abilities\":[\"/datum/action/changeling/weapon/shield\"],\"absorb_count\":0,\"dna_count\":1}";

    [Fact]
    public void The_emporium_sells_the_abilities_of_a_category()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(CellularEmporium.Render, "CellularEmporium", EmporiumJson, local);
        Assert.Equal((900.0, 520.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("1 DNA", tree.Walk().Single(n => n.Key == "emporium").Title);
        UiNode readapt = tree.Walk().Single(n => n.Key == "readapt");
        Assert.Equal(("Readapt (0)", true, "Need more DNA to readapt."), (readapt.Text, readapt.Disabled, readapt.Tooltip));
        // tg's order of the categories, combat selected first
        Assert.Equal(new[] { "Combat", "Stealth", "Utility", "Stings" }, tree.Walk().Where(n => n.Kind == UiKind.Tab).Select(n => n.Text));
        // the combat abilities by name: the arm blade unaffordable, the shield owned
        Assert.Equal(new[] { "Arm Blade", "Organic Shield (Owned)" }, tree.Walk().Where(n => n.Key == "ability").Select(n => n.Title));
        var buys = tree.Walk().Where(n => n.Key == "buy").ToList();
        Assert.Equal(("Cost: 2 DNA", true, "bad"), (buys[0].Text, buys[0].Disabled, buys[0].Color));
        Assert.Equal((true, "good"), (buys[1].Disabled, buys[1].Color));

        // the stealth tab: the lesser form is affordable; buying sends the path
        tree.Walk().Single(n => n.Kind == UiKind.Tab && n.Text == "Stealth").OnClick();
        var (_, stealth, stealthActs) = Render(CellularEmporium.Render, "CellularEmporium", EmporiumJson, local);
        UiNode buy = stealth.Walk().Single(n => n.Key == "buy");
        Assert.Equal(("Cost: 1 DNA", false, "average"), (buy.Text, buy.Disabled, buy.Color));
        buy.OnClick();
        Assert.Equal(("evolve", "{\"path\":\"/datum/action/changeling/lesserform\"}"), Assert.Single(stealthActs));

        // the utility tab's spiders need absorptions; the search finds over every category, compact
        tree.Walk().Single(n => n.Kind == UiKind.Tab && n.Text == "Utility").OnClick();
        var (_, utility, _) = Render(CellularEmporium.Render, "CellularEmporium", EmporiumJson, local);
        UiNode spiders = utility.Walk().Single(n => n.Key == "buy");
        Assert.Equal(("1 DNA, 3 absorptions", true), (spiders.Tooltip, spiders.Disabled));
        utility.Walk().Single(n => n.Kind == UiKind.Input && n.Key == "search").OnInput("sting");
        var (_, search, _) = Render(CellularEmporium.Render, "CellularEmporium", EmporiumJson, local);
        Assert.Contains(search.Walk(), n => n.Key == "name" && n.Text == "Mute Sting");
        Assert.DoesNotContain(search.Walk(), n => n.Key == "ability");
        Assert.Empty(acts);
        Assert.Contains("CellularEmporium", TguiInterfaces.Names);
    }

    [Fact]
    public void The_emporium_readapts_and_says_when_a_search_finds_nothing()
    {
        var local = new TguiLocalState();
        string json = EmporiumJson.Replace("\"can_readapt\":0", "\"can_readapt\":2");
        var (_, tree, acts) = Render(CellularEmporium.Render, "CellularEmporium", json, local);
        UiNode readapt = tree.Walk().Single(n => n.Key == "readapt");
        Assert.Equal(("Readapt (2)", false), (readapt.Text, readapt.Disabled));
        readapt.OnClick();
        Assert.Equal("readapt", Assert.Single(acts).Action);
        tree.Walk().Single(n => n.Kind == UiKind.Input && n.Key == "search").OnInput("nothing like this");
        var (_, none, _) = Render(CellularEmporium.Render, "CellularEmporium", json, local);
        Assert.Equal("No abilities found.", none.Walk().Single(n => n.Key == "empty").Text);
    }

    [Fact]
    public void The_mmi_window_shows_the_view_and_sends_the_message()
    {
        var local = new TguiLocalState();
        var (context, tree, _) = Render(LingMMITalk.Render, "LingMMITalk", "{\"mmi_view\":\"ling_mmi_[0x2a]_view\"}", local);
        Assert.Equal((360.0, 360.0, "Decoy Brain MMI View"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        UiNode map = tree.Walk().Single(n => n.Kind == UiKind.ByondUi);
        Assert.Equal(("ling_mmi_[0x2a]_view", "240px"), (map.MapRef, map.Width as string));
        tree.Walk().Single(n => n.Kind == UiKind.TextArea).OnInput("hello there");
        var (_, typed, acts) = Render(LingMMITalk.Render, "LingMMITalk", "{\"mmi_view\":\"ling_mmi_[0x2a]_view\"}", local);
        typed.Walk().Single(n => n.Kind == UiKind.Button && n.Key == "send").OnClick();
        Assert.Equal(("send_mmi_message", "{\"message\":\"hello there\"}"), Assert.Single(acts));
        var (_, cleared, _) = Render(LingMMITalk.Render, "LingMMITalk", "{\"mmi_view\":\"ling_mmi_[0x2a]_view\"}", local);
        Assert.Equal("", cleared.Walk().Single(n => n.Kind == UiKind.TextArea).TextValue);
        Assert.Contains("LingMMITalk", TguiInterfaces.Names);
    }
}
