using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The library through the real server with a headless client: a joined curator puts a book in the scanner, opens its window and
/// scans it; her ID and a written paper go into the book binder, which binds the paper into a book credited to her; her barcode
/// scanner links to the book management console and adds the bound book to its inventory; in the console's window she loans the
/// book out from the check-out screen, and the barcode scanner checks it back in. Her client draws each window from the server's data.
/// </summary>
public class LibraryGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static void Hold(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item) => Do(w, () =>
    {
        foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
            Waiting.NoSleep(me.dropItemToGround(held));
        if (item != null)
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(item))));
    });

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [Fact]
    public void A_curator_scans_and_binds_a_book_and_checks_one_out_at_the_console()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14140, TimeoutSteps = 8000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        ReplicaWorld replica = alice.Session.Replica;
        var heard = new List<string>();
        alice.Client.ServerTextReceived += t => heard.Add(t.Html ?? "");
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second);

        // the machines around her
        turf here = In(w, () => me.loc as turf);
        var free = In(w, () => new[] { EAST, WEST, NORTH, SOUTH }.Select(d => get_step(me, d) as turf)
            .Where(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density))).ToList());
        Assert.True(free.Count >= 3, "three free tiles around her");
        var scanner = In(w, () => Dm.@new<obj_machinery_libraryscanner>(free[0]));
        var binder = In(w, () => Dm.@new<obj_machinery_bookbinder>(free[1]));
        var console = In(w, () => Dm.@new<obj_machinery_computer_libraryconsole_bookmanagement>(free[2]));
        w.Run(Second);

        // the scanner: the book goes in, her empty hand opens its window, Scan Book caches it
        var book = In(w, () =>
        {
            var made = Dm.@new<obj_item_book>(here);
            made.book_data.set_title("Robust Librarianship", trusted: TRUE);
            made.book_data.set_author("Alice", trusted: TRUE);
            made.book_data.set_content("Quiet, please.", trusted: TRUE);
            return made;
        });
        Hold(w, me, book);
        Assert.True(alice.Session.Click(On(scanner), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Same(scanner, In(w, () => book.loc));
        Hold(w, me, null);
        Assert.True(alice.Session.Click(On(scanner), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("LibraryScanner", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "scan" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.NotNull(In(w, () => scanner.cache));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Robust Librarianship", content.Walk().Single(n => n.Key == "title").Text);
        content.Walk().Single(n => n.Key == "eject" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.@in(book, me.held_items)), "the book is back in her hand");
        Assert.True(alice.Session.UiClose(window.Window));
        w.Run(4);

        // the binder: her ID, then a written paper; four seconds later a bound book credited to her
        var id = In(w, () =>
        {
            var card = Dm.@new<obj_item_card_id>(here);
            card.registered_name = "Alice Curator";
            return card;
        });
        Hold(w, me, id);
        Assert.True(alice.Session.Click(On(binder), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        var paper = In(w, () =>
        {
            var sheet = Dm.@new<obj_item_paper>(here);
            sheet.add_raw_text("The second edition.");
            return sheet;
        });
        Hold(w, me, paper);
        Assert.True(alice.Session.Click(On(binder), ClickButton.Left, ClickModifiers.None));
        w.Run(5 * Second);
        var bound = In(w, () => Dm.each<obj_item_book>(binder.loc).SingleOrDefault(b => b.GetType() == typeof(obj_item_book)));
        Assert.NotNull(bound);
        Assert.True(In(w, () => Dm.eq(bound.book_data.author, "Alice Curator")));
        Assert.Contains(heard, h => h.Contains("whirs as it prints and binds a new book.", StringComparison.Ordinal));
        Assert.True(alice.Session.Replica.TryGet(In(w, () => bound.dm_id), out _), "the bound book is in her replica");
        // a fresh print has no title (bind_book names the item only); she titles it, as her pen would
        Do(w, () => bound.book_data.set_title("The Second Edition", trusted: TRUE));

        // the barcode scanner: linked to the console, then switched to its inventory mode and the bound book added
        var barcode = In(w, () => Dm.@new<obj_item_barcodescanner>(here));
        Hold(w, me, barcode);
        Assert.True(alice.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Same(console, In(w, () => barcode.computer_ref?.resolve()));
        Do(w, () =>
        {
            Waiting.NoSleep(barcode.attack_self(me));
            Waiting.NoSleep(barcode.attack_self(me));
        });
        Assert.True(alice.Session.Click(On(bound), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Equal(1, In(w, () => length(console.inventory)));

        // the console's window: the inventory lists it; the check-out screen loans it out to Bob for 20 minutes
        Hold(w, me, null);
        Assert.True(alice.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("LibraryConsole", window.Interface);
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Library Inventory", content.AllText());
        content.Walk().Single(n => n.Key == "screen:2").OnClick();
        w.Run(4);
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "check-out-book").OnClick();
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "book_name").OnSelected(In(w, () => Dm.str(bound.book_data.title)));
        w.Run(4);
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "loaned_to").OnInput("Bob");
        content.Walk().Single(n => n.Key == "checkout_time").OnChange(20);
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "loan-out").OnClick();
        w.Run(Second / 2);
        var loan = In(w, () => console.checkouts[Dm.each(console.checkouts).Single()] as datum_borrowbook);
        Assert.Equal("Bob", In(w, () => Dm.str(loan.loanedto)));
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Bob", content.AllText());
        Assert.Contains("20 Minutes", content.AllText());

        // the barcode scanner checks it back in
        Hold(w, me, barcode);
        Do(w, () => Waiting.NoSleep(barcode.attack_self(me)));
        Assert.True(alice.Session.Click(On(bound), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Equal(0, In(w, () => length(console.checkouts)));
        // with no loans left the console sends no checkouts list, and tgui merges an update into the data it has (update.ts), so the
        // open window keeps the last list as tg's does; reopened, it starts from fresh data
        Assert.True(alice.Session.UiClose(window.Window));
        w.Run(4);
        Hold(w, me, null);
        Assert.True(alice.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        window = Assert.Single(replica.UiWindows.Tgui());
        content = Shown(windows, replica, window.Window);
        Assert.Contains("No books checked out.", content.AllText());
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(20)));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
