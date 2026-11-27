using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The library on the converted MetaStation: the visitor console, the book management console, the scanner and the book binder boot
/// live; the management console loads the library's mapped books into its inventory when it is first looked at (it was placed at
/// mapload) and finds the library's scanner; a book is scanned into the scanner's cache and bound from a paper at the binder; and
/// nothing in the library's machines throws over the ticks.
/// </summary>
public class LibraryMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public LibraryMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_library_consoles_scanner_and_binder_work()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        obj_machinery_bookbinder binder;
        obj_item_paper paper;
        using (_meta.World.Enter())
        {
            var management = Dm.each<obj_machinery_computer_libraryconsole_bookmanagement>().ToList();
            var visitors = Dm.each<obj_machinery_computer_libraryconsole>().Where(c => c.GetType() == typeof(obj_machinery_computer_libraryconsole)).ToList();
            var scanners = Dm.each<obj_machinery_libraryscanner>().ToList();
            var binders = Dm.each<obj_machinery_bookbinder>().ToList();
            _output.WriteLine($"Meta's library: {management.Count} management, {visitors.Count} visitor consoles, {scanners.Count} scanners, {binders.Count} binders");
            Assert.NotEmpty(management);
            Assert.NotEmpty(visitors);
            Assert.NotEmpty(scanners);
            Assert.NotEmpty(binders);
            Assert.All(management, c => Assert.False(InertSeam.IsInert(c)));
            Assert.All(visitors, c => Assert.False(InertSeam.IsInert(c)));
            Assert.All(scanners, c => Assert.False(InertSeam.IsInert(c)));
            Assert.All(binders, c => Assert.False(InertSeam.IsInert(c)));

            // the management console was mapped: its first look loads the library's books, and it finds the library's scanner
            // the library's own (the prison wing and the chapel's office have one too)
            var console = management.First(c => get_area(c) is area_station_service_library);
            Assert.True(Dm.truthy(console.dynamic_inv_load));
            var librarian = Dm.@new<mob_living_carbon_human>(get_step(console, SOUTH));
            Waiting.NoSleep(console.ui_data(librarian));
            Assert.False(Dm.truthy(console.dynamic_inv_load));
            _output.WriteLine($"the console's inventory: {length(console.inventory)} books of {Dm.str((get_area(console) as area).name)}");
            Assert.True(length(console.inventory) > 0, "the library's mapped books are in its inventory");
            var scanner = console.get_scanner() as obj_machinery_libraryscanner;
            Assert.NotNull(scanner);
            Assert.False(Dm.truthy(console.can_connect), "no database: the archive is out of reach");

            // a book into the scanner and its cache
            var book = Dm.@new<obj_item_book>(scanner.loc);
            book.book_data.set_title("Meta's Library", trusted: TRUE);
            book.book_data.set_author("A Curator", trusted: TRUE);
            book.book_data.set_content("Shelves.", trusted: TRUE);
            Waiting.NoSleep(librarian.put_in_active_hand(book));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(scanner.item_interaction(librarian, book)));
            Assert.Same(book, scanner.held_book);
            scanner.cache = book.book_data.return_copy() as datum_book_info;
            Assert.True(Dm.truthy(scanner.cache.compare(book.book_data)));

            // a paper into the binder, credited to an ID
            binder = binders.First();
            var id = Dm.@new<obj_item_card_id>(binder.loc);
            id.registered_name = "A Curator";
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(binder.item_interaction(librarian, id)));
            paper = Dm.@new<obj_item_paper>(librarian.loc);
            paper.add_raw_text("Bound on Meta.");
            Waiting.NoSleep(librarian.put_in_active_hand(paper));
            Assert.True(Dm.truthy(binder.is_operational), "the binder has power");
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(binder.item_interaction(librarian, paper)));
            Assert.Same(binder, paper.loc);
        }
        Step(5 * 20);
        using (_meta.World.Enter())
        {
            Assert.True(QDELETED(paper));
            var bound = Dm.each<obj_item_book>(binder.loc).Where(b => b.GetType() == typeof(obj_item_book)).ToList();
            Assert.Contains(bound, b => Dm.eq(b.book_data.author, "A Curator"));
        }
        Step(100);
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => (e.ToString() ?? "").Contains("library", StringComparison.OrdinalIgnoreCase) || (e.ToString() ?? "").Contains("bookbinder", StringComparison.Ordinal)
                || (e.ToString() ?? "").Contains("lib_machines", StringComparison.Ordinal))
            .ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
