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
/// Gaps package ST4 on the converted MetaStation: the gym's weight machines (with their Work out actions), punching bags, training
/// machines and training toolboxes boot live, with no runtime from ST4's files.
/// </summary>
public class StructuresST4MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public StructuresST4MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "gym/weight_machine", "gym/punching_bag", "training_machine", "bonfire", "fireplace", "mineral_doors", "guillotine",
        "platform", "spirit_board", "loom", "steps",
    };

    [MetaFact]
    public void Metas_gym_equipment_boots_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, actionless = 0;
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
                                obj_structure_weightmachine => "weight machine",
                                obj_structure_punching_bag => "punching bag",
                                obj_structure_training_machine => "training machine",
                                obj_item_training_toolbox => "training toolbox",
                                _ => null,
                            };
                            if (kind == null)
                                continue;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                            if (thing is obj_structure_weightmachine { weight_action: null })
                                actionless++;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {actionless} weight machines without their action; ST4 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("weight machine") >= 5, "too few weight machines");
            Assert.True(counts.GetValueOrDefault("punching bag") >= 3, "too few punching bags");
            Assert.True(counts.GetValueOrDefault("training machine") >= 3, "too few training machines");
            Assert.True(counts.GetValueOrDefault("training toolbox") >= 1, "no training toolbox");
            Assert.Equal(0, inert);
            Assert.Equal(0, actionless);
            Assert.Empty(runtimes);
        }
    }
}
