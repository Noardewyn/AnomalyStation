using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>The client's LibraryScanner window without the engine: the insert notice, the cached book and the three buttons.</summary>
public class LibraryScannerWindowTests
{
    private static (UiNode Tree, List<string> Acts) Render(string json)
    {
        var acts = new List<string>();
        var c = new TguiContext(1, "LibraryScanner", "Library Scanner", 2, UiValue.FromJson(json), (a, _) => acts.Add(a), new TguiLocalState());
        UiNode tree = LibraryScanner.Render(c);
        Assert.Equal((350.0, 150.0, "Library Scanner"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        return (tree, acts);
    }

    [Fact]
    public void AnEmptyScannerAsksForABook()
    {
        var (tree, _) = Render("{\"has_book\":0,\"has_cache\":0,\"book\":[]}");
        Assert.Equal("Insert a book to scan", tree.Walk().Single(n => n.Kind == UiKind.NoticeBox).Text);
    }

    [Fact]
    public void TheCachedBookShowsAndTheButtonsAct()
    {
        var (tree, acts) = Render("{\"has_book\":1,\"has_cache\":1,\"book\":{\"title\":\"Space Law\",\"author\":\"Beepsky\"}}");
        Assert.Equal("Beepsky", tree.Walk().Single(n => n.Key == "cache").Title);
        Assert.Equal("Space Law", tree.Walk().Single(n => n.Key == "title").Text);
        foreach (string key in new[] { "scan", "eject", "clear" })
            tree.Walk().Single(n => n.Key == key && n.Kind == UiKind.Button).OnClick();
        Assert.Equal(new[] { "scan", "eject", "clear" }, acts);

        (tree, _) = Render("{\"has_book\":0,\"has_cache\":1,\"book\":{\"title\":\"Space Law\",\"author\":\"Beepsky\"}}");
        Assert.True(tree.Walk().Single(n => n.Key == "scan" && n.Kind == UiKind.Button).Disabled);
        Assert.False(tree.Walk().Single(n => n.Key == "clear" && n.Kind == UiKind.Button).Disabled);
    }
}
