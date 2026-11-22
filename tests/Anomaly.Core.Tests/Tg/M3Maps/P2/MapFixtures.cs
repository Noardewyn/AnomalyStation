using System.IO;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>Where the map tests find the tg clone, the converted maps and the hand-written fixtures (they skip without them).</summary>
internal static class MapFixtures
{
    /// <summary>The repository root (the folder with AnomalyStation.sln), from the test's folder upward.</summary>
    internal static string RepoRoot()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "AnomalyStation.sln"))) return dir;
        }
        return null;
    }

    /// <summary>The tg clone (<c>ANOMALY_TG</c>, or <c>../tgstation</c> beside the main checkout), or null.</summary>
    internal static string TgClone()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_TG");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "_maps"))) return env;
        for (string dir = RepoRoot(); dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tgstation");
            if (Directory.Exists(Path.Combine(candidate, "_maps"))) return candidate;
        }
        return null;
    }

    /// <summary>The converted MetaStation (<c>ANOMALY_META_MAP</c>, or <c>maps/</c> from the repository root upward), or null.</summary>
    internal static string ConvertedMeta()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_META_MAP");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
        for (string dir = RepoRoot(); dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "maps", "map_files", "MetaStation", "MetaStation.as3dmap");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>The items of a DM list, in order (DmList is not an IEnumerable).</summary>
    internal static IEnumerable<object> Items(this Anomaly.Core.DmShim.DmList list)
    {
        for (int i = 1; i <= list.Count; i++) yield return list[i];
    }

    /// <summary>The folder of the hand-written fixture maps (<c>tests/fixtures/maps</c>).</summary>
    internal static string FixtureFolder() => Path.Combine(RepoRoot(), "tests", "fixtures", "maps");
}
