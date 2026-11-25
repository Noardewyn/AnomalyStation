using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: M2 P5 (the bodies); the API is the M2 contracts step's (frozen, docs/ai/specs/m2-ownership.md). Users: the Godot
// client (M2 P7 draws appearances and the turf kit from the tables, M2 P8 reads the own state, the screen entries and the
// collision view for its predictor), the bots (M2 P6, predicting clients) and the tests. The version-1 members of
// ReplicaWorld.cs keep working; Apply of a version-2 Snapshot, Delta or MapGrid fills the members below.

/// <summary>
/// The newest private block a connection received (<see cref="OwnBlock"/>), as the replica keeps it: what the predictor
/// reconciles against and what the client's input clock reads.
/// </summary>
/// <param name="Tick">The tick of the Delta or Snapshot that carried it.</param>
/// <param name="AckedSeq">The header's <c>lastInputSeq</c>: the frame <see cref="Motion"/> follows.</param>
/// <param name="HasMotion">Whether the own mob is a capsule mob.</param>
/// <param name="Motion">The authoritative own state after <see cref="AckedSeq"/>.</param>
/// <param name="Params">The motion parameters for the next tick.</param>
/// <param name="InputBufferDepth">Frames waiting in the server's jitter buffer.</param>
public readonly record struct OwnState(long Tick, uint AckedSeq, bool HasMotion, MotionState Motion, MotionParams Params, byte InputBufferDepth)
{
    /// <summary>Version 43: the connection's Action messages the server has settled (<see cref="OwnBlock.ActionsDone"/>).</summary>
    public uint ActionsDone { get; init; }

    /// <summary>Version 89: tg's ClickOn refuses the own mob's clicks now (<see cref="OwnBlock.HandsBlocked"/>).</summary>
    public bool HandsBlocked { get; init; }

    /// <summary>Version 95: the own mob's melee click cooldown, milliseconds (<see cref="OwnBlock.MeleeCooldownMs"/>).</summary>
    public int MeleeCooldownMs { get; init; } = OwnBlock.DefaultMeleeCooldownMs;
}

public sealed partial class ReplicaWorld
{
    private readonly List<string> _resources = new();
    private readonly List<string> _strings = new();
    private readonly List<AppearanceEntry> _appearances = new();
    private readonly List<string> _privateResources = new();
    private readonly List<string> _privateStrings = new();
    private readonly List<AppearanceEntry> _privateAppearances = new();

    // The map's cells as the replica has them: a copy of the MapGrid's solidity bits and the turf appearances, then the Delta's changes.
    private byte[] _solid;
    private uint[] _turfAppearance;
    private long _maxTurfAppearance;
    private readonly List<(int X, int Y, int Z)> _cellsChanged = new();

    private OwnState _own = new(-1, 0, false, default, default, 0);
    private bool _ownPending;
    private readonly Dictionary<long, ScreenEntry> _screen = new();
    private readonly List<ScreenEntry> _screenTouched = new();
    private readonly List<long> _screenGone = new();
    private CollisionViewImpl _collisionView;

    // ---- the tables (public: shared by every connection; private: this connection's) ----

    /// <summary>The public resource table so far (index 1 is element 0).</summary>
    public IReadOnlyList<string> Resources => _resources;

    /// <summary>The public string table so far.</summary>
    public IReadOnlyList<string> Strings => _strings;

    /// <summary>The public appearance table so far.</summary>
    public IReadOnlyList<AppearanceEntry> Appearances => _appearances;

    /// <summary>This connection's private resource table so far (D40: only what its owner may see; a P5 addition).</summary>
    public IReadOnlyList<string> PrivateResources => _privateResources;

    /// <summary>This connection's private string table so far (a P5 addition).</summary>
    public IReadOnlyList<string> PrivateStrings => _privateStrings;

    /// <summary>This connection's private appearance table so far (a P5 addition).</summary>
    public IReadOnlyList<AppearanceEntry> PrivateAppearances => _privateAppearances;

