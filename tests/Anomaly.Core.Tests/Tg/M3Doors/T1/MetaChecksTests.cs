using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: MetaStation checks on one booted Meta (seed 1) shared by the class, against tg's own MetaStation.dmm read by an
// independent reader (TgDmm). Skipped without the converted map or the tg clone.

public class MetaDoorChecks : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private static readonly Lazy<TgDmm> Dmm = new(() => TgDmm.Read(Path.Combine(TgClone.Root, "_maps", "map_files", "MetaStation", "MetaStation.dmm")));

    public MetaDoorChecks(MetaWorldFixture meta) => _meta = meta;

    private static bool NeedsTg => TgClone.Root == null;

    private int CountInDmm(string prefix, Func<string, bool> extra = null)
        => Dmm.Value.Cells.Values.Sum(c => c.Count(e => (e.Path == prefix || e.Path.StartsWith(prefix + "/")) && (extra == null || extra(e.Path))));

    private static IEnumerable<T> Live<T>() where T : class
    {
        foreach (T t in Dm.each<T>())
            // the shuttles loaded at round start (M10 slice G3) are not tg's map file: their doors and lights are left out
            if (t is datum d && !QDELETED(d) && !(t is atom a && (a.z != 1 || get_area(a) is area_shuttle))) yield return t;
    }

    [MetaFact]
    public void TheKindsOfDoorsAndLightsMatchTheirCountsInTgsMap()
    {
        if (NeedsTg) return;
        using (_meta.World.Enter())
        {
            int abandoned = Dmm.Value.Cells.Values.Count(c => c.Any(e => e.Path.StartsWith("/obj/effect/mapping_helpers/airlock/abandoned") && !e.Path.EndsWith("/nowall"))
                && c.Any(e => e.Path.StartsWith("/obj/machinery/door/airlock")));
            int airlocksInMap = CountInDmm("/obj/machinery/door/airlock");
            int airlocks = Live<obj_machinery_door_airlock>().Count();
            // an abandoned helper with the wall (8%) or falsewall (1%) outcome removes its airlock (mapping_helpers.dm:262-270)
            Assert.InRange(airlocks, airlocksInMap - abandoned, airlocksInMap);
            Assert.Equal(CountInDmm("/obj/machinery/door/window"), Live<obj_machinery_door_window>().Count());
            Assert.Equal(CountInDmm("/obj/machinery/door/poddoor"), Live<obj_machinery_door_poddoor>().Count());
            Assert.Equal(CountInDmm("/obj/machinery/door/poddoor/shutters"), Live<obj_machinery_door_poddoor_shutters>().Count());
            Assert.Equal(CountInDmm("/obj/machinery/door/firedoor"), Live<obj_machinery_door_firedoor>().Count());
            Assert.Equal(CountInDmm("/obj/machinery/door/morgue"), Live<obj_machinery_door_morgue>().Count());
            Assert.Equal(CountInDmm("/obj/machinery/light"), Live<obj_machinery_light>().Count());
            // the counts P3 reported for seed 1 (m3/results/p3.json): the map's own numbers
            Assert.Equal(143, Live<obj_machinery_door_window>().Count());
            Assert.Equal(299, Live<obj_machinery_door_poddoor>().Count());
            Assert.Equal(390, Live<obj_machinery_door_firedoor>().Count());
            Assert.Equal(641, Live<obj_machinery_light>().Count());
            Assert.Equal(5, Live<obj_machinery_door_morgue>().Count());
        }
    }

    [MetaFact]
    public void NoDoorIsLeftHalfOpenOrDenseAndOpenAfterInit()
    {
        using (_meta.World.Enter())
        {
            var bad = new List<string>();
            foreach (obj_machinery_door door in Live<obj_machinery_door>())
            {
                string at = $"{door.GetType().Name} ({door.x},{door.y})";
                bool dense = Dm.truthy(door.density);
                if (door is obj_machinery_door_firedoor)
                {
                    if (dense) bad.Add(at + " firelock dense (D117)");
                    continue;
                }
                if (Dm.truthy(door.operating))
                    continue; // in motion: the arrivals shuttle's dock cycles (D299) and whoever roams opens doors; the checks below are for a door at rest
                // door.dm:173 set_init_door_layer: a dense door starts on its closing layer
                if (dense && !Dm.eq(door.layer, door.closingLayer)) bad.Add($"{at} dense on layer {door.layer}");
                if (door is obj_machinery_door_airlock a)
                {
                    object state = a.airlock_state;
                    string want = dense ? AIRLOCK_CLOSED : AIRLOCK_OPEN;
                    if (!Equals(state ?? want, want)) bad.Add($"{at} state {state} density {dense}");
                    if (Dm.truthy(a.welded) && !dense) bad.Add(at + " welded open");
                    bool opaque = Dm.truthy(a.opacity);
                    if (opaque != (dense && Dm.truthy(a.visible) && !Dm.truthy(a.glass))) bad.Add($"{at} opacity {opaque} density {dense} glass {a.glass}");
                }
                else if (door is obj_machinery_door_window w)
                {
                    if (Dm.truthy(w.opacity)) bad.Add(at + " windoor opaque");
                }
                else
                {
                    bool opaque = Dm.truthy(door.opacity);
                    if (opaque != (dense && Dm.truthy(door.visible) && !Dm.truthy(door.glass))) bad.Add($"{at} opacity {opaque} density {dense} glass {door.glass}");
                }
            }
            Assert.True(bad.Count == 0, string.Join("\n", bad.Take(15)) + $"\n({bad.Count} bad)");
        }
    }

    [MetaFact]
    public void EveryAirlockWithAccessHelpersHasTgsAccessAfterInit()
    {
        if (NeedsTg) return;
        var helpers = TgAccessHelpers.Parse();
        var defined = TgText.AccessDefines().ToDictionary(d => d.Name, d => d.Value);
        var byPath = helpers.ToDictionary(h => ((h.Any ? "any/" : "all/") + h.Path), h => h.Names.Select(n => defined[n]).ToList());
        int checkedCells = 0, skipped = 0;
        var bad = new List<string>();
        using (_meta.World.Enter())
        {
            foreach (var ((x, y), cell) in Dmm.Value.Cells)
            {
                var accessHelpers = cell.Where(e => e.Path.StartsWith("/obj/effect/mapping_helpers/airlock/access/")).ToList();
                if (accessHelpers.Count == 0) continue;
                var airlocks = cell.Where(e => e.Path.StartsWith("/obj/machinery/door/airlock")).ToList();
                bool anyKind = accessHelpers.Any(h => h.Path.Contains("/access/any/")), allKind = accessHelpers.Any(h => h.Path.Contains("/access/all/"));
                if (airlocks.Count != 1 || (anyKind && allKind) || airlocks[0].Vars.Contains("req_access") || airlocks[0].Vars.Contains("req_one_access"))
                {
                    skipped++; // no airlock, several, mixed kinds or a mapped access: the helpers' order or the refusal rules decide
                    continue;
                }
                var expected = new List<string>();
                foreach (var h in accessHelpers)
                    expected.AddRange(byPath[h.Path.Substring("/obj/effect/mapping_helpers/airlock/access/".Length)]);
                var airlock = Dm.locate<obj_machinery_door_airlock>(locate(x, y, 1));
                if (airlock == null || QDELETED(airlock)) { skipped++; continue; } // an abandoned airlock walled up
                checkedCells++;
                DmList got = (anyKind ? airlock.req_one_access : airlock.req_access) as DmList;
                var actual = got == null ? new List<string>() : Dm.each(got).Cast<string>().ToList();
                if (!expected.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(actual.OrderBy(s => s, StringComparer.Ordinal)))
                    bad.Add($"({x},{y}) {airlock.GetType().Name}: tg [{string.Join(",", expected)}] port [{string.Join(",", actual)}]");
                DmList other = (anyKind ? airlock.req_access : airlock.req_one_access) as DmList;
                if (other != null && Dm.truthy(other.len)) bad.Add($"({x},{y}) the other access list is not empty");
            }
        }
        Assert.True(checkedCells > 300, $"only {checkedCells} airlocks were checked ({skipped} skipped)");
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(15)) + $"\n({bad.Count} bad of {checkedCells})");
    }

    [MetaFact]
    public void MappedPodDoorIdsAndStatesSurviveTheLoad()
    {
        if (NeedsTg) return;
        // the map's `id = "..."` edits on pod doors and the open/closed state of the preopen types
        var wantIds = new Dictionary<(int, int), string>();
        foreach (var ((x, y), cell) in Dmm.Value.Cells)
            foreach (var e in cell.Where(e => e.Path.StartsWith("/obj/machinery/door/poddoor") && e.Vars.Contains("id = \"")))
            {
                var m = System.Text.RegularExpressions.Regex.Match(e.Vars, "\\bid = \"([^\"]*)\"");
                if (m.Success) wantIds[(x, y)] = m.Groups[1].Value;
            }
        Assert.True(wantIds.Count > 50, $"{wantIds.Count} pod doors with ids");
        using (_meta.World.Enter())
        {
            var bad = new List<string>();
            foreach (var ((x, y), id) in wantIds)
            {
                var door = Dm.each<obj_machinery_door_poddoor>(locate(x, y, 1)).FirstOrDefault();
                if (door == null) bad.Add($"({x},{y}) no pod door");
                else if ((string)door.id != id) bad.Add($"({x},{y}) id tg \"{id}\" port \"{door.id}\"");
            }
            Assert.True(bad.Count == 0, string.Join("\n", bad.Take(10)) + $"\n({bad.Count} bad)");
            int closed = Live<obj_machinery_door_poddoor>().Count(d => Dm.truthy(d.density));
            int preopen = Live<obj_machinery_door_poddoor>().Count(d => d is obj_machinery_door_poddoor_preopen or obj_machinery_door_poddoor_shutters_preopen);
            Assert.True(closed > 0 && preopen > 0);
        }
    }

    [MetaFact]
    public void LampsMountOnTheirWallsAndTheInitBreaksAboutTwoToFivePercent()
    {
        using (_meta.World.Enter())
        {
            var lamps = Live<obj_machinery_light>().ToList();
            int broken = lamps.Count(l => Dm.eq(l.status, LIGHT_BROKEN));
            int bulbs = lamps.Count(l => (string)l.fitting == "bulb");
            double expected = (lamps.Count - bulbs) * 0.02 + bulbs * 0.05;
            Assert.InRange(broken, expected * 0.3, expected * 2.5); // light.dm:134-141, the draw of prob(2) / prob(5) per mapped lamp
            int mounted = lamps.Count(l => l.GetComponent(typeof(datum_component_atom_mounted)) != null);
            Assert.InRange(mounted, lamps.Count - 12, lamps.Count);
            foreach (var l in lamps.Where(l => Dm.eq(l.status, LIGHT_OK) && Dm.truthy(l.on)))
                Assert.True(Dm.num(l.light_range) > 0, $"lit lamp ({l.x},{l.y}) without light range");
        }
    }
}
