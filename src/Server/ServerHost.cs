using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server;

/// <summary>The outcome of a run: where the world stopped and its state hash there.</summary>
public readonly record struct RunResult(long Tick, float Time, ulong FinalHash, bool Stopped);

/// <summary>
/// The server host (M1 scope 2a, 2b, 2e, 2h-2j; M2 scope 2b, 2d, 2k): one world, one transport, the sessions, the command
/// queue and its command phase, the locomotion seam, the replication calls, the console and dev commands, the replay log and
/// the metrics. One host thread runs everything; a transport
/// may move bytes on its own threads but raises its events only inside <see cref="IServerTransport.Poll"/>, and the
/// console's reader thread only hands over lines (D31). One tick (<see cref="RunTick"/>, scope 2e "Where the network meets
/// the tick"):
/// <list type="number">
/// <item><description>the pacer waits for the tick (<see cref="Run"/>);</description></item>
/// <item><description><c>Poll()</c>: connection requests are validated (the Hello) and answered, received messages are
/// size-capped, decoded, validated and rate-limited (MotionInput frames go into the session's jitter buffer, actions wait for
/// their frame), and joins and leaves are queued; console lines are parsed and queued; then each joined session's jitter
/// buffer gives its frame for the tick, which becomes a recorded Motion command with the actions that were waiting for
/// it;</description></item>
/// <item><description><c>World.Step()</c>, whose command phase (a world phase after the Master's drain: BYOND's step 6,
/// waiting-model.md 7.1) applies the due commands in (session ordinal, seq) order with <c>usr</c> the session's mob, and
/// whose locomotion phase (M2 P4) then steps every capsule mob;</description></item>
/// <item><description>the replicator builds the tick's Delta once, read-only and outside the world, each joined session's
/// private block, and the join messages of the sessions whose <c>client/New</c> returned;</description></item>
/// <item><description>the host sends (Deltas, Welcomes, chat), closes what must close, and flushes the
/// transport;</description></item>
/// <item><description>metrics.</description></item>
/// </list>
/// A replay (<see cref="ServerConfig.ReplayPath"/>) runs the same host without a transport, feeding the recorded commands
/// to the same command phase.
/// </summary>
public sealed partial class ServerHost : IClientHost, IDisposable
{
    private readonly ServerConfig _config;
    private readonly IServerTransport _transport;
    private IReplicator _replicator; // M10 slice H: a new round's world gets a fresh one (RestartWorld)

    /// <summary>The replicator of the current world (a round's restart replaces it).</summary>
    public IReplicator Replicator => _replicator;
    private readonly IServerCodec _codec;
    private readonly IClientEntryPoints _clients;
    private readonly ILocomotionEntryPoints _locomotion;
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly Func<SessionToken> _newToken;
    private readonly bool _replay;
    private readonly CommandLog _replayLog;
    // Gaps R6 (D332): the next world's world.realtime start (deciseconds since 2000-01-01, DV3): the wall clock at the host's start with
    // ServerConfig.RealDate (a replay's from its log), then where each round ended; 0 is the fixed 2000-01-01 clock
    private double _realtimeStart;
    private readonly bool _strictNotTranslated;
    private readonly long? _endTick;

    private readonly CommandQueue _queue = new();
    private readonly List<Session> _sessions = new();           // every session of the run, in ordinal order
    private readonly Dictionary<int, Session> _byOrdinal = new();
    private readonly Dictionary<int, Session> _byConnection = new();
    private readonly Dictionary<string, PlayerRecord> _players = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _consoleLines = new();
    private readonly List<Session> _joinedThisTick = new();
    private readonly List<(Session Session, JoinMessages Messages)> _joins = new();
    private readonly List<int> _forget = new();
    private readonly AirflowFeed _airflow; // D141: null when ServerConfig.AirflowRadius is 0, in a replay or without a replicator
    private readonly SoundFeed _sounds;    // version 13: tg's sounds for the sessions; null in a replay, without a transport or for --empty
    private byte[] _deltaBuffer = new byte[4096];

    private int _lastOrdinal;
    private uint _consoleSeq;
    private int _requestsThisPoll;
    private int _rejectLinesThisWindow;
    private long _rejectedRequests;
    private volatile bool _stopRequested;
    private bool _quitQueued;
    private bool _started;
    private long _tickBytesOut, _tickBytesIn, _totalBytesOut, _totalBytesIn;
    private int _sessionsServed;

