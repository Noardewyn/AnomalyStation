using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Replication;

// M10 slice C (D23, D37, D40; docs/ai/specs/multiplayer.md section 6): interest management. With ReplicatorOptions.Interest set, the shared
// Delta body carries no entity: every entity goes through each session's private block (the lists the observer layer has used since
// version 22), and a session's client holds only the entities its eye is interested in:
// - the chunks (8 by 8 tiles) next to the eye's chunk (InterestOptions.NearChunks), whatever the walls;
// - within the far radius (InterestOptions.FarTiles): the entities of the rooms the eye sees, tg's areas linked through a transparent
//   boundary (two tiles of different areas side by side, neither opaque: a window, an open door, a glass airlock), up to
//   InterestOptions.RoomDepth rooms away, every light source that is not a mob (the client lights the map from them, D24) and
//   every door (the cell-polled types: airlocks, firelocks, windoors, flaps; seen far down a corridor, they must not pop in);
// - the own mob always; a layer entity (a ghost) only within the mob's see_invisible, as before; DEVIATION(DV382): the mob that is the
//   session's client.eye (a camera eye, tg's /mob/eye, INVISIBILITY_OBSERVER) always, as the own mob: the client looks from it.
// An entity it no longer wants is despawned after InterestOptions.HoldTicks, so walking along an edge does not spawn and despawn it.
// Sounds, hearing and radio do not depend on it (D23); the map's cells stay whole.
// The performance plan (docs/ai/specs/performance.md P3.7, P3.8): an entity the sets bring in is queued and spawned nearest the eye first,
// InterestOptions.SpawnBudget a tick (the own mob, the mobs, the near doors, the visuals and what the world made this tick at once); and
// the sets are left past a margin (NearLeaveChunks, FarLeaveTiles, RoomHoldTicks), so walking to and fro over a chunk's border or a
// door's cycle does not send the same things again.
public sealed partial class Replicator
{
    private const int ChunkTiles = 8;

    private readonly InterestOptions _interest;

    // The tick's entity events for the sessions' interest (both layers); the tick's change records are in _layerChanged.
    private readonly List<long> _tickSpawned = new();
    private readonly List<long> _tickMoved = new();
    private readonly List<long> _tickDespawned = new();

    // Space: the area of every tile, the entities by chunk and by area, and the areas' transparent boundaries.
    private bool _spaceBuilt;
    private int _chunksX, _chunksY;
    private int[] _tileArea;
    private readonly Dictionary<area, int> _areaIndex = new(ReferenceEqualityComparer.Instance);
    private readonly List<HashSet<long>> _areaEntities = new();
    private readonly List<Dictionary<int, int>> _portals = new(); // per area: a neighbour area -> its transparent tile pairs
    private readonly Dictionary<int, HashSet<long>> _chunkEntities = new();
    private long _roomsVersion; // grows whenever two areas become linked or unlinked

    /// <summary>True when the replicator filters entities by each session's interest (<see cref="ReplicatorOptions.Interest"/>).</summary>
    public bool InterestOn => _interest != null;

    /// <summary>The areas tg's map has, as the interest indexes them (tests and diagnostics).</summary>
    public int AreaCount => _areaEntities.Count;

    /// <summary>Diagnostics: true when the area of the tile is among the rooms the session's eye sees.</summary>
    public bool SeesArea(int session, int x, int y, int z)
        => _spaceBuilt && InMap(x, y, z) && _sessions.TryGetValue(session, out SessionState s) && s.SeesArea(_tileArea[CellIndex(x, y, z)]);

    /// <summary>Diagnostics: true when the session's client holds the entity.</summary>
    public bool Holds(int session, long id) => _sessions.TryGetValue(session, out SessionState s) && s.Holds(id);

    /// <summary>Diagnostics: the entities the session's client wants and does not have yet (P3.7's queue; tests wait for a join's stream).</summary>
    public int QueuedCount(int session) => _sessions.TryGetValue(session, out SessionState s) ? s.QueuedCount : 0;

    private int CellIndex(int x, int y, int z) => ((z - 1) * _height + (y - 1)) * _width + (x - 1);

    private bool InMap(int x, int y, int z) => x >= 1 && y >= 1 && z >= 1 && x <= _width && y <= _height && z <= _levels;

