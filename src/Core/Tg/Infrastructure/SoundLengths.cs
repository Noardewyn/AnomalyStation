using System.Linq;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// M4b audio slice 1 (docs/.. audio plan 3.3). A host seam, not tg code: tg reads the length of a sound file through rust-g
// (sound_len, sound_len_list; code/__DEFINES/rust_g.dm:488-528), which decodes the file in BYOND's resource folder. The port
// has no resource folder on the server: the host installs a table of the lengths (the server's from the sound tool's index,
// tools/Anomaly.Tools.Sounds, M4b slice 2; Core tests a fixture table), which the engine part's rust-g functions read
// (Builtin/rust_g.builtin.cs). Not world state: the table is the host's data, like the map files.

/// <summary>The lengths of the sound files a host knows (deciseconds, rust-g's unit: tg keeps them in <c>SSsounds.sound_lengths</c>).</summary>
public interface ISoundLengths
{
    /// <summary>
    /// The length in deciseconds of the file at <paramref name="path"/>, the tg resource path as tg writes it
    /// (<c>sound/machines/airlock/airlock.ogg</c>), or null when the host has no such file.
    /// </summary>
    float? Length(string path);
}

/// <summary>The sound length seam: the world's table (none by default: every file is unknown) and its lookups.</summary>
public static class SoundLengths
{
    /// <summary>Installs the world's table (the host, before the world runs; null removes it).</summary>
    public static void Install(World world, ISoundLengths table)
    {
        ArgumentNullException.ThrowIfNull(world);
        Holder.Of(world).Table = table;
        // silicons SI6c: fexists sees tg's sound files the host's index lists (a legacy instrument's note files)
        Anomaly.Core.DmShim.DmWorldState.Of(world).GameFileExists = path => table?.Length(path) != null;
    }

    /// <summary>Installs a table of the given lengths (deciseconds by tg path).</summary>
    public static void Install(World world, IReadOnlyDictionary<string, float> lengths)
    {
        ArgumentNullException.ThrowIfNull(lengths);
        Install(world, new DictionaryTable(lengths));
    }

    /// <summary>
    /// A digest of <paramref name="world"/>'s table (0 without one or for a table that is not a list of lengths): the lengths are
    /// world input (a loop's timing reads them), so a command log records it and a replay checks that it runs with the same table.
    /// </summary>
    public static ulong Digest(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (Holder.Of(world).Table is not DictionaryTable table)
            return 0;
        ulong h = 14695981039346656037UL; // FNV-1a over the sorted paths and their lengths' bits
        foreach (string path in table.Lengths.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            foreach (char c in path)
                h = (h ^ c) * 1099511628211UL;
            h = (h ^ BitConverter.SingleToUInt32Bits(table.Lengths[path])) * 1099511628211UL;
        }
        return h == 0 ? 1 : h;
    }

    /// <summary>
    /// The paths of the current world's table in ordinal order (none without a table, or for a table that is not a list of
    /// lengths): SSsounds' list of every sound file (tg walks BYOND's resource folder for it) reads them.
    /// </summary>
    public static IReadOnlyList<string> Paths()
    {
        World world = World.CurrentOrNull;
        if (world == null || Holder.Of(world).Table is not DictionaryTable table)
            return Array.Empty<string>();
        return table.Lengths.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    /// <summary>The length of <paramref name="path"/> in the current world's table, in deciseconds; null without a table or an entry.</summary>
    public static float? Length(string path)
    {
        World world = World.CurrentOrNull;
        if (world == null || path == null)
            return null;
        return Holder.Of(world).Table?.Length(path);
    }

    /// <summary>
    /// True the first time the current world asks about <paramref name="path"/> as a file its table lacks, false afterwards:
    /// <c>SSsounds.get_sound_length</c> reports a missing file once (tg reports its CRASH on every call).
    /// </summary>
    internal static bool FirstMiss(string path)
    {
        World world = World.CurrentOrNull;
        if (world == null)
            return true;
        return Holder.Of(world).Missed.Add(path ?? "");
    }

    private sealed class DictionaryTable : ISoundLengths
    {
        private readonly IReadOnlyDictionary<string, float> _lengths;

        internal DictionaryTable(IReadOnlyDictionary<string, float> lengths) => _lengths = lengths;

        internal IReadOnlyDictionary<string, float> Lengths => _lengths;

        public float? Length(string path) => _lengths.TryGetValue(path, out float length) ? length : null;
    }

    // The per-world table (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal ISoundLengths Table;

        internal readonly HashSet<string> Missed = new HashSet<string>(StringComparer.Ordinal);

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
