using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Anomaly.Server.Replication;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The map editor's feed (docs/ai/specs/map-editor.md 3, "The view"): a <see cref="MapDocument"/> shown as the game shows it, with no
/// server to run and no player. The feed starts the game's own world inside the program (<see cref="ServerHost"/> with
/// <see cref="ServerConfig.MapEditor"/>, over a loopback network), so the map goes through tg's init and the game's replication: the
/// models, the joints of pipes, cables and walls and the decals are the game's. The world stands in the lobby with the round's start
/// held and is ticked only to start and after an edit, so nothing of the map is played. The connection has no mob on the map: the
/// scene flies its free camera.
/// <para>
/// An edit of the document is given to the world with the next <see cref="Poll"/>: tg loads the changed tiles as a map template
/// (<see cref="ServerHost.SetMapTiles"/>), and the replication's Deltas show them.
/// </para>
/// </summary>
public sealed class EditorFeed : IWorldFeed, IDisposable
{
    private const int StartTicks = 2000; // the Master initializes within a few dozen ticks; a map that does not start in this many has failed
    private const int SettleTicks = 40; // two seconds of the lobby after the start: what an init left to a timer is done
    private const int EditTicks = 20; // the ticks after an edit, one a poll: the loader's waits, the joints' queues, a second of timers
    private const int PreviewTicks = 4; // the ticks after a preview's load: the tick that sends it, and the joints' queues
    private const int Port = 7777; // the loopback network's, where no one else listens

    private readonly LoopbackNetwork _net = new(new LoopbackOptions { Seed = 1, TimeoutSteps = int.MaxValue });
    private readonly GameClient _client;
    private readonly HashSet<(int X, int Y, int Z)> _pending = new(); // the tiles changed since the world last took them
    // the preview (docs/ai/specs/map-editor.md 5.9): the tiles the world shows otherwise than the document has them, and those whose shown model changed
    private readonly Dictionary<(int X, int Y, int Z), MapModel> _preview = new();
    private readonly HashSet<(int X, int Y, int Z)> _previewPending = new();
    private readonly Dictionary<(int X, int Y, int Z), int[]> _previewMembers = new(); // per previewed tile: its shown members' places in the document's tile
    private readonly HashSet<long> _previewThings = new(); // the shown things that are the preview's own
    private long _previewThingsAt = -1; // the tick they were found in
    private readonly Dictionary<(int X, int Y, int Z), List<long>> _previewHeld = new(); // the shown things of each previewed tile, in that tick
    private readonly string _mapFile; // null: the document has no file to load, the world reads a copy of its text
    private readonly bool _fileIsOlder; // the file is not what the document holds: a maps folder's map is loaded with the document's text in the file's place
    private readonly Func<(string Json, string SkyImage)> _settings; // the map json's text and the sky's picture, as they are when the world starts
    private readonly EditorLog _log = new();
    private Task<ServerHost> _start; // the world's start, off the caller's thread
    private ServerHost _host;
    private DmTask? _loading; // the edit tg's loader is at
    private string _failure, _scratch;
    private int _ticksOwed;
    private long _loadedRevision, _shownRevision; // the document's revision the world last took, and the one the replica shows
    private bool _disposed;
    private bool _underFloor, _markers = true; // the layers asked for
    private (bool UnderFloor, bool Markers) _layersGiven = (false, true); // the layers the world has

    // the things of one tile with their members, kept for the tick they were found in
    private (long Tick, int X, int Y, int Z) _foundAt;
    private readonly List<(long Id, int Member)> _found = new();