    private int ChunkOf(int x, int y, int z) => ((z - 1) * _chunksY + (y - 1) / ChunkTiles) * _chunksX + (x - 1) / ChunkTiles;

    private int ChunkOfCell(int cell)
    {
        int x = cell % _width + 1, rest = cell / _width;
        return ChunkOf(x, rest % _height + 1, rest / _height + 1);
    }

    private bool Transparent(int cell) => (_cellFlags[cell] & CellFlags.Opaque) == 0;

    // Builds the indexes once the cells exist (and again after the map's size changed: BuildCells clears _spaceBuilt).
    private void EnsureSpace()
    {
        if (_spaceBuilt || _cellFlags == null)
            return;
        _spaceBuilt = true;
        _chunksX = (_width + ChunkTiles - 1) / ChunkTiles;
        _chunksY = (_height + ChunkTiles - 1) / ChunkTiles;
        _areaIndex.Clear();
        _areaEntities.Clear();
        _portals.Clear();
        _chunkEntities.Clear();
        _tileArea = new int[CellCount];
        int i = 0;
        for (int z = 1; z <= _levels; z++)
            for (int y = 1; y <= _height; y++)
                for (int x = 1; x <= _width; x++, i++)
                    _tileArea[i] = AreaIndexOf(MapView.TurfAt(_world, x, y, z)?.loc as area);
        i = 0;
        for (int z = 1; z <= _levels; z++)
            for (int y = 1; y <= _height; y++)
                for (int x = 1; x <= _width; x++, i++)
                {
                    if (!Transparent(i)) continue;
                    if (x < _width) LinkIfTransparent(i, i + 1, +1);
                    if (y < _height) LinkIfTransparent(i, i + _width, +1);
                }
        for (int slot = 0; slot < _slotCount; slot++)
            if (_slots[slot].Id != 0) Place(ref _slots[slot]);
        BuildStacks();
        _roomsVersion++;
    }

    // D325: each level's multi-z stack (tg's ZTRAIT_DOWN links a level to the one below it), lowest and highest level, read raw as
    // ReadMapGrid reads the traits; a session holds the chunks round its eye on every level of the eye's stack.
    private int[] _stackLow = Array.Empty<int>(), _stackHigh = Array.Empty<int>();

    private void BuildStacks()
    {
        _stackLow = new int[_levels + 1];
        _stackHigh = new int[_levels + 1];
        DmList levels = Dm.each<datum_controller_subsystem_mapping>(TgState.Of(_world).Master?.subsystems).FirstOrDefault()?.z_list;
        bool LinkedBelow(int z) => z > 1 && z <= (levels?.Count ?? 0) && levels[z] is datum_space_level level && Dm.truthy(level.traits?[ZTRAIT_DOWN]);
        for (int z = 1; z <= _levels; z++)
            _stackLow[z] = LinkedBelow(z) ? _stackLow[z - 1] : z;
        for (int z = _levels; z >= 1; z--)
            _stackHigh[z] = z < _levels && LinkedBelow(z + 1) ? _stackHigh[z + 1] : z;
    }

    private int StackLow(int z) => z >= 1 && z < _stackLow.Length ? _stackLow[z] : z;

    private int StackHigh(int z) => z >= 1 && z < _stackHigh.Length ? _stackHigh[z] : z;

    private int AreaIndexOf(area a)
    {
        if (a == null) return -1;
        if (_areaIndex.TryGetValue(a, out int index)) return index;
        index = _areaEntities.Count;
        _areaIndex.Add(a, index);
        _areaEntities.Add(new HashSet<long>());
        _portals.Add(new Dictionary<int, int>());
        return index;
    }

    // A transparent tile next to another of a different area adds (or removes) one to the two areas' link.
    private void LinkIfTransparent(int cell, int other, int delta)
    {
        int a = _tileArea[cell], b = _tileArea[other];
        if (a < 0 || b < 0 || a == b || !Transparent(other)) return;
        Link(a, b, delta);
        Link(b, a, delta);
    }

    private void Link(int a, int b, int delta)
    {
        Dictionary<int, int> links = _portals[a];
        links.TryGetValue(b, out int count);
        int now = count + delta;
        if (now <= 0) links.Remove(b);
        else links[b] = now;
        if ((count > 0) != (now > 0)) _roomsVersion++;
    }

