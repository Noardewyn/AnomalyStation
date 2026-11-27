using System.Linq;
using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: M1 P2. Contract (the API the Godot client, P6, the bots, P5, and the tests call) by the M1 contracts step; the
// bodies are P2's. Additions of P2 beyond the contract: ReplicaWorld.IsDesynced, ReplicaEntity.TryInterpolate with
// EntityPose and EntityGlide (the interpolation math, tested headless: scope 2g).

/// <summary>
/// One recorded state of an entity for interpolation (scope 2g): its position, direction and glide at a server tick.
/// </summary>
/// <param name="Tick">The server tick of the Snapshot or Delta that set these values.</param>
/// <param name="X">BYOND x.</param>
/// <param name="Y">BYOND y.</param>
/// <param name="Z">BYOND z.</param>
/// <param name="Dir">BYOND dir.</param>
/// <param name="GlideSize">BYOND glide_size at that tick (pixels per tick; 32 pixels are one tile; 0: BYOND's default glide).</param>
/// <param name="LastMoveTick">The tick of the entity's last move as of that tick.</param>
public readonly record struct EntitySample(long Tick, int X, int Y, int Z, byte Dir, float GlideSize, long LastMoveTick)
{
    // ---- protocol version 2 (M2 contracts): the capsule fields at that tick (EntityState's), filled by M2 P5 ----

    /// <summary><see cref="NetField.Pos"/> x in 1/256 tile.</summary>
    public int PosX { get; init; }

    /// <summary><see cref="NetField.Pos"/> y in 1/256 tile.</summary>
    public int PosY { get; init; }

    /// <summary><see cref="NetField.Yaw"/>.</summary>
    public byte Yaw { get; init; }

    /// <summary><see cref="NetField.Posture"/>.</summary>
    public byte Posture { get; init; }

    /// <summary><see cref="NetField.Appearance"/>.</summary>
    public TableRef Appearance { get; init; }
}

/// <summary>An entity as drawn at a render time: BYOND tile coordinates with a fraction (a tile centre is a whole number), the level and the direction.</summary>
public readonly record struct EntityPose(float X, float Y, int Z, byte Dir);

/// <summary>
/// The client-side copy of the server's entity table (scope 2f, 2g: "M1's client replica is a plain entity table"). It
/// applies <see cref="MapGrid"/>, <see cref="Snapshot"/> and <see cref="Delta"/> in the order the State channel delivers
/// them and keeps, per entity, a ring of the last <see cref="History"/> samples for interpolation. Single-threaded: the
/// caller applies and reads on one thread (the Godot main thread in <c>_Process</c>, a bot's update).
/// <para>
/// Consistency: a Delta must follow the Snapshot or Delta of the previous tick, its changed and despawned ids must exist,
/// its spawned ids must not, and every type index must be in the table. Anything else is a desync and throws
/// <see cref="ProtocolException"/> (<see cref="ProtocolError.BadValue"/>); the client then leaves and joins again. A
/// Snapshot replaces the whole table (a resynchronization).
/// </para>
/// </summary>
public sealed partial class ReplicaWorld
{
    /// <summary>The default number of samples kept per entity.</summary>
    public const int DefaultHistory = 16;

    private readonly Dictionary<long, ReplicaEntity> _byId = new();
    private readonly List<ReplicaEntity> _ordered = new();
    private readonly List<string> _types = new();
    private readonly List<ReplicaEntity> _spawnedScratch = new();
    private readonly List<ReplicaEntity> _despawnedScratch = new();
    private bool _orderedStale;
    private MapGrid _map;
    private bool _hasSnapshot;
    private long _tick = -1;
    private uint _lastInputSeq;
    private long _ownEntity;
    private bool _desynced;

    /// <param name="history">Samples kept per entity, at least 2.</param>
    public ReplicaWorld(int history = DefaultHistory)
    {
        if (history < 2) throw new ArgumentOutOfRangeException(nameof(history), history, "an entity keeps at least two samples to interpolate between");
        History = history;
    }

    /// <summary>Samples kept per entity.</summary>
    public int History { get; }

    /// <summary>The map of the join sequence, null before it arrived.</summary>
    public MapGrid Map => _map;

