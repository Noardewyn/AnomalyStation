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
/// Gaps package WP1 on the converted MetaStation: the cane, the captain's chain of command, the captain's sabre in its sheath (in
/// the captain's locker) and the fire axes in their cabinets boot live, with no runtime from WP1's files.
/// </summary>
public class WeaponryWP1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public WeaponryWP1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "melee/spear", "melee/chainsaw", "melee/sabre", "melee/katana", "melee/chainofcommand", "melee/baseball_bat",
        "melee/high_frequency_blade", "melee/supermatter_sword", "melee/baton", "melee/fireaxe", "melee/knives", "structures/headpike",
        "items/pillow", "basic/illusion", "grenades/ghettobomb", "items/shields", "elements/prosthetic_icon", "elements/kneecapping",
        "medical/cane",
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
    public void Metas_weaponry_boots_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, sheathless = 0;
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
                                obj_item_cane => "cane",
                                obj_item_melee_chainofcommand => "chain of command",
                                obj_item_melee_sabre => "sabre",
                                obj_item_fireaxe => "fire axe",
                                _ => null,
                            };
                            if (kind == null)
                                return;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                            if (thing is obj_item_melee_sabre && thing.loc is not obj_item_storage_belt_sheath)
                                sheathless++;
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {sheathless} sabres out of their sheath; WP1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("cane") >= 1, "no cane");
            Assert.True(counts.GetValueOrDefault("chain of command") >= 1, "no chain of command");
            Assert.True(counts.GetValueOrDefault("sabre") >= 1, "no captain's sabre");
            Assert.True(counts.GetValueOrDefault("fire axe") >= 2, "too few fire axes");
            Assert.Equal(0, inert);
            Assert.Equal(0, sheathless);
            Assert.Empty(runtimes);
        }
    }
}
