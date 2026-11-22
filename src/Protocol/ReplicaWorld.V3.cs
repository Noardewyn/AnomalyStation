using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: the API is the M3 contracts step's (frozen, docs/ai/specs/m3-ownership.md); the bodies are M3 P5's. The map state of
// version 3 (MessagesV3.cs) as the client's renderers (M3 P6), light grid (M3 P7), predictor and bots read it. Every member
// reads after Apply on the caller's thread, like the version-2 members. CellsChanged (ReplicaWorld.V2.cs) is raised for the
// version-3 cell changes too (flags, base light, turf type), in the Delta's order.

public sealed partial class ReplicaWorld
{
    // ---- the cells (MapGrid version 3 and the Delta's cell changes) ----

    private byte[] _flags;
    private ushort[] _baseLightCells;
    private int[] _turfTypeCells;
    private readonly List<BaseLight> _baseLights = new();
    private readonly List<string> _turfTypes = new();
    private LightState _starlight;
    private readonly List<ReplicaEntity> _lightScratch = new();

    private bool InMap(int x, int y, int z) => _map != null && x >= 1 && y >= 1 && z >= 1 && x <= _map.Width && y <= _map.Height && z <= _map.Levels;

    /// <summary>The cell's flags byte (<see cref="CellFlags"/>: MapGrid's runs, then <see cref="Delta.CellFlagsChanged"/>); outside the map: 0.</summary>
    public byte CellFlagsAt(int x, int y, int z) => InMap(x, y, z) && _flags != null ? _flags[CellIndex(x, y, z)] : (byte)0;

    /// <summary>The cell's blocked faces (<see cref="CellFlags.FacesOf"/> of <see cref="CellFlagsAt"/>): what <see cref="CollisionView"/>'s <c>EdgesOf</c> returns from version 3 on.</summary>
    public TileEdges FacesOf(int x, int y, int z) => CellFlags.FacesOf(CellFlagsAt(x, y, z));

    /// <summary>Version 37 (D239): whether the cell is solid only through <c>climb_walkable</c> structures (<see cref="CellFlags.ClimbWalkable"/>); outside the map: false.</summary>
    public bool IsClimbWalkable(int x, int y, int z) => (CellFlagsAt(x, y, z) & CellFlags.ClimbWalkable) != 0;

    /// <summary>Version 73 (DV360): whether tg's plastic flaps that refuse a standing mob stand on the cell (<see cref="CellFlags.Flaps"/>); outside the map: false.</summary>
    public bool IsFlaps(int x, int y, int z) => (CellFlagsAt(x, y, z) & CellFlags.Flaps) != 0;

    /// <summary>Whether the cell blocks light (<see cref="CellFlags.Opaque"/>); outside the map: true.</summary>
    public bool IsOpaque(int x, int y, int z) => !InMap(x, y, z) || (CellFlagsAt(x, y, z) & CellFlags.Opaque) != 0;

    /// <summary>Whether the cell is a starlit space cell (<see cref="CellFlags.Starlight"/>), a light source with <see cref="Starlight"/>'s values.</summary>
    public bool IsStarlit(int x, int y, int z) => (CellFlagsAt(x, y, z) & CellFlags.Starlight) != 0;

    /// <summary>The base-light table so far (index 1 is element 0).</summary>
    public IReadOnlyList<BaseLight> BaseLights => _baseLights;

    /// <summary>The cell's base-light index (0: none; MapGrid's runs, then <see cref="Delta.BaseLightChanged"/>).</summary>
    public int BaseLightIndexAt(int x, int y, int z) => InMap(x, y, z) && _baseLightCells != null ? _baseLightCells[CellIndex(x, y, z)] : 0;

    /// <summary>The starlight values (<see cref="MapGrid.Starlight"/>, then <see cref="Delta.StarlightChanged"/>).</summary>
    public LightState Starlight => _starlight;

    /// <summary>The map's turf type table so far (the MapGrid's <see cref="MapGrid.TurfTypes"/> plus every Delta's <see cref="Delta.NewTurfTypes"/>).</summary>
    public IReadOnlyList<string> TurfTypes => _turfTypes;

    /// <summary>The cell's turf type path (MapGrid's runs, then <see cref="Delta.TurfTypeChanged"/>); outside the map: null.</summary>
    public string TurfTypeAt(int x, int y, int z) => InMap(x, y, z) && _turfTypeCells != null ? _turfTypes[_turfTypeCells[CellIndex(x, y, z)]] : null;

    /// <summary>The cell's index in <see cref="TurfTypes"/>; outside the map: -1.</summary>
    public int TurfTypeIndexAt(int x, int y, int z) => InMap(x, y, z) && _turfTypeCells != null ? _turfTypeCells[CellIndex(x, y, z)] : -1;

    // The replica keeps its own copy of the version-3 cells (a Delta changes it; the MapGrid object stays as received).
    private void TakeMapCellsV3(MapGrid grid)
    {
        int cells = grid.CellCount;
        try
        {
            _flags = grid.FlagRuns.Count == 0 ? new byte[cells] : grid.ExpandFlags();
            _baseLightCells = grid.BaseLightRuns.Count == 0 ? new ushort[cells] : grid.ExpandBaseLights();
        }
        catch (InvalidOperationException e)
        {
            throw Bad(e.Message);
        }

        foreach (byte f in _flags)
            if ((f & ~CellFlags.All) != 0)
                throw Bad("a map cell flags byte sets bits that are not defined");
        if (grid.BaseLights.Count > Limits.MaxBaseLightEntries)
            throw Bad("a map base-light table over " + Limits.MaxBaseLightEntries + " entries");
        foreach (ushort index in _baseLightCells)
            if (index > grid.BaseLights.Count)
                throw Bad("a map base-light index outside the table");
        _baseLights.Clear();
        _baseLights.AddRange(grid.BaseLights);
        _turfTypes.Clear();
        _turfTypes.AddRange(grid.TurfTypes);
        _turfTypeCells = new int[cells];
        long next = 0;
        foreach (TurfRun run in grid.Runs)
        {
            Array.Fill(_turfTypeCells, run.TypeIndex, (int)next, run.Count);
            next += run.Count;
        }

        _starlight = grid.Starlight;
    }

