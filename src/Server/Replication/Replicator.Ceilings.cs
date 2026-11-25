using System.Linq;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

public sealed partial class Replicator
{
    private readonly Dictionary<(int X, int Y, int Z), CeilingChange> _ceilingShadow = new();
    private readonly HashSet<(int X, int Y, int Z)> _ceilingDirty = new();
    private readonly Dictionary<(int X, int Y, int Z), LightState> _ceilingLight = new(); // version 96: the lit ceilings' blocks

    private void InitializeCeilings()
    {
        var ceilings = CeilingSystem.Of(_world);
        // (what is shown: a cell's ceiling, or under a level above the tile its map laid under that level's plating, DV507)
        foreach (var p in ceilings.ShownCells)
        {
            CeilingChange cell = CeilingCell(ceilings, p);
            if (cell.State.Exists) _ceilingShadow[p] = cell;
            LightState light = TurfLightOf(ceilings.LitBlockAt(p.X, p.Y, p.Z));
            if (!light.IsNone) _ceilingLight[p] = light;
        }
        // D295: a block's picture smooths with its neighbours, so a change redraws the eight around it too
        ceilings.Changed += (x, y, z) =>
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                _ceilingDirty.Add((x + dx, y + dy, z));
        };
    }

    private CeilingChange CeilingCell(CeilingSystem ceilings, (int X, int Y, int Z) p)
    {
        var look = ceilings.Appearance(p.X, p.Y, p.Z);
        return new CeilingChange(p.X, p.Y, p.Z, ceilings.ShownAt(p.X, p.Y, p.Z), look == null ? TableRef.None : TableRef.Public(_public.InternTop(look, false)));
    }

    private void ReadCeilingChanges()
    {
        var ceilings = CeilingSystem.Of(_world);
        _delta.Ceilings.Clear();
        int room = Math.Max(0, Limits.MaxCellChangesPerDelta - _delta.SolidChanged.Count - _delta.TurfAppearanceChanged.Count - _delta.CellFlagsChanged.Count
            - _delta.BaseLightChanged.Count - _delta.TurfTypeChanged.Count - _delta.CellGasChanged.Count - _delta.TurfLightChanged.Count);
        foreach (var p in _ceilingDirty.OrderBy(p => p.Z).ThenBy(p => p.Y).ThenBy(p => p.X).ToArray())
        {
            _ceilingDirty.Remove(p);
            var cell = CeilingCell(ceilings, p);
            if (_ceilingShadow.TryGetValue(p, out var drawn) ? drawn == cell : !cell.State.Exists) continue;
            if (_delta.Ceilings.Count >= room) { _ceilingDirty.Add(p); break; }
            if (cell.State.Exists) _ceilingShadow[p] = cell;
            else _ceilingShadow.Remove(p);
            _delta.Ceilings.Add(cell);
            _mapChanged = true;
            LightState light = TurfLightOf(ceilings.LitBlockAt(p.X, p.Y, p.Z)); // version 96
            if (light != _ceilingLight.GetValueOrDefault(p))
            {
                if (light.IsNone) _ceilingLight.Remove(p);
                else _ceilingLight[p] = light;
                _delta.TurfLightChanged.Add(new TurfLightChange(p.X, p.Y, p.Z, true, light));
            }
        }
    }
}