    /// <summary>
    /// A host for <paramref name="config"/>. <paramref name="transport"/> null: no network (a replay, or a world-only test);
    /// <paramref name="replicator"/> null: none (nothing is replicated; joins get no world state); the codec and the client
    /// entry points default to the real ones (M1 P2's <c>Codec</c>, M1 P1's <c>ClientSeam</c>). <paramref name="replay"/>: the
    /// recorded commands to re-run (no transport then). <paramref name="strictNotTranslated"/>: a reached
    /// <c>Dm.NotTranslated</c> throws (tests) instead of being reported once (a running server). <paramref name="newToken"/>:
    /// the reconnect tokens (default: 16 random bytes from the OS, never from the world RNG). <paramref name="locomotion"/>:
    /// the locomotion seam as the host calls it (default: M2 P4's <c>LocomotionSeam</c>).
    /// </summary>
    public ServerHost(
        ServerConfig config,
        IServerTransport transport = null,
        IReplicator replicator = null,
        IServerCodec codec = null,
        IClientEntryPoints clients = null,
        TextWriter output = null,
        TextWriter errors = null,
        CommandLog replay = null,
        TextWriter metricsCsv = null,
        TextWriter recordTo = null,
        bool strictNotTranslated = false,
        Func<SessionToken> newToken = null,
        ILocomotionEntryPoints locomotion = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        if (config.JoinAt is { } at)
            _joinAt = new[] { new SpawnPoint(at.X, at.Y, at.Z) };
        if (replay != null && transport != null) throw new ArgumentException("a replay runs without a transport", nameof(transport));
        _transport = transport;
        _replicator = replicator ?? new NoReplicator();
        _codec = codec ?? ProtocolCodec.Instance;
        _clients = clients ?? TgClientEntryPoints.Instance;
        _locomotion = locomotion ?? TgLocomotionEntryPoints.Instance;
        _out = TextWriter.Synchronized(output ?? TextWriter.Null); // D306: the next round's world is built on a worker thread
        _err = TextWriter.Synchronized(errors ?? TextWriter.Null);
        _newToken = newToken ?? RandomToken;
        _replay = replay != null;
        _replayLog = replay;
        if (transport != null && replicator != null && !config.Empty && config.AirflowRadius > 0)
            _airflow = new AirflowFeed(config.AirflowRadius);
        if (transport != null && !config.Empty)
            _sounds = new SoundFeed(c => SessionOfClient(c)?.Sounds, SessionClients);
        _strictNotTranslated = strictNotTranslated;
        _endTick = replay != null ? replay.EndTick ?? LastTick(replay) : config.Ticks;

        Log = CommandLog.For(config);
        _realtimeStart = config.RealtimeStart != 0d ? config.RealtimeStart : (config.RealDate && !_replay ? RealDateNow() : 0d);
        Log.RealtimeStart = _realtimeStart;
        if (recordTo != null) Log.WriteTo(recordTo);

        string ticks = _endTick.HasValue ? _endTick.Value.ToString(CultureInfo.InvariantCulture) : "until-stopped";
        string world = config.Empty ? "empty" : $"tg room={config.RoomWidth}x{config.RoomHeight} layout={config.RoomLayout.ToString().ToLowerInvariant()} space={config.RoomSpace}";
        string net = transport != null ? "" : (_replay ? " replay" : " no-network");
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Anomaly.Server: seed={config.Seed} ticks={ticks} fps={World.Fps} mode={(config.Realtime ? "realtime" : "fast-forward")} world={world} smoke={config.SmokeActors} speed-scale={config.MoveSpeedScale:0.###} dev-commands={(config.DevCommandsEnabled ? "on" : "off")}{(config.ClientConsole ? " client-console=on" : "")}{net}"));
        OpenAdmin(); // M10 slice I: the database and tg's admin files, before the boot reads them
        World = CreateWorld();
        Metrics = new ServerMetrics(metricsCsv); // after the boot, so that the first tick's counters are the tick's own
        if (replay != null)
        {
            foreach (QueuedCommand c in replay.Commands)
                _queue.Enqueue(c);
        }
    }

    /// <summary>The world the host runs (M10 slice H: a new round's after tg's <c>world/Reboot</c>, <see cref="RestartWorld"/>).</summary>
    public World World { get; private set; }

    /// <summary>The test room (null in an empty world, and from M3 in a world that loads a map).</summary>
    public TestRoom Room { get; private set; }

    /// <summary>
    /// M3 (the contracts step's seam; M3 P2 makes it return <c>MapSeam.LatejoinSpawns</c> when <see cref="ServerConfig.Map"/>
    /// is set): where players join and where the dev and console spawns put a human, in order; empty in an empty world.
    /// With <see cref="ServerConfig.JoinAt"/> it is that one tile.
    /// </summary>
    public IReadOnlyList<SpawnPoint> SpawnPoints => _joinAt ?? (MapLoaded ? MapSeam.LatejoinSpawns(World) : Room?.SpawnPoints ?? (IReadOnlyList<SpawnPoint>)Array.Empty<SpawnPoint>());

    private SpawnPoint[] _joinAt; // ServerConfig.JoinAt

    /// <summary>M3 P2: whether the world loads a converted map (<see cref="ServerConfig.Map"/>) instead of the test room.</summary>
    public bool MapLoaded { get; private set; }

    /// <summary>The config.</summary>
    public ServerConfig Config => _config;

    /// <summary>The last stepped tick.</summary>
    public long Tick => World.Tick;

    /// <summary>True once the Master finished initializing and the host set <c>RUNLEVEL_GAME</c>: joins are accepted from then on.</summary>
    public bool JoinsOpen
    {
        get => _joinsOpen;
        private set => _joinsOpen = value;
    }

    private volatile bool _joinsOpen; // read by the thread that waits for the world to start (the map editor's Play)

    /// <summary>The host's timing of the tick's pieces and of the boot's inits (M3 I1b: the stalls).</summary>
    public StepProfiler Profiler { get; } = new();

    /// <summary>A tick whose world step takes longer is reported with its pieces and the GC's work (<see cref="StepProfiler.SlowTick"/>).</summary>
    public const double SlowStepMs = 250;

    /// <summary>The tick at which joins opened (0 before).</summary>
    public long JoinsOpenedAt { get; private set; }

    /// <summary>Every session of the run, in ordinal order (closed ones included).</summary>
    public IReadOnlyList<Session> Sessions => _sessions;

    /// <summary>The commands applied so far (the replay log).</summary>
    public CommandLog Log { get; }

    /// <summary>The metrics.</summary>
    public ServerMetrics Metrics { get; }

    /// <summary>Commands waiting for their tick.</summary>
    public CommandQueue Queue => _queue;

    /// <summary>True once the shutdown ran (the run is over).</summary>
    public bool Finished { get; private set; }

    /// <summary>True when the run ended on a stop request (Ctrl+C, SIGTERM, the console's <c>quit</c>).</summary>
    public bool Stopped { get; private set; }

    /// <summary>The state hash at the end of the run (after <see cref="Finished"/>).</summary>
    public ulong FinalHash { get; private set; }

    /// <summary>Connection requests refused so far.</summary>
    public long RejectedRequests => _rejectedRequests;

    /// <summary>The session of an ordinal, or null.</summary>
    public Session SessionOf(int ordinal) => _byOrdinal.TryGetValue(ordinal, out Session s) ? s : null;

    /// <summary>The live (not closed) session of a key (compared as ckeys), or null.</summary>
    public Session LiveSession(string key)
    {
        string ckey = Dm.ckey(key);
        for (int i = _sessions.Count - 1; i >= 0; i--)
        {
            Session s = _sessions[i];
            if (s.Ckey == ckey && s.State != SessionState.Closed) return s;
        }
        return null;
    }

    /// <summary>Asks for a clean shutdown (thread-safe): the next tick applies the shutdown, then the run ends.</summary>
    public void RequestStop() => _stopRequested = true;

    /// <summary>Hands over a console line (thread-safe; the console's reader thread calls it). The host parses it between ticks.</summary>
    public void EnqueueConsoleLine(string line)
    {
        if (line != null) _consoleLines.Enqueue(line);
    }

    // ---- the world ----

    // The runtimes seen so far, by their site (the exception type and its top stack frame), with how often each happened.
    private readonly Dictionary<string, int> _runtimeSites = new();

    private const int MaxRuntimeFrames = 14;

    // A runtime of the world on the error output (the owner's play test, 2026-10-10): the first time a site fails, the line
    // and its stack (the frames of our code, paths from the repository root); after that the same site prints its line only
    // at its 2nd, 4th, 8th, ... time, so a runtime that repeats every tick does not flood the console.
    private void ReportRuntime(World world, Exception e)
    {
        string head = string.Create(CultureInfo.InvariantCulture, $"runtime at tick {world.Tick}: {e.GetType().Name}: {e.Message}");
        List<string> frames = RuntimeFrames(e);
        string site = e.GetType().Name + "|" + (frames.Count > 0 ? frames[0] : e.Message);
        int n = _runtimeSites.TryGetValue(site, out int seen) ? seen + 1 : 1;
        _runtimeSites[site] = n;
        if (n == 1)
        {
            _err.WriteLine(head);
            foreach (string frame in frames)
                _err.WriteLine("    " + frame);
            return;
        }
        if ((n & (n - 1)) == 0)
            _err.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{head} (the same runtime for the {n}th time; its stack is above)"));
    }

    private static List<string> RuntimeFrames(Exception e)
    {
        var frames = new List<string>();
        foreach (string raw in (e.StackTrace ?? "").Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("at ", StringComparison.Ordinal) || line.Contains("DmTaskAwaiter", StringComparison.Ordinal)
                || line.Contains(" System.", StringComparison.Ordinal))
                continue;
            int src = line.IndexOf(" in ", StringComparison.Ordinal);
            if (src > 0)
            {
                string path = line[(src + 4)..];
                int repo = path.IndexOf("/src/", StringComparison.Ordinal);
                if (repo < 0) repo = path.IndexOf("/tests/", StringComparison.Ordinal);
                if (repo >= 0) path = path[(repo + 1)..];
                line = line[..src] + " in " + path.Replace(":line ", ":", StringComparison.Ordinal);
            }
            frames.Add(line);
            if (frames.Count == MaxRuntimeFrames) break;
        }
        return frames;
    }

