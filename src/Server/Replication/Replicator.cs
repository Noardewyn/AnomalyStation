using System.Runtime.CompilerServices;
using System.Linq;
using static Anomaly.Core.Tg.Defines;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Owner: M1 P4 (replication), M2 P5 (protocol version 2: appearances, positions, private blocks, the cells). The mechanism is
// R3 of scope 2d (D99), documented with its measurements in docs/ai/specs/replication.md.

/// <summary>What one <see cref="Replicator.BuildDelta"/> did, in more detail than <see cref="ReplicationStats"/> (diagnostics and measurements).</summary>
/// <param name="Tick">The tick.</param>
/// <param name="Hinted">Distinct candidates named by a moved, appearance or visibility hint.</param>
/// <param name="Created">Distinct candidates found above the registry's id watermark (new movables).</param>
/// <param name="Deleted">Distinct candidates named by a deleted hint (only ids that were entities).</param>
/// <param name="Swept">Distinct candidates the round-robin sweep visited.</param>
/// <param name="Candidates">Distinct candidates compared (the union).</param>
/// <param name="Spawned">Entities spawned.</param>
/// <param name="Changed">Entities changed.</param>
/// <param name="Despawned">Entities despawned.</param>
/// <param name="SweepCatches">Changed entities that only the sweep had named (no hint): the changes the hints missed.</param>
/// <param name="SweepCaughtFields">The fields of those changes.</param>
/// <param name="NewTypes">Type table entries added.</param>
/// <param name="BodyBytes">The encoded body's size.</param>
/// <param name="Polled">Capsule mobs read this tick (version 2).</param>
/// <param name="NewAppearances">Public appearance table entries added (version 2).</param>
/// <param name="CellChanges">Cells whose solidity or turf appearance changed (version 2).</param>
public readonly record struct ReplicationDetail(
    long Tick, int Hinted, int Created, int Deleted, int Swept, int Candidates,
    int Spawned, int Changed, int Despawned, int SweepCatches, NetField SweepCaughtFields, int NewTypes, int BodyBytes,
    int Polled = 0, int NewAppearances = 0, int CellChanges = 0);

/// <summary>A change the sweep found and no hint had named: the entity and the fields.</summary>
public readonly record struct SweepCatch(long Id, NetField Fields);

/// <summary>
/// The server's replicator, R3 of scope 2d (D99): engine hints and the registry's id watermark select candidate objects
/// each tick, shadow copies of the whitelisted fields (<see cref="ReplicatedFields"/>) decide what changed, a round-robin
/// sweep compares a slice of the entities every tick whatever the hints said, and every changeable field of every entity
/// carries the tick it last changed (the per-client baselines of M10). It follows <see cref="IReplicator"/>'s read rules:
/// it reads after <c>World.Step</c>, outside the world, never writes it, keeps its state outside it (shadows hold values
/// and ids, the hint sink records ids), so a world's state hash is the same with and without it.
/// <para>
/// Entities are the movables standing directly on a turf (<see cref="ReplicatedFields.EntityRule"/>); container contents
/// are not replicated (M4). One <see cref="Delta"/> body per tick is encoded once for every session; the per-session parts
/// are the header and the private block (<see cref="BuildPrivate"/>) the host writes (<c>Codec.EncodeDelta</c>), so nothing
/// in the shared body depends on a session.
/// </para>
/// <para>
/// Version 2 adds: the public appearance, resource and string tables (every entity's appearance is interned from the atom,
/// <see cref="AppearanceTables"/>); <c>Pos</c>, <c>Yaw</c> of capsule mobs every tick they change and <c>Posture</c> of living
/// mobs; the map's cells (the turf appearance and the solidity bit of every tile, kept current and sent as cell changes);
/// and the per-session private block (the own motion state, the input buffer depth, the <c>client.screen</c> entries in the
/// session's private tables).
/// </para>
/// </summary>
public sealed partial class Replicator : IReplicator
{
    // Where a candidate came from (combined when several sources name the same id in one tick).
    private const byte FromHint = 1;
    private const byte FromDeleted = 2;
    private const byte FromCreated = 4;
    private const byte FromSweep = 8;
    private const byte FromPoll = 16;
    private const byte FromAppearance = 32;

    private const NetField Position = NetField.X | NetField.Y | NetField.Z;

    private readonly IReplicationEncoder _encoder;
    private readonly ILocomotionView _locomotion;
    private readonly int _sweepDivisor;
    private readonly int _minSweep;
    private readonly int _cellSweep;
    private readonly int _screenSweepPeriod;

    private World _world;
    private LevelRides _rides; // DV371: the world's announced level rides
    private TramRides _trams; // DV403: the world's announced tram trips
    private FlightSeam _flights; // DV416: the world's free-motion bodies
    private ObjectRegistry _registry;

    // The shadows: one slot per entity, stable while it lives (the sweep walks slot indexes), free slots reused.
    private Shadow[] _slots = new Shadow[64];
    private int _slotCount; // slots in use or freed (the high-water mark); the sweep walks [0, _slotCount)
    private readonly Stack<int> _free = new();
    private readonly Dictionary<long, int> _index = new();
    private int _sweepCursor;
    private readonly List<int> _mobSlots = new(); // the slots of the mob entities, polled every tick for capsule state

    // Hints of the running tick, recorded by the sink inside the world (ids only), consumed by BuildDelta.
    private readonly List<Candidate> _pending = new();
    private readonly List<Candidate> _candidates = new();
    private readonly List<long> _pendingTurfs = new();
    private readonly HashSet<long> _appearanceHinted = new(); // ids named by an appearance hint in the last BuildDelta (the screen objects' check)
    private readonly HashSet<long> _maptextHinted = new(); // version 18: ids whose maptext (version 29: or filters) changed since the last BuildDelta (joins _appearanceHinted for the screen objects and the game plane only)
    private bool _mapChanged = true;
    private long _watermark;
    private long _stateTick = -1;

    // The run's entity type table (Snapshot.Types, Delta.NewTypes): append-only, an index per DM path at its first replication.
    private readonly List<string> _types = new();
    private readonly Dictionary<Type, int> _typeIndex = new();

    // The public appearance, resource and string tables (version 2).
    private readonly AppearanceTables _public = new(false);

    // The map's cells: the solidity bit and the public turf appearance of every tile, as the clients have them.
    private int _width, _height, _levels;
    private byte[] _cellSolid;
    private uint[] _cellAppearance;
    private bool[] _cellMarked;
    private readonly List<int> _dirtyCells = new();
    private int _cellCursor;

    // Version 3: the flags byte, the base-light index and the turf type of every tile as the clients have them, with the
    // append-only base-light and turf type tables (a Delta extends them; a join sends them whole).
    private byte[] _cellFlags;
    private ushort[] _cellBase;
    private int[] _cellTurfType;
    private readonly List<BaseLight> _baseLights = new();
    private readonly Dictionary<uint, int> _baseLightIndex = new(); // rgba -> table index (1-based)
    private readonly List<string> _turfTypePaths = new();
    private readonly Dictionary<Type, int> _turfTypeIndex = new();
    private (float Range, float Power, uint Rgba) _starlight;
    private int _baseLightsBefore, _turfTypesBefore;
    // Version 5: the gas every tile shows as the clients have it (null: none), the fixed table of gas kinds (tg's gas overlay
    // icon states) and the scratch list of a cell's overlays.
    private GasLevel[][] _cellGas;
    private readonly List<string> _gasKinds = new();
    private readonly Dictionary<string, int> _gasKindIndex = new();
    private readonly List<(string State, int Level)> _gasScratch = new();
    private readonly List<int> _cellPollSlots = new(); // entities that open and close (doors): their cell flags are polled every tick
    private readonly HashSet<long> _hits = new(); // the entities tg showed an attack visual on since the last read (HitEffectSeam)
    private readonly Dictionary<long, byte> _lunges = new(); // the attack lunges (tg's do_attack_animation) since the last read: id to BYOND dir
    private readonly Dictionary<long, object> _flicks = new(); // version 8: the flick(state, atom) calls since the last read: id to the flicked icon state or icon
    private readonly AnimationRecorder _animations = new(); // version 9: the animate() chains (AnimationRecorder)
    private readonly List<long> _animationScratch = new();
    private readonly Dictionary<long, atom> _visuals = new(); // version 10: the flick visuals (flick_overlay_view) seen in a host's vis_contents: id to host, polled every tick
    private readonly List<atom> _visHolders = new(); // version 10: the atoms whose vis_contents were written since the last read
    private readonly Dictionary<long, List<atom>> _visShownIn = new(); // an atom's id to the holders whose vis_contents held it when last read
    private readonly HashSet<long> _visHolderReads = new(); // the holders re-read for a changed look in this BuildDelta (a cycle ends)
    private readonly HashSet<long> _flagged = new(); // version 4: ids whose last sent entity flags are not None (a throw ends without a hint): polled every tick
    private static readonly string[] CellPollPrefixes = { "/obj/machinery/door", "/obj/structure/mineral_door", "/obj/structure/plasticflaps" };

    private readonly Delta _delta = new();
    private byte[] _body = new byte[1024]; // the Delta body of the last tick (DeltaFrame.Body is valid until the next BuildDelta)
    private byte[] _mapGridBytes;
    private readonly List<SweepCatch> _sweepCatches = new();

    // The private state of every session that joined (D40: its tables and screen shadow are its own).
    private readonly Dictionary<int, SessionState> _sessions = new();
    private int _polled;

    /// <summary>A replicator with the default options (the protocol codec, a sweep of 1/64 per tick and at least 32).</summary>
    public Replicator()
        : this(new ReplicatorOptions())
    {
    }

    public Replicator(ReplicatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _encoder = options.Encoder;
        _locomotion = options.Locomotion;
        _sweepDivisor = options.SweepDivisor;
        _minSweep = options.MinSweepPerTick;
        _cellSweep = options.CellSweepPerTick;
        _screenSweepPeriod = options.ScreenSweepPeriodTicks;
        _interest = options.Interest; // M10 slice C
    }

    /// <inheritdoc/>
    public ReplicationStats LastStats { get; private set; }

    /// <summary>The counters of the last <see cref="BuildDelta"/>, in detail.</summary>
    public ReplicationDetail LastDetail { get; private set; }

    /// <summary>The changes of the last <see cref="BuildDelta"/> that only the sweep found (no hint named the entity).</summary>
    public IReadOnlyList<SweepCatch> LastSweepCatches => _sweepCatches;

    /// <summary>The tick the shadows hold: the attach tick, then the tick of the last <see cref="BuildDelta"/>; -1 before <see cref="Attach"/>.</summary>
    public long StateTick => _stateTick;

    /// <summary>The number of entities.</summary>
    public int EntityCount => _index.Count;

    /// <summary>The run's type table so far (index to DM type path).</summary>
    public IReadOnlyList<string> Types => _types;

    /// <summary>The public resource table so far (index 1 is element 0).</summary>
    public IReadOnlyList<string> Resources => _public.Resources;

    /// <summary>The public string table so far.</summary>
    public IReadOnlyList<string> Strings => _public.Strings;

    /// <summary>The public appearance table so far.</summary>
    public IReadOnlyList<AppearanceEntry> Appearances => _public.Appearances;

    /// <summary>Table entries that were not interned because a table was full or a string over its cap (version 2).</summary>
    public int TableOverflows => _public.Overflows;

    /// <summary>
    /// The longest time, in ticks, between two sweeps of one entity at the current number of slots: a change no hint names
    /// is replicated at most this many ticks late.
    /// </summary>
    public int SweepPeriodTicks
    {
        get
        {
            int perTick = SweepPerTick();
            return perTick == 0 ? 0 : (_slotCount + perTick - 1) / perTick;
        }
    }

    /// <summary>The longest time, in ticks, between two looks at one map cell by the cell sweep (0 when it is off).</summary>
    public int CellSweepPeriodTicks => _cellSweep == 0 || _cellSolid == null ? 0 : (CellCount + _cellSweep - 1) / _cellSweep;

    // ---- IReplicator ----

    private readonly ReplicatorOptions _options; // M10 slice H: a new round's replicator has the same

    /// <inheritdoc/>
    public IReplicator ForNewWorld()
    {
        // whoever still holds this one (the server's Main made it) must not keep the old world alive (M10 slice J): its references go
        _rides = null;
        _trams = null;
        _flights = null;
        _sun = null;
        _world = null;
        _registry = null;
        _visuals.Clear();
        _visHolders.Clear();
        _visShownIn.Clear();
        _animations.Clear();
        _slots = new Shadow[64];
        _free.Clear();
        _index.Clear();
        _sessions.Clear();
        _areaIndex.Clear();
        _cameraChains.Clear();
        _cameraChainTicks.Clear();
        _flicks.Clear();
        _pending.Clear();
        _candidates.Clear();
        return new Replicator(_options);
    }

    /// <inheritdoc/>
    public void Attach(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_world != null) throw new InvalidOperationException("the replicator is attached already");
        EnsureOutsideWorld();
        _world = world;
        _registry = world.Objects;
        _stateTick = world.Tick;
        PresentationSinks.Of(world).Replication = new HintSink(this);
        PresentationSinks.Of(world).Appearance = new LightHintSink(this, PresentationSinks.Of(world).Appearance);
        HitEffectSeam.Install(world, HitSink); // the owner's play test (2026-10-09): tg's attack visual as EntityFlags.Hit
        HeldMotionSeam.Install(world, HeldMotionSink); // version 54: the own gun's reload (DV213)

