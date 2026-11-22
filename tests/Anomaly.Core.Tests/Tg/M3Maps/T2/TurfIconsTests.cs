using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Anomaly.Tools.Icons;
using static Anomaly.Core.Tg.Builtins;
using Xunit;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>A fact that needs the tg clone (its dmi files); skipped without it, as the icon tests are.</summary>
public sealed class TgCloneFactAttribute : FactAttribute
{
    public TgCloneFactAttribute()
    {
        if (MapFixtures.TgClone() == null)
            Skip = "no tg clone (ANOMALY_TG or ../tgstation)";
    }
}

/// <summary>
/// M3 T2: tg's unit_tests/turf_icons.dm (maptest_turf_icons): every turf type's default icon_state exists in its icon file.
/// The icon states are read from the dmi files of the tg clone (tools/Anomaly.Tools.Icons' reader). The generated types carry
/// OpenDream's folded defaults, so this also checks the generator's icon and icon_state values of every turf type.
/// </summary>
public class TurfIconsTests
{
    private static readonly Dictionary<string, HashSet<string>> StatesCache = new(StringComparer.Ordinal);

    private static HashSet<string> StatesOf(string clone, string resourcePath)
    {
        if (StatesCache.TryGetValue(resourcePath, out HashSet<string> cached)) return cached;
        string file = Path.Combine(clone, resourcePath);
        HashSet<string> states = null;
        if (File.Exists(file))
        {
            try
            {
                states = new HashSet<string>(DmiReader.Read(File.ReadAllBytes(file)).States.Select(s => s.Name), StringComparer.Ordinal);
            }
            catch (InvalidDataException)
            {
                // a png without a DMI description is one unnamed icon state for BYOND
                states = new HashSet<string>(StringComparer.Ordinal) { "" };
            }
        }
        lock (StatesCache) StatesCache[resourcePath] = states;
        return states;
    }

    // icon_exists (__HELPERS/icons.dm:1214-1220): false for a null file or state, else the state is in the file's icon states
    private static bool IconExists(string clone, object icon, string state)
    {
        if (icon is not DmResource resource || state == null) return false;
        HashSet<string> states = StatesOf(clone, resource.Path);
        return states != null && states.Contains(state);
    }

    [TgCloneFact]
    public void Ported_maptest_turf_icons()
    {
        string clone = MapFixtures.TgClone();
        var fails = new List<string>();
        Type mineral = DmTypes.FromPath("/turf/closed/mineral");
        Type rock = DmTypes.FromPath("/turf/closed/indestructible/rock");
        IReadOnlyList<Type> all = DmTypes.Subtypes(typeof(turf), false);
        int checkedTypes = 0;
        foreach (Type turf_path in all)
        {
            if (DmTypes.IsSubtypeOrSame(turf_path, mineral) || DmTypes.IsSubtypeOrSame(turf_path, rock)) continue;
            if (Dm.initial(turf_path, "icon_state") is not string icon_state || Dm.initial(turf_path, "icon") is not DmResource icon_file)
                continue; // tg: `if(isnull(icon_state) || isnull(icon_file)) continue`
            // PORT: the empty icon_state. tg's CI passes /turf/closed/wall/mineral (mineral_walls.dm:4 `icon_state = ""`, wall.dmi has
            // no unnamed state), so BYOND's initial() reads an empty icon_state like null here: the unnamed state is not checked.
            if (icon_state.Length == 0) continue;
            checkedTypes++;
            if (!IconExists(clone, icon_file, icon_state))
                fails.Add($"{DmTypes.PathOf(turf_path)} using invalid icon_state - \"{icon_state}\" in icon file, '{icon_file.Path}");
        }
        // minerals use a special MAP_SWITCH definition (the compile-time state is the generator's value): the state may also be
        // in icons/turf/mining.dmi (tg: the second loop of the test)
        foreach (Type turf_path in DmTypes.Subtypes(mineral, true))
        {
            if (Dm.initial(turf_path, "icon_state") is not string icon_state || Dm.initial(turf_path, "icon") is not DmResource icon_file)
                continue;
            checkedTypes++;
            if (!IconExists(clone, icon_file, icon_state) && !(StatesOf(clone, "icons/turf/mining.dmi")?.Contains(icon_state) ?? false))
                fails.Add($"{DmTypes.PathOf(turf_path)} using invalid icon_state - \"{icon_state}\" in icon file, '{icon_file.Path}");
        }
        Assert.True(checkedTypes > 300, $"only {checkedTypes} turf types had an icon and a state");
        Assert.True(fails.Count == 0, $"{fails.Count} of {checkedTypes}:\n" + string.Join("\n", fails.Take(30)));
    }

    /// <summary>
    /// The third loop of maptest_turf_icons (turf_icons.dm:34-57): every /turf/open type with a damaged_dmi is made with
    /// ChangeTurf, and each state of its burnt_states() and broken_states() exists in that file. PORT: a floor type whose
    /// burnt_states/broken_states override is not translated (wood, carpet, engine, ... are inert at this snapshot) would
    /// answer with the base class's list, which says nothing about tg's, so inert types are counted and not checked.
    /// </summary>
    [TgCloneFact]
    public void Ported_maptest_turf_icons_burnt_and_broken_states()
    {
        string clone = MapFixtures.TgClone();
        var fails = new List<string>();
        int checkedTypes = 0, inertTypes = 0;
        var room = TgToyRoom.Create(5, 5);
        using (room.World.Enter())
        {
            foreach (Type open_turf_path in DmTypes.Subtypes(typeof(turf_open), false))
            {
                if (Dm.initial(open_turf_path, "damaged_dmi") is not DmResource damaged_dmi) continue;
                if (InertSeam.IsInertType(open_turf_path))
                {
                    inertTypes++;
                    continue;
                }
                turf bottom_left = locate(2, 2, 1);
                bottom_left = Waiting.NoSleep(bottom_left.dm_change_turf(open_turf_path, null, null)) as turf;
                // tg: `run_loc_floor_bottom_left = get_turf(locate(/obj/effect/landmark/unit_test_bottom_left) ...)`, the turf
                // after an Initialize that replaced it; the port's ChangeTurf returns the final turf
                if (bottom_left is not turf_open instanced_turf) continue;
                checkedTypes++;
                foreach (object state in Dm.each_cast<object>(instanced_turf.burnt_states()))
                {
                    if (!IconExists(clone, damaged_dmi, (string)state))
                        fails.Add($"{DmTypes.PathOf(open_turf_path)} has an invalid icon in burnt_states - \"{state}\", in '{damaged_dmi.Path}'");
                }
                foreach (object state in Dm.each_cast<object>(instanced_turf.broken_states()))
                {
                    if (!IconExists(clone, damaged_dmi, (string)state))
                        fails.Add($"{DmTypes.PathOf(open_turf_path)} has an invalid icon in broken_states - \"{state}\", in '{damaged_dmi.Path}'");
                }
            }
        }
        Assert.True(checkedTypes >= 3, $"only {checkedTypes} live open turf types with a damaged_dmi ({inertTypes} inert)");
        Assert.True(fails.Count == 0, $"{fails.Count} of {checkedTypes}:\n" + string.Join("\n", fails.Take(30)));
    }
}