    // A tile's opacity changed (a door opened or closed, a wall fell): its links to the next tiles of other areas follow.
    private void OnOpacityChanged(int x, int y, int z, int cell)
    {
        if (!_spaceBuilt) return;
        int delta = Transparent(cell) ? +1 : -1;
        if (x > 1) LinkIfTransparent(cell, cell - 1, delta);
        if (x < _width) LinkIfTransparent(cell, cell + 1, delta);
        if (y > 1) LinkIfTransparent(cell, cell - _width, delta);
        if (y < _height) LinkIfTransparent(cell, cell + _width, delta);
    }

    private void Place(ref Shadow s)
    {
        if (!InMap(s.X, s.Y, s.Z))
        {
            s.Area = -1;
            s.Chunk = -1;
            return;
        }
        s.Area = _tileArea[CellIndex(s.X, s.Y, s.Z)];
        s.Chunk = ChunkOf(s.X, s.Y, s.Z);
        if (s.Area >= 0) _areaEntities[s.Area].Add(s.Id);
        if (!_chunkEntities.TryGetValue(s.Chunk, out HashSet<long> ids)) _chunkEntities[s.Chunk] = ids = new HashSet<long>();
        ids.Add(s.Id);
    }

    private void Unplace(ref Shadow s)
    {
        if (s.Area >= 0) _areaEntities[s.Area].Remove(s.Id);
        if (s.Chunk >= 0 && _chunkEntities.TryGetValue(s.Chunk, out HashSet<long> ids)) ids.Remove(s.Id);
    }

    // The interest's part of a spawn, a move and a despawn (Spawn, Examine, Despawn).
    private void NoteSpawned(ref Shadow s)
    {
        if (_spaceBuilt) Place(ref s);
        if (_interest != null) _tickSpawned.Add(s.Id);
    }

    private void NoteMoved(ref Shadow s)
    {
        if (_spaceBuilt)
        {
            Unplace(ref s);
            Place(ref s);
        }
        if (_interest != null) _tickMoved.Add(s.Id);
    }

    private void NoteDespawned(ref Shadow s)
    {
        if (_spaceBuilt) Unplace(ref s);
        if (_interest != null) _tickDespawned.Add(s.Id);
    }

    // The turf of an atom: itself or the first turf up its loc chain (tg's get_turf, read without calling tg code).
    private static turf TurfOf(atom a)
    {
        for (int depth = 0; a != null && depth < 64; depth++)
        {
            if (a is turf t) return t;
            a = a.loc;
        }
        return null;
    }

    private sealed partial class SessionState
    {
        private readonly HashSet<long> _held = new();
        private readonly Dictionary<long, long> _unwanted = new(); // held, no longer wanted: id -> the tick it stopped being wanted
        private HashSet<int> _near = new(), _far = new(), _rooms = new();
        private HashSet<int> _nearWas = new(), _farWas = new(), _roomsWas = new();
        // P3.8: a room the eye stopped seeing -> the tick it stopped; for RoomHoldTicks its entities the client holds stay, but its mobs (a
        // player behind a shut door leaves after HoldTicks, as before, D40) and nothing new of it is sent (a teleport's old rooms, space
        // through the windows, reach far past what the client ever had)
        private readonly Dictionary<int, long> _roomLeft = new();
        private readonly List<int> _roomsGone = new();
        // A list, not a set: HashSet.Clear zeroes its whole bucket array, which grows to the thousands of a chunk change and then cost
        // every session milliseconds a tick (M10 slice J); an id checked twice is harmless.
        private readonly List<long> _check = new();
        private HashSet<long> _spawnedNow = new();
        private readonly List<long> _expired = new();
        // P3.7: the wanted entities the client does not have yet, spawned nearest the eye first within InterestOptions.SpawnBudget a tick
        // (a step into a new part of the map wanted 400-700 at once: one reliable Delta of 8-26 fragments, built by the client in a burst);
        // their order, consumed from _orderAt and sorted again when the queue gains an entity.
        private readonly HashSet<long> _queued = new();
        private readonly List<Queued> _order = new();
        private int _orderAt;
        private bool _orderStale;
        private readonly HashSet<int> _doorCells = new(); // the tiles of the doors spawned this tick: what lies on them comes with them
        private readonly Queue<(int Area, int Depth)> _bfs = new();
        private HashSet<int> _viewTiles = new(), _viewTilesWas = new(); // M10 slice D: the cells of the map views on the client's screen
        private readonly HashSet<int> _viewChunks = new();
        private bool _hasEye;
        private int _eyeChunk = int.MinValue, _eyeArea = int.MinValue;
        private int _eyeX, _eyeY, _eyeZ;
        private long _roomsSeen = -1;
        private float _seeWas = float.NaN, _see;
        private long _own;
        private long _eyeMob, _eyeMobWas; // DV382: the session's client.eye when it is a mob other than its own (a camera eye)

