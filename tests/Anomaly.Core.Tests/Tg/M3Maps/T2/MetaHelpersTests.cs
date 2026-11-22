using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// Gaps package G3 on the booted MetaStation (one world shared by the class, read-only): every mapping helper and spawner of tg's map
/// did its work and is gone (ROADMAP M4 follow-up: none is left on a turf), checked cell by cell against tg's MetaStation.dmm. The
/// request consoles carry their helpers' announcement and ore flags and their departments are in the helpers' lists; each mail sorting
/// pipe has its helpers' tags; the supermatter room's APC has the 10k cell; the cargo warehouse's lights are off; the AI upload has the
/// round default's core board (asimov); the costume spawners left their costumes.
/// </summary>
public class MetaHelpersTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaHelpersTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static TgDmm Dmm()
    {
        string clone = MapFixtures.TgClone();
        return TgDmm.Load(Path.Combine(clone, "_maps", "map_files", "MetaStation", "MetaStation.dmm"));
    }

    private static Type TypeOf(string dmPath)
        => typeof(obj_effect_mapping_helpers).Assembly.GetType("Anomaly.Core.Tg." + dmPath.Trim('/').Replace('/', '_'));

    private static IEnumerable<(int X, int Y, List<string> Paths)> Cells(TgDmm dmm, string prefix)
    {
        foreach (var ((x, y, z), key) in dmm.Cells)
        {
            if (z != 1) continue;
            var paths = dmm.Models[key].Where(p => p.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            if (paths.Count > 0) yield return (x, y, paths);
        }
    }

    [MetaChecksTests.MetaTgFact]
    public void No_mapping_helper_or_spawner_is_left_on_Meta()
    {
        using (_meta.World.Enter())
        {
            var left = new List<string>();
            for (int z = 1; z <= Dm.world.maxz; z++)
            {
                for (int y = 1; y <= Dm.world.maxy; y++)
                {
                    for (int x = 1; x <= Dm.world.maxx; x++)
                    {
                        foreach (atom_movable m in Dm.each_cast<atom_movable>(locate(x, y, z).contents))
                        {
                            if (m is obj_effect_mapping_helpers or obj_effect_spawner && !QDELETED(m))
                                left.Add($"({x},{y},{z}) {DmTypes.PathOf(m.GetType())}{(InertSeam.IsInert(m) ? " (inert)" : "")}");
                        }
                    }
                }
            }
            foreach (string l in left) _output.WriteLine(l);
            Assert.True(left.Count == 0, string.Join("\n", left.Take(20)));
        }
    }

    [MetaChecksTests.MetaTgFact]
    public void Metas_request_consoles_have_their_helpers_settings()
    {
        TgDmm dmm = Dmm();
        using (_meta.World.Enter())
        {
            var wrong = new List<string>();
            int consoles = 0, announcing = 0, ores = 0;
            foreach (var (x, y, paths) in Cells(dmm, "/obj/effect/mapping_helpers/requests_console/"))
            {
                var console = Dm.locate<obj_machinery_requests_console>(locate(x, y, 1));
                if (console == null)
                {
                    wrong.Add($"({x},{y}) no console");
                    continue;
                }
                consoles++;
                if (!Dm.truthy(console.department)) wrong.Add($"({x},{y}) no department");
                foreach (string p in paths)
                {
                    string kind = p[(p.LastIndexOf('/') + 1)..];
                    bool ok = kind switch
                    {
                        "announcement" => console.can_send_announcements == 1,
                        "ore_update" => console.receive_ore_updates == 1,
                        "assistance" => Dm.@in(console.department, GLOB.req_console_assistance),
                        "supplies" => Dm.@in(console.department, GLOB.req_console_supplies),
                        "information" => Dm.@in(console.department, GLOB.req_console_information),
                        _ => false,
                    };
                    if (!ok) wrong.Add($"({x},{y}) {console.department}: {kind}");
                    if (kind == "announcement") announcing++;
                    if (kind == "ore_update") ores++;
                }
            }
            _output.WriteLine($"{consoles} consoles with helpers; {announcing} announce, {ores} get ore updates; assistance {GLOB.req_console_assistance.len}, supplies {GLOB.req_console_supplies.len}, information {GLOB.req_console_information.len} departments; {GLOB.req_console_all.len} consoles");
            Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(20)));
            Assert.Equal(10, announcing);
            Assert.Equal(6, ores);
            // every console of the map file registered itself (the arrivals shuttle brings one more)
            var mapped = dmm.Cells.Where(c => c.Key.Z == 1 && dmm.Models[c.Value].Any(p => p.StartsWith("/obj/machinery/requests_console", StringComparison.Ordinal))).ToList();
            Assert.Equal(40, mapped.Count);
            foreach (var ((x, y, _), _) in mapped)
                Assert.True(Dm.@in(Dm.locate<obj_machinery_requests_console>(locate(x, y, 1)), GLOB.req_console_all), $"({x},{y}) not in GLOB.req_console_all");
        }
    }

    [MetaChecksTests.MetaTgFact]
    public void Metas_mail_sorting_pipes_have_their_helpers_tags()
    {
        TgDmm dmm = Dmm();
        using (_meta.World.Enter())
        {
            var wrong = new List<string>();
            int pipes = 0;
            foreach (var (x, y, paths) in Cells(dmm, "/obj/effect/mapping_helpers/mail_sorting/"))
            {
                var pipe = Dm.locate<obj_structure_disposalpipe_sorting_mail>(locate(x, y, 1));
                if (pipe == null)
                {
                    wrong.Add($"({x},{y}) no sorting pipe");
                    continue;
                }
                pipes++;
                foreach (string p in paths)
                {
                    float tag = Dm.initial(TypeOf(p), (obj_effect_mapping_helpers_mail_sorting h) => h.sort_type);
                    if (!Dm.@in(tag, pipe.sortTypes)) wrong.Add($"({x},{y}) {p}: tag {tag} missing");
                }
            }
            _output.WriteLine($"{pipes} tagged sorting pipes");
            Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(20)));
            Assert.Equal(32, pipes);
        }
    }

    [MetaChecksTests.MetaTgFact]
    public void Metas_apc_lights_lawset_and_costume_helpers_did_their_work()
    {
        TgDmm dmm = Dmm();
        using (_meta.World.Enter())
        {
            var (ax, ay, _) = Cells(dmm, "/obj/effect/mapping_helpers/apc/cell_10k").Single();
            var apc = Dm.locate<obj_machinery_power_apc>(locate(ax, ay, 1));
            Assert.IsType<obj_item_stock_parts_power_store_battery_high>(apc.cell);

            var (lx, ly, _) = Cells(dmm, "/obj/effect/mapping_helpers/turn_off_lights_with_lightswitch").Single();
            var warehouse = (area)get_area(locate(lx, ly, 1));
            Assert.IsType<area_station_cargo_warehouse>(warehouse);
            Assert.False(Dm.truthy(warehouse.lightswitch));

            var (mx, my, _) = Cells(dmm, "/obj/effect/spawner/round_default_module").Single();
            Assert.NotNull(Dm.locate<obj_item_ai_module_law_core_full_asimov>(locate(mx, my, 1)));

            // the random costume spawners pick a costume spawner, which lays its items (costume.dm:9)
            int withClothes = Cells(dmm, "/obj/effect/spawner/random/clothing/costume").Count(c =>
                Dm.each<obj_item_clothing>(locate(c.X, c.Y, 1)).Any() || Dm.each<obj_item>(locate(c.X, c.Y, 1)).Any());
            Assert.Equal(Cells(dmm, "/obj/effect/spawner/random/clothing/costume").Count(), withClothes);
        }
    }
}
