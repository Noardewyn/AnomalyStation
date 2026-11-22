using System.Globalization;
using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: boots the hand-written fixture maps of tests/fixtures/maps/t2 (small TGM maps, converted with the map
/// converter's library entry point against types-index.tsv, or permissively for the maps that use the test-assembly
/// types of <see cref="T2Probes"/>) through tg's own load path: <see cref="MapSeam.Select"/> mounts the json and the map,
/// <see cref="TgWorld.CompiledMap"/> sizes the world, SSmapping's loadWorld loads the station level.
/// </summary>
internal static class T2Maps
{
    /// <summary>The maps folder of the fixture <paramref name="name"/> (map_jsons/<name>.json, map_files/...).</summary>
    internal static string Folder(string name) => Path.Combine(MapFixtures.FixtureFolder(), "t2", name);

    /// <summary>The map selection of the fixture <paramref name="name"/>.</summary>
    internal static MapSelection Selection(string name) => new(Folder(name), name);

    /// <summary>
    /// Boots the fixture: world seed <paramref name="seed"/>, <paramref name="configure"/> runs before the boot (after the
    /// map was selected), <paramref name="compiledMap"/> replaces <see cref="TgWorld.CompiledMap"/> (a world bigger than the
    /// map, for the centring), and the world is stepped until the Master initialized everything.
    /// </summary>
    internal static World Boot(string name, out TgTestWorld.Log log, ulong seed = 1, Action<World> configure = null, Action<World> compiledMap = null)
        => TgTestWorld.Boot(out log, seed, configure: w =>
        {
            MapSeam.Select(w, Selection(name));
            configure?.Invoke(w);
        }, compiledMap: compiledMap ?? TgWorld.CompiledMap);

    /// <summary>Boots the fixture and returns the world only.</summary>
    internal static World Boot(string name) => Boot(name, out _);
}

/// <summary>The per-world trace of the probe types (a Kernel extension, so no static state): "&lt;id&gt; &lt;event&gt;".</summary>
internal sealed class T2Trace
{
    public readonly List<string> Lines = new();

    public static T2Trace Of(World world) => world.GetExtension(static _ => new T2Trace());

    public static void Add(datum who, string what) => Of(World.Current).Lines.Add(Id(who) + " " + what);

    public static string Id(datum d) => d == null ? "null" : d.dm_id.ToString(CultureInfo.InvariantCulture);
}
