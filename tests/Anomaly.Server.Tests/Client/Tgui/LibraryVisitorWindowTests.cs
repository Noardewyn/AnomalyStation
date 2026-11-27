using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>The client's LibraryVisitor window without the engine: tg's notice without the archive, the search row and the listing.</summary>
public class LibraryVisitorWindowTests
{
    private static (UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var c = new TguiContext(1, "LibraryVisitor", "Library Lookup Console", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        UiNode tree = LibraryVisitor.Render(c);
        Assert.Equal((702.0, 421.0, "Library Lookup Console"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        return (tree, acts);
    }

    [Fact]
    public void WithoutTheArchiveTheVisitorSeesTgsNotice()
    {
        var (tree, _) = Render("{\"can_connect\":0,\"can_db_request\":1,\"page_count\":1,\"our_page\":1,\"pages\":[]}");
        Assert.Contains("Unable to retrieve book listings.", tree.AllText());
    }

    [Fact]
    public void TheSearchRowSendsItsFieldsAndTheListingShowsThePage()
    {
        var (tree, acts) = Render("{\"can_connect\":1,\"can_db_request\":1,\"page_count\":3,\"our_page\":2,\"search_categories\":[\"Any\",\"Fiction\"],\"category\":\"Any\","
            + "\"title\":\"\",\"author\":\"\",\"book_id\":null,\"params_changed\":1,\"pages\":[{\"author\":\"Ann\",\"title\":\"Rules\",\"category\":\"Reference\",\"id\":12}]}");
        Assert.Contains("Rules", tree.AllText());
        Assert.Contains("Reference", tree.AllText());
        Assert.Equal("good", tree.Walk().First(n => n.Key == "search").Color);
        Assert.Equal("ID", tree.Walk().First(n => n.Key == "search_id").Tooltip);
        tree.Walk().First(n => n.Key == "search_id").OnCommit("12");
        tree.Walk().First(n => n.Key == "search_category").OnSelected("Fiction");
        tree.Walk().First(n => n.Key == "search_author").OnCommit("Ann");
        tree.Walk().First(n => n.Key == "clear_data").OnClick();
        tree.Walk().First(n => n.Key == "pages:first").OnClick();
        Assert.Equal(new[] { "set_search_id", "set_search_category", "set_search_author", "clear_data", "switch_page" }, acts.Select(a => a.Action));
        Assert.Contains("\"page\":1", acts[4].Params);
    }
}
