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
/// Gaps package OD2 on the converted MetaStation: the mapped flatpacks (the science lab's and the shared engineering storage's
/// flatpackers) boot live with their boards made and named after the machine, the maintenance's lead-acid battery boots live, part
/// charged and with its avatar gear component, and any other item of OD2's files the maintenance loot spawned boots live, with no
/// runtime from OD2's files.
/// </summary>
public class ItemsOD2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ItemsOD2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "items/v8_engine", "items/drug_items", "items/flatpacks", "items/piggy_bank", "items/etherealdiscoball", "items/debug_items",
        "items/busts_and_figurines", "items/virgin_mary", "items/rollertable_dock", "items/botpad_remote", "items/dna_probe",
        "items/maintenance_loot", "items/dyespray", "items/gun_maintenance", "items/pitchfork", "machinery/botlaunchpad",
        "components/areabound", "components/avatar_gear",
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
    public void Metas_flatpacks_and_lead_battery_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, boardless = 0, misnamed = 0, uncharged = 0, gearless = 0;
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
                                obj_item_flatpack => "flatpack",
                                obj_item_stock_parts_power_store_cell_lead => "lead-acid battery",
                                obj_item_lead_pipe => "lead pipe",
                                obj_item_food_drug => "drug",
                                obj_item_reagent_containers_cup_blastoff_ampoule => "ampoule",
                                obj_item_piggy_bank => "piggy bank",
                                obj_item_statuebust or obj_item_maneki_neko => "figurine",
                                obj_item_virgin_mary => "virgin mary",
                                obj_item_gun_maintenance_supplies => "gun kit",
                                obj_item_dyespray => "dye spray",
                                obj_item_pitchfork => "pitchfork",
                                obj_item_v8_engine or obj_item_house_edge => "v8",
                                obj_structure_flatpack_cart => "flatpack cart",
                                obj_machinery_botpad or obj_item_botpad_remote => "bot pad",
                                _ => null,
                            };
                            if (kind == null)
                                return;
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                            if (InertSeam.IsInert(thing))
                                inert++;
                            if (thing is obj_item_flatpack pack)
                            {
                                if (pack.board is not obj_item_circuitboard_machine board || board.loc != pack)
                                    boardless++;
                                else if (pack.name != "flatpack (" + Dm.initial(board.build_path, (atom p) => p.name) + ")")
                                    misnamed++;
                            }
                            if (thing is obj_item_stock_parts_power_store_cell_lead cell)
                            {
                                if (cell.charge < 0.4f * cell.maxcharge - 1 || cell.charge > 0.6f * cell.maxcharge + 1)
                                    uncharged++;
                                if (cell.GetComponent(typeof(datum_component_loads_avatar_gear)) == null)
                                    gearless++;
                            }
                        });
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {boardless} flatpacks without a board, {misnamed} misnamed, {uncharged} batteries out of 40-60 %, {gearless} without their gear; OD2 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("flatpack") >= 2, "too few flatpacks");
            Assert.True(counts.GetValueOrDefault("lead-acid battery") >= 1, "no lead-acid battery");
            Assert.Equal(0, inert);
            Assert.Equal(0, boardless);
            Assert.Equal(0, misnamed);
            Assert.Equal(0, uncharged);
            Assert.Equal(0, gearless);
            Assert.Empty(runtimes);
        }
    }
}