    /// <summary>True once a Snapshot has been applied.</summary>
    public bool HasSnapshot => _hasSnapshot;

    /// <summary>How many Snapshots were applied: a Snapshot restarts the private tables at index 1 (the player's new mob), so a cache
    /// keyed by a table index is stale once this changes.</summary>
    public int SnapshotsApplied { get; private set; }

    /// <summary>
    /// True after a Delta was refused as a desync (a gap, a wrong id or type, a coordinate outside the map): the table is
    /// then unreliable, every further Delta is refused too, and only a Snapshot clears it. Not part of the contract.
    /// </summary>
    public bool IsDesynced => _desynced;

    /// <summary>The server tick of the last applied Snapshot or Delta; -1 before the first.</summary>
    public long Tick => _tick;

    /// <summary>The last input seq the server applied (<see cref="Delta.LastInputSeq"/>), for prediction and tests.</summary>
    public uint LastInputSeq => _lastInputSeq;

    /// <summary>The id of the connection's own mob, 0 when it has none.</summary>
    public long OwnEntity => _ownEntity;

    /// <summary>The connection's type table (index to DM type path).</summary>
    public IReadOnlyList<string> Types => _types;

    /// <summary>The number of entities.</summary>
    public int Count => _byId.Count;

    /// <summary>Every entity, in increasing id order. The sequence is valid until the next <c>Apply</c>.</summary>
    public IEnumerable<ReplicaEntity> Entities
    {
        get
        {
            Order();
            return _hidden.Count == 0 ? _ordered : _ordered.Where(e => !_hidden.Contains(e.Id));
        }
    }

    /// <summary>The entity of <paramref name="id"/>, if it exists.</summary>
    public bool TryGet(long id, out ReplicaEntity entity)
    {
        if (_byId.TryGetValue(id, out entity) && (_hidden.Count == 0 || !_hidden.Contains(id)))
            return true;
        entity = null; // version 43: an entity the client guessed picked up is out of its view
        return false;
    }

    /// <summary>Called for each entity a Snapshot or a Delta adds, after it was applied.</summary>
    public event Action<ReplicaEntity> Spawned;

    /// <summary>Called for each entity a Snapshot or a Delta removes, after it was applied.</summary>
    public event Action<ReplicaEntity> Despawned;

    /// <summary>Takes the map of the join sequence.</summary>
    public void Apply(MapGrid grid)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (grid.Width < 1 || grid.Width > Limits.MaxMapSize || grid.Height < 1 || grid.Height > Limits.MaxMapSize
            || grid.Levels < 1 || grid.Levels > Limits.MaxMapLevels)
            throw Bad("a map size outside the limits");
        long covered = 0;
        foreach (TurfRun run in grid.Runs)
        {
            if (run.Count < 1 || run.TypeIndex < 0 || run.TypeIndex >= grid.TurfTypes.Count)
                throw Bad("a map run with no cells or a type outside the table");
            covered += run.Count;
        }