    /// <summary>The appearance a reference names (public or this connection's private table); false for none or an unknown index.</summary>
    public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
    {
        if (reference.Index > LocalBase) // version 43: the client's own entries
            return TryGetLocal(_localAppearances, reference, out entry);
        List<AppearanceEntry> table = reference.Private ? _privateAppearances : _appearances;
        if (reference.Index >= 1 && reference.Index <= table.Count)
        {
            entry = table[(int)reference.Index - 1];
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>The string a reference names, or null for none.</summary>
    public string ResolveString(TableRef reference)
    {
        if (reference.Index > LocalBase) // version 43: the client's own entries
            return TryGetLocal(_localStrings, reference, out string local) ? local : null;
        List<string> table = reference.Private ? _privateStrings : _strings;
        return reference.Index >= 1 && reference.Index <= table.Count ? table[(int)reference.Index - 1] : null;
    }

    /// <summary>The resource a reference names, or null for none.</summary>
    public string ResolveResource(TableRef reference)
    {
        List<string> table = reference.Private ? _privateResources : _resources;
        return reference.Index >= 1 && reference.Index <= table.Count ? table[(int)reference.Index - 1] : null;
    }

    // ---- the map (MapGrid version 2 and the Delta's cell changes) ----

    /// <summary>Whether the cell blocks capsules (the MapGrid's bit, then the <see cref="Delta.SolidChanged"/> records); outside the map: true.</summary>
    /// <summary>True outside the map (and before MapGrid): the edge a noclip capsule stops at.</summary>
    public bool IsOutsideMap(int x, int y, int z) => _map == null || x < 1 || y < 1 || z < 1 || x > _map.Width || y > _map.Height || z > _map.Levels;

    /// <summary>
    /// DV534: whether a lattice or a catwalk (<c>/obj/structure/lattice</c>) stands on the cell, which tg's <c>get_spacemove_backup</c> holds on
    /// to; the cells are found from the entities once a tick (lattices hardly move).
    /// </summary>
    public bool IsLattice(int x, int y, int z)
    {
        if (_latticeCells == null || _latticeTick != _tick || _latticeCount != _byId.Count)
        {
            (_latticeCells ??= new HashSet<(int, int, int)>()).Clear();
            foreach (ReplicaEntity e in Entities)
                if (e.TypePath == "/obj/structure/lattice" || e.TypePath?.StartsWith("/obj/structure/lattice/", StringComparison.Ordinal) == true)
                    _latticeCells.Add((e.State.X, e.State.Y, e.State.Z));
            _latticeTick = _tick;
            _latticeCount = _byId.Count;
        }
        return _latticeCells.Contains((x, y, z));
    }

    private HashSet<(int, int, int)> _latticeCells;
    private long _latticeTick = long.MinValue;
    private int _latticeCount = -1;

    public bool IsSolid(int x, int y, int z)
    {
        if (_map == null || x < 1 || y < 1 || z < 1 || x > _map.Width || y > _map.Height || z > _map.Levels)
            return true;
        int i = CellIndex(x, y, z);
        if (Guessed(i) is bool guessed) // version 43: an airlock the client guessed open, a thing it guessed pushed
            return guessed;
        return _solid != null && (_solid[i >> 3] & (1 << (i & 7))) != 0;
    }

    /// <summary>The cell's turf appearance (MapGrid's runs, then <see cref="Delta.TurfAppearanceChanged"/>).</summary>
    public TableRef TurfAppearance(int x, int y, int z)
    {
        if (_map == null || _turfAppearance == null || x < 1 || y < 1 || z < 1 || x > _map.Width || y > _map.Height || z > _map.Levels)
            return TableRef.None;
        return TableRef.Public(_turfAppearance[CellIndex(x, y, z)]);
    }

    private int CellIndex(int x, int y, int z) => (x - 1) + (y - 1) * _map.Width + (z - 1) * _map.Width * _map.Height;

    private Action<IReadOnlyList<(int X, int Y, int Z)>> _cellsChangedHandlers;

    /// <summary>Raised after an applied Delta changed cells (solidity or turf appearance): the cells changed, in the Delta's order.</summary>
    public event Action<IReadOnlyList<(int X, int Y, int Z)>> CellsChanged
    {
        add => _cellsChangedHandlers += value;
        remove => _cellsChangedHandlers -= value;
    }

    // MapGrid version 2: the replica keeps its own copy of the cells, so a Delta changes it and the MapGrid object stays as received.
    private void TakeMapCells(MapGrid grid)
    {
        int cells = grid.CellCount;
        _solid = new byte[(cells + 7) / 8];
        if (grid.SolidBits != null && grid.SolidBits.Length == _solid.Length)
            Array.Copy(grid.SolidBits, _solid, _solid.Length);
        else if (grid.SolidBits != null && grid.SolidBits.Length != 0)
            throw Bad("the map's solidity bits are " + grid.SolidBits.Length + " bytes, the map needs " + _solid.Length);
        _turfAppearance = null;
        _maxTurfAppearance = 0;
        if (grid.AppearanceRuns.Count > 0)
        {
            _turfAppearance = new uint[cells];
            long next = 0;
            foreach (TurfRun run in grid.AppearanceRuns)
            {
                if (run.Count < 1 || run.Count > cells - next || run.TypeIndex < 0 || run.TypeIndex > Limits.MaxAppearanceTableEntries)
                    throw Bad("a map appearance run does not fit the map");
                Array.Fill(_turfAppearance, (uint)run.TypeIndex, (int)next, run.Count);
                next += run.Count;
                _maxTurfAppearance = Math.Max(_maxTurfAppearance, run.TypeIndex);
            }

            if (next != cells)
                throw Bad("the map appearance runs cover " + next + " of " + cells + " cells");
        }
    }

    private void ApplyCellChanges(Delta delta)
    {
        _cellsChanged.Clear();
        foreach (CellSolidChange c in delta.SolidChanged)
        {
            CheckCell(c.X, c.Y, c.Z);
            int i = CellIndex(c.X, c.Y, c.Z);
            if (c.Solid)
                _solid[i >> 3] |= (byte)(1 << (i & 7));
            else
                _solid[i >> 3] &= (byte)~(1 << (i & 7));
            _cellsChanged.Add((c.X, c.Y, c.Z));
        }

        foreach (CellAppearanceChange c in delta.TurfAppearanceChanged)
        {
            CheckCell(c.X, c.Y, c.Z);
            if (c.Appearance.Private || c.Appearance.Index > _appearances.Count)
                throw Bad("a turf appearance change names an entry the table does not have");
            _turfAppearance ??= new uint[_map.CellCount];
            _turfAppearance[CellIndex(c.X, c.Y, c.Z)] = c.Appearance.Index;
            _maxTurfAppearance = Math.Max(_maxTurfAppearance, c.Appearance.Index);
            _cellsChanged.Add((c.X, c.Y, c.Z));
        }

        ApplyCellChangesV3(delta);
        ApplyCellChangesV5(delta);
        ApplyCeilings(delta);
        SunAzimuth = delta.SunAzimuth;
    }

    private void CheckCell(int x, int y, int z)
    {
        if (_map == null || x < 1 || y < 1 || z < 1 || x > _map.Width || y > _map.Height || z > _map.Levels)
            throw Bad("a cell change at " + x + "," + y + "," + z + " is outside the map");
    }

    // ---- the tables' updates ----

    // The lengths of the tables an entry's references are checked against (a Snapshot checks the tables it brings before
    // it replaces the replica's own).
    private readonly record struct TableCounts(int Resources, int Strings, int Appearances, int PrivateResources, int PrivateStrings, int PrivateAppearances);

    private TableCounts Counts() => new(_resources.Count, _strings.Count, _appearances.Count, _privateResources.Count, _privateStrings.Count, _privateAppearances.Count);

    private void ValidateSnapshotTables(Snapshot snapshot)
    {
        var counts = new TableCounts(snapshot.Resources.Count, snapshot.Strings.Count, snapshot.Appearances.Count, 0, 0, 0);
        for (int i = 0; i < snapshot.Appearances.Count; i++)
        {
            if (snapshot.Appearances[i] == null) throw Bad("a null appearance entry");
            CheckAppearance(snapshot.Appearances[i], false, (uint)i + 1, counts);
        }

        if (_maxTurfAppearance > snapshot.Appearances.Count)
            throw Bad("the map's turf appearances name entries the Snapshot's table does not have");
    }

    private static void ValidateSnapshotOwn(Snapshot snapshot)
    {
        OwnBlock own = snapshot.Own ?? throw Bad("a Snapshot without a private block");
        if (own.ScreenChanged.Count != 0 || own.ScreenRemoved.Count != 0)
            throw Bad("a Snapshot's private block lists screen entries as changed or removed");
        if ((own.NewPrivateStrings.Count > 0 && own.FirstNewPrivateString != 1) || (own.NewPrivateResources.Count > 0 && own.FirstNewPrivateResource != 1)
            || (own.NewPrivateAppearances.Count > 0 && own.FirstNewPrivateAppearance != 1))
            throw Bad("a Snapshot's private tables do not start at index 1");
        var counts = new TableCounts(snapshot.Resources.Count, snapshot.Strings.Count, snapshot.Appearances.Count,
            own.NewPrivateResources.Count, own.NewPrivateStrings.Count, own.NewPrivateAppearances.Count);
        for (int i = 0; i < own.NewPrivateAppearances.Count; i++)
        {
            if (own.NewPrivateAppearances[i] == null) throw Bad("a null appearance entry");
            CheckAppearance(own.NewPrivateAppearances[i], true, (uint)i + 1, counts);
        }

        if (own.ScreenAdded.Count > Limits.MaxScreenEntries)
            throw Bad("a Snapshot lists more than " + Limits.MaxScreenEntries + " screen entries");
        foreach (ScreenEntry entry in own.ScreenAdded)
        {
            TableRef a = entry.Appearance;
            if (a.Index > (a.Private ? counts.PrivateAppearances : counts.Appearances))
                throw Bad("screen entry " + entry.Id + " names an appearance the table does not have");
            TableRef f = entry.FlickAppearance; // version 11
            if (f.Index > (f.Private ? counts.PrivateAppearances : counts.Appearances))
                throw Bad("screen entry " + entry.Id + " flicks an appearance the table does not have");
            if (entry.VisChildren != null) // version 12
                foreach (ScreenChild c in entry.VisChildren.Items)
                    if (c.Appearance.Index > (c.Appearance.Private ? counts.PrivateAppearances : counts.Appearances))
                        throw Bad("screen entry " + entry.Id + "'s vis child " + c.Id + " names an appearance the table does not have");
            if (entry.Group > Limits.MaxHudGroup)
                throw Bad("screen entry " + entry.Id + " is in HUD group " + entry.Group + ", over " + Limits.MaxHudGroup);
        }

        ValidateSnapshotImages(own, counts);
    }

    private void ReplaceTables(Snapshot snapshot)
    {
        _resources.Clear();
        _resources.AddRange(snapshot.Resources);
        _strings.Clear();
        _strings.AddRange(snapshot.Strings);
        _appearances.Clear();
        _appearances.AddRange(snapshot.Appearances);
    }

    private void AppendTables(Delta delta)
    {
        AppendStrings(_resources, delta.FirstNewResource, delta.NewResources, "resource");
        AppendStrings(_strings, delta.FirstNewString, delta.NewStrings, "string");
        if (delta.NewAppearances.Count > 0)
        {
            if (delta.FirstNewAppearance != _appearances.Count + 1)
                throw Bad("a Delta adds appearances from index " + delta.FirstNewAppearance + " to a table of " + _appearances.Count);
            foreach (AppearanceEntry entry in delta.NewAppearances)
            {
                if (entry == null) throw Bad("a null appearance entry");
                _appearances.Add(entry);
                CheckAppearance(entry, false, (uint)_appearances.Count, Counts());
            }
        }
    }

    private static void AppendStrings(List<string> table, uint first, List<string> added, string what)
    {
        if (added.Count == 0)
            return;
        if (first != table.Count + 1)
            throw Bad("a Delta adds " + what + "s from index " + first + " to a table of " + table.Count);
        foreach (string s in added)
        {
            if (s == null) throw Bad("a null " + what);
            table.Add(s);
        }
    }

    // The references of an entry must name entries that exist (the codec cannot know the tables' lengths), a public entry
    // only public ones, and an overlay one of its own table that precedes it (no cycles).
    private static void CheckAppearance(AppearanceEntry e, bool isPrivate, uint ownIndex, in TableCounts counts)
    {
        CheckRef(e.Icon, isPrivate, counts.Resources, counts.PrivateResources, "an appearance's icon");
        CheckRef(e.IconState, isPrivate, counts.Strings, counts.PrivateStrings, "an appearance's icon state");
        CheckRef(e.Maptext, isPrivate, counts.Strings, counts.PrivateStrings, "an appearance's maptext");
        for (int i = 0; i < e.OverlayCount; i++)
            CheckOverlay(e.Overlays[i], isPrivate, ownIndex, counts);
        for (int i = 0; i < e.UnderlayCount; i++)
            CheckOverlay(e.Underlays[i], isPrivate, ownIndex, counts);
    }

    private static void CheckRef(TableRef r, bool isPrivate, int publicCount, int privateCount, string what)
    {
        if (r.Private && !isPrivate)
            throw Bad(what + " names a private entry in a public appearance");
        if (r.Index > (r.Private ? privateCount : publicCount))
            throw Bad(what + " names an entry the table does not have");
    }

    private static void CheckOverlay(TableRef r, bool isPrivate, uint ownIndex, in TableCounts counts)
    {
        CheckRef(r, isPrivate, counts.Appearances, counts.PrivateAppearances, "an overlay");
        if (r.IsNone)
            throw Bad("an overlay names no entry");
        if (r.Private == isPrivate && r.Index >= ownIndex)
            throw Bad("an overlay names an entry that does not precede the appearance that holds it");
    }

    // ---- the private block ----

    /// <summary>The newest private block's state; <see cref="OwnState.Tick"/> is -1 before the first.</summary>
    public OwnState Own => _own;

    private Action<OwnState> _ownStateHandlers;

    /// <summary>Raised after every applied Snapshot or Delta, once its private block is applied: the predictor's reconciliation point.</summary>
    public event Action<OwnState> OwnStateReceived
    {
        add => _ownStateHandlers += value;
        remove => _ownStateHandlers -= value;
    }

    /// <summary>The player's <c>client.screen</c> entries now, by id.</summary>
    public IReadOnlyDictionary<long, ScreenEntry> Screen => _screenView ?? _screen;

    private Action<ScreenEntry> _screenChangedHandlers;

    /// <summary>Raised after a message added or changed a screen entry (once per entry and message, after the message is applied).</summary>
    public event Action<ScreenEntry> ScreenEntryChanged
    {
        add => _screenChangedHandlers += value;
        remove => _screenChangedHandlers -= value;
    }

    private Action<long> _screenRemovedHandlers;

    /// <summary>Raised after a message removed a screen entry: its id.</summary>
    public event Action<long> ScreenEntryRemoved
    {
        add => _screenRemovedHandlers += value;
        remove => _screenRemovedHandlers -= value;
    }

    private void ApplyOwn(OwnBlock block, long tick, uint ackedSeq, bool isSnapshot)
    {
        if (isSnapshot)
        {
            _privateResources.Clear();
            _privateStrings.Clear();
            _privateAppearances.Clear();
        }

        AppendPrivate(_privateStrings, block.FirstNewPrivateString, block.NewPrivateStrings, "string");
        AppendPrivate(_privateResources, block.FirstNewPrivateResource, block.NewPrivateResources, "resource");
        if (block.NewPrivateAppearances.Count > 0)
        {
            if (block.FirstNewPrivateAppearance != _privateAppearances.Count + 1)
                throw Bad("a private block adds appearances from index " + block.FirstNewPrivateAppearance + " to a table of " + _privateAppearances.Count);
            foreach (AppearanceEntry entry in block.NewPrivateAppearances)
            {
                if (entry == null) throw Bad("a null appearance entry");
                _privateAppearances.Add(entry);
                CheckAppearance(entry, true, (uint)_privateAppearances.Count, Counts());
            }
        }

        _screenTouched.Clear();
        _screenGone.Clear();
        if (isSnapshot)
        {
            if (block.ScreenChanged.Count != 0 || block.ScreenRemoved.Count != 0)
                throw Bad("a Snapshot's private block lists screen entries as changed or removed");
            foreach (long id in _screen.Keys)
                _screenGone.Add(id);
            _screen.Clear();
        }

        if ((long)block.ScreenAdded.Count + block.ScreenChanged.Count + _screen.Count > Limits.MaxScreenEntries + (long)block.ScreenRemoved.Count)
            throw Bad("the client.screen would hold more than " + Limits.MaxScreenEntries + " entries");
        foreach (ScreenEntry entry in block.ScreenAdded)
        {
            CheckScreenEntry(entry);
            if (_screen.ContainsKey(entry.Id))
                throw Bad("screen entry " + entry.Id + " is added twice");
            _screen[entry.Id] = entry;
            _screenTouched.Add(entry);
            _screenGone.Remove(entry.Id);
        }

        foreach (ScreenEntry entry in block.ScreenChanged)
        {
            CheckScreenEntry(entry);
            if (!_screen.ContainsKey(entry.Id))
                throw Bad("screen entry " + entry.Id + " changes and does not exist");
            _screen[entry.Id] = entry;
            _screenTouched.Add(entry);
        }

        foreach (long id in block.ScreenRemoved)
        {
            if (!_screen.Remove(id))
                throw Bad("screen entry " + id + " is removed and does not exist");
            _screenGone.Add(id);
        }

        ApplyImages(block, isSnapshot);
        ApplyLayer(block, tick); // version 22
        if (block.BodyChanged || isSnapshot) // version 23: a Snapshot's block names the body or there is none
            OwnBody = block.BodyChanged ? block.Body : null;
        if (block.GamePlaneChanged || isSnapshot) // version 29: a Snapshot's block names the game plane or there is none
        {
            if (block.GamePlaneChanged && !block.GamePlane.IsNone && !TryGetAppearance(block.GamePlane, out _))
                throw Bad("the game plane names an appearance outside its table");
            GamePlane = block.GamePlaneChanged ? block.GamePlane : TableRef.None;
        }
        if (block.VisionChanged || isSnapshot) // version 31: a Snapshot's block names the vision or the mob sees as a person does
            OwnVision = block.VisionChanged ? block.Vision : VisionState.Normal;
        if (block.Access != null || isSnapshot) // version 43: a Snapshot's block names the access or the mob has none
            OwnAccess = block.Access ?? new List<string>();
        if (block.Eye != null || isSnapshot) // version 51: a Snapshot's block names the eye or the eye is the own mob
            EyeEntity = block.Eye ?? 0;
        ApplyOwnV78(block, isSnapshot); // version 78
        _own = new OwnState(tick, ackedSeq, block.HasMotion, block.Motion, block.Params, block.InputBufferDepth) { ActionsDone = block.ActionsDone, HandsBlocked = block.HandsBlocked, MeleeCooldownMs = block.MeleeCooldownMs };
        _ownPending = true;
    }

    private static void AppendPrivate(List<string> table, uint first, List<string> added, string what)
    {
        if (added.Count == 0)
            return;
        if (first != table.Count + 1)
            throw Bad("a private block adds " + what + "s from index " + first + " to a table of " + table.Count);
        foreach (string s in added)
        {
            if (s == null) throw Bad("a null " + what);
            table.Add(s);
        }
    }

    private void CheckScreenEntry(in ScreenEntry entry)
    {
        TableRef a = entry.Appearance;
        if (a.Index > (a.Private ? _privateAppearances.Count : _appearances.Count))
            throw Bad("screen entry " + entry.Id + " names an appearance the table does not have");
        TableRef f = entry.FlickAppearance; // version 11
        if (f.Index > (f.Private ? _privateAppearances.Count : _appearances.Count))
            throw Bad("screen entry " + entry.Id + " flicks an appearance the table does not have");
        if (entry.VisChildren != null) // version 12
            foreach (ScreenChild c in entry.VisChildren.Items)
                if (c.Appearance.Index > (c.Appearance.Private ? _privateAppearances.Count : _appearances.Count))
                    throw Bad("screen entry " + entry.Id + "'s vis child " + c.Id + " names an appearance the table does not have");
        if (entry.Group > Limits.MaxHudGroup)
            throw Bad("screen entry " + entry.Id + " is in HUD group " + entry.Group + ", over " + Limits.MaxHudGroup);
    }

    // Raised once the whole message is applied (called by Raise).
    private void RaiseV2()
    {
        if (_cellsChanged.Count > 0)
        {
            var cells = _cellsChanged.ToArray();
            _cellsChanged.Clear();
            _cellsChangedHandlers?.Invoke(cells);
        }

        if (_ownPending)
        {
            _ownPending = false;
            ScreenEntry[] touched = _screenTouched.ToArray();
            long[] gone = _screenGone.ToArray();
            _screenTouched.Clear();
            _screenGone.Clear();
            if (_screenGuess.Count > 0)
                RebuildScreenView();
            foreach (long id in gone)
                if (!_screenGuess.ContainsKey(id)) // version 43: a guessed entry stays as guessed
                    _screenRemovedHandlers?.Invoke(id);
            foreach (ScreenEntry entry in touched)
                if (!_screenGuess.ContainsKey(entry.Id))
                    _screenChangedHandlers?.Invoke(entry);
            RaiseImages();
            _ownStateHandlers?.Invoke(_own);
        }
    }

    // ---- the predictor's view ----

    /// <summary>
    /// The collision view of this replica for the own capsule's predictor (D103, scope 2d): solidity from the map, and the
    /// other standing capsule mobs at their NEWEST received <see cref="NetField.Pos"/> (not the render-delayed position),
    /// lying ones left out (D51), the own entity excluded by the caller's id. One instance per replica, valid for its life.
    /// Every capsule has the default radius (the server's capsules differ only by their parameters, which are not replicated for others).
    /// </summary>
    public ICollisionView CollisionView => _collisionView ??= new CollisionViewImpl(this);

    private sealed class CollisionViewImpl : ICollisionView
    {
        private readonly ReplicaWorld _world;
        private long _frameTick = long.MinValue; // DV403: the server tick of the frame being stepped (TickOfFrame)

        public CollisionViewImpl(ReplicaWorld world)
        {
            _world = world;
        }

        // DV403: a tile at the stepping frame's tick, where the replica holds what will stand there (a tram's car moves on)
        private (int X, int Y) At(int x, int y, int z) => _frameTick == long.MinValue ? (x, y) : _world.TramMap(x, y, z, _frameTick);

        public bool IsSolid(int x, int y, int z) { (x, y) = At(x, y, z); return _world.IsSolid(x, y, z); }

        public uint SteppingFrame
        {
            set
            {
                _world.GuessFrame = value;
                _frameTick = _world.TickOfFrame(value);
            }
        }

        public bool IsOutsideMap(int x, int y, int z) { (x, y) = At(x, y, z); return _world.IsOutsideMap(x, y, z); }

        public bool IsClimbWalkable(int x, int y, int z) { (x, y) = At(x, y, z); return _world.IsClimbWalkable(x, y, z); }

        public bool IsFlaps(int x, int y, int z) { (x, y) = At(x, y, z); return _world.IsFlaps(x, y, z); }

        public bool IsHandhold(int x, int y, int z) { (x, y) = At(x, y, z); return _world.IsSolid(x, y, z) || _world.IsLattice(x, y, z); }

        public TileEdges EdgesOf(int x, int y, int z) { (x, y) = At(x, y, z); return _world.FacesOf(x, y, z); }

        public int LevelShift(int x, int y, int z, byte dir) { (x, y) = At(x, y, z); return _world.LevelShiftGuess(x, y, z, dir); }

        public bool OnLift(int x, int y, int z) { (x, y) = At(x, y, z); return _world.LiftAt(x, y, z); }

        public bool LadderAt(int x, int y, int z) { (x, y) = At(x, y, z); return _world.LadderAt(x, y, z); }

        public (int Dx, int Dy) TramCarry(int x, int y, int z) => _frameTick == long.MinValue ? (0, 0) : _world.TramCarry(x, y, z, _frameTick);

        public (float Dx, float Dy) TramDrawShift(int x, int y, int z, float phase)
            => _frameTick == long.MinValue ? (0f, 0f) : _world.TramDrawShift(x, y, z, _frameTick, _frameTick + (double)phase);

        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
        {
            foreach (ReplicaEntity e in _world.Entities)
            {
                EntityState s = e.State;
                if (!s.HasPos || s.Id == exclude || s.Z != z || PostureBits.IsLying(s.Posture))
                    continue;
                int px = s.PosX, py = s.PosY;
                if (_frameTick != long.MinValue && _world.TramTrips.Count > 0)
                {
                    // DV403: a capsule on a tram is where the car will have carried it by the frame's tick
                    (int dx, int dy) = _world.TramMoved(MotionMath.TileOf(px), MotionMath.TileOf(py), z, _world.Tick, _frameTick);
                    px += dx * MotionMath.SubunitsPerTile;
                    py += dy * MotionMath.SubunitsPerTile;
                }
                if (Math.Abs(px - x) > range || Math.Abs(py - y) > range)
                    continue;
                into.Add(new CapsuleObstacle(s.Id, px, py, e.TypePath?.StartsWith(MechPath, StringComparison.Ordinal) == true ? MotionMath.MechRadius : MotionMath.DefaultRadius));
            }
        }

        private const string MechPath = "/obj/vehicle/sealed/mecha";
    }
}

public sealed partial class ReplicaEntity
{
    /// <summary>True when the entity is a capsule mob (it has sent <see cref="NetField.Pos"/>); its continuous position is then <see cref="EntityState.PosX"/>, <see cref="EntityState.PosY"/>.</summary>
    public bool HasPos => _state.HasPos;
}
