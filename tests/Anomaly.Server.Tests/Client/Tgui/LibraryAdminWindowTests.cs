using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's LibraryAdmin and AdminBookViewer windows without the engine: the archive's listing with the ckey search and the
/// toggles, a book's delete opening the modify page with its history, the hide sent with the typed reason; the viewer's text.
/// </summary>
public class LibraryAdminWindowTests
{
    private const string Json = "{\"can_connect\":1,\"can_db_request\":1,\"page_count\":1,\"our_page\":1,\"search_categories\":[\"Any\"],\"category\":\"Any\","
        + "\"title\":\"\",\"author\":\"\",\"book_id\":null,\"params_changed\":0,\"view_raw\":0,\"show_deleted\":1,"
        + "\"pages\":[{\"id\":5,\"author\":\"Ann\",\"title\":\"Rude\",\"category\":\"Adult\",\"author_ckey\":\"ann\",\"deleted\":null}],"
        + "\"history\":{\"5\":[{\"id\":1,\"book\":5,\"reason\":\"old\",\"ckey\":\"admin\",\"datetime\":\"2026\",\"action\":\"undeleted\",\"address\":\"1.2.3.4\"},"
        + "{\"id\":2,\"book\":5,\"reason\":\"older\",\"ckey\":\"admin\",\"datetime\":\"2026\",\"action\":\"deleted\",\"address\":\"1.2.3.4\"}]}}";

    private static (UiNode Tree, List<(string Action, string Params)> Acts, TguiContext Context) Render(TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var c = new TguiContext(1, "LibraryAdmin", "Admin Library Console", 2, UiValue.FromJson(Json), (a, p) => acts.Add((a, p?.ToJson())), local);
        UiNode tree = LibraryAdmin.Render(c);
        Assert.Equal((800.0, 600.0, "Admin Library Console"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        return (tree, acts, c);
    }

    [Fact]
    public void TheListingDeletesABookWithAReasonFromTheModifyPage()
    {
        var local = new TguiLocalState();
        var (tree, acts, c) = Render(local);
        Assert.Equal("admin", c.WindowTheme);
        Assert.Contains("Rude", tree.AllText());
        Assert.Contains("ann", tree.AllText());
        Assert.Equal("All", tree.Walk().Single(n => n.Key == "toggle_deleted").Text);
        tree.Walk().Single(n => n.Key == "search_ckey").OnCommit("ann");
        tree.Walk().Single(n => n.Key == "delete:5").OnClick();
        Assert.Equal(new[] { "set_search_ckey", "get_history" }, acts.Select(a => a.Action));

        (tree, acts, _) = Render(local);
        Assert.Contains("Why do you want to delete this book?", tree.AllText());
        string[] history = tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("entry:", StringComparison.Ordinal)).Select(n => n.Key).ToArray();
        Assert.Equal(new[] { "entry:2", "entry:1" }, history); // newest first
        Assert.Equal("rgba(255, 0, 0, 0.4)", tree.Walk().First(n => n.Key == "action" && n.Kind == UiKind.Box).Background);
        tree.Walk().Single(n => n.Key == "reason" && n.Kind == UiKind.TextArea).OnInput("spam");
        (tree, acts, _) = Render(local);
        tree.Walk().Single(n => n.Key == "modify").OnClick();
        Assert.Equal("hide_book", acts.Single().Action);
        Assert.Contains("\"delete_reason\":\"spam\"", acts.Single().Params);
        Assert.Contains("\"book_id\":5", acts.Single().Params);
        (tree, _, _) = Render(local);
        Assert.Contains("Rude", tree.AllText()); // back to the listing
    }

    [Fact]
    public void TheViewerShowsTheBooksText()
    {
        var c = new TguiContext(1, "AdminBookViewer", null, 2, UiValue.FromJson("{\"title\":\"Rude\",\"content\":\"# Words\",\"author\":\"Ann\",\"view_raw\":1}"), (_, _) => { }, new TguiLocalState());
        UiNode tree = AdminBookViewer.Render(c);
        Assert.Equal((400.0, 400.0, "Reading: Rude"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        Assert.Equal("# Words", tree.Walk().Single(n => n.Key == "raw").Text);
    }
}