        private readonly record struct Queued(int Distance, int Cell, long Id);

        /// <summary>True when the session's client holds the entity (interest on: every entity; off: the observer layer's).</summary>
        public bool Holds(long id) => _held.Contains(id) || _layerHeld.Contains(id);

        public bool SeesArea(int area) => area >= 0 && _rooms.Contains(area);

        public int QueuedCount => _queued.Count;

        /// <summary>The entities the session's client holds (interest on; tests and diagnostics).</summary>
        public IReadOnlyCollection<long> Held => _held;

        // Fills the block's entity lists: what the session wants now against what its client has.
        private void UpdateInterest(Replicator owner, client player, long ownEntity, long tick, bool full)
        {
            OwnBlock block = Block;
            block.LayerTick = tick;
            block.LayerSpawned.Clear();
            block.LayerChanged.Clear();
            block.LayerDespawned.Clear();
            owner.EnsureSpace();
            InterestOptions options = owner._interest;
            mob viewer = player?.mob;
            _see = viewer == null || viewer.gc_destroyed != null ? -1 : viewer.see_invisible;
            _own = ownEntity;
            _check.Clear();
            _eyeMob = player?.eye is mob eyeMob && eyeMob != viewer && eyeMob.dm_id != 0 ? eyeMob.dm_id : 0;
            if (_eyeMob != _eyeMobWas)
            {
                if (_eyeMobWas != 0) _check.Add(_eyeMobWas);
                _eyeMobWas = _eyeMob;
            }
            _doorCells.Clear();
            if (_spawnedNow.Count > 256) _spawnedNow = new(); // a burst's capacity would be zeroed tick after tick
            else _spawnedNow.Clear();
            if (_see != _seeWas)
            {
                foreach (long id in owner._layerIds) _check.Add(id);
                _seeWas = _see;
            }

            // The eye: tg's client.eye, else the mob; its chunk and area decide the sets.
            turf at = TurfOf(player?.eye ?? viewer);
            bool hasEye = at != null && owner._spaceBuilt && owner.InMap((int)at.x, (int)at.y, (int)at.z);
            int chunk = hasEye ? owner.ChunkOf((int)at.x, (int)at.y, (int)at.z) : int.MinValue;
            int areaNow = hasEye ? owner._tileArea[owner.CellIndex((int)at.x, (int)at.y, (int)at.z)] : int.MinValue;
            if (full || hasEye != _hasEye || chunk != _eyeChunk || areaNow != _eyeArea || owner._roomsVersion != _roomsSeen)
            {
                (_nearWas, _near) = (_near, _nearWas);
                (_farWas, _far) = (_far, _farWas);
                (_roomsWas, _rooms) = (_rooms, _roomsWas);
                _near.Clear();
                _far.Clear();
                _rooms.Clear();
                if (hasEye)
                {
                    int eyeZ = (int)at.z, cx = ((int)at.x - 1) / ChunkTiles, cy = ((int)at.y - 1) / ChunkTiles;
                    int farChunks = (options.FarTiles + ChunkTiles - 1) / ChunkTiles;
                    // D325: every level of the eye's multi-z stack, seen through open space
                    for (int z = owner.StackLow(eyeZ); z <= owner.StackHigh(eyeZ); z++)
                    {
                        for (int dy = -farChunks; dy <= farChunks; dy++)
                            for (int dx = -farChunks; dx <= farChunks; dx++)
                            {
                                int x = cx + dx, y = cy + dy;
                                if (x < 0 || y < 0 || x >= owner._chunksX || y >= owner._chunksY) continue;
                                int key = ((z - 1) * owner._chunksY + y) * owner._chunksX + x;
                                _far.Add(key);
                                if (Math.Abs(dx) <= options.NearChunks && Math.Abs(dy) <= options.NearChunks) _near.Add(key);
                            }
                        // P3.8: a chunk is left only past a margin, so walking to and fro over a chunk's border does not send its column again
                        Keep(owner, _near, _nearWas, z, cx, cy, options.NearChunks + options.NearLeaveChunks);
                        Keep(owner, _far, _farWas, z, cx, cy, (options.FarTiles + options.FarLeaveTiles + ChunkTiles - 1) / ChunkTiles);
                    }
                    Rooms(owner, areaNow, options.RoomDepth);
                    // P3.8: a room no longer seen (its door shut) is kept for RoomHoldTicks, so the door's next opening does not send it again
                    if (_roomLeft.Count > 0)
                        foreach (int a in _rooms) _roomLeft.Remove(a);
                    if (options.RoomHoldTicks > 0)
                        foreach (int a in _roomsWas)
                            if (!_rooms.Contains(a)) _roomLeft.TryAdd(a, tick);
                }
                else
                {
                    _roomLeft.Clear();
                }
                AddChanged(owner, _near, _nearWas);
                AddChanged(owner, _far, _farWas);
                foreach (int a in _rooms)
                    if (!_roomsWas.Contains(a)) _check.AddRange(owner._areaEntities[a]);
                foreach (int a in _roomsWas)
                    if (!_rooms.Contains(a)) _check.AddRange(owner._areaEntities[a]);
                _hasEye = hasEye;
                _eyeChunk = chunk;
                _eyeArea = areaNow;
                _roomsSeen = owner._roomsVersion;
            }
            if (hasEye) (_eyeX, _eyeY, _eyeZ) = ((int)at.x, (int)at.y, (int)at.z);
            if (_roomLeft.Count > 0)
            {
                _roomsGone.Clear();
                foreach (KeyValuePair<int, long> pair in _roomLeft)
                    if (tick - pair.Value >= options.RoomHoldTicks) _roomsGone.Add(pair.Key);
                foreach (int a in _roomsGone)
                {
                    _roomLeft.Remove(a);
                    _check.AddRange(owner._areaEntities[a]);
                }
            }

            // M10 slice D (D23, D268): the second eyes, the turfs a map view on the client's screen shows (tg's camera console's
            // cam_screen.vis_contents); an entity on one of them is wanted wherever the eye is.
            (_viewTilesWas, _viewTiles) = (_viewTiles, _viewTilesWas);
            _viewTiles.Clear();
            if (player != null && owner._spaceBuilt)
            {
                DmList screen = player.screen;
                for (int i = 1; i <= screen.Count; i++)
                {
                    // silicons SI4 (DV387): an AI's multicamera window shows the turfs in its vis_contents as a map view does
                    if (screen[(float)i] is not (atom_movable_screen_map_view or atom_movable_screen_movable_pic_in_pic) || screen[(float)i] is not atom_movable_screen view) continue;
                    DmList shown = view.vis_contents;
                    for (int k = 1; k <= shown.Count; k++)
                        if (shown[(float)k] is turf t && owner.InMap((int)t.x, (int)t.y, (int)t.z))
                            _viewTiles.Add(owner.CellIndex((int)t.x, (int)t.y, (int)t.z));
                }
            }
            if (!_viewTiles.SetEquals(_viewTilesWas))
            {
                _viewChunks.Clear();
                foreach (int cell in _viewTiles) _viewChunks.Add(owner.ChunkOfCell(cell));
                foreach (int cell in _viewTilesWas) _viewChunks.Add(owner.ChunkOfCell(cell));
                foreach (int key in _viewChunks)
                    if (owner._chunkEntities.TryGetValue(key, out HashSet<long> ids)) _check.AddRange(ids);
            }

            // The tick's events, then every entity whose wanting may have changed; what the world made this tick (a shot, a spark, a
            // dropped item, an opened crate's things) comes at once, what the sets brought in waits in the queue (P3.7).
            foreach (long id in owner._tickMoved) _check.Add(id);
            if (ownEntity != 0) _check.Add(ownEntity);
            if (_eyeMob != 0) _check.Add(_eyeMob);
            foreach (long id in owner._tickSpawned) Consider(owner, id, tick, true);
            foreach (long id in _check) Consider(owner, id, tick, false);
            if (_queued.Count > 0) SpawnQueued(owner, full ? options.JoinSpawnBudget : options.SpawnBudget);

            // The held entities' changes (a queued one has none: its spawn carries its state then), the despawns, and the unwanted ones
            // whose hold ran out.
            foreach (KeyValuePair<long, EntityChange> change in owner._layerChanged)
                if (_held.Contains(change.Key) && !_spawnedNow.Contains(change.Key)) block.LayerChanged.Add(change.Value);
            foreach (long id in owner._tickDespawned)
            {
                if (!_held.Remove(id))
                {
                    _queued.Remove(id);
                    continue;
                }
                _unwanted.Remove(id);
                block.LayerDespawned.Add(id);
            }
            _expired.Clear();
            foreach (KeyValuePair<long, long> pair in _unwanted)
                if (tick - pair.Value >= options.HoldTicks) _expired.Add(pair.Key);
            int despawned = 0;
            foreach (long id in _expired)
            {
                if (despawned >= options.DespawnBudget) break; // the rest stay in _unwanted, expired, for the next ticks
                _unwanted.Remove(id);
                if (owner._index.TryGetValue(id, out int slot) && Wanted(owner, ref owner._slots[slot])) continue;
                if (_held.Remove(id))
                {
                    block.LayerDespawned.Add(id);
                    despawned++;
                }
            }

            block.LayerSpawned.Sort(static (a, b) => a.Id.CompareTo(b.Id));
            block.LayerChanged.Sort(static (a, b) => a.Id.CompareTo(b.Id));
            block.LayerDespawned.Sort();
        }