    // Gaps R2: the old world's tg FUTURE_STATION_TRAITS_FILE between a reboot and the next world (BYOND's data folder outlives a reboot).
    private string _futureStationTraits;

    private World CreateWorld()
    {
        var world = new World(_config.Seed);
        world.Profiler = Profiler; // M3 I1b: the host times the tick's pieces (StepProfiler); the world never reads it
        world.RuntimeErrorSink = e => ReportRuntime(world, e);
        if (_config.SmokeActors > 0)
            SmokeLoad.Install(world, _config.SmokeActors);
        if (!_config.Empty)
        {
            DmWorldState state = DmWorldState.Of(world);
            state.NotTranslatedThrows = _strictNotTranslated;
            state.RealtimeStart = _realtimeStart; // gaps R6 (D332): the round's date
            if (_config.PlayerSavesFolder != null)
                state.Files = new DmMountedFiles(state.Files, "data/player_saves/", new DmDirectoryFiles(_config.PlayerSavesFolder));
            if (_config.NpcSavesFolder != null)
                state.Files = new DmMountedFiles(state.Files, "data/npc_saves/", new DmDirectoryFiles(_config.NpcSavesFolder));
            if (_config.PersistenceSavesFolder != null)
                state.Files = new DmMountedFiles(state.Files, "data/persistence/", new DmDirectoryFiles(_config.PersistenceSavesFolder));
            // tg's configuration: world/New loads config/config.txt from the world's file seam (configuration.dm:88-100); the
            // host writes the game options the port reads into it (tg's game_options.txt keys), so tg's walk and run
            // movespeed modifiers read RUN_DELAY and WALK_DELAY through tg's own loader.
            state.Files.WriteText("config/config.txt", TgConfigText(_config));
            if (_config.Lobby)
                state.Files.WriteText("config/jobs.txt", TgJobsText(_config.OpenJobs));
            // gaps R2: the station traits an admin planned in the last round (RestartWorld carries the file over the reboot)
            if (_futureStationTraits != null)
                state.Files.WriteText(Defines.FUTURE_STATION_TRAITS_FILE, _futureStationTraits);
            _futureStationTraits = null;
            // tg's strings (M2 F1): the name and word lists that world start reads with world.file2list (names.dm,
            // languages.dm), from the tgstation clone (TgStrings.Locate, as the icons tool finds it). Without them a crew
            // member keeps tg's "Unknown" (CrewStandIn.Name).
            MountTgStrings(state);
            // tg's config files a host edits, from the config folder, over the clone's: Dynamic's (syndicate A3, D316: dynamic.toml, read
            // by SSdynamic's load_config) and the custom AI law board's laws (silicon_laws.txt, /datum/ai_laws/custom/New; tg's own by default)
            _tgConfigFiles ??= TgConfigFiles.Select(name => (name, ReadConfigText(name))).Where(f => f.Item2 != null).ToArray();
            foreach ((string name, string text) in _tgConfigFiles)
                state.Files.WriteText("config/" + name, text);
            MountChangelog(state);
            state.WorldLog = line => _out.WriteLine(line);
            InstallAdmin(world, state); // M10 slice I: tg's admin files and the round rows
            if (!_replay)
                HttpSeam.Install(world, new WebhookRequests(this)); // M10x slice G6: rust-g's HTTP for tg's admin help webhook
            ClientSeam.SetHost(world, this);
            LevelHeight.Of(world).Set(_config.WallHeight); // the owner (2026-11-20): the one wall height, the clients' too (MapGrid, protocol 91)
            ShotSpread.Of(world).Set(_config.SpreadScale); // DV96: the share of tg's spread aimed shots keep
            if (!_replay)
                PresentationSinks.Of(world).Messages = new ChatRouter(this);
            _sounds?.Attach(world); // version 13: tg's SEND_SOUND for the sessions (a replay has no transport, so no feed)
            // tg's sound lengths (rust-g's sound_len, SSsounds.get_sound_length) from the sound tool's index, in every mode: they
            // are world input (a loop's timing reads them), so a replay loads the same table.
            SoundLengths.Install(world, SoundIndex());
            string map = MapName; // the config's, or an admin's Change Map
            if (map != null)
            {
                // M3 P2 (D108, D109): the converted map's station level, which SSmapping loads in its Initialize; the room
                // options are not used.
                if (_config.MapEditor)
                    MapEditSeam.Select(world); // DV504: the spawners and the landmarks of the map stay, for the editor to show; the map is loaded alone
                MapSeam.Select(world, new MapSelection(ResolveMapsFolder(_config.MapsFolder, map), map, _config.MapTexts));
                MapLoaded = true;
                TgWorld.Boot(world, TgWorld.CompiledMap);
            }
            else
            {
                int width = _config.RoomWidth, height = _config.RoomHeight;
                TestRoomLayout layout = _config.RoomLayout;
                int space = _config.RoomSpace;
                TgWorld.Boot(world, _ => Room = TestRoom.Build(width, height, layout, space: space));
            }
            // The 3D model catalog's rules of the way modelled things face (DV56), on every atom tg initializes (the map's ones initialize with the
            // Master, in the first ticks, so the listener is in place for them).
            string catalog = ModelDirRules.Locate(_config.ModelsCatalog);
            _modelDirRules = catalog == null ? ModelDirRules.None : ModelDirRules.Parse(File.ReadAllText(catalog));
            _out.WriteLine($"model rules: {_modelDirRules.Rules.Count} from {catalog ?? "(no catalog)"}");
            using (world.Enter())
            {
                _modelDirRules.Listen();
                if (_config.MapEditor && map != null)
                    MapEditSeam.Listen(); // DV504: the landmarks are shown
            }
            _replicator.Attach(world);
            if (_airflow != null) _airflow.Attach(world); // D141: the air flow of each SSair cycle, for the clients' streams
        }
        world.AddPhase("player commands", CommandPhase);
        if (!_config.Empty)
        {
            // M2 (D105): the locomotion phase runs right after the command phase; the speed scale is D106's D74 row.
            _locomotion.Install(world);
            _locomotion.SetSpeedScale(world, _config.MoveSpeedScale);
            _locomotion.SetFeel(world, _config.MoveFeel);
        }
        return world;
    }