        if (covered != grid.CellCount)
            throw Bad("the map runs cover " + covered + " of " + grid.CellCount + " cells");
        TakeMapCells(grid);
        _map = grid;
        TakeCeilings(grid);
        TakeTurfLights(grid); // version 96
        TakeMapCellsV3(grid);
        TakeMapCellsV5(grid);
        TakeMapCellsV6();
        TakeMapCellsV13();
        TakeMapCellsV15();
        TakeMapCellsV16();
        TakeMapCellsV19();
    }

    /// <summary>Replaces the entity table with the Snapshot's (the entities it lacks despawn, the new ones spawn).</summary>
    public void Apply(Snapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (_hasSnapshot && snapshot.Tick < _tick)
            throw Bad("a Snapshot of tick " + snapshot.Tick + " arrives after tick " + _tick);
        long previousId = 0;
        foreach (EntityState e in snapshot.Entities)
        {
            if (e.Id <= previousId)
                throw Bad("snapshot entities must be in increasing id order");
            previousId = e.Id;
            CheckState(e, snapshot.Types.Count, snapshot.Appearances.Count);
        }

        ValidateSnapshotTables(snapshot);
        ValidateSnapshotOwn(snapshot);
        _types.Clear();
        _types.AddRange(snapshot.Types);
        var next = new Dictionary<long, ReplicaEntity>(snapshot.Entities.Count);
        _ordered.Clear();
        foreach (EntityState e in snapshot.Entities)
        {
            string path = snapshot.Types[e.Type];
            if (_byId.Remove(e.Id, out ReplicaEntity existing) && existing.TypePath == path)
            {
                LightState before = existing.Light;
                existing.Reset(e, snapshot.Tick);
                if (before != e.Light)
                    NoteLight(existing);
            }
            else
            {
                if (existing != null)
                {
                    _despawnedScratch.Add(existing);
                    if (existing.HasLight)
                    {
                        existing.ClearLight();
                        NoteLight(existing);
                    }
                }

                existing = new ReplicaEntity(this, path, e, snapshot.Tick);
                _spawnedScratch.Add(existing);
                if (existing.HasLight)
                    NoteLight(existing);
            }

            next.Add(e.Id, existing);
            _ordered.Add(existing);
        }

        foreach (ReplicaEntity gone in _byId.Values)
        {
            _despawnedScratch.Add(gone);
            if (gone.HasLight)
            {
                gone.ClearLight();
                NoteLight(gone);
            }
        }
        _byId.Clear();
        foreach (KeyValuePair<long, ReplicaEntity> pair in next)
            _byId.Add(pair.Key, pair.Value);
        _orderedStale = false;
        _hasSnapshot = true;
        _desynced = false;
        _tick = snapshot.Tick;
        _motionTick = -1; // version 42
        _lastInputSeq = snapshot.LastInputSeq;
        _ownEntity = snapshot.OwnEntity;
        ReplaceTables(snapshot);
        ApplyOwn(snapshot.Own, snapshot.Tick, snapshot.LastInputSeq, true);
        SnapshotsApplied++;
        Raise();
        ResetGuesses();
    }

    /// <summary>Applies one tick's changes: new types, spawned, changed, despawned; records a sample for every entity whose position, direction or glide changed.</summary>
    public void Apply(Delta delta)
    {
        if (delta == null) throw new ArgumentNullException(nameof(delta));
        if (_desynced)
            throw Bad("the replica is out of sync and waits for a Snapshot");
        try
        {
            ApplyDelta(delta);
        }
        catch (ProtocolException)
        {
            _desynced = true;
            _spawnedScratch.Clear();
            _despawnedScratch.Clear();
            throw;
        }

        Raise();
    }

    /// <summary>
    /// Decodes a State channel message and applies it when it is a MapGrid, Snapshot, Delta, (version 6) Airflow, (version 13) Sounds, (version 15) RadialMenu, (version 16) UiWindow, (version 19) Tooltip or (version 100) Hotbar and Storages, among others. Returns false with the
    /// error for an undecodable message, and with <see cref="ProtocolError.WrongMessage"/> for any other id (a
    /// <see cref="Welcome"/> is the caller's). A desync still throws (<see cref="Apply(Delta)"/>).
    /// </summary>
    public bool TryApply(ReadOnlySpan<byte> message, out MessageId id, out ProtocolError error)
    {
        if (!Codec.TryPeekId(message, out id))
        {
            error = message.Length == 0 ? ProtocolError.Truncated : ProtocolError.UnknownMessage;
            return false;
        }

        switch (id)
        {
            case MessageId.MapGrid:
                if (!Codec.TryDecode(message, out MapGrid grid, out error))
                    return false;
                Apply(grid);
                return true;
            case MessageId.Snapshot:
                if (!Codec.TryDecode(message, out Snapshot snapshot, out error))
                    return false;
                Apply(snapshot);
                return true;
            case MessageId.Delta:
                if (!Codec.TryDecode(message, out Delta delta, out error))
                    return false;
                Apply(delta);
                return true;
            case MessageId.Airflow:
                if (!Codec.TryDecode(message, out AirflowUpdate airflow, out error))
                    return false;
                Apply(airflow);
                return true;
            case MessageId.Sounds:
                if (!Codec.TryDecode(message, out Sounds sounds, out error))
                    return false;
                Apply(sounds);
                return true;
            case MessageId.Radial:
                if (!Codec.TryDecode(message, out RadialMenuMessage radial, out error))
                    return false;
                Apply(radial);
                return true;
            case MessageId.UiWindow:
                if (!Codec.TryDecode(message, out UiWindowMessage uiWindow, out error))
                    return false;
                Apply(uiWindow);
                return true;
            case MessageId.Tooltip:
                if (!Codec.TryDecode(message, out TooltipMessage tooltip, out error))
                    return false;
                Apply(tooltip);
                return true;
            case MessageId.CameraShake:
                if (!Codec.TryDecode(message, out CameraShakeMessage shake, out error))
                    return false;
                Apply(shake);
                return true;
            case MessageId.HeldMotion: // version 54
                if (!Codec.TryDecode(message, out HeldMotionMessage held, out error))
                    return false;
                Apply(held);
                return true;
            case MessageId.RadioChannels: // version 40
                if (!Codec.TryDecode(message, out RadioChannelsMessage radio, out error))
                    return false;
                Apply(radio);
                return true;
            case MessageId.Motion: // version 42
                if (!Codec.TryDecode(message, out MotionMessage motion, out error))
                    return false;
                Apply(motion);
                return true;
            case MessageId.Hotbar: // version 100 also (DV693)
                if (!Codec.TryDecode(message, out HotbarMessage hotbar, out error))
                    return false;
                Apply(hotbar);
                return true;
            case MessageId.Storages: // version 100 also (DV696)
                if (!Codec.TryDecode(message, out StoragesMessage storages, out error))
                    return false;
                Apply(storages);
                return true;
            default:
                error = ProtocolError.WrongMessage;
                return false;
        }
    }

    private void ApplyDelta(Delta delta)
    {
        if (!_hasSnapshot)
            throw Bad("a Delta arrives before any Snapshot");
        if (delta.Tick != _tick + 1)
            throw Bad("a Delta of tick " + delta.Tick + " follows tick " + _tick + " (a gap or a repeat)");
        // FirstNewType says where NewTypes goes; with no new types it carries nothing and is not checked.
        if (delta.NewTypes.Count > 0 && delta.FirstNewType != _types.Count)
            throw Bad("a Delta adds types from index " + delta.FirstNewType + " to a table of " + _types.Count);
        _types.AddRange(delta.NewTypes);
        AppendTables(delta);

        foreach (EntityState e in delta.Spawned)
        {
            CheckState(e, _types.Count, _appearances.Count);
            var entity = new ReplicaEntity(this, _types[e.Type], e, delta.Tick);
            if (!_byId.TryAdd(e.Id, entity))
                throw Bad("entity " + e.Id + " spawns twice");
            _ordered.Add(entity);
            if (_ordered.Count > 1 && _ordered[^2].Id > e.Id)
                _orderedStale = true;
            _spawnedScratch.Add(entity);
            if (entity.HasLight)
                NoteLight(entity);
        }

        foreach (EntityChange change in delta.Changed)
        {
            if (!_byId.TryGetValue(change.Id, out ReplicaEntity entity))
                throw Bad("entity " + change.Id + " changes but does not exist");
            LightState before = entity.Light;
            entity.Change(change, delta.Tick, _map, _appearances.Count);
            if ((change.Mask & NetField.Light) != 0 && before != entity.Light)
                NoteLight(entity);
        }

        foreach (long id in delta.Despawned)
        {
            if (!_byId.Remove(id, out ReplicaEntity entity))
                throw Bad("entity " + id + " despawns but does not exist");
            _orderedStale = true;
            _despawnedScratch.Add(entity);
            if (entity.HasLight)
            {
                entity.ClearLight();
                NoteLight(entity);
            }
        }

        ApplyCellChanges(delta);
        _tick = delta.Tick;
        _lastInputSeq = delta.LastInputSeq;
        _ownEntity = delta.OwnEntity;
        if (_despawnedScratch.Count > 0)
            _ordered.RemoveAll(e => !_byId.TryGetValue(e.Id, out ReplicaEntity live) || !ReferenceEquals(live, e));
        ApplyOwn(delta.Own, delta.Tick, delta.LastInputSeq, false);
    }

    private void CheckState(in EntityState e, int typeCount, int appearanceCount)
    {
        if (e.Type < 0 || e.Type >= typeCount)
            throw Bad("entity " + e.Id + " has type index " + e.Type + " in a table of " + typeCount);
        CheckPosition(e.X, e.Y, e.Z, _map);
        if (e.Appearance.Private || e.Appearance.Index > appearanceCount)
            throw Bad("entity " + e.Id + " has appearance " + e.Appearance.Index + " in a table of " + appearanceCount);
        if (e.FlickAppearance.Private || e.FlickAppearance.Index > appearanceCount)
            throw Bad("entity " + e.Id + " flicks appearance " + e.FlickAppearance.Index + " in a table of " + appearanceCount);
        if (e.HasPos)
            CheckPosition(MotionMath.TileOf(e.PosX), MotionMath.TileOf(e.PosY), e.Z, _map);
        if ((e.Flags & ~EntityFlags.All) != 0)
            throw Bad("entity " + e.Id + " sets entity flag bits that are not defined");
    }

    internal static void CheckPosition(int x, int y, int z, MapGrid map)
    {
        if (map != null)
        {
            if (x < 1 || x > map.Width || y < 1 || y > map.Height || z < 1 || z > map.Levels)
                throw Bad("a position " + x + "," + y + "," + z + " is outside the map");
        }
        else if (x < 1 || x > Limits.MaxMapSize || y < 1 || y > Limits.MaxMapSize || z < 1 || z > Limits.MaxMapLevels)
        {
            throw Bad("a position " + x + "," + y + "," + z + " is outside the limits");
        }
    }

    private void Order()
    {
        if (!_orderedStale)
            return;
        _ordered.Sort((a, b) => a.Id.CompareTo(b.Id));
        _orderedStale = false;
    }

    private void Raise()
    {
        // Events fire once the table is complete, so a handler sees the whole tick: the entities first, then the cells, then
        // the private block's (the own state last: the predictor's reconciliation point).
        if (_spawnedScratch.Count > 0 || _despawnedScratch.Count > 0)
        {
            ReplicaEntity[] spawned = _spawnedScratch.ToArray();
            ReplicaEntity[] despawned = _despawnedScratch.ToArray();
            _spawnedScratch.Clear();
            _despawnedScratch.Clear();
            foreach (ReplicaEntity entity in despawned)
                if (_hidden.Count == 0 || !_hidden.Remove(entity.Id)) // version 43: a hidden entity's despawn was raised when it was hidden
                    Despawned?.Invoke(entity);
            foreach (ReplicaEntity entity in spawned)
                Spawned?.Invoke(entity);
        }

        RaiseV2();
        RaiseV3();
        RaiseV5();
        RaiseV96();
    }

    private static ProtocolException Bad(string message) => new(ProtocolError.BadValue, message);
}

