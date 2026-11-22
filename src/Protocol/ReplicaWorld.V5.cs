namespace Anomaly.Protocol;

// M4b slice 3 (MessagesV5.cs): the gas each cell shows, as the client's gas layer reads it. GasChanged is raised after an
// applied Delta with the cells whose gas changed (not CellsChanged: a gas change does not touch the map's meshes).

public sealed partial class ReplicaWorld
{
    private readonly List<string> _gasKinds = new();
    private readonly Dictionary<int, GasLevel[]> _gasCells = new();
    private readonly List<(int X, int Y, int Z)> _gasScratch = new();
    private Action<IReadOnlyList<(int X, int Y, int Z)>> _gasChangedHandlers;

    /// <summary>The icon states of tg's gas overlays (<see cref="MapGrid.GasKinds"/>; <see cref="GasLevel.Kind"/> 1 is element 0).</summary>
    public IReadOnlyList<string> GasKinds => _gasKinds;

    /// <summary>The gas the cell shows (empty when none or outside the map).</summary>
    public GasLevel[] GasAt(int x, int y, int z) =>
        InMap(x, y, z) && _gasCells.TryGetValue(CellIndex(x, y, z), out GasLevel[] gases) ? gases : Array.Empty<GasLevel>();

    /// <summary>Every cell that shows gas now.</summary>
    public IEnumerable<(int X, int Y, int Z, GasLevel[] Gases)> GasCells
    {
        get
        {
            if (_map == null) yield break;
            int w = _map.Width, h = _map.Height;
            foreach (KeyValuePair<int, GasLevel[]> cell in _gasCells)
                yield return (cell.Key % w + 1, cell.Key / w % h + 1, cell.Key / (w * h) + 1, cell.Value);
        }
    }

    /// <summary>Raised after an applied Delta with the cells whose shown gas changed (<see cref="Delta.CellGasChanged"/>), in the Delta's order.</summary>
    public event Action<IReadOnlyList<(int X, int Y, int Z)>> GasChanged
    {
        add => _gasChangedHandlers += value;
        remove => _gasChangedHandlers -= value;
    }

    private void TakeMapCellsV5(MapGrid grid)
    {
        if (grid.GasKinds.Count > Limits.MaxGasKinds)
            throw Bad("a map gas kinds table over " + Limits.MaxGasKinds + " entries");
        _gasKinds.Clear();
        _gasKinds.AddRange(grid.GasKinds);
        _gasCells.Clear();
        foreach (CellGasChange c in grid.GasCells)
            SetGas(c);
    }

    private void ApplyCellChangesV5(Delta delta)
    {
        foreach (CellGasChange c in delta.CellGasChanged)
        {
            SetGas(c);
            _gasScratch.Add((c.X, c.Y, c.Z));
        }
    }

    private void SetGas(CellGasChange c)
    {
        CheckCell(c.X, c.Y, c.Z);
        GasLevel[] gases = c.Gases ?? Array.Empty<GasLevel>();
        if (gases.Length > Limits.MaxGasesPerCell)
            throw Bad("a cell shows more than " + Limits.MaxGasesPerCell + " gases");
        foreach (GasLevel g in gases)
        {
            if (g.Kind < 1 || g.Kind > _gasKinds.Count)
                throw Bad("a gas level names a kind the table does not have");
            if (g.Level < 1 || g.Level > Limits.MaxGasLevel)
                throw Bad("a gas level outside 1.." + Limits.MaxGasLevel);
        }

        int i = CellIndex(c.X, c.Y, c.Z);
        if (gases.Length == 0) _gasCells.Remove(i);
        else _gasCells[i] = gases;
    }

    private void RaiseV5()
    {
        if (_gasScratch.Count == 0) return;
        (int X, int Y, int Z)[] changed = _gasScratch.ToArray();
        _gasScratch.Clear();
        _gasChangedHandlers?.Invoke(changed);
    }
}
