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
/// Gaps package SK1 on the converted MetaStation: the mapped cardboard and cloth (and any other stack of SK1's files a locker or crate
/// holds) boot live with their recipe lists, with no runtime from SK1's files.
/// </summary>
public class StacksSK1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public StacksSK1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "sheets/leather", "sheets/mineral", "sheets/sheet_types", "sheets/light", "sheets/hot_ice", "stacks/wall_filling",
        "stacks/bscrystal", "stacks/tickets", "stacks/ammonia_crystals", "tiles/tile_types", "tiles/tile_mineral", "stacks/tape",
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
    public void Metas_sheets_boot_live_with_their_recipes()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, recipeless = 0;
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
                                obj_item_stack_sheet_cardboard => "cardboard",
                                obj_item_stack_sheet_cloth => "cloth",
                                obj_item_stack_sheet_leather => "leather",
                                obj_item_stack_sheet_animalhide => "hide",
                                obj_item_stack_sheet_bluespace_crystal => "bluespace polycrystal",
                                obj_item_stack_tile_mineral => "mineral tiles",
                                obj_item_stack_arcadeticket => "arcade tickets",
                                obj_item_stack_medical_wrap_sticky_tape_duct => "duct tape",
                                _ => null,
                            };
                            if (kind == null)
                                return;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                            if (thing is obj_item_stack_sheet_cardboard or obj_item_stack_sheet_cloth && Dm.each(((obj_item_stack)thing).get_main_recipes() as DmList).Count() == 0)
                                recipeless++;
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {recipeless} sheets without recipes; SK1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("cardboard") >= 3, "too few cardboard stacks");
            Assert.True(counts.GetValueOrDefault("cloth") >= 2, "too few cloth stacks");
            Assert.Equal(0, inert);
            Assert.Equal(0, recipeless);
            Assert.Empty(runtimes);
        }
    }
}
