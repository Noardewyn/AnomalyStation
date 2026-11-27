using System.Globalization;
using System.Linq;
using System.Text;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>Settings of the scripted server.</summary>
public sealed class FakeServerOptions
{
    /// <summary>The room's width and height in tiles, rim included (at least 12).</summary>
    public int Size { get; init; } = 32;

    /// <summary>Wandering bots.</summary>
    public int Bots { get; init; } = 8;

    /// <summary>Seeds the bots and the room.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>Dev commands are accepted (the server's <c>DEV_COMMANDS</c>).</summary>
    public bool DevCommands { get; init; } = true;

    /// <summary>The server's <c>MOVE_SPEED_SCALE</c> (D106).</summary>
    public float SpeedScale { get; init; } = 1f;

    /// <summary>Whether the scripted leaver and joiner (ticks 200 and 240) run.</summary>
    public bool ScriptedEvents { get; init; } = true;

    /// <summary>The bots stand where they are put (a body to bump into or to shove); they do not wander.</summary>
    public bool StillBots { get; init; }

    /// <summary>The most input frames the player's jitter buffer keeps (the real server's <see cref="Limits.MaxJitterBufferFrames"/>).</summary>
    public int JitterCap { get; init; } = Limits.MaxJitterBufferFrames;

    /// <summary>
    /// M3 (the FakeFeed version-3 contract of the M3 contracts step; M3 P6 implements it): serve this converted map instead
    /// of the generated room (<see cref="Size"/> is then ignored; the bots spawn on <see cref="FakeMap.SpawnCells"/>).
    /// </summary>
    public FakeMap Map { get; init; }

    /// <summary>
    /// M4a (the FakeServer version-4 contract of the M4a contracts step; M4a P7 implements it, docs/ai/specs/interaction.md
    /// section 8): the player gets tg's hands and a storage to drive the client's interaction code without the real server.
    /// The private block then carries the screen entries the real server sends for the same state (tg's screen objects:
    /// the two hand slots <c>/atom/movable/screen/inventory/hand</c> with the held item's picture in their appearance and, from
    /// protocol version 12, the item as the entry's clickable vis_contents child (<see cref="ScreenEntry.VisChildren"/>; so the worn
    /// items of the slots), swap,
    /// drop and throw in <see cref="HudGroup.Static"/>; the 15 slots in <see cref="HudGroup.ToggleableInventory"/>; an open
    /// storage's cells, its close button and its items, at tg's <c>orient_storage</c> <c>screen_loc</c>s, in
    /// <see cref="HudGroup.Storage"/>), and the fake serves:
    /// <list type="bullet">
    /// <item><description>a toolbox (<c>/obj/item/storage/toolbox/mechanical</c> with tg's screwdriver, wrench, welder, crowbar and wirecutters) and an airlock near the spawn;</description></item>
    /// <item><description>an entity click on an item in reach: into the active hand (tg's <c>attack_hand</c> to <c>put_in_hands</c>); a click on the held toolbox with the other, empty hand, or a right click on it: its storage opens; a screen click on an item entry: into the active hand; on a cell entry with an item in hand: the item goes in; on the close entry: the storage closes;</description></item>
    /// <item><description>version 12, a screen click on the item in a hand or a slot (the slot entry's child, tg's item <c>Click</c>): the active hand's own item is used (<c>attack_self</c>); with the active hand empty a toolbox opens and another item goes to the active hand (<c>attack_hand</c>); with an item in the active hand it goes into the toolbox (<c>attackby</c>);</description></item>
    /// <item><description>the keybindings swap hands, drop (to the floor at tg's <c>rand(-6, 6)</c> offsets), activate in-hand (the welder lights, its appearance changes) and throw mode (a turf click throws the held item: the entity is flagged <see cref="EntityFlags.Thrown"/> for its flight of tg's tiles at 3 tiles per tick);</description></item>
    /// <item><description>a hover: a <see cref="ContextHintsMessage"/> with tg's labels for the target and the held item (the toolbox: LMB "Pick up", RMB "Open"; the airlock with a lit welder: RMB "Weld shut");</description></item>
    /// <item><description>a right click on the airlock with a lit welder: a private progress-bar image (<see cref="ImageEntry"/>, tg's <c>prog_bar_NN</c> states over 40 ticks) and then the welded overlay.</description></item>
    /// </list>
    /// Off by default: the M2 and M3 fakes stay as they are.
    /// </summary>
    public bool Inventory { get; init; }

    /// <summary>M4a (P7, beside <see cref="Inventory"/>): ticks a thrown item takes per tile (1: a tile every tick, 20 a second; a larger number makes the flight slow enough to see, for the shots).</summary>
    public int ThrowTicksPerTile { get; init; } = 1;

    /// <summary>
    /// M4b audio slice 3: every 2 s (40 ticks) the server plays tg's airlock sound (<c>sound/machines/airlock/airlock.ogg</c>) 3 tiles
    /// east of the player (protocol 13, a <see cref="Sounds"/> message after the tick's Delta), so the fake world is audible. Off by
    /// default: the older fakes' message streams stay as they are; <c>--fake-server</c> turns it on.
    /// </summary>
    public bool DoorSounds { get; init; }
}

/// <summary>
/// The client's own scripted server (<c>--fake-server</c> and the client tests): a 20 TPS world of capsule mobs that speaks
/// protocol version 2 and behaves as the real server does as far as the client can tell. It is not a copy of tg; it runs the same
/// controller (<see cref="MotionStep"/>) with the same parameters, so the client's prediction can be checked against it bit for
/// bit, and it has just enough of tg's rules to exercise every client path:
/// <list type="bullet">
/// <item><description>the player's frames go through a jitter buffer (one frame per tick, the last one repeated when none came, the oldest dropped above the cap, 4) and the private block carries the state after the last applied frame, the parameters and the buffer's depth;</description></item>
/// <item><description>rest (lying at once, getting up takes 1 s), walk or run (tg's 4 and 1.5 deciseconds per tile, crawling adds 4), combat mode, a left click on a mob in reach (help) and a right click (the shove: one tile away, a knockdown against a wall or a mob, the click cooldown of 16 ticks);</description></item>
/// <item><description>the player's HUD as private screen entries: the health doll (tg's <c>head3</c> style states per zone, level = <c>ceil(damage / max * 5)</c>), the health indicator, a damage fullscreen, the move intent, tg's zone selector (a click selects the zone under its pixel, tg's <c>get_zone_at</c>) and the combat toggle (a click switches combat mode); a wounded limb carries tg's wound outline filter, steady;</description></item>
/// <item><description>dev commands in the real server's syntax (<c>damage WHO brute|burn|tox|oxy N [ZONE]</c>, <c>wound WHO ZONE [TYPE SEVERITY]</c>, <c>heal</c>, <c>fullheal</c>, <c>kill</c>, <c>revive</c>, <c>knockdown WHO S</c>, <c>stun</c>, <c>analyze WHO</c>, <c>speedscale F</c>, <c>spawn /mob/living/carbon/human [X Y]</c>, <c>help</c>; WHO is <c>me</c>, the player's name or a world id), replies to the requester only; and the fake's own <c>alert me NAME</c>, which throws or clears one of tg's alerts (<see cref="AlertNames"/>) at tg's places in its column;</description></item>
/// </list>
/// Everything leaves as encoded messages, through <see cref="Step"/>'s callback; the owner (<see cref="FakeFeed"/>, a test
/// harness) moves them to a client.
/// </summary>
public sealed partial class FakeServer
{
    public const long OwnId = 1;
    private const long ItemIdBase = 100000;
    private const long JoinerId = 200000;
    private const long SpawnedIdBase = 300000;
    private const string HumanType = "/mob/living/carbon/human";
    private const string ItemType = "/obj/item/toolbox";
    private const int GetUpTicks = 20;
    private const int ShoveCooldownTicks = 16;
    private const int KnockdownWallTicks = 60;
    private const int ShoveGlideTicks = 6;

    // Private (session) table layout: what the client's HUD asks the atlas for.
    private const string ScreenGenIcon = "icons/hud/screen_gen.dmi";
    private const string ScreenFullIcon = "icons/hud/screen_full.dmi";
    private const long DollId = 1001, HealthId = 1002, FullscreenId = 1003, IntentId = 1004;

    /// <summary>tg's target zone selector (<c>zone_sel</c>) and combat toggle on the player's screen.</summary>
    public const long ZoneSelId = 1005, CombatId = 1006;

    /// <summary>
    /// tg's alerts the fake throws with <c>alert WHO NAME</c> (slice D): the name, the screen entry's id (<see cref="AlertIdBase"/> plus the index),
    /// and the picture: tg's <c>screen_alert.dmi</c> state, or for weightless (tg's USER_HUD_STYLE_INHERIT) the HUD style's <c>template</c> with
    /// the alert's state as an overlay. A click on the buckled one unbuckles (tg's <c>/alert/buckled/Click</c>): it goes.
    /// </summary>
    public static readonly string[] AlertNames = { "not_enough_oxy", "cold", "weightless", "buckled" };

    /// <summary>The first fake alert's screen entry id.</summary>
    public const long AlertIdBase = 1100;

    private const string AlertIcon = "icons/hud/screen_alert.dmi";
    private readonly List<int> _alertsUp = new(); // tg's mob.alerts, in the order thrown
    private readonly Dictionary<int, TableRef> _alertLooks = new();

    private static readonly int[] ZoneMax = { 200, 200, 50, 50, 50, 50 };

    private readonly FakeServerOptions _options;
    private readonly string _name;
    private readonly Random _random;
    private readonly bool[,] _closed;
    private readonly MapGrid _map;

    // M3 P6: the converted-map mode (FakeServerOptions.Map): the cells' mutable solidity and flags, the map's entities, the animated doors.
    private readonly bool _isMap;
    private readonly int _width, _height;
    private byte[] _solidBits;
    private byte[] _cellFlags;
    private readonly AppearanceTables _tables = new();
    private FakeMapBinding _binding;
    private readonly List<MapEntity> _mapEntities = new();
    private readonly Dictionary<long, MapEntity> _mapById = new();
    private readonly List<DoorRun> _doorRuns = new();
    private readonly List<(int X, int Y, int Z, bool Solid)> _solidChanges = new();
    private readonly List<(int X, int Y, int Z, byte Flags)> _flagChanges = new();

    private sealed class MapEntity
    {
        public FakeMapEntity Source;
        public EntityState Wire;
        public EntityState Sent;
        public int TypeIndex;
        public TableRef Appearance;

        /// <summary>For a windoor, whether it is open (its cell is never solid; its face is blocked while it is shut).</summary>
        public bool DoorOpen;
    }

    private enum DoorPhase : byte { Opening, Closing, Deny }

