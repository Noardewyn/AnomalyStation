using System.IO;
using Xunit.Sdk;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: converted MetaStation's station level (one booted world shared by the class, read-only; skipped without the
/// converted map) against tg's own map file and tg's map unit tests. The cell-by-cell comparison reads the clone's
/// MetaStation.dmm with <see cref="TgDmm"/>, which shares no code with the converter or the reader.
/// </summary>
public class MetaChecksTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaChecksTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static string TgDmmPath()
    {
        string clone = MapFixtures.TgClone();
        if (clone == null) return null;
        string path = Path.Combine(clone, "_maps", "map_files", "MetaStation", "MetaStation.dmm");
        return File.Exists(path) ? path : null;
    }

    // A fact that needs the converted map and the tg clone.
    [TraitDiscoverer(MetaTraitDiscoverer.TypeName, MetaTraitDiscoverer.AssemblyName)]
    public sealed class MetaTgFactAttribute : FactAttribute, ITraitAttribute
    {
        public MetaTgFactAttribute()
        {
            if (MetaMaps.Folder() == null)
                Skip = "no converted MetaStation (run tools/convert-maps.sh, or set ANOMALY_META_MAP)";
            else if (TgDmmPath() == null)
                Skip = "no tg clone (ANOMALY_TG or ../tgstation)";
        }
    }

    // What the checked-in MetaStation has not of tg's map file, on purpose (D337: the owner's map patches, folded into the map on 2026-11-17): the
    // table under the power monitoring console (DV365) and two start landmarks no one could walk out of (DV353). The map is edited in the map
    // editor now: a thing of tg's that an edit takes away goes here, or these checks against tg's own file say so.
    private static readonly (string Type, int X, int Y)[] TakenAway =
    {
        ("/obj/structure/table", 220, 135), ("/obj/effect/landmark/start/hangover", 74, 139), ("/obj/effect/landmark/start/assistant", 75, 139),
    };

    // ---- the cell-by-cell comparison with tg's own map file

    [MetaTgFact]
    public void Every_cell_has_the_turf_area_and_movables_tgs_map_file_gives_it()
    {
        TgDmm dmm = TgDmm.Load(TgDmmPath());
        World world = _meta.World;
        var wrong = new List<string>();
        int movablesExpected = 0, movablesFound = 0;
        using (world.Enter())
        {
            // the station, CentCom (DV239), Lavaland (L1, D304) and tg's first transit level (M10 slice G1)
            Assert.Equal((dmm.MaxX, dmm.MaxY, dmm.MaxZ + 3), ((int)Dm.world.maxx, (int)Dm.world.maxy, (int)Dm.world.maxz));
            for (int y = 1; y <= dmm.MaxY; y++)
            {
                for (int x = 1; x <= dmm.MaxX; x++)
                {
                    List<string> model = dmm.At(x, y, 1);
                    turf t = locate(x, y, 1);
                    string area_path = model[^1];
                    string turf_path = model[^2];
                    // a shuttle docked at round start (SSshuttle.setup_shuttles, M10 slice G3) stands on cells tg's map file leaves to its dock
                    if (t.loc is area_shuttle && DmTypes.PathOf(t.loc.GetType()) != area_path)
                        continue;
                    if (DmTypes.PathOf(t.loc.GetType()) != area_path)
                        wrong.Add($"({x},{y}) area {DmTypes.PathOf(t.loc.GetType())}, tg's map has {area_path}");
                    // /turf/open/space/basic is a map turf that ChangeTurf would turn into /turf/open/space; the load keeps it
                    string turf_found = DmTypes.PathOf(t.GetType());
                    // the abandoned airlock helper walls its airlock up with place_on_top(wall) (mapping_helpers.dm:266-269)
                    bool walled_up = model.Any(p => p.StartsWith("/obj/effect/mapping_helpers/airlock/abandoned", StringComparison.Ordinal))
                        && turf_found.StartsWith("/turf/closed/wall", StringComparison.Ordinal) && turf_path.StartsWith("/turf/open/floor", StringComparison.Ordinal);
                    // D147: the converter gives a door's plating the floor of its neighbour (tools/Anomaly.Tools.Maps/DoorFloors.cs)
                    bool door_floor = turf_path == "/turf/open/floor/plating" && turf_found.StartsWith("/turf/open/floor/", StringComparison.Ordinal)
                        && model.Any(p => p.StartsWith("/obj/machinery/door/", StringComparison.Ordinal));
                    if (turf_found != turf_path && !walled_up && !door_floor)
                        wrong.Add($"({x},{y}) turf {turf_found}, tg's map has {turf_path}");
                    // movables: tg creates them in model order, so the ones it keeps are on the turf in that order. The transient
                    // ones (spawners, mapping helpers, turf decals) are gone after the init (INITIALIZE_HINT_QDEL, or they replace
                    // themselves with what they spawn), and a cell that held one may hold things tg's map file does not list.
                    var expected = model.Take(model.Count - 2).ToList();
                    foreach ((string gone, _, _) in TakenAway.Where(g => (g.X, g.Y) == (x, y)))
                        expected.Remove(gone);
                    movablesExpected += expected.Count;
                    var found = t.contents.Items().Cast<atom_movable>().Select(m => DmTypes.PathOf(m.GetType())).ToList();
                    // M4a P3: a closed closet takes the map's movables of its turf inside at LateInitialize (tg's take_contents,
                    // closets.dm:521), so those are in the cell inside the closet, not on the turf; (gaps S3) so are the pen a paper bin
                    // took (paperbin.dm:32), the small items a filing cabinet took (filingcabinet.dm:37) and the papers a notice board
                    // took (noticeboard.dm:31) at their Initialize.
                    foreach (atom_movable holder in t.contents.Items().Cast<atom_movable>().Where(c => c is obj_structure_closet or obj_item_paper_bin or obj_structure_filingcabinet or obj_structure_noticeboard))
                        foreach (atom_movable inside in holder.contents.Items().Cast<atom_movable>())
                            expected.Remove(DmTypes.PathOf(inside.GetType()));
                    // M7 slice 3: an APC made at map load makes its terminal on its turf when the map gives it none (make_terminal,
                    // apc_power_proc.dm:15), after the cell's movables.
                    if (expected.Any(p => p.StartsWith("/obj/machinery/power/apc", StringComparison.Ordinal)) && !expected.Contains("/obj/machinery/power/terminal"))
                        expected.Add("/obj/machinery/power/terminal");
                    movablesFound += found.Count;
                    string problem = CellProblem(expected, found, NearTransient(dmm, x, y));
                    if (problem != null)
                        wrong.Add($"({x},{y}) movables [{string.Join(", ", found)}], tg's map has [{string.Join(", ", expected)}]: {problem}");
                }
            }
        }
        _output.WriteLine($"movables: {movablesFound} found, {movablesExpected} in tg's map file; {wrong.Count} differences");
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(25)));
    }

    /// <summary>
    /// The types that delete themselves in Initialize or replace themselves with what they make (tg: spawners.dm and
    /// spawners/structure.dm and random/_random.dm return INITIALIZE_HINT_QDEL; mapping_helpers.dm; turf_decal in decal.dm
    /// returns INITIALIZE_HINT_QDEL after giving the turf its decal element). A type that is still inert at this snapshot
    /// stays on its tile, which the check allows too (it is a map member that survives).
    /// </summary>
    internal static bool IsTransient(string path)
        => path.StartsWith("/obj/effect/spawner", StringComparison.Ordinal)
        || path.StartsWith("/obj/effect/mapping_helpers", StringComparison.Ordinal)
        || path.StartsWith("/obj/effect/turf_decal", StringComparison.Ordinal)
        || path == "/mob/living/basic/pet/dog/corgi/ian" // with no save he goes on as his puppy (corgi.dm:393)
        || path == "/obj/item/reagent_containers/cup/soda_cans/random" // a random can in its place (soda.dm:37, live since gaps H3)
        || path == "/obj/docking_port/stationary/random" // the escape pods' landing spot moves to Lavaland at the map load (pods.dm:89)
        || path == "/obj/item/raw_anomaly_core/random" // a random raw core in its place (raw_anomaly.dm:84)
        // landmarks that put their tile on a list and delete themselves (landmarks.dm:368, :379, :389)
        || path is "/obj/effect/landmark/generic_maintenance_landmark" or "/obj/effect/landmark/blobstart" or "/obj/effect/landmark/secequipment";

    /// <summary>
    /// Null when the cell's movables are what tg's map file gives after the init, else what is wrong. The map's members that no
    /// transient one can touch must all be there in the map's order; a member can be missing, and a thing the map does not
    /// list can be there, only in a cell that is within a random spawner's reach (spawn_scatter_radius is at most 2 in tg,
    /// trash.dm:215) of a cell whose model has a transient member (the spawned loot, windows and grilles, closets, the
    /// abandoned airlock helper's false wall), or a cleanable decal (dirtystation.dm: tg dirties its floors at init). A pipe's cap
    /// visual is tg's init too (atmosmachinery.dm:656 update_cap_visuals, where a hidden pipe meets an exposed one), never the map's.
    /// </summary>
    internal static string CellProblem(List<string> expected, List<string> found, bool nearTransient)
    {
        bool transient = expected.Any(IsTransient);
        // cleanable decals merge into one another (a second dirt on a turf, in LateInitialize) and tg's dirtystation adds more, so
        // which of two equal ones survives, and where it stands among the others, is not part of the map
        expected = expected.Where(p => !IsCleanable(p)).ToList();
        found = found.Where(p => !IsCleanable(p) && !IsCapVisual(p)).ToList();
        var used = new bool[found.Count];
        int from = 0;
        foreach (string path in expected.Where(p => !IsTransient(p)))
        {
            int at = -1;
            for (int i = from; i < found.Count; i++)
            {
                if (!used[i] && found[i] == path) { at = i; break; }
            }
            if (at < 0)
            {
                // a cell's transient member may replace it (abandoned.dm)
                if (transient) continue;
                // one emergency closet in a hundred deletes itself at init (utility_closets.dm:30, prob(1)): which one is the random stream's
                if (path == "/obj/structure/closet/emcloset") continue;
                // a stack merges into one of its kind on its tile at init (stack.dm:108, merge_with_loc)
                if (IsStack(path) && found.Contains(path)) continue;
                return $"{path} of the map is missing";
            }
            used[at] = true;
            from = at + 1;
        }
        for (int i = 0; i < found.Count; i++)
        {
            if (used[i]) continue;
            // a survivor transient type (inert), an extra of a transient cell, or a dirt decal
            if (nearTransient || expected.Contains(found[i])) continue;
            return $"{found[i]} is not in the map file's cell";
        }
        return null;
    }

    internal static bool IsCleanable(string path) => path.StartsWith("/obj/effect/decal/cleanable", StringComparison.Ordinal);

    // a live stack merges into its twin on the turf at map load (stack.dm Initialize, merge_type): the plasma sheets of a room are one pile
    internal static bool IsStack(string path) => path.StartsWith("/obj/item/stack/", StringComparison.Ordinal);

    // tg's atmos_init adds it beside a pipe end (atmosmachinery.dm:656): init content, never in a map file
    internal static bool IsCapVisual(string path) => path == "/obj/effect/overlay/cap_visual";

    /// <summary>Whether a cell with a transient member in tg's map file lies within 2 cells (Chebyshev) of (x, y).</summary>
    internal static bool NearTransient(TgDmm dmm, int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dmm.Cells.TryGetValue((x + dx, y + dy, 1), out string key) && dmm.Models[key].Any(IsTransient)) return true;
            }
        }
        return false;
    }

    [MetaTgFact]
    public void Every_area_type_of_tgs_Meta_has_one_area_object_and_no_other_exists()
    {
        TgDmm dmm = TgDmm.Load(TgDmmPath());
        var expected = new HashSet<string>(dmm.Cells.Values.Distinct().Select(k => dmm.Models[k][^1]), StringComparer.Ordinal);
        World world = _meta.World;
        using (world.Enter())
        {
            // the shuttles loaded at round start (M10 slice G3) bring their own areas, one object per load (a shuttle area is not
            // UNIQUE_AREA), and the reservations' cordons theirs (M10 slice G1): those are left out
            // and CentCom's level (D299) brings its own, which have no turf on the station's z 1
            var found = GLOB.areas.Items().Cast<area>().Where(a => a is not (area_shuttle or area_misc_cordon) && Dm.each<turf>(a.contents).Any(t => t.z == 1) || expected.Contains(DmTypes.PathOf(a.GetType())))
                .Select(a => DmTypes.PathOf(a.GetType())).ToList();
            Assert.Equal(found.Count, found.Distinct().Count()); // one object per area type
            Assert.Equal(expected.OrderBy(s => s, StringComparer.Ordinal), found.OrderBy(s => s, StringComparer.Ordinal));
            _output.WriteLine($"{found.Count} area types");
        }
    }

    // ---- tg's map unit tests

    [MetaFact]
    public void Ported_maptest_area_contents_on_Meta()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            List<string> fails = TgMapChecks.AreaContents();
            Assert.True(fails.Count == 0, string.Join("\n", fails.Take(20)));
        }
    }

    [MetaFact]
    public void Ported_maptest_mapload_space_verification_on_Meta()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            List<string> fails = TgMapChecks.MaploadSpaceVerification();
            Assert.True(fails.Count == 0, string.Join("\n", fails.Take(20)));
        }
    }

    [MetaFact]
    public void Ported_maptest_mapping_nearstation_on_Meta()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            List<string> fails = TgMapChecks.MappingNearstation();
            Assert.True(fails.Count == 0, string.Join("\n", fails.Take(20)));
        }
    }

    [MetaFact]
    public void Ported_maptest_log_mapping_on_Meta()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            List<string> fails = TgMapChecks.MappingLogs(_meta.Log.Lines);
            Assert.True(fails.Count == 0, string.Join("\n", fails.Take(20)));
        }
    }

    // ---- the runtimes of Meta's init, by site

    /// <summary>
    /// Every runtime during Meta's init, with the open item it belongs to. A runtime of an unlisted site fails the test. The
    /// counts are the maxima measured at main ec4af41; a site may vanish (its package merged) but not grow. Gone since phase A
    /// (main 3e36fe3): P4's update_greyscale (24) and bitmask_smooth cast (1), and I1a's generator typing removed the
    /// preloader's `id` text on a number (5).
    /// </summary>
    private static readonly (string Site, int Max, string Item)[] KnownRuntimes =
    {
        ("areas.cs LateInitialize monitored_area", 10, "cameras: AddComponent(/datum/component/monitored_area), areas.dm:228 (NotTranslated, no not-translated.md row)"),
        ("atom_materials.cs set_custom_materials", 5, "M4a: materials (atom_materials.dm:13)"),
        ("items.cs add_item_action", 2, "M2 P2 skeleton: obj/item add_item_action (items.dm:316)"),
        ("_organ.cs Initialize setup_bodypart_overlay", 2, "M9: visual organs, an organ with a bodypart overlay (setup_bodypart_overlay, _organ.dm:102); a random pick of Meta's spawners since M4b slice 2 moved the RNG stream (the firelocks' and devices' id tags)"),
    };

    // The site of a runtime: by what the exception says (a NotTranslated names its proc and tg line).
    internal static string SiteOf(Exception e)
    {
        string text = e.Message ?? "";
        if (text.Contains("monitored_area", StringComparison.Ordinal)) return "areas.cs LateInitialize monitored_area";
        if (text.Contains("set_custom_materials", StringComparison.Ordinal)) return "atom_materials.cs set_custom_materials";
        if (text.Contains("add_item_action", StringComparison.Ordinal)) return "items.cs add_item_action";
        if (text.Contains("setup_bodypart_overlay", StringComparison.Ordinal)) return "_organ.cs Initialize setup_bodypart_overlay";
        return e.GetType().Name + ": " + (text.Length > 160 ? text[..160] : text);
    }

    [MetaFact]
    public void Every_runtime_during_Metas_init_belongs_to_a_known_open_item()
    {
        var bySite = _meta.Log.Runtimes.GroupBy(SiteOf).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var (site, count) in bySite.OrderByDescending(p => p.Value))
            _output.WriteLine($"runtime x{count}: {site}");
        var unknown = bySite.Where(p => KnownRuntimes.All(k => k.Site != p.Key)).ToList();
        Assert.True(unknown.Count == 0, "runtimes of unlisted sites:\n" + string.Join("\n", _meta.Log.Runtimes
            .Where(r => unknown.Any(u => u.Key == SiteOf(r))).Take(5)
            .Select(r => $"{r.GetType().Name}: {r.Message}\n{string.Join("\n", (r.StackTrace ?? "").Split('\n').Take(8))}")));
        foreach (var (site, max, item) in KnownRuntimes)
        {
            if (bySite.TryGetValue(site, out int count))
                Assert.True(count <= max, $"{site}: {count} runtimes, at most {max} known ({item})");
        }
    }

    // An inert machine runs no post_machine_initialize, yet its screen shows from round start (its power_change runs at init).
    [MetaFact]
    public void Every_powered_computer_shows_its_screen_at_round_start()
    {
        using (_meta.World.Enter())
        {
            var dark = Dm.each<obj_machinery_computer>(Dm.world)
                .Where(c => c.icon_screen != null && !Dm.truthy(Dm.bitand(c.machine_stat, Dm.bitor(NOPOWER, BROKEN))))
                .Where(c => !Dm.each_cast<object>(c.overlays).Any(o => o is IDmAppearance a && a.icon_state == c.icon_screen))
                .Select(c => DmTypes.PathOf(c.GetType())).ToList();
            Assert.True(dark.Count == 0, $"{dark.Count} dark screens: " + string.Join(", ", dark.Distinct().Take(10)));
        }
    }

    // ---- counts: live, inert and deleted movables

    [MetaTgFact]
    public void The_live_inert_and_lost_movables_of_tgs_map_file()
    {
        TgDmm dmm = TgDmm.Load(TgDmmPath());
        int inMapFile = 0;
        var wantedByType = new Dictionary<string, int>(StringComparer.Ordinal);
        // the airlock types the abandoned airlock helper may wall up: any airlock in a cell of the helper (mapping_helpers.dm:252,
        // a weighted pick from the world's RNG decides which)
        var abandonable = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in dmm.Cells.Values)
        {
            foreach (string path in dmm.Models[key].SkipLast(2))
            {
                inMapFile++;
                wantedByType[path] = wantedByType.GetValueOrDefault(path) + 1;
            }
            if (dmm.Models[key].Any(p => p.StartsWith("/obj/effect/mapping_helpers/airlock/abandoned", StringComparison.Ordinal)))
                abandonable.UnionWith(dmm.Models[key].Where(p => p.StartsWith("/obj/machinery/door/airlock", StringComparison.Ordinal)));
        }
        foreach ((string gone, _, _) in TakenAway)
            wantedByType[gone]--;
        World world = _meta.World;
        using (world.Enter())
        {
            int live = 0, inert = 0;
            var aliveByType = new Dictionary<string, int>(StringComparer.Ordinal);
            var kinds = new SortedDictionary<string, (int Live, int Inert)>(StringComparer.Ordinal);
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    // M4a P3: the map's movables a closed closet took inside at LateInitialize (closets.dm:521) are alive in it; and
                    // (2026-10-18) the items a locked safe took inside at its Initialize (safe.dm:72); (gaps S3) the pen a paper bin took
                    // (paperbin.dm:32), the small items a filing cabinet took (filingcabinet.dm:37) and the papers a notice board took
                    // (noticeboard.dm:31) at theirs; and the books a bookcase took at its mapload (bookcase.dm:41).
                    var cellMovables = locate(x, y, 1).contents.Items().Cast<atom_movable>().ToList();
                    cellMovables.AddRange(cellMovables.Where(c => c is obj_structure_closet or obj_structure_safe or obj_item_paper_bin or obj_structure_filingcabinet or obj_structure_noticeboard or obj_structure_bookcase)
                        .SelectMany(c => c.contents.Items().Cast<atom_movable>()).ToList());
                    foreach (atom_movable m in cellMovables)
                    {
                        string path = DmTypes.PathOf(m.GetType());
                        aliveByType[path] = aliveByType.GetValueOrDefault(path) + 1;
                        bool isInert = InertSeam.IsInert(m);
                        if (isInert) inert++; else live++;
                        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        string kind = "/" + string.Join("/", parts.Take(Math.Min(3, parts.Length)));
                        (int l, int i) = kinds.GetValueOrDefault(kind);
                        kinds[kind] = isInert ? (l, i + 1) : (l + 1, i);
                    }
                }
            }
            int deleted = 0;
            foreach ((string path, int wanted) in wantedByType)
            {
                int alive = aliveByType.GetValueOrDefault(path);
                if (alive != wanted)
                    _output.WriteLine($"  {path}: {wanted} in tg's map file, {alive} alive");
                deleted += wanted - alive;
            }
            _output.WriteLine($"movables: {inMapFile} in tg's map file, {live} live + {inert} inert alive, {deleted} deleted");
            foreach (var (kind, (l, i)) in kinds)
                _output.WriteLine($"  {kind}: {l} live, {i} inert");
            // tg's map file has 35,364 movables. After the init the transient ones are gone (spawners, mapping helpers and turf
            // decals delete themselves), and what they and the dirty station made is there instead, so the totals are not
            // compared; the types are: a type of tg's map file with fewer alive than the file has is transient, or one of the
            // walled-up airlocks (abandoned.dm replaces it), or a cleanable decal or a stack that merged into its twin.
            // (35,364 as tg's map was converted; the map is edited in the map editor now, D337, so the count is the mapper's)
            Assert.InRange(inMapFile, 30000, 60000);
            var lost = wantedByType.Where(p => aliveByType.GetValueOrDefault(p.Key) < p.Value).Select(p => p.Key)
                .Where(p => !IsTransient(p) && !IsCleanable(p) && !IsStack(p) && !abandonable.Contains(p)).ToList();
            Assert.True(lost.Count == 0, "types of tg's map that are fewer on Meta than in the file, and are not transient: " + string.Join(", ", lost));
            // every turf decal is gone (decal.dm: INITIALIZE_HINT_QDEL after the turf has its decal element)
            Assert.Equal(0, aliveByType.Where(p => p.Key.StartsWith("/obj/effect/turf_decal", StringComparison.Ordinal)).Sum(p => p.Value));
            // The measured split at main ec4af41 (I1a merged): 20,789 live + 9,257 inert alive. A type going live moves movables
            // from inert to live (and a live spawner is gone), so inert may only fall.
            Assert.True(inert <= 9257, $"inert movables: {inert}");
        }
    }

    // ---- the landmarks of Meta against tg's map file

    [MetaTgFact]
    public void The_start_landmarks_and_the_spawns_follow_tgs_creation_order()
    {
        TgDmm dmm = TgDmm.Load(TgDmmPath());
        // the reader's order: columns left to right, each from its top row down, a tile's members in model order
        var expectedStarts = new List<(int X, int Y, string Path)>();
        var expectedAssistants = new List<(int X, int Y)>();
        for (int x = 1; x <= dmm.MaxX; x++)
        {
            for (int y = dmm.MaxY; y >= 1; y--)
            {
                foreach (string path in dmm.At(x, y, 1).SkipLast(2))
                {
                    if (!path.StartsWith("/obj/effect/landmark/start", StringComparison.Ordinal) || TakenAway.Contains((path, x, y))) continue;
                    expectedStarts.Add((x, y, path));
                    if (path == "/obj/effect/landmark/start/assistant" || path == "/obj/effect/landmark/start/assistant/override")
                        expectedAssistants.Add((x, y));
                }
            }
        }
        World world = _meta.World;
        using (world.Enter())
        {
            // only the types that are live run /obj/effect/landmark/start/Initialize and register (the others are inert)
            var live = expectedStarts.Where(e => !InertSeam.IsInertType(DmTypes.FromPath(e.Path))).ToList();
            var registered = GLOB.start_landmarks_list.Items().Cast<obj_effect_landmark_start>()
                .Select(l => ((int)((turf)l.loc).x, (int)((turf)l.loc).y, DmTypes.PathOf(l.GetType()))).ToList();
            _output.WriteLine($"start landmarks: {expectedStarts.Count} in tg's map file, {live.Count} of live types, {registered.Count} registered; {expectedAssistants.Count} Assistant");
            foreach (var group in expectedStarts.Where(e => InertSeam.IsInertType(DmTypes.FromPath(e.Path))).GroupBy(e => e.Path).OrderByDescending(g => g.Count()).Take(10))
                _output.WriteLine($"  inert, so not registered: {group.Count()} x {group.Key}");
            Assert.Equal(live, registered);
        }
        // the spawns are the docked arrivals shuttle's chairs in SSjob.latejoin_trackers' order (arrivals.dm LateInitialize, M10 slice G3)
        List<SpawnPoint> chairs;
        using (world.Enter())
            chairs = SSjob.latejoin_trackers.Items().Cast<atom>().Select(c => new SpawnPoint((int)c.x, (int)c.y, (int)c.z)).ToList();
        Assert.NotEmpty(chairs);
        Assert.Equal(chairs, MapSeam.LatejoinSpawns(world).ToList());
        Assert.Equal(20 - TakenAway.Count(g => g.Type == "/obj/effect/landmark/start/assistant"), expectedAssistants.Count); // (tg's map has 20)
    }

    // ---- required_map_items

    private static readonly (string Root, bool Subtypes, int Min, float Max)[] RequiredItems =
    {
        // tg: unit_tests/required_map_items.dm setup_expected_types and the REGISTER_REQUIRED_MAP_ITEM(min, max) of each type
        ("/obj/item/stamp/head", true, 1, float.PositiveInfinity),                       // paperwork/stamps.dm:42
        ("/obj/machinery/modular_computer/preset/cargochat", true, 1, 1),               // modular_computers/.../console_presets.dm:106
        ("/obj/machinery/fax/heads", true, 1, 1),                                        // paperwork/fax.dm:81
        ("/mob/living/basic/parrot/poly", false, 1, 1),                                  // parrot/poly.dm:39
        ("/mob/living/basic/pet/dog/corgi/ian", false, 1, 1),                            // dog/corgi.dm:385
        ("/mob/living/carbon/human/species/monkey/punpun", false, 1, 1),                 // human/monkey.dm:49
        ("/obj/machinery/computer/communications", false, 1, float.PositiveInfinity),   // computer/communications.dm:93
        ("/obj/machinery/drone_dispenser", false, 1, 1),                                 // machinery/drone_dispenser.dm:84
        ("/obj/item/piggy_bank/vault", false, 1, 1),                                     // items/piggy_bank.dm:187
        ("/obj/item/storage/photo_album/bar", false, 1, 1),                              // photography/photos/album.dm:93-198
        ("/obj/item/storage/photo_album/chapel", false, 1, 1),
        ("/obj/item/storage/photo_album/library", false, 1, 1),
        ("/obj/item/storage/photo_album/prison", false, 1, 1),
        ("/obj/item/storage/photo_album/captain", false, 1, 1),
        ("/obj/item/storage/photo_album/ce", false, 1, 1),
        ("/obj/item/storage/photo_album/cmo", false, 1, 1),
        ("/obj/item/storage/photo_album/hop", false, 1, 1),
        ("/obj/item/storage/photo_album/hos", false, 1, 1),
        ("/obj/item/storage/photo_album/qm", false, 1, 1),
        ("/obj/item/storage/photo_album/rd", false, 1, 1),
    };

    // Expected items that tg's own MetaStation.dmm does not map (only other maps do): at the snapshot the real maptest_required_map_items
    // would not pass on Meta for them, so the port's subset leaves them out (D108's station level is Meta). TgDmm checks the claim.
    private static readonly string[] NotMappedByTgsMeta =
    {
        "/obj/item/piggy_bank/vault",
        "/obj/item/storage/photo_album/captain", "/obj/item/storage/photo_album/ce", "/obj/item/storage/photo_album/cmo",
        "/obj/item/storage/photo_album/hop", "/obj/item/storage/photo_album/hos", "/obj/item/storage/photo_album/qm",
        "/obj/item/storage/photo_album/rd",
    };

    // tg: unit_tests/required_map_items.dm (maptest_required_map_items). tg counts through REGISTER_REQUIRED_MAP_ITEM in each
    // type's Initialize (mapload, station z), which only runs for live types; most of these types are inert here, so the
    // port counts the exact types on the station level of the loaded world, which is what the registrations count.
    [MetaFact]
    public void Ported_maptest_required_map_items_subset_Meta_maps()
    {
        World world = _meta.World;
        var fails = new List<string>();
        using (world.Enter())
        {
            var count = new Dictionary<Type, int>();
            for (int y = 1; y <= Dm.world.maxy; y++)
                for (int x = 1; x <= Dm.world.maxx; x++)
                    foreach (atom_movable m in locate(x, y, 1).contents.Items())
                        count[m.GetType()] = count.GetValueOrDefault(m.GetType()) + 1;

            // tg registers Ian at his Initialize, before he reads his memory: with no save he goes on as his puppy (corgi.dm:385-393)
            count[typeof(mob_living_basic_pet_dog_corgi_ian)] = count.GetValueOrDefault(typeof(mob_living_basic_pet_dog_corgi_ian)) + count.GetValueOrDefault(typeof(mob_living_basic_pet_dog_corgi_puppy_ian));
            var checkedTypes = new HashSet<Type>();
            var roots = new List<Type>();
            foreach (var (root, subtypes, min, max) in RequiredItems)
            {
                Type rootType = DmTypes.FromPath(root);
                Assert.NotNull(rootType);
                roots.Add(rootType);
                IEnumerable<Type> expected = subtypes ? DmTypes.Subtypes(rootType, false) : new[] { rootType };
                foreach (Type got_type in expected)
                {
                    checkedTypes.Add(got_type);
                    int items_found = count.GetValueOrDefault(got_type);
                    string path = DmTypes.PathOf(got_type);
                    if (NotMappedByTgsMeta.Contains(path))
                    {
                        if (items_found != 0) fails.Add($"Item {path} is not mapped in tg's Meta but {items_found} are in the loaded world");
                        continue;
                    }
                    if (items_found <= 0) fails.Add($"Item {path} was not found, but is expected to be mapped in on mapload!");
                    else if (items_found < min) fails.Add($"Item {path} should have at least {min} mapped in but only had {items_found} on mapload!");
                    else if (items_found > max) fails.Add($"Item {path} should have at most {max} mapped in but had {items_found} on mapload!");
                }
            }
            // tg's last assertion: a registered type that the test does not check. Registered types are exactly those of the
            // roots above and their subtypes that exist on the map.
            foreach ((Type type, int n) in count)
            {
                if (checkedTypes.Contains(type)) continue;
                if (roots.Any(r => DmTypes.IsSubtypeOrSame(type, r)))
                    fails.Add($"The following paths were found in required map items, but weren't checked: {DmTypes.PathOf(type)} x{n}");
            }
        }
        Assert.True(fails.Count == 0, string.Join("\n", fails.Take(30)));
    }

    [MetaTgFact]
    public void The_required_items_tgs_Meta_does_not_map_are_really_absent_from_its_map_file()
    {
        string text = File.ReadAllText(TgDmmPath());
        foreach (string path in NotMappedByTgsMeta)
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(text, "^" + System.Text.RegularExpressions.Regex.Escape(path) + @"[,{)]", System.Text.RegularExpressions.RegexOptions.Multiline), path);
    }

    // tg: unit_tests/map_landmarks.dm (maptest_job_roundstart_spawnpoints): every job with spawn positions has a default round
    // start landmark. P2 ported only the Assistant. SSjob.joinable_occupations is built by SSjob (M10); here the jobs are tg's
    // non-abstract /datum/job types with JOB_NEW_PLAYER_JOINABLE (job.dm:165-179 without config_check and map_check), and
    // get_default_roundstart_spawn_point is _job.dm:498-509 over GLOB.start_landmarks_list. A job whose landmark type is
    // inert (the department security officers' and the AI's, M10) has none in the port's list: the test then asks tg's own
    // map file for a landmark of that name, so a job that tg's Meta does not provide for at all still fails.
    [MetaTgFact]
    public void Ported_maptest_job_roundstart_spawnpoints()
    {
        TgDmm dmm = TgDmm.Load(TgDmmPath());
        var mappedByName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // landmark name -> the map file's types
        World world = _meta.World;
        var fails = new List<string>();
        var viaInert = new List<string>();
        int jobs = 0;
        using (world.Enter())
        {
            // the names of the landmark types, from their prototypes
            foreach (string key in dmm.Cells.Values.Distinct())
            {
                foreach (string path in dmm.Models[key].SkipLast(2).Where(p => p.StartsWith("/obj/effect/landmark/start", StringComparison.Ordinal)).Distinct())
                {
                    string name = Dm.initial(DmTypes.FromPath(path), "name") as string;
                    if (!mappedByName.TryGetValue(name, out HashSet<string> set)) mappedByName[name] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.Add(path);
                }
            }
            foreach (Type job_type in DmTypes.Subtypes(typeof(datum_job), false))
            {
                if (Equals(Dm.initial(job_type, "abstract_type"), job_type)) continue; // valid_subtypesof
                if (!Dm.truthy(Dm.bitand(Dm.initial(job_type, "job_flags"), JOB_NEW_PLAYER_JOINABLE))) continue;
                if (Dm.num(Dm.initial(job_type, "spawn_positions")) <= 0) continue; // zero spawn positions: no landmark needed
                jobs++;
                string title = (string)Dm.initial(job_type, "title");
                // get_default_roundstart_spawn_point: the first landmark of the job's title
                bool found = GLOB.start_landmarks_list.Items().Cast<obj_effect_landmark_start>().Any(l => l.name == title);
                if (found) continue;
                if (mappedByName.TryGetValue(title, out HashSet<string> types) && types.All(t => InertSeam.IsInertType(DmTypes.FromPath(t))))
                    viaInert.Add($"{title} ({string.Join(", ", types)})");
                else
                    fails.Add($"Job {title} ({DmTypes.PathOf(job_type)}) has no default roundstart spawn landmark.");
            }
        }
        _output.WriteLine($"{jobs} jobs with spawn positions; without a registered landmark because its type is inert: {string.Join("; ", viaInert)}");
        Assert.True(jobs > 20, $"only {jobs} jobs found");
        Assert.True(fails.Count == 0, string.Join("\n", fails));
    }
}