/// <summary>
/// The glide model of <see cref="ReplicaEntity.TryInterpolate"/> (scope 2g). The server moves an entity a whole tile in one
/// tick; the client draws it gliding to the new tile from the render time of that tick (BYOND's own glide), over
/// <c>32 / glide_size</c> ticks, or, when <c>glide_size</c> is 0, over the interval since the previous move.
/// </summary>
public static class EntityGlide
{
    /// <summary>A glide size is pixels per tick; a tile is 32 pixels (the 32 of tg's <c>ICON_SIZE_ALL</c>).</summary>
    public const float TilePixels = 32f;

    /// <summary>A move of more than this many tiles (Chebyshev) is a teleport: no glide.</summary>
    public const float TeleportTiles = 2.5f;

    /// <summary>The shortest glide, in ticks.</summary>
    public const float MinGlideTicks = 1f;

    /// <summary>The longest glide, in ticks.</summary>
    public const float MaxGlideTicks = 10f;

    /// <summary>The glide of an entity whose glide size is 0 and whose previous move is unknown, in ticks.</summary>
    public const float DefaultGlideTicks = 3f;

    /// <summary>The length of a glide in ticks for a replicated <paramref name="glideSize"/> and the ticks since the previous move (0 or less: unknown).</summary>
    public static float GlideTicks(float glideSize, long ticksSincePreviousMove)
    {
        float ticks;
        if (glideSize > 0f)
            ticks = TilePixels / glideSize;
        else if (ticksSincePreviousMove > 0)
            ticks = ticksSincePreviousMove;
        else
            ticks = DefaultGlideTicks;
        return Math.Clamp(ticks, MinGlideTicks, MaxGlideTicks);
    }
}

