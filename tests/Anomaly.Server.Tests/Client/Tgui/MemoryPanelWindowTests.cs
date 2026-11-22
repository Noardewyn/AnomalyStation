using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package G4: the client's MemoryPanel window (MemoryPanel.jsx) without the engine. It reads tg's ui_data of the memory panel (each
/// memory's name and quality) and draws each memory's quality button (its icon, look and tooltip) beside its name; it sends no act.
/// </summary>
public class MemoryPanelWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<string> Acts) Render(string json)
    {
        var acts = new List<string>();
        var context = new TguiContext(1, "MemoryPanel", "MemoryPanel", 2, UiValue.FromJson(json), (a, _) => acts.Add(a), new TguiLocalState());
        return (context, MemoryPanel.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void EachMemoryShowsItsQualityButtonAndItsName()
    {
        var (context, tree, acts) = Render("{\"memories\":[{\"name\":\"The slipping of the assistant.\",\"quality\":2},"
            + "{\"name\":\"The code to the golden safe on the bridge, 40213.\",\"quality\":-1},{\"name\":\"The death of the captain.\",\"quality\":5},"
            + "{\"name\":\"Erroneous memory - This is a bug\",\"quality\":0},{\"name\":\"The borging of the clown.\",\"quality\":3},"
            + "{\"name\":\"The award ceremony.\",\"quality\":4},{\"name\":\"A kiss.\",\"quality\":1},{\"name\":\"No quality\"}]}");
        Assert.Equal((400.0, 500.0, "Memory Panel"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));

        UiNode header = ByKey(tree, "header");
        Assert.Equal(("Memories", "info", "transparent"), (header.Title, header.Buttons.Icon, header.Buttons.Color));
        Assert.Equal("These are your memories. You gain them from doing notable things and you can use them in art!", header.Buttons.Tooltip);

        Assert.Equal(new[] { "The slipping of the assistant.", "The code to the golden safe on the bridge, 40213.", "The death of the captain.",
            "Erroneous memory - This is a bug", "The borging of the clown.", "The award ceremony.", "A kiss.", "No quality" },
            Enumerable.Range(0, 8).Select(i => ByKey(tree, "name:" + i).Text));
        // tg's quality buttons: the key and the poop and plain star are transparent, the others have tg's gradient (the "meh" one's CSS ends in
        // a semicolon, as tg's does, so the renderer drops it), an unknown quality is the question mark
        (string, string, string)[] looks = Enumerable.Range(0, 8).Select(i => ByKey(tree, "quality:" + i)).Select(b => (b.Icon, b.Color, b.Background)).ToArray();
        Assert.Equal(new (string, string, string)[]
        {
            ("star", null, "linear-gradient(to right, #964B30, #D68B60, #B66B30, #D68B60, #964B30);"),
            ("key", "transparent", null),
            ("crown", null, "linear-gradient(to right, #56A5B3, #75D4E2, #56A5B3, #75D4E2, #56A5B3)"),
            ("poop", "transparent", null),
            ("star", null, "linear-gradient(to right, #636363, #a3a3a3, #6e6e6e, #a3a3a3, #636363)"),
            ("star", null, "linear-gradient(to right, #AA771C, #BCB68A, #B38728, #BCB68A, #AA771C)"),
            ("star", "transparent", null),
            ("question", null, null),
        }, looks);
        Assert.Equal("This is a key memory. It contains important information you may want to double-check in the future.", ByKey(tree, "quality:1").Tooltip);
        Assert.StartsWith("This memory has no valid quality assigned.", ByKey(tree, "quality:7").Tooltip);

        // the buttons only explain: nothing is sent
        foreach (UiNode button in tree.Walk().Where(n => n.Kind == UiKind.Button))
            button.OnClick?.Invoke();
        Assert.Empty(acts);
    }

    [Fact]
    public void AnEmptyPanelIsTheTitleAloneAsTgsAlwaysTruthyListShowsNoDimmer()
    {
        var (_, tree, _) = Render("{\"memories\":[]}");
        Assert.Equal("Memories", ByKey(tree, "header").Title);
        Assert.Empty(ByKey(tree, "memories").Children);
        Assert.DoesNotContain("You have no memories!", tree.AllText());
    }
}
