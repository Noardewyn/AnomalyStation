namespace Anomaly.Protocol;

// Owner: M1 P2 (contract: M1 contracts step), M2 P5 for version 2 (contract: M2 contracts step; the version-2 additions
// are marked and MessagesV2.cs holds the new types). The meaning of every field is fixed here; the encoding is the codec's
// (docs/ai/specs/protocol.md).
//
// Tick numbering, used by every message below: a tick is the server world's World.Tick, 20 per second (tg world.fps 20,
// 1 tick = 0.5 ds). "The state of tick T" is the world after its T-th World.Step, read by the replicator after that Step
// returns. A command a client sends is queued for the next tick: received while tick T is the last stepped, it is applied
// in the command phase of tick T + 1 (scope 2e "Where the network meets the tick").

/// <summary>
/// A server-issued reconnect token (scope Q3, lead plan: name plus token until accounts in M10): 16 random bytes from the
/// host (never from the world RNG, scope 2j), given in <see cref="Welcome"/> and presented in <see cref="Hello"/> to take
/// the session back after a drop. <see cref="None"/> (all zero) means "no token".
/// </summary>
public readonly record struct SessionToken(ulong Low, ulong High)
{
    /// <summary>No token (a first connection).</summary>
    public static SessionToken None => default;

    /// <summary>True for <see cref="None"/>.</summary>
    public bool IsNone => Low == 0 && High == 0;
}

/// <summary>
/// Client to server, the first and only message before <see cref="Welcome"/>; it travels in the transport's connection
/// request data, so the server refuses a bad one before a connection exists (a request flood is cheap to refuse). Wire:
/// <c>[Hello][magic "ANOM"][u16 protocol][name][u8 flags][token if flags bit 0][login token if flags bit 1]</c> (flags bit 2: <paramref name="WaitsForLoad"/>) (docs/ai/specs/protocol.md section 3.1). The server answers with a reject
/// (<see cref="DisconnectInfo"/>) or, once the world accepted the player (tg's <c>client/New</c> and <c>mob/Login</c>
/// finished), with the join sequence.
/// </summary>
/// <param name="Protocol">The client's <see cref="ProtocolVersion.Current"/>; another value is <see cref="DisconnectReason.WrongVersion"/>.</param>
/// <param name="Name">The player's name, at most <see cref="Limits.MaxNameChars"/> characters, no control characters. It becomes the BYOND key of the session (the tg <c>client.key</c>; <c>ckey</c> is its canonical form).</param>
/// <param name="ReconnectToken">The token of an earlier <see cref="Welcome"/> to take that session back, or <see cref="SessionToken.None"/>.</param>
/// <param name="LoginToken">Version 85 (D335): the hub's game token the launcher handed the client (an ES256 JWT of at most <see cref="Limits.MaxLoginTokenBytes"/> bytes that names the account and this server), or null. A server that holds the hub's key takes the account's name from it in place of <paramref name="Name"/>; a server without one does not read it.</param>
/// <param name="WaitsForLoad">
/// Version 97 (DV597): the client loads the world before it plays and says so with <see cref="ActionKind.Loaded"/>: until then the server keeps
/// the new player in tg's lobby (<c>/mob/dead/new_player</c>, no body) and does not late-join it. The game's own client sets it; a bot or a test
/// rig that plays at once does not.
/// </param>
public readonly record struct Hello(ushort Protocol, string Name, SessionToken ReconnectToken, string LoginToken = null, bool WaitsForLoad = false);

/// <summary>
/// Server to client, State channel, the first message of the join sequence (Welcome, <see cref="MapGrid"/>,
/// <see cref="Snapshot"/>): sent once tg's <c>client/New</c> has returned, which ran <c>mob/Login</c> on the avatar.
/// </summary>
/// <param name="Session">The session's ordinal: assigned by the server in acceptance order, never reused in a run; it orders the commands of one tick (scope 2j).</param>
/// <param name="Token">The reconnect token of this session (keep it for <see cref="Hello.ReconnectToken"/>).</param>
/// <param name="Tick">The server tick at which the session joined; the Snapshot that follows is of this tick.</param>
/// <param name="TickRate">Ticks per second (20).</param>
/// <param name="OwnEntity">The id of the client's mob (<see cref="NetField.OwnMob"/>); later changes come in the headers of Snapshot and Delta.</param>
public readonly record struct Welcome(int Session, SessionToken Token, long Tick, byte TickRate, long OwnEntity);

