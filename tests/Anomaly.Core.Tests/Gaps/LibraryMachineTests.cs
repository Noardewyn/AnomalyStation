using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The library's machines in a toy room (modules/library/lib_machines.dm, admin_only.dm): the scanner takes a book, scans it into
/// its cache with its flick and sound, ejects it and clears the cache (the LibraryScanner window); the book binder credits the ID it
/// scanned and binds a paper into a book after its drums warm up; the book management console takes books into its inventory from
/// a linked barcode scanner, loans them out and checks them back in, prints bibles and posters on its printer's cooldown, posts the
/// scanner's book to the newscasters' book club, and spawns the forbidden lore once emagged; the archive is tg's database, which the
/// port does not have, so the consoles take tg's no-connection paths. The DSM and IDC manuals list their entries.
/// </summary>
public class LibraryMachineTests
{
    private sealed class FlickRecorder : IAppearanceSink
    {
        public readonly List<(object Target, string State)> Flicks = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) { }

        public void Flick(object target, object state) => Flicks.Add((target, Dm.str(state)));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    private sealed class Library
    {
        public AudioRoom R;
        public Feed Feed;
        public FlickRecorder Flicks;
        public mob_living_carbon_human Bob;
        public obj_machinery_computer_libraryconsole_bookmanagement Console;
        public obj_machinery_libraryscanner Scanner;
        public obj_machinery_bookbinder Binder;
    }

    private static Library Room(ulong seed)
    {
        var lib = new Library();
        lib.R = new AudioRoom(13, 11, seed: seed, mapExtras: w =>
        {
            lib.Console = Dm.@new<obj_machinery_computer_libraryconsole_bookmanagement>(locate(3, 5, 1));
            lib.Scanner = Dm.@new<obj_machinery_libraryscanner>(locate(5, 6, 1));
            lib.Binder = Dm.@new<obj_machinery_bookbinder>(locate(5, 5, 1));
        });
        lib.Feed = Listen(lib.R);
        lib.Flicks = new FlickRecorder();
        PresentationSinks.Of(lib.R.World).Appearance = lib.Flicks;
        lib.Bob = lib.R.Join(4, 5, "Bob");
        lib.R.Step(Second);
        return lib;
    }