        // An entity whose wanting may have changed: held ones start or stop their hold, a new one is spawned at once or queued.
        private void Consider(Replicator owner, long id, long tick, bool made)
        {
            if (!owner._index.TryGetValue(id, out int slot)) return;
            ref Shadow s = ref owner._slots[slot];
            if (!Wanted(owner, ref s))
            {
                if (_held.Contains(id)) _unwanted.TryAdd(id, tick);
                else _queued.Remove(id); // its client never had it: it just leaves the queue
                return;
            }
            _unwanted.Remove(id);
            if (_held.Contains(id)) return;
            // These skip the queue: the own mob, the mobs (they move and act), the doors in the near chunks (the map's shape round the eye,
            // and the client's guess of them, DV179; a far one is queued, nearest first), and a visual, which the client draws on its host,
            // with that host.
            if (made || s.Id == _own || s.IsMob || s.CellPollIndex >= 0 && _near.Contains(s.Chunk) || s.IsVisual)
            {
                if (s.IsVisual && s.VisHost != 0 && !_held.Contains(s.VisHost) && owner._index.TryGetValue(s.VisHost, out int host))
                    Spawn(owner, s.VisHost, host);
                if (s.CellPollIndex >= 0 && owner.InMap(s.X, s.Y, s.Z)) _doorCells.Add(owner.CellIndex(s.X, s.Y, s.Z));
                Spawn(owner, id, slot);
            }
            else if (_queued.Add(id))
            {
                _orderStale = true;
            }
        }