/// <summary>A run of <see cref="Count"/> consecutive cells whose turf type is <c>TurfTypes[TypeIndex]</c> (<see cref="MapGrid.Runs"/>).</summary>
public readonly record struct TurfRun(int Count, int TypeIndex);

/// <summary>
/// Server to client, State channel, after <see cref="Welcome"/>: the static map, sent in full once at join (D23: the map
/// is small, at most 255 x 255). Every cell's turf type as a run-length list. Turf changes arrive with tg's
/// <c>ChangeTurf</c> in M3 (a protocol version bump); in M1 the map never changes after boot.
/// </summary>
public sealed partial class MapGrid
{
    /// <summary>The server tick the map was read at.</summary>
    public long Tick { get; set; }

    /// <summary>BYOND <c>world.maxx</c>, 1 to <see cref="Limits.MaxMapSize"/>.</summary>
    public int Width { get; set; }

    /// <summary>BYOND <c>world.maxy</c>.</summary>
    public int Height { get; set; }

    /// <summary>BYOND <c>world.maxz</c>, 1 to <see cref="Limits.MaxMapLevels"/>.</summary>
    public int Levels { get; set; }

    /// <summary>The turf types of this message (index to DM type path, for example <c>/turf/closed/indestructible</c>); a table local to the MapGrid, not the connection's entity type table.</summary>
    public List<string> TurfTypes { get; } = new();

    /// <summary>
    /// The cells in BYOND's turf order, x fastest, then y, then z (x and y from 1), as runs; the counts add up to
    /// <c>Width * Height * Levels</c>.
    /// </summary>
    public List<TurfRun> Runs { get; } = new();

    /// <summary><c>Width * Height * Levels</c>: the number of cells the <see cref="Runs"/> cover.</summary>
    public int CellCount => Width * Height * Levels;

    // ---- protocol version 2 (M2 contracts; scope 2d, 2h; the encoding is M2 P5's) ----

    /// <summary>
    /// Version 2: every cell's turf appearance, as runs over the cells in the order of <see cref="Runs"/> (a run's
    /// <see cref="TurfRun.TypeIndex"/> is the PUBLIC appearance table index here, 0 for none). The Snapshot that follows
    /// carries the public table, so a client resolves them once the Snapshot arrived.
    /// </summary>
    public List<TurfRun> AppearanceRuns { get; } = new();

    /// <summary>
    /// Version 2: one bit per cell (bit <c>i % 8</c> of byte <c>i / 8</c>, cells in the order of <see cref="Runs"/>), set
    /// when the cell blocks capsules: the server computes it with the predicate its controller uses
    /// (<c>Anomaly.Core.Tg.LocomotionSeam.IsSolid</c>), so the client's prediction collides with exactly what the server's does
    /// (D103). Later changes come as <see cref="Delta.SolidChanged"/>. Length <c>ceil(CellCount / 8)</c>.
    /// </summary>
    public byte[] SolidBits { get; set; } = Array.Empty<byte>();

    /// <summary>Whether the cell (x, y, z) (from 1) is solid by <see cref="SolidBits"/>; outside the map: true.</summary>
    public bool IsSolid(int x, int y, int z)
    {
        if (x < 1 || y < 1 || z < 1 || x > Width || y > Height || z > Levels) return true;
        int i = (x - 1) + (y - 1) * Width + (z - 1) * Width * Height;
        return i / 8 < SolidBits.Length && (SolidBits[i / 8] & (1 << (i % 8))) != 0;
    }

