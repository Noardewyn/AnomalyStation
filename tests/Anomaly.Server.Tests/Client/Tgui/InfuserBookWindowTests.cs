using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps P3b (D327): the client's InfuserBook window without the engine: the introduction, the chapter tabs (the restricted tier 3
/// not clickable), an entry's page with its tier, threshold, qualities and source, and the page turning across chapters, each turn
/// asking the server for the page sound.
/// </summary>
public class InfuserBookWindowTests
{
    private const string Json = "{\"entries\":["
        + "{\"name\":\"Rejected\",\"infuse_mob_name\":\"rejected creatures\",\"desc\":\"Flies.\",\"threshold_desc\":\"\",\"qualities\":[\"buzz\"],\"tier\":0},"
        + "{\"name\":\"Mothroach\",\"infuse_mob_name\":\"mothroaches\",\"desc\":\"Fluttery.\",\"threshold_desc\":\"\",\"qualities\":[\"wings\",\"antennae\"],\"tier\":0},"
        + "{\"name\":\"Rat\",\"infuse_mob_name\":\"rodents\",\"desc\":\"Skittish.\",\"threshold_desc\":\"you can crawl through vents.\",\"qualities\":[\"eat anything\",\"small\"],\"tier\":1},"
        + "{\"name\":\"Gondola\",\"infuse_mob_name\":\"gondolas\",\"desc\":\"Calm.\",\"threshold_desc\":\"you are at peace.\",\"qualities\":[\"pacifism\"],\"tier\":2}"
        + "]}";

    private static (UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiLocalState local, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var c = new TguiContext(1, "InfuserBook", "DNA Infusion Manual", 2, UiValue.FromJson(Json), (a, p) => acts.Add((a, p?.ToJson())), local);
        UiNode tree = InfuserBook.Render(c);
        Assert.Equal((620.0, 500.0, "DNA Infusion Manual"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        return (tree, acts);
    }

    [Fact]
    public void TheBookOpensOnTheIntroductionAndTheRestrictedTabCannotBeOpened()
    {
        var local = new TguiLocalState();
        var (tree, acts) = Render(local);
        Assert.Equal("DNA Infusion Guide", tree.Walk().Single(n => n.Key == "instructions").Title);
        Assert.Equal("Page 1/1", tree.Walk().Single(n => n.Key == "page").Text);
        UiNode[] tabs = tree.Walk().Where(n => n.Kind == UiKind.Tab).ToArray();
        Assert.Equal(5, tabs.Length);
        Assert.True(tabs[0].Selected);
        UiNode restricted = tabs.Single(t => t.Key == "tab:4");
        Assert.Null(restricted.OnClick);
        Assert.Equal(("Tier 3 - Abberations - RESTRICTED", "red"), (restricted.Walk().Single(n => n.Key == "tab-name").Text, restricted.Walk().Single(n => n.Key == "tab-name").Color));
        Assert.Equal(2, tree.Walk().Count(n => n.Kind == UiKind.Tabs)); // rows of three
        tree.Walk().Single(n => n.Key == "last-page").OnClick(); // the first page has nothing before it
        Assert.Empty(acts);
    }

    [Fact]
    public void AChapterTabShowsItsFirstEntryWithItsTierQualitiesAndSource()
    {
        var local = new TguiLocalState();
        var (tree, acts) = Render(local);
        tree.Walk().Single(n => n.Key == "tab:2").OnClick();
        Assert.Equal(("play_flip_sound", (string)null), acts.Single());
        (tree, _) = Render(local);
        UiNode entry = tree.Walk().Single(n => n.Key == "entry");
        Assert.Equal("Rat Mutant", entry.Title);
        Assert.Equal(("Regular Mutant", "circle-half-stroke"), (entry.Buttons.Text, entry.Buttons.Icon));
        Assert.Equal("Skittish. If a subject infuses with enough DNA, you can crawl through vents.", entry.Walk().Single(n => n.Key == "desc").Text);
        Assert.Equal(new[] { "- eat anything", "- small" }, entry.Walk().Where(n => n.Key != null && n.Key.StartsWith("quality:")).Select(n => n.Text));
        UiNode mob = entry.Walk().Single(n => n.Key == "source-mob");
        Assert.Equal(("rodents", "green"), (mob.Text, mob.Color));
        Assert.True(tree.Walk().Single(n => n.Key == "tab:2").Selected);
    }

    [Fact]
    public void ThePagesTurnAcrossChaptersAndTheRejectedSourceIsRed()
    {
        var local = new TguiLocalState();
        var acts = new List<(string, string)>();
        var (tree, _) = Render(local, acts);
        tree.Walk().Single(n => n.Key == "next-page").OnClick(); // the introduction's last page: on to tier 0
        (tree, _) = Render(local, acts);
        UiNode entry = tree.Walk().Single(n => n.Key == "entry");
        Assert.Equal("Rejected Mutant", entry.Title);
        Assert.Equal("red", entry.Walk().Single(n => n.Key == "source-mob").Color);
        Assert.Equal("Page 1/2", tree.Walk().Single(n => n.Key == "page").Text);
        tree.Walk().Single(n => n.Key == "next-page").OnClick();
        (tree, _) = Render(local, acts);
        Assert.Equal("Mothroach Mutant", tree.Walk().Single(n => n.Key == "entry").Title);
        tree.Walk().Single(n => n.Key == "next-page").OnClick(); // the end of tier 0: on to tier 1
        (tree, _) = Render(local, acts);
        Assert.Equal("Rat Mutant", tree.Walk().Single(n => n.Key == "entry").Title);
        tree.Walk().Single(n => n.Key == "last-page").OnClick(); // and back to the last page of tier 0
        (tree, _) = Render(local, acts);
        Assert.Equal("Mothroach Mutant", tree.Walk().Single(n => n.Key == "entry").Title);
        Assert.Equal("Page 2/2", tree.Walk().Single(n => n.Key == "page").Text);
        Assert.Equal(4, acts.Count(a => a.Item1 == "play_flip_sound")); // every turn asks the server for the page sound

        // the last page of tier 2 shows the restricted next button and goes no further
        local = new TguiLocalState();
        local.Set("chapter", 3);
        local.Set("pageInChapter", 0);
        (tree, _) = Render(local);
        UiNode next = tree.Walk().Single(n => n.Key == "next-page");
        Assert.Equal(("RESTRICTED", "black"), (next.Text, next.Color));
        Assert.Equal("Gondola Mutant", tree.Walk().Single(n => n.Key == "entry").Title);
        var turns = new List<(string, string)>();
        (tree, _) = Render(local, turns);
        tree.Walk().Single(n => n.Key == "next-page").OnClick();
        Assert.Empty(turns);
    }
}
