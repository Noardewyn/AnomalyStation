using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package OD1 on the converted MetaStation: the mapped Space Law books (a traitor's steal target since they are live), the party
/// game poster, the crushed can and the boritos bag boot live, with no runtime from OD1's files.
/// </summary>
public class ItemsOD1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ItemsOD1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "items/wiki_manuals", "items/tail_pin", "items/trash", "items/puzzle_pieces", "items/cigarettes", "items/choice_beacon",
        "reagent_containers/inhaler", "reagent_containers/jerrycan", "gamemodes/objective_items",
    };

    private static void Walk(atom holder, Action<atom> visit)
    {
        foreach (object o in Dm.each(holder.contents))
        {
            if (o is not atom thing)
                continue;
            visit(thing);
            Walk(thing, visit);
        }
    }

    [MetaFact]
    public void Metas_odds_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0;
            for (int z = 1; z <= world.maxz; z++)
            {
                for (int y = 1; y <= world.maxy; y++)
                {
                    for (int x = 1; x <= world.maxx; x++)
                    {
                        turf t = locate(x, y, z);
                        if (t == null)
                            continue;
                        Walk(t, thing =>
                        {
                            string kind = thing switch
                            {
                                obj_item_book_manual_wiki_security_space_law => "space law",
                                obj_structure_sign_poster_party_game => "party poster",
                                obj_item_trash_can => "can",
                                obj_item_trash_boritos => "boritos",
                                _ => null,
                            };
                            if (kind == null)
                                return;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert; OD1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("space law") >= 10, "too few Space Law books");
            Assert.Equal(0, inert);
            Assert.Empty(runtimes);
        }
    }
}