    /// <summary>
    /// The runs expanded: one entry per cell in the order of <see cref="Runs"/> (x fastest, then y, then z), each the index
    /// into <see cref="TurfTypes"/> (an addition of P2 for the client's mesh and for tests). Throws
    /// <see cref="InvalidOperationException"/> when the runs do not cover exactly <see cref="CellCount"/> cells.
    /// </summary>
    public ushort[] ExpandCells()
    {
        var cells = new ushort[CellCount];
        int next = 0;
        foreach (TurfRun run in Runs)
        {
            if (run.Count < 0 || run.Count > cells.Length - next || run.TypeIndex < 0 || run.TypeIndex > ushort.MaxValue)
                throw new InvalidOperationException("the MapGrid runs do not fit the map");
            Array.Fill(cells, (ushort)run.TypeIndex, next, run.Count);
            next += run.Count;
        }

        if (next != cells.Length)
            throw new InvalidOperationException("the MapGrid runs cover " + next + " of " + cells.Length + " cells");
        return cells;
    }
}

/// <summary>
/// The full state of one entity: a record of a <see cref="Snapshot"/> or of <see cref="Delta.Spawned"/>. Every field of
/// <see cref="NetField"/> but <see cref="NetField.OwnMob"/>.
/// </summary>
/// <param name="Id"><see cref="NetField.Id"/>.</param>
/// <param name="Type"><see cref="NetField.Type"/>: the index in the connection's type table.</param>
/// <param name="X"><see cref="NetField.X"/>.</param>
/// <param name="Y"><see cref="NetField.Y"/>.</param>
/// <param name="Z"><see cref="NetField.Z"/>.</param>
/// <param name="Dir"><see cref="NetField.Dir"/>.</param>
/// <param name="Name"><see cref="NetField.Name"/>, at most <see cref="Limits.MaxEntityNameBytes"/> bytes.</param>
/// <param name="GlideSize"><see cref="NetField.GlideSize"/>.</param>
/// <param name="LastMoveTick"><see cref="NetField.LastMoveTick"/>.</param>
public readonly record struct EntityState(long Id, int Type, int X, int Y, int Z, byte Dir, string Name, float GlideSize, long LastMoveTick)
{
    // ---- protocol version 2 (M2 contracts; the encoding is M2 P5's) ----

    /// <summary><see cref="NetField.Pos"/>: the centre's x in 1/256 tile (Anomaly.Core.Motion's units: a tile centre is 256 times the tile's x). A capsule mob's continuous position; for any other entity the centre of its tile (<c>X * 256</c>), which the codec need not send.</summary>
    public int PosX { get; init; }

    /// <summary><see cref="NetField.Pos"/>: the centre's y in 1/256 tile.</summary>
    public int PosY { get; init; }

    /// <summary><see cref="NetField.Yaw"/>: where a capsule mob looks, the high byte of <c>MotionState.Yaw</c> (256 per turn, a compass bearing): the billboard's frame by viewer angle (D104). 0 for other entities.</summary>
    public byte Yaw { get; init; }

    /// <summary><see cref="NetField.Posture"/>: tg's body position and lying angle (<see cref="PostureBits"/>).</summary>
    public byte Posture { get; init; }

    /// <summary><see cref="NetField.Appearance"/>: the entity's appearance in the public table (none before the replicator interns appearances).</summary>
    public TableRef Appearance { get; init; }

    /// <summary>
    /// A P5 addition: true when <see cref="PosX"/>, <see cref="PosY"/> and <see cref="Yaw"/> are a capsule mob's continuous
    /// state (the record carried <see cref="NetField.Pos"/>). False: <c>PosX = X * 256</c>, <c>PosY = Y * 256</c> (the codec
    /// fills them in) and <see cref="Yaw"/> is 0. It is what <see cref="ReplicaEntity.HasPos"/> reports.
    /// </summary>
    public bool HasPos { get; init; }

    // ---- protocol version 3 (M3 contracts, MessagesV3.cs; the encoding is M3 P5's) ----

    /// <summary><see cref="NetField.Light"/>: the entity's light source, <see cref="LightState.None"/> when it has none.</summary>
    public LightState Light { get; init; }

    // ---- protocol version 4 (M4a contracts, MessagesV4.cs; the encoding is M4a P6's) ----

    /// <summary><see cref="NetField.Flags"/>: the entity's flags (<see cref="EntityFlags"/>; <see cref="EntityFlags.Thrown"/> in flight).</summary>
    public EntityFlags Flags { get; init; }

    // ---- protocol version 8 ----

    /// <summary><see cref="NetField.Flick"/>: the appearance of the entity's last <c>flick</c> in the public table (none: it never flicked).</summary>
    public TableRef FlickAppearance { get; init; }

    /// <summary><see cref="NetField.Flick"/>: the server tick that flick started in (0 with no flick; at most the message's tick).</summary>
    public long FlickTick { get; init; }

    // ---- protocol version 9 ----

    /// <summary><see cref="NetField.Animation"/>: the atom's running <c>animate()</c> chains, null for none.</summary>
    public EntityAnimation Animation { get; init; }

    // ---- protocol version 10 ----

    /// <summary><see cref="NetField.VisHost"/>: true when the entity is a short-lived visual drawn on another atom (<see cref="VisHost"/>).</summary>
    public bool IsVisual { get; init; }

    /// <summary><see cref="NetField.VisHost"/>: the id of the entity the visual is drawn on, 0 for the turf at <see cref="X"/>, <see cref="Y"/>, <see cref="Z"/> (0 when <see cref="IsVisual"/> is false).</summary>
    public long VisHost { get; init; }

    // ---- protocol version 34 ----

    /// <summary><see cref="NetField.Shot"/>: where a projectile's or a shot effect's 3D line runs, null for any other entity.</summary>
    public ShotState? Shot { get; init; }

    // ---- protocol version 43 ----

    /// <summary><see cref="NetField.Door"/>: an airlock's access and state for the client's guess (MessagesV43.cs), null for any other entity.</summary>
    public DoorState Door { get; init; }

    // ---- protocol version 77 ----

    /// <summary><see cref="NetField.Ride"/>: the level ride announced now (DV371, MessagesV77.cs), <see cref="RideState.None"/> for none.</summary>
    public RideState Ride { get; init; }

    // ---- protocol version 80 ----

    /// <summary><see cref="NetField.Tram"/>: a tram platform's announced trip (DV403), <see cref="Anomaly.Core.Motion.TramRide.None"/> for none.</summary>
    public Anomaly.Core.Motion.TramRide Tram { get; init; }

    // ---- protocol version 81 ----

    /// <summary><see cref="NetField.Flight"/>: the free motion (DV416): a moving segment, a rest point, or <see cref="Anomaly.Core.Motion.FlightArc.None"/>.</summary>
    public Anomaly.Core.Motion.FlightArc Flight { get; init; }
}

