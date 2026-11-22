using System.Diagnostics;
using System.IO;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Sdk;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>The converted MetaStation's maps folder (maps/ with map_jsons/metastation.json), or null.</summary>
internal static class MetaMaps
{
    internal static string Folder()
    {
        string meta = MapFixtures.ConvertedMeta();
        if (meta == null) return null;
        // <folder>/map_files/MetaStation/MetaStation.as3dmap
        string folder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(meta)));
        return File.Exists(Path.Combine(folder, "map_jsons", "metastation.json")) ? folder : null;
    }

    /// <summary>
    /// Boots Meta's station level (MapSeam.Select, TgWorld.CompiledMap) and steps until the Master initialized, once the memory has
    /// room for it (<see cref="MetaMemoryGate"/>).
    /// </summary>
    internal static World Boot(out TgTestWorld.Log log, ulong seed = 1)
    {
        (World world, TgTestWorld.Log booted) = MetaMemoryGate.Boot(() =>
        {
            World w = TgTestWorld.Boot(out TgTestWorld.Log l, seed, configure: x => MapSeam.Select(x, new MapSelection(Folder(), "metastation")), compiledMap: TgWorld.CompiledMap, maxTicks: 400);
            return (w, l);
        });
        log = booted;
        return world;
    }

    /// <summary>
    /// The map digest (plan.md): every cell of every level in BYOND's order, its turf's and its movables' (in contents order)
    /// type, density and appearance (icon, state, dir, colour, alpha, layer, plane, pixel offsets, invisibility, opacity),
    /// as a 64-bit FNV-1a hash and the number of atoms it covered.
    /// </summary>
    internal static (ulong Hash, int Atoms) Digest(World world)
    {
        using (world.Enter())
        {
            ulong hash = 14695981039346656037UL;
            int atoms = 0;
            var sb = new StringBuilder(256);
            void Add(atom a)
            {
                sb.Clear();
                sb.Append(DmTypes.PathOf(a.GetType())).Append('|').Append(a.density).Append('|').Append(a.icon).Append('|')
                    .Append(a.icon_state).Append('|').Append(a.dir).Append('|').Append(a.color).Append('|').Append(a.alpha).Append('|')
                    .Append(a.layer).Append('|').Append(a.plane).Append('|').Append(a.pixel_x).Append('|').Append(a.pixel_y).Append('|')
                    .Append(a.invisibility).Append('|').Append(a.opacity).Append('\n');
                foreach (byte b in Encoding.UTF8.GetBytes(sb.ToString()))
                {
                    hash ^= b;
                    hash *= 1099511628211UL;
                }
                atoms++;
            }
            for (int z = 1; z <= Dm.world.maxz; z++)
            {
                for (int y = 1; y <= Dm.world.maxy; y++)
                {
                    for (int x = 1; x <= Dm.world.maxx; x++)
                    {
                        turf t = locate(x, y, z);
                        Add(t);
                        Add((atom)t.loc);
                        foreach (atom thing in t.dm_held) Add(thing);
                    }
                }
            }
            return (hash, atoms);
        }
    }
}

/// <summary>A fact that needs the converted MetaStation (tools/convert-maps.sh); skipped without it.</summary>
[TraitDiscoverer(MetaTraitDiscoverer.TypeName, MetaTraitDiscoverer.AssemblyName)]
public sealed class MetaFactAttribute : FactAttribute, ITraitAttribute
{
    public MetaFactAttribute()
    {
        if (MetaMaps.Folder() == null)
            Skip = "no converted MetaStation (run tools/convert-maps.sh, or set ANOMALY_META_MAP)";
    }
}

/// <summary>A Meta fact marked slow (two full boots): it runs with ANOMALY_SLOW=1.</summary>
[TraitDiscoverer(MetaTraitDiscoverer.TypeName, MetaTraitDiscoverer.AssemblyName)]
public sealed class SlowMetaFactAttribute : FactAttribute, ITraitAttribute
{
    public SlowMetaFactAttribute()
    {
        if (MetaMaps.Folder() == null)
            Skip = "no converted MetaStation (run tools/convert-maps.sh, or set ANOMALY_META_MAP)";
        else if (Environment.GetEnvironmentVariable("ANOMALY_SLOW") != "1")
            Skip = "slow (two full boots of Meta): ANOMALY_SLOW=1 dotnet test tests/Anomaly.Core.Tests -c Release --filter Category=Slow";
    }
}

/// <summary>One booted Meta (seed 1) shared by the tests of a class (read-only: the tests do not change it).</summary>
public sealed class MetaWorldFixture
{
    public World World { get; }

    public TgTestWorld.Log Log { get; }

    public double BootSeconds { get; }

    public MetaWorldFixture()
    {
        if (MetaMaps.Folder() == null) return;
        var sw = Stopwatch.StartNew();
        TgTestWorld.Log log;
        World = MetaMaps.Boot(out log);
        Log = log;
        BootSeconds = sw.Elapsed.TotalSeconds;
    }
}
