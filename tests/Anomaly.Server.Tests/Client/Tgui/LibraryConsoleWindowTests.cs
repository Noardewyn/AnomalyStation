using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's LibraryConsole window without the engine: the side menu of screens, the inventory with its page selector, the
/// check-out modal sending the picked book, the borrower and the period, the archive's no-connection notice, the upload screen with
/// its archive modal and the newscaster's cooldown, the print screen's posters and bible, and the forbidden lore's modal.
/// </summary>
public class LibraryConsoleWindowTests
{
    private const string Base = "\"can_db_request\":1,\"show_dropdown\":1,\"display_lore\":0,\"search_categories\":[\"Any\",\"Fiction\"],\"category\":\"Any\","
        + "\"author\":\"\",\"title\":\"\",\"book_id\":null,\"page_count\":1,\"our_page\":1,\"pages\":[],\"can_connect\":0,\"params_changed\":0";

    private static (UiNode Tree, List<(string Action, string Params)> Acts, TguiContext Context) Render(string json, TguiLocalState local = null)
    {
        local ??= new TguiLocalState();
        var acts = new List<(string, string)>();
        var c = new TguiContext(1, "LibraryConsole", "Library Terminal", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        UiNode tree = LibraryConsole.Render(c);
        Assert.Equal((880.0, 520.0, "Library Terminal"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        return (tree, acts, c);
    }

    private static UiNode Key(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheInventoryListsTheBooksAndTheMenuSwitchesScreens()
    {
        string json = "{" + Base + ",\"screen_state\":1,\"has_inventory\":1,\"inventory_page\":1,\"inventory_page_count\":2,"
            + "\"inventory\":[{\"id\":1,\"ref\":\"[0x1]\",\"title\":\"Space Law\",\"author\":\"Beepsky\"}]}";
        var (tree, acts, _) = Render(json);
        Assert.Contains("Library Inventory", tree.AllText());
        Assert.Contains("Space Law", tree.AllText());
        Assert.Equal("good", Key(tree, "screen:1").Color);
        Key(tree, "remove:[0x1]").OnClick();
        Key(tree, "screen:2").OnClick();
        Key(tree, "toggle_dropdown").OnClick();
        Key(tree, "inventory-pages:next").OnClick();
        Assert.Equal(new[] { "inventory_remove", "set_screen", "toggle_dropdown", "switch_inventory_page" }, acts.Select(a => a.Action));
        Assert.Contains("\"book_id\":\"[0x1]\"", acts[0].Params);
        Assert.Contains("\"screen_index\":2", acts[1].Params);
        Assert.Contains("\"page\":2", acts[3].Params);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "screen:6"); // no forbidden lore without the emag

        (tree, _, _) = Render("{" + Base + ",\"screen_state\":1,\"has_inventory\":0}");
        Assert.Contains("No Book Records detected. Update your inventory!", tree.AllText());
    }

    [Fact]
    public void TheCheckoutModalLoansTheBookOutForThePeriod()
    {
        string json = "{" + Base + ",\"screen_state\":2,\"has_checkout\":1,\"checkout_page\":1,\"checkout_page_count\":1,\"checkout_title\":\"Space Law\","
            + "\"inventory\":[{\"id\":1,\"ref\":\"[0x1]\",\"title\":\"Space Law\",\"author\":\"Beepsky\"}],"
            + "\"checkouts\":[{\"id\":1,\"ref\":\"[0x9]\",\"borrower\":\"Alice\",\"overdue\":1,\"due_in_minutes\":0,\"title\":\"Old\",\"author\":\"X\"}]}";
        var local = new TguiLocalState();
        var (tree, acts, _) = Render(json, local);
        Assert.Contains("Checked Out Books", tree.AllText());
        Assert.Equal(("Overdue", "bad"), (Key(tree, "due").Text, Key(tree, "due").Background));
        Key(tree, "checkin:[0x9]").OnClick();
        Assert.Contains("\"checked_out_id\":\"[0x9]\"", acts.Single().Params);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Modal);

        Key(tree, "check-out-book").OnClick();
        (tree, acts, _) = Render(json, local);
        UiNode modal = tree.Walk().Single(n => n.Kind == UiKind.Modal);
        UiNode dropdown = Key(modal, "book_name");
        Assert.Equal("Space Law", dropdown.TextValue);
        Assert.Equal(new[] { "Space Law" }, dropdown.Options.ToArray());
        dropdown.OnSelected("Space Law");
        Key(modal, "loaned_to").OnInput("Bob");
        Key(modal, "checkout_time").OnChange(15);
        (tree, _, _) = Render(json, local);
        acts.Clear();
        var c2 = new TguiContext(1, "LibraryConsole", "Library Terminal", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        tree = LibraryConsole.Render(c2);
        Key(tree, "loan-out").OnClick();
        Assert.Equal("checkout", acts.Single().Action);
        Assert.Contains("\"loaned_to\":\"Bob\"", acts.Single().Params);
        Assert.Contains("\"checkout_time\":15", acts.Single().Params);
        (tree, _, _) = Render(json, local);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Modal);
    }

    [Fact]
    public void TheArchiveWithoutAConnectionShowsTgsNotice()
    {
        var (tree, _, _) = Render("{" + Base + ",\"screen_state\":3}");
        Assert.Contains("Unable to retrieve book listings. Please contact your system administrator for assistance.", tree.AllText());

        string connected = ("{" + Base + ",\"screen_state\":3,\"pages\":[{\"author\":\"A\",\"title\":\"T\",\"category\":\"Fiction\",\"id\":7}]}").Replace("\"can_connect\":0", "\"can_connect\":1");
        var (tree2, acts, _) = Render(connected);
        Assert.Contains("T", tree2.AllText());
        Key(tree2, "print:7").OnClick();
        Key(tree2, "search_title").OnCommit("Law");
        Key(tree2, "search").OnClick();
        Assert.Equal(new[] { "print_book", "set_search_title", "search" }, acts.Select(a => a.Action));
        Assert.DoesNotContain(tree2.Walk(), n => n.Key == "archive-pages"); // a single page has no selector
    }

    [Fact]
    public void TheUploadScreenSendsTheCacheToTheNewscasterOrTheArchive()
    {
        var (tree, _, _) = Render("{" + Base + ",\"screen_state\":4,\"has_scanner\":0}");
        Assert.Contains("No nearby scanner detected, construct one to continue.", tree.AllText());
        (tree, _, _) = Render("{" + Base + ",\"screen_state\":4,\"has_scanner\":1,\"has_cache\":0}");
        Assert.Contains("Scan in a book to upload.", tree.AllText());

        string json = "{" + Base + ",\"screen_state\":4,\"has_scanner\":1,\"has_cache\":1,\"cache_title\":\"Tome\",\"cache_author\":\"Me\",\"cache_content\":\"Words\","
            + "\"upload_categories\":[\"Fiction\",\"Religion\"],\"default_category\":\"Fiction\",\"active_newscaster_cooldown\":0,\"cooldown_string\":\"8 seconds\"}";
        var local = new TguiLocalState();
        var (upload, acts, _) = Render(json, local);
        Assert.Equal("Words", Key(upload, "cache_content").TextValue);
        UiNode news = Key(upload, "news_post");
        Assert.True(news.Disabled);
        Assert.Equal("Please wait 8 seconds before sending your book to the newscaster!", news.Tooltip);
        Key(upload, "cache_title").OnCommit("Better Tome");
        Key(upload, "archive").OnClick();
        (upload, _, _) = Render(json, local);
        UiNode modal = upload.Walk().Single(n => n.Kind == UiKind.Modal);
        Key(modal, "upload_category").OnSelected("Religion");
        var acts2 = new List<(string, string)>();
        var c = new TguiContext(1, "LibraryConsole", "Library Terminal", 2, UiValue.FromJson(json), (a, p) => acts2.Add((a, p?.ToJson())), local);
        upload = LibraryConsole.Render(c);
        Key(upload, "upload").OnClick();
        Assert.Equal(("set_cache_title", "upload"), (acts.Single().Action, acts2.Single().Item1));
        Assert.Contains("\"category\":\"Religion\"", acts2.Single().Item2);
    }

    [Fact]
    public void ThePrintScreenPrintsTheSelectedPosterAndTheBible()
    {
        string json = "{" + Base + ",\"screen_state\":5,\"deity\":\"Space Jesus\",\"religion\":\"Christianity\",\"bible_name\":\"The Holy Book of Christianity\","
            + "\"bible_sprite\":\"display-bible\",\"posters\":[\"Cohiba Robusto\",\"Here For Your Safety\"]}";
        var local = new TguiLocalState();
        var (tree, acts, _) = Render(json, local);
        Assert.Contains("In the Name of Space Jesus", tree.AllText());
        Assert.Contains("For the Sake of Christianity", tree.AllText());
        UiNode sprite = Key(tree, "bible_sprite");
        Assert.Equal(("icons/obj/storage/book.dmi", "bible", "224px"), (sprite.DmIcon, sprite.DmIconState, (string)sprite.Width));
        Assert.True(Key(tree, "poster:Cohiba Robusto").Selected);
        Key(tree, "poster:Here For Your Safety").OnClick();
        var acts2 = new List<(string, string)>();
        var c = new TguiContext(1, "LibraryConsole", "Library Terminal", 2, UiValue.FromJson(json), (a, p) => acts2.Add((a, p?.ToJson())), local);
        tree = LibraryConsole.Render(c);
        Key(tree, "print_poster").OnClick();
        Key(tree, "print_bible").OnClick();
        Assert.Equal("print_poster", acts2[0].Item1);
        Assert.Contains("\"poster_name\":\"Here For Your Safety\"", acts2[0].Item2);
        Assert.Equal("print_bible", acts2[1].Item1);
    }

    [Fact]
    public void TheForbiddenLoreOffersItsChoice()
    {
        string json = ("{" + Base + ",\"screen_state\":6}").Replace("\"display_lore\":0", "\"display_lore\":1");
        var (tree, acts, c) = Render(json);
        Assert.Equal("spookyconsole", c.WindowTheme);
        Assert.Equal("Gur Fbeprere", Key(tree, "screen:6").Text);
        Assert.Contains("Accessing Forbidden Lore Vault v 1.3:", tree.AllText());
        Key(tree, "lore_spawn").OnClick();
        Key(tree, "lore_deny").OnClick();
        Assert.Equal(new[] { "lore_spawn", "lore_deny" }, acts.Select(a => a.Action));
    }
}