        private void Spawn(Replicator owner, long id, int slot)
        {
            _held.Add(id);
            _queued.Remove(id);
            Block.LayerSpawned.Add(StateOf(in owner._slots[slot]));
            _spawnedNow.Add(id);
        }

        // P3.7: the queue, nearest the eye first, at most budget a tick; what lies on the tile of a door spawned this tick comes with it
        // whatever the budget (the client draws a door across a window block, or shutters over a window, from what its tile holds when
        // the door arrives: MapRenderer.AcrossGlass). A tile sorts as one run (distance, then cell), so a queued door's tile follows it
        // at once; a door spawned at once may have its tile anywhere in the order, which is then read to its end. Any other tile cut by
        // the budget is harmless: the client builds its arrivals over frames anyway (EntityLayer.AddPending) and redraws a grille or a
        // neighbour when a window or a wall piece joins later (MapRenderer.Rejoin).
        private void SpawnQueued(Replicator owner, int budget)
        {
            if (_orderStale)
            {
                _order.Clear();
                _orderAt = 0;
                foreach (long id in _queued)
                {
                    if (!owner._index.TryGetValue(id, out int slot)) continue;
                    ref Shadow s = ref owner._slots[slot];
                    int dx = s.X - _eyeX, dy = s.Y - _eyeY;
                    _order.Add(new Queued(s.Z == _eyeZ ? dx * dx + dy * dy : int.MaxValue, owner.InMap(s.X, s.Y, s.Z) ? owner.CellIndex(s.X, s.Y, s.Z) : -1, id));
                }
                _order.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Cell != b.Cell ? a.Cell.CompareTo(b.Cell) : a.Id.CompareTo(b.Id));
                _orderStale = false;
            }
            int spawned = 0;
            bool doorAtOnce = _doorCells.Count > 0;
            for (int i = _orderAt; i < _order.Count; i++)
            {
                Queued q = _order[i];
                if (spawned >= budget && !_doorCells.Contains(q.Cell))
                {
                    if (!doorAtOnce) break;
                    continue;
                }
                if (!_queued.Remove(q.Id)) continue; // spawned at once since, or no longer wanted
                if (!owner._index.TryGetValue(q.Id, out int slot) || !Wanted(owner, ref owner._slots[slot])) continue;
                Spawn(owner, q.Id, slot);
                spawned++;
                if (owner._slots[slot].CellPollIndex >= 0 && q.Cell >= 0) _doorCells.Add(q.Cell);
            }
            while (_orderAt < _order.Count && !_queued.Contains(_order[_orderAt].Id)) _orderAt++;
            if (_orderAt == _order.Count)
            {
                _order.Clear();
                _orderAt = 0;
            }
        }