/// <summary>An entity of a <see cref="ReplicaWorld"/>: its newest state and a ring of samples (newest first).</summary>
public sealed partial class ReplicaEntity
{
    private readonly ReplicaWorld _world;
    private readonly string _typePath;
    private EntityState _state;
    private long _spawnTick;
    private EntitySample[] _ring;
    private int _head;
    private int _count;

    internal ReplicaEntity(ReplicaWorld world, string typePath, in EntityState state, long tick)
    {
        _world = world;
        _typePath = typePath;
        _spawnTick = tick;
        _ring = new EntitySample[Math.Min(2, world.History)];
        Reset(state, tick);
    }

    /// <summary>The entity's id (<see cref="NetField.Id"/>).</summary>
    public long Id => _state.Id;

    /// <summary>The DM type path (from the type table).</summary>
    public string TypePath => _typePath;

    /// <summary>The newest values of every field.</summary>
    public EntityState State => _world.HasPlaceGuesses ? _world.Guessed(_state) : _state;

    /// <summary>The state as the server sent it, without the client's guess of where the entity is (version 56, <see cref="ReplicaWorld.GuessPlace"/>).</summary>
    public EntityState ServerState => _state;

    /// <summary>True for the connection's own mob (<see cref="NetField.OwnMob"/>).</summary>
    public bool IsOwn => _world.OwnEntity == _state.Id;

