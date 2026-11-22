using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.T2;

/// <summary>M3 T2 phase B: Meta's smoothed walls, windows and tables against tg's bitmask_smooth (<see cref="Oracle"/>).</summary>
public class MetaSmoothingTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaSmoothingTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static bool UsesBitmask(atom a)
    {
        int flags = (int)a.smoothing_flags;
        return (flags & (SMOOTH_BITMASK | SMOOTH_BITMASK_CARDINALS)) != 0 && (flags & (SMOOTH_BORDER_OBJECT | SMOOTH_PROC_FILTER)) == 0;
    }

    [MetaFact]
    public void Every_smoothing_wall_window_and_table_of_Meta_has_the_junction_tgs_bitmask_smooth_gives_its_neighbours()
    {
        World world = _meta.World;
        int walls = 0, windows = 0, tables = 0, others = 0;
        var wrong = new List<string>();
        var inertUnsmoothed = new List<string>();
        // the shuttles load after SSicon_smooth's Initialize (SSshuttle.setup_shuttles, M10 slice G3): it smooths them and the
        // walls they touch at its next fires, as in tg
        for (int i = 0; i < 50; i++)
        {
            using (world.Enter())
            {
                if (SSicon_smooth.smooth_queue.len == 0)
                    break;
            }
            world.Step();
        }
        using (world.Enter())
        {
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    turf t = locate(x, y, 1);
                    var things = new List<atom> { t };
                    things.AddRange(t.contents.Items().Cast<atom_movable>());
                    foreach (atom a in things)
                    {
                        if (!UsesBitmask(a)) continue;
                        if (a is turf_closed_wall) walls++;
                        else if (a is obj_structure_window) windows++;
                        else if (a is obj_structure_table) tables++;
                        else others++;
                        string who = $"{DmTypes.PathOf(a.GetType())} at ({x},{y})";
                        if (a.smoothing_junction == null)
                        {
                            // D112 (types-converter.md section 7): an inert structure runs only the presentation part of
                            // /atom/Initialize, not /obj/structure/Initialize (structures.dm:21-26), which is what queues it
                            // for smoothing; the girders and false walls of Meta are inert until M5
                            if (InertSeam.IsInert(a)) inertUnsmoothed.Add(who);
                            else wrong.Add(who + " has no junction");
                            continue;
                        }
                        int expected = Oracle.Junction(flag =>
                        {
                            foreach (var (dx, dy, f) in Oracle.Neighbours)
                            {
                                if (f == flag) return Oracle.FoundInWorld(a, dx, dy);
                            }
                            return false;
                        }, ((int)a.smoothing_flags & SMOOTH_BITMASK_CARDINALS) != 0);
                        int actual = Convert.ToInt32(a.smoothing_junction);
                        if (actual != expected) wrong.Add($"{who}: junction {actual}, tg's bitmask_smooth gives {expected}");
                        if (a is turf && a.base_icon_state is string b)
                        {
                            string state = b + "-" + actual;
                            bool diagonal = ((int)a.smoothing_flags & SMOOTH_DIAGONAL_CORNERS) != 0 && Oracle.IsDiagonalState(actual);
                            if (a.icon_state != state && !(diagonal && a.icon_state == state + "-diagonal"))
                                wrong.Add($"{who}: icon_state \"{a.icon_state}\", tg's is \"{state}\"{(diagonal ? "-diagonal" : "")}");
                        }
                        if (Dm.truthy(Dm.bitand(a.smoothing_flags, SMOOTH_QUEUED))) wrong.Add(who + " is still queued for smoothing");
                    }
                }
            }
        }
        _output.WriteLine($"smoothed on Meta: {walls} walls, {windows} windows, {tables} tables, {others} others; {wrong.Count} wrong; {inertUnsmoothed.Count} inert without a junction: {string.Join(", ", inertUnsmoothed.Take(10))}");
        // every one of them is an inert girder or false wall
        Assert.All(inertUnsmoothed, w => Assert.True(w.StartsWith("/obj/structure/girder", StringComparison.Ordinal) || w.StartsWith("/obj/structure/falsewall", StringComparison.Ordinal), w));
        Assert.True(walls > 1000 && windows > 1000 && tables > 500, $"{walls} walls, {windows} windows, {tables} tables");
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(15)));
    }
}