    // A bump-opened door's auto-close (tg's autoclose_in): at AtTick it closes unless a mob is in its doorway, then it tries again later.
    private sealed class AutoClose
    {
        public MapEntity Door;
        public long AtTick;
    }

    private readonly List<AutoClose> _autoClose = new();
    private Dictionary<(int X, int Y, int Z), List<MapEntity>> _bumpDoors;

    private sealed class DoorRun
    {
        public MapEntity Door;
        public DoorPhase Phase;
        public long StartTick;
        public bool FlippedCell;
    }
    private readonly List<Mob> _mobs = new();
    private readonly MotionEvents _events = new();
    private readonly CollisionView _view;
    private long _tick;
    private float _speedScale;
    private bool _joinerSpawned;
    private int _spawnCount;

    // The player's input.
    private readonly List<MotionInput> _jitter = new();
    private MotionInput _lastInput;
    private bool _hasLastInput;
    private uint _newestSeen;
    private uint _appliedSeq;
    private readonly List<ActionMessage> _pendingActions = new();

    // The public tables (shared by every client) and the player's private ones.
    private readonly List<string> _types = new() { HumanType, ItemType };
    private readonly List<string> _pubResources = new();
    private readonly List<string> _pubStrings = new();
    private readonly List<AppearanceEntry> _pubAppearances = new();
    private int _sentResources, _sentStrings, _sentAppearances;
    private TableRef _floorApp, _wallApp, _itemApp;
    private TableRef[] _humanApps, _humanLyingApps;

    // The crew of the fake feed (M3 R2): tg's human picture composed the way update_icons does (limbs tinted by the skin, face, a GAGS jumpsuit), as
    // the real server will send it. Before this the bots had icon_state "human", which tg's human.dmi lacks: black capsules on the map.
    private static readonly string[] Skins = { "#ffe0d1", "#fcccb3", "#e8b59b", "#d9ba93", "#a98f6a", "#6b4a2e" };
    private static readonly string[] Suits = { "#3f3f3f", "#b0302e", "#2e60b0", "#2eb04f", "#d6a82b", "#7f3fa8" };

    private static HumanLook LookOf(int index, int lyingAngle = 0)
        => new(Skins[index % Skins.Length], index % 3 == 2 ? null : (index % 2 == 0 ? "#3b2a1a" : "#c9a14a"), Suits[(index / 2 + index) % Suits.Length], null, null, lyingAngle);

    private TableRef HumanAppOf(long id, bool lying) => lying ? _humanLyingApps[(int)(id % _humanLyingApps.Length)] : _humanApps[(int)(id % _humanApps.Length)];

    private readonly List<string> _privResources = new();
    private readonly List<string> _privStrings = new();
    private readonly List<AppearanceEntry> _privAppearances = new();
    private readonly Dictionary<string, TableRef> _privLeaf = new();
    private int _sentPrivResources, _sentPrivStrings, _sentPrivAppearances;
    private readonly Dictionary<long, ScreenEntry> _screen = new();
    private readonly Dictionary<long, ScreenEntry> _screenSent = new();
    private string _dollSignature = "";

    /// <param name="playerName">The own mob's name.</param>
    public FakeServer(string playerName, FakeServerOptions options = null)
    {
        _options = options ?? new FakeServerOptions();
        _isMap = _options.Map != null;
        if (!_isMap && _options.Size < 12)
            throw new ArgumentOutOfRangeException(nameof(options), "the fake room needs at least 12 x 12 tiles");
        _name = playerName;
        _speedScale = _options.SpeedScale;
        _random = new Random(_options.Seed);
        _width = _isMap ? _options.Map.Width : _options.Size;
        _height = _isMap ? _options.Map.Height : _options.Size;
        _closed = new bool[_width + 2, _height + 2];
        _view = new CollisionView(this);
        BuildTables();
        if (_isMap)
        {
            _map = BuildMapFromFile();
            PopulateOnMap();
        }
        else
        {
            _map = BuildMap();
            Populate();
        }
        BuildHud(initial: true);
        if (_options.Inventory)
        {
            InitInventory();
            InitHotbar(); // ui-overhaul.md section 10 (DV689)
            BuildInventoryHud();
        }
    }

    /// <summary>The server's tick.</summary>
    public long Tick => _tick;

    /// <summary>The room's size in tiles (the larger side of a served map).</summary>
    public int Size => _isMap ? Math.Max(_width, _height) : _options.Size;

    /// <summary>The map the server sent.</summary>
    public MapGrid Map => _map;

    /// <summary>True when (<paramref name="x"/>, <paramref name="y"/>) is a wall.</summary>
    public bool IsWall(int x, int y) => _isMap ? SolidAt(x, y, 1) : _closed[x, y];

    /// <summary>The player's authoritative state.</summary>
    public MotionState PlayerState => _mobs[0].Motion;

    /// <summary>The player's posture, as tg's body position.</summary>
    public bool PlayerLying => _mobs[0].Lying;

    /// <summary>The player's last applied input seq.</summary>
    public uint AppliedSeq => _appliedSeq;

    /// <summary>Ticks in which the player's buffer was empty and the last frame was repeated.</summary>
    public int Starved { get; private set; }

    /// <summary>Frames the buffer dropped because more than 3 were waiting (the oldest goes, scope 2d).</summary>
    public int Dropped { get; private set; }

    /// <summary>Frames waiting in the player's buffer after the last tick.</summary>
    public int BufferDepth => _jitter.Count;

    /// <summary>The server's walk speed scale (<c>speedscale</c>).</summary>
    public float SpeedScale => _speedScale;

    /// <summary>Puts a mob on a tile (centre) with its capsule: the tests' way to arrange a meeting.</summary>
    public void Place(long id, int tileX, int tileY)
    {
        Mob mob = Find(id) ?? throw new ArgumentException("no such mob", nameof(id));
        mob.Motion = MotionState.AtTile(tileX, tileY, 1, mob.Motion.Yaw);
    }

    /// <summary>The state of a mob by id, for the tests.</summary>
    public bool TryGetState(long id, out MotionState state)
    {
        Mob mob = Find(id);
        state = mob?.Motion ?? default;
        return mob != null;
    }

    /// <summary>The collision view the server steps with (for a predictor that wants the server's own).</summary>
    public ICollisionView View => _view;

    /// <summary>The ids of the mobs, the player first.</summary>
    public IEnumerable<long> MobIds => _mobs.Where(m => !m.IsStatic).Select(m => m.Id);

    // ---- the join ----

    /// <summary>The messages of the join sequence: Welcome, MapGrid, Snapshot (State channel).</summary>
    public List<byte[]> JoinMessages()
    {
        _doorSoundPathSent = false; // the client's sound path table starts again with the MapGrid
        var messages = new List<byte[]>
        {
            Codec.Encode(new Welcome(1, SessionToken.None, _tick, 20, OwnId)),
            Codec.Encode(_map),
        };
        var snapshot = new Snapshot { Tick = _tick, LastInputSeq = _appliedSeq, OwnEntity = OwnId };
        snapshot.Types.AddRange(_types);
        snapshot.Resources.AddRange(_pubResources);
        snapshot.Strings.AddRange(_pubStrings);
        snapshot.Appearances.AddRange(_pubAppearances);
        _sentResources = _pubResources.Count;
        _sentStrings = _pubStrings.Count;
        _sentAppearances = _pubAppearances.Count;
        foreach (Mob m in _mobs)
        {
            m.Sent = m.Wire();
            snapshot.Entities.Add(m.Sent);
        }
        foreach (MapEntity e in _mapEntities)
        {
            e.Sent = e.Wire;
            snapshot.Entities.Add(e.Wire);
        }
        if (InventoryOn)
            InventorySnapshot(snapshot);
        snapshot.Own = BuildOwnBlock(snapshot: true);
        messages.Add(Codec.Encode(snapshot));
        return messages;
    }

    // ---- what the client sends ----

    /// <summary>A message of the Input channel: the newest frames. A frame already seen or stood in for is ignored; the buffer keeps at most the cap.</summary>
    public void OnMotion(MotionInputMessage message)
    {
        foreach (MotionInput frame in message.Frames)
        {
            if (frame.Seq <= _newestSeen || frame.Seq <= _appliedSeq)
                continue;
            if (_jitter.Count >= _options.JitterCap)
            {
                _jitter.RemoveAt(0);
                Dropped++;
            }
            _jitter.Add(frame);
            _newestSeen = frame.Seq;
        }
    }

    /// <summary>An action of the Control channel: it is applied in the tick that applies its frame.</summary>
    public void OnAction(ActionMessage action)
    {
        _actionsReceived++;
        if (_pendingActions.Count < 64)
            _pendingActions.Add(action);
    }

    // Version 43: the Action messages received; less the pending ones, the settled ones the private block reports (as the real server's
    // Session), so the client's guesses of its hands (OwnGuesses, which the hand indices of the fake's hand boxes turn on) end with the answer
    private uint _actionsReceived;

    // ---- the tick ----

    /// <summary>Runs one tick and gives the messages it produced (a Delta on the State channel, texts on the Control channel) to <paramref name="emit"/>.</summary>
    public void Step(Action<Channel, byte[]> emit)
    {
        _tick++;
        Mob player = _mobs[0];

        // The command phase: this tick's frame of the player, then the actions that belong to it.
        MotionInput frame;
        if (_jitter.Count > 0)
        {
            frame = _jitter[0];
            _jitter.RemoveAt(0);
            _lastInput = frame;
            _hasLastInput = true;
            _appliedSeq = frame.Seq;
        }
        else
        {
            // No frame came in time: the last one is repeated (scope 2d), standing in for the next seq (D331, as the real server's buffer)
            if (_hasLastInput)
            {
                _lastInput = _lastInput with { Seq = _lastInput.Seq + 1 };
                _appliedSeq = _lastInput.Seq;
                Starved++;
            }
            frame = _hasLastInput ? _lastInput : MotionInput.Idle(0, player.Motion.Yaw);
        }
        var replies = new List<string>();
        ApplyActions(replies);

        if (_options.ScriptedEvents && !_isMap && _tick == 200 && _options.Bots >= 2)
        {
            Mob leaving = _mobs.Find(m => m.Id == 3);
            if (leaving != null)
            {
                _mobs.Remove(leaving);
                _despawned.Add(leaving.Id);
            }
        }

        // The locomotion phase: every mob in id order.
        foreach (Mob m in _mobs)
        {
            if (m.IsStatic)
                continue;
            m.Tick(this);
            MotionInput input;
            if (ReferenceEquals(m, player))
                input = frame;
            else
                input = m.Walker == null ? MotionInput.Idle(0, m.Motion.Yaw) : m.WalkerInput(_tick);
            MotionParams parameters = ParamsFor(m);
            m.Motion.Driver = m.Lying ? MotionDriver.Prone : MotionDriver.Grounded;
            m.Motion.Posture = m.Lying ? Posture.Lying : Posture.Standing;
            _stepping = m;
            _events.Clear();
            MotionStep.Step(ref m.Motion, input, parameters, _view, _events);
            _stepping = null;
            m.LastParams = parameters;
            if (_isMap && ReferenceEquals(m, player))
                BumpDoors(player);
        }

        if (_options.ScriptedEvents && !_isMap && _tick == 240 && !_joinerSpawned)
        {
            _joinerSpawned = true;
            var joiner = new Mob(JoinerId, "Late Joiner", new AutoWalker(_options.Seed + 300), this);
            PlaceFree(joiner);
            _mobs.Add(joiner);
            _mobs.Sort((a, b) => a.Id.CompareTo(b.Id)); // the view and the step walk the mobs in id order
            _spawnedThisTick.Add(joiner);
        }

        if (_isMap)
            StepDoors();
        if (InventoryOn)
            StepInventory();
        BuildHud(initial: false);
        if (InventoryOn)
        {
            BuildInventoryHud();
            BuildHotbar(); // DV689: its pictures' rows go out in this Delta, the message after it
        }
        emit(Channel.State, Codec.Encode(BuildDelta()));
        foreach (string text in replies)
            emit(Channel.Control, Codec.Encode(new ServerText("dev", text)));
        if (InventoryOn)
            FlushOutbox(emit);
        if (_options.DoorSounds && _tick % DoorSoundTicks == 0)
            emit(Channel.State, Codec.Encode(DoorSound(player)));
    }