    /// <summary>The tick at which the entity appeared.</summary>
    public long SpawnTick => _spawnTick;

    /// <summary>Samples kept (1 to the world's <see cref="ReplicaWorld.History"/>).</summary>
    public int SampleCount => _count;

    /// <summary>A kept sample: <paramref name="age"/> 0 is the newest.</summary>
    public EntitySample Sample(int age)
    {
        if (age < 0 || age >= _count)
            throw new ArgumentOutOfRangeException(nameof(age), age, "the entity keeps " + _count + " samples");
        int index = _head - age;
        if (index < 0)
            index += _ring.Length;
        return _ring[index];
    }

    /// <summary>
    /// The two kept samples around <paramref name="renderTick"/> (a server tick with a fraction, scope 2g: the latest tick
    /// minus about 2) and the fraction <paramref name="t"/> (0 to 1) between them. Before the oldest sample both are the
    /// oldest, after the newest both are the newest (no extrapolation). False when no sample is kept.
    /// </summary>
    public bool TrySample(double renderTick, out EntitySample from, out EntitySample to, out float t)
    {
        t = 0f;
        if (_count == 0)
        {
            from = to = default;
            return false;
        }

        EntitySample newest = Sample(0);
        EntitySample oldest = Sample(_count - 1);
        if (renderTick >= newest.Tick)
        {
            from = to = newest;
            return true;
        }

        if (renderTick < oldest.Tick)
        {
            from = to = oldest;
            return true;
        }

        for (int age = 0; age < _count - 1; age++)
        {
            EntitySample later = Sample(age);
            EntitySample earlier = Sample(age + 1);
            if (earlier.Tick <= renderTick)
            {
                from = earlier;
                to = later;
                t = (float)((renderTick - earlier.Tick) / (later.Tick - earlier.Tick));
                return true;
            }
        }

        from = to = oldest;
        return true;
    }