    /// <param name="document">The map.</param>
    /// <param name="mapFile">The map's file when it holds what the document does (a map that was opened), else null (a new map).</param>
    /// <param name="name">The name the feed shows (the file's).</param>
    /// <param name="settings">The map json's text and the file of the sky's picture, for a map that is loaded from a scratch folder; null: none.</param>
    /// <param name="fileIsOlder">The file holds an older map than the document (a kept copy restored): the world loads the document's text in its place.</param>
    public EditorFeed(MapDocument document, string mapFile, string name = null, Func<(string Json, string SkyImage)> settings = null, bool fileIsOlder = false)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _settings = settings;
        _fileIsOlder = fileIsOlder;
        _mapFile = mapFile == null ? null : Path.GetFullPath(mapFile);
        Name = name ?? (mapFile == null ? "untitled" + MapText.Extension : Path.GetFileName(mapFile));
        _client = new GameClient(_net.CreateClient(), "127.0.0.1", Port, "Map editor");
        _client.Spawned += e => Spawned?.Invoke(e);
        _client.Despawned += e => Despawned?.Invoke(e);
        _client.ServerTextReceived += text => ServerTextReceived?.Invoke(text);
        Document.Changed += changes =>
        {
            foreach (MapTileChange change in changes)
            {
                _pending.Add((change.X, change.Y, change.Z));
                DropPreview((change.X, change.Y, change.Z)); // (the tile is another: what was previewed on it is for the tool to say again)
            }
        };
    }

    /// <summary>The map the feed shows.</summary>
    public MapDocument Document { get; }

    /// <summary>The map's name as the feed shows it.</summary>
    public string Name { get; }

    /// <summary>What the world wrote: its warnings and the runtime errors of the map's init (the last few hundred lines).</summary>
    public IReadOnlyCollection<string> Log => _log.Lines;

    /// <summary>
    /// The layer of what the floor tiles cover (docs/ai/specs/map-editor.md 5.5): the cables, the pipes and the disposal pipes under an intact
    /// tile, which the game hides, are shown half clear over it. Off when a map opens.
    /// </summary>
    public bool UnderFloor
    {
        get => _underFloor;
        set => _underFloor = value;
    }

    /// <summary>The layer of the markers a mapper places: the landmarks, the spawners and the mapping helpers. On when a map opens.</summary>
    public bool Markers
    {
        get => _markers;
        set => _markers = value;
    }

    /// <summary>
    /// True when what the feed shows is the document as it is now: no edit waits for the world or for the tick that sends it. Until then a shown thing
    /// may be one the document has not got any more, so its member is not to be trusted.
    /// </summary>
    public bool Shown => _host != null && _pending.Count == 0 && _shownRevision == Document.Revision;

    /// <summary>
    /// The member a shown thing has when it is the preview's own and none of the document's (<see cref="TryLocate"/>): what a tool is about to place.
    /// </summary>
    public const int PreviewMember = -2;

    /// <summary>
    /// Shows tiles otherwise than the document has them (docs/ai/specs/map-editor.md 5.9): what a tool would place where the mouse is, made by the
    /// world as the placed thing will be made, joints and all. The document is not changed. Each call says the whole preview: a tile it does not
    /// name is shown as the document has it again; null or none takes the preview away. The world takes the change with the next polls.
    /// </summary>
    public void SetPreview(IReadOnlyCollection<(int X, int Y, int Z, MapModel Model)> tiles)
    {
        var next = new Dictionary<(int X, int Y, int Z), MapModel>();
        foreach ((int x, int y, int z, MapModel model) in tiles ?? Array.Empty<(int, int, int, MapModel)>())
        {
            if (model != null && Document.Contains(x, y, z) && !ReferenceEquals(model, Document[x, y, z]))
                next[(x, y, z)] = model;
        }
        foreach ((int X, int Y, int Z) cell in _preview.Keys.ToList())
        {
            if (!next.TryGetValue(cell, out MapModel model) || !ReferenceEquals(model, _preview[cell]))
                DropPreview(cell);
        }
        foreach (((int X, int Y, int Z) cell, MapModel model) in next)
        {
            if (_preview.ContainsKey(cell))
                continue;
            _preview[cell] = model;
            _previewPending.Add(cell);
            _previewMembers[cell] = MembersInDocument(model, Document[cell.X, cell.Y, cell.Z]);
        }
    }

    /// <summary>True when the tile is shown with a preview on it.</summary>
    public bool Previewed(int x, int y, int z) => _preview.ContainsKey((x, y, z));

    /// <summary>The previewed tiles, each with its turf's path when that turf is the preview's own and not the document's (null: the tile keeps its turf).</summary>
    public IEnumerable<(int X, int Y, int Z, string Turf)> PreviewCells
    {
        get
        {
            foreach (((int X, int Y, int Z) cell, MapModel model) in _preview)
            {
                int[] places = _previewMembers[cell];
                string turf = null;
                for (int i = 0; i < places.Length; i++)
                {
                    if (places[i] == PreviewMember && MapEdits.IsTurf(model.Members[i].Path))
                        turf = model.Members[i].Path;
                }
                yield return (cell.X, cell.Y, cell.Z, turf);
            }
        }
    }

    /// <summary>True when a shown thing is the preview's own: no member of the document, so no tool acts on it and no click meets it.</summary>
    public bool IsPreview(long thingId)
    {
        if (_preview.Count == 0 && _previewThings.Count == 0)
            return false;
        if (_previewThingsAt != _client.Tick)
        {
            _previewThingsAt = _client.Tick;
            _previewThings.Clear();
            // One pass over what is shown gives each previewed tile its things: a shuttle shown at its dock (5.11) is hundreds of tiles, and a
            // look through everything for each of them stops a large map for a moment.
            _previewHeld.Clear();
            foreach (ReplicaEntity thing in _client.Entities)
            {
                (int X, int Y, int Z) cell = (thing.State.X, thing.State.Y, thing.State.Z);
                if (!_preview.ContainsKey(cell))
                    continue;
                if (!_previewHeld.TryGetValue(cell, out List<long> held))
                    _previewHeld[cell] = held = new List<long>();
                held.Add(thing.Id);
            }
            foreach (((int X, int Y, int Z) cell, List<long> held) in _previewHeld)
            {
                foreach ((long id, int member) in ThingsAt(cell.X, cell.Y, cell.Z, held))
                {
                    if (member == PreviewMember)
                        _previewThings.Add(id);
                }
            }
        }
        return _previewThings.Contains(thingId);
    }

    private void DropPreview((int X, int Y, int Z) cell)
    {
        if (!_preview.Remove(cell))
            return;
        _previewMembers.Remove(cell);
        _previewPending.Add(cell);
    }

    // For each member of a previewed tile's shown model, its place among the document's members of that tile: the same text, each of the document's
    // taken once; PreviewMember for one the document has not got.
    private static int[] MembersInDocument(MapModel shown, MapModel document)
    {
        var places = new int[shown.Members.Count];
        var taken = new bool[document.Members.Count];
        for (int i = 0; i < places.Length; i++)
        {
            string text = MapDocument.TextOf(new[] { shown.Members[i] });
            places[i] = PreviewMember;
            for (int j = 0; j < taken.Length; j++)
            {
                if (taken[j] || MapDocument.TextOf(new[] { document.Members[j] }) != text)
                    continue;
                (places[i], taken[j]) = (j, true);
                break;
            }
        }
        return places;
    }

    /// <summary>True while the world has an edit to load or to show.</summary>
    public bool Busy => _pending.Count > 0 || _previewPending.Count > 0 || _ticksOwed > 0 || (_loading is DmTask loading && !loading.IsCompleted) || (_host != null && _layersGiven != (_underFloor, _markers));

    public FeedPhase Phase => _failure != null ? FeedPhase.Closed : _host == null ? (_start == null ? FeedPhase.Idle : FeedPhase.Connecting) : _client.Phase;

    public string CloseReason => _failure ?? _client.CloseReason;

    public bool Rejected => false;

    public string Description => ClientArgs.EditorName + ": " + Name;

    public uint LastSentSeq => 0;

    public uint LastAppliedSeq => 0;

    public MapGrid Map => _client.Map;

    public long Tick => _client.Tick;

    public long DeltaCount => _client.DeltaCount;

    /// <summary>The connection's mob waits in the lobby, off the map: the scene has no mob to follow.</summary>
    public long OwnEntity => 0;

    public int Count => _client.Count;

    public IEnumerable<ReplicaEntity> Entities => _client.Entities;

    public int RoundTripMs => 0;

    public long BytesSent => _client.BytesSent;

    public long BytesReceived => _client.BytesReceived;

    public ReplicaWorld Replica => _client.Replica;

    public event Action<ReplicaEntity> Spawned;

    public event Action<ReplicaEntity> Despawned;

    public event Action<ServerText> ServerTextReceived;

    /// <summary>
    /// The first poll starts the world on another thread (a station's init takes many seconds) and the feed is
    /// <see cref="FeedPhase.Connecting"/> until it stands; then the connection joins. After that a poll gives the world the document's
    /// edits and runs a tick only while one is owed.
    /// </summary>
    public void Poll(double nowSeconds)
    {
        if (_failure != null || _disposed)
            return;
        if (_start == null)
        {
            _start = Task.Run(Start);
            return;
        }
        if (_host == null)
        {
            if (!_start.IsCompleted)
                return;
            if (_start.IsFaulted)
            {
                Exception error = _start.Exception.GetBaseException();
                _failure = "the map did not start: " + error.Message;
                _log.WriteLine(error.ToString());
                return;
            }
            _host = _start.Result;
            _loadedRevision = _shownRevision = Document.Revision; // (an edit made while the world started is in _pending)
            _client.Connect();
        }

        bool loading = _loading is DmTask task && !task.IsCompleted;
        if (_client.Phase == FeedPhase.Playing && !loading && _layersGiven != (_underFloor, _markers))
        {
            _layersGiven = (_underFloor, _markers);
            _host.SetMapLayers(_underFloor, _markers);
            _ticksOwed = Math.Max(_ticksOwed, EditTicks);
        }
        if (_client.Phase == FeedPhase.Playing && _pending.Count > 0 && !loading)
        {
            var tiles = _pending.Select(t => (t.X, t.Y, t.Z, Document[t.X, t.Y, t.Z])).ToList();
            _pending.Clear();
            _previewPending.ExceptWith(tiles.Select(t => (t.X, t.Y, t.Z))); // (their preview was dropped when they changed: this load shows them)
            _loading = _host.SetMapTiles(tiles);
            _loadedRevision = Document.Revision;
            if (_loading.Value.IsFaulted)
                _log.WriteLine("the edit was not loaded: " + _loading.Value.Exception);
            _ticksOwed = EditTicks;
            loading = !_loading.Value.IsCompleted;
        }
        // The preview's tiles: first the ones that are shown as the document has them again, then the ones that show the preview. Each is a load of its own,
        // so that a preview that moved from one place to another loads two small squares and not the rectangle that holds both.
        for (int pass = 0; pass < 2 && _client.Phase == FeedPhase.Playing && _previewPending.Count > 0 && !loading; pass++)
        {
            var tiles = _previewPending.Where(t => _preview.ContainsKey(t) == (pass == 1)).Select(t => (t.X, t.Y, t.Z, _preview.TryGetValue(t, out MapModel shown) ? shown : Document[t.X, t.Y, t.Z])).ToList();
            if (tiles.Count == 0)
                continue;
            _previewPending.ExceptWith(tiles.Select(t => (t.X, t.Y, t.Z)));
            _loading = _host.SetMapTiles(tiles);
            if (_loading.Value.IsFaulted)
                _log.WriteLine("the preview was not loaded: " + _loading.Value.Exception);
            _ticksOwed = Math.Max(_ticksOwed, PreviewTicks);
            loading = !_loading.Value.IsCompleted;
        }
        if (_client.Phase != FeedPhase.Playing || _ticksOwed > 0 || loading)
        {
            _host.RunTick();
            _net.Advance();
            if (_ticksOwed > 0)
                _ticksOwed--;
            _client.Poll(nowSeconds);
            if (_joinedAt == 0 && _client.Phase == FeedPhase.Playing)
                _joinedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!loading)
                _shownRevision = _loadedRevision; // the tick after the load sent the tiles
            return;
        }
        _client.Poll(nowSeconds);
    }

    public void SendMotion(MotionInputMessage frames)
    {
        // no mob to move
    }

    public void SendAction(in ActionMessage action)
    {
        // the editor acts on the document
    }

    public void Disconnect() => Dispose();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _start?.Wait(); // a world that is starting is let finish: its thread holds it
        }
        catch (AggregateException)
        {
            // it failed to start: nothing to stop
        }
        _client.Dispose();
        (_host ?? (_start is { IsCompletedSuccessfully: true } ? _start.Result : null))?.Dispose();
        if (_scratch != null && Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    /// <summary>The tile of a shown thing and the member of the tile's model it came from (-1 when it is none's: what the init made); false for an id that is not shown.</summary>
    public bool TryLocate(long thingId, out int x, out int y, out int z, out int member)
    {
        (x, y, z, member) = (0, 0, 0, -1);
        if (!_client.Replica.TryGet(thingId, out ReplicaEntity thing) || !Document.Contains(thing.State.X, thing.State.Y, thing.State.Z))
            return false;
        (x, y, z) = (thing.State.X, thing.State.Y, thing.State.Z);
        foreach ((long id, int of) in ThingsAt(x, y, z))
        {
            if (id == thingId)
                member = of;
        }
        return true;
    }

    /// <summary>The first shown thing of a member of a tile's model; 0 when the member shows none (a turf, an area, a thing the init took away).</summary>
    public long ThingOf(int x, int y, int z, int member)
    {
        foreach ((long id, int of) in ThingsAt(x, y, z))
        {
            if (of == member)
                return id;
        }
        return 0;
    }

    // The shown things of a tile in the order they were made, each with the member of the tile's model it came from. The world makes a tile's things in
    // the members' order and numbers them as it makes them; a member gives the things PropKinds.ExpandSpawner names (a window spawner: its window and its
    // grille), a member the init took away gives none, and a thing no member gives (what an init made beside itself) has none.
    private List<(long Id, int Member)> ThingsAt(int x, int y, int z, List<long> held = null)
    {
        if (_foundAt == (_client.Tick, x, y, z))
            return _found;
        _foundAt = (_client.Tick, x, y, z);
        _found.Clear();
        if (!Document.Contains(x, y, z))
            return _found;
        if (held != null)
            _found.AddRange(held.Select(id => (id, -1))); // (the tile's things, found by the caller in its one pass)
        else
        {
            foreach (ReplicaEntity thing in _client.Entities)
            {
                if (thing.State.X == x && thing.State.Y == y && thing.State.Z == z)
                    _found.Add((thing.Id, -1));
            }
        }
        _found.Sort((a, b) => a.Id.CompareTo(b.Id));
        // (a previewed tile holds the preview's members: its things are matched to those, then given their places in the document's tile)
        _previewMembers.TryGetValue((x, y, z), out int[] places);
        IReadOnlyList<MapMember> members = (places != null ? _preview[(x, y, z)] : Document[x, y, z]).Members;
        int at = 0;
        List<string> left = members.Count > 0 ? PropKinds.ExpandSpawner(members[0].Path).ToList() : new List<string>();
        for (int i = 0; i < _found.Count; i++)
        {
            string type = _client.Replica.TryGet(_found[i].Id, out ReplicaEntity thing) ? thing.TypePath : null;
            for (int member = at; member < members.Count; member++)
            {
                List<string> gives = member == at ? left : PropKinds.ExpandSpawner(members[member].Path).ToList();
                if (!gives.Remove(type))
                    continue;
                (at, left) = (member, gives);
                _found[i] = (_found[i].Id, places != null ? places[member] : member);
                break;
            }
        }
        return _found;
    }

    // ---- the world's start (another thread) ----

    private volatile ServerHost _starting; // the world as it starts: its profiler says how far the Master's init is
    private volatile int _initsExpected; // the subsystems the Master initializes
    private volatile int _settled; // the lobby's ticks after the init, of SettleTicks
    private long _startedAt, _madeAt, _initializedAt, _settledAt, _joinedAt; // the clock at the ends of the start's parts

    /// <summary>
    /// How far the world's start is, 0 to 1, and what it does now (the editor's loading screen). Its parts, by the time they take: the world is made
    /// (half of a start, and nothing says how far it is: the bar creeps by the clock, never reaching the part's end), the Master initializes its
    /// subsystems one by one (counted), the lobby's first seconds, and the map is read into the editor.
    /// </summary>
    public (float Fraction, string Doing) StartProgress
    {
        get
        {
            const float Made = 0.45f, Initialized = 0.9f, Settled = 0.94f;
            if (_host != null)
                return (_client.Phase == FeedPhase.Playing ? 1f : Settled, "reading the map into the editor");
            ServerHost host = _starting;
            if (host == null)
            {
                long started = System.Threading.Volatile.Read(ref _startedAt);
                double seconds = started == 0 ? 0 : (System.Diagnostics.Stopwatch.GetTimestamp() - started) / (double)System.Diagnostics.Stopwatch.Frequency;
                return ((float)(Made * seconds / (seconds + 3.0)), "starting the game's world");
            }
            int expected = _initsExpected, done = host.Profiler.InitsDone, settled = _settled;
            if (settled > 0 || (expected > 0 && done >= expected))
                return (Initialized + (Settled - Initialized) * settled / SettleTicks, "the world's first seconds");
            string now = host.Profiler.InitNow;
            return (Made + (Initialized - Made) * (expected > 0 ? (float)done / expected : 0f), now == null ? "initializing the game's subsystems" : "initializing " + now);
        }
    }

    /// <summary>How long the parts of the start took, for the log: the world made, its init, the lobby's first seconds, the join.</summary>
    public string StartTimes
    {
        get
        {
            static double Seconds(long from, long to) => (to - from) / (double)System.Diagnostics.Stopwatch.Frequency;
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the world made in {Seconds(_startedAt, _madeAt):0.0} s, its init {Seconds(_madeAt, _initializedAt):0.0} s, the lobby's first seconds {Seconds(_initializedAt, _settledAt):0.0} s, the join {Seconds(_settledAt, _joinedAt):0.0} s");
        }
    }

    private ServerHost Start()
    {
        _startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        (string json, string skyImage) = _settings?.Invoke() ?? (null, null);
        (string folder, string name, _scratch) = MapsOf(Document, _mapFile, Name, json, skyImage);
        var config = new ServerConfig
        {
            Seed = 1, // the same picks (a random poster's picture) every time the map is opened
            Port = Port,
            Console = false,
            StatusEveryTicks = int.MaxValue,
            Map = name,
            MapsFolder = folder,
            // (a scratch copy is the document's text already)
            MapTexts = _fileIsOlder && _scratch == null ? new Dictionary<string, string> { [_mapFile] = Document.ToText() } : null,
            Lobby = true,
            MapEditor = true,
        };
        var host = new ServerHost(config, _net.CreateServer(), new Replicator(new ReplicatorOptions { Interest = null, MapMarkers = true }), output: _log, errors: _log);
        try
        {
            host.Start();
            _starting = host;
            _madeAt = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int tick = 0; !host.JoinsOpen && tick < StartTicks; tick++)
            {
                Step(host);
                if (_initsExpected == 0)
                    _initsExpected = host.InitsExpected(); // (the Master is made at the boot, before it initializes)
            }
            if (!host.JoinsOpen)
                throw new InvalidOperationException("its init did not finish in " + StartTicks + " ticks");
            _initializedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int tick = 0; tick < SettleTicks; tick++)
            {
                Step(host);
                _settled = tick + 1;
            }
            _settledAt = System.Diagnostics.Stopwatch.GetTimestamp();
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private void Step(ServerHost host)
    {
        host.RunTick();
        _net.Advance();
    }

    // The maps folder and the map json's name that load the map (tg's layout, docs/ai/specs/map-format.md 2): the folder above the file whose map_jsons has a json
    // that names it, so the map starts with its shuttles and templates. A document with no such file gets a scratch folder with its text and the least json,
    // which the caller removes when its world is over.
    // The scratch folder's json is the map's own (jsonText: its settings and its level traits) with the scratch files' names, and the sky's picture is
    // copied beside the map there.
    internal static (string Folder, string Name, string Scratch) MapsOf(MapDocument document, string mapFile, string shownName, string jsonText = null, string skyImage = null)
    {
        if (mapFile != null && FindJson(mapFile) is { } found)
            return (found.Folder, found.Name, null);
        string stem = new(Path.GetFileNameWithoutExtension(shownName).Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        if (stem.Length == 0)
            stem = "map";
        string scratch = Path.Combine(Path.GetTempPath(), "anomaly-editor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(scratch, "map_jsons"));
        Directory.CreateDirectory(Path.Combine(scratch, "map_files", stem));
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(Path.Combine(scratch, "map_files", stem, stem + MapText.Extension), document.ToText(), utf8);
        System.Text.Json.Nodes.JsonObject config = null;
        try
        {
            config = MapSettings.ParseObject(jsonText);
        }
        catch (JsonException)
        {
            // a json that cannot be read gives nothing: the least one loads the map
        }
        config ??= new System.Text.Json.Nodes.JsonObject { ["version"] = 1, ["map_name"] = stem };
        (config["map_path"], config["map_file"]) = ("map_files/" + stem, stem + ".dmm");
        File.WriteAllText(Path.Combine(scratch, "map_jsons", stem.ToLowerInvariant() + ".json"), config.ToJsonString(), utf8);
        if (skyImage != null && File.Exists(skyImage))
            File.Copy(skyImage, Path.Combine(scratch, "map_files", stem, Path.GetFileName(skyImage)), overwrite: true);
        // the game's own shuttles, so that a lone map's docks load theirs (tg's arrivals shuttle, and those CentCom's stand-in docks hold)
        string stock = GameMaps == null ? null : Path.Combine(GameMaps, "shuttles");
        if (stock != null && Directory.Exists(stock))
        {
            foreach (string file in Directory.EnumerateFiles(stock, "*", SearchOption.AllDirectories))
            {
                string copy = Path.Combine(scratch, "shuttles", Path.GetRelativePath(stock, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(file, copy);
            }
        }
        return (scratch, stem.ToLowerInvariant(), scratch);
    }

    /// <summary>
    /// The game's own maps folder (tg's layout): the nearest <c>maps</c> with a <c>map_jsons</c> above the working folder or the program's, unless the
    /// program names another (a build's, beside its executable); null when there is none. A new map is offered a place in it, and a map of no maps
    /// folder is played with its shuttles.
    /// </summary>
    public static string GameMaps { get; set; } = FindGameMaps();

    private static string FindGameMaps()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "maps", "map_jsons")))
                    return Path.Combine(dir, "maps");
            }
        }
        return null;
    }

    /// <summary>
    /// The maps folder a map's file lies in as tg lays a station out, <c>map_files/NAME/NAME</c> of a folder that has <c>map_jsons</c>, and the name
    /// of the json that would name it there (a plain word, as the server's <c>--map</c> takes); null for a file of any other place. A save of a
    /// map at such a place writes that json when no json names the map yet (<see cref="EditorSession.SaveAs"/>).
    /// </summary>
    public static (string Folder, string Name)? PlaceOf(string mapFile)
    {
        if (string.IsNullOrEmpty(mapFile))
            return null;
        mapFile = Path.GetFullPath(mapFile);
        string stem = Path.GetFileNameWithoutExtension(mapFile), own = Path.GetDirectoryName(mapFile), files = Path.GetDirectoryName(own), folder = Path.GetDirectoryName(files);
        if (folder == null || !string.Equals(Path.GetFileName(own), stem, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(files), "map_files", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.Combine(folder, "map_jsons")))
            return null;
        return (folder, JsonName(stem));
    }

    /// <summary>The name of the map json of a map's file: the file's, in small letters, a plain word of letters, digits, <c>_</c> and <c>-</c>.</summary>
    public static string JsonName(string stem)
    {
        string name = new(stem.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        return name.Length == 0 ? "map" : name;
    }

    /// <summary>
    /// The maps folder (tg's layout) and the name of its map json that names a map's file, so that the map loads with that folder's shuttles and
    /// templates; null for a file that no maps folder above it names.
    /// </summary>
    public static (string Folder, string Name)? FindJson(string mapFile)
    {
        if (string.IsNullOrEmpty(mapFile))
            return null;
        mapFile = Path.GetFullPath(mapFile);
        for (string folder = Path.GetDirectoryName(mapFile); folder != null; folder = Path.GetDirectoryName(folder))
        {
            string jsons = Path.Combine(folder, "map_jsons");
            if (!Directory.Exists(jsons))
                continue;
            foreach (string json in Directory.EnumerateFiles(jsons, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                if (Names(json, folder, mapFile))
                    return (folder, Path.GetFileNameWithoutExtension(json));
            }
        }
        return null;
    }

    // Whether a map json of a maps folder names the file as one of its map files (map_path and map_file: tg's own file name, which our format's file bears with its own extension).
    private static bool Names(string json, string folder, string mapFile)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(json));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("map_path", out JsonElement path) || path.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("map_file", out JsonElement files))
                return false;
            IEnumerable<JsonElement> names = files.ValueKind == JsonValueKind.Array ? files.EnumerateArray() : new[] { files };
            foreach (JsonElement file in names)
            {
                if (file.ValueKind == JsonValueKind.String
                    && string.Equals(Path.GetFullPath(Path.Combine(folder, path.GetString(), Path.ChangeExtension(file.GetString(), MapText.Extension))), mapFile, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException)
        {
            return false; // a json that cannot be read names nothing
        }
    }

    // What the world writes, the last lines of it: any thread writes, the editor reads.
    internal sealed class EditorLog : TextWriter
    {
        private const int Kept = 400;
        private readonly Queue<string> _lines = new();
        private readonly StringBuilder _line = new();

        public override Encoding Encoding => Encoding.UTF8;

        public string[] Lines
        {
            get
            {
                lock (_lines)
                    return _lines.ToArray();
            }
        }

        public override void Write(char value)
        {
            lock (_lines)
            {
                if (value != '\n')
                {
                    _line.Append(value);
                    return;
                }
                _lines.Enqueue(_line.ToString().TrimEnd('\r'));
                _line.Clear();
                if (_lines.Count > Kept)
                    _lines.Dequeue();
            }
        }
    }
}
