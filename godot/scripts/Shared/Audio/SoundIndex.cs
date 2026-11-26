using System.IO;
using System.Text.Json;

namespace Anomaly.Client.Shared.Audio;

// M4b audio slice 3. Engine-free (compiled into tests/Anomaly.Server.Tests too).

/// <summary>One file of the sound tool's index (tools/Anomaly.Tools.Sounds, <c>sound/index.json</c>).</summary>
public readonly record struct SoundIndexEntry(string Path, string File, int Rate, int Channels, float Ds, string Format, long Bytes, bool NonCommercial);

/// <summary>
/// The sound tool's index: tg's resource path (the key the server sends, <c>sound/machines/airlock/airlock.ogg</c>) to where the
/// copied file is under the asset root (<c>file</c>: <c>sound/...</c>, or <c>sound-nc/...</c> for the non-commercial ones, D35), its
/// sample rate, length in deciseconds and format (<c>ogg</c>, or <c>wav</c> for a file named .ogg that is not Ogg Vorbis).
/// </summary>
public sealed class SoundIndex
{
    private readonly Dictionary<string, SoundIndexEntry> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    /// <summary>Every file of the index (the start's preload loads them all).</summary>
    public IEnumerable<SoundIndexEntry> Entries => _entries.Values;

    public bool TryGet(string path, out SoundIndexEntry entry) => _entries.TryGetValue(path ?? string.Empty, out entry);

    /// <summary>The mixer's facts of a file, null when the index does not know it.</summary>
    public SoundFileFacts? Facts(string path) =>
        TryGet(path, out SoundIndexEntry e) ? new SoundFileFacts(e.Rate, Math.Max(0f, e.Ds / 10f)) : null;

    /// <summary>Reads the index's JSON; entries without a path are skipped. Throws <see cref="JsonException"/> or <see cref="FormatException"/> for a file that is not an index.</summary>
    public static SoundIndex Parse(string json)
    {
        var index = new SoundIndex();
        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array)
            throw new FormatException("no \"files\" list");
        foreach (JsonElement f in files.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object) continue;
            string path = Str(f, "path");
            if (string.IsNullOrEmpty(path)) continue;
            index._entries[path] = new SoundIndexEntry(
                path,
                Str(f, "file") ?? path,
                (int)Num(f, "rate"),
                (int)Num(f, "channels"),
                (float)Num(f, "ds"),
                Str(f, "format") ?? "ogg",
                (long)Num(f, "bytes"),
                f.TryGetProperty("nc", out JsonElement nc) && nc.ValueKind == JsonValueKind.True);
        }
        return index;
    }

    /// <summary>Reads the index file; null with the problem when it is missing or unreadable.</summary>
    public static SoundIndex Load(string file, out string problem)
    {
        problem = null;
        try
        {
            if (!File.Exists(file))
            {
                problem = "there is no " + file;
                return null;
            }
            return Parse(File.ReadAllText(file));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            problem = file + ": " + e.Message;
            return null;
        }
    }

    private static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double Num(JsonElement o, string name) =>
        o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double d) ? d : 0;
}

/// <summary>A least-recently-used cache with a byte budget (the loaded sounds, about 64 MB). Engine-free.</summary>
public sealed class SoundCache<T> where T : class
{
    private readonly Dictionary<string, LinkedListNode<(string Key, T Value, long Bytes)>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, T Value, long Bytes)> _order = new();

    public SoundCache(long budgetBytes) => BudgetBytes = budgetBytes;

    public long BudgetBytes { get; }
    public long Bytes { get; private set; }
    public int Count => _map.Count;
    public long Evicted { get; private set; }

    public bool TryGet(string key, out T value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>Adds (or replaces) an entry as the newest, then drops the oldest ones while over the budget (never the new one).</summary>
    public void Add(string key, T value, long bytes)
    {
        if (_map.TryGetValue(key, out var old))
        {
            _order.Remove(old);
            Bytes -= old.Value.Bytes;
        }
        var node = _order.AddFirst((key, value, Math.Max(0, bytes)));
        _map[key] = node;
        Bytes += node.Value.Bytes;
        while (Bytes > BudgetBytes && _order.Last != node)
        {
            var last = _order.Last;
            _order.RemoveLast();
            _map.Remove(last.Value.Key);
            Bytes -= last.Value.Bytes;
            Evicted++;
        }
    }
}