/// <summary>
/// The changed fields of one entity in a <see cref="Delta"/>: <see cref="Mask"/> says which (only bits of
/// <see cref="NetField.Changeable"/>); the other value fields are meaningless and are not on the wire.
/// </summary>
public struct EntityChange
{
    /// <summary>The entity's id.</summary>
    public long Id;

    /// <summary>Which fields changed in this tick.</summary>
    public NetField Mask;

    public int X;
    public int Y;
    public int Z;
    public byte Dir;
    public string Name;
    public float GlideSize;
    public long LastMoveTick;

    // ---- protocol version 2 (M2 contracts): see EntityState ----

    /// <summary><see cref="NetField.Pos"/>, x in 1/256 tile.</summary>
    public int PosX;

    /// <summary><see cref="NetField.Pos"/>, y in 1/256 tile.</summary>
    public int PosY;

    /// <summary><see cref="NetField.Yaw"/>.</summary>
    public byte Yaw;

    /// <summary><see cref="NetField.Posture"/>.</summary>
    public byte Posture;

    /// <summary><see cref="NetField.Appearance"/>.</summary>
    public TableRef Appearance;

    // ---- protocol version 3 (M3 contracts, MessagesV3.cs) ----

    /// <summary><see cref="NetField.Light"/>: the new light state (<see cref="LightState.None"/>: the light is gone).</summary>
    public LightState Light;

    // ---- protocol version 4 (M4a contracts, MessagesV4.cs) ----

    /// <summary><see cref="NetField.Flags"/>: the new flags.</summary>
    public EntityFlags Flags;

    // ---- protocol version 8 ----

    /// <summary><see cref="NetField.Flick"/>: the new flick's appearance.</summary>
    public TableRef FlickAppearance;

    /// <summary><see cref="NetField.Flick"/>: the tick it started in.</summary>
    public long FlickTick;

