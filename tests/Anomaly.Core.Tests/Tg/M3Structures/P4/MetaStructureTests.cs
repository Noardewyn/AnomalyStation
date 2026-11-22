using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.P4;

// M3 P4 on converted MetaStation (P2's fixture, skipped without the converted map): the smoothing, the floors' broken and
// burnt states, the structures, spawners, decals and the undertile element through Meta's own init.
public class MetaStructureTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaStructureTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    // The runtimes of Meta's boot whose stack passes through one of P4's files.
    private List<Exception> P4Runtimes(params string[] files)
        => _meta.Log.Runtimes.Where(e => files.Any(f => (e.ToString()).Contains(f, StringComparison.Ordinal))).ToList();

    private void Report(IEnumerable<Exception> runtimes)
    {
        foreach (var group in runtimes.GroupBy(e => e.ToString().Split('\n')[0]).OrderByDescending(g => g.Count()).Take(20))
            _output.WriteLine($"x{group.Count()}: {group.Key}\n{group.First().ToString().Split('\n').Skip(1).Take(6).Aggregate("", (a, b) => a + b + "\n")}");
    }

    [MetaFact]
    public void IconSmoothingInitializesMetaWithoutARuntime()
    {
        var smoothing = P4Runtimes("icon_smoothing.cs", "icon_smooth.cs", "bitflag_lists.cs");
        Report(smoothing);
        Assert.Empty(smoothing);
        using (_meta.World.Enter())
        {
            Assert.True(Dm.truthy(SSicon_smooth.initialized));
            // a wall of Meta smoothed into a junction state (walls.dm, icon_smoothing.dm:285)
            int smoothed = 0;
            for (int y = 1; y <= 255 && smoothed == 0; y++)
                for (int x = 1; x <= 255; x++)
                    if (locate(x, y, 1) is turf_closed_wall wall && wall.icon_state != "wall-0" && wall.icon_state.StartsWith("wall-", StringComparison.Ordinal))
                        smoothed++;
            Assert.True(smoothed > 0);
        }
    }

    [MetaFact]
    public void TheFloorsBrokenAndBurntTilesInitializeWithoutARuntime()
    {
        var floors = P4Runtimes("_open.cs", "floor.cs", "dirtystation.cs", "iron_floor.cs", "plating.cs");
        Report(floors);
        Assert.Empty(floors);
        using (_meta.World.Enter())
        {
            // Meta's 123 broken_floor and 55 burnt_floor helpers break and burn tiles at init; each damaged tile carries
            // tg's damaged_dmi overlay (_open.dm:76, :92)
            int damaged = 0, with_overlay = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    if (locate(x, y, 1) is not turf_open t || t.damaged_dmi == null || !(Dm.truthy(t.broken) || Dm.truthy(t.burnt)))
                        continue;
                    damaged++;
                    if (Dm.each_cast<object>(t.overlays).OfType<IDmAppearance>().Any(a => Dm.eq(a.icon, t.damaged_dmi)))
                        with_overlay++;
                }
            }
            _output.WriteLine($"damaged tiles: {damaged}, with the damaged overlay: {with_overlay}");
            Assert.True(damaged > 50, damaged.ToString());
            Assert.Equal(damaged, with_overlay);
        }
    }

    [MetaFact]
    public void TheStructuresSpawnersAndDecalsInitializeWithoutARuntime()
    {
        var structures = P4Runtimes("structures/window.cs", "structures/grille.cs", "structures/tables_racks.cs", "structures/railings.cs",
            "structures/plasticflaps.cs", "spawners/structure.cs", "spawners/random/_random.cs", "effects/misc.cs", "decals/decal.cs",
            "decals/_decal.cs", "elements/undertile.cs", "atom_invisibility.cs", "walls.cs", "reinf_walls.cs", "atmosmachinery.cs",
            "pipes/pipes.cs", "pipes/layermanifold.cs", "pipes/bridge_pipe.cs", "power/cable.cs", "disposal/pipe.cs",
            "disposal/pipe_sorting.cs", "atom_greyscale.cs");
        Report(structures);
        Assert.Empty(structures);
    }

    [MetaFact]
    public void TheHiddenPipesCablesAndDisposalPipesAreInvisibleAndTheExposedOnesShow()
    {
        using (_meta.World.Enter())
        {
            int hidden = 0, shown = 0, wrong = 0;
            var wrongKinds = new Dictionary<string, int>();
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    turf t = locate(x, y, 1);
                    foreach (atom thing in t.dm_held)
                    {
                        // the undertile ones: cables and disposal pipes, and the pipes that hide (atmosmachinery.dm:95)
                        bool undertile = thing is obj_structure_cable or obj_structure_disposalpipe
                            || (thing is obj_machinery_atmospherics_pipe pipe && Dm.truthy(pipe.hide));
                        if (!undertile)
                            continue;
                        bool covered = t.underfloor_accessibility < UNDERFLOOR_VISIBLE; // undertile.dm:41
                        if (covered != (thing.invisibility > 25))
                        {
                            wrong++;
                            string k = DmTypes.PathOf(t.GetType()) + " acc=" + t.underfloor_accessibility + " " + DmTypes.PathOf(thing.GetType()) + " inv=" + thing.invisibility;
                            wrongKinds[k] = wrongKinds.GetValueOrDefault(k) + 1;
                        }
                        if (thing.invisibility > 25) hidden++; else shown++;
                    }
                }
            }
            _output.WriteLine($"infrastructure: {hidden} hidden (invisibility > 25, D115), {shown} shown");
            foreach (var kv in wrongKinds.OrderByDescending(k => k.Value).Take(15)) _output.WriteLine($"  wrong x{kv.Value}: {kv.Key}");
            Assert.True(hidden > 1000, hidden.ToString()); // Meta's floors hide thousands of pipes and cables
            Assert.True(shown > 0, shown.ToString()); // the ones on plating show
            Assert.Equal(0, wrong);
        }
    }

    [MetaFact]
    public void MetasTurfDecalsAreInTheirTurfsOverlays()
    {
        using (_meta.World.Enter())
        {
            int decorated = 0, decals_left = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    turf t = locate(x, y, 1);
                    if (Dm.truthy(t.overlays.len))
                        decorated++;
                    decals_left += t.dm_held.Count(a => a is obj_effect_turf_decal);
                }
            }
            _output.WriteLine($"turfs with overlays: {decorated}; turf decals left: {decals_left}");
            Assert.Equal(0, decals_left); // INITIALIZE_HINT_QDEL (decal.dm:83)
            Assert.True(decorated > 1000, decorated.ToString()); // Meta has 5,731 decals (scope 2e)
        }
    }

    [MetaFact]
    public void ReportMetasRuntimes()
    {
        _output.WriteLine($"Meta boot {_meta.BootSeconds:F1} s, {_meta.Log.Runtimes.Count} runtimes");
        Report(_meta.Log.Runtimes);
    }
}