    // ---- M4b audio: the door sound ----

    public const string DoorSoundPath = "sound/machines/airlock/airlock.ogg";
    private const int DoorSoundTicks = 40;
    private bool _doorSoundPathSent;
    private int _doorSounds;

    /// <summary>The door sounds sent (the tests).</summary>
    public int DoorSoundsSent => _doorSounds;

    // tg's airlock open: playsound(src, 'sound/machines/airlock/airlock.ogg', 30, TRUE) heard 3 tiles away is about 17 (tg's
    // falloff, sound.dm: 21.5 at 1 tile, 13.9 at 5), on one of SSsounds' random channels, at tg's random frequency (vary).
    private Sounds DoorSound(Mob player)
    {
        var message = new Sounds();
        if (!_doorSoundPathSent)
        {
            message.FirstPath = 1;
            message.NewPaths.Add(DoorSoundPath);
            _doorSoundPathSent = true;
        }
        int x = Math.Clamp(player.Motion.TileX + 3, 1, Math.Min(_width, 255));
        int y = Math.Clamp(player.Motion.TileY, 1, Math.Min(_height, 255));
        _doorSounds++;
        message.Events.Add(new SoundEvent
        {
            Kind = SoundKind.Play,
            Channel = (ushort)(1 + _doorSounds % 16),
            Path = 1,
            Volume = 34,
            Environment = 0,
            Positional = true,
            X = (byte)x,
            Y = (byte)y,
            Z = Math.Max(1, player.Motion.Z),
            Frequency = 32000 + (int)(_tick * 7919 % 23001),
        });
        return message;
    }

    private readonly List<long> _despawned = new();
    private readonly List<Mob> _spawnedThisTick = new();
    private Mob _stepping;

    private MotionParams ParamsFor(Mob m)
    {
        // tg's slowdown in deciseconds per tile: run 1.5, walk 4, crawling +4 (scope 2c).
        float slowdown = (m.Run ? 1.5f : 4f) + (m.Lying ? 4f : 0f);
        short delay = (short)Math.Max(1, (int)Math.Floor(slowdown / MotionMath.TickDeciseconds));
        return new MotionParams(MotionMath.TilesPerTickFor(slowdown, _speedScale), MotionMath.DefaultRadius, m.CanMove, delay);
    }

    private void ApplyActions(List<string> replies)
    {
        for (int i = 0; i < _pendingActions.Count; i++)
        {
            ActionMessage a = _pendingActions[i];
            if (a.FrameSeq > _appliedSeq)
                continue; // its frame has not been applied yet
            _pendingActions.RemoveAt(i--);
            Mob player = _mobs[0];
            switch (a.Kind)
            {
                case ActionKind.Rest:
                    player.ToggleRest(replies);
                    break;
                case ActionKind.ToggleMoveIntent:
                    player.Run = !player.Run;
                    replies.Add(player.Run ? "You are running." : "You are walking.");
                    break;
                case ActionKind.ToggleCombatMode:
                    player.Combat = !player.Combat;
                    replies.Add(player.Combat ? "You are now in combat mode." : "You are no longer in combat mode.");
                    break;
                case ActionKind.Click:
                    Click(player, a, replies);
                    break;
                case ActionKind.Throw when InventoryOn && a.EffectiveTargetKind == TargetKind.Turf:
                    ThrowAt(player, a.TurfX, a.TurfY, a.TurfZ, replies); // version 81 (D328): a throw whatever the throw mode is
                    break;
                case ActionKind.DevCommand:
                    DevCommand(a.Text, replies);
                    break;
                case ActionKind.Keybinding when InventoryOn:
                    InventoryKeybinding(a, replies);
                    break;
                case ActionKind.Hover when InventoryOn:
                    InventoryHover(a);
                    break;
                case ActionKind.HotbarPress when InventoryOn:
                    HotbarPress(a.HotbarCell); // DV689 (FakeServer.Hotbar.cs)
                    break;
            }
        }
    }

    // ---- clicks ----

    private void Click(Mob player, ActionMessage a, List<string> replies)
    {
        if (HudClick(player, a, replies))
            return;
        if (InventoryOn && InventoryClick(player, a, replies))
            return;
        Mob target = Find(a.Target);
        if (target == null || target.IsStatic || ReferenceEquals(target, player))
            return;
        if (_tick < player.NextClickTick)
            return; // tg's click cooldown (CLICK_CD_MELEE, 8 ds = 16 ticks)
        if (Math.Max(Math.Abs(target.Motion.TileX - player.Motion.TileX), Math.Abs(target.Motion.TileY - player.Motion.TileY)) > 1 || target.Motion.Z != player.Motion.Z)
            return; // tg's Adjacent
        if (player.Lying)
            return;
        player.NextClickTick = _tick + ShoveCooldownTicks;
        if (a.Button == ClickButton.Right)
        {
            if (Shove(player, target))
                replies.Add("You shove " + target.Name + ".");
        }
        else if (a.Button == ClickButton.Left)
        {
            replies.Add("You pat " + target.Name + " on the shoulder.");
        }
    }

    /// <summary>
    /// The shove (tg's <c>disarm</c>, DV17): the target moves one tile away from the shover (the dominant axis of the centres),
    /// slid over <see cref="ShoveGlideTicks"/>; a wall or a standing mob behind it is a knockdown instead of a move.
    /// </summary>
    public bool Shove(Mob shover, Mob target)
    {
        if (target.Lying)
            return false;
        double dx = (target.Motion.X - shover.Motion.X) / 256.0, dy = (target.Motion.Y - shover.Motion.Y) / 256.0;
        int stepX = 0, stepY = 0;
        if (Math.Abs(dx) >= Math.Abs(dy))
            stepX = dx >= 0 ? 1 : -1;
        else
            stepY = dy >= 0 ? 1 : -1;
        int toX = target.Motion.TileX + stepX, toY = target.Motion.TileY + stepY;
        bool blocked = _view.IsSolid(toX, toY, target.Motion.Z) || _mobs.Any(m => !m.IsStatic && !m.Lying && !ReferenceEquals(m, target) && m.Motion.TileX == toX && m.Motion.TileY == toY);
        if (blocked)
        {
            target.Knockdown(KnockdownWallTicks);
            return true;
        }
        MotionStep.Displace(ref target.Motion, toX, toY, target.Motion.Z, ShoveGlideTicks);
        return true;
    }

    /// <summary>The shove for the tests and the dev tools: the player's neighbour moves as if <paramref name="shoverId"/> had shoved <paramref name="targetId"/>.</summary>
    public bool Shove(long shoverId, long targetId)
    {
        Mob shover = Find(shoverId), target = Find(targetId);
        return shover != null && target != null && Shove(shover, target);
    }

    // ---- dev commands ----

