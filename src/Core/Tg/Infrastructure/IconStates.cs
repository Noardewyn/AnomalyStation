using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the committed icon state table (Generated/IconStates.bin, made by tools/Anomaly.Tools.Icons --states): the state
// names of every .dmi under tg's icons/, which tg's icon_exists reads from rust-g's icon metadata (icons.dm:1214). The server reads no
// .dmi at run time (D104); as the body-part masks (D179), the table is generated from tg's icons and read once (an immutable,
// process-wide cache, object-model.md section 10).

/// <summary>The icon state table: whether a tg .dmi has a state.</summary>
public static class IconStates
{
    private static readonly Lazy<IReadOnlyDictionary<string, HashSet<string>>> s_table = new(Load);

    /// <summary>Whether the icon file <paramref name="path"/> (<c>icons/mob/silicon/ai.dmi</c>) has the state <paramref name="state"/>; false for a file the table does not hold.</summary>
    public static bool Has(string path, string state)
        => path != null && state != null && s_table.Value.TryGetValue(path, out HashSet<string> states) && states.Contains(state);

    /// <summary>Whether the table holds the icon file <paramref name="path"/>.</summary>
    public static bool Knows(string path) => path != null && s_table.Value.ContainsKey(path);

    /// <summary>The states of the icon file <paramref name="path"/>, or null for a file the table does not hold.</summary>
    public static IReadOnlyCollection<string> States(string path) => path != null && s_table.Value.TryGetValue(path, out HashSet<string> states) ? states : null;

    private static IReadOnlyDictionary<string, HashSet<string>> Load()
    {
        var table = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        using Stream stream = typeof(IconStates).Assembly.GetManifestResourceStream("Anomaly.Core.Tg.IconStates.bin")
            ?? throw new InvalidOperationException("the icon state table (Generated/IconStates.bin) is not embedded");
        using var deflate = new DeflateStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(deflate, Encoding.UTF8);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            string[] parts = line.Split('\t');
            if (parts.Length == 0 || parts[0].Length == 0)
                continue;
            table[parts[0]] = new HashSet<string>(parts.Skip(1), StringComparer.Ordinal);
        }
        return table;
    }
}
