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
/// Gaps package ST5 on the converted MetaStation: the wooden chairs left lying about (items), the eye chart and the spawned
/// maintenance oddities (steam vents, moisture traps, pants altars) boot live, with no runtime from ST5's files.
/// </summary>
public class StructuresST5MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public StructuresST5MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "effects/mines", "mystery_box", "structures/maintenance", "tripwire", "structures/traps", "signboard", "holosign", "votingbox",
        "petrified_statue", "false_walls", "structures/fence", "guncase", "toiletbong", "signs/_signs", "sign_eyechart", "life_candle",
        "ore_containers", "structures/fans", "structures/chess", "billboard", "structures/containers", "outfit_wardrobe",
        "beds_chairs/chair", "structures/window", "tables_racks", "crates_lockers/closets", "fireaxe", "extinguisher", "displaycase",
        "structures/grille", "girders", "structures/lattice", "plasticflaps", "secure_safe", "table_frames", "structures/fluff",
        "destructible_structures", "cult_structures", "hunter_gear",
    };

    [MetaFact]
    public void Metas_st5_structures_boot_live()
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
                        foreach (atom thing in t.dm_held)
                        {
                            string kind = thing switch
                            {
                                obj_item_chair_wood => "wooden chair item",
                                obj_structure_sign_eyechart => "eye chart",
                                obj_structure_steam_vent => "steam vent",
                                obj_structure_moisture_trap => "moisture trap",
                                obj_structure_destructible_cult_pants_altar => "pants altar",
                                obj_effect_mine => "mine",
                                obj_structure_trap => "trap",
                                obj_structure_lattice => "lattice",
                                _ => null,
                            };
                            if (kind == null)
                                continue;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert; ST5 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("wooden chair item") >= 2, "too few wooden chair items");
            Assert.True(counts.GetValueOrDefault("eye chart") >= 1, "no eye chart");
            Assert.Equal(0, inert);
            Assert.Empty(runtimes);
        }
    }
}
