using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package ST2 on the converted MetaStation: its curtains, bookcases, display cases, showcases, mannequin, tank holders, broken
/// flooring, basketball hoop, plaques, easels, notice boards and the theatre's curtain button boot live: the bookcases finished with
/// their books loaded by the library subsystem, the mechanical curtains and the trophy cases in their global lists, the captain's
/// laser in its case, the tank holders holding their tanks, with no runtime from their files.
/// </summary>
public class FurnitureST2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public FurnitureST2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "curtains", "bookcase", "random_books", "subsystem/library", "displaycase", "showcase", "mannequin", "tank_holder",
        "broken_flooring", "hoop", "basketball", "plaques", "doorcontrol", "burglaralert",
    };

    [MetaFact]
    public void Metas_furniture_and_odd_structures_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, unfinished = 0, emptyHolders = 0, books = 0, lasers = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        string kind = thing switch
                        {
                            obj_structure_curtain => "curtain",
                            obj_structure_bookcase => "bookcase",
                            obj_structure_displaycase => "display case",
                            obj_structure_showcase => "showcase",
                            obj_structure_mannequin => "mannequin",
                            obj_structure_tank_holder => "tank holder",
                            obj_structure_broken_flooring => "broken flooring",
                            obj_structure_hoop => "hoop",
                            obj_structure_plaque => "plaque",
                            obj_structure_easel => "easel",
                            obj_structure_noticeboard => "noticeboard",
                            obj_machinery_button_curtain => "curtain button",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                            inert++;
                        if (thing is obj_structure_bookcase bookcase)
                        {
                            if (bookcase.state != 2 || !Dm.truthy(bookcase.anchored))
                                unfinished++;
                            books += Dm.each<obj_item>(bookcase.contents).Count();
                        }
                        if (thing is obj_structure_tank_holder holder && holder.tank == null)
                            emptyHolders++;
                        if (thing is obj_structure_displaycase_captain { showpiece: obj_item_gun_energy_laser_captain })
                            lasers++;
                    }
                }
            }
            int mechanical = Dm.each(GLOB.curtains).Count();
            int trophies = Dm.each(GLOB.trophy_cases).Count();
            int areasWithBooks = SSlibrary.books_by_area == null ? 0 : Dm.each(SSlibrary.books_by_area).Count();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {unfinished} unfinished bookcases with {books} books, {areasWithBooks} areas with books, {mechanical} mechanical curtains,"
                + $" {trophies} trophy cases, {lasers} captain's lasers, {emptyHolders} empty tank holders; ST2 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("bookcase") >= 5, "too few bookcases");
            Assert.True(counts.GetValueOrDefault("curtain") >= 2, "too few curtains");
            Assert.True(counts.GetValueOrDefault("display case") >= 4, "too few display cases");
            Assert.True(counts.GetValueOrDefault("showcase") >= 5, "too few showcases");
            Assert.True(counts.GetValueOrDefault("easel") >= 1, "no easel");
            Assert.True(counts.GetValueOrDefault("noticeboard") >= 3, "too few notice boards");
            foreach (string one in new[] { "mannequin", "tank holder", "broken flooring", "hoop", "plaque", "curtain button" })
                Assert.True(counts.GetValueOrDefault(one) >= 1, "no " + one);
            Assert.Equal(0, inert);
            Assert.Equal(0, unfinished);
            Assert.True(books > 0, "the mapped bookcases hold no books");
            Assert.True(Dm.truthy(SSlibrary.initialized));
            Assert.Null(SSlibrary.shelves_to_load); // every mapped shelf was loaded
            Assert.True(areasWithBooks > 0, "the library lists no books by area");
            Assert.True(mechanical >= 1, "no mechanical curtain in GLOB.curtains");
            Assert.True(trophies >= 1, "no trophy case in GLOB.trophy_cases");
            Assert.Equal(1, lasers);
            Assert.Equal(0, emptyHolders);
            Assert.Empty(runtimes);
        }
    }
}
