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
/// Gaps package GU1 on the converted MetaStation: every gun, firing pin, casing and magazine the map holds boots live (the armory's
/// and the security lockers' guns, the pneumatic grenade launcher, the syringe guns, the ion rifle, the temperature guns, the practice
/// lasers, the captain's antique laser in its display case with its lore), with no runtime from the gun files.
/// </summary>
public class GunsGU1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public GunsGU1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    // the C# files of the guns, their ammunition, projectiles and pins (a runtime's stack names them), and the two outside files GU1 touched
    private static readonly string[] Files = { "modules/projectiles/", "components/crank_recharge", "storage/lockbox" };

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

    // the examine_lore element (add_deep_lore) listens to the gun's closer look
    private static bool HasLore(atom thing)
    {
        object listeners = thing._listen_lookup?[Defines.COMSIG_ATOM_EXAMINE_MORE];
        return listeners is datum_element_examine_lore || (listeners is DmList list && Dm.each(list).Any(l => l is datum_element_examine_lore));
    }

    [MetaFact]
    public void Metas_guns_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            var inert = new List<string>();
            bool antiqueLore = false;
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
                                obj_item_gun_energy_laser_captain => "antique laser",
                                obj_item_gun_grenadelauncher => "grenade launcher",
                                obj_item_gun_energy => "energy gun",
                                obj_item_gun_ballistic => "ballistic gun",
                                obj_item_gun => "other gun",
                                obj_item_firing_pin => "firing pin",
                                obj_item_ammo_casing => "casing",
                                obj_item_ammo_box => "ammo box",
                                _ => null,
                            };
                            if (kind == null)
                                return;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert.Add(thing.GetType().Name);
                            if (thing is obj_item_gun_energy_laser_captain)
                                antiqueLore |= HasLore(thing);
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; inert {inert.Count} ({string.Join(", ", inert.Distinct())}); antique laser lore {antiqueLore}; GU1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
            {
                string first = group.First().ToString() ?? "";
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}\n    {(first.Length > 1500 ? first[..1500] : first)}");
            }
            Assert.True(counts.GetValueOrDefault("energy gun") >= 5, "too few energy guns");
            Assert.True(counts.GetValueOrDefault("ballistic gun") >= 1, "no ballistic gun");
            Assert.True(counts.GetValueOrDefault("grenade launcher") >= 1, "no grenade launcher");
            Assert.True(counts.GetValueOrDefault("antique laser") >= 1, "no antique laser");
            Assert.True(antiqueLore, "the antique laser has no lore hint");
            Assert.Empty(inert);
            Assert.Empty(runtimes);
        }
    }
}