    private void ApplyCellChangesV3(Delta delta)
    {
        foreach (CellFlagsChange c in delta.CellFlagsChanged)
        {
            CheckCell(c.X, c.Y, c.Z);
            if ((c.Flags & ~CellFlags.All) != 0)
                throw Bad("a cell flags change sets bits that are not defined");
            _flags[CellIndex(c.X, c.Y, c.Z)] = c.Flags;
            _cellsChanged.Add((c.X, c.Y, c.Z));
        }

        if (delta.NewBaseLights.Count > 0)
        {
            if (delta.FirstNewBaseLight != _baseLights.Count + 1)
                throw Bad("a Delta adds base lights from index " + delta.FirstNewBaseLight + " to a table of " + _baseLights.Count);
            if (_baseLights.Count + delta.NewBaseLights.Count > Limits.MaxBaseLightEntries)
                throw Bad("a Delta grows the base-light table past " + Limits.MaxBaseLightEntries);
            _baseLights.AddRange(delta.NewBaseLights);
        }

        foreach (CellBaseLightChange c in delta.BaseLightChanged)
        {
            CheckCell(c.X, c.Y, c.Z);
            if (c.BaseLight > _baseLights.Count)
                throw Bad("a base-light change names an entry the table does not have");
            _baseLightCells[CellIndex(c.X, c.Y, c.Z)] = c.BaseLight;
            _cellsChanged.Add((c.X, c.Y, c.Z));
        }

        if (delta.NewTurfTypes.Count > 0)
        {
            if (delta.FirstNewTurfType != _turfTypes.Count)
                throw Bad("a Delta adds turf types from index " + delta.FirstNewTurfType + " to a table of " + _turfTypes.Count);
            if (_turfTypes.Count + delta.NewTurfTypes.Count > Limits.MaxTurfTypeEntries)
                throw Bad("a Delta grows the turf type table past " + Limits.MaxTurfTypeEntries);
            foreach (string type in delta.NewTurfTypes)
                _turfTypes.Add(type ?? throw Bad("a null turf type path"));
        }

        foreach (CellTurfTypeChange c in delta.TurfTypeChanged)
        {
            CheckCell(c.X, c.Y, c.Z);
            if (c.TypeIndex < 0 || c.TypeIndex >= _turfTypes.Count)
                throw Bad("a turf type change names an entry the table does not have");
            _turfTypeCells[CellIndex(c.X, c.Y, c.Z)] = c.TypeIndex;
            _cellsChanged.Add((c.X, c.Y, c.Z));
        }

        if (delta.StarlightChanged.HasValue)
        {
            _starlight = delta.StarlightChanged.Value;
            _starlightPending = true;
        }
    }

    private bool _starlightPending;

    private void NoteLight(ReplicaEntity entity)
    {
        if (!_lightScratch.Contains(entity))
            _lightScratch.Add(entity);
    }

    // Raised once the whole message is applied (called by Raise): the lights in id order, then the starlight.
    private void RaiseV3()
    {
        if (_lightScratch.Count > 0)
        {
            ReplicaEntity[] touched = _lightScratch.ToArray();
            _lightScratch.Clear();
            Array.Sort(touched, (a, b) => a.Id.CompareTo(b.Id));
            foreach (ReplicaEntity entity in touched)
                _lightChangedHandlers?.Invoke(entity);
        }

        if (_starlightPending)
        {
            _starlightPending = false;
            _starlightChangedHandlers?.Invoke(_starlight);
        }
    }

    // ---- the lights ----

    private Action<ReplicaEntity> _lightChangedHandlers;
    private Action<LightState> _starlightChangedHandlers;

    /// <summary>Raised after an applied Snapshot or Delta, once per entity whose <see cref="NetField.Light"/> changed (spawned with a light, changed, or despawned with one: then <see cref="ReplicaEntity.Light"/> is none), in id order. P5's Apply raises it (<c>_lightChangedHandlers</c>).</summary>
    public event Action<ReplicaEntity> LightChanged
    {
        add => _lightChangedHandlers += value;
        remove => _lightChangedHandlers -= value;
    }

    /// <summary>Raised after an applied Delta whose <see cref="Delta.StarlightChanged"/> was set. P5's Apply raises it (<c>_starlightChangedHandlers</c>).</summary>
    public event Action<LightState> StarlightChanged
    {
        add => _starlightChangedHandlers += value;
        remove => _starlightChangedHandlers -= value;
    }
}

public sealed partial class ReplicaEntity
{
    /// <summary>The entity's light source (<see cref="NetField.Light"/>), <see cref="LightState.None"/> when it has none.</summary>
    public LightState Light => _state.Light;

    /// <summary>True when the entity has a light (<see cref="LightState.Range"/> above 0; whether it is lit is <see cref="LightState.On"/>).</summary>
    public bool HasLight => !_state.Light.IsNone;

    internal void ClearLight() => _state = _state with { Light = LightState.None };
}