    private static T Hold<T>(Library lib, Func<T> make) where T : obj_item
    {
        T item = lib.R.In(() =>
        {
            T made = make();
            foreach (obj_item held in Dm.each<obj_item>(lib.Bob.held_items).ToList())
                Waiting.NoSleep(lib.Bob.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(lib.Bob.put_in_active_hand(made))));
            return made;
        });
        lib.R.Step(2);
        return item;
    }

    private static void EmptyHands(Library lib)
    {
        lib.R.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(lib.Bob.held_items).ToList())
                Waiting.NoSleep(lib.Bob.dropItemToGround(held));
        });
        lib.R.Step(2);
    }

    private static obj_item_book Book(string title, string author, string content) =>
        Dm.@new<obj_item_book>(locate(4, 4, 1)) is var book && SetBook(book, title, author, content) ? book : null;

    private static bool SetBook(obj_item_book book, string title, string author, string content)
    {
        book.book_data.set_title(title, trusted: TRUE);
        book.book_data.set_author(author, trusted: TRUE);
        book.book_data.set_content(content, trusted: TRUE);
        return true;
    }

    private static JsonElement Open(Library lib, atom machine, out string windowId)
    {
        EmptyHands(lib);
        Take(lib.Bob);
        Click(lib.R, lib.Bob, machine);
        lib.R.Step(4);
        JsonElement payload = LastFullUpdate(Take(lib.Bob), out windowId);
        return payload;
    }

    [Fact]
    public void TheScannerTakesABookScansItWithItsFlickAndEjectsIt()
    {
        var lib = Room(14101);
        var book = Hold(lib, () => Book("Space Law", "Beepsky", "Stop right there."));
        Click(lib.R, lib.Bob, lib.Scanner);
        lib.R.Step(2);
        Assert.Same(lib.Scanner, lib.R.In(() => book.loc));
        Assert.Same(book, lib.R.In(() => lib.Scanner.held_book));
        Assert.Contains(lib.R.Sounds.Heard, h => h.File == "sound/machines/eject.ogg");

        JsonElement payload = Open(lib, lib.Scanner, out string window);
        Assert.Equal("LibraryScanner", InterfaceOf(payload));
        Assert.Equal(1, payload.GetProperty("data").GetProperty("has_book").GetInt32());
        Assert.Equal(0, payload.GetProperty("data").GetProperty("has_cache").GetInt32());

        Act(lib.R, lib.Bob, window, "scan");
        JsonElement data = Data(lib.R, lib.Bob, out _);
        Assert.Equal(1, data.GetProperty("has_cache").GetInt32());
        Assert.Equal("Space Law", data.GetProperty("book").GetProperty("title").GetString());
        Assert.Equal("Beepsky", data.GetProperty("book").GetProperty("author").GetString());
        Assert.Contains(lib.Flicks.Flicks, f => ReferenceEquals(f.Target, lib.Scanner) && f.State == "bigscanner1");
        Assert.Contains(lib.R.Sounds.Heard, h => h.File == "sound/machines/scanner/scanner.ogg");
        Assert.NotSame(book.book_data, lib.R.In(() => lib.Scanner.cache)); // a copy

        // the same book again: tg says it is cached already
        Act(lib.R, lib.Bob, window, "scan");
        lib.R.Step(2);
        Assert.True(lib.Feed.Any("This book is already in my internal cache"));

        Act(lib.R, lib.Bob, window, "eject");
        lib.R.Step(2);
        Assert.True(lib.R.In(() => Dm.@in(book, lib.Bob.held_items)), "the book is back in his hand");
        Assert.Null(lib.R.In(() => lib.Scanner.held_book));

        Act(lib.R, lib.Bob, window, "clear");
        lib.R.Step(2);
        Assert.Null(lib.R.In(() => lib.Scanner.cache));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheScannerSwapsABookAndShowsItsOpenPanel()
    {
        var lib = Room(14102);
        var first = Hold(lib, () => Book("First", "A", "a"));
        Click(lib.R, lib.Bob, lib.Scanner);
        var second = Hold(lib, () => Book("Second", "B", "b"));
        Click(lib.R, lib.Bob, lib.Scanner);
        lib.R.Step(2);
        Assert.Same(second, lib.R.In(() => lib.Scanner.held_book));
        Assert.True(lib.R.In(() => Dm.@in(first, lib.Bob.held_items)), "the first book comes back to his hands");

        // the screwdriver opens the panel: bigscanner2
        lib.R.In(() =>
        {
            lib.Scanner.panel_open = TRUE;
            lib.Scanner.update_appearance();
        });
        Assert.Equal("bigscanner2", lib.R.In(() => lib.Scanner.icon_state));
        lib.R.In(() =>
        {
            lib.Binder.panel_open = TRUE;
            lib.Binder.update_appearance();
        });
        Assert.Equal("binder2", lib.R.In(() => lib.Binder.icon_state));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBinderCreditsTheScannedIdAndBindsAPaperIntoABook()
    {
        var lib = Room(14103);
        var id = Hold(lib, () =>
        {
            var card = Dm.@new<obj_item_card_id>(locate(4, 4, 1));
            card.registered_name = "Bob Curator";
            return card;
        });
        Click(lib.R, lib.Bob, lib.Binder);
        lib.R.Step(2);
        Assert.True(Dm.eq(lib.R.In(() => lib.Binder.scanned_name), "Bob Curator"));
        Assert.True(lib.Feed.Any("balloon: scanned"));

        var paper = Hold(lib, () =>
        {
            var sheet = Dm.@new<obj_item_paper>(locate(4, 4, 1));
            sheet.add_raw_text("Chapter one: the station.");
            return sheet;
        });
        Click(lib.R, lib.Bob, lib.Binder);
        lib.R.Step(2);
        Assert.Same(lib.Binder, lib.R.In(() => paper.loc));
        Assert.True(Dm.truthy(lib.R.In(() => lib.Binder.busy)));
        Assert.Contains(lib.Flicks.Flicks, f => ReferenceEquals(f.Target, lib.Binder) && f.State == "binder1");
        Assert.Contains(lib.R.Sounds.Heard, h => h.File == "sound/machines/printer.ogg");
        Assert.True(lib.Feed.Any("begins to hum as it warms up its printing drums."));

        // tg's 4.1 seconds later
        lib.R.Step(5 * Second);
        var bound = lib.R.In(() => Dm.each<obj_item_book>(lib.Binder.loc).Single(b => b.GetType() == typeof(obj_item_book)));
        Assert.StartsWith("Print Job #", lib.R.In(() => bound.name));
        Assert.True(Dm.eq(lib.R.In(() => bound.book_data.author), "Bob Curator"));
        Assert.Contains("Chapter one: the station.", lib.R.In(() => Dm.str(bound.book_data.content)));
        Assert.True(lib.R.In(() => QDELETED(paper)));
        Assert.Null(lib.R.In(() => lib.Binder.scanned_name));
        Assert.False(Dm.truthy(lib.R.In(() => lib.Binder.busy)));
        Assert.True(lib.Feed.Any("whirs as it prints and binds a new book."));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutAnIdTheBinderAsksOnceThenCreditsAnUnknownAuthor()
    {
        var lib = Room(14104);
        var paper = Hold(lib, () =>
        {
            var sheet = Dm.@new<obj_item_paper>(locate(4, 4, 1));
            sheet.add_raw_text("Anonymous.");
            return sheet;
        });
        Click(lib.R, lib.Bob, lib.Binder);
        lib.R.Step(2);
        Assert.True(lib.Feed.Any("No ID detected."));
        Assert.True(lib.R.In(() => Dm.@in(paper, lib.Bob.held_items)), "the paper stays in his hand");
        Click(lib.R, lib.Bob, lib.Binder);
        lib.R.Step(5 * Second);
        var bound = lib.R.In(() => Dm.each<obj_item_book>(lib.Binder.loc).Single(b => b.GetType() == typeof(obj_item_book)));
        Assert.True(Dm.eq(lib.R.In(() => bound.book_data.author), "unknown author"));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void ABarcodeScannerFillsTheInventoryAndALoanIsCheckedOutAndBackIn()
    {
        var lib = Room(14105);
        var barcode = Hold(lib, () => Dm.@new<obj_item_barcodescanner>(locate(4, 4, 1)));
        Click(lib.R, lib.Bob, lib.Console);
        lib.R.Step(2);
        Assert.Same(lib.Console, lib.R.In(() => barcode.computer_ref?.resolve()));
        Assert.True(lib.Feed.Any("balloon: scanner connected"));
        Click(lib.R, lib.Bob, lib.Console);
        Assert.True(lib.Feed.Any("balloon: already connected!"));

        // check-in, check-out, then the inventory mode
        lib.R.In(() => Waiting.NoSleep(barcode.attack_self(lib.Bob)));
        lib.R.In(() => Waiting.NoSleep(barcode.attack_self(lib.Bob)));
        Assert.Equal("inventory", lib.R.In(() => barcode.scan_mode));
        var book = lib.R.In(() => Book("Robust Librarianship", "Alice", "Shh."));
        lib.R.Step(2);
        Click(lib.R, lib.Bob, book);
        Assert.True(lib.Feed.Any("balloon: added to inventory"));
        Assert.Equal(1, lib.R.In(() => length(lib.Console.inventory)));

        JsonElement payload = Open(lib, lib.Console, out string window);
        Assert.Equal("LibraryConsole", InterfaceOf(payload));
        JsonElement inventory = payload.GetProperty("static_data").GetProperty("inventory");
        Assert.Equal("Robust Librarianship", inventory[0].GetProperty("title").GetString());
        Assert.Equal(1, payload.GetProperty("static_data").GetProperty("has_inventory").GetInt32());

        // the check-out screen: the book picked by name, loaned to Alice for 10 minutes
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 2));
        Act(lib.R, lib.Bob, window, "set_checkout", ("book_name", "Robust Librarianship"));
        Act(lib.R, lib.Bob, window, "checkout", ("loaned_to", "Alice"), ("checkout_time", 10));
        JsonElement data = Data(lib.R, lib.Bob, out _);
        JsonElement loan = data.GetProperty("checkouts")[0];
        Assert.Equal("Alice", loan.GetProperty("borrower").GetString());
        Assert.Equal(10, loan.GetProperty("due_in_minutes").GetDouble());
        Assert.Equal(0, loan.GetProperty("overdue").GetInt32());
        Assert.Equal("Robust Librarianship", data.GetProperty("checkout_title").GetString());

        // the barcode scanner checks it back in
        Hold(lib, () => barcode);
        lib.R.In(() => Waiting.NoSleep(barcode.attack_self(lib.Bob))); // inventory -> check-in
        Assert.Equal("check_in", lib.R.In(() => barcode.scan_mode));
        Click(lib.R, lib.Bob, book);
        Assert.True(lib.Feed.Any("balloon: checked in"));
        Assert.Equal(0, lib.R.In(() => length(lib.Console.checkouts)));
        Assert.Contains(lib.R.Sounds.Heard, h => h.File == "sound/items/barcodebeep.ogg");
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void ALoanRunsOverdueAndTheWindowsCheckInClearsIt()
    {
        var lib = Room(14106);
        lib.R.In(() =>
        {
            var info = Dm.@new<datum_book_info>("Overdue", "Late", "...");
            lib.Console.inventory[@ref(info)] = info;
            lib.Console.inventory_update();
        });
        JsonElement payload = Open(lib, lib.Console, out string window);
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 2));
        Act(lib.R, lib.Bob, window, "set_checkout", ("book_name", "Overdue"));
        Act(lib.R, lib.Bob, window, "checkout", ("loaned_to", "Carol"), ("checkout_time", 0)); // max(0, 1): one minute
        lib.R.Step(61 * Second);
        JsonElement data = Data(lib.R, lib.Bob, out _);
        JsonElement loan = data.GetProperty("checkouts")[0];
        Assert.Equal(1, loan.GetProperty("overdue").GetInt32());
        Act(lib.R, lib.Bob, window, "checkin", ("checked_out_id", loan.GetProperty("ref").GetString()));
        data = Data(lib.R, lib.Bob, out _);
        Assert.Equal(0, data.GetProperty("has_checkout").GetInt32());
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheConsolePrintsABibleAndAPosterOnItsPrintersCooldown()
    {
        var lib = Room(14107);
        JsonElement payload = Open(lib, lib.Console, out string window);
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 5));
        JsonElement data = Data(lib.R, lib.Bob, out _);
        Assert.Equal("Space Jesus", data.GetProperty("deity").GetString());
        Assert.Equal("Christianity", data.GetProperty("religion").GetString());
        Assert.Equal("The Holy Book of Christianity", data.GetProperty("bible_name").GetString());
        Assert.Equal("display-bible", data.GetProperty("bible_sprite").GetString());
        string poster = data.GetProperty("posters")[0].GetString();

        Act(lib.R, lib.Bob, window, "print_bible");
        Assert.Contains(lib.R.Sounds.Heard, h => h.File == "sound/machines/printer.ogg");
        Assert.Empty(lib.R.In(() => Dm.each<obj_item_book_bible>(lib.Console.loc).ToList()));
        lib.R.Step(5 * Second);
        Assert.Single(lib.R.In(() => Dm.each<obj_item_book_bible>(lib.Console.loc).ToList()));

        // within the six seconds of the printer's cooldown the poster waits
        Act(lib.R, lib.Bob, window, "print_poster", ("poster_name", poster));
        lib.R.Step(2);
        Assert.True(lib.Feed.Any("Printer currently unavailable, please wait a moment."));
        lib.R.Step(2 * Second);
        Act(lib.R, lib.Bob, window, "print_poster", ("poster_name", poster));
        lib.R.Step(5 * Second);
        var printed = lib.R.In(() => Dm.each<obj_item_poster>(lib.Console.loc).Single());
        Assert.NotNull(lib.R.In(() => printed.poster_structure));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutADatabaseTheArchiveCannotBeReachedAndTheUploadIsSevered()
    {
        var lib = Room(14108);
        Assert.False(Dm.truthy(lib.R.In(() => SSdbcore.Connect())));
        JsonElement payload = Open(lib, lib.Console, out string window);
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 3));
        Act(lib.R, lib.Bob, window, "search");
        JsonElement data = Data(lib.R, lib.Bob, out _);
        Assert.Equal(0, data.GetProperty("can_connect").GetInt32());
        Assert.Equal(1, data.GetProperty("page_count").GetInt32());
        Assert.Equal(0, data.GetProperty("pages").GetArrayLength());
        Assert.Equal("Any", data.GetProperty("category").GetString());

        // a second request within the second of the cooldown: the cables refresh
        Act(lib.R, lib.Bob, window, "search");
        Act(lib.R, lib.Bob, window, "search");
        lib.R.Step(2);
        Assert.True(lib.Feed.Any("Database cables refreshing. Please wait a moment."));

        // the upload screen with the scanner's cached book: the archive is severed
        lib.R.In(() => lib.Scanner.cache = Dm.@new<datum_book_info>("Cached", "Bob", "Words."));
        lib.R.Step(Second);
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 4));
        data = Data(lib.R, lib.Bob, out _);
        Assert.Equal(1, data.GetProperty("has_scanner").GetInt32());
        Assert.Equal("Cached", data.GetProperty("cache_title").GetString());
        Act(lib.R, lib.Bob, window, "upload", ("category", "Fiction"));
        lib.R.Step(2);
        Assert.True(lib.Feed.Any("Connection to Archive has been severed. Aborting."));

        // the archive's print: severed too, after the printer's run
        int before = lib.Feed.Chat.Count(c => c.Html != null && c.Html.Contains("Connection to Archive has been severed", StringComparison.Ordinal));
        lib.R.Step(2 * Second);
        Act(lib.R, lib.Bob, window, "print_book", ("book_id", 1));
        lib.R.Step(5 * Second);
        Assert.True(lib.Feed.Chat.Count(c => c.Html != null && c.Html.Contains("Connection to Archive has been severed", StringComparison.Ordinal)) > before);
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheVisitorConsoleShowsItsNoConnectionStateAndTheAdminConsoleCloses()
    {
        var lib = Room(14109);
        var visitor = lib.R.In(() => Dm.@new<obj_machinery_computer_libraryconsole>(locate(4, 6, 1)));
        lib.R.Step(Second);
        JsonElement payload = Open(lib, visitor, out string window);
        Assert.Equal("LibraryVisitor", InterfaceOf(payload));
        Assert.Equal(0, payload.GetProperty("data").GetProperty("can_connect").GetInt32());
        Act(lib.R, lib.Bob, window, "set_search_title", ("title", "Robust"));
        Act(lib.R, lib.Bob, window, "set_search_category", ("category", "Nonsense"));
        JsonElement data = Data(lib.R, lib.Bob, out _);
        Assert.Equal("Robust", data.GetProperty("title").GetString());
        Assert.Equal("Any", data.GetProperty("category").GetString()); // nice try
        Assert.Equal(1, data.GetProperty("params_changed").GetInt32());

        // the admins' console: without rights (and without the database) its window closes
        var admin = lib.R.In(() => Dm.@new<obj_machinery_computer_libraryconsole_admin_only_do_not_map_in_you_fucker>(locate(5, 4, 1)));
        lib.R.Step(Second);
        EmptyHands(lib);
        Take(lib.Bob);
        Click(lib.R, lib.Bob, admin);
        lib.R.Step(4);
        Assert.Equal(UI_CLOSE, lib.R.In(() => Dm.num(Waiting.NoSleep(admin.ui_status(lib.Bob, null)))));
        Assert.Null(lib.R.In(() => SStgui.get_open_ui(lib.Bob, admin)));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBoardSwitchesBetweenTheTwoConsoles()
    {
        var lib = Room(14110);
        var board = lib.R.In(() => Dm.@new<obj_item_circuitboard_computer_libraryconsole>(locate(4, 4, 1)));
        lib.R.In(() => Waiting.NoSleep(board.screwdriver_act(lib.Bob, null)));
        Assert.Equal(typeof(obj_machinery_computer_libraryconsole_bookmanagement), lib.R.In(() => board.build_path));
        Assert.Equal("Book Inventory Management Console", lib.R.In(() => board.name));
        lib.R.In(() => Waiting.NoSleep(board.screwdriver_act(lib.Bob, null)));
        Assert.Equal(typeof(obj_machinery_computer_libraryconsole), lib.R.In(() => board.build_path));
        Assert.True(lib.Feed.Any("Defaulting access protocols."));
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmaggedConsoleOffersTheForbiddenLoreOnce()
    {
        var lib = Room(14111);
        float age = lib.R.In(() => Dm.num(lib.Bob.age));
        lib.R.In(() => Waiting.NoSleep(lib.Console.emag_act(lib.Bob, null)));
        Assert.True(lib.Feed.Any("balloon: forbidden knowledge unlocked"));
        JsonElement payload = Open(lib, lib.Console, out string window);
        Assert.Equal(1, payload.GetProperty("data").GetProperty("display_lore").GetInt32());
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 6));
        Act(lib.R, lib.Bob, window, "lore_spawn");
        lib.R.Step(2);
        Assert.Single(lib.R.In(() => Dm.each<obj_item_melee_cultblade_dagger>(lib.Console.loc).ToList()));
        Assert.Equal(age + 10, lib.R.In(() => Dm.num(lib.Bob.age)));
        Assert.Equal(1f, lib.R.In(() => lib.Console.screen_state));
        Assert.True(lib.Feed.Any("a sinister dagger sitting on the desk"));
        JsonElement data = Data(lib.R, lib.Bob, out _);
        Assert.Equal(0, data.GetProperty("display_lore").GetInt32());
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheScannersBookGoesToTheNewscastersBookClub()
    {
        var lib = Room(14112);
        lib.R.In(() => lib.Scanner.cache = Dm.@new<datum_book_info>("Club Pick", "Bob", "A fine read."));
        JsonElement payload = Open(lib, lib.Console, out string window);
        Act(lib.R, lib.Bob, window, "set_screen", ("screen_index", 4));
        Act(lib.R, lib.Bob, window, "news_post");
        lib.R.Step(2);
        Assert.True(lib.Feed.Any("Upload complete. Your uploaded title is now available on station newscasters."));
        var channel = lib.R.In(() => GLOB.news_network.network_channels_by_name["Nanotrasen Book Club"] as datum_feed_channel);
        Assert.NotNull(channel);
        Assert.Contains(lib.R.In(() => Dm.each<datum_feed_message>(channel.messages).Select(m => Dm.str(m.author)).ToList()), a => a == "Bob: Club Pick");
        JsonElement data = Data(lib.R, lib.Bob, out _);
        Assert.Equal(0, data.GetProperty("active_newscaster_cooldown").GetInt32());
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheDsmAndIdcManualsListTheirEntries()
    {
        var lib = Room(14113);
        var dsm = Hold(lib, () => Dm.@new<obj_item_tgui_book_manual_dsm>(locate(4, 4, 1)));
        Take(lib.Bob);
        lib.R.In(() => Waiting.NoSleep(dsm.attack_self(lib.Bob)));
        lib.R.Step(4);
        JsonElement payload = LastFullUpdate(Take(lib.Bob), out _);
        Assert.Equal("DSMBook", InterfaceOf(payload));
        JsonElement traumas = payload.GetProperty("static_data").GetProperty("traumas");
        Assert.True(traumas.GetArrayLength() > 20, traumas.GetArrayLength() + " traumas");
        Assert.Contains(traumas.EnumerateArray(), t => t.GetProperty("full_name").GetString().EndsWith("Addiction", StringComparison.Ordinal));
        Assert.Contains(traumas.EnumerateArray(), t => t.GetProperty("scan_name").GetString().StartsWith("phobia of", StringComparison.Ordinal)
            || t.GetProperty("scan_name").GetString().Contains(" of ", StringComparison.Ordinal));

        var idc = Hold(lib, () => Dm.@new<obj_item_tgui_book_manual_idc>(locate(4, 4, 1)));
        Take(lib.Bob);
        lib.R.In(() => Waiting.NoSleep(idc.attack_self(lib.Bob)));
        lib.R.Step(4);
        payload = LastFullUpdate(Take(lib.Bob), out _);
        Assert.Equal("IDCBook", InterfaceOf(payload));
        JsonElement diseases = payload.GetProperty("static_data").GetProperty("diseases");
        Assert.True(diseases.GetArrayLength() > 20, diseases.GetArrayLength() + " diseases");
        Assert.Contains(diseases.EnumerateArray(), d => d.GetProperty("form").GetString() == "Symptom");
        Assert.Contains(diseases.EnumerateArray(), d => d.GetProperty("name").GetString() == "Advanced Disease");
        lib.R.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewlyLiveLibraryTypeIsMadeAndDeletedCleanly()
    {
        var lib = Room(14114);
        foreach (Type type in new[]
        {
            typeof(obj_machinery_computer_libraryconsole), typeof(obj_machinery_computer_libraryconsole_bookmanagement),
            typeof(obj_machinery_computer_libraryconsole_admin_only_do_not_map_in_you_fucker), typeof(obj_machinery_libraryscanner),
            typeof(obj_machinery_bookbinder), typeof(obj_item_circuitboard_computer_libraryconsole), typeof(obj_item_circuitboard_computer_libraryconsole_bookconsole),
            typeof(obj_item_tgui_book_manual_dsm), typeof(obj_item_tgui_book_manual_idc), typeof(obj_effect_forcefield_wizard),
        })
        {
            atom made = lib.R.In(() => Dm.@new(type, locate(9, 8, 1)) as atom);
            lib.R.Step(2);
            Assert.False(lib.R.In(() => InertSeam.IsInert(made)), type.Name + " is inert");
            lib.R.In(() => qdel(made));
            lib.R.Step(2);
            Assert.True(lib.R.In(() => QDELETED(made)), type.Name);
        }
        foreach (Type type in new[] { typeof(datum_book_history_entry), typeof(datum_admin_book_viewer), typeof(datum_borrowbook) })
        {
            datum made = lib.R.In(() => Dm.@new(type) as datum);
            lib.R.In(() => qdel(made));
            lib.R.Step(1);
            Assert.True(lib.R.In(() => QDELETED(made)), type.Name);
        }
        lib.R.AssertNoRuntimes();
    }
}