    /// <summary>
    /// The entity as it is drawn at <paramref name="renderTick"/> (a server tick with a fraction): a step that the samples
    /// show starts at the tick of its sample and glides to the new tile over <see cref="EntityGlide.GlideTicks"/> ticks; a
    /// step that arrives while a glide runs starts from where the entity is drawn then, so a walk of one tile per 3 ticks
    /// is continuous and a run of one per tick is too; a move of more than <see cref="EntityGlide.TeleportTiles"/> tiles or
    /// to another level is a teleport. The direction changes at the tick of its sample. Nothing is extrapolated: after the
    /// newest sample the entity stays where it is, and before the oldest it is at the oldest. False when no sample is kept.
    /// </summary>
    public bool TryInterpolate(double renderTick, out EntityPose pose)
    {
        if (_count == 0)
        {
            pose = default;
            return false;
        }

        EntitySample first = Sample(_count - 1);
        float startX = first.X, startY = first.Y, endX = first.X, endY = first.Y;
        double startTick = first.Tick;
        float glide = 1f;
        long previousMoveTick = -1;
        int z = first.Z;
        byte dir = first.Dir;
        EntitySample previous = first;
        for (int age = _count - 2; age >= 0; age--)
        {
            EntitySample s = Sample(age);
            if (s.Tick > renderTick)
                break;
            dir = s.Dir;
            bool moved = s.X != previous.X || s.Y != previous.Y || s.Z != previous.Z;
            if (moved)
            {
                // Where the entity is drawn at the tick of the move, before this move changes anything.
                Lerp(startX, startY, endX, endY, startTick, glide, s.Tick, out float visibleX, out float visibleY);
                float jump = Math.Max(Math.Abs(s.X - previous.X), Math.Abs(s.Y - previous.Y));
                if (s.Z != previous.Z || jump > EntityGlide.TeleportTiles)
                {
                    startX = endX = s.X;
                    startY = endY = s.Y;
                    glide = 1f;
                }
                else
                {
                    startX = visibleX;
                    startY = visibleY;
                    endX = s.X;
                    endY = s.Y;
                    glide = EntityGlide.GlideTicks(s.GlideSize, previousMoveTick < 0 ? -1 : s.Tick - previousMoveTick);
                }

                startTick = s.Tick;
                previousMoveTick = s.Tick;
                z = s.Z;
            }

            previous = s;
        }

        Lerp(startX, startY, endX, endY, startTick, glide, renderTick, out float x, out float y);
        pose = new EntityPose(x, y, z, dir);
        return true;
    }

    private static void Lerp(float sx, float sy, float ex, float ey, double startTick, float glide, double at, out float x, out float y)
    {
        double t = (at - startTick) / glide;
        if (t <= 0)
        {
            x = sx;
            y = sy;
        }
        else if (t >= 1)
        {
            x = ex;
            y = ey;
        }
        else
        {
            x = (float)(sx + (ex - sx) * t);
            y = (float)(sy + (ey - sy) * t);
        }
    }

    /// <summary>A Snapshot sets the whole state again: the history restarts at this one sample (a resync is no motion).</summary>
    internal void Reset(in EntityState state, long tick)
    {
        _state = state;
        ForgetFlights(); // version 81: a resync is no motion
        _motionTick = tick; // version 42: a Motion must be newer than the state set here
        _count = 0;
        _head = 0;
        Push(tick);
    }