    /// <summary>
    /// M3 P2: the maps folder that holds <paramref name="map"/> (<c>map_jsons/&lt;map&gt;.json</c>): <paramref name="folder"/>
    /// as given when it is absolute or holds the map; a relative folder (the default <c>maps</c>) is also looked for from the
    /// working folder and the server's own folder upward, so the repository's <c>maps/</c> is found from anywhere inside it.
    /// </summary>
    public static string ResolveMapsFolder(string folder, string map)
    {
        folder ??= ServerConfig.DefaultMapsFolder;
        bool Holds(string f) => File.Exists(Path.Combine(f, "map_jsons", map + ".json"));
        if (Path.IsPathRooted(folder) || Holds(folder)) return Path.GetFullPath(folder);
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, folder);
                if (Holds(candidate)) return candidate;
            }
        }
        return Path.GetFullPath(folder);
    }

    // The tgstation clone's strings/ files into the world's file seam, once per world; the clone is looked up once per host.
    private void MountTgStrings(DmWorldState state)
    {
        _tgRoot ??= TgStrings.Locate(AppContext.BaseDirectory, Directory.GetCurrentDirectory()) ?? "";
        int mounted = TgStrings.Mount(state.Files, _tgRoot);
        if (mounted == 0 && !_warnedNoTgStrings)
        {
            _warnedNoTgStrings = true;
            _err.WriteLine($"warning: tg's strings/ files were not found (set {TgStrings.EnvironmentVariable} or put the tgstation clone next to the repository): players keep the name \"Unknown\"");
        }
    }

    private string _changelogText;
    private (string Name, string Text)[] _tgConfigFiles;
    private static readonly string[] TgConfigFiles = { "dynamic.toml", "silicon_laws.txt" };

    // M10x slice H (DV200): the port's changelog, CHANGELOG.md (found as the model catalog is), for tg's changelog window and its
    // unread notice (world/SetupLogs' changelog_hash)
    private void MountChangelog(DmWorldState state)
    {
        _changelogText ??= ModelDirRules.FindUp(datum_changelog.ChangelogFile) is string path ? File.ReadAllText(path) : "";
        if (_changelogText.Length > 0)
            state.Files.WriteText(datum_changelog.ChangelogFile, _changelogText);
    }

    private ModelDirRules _modelDirRules = ModelDirRules.None;

    private string _tgRoot;
    private bool _warnedNoTgStrings;

    /// <summary>
    /// The text of tg's <c>config/config.txt</c> the world loads: tg's <c>KEY value</c> lines of the game options the host sets
    /// (<c>RUN_DELAY</c>, <c>WALK_DELAY</c>; tgstation ships them in <c>config/game_options.txt</c>), and, as tgstation ships
    /// them there (M5x slice 1), <c>REACTIONARY_EXPLOSIONS</c> (explosions that react to walls and doors, game_options.txt:436)
    /// and <c>BOMBCAP 20</c> (the (5, 10, 20) cap, :447); M10 slice F: <c>OOC_DURING_ROUND</c> (:16), so the round start leaves OOC on; with the lobby also <c>ENABLE_LOCALHOST_RANK</c> (config.txt:55), the host client's !localhost! rank, and without <see cref="ServerConfig.OpenJobs"/> the station engineer as the overflow role.
    /// Syndicate A3 (D316): Dynamic's flags as tgstation ships them, <c>PROTECT_ROLES_FROM_ANTAGONIST</c> (:128),
    /// <c>ALLOW_LATEJOIN_ANTAGONISTS</c> (:142) and <c>DYNAMIC_CONFIG_ENABLED</c> (:148, its config/dynamic.toml from the config folder).
    /// The owner (2026-11-08): <c>EMERGENCY_SHUTTLE_AUTOCALL_THRESHOLD 0.2</c> (game_options.txt:480, commented out in tg's shipped
    /// file) and <c>ALLOW_VOTE_RESTART</c> (config.txt:117, likewise). Silicons SI4 (D326): <c>ALLOW_AI</c> (:171, the AI job) and
    /// <c>ALLOW_AI_MULTICAM</c> (:174, the AI's multicamera mode), as tgstation ships them. Gaps R5: <c>COMMENDATION_PERCENT_POLL 0.05</c>
    /// (game_options.txt:24, the end-of-round commendations), as tgstation ships it. Gaps SP1 (D338): <c>ROUNDSTART_RACES</c> (:365-377) of the
    /// roundstart species that are ported, the human, the felinid and the lizard.
    /// </summary>
    /// <summary>Gaps R6 (D332): the wall clock as <c>world.realtime</c> counts it, whole deciseconds since 2000-01-01 00:00 UTC (DV3).</summary>
    public static double RealDateNow()
        => Math.Floor((DateTime.UtcNow - new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds * 10d);

    public static string TgConfigText(ServerConfig config)
        => string.Create(CultureInfo.InvariantCulture, $"RUN_DELAY {config.RunDelay:R}\nWALK_DELAY {config.WalkDelay:R}\nREACTIONARY_EXPLOSIONS\nBOMBCAP {TgBombCap}\nOOC_DURING_ROUND\n")
            + "PROTECT_ROLES_FROM_ANTAGONIST\nALLOW_LATEJOIN_ANTAGONISTS\nDYNAMIC_CONFIG_ENABLED\n"
            + "ALLOW_AI\nALLOW_AI_MULTICAM\n" // silicons SI4: the AI job and its multicamera mode (game_options.txt:171, :174)
            + "ALLOW_RANDOM_EVENTS\n" // the owner (2026-11-14): tg's random events, on as tgstation ships them (game_options.txt:155)
            + "EMERGENCY_SHUTTLE_AUTOCALL_THRESHOLD 0.2\nALLOW_VOTE_RESTART\n" // the owner (2026-11-08): tg's crew-death shuttle call at its suggested 20% (game_options.txt:480) and players' restart votes (config.txt:117)
            + "COMMENDATION_PERCENT_POLL 0.05\n" // gaps R5: the end-of-round commendations' poll, as tgstation ships it (game_options.txt:24)
            + "ROUNDSTART_RACES human\nROUNDSTART_RACES felinid\nROUNDSTART_RACES lizard\n" // gaps SP1 (D338): the roundstart species tgstation ships that are ported (game_options.txt:365-377; the moth, plasmaman, ethereal and cerulean with gaps SP2)
            + (config.StationTraits ? "" : "FORBID_STATION_TRAITS\n") // gaps R2: tg's station traits only when the config turns them on (game_options.txt:158)
            + (config.RealDate || config.RealtimeStart != 0d ? "ALLOW_HOLIDAYS\n" : "") // gaps R6 (D332): tg's holidays with the real date (config.txt:260)
            + (config.MapEditor ? "MICE_ROUNDSTART 0\n" : "") // a map editor's world shows the map: no mice of tg's on its bare cables (game_options.txt, minor_mapping.dm)
            + (config.Lobby && !config.OpenJobs ? $"OVERFLOW_JOB {LobbyJob}\n" : "") // D278: open jobs keep tg's overflow role, the assistant
            + (config.Lobby ? "LOAD_JOBS_FROM_TXT\n" : "")
            + (config.Lobby || config.LocalAdmin ? "ENABLE_LOCALHOST_RANK\n" : "") // the host client holds tg's !localhost! rank (config.txt:55): the lobby's Start Now needs R_SERVER; the map editor's Play (LocalAdmin)
            + (config.AdminhelpWebhook != null ? $"REGULAR_ADMINHELP_WEBHOOK_URL {config.AdminhelpWebhook}\n" : ""); // M10x slice G6: tg's admin help relay

    /// <summary>M10 slice F (D270): the one joinable job of the lobby's rounds, tg's overflow role then (<c>OVERFLOW_JOB</c>).</summary>
    public const string LobbyJob = Defines.JOB_STATION_ENGINEER;

    /// <summary>
    /// M10 slice F (D270): tg's legacy <c>config/jobs.txt</c> (<c>Title=total,spawn</c>, read with <c>LOAD_JOBS_FROM_TXT</c>,
    /// jobs.dm <c>legacy_load</c>) that closes every job of tg's job titles (the <c>JOB_*</c> defines) but <see cref="LobbyJob"/>,
    /// as a server operator closes jobs; the overflow role's own positions come from <c>set_overflow_role</c> (unlimited). With
    /// <paramref name="openJobs"/> (<see cref="ServerConfig.OpenJobs"/>) it closes only <see cref="ClosedOpenJobs"/>: a job the file
    /// does not list keeps tg's own slots (DV181).
    /// </summary>
    public static string TgJobsText(bool openJobs = false)
    {
        var sb = new System.Text.StringBuilder();
        foreach (System.Reflection.FieldInfo f in typeof(Defines).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (!f.IsLiteral || f.FieldType != typeof(string) || !f.Name.StartsWith("JOB_", StringComparison.Ordinal)
                || f.Name.StartsWith("JOB_CONFIG_", StringComparison.Ordinal) || f.Name.StartsWith("JOB_AUTHORITY_", StringComparison.Ordinal))
                continue;
            string title = (string)f.GetRawConstantValue();
            if (openJobs ? ClosedOpenJobs.Contains(title) : title != LobbyJob)
                sb.Append(title).Append("=0,0\n");
        }
        if (openJobs) // DV426: tg's cyborg has no late-join slot (total 0); the owner opens its three round-start slots to late joins
            sb.Append(Defines.JOB_CYBORG).Append("=3,3\n");
        return sb.ToString();
    }

    /// <summary>
    /// The jobs <see cref="ServerConfig.OpenJobs"/> keeps closed: none now. The heads of staff open with tg's captaincy (M10 slice G2,
    /// D274); the AI and the cyborg with the silicons (silicons SI2 and SI4, D326). The personal AI is no job datum (only a ban key), and
    /// its "Personal AI=0,0" line closed the AI too: tg's legacy regex "[title]=..." is unanchored, so "AI=" matched inside it.
    /// </summary>
    public static readonly IReadOnlySet<string> ClosedOpenJobs = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>tg's shipped <c>BOMBCAP</c> (tgstation <c>config/game_options.txt:447</c>; the entry's default is 14).</summary>
    public const int TgBombCap = 20;

    // ---- the loop ----

    /// <summary>Starts the transport (if any) and prints the header line. <see cref="Run"/> and <see cref="RunTick"/> call it once.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        // M4b audio: a replay runs with the recording's sound lengths or not at all (they time the loops, so another table
        // would end with another hash; CommandLog.SoundsDigest). A log without the line predates it.
        if (_replayLog != null && _replayLog.SoundsDigest != 0 && _replayLog.SoundsDigest != SoundLengths.Digest(World))
            throw new InvalidOperationException("the replay's sound lengths differ from the recording's (another sound index, "
                + "--sound-index, or a test's table): the run would end with another hash");
        _transport?.Start(_config.BindAddress, _config.Port);
        if (_transport != null)
        {
            _transport.ConnectionRequested += OnConnectionRequested;
            _transport.Connected += OnConnected;
            _transport.Received += OnReceived;
            _transport.Disconnected += OnDisconnected;
        }
        if (_transport != null)
        {
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"listening on {_config.BindAddress}:{_transport.Port} (max_players={_config.MaxPlayers})"));
            StartLauncherSide();
        }
        if (_endTick == 0)
        {
            Finish();
            WriteSummary();
        }
    }

    /// <summary>
    /// Runs ticks until the run is over (<see cref="ServerConfig.Ticks"/> reached, a replay's end, or a stop request
    /// through <paramref name="stop"/>, <see cref="RequestStop"/> or the console's <c>quit</c>, which are applied in one
    /// more tick: the shutdown tick).
    /// </summary>
    public RunResult Run(ITickPacer pacer, CancellationToken stop = default)
    {
        ArgumentNullException.ThrowIfNull(pacer);
        Start();
        long overruns = pacer.Overruns;
        while (!Finished)
        {
            if (stop.IsCancellationRequested) _stopRequested = true;
            pacer.WaitForNextTick(_stopRequested ? CancellationToken.None : stop);
            if (stop.IsCancellationRequested) _stopRequested = true;
            long now = pacer.Overruns;
            RunTick(pacer.LastLatenessMs, now != overruns);
            overruns = now;
        }
        return new RunResult(Tick, World.Time, FinalHash, Stopped);
    }

    /// <summary>One tick (steps 2 to 6 above). <paramref name="latenessMs"/> and <paramref name="overrun"/> come from the pacer, for the metrics.</summary>
    public void RunTick(double latenessMs = 0, bool overrun = false)
    {
        if (Finished) throw new InvalidOperationException("the run is over");
        Start();
        if (Finished) return;
        if (_nextWorld != null)
        {
            PumpBetweenRounds(); // D306
            return;
        }
        var sample = new TickSample { LatenessMicros = (long)(latenessMs * 1000), Overrun = overrun };
        _tickBytesOut = 0;
        _tickBytesIn = 0;
        long t0 = Stopwatch.GetTimestamp();

        // 2. The network and the console: validate, rate-limit, queue for the next tick. A tick that starts late by whole
        // ticks (a stall) first credits the sessions' buckets with the commands the stall held back (TokenBucket).
        _requestsThisPoll = 0;
        CreditStall(latenessMs);
        _transport?.Poll();
        DrainConsole();
        long next = World.Tick + 1;
        if (!_replay && !_quitQueued && (_stopRequested || (_endTick.HasValue && next >= _endTick.Value)))
        {
            Stopped = _stopRequested;
            QueueQuit(next);
        }
        long t1 = Stopwatch.GetTimestamp();

        // 2b. The input of the tick (M2): each joined session's jitter buffer gives one frame, queued with the actions that
        // were waiting for it. A replay has its commands from the log.
        if (!_replay && !_quitQueued) FormInputs(next);

        // 3. The world.
        Profiler.BeginTick();
        World.Step();
        long t2 = Stopwatch.GetTimestamp();
        double stepMs = (t2 - t1) * 1000.0 / Stopwatch.Frequency;
        if (stepMs > SlowStepMs && JoinsOpen)
            _out.WriteLine("status " + Profiler.SlowTick(World.Tick, stepMs));
        bool mobsFired = MobsFired();
        if (!_replay) ReadAccess();
        PollLogins();

        // 4. Replication: the tick's Delta once, then the joins of the tick.
        DeltaFrame frame = _replicator.BuildDelta(World.Tick);
        _joins.Clear();
        foreach (Session s in _joinedThisTick)
            _joins.Add((s, _replicator.BuildJoin(World.Tick, PrivateInputsOf(s))));
        long t3 = Stopwatch.GetTimestamp();

        // 5. Send, close, flush.
        if (_transport != null)
        {
            SendDeltas(frame);
            foreach (var (s, messages) in _joins)
                SendJoin(s, messages);
            SendAirflow();
            SendSounds();
            SendChat();
        }
        _joinedThisTick.Clear();
        _joins.Clear();
        ProcessCloses();
        foreach (int ordinal in _forget)
            _replicator.Forget(ordinal);
        _forget.Clear();
        _transport?.Flush();
        long t4 = Stopwatch.GetTimestamp();

        // 6. Metrics.
        sample.Tick = World.Tick;
        sample.MobsFired = mobsFired;
        sample.PollMicros = Micros(t0, t1);
        sample.StepMicros = Micros(t1, t2);
        sample.ReplicationMicros = Micros(t2, t3);
        sample.SendMicros = Micros(t3, t4);
        sample.TotalMicros = Micros(t0, t4);
        sample.Sessions = LiveSessionCount();
        sample.BytesOut = _tickBytesOut;
        sample.BytesIn = _tickBytesIn;
        Metrics.EndTick(sample);
        if (_shutdownApplied || (_replay && _endTick.HasValue && World.Tick >= _endTick.Value))
        {
            Finish(); // the final hash and the closes are not part of the tick's timing
            WriteSummary();
            return;
        }
        // M10 slice H: tg's world/Reboot asked for a new round (BYOND's reboot): the host replaces the world after the tick (D15)
        if (!_replay && DmWorldState.Of(World).RebootRequested)
        {
            RestartWorld();
            return;
        }
        SendDevAccess();
        if (World.Tick % _config.StatusEveryTicks == 0)
            EndWindow();
        if (_config.HashEvery > 0 && World.Tick % _config.HashEvery == 0)
            _out.WriteLine("hash " + StatusLine(World, World.ComputeStateHash()));
    }

    private void EndWindow()
    {
        long sent = 0, received = 0;
        foreach (Session s in _sessions)
        {
            if (s.Connection == null || s.TransportCounted) continue;
            var (wireSent, wireReceived) = TransportBytes(s.Connection);
            sent += wireSent - s.WindowSentMark;
            received += wireReceived - s.WindowReceivedMark;
            s.WindowSentMark = wireSent;
            s.WindowReceivedMark = wireReceived;
            if (s.State == SessionState.Closed) s.TransportCounted = true; // a closed connection's counters are final
        }
        Metrics.AddTransportBytes(sent, received);
        _out.WriteLine(Metrics.EndWindow(World.Tick)
            + (_rejectedRequests > 0 ? string.Create(CultureInfo.InvariantCulture, $" | rejected requests {_rejectedRequests}") : ""));
        _out.WriteLine("status " + Profiler.WindowSummary(_config.StatusEveryTicks)
            + string.Create(CultureInfo.InvariantCulture, $" | gc gen0 {GC.CollectionCount(0)} gen1 {GC.CollectionCount(1)} gen2 {GC.CollectionCount(2)} pause total {GC.GetTotalPauseDuration().TotalMilliseconds:0} ms heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB")
            + (StallCreditedTicks > 0 ? string.Create(CultureInfo.InvariantCulture, $" | stall credit {StallCreditedTicks} ticks") : ""));
        _rejectLinesThisWindow = 0;
        Log.Flush();
    }

    // The transport's own count of a connection's bytes: with its headers and acks where it measures them
    // (IChannelCounters.WireBytes*, LiteNetLib's statistics), else its payload counters.
    private static (long Sent, long Received) TransportBytes(IConnection connection)
    {
        if (connection is IChannelCounters counters && (counters.WireBytesSent > 0 || counters.WireBytesReceived > 0))
            return (counters.WireBytesSent, counters.WireBytesReceived);
        return (connection.BytesSent, connection.BytesReceived);
    }

    private void Finish()
    {
        Finished = true;
        foreach (Session s in _sessions)
        {
            if (s.Connection != null && s.Connection.IsConnected)
                CloseConnection(s, DisconnectReason.ServerShutdown, "The server is shutting down.");
            if (s.State != SessionState.Closed) s.State = SessionState.Closed;
        }
        if (_transport != null)
        {
            _transport.Flush();
            _transport.Stop();
        }
        FinalHash = World.ComputeStateHash();
        Log.Finish(World.Tick, FinalHash, SoundLengths.Digest(World));
        if (Stopped)
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"stop requested: stopped at tick {World.Tick}"));
        _out.WriteLine("final " + StatusLine(World, FinalHash)
            + string.Create(CultureInfo.InvariantCulture, $" runtimes={World.RuntimeErrorCount}"));
    }

    private void WriteSummary()
    {
        _out.Write(Metrics.Summary(_totalBytesOut, _totalBytesIn, _sessionsServed, World.RuntimeErrorCount));
        _out.WriteLine();
        _out.Flush();
    }

    /// <summary><c>tick=40 time=20 hash=0123456789ABCDEF</c>; time is <c>world.time</c> in deciseconds.</summary>
    public static string StatusLine(World world, ulong hash) =>
        string.Create(CultureInfo.InvariantCulture, $"tick={world.Tick} time={world.Time:0.0#} hash={FormatHash(hash)}");

    public static string FormatHash(ulong hash) => hash.ToString("X16", CultureInfo.InvariantCulture);

    private static long Micros(long from, long to) => (to - from) * 1_000_000 / Stopwatch.Frequency;

    private static long LastTick(CommandLog log)
    {
        long last = 0;
        foreach (QueuedCommand c in log.Commands) last = Math.Max(last, c.ApplyTick);
        return last;
    }

    private int LiveSessionCount()
    {
        int n = 0;
        foreach (Session s in _sessions)
        {
            if (s.State != SessionState.Closed) n++;
        }
        return n;
    }

    private static SessionToken RandomToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        SessionToken token;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            token = new SessionToken(BitConverter.ToUInt64(bytes), BitConverter.ToUInt64(bytes.Slice(8)));
        }
        while (token.IsNone);
        return token;
    }

    public void Dispose()
    {
        Metrics.Dispose();
        StopLauncherSide();
        _transport?.Dispose();
        _db?.Dispose();
    }

    // A player key's server-side record: the reconnect token (host only, never in the world).
    private sealed class PlayerRecord
    {
        public PlayerRecord(string key, SessionToken token)
        {
            Key = key;
            Token = token;
        }

        public string Key { get; }
        public SessionToken Token { get; }

        /// <summary>D335: the key joined with the launcher's login since this server started (its reconnect token then stands in for it).</summary>
        public bool LoggedIn { get; set; }
    }
}
