using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// tg's map unit tests (code/modules/unit_tests/{area_contents,mapload_space_verification,mapping_nearstation_test,mapping}.dm)
/// as functions over a world: each returns its TEST_FAIL messages, so the same check runs on the fixture maps (with positive
/// and negative controls) and on converted Meta. They run inside <c>world.Enter()</c>. Differences from tg's text are marked
/// "PORT"; everything else follows the DM statement for statement.
/// </summary>
internal static class TgMapChecks
{
    private static string Coord(turf t) => $"({t.x},{t.y},{t.z})";

    // ALL_TURFS() (__DEFINES/..): block(1,1,1 to maxx,maxy,maxz)
    internal static IEnumerable<turf> AllTurfs()
    {
        for (float z = 1; z <= Dm.world.maxz; z++)
            for (float y = 1; y <= Dm.world.maxy; y++)
                for (float x = 1; x <= Dm.world.maxx; x++)
                    yield return locate(x, y, z);
    }

    private static string ZLevelName(float i) => (SSmapping.get_level(i) as datum_space_level)?.name;

    /// <summary>tg: unit_tests/area_contents.dm (maptest_area_contents).</summary>
    internal static List<string> AreaContents()
    {
        var fails = new List<string>();
        // PORT: tg keeps the "already listed" mark in the turf's test-only var in_contents_of (turf.dm:93-98, UNIT_TESTS)
        var in_contents_of = new Dictionary<turf, area>(ReferenceEqualityComparer.Instance);
        // First, we check that there are no entries in more then one area
        // That or duplicate entries
        foreach (area area_to_test in Dm.each<area>(GLOB.areas))
        {
            area_to_test.cannonize_contained_turfs();
            for (float i = 1; i <= area_to_test.turfs_by_zlevel.len; i++)
            {
                if (!islist(area_to_test.turfs_by_zlevel[i]))
                {
                    fails.Add($"zlevel index {i} (zlevel name: {ZLevelName(i)}) in {DmTypes.PathOf(area_to_test.GetType())} is not a list.");
                    continue;
                }

                foreach (object item in Dm.each(area_to_test.turfs_by_zlevel[i]))
                {
                    if (item is not turf turf_to_check)
                    {
                        fails.Add($"Found a {item} (zlevel name: {ZLevelName(i)}) in {DmTypes.PathOf(area_to_test.GetType())}'s turf listing");
                        continue;
                    }

                    if (in_contents_of.TryGetValue(turf_to_check, out area existing))
                    {
                        if (ReferenceEquals(existing, turf_to_check.loc))
                            fails.Add($"Found a duplicate turf {DmTypes.PathOf(turf_to_check.GetType())} {Coord(turf_to_check)} (zlevel name: {ZLevelName(i)}) inside {DmTypes.PathOf(area_to_test.GetType())}'s turf listing");
                        else
                            fails.Add($"Found a shared turf {DmTypes.PathOf(turf_to_check.GetType())} {Coord(turf_to_check)} (zlevel name: {ZLevelName(i)}) between {DmTypes.PathOf(area_to_test.GetType())} and {DmTypes.PathOf(existing.GetType())}'s turf listings");
                    }

                    var turfs_actual_area = turf_to_check.loc as area;
                    if (turfs_actual_area == null)
                    {
                        // PORT: a replaced turf has no loc (object-model.md 7.1); in BYOND the listing names the new turf of the cell
                        fails.Add($"Found a deleted turf {DmTypes.PathOf(turf_to_check.GetType())} (zlevel name: {ZLevelName(i)}) in {DmTypes.PathOf(area_to_test.GetType())}'s turf listing");
                        continue;
                    }
                    if (!ReferenceEquals(turfs_actual_area, area_to_test))
                        fails.Add($"Found a turf {DmTypes.PathOf(turf_to_check.GetType())} {Coord(turf_to_check)} (zlevel name: {ZLevelName(i)}) which is IN {DmTypes.PathOf(turfs_actual_area.GetType())}, but is registered as being in {DmTypes.PathOf(area_to_test.GetType())}");

                    in_contents_of[turf_to_check] = turfs_actual_area;
                }
            }
        }

        foreach (turf position in AllTurfs())
        {
            if (!in_contents_of.ContainsKey(position))
                fails.Add($"Found a turf {DmTypes.PathOf(position.GetType())} {Coord(position)} ({ZLevelName(position.z)}) inside {DmTypes.PathOf(position.loc.GetType())} that is NOT stored in any area's turf listing");
        }
        return fails;
    }

