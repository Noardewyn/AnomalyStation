namespace Anomaly.Core.Tg;

/// <summary>A full-cell structural support using the existing wall construction, damage and dismantling rules.</summary>
// No [DmPath]: tg has no such type, and the type registry lists only tg's generated TypeOrder (DV183).
public sealed class turf_closed_wall_ceiling_column : turf_closed_wall
{
    public turf_closed_wall_ceiling_column()
    {
        name = "ceiling support column";
        desc = "A full-height structural column anchoring connected ceiling frames.";
    }
}