    // ---- protocol version 9 ----

    /// <summary><see cref="NetField.Animation"/>: the new animation, null when it ended or was stopped.</summary>
    public EntityAnimation Animation;

    // ---- protocol version 10 ----

    /// <summary><see cref="NetField.VisHost"/>: the entity the visual is now drawn on, 0 for its turf (a change never makes a visual an ordinary entity: it despawns).</summary>
    public long VisHost;

    // ---- protocol version 34 ----

    /// <summary><see cref="NetField.Shot"/>: the shot's new line (a shot never loses it).</summary>
    public ShotState Shot;

    // ---- protocol version 43 ----

    /// <summary><see cref="NetField.Door"/>: the airlock's new door state (an airlock never loses it).</summary>
    public DoorState Door;

    // ---- protocol version 77 ----

    /// <summary><see cref="NetField.Ride"/>: the entity's new level ride, <see cref="RideState.None"/> when it ended.</summary>
    public RideState Ride;

    // ---- protocol version 80 ----

    /// <summary><see cref="NetField.Tram"/>: the tram platform's new trip, none when it ended (DV403).</summary>
    public Anomaly.Core.Motion.TramRide Tram;

    // ---- protocol version 81 ----

    /// <summary><see cref="NetField.Flight"/>: the new free-motion segment or rest point, none when it ended (DV416).</summary>
    public Anomaly.Core.Motion.FlightArc Flight;
}

/// <summary>
/// Server to client, State channel, after <see cref="MapGrid"/>: the full entity state of one tick, for one connection
/// (at join, and again whenever the server must resynchronize the connection). The Deltas of the following ticks build on
/// it: a connection that received the Snapshot of tick T receives exactly the Deltas of ticks T + 1, T + 2, ...
/// </summary>
public sealed class Snapshot
{
    /// <summary>The tick whose state this is.</summary>
    public long Tick { get; set; }

    /// <summary>The seq of the last <see cref="InputFrame"/> of this connection that the world applied in a tick up to <see cref="Tick"/> (0: none yet).</summary>
    public uint LastInputSeq { get; set; }

    /// <summary>The id of the connection's own mob (<see cref="NetField.OwnMob"/>), 0 when it has none.</summary>
    public long OwnEntity { get; set; }

    /// <summary>
    /// The connection's type table, complete so far (index to DM type path, for example <c>/mob</c>). The table is the
    /// server's for the whole run, append-only: an index is given to a path the first time an entity of that type is
    /// replicated, and every Delta carries the entries its tick added (<see cref="Delta.NewTypes"/>). So every joined
    /// connection holds a prefix of the same table, a Delta can be encoded once for all of them, and bots and clients
    /// never depend on the server build's type order.
    /// </summary>
    public List<string> Types { get; } = new();

    /// <summary>Every entity, in increasing id order.</summary>
    public List<EntityState> Entities { get; } = new();

    // ---- protocol version 2 (M2 contracts) ----

    /// <summary>Version 2: the public resource table, complete so far (index 1 is <c>Resources[0]</c>); append-only for the run, like <see cref="Types"/>.</summary>
    public List<string> Resources { get; } = new();

    /// <summary>Version 2: the public string table, complete so far (index 1 is <c>Strings[0]</c>).</summary>
    public List<string> Strings { get; } = new();

    /// <summary>Version 2: the public appearance table, complete so far (index 1 is <c>Appearances[0]</c>).</summary>
    public List<AppearanceEntry> Appearances { get; } = new();

    /// <summary>Version 2: the connection's private block (its private tables complete so far, every <c>client.screen</c> entry as added, the own motion state).</summary>
    public OwnBlock Own { get; set; } = new();
}

/// <summary>
/// Server to client, State channel, one per tick after the connection's <see cref="Snapshot"/>: what changed in the tick,
/// from the state of tick <c>Tick - 1</c> to the state of <see cref="Tick"/>. Applied in this order: new types, spawned,
/// changed, despawned; an id is in at most one of the three entity lists.
/// <para>
/// Wire: a per-connection header (<see cref="Tick"/>, <see cref="LastInputSeq"/>, <see cref="OwnEntity"/>) followed by a
/// body that is the same for every connection (<see cref="FirstNewType"/> onwards). The server encodes the body once per
/// tick and writes only the header per connection (<c>Codec.EncodeDeltaBody</c>, <c>Codec.EncodeDelta</c>).
/// </para>
/// </summary>
public sealed partial class Delta
{
    /// <summary>Header: the tick whose changes these are; always one more than the previous Snapshot or Delta of the connection.</summary>
    public long Tick { get; set; }