    /// <summary>tg: unit_tests/mapload_space_verification.dm (maptest_mapload_space_verification), planetary branch included.</summary>
    internal static List<string> MaploadSpaceVerification()
    {
        var fails = new List<string>();
        // Is our current map a planetary station (NO space turfs allowed)? If so, check for ANY space turfs.
        if (Dm.truthy(SSmapping.is_planetary()))
        {
            // We want to get both the station level and the mining level (if the two are seperate for any reason).
            var testable_levels = new List<object>();
            testable_levels.AddRange(((DmList)SSmapping.levels_by_trait(ZTRAIT_STATION)).Items());
            foreach (object mining_level in Dm.each(SSmapping.levels_by_trait(ZTRAIT_MINING)))
            {
                if (testable_levels.Any(l => Dm.eq(l, mining_level)))
                    continue;
                testable_levels.Add(mining_level);
            }
            foreach (object level in testable_levels)
            {
                // Remember, any space turf is a failure.
                for (float y = 1; y <= Dm.world.maxy; y++)
                    for (float x = 1; x <= Dm.world.maxx; x++)
                        if (locate(x, y, Dm.num(level)) is turf_open_space iterated_turf)
                            fails.Add($"Space turf found on planetary map! {Coord(iterated_turf)} ({DmTypes.PathOf(iterated_turf.loc.GetType())}) Please verify the map file and remove any space turfs.");
            }
            return fails;
        }

        // Let's explicitly outline valid areas to catch anything that we're... okay with for now.
        Type[] excluded =
        {
            typeof(area_space), typeof(area_shuttle_transit), typeof(area_ruin_space), typeof(area_ruin_unpowered_no_grav),
            typeof(area_shuttle_ruin), typeof(area_shuttle_abandoned), typeof(area_station_solars), typeof(area_station_engineering_atmos_space_catwalk),
        };
        // We aren't planetary, so let's check area placements and ensure stuff lines up.
        foreach (turf iterated_turf in AllTurfs())
        {
            var turf_area = (area)iterated_turf.loc;
            if (!isspaceturf(iterated_turf) || excluded.Any(t => t.IsInstanceOfType(turf_area)))
                continue;
            // PORT: TRAIT_HYPERSPACE_STOPPED (a docked shuttle with a template_noop turf on top of us) cannot occur: no shuttle is loaded (D108)
            fails.Add($"Space turf {DmTypes.PathOf(iterated_turf.GetType())} found in non-allowed area ({DmTypes.PathOf(turf_area.GetType())}) at {DmTextRender.StripMarkers(turf_area.name)} {Coord(iterated_turf)}! Please ensure that all space turfs are in an /area/space!");
        }
        return fails;
    }

    /// <summary>tg: unit_tests/mapping_nearstation_test.dm (maptest_mapping_nearstation_test).</summary>
    internal static List<string> MappingNearstation()
    {
        var fails = new List<string>();
        if (Dm.truthy(SSmapping.is_planetary()))
            return fails; //No need to test for orphaned spaced atoms on this map.

        Type[] safe_atoms = { typeof(atom_movable_mirage_holder), typeof(obj_docking_port), typeof(obj_effect_landmark), typeof(obj_effect_abstract), typeof(obj_effect_mapping_error) };
        Type[] safe_areas = { typeof(area_misc_testroom), typeof(area_station_holodeck) };

        foreach (object station_z in Dm.each(SSmapping.levels_by_trait(ZTRAIT_STATION)))
        {
            for (float y = 1; y <= Dm.world.maxy; y++)
            {
                for (float x = 1; x <= Dm.world.maxx; x++)
                {
                    turf station_turf = locate(x, y, Dm.num(station_z));
                    var turf_area = (area)station_turf.loc;
                    if (Dm.truthy(turf_area.static_lighting) || safe_areas.Any(t => t.IsInstanceOfType(turf_area))) //Only care about turfs that don't have lighting enabled.
                        continue;
                    bool has_thing = false;
                    foreach (atom_movable thing_on_the_turf in Dm.each<atom_movable>(station_turf.contents)) //Find an item on the turf, this can help the mapper identify the turf more easily when combined with the exact coords.
                    {
                        if (safe_atoms.Any(t => t.IsInstanceOfType(thing_on_the_turf)))
                            continue;
                        fails.Add($"{station_turf.x}, {station_turf.y}, {station_turf.z}: {DmTypes.PathOf(thing_on_the_turf.GetType())} with area of type {DmTypes.PathOf(turf_area.GetType())}");
                        has_thing = true;
                        break;
                    }
                    if (!has_thing && !isspaceturf(station_turf) && station_turf is not turf_open_openspace) //In case it's just a turf without an area
                        fails.Add($"{station_turf.x}, {station_turf.y}, {station_turf.z}: {DmTypes.PathOf(station_turf.GetType())} with area of type {DmTypes.PathOf(turf_area.GetType())}");
                }
            }
        }
        return fails;
    }

    private static readonly Regex AreaCoordRegex = new(@"\(-?\d+,-?\d+,(-?\d+)\)", RegexOptions.CultureInvariant);

    /// <summary>
    /// tg: unit_tests/mapping.dm (maptest_log_mapping) with log_mapping (__HELPERS/logging/debug.dm:27-37): under UNIT_TESTS a
    /// log_mapping text that carries an AREACOORD `(x,y,z)` on a station or mining z-level is a failure; one without
    /// coordinates is not. PORT: GLOB.unit_test_mapping_logs does not exist; the port's log_mapping writes to world.log, so the
    /// world log lines are filtered the same way.
    /// </summary>
    internal static List<string> MappingLogs(IEnumerable<string> worldLogLines)
    {
        var fails = new List<string>();
        foreach (string text in worldLogLines)
        {
            Match m = AreaCoordRegex.Match(text);
            if (!m.Success) continue;
            float z = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (Dm.truthy(SSmapping.level_trait(z, ZTRAIT_STATION)) || Dm.truthy(SSmapping.level_trait(z, ZTRAIT_MINING)))
                fails.Add(text);
        }
        return fails;
    }
}