        // The chunks of was within leave chunks of the eye's chunk (cx, cy) on its level stay in now.
        private static void Keep(Replicator owner, HashSet<int> now, HashSet<int> was, int z, int cx, int cy, int leave)
        {
            int level = owner._chunksX * owner._chunksY;
            foreach (int key in was)
                if (key / level == z - 1 && Math.Abs(key % owner._chunksX - cx) <= leave && Math.Abs(key / owner._chunksX % owner._chunksY - cy) <= leave)
                    now.Add(key);
        }

        private void AddChanged(Replicator owner, HashSet<int> now, HashSet<int> was)
        {
            foreach (int key in now)
                if (!was.Contains(key) && owner._chunkEntities.TryGetValue(key, out HashSet<long> ids)) _check.AddRange(ids);
            foreach (int key in was)
                if (!now.Contains(key) && owner._chunkEntities.TryGetValue(key, out HashSet<long> ids)) _check.AddRange(ids);
        }

        // The rooms the eye sees: its area and the areas linked to it, up to depth links away.
        private void Rooms(Replicator owner, int start, int depth)
        {
            if (start < 0) return;
            _rooms.Add(start);
            _bfs.Clear();
            _bfs.Enqueue((start, 0));
            while (_bfs.Count > 0)
            {
                (int area, int d) = _bfs.Dequeue();
                if (d >= depth) continue;
                foreach (int next in owner._portals[area].Keys)
                    if (_rooms.Add(next)) _bfs.Enqueue((next, d + 1));
            }
        }

        private bool Wanted(Replicator owner, ref Shadow s)
        {
            if (s.Id == _eyeMob && s.Chunk >= 0) return true; // DV382: the camera eye the client looks from
            if (s.Id == _own) return true; // even above its own see_invisible (an invisimin mob): the client plays from it
            if (s.Layer != 0 && s.Invisibility > _see) return false;
            if (s.IsVisual && s.VisHost != 0 && owner._index.TryGetValue(s.VisHost, out int host))
                return Wanted(owner, ref owner._slots[host]); // a flick visual goes with its host
            if (s.Chunk < 0) return false;
            if (_viewTiles.Count > 0 && owner.InMap(s.X, s.Y, s.Z) && _viewTiles.Contains(owner.CellIndex(s.X, s.Y, s.Z))) return true; // M10 slice D
            if (!_hasEye) return false;
            if (_near.Contains(s.Chunk)) return true;
            if (!_far.Contains(s.Chunk)) return false;
            if (!s.IsMob && !s.Light.IsNone) return true;
            if (s.CellPollIndex >= 0) return true; // doors are part of the map's shape: seen down a long corridor, they must not pop in
            return s.Area >= 0 && (_rooms.Contains(s.Area) || !s.IsMob && _roomLeft.ContainsKey(s.Area) && _held.Contains(s.Id));
        }
    }
}