    /// <summary>
    /// Header: the seq of the last <see cref="InputFrame"/> of this connection that the world applied in a tick up to
    /// <see cref="Tick"/>. Every Delta carries it, so M2/M10 prediction can reconcile without a format change (scope 2c).
    /// </summary>
    public uint LastInputSeq { get; set; }

    /// <summary>Header: the id of the connection's own mob at <see cref="Tick"/> (<see cref="NetField.OwnMob"/>), 0 when it has none.</summary>
    public long OwnEntity { get; set; }

    /// <summary>Body: the type table index of <c>NewTypes[0]</c>; equal to the table's length before this Delta.</summary>
    public int FirstNewType { get; set; }

    /// <summary>Body: the type table entries this tick added, in index order.</summary>
    public List<string> NewTypes { get; } = new();

    /// <summary>Body: the entities that appeared in this tick (created on a turf, or moved onto one from nullspace or a container), with their full state.</summary>
    public List<EntityState> Spawned { get; } = new();

    /// <summary>Body: the entities whose fields changed in this tick.</summary>
    public List<EntityChange> Changed { get; } = new();

    /// <summary>Body: the ids of the entities that disappeared in this tick (deleted, or moved off the turfs into nullspace or a container).</summary>
    public List<long> Despawned { get; } = new();

    // ---- protocol version 2 (M2 contracts; scope 2d, 2h; the encoding is M2 P5's) ----

    /// <summary>Body: the public resource table index of <c>NewResources[0]</c> (the table's length before this Delta, plus one).</summary>
    public uint FirstNewResource { get; set; }

    /// <summary>Body: public resource table entries this tick added (icon file paths, GAGS references), in index order.</summary>
    public List<string> NewResources { get; } = new();

    /// <summary>Body: the public string table index of <c>NewStrings[0]</c>.</summary>
    public uint FirstNewString { get; set; }

    /// <summary>Body: public string table entries this tick added (icon states), in index order.</summary>
    public List<string> NewStrings { get; } = new();

    /// <summary>Body: the public appearance table index of <c>NewAppearances[0]</c>.</summary>
    public uint FirstNewAppearance { get; set; }

    /// <summary>Body: public appearance entries this tick added, in index order (an entry references only earlier ones and the tables).</summary>
    public List<AppearanceEntry> NewAppearances { get; } = new();

    /// <summary>Body: cells whose solidity changed this tick (M3 doors; empty in M2).</summary>
    public List<CellSolidChange> SolidChanged { get; } = new();

    /// <summary>Body: cells whose turf appearance changed this tick (M3; empty in M2).</summary>
    public List<CellAppearanceChange> TurfAppearanceChanged { get; } = new();

    /// <summary>Private (per connection, after the body): the connection's own block. The shared body never depends on it.</summary>
    public OwnBlock Own { get; set; } = new();
}

/// <summary>
/// Server to client, Control channel: one chat message tg sent to this player (<c>to_chat</c> through the message sink,
/// presentation-seam.md section 3: the login messages, tg's kick reasons). Text localization (D75) comes in M9.
/// </summary>
/// <param name="Type">tg's message type (the <c>"type"</c> key of tg's message list, a <c>MESSAGE_TYPE_*</c> text), at most <see cref="Limits.MaxServerTextTypeBytes"/> bytes; may be null.</param>
/// <param name="Html">tg's message text with its HTML spans (the <c>"html"</c> key, else <c>"text"</c>), at most <see cref="Limits.MaxServerTextBytes"/> bytes.</param>
public readonly record struct ServerText(string Type, string Html);

/// <summary>The direction bits of <see cref="InputFrame.HeldDirs"/>: BYOND's NORTH, SOUTH, EAST and WEST.</summary>
[Flags]
public enum HeldDirs : byte
{
    None = 0,
    North = 1,
    South = 2,
    East = 4,
    West = 8,