    /// <summary>Applies a Delta's change of this entity at <paramref name="tick"/>.</summary>
    internal void Change(in EntityChange change, long tick, MapGrid map, int appearanceCount)
    {
        NetField mask = change.Mask;
        int x = (mask & NetField.X) != 0 ? change.X : _state.X;
        int y = (mask & NetField.Y) != 0 ? change.Y : _state.Y;
        int z = (mask & NetField.Z) != 0 ? change.Z : _state.Z;
        if ((mask & (NetField.X | NetField.Y | NetField.Z)) != 0)
            ReplicaWorld.CheckPosition(x, y, z, map);
        if ((mask & NetField.Pos) != 0)
            ReplicaWorld.CheckPosition(MotionMath.TileOf(change.PosX), MotionMath.TileOf(change.PosY), z, map);
        if ((mask & NetField.Appearance) != 0 && (change.Appearance.Private || change.Appearance.Index > appearanceCount))
            throw new ProtocolException(ProtocolError.BadValue, "entity " + _state.Id + " changes to appearance " + change.Appearance.Index + " in a table of " + appearanceCount);
        if ((mask & NetField.Flick) != 0 && (change.FlickAppearance.Private || change.FlickAppearance.Index > appearanceCount))
            throw new ProtocolException(ProtocolError.BadValue, "entity " + _state.Id + " flicks appearance " + change.FlickAppearance.Index + " in a table of " + appearanceCount);
        EntityState next = _state with
        {
            X = x,
            Y = y,
            Z = z,
            Dir = (mask & NetField.Dir) != 0 ? change.Dir : _state.Dir,
            Name = (mask & NetField.Name) != 0 ? change.Name : _state.Name,
            GlideSize = (mask & NetField.GlideSize) != 0 ? change.GlideSize : _state.GlideSize,
            LastMoveTick = (mask & NetField.LastMoveTick) != 0 ? change.LastMoveTick : _state.LastMoveTick,
            Yaw = (mask & NetField.Yaw) != 0 ? change.Yaw : _state.Yaw,
            Posture = (mask & NetField.Posture) != 0 ? change.Posture : _state.Posture,
            Appearance = (mask & NetField.Appearance) != 0 ? change.Appearance : _state.Appearance,
            Light = (mask & NetField.Light) != 0 ? change.Light : _state.Light,
            Flags = (mask & NetField.Flags) != 0 ? change.Flags : _state.Flags,
            FlickAppearance = (mask & NetField.Flick) != 0 ? change.FlickAppearance : _state.FlickAppearance,
            FlickTick = (mask & NetField.Flick) != 0 ? change.FlickTick : _state.FlickTick,
            Animation = (mask & NetField.Animation) != 0 ? change.Animation : _state.Animation,
            IsVisual = _state.IsVisual || (mask & NetField.VisHost) != 0,
            VisHost = (mask & NetField.VisHost) != 0 ? change.VisHost : _state.VisHost,
            Shot = (mask & NetField.Shot) != 0 ? change.Shot : _state.Shot,
            Door = (mask & NetField.Door) != 0 ? change.Door : _state.Door,
            Ride = (mask & NetField.Ride) != 0 ? change.Ride : _state.Ride,
            Tram = (mask & NetField.Tram) != 0 ? change.Tram : _state.Tram,
            Flight = (mask & NetField.Flight) != 0 ? change.Flight : _state.Flight,
        };
        if ((mask & NetField.Pos) != 0)
            next = next with { HasPos = true, PosX = change.PosX, PosY = change.PosY };
        else if (!next.HasPos && (mask & (NetField.X | NetField.Y)) != 0)
            next = next with { PosX = x * MotionMath.SubunitsPerTile, PosY = y * MotionMath.SubunitsPerTile };
        if (next.Flight != _state.Flight)
            KeepFlight(_state.Flight); // version 81: the segment it replaces is drawn until the new one starts
        _state = next;
        if ((mask & ~(NetField.Name | NetField.Light | NetField.Flags | NetField.Flick | NetField.Animation | NetField.VisHost | NetField.Shot | NetField.Door | NetField.Ride | NetField.Tram | NetField.Flight)) != 0)
            Push(tick);
    }

    private void Push(long tick)
    {
        // Version 42: a Motion can be ahead of the Delta that changes the entity's other fields; the samples stay in tick order.
        if (_count > 0 && tick < _ring[_head].Tick)
            tick = _ring[_head].Tick;
        var sample = new EntitySample(tick, _state.X, _state.Y, _state.Z, _state.Dir, _state.GlideSize, _state.LastMoveTick)
        {
            PosX = _state.PosX,
            PosY = _state.PosY,
            Yaw = _state.Yaw,
            Posture = _state.Posture,
            Appearance = _state.Appearance,
        };
        if (_count > 0 && _ring[_head].Tick == tick)
        {
            _ring[_head] = sample;
            return;
        }

        if (_count == _ring.Length && _ring.Length < _world.History)
        {
            var bigger = new EntitySample[Math.Min(_ring.Length * 2, _world.History)];
            for (int i = 0; i < _count; i++)
                bigger[i] = Sample(_count - 1 - i);
            _ring = bigger;
            _head = _count - 1;
        }

        _head = _count == 0 ? 0 : (_head + 1) % _ring.Length;
        _ring[_head] = sample;
        if (_count < _ring.Length)
            _count++;
    }
}