    private void DevCommand(string text, List<string> replies)
    {
        if (!_options.DevCommands)
        {
            replies.Add("Dev commands are off on this server.");
            return;
        }
        string[] words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return;
        var c = CultureInfo.InvariantCulture;
        string verb = words[0].ToLowerInvariant();
        if (verb == "help")
        {
            replies.Add("Commands (the real server's syntax, WHO is me, your name or #1): damage WHO brute|burn|tox|oxy N [ZONE], wound WHO ZONE [TYPE SEVERITY], heal WHO, fullheal WHO, kill WHO, revive WHO, knockdown WHO S, stun WHO S, analyze WHO, speedscale F; the fake's own: alert me " + string.Join('|', AlertNames) + ", toolbelt (the hotbar's kit, with --fake-inventory).");
            return;
        }
        if (verb == "toolbelt")
        {
            GiveToolbelt(replies); // DV689: the hotbar's kit (FakeServer.Hotbar.cs)
            return;
        }
        if (verb == "speedscale")
        {
            if (words.Length == 2 && float.TryParse(words[1], NumberStyles.Float, c, out float scale) && scale > 0.05f && scale <= 4f)
            {
                _speedScale = scale;
                replies.Add("Speed scale is " + scale.ToString("0.##", c) + ".");
            }
            else
                replies.Add("Usage: speedscale FACTOR (above 0, at most 4).");
            return;
        }
        if (verb == "rotate")
        {
            // rotate #ID [cw|ccw|north|south|east|west]: the server's dev command (DV56), here without the map edit.
            string turn = words.Length >= 3 ? words[2].ToLowerInvariant() : "cw";
            if (words.Length < 2 || !long.TryParse(words[1].TrimStart('#'), NumberStyles.None, c, out long id) || !_mapById.TryGetValue(id, out MapEntity thing))
            {
                replies.Add("Usage: rotate #ID [cw|ccw|north|south|east|west] (a thing of the map).");
                return;
            }
            byte[] clockwise = { 1, 4, 2, 8 };
            int at = Array.IndexOf(clockwise, thing.Wire.Dir);
            byte dir = turn switch
            {
                "north" => 1, "south" => 2, "east" => 4, "west" => 8,
                "ccw" => clockwise[((at < 0 ? 0 : at) + 3) % 4],
                _ => clockwise[((at < 0 ? -1 : at) + 1) % 4],
            };
            thing.Wire = thing.Wire with { Dir = dir };
            _dirtyMapEntities.Add(thing);
            replies.Add("The thing #" + id + " faces " + (dir switch { 1 => "north", 2 => "south", 4 => "east", _ => "west" }) + ".");
            return;
        }
        if (verb == "door")
        {
            // door X Y open|close|deny, or door near open|close|deny (the nearest door to the player): the scripted world's stand-in for a bump (M3 P6).
            Mob player = _mobs[0];
            string action = words[^1];
            int doorX, doorY;
            if (words.Length == 3 && words[1] == "near")
            {
                FakeMapEntity nearest = Doors.OrderBy(d => (d.X - player.Motion.TileX) * (d.X - player.Motion.TileX) + (d.Y - player.Motion.TileY) * (d.Y - player.Motion.TileY)).FirstOrDefault();
                if (nearest.Id == 0)
                {
                    replies.Add("There is no door.");
                    return;
                }
                doorX = nearest.X;
                doorY = nearest.Y;
            }
            else if (words.Length != 4 || !int.TryParse(words[1], NumberStyles.None, c, out doorX) || !int.TryParse(words[2], NumberStyles.None, c, out doorY))
            {
                replies.Add("Usage: door X Y open|close|deny, or door near open|close|deny");
                return;
            }
            replies.Add(_isMap && AnimateDoor(doorX, doorY, 1, action) ? "The door at " + doorX + "," + doorY + " is " + (action == "close" ? "closing" : action + "ing") + "." : "No door there, or it cannot " + action + " now.");
            return;
        }
        if (verb == "spawn")
        {
            // A human that stands where it is put: by default one tile north of the player (a body to click on).
            if (words.Length < 2 || words[1] != "/mob/living/carbon/human")
            {
                replies.Add("Usage: spawn /mob/living/carbon/human [X Y [Z]]");
                return;
            }
            Mob player = _mobs[0];
            int x = player.Motion.TileX, y = player.Motion.TileY + 1;
            if (words.Length >= 4 && (!int.TryParse(words[2], NumberStyles.None, c, out x) || !int.TryParse(words[3], NumberStyles.None, c, out y)))
            {
                replies.Add("Usage: spawn /mob/living/carbon/human [X Y [Z]]");
                return;
            }
            if (_view.IsSolid(x, y, 1))
            {
                replies.Add("That turf is solid.");
                return;
            }
            var spawned = new Mob(SpawnedIdBase + _spawnCount++, "Human " + _spawnCount, null, this);
            spawned.Motion = MotionState.AtTile(x, y, 1, 32768);
            _mobs.Add(spawned);
            _mobs.Sort((a, b) => a.Id.CompareTo(b.Id));
            _spawnedThisTick.Add(spawned);
            replies.Add("Spawned " + spawned.Name + " (#" + spawned.Id + ") at " + x + "," + y + ".");
            return;
        }
        if (words.Length < 2)
        {
            replies.Add("Usage: " + verb + " WHO ...");
            return;
        }
        Mob target = Who(words[1]);
        if (target == null || target.IsStatic)
        {
            replies.Add("No such living mob: " + words[1]);
            return;
        }
        switch (verb)
        {
            case "damage":
                if (words.Length < 4 || words.Length > 5 || !int.TryParse(words[3], NumberStyles.None, c, out int amount) || amount < 1 || amount > 1000
                    || words[2] is not ("brute" or "burn" or "tox" or "oxy"))
                {
                    replies.Add("Usage: damage WHO brute|burn|tox|oxy N [ZONE] (N 1 to 1000; zones: head chest l_arm r_arm l_leg r_leg).");
                    break;
                }
                if (words[2] is "tox" or "oxy")
                {
                    target.Overall += amount;
                }
                else if (words.Length == 5)
                {
                    int zone = Array.IndexOf(HealthZones.Zones, words[4]);
                    if (zone < 0)
                    {
                        replies.Add("No such zone: " + words[4]);
                        break;
                    }
                    target.Damage[zone] = Math.Min(target.Damage[zone] + amount, ZoneMax[zone] * 2);
                }
                else
                {
                    // Spread over the parts (take_overall_damage), the chest and head taking the most.
                    for (int i = 0; i < 6; i++)
                        target.Damage[i] = Math.Min(target.Damage[i] + Math.Max(1, amount * ZoneMax[i] / ZoneMax.Sum()), ZoneMax[i] * 2);
                }
                replies.Add("Dealt " + amount + " " + words[2] + " damage to " + target.Name + ".");
                break;
            case "wound":
                // the real server's "wound WHO ZONE TYPE SEVERITY"; here only that the limb has a wound (tg's wound outline on the doll)
                int woundZone = words.Length >= 3 ? Array.IndexOf(HealthZones.Zones, words[2]) : -1;
                if (woundZone < 0 || words.Length > 5)
                {
                    replies.Add("Usage: wound WHO ZONE [TYPE SEVERITY] (zones: head chest l_arm r_arm l_leg r_leg).");
                    break;
                }
                target.Wounded[woundZone] = true;
                // tg's compound fracture (blunt critical, bones.dm: disabling = TRUE) paralyses an arm or a leg (can_be_disabled): doll key 7
                if (words.Length == 5 && words[3] == "blunt" && words[4] == "critical" && woundZone >= 2)
                    target.Disabled[woundZone] = true;
                replies.Add(target.Name + "'s " + HealthZones.ZoneName(words[2]) + " is wounded.");
                break;
            case "heal":
            case "fullheal":
                Array.Clear(target.Wounded);
                Array.Clear(target.Disabled);
                Array.Clear(target.Damage);
                target.Overall = 0;
                replies.Add("You feel fine.");
                break;
            case "kill":
                target.Dead = true;
                target.Knockdown(int.MaxValue / 2);
                replies.Add(target.Name + " dies.");
                break;
            case "revive":
                target.Dead = false;
                target.Revive();
                replies.Add(target.Name + " is back.");
                break;
            case "knockdown":
            case "stun":
                if (words.Length == 3 && int.TryParse(words[2], NumberStyles.None, c, out int seconds) && seconds >= 1 && seconds <= 60)
                {
                    target.Knockdown(seconds * 20);
                    replies.Add(target.Name + " falls down.");
                }
                else
                    replies.Add("Usage: " + verb + " WHO S (S 1 to 60).");
                break;
            case "analyze":
                replies.Add(Analyze(target));
                break;
            case "alert":
                int alert = words.Length == 3 && ReferenceEquals(target, _mobs[0]) ? Array.IndexOf(AlertNames, words[2]) : -1;
                if (alert < 0)
                {
                    replies.Add("Usage: alert me " + string.Join('|', AlertNames) + " (throws it, or clears it when it is up).");
                    break;
                }
                if (!_alertsUp.Remove(alert))
                    _alertsUp.Add(alert);
                break;
            default:
                replies.Add("Unknown command: " + words[0]);
                break;
        }
    }

    // WHO: me, the player's name, or a world id (#1 or 1). Only the player has a body worth healing here.
    private Mob Who(string who)
    {
        if (who == "me" || string.Equals(who, _name, StringComparison.OrdinalIgnoreCase))
            return _mobs[0];
        if (long.TryParse(who.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out long id))
            return Find(id);
        return null;
    }

