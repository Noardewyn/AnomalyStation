using System.IO;
using System.Runtime;
using System.Threading;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server;
using Anomaly.Server.Replication;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The round the map editor's Play runs (docs/ai/specs/map-editor.md 5.7): the game's own server inside the program, on a thread of its own at the game's
/// pace, listening on this machine alone. It loads the document as it is when Play is pressed, the changes that are not saved included, and the map's file
/// is not written. It is a round without the lobby: it starts at once, and a join is an assistant at the given tile, or a new arrival on the map's
/// arrivals shuttle. Its one player is the mapper, who
/// holds tg's admin rank and may type the dev commands and the server console's.
/// <para>
/// It is another world than the one that shows the map (<see cref="EditorFeed"/>), which stands and is never played: what happens in the round changes
/// nothing of the map.
/// </para>
/// </summary>
public sealed class PlayWorld : IDisposable
{
    private const int Stack = 16 * 1024 * 1024; // the server's own thread is the program's first, with its large stack: a round's deep calls get as much here

    private readonly CancellationTokenSource _stop = new();
    private readonly EditorFeed.EditorLog _log = new();
    private readonly ServerConfig _config;
    private readonly string _scratch;
    private volatile ServerHost _host;
    private volatile string _failure;
    private volatile int _port;
    private volatile bool _over;

    /// <param name="document">The map; its text is taken now.</param>
    /// <param name="mapFile">The map's file when the map was opened from it or saved to it, else null (a new map).</param>
    /// <param name="name">The map's name (the file's).</param>
    /// <param name="at">The tile the player starts on; null: the player is a new arrival, on the arrivals shuttle or at a late-join landmark.</param>
    /// <param name="jsonText">The map json's text with the map's settings as they are now (docs/ai/specs/map-editor.md 5.10); null: the file's, or none.</param>
    /// <param name="skyImage">The file of the sky's picture; null: none.</param>
    public PlayWorld(MapDocument document, string mapFile, string name, (int X, int Y, int Z)? at, string jsonText = null, string skyImage = null)
    {
        (_config, _scratch) = ConfigOf(document, mapFile, name, at, jsonText, skyImage);
        new Thread(Run, Stack) { IsBackground = true, Name = "map editor: play" }.Start();
    }

    // The round's server: what it loads and how it runs, and the scratch folder to remove after it (null: none).
    internal static (ServerConfig Config, string Scratch) ConfigOf(MapDocument document, string mapFile, string name, (int X, int Y, int Z)? at, string jsonText = null, string skyImage = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        mapFile = mapFile == null ? null : Path.GetFullPath(mapFile);
        (string folder, string map, string scratch) = EditorFeed.MapsOf(document, mapFile, name ?? "map", jsonText, skyImage);
        // a map of a maps folder is loaded from that folder, for its shuttles and its other levels, with the document's text in the file's place, and the
        // json's text with the settings that are not saved in its json's
        Dictionary<string, string> texts = scratch == null ? new Dictionary<string, string> { [mapFile] = document.ToText() } : null;
        if (texts != null && jsonText != null)
            texts[Path.GetFullPath(Path.Combine(folder, "map_jsons", map + ".json"))] = jsonText;
        var config = new ServerConfig
        {
            Seed = (ulong)Random.Shared.NextInt64(), // a round of its own each time, as a server's
            Port = 0, // any free one
            Console = false,
            StatusEveryTicks = int.MaxValue,
            Realtime = true,
            Map = map,
            MapsFolder = folder,
            MapTexts = texts,
            JoinAt = at,
            JoinAsArrival = at == null,
            LocalAdmin = true,
            DevCommands = true,
            ClientConsole = true,
        };
        return (config, scratch);
    }

    /// <summary>True once the round takes joins, at <see cref="Port"/> of this machine.</summary>
    public bool Ready => _port != 0 && _host is { JoinsOpen: true } && !_over;

    /// <summary>The port the round listens on; 0 until it does.</summary>
    public int Port => _port;

    /// <summary>True when the round is over: it was stopped, or it failed (<see cref="Failure"/>).</summary>
    public bool Over => _over;

    /// <summary>Why the round did not start or ended by itself; null when it did not.</summary>
    public string Failure => _failure;

    /// <summary>What the round's world wrote: its warnings and its runtime errors (the last few hundred lines).</summary>
    public IReadOnlyCollection<string> Log => _log.Lines;

    /// <summary>Stops the round. It does not wait: the round's thread ends its tick, closes its connections and removes its scratch files.</summary>
    public void Dispose() => _stop.Cancel();

    private void Run()
    {
        GCLatencyMode latency = GCSettings.LatencyMode;
        ServerHost host = null;
        try
        {
            // as the server does for a paced run: fewer collections that stop every thread, the one that draws included
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            var transport = new LiteNetServerTransport();
            host = new ServerHost(_config, transport, new Replicator(new ReplicatorOptions { Interest = new InterestOptions() }), output: _log, errors: _log);
            host.Start();
            _port = transport.Port;
            _host = host;
            host.Run(new RealtimePacer(), _stop.Token);
            if (!_stop.IsCancellationRequested)
                _failure = "the round ended";
        }
        catch (Exception e)
        {
            _failure = "the round stopped: " + e.Message;
            _log.WriteLine(e.ToString());
        }
        finally
        {
            _over = true;
            try
            {
                host?.Dispose();
                if (_scratch != null && Directory.Exists(_scratch))
                    Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _log.WriteLine("the round's files were not removed: " + e.Message);
            }
            GCSettings.LatencyMode = latency;
        }
    }
}
