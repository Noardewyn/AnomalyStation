using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

public sealed partial class CeilingSystem
{
    private readonly HashSet<(int X, int Y, int Z)> _exposed = new();
    private datum_gas_mixture_immutable_space _vacuum;
    public bool IsExteriorOutlet(turf_open t) => t.pressure_direction == UP && _exposed.Contains(((int)t.x, (int)t.y, (int)t.z));

    private void UpdateExposure(int x, int y, int z)
    {
        // D304: a planet has its own air above an open roof (tg's planetary_atmos turfs share with it), not space: neither a
        // planetary turf nor anything on a planet's level (tg's mining trait, Lavaland) vents through a missing ceiling
        // D325: a turf with a level above vents into that level through open space (LINDA), not into space
        if (MapView.TurfAt(_world, x, y, z) is turf_open t and not turf_open_space && !At(x, y, z).Sealed && !HasLevelAbove(t)
            && !Dm.truthy(t.planetary_atmos) && !Dm.truthy(SSmapping.level_trait(z, ZTRAIT_MINING)))
            _exposed.Add((x, y, z));
        else _exposed.Remove((x, y, z));
    }

    /// <summary>One full-cell outlet per atmospheric cycle, using LINDA's existing space sharing rule.
    /// The exterior has no turf identity and cannot join an excited group or receive an actor.</summary>
    public void ExchangeExterior()
    {
        if (!Initialized || _exposed.Count == 0) return;
        _vacuum ??= Dm.@new<datum_gas_mixture_immutable_space>();
        foreach (var p in _exposed.OrderBy(p => p.Z).ThenBy(p => p.Y).ThenBy(p => p.X))
        {
            if (MapView.TurfAt(_world, p.X, p.Y, p.Z) is not turf_open t || t.air == null || At(p.X, p.Y, p.Z).Sealed) continue;
            float before = Dm.num(t.air.return_pressure());
            if (before <= 0) continue;
            t.air.archive();
            _vacuum.garbage_collect();
            _vacuum.archive();
            t.air.share(_vacuum, 1, 1);
            _vacuum.garbage_collect();
            float difference = Math.Max(0, before - Dm.num(t.air.return_pressure()));
            if (difference <= 0) continue;
            SSair.add_to_active(t);
            t.excited_group?.reset_cooldowns();
            foreach (turf neighbor in Dm.each<turf>(t.atmos_adjacent_turfs)) SSair.add_to_active(neighbor);
            // Only the outlet's visual has an UP direction. Actors remain on their existing plane.
            Dm.combine(ref SSair.high_pressure_delta, t);
            if (difference >= t.pressure_difference)
            {
                t.pressure_difference = difference;
                t.pressure_direction = UP;
            }
            t.update_visuals();
        }
    }
}