    private string Analyze(Mob mob)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='info'><b>Analyzing results for ").Append(mob.Name).Append("</b><br>");
        sb.Append("Overall status: ").Append(mob.Dead ? "dead" : mob.Damage.Sum() == 0 ? "healthy" : "wounded").Append("<br>");
        for (int i = 0; i < 6; i++)
            sb.Append("&bull; ").Append(HealthZones.ZoneName(HealthZones.Zones[i])).Append(": ").Append(mob.Damage[i]).Append(" brute<br>");
        sb.Append("</div>");
        return sb.ToString();
    }

    // ---- the HUD (private screen entries) ----

    private static int Level(int damage, int max) => Math.Clamp((int)Math.Ceiling(damage / (double)max * 5), 0, 5);

    private void BuildHud(bool initial)
    {
        Mob p = _mobs[0];
        var keys = new string[6];
        for (int i = 0; i < 6; i++)
            keys[i] = HealthZones.Zones[i] + (p.Disabled[i] ? "7" : p.Dead ? "DEAD" : Level(p.Damage[i], ZoneMax[i]).ToString(CultureInfo.InvariantCulture));
        string signature = string.Join('|', keys) + "|" + string.Join(',', p.Wounded);
        if (signature != _dollSignature || initial)
        {
            _dollSignature = signature;
            var root = new AppearanceEntry { Icon = PrivateResource(ScreenGenIcon), IconState = PrivateString("healthdoll"), Layer = 20f };
            for (int i = 0; i < 6; i++)
                root.Overlays.Add(p.Wounded[i] ? WoundedLimb(keys[i]) : Leaf(ScreenGenIcon, keys[i], 255, 20.1f));
            _screen[DollId] = new ScreenEntry(DollId, PrivateAppearance(root), "EAST-1:28,CENTER-2:17");
        }
        double frac = p.Dead ? 1.0 : (Enumerable.Range(0, 6).Sum(i => (double)p.Damage[i]) + p.Overall) / ZoneMax.Sum();
        int health = p.Dead ? 7 : Math.Min(6, (int)Math.Ceiling(frac * 6));
        _screen[HealthId] = new ScreenEntry(HealthId, Leaf(ScreenGenIcon, "health" + health.ToString(CultureInfo.InvariantCulture), 255, 20f), "EAST-1:28,CENTER-1:19");
        int severity = p.Dead ? 0 : Math.Min(6, (int)Math.Ceiling(frac * 6));
        if (severity > 0)
            _screen[FullscreenId] = new ScreenEntry(FullscreenId, Leaf(ScreenFullIcon, "brutedamageoverlay", (byte)(40 + severity * 30), 18f), "CENTER-7,CENTER-7");
        else
            _screen.Remove(FullscreenId);
        _screen[IntentId] = new ScreenEntry(IntentId, Leaf(ScreenGenIcon, p.Run ? "running" : "walking", 255, 20f), "EAST-2:26,SOUTH:5") { Group = HudGroup.Static }; // tg: HUD_GROUP_STATIC (hud/human.dm)
        // tg's human HUD (hud/human.dm): the zone selector (HUD_GROUP_STATIC, ui_zonesel) with the selected zone as its overlay (the client shows it
        // in cursor mode only), and the combat toggle (HUD_GROUP_INFO, ui_acti) whose icon state follows combat mode
        if (p.ZoneSelected != _zoneShown || initial)
        {
            _zoneShown = p.ZoneSelected;
            var selector = new AppearanceEntry { Icon = PrivateResource(HudIcon), IconState = PrivateString("zone_sel"), Layer = 20f };
            selector.Overlays.Add(Leaf(ScreenGenIcon, p.ZoneSelected, 255, 20.1f));
            _screen[ZoneSelId] = new ScreenEntry(ZoneSelId, PrivateAppearance(selector), "EAST-1:28,SOUTH:5") { Group = HudGroup.Static };
        }
        _screen[CombatId] = new ScreenEntry(CombatId, Leaf(HudIcon, p.Combat ? "combat" : "combat_off", 255, 20f), "EAST-3:24,SOUTH:5") { Group = HudGroup.Info };
        // tg's alerts (alert.dm reorganize_alerts): in no HUD group, at get_ui_alert_placement(i) in the order thrown, no place past the fifth
        for (int i = 0; i < AlertNames.Length; i++)
        {
            int at = _alertsUp.IndexOf(i) + 1;
            if (at == 0)
            {
                _screen.Remove(AlertIdBase + i);
                continue;
            }
            string loc = at <= 5 ? "EAST-1:28,CENTER+" + (6 - at).ToString(CultureInfo.InvariantCulture) + ":" + (29 - 2 * at).ToString(CultureInfo.InvariantCulture) : "";
            _screen[AlertIdBase + i] = new ScreenEntry(AlertIdBase + i, AlertLook(i), loc);
        }
    }

    private TableRef AlertLook(int alert)
    {
        if (_alertLooks.TryGetValue(alert, out TableRef look))
            return look;
        string name = AlertNames[alert];
        if (name == "weightless")
        {
            var styled = new AppearanceEntry { Icon = PrivateResource(HudIcon), IconState = PrivateString("template"), Layer = 20f };
            styled.Overlays.Add(Leaf(AlertIcon, name, 255, 20.1f));
            look = PrivateAppearance(styled);
        }
        else
            look = Leaf(AlertIcon, name == "cold" ? "cold2" : name, 255, 20f); // tg's severity is the state's number
        _alertLooks[alert] = look;
        return look;
    }

    // A wounded limb (tg's healthdoll/human, screen_objects.dm:1031): the "wound_outline" filter, a #FF0033 outline of size 1.2; tg pulses its
    // alpha from 0 to 200 and back, the fake shows it steady at 200
    private TableRef WoundedLimb(string state)
    {
        var limb = new AppearanceEntry { Icon = PrivateResource(ScreenGenIcon), IconState = PrivateString(state), Layer = 20.1f };
        limb.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Outline, Size = 1.2f, Rgba = 0xFF0033C8 } };
        return PrivateAppearance(limb);
    }

    private string _zoneShown;

    // tg's zone_sel/get_zone_at: the zone under a pixel of the selector's icon, or null
    private static string ZoneSelAt(int x, int y) => y switch
    {
        >= 1 and <= 9 => x switch { >= 10 and <= 15 => "r_leg", >= 17 and <= 22 => "l_leg", _ => null },
        >= 10 and <= 13 => x switch { >= 8 and <= 11 => "r_arm", >= 12 and <= 20 => "groin", >= 21 and <= 24 => "l_arm", _ => null },
        >= 14 and <= 22 => x switch { >= 8 and <= 11 => "r_arm", >= 12 and <= 20 => "chest", >= 21 and <= 24 => "l_arm", _ => null },
        >= 23 and <= 30 when x is >= 12 and <= 20 => y switch
        {
            >= 23 and <= 24 when x is >= 15 and <= 17 => "mouth",
            26 when x is >= 14 and <= 18 => "eyes",
            >= 25 and <= 27 when x is >= 15 and <= 17 => "eyes",
            _ => "head",
        },
        _ => null,
    };

    // A click on the zone selector selects the zone under its pixel; one on the combat toggle switches combat mode (tg's Click procs).
    private bool HudClick(Mob player, ActionMessage a, List<string> replies)
    {
        if (a.EffectiveTargetKind != TargetKind.Screen)
            return false;
        if (a.Target == AlertIdBase + Array.IndexOf(AlertNames, "buckled") && _alertsUp.Remove(Array.IndexOf(AlertNames, "buckled")))
        {
            replies.Add("You unbuckle yourself.");
            return true;
        }
        if (a.Target == ZoneSelId)
        {
            if (ZoneSelAt(a.IconX, a.IconY) is string zone)
                player.ZoneSelected = zone;
            return true;
        }
        if (a.Target != CombatId)
            return false;
        player.Combat = !player.Combat;
        replies.Add(player.Combat ? "You are now in combat mode." : "You are no longer in combat mode.");
        return true;
    }

    private TableRef Leaf(string icon, string state, byte alpha, float layer)
    {
        string key = icon + "#" + state + "#" + alpha.ToString(CultureInfo.InvariantCulture) + "#" + layer.ToString(CultureInfo.InvariantCulture);
        if (_privLeaf.TryGetValue(key, out TableRef existing))
            return existing;
        var entry = new AppearanceEntry { Icon = PrivateResource(icon), IconState = PrivateString(state), Alpha = alpha, Layer = layer };
        TableRef made = PrivateAppearance(entry);
        _privLeaf[key] = made;
        return made;
    }

    private TableRef PrivateResource(string path)
    {
        int index = _privResources.IndexOf(path);
        if (index < 0)
        {
            _privResources.Add(path);
            index = _privResources.Count - 1;
        }
        return new TableRef((uint)(index + 1), true);
    }

    private TableRef PrivateString(string text)
    {
        int index = _privStrings.IndexOf(text);
        if (index < 0)
        {
            _privStrings.Add(text);
            index = _privStrings.Count - 1;
        }
        return new TableRef((uint)(index + 1), true);
    }

    private TableRef PrivateAppearance(AppearanceEntry entry)
    {
        _privAppearances.Add(entry);
        return new TableRef((uint)_privAppearances.Count, true);
    }

    private OwnBlock BuildOwnBlock(bool snapshot)
    {
        Mob p = _mobs[0];
        var block = new OwnBlock
        {
            HasMotion = true,
            Motion = p.Motion,
            Params = ParamsFor(p),
            InputBufferDepth = (byte)_jitter.Count,
            ActionsDone = _actionsReceived - (uint)_pendingActions.Count,
        };
        if (snapshot)
        {
            _sentPrivResources = _sentPrivStrings = _sentPrivAppearances = 0;
            _screenSent.Clear();
        }
        block.FirstNewPrivateResource = (uint)_sentPrivResources + 1;
        for (int i = _sentPrivResources; i < _privResources.Count; i++)
            block.NewPrivateResources.Add(_privResources[i]);
        block.FirstNewPrivateString = (uint)_sentPrivStrings + 1;
        for (int i = _sentPrivStrings; i < _privStrings.Count; i++)
            block.NewPrivateStrings.Add(_privStrings[i]);
        block.FirstNewPrivateAppearance = (uint)_sentPrivAppearances + 1;
        for (int i = _sentPrivAppearances; i < _privAppearances.Count; i++)
            block.NewPrivateAppearances.Add(_privAppearances[i]);
        _sentPrivResources = _privResources.Count;
        _sentPrivStrings = _privStrings.Count;
        _sentPrivAppearances = _privAppearances.Count;

        foreach (ScreenEntry entry in _screen.Values.OrderBy(e => e.Id))
        {
            if (!_screenSent.TryGetValue(entry.Id, out ScreenEntry sent))
                block.ScreenAdded.Add(entry);
            else if (sent != entry)
                block.ScreenChanged.Add(entry);
        }
        foreach (long id in _screenSent.Keys.OrderBy(k => k))
        {
            if (!_screen.ContainsKey(id))
                block.ScreenRemoved.Add(id);
        }
        _screenSent.Clear();
        foreach (ScreenEntry entry in _screen.Values)
            _screenSent[entry.Id] = entry;
        if (InventoryOn)
            InventoryOwnImages(block, snapshot);
        return block;
    }

    // ---- the delta ----

    private Delta BuildDelta()
    {
        var delta = new Delta { Tick = _tick, LastInputSeq = _appliedSeq, OwnEntity = OwnId };
        delta.FirstNewType = _types.Count;
        foreach (long id in _despawned)
            delta.Despawned.Add(id);
        _despawned.Clear();
        foreach (Mob m in _spawnedThisTick)
        {
            m.Sent = m.Wire();
            delta.Spawned.Add(m.Sent);
        }
        _spawnedThisTick.Clear();
        foreach (Mob m in _mobs)
        {
            if (delta.Spawned.Any(s => s.Id == m.Id))
                continue;
            EntityState now = m.Wire();
            if (!now.Equals(m.Sent))
            {
                delta.Changed.Add(Diff(m.Sent, now));
                m.Sent = now;
            }
        }
        foreach (MapEntity e in _dirtyMapEntities.OrderBy(m => m.Wire.Id))
        {
            if (!e.Wire.Equals(e.Sent))
            {
                delta.Changed.Add(Diff(e.Sent, e.Wire));
                e.Sent = e.Wire;
            }
        }
        _dirtyMapEntities.Clear();
        if (InventoryOn)
            InventoryDelta(delta); // before the tables: a thing's new look may add an appearance
        foreach (var c in _solidChanges)
            delta.SolidChanged.Add(new CellSolidChange(c.X, c.Y, c.Z, c.Solid));
        foreach (var c in _flagChanges)
            delta.CellFlagsChanged.Add(new CellFlagsChange(c.X, c.Y, c.Z, c.Flags));
        _solidChanges.Clear();
        _flagChanges.Clear();
        delta.FirstNewResource = (uint)_sentResources + 1;
        for (int i = _sentResources; i < _pubResources.Count; i++)
            delta.NewResources.Add(_pubResources[i]);
        delta.FirstNewString = (uint)_sentStrings + 1;
        for (int i = _sentStrings; i < _pubStrings.Count; i++)
            delta.NewStrings.Add(_pubStrings[i]);
        delta.FirstNewAppearance = (uint)_sentAppearances + 1;
        for (int i = _sentAppearances; i < _pubAppearances.Count; i++)
            delta.NewAppearances.Add(_pubAppearances[i]);
        _sentResources = _pubResources.Count;
        _sentStrings = _pubStrings.Count;
        _sentAppearances = _pubAppearances.Count;
        delta.Own = BuildOwnBlock(snapshot: false);
        return delta;
    }

    private static EntityChange Diff(EntityState a, EntityState b)
    {
        var c = new EntityChange { Id = b.Id };
        if (a.X != b.X) { c.Mask |= NetField.X; c.X = b.X; }
        if (a.Y != b.Y) { c.Mask |= NetField.Y; c.Y = b.Y; }
        if (a.Z != b.Z) { c.Mask |= NetField.Z; c.Z = b.Z; }
        if (a.Dir != b.Dir) { c.Mask |= NetField.Dir; c.Dir = b.Dir; }
        if (a.LastMoveTick != b.LastMoveTick) { c.Mask |= NetField.LastMoveTick; c.LastMoveTick = b.LastMoveTick; }
        if (a.PosX != b.PosX || a.PosY != b.PosY) { c.Mask |= NetField.Pos; c.PosX = b.PosX; c.PosY = b.PosY; }
        if (a.Yaw != b.Yaw) { c.Mask |= NetField.Yaw; c.Yaw = b.Yaw; }
        if (a.Posture != b.Posture) { c.Mask |= NetField.Posture; c.Posture = b.Posture; }
        if (a.Appearance != b.Appearance) { c.Mask |= NetField.Appearance; c.Appearance = b.Appearance; }
        if (a.Flags != b.Flags) { c.Mask |= NetField.Flags; c.Flags = b.Flags; }
        return c;
    }

    // ---- the world ----

    private Mob Find(long id) => _mobs.Find(m => m.Id == id);

    private void BuildTables()
    {
        _pubResources.Add("icons/mob/human/human.dmi");
        _pubResources.Add("icons/obj/storage/toolbox.dmi");
        _pubResources.Add("icons/turf/floors.dmi");
        _pubResources.Add("icons/turf/walls/wall.dmi");
        _pubStrings.AddRange(new[] { "human", "toolbox", "floor", "wall" });
        TableRef Pub(int index) => TableRef.Public((uint)index);
        // Appearances 1 and 2 stay where they were (tests and the replica's first entries count on them); the real humans come after the room's pictures.
        AddPublic(new AppearanceEntry { Icon = Pub(1), IconState = Pub(1), Dir = 0, Layer = 4f });
        AddPublic(new AppearanceEntry { Icon = Pub(1), IconState = Pub(1), Layer = 3.8f, Transform = new[] { 0f, 1f, 0f, -1f, 0f, 0f } });
        _itemApp = AddPublic(new AppearanceEntry { Icon = Pub(2), IconState = Pub(2), Layer = 3f });
        _floorApp = AddPublic(new AppearanceEntry { Icon = Pub(3), IconState = Pub(3), Layer = 2f });
        _wallApp = AddPublic(new AppearanceEntry { Icon = Pub(4), IconState = Pub(4), Layer = 2f });
        var builder = new AppearanceBuilder(_pubResources, _pubStrings, _pubAppearances);
        _humanApps = new TableRef[Skins.Length];
        _humanLyingApps = new TableRef[Skins.Length];
        for (int i = 0; i < Skins.Length; i++)
        {
            _humanApps[i] = builder.Human(LookOf(i), 4f);
            _humanLyingApps[i] = builder.Human(LookOf(i, 90), 3.8f);
        }
    }

    private TableRef AddPublic(AppearanceEntry entry)
    {
        _pubAppearances.Add(entry);
        return TableRef.Public((uint)_pubAppearances.Count);
    }

    private void Populate()
    {
        int n = _options.Size;
        var player = new Mob(OwnId, _name, null, this);
        player.Motion = MotionState.AtTile(n / 2, 4, 1, 0);
        _mobs.Add(player);
        for (int i = 0; i < _options.Bots; i++)
        {
            var bot = new Mob(2 + i, "Bot " + (i + 1), _options.StillBots ? null : new AutoWalker(_options.Seed * 7919 + i), this);
            PlaceFree(bot);
            _mobs.Add(bot);
        }
        string[] items = { "toolbox", "crate", "medkit", "fire extinguisher", "bucket" };
        for (int i = 0; i < items.Length; i++)
        {
            var item = new Mob(ItemIdBase + i, items[i], null, this) { IsStatic = true };
            PlaceFree(item);
            _mobs.Add(item);
        }
    }

    private void PlaceFree(Mob m)
    {
        int n = _options.Size;
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            int x = _random.Next(2, n);
            int y = _random.Next(2, n);
            bool taken = _mobs.Any(o => o.Motion.TileX == x && o.Motion.TileY == y && o.Motion.Z == 1 && o.Id != m.Id);
            if (!_closed[x, y] && !taken && !(Math.Abs(x - n / 2) < 2 && Math.Abs(y - 4) < 2))
            {
                m.Motion = MotionState.AtTile(x, y, 1, (ushort)_random.Next(0, 65536));
                return;
            }
        }
        throw new InvalidOperationException("no free tile in the fake room");
    }

    private MapGrid BuildMap()
    {
        int n = _options.Size;
        for (int x = 1; x <= n; x++)
        {
            for (int y = 1; y <= n; y++)
                _closed[x, y] = x == 1 || y == 1 || x == n || y == n;
        }
        // A partition with a doorway, and four pillars, so the room has corners to look around.
        int wallY = n / 2 + 1;
        for (int x = 3; x <= n - 2; x++)
        {
            if (Math.Abs(x - n / 2) > 2)
                _closed[x, wallY] = true;
        }
        foreach (int px in new[] { n / 4, 3 * n / 4 })
        {
            _closed[px, n / 4 + 1] = true;
            _closed[px, 3 * n / 4] = true;
        }

        var map = new MapGrid { Tick = 0, Width = n, Height = n, Levels = 1 };
        map.TurfTypes.Add("/turf/open/indestructible");
        map.TurfTypes.Add("/turf/closed/indestructible");
        map.TurfTypes.Add("/turf/closed/wall");
        var solid = new byte[(n * n + 7) / 8];
        int runType = -1, runLength = 0, appType = -1, appLength = 0;
        int cell = 0;
        for (int y = 1; y <= n; y++)
        {
            for (int x = 1; x <= n; x++, cell++)
            {
                map.Ceilings.Add(new CeilingChange(x, y, 1, Anomaly.Core.Motion.CeilingState.Of(Anomaly.Core.Motion.CeilingState.Plating)));
                int type = !_closed[x, y] ? 0 : (x == 1 || y == 1 || x == n || y == n ? 1 : 2);
                if (type == runType)
                    runLength++;
                else
                {
                    if (runLength > 0)
                        map.Runs.Add(new TurfRun(runLength, runType));
                    runType = type;
                    runLength = 1;
                }
                int app = (int)(_closed[x, y] ? _wallApp.Index : _floorApp.Index);
                if (app == appType)
                    appLength++;
                else
                {
                    if (appLength > 0)
                        map.AppearanceRuns.Add(new TurfRun(appLength, appType));
                    appType = app;
                    appLength = 1;
                }
                if (_closed[x, y])
                    solid[cell / 8] |= (byte)(1 << (cell % 8));
            }
        }
        map.Runs.Add(new TurfRun(runLength, runType));
        map.AppearanceRuns.Add(new TurfRun(appLength, appType));
        map.SolidBits = solid;
        return map;
    }


    // ---- the converted-map mode (M3 P6, FakeFeed version 3) ----

    /// <summary>The id the map's first entity has on the wire: <see cref="FakeMapEntity.Id"/> (from 1) plus this, clear of the mobs' ids.</summary>
    public const long MapEntityIdBase = 1_000_000;

    /// <summary>The wire id of a <see cref="FakeMapEntity"/>.</summary>
    public static long WireId(FakeMapEntity entity) => MapEntityIdBase + entity.Id;

    private int CellIndex(int x, int y, int z) => (x - 1) + (y - 1) * _width + (z - 1) * _width * _height;

    private bool SolidAt(int x, int y, int z)
    {
        if (x < 1 || y < 1 || z < 1 || x > _width || y > _height || z > _options.Map.Levels)
            return true;
        int i = CellIndex(x, y, z);
        return (_solidBits[i >> 3] & (1 << (i & 7))) != 0;
    }

    private MapGrid BuildMapFromFile()
    {
        FakeMap file = _options.Map;
        _tables.Seed(_pubResources, _pubStrings, _pubAppearances);
        _binding = file.Bind(_tables);
        SyncTables();
        MapGrid grid = _binding.Grid;
        _solidBits = (byte[])grid.SolidBits.Clone();
        _cellFlags = grid.ExpandFlags();
        // DV56, as the server does: the things face away from their walls unless their map says the way they face (FakeMapWire).
        foreach ((FakeMapEntity e, EntityState wire) in FakeMapWire.Entities(file, _binding, _types))
        {
            var entity = new MapEntity { Source = e, Wire = wire, Sent = wire, TypeIndex = wire.Type, Appearance = wire.Appearance, DoorOpen = !e.Density };
            _mapEntities.Add(entity);
            _mapById[wire.Id] = entity;
        }
        return grid;
    }

    // Copies the entries the interner added after the base tables into the lists the Snapshot and the Delta send.
    private void SyncTables()
    {
        for (int i = _pubResources.Count; i < _tables.Resources.Count; i++)
            _pubResources.Add(_tables.Resources[i]);
        for (int i = _pubStrings.Count; i < _tables.Strings.Count; i++)
            _pubStrings.Add(_tables.Strings[i]);
        for (int i = _pubAppearances.Count; i < _tables.Appearances.Count; i++)
            _pubAppearances.Add(_tables.Appearances[i]);
    }

    private void PopulateOnMap()
    {
        IReadOnlyList<(int X, int Y, int Z)> spawns = _options.Map.SpawnCells;
        (int X, int Y, int Z) first = spawns.Count > 0 ? spawns[0] : FindOpenCell();
        var player = new Mob(OwnId, _name, null, this);
        player.Motion = MotionState.AtTile(first.X, first.Y, first.Z, 0);
        _mobs.Add(player);
        for (int i = 0; i < _options.Bots; i++)
        {
            var bot = new Mob(2 + i, "Bot " + (i + 1), _options.StillBots ? null : new AutoWalker(_options.Seed * 7919 + i), this);
            (int X, int Y, int Z) at = spawns.Count > 0 ? spawns[_random.Next(spawns.Count)] : FindOpenCell();
            bot.Motion = MotionState.AtTile(at.X, at.Y, at.Z, (ushort)_random.Next(0, 65536));
            _mobs.Add(bot);
        }
    }

    private (int X, int Y, int Z) FindOpenCell()
    {
        for (int y = 1; y <= _height; y++)
        {
            for (int x = 1; x <= _width; x++)
            {
                if (!SolidAt(x, y, 1) && !_map.TurfTypes[_map.ExpandCells()[CellIndex(x, y, 1)]].StartsWith("/turf/open/space", StringComparison.Ordinal))
                    return (x, y, 1);
            }
        }
        return (1, 1, 1);
    }

    /// <summary>The map's door entities (airlocks, pod doors and shutters) the <c>door</c> command can animate.</summary>
    public IEnumerable<FakeMapEntity> Doors
        => _mapEntities.Where(m => PropKinds.Classify(m.Source.TypePath) is RenderKind.Airlock or RenderKind.PodDoor).Select(m => m.Source);

    /// <summary>
    /// Starts a door's animation as tg's door procs do (door.dm: <c>open</c>, <c>close</c>, <c>do_animate</c>), at 20 ticks per second:
    /// opening is the state <c>opening</c> for 0.6 s with the cell turning passable at 0.5 s, then <c>open</c>; closing is <c>closing</c>,
    /// the cell turning solid at 0.2 s, then <c>closed</c>; denying is <c>deny</c> for 0.3 s (a windoor: the states of its own icon, <c>leftopening</c>,
    /// <c>leftopen</c> and so on, 0.7 s until its face is open and 0.9 s in all, windowdoor.dm). The airlock or pod door of the cell, else its windoor.
    /// Returns false when there is no door at the cell or it is busy or already in that state. The console's command: such a door stays as it is
    /// (it has no auto-close); a bump opens an airlock or windoor that closes again (<see cref="BumpOpen"/>).
    /// </summary>
    public bool AnimateDoor(int x, int y, int z, string action)
    {
        // A closet (closets.dm): its door overlay, "[state]_open" or "[state]_door", as closet_update_overlays sets it once its door has moved.
        MapEntity closet = _mapEntities.FirstOrDefault(m => m.Source.X == x && m.Source.Y == y && m.Source.Z == z
            && PropKinds.Is(m.Source.TypePath, "/obj/structure/closet") && m.Source.Icon == "icons/obj/storage/closet.dmi");
        if (closet != null && action is "open" or "close")
        {
            FakeMapEntity c = closet.Source;
            closet.Appearance = _tables.AppearanceWithOverlay(c.Icon, c.IconState, c.IconState + (action == "open" ? "_open" : "_door"), c.Layer, c.PixelX, c.PixelY);
            SyncTables();
            closet.Wire = closet.Wire with { Appearance = closet.Appearance };
            _dirtyMapEntities.Add(closet);
            return true;
        }
        // A crate (crates.dm update_icon_state): its state "[base]open" while it is open.
        MapEntity crate = _mapEntities.FirstOrDefault(m => m.Source.X == x && m.Source.Y == y && m.Source.Z == z
            && PropKinds.Is(m.Source.TypePath, "/obj/structure/closet/crate") && m.Source.Icon == "icons/obj/storage/crates.dmi");
        if (crate != null && action is "open" or "close")
        {
            FakeMapEntity c = crate.Source;
            string closed = c.IconState.EndsWith("open", StringComparison.Ordinal) ? c.IconState[..^"open".Length] : c.IconState;
            crate.Appearance = _tables.Appearance(c.Icon, action == "open" ? closed + "open" : closed, c.Layer, c.PixelX, c.PixelY);
            SyncTables();
            crate.Wire = crate.Wire with { Appearance = crate.Appearance };
            _dirtyMapEntities.Add(crate);
            return true;
        }
        // A firelock alone on its cell (firedoor.dm update_icon_state): its state "door_open" or "door_closed" (the scripted world plays no flick).
        MapEntity firelock = _mapEntities.FirstOrDefault(m => m.Source.X == x && m.Source.Y == y && m.Source.Z == z
            && PropKinds.Classify(m.Source.TypePath) == RenderKind.Firelock);
        if (firelock != null && action is "open" or "close" && !_mapEntities.Any(m => m.Source.X == x && m.Source.Y == y && m.Source.Z == z
            && PropKinds.Classify(m.Source.TypePath) is RenderKind.Airlock or RenderKind.PodDoor))
        {
            FakeMapEntity f = firelock.Source;
            firelock.Appearance = _tables.Appearance(f.Icon, action == "open" ? "door_open" : "door_closed", f.Layer, f.PixelX, f.PixelY);
            SyncTables();
            firelock.Wire = firelock.Wire with { Appearance = firelock.Appearance };
            _dirtyMapEntities.Add(firelock);
            return true;
        }
        MapEntity door = _mapEntities.FirstOrDefault(m => m.Source.X == x && m.Source.Y == y && m.Source.Z == z
            && PropKinds.Classify(m.Source.TypePath) is RenderKind.Airlock or RenderKind.PodDoor)
            ?? _mapEntities.FirstOrDefault(m => m.Source.X == x && m.Source.Y == y && m.Source.Z == z && IsWindoor(m));
        return door != null && StartDoor(door, action);
    }

    private static bool IsWindoor(MapEntity door) => PropKinds.Classify(door.Source.TypePath) == RenderKind.Windoor;

    private bool StartDoor(MapEntity door, string action)
    {
        if (_doorRuns.Any(r => r.Door == door))
            return false;
        DoorPhase phase;
        switch (action)
        {
            case "open" when IsDoorClosed(door): phase = DoorPhase.Opening; break;
            case "close" when !IsDoorClosed(door): phase = DoorPhase.Closing; break;
            case "deny" when IsDoorClosed(door): phase = DoorPhase.Deny; break;
            default: return false;
        }
        SetDoorState(door, phase switch { DoorPhase.Opening => "opening", DoorPhase.Closing => "closing", _ => "deny" });
        _doorRuns.Add(new DoorRun { Door = door, Phase = phase, StartTick = _tick });
        return true;
    }

    // An airlock or pod door is shut while its cell is solid; a windoor (its cell never is) while it says so.
    private bool IsDoorClosed(MapEntity door) => IsWindoor(door) ? !door.DoorOpen : SolidAt(door.Source.X, door.Source.Y, door.Source.Z);

    // tg's icon states of a door: an airlock or pod door's are closed, opening, open, closing, deny; a windoor's are those after its base state (left, rightsecure).
    private static string DoorStateName(MapEntity door, string state)
    {
        if (!IsWindoor(door))
            return state;
        string basis = door.Source.IconState ?? "left";
        return state == "closed" ? basis : state == "open" ? basis + "open" : basis + state;
    }

    private void SetDoorState(MapEntity door, string state)
    {
        FakeMapEntity e = door.Source;
        door.Appearance = _tables.Appearance(e.Icon, DoorStateName(door, state), e.Layer, e.PixelX, e.PixelY);
        SyncTables();
        door.Wire = door.Wire with { Appearance = door.Appearance };
        _dirtyMapEntities.Add(door);
    }

    private readonly HashSet<MapEntity> _dirtyMapEntities = new();

    // tg's animation_segment_delay in ticks: (the door turns passable, the opening ends, it turns impassable, the closing ends).
    private static (int OpenPassable, int OpenDone, int ClosePassable, int CloseDone) TimesOf(MapEntity door)
        => IsWindoor(door) ? (14, 18, 4, 18) : (10, 12, 4, 12);

    // The door lets things through or stops them: an airlock or pod door by its cell, a windoor by its face.
    private void SetDoorBlocking(MapEntity door, bool blocking)
    {
        FakeMapEntity e = door.Source;
        if (IsWindoor(door))
        {
            SetFace(e.X, e.Y, e.Z, e.Dir, blocking);
            door.DoorOpen = !blocking;
        }
        else
            SetCell(e.X, e.Y, e.Z, solid: blocking, opaque: blocking && e.Opacity);
    }

    private void StepDoors()
    {
        for (int i = 0; i < _doorRuns.Count; i++)
        {
            DoorRun run = _doorRuns[i];
            long elapsed = _tick - run.StartTick;
            (int openPassable, int openDone, int closePassable, int closeDone) = TimesOf(run.Door);
            bool done = false;
            switch (run.Phase)
            {
                case DoorPhase.Opening:
                    if (elapsed >= openPassable && !run.FlippedCell)
                    {
                        SetDoorBlocking(run.Door, false);
                        run.FlippedCell = true;
                    }
                    if (elapsed >= openDone)
                    {
                        SetDoorState(run.Door, "open");
                        done = true;
                    }
                    break;
                case DoorPhase.Closing:
                    if (elapsed >= closePassable && !run.FlippedCell)
                    {
                        SetDoorBlocking(run.Door, true);
                        run.FlippedCell = true;
                    }
                    if (elapsed >= closeDone)
                    {
                        SetDoorState(run.Door, "closed");
                        done = true;
                    }
                    break;
                default:
                    if (elapsed >= 6)
                    {
                        SetDoorState(run.Door, "closed");
                        done = true;
                    }
                    break;
            }
            if (done)
                _doorRuns.RemoveAt(i--);
        }
        StepAutoClose();
    }

    // ---- bump-open doors (a stand-in until P3's doors run on the real server) ----

    /// <summary>tg's <c>DOOR_CLOSE_WAIT</c> (door.dm:2, airlock.dm:71): 60 deciseconds, the wait before an airlock tries to close again when something is in its way.</summary>
    public const int DoorCloseWaitTicks = 120;

    /// <summary>An airlock's auto-close wait: <c>autoclose_in(normalspeed ? 8 SECONDS : 1.5 SECONDS)</c> from the start of its opening (airlock.dm:1343; <c>normalspeed</c> is TRUE).</summary>
    public const int AirlockAutoCloseTicks = 160;

    /// <summary>A windoor's wait after its opening: <c>open_and_close</c> sleeps 8 seconds, or 5 when the door has an access requirement (windowdoor.dm:146-153).</summary>
    public const int WindoorAutoCloseTicks = 160, SecureWindoorAutoCloseTicks = 100;

    // An airlock of tg's airlock family or a windoor opens when something bumps it (door.dm Bumped, windowdoor.dm Bumped): the pod doors and shutters
    // (can_open_with_hands FALSE), the morgue doors and the incinerator airlocks (autoclose FALSE) keep their state. There is no access check here.
    private static bool BumpsOpen(MapEntity door)
    {
        string path = door.Source.TypePath;
        if (IsWindoor(door))
            return true;
        return PropKinds.Classify(path) == RenderKind.Airlock && PropKinds.Is(path, "/obj/machinery/door/airlock") && !path.Contains("/incinerator", StringComparison.Ordinal);
    }

    private IEnumerable<MapEntity> BumpDoorsAt(int x, int y, int z)
    {
        if (_bumpDoors == null)
        {
            _bumpDoors = new Dictionary<(int, int, int), List<MapEntity>>();
            foreach (MapEntity m in _mapEntities.Where(BumpsOpen))
            {
                var key = (m.Source.X, m.Source.Y, m.Source.Z);
                if (!_bumpDoors.TryGetValue(key, out List<MapEntity> list))
                    _bumpDoors[key] = list = new List<MapEntity>();
                list.Add(m);
            }
        }
        return _bumpDoors.TryGetValue((x, y, z), out List<MapEntity> found) ? found : Enumerable.Empty<MapEntity>();
    }

    private static byte Opposite(byte dir) => dir switch { 1 => (byte)2, 2 => (byte)1, 4 => (byte)8, _ => (byte)4 };

    // The own mob pressed into a blocked face of its tile and the press timer ran out (the controller's Pressing event: tg's Move into that tile, so Bump
    // fires): the airlock in the tile ahead, a windoor ahead facing back or one of its own tile facing that way opens.
    private void BumpDoors(Mob player)
    {
        for (int i = 0; i < _events.Count; i++)
        {
            MotionEvent e = _events[i];
            if (e.Kind != MotionEventKind.Pressing)
                continue;
            int z = player.Motion.Z;
            foreach (MapEntity door in BumpDoorsAt(e.ToX, e.ToY, z).ToList())
            {
                if (!IsWindoor(door) || door.Source.Dir == Opposite(e.Dir))
                    BumpOpen(door);
            }
            foreach (MapEntity door in BumpDoorsAt(e.FromX, e.FromY, z).ToList())
            {
                if (IsWindoor(door) && door.Source.Dir == e.Dir)
                    BumpOpen(door);
            }
        }
    }

    // door.dm open() and airlock.dm open(): an airlock schedules its auto-close at the start of the opening; windowdoor.dm open_and_close() closes the
    // windoor after its opening (0.9 s) and the wait.
    private bool BumpOpen(MapEntity door)
    {
        if (!StartDoor(door, "open"))
            return false;
        int wait = IsWindoor(door)
            ? TimesOf(door).OpenDone + (door.Source.IconState != null && door.Source.IconState.Contains("secure", StringComparison.Ordinal) ? SecureWindoorAutoCloseTicks : WindoorAutoCloseTicks)
            : AirlockAutoCloseTicks;
        _autoClose.RemoveAll(a => a.Door == door); // TIMER_OVERRIDE
        _autoClose.Add(new AutoClose { Door = door, AtTick = _tick + wait });
        return true;
    }

    // tg's autoclose() and close(): a door that is open and idle closes, unless something dense is in its doorway (airlock.dm:1417-1430,
    // door.dm:624-629): then it waits DOOR_CLOSE_WAIT and tries again. (A windoor in tg closes whatever is there; this one waits too, so that a mob
    // standing in a doorway is never shut in.)
    private void StepAutoClose()
    {
        for (int i = 0; i < _autoClose.Count; i++)
        {
            AutoClose pending = _autoClose[i];
            if (pending.AtTick > _tick)
                continue;
            _autoClose.RemoveAt(i--);
            MapEntity door = pending.Door;
            if (IsDoorClosed(door) || _doorRuns.Any(r => r.Door == door))
                continue;
            FakeMapEntity e = door.Source;
            if (_mobs.Any(m => !m.IsStatic && m.Motion.TileX == e.X && m.Motion.TileY == e.Y && m.Motion.Z == e.Z))
                _autoClose.Add(new AutoClose { Door = door, AtTick = _tick + DoorCloseWaitTicks });
            else
                StartDoor(door, "close");
        }
    }

    private void SetFace(int x, int y, int z, byte face, bool blocked)
    {
        int i = CellIndex(x, y, z);
        byte bits = (byte)(face & CellFlags.Faces);
        _cellFlags[i] = (byte)(blocked ? _cellFlags[i] | bits : _cellFlags[i] & ~bits);
        _flagChanges.Add((x, y, z, _cellFlags[i]));
    }

    private void SetCell(int x, int y, int z, bool solid, bool opaque)
    {
        int i = CellIndex(x, y, z);
        if (solid)
            _solidBits[i >> 3] |= (byte)(1 << (i & 7));
        else
            _solidBits[i >> 3] &= (byte)~(1 << (i & 7));
        _cellFlags[i] = (byte)(opaque ? _cellFlags[i] | CellFlags.Opaque : _cellFlags[i] & ~CellFlags.Opaque);
        _solidChanges.Add((x, y, z, solid));
        _flagChanges.Add((x, y, z, _cellFlags[i]));
    }

    private static byte DirOfYaw(ushort yaw)
    {
        // A compass bearing: 0 north, 16384 east, 32768 south, 49152 west; each direction holds a quarter turn around it.
        int sector = ((yaw + 8192) >> 14) & 3;
        return sector switch { 0 => (byte)1, 1 => (byte)4, 2 => (byte)2, _ => (byte)8 };
    }

    /// <summary>The collision view of the room: the walls and the standing mobs, in id order (the mover steps, so it is left out).</summary>
    private sealed class CollisionView : ICollisionView
    {
        private readonly FakeServer _s;

        public CollisionView(FakeServer server) => _s = server;

        public bool IsSolid(int x, int y, int z)
            => z != 1 || x < 1 || y < 1 || x > _s._width || y > _s._height || (_s._isMap ? _s.SolidAt(x, y, z) : _s._closed[x, y]);

        public TileEdges EdgesOf(int x, int y, int z)
            => _s._isMap && z == 1 && x >= 1 && y >= 1 && x <= _s._width && y <= _s._height
                ? (TileEdges)(_s._cellFlags[_s.CellIndex(x, y, z)] & CellFlags.Faces)
                : TileEdges.None;

        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
        {
            foreach (Mob m in _s._mobs) // ascending id: the list is kept in order
            {
                if (m.IsStatic || m.Lying || m.Id == exclude || ReferenceEquals(m, _s._stepping) || m.Motion.Z != z)
                    continue;
                if (Math.Abs(m.Motion.X - x) <= range && Math.Abs(m.Motion.Y - y) <= range)
                    into.Add(new CapsuleObstacle(m.Id, m.Motion.X, m.Motion.Y, MotionMath.DefaultRadius));
            }
        }
    }

    /// <summary>One mob of the room (a human or an object).</summary>
    public sealed class Mob
    {
        private readonly FakeServer _server;
        public MotionState Motion;
        public MotionParams LastParams;
        public bool IsStatic;
        public bool Run = true;
        public bool Combat;

        /// <summary>The body zones (<see cref="HealthZones.Zones"/>) with a wound: their doll limb carries tg's wound outline.</summary>
        public readonly bool[] Wounded = new bool[6];

        /// <summary>The body zones disabled (paralysed by a compound fracture): their doll state is tg's key 7.</summary>
        public readonly bool[] Disabled = new bool[6];

        /// <summary>tg's <c>mob.zone_selected</c> (the chest by default).</summary>
        public string ZoneSelected = "chest";
        public bool Dead;
        public readonly int[] Damage = new int[6];

        /// <summary>Toxin and oxygen damage: it counts for the health indicator, not for a zone.</summary>
        public int Overall;
        public long NextClickTick;
        public long LastMoveTick;
        public EntityState Sent;
        private int _knockdownLeft;
        private int _getUpLeft;
        private bool _resting;
        private long _lastTileKey = -1;

        public Mob(long id, string name, AutoWalker walker, FakeServer server)
        {
            Id = id;
            Name = name;
            Walker = walker;
            _server = server;
        }

        public long Id { get; }
        public string Name { get; }
        public AutoWalker Walker { get; }

        /// <summary>True while the body is on the floor: resting, knocked down or dead.</summary>
        public bool Lying => _resting || _knockdownLeft > 0 || _getUpLeft > 0 || Dead;

        /// <summary>tg's movement gate: not while knocked down, getting up or dead (crawling is allowed).</summary>
        public bool CanMove => !Dead && _knockdownLeft <= 0 && _getUpLeft <= 0;

        public void Revive()
        {
            _knockdownLeft = 0;
            _getUpLeft = GetUpTicks;
        }

        public void Knockdown(int ticks)
        {
            _knockdownLeft = Math.Max(_knockdownLeft, ticks);
            _resting = false;
        }

        public void ToggleRest(List<string> replies)
        {
            if (Dead || _knockdownLeft > 0 || _getUpLeft > 0)
            {
                replies.Add("You can't do that right now.");
                return;
            }
            if (_resting)
            {
                // Getting up takes a second (tg's get_up do_after); the body stays down until it is done.
                _resting = false;
                _getUpLeft = GetUpTicks;
                replies.Add("You start to get up.");
            }
            else
            {
                _resting = true;
                replies.Add("You lie down.");
            }
        }

        public void Tick(FakeServer server)
        {
            if (_knockdownLeft > 0 && --_knockdownLeft == 0 && !Dead)
                _getUpLeft = GetUpTicks; // the automatic get up after a knockdown
            else if (_getUpLeft > 0)
                _getUpLeft--;
        }

        /// <summary>A bot's input: its walker's held directions as a unit wish vector, facing the way it walks.</summary>
        public MotionInput WalkerInput(long tick)
        {
            HeldDirs held = Walker.Update(tick / TileSpace.TickRate, Motion.TileX, Motion.TileY);
            held = ViewCancel(held);
            int dx = ((held & HeldDirs.East) != 0 ? 1 : 0) - ((held & HeldDirs.West) != 0 ? 1 : 0);
            int dy = ((held & HeldDirs.North) != 0 ? 1 : 0) - ((held & HeldDirs.South) != 0 ? 1 : 0);
            if (dx == 0 && dy == 0)
                return MotionInput.Idle(0, Motion.Yaw);
            double length = Math.Sqrt(dx * dx + dy * dy);
            ushort yaw = (ushort)((int)Math.Round(Math.Atan2(dx, dy) / (2 * Math.PI) * 65536.0) & 0xFFFF);
            return new MotionInput(0, MoveWish.ToWire((float)(dx / length)), MoveWish.ToWire((float)(dy / length)), yaw, MotionInputFlags.None);
        }

        private static HeldDirs ViewCancel(HeldDirs held)
        {
            if ((held & HeldDirs.North) != 0 && (held & HeldDirs.South) != 0)
                held &= ~(HeldDirs.North | HeldDirs.South);
            if ((held & HeldDirs.East) != 0 && (held & HeldDirs.West) != 0)
                held &= ~(HeldDirs.East | HeldDirs.West);
            return held;
        }

        /// <summary>The state as the wire carries it (the type index is the snapshot's table: 0 mobs, 1 objects).</summary>
        public EntityState Wire()
        {
            long key = ((long)Motion.TileX << 32) ^ (uint)Motion.TileY;
            if (key != _lastTileKey)
            {
                if (_lastTileKey != -1)
                    LastMoveTick = _server._tick;
                _lastTileKey = key;
            }
            TableRef appearance = IsStatic ? _server._itemApp : _server.HumanAppOf(Id, Lying);
            bool lying = Lying;
            return new EntityState(Id, IsStatic ? 1 : 0, Motion.TileX, Motion.TileY, Motion.Z, DirOfYaw(Motion.Yaw), Name, 0f, LastMoveTick)
            {
                HasPos = !IsStatic,
                PosX = IsStatic ? Motion.TileX * MotionMath.SubunitsPerTile : Motion.X,
                PosY = IsStatic ? Motion.TileY * MotionMath.SubunitsPerTile : Motion.Y,
                Yaw = (byte)(Motion.Yaw >> 8),
                Posture = PostureBits.Encode(lying, lying ? 90 : 0),
                Appearance = appearance,
            };
        }
    }
}