    /// <summary>Every valid bit; any other bit makes the frame invalid.</summary>
    All = North | South | East | West,
}

/// <summary>
/// Client to server, Control channel (reliable ordered in M1, like tg's keyDown and keyUp verbs, scope 2c M-C): the movement
/// keys the player holds now, sent when they change. The server turns a change into tg's keyDown/keyUp movement branch on
/// the player's <c>client</c> in the command phase of the next tick; tg's <c>SSinput</c> then moves the mob. M2 adds the
/// wish vector and the yaw under a protocol version bump.
/// </summary>
/// <param name="Seq">1 for the first frame of a connection, one more for each frame after; the server refuses a frame whose seq does not grow (a strike) and echoes the last applied one in <see cref="Delta.LastInputSeq"/>.</param>
/// <param name="HeldDirs">The held directions, absolute (map NORTH is up); only the bits of <see cref="Protocol.HeldDirs.All"/>.</param>
public readonly record struct InputFrame(uint Seq, HeldDirs HeldDirs);

/// <summary>Why the server refused a connection or closed a session (<see cref="DisconnectInfo.Reason"/>).</summary>
public enum DisconnectReason : byte
{
    /// <summary>No reason given (a transport-level drop, or a peer that sent no data).</summary>
    None = 0,

    /// <summary>The server has <c>max_players</c> sessions or too many pending requests.</summary>
    ServerFull = 1,

    /// <summary><see cref="Hello.Protocol"/> is not the server's <see cref="ProtocolVersion.Current"/>.</summary>
    WrongVersion = 2,

    /// <summary>The connection request held no valid <see cref="Hello"/>.</summary>
    BadHello = 3,

    /// <summary>The name is empty, too long or has control characters.</summary>
    InvalidName = 4,

    /// <summary>Another connected session has this name (its canonical key).</summary>
    NameInUse = 5,

    /// <summary>The reconnect token belongs to no session of this name.</summary>
    BadToken = 6,

    /// <summary>The world has not finished booting (joins open at <c>RUNLEVEL_GAME</c>).</summary>
    NotReady = 7,

    /// <summary>A console <c>kick</c>.</summary>
    Kicked = 8,

    /// <summary>The host's rate limits were exceeded.</summary>
    Flooding = 9,

    /// <summary><see cref="Limits.MaxStrikes"/> protocol violations within <see cref="Limits.StrikeWindowTicks"/>.</summary>
    ProtocolViolation = 10,

    /// <summary>The server is shutting down.</summary>
    ServerShutdown = 11,

    /// <summary>A newer connection took the session over with its token.</summary>
    Replaced = 12,

    /// <summary>tg's <c>client/New</c> refused the connection (it returned null).</summary>
    Refused = 13,

    /// <summary>The world deleted the player's <c>client</c> (tg's own kicks, such as the keysend flood autokick; the reason came as <see cref="ServerText"/> first).</summary>
    ClientDeleted = 14,

    /// <summary>Version 45 (M10 slice H): tg's <c>world/Reboot</c> ended the round; the server is starting the next one, and the client joins it again with its token.</summary>
    RoundRestart = 15,

    /// <summary>Version 46 (M10 slice I): tg's <c>world/IsBanned</c> refused the key or the address (the text is tg's ban description), or a server ban closed the session.</summary>
    Banned = 16,

    /// <summary>Version 85 (D335): the server takes players through the launcher's login, and the <see cref="Hello"/> carried neither a valid <see cref="Hello.LoginToken"/> nor the reconnect token of a session logged in since the server started.</summary>
    LoginRequired = 17,
}

/// <summary>
/// Server to client, in the transport's reject data (a refused connection request) or disconnect data (a closed session):
/// <c>[Disconnect][u8 reason][text]</c>.
/// </summary>
/// <param name="Reason">Why.</param>
/// <param name="Text">Human-readable detail, at most <see cref="Limits.MaxDisconnectTextBytes"/> bytes; may be null.</param>
public readonly record struct DisconnectInfo(DisconnectReason Reason, string Text);
