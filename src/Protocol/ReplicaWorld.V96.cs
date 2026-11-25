using System.Linq;

namespace Anomaly.Protocol;

// Version 96 (MessagesV96.cs): the lights of the turfs and of the ceilings' floor blocks, as the client's light layer reads them.
// TurfLightChanged is raised after an applied Delta with the cells whose turf or ceiling light changed.

public sealed partial class ReplicaWorld
{
    private readonly Dictionary<(int X, int Y, int Z, bool Ceiling), LightState> _turfLights = new();
    private readonly List<TurfLightChange> _turfLightScratch = new();

    /// <summary>The light the cell's turf (or its ceiling's floor block) gives now; <see cref="LightState.None"/> for none.</summary>
    public LightState TurfLightAt(int x, int y, int z, bool ceiling) => _turfLights.GetValueOrDefault((x, y, z, ceiling));

    /// <summary>Every lit turf and ceiling now.</summary>
    public IEnumerable<TurfLightChange> TurfLights => _turfLights.Select(l => new TurfLightChange(l.Key.X, l.Key.Y, l.Key.Z, l.Key.Ceiling, l.Value));

    /// <summary>Raised after an applied Delta with the turfs and ceilings whose light changed (<see cref="Delta.TurfLightChanged"/>), in its order.</summary>
    public event Action<IReadOnlyList<TurfLightChange>> TurfLightChanged;

    private void TakeTurfLights(MapGrid grid)
    {
        _turfLights.Clear();
        foreach (TurfLightChange c in grid.TurfLights)
            SetTurfLight(c);
    }

    private void ApplyTurfLights(Delta delta)
    {
        foreach (TurfLightChange c in delta.TurfLightChanged)
        {
            SetTurfLight(c);
            _turfLightScratch.Add(c);
            _cellsChanged.Add((c.X, c.Y, c.Z)); // a glowing tile is drawn self-lit (the map's meshes)
        }
    }

    private void SetTurfLight(TurfLightChange c)
    {
        CheckCell(c.X, c.Y, c.Z);
        if (c.Light.IsNone) _turfLights.Remove((c.X, c.Y, c.Z, c.Ceiling));
        else _turfLights[(c.X, c.Y, c.Z, c.Ceiling)] = c.Light;
    }

    private void RaiseV96()
    {
        if (_turfLightScratch.Count == 0) return;
        TurfLightChange[] changed = _turfLightScratch.ToArray();
        _turfLightScratch.Clear();
        TurfLightChanged?.Invoke(changed);
    }
}
