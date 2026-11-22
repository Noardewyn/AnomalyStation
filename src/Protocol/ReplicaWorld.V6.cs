namespace Anomaly.Protocol;

// Version 6 (MessagesV6.cs, D141): the air flow of the cells around the player, as the client's airflow streams read it.
// An Airflow message changes it (Apply); a MapGrid drops it all (the server's interest starts empty for a joining session).
// AirflowChanged is raised after each applied update with the cells it changed.

public sealed partial class ReplicaWorld
{
    private readonly Dictionary<int, byte> _airflow = new();
    private readonly Dictionary<int, byte> _airflowVents = new();
    private readonly List<(int X, int Y, int Z)> _airflowScratch = new();
    private Action<IReadOnlyList<(int X, int Y, int Z)>> _airflowChangedHandlers;

    /// <summary>The cell's air flow byte (<see cref="Airflow.Clear"/> when none or outside the map).</summary>
    public byte AirflowAt(int x, int y, int z) =>
        InMap(x, y, z) && _airflow.TryGetValue(CellIndex(x, y, z), out byte cell) ? cell : Airflow.Clear;

    /// <summary>Cells with a flow now.</summary>
    public int AirflowCount => _airflow.Count;

    /// <summary>Version 7: the air movement byte of the floor devices on a cell (<see cref="AirflowVents"/>; <see cref="Airflow.Clear"/> when none).</summary>
    public byte AirflowVentAt(int x, int y, int z) =>
        InMap(x, y, z) && _airflowVents.TryGetValue(CellIndex(x, y, z), out byte cell) ? cell : Airflow.Clear;

    /// <summary>Version 7: every cell whose floor devices move air now.</summary>
    public IEnumerable<(int X, int Y, int Z, byte Cell)> AirflowVentCells
    {
        get
        {
            if (_map == null) yield break;
            int w = _map.Width, h = _map.Height;
            foreach (KeyValuePair<int, byte> cell in _airflowVents)
                yield return (cell.Key % w + 1, cell.Key / w % h + 1, cell.Key / (w * h) + 1, cell.Value);
        }
    }

    /// <summary>Every cell with a flow now.</summary>
    public IEnumerable<(int X, int Y, int Z, byte Cell)> AirflowCells
    {
        get
        {
            if (_map == null) yield break;
            int w = _map.Width, h = _map.Height;
            foreach (KeyValuePair<int, byte> cell in _airflow)
                yield return (cell.Key % w + 1, cell.Key / w % h + 1, cell.Key / (w * h) + 1, cell.Value);
        }
    }

    /// <summary>Raised after an applied <see cref="AirflowUpdate"/> with the cells whose flow or floor devices' air movement it changed.</summary>
    public event Action<IReadOnlyList<(int X, int Y, int Z)>> AirflowChanged
    {
        add => _airflowChangedHandlers += value;
        remove => _airflowChangedHandlers -= value;
    }

    /// <summary>Applies an <see cref="AirflowUpdate"/>; throws <see cref="ProtocolException"/> (and changes nothing) for a cell outside the map.</summary>
    public void Apply(AirflowUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        foreach (AirflowRun run in update.Runs)
        {
            CheckCell(run.X0, run.Y, run.Z);
            CheckCell(run.X0 + run.Cells.Length - 1, run.Y, run.Z);
        }
        foreach (AirflowVent vent in update.Vents)
            CheckCell(vent.X, vent.Y, vent.Z);

        foreach (AirflowRun run in update.Runs)
        {
            for (int k = 0; k < run.Cells.Length; k++)
            {
                byte cell = run.Cells[k];
                if (cell == Airflow.Unchanged) continue;
                int x = run.X0 + k, i = CellIndex(x, run.Y, run.Z);
                if (cell == Airflow.Clear) _airflow.Remove(i);
                else _airflow[i] = cell;
                _airflowScratch.Add((x, run.Y, run.Z));
            }
        }

        foreach (AirflowVent vent in update.Vents)
        {
            int i = CellIndex(vent.X, vent.Y, vent.Z);
            if (vent.Cell == Airflow.Clear) _airflowVents.Remove(i);
            else _airflowVents[i] = vent.Cell;
            _airflowScratch.Add((vent.X, vent.Y, vent.Z));
        }

        if (_airflowScratch.Count == 0) return;
        (int X, int Y, int Z)[] changed = _airflowScratch.ToArray();
        _airflowScratch.Clear();
        _airflowChangedHandlers?.Invoke(changed);
    }

    private void TakeMapCellsV6()
    {
        _airflow.Clear();
        _airflowVents.Clear();
    }
}