        // The full scan: every entity present, in id order (so the type table starts in id order).
        _watermark = _registry.HighestId;
        long cursor = 0;
        while (_registry.TryGetNextLive(cursor, out long id, out object obj))
        {
            cursor = id;
            if (ReplicatedFields.IsEntity(obj, out atom_movable movable, out turf on, _options.MapMarkers))
            {
                Spawn(id, movable, on, _stateTick, record: false);
                if (movable.vis_contents.Count > 0) NoteVisuals(movable); // M8x: a visual helper made at the map load (the cryo cell's occupant) spawns in the first read
            }
            if (obj is atom holder && holder.vis_contents.Count > 0) IndexVisChildren(holder);
        }
        BuildCells();
        InitializeCeilings();
        _public.BeginTick();
        _mapChanged = true;
    }

    /// <inheritdoc/>
    private datum_controller_subsystem_sun _sun;

    // tg's SSsun, found among Master's subsystems (TgState's subsystem fields are internal, translation-rules 10.6) and kept while it lives.
    private datum_controller_subsystem_sun SunOf(World world)
    {
        if (_sun != null && _sun.gc_destroyed == null)
            return _sun;
        _sun = null;
        foreach (datum_controller_subsystem_sun sun in Dm.each<datum_controller_subsystem_sun>(TgState.Of(world).Master?.subsystems))
            return _sun = sun;
        return _sun;
    }

    public DeltaFrame BuildDelta(long tick)
    {
        EnsureAttached();
        EnsureOutsideWorld();
        if (tick != _stateTick + 1 || tick != _world.Tick)
            throw new ArgumentOutOfRangeException(nameof(tick), tick, $"BuildDelta runs once per tick, in order, right after World.Step: expected tick {_stateTick + 1} (the world is at {_world.Tick})");

        PruneCameraChains(tick); // version 21
        PruneHeldMotions(tick); // version 54
        _delta.Spawned.Clear();
        _delta.Changed.Clear();
        _delta.Despawned.Clear();
        _layerChanged.Clear(); // version 22
        _tickSpawned.Clear(); // M10 slice C
        _tickMoved.Clear();
        _tickDespawned.Clear();
        _delta.NewTypes.Clear();
        _delta.NewResources.Clear();
        _delta.NewStrings.Clear();
        _delta.NewAppearances.Clear();
        _delta.SolidChanged.Clear();
        _delta.TurfAppearanceChanged.Clear();
        _delta.CellFlagsChanged.Clear();
        _delta.NewBaseLights.Clear();
        _delta.BaseLightChanged.Clear();
        _delta.NewTurfTypes.Clear();
        _delta.TurfTypeChanged.Clear();
        _delta.StarlightChanged = null;
        _delta.CellGasChanged.Clear();
        _delta.Tick = tick;
        _delta.LastInputSeq = 0;
        _delta.OwnEntity = 0;
        _delta.FirstNewType = _types.Count;
        _sweepCatches.Clear();
        _appearanceHinted.Clear();
        _appearanceHinted.UnionWith(_maptextHinted);
        _maptextHinted.Clear();
        _public.BeginTick();

        // 1. The candidates: the hints of the tick, the movables created since the watermark, a slice of the sweep, the capsule mobs.
        _candidates.Clear();
        _candidates.AddRange(_pending);
        _pending.Clear();
        // BYOND draws an atom in every holder whose vis_contents hold it (vis_locs), and a holder's picture flattens its vis_contents
        // (AppearanceTables.AddVisContents): a changed look re-reads its holders, and theirs (tg's greenscreen display, held in
        // nullspace and shown by every status display, status_display.dm:968-1006). The holders come from the replicator's own index of
        // the vis_contents it was told of: the world's vis_locs index fills lazily and is world state, which the replicator never writes.
        foreach (atom written in _visHolders)
            IndexVisChildren(written);
        for (int i = 0; i < _candidates.Count; i++)
        {
            Candidate c = _candidates[i];
            if ((c.Source & FromAppearance) == 0 || !_visShownIn.TryGetValue(c.Id, out List<atom> holders)) continue;
            for (int k = holders.Count - 1; k >= 0; k--)
            {
                atom holder = holders[k];
                if (!HoldsChild(holder, c.Id))
                {
                    holders.RemoveAt(k); // taken out since (a deleted holder's vis_contents were cut)
                    continue;
                }
                if (holder is turf holderTurf) _pendingTurfs.Add(holderTurf.dm_id);
                else if (holder.dm_id != 0 && _visHolderReads.Add(holder.dm_id)) _candidates.Add(new Candidate(holder.dm_id, (byte)(FromHint | FromAppearance)));
            }
            if (holders.Count == 0) _visShownIn.Remove(c.Id);
        }
        _visHolderReads.Clear();
        long high = _registry.HighestId;
        if (high > _watermark)
        {
            long cursor = _watermark;
            while (_registry.TryGetNextLive(cursor, out long id, out object obj))
            {
                cursor = id;
                if (obj is atom_movable) _candidates.Add(new Candidate(id, FromCreated));
                else if (obj is turf newTurf) MarkTurf(newTurf);
            }
            _watermark = high;
        }
        int sweep = Math.Min(SweepPerTick(), _slotCount);
        for (int n = 0; n < sweep; n++)
        {
            if (_sweepCursor >= _slotCount) _sweepCursor = 0;
            long id = _slots[_sweepCursor++].Id;
            if (id != 0) _candidates.Add(new Candidate(id, FromSweep));
        }
        _polled = 0;
        for (int i = 0; i < _mobSlots.Count; i++)
        {
            ref Shadow m = ref _slots[_mobSlots[i]];
            if (m.PosMode || _locomotion.TryGetCapsule(_world, m.Id, out _))
            {
                _candidates.Add(new Candidate(m.Id, FromPoll));
                _polled++;
            }
        }
        for (int i = 0; i < _cellPollSlots.Count; i++)
        {
            _candidates.Add(new Candidate(_slots[_cellPollSlots[i]].Id, FromPoll));
            _polled++;
        }
        foreach (long flaggedId in _flagged)
        {
            _candidates.Add(new Candidate(flaggedId, FromPoll));
            _polled++;
        }
        // DV371: a movable with an announced level ride, read until it is sent without one
        _rides ??= LevelRides.Of(_world);
        foreach (long ridingId in _rides.Riding)
        {
            _candidates.Add(new Candidate(ridingId, FromPoll));
            _polled++;
        }
        // DV403: a tram platform with an announced trip, read until it is sent without one
        _trams ??= TramRides.Of(_world);
        foreach (long tramId in _trams.Riding)
        {
            _candidates.Add(new Candidate(tramId, FromPoll));
            _polled++;
        }
        // DV416: a movable whose free motion changed, or moves, read until it is sent at rest or with none
        _flights ??= FlightSeam.Of(_world);
        foreach (long flyingId in _flights.Announced)
        {
            _candidates.Add(new Candidate(flyingId, FromPoll));
            _polled++;
        }
        _animationScratch.Clear();
        _animations.Due(tick, _animationScratch); // version 9: an animation that ended is sent as none
        foreach (long endedId in _animationScratch)
            if (_index.ContainsKey(endedId)) _candidates.Add(new Candidate(endedId, FromPoll));
        // Version 10: the flick visuals in the vis_contents written this tick join the known ones; every known one is polled
        // (a visual lives for a second or so and follows its host, which can move or leave the map).
        foreach (atom holder in _visHolders)
            NoteVisuals(holder);
        _visHolders.Clear();
        foreach (long visualId in _visuals.Keys)
        {
            _candidates.Add(new Candidate(visualId, FromPoll));
            _polled++;
        }
        foreach (long turfId in _pendingTurfs)
            if (_registry.TryGet(turfId, out object t) && t is turf hintedTurf) MarkTurf(hintedTurf);
        _pendingTurfs.Clear();

        // 2. Compare each candidate once, in increasing id order (so every list of the Delta is in id order).
        _candidates.Sort(CandidateOrder.Instance);
        int hinted = 0, created = 0, deleted = 0, swept = 0, distinct = 0;
        NetField sweepFields = NetField.None;
        for (int i = 0; i < _candidates.Count;)
        {
            long id = _candidates[i].Id;
            byte source = 0;
            for (; i < _candidates.Count && _candidates[i].Id == id; i++) source |= _candidates[i].Source;
            distinct++;
            if ((source & FromHint) != 0) hinted++;
            if ((source & FromCreated) != 0) created++;
            if ((source & FromDeleted) != 0) deleted++;
            if ((source & FromSweep) != 0) swept++;
            if ((source & FromAppearance) != 0) _appearanceHinted.Add(id);
            NetField changed = Examine(id, tick, source);
            if (changed != NetField.None && source == FromSweep)
            {
                _sweepCatches.Add(new SweepCatch(id, changed));
                sweepFields |= changed;
            }
        }
        _candidates.Clear();
        _stateTick = tick;
        // Version 9: a chain recorded for an id that is no entity after the reads (an item in a hand) is not kept; a screen object's is
        // (version 11: the private blocks read it).
        if (_animations.Changed.Count > 0)
        {
            _animationScratch.Clear();
            foreach (long animatedId in _animations.Changed)
                if (!_index.ContainsKey(animatedId) && !IsLiveScreenObject(animatedId)) _animationScratch.Add(animatedId);
            foreach (long gone in _animationScratch)
            {
                KeepTextChain(gone, tick);
                _animations.Forget(gone);
            }
        }

        // 3. The cells (and the starlight values), then the new table entries, then one body for every session in the replicator's reused buffer.
        int cellChanges = UpdateCells();
        ReadCeilingChanges();
        _delta.SunAzimuth = SunOf(_world) is { } sun ? sun.azimuth : float.NaN; // read raw, outside the world's step (protocol 47)
        var star = CellState.Starlight(_world);
        if (star != _starlight)
        {
            _starlight = star;
            _delta.StarlightChanged = StarlightState(star);
            _mapChanged = true;
        }
        FillNewTableEntries();
        int length = _encoder.EncodeDeltaBody(_delta, ref _body);
        LastStats = new ReplicationStats(tick, distinct, _index.Count, length);
        LastDetail = new ReplicationDetail(tick, hinted, created, deleted, swept, distinct,
            _delta.Spawned.Count, _delta.Changed.Count, _delta.Despawned.Count, _sweepCatches.Count, sweepFields,
            _delta.NewTypes.Count, length, _polled, _delta.NewAppearances.Count, cellChanges);
        return new DeltaFrame(tick, new ReadOnlyMemory<byte>(_body, 0, length), _delta.Spawned.Count, _delta.Changed.Count, _delta.Despawned.Count);
    }

    /// <inheritdoc/>
    public JoinMessages BuildJoin(long tick, long ownEntity, uint lastInputSeq)
    {
        EnsureAttached();
        EnsureOutsideWorld();
        CheckJoinTick(tick);
        return new JoinMessages(EncodeMapGrid(), _encoder.EncodeSnapshot(BuildSnapshot(ownEntity, lastInputSeq)));
    }

    /// <inheritdoc/>
    public JoinMessages BuildJoin(long tick, in PrivateInputs inputs)
    {
        EnsureAttached();
        EnsureOutsideWorld();
        CheckJoinTick(tick);
        // A join starts the session's private tables and screen shadow afresh: the Snapshot carries them whole.
        var session = new SessionState(_screenSweepPeriod);
        _sessions[inputs.Session] = session;
        session.Tables.BeginTick();
        session.Update(this, inputs, tick, full: true);
        Snapshot snapshot = BuildSnapshot(inputs.Body, inputs.LastInputSeq);
        snapshot.Own = session.Block;
        return new JoinMessages(EncodeMapGrid(), _encoder.EncodeSnapshot(snapshot));
    }

    /// <inheritdoc/>
    public PrivateFrame BuildPrivate(long tick, in PrivateInputs inputs)
    {
        EnsureAttached();
        EnsureOutsideWorld();
        if (tick != _stateTick)
            throw new ArgumentOutOfRangeException(nameof(tick), tick, $"a private block is built for the tick the shadows hold ({_stateTick}), after that tick's BuildDelta");
        if (!_sessions.TryGetValue(inputs.Session, out SessionState session))
        {
            // No join was built for this session (a host that skips it): start the tables at this block.
            session = new SessionState(_screenSweepPeriod);
            _sessions[inputs.Session] = session;
        }
        session.Tables.BeginTick();
        session.Update(this, inputs, tick, full: false);
        long need = Codec.MaxOwnBlockSize(session.Block);
        if (session.Buffer.Length < need) session.Buffer = new byte[Math.Max(need, session.Buffer.Length * 2L)];
        int length = Codec.EncodeOwnBlock(session.Block, session.Buffer);
        return new PrivateFrame(new ReadOnlyMemory<byte>(session.Buffer, 0, length), session.ScreenChanges, session.NewEntries);
    }

    /// <inheritdoc/>
    public void Forget(int session) => _sessions.Remove(session);

    /// <inheritdoc/>
    public IReadOnlyList<UiWindowMessage> TakeUiWindows(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingUi.Count == 0)
            return Array.Empty<UiWindowMessage>();
        var taken = state.PendingUi.ToArray();
        state.PendingUi.Clear();
        return taken;
    }

    /// <inheritdoc/>
    public void ForgetBrowse(int session, int window)
    {
        if (_sessions.TryGetValue(session, out SessionState state))
            state.ForgetBrowse(window);
    }

    /// <inheritdoc/>
    public IReadOnlyList<RadialMenuMessage> TakeRadials(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingRadials.Count == 0)
            return Array.Empty<RadialMenuMessage>();
        var taken = state.PendingRadials.ToArray();
        state.PendingRadials.Clear();
        return taken;
    }

    private void CheckJoinTick(long tick)
    {
        if (tick != _stateTick)
            throw new ArgumentOutOfRangeException(nameof(tick), tick, $"a join is built for the tick the shadows hold ({_stateTick}), after that tick's BuildDelta");
    }

    private byte[] EncodeMapGrid()
    {
        if (_mapChanged || _mapGridBytes == null)
        {
            _mapGridBytes = _encoder.EncodeMapGrid(ReadMapGrid());
            _mapChanged = false;
        }
        return _mapGridBytes;
    }

    // ---- the messages, before encoding (tests and the measurement harness read them too) ----

    /// <summary>
    /// The map as the world has it now (<see cref="MapView"/>): size and the run-length turf types, x fastest, then y, then
    /// z; and (version 2) the turf appearance runs and the solidity bits the replicator keeps current.
    /// </summary>
    public MapGrid ReadMapGrid()
    {
        EnsureAttached();
        EnsureOutsideWorld();
        var grid = new MapGrid
        {
            Tick = _stateTick,
            Width = MapView.Width(_world),
            Height = MapView.Height(_world),
            Levels = MapView.Levels(_world),
        };
        // The turf types come from the persistent table (the clients' table plus every Delta's additions): index order is the
        // replicator's, so a late joiner and a client that joined earlier agree on every TurfTypeChanged index.
        grid.TurfTypes.AddRange(_turfTypePaths);
        if (_cellTurfType != null && CellCount == grid.CellCount)
        {
            AddRuns(grid.Runs, _cellTurfType.Length, i => _cellTurfType[i]);
        }
        else
        {
            if (grid.TurfTypes.Count == 0) grid.TurfTypes.Add(PathOf(typeof(turf)));
            grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        }

        if (_cellSolid != null && CellCount == grid.CellCount)
        {
            grid.SolidBits = (byte[])_cellSolid.Clone();
            int runStart = 0;
            for (int i = 1; i <= _cellAppearance.Length; i++)
            {
                if (i < _cellAppearance.Length && _cellAppearance[i] == _cellAppearance[runStart]) continue;
                grid.AppearanceRuns.Add(new TurfRun(i - runStart, (int)_cellAppearance[runStart]));
                runStart = i;
            }

            AddRuns(grid.FlagRuns, _cellFlags.Length, i => _cellFlags[i]);
            grid.BaseLights.AddRange(_baseLights);
            AddRuns(grid.BaseLightRuns, _cellBase.Length, i => _cellBase[i]);
            for (int i = 0; i < _cellGas.Length; i++)
            {
                if (_cellGas[i] == null) continue;
                grid.GasCells.Add(new CellGasChange(i % _width + 1, i / _width % _height + 1, i / (_width * _height) + 1, _cellGas[i]));
            }
        }
        grid.GasKinds.AddRange(_gasKinds);
        foreach (var entry in _ceilingShadow.OrderBy(e => e.Key.Z).ThenBy(e => e.Key.Y).ThenBy(e => e.Key.X))
            grid.Ceilings.Add(entry.Value);

        grid.Starlight = StarlightState(_starlight);
        // read raw, outside the world's step (protocol 60): the levels with tg's ZTRAIT_NOPARALLAX (Lavaland)
        DmList levels = Dm.each<datum_controller_subsystem_mapping>(TgState.Of(_world).Master?.subsystems).FirstOrDefault()?.z_list;
        for (int z = 1; z <= Math.Min(grid.Levels, levels?.Count ?? 0); z++)
            if (levels[z] is datum_space_level level && Dm.truthy(level.traits?[ZTRAIT_NOPARALLAX]))
                grid.NoParallaxLevels.Add(z);
        // protocol 76 (D325): the levels that stand on the level below them (tg's ZTRAIT_DOWN), the multi-z stacks
        for (int z = 2; z <= Math.Min(grid.Levels, levels?.Count ?? 0); z++)
            if (levels[z] is datum_space_level level && Dm.truthy(level.traits?[ZTRAIT_DOWN]))
                grid.LinkedBelowLevels.Add(z);
        grid.Look = MapLookOf(_world);
        grid.WallHeightMillimetres = (int)Math.Round(LevelHeight.Of(_world).Metres * 1000f); // protocol 91: the clients build their world with it
        return grid;
    }

    // Protocol 87: the map's own sky and sun, when its json sets them (MapSettings); null: the game's own.
    private static MapLook MapLookOf(World world)
    {
        MapSettings s = MapSettingsSeam.Of(world), d = MapSettings.Default;
        if (s.Sky == d.Sky && s.Stars == d.Stars && s.Sun == d.Sun && s.SunColour == d.SunColour && s.SunEnergy == d.SunEnergy && s.SunSize == d.SunSize && s.SunElevation == d.SunElevation)
            return null;
        byte[] picture = s.Sky == MapSkyKind.Image ? MapSettingsSeam.SkyImageOf(world) : null;
        return new MapLook
        {
            Sky = (byte)s.Sky, SkyTop = MapSettings.Rgb(s.SkyTop), SkyHorizon = MapSettings.Rgb(s.SkyHorizon), Stars = s.Stars,
            Image = picture != null && picture.Length <= Limits.MaxSkyImageBytes && Codec.IsPicture(picture) ? picture : Array.Empty<byte>(),
            Sun = s.Sun, SunColour = MapSettings.Rgb(s.SunColour), SunEnergy = s.SunEnergy, SunSize = s.SunSize, SunElevation = s.SunElevation,
        };
    }

    private static void AddRuns(List<TurfRun> runs, int count, Func<int, int> valueAt)
    {
        int runStart = 0;
        for (int i = 1; i <= count; i++)
        {
            if (i < count && valueAt(i) == valueAt(runStart)) continue;
            runs.Add(new TurfRun(i - runStart, valueAt(runStart)));
            runStart = i;
        }
    }

    // tg's starlight as a light source of the replica: the space turf's height, the cone all around.
    private static LightState StarlightState((float Range, float Power, uint Rgba) star) =>
        star.Range > 0f && star.Power != 0f
            ? new LightState(star.Range, star.Power, star.Rgba, true, 360f, 0, LIGHTING_HEIGHT_SPACE, 0f, 0f, (byte)COMPLEX_LIGHT)
            : LightState.None;

    /// <summary>The Snapshot of the tick the shadows hold: the whole type table and the public tables, the entities, and the session's header values (an empty private block).</summary>
    public Snapshot BuildSnapshot(long ownEntity, uint lastInputSeq)
    {
        EnsureAttached();
        var snapshot = new Snapshot { Tick = _stateTick, LastInputSeq = lastInputSeq, OwnEntity = ownEntity };
        snapshot.Types.AddRange(_types);
        snapshot.Resources.AddRange(_public.Resources);
        snapshot.Strings.AddRange(_public.Strings);
        snapshot.Appearances.AddRange(_public.Appearances);
        var ids = new long[_index.Count];
        _index.Keys.CopyTo(ids, 0);
        Array.Sort(ids);
        snapshot.Entities.Capacity = ids.Length;
        foreach (long id in ids)
        {
            ref Shadow s = ref _slots[_index[id]];
            if (s.Layer == 0 && _interest == null) snapshot.Entities.Add(StateOf(in s)); // version 22: the layer entities are in the session's own block (M10 slice C: all of them with interest on)
        }
        return snapshot;
    }

    /// <summary>The replicated state of an entity as of <see cref="StateTick"/>.</summary>
    public bool TryGetEntity(long id, out EntityState state)
    {
        if (_index.TryGetValue(id, out int slot))
        {
            state = StateOf(in _slots[slot]);
            return true;
        }
        state = default;
        return false;
    }

    /// <summary>
    /// The tick in which <paramref name="field"/> (one changeable field) of the entity last changed, its spawn tick if it
    /// never did; -1 for an id that is not an entity. M10 builds a client's delta from these: the fields stamped after the
    /// client's acknowledged tick.
    /// </summary>
    public long ChangedAt(long id, NetField field)
    {
        int bit = StampIndex(field);
        return _index.TryGetValue(id, out int slot) ? _slots[slot].ChangedAt[bit] : -1;
    }

    // ---- the comparison ----

    // Reads the candidate from the world and compares it with its shadow: a spawn, a change, a despawn or nothing. Returns
    // the fields of a change (None for a spawn, a despawn or no change).
    private NetField Examine(long id, long tick, byte source)
    {
        bool known = _index.TryGetValue(id, out int slot);
        if (!_registry.TryGet(id, out object obj) || !Classify(id, obj, out atom_movable movable, out turf on, out bool visual, out long visHost))
        {
            _visuals.Remove(id);
            if (known) Despawn(id, slot);
            return NetField.None;
        }
        if (known && visual != _slots[slot].IsVisual)
        {
            // An ordinary entity never becomes a visual (a visual is in nullspace) nor the reverse in tg; should it happen, the
            // entity is despawned and spawned again in the next read, since a change record cannot carry it.
            Despawn(id, slot);
            _pending.Add(new Candidate(id, FromHint));
            return NetField.None;
        }
        if (known && ReplicatedFields.LayerOf(movable) != _slots[slot].Layer)
        {
            // Version 22: an entity whose invisibility crossed SEE_INVISIBLE_LIVING leaves its layer now and enters the other in
            // the next read, so one tick never names it in both the body and a private block.
            Despawn(id, slot);
            _pending.Add(new Candidate(id, FromHint));
            return NetField.None;
        }
        if (!known)
        {
            Spawn(id, movable, on, tick, record: true, visual, visHost);
            return NetField.None;
        }

        ref Shadow s = ref _slots[slot];
        s.Invisibility = movable.invisibility; // version 22: who sees a layer entity
        NetField mask = NetField.None;
        if (visual && visHost != s.VisHost)
        {
            s.VisHost = visHost;
            mask |= NetField.VisHost;
        }
        int oldX = s.X, oldY = s.Y, oldZ = s.Z;
        int x = (int)on.x, y = (int)on.y, z = (int)on.z;
        if (x != s.X)
        {
            s.X = x;
            mask |= NetField.X;
        }
        if (y != s.Y)
        {
            s.Y = y;
            mask |= NetField.Y;
        }
        if (z != s.Z)
        {
            s.Z = z;
            mask |= NetField.Z;
        }
        byte dir = ReplicatedFields.WireDir(movable.dir);
        if (dir != s.Dir)
        {
            s.Dir = dir;
            mask |= NetField.Dir;
        }
        string name = movable.name;
        if (!ReferenceEquals(name, s.Name))
        {
            if (!string.Equals(name, s.Name, StringComparison.Ordinal))
            {
                string wire = ReplicatedFields.WireName(name);
                if (!string.Equals(wire, s.WireName, StringComparison.Ordinal))
                {
                    s.WireName = wire;
                    mask |= NetField.Name;
                }
            }
            s.Name = name;
        }
        float glide = ReplicatedFields.WireGlide(movable.glide_size);
        if (glide != s.Glide)
        {
            s.Glide = glide;
            mask |= NetField.GlideSize;
        }
        if ((mask & Position) != 0)
        {
            s.LastMoveTick = tick;
            mask |= NetField.LastMoveTick;
        }

        // The appearance: from the atom's vars and lists, when a hint named it, the sweep reached it, or it just appeared.
        if ((source & (FromAppearance | FromSweep | FromCreated)) != 0)
        {
            uint appearance = _public.InternTop(movable, false);
            if (appearance != s.Appearance)
            {
                s.Appearance = appearance;
                mask |= NetField.Appearance;
            }
        }

        // Posture of a living mob, from its vars.
        if (movable is mob_living living)
        {
            byte posture = ReplicatedFields.WirePosture(living);
            if (posture != s.Posture)
            {
                s.Posture = posture;
                mask |= NetField.Posture;
            }
        }

        // A capsule mob's continuous position and yaw. Once an entity has been a capsule it keeps sending Pos (the tile centre
        // when the bridge no longer drives it), so a client never holds a stale position for it. Version 42: only its first record
        // carries them; afterwards a change goes to the Motion messages (Replicator.Motion.cs), stamped but not in the record.
        NetField motion = NetField.None;
        if (s.IsMob)
        {
            bool capsule = _locomotion.TryGetCapsule(_world, id, out MotionState state);
            if (capsule || s.PosMode)
            {
                int px, py;
                byte yaw = s.Yaw;
                if (capsule)
                {
                    px = Math.Clamp(state.X, MinPos, MaxPos);
                    py = Math.Clamp(state.Y, MinPos, MaxPos);
                    yaw = ReplicatedFields.WireYaw(movable, state.Yaw);
                }
                else
                {
                    px = s.X * MotionMath.SubunitsPerTile;
                    py = s.Y * MotionMath.SubunitsPerTile;
                }
                bool becameCapsule = !s.PosMode;
                if (becameCapsule || px != s.PosX || py != s.PosY)
                {
                    s.PosX = px;
                    s.PosY = py;
                    motion |= NetField.Pos;
                }
                if (becameCapsule || yaw != s.Yaw)
                {
                    s.Yaw = yaw;
                    motion |= NetField.Yaw;
                }
                if (becameCapsule)
                    mask |= motion;
                else if (motion != NetField.None)
                    _moved[id] = tick;
                s.PosMode = true;
            }
        }

        // The light source (version 3).
        LightState light = ReplicatedFields.WireLight(movable);
        if (light != s.Light)
        {
            s.Light = light;
            mask |= NetField.Light;
        }

        // The entity flags (version 4): Thrown while tg's throwing is set; version 56: Pushable. An entity flagged but Pushable is polled every tick (_flagged).
        EntityFlags flags = ReplicatedFields.WireFlags(movable);
        if (_lunges.Remove(id, out byte lungeDir)) // one read carries it; the next tick's poll of the flagged entity clears it
            flags |= EntityFlags.Lunge | (EntityFlags)(lungeDir << 4);
        if (_hits.Remove(id)) // the same for tg's attack visual on it
            flags |= EntityFlags.Hit;
        if (flags != s.Flags)
        {
            s.Flags = flags;
            mask |= NetField.Flags;
            if ((flags & ~EntityFlags.Pushable) != EntityFlags.None) _flagged.Add(id);
            else _flagged.Remove(id);
        }

        // The last flick (version 8): the atom's appearance as it is now with the flicked icon state (or icon) in its place.
        if (_flicks.Remove(id, out object flicked))
        {
            uint look = _public.InternFlick(movable, flicked);
            if (look != 0)
            {
                s.FlickAppearance = look;
                s.FlickTick = tick;
                mask |= NetField.Flick;
            }
        }

        // A shot's 3D line (version 34): it changes when the shot turns in flight (a ricochet, a reflection).
        ShotState? shot = ReplicatedFields.WireShot(movable);
        if (shot.HasValue && shot != s.Shot)
        {
            s.Shot = shot;
            mask |= NetField.Shot;
        }

        // An airlock's access and state (version 43): the client's guess of its opening.
        DoorState door = ReplicatedFields.WireDoor(movable);
        if (door != null && !door.Equals(s.Door))
        {
            s.Door = door;
            mask |= NetField.Door;
        }

        // An announced level ride (version 77, DV371): an elevator's car and riders, a ladder's climber; polled while it lasts.
        RideState ride = ReplicatedFields.WireRide(movable);
        if (ride != s.Ride)
        {
            s.Ride = ride;
            mask |= NetField.Ride;
        }
        if (ride.IsNone)
            _rides?.Forget(id);

        // A tram's announced trip (version 80, DV403): on its platform, polled while it lasts.
        TramRide tram = movable.dm_tram_ride;
        if (tram != s.Tram)
        {
            s.Tram = tram;
            mask |= NetField.Tram;
        }
        if (tram.IsNone)
            _trams?.Forget(id);

        // The free motion (version 81, DV416): a throw's arc, a fall, a slide, a rest point; polled while it moves.
        FlightArc flight = ReplicatedFields.WireFlight(movable);
        if (flight != s.Flight)
        {
            s.Flight = flight;
            mask |= NetField.Flight;
        }
        if (!flight.IsMoving)
            _flights?.Forget(id);

        // The running animate() chains (version 9): when a step was recorded since the last read, or the one sent has ended.
        // Version 74: with the maptext chains of its vis_contents children (tg's status display text), whenever it is read.
        if (_animations.TryTake(id, tick, s.OwnAnimation, out EntityAnimation animation))
            s.OwnAnimation = animation;
        EntityAnimation shownAnimation = WithTextChains(s.OwnAnimation, movable, tick);
        if (!Equals(shownAnimation, s.Animation))
        {
            s.Animation = shownAnimation;
            mask |= NetField.Animation;
        }

        // The cells: a dense non-mob movable (solidity), a dense ON_BORDER_1 one (a face) or an opaque atom (light) that moved,
        // toggled or turned changes the cells it leaves and enters.
        int cellKey = CellKeyOf(movable, s.IsMob);
        if (cellKey != 0 || s.CellKey != 0)
        {
            if ((mask & Position) != 0 || cellKey != s.CellKey)
            {
                if (s.CellKey != 0) MarkCell(oldX, oldY, oldZ);
                if (cellKey != 0) MarkCell(s.X, s.Y, s.Z);
            }
        }
        s.CellKey = cellKey;

        NetField stamped = mask | motion;
        if (stamped == NetField.None) return NetField.None;

        for (int i = 0; i < StampCount; i++)
            if ((stamped & ChangeableOrder[i]) != 0) s.ChangedAt[i] = tick;
        if (mask == NetField.None) return stamped; // version 42: only the position or yaw changed, which the Motion carries
        if ((mask & Position) != 0) NoteMoved(ref s); // M10 slice C
        EntityChange record = new EntityChange
        {
            Id = id,
            Mask = mask,
            X = s.X,
            Y = s.Y,
            Z = s.Z,
            Dir = s.Dir,
            Name = s.WireName,
            GlideSize = s.Glide,
            LastMoveTick = s.LastMoveTick,
            PosX = s.PosX,
            PosY = s.PosY,
            Yaw = s.Yaw,
            Posture = s.Posture,
            Appearance = TableRef.Public(s.Appearance),
            Light = s.Light,
            Flags = s.Flags,
            FlickAppearance = TableRef.Public(s.FlickAppearance),
            FlickTick = s.FlickTick,
            Animation = s.Animation,
            VisHost = s.VisHost,
            Shot = s.Shot ?? default,
            Door = s.Door,
            Ride = s.Ride,
            Tram = s.Tram,
            Flight = s.Flight,
        };
        if (s.Layer == 0 && _interest == null)
            _delta.Changed.Add(record);
        else
            _layerChanged[id] = record; // version 22: the sessions that see it take it (SessionState.UpdateLayer; M10 slice C: UpdateInterest)
        return mask;
    }

    // Version 10: an entity (ReplicatedFields.IsEntity) or a flick visual on a known host (ReplicatedFields.IsVisual).
    private bool Classify(long id, object obj, out atom_movable movable, out turf on, out bool visual, out long visHost)
    {
        visual = false;
        visHost = 0;
        if (ReplicatedFields.IsEntity(obj, out movable, out on, _options.MapMarkers))
            return true;
        if (_visuals.TryGetValue(id, out atom host) && ReplicatedFields.IsVisual(obj, host, out movable, out on, out visHost))
        {
            visual = true;
            return true;
        }
        return false;
    }

    // Version 10: the flick visuals (tg's flick_overlay_view) and the guns' firing effects a written vis_contents holds now. The engine keeps no vis_locs the
    // replicator could read from outside the world, so the replicator keeps its own index of the hosts, from the writes it is told of.
    private void NoteVisuals(atom holder)
    {
        if (holder == null || holder.gc_destroyed != null) return;
        DmList list = holder.vis_contents;
        int count = list.Count;
        for (int i = 1; i <= count; i++)
        {
            if (list[(float)i] is atom_movable visual && (ReplicatedFields.IsHeldVisual(visual) || ReplicatedFields.IsInnerVisual(visual) || ReplicatedFields.IsInsideVisual(visual)) && visual.dm_id != 0 && visual.gc_destroyed == null)
                _visuals[visual.dm_id] = holder;
        }
    }

    // ---- protocol version 74 (gaps W1): the maptext chains of a world entity's vis_contents children ----

    // The maptext_x chains of objects that are no entity (tg's status display text: one object shared by every display that shows it, in nullspace),
    // kept past the tick they were recorded in, by id. shortcut: a child's chain is read as it was first recorded (tg starts a new text object
    // for a new message); a later animate() of the same object is not sent to holders already read.
    private readonly Dictionary<long, EntityAnimation> _textChains = new();

    private void KeepTextChain(long id, long tick)
    {
        EntityAnimation chains = TextTracks(_animations.Peek(id, tick));
        if (chains == null)
            return;
        if (_textChains.Count >= 1024)
            foreach (long known in _textChains.Keys.ToArray())
                if (!_registry.TryGet(known, out object o) || o is not datum { gc_destroyed: null })
                    _textChains.Remove(known);
        _textChains[id] = chains;
    }

    private static EntityAnimation TextTracks(EntityAnimation chains)
    {
        if (chains == null)
            return null;
        AnimationTrack[] text = chains.Tracks.Where(t => t.Filter == 0 && Named(t).HasFlag(AnimatedVar.MaptextX)).ToArray();
        return text.Length == 0 ? null : new EntityAnimation { Tracks = text };
    }

    private static AnimatedVar Named(AnimationTrack t)
    {
        AnimatedVar mask = t.Start.Mask;
        foreach (AnimationStep step in t.Steps) mask |= step.Vars.Mask;
        return mask;
    }

    // The entity's own chains, then each vis_contents child's maptext chain naming the child's row (Child), up to the track limit.
    private EntityAnimation WithTextChains(EntityAnimation own, atom_movable m, long tick)
    {
        DmList vis = m.vis_contents;
        if (vis == null || vis.Count == 0)
            return own;
        List<AnimationTrack> tracks = null;
        for (int i = 1; i <= vis.Count; i++)
        {
            if (vis[(float)i] is not atom_movable child || child.dm_id == 0)
                continue;
            EntityAnimation chains = _animations.Has(child.dm_id) ? TextTracks(_animations.Peek(child.dm_id, tick)) : _textChains.GetValueOrDefault(child.dm_id);
            if (chains == null)
                continue;
            uint row = _public.InternVisChild(child);
            if (row == 0)
                continue;
            tracks ??= new List<AnimationTrack>(own?.Tracks ?? Array.Empty<AnimationTrack>());
            foreach (AnimationTrack t in chains.Tracks)
            {
                if (tracks.Count >= Limits.MaxAnimationTracks)
                    break;
                tracks.Add(new AnimationTrack { StartTick = t.StartTick, Loop = t.Loop, Parallel = t.Parallel, Start = t.Start, Steps = t.Steps, Child = TableRef.Public(row) });
            }
        }
        return tracks == null ? own : new EntityAnimation { Tracks = tracks.ToArray() };
    }

    private const int MinPos = MotionMath.SubunitsPerTile / 2;
    private const int MaxPos = Limits.MaxMapSize * MotionMath.SubunitsPerTile + MotionMath.SubunitsPerTile / 2 - 1;

    private void Spawn(long id, atom_movable movable, turf on, long tick, bool record, bool visual = false, long visHost = 0)
    {
        int slot;
        if (_free.Count > 0)
        {
            slot = _free.Pop();
        }
        else
        {
            if (_slotCount == _slots.Length) Array.Resize(ref _slots, _slots.Length * 2);
            slot = _slotCount++;
        }
        string name = movable.name;
        ref Shadow s = ref _slots[slot];
        s = new Shadow
        {
            Id = id,
            Type = TypeIndexOf((movable as IShownAsType)?.ShownType ?? movable.GetType(), record), // DV376: drawn as another type
            X = (int)on.x,
            Y = (int)on.y,
            Z = (int)on.z,
            Dir = ReplicatedFields.WireDir(movable.dir),
            Name = name,
            WireName = ReplicatedFields.WireName(name),
            Glide = ReplicatedFields.WireGlide(movable.glide_size),
            LastMoveTick = tick,
            Appearance = _public.InternTop(movable, false),
            IsMob = movable is mob or obj_vehicle_sealed_mecha, // an exosuit walks a capsule of its own (DV358)
            MobListIndex = -1,
            IsVisual = visual,
            VisHost = visHost,
            Layer = ReplicatedFields.LayerOf(movable),
            Invisibility = movable.invisibility,
            Shot = ReplicatedFields.WireShot(movable),
            Ride = ReplicatedFields.WireRide(movable),
            Tram = movable.dm_tram_ride,
            Flight = ReplicatedFields.WireFlight(movable),
        };
        s.PosX = s.X * MotionMath.SubunitsPerTile;
        s.PosY = s.Y * MotionMath.SubunitsPerTile;
        if (movable is mob_living living) s.Posture = ReplicatedFields.WirePosture(living);
        if (s.IsMob)
        {
            if (_locomotion.TryGetCapsule(_world, id, out MotionState state))
            {
                s.PosMode = true;
                s.PosX = Math.Clamp(state.X, MinPos, MaxPos);
                s.PosY = Math.Clamp(state.Y, MinPos, MaxPos);
                s.Yaw = ReplicatedFields.WireYaw(movable, state.Yaw);
            }
            s.MobListIndex = _mobSlots.Count;
            _mobSlots.Add(slot);
        }
        s.Light = ReplicatedFields.WireLight(movable);
        s.Flags = ReplicatedFields.WireFlags(movable);
        if ((s.Flags & ~EntityFlags.Pushable) != EntityFlags.None) _flagged.Add(id);
        _animations.TryTake(id, tick, null, out s.OwnAnimation); // a chain that ran as it appeared (an item dropped from a hand glides down)
        s.Animation = WithTextChains(s.OwnAnimation, movable, tick);
        // A flick as it appeared (M4c slice 6: tg's trail particles flick ion_fade the tick they are made, effects_trail.dm:72).
        if (_flicks.Remove(id, out object flicked))
        {
            uint look = _public.InternFlick(movable, flicked);
            if (look != 0)
            {
                s.FlickAppearance = look;
                s.FlickTick = tick;
            }
        }
        s.CellKey = CellKeyOf(movable, s.IsMob);
        if (s.CellKey != 0) MarkCell(s.X, s.Y, s.Z);
        s.CellPollIndex = -1;
        if (!s.IsMob && PollsCell(_types[s.Type]))
        {
            s.CellPollIndex = _cellPollSlots.Count;
            _cellPollSlots.Add(slot);
        }
        for (int i = 0; i < StampCount; i++) s.ChangedAt[i] = tick;
        _index.Add(id, slot);
        NoteSpawned(ref s); // M10 slice C
        if (s.Layer != 0)
            _layerIds.Add(id); // version 22: each session's block spawns it to the viewers that see it
        else if (record && _interest == null) // M10 slice C: with interest on, each session's block spawns it
            _delta.Spawned.Add(StateOf(in s));
    }

    private void Despawn(long id, int slot)
    {
        _moved.Remove(id); // version 42
        ref Shadow s = ref _slots[slot];
        if (s.CellKey != 0) MarkCell(s.X, s.Y, s.Z);
        if (s.CellPollIndex >= 0)
        {
            int last = _cellPollSlots.Count - 1;
            int moved = _cellPollSlots[last];
            _cellPollSlots[s.CellPollIndex] = moved;
            _slots[moved].CellPollIndex = s.CellPollIndex;
            _cellPollSlots.RemoveAt(last);
        }
        if (s.MobListIndex >= 0)
        {
            int last = _mobSlots.Count - 1;
            int moved = _mobSlots[last];
            _mobSlots[s.MobListIndex] = moved;
            _slots[moved].MobListIndex = s.MobListIndex;
            _mobSlots.RemoveAt(last);
        }
        bool layer = s.Layer != 0;
        NoteDespawned(ref s); // M10 slice C
        _index.Remove(id);
        _flagged.Remove(id);
        _animations.Forget(id);
        _visuals.Remove(id);
        _slots[slot] = default;
        _free.Push(slot);
        if (layer)
            _layerIds.Remove(id); // version 22: each session's block despawns it where its client has it
        else if (_interest == null)
            _delta.Despawned.Add(id);
    }

    private int TypeIndexOf(Type type, bool record)
    {
        if (_typeIndex.TryGetValue(type, out int index)) return index;
        index = _types.Count;
        string path = PathOf(type);
        _types.Add(path);
        _typeIndex.Add(type, index);
        if (record) _delta.NewTypes.Add(path);
        return index;
    }

    // The DM path of a type, or of its nearest ancestor that has one (a C#-only subclass, such as a test fixture).
    private static string PathOf(Type type)
    {
        for (Type level = type; level != null; level = level.BaseType)
        {
            string path = DmTypes.PathOf(level);
            if (path != null) return path;
        }
        return "/datum";
    }

    private static EntityState StateOf(in Shadow s) => new(s.Id, s.Type, s.X, s.Y, s.Z, s.Dir, s.WireName, s.Glide, s.LastMoveTick)
    {
        HasPos = s.PosMode,
        PosX = s.PosX,
        PosY = s.PosY,
        Yaw = s.PosMode ? s.Yaw : (byte)0,
        Posture = s.Posture,
        Appearance = TableRef.Public(s.Appearance),
        Light = s.Light,
        Flags = s.Flags,
        FlickAppearance = TableRef.Public(s.FlickAppearance),
        FlickTick = s.FlickTick,
        Animation = s.Animation,
        IsVisual = s.IsVisual,
        VisHost = s.VisHost,
        Shot = s.Shot,
        Door = s.Door,
        Ride = s.Ride,
        Tram = s.Tram,
        Flight = s.Flight,
    };

    private int SweepPerTick() => Math.Max(_minSweep, (_slotCount + _sweepDivisor - 1) / _sweepDivisor);

    private void EnsureAttached()
    {
        if (_world == null) throw new InvalidOperationException("Attach the replicator to the world first");
    }

    private static void EnsureOutsideWorld()
    {
        if (World.CurrentOrNull != null)
            throw new InvalidOperationException("the replicator reads the world from outside: call it after World.Step has returned, never inside world.Enter() (IReplicator)");
    }

    // The children of a holder whose vis_contents were written (or were there at the attach) join the index of where each is shown.
    private void IndexVisChildren(atom holder)
    {
        DmList vis = holder.vis_contents;
        for (int i = 1; i <= vis.Count; i++)
        {
            if (vis[(float)i] is not atom child || child.dm_id == 0) continue;
            if (!_visShownIn.TryGetValue(child.dm_id, out List<atom> holders))
                _visShownIn[child.dm_id] = holders = new List<atom>(1);
            if (!holders.Exists(known => ReferenceEquals(known, holder)))
                holders.Add(holder);
        }
    }

    private static bool HoldsChild(atom holder, long childId)
    {
        DmList vis = holder.vis_contents;
        for (int i = 1; i <= vis.Count; i++)
            if (vis[(float)i] is atom child && child.dm_id == childId) return true;
        return false;
    }

    // ---- the public tables' new entries ----

    private void FillNewTableEntries()
    {
        _delta.FirstNewResource = (uint)_public.ResourcesBefore + 1;
        for (int i = _public.ResourcesBefore; i < _public.Resources.Count; i++) _delta.NewResources.Add(_public.Resources[i]);
        _delta.FirstNewString = (uint)_public.StringsBefore + 1;
        for (int i = _public.StringsBefore; i < _public.Strings.Count; i++) _delta.NewStrings.Add(_public.Strings[i]);
        _delta.FirstNewAppearance = (uint)_public.AppearancesBefore + 1;
        for (int i = _public.AppearancesBefore; i < _public.Appearances.Count; i++) _delta.NewAppearances.Add(_public.Appearances[i]);
        _delta.FirstNewBaseLight = (uint)_baseLightsBefore + 1;
        for (int i = _baseLightsBefore; i < _baseLights.Count; i++) _delta.NewBaseLights.Add(_baseLights[i]);
        _baseLightsBefore = _baseLights.Count;
        _delta.FirstNewTurfType = _turfTypesBefore;
        for (int i = _turfTypesBefore; i < _turfTypePaths.Count; i++) _delta.NewTurfTypes.Add(_turfTypePaths[i]);
        _turfTypesBefore = _turfTypePaths.Count;
    }

    // ---- the map's cells ----

    private int CellCount => _width * _height * _levels;

    // rebuild: a later rebuild (UpdateCells, the map's size changed) keeps the table watermarks, so the entries it adds go
    // out in the Delta of the tick; and drops the cells marked for the old size.
    private void BuildCells(bool rebuild = false)
    {
        _spaceBuilt = false; // M10 slice C: the interest's indexes are built again from the new cells
        if (rebuild)
        {
            _dirtyCells.Clear();
            _cellCursor = 0;
            _cellSolid = null;
            _cellAppearance = null;
            _cellMarked = null;
            _cellFlags = null;
            _cellBase = null;
            _cellTurfType = null;
            _cellGas = null;
        }
        _width = MapView.Width(_world);
        _height = MapView.Height(_world);
        _levels = MapView.Levels(_world);
        int cells = CellCount;
        if (_width < 1 || _height < 1 || _levels < 1 || _width > Limits.MaxMapSize || _height > Limits.MaxMapSize || _levels > Limits.MaxMapLevels)
        {
            _width = _height = _levels = 0;
            return;
        }
        _cellSolid = new byte[(cells + 7) / 8];
        _cellAppearance = new uint[cells];
        _cellMarked = new bool[cells];
        _cellFlags = new byte[cells];
        _cellBase = new ushort[cells];
        _cellTurfType = new int[cells];
        _cellGas = new GasLevel[cells][];
        if (_gasKindIndex.Count == 0)
        {
            IReadOnlyList<string> kinds = CellState.GasKinds();
            for (int k = 0; k < kinds.Count && k < Limits.MaxGasKinds; k++)
            {
                _gasKinds.Add(kinds[k]);
                _gasKindIndex[kinds[k]] = k + 1;
            }
        }
        if (!rebuild) _starlight = CellState.Starlight(_world); // a rebuild leaves the change to BuildDelta's StarlightChanged
        int i = 0;
        for (int z = 1; z <= _levels; z++)
            for (int y = 1; y <= _height; y++)
                for (int x = 1; x <= _width; x++, i++)
                {
                    if (_locomotion.IsSolid(_world, x, y, z)) _cellSolid[i >> 3] |= (byte)(1 << (i & 7));
                    turf cell = MapView.TurfAt(_world, x, y, z);
                    _cellAppearance[i] = cell == null ? 0 : _public.InternTop(cell, false);
                    _cellFlags[i] = FlagsOf(x, y, z);
                    _cellBase[i] = (ushort)BaseLightIndexOf(CellState.BaseLightOf(_world, x, y, z));
                    _cellTurfType[i] = TurfTypeIndexOf(cell?.GetType() ?? typeof(turf));
                    _cellGas[i] = GasOf(x, y, z);
                }
        if (_turfTypePaths.Count == 0) _turfTypePaths.Add(PathOf(typeof(turf)));
        if (rebuild) return;
        _baseLightsBefore = _baseLights.Count;
        _turfTypesBefore = _turfTypePaths.Count;
    }

    // The flags byte of a tile (CellFlags): the faces LocomotionSeam gives the controller, tg's opacity, starlight, the climb-walkable bit (version 37).
    private byte FlagsOf(int x, int y, int z)
    {
        int flags = (int)_locomotion.FacesOf(_world, x, y, z) & CellFlags.Faces;
        if (_locomotion.IsClimbWalkable(_world, x, y, z)) flags |= CellFlags.ClimbWalkable;
        if (_locomotion.IsFlaps(_world, x, y, z)) flags |= CellFlags.Flaps; // version 73 (DV360)
        if (CellState.IsOpaque(_world, x, y, z)) flags |= CellFlags.Opaque;
        if (CellState.IsStarlit(_world, x, y, z)) flags |= CellFlags.Starlight;
        return (byte)flags;
    }

    // Version 5: the gas a tile shows (CellState.GasOf: tg's gas overlays of the turf), null for none.
    private GasLevel[] GasOf(int x, int y, int z)
    {
        CellState.GasOf(_world, x, y, z, _gasScratch);
        if (_gasScratch.Count == 0) return null;
        int n = Math.Min(_gasScratch.Count, Limits.MaxGasesPerCell);
        var gases = new GasLevel[n];
        int used = 0;
        for (int k = 0; k < n; k++)
        {
            if (!_gasKindIndex.TryGetValue(_gasScratch[k].State, out int kind)) continue;
            gases[used++] = new GasLevel((byte)kind, (byte)_gasScratch[k].Level);
        }
        if (used == 0) return null;
        return used == n ? gases : gases[..used];
    }

    private static bool SameGas(GasLevel[] a, GasLevel[] b)
    {
        if (a == null || b == null) return a == b;
        return a.AsSpan().SequenceEqual(b);
    }

    // The base-light table index (1-based, 0 for none) of an rgba; a new value extends the table (false at the cap: 0, no light).
    private int BaseLightIndexOf(uint rgba)
    {
        if (rgba == 0) return 0;
        if (_baseLightIndex.TryGetValue(rgba, out int index)) return index;
        if (_baseLights.Count >= Limits.MaxBaseLightEntries) return 0;
        _baseLights.Add(new BaseLight(rgba));
        index = _baseLights.Count;
        _baseLightIndex.Add(rgba, index);
        return index;
    }

    // The turf type table index of a tile's runtime type; a new type extends the table (-1 at the cap).
    private int TurfTypeIndexOf(Type type)
    {
        if (_turfTypeIndex.TryGetValue(type, out int index)) return index;
        if (_turfTypePaths.Count >= Limits.MaxTurfTypeEntries) return -1;
        index = _turfTypePaths.Count;
        _turfTypePaths.Add(PathOf(type));
        _turfTypeIndex.Add(type, index);
        return index;
    }

    // What an entity adds to its cell's state (the flags, solidity): see Examine.
    private static int CellKeyOf(atom_movable m, bool isMob)
    {
        int key = 0;
        if (!isMob && Dm.truthy(m.density))
        {
            key |= 1;
            if (LocomotionSeam.BlocksFace(m)) // a flipped table too: its flip changes the key, so its cells are sent again
                key |= 2 | ((int)TileFaces.OfDir((int)m.dir) << 4);
        }
        if (Dm.truthy(m.opacity)) key |= 4;
        if (m is obj_structure_plasticflaps flaps && Dm.truthy(flaps.require_resting)) key |= 8; // the flaps bit (DV360)
        return key;
    }

    // Doors and the like change density, opacity and so their cell's flags without any hint; they are polled (replication.md 8).
    private static bool PollsCell(string path)
    {
        foreach (string prefix in CellPollPrefixes)
            if (path.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    private void MarkCell(int x, int y, int z)
    {
        if (_cellMarked == null || x < 1 || y < 1 || z < 1 || x > _width || y > _height || z > _levels) return;
        int i = (x - 1) + (y - 1) * _width + (z - 1) * _width * _height;
        if (_cellMarked[i]) return;
        _cellMarked[i] = true;
        _dirtyCells.Add(i);
    }

    private void MarkTurf(turf t)
    {
        _mapChanged = true;
        MarkCell((int)t.x, (int)t.y, (int)t.z);
    }

    // M3 R4: the map's size as the tables have it no longer matches the world's (a map loaded after Attach: SSmapping loads
    // the station in the Master's init, after the host attached the replicator to the booted world, so Attach saw no map;
    // or a level added later). The tables are rebuilt from the world; the table entries they add go into this Delta.
    private bool MapSizeChanged()
        => MapView.Width(_world) != _width || MapView.Height(_world) != _height || MapView.Levels(_world) != _levels;

    /// <summary>
    /// M3 R4: re-read every cell of the map at the next <see cref="BuildDelta"/> (<see cref="IReplicator.RereadMap"/>): each
    /// cell is marked, so its differences from the tables go out in the Deltas (capped as hinted cells are) and the join map
    /// follows them.
    /// </summary>
    public void RereadMap() => _rereadPending = true;

    private bool _rereadPending;

    // Re-reads the marked cells and a slice of the cell sweep; returns the number of cell changes put into the Delta.
    private int UpdateCells()
    {
        if (MapSizeChanged())
        {
            BuildCells(rebuild: true);
            _mapChanged = true;
            _rereadPending = false;
        }
        if (_cellSolid == null) return 0;
        if (_rereadPending)
        {
            _rereadPending = false;
            for (int i = 0; i < _cellMarked.Length; i++)
            {
                if (_cellMarked[i]) continue;
                _cellMarked[i] = true;
                _dirtyCells.Add(i);
            }
        }
        for (int n = 0; n < _cellSweep; n++)
        {
            if (_cellCursor >= CellCount) _cellCursor = 0;
            int i = _cellCursor++;
            if (_cellMarked[i]) continue;
            _cellMarked[i] = true;
            _dirtyCells.Add(i);
        }
        if (_dirtyCells.Count == 0) return 0;
        _dirtyCells.Sort();
        int changes = 0, done = 0;
        for (; done < _dirtyCells.Count && changes < Limits.MaxCellChangesPerDelta - 4 && _delta.NewTurfTypes.Count < Limits.MaxNewTurfTypesPerDelta; done++)
        {
            int i = _dirtyCells[done];
            _cellMarked[i] = false;
            int x = i % _width + 1, y = i / _width % _height + 1, z = i / (_width * _height) + 1;
            bool solid = _locomotion.IsSolid(_world, x, y, z);
            bool wasSolid = (_cellSolid[i >> 3] & (1 << (i & 7))) != 0;
            if (solid != wasSolid)
            {
                _cellSolid[i >> 3] ^= (byte)(1 << (i & 7));
                _delta.SolidChanged.Add(new CellSolidChange(x, y, z, solid));
                changes++;
            }
            turf cell = MapView.TurfAt(_world, x, y, z);
            uint appearance = cell == null ? 0 : _public.InternTop(cell, false);
            if (appearance != _cellAppearance[i])
            {
                _cellAppearance[i] = appearance;
                _delta.TurfAppearanceChanged.Add(new CellAppearanceChange(x, y, z, TableRef.Public(appearance)));
                changes++;
            }

            byte flags = FlagsOf(x, y, z);
            if (flags != _cellFlags[i])
            {
                bool opacity = ((flags ^ _cellFlags[i]) & CellFlags.Opaque) != 0;
                _cellFlags[i] = flags;
                if (opacity) OnOpacityChanged(x, y, z, i); // M10 slice C: the rooms' links
                _delta.CellFlagsChanged.Add(new CellFlagsChange(x, y, z, flags));
                changes++;
            }

            int baseIndex = BaseLightIndexOf(CellState.BaseLightOf(_world, x, y, z));
            if (baseIndex != _cellBase[i])
            {
                _cellBase[i] = (ushort)baseIndex;
                _delta.BaseLightChanged.Add(new CellBaseLightChange(x, y, z, (ushort)baseIndex));
                changes++;
            }

            int typeIndex = TurfTypeIndexOf(cell?.GetType() ?? typeof(turf));
            if (typeIndex >= 0 && typeIndex != _cellTurfType[i])
            {
                _cellTurfType[i] = typeIndex;
                _delta.TurfTypeChanged.Add(new CellTurfTypeChange(x, y, z, typeIndex));
                changes++;
            }

            GasLevel[] gas = GasOf(x, y, z);
            if (!SameGas(gas, _cellGas[i]))
            {
                _cellGas[i] = gas;
                _delta.CellGasChanged.Add(new CellGasChange(x, y, z, gas ?? Array.Empty<GasLevel>()));
                changes++;
            }
        }
        _dirtyCells.RemoveRange(0, done); // what did not fit this Delta stays marked for the next
        if (changes > 0) _mapChanged = true;
        return changes;
    }

    // ---- the shadow of one entity ----

    // The changeable fields, each with its "changed at" stamp.
    private static readonly NetField[] ChangeableOrder =
    {
        NetField.X, NetField.Y, NetField.Z, NetField.Dir, NetField.Name, NetField.GlideSize, NetField.LastMoveTick,
        NetField.Pos, NetField.Yaw, NetField.Posture, NetField.Appearance, NetField.Light, NetField.Flags, NetField.Flick, NetField.Animation,
        NetField.VisHost, NetField.Shot, NetField.Door, NetField.Ride, NetField.Tram, NetField.Flight,
    };

    private const int StampCount = 21;

    private static int StampIndex(NetField field)
    {
        int index = Array.IndexOf(ChangeableOrder, field);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(field), field, "one changeable field");
        return index;
    }

    private struct Shadow
    {
        public long Id; // 0 for a free slot
        public int Type;
        public int X;
        public int Y;
        public int Z;
        public byte Dir;
        public string Name; // tg's value as last read (compared by reference first)
        public string WireName; // its capped wire form
        public float Glide;
        public long LastMoveTick;
        public uint Appearance; // the public appearance table index, 0 for none
        public int PosX; // 1/256 tile; the tile centre until the entity is a capsule
        public int PosY;
        public byte Yaw;
        public byte Posture;
        public bool PosMode; // the entity has been a capsule mob: its records carry Pos
        public bool IsMob;
        public int CellKey; // what the entity contributes to its cell: bit 0 dense, 1 border face, 2 opaque, 3 flaps, 4-7 the face bits
        public int CellPollIndex; // position in the cell-polled list (doors), -1 otherwise
        public LightState Light; // the light source as last sent
        public EntityFlags Flags; // the entity flags as last sent (version 4)
        public uint FlickAppearance; // the last flick's public appearance index, 0 for none (version 8)
        public long FlickTick; // the tick it started in
        public EntityAnimation Animation; // the animate() chains as last sent, null for none (version 9)
        public EntityAnimation OwnAnimation; // version 74: the entity's own chains, without its vis_contents children's maptext chains
        public bool IsVisual; // a flick visual drawn on another atom (version 10)
        public long VisHost; // its host entity's id, 0 for its turf (version 10)
        public ShotState? Shot; // a shot's 3D line as last sent, null for an entity that is no shot (version 34)
        public DoorState Door; // an airlock's access and state as last sent, null for any other entity (version 43)
        public RideState Ride; // the announced level ride as last sent (version 77, DV371)
        public TramRide Tram; // a tram platform's announced trip as last sent (version 80, DV403)
        public FlightArc Flight; // the free motion as last sent (version 81, DV416)
        public int MobListIndex; // position in the polled list, -1 for a non-mob
        public byte Layer; // version 22: 0 the shared body, 1 the observer layer (Replicator.Layer.cs)
        public int Area; // M10 slice C: the interest's index of the entity's area, -1 for none (Replicator.Interest.cs)
        public int Chunk; // M10 slice C: the entity's chunk key, -1 off the map
        public float Invisibility; // version 22: the atom's invisibility as last read (a layer entity's viewers)
        public FieldStamps ChangedAt;
    }

    [InlineArray(StampCount)]
    private struct FieldStamps
    {
        private long _element;
    }

    private readonly record struct Candidate(long Id, byte Source);

    private sealed class CandidateOrder : IComparer<Candidate>
    {
        public static readonly CandidateOrder Instance = new();

        public int Compare(Candidate a, Candidate b) => a.Id.CompareTo(b.Id);
    }

    // ---- a session's private state (D40) ----

    private sealed partial class SessionState
    {
        private readonly int _sweepPeriod;
        private readonly Dictionary<long, ScreenShadow> _shadow = new();
        private readonly List<atom_movable> _current = new();
        private readonly List<long> _gone = new();

        // Version 4: the HUD group of each screen object (tg's hud_used.screen_groups), and the private images (client.images).
        private readonly Dictionary<long, byte> _groupOf = new();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<image, ImageId> _imageIds = new();
        private readonly Dictionary<long, ImageShadow> _imageShadow = new();
        private readonly List<(long Id, image Image)> _images = new();
        private long _nextImageId = 1;

        private sealed class ImageId
        {
            public long Value;
        }

        private struct ImageShadow
        {
            public TableRef Appearance;
            public long Attached;
            public long Holder; // version 27: the atom the image hangs on, and its name
            public string HolderName;
            public int TurfX, TurfY, TurfZ; // version 30: the tile of an image on a turf
            public bool Override; // version 35: BYOND's image.override
            public long Seen;
        }

        public SessionState(int sweepPeriod)
        {
            _sweepPeriod = sweepPeriod;
        }

        /// <summary>The session's private tables; entries of its screen objects only.</summary>
        public AppearanceTables Tables { get; } = new(true);

        /// <summary>The block built by the last <see cref="Update"/> (reused).</summary>
        public OwnBlock Block { get; } = new();

        public byte[] Buffer { get; set; } = new byte[256];

        public int ScreenChanges { get; private set; }

        public int NewEntries { get; private set; }

        private struct ScreenShadow
        {
            public TableRef Appearance;
            public string Loc;
            public byte Group;
            public long Seen;
            public TableRef Flick; // version 11: the last flick (its private appearance) and its tick
            public long FlickTick;
            public EntityAnimation Animation; // version 11: the running animate() chains sent (the object's own)
            public ScreenChildren Children; // version 12: the clickable vis_contents children sent (null for none)
            public Dictionary<long, EntityAnimation> ChildAnimations; // version 26: the vis_contents children's chains sent, by child id
            public EntityAnimation Shown; // version 26: the entry's animation as sent (its own and its children's filter tracks)
            public long ActionTarget; // version 27: an action button's target item
            public ScreenInventory Inventory; // version 43: the inventory box's slot and hand, the item's slots and delay
        }

        // Version 43 (M10 slice E): an inventory box's slot or hand, for the client's guess of an equip or a hand swap, read raw.
        private readonly record struct ScreenInventory(uint SlotId, byte HandIndex);

        private static ScreenInventory InventoryOf(atom_movable m) => m switch
        {
            atom_movable_screen_inventory_hand hand => new ScreenInventory(0, (byte)Math.Clamp(hand.held_index, 0, 255)),
            atom_movable_screen_inventory box => new ScreenInventory(Dm.num(box.slot_id) is float slot && slot > 0 && slot <= uint.MaxValue ? (uint)slot : 0, 0),
            _ => default,
        };

        private static ScreenEntry WithInventory(ScreenEntry entry, in ScreenInventory inventory)
            => entry with { SlotId = inventory.SlotId, HandIndex = inventory.HandIndex };

        // Version 43: a vis child that is an item (held or worn) with its slot_flags and whether it has an equip delay.
        private static ScreenChild WithItem(ScreenChild child, atom m)
            => m is obj_item item
                ? child with { IsItem = true, ItemSlots = item.slot_flags is float flags && flags > 0 && flags <= uint.MaxValue ? (uint)flags : 0, EquipDelay = item.equip_delay_self > 0,
                    HasStorage = item.atom_storage != null }
                : child;

        // Version 78 (DV386): the camera chunks the client's camera eyes look into, as the obscured tiles of each chunk's levels, and the
        // static images tg puts in client.images for them (left out of the private images: the client draws static from the masks).
        private readonly Dictionary<(int X, int Y, int Z), ulong> _staticNow = new();
        private readonly Dictionary<(int X, int Y, int Z), ulong> _staticSent = new();
        private readonly HashSet<image> _staticImages = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<datum_camerachunk> _staticChunks = new(ReferenceEqualityComparer.Instance);
        private long _popupsSerialSent = -1; // version 78 (DV387): the client's popup serial the last block carried
        private IReadOnlyList<CommandVerb> _verbsSent; // version 78: the verbs the last block carried

        private void UpdateStatic(client player, bool full)
        {
            OwnBlock block = Block;
            block.StaticChunks = null;
            _staticNow.Clear();
            _staticImages.Clear();
            _staticChunks.Clear();
            if (player != null)
            {
                // tg's chunk.add puts the chunk's static in eye.GetViewerClient()'s images when the eye uses visibility: the client's
                // own camera eye (an advanced camera console's) and an AI's eyes (its eye and its multicamera windows' eyes)
                if (player.eye is mob_eye_camera own_eye && Dm.truthy(own_eye.use_visibility))
                    AddStaticOf(own_eye);
                if (player.mob is mob_living_silicon_ai ai && ai.all_eyes != null)
                    for (int i = 1; i <= ai.all_eyes.Count; i++)
                        if (ai.all_eyes[(float)i] is mob_eye_camera ai_eye && Dm.truthy(ai_eye.use_visibility))
                            AddStaticOf(ai_eye);
            }
            bool changed = full || _staticNow.Count != _staticSent.Count;
            if (!changed)
                foreach (KeyValuePair<(int X, int Y, int Z), ulong> pair in _staticNow)
                    if (!_staticSent.TryGetValue(pair.Key, out ulong sent) || sent != pair.Value) { changed = true; break; }
            if (!changed)
                return;
            block.StaticChunks = new List<StaticChunk>(_staticNow.Count);
            foreach (KeyValuePair<(int X, int Y, int Z), ulong> pair in _staticNow)
            {
                if (block.StaticChunks.Count >= Limits.MaxStaticChunks) break;
                block.StaticChunks.Add(new StaticChunk(pair.Key.X, pair.Key.Y, pair.Key.Z, pair.Value));
            }
            block.StaticChunks.Sort(static (a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            _staticSent.Clear();
            foreach (KeyValuePair<(int X, int Y, int Z), ulong> pair in _staticNow) _staticSent[pair.Key] = pair.Value;
        }

        private void AddStaticOf(mob_eye_camera eye)
        {
            DmList chunks = eye.visibleCameraChunks;
            if (chunks == null) return;
            for (int i = 1; i <= chunks.Count; i++)
            {
                if (chunks[(float)i] is not datum_camerachunk chunk || !_staticChunks.Add(chunk)) continue;
                int cx = (int)(chunk.x ?? 0), cy = (int)(chunk.y ?? 0);
                if (cx < 1 || cy < 1 || cx > Limits.MaxMapSize || cy > Limits.MaxMapSize) continue;
                // every level the chunk covers has a mask, so a level whose tiles are all seen clears its static
                DmList turfs = chunk.turfs;
                if (turfs != null)
                    for (int k = 1; k <= turfs.Count; k++)
                        if (turfs[(float)k] is turf t && t.z >= 1 && t.z <= Limits.MaxMapLevels)
                            _staticNow.TryAdd((cx, cy, (int)t.z), 0UL);
                DmList obscured = chunk.obscuredTurfs;
                if (obscured == null) continue;
                for (int k = 1; k <= obscured.Count; k++)
                {
                    if (obscured[(float)k] is not turf t) continue;
                    int dx = (int)t.x - cx, dy = (int)t.y - cy;
                    if (dx < 0 || dy < 0 || dx >= 8 || dy >= 8 || t.z < 1 || t.z > Limits.MaxMapLevels) continue;
                    var key = (cx, cy, (int)t.z);
                    _staticNow.TryGetValue(key, out ulong mask);
                    _staticNow[key] = mask | (1UL << (dy * 8 + dx));
                    if (obscured[t] is image img) _staticImages.Add(img);
                }
            }
        }

        private void UpdatePopups(client player, bool full)
        {
            OwnBlock block = Block;
            block.Popups = null;
            long serial = player?.dm_popups_serial ?? 0;
            if (!full && serial == _popupsSerialSent)
                return;
            _popupsSerialSent = serial;
            block.Popups = new List<PopupWindow>();
            DmList popups = player?.dm_popups;
            if (popups == null) return;
            for (int i = 1; i <= popups.Count && block.Popups.Count < Limits.MaxPopups; i++)
            {
                if (popups[(float)i] is not string name || popups[name] is not DmList window || window.Count < 3) continue;
                if (System.Text.Encoding.UTF8.GetByteCount(name) > Limits.MaxPopupNameBytes) continue;
                string title = ServerHost.Truncate(window[1f] as string ?? "", Limits.MaxPopupTitleBytes);
                int width = Math.Clamp((int)Dm.num(window[2f]), 1, Limits.MaxPopupSide);
                int height = Math.Clamp((int)Dm.num(window[3f]), 1, Limits.MaxPopupSide);
                block.Popups.Add(new PopupWindow(name, title, width, height));
            }
        }

        private readonly List<ViewArea> _areasNow = new();
        private List<ViewArea> _areasSent = new();

        private void UpdateViewAreas(client player, bool full)
        {
            OwnBlock block = Block;
            block.ViewAreas = null;
            _areasNow.Clear();
            if (player != null)
            {
                // a popup window's map: the turfs its screen objects show (tg's screen_maps[name_map], the map view's vis_contents)
                DmList popups = player.dm_popups;
                for (int i = 1; popups != null && i <= popups.Count && _areasNow.Count < Limits.MaxViewAreas; i++)
                {
                    if (popups[(float)i] is not string name || player.screen_maps?[name + "_map"] is not DmList shown) continue;
                    if (AreaOf(shown, out int x1, out int y1, out int x2, out int y2, out int z))
                        _areasNow.Add(new ViewArea(0, name + "_map", x1, y1, x2, y2, z));
                }
                // a picture-in-picture screen object (the AI's multicamera windows): the turfs in its own vis_contents
                DmList screen = player.screen;
                for (int i = 1; i <= screen.Count && _areasNow.Count < Limits.MaxViewAreas; i++)
                    if (screen[(float)i] is atom_movable_screen_movable_pic_in_pic pip && pip.dm_id != 0
                        && AreaOf(pip.vis_contents, out int x1, out int y1, out int x2, out int y2, out int z))
                        _areasNow.Add(new ViewArea(pip.dm_id, null, x1, y1, x2, y2, z));
            }
            if (!full && _areasNow.SequenceEqual(_areasSent))
                return;
            block.ViewAreas = new List<ViewArea>(_areasNow);
            _areasSent = new List<ViewArea>(_areasNow);
        }

        // The bounding rectangle of the turfs in a list, or of the turfs in the vis_contents of the screen objects in it, on the first
        // turf's level.
        private static bool AreaOf(DmList list, out int x1, out int y1, out int x2, out int y2, out int z)
        {
            x1 = y1 = int.MaxValue;
            x2 = y2 = z = 0;
            for (int i = 1; list != null && i <= list.Count; i++)
            {
                object item = list[(float)i];
                if (item is atom_movable_screen holder && holder.vis_contents != null)
                {
                    for (int k = 1; k <= holder.vis_contents.Count; k++)
                        Widen(holder.vis_contents[(float)k] as turf, ref x1, ref y1, ref x2, ref y2, ref z);
                }
                else
                    Widen(item as turf, ref x1, ref y1, ref x2, ref y2, ref z);
            }
            return z != 0;
        }

        private static void Widen(turf t, ref int x1, ref int y1, ref int x2, ref int y2, ref int z)
        {
            if (t == null || t.x < 1 || t.y < 1 || t.x > Limits.MaxMapSize || t.y > Limits.MaxMapSize || t.z < 1 || t.z > Limits.MaxMapLevels) return;
            if (z == 0) z = (int)t.z;
            if (t.z != z) return;
            x1 = Math.Min(x1, (int)t.x);
            y1 = Math.Min(y1, (int)t.y);
            x2 = Math.Max(x2, (int)t.x);
            y2 = Math.Max(y2, (int)t.y);
        }

        private void UpdateVerbs(client player, bool full)
        {
            OwnBlock block = Block;
            block.Verbs = null;
            IReadOnlyList<CommandVerb> verbs = CommandVerbs.For(player?.mob);
            // a type's verbs are one cached list; a mob given verbs at run time makes a new list each time, compared by its entries
            if (!full && _verbsSent != null && (ReferenceEquals(verbs, _verbsSent) || verbs.SequenceEqual(_verbsSent)))
                return;
            _verbsSent = verbs;
            block.Verbs = new List<VerbEntry>();
            foreach (CommandVerb verb in verbs)
            {
                if (block.Verbs.Count >= Limits.MaxVerbs) break;
                block.Verbs.Add(new VerbEntry(ServerHost.Truncate(verb.Category, Limits.MaxVerbNameBytes), ServerHost.Truncate(verb.Name, Limits.MaxVerbNameBytes), verb.Path));
            }
        }

        private IReadOnlyList<string> _accessSent; // version 43: the access list the last block carried
        private long _eyeSent = -1; // version 51: the eye the last block named (-1 before the first)

        /// <summary>Fills <see cref="Block"/>: the own motion, the new private table entries and the screen entries' changes (all of them added when <paramref name="full"/>).</summary>
        public void Update(Replicator owner, in PrivateInputs inputs, long tick, bool full)
        {
            OwnBlock block = Block;
            block.HasMotion = inputs.HasMotion;
            block.Motion = inputs.HasMotion ? inputs.Motion : default;
            block.Params = inputs.HasMotion ? inputs.Params : default;
            block.InputBufferDepth = inputs.InputBufferDepth;
            block.ActionsDone = inputs.ActionsDone; // version 43
            block.HandsBlocked = inputs.HandsBlocked; // version 89
            block.Access = null;
            if (full || !ReferenceEquals(inputs.Access, _accessSent))
            {
                block.Access = inputs.Access != null ? new List<string>(inputs.Access) : new List<string>();
                _accessSent = inputs.Access;
            }
            block.Eye = null;
            block.ScreenAdded.Clear();
            block.ScreenChanged.Clear();
            block.ScreenRemoved.Clear();
            block.NewPrivateStrings.Clear();
            block.NewPrivateResources.Clear();
            block.NewPrivateAppearances.Clear();
            block.ImagesAdded.Clear();
            block.ImagesChanged.Clear();
            block.ImagesRemoved.Clear();

            // The screen objects now: the client's screen list, the atoms with an id, in increasing id order.
            _current.Clear();
            _groupOf.Clear();
            client player = null;
            if (inputs.ClientId != 0 && owner._registry.TryGet(inputs.ClientId, out object c) && c is client found)
            {
                player = found;
                DmList screen = player.screen;
                // M4c slice 3: BYOND draws no screen object whose invisibility is above the viewer's see_invisible (BYOND ref
                // /atom/var/invisibility); tg hides the empty action palette so (INVISIBILITY_ABSTRACT, action_button.dm:328).
                float seeInvisible = player.mob?.see_invisible ?? Defines.SEE_INVISIBLE_LIVING;
                for (int i = 1; i <= screen.Count; i++)
                    if (screen[(float)i] is atom_movable m && m.dm_id != 0 && m.invisibility <= seeInvisible) _current.Add(m);
                _current.Sort(static (a, b) => a.dm_id.CompareTo(b.dm_id));
                FillGroups(player);
            }
            // Version 51: tg's client.eye when it is another mob (do_observe), read raw; the client's camera looks from it.
            long eye = player?.eye is mob eyeMob && eyeMob != player.mob && eyeMob.dm_id != 0 ? eyeMob.dm_id : 0;
            if (full || eye != _eyeSent)
            {
                block.Eye = eye;
                _eyeSent = eye;
            }

            long previous = 0;
            int count = 0;
            foreach (atom_movable m in _current)
            {
                long id = m.dm_id;
                if (id == previous) continue; // the same screen object listed twice
                previous = id;
                if (++count > Limits.MaxScreenEntries) break;
                string loc = m.screen_loc;
                byte group = _groupOf.TryGetValue(id, out byte g) ? g : HudGroup.None;
                bool known = _shadow.TryGetValue(id, out ScreenShadow shadow);
                // Version 11: the screen object's flick (tg's resist button) and animate() chains (an alert sliding in, a fullscreen
                // fading out), as an entity's (Flick, Animation); the first session whose screen lists the object takes them.
                TableRef flick = known ? shadow.Flick : TableRef.None;
                long flickTick = known ? shadow.FlickTick : 0;
                bool flicked = false;
                if (owner._flicks.Remove(id, out object flickedState))
                {
                    uint flickLook = Tables.InternFlick(m, flickedState);
                    if (flickLook != 0)
                    {
                        flick = new TableRef(flickLook, true);
                        flickTick = tick;
                        flicked = true;
                    }
                }
                bool animated = owner._animations.TryTake(id, tick, known ? shadow.Animation : null, out EntityAnimation animation);
                // Version 26: the filter chains of its vis_contents children, which it draws flattened (tg's health doll's limbs).
                Dictionary<long, EntityAnimation> childAnimations = ChildAnimations(owner, m, tick, known ? shadow.ChildAnimations : null, ref animated);
                EntityAnimation shown = WithChildren(animation, childAnimations, m);
                // Version 12: the clickable vis_contents children (the item in a hand or a slot); a child put in or taken out also
                // changes the holder's flattened picture.
                CollectVisChildren(m);
                long actionTarget = ActionTargetOf(m);
                ScreenInventory inventory = InventoryOf(m);
                if (!known)
                {
                    TableRef appearance = Intern(m);
                    ScreenChildren added = VisChildrenOf(m, null);
                    _shadow[id] = new ScreenShadow { Appearance = appearance, Loc = loc, Group = group, Seen = tick, Flick = flick, FlickTick = flickTick, Animation = animation, Children = added, ChildAnimations = childAnimations, Shown = shown, ActionTarget = actionTarget, Inventory = inventory };
                    block.ScreenAdded.Add(WithInventory(new ScreenEntry(id, appearance, WireLoc(loc)) { Group = group, FlickAppearance = flick, FlickTick = flickTick, Animation = shown, VisChildren = added, ActionTarget = actionTarget }, inventory));
                    continue;
                }
                bool look = owner._appearanceHinted.Contains(id) || VisChildHinted(owner, m) || (_sweepPeriod > 0 && (tick + id) % _sweepPeriod == 0)
                    || !SameChildIds(shadow.Children);
                TableRef current = look ? Intern(m) : shadow.Appearance;
                ScreenChildren children = look ? VisChildrenOf(m, shadow.Children) : shadow.Children;
                // a child's chain names the child's row of the holder's appearance, which the child's new look changes (the doll's limb)
                if (!Equals(shown, shadow.Shown))
                    animated = true;
                if (current != shadow.Appearance || group != shadow.Group || !string.Equals(loc, shadow.Loc, StringComparison.Ordinal) || flicked || animated
                    || !ScreenChildren.Same(children, shadow.Children) || actionTarget != shadow.ActionTarget || inventory != shadow.Inventory)
                    block.ScreenChanged.Add(WithInventory(new ScreenEntry(id, current, WireLoc(loc)) { Group = group, FlickAppearance = flick, FlickTick = flickTick, Animation = shown, VisChildren = children, ActionTarget = actionTarget }, inventory));
                _shadow[id] = new ScreenShadow { Appearance = current, Loc = loc, Group = group, Seen = tick, Flick = flick, FlickTick = flickTick, Animation = animation, Children = children, ChildAnimations = childAnimations, Shown = shown, ActionTarget = actionTarget, Inventory = inventory };
            }

            _gone.Clear();
            foreach (var pair in _shadow)
                if (pair.Value.Seen != tick) _gone.Add(pair.Key);
            _gone.Sort();
            foreach (long id in _gone)
            {
                _shadow.Remove(id);
                block.ScreenRemoved.Add(id);
                // Version 11: a deleted screen object's chain is not kept (a fullscreen is deleted once its fade ended).
                if (!owner._registry.TryGet(id, out object goneObject) || goneObject is not datum { gc_destroyed: null })
                    owner._animations.Forget(id);
            }
            if (full)
            {
                // A Snapshot lists every entry as added and nothing as changed or removed.
                block.ScreenChanged.Clear();
                block.ScreenRemoved.Clear();
            }

            UpdateStatic(player, full); // version 78 (DV386), before the images: it names the static images they leave out
            UpdateImages(owner, player, tick, full);
            UpdatePopups(player, full); // version 78 (DV387)
            UpdateViewAreas(player, full); // version 78 (DV387)
            UpdateVerbs(player, full); // version 78
            UpdateRadials(player, full);
            UpdateUiWindows(player, full);
            UpdateTooltip(player, full);
            UpdateCameraShake(owner, player);
            UpdateHeldMotion(owner, inputs.OwnEntity); // version 54
            if (owner._interest != null) UpdateInterest(owner, player, inputs.Body, tick, full); // M10 slice C
            else UpdateLayer(owner, player, tick); // version 22
            UpdateBody(player, full); // version 23
            UpdateGamePlane(owner, player, tick, full); // version 29
            UpdateVision(player, full); // version 31
            UpdateRadioChannels(owner, player, full); // version 40

            // The private tables' new entries (all of them in a join: the tables start afresh).
            AppearanceTables t = Tables;
            block.FirstNewPrivateString = (uint)t.StringsBefore + 1;
            for (int i = t.StringsBefore; i < t.Strings.Count; i++) block.NewPrivateStrings.Add(t.Strings[i]);
            block.FirstNewPrivateResource = (uint)t.ResourcesBefore + 1;
            for (int i = t.ResourcesBefore; i < t.Resources.Count; i++) block.NewPrivateResources.Add(t.Resources[i]);
            block.FirstNewPrivateAppearance = (uint)t.AppearancesBefore + 1;
            for (int i = t.AppearancesBefore; i < t.Appearances.Count; i++) block.NewPrivateAppearances.Add(t.Appearances[i]);
            ScreenChanges = block.ScreenAdded.Count + block.ScreenChanged.Count + block.ScreenRemoved.Count + block.ImagesAdded.Count + block.ImagesChanged.Count + block.ImagesRemoved.Count;
            NewEntries = block.NewPrivateStrings.Count + block.NewPrivateResources.Count + block.NewPrivateAppearances.Count;
            _current.Clear(); // nothing of the world is kept from one call to the next
        }

        // tg's HUD groups of the client's mob (code/_onclick/hud/hud.dm, screen_groups): an object listed in a group has its
        // number (the first group that lists it); one that no group lists, none. Read raw; nothing is kept past the call.
        private void FillGroups(client player)
        {
            // M10x slice C: an observer looking through a mob's eyes is shown that mob's HUD (tg's show_hud(version, viewmob)), so the
            // groups are the observed mob's
            DmList groups = ((player.mob as mob_dead_observer)?.observetarget?.hud_used ?? player.mob?.hud_used)?.screen_groups;
            if (groups == null) return;
            int top = Math.Min(groups.Count, (int)Limits.MaxHudGroup);
            for (int g = 1; g <= top; g++)
            {
                if (groups[(float)g] is not DmList list) continue;
                for (int i = 1; i <= list.Count; i++)
                    if (list[(float)i] is atom_movable m && m.dm_id != 0)
                        _groupOf.TryAdd(m.dm_id, (byte)g);
            }
        }

        // Version 27: the item of tg's action button (linked_action.target, an item action's: the flashlight of "Toggle Light"), 0 for
        // any other screen object or an action whose target is no atom (a spell's, a mob's own).
        private static long ActionTargetOf(atom_movable screenObject)
            => screenObject is atom_movable_screen_movable_action_button button && button.linked_action?.target is atom target ? target.dm_id : 0;

        // The own client's images (tg's client.images: the do_after progress bar, D128), as private image entries. An image has
        // no world id (it is a plain value object), so the session numbers them in the order it first sees them (a weak table
        // keyed by the image, so nothing is kept alive); a number is never reused in the session. At most MaxPrivateImages
        // are sent (the first in the list's order). Another session never sees them (D40: the block is the session's own).
        private void UpdateImages(Replicator owner, client player, long tick, bool full)
        {
            OwnBlock block = Block;
            _images.Clear();
            if (player != null)
            {
                DmList images = player.images;
                for (int i = 1; i <= images.Count && _images.Count < Limits.MaxPrivateImages; i++)
                {
                    if (images[(float)i] is not image img) continue;
                    if (_staticImages.Contains(img)) continue; // version 78 (DV386): a camera chunk's static travels as its chunk's mask
                    if (!_imageIds.TryGetValue(img, out ImageId box))
                    {
                        box = new ImageId { Value = _nextImageId++ };
                        _imageIds.Add(img, box);
                    }
                    _images.Add((box.Value, img));
                }
                _images.Sort(static (a, b) => a.Id.CompareTo(b.Id));
            }

            long previous = 0;
            foreach ((long id, image img) in _images)
            {
                if (id == previous) continue; // the same image listed twice
                previous = id;
                uint index = Tables.InternTop(img, true);
                // version 35: an override image of nothing hides the entity it hangs on (index 0)
                if (index == 0 && img.@override == 0f) continue;
                var appearance = index == 0 ? default(TableRef) : new TableRef(index, true);
                bool known_host = ReplicatedFields.IsEntity(img.loc, out atom_movable host, out turf host_turf);
                // Version 35 (DV112): an image on an effect this viewer cannot see (the invisible holder of a hallucination's image, tg's
                // INVISIBILITY_OBSERVER) hangs on the effect's tile, as the client has no such entity to draw it on.
                bool unseen_host = known_host && host.invisibility > ReplicatedFields.MaxEntityInvisibility && host.invisibility > (player.mob?.see_invisible ?? 0);
                long attached = known_host && !unseen_host ? host.dm_id : 0;
                // Version 27: the atom it hangs on wherever it is (a tank on the back), and that atom's name, the bar's label.
                atom holderAtom = img.loc as atom;
                long holder = holderAtom?.dm_id ?? 0;
                string holderName = holderAtom == null || string.IsNullOrEmpty(holderAtom.name) ? null : ServerHost.Truncate(holderAtom.name, Limits.MaxImageHolderNameBytes);
                // Version 30 (DV75): an image on a turf (tg's T-ray pulse, image(loc = get_turf(O))) names the tile it is drawn on.
                int turfX = 0, turfY = 0, turfZ = 0;
                if ((unseen_host ? host_turf : img.loc as turf) is turf onTurf && onTurf.x >= 1 && onTurf.x <= Limits.MaxMapSize && onTurf.y >= 1 && onTurf.y <= Limits.MaxMapSize && onTurf.z >= 1 && onTurf.z <= Limits.MaxMapLevels)
                    (turfX, turfY, turfZ) = ((int)onTurf.x, (int)onTurf.y, (int)onTurf.z);
                bool overridden = img.@override != 0f;
                bool known = _imageShadow.TryGetValue(id, out ImageShadow shadow);
                if (!known || shadow.Override != overridden || shadow.Appearance != appearance || shadow.Attached != attached || shadow.Holder != holder
                    || !string.Equals(shadow.HolderName, holderName, StringComparison.Ordinal)
                    || shadow.TurfX != turfX || shadow.TurfY != turfY || shadow.TurfZ != turfZ)
                {
                    var entry = new ImageEntry(id, appearance, attached) { Holder = holder, HolderName = holderName, TurfX = turfX, TurfY = turfY, TurfZ = turfZ, Override = overridden };
                    (known ? block.ImagesChanged : block.ImagesAdded).Add(entry);
                }
                _imageShadow[id] = new ImageShadow { Appearance = appearance, Attached = attached, Holder = holder, HolderName = holderName, TurfX = turfX, TurfY = turfY, TurfZ = turfZ, Override = overridden, Seen = tick };
            }

            _gone.Clear();
            foreach (var pair in _imageShadow)
                if (pair.Value.Seen != tick) _gone.Add(pair.Key);
            _gone.Sort();
            foreach (long id in _gone)
            {
                _imageShadow.Remove(id);
                block.ImagesRemoved.Add(id);
            }

            if (full)
            {
                // A Snapshot lists every image as added and nothing as changed or removed.
                block.ImagesChanged.Clear();
                block.ImagesRemoved.Clear();
            }
            _images.Clear(); // nothing of the world is kept from one call to the next
        }

        // A screen object's appearance in the session's private tables; its own dir stays (nothing else carries it).
        // A screen object draws its vis_contents (AppearanceTables flattens them): a change of one of them is a change of it
        // (tg's health doll sets its limbs' icon states, screen_objects.dm:986-1012).
        private static bool VisChildHinted(Replicator owner, atom_movable m)
        {
            DmList vis = m.vis_contents;
            for (int i = 1; i <= vis.Count; i++)
                if (vis[(float)i] is atom_movable child && owner._appearanceHinted.Contains(child.dm_id)) return true;
            return false;
        }

        // Version 12 (docs/ai/specs/protocol.md section 21): the screen object's clickable vis_contents children in BYOND's draw order,
        // bottom to top (ClientSeam.IsClickableVisChild: not VIS_INHERIT_ID, not mouse_opacity 0, not VIS_HIDE; a registered atom).
        // BYOND sorts by plane, then layer; an inherited plane or layer (VIS_INHERIT_PLANE, VIS_INHERIT_LAYER) and a FLOAT_LAYER are the
        // holder's, a VIS_UNDERLAY child is under the others of its plane and layer, and equal ones keep their vis_contents order. Fills
        // _children and returns whether there are any (false: nothing to send).
        private readonly List<(atom Child, float Plane, int Under, float Layer, int Order)> _children = new();
        private static readonly Comparison<(atom Child, float Plane, int Under, float Layer, int Order)> DrawOrder = static (a, b) =>
            a.Plane != b.Plane ? a.Plane.CompareTo(b.Plane) : a.Under != b.Under ? b.Under.CompareTo(a.Under) : a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Order.CompareTo(b.Order);

        private bool CollectVisChildren(atom_movable m)
        {
            _children.Clear();
            DmList vis = m.vis_contents;
            if (vis.Count == 0)
                return false;
            for (int i = 1; i <= vis.Count && _children.Count < Limits.MaxScreenChildren; i++)
            {
                if (vis[(float)i] is not atom child || child.dm_id == 0 || !ClientSeam.IsClickableVisChild(child))
                    continue;
                bool listed = false;
                foreach (var known in _children)
                    listed |= ReferenceEquals(known.Child, child);
                if (listed)
                    continue;
                int flags = (int)child.vis_flags;
                float plane = (flags & (int)Dm.VIS_INHERIT_PLANE) != 0 ? m.plane : child.plane;
                float layer = (flags & (int)Dm.VIS_INHERIT_LAYER) != 0 || child.layer < 0 ? m.layer : child.layer;
                _children.Add((child, plane, (flags & (int)Dm.VIS_UNDERLAY) != 0 ? 1 : 0, layer, i));
            }
            _children.Sort(DrawOrder);
            return _children.Count > 0;
        }

        // True when the children collected for this tick are the ones sent before (by id and order).
        private bool SameChildIds(ScreenChildren sent)
        {
            int count = sent?.Items.Length ?? 0;
            if (count != _children.Count)
                return false;
            for (int i = 0; i < count; i++)
                if (sent.Items[i].Id != _children[i].Child.dm_id)
                    return false;
            return true;
        }

        // The collected children with their own appearances (each drawn alone, as it is flattened into the holder); the previous list
        // when nothing changed, so that the shadow compares by reference first; null for none.
        private ScreenChildren VisChildrenOf(atom_movable m, ScreenChildren previous)
        {
            if (_children.Count == 0)
                return null;
            var items = new List<ScreenChild>(_children.Count);
            foreach (var c in _children)
            {
                uint look = Tables.InternVisChild(c.Child);
                if (look == 0)
                    continue; // a child that shows nothing has no pixel to click
                items.Add(WithItem(new ScreenChild(c.Child.dm_id, new TableRef(look, true), c.Child.mouse_opacity == Defines.MOUSE_OPACITY_OPAQUE), c.Child));
            }
            if (items.Count == 0)
                return null;
            var made = new ScreenChildren { Items = items.ToArray() };
            return made.Equals(previous) ? previous : made;
        }

        // Version 26: the chains of the holder's vis_contents children (their filter tracks: the flattened picture cannot move a child
        // on its own), each as the recorder sends it; null for none. A child's chain that changed or ended marks the entry animated.
        private static Dictionary<long, EntityAnimation> ChildAnimations(Replicator owner, atom_movable m, long tick, Dictionary<long, EntityAnimation> sent, ref bool animated)
        {
            DmList vis = m.vis_contents;
            Dictionary<long, EntityAnimation> made = null;
            for (int i = 1; i <= vis.Count; i++)
            {
                if (vis[(float)i] is not atom_movable child || child.dm_id == 0)
                    continue;
                EntityAnimation before = null;
                bool wasSent = sent != null && sent.TryGetValue(child.dm_id, out before);
                if (!wasSent && !owner._animations.Has(child.dm_id))
                    continue;
                if (owner._animations.TryTake(child.dm_id, tick, before, out EntityAnimation now))
                    animated = true;
                if (now != null)
                    (made ??= new Dictionary<long, EntityAnimation>())[child.dm_id] = now;
            }
            if (sent != null)
                foreach (long gone in sent.Keys)
                    if (made == null || !made.ContainsKey(gone))
                        animated |= !ContainsChild(vis, gone); // a child taken out of vis_contents: its chain is no longer shown
            return made;
        }

        private static bool ContainsChild(DmList vis, long id)
        {
            for (int i = 1; i <= vis.Count; i++)
                if (vis[(float)i] is atom_movable child && child.dm_id == id) return true;
            return false;
        }

        // The entry's animation: its own tracks, then each child's filter tracks naming the child's row (Child), up to the track limit.
        private EntityAnimation WithChildren(EntityAnimation own, Dictionary<long, EntityAnimation> children, atom_movable m)
        {
            if (children == null)
                return own;
            var tracks = new List<AnimationTrack>(own?.Tracks ?? Array.Empty<AnimationTrack>());
            DmList vis = m.vis_contents;
            for (int i = 1; i <= vis.Count && tracks.Count < Limits.MaxAnimationTracks; i++)
            {
                if (vis[(float)i] is not atom_movable child || !children.TryGetValue(child.dm_id, out EntityAnimation chains))
                    continue;
                uint row = Tables.InternVisChild(child);
                if (row == 0)
                    continue;
                foreach (AnimationTrack t in chains.Tracks)
                {
                    if (t.Filter == 0 || tracks.Count >= Limits.MaxAnimationTracks)
                        continue;
                    tracks.Add(new AnimationTrack
                    {
                        StartTick = t.StartTick, Loop = t.Loop, Parallel = t.Parallel, Start = t.Start, Steps = t.Steps, Filter = t.Filter,
                        FilterStart = t.FilterStart, Child = new TableRef(row, true),
                    });
                }
            }
            return tracks.Count == 0 ? null : new EntityAnimation { Tracks = tracks.ToArray() };
        }

        private TableRef Intern(atom_movable m)
        {
            uint id = Tables.InternTop(m, true);
            return id == 0 ? TableRef.None : new TableRef(id, true);
        }

        // ---- protocol version 15 (M4c slice 4, DV40): the client's radial menus ----

        private readonly Dictionary<long, int> _radialShadow = new(); // the menus sent open: id -> the page serial sent
        private readonly HashSet<long> _radialSeen = new();
        private readonly List<long> _radialGone = new();

        /// <summary>The RadialMenu messages of the last <see cref="Update"/>: the closes, then the opens and new pages (oldest menu first).</summary>
        public List<RadialMenuMessage> PendingRadials { get; } = new();

        // tg's radial menus shown to the client (client.dm_radial_menus, the port's show_to): a menu not sent yet, or whose page
        // changed since (its serial), is an open with the page's slices, each picture interned into the session's private tables
        // (D40: only this session's tables learn them); a menu sent and no longer listed is a close. A join (full) sends every
        // open menu again. Reads only: the menus' records are values tg's code wrote.
        private void UpdateRadials(client player, bool full)
        {
            PendingRadials.Clear();
            if (full) _radialShadow.Clear();
            _radialSeen.Clear();
            var opens = new List<RadialMenuMessage>();
            DmList menus = player?.dm_radial_menus;
            if (menus != null)
            {
                for (int i = 1; i <= menus.Count; i++)
                {
                    if (menus[(float)i] is not datum_radial_menu menu || menu.dm_id == 0 || menu.gc_destroyed != null) continue;
                    if (!_radialSeen.Add(menu.dm_id)) continue;
                    if (_radialShadow.TryGetValue(menu.dm_id, out int serial) && serial == menu.dm_serial) continue;
                    RadialMenuMessage open = RadialOpen(menu);
                    if (open == null) continue;
                    _radialShadow[menu.dm_id] = menu.dm_serial;
                    opens.Add(open);
                }
            }
            _radialGone.Clear();
            foreach (long id in _radialShadow.Keys)
                if (!_radialSeen.Contains(id)) _radialGone.Add(id);
            _radialGone.Sort();
            foreach (long id in _radialGone)
            {
                _radialShadow.Remove(id);
                PendingRadials.Add(new RadialMenuMessage { Kind = RadialMenuKind.Close, MenuId = id });
            }
            PendingRadials.AddRange(opens);
        }

        private RadialMenuMessage RadialOpen(datum_radial_menu menu)
        {
            List<RadialElement> elements = menu.dm_elements;
            if (elements.Count == 0) return null;
            var message = new RadialMenuMessage
            {
                Kind = RadialMenuKind.Open,
                MenuId = menu.dm_id,
                Page = (byte)Math.Clamp((int)menu.current_page, 1, 255),
                Pages = (byte)Math.Clamp((int)menu.pages, 1, 255),
            };
            if (message.Page > message.Pages) message.Page = message.Pages;
            RadialMenuFlags flags = RadialMenuFlags.None;
            if (menu.dm_tooltips != 0) flags |= RadialMenuFlags.Tooltips;
            int animation = (int)menu.button_animation_flags;
            if ((animation & BUTTON_SLIDE_IN) != 0) flags |= RadialMenuFlags.SlideIn;
            if ((animation & BUTTON_FADE_IN) != 0) flags |= RadialMenuFlags.FadeIn;
            if ((animation & BUTTON_FADE_OUT) != 0) flags |= RadialMenuFlags.FadeOut;
            if (menu.dm_close_button != null)
            {
                TableRef center = InternLook(menu.dm_close_button);
                TableRef focus = InternLook(menu.dm_close_button_focus);
                if (!center.IsNone && !focus.IsNone)
                {
                    flags |= RadialMenuFlags.CloseButton;
                    message.Center = center;
                    message.CenterFocus = focus;
                }
            }
            message.Flags = flags;
            foreach (RadialElement e in elements)
            {
                if (message.Elements.Count >= Limits.MaxRadialElements) break;
                TableRef background = InternLook(e.Background);
                TableRef focus = InternLook(e.BackgroundFocus);
                if (background.IsNone || focus.IsNone) return null; // a table overflow: nothing the client could draw
                RadialElementFlags elementFlags = RadialElementFlags.None;
                if (e.NextPage) elementFlags |= RadialElementFlags.NextPage;
                if (e.ClickOnHover) elementFlags |= RadialElementFlags.ClickOnHover;
                string name = string.IsNullOrEmpty(e.Name) ? "?" : ServerHost.Truncate(e.Name, Limits.MaxRadialNameBytes);
                message.Elements.Add(new RadialSlice(name, elementFlags, InternLook(e.Icon), background, focus));
            }
            return message;
        }

        private TableRef InternLook(IDmAppearance look)
        {
            if (look == null) return TableRef.None;
            uint id = Tables.InternTop(look, true);
            return id == 0 ? TableRef.None : new TableRef(id, true);
        }

        private static string WireLoc(string loc) => ReplicatedFields.WireScreenLoc(loc);
    }

    // Version 11: a screen object's chain is kept for the private blocks, which read it after the tick's entities (SessionState.Update).
    private bool IsLiveScreenObject(long id) => _registry.TryGet(id, out object o) && o is atom_movable_screen { gc_destroyed: null };

    // ---- the hint sink (inside the world: it records ids and nothing else) ----

    // The appearance sink the replicator wraps (presentation-seam.md 5): tg's update_light() becomes LightChanged, a hint for the
    // atom's light record (a movable) or for its cell's starlight (a turf). Every call is forwarded to the sink that was there.
    // tg's do_attack_animation (atoms_movable.dm:1545): the first animate of a living mob names pixel_x and pixel_y 8 pixels
    // off its base toward the target, over time 1; BYOND (and Dm.animate) has already set them, so the offset is the lunge's
    // direction. The lunge travels as EntityFlags.Lunge for one tick and its chain is not sent again; every other chain of a movable
    // is the entity's Animation field (version 9, AnimationRecorder).
    /// <summary>The sink of <see cref="HitEffectSeam"/>: tg's attack visual on a replicated movable becomes its one-tick <see cref="EntityFlags.Hit"/>.</summary>
    public IHitEffectSink HitSink => _hitSink ??= new HitSinkImpl(this);

    private IHitEffectSink _hitSink;

    private sealed class HitSinkImpl(Replicator owner) : IHitEffectSink
    {
        public void Hit(atom attacked)
        {
            if (attacked is not atom_movable movable)
                return;
            owner._hits.Add(movable.dm_id);
            owner._pending.Add(new Candidate(movable.dm_id, FromHint));
        }
    }

    private static class Dirs
    {
        public const int North = 1, South = 2, East = 4, West = 8; // BYOND's
    }

    private bool NoteLunge(object target, DmList arguments)
    {
        if (target is not mob_living mob || arguments == null || !Dm.@in("pixel_x", arguments) || !Dm.@in("pixel_y", arguments))
            return false;
        if (!Dm.@in("time", arguments) || Dm.num(arguments["time"]) > 1)
            return false;
        float dx = mob.pixel_x - mob.base_pixel_x;
        float dy = mob.pixel_y - mob.base_pixel_y;
        byte dir = (byte)((dy > 0 ? Dirs.North : dy < 0 ? Dirs.South : 0) | (dx > 0 ? Dirs.East : dx < 0 ? Dirs.West : 0));
        if (dir == 0)
            return false;
        _lunges[mob.dm_id] = dir;
        _pending.Add(new Candidate(mob.dm_id, FromHint));
        return true;
    }

    private sealed class LightHintSink : IAppearanceSink
    {
        private readonly Replicator _owner;
        private readonly IAppearanceSink _next;

        public LightHintSink(Replicator owner, IAppearanceSink next)
        {
            _owner = owner;
            _next = next;
        }

        public void Changed(object atom, AppearanceAspect aspect) => _next.Changed(atom, aspect);

        public void Animate(object target, DmList arguments)
        {
            _owner.NoteLunge(target, arguments);
            _next.Animate(target, arguments);
        }

        // Version 9: every other chain of a movable is recorded for its entity's Animation field (AnimationRecorder).
        public void AnimateStep(object target, object subject, DmList arguments, DmList before)
        {
            if (subject is client camera) // version 21: tg's shake_camera animates the player's client
                _owner.NoteCameraStep(camera, target != null, arguments);
            bool lunge = target != null && _owner.NoteLunge(target, arguments);
            _owner._animations.Step(target, subject, arguments, before, lunge);
            if (!lunge && subject is atom_movable movable && movable.dm_id != 0)
                _owner._pending.Add(new Candidate(movable.dm_id, FromHint));
            else if (subject is dm_filter { Owner: atom_movable owner } && owner.dm_id != 0) // version 26: a filter's chain is its atom's
                _owner._pending.Add(new Candidate(owner.dm_id, FromHint));
            _next.AnimateStep(target, subject, arguments, before);
        }

        public void Flick(object target, object state)
        {
            if (target is atom_movable movable && state is string or DmResource)
            {
                _owner._flicks[movable.dm_id] = state;
                _owner._pending.Add(new Candidate(movable.dm_id, FromHint));
            }
            _next.Flick(target, state);
        }

        public void VisibilityChanged(object atom) => _next.VisibilityChanged(atom);

        public void LightChanged(object atom)
        {
            if (atom is atom_movable movable)
            {
                _owner._pending.Add(new Candidate(movable.dm_id, FromHint));
                // tg's top atom (update_light): a light in a movable's contents is that movable's light (ReplicatedFields.WireLight).
                if (movable.loc is atom_movable top && top.dm_id != 0) _owner._pending.Add(new Candidate(top.dm_id, FromHint));
            }
            else if (atom is turf turf) _owner._pendingTurfs.Add(turf.dm_id);
            _next.LightChanged(atom);
        }
    }

    private sealed class HintSink : IReplicationHintSink
    {
        private readonly Replicator _owner;

        public HintSink(Replicator owner)
        {
            _owner = owner;
        }

        public void Moved(object atom)
        {
            if (atom is atom_movable movable) _owner._pending.Add(new Candidate(movable.dm_id, FromHint));
            else if (atom is turf turf)
            {
                _owner._mapChanged = true; // a replaced turf: the joiners' map is rebuilt, and its cell is re-read
                _owner._pendingTurfs.Add(turf.dm_id);
            }
        }

        public void AppearanceChanged(object atom, AppearanceAspect aspect)
        {
            if (atom is atom_movable movable)
            {
                switch (aspect)
                {
                    case AppearanceAspect.Dir when movable is atom_movable_screen:
                        // a screen entry has no Dir field: its dir is in its appearance (tg's character preview, turned by setDir)
                        _owner._pending.Add(new Candidate(movable.dm_id, (byte)(FromHint | FromAppearance)));
                        break;
                    case AppearanceAspect.Dir:
                    case AppearanceAspect.Glide:
                    case AppearanceAspect.Name:
                        _owner._pending.Add(new Candidate(movable.dm_id, FromHint));
                        break;
                    case AppearanceAspect.VisContents:
                        _owner._visHolders.Add(movable); // version 10: a flick visual may have joined it (flick_overlay_view)
                        // and its appearance flattens its vis_contents (AppearanceTables.AddVisContents): one taken out, the cryo
                        // cell's ejected occupant, must leave the picture
                        _owner._pending.Add(new Candidate(movable.dm_id, (byte)(FromHint | FromAppearance)));
                        break;
                    case AppearanceAspect.Maptext:
                        _owner._maptextHinted.Add(movable.dm_id); // version 18: a screen object's text; the public tables hold none
                        break;
                    case AppearanceAspect.Filters:
                        // version 29: a filter of a private appearance (the game plane master controller's eye blur, a screen object's
                        // outline) re-interns it, as a screen object's text does; an entity's filters are part of its public
                        // appearance (version 26), so the change is an appearance hint like any other
                        _owner._maptextHinted.Add(movable.dm_id);
                        _owner._pending.Add(new Candidate(movable.dm_id, (byte)(FromHint | FromAppearance)));
                        break;
                    case AppearanceAspect.Render:
                        break; // not part of the wire appearance
                    default:
                        _owner._pending.Add(new Candidate(movable.dm_id, (byte)(FromHint | FromAppearance)));
                        break;
                }
            }
            else if (atom is turf turf && aspect != AppearanceAspect.Name && aspect != AppearanceAspect.Glide)
            {
                _owner._pendingTurfs.Add(turf.dm_id);
                if (aspect == AppearanceAspect.VisContents) _owner._visHolders.Add(turf); // version 10: the pickup image's turf
            }
        }

        public void VisibilityChanged(object atom)
        {
            // invisibility decides entityhood (INVISIBILITY_ABSTRACT); opacity is not replicated.
            if (atom is atom_movable movable) _owner._pending.Add(new Candidate(movable.dm_id, FromHint));
        }

        public void Deleted(long id)
        {
            if (_owner._index.ContainsKey(id)) _owner._pending.Add(new Candidate(id, FromDeleted));
            _owner._visShownIn.Remove(id);
        }
    }
}