/// <summary>M10 slice C: the interest settings (docs/ai/specs/multiplayer.md section 6).</summary>
public sealed class InterestOptions
{
    /// <summary>The chunks around the eye's chunk a session always holds, whatever the walls (1: the 3 by 3 chunks, at least 8 tiles each way).</summary>
    public int NearChunks { get; init; } = 1;

    /// <summary>How far, in tiles, a session holds the rooms it sees and the light sources (64 tiles, 128 m: past the client's medium view distance).</summary>
    public int FarTiles { get; init; } = 64;

    /// <summary>How many transparent links away from the eye's area a room is seen (2: through a window into a room and its window beyond).</summary>
    public int RoomDepth { get; init; } = 2;

    /// <summary>How long an entity a session no longer wants stays with its client before it is despawned (40 ticks, 2 s).</summary>
    public int HoldTicks { get; init; } = 40;

    /// <summary>P3.8: a near chunk is left only this many chunks beyond <see cref="NearChunks"/> (1: entered in the 3 by 3, left outside the 5 by 5).</summary>
    public int NearLeaveChunks { get; init; } = 1;

    /// <summary>P3.8: the far radius is left only this many tiles beyond <see cref="FarTiles"/> (8: one chunk).</summary>
    public int FarLeaveTiles { get; init; } = 8;

    /// <summary>P3.8: a room the eye no longer sees (its door shut) keeps what the client holds of it this long (100 ticks, 5 s; not its mobs), so a door's cycle does not send the room again.</summary>
    public int RoomHoldTicks { get; init; } = 100;

    /// <summary>P3.7: the queued entities a session's block spawns in a tick, nearest the eye first (150: 4-8 KB a Delta on MetaStation, within 8 fragments).</summary>
    public int SpawnBudget { get; init; } = 150;

    /// <summary>P3.7: the queued entities the join's Snapshot spawns; the rest follow at <see cref="SpawnBudget"/> a tick.</summary>
    public int JoinSpawnBudget { get; init; } = 1000;

    /// <summary>
    /// P3.8: the held entities whose hold ran out a session's block despawns in a tick; the rest stay held for the next ticks (a room set
    /// left whole is thousands at once, 119 ms of the client's frame). What the world deleted is despawned at once whatever the budget.
    /// </summary>
    public int DespawnBudget { get; init; } = 300;

    internal void Validate()
    {
        if (NearChunks < 0) throw new ArgumentOutOfRangeException(nameof(NearChunks), NearChunks, "not negative");
        if (FarTiles < 0) throw new ArgumentOutOfRangeException(nameof(FarTiles), FarTiles, "not negative");
        if (RoomDepth < 0) throw new ArgumentOutOfRangeException(nameof(RoomDepth), RoomDepth, "not negative");
        if (HoldTicks < 0) throw new ArgumentOutOfRangeException(nameof(HoldTicks), HoldTicks, "not negative");
        if (NearLeaveChunks < 0) throw new ArgumentOutOfRangeException(nameof(NearLeaveChunks), NearLeaveChunks, "not negative");
        if (FarLeaveTiles < 0) throw new ArgumentOutOfRangeException(nameof(FarLeaveTiles), FarLeaveTiles, "not negative");
        if (RoomHoldTicks < 0) throw new ArgumentOutOfRangeException(nameof(RoomHoldTicks), RoomHoldTicks, "not negative");
        if (SpawnBudget < 1) throw new ArgumentOutOfRangeException(nameof(SpawnBudget), SpawnBudget, "at least 1");
        if (JoinSpawnBudget < 1) throw new ArgumentOutOfRangeException(nameof(JoinSpawnBudget), JoinSpawnBudget, "at least 1");
        if (DespawnBudget < 1) throw new ArgumentOutOfRangeException(nameof(DespawnBudget), DespawnBudget, "at least 1");
    }
}
