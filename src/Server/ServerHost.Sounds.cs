using System.IO;
using System.Text.Json;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server;

// Version 13 (M4b audio slice 2, docs/ai/specs/protocol.md section 22): after the tick's Deltas, joins and Airflow, each joined
// session gets the sounds tg sent its mob or client in the tick (SoundFeed.Build) on the State channel, so they follow the
// MapGrid of a session that joined this tick; a session that is not joined at the send loses them (a join gets nothing old).
// Outside the world, after the step. And the sound lengths: tg reads them through rust-g, the port from the sound tool's index.
public sealed partial class ServerHost
{
    private static readonly object SoundIndexLock = new();
    private static readonly Dictionary<string, (IReadOnlyDictionary<string, float> Lengths, string Error)> SoundIndexCache = new(StringComparer.Ordinal);
    private bool _soundErrorsReported;

    private void SendSounds()
    {
        if (_sounds == null) return;
        foreach (Session s in _sessions)
        {
            if (s.State != SessionState.Joined)
            {
                s.Sounds.Discard();
                continue;
            }
            List<Sounds> messages = _sounds.Build(s.Sounds, out int dropped);
            if (dropped > 0) Metrics.AddSoundsDropped(dropped);
            foreach (Sounds message in messages)
                Send(s, Channel.State, Codec.Encode(message));
        }
        if (_sounds.Errors > 0 && !_soundErrorsReported)
        {
            _soundErrorsReported = true;
            _err.WriteLine("warning: the sound feed failed on a sound (the world went on): " + _sounds.FirstError);
        }
    }

    // The clients of the sessions, for a sound tg sends to the world.
    private IEnumerable<Anomaly.Core.Tg.client> SessionClients()
    {
        foreach (Session s in _sessions)
        {
            if (s.Client != null) yield return s.Client;
        }
    }

    /// <summary>Version 13: the plays the sound budget dropped over the run (the quietest first; never a stop, an update or a mute).</summary>
    public long SoundsDropped => _sounds?.Dropped ?? 0;

    // The lengths of the sound tool's index (ServerConfig.SoundIndex), read once per file and process; a missing or unreadable
    // index gives no table (every length 0, as tg's get_sound_length for an unknown file) and one line on the output per host
    // (not the error stream: the index is generated, tools/Anomaly.Tools.Sounds, and a checkout without it still runs).
    private IReadOnlyDictionary<string, float> SoundIndex()
    {
        string path = ResolveSoundIndex(_config.SoundIndex);
        (IReadOnlyDictionary<string, float> Lengths, string Error) entry;
        lock (SoundIndexLock)
        {
            if (!SoundIndexCache.TryGetValue(path, out entry))
            {
                string error = null;
                IReadOnlyDictionary<string, float> lengths = File.Exists(path) ? ReadSoundIndex(path, out error) : null;
                if (lengths == null) error ??= "it does not exist";
                entry = (lengths ?? new Dictionary<string, float>(StringComparer.Ordinal), error);
                SoundIndexCache[path] = entry;
            }
        }
        if (entry.Error != null && !_warnedNoSoundIndex)
        {
            _warnedNoSoundIndex = true;
            _out.WriteLine($"sounds: the index {path} cannot be read ({entry.Error}; tools/Anomaly.Tools.Sounds writes it): every sound length is 0");
        }
        return entry.Lengths;
    }

    private bool _warnedNoSoundIndex;

    /// <summary>
    /// Reads the sound tool's <c>sound/index.json</c> (tools/Anomaly.Tools.Sounds, <c>SoundCopier.WriteIndex</c>): each file's tg path
    /// and its length in deciseconds (<c>ds</c>). Null and the reason for a file that is no index.
    /// </summary>
    public static IReadOnlyDictionary<string, float> ReadSoundIndex(string path, out string error)
    {
        error = null;
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array)
            {
                error = "no \"files\" list";
                return null;
            }
            var lengths = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (JsonElement entry in files.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("path", out JsonElement p) || p.ValueKind != JsonValueKind.String
                    || !entry.TryGetProperty("ds", out JsonElement ds) || ds.ValueKind != JsonValueKind.Number)
                    continue;
                string key = p.GetString();
                float length = (float)ds.GetDouble();
                if (!string.IsNullOrEmpty(key) && float.IsFinite(length) && length >= 0)
                    lengths[key] = length;
            }
            return lengths;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            error = e.Message;
            return null;
        }
    }

    /// <summary>
    /// The sound index file of <paramref name="file"/>: as given when it is absolute or exists; a relative one (the default
    /// <c>godot/assets/tg/sound/index.json</c>) is also looked for from the working folder and the server's own folder upward,
    /// so the repository's is found from anywhere inside it.
    /// </summary>
    public static string ResolveSoundIndex(string file)
    {
        file ??= ServerConfig.DefaultSoundIndex;
        if (Path.IsPathRooted(file) || File.Exists(file)) return Path.GetFullPath(file);
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, file);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return Path.GetFullPath(file);
    }
}
