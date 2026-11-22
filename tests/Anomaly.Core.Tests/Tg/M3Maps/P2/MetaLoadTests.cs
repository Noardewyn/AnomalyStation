using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>
/// M3 P2: converted MetaStation's station level through tg's load path (the counts, the landmarks, the spawns, tg's map
/// tests ported) on one booted world, and its determinism (the map digest of two loads; the full state hash, slow).
/// </summary>
public class MetaLoadTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaLoadTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Meta_loads_with_its_counts()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            Assert.Equal((255f, 255f, 4f), (Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)); // the station, CentCom (D299, DV239), Lavaland (L1, D304) and tg's first transit level (M10 slice G1)
            Assert.Equal("MetaStation", SSmapping.current_map.map_name);
            var turfs = new HashSet<turf>(ReferenceEqualityComparer.Instance);
            var areas = new HashSet<Type>();
            int movables = 0, inert = 0, live = 0;
            var kinds = new SortedDictionary<string, (int Live, int Inert)>(StringComparer.Ordinal);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    turf t = locate(x, y, 1);
                    turfs.Add(t);
                    areas.Add(t.loc.GetType());
                    foreach (atom thing in t.dm_held)
                    {
                        movables++;
                        bool isInert = InertSeam.IsInert(thing);
                        if (isInert) inert++; else live++;
                        string kind = Kind(DmTypes.PathOf(thing.GetType()));
                        (int l, int i) = kinds.GetValueOrDefault(kind);
                        kinds[kind] = isInert ? (l, i + 1) : (l + 1, i);
                    }
                }
            }
            Assert.Equal(65025, turfs.Count);
            Assert.Equal(176, areas.Count); // Meta's 171 and the five areas of the shuttles docked at the station at round start (the cargo shuttle waits at CentCom, D299)
            _output.WriteLine($"Meta after init: {movables} movables on turfs ({live} live, {inert} inert); boot {_meta.BootSeconds:F1} s; runtimes {_meta.Log.Runtimes.Count}");
            foreach ((string kind, (int l, int i)) in kinds)
                _output.WriteLine($"  {kind}: {l} live, {i} inert");
            foreach (var group in _meta.Log.Runtimes.GroupBy(e => e.Message.Length > 120 ? e.Message[..120] : e.Message).OrderByDescending(g => g.Count()).Take(12))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            // The movables of the map are 35,364; the late-join landmarks' QDEL hint removes some, spawners and helpers
            // delete themselves (their types' INITIALIZE_HINT_QDEL), and live Initialize code may make new ones.
            Assert.InRange(movables, 20000, 40000);
        }
    }

    // A coarse kind of a movable's path, for the report.
    private static string Kind(string path)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? "/" + parts[0] + "/" + parts[1] + "/" + parts[2] : path;
    }

    [MetaFact]
    public void The_station_level_has_tgs_traits_and_station_areas()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            var level = (datum_space_level)SSmapping.z_list[1f];
            Assert.Equal("Station", level.name);
            Assert.Equal(Dm.TRUE, SSmapping.level_trait(1f, ZTRAIT_STATION));
            Assert.Equal(CROSSLINKED, level.linkage);
            Assert.Equal(new object[] { 1f }, ((DmList)SSmapping.levels_by_trait(ZTRAIT_STATION)).Items().ToArray());
            Assert.True(GLOB.the_station_areas.len > 100);
            Assert.Contains(typeof(area_station_command_bridge), GLOB.the_station_areas.Items());
            Assert.True(Dm.truthy(GLOB.teleportlocs.len));
        }
    }

    [MetaFact]
    public void The_start_landmarks_stay_and_the_spawns_are_the_assistants_starts()
    {
        World world = _meta.World;
        int expected;
        string text = File.ReadAllText(MapFixtures.ConvertedMeta());
        ParsedMapText map = MapText.Parse(text, "meta");
        expected = 0;
        int assistants = 0;
        for (int y = 1; y <= 255; y++)
        {
            for (int x = 1; x <= 255; x++)
            {
                foreach (MapMember m in map.ModelAt(x, y, 1).Members)
                {
                    if (m.Path.StartsWith("/obj/effect/landmark/start/", StringComparison.Ordinal) || m.Path == "/obj/effect/landmark/start") expected++;
                    if (m.Path == "/obj/effect/landmark/start/assistant") assistants++;
                }
            }
        }
        using (world.Enter())
        {
            // tg: every live start landmark registers in GLOB.start_landmarks_list (landmarks.dm:46); the subtypes with
            // procs of their own stay inert until their milestone and do not.
            int registered = GLOB.start_landmarks_list.len > 0 ? (int)GLOB.start_landmarks_list.len : 0;
            _output.WriteLine($"start landmarks: {expected} on the map, {registered} registered, {assistants} assistants");
            Assert.InRange(registered, assistants, expected);
            // Meta has no late-join landmark: the docked arrivals shuttle's chairs are the spawns (arrivals.dm LateInitialize, M10 slice G3)
            Assert.True(SSjob.latejoin_trackers.len > 0);
        }
        IReadOnlyList<SpawnPoint> spawns = MapSeam.LatejoinSpawns(world);
        using (world.Enter())
        {
            Assert.Equal((int)SSjob.latejoin_trackers.len, spawns.Count);
            foreach (SpawnPoint p in spawns)
                Assert.Contains(locate(p.X, p.Y, p.Z).contents.Items(), o => o is obj_structure_chair c && get_area(c) is area_shuttle_arrival);
        }
    }

    // tg: code/modules/unit_tests/area_contents.dm (maptest_area_contents): no turf in two areas' listings, no duplicate,
    // every listed turf in the area that lists it, every turf of the map listed. The port keeps the listing check in a
    // local set instead of tg's turf var in_contents_of (the test's own bookkeeping).
    [MetaFact]
    public void Ported_maptest_area_contents()
    {
        World world = _meta.World;
        var failures = new List<string>();
        using (world.Enter())
        {
            var listed = new Dictionary<turf, area>(ReferenceEqualityComparer.Instance);
            foreach (area area_to_test in Dm.each<area>(GLOB.areas))
            {
                // tg: area_to_test.cannonize_contained_turfs(): the turfs_to_uncontain lists applied (SSarea_contents' job)
                for (float i = 1; i <= Dm.length(area_to_test.turfs_to_uncontain_by_zlevel) && i <= Dm.length(area_to_test.turfs_by_zlevel); i++)
                {
                    if (area_to_test.turfs_by_zlevel[i] is DmList all && area_to_test.turfs_to_uncontain_by_zlevel[i] is DmList gone && gone.Count > 0)
                    {
                        var drop = new HashSet<object>(gone.Items(), ReferenceEqualityComparer.Instance);
                        var keep = all.Items().Where(t => !drop.Contains(t)).ToList();
                        all.Cut();
                        foreach (object t in keep) all.Add(t);
                    }
                }
                area_to_test.turfs_to_uncontain_by_zlevel = Dm.list();
                for (float i = 1; i <= Dm.length(area_to_test.turfs_by_zlevel); i++)
                {
                    if (area_to_test.turfs_by_zlevel[i] is not DmList zturfs)
                    {
                        failures.Add($"zlevel index {i} in {area_to_test.type} is not a list.");
                        continue;
                    }
                    foreach (object item in zturfs.Items())
                    {
                        if (item is not turf turf_to_check)
                        {
                            failures.Add($"Found a {item} in {area_to_test.type}'s turf listing");
                            continue;
                        }
                        if (listed.TryGetValue(turf_to_check, out area existing))
                            failures.Add(ReferenceEquals(existing, turf_to_check.loc)
                                ? $"Found a duplicate turf {turf_to_check.type} inside {area_to_test.type}'s turf listing"
                                : $"Found a shared turf {turf_to_check.type} between {area_to_test.type} and {existing.type}'s turf listings");
                        if (!ReferenceEquals(turf_to_check.loc, area_to_test))
                            failures.Add($"Found a turf {turf_to_check.type} which is IN {turf_to_check.loc.type}, but is registered as being in {area_to_test.type}");
                        listed[turf_to_check] = (area)turf_to_check.loc;
                    }
                }
            }
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    turf position = locate(x, y, 1);
                    if (!listed.ContainsKey(position))
                        failures.Add($"Found a turf {position.type} ({x},{y},1) inside {position.loc.type} that is NOT stored in any area's turf listing");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    // tg: code/modules/unit_tests/mapload_space_verification.dm (maptest_mapload_space_verification), the station branch:
    // no space turf in an area other than space, transit, the ruins, the solars and Birdshot's space catwalk.
    [MetaFact]
    public void Ported_maptest_mapload_space_verification()
    {
        World world = _meta.World;
        var failures = new List<string>();
        using (world.Enter())
        {
            Assert.False(Dm.truthy(SSmapping.current_map.planetary));
            Type[] excluded =
            {
                typeof(area_space), typeof(area_shuttle_transit), typeof(area_ruin_space), typeof(area_ruin_unpowered_no_grav),
                typeof(area_shuttle_ruin), typeof(area_shuttle_abandoned), typeof(area_station_solars), typeof(area_station_engineering_atmos_space_catwalk),
            };
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    turf iterated_turf = locate(x, y, 1);
                    var turf_area = (area)iterated_turf.loc;
                    if (!isspaceturf(iterated_turf) || excluded.Any(t => t.IsInstanceOfType(turf_area)))
                        continue;
                    failures.Add($"Space turf {iterated_turf.type} found in non-allowed area ({turf_area.type}) at ({x},{y},1)!");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    // tg: code/modules/unit_tests/mapping.dm (maptest_log_mapping): no log_mapping message from the map load. The one
    // message D108 causes is left out: InitializeDefaultZLevels logs that the compiled-in CentCom level is missing (no
    // compiled-in level is loaded, zlevel_manager.dm:13).
    [MetaFact]
    public void Ported_maptest_log_mapping()
    {
        string[] mapping = _meta.Log.Lines.Where(l => l.Contains("map attributes pre-defined", StringComparison.Ordinal) == false)
            .Where(l => l.StartsWith("[MAPPING]", StringComparison.Ordinal) || l.Contains("log_mapping", StringComparison.Ordinal)).ToArray();
        Assert.True(mapping.Length == 0, string.Join("\n", mapping.Take(20)));
    }

    // tg: code/modules/unit_tests/map_landmarks.dm (maptest_job_roundstart_spawnpoints) needs SSjob's joinable occupations
    // (the jobs, M10); its M3 subset: the round-start landmark of the jobs Meta maps exists and is registered.
    [MetaFact]
    public void Ported_maptest_job_roundstart_spawnpoints_subset()
    {
        World world = _meta.World;
        using (world.Enter())
        {
            var names = new HashSet<string>(GLOB.start_landmarks_list.Items().OfType<obj_effect_landmark_start>().Select(l => DmTextRender.StripMarkers(l.name)));
            Assert.Contains(JOB_ASSISTANT, names);
            Assert.True(names.Count >= 1);
        }
    }

    [MetaFact]
    public void Two_loads_of_one_seed_have_the_same_map_digest()
    {
        (ulong hash, int atoms) first = MetaMaps.Digest(_meta.World);
        World second = MetaMaps.Boot(out _);
        Assert.Equal(first, MetaMaps.Digest(second));
        _output.WriteLine($"Meta map digest 0x{first.hash:x16} over {first.atoms} atoms");
    }

    [SlowMetaFact]
    [Trait("Category", "Slow")]
    public void Two_loads_of_one_seed_have_the_same_state_hash()
    {
        World a = MetaMaps.Boot(out _);
        World b = MetaMaps.Boot(out _);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        a.Step(100);
        b.Step(100);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }
}
