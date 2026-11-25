namespace Anomaly.Protocol;

// Protocol version 96 (docs/ai/specs/protocol.md section 91): the lights of the turfs themselves. tg lights a turf with its own
// light vars (a light floor, a circuit floor, lava); a turf is no entity, so these come as cell lights: every lit cell in MapGrid,
// then each change in the Delta. A ceiling's floor block (D295) lights the room under it the same way, marked Ceiling.

/// <summary>
/// The light a cell's turf gives (<see cref="Ceiling"/> false) or the floor block of its ceiling gives (true): tg's own light vars
/// of that turf, <see cref="LightState.None"/> in a Delta when it went out.
/// </summary>
public readonly record struct TurfLightChange(int X, int Y, int Z, bool Ceiling, LightState Light);

/// <summary>Version 96 of <see cref="MapGrid"/>: the lit turfs and ceilings at the join.</summary>
public sealed partial class MapGrid
{
    /// <summary>Version 96: every turf and ceiling that gives light at the join (none of them <see cref="LightState.None"/>); later changes come as <see cref="Delta.TurfLightChanged"/>.</summary>
    public List<TurfLightChange> TurfLights { get; } = new();
}

/// <summary>Version 96 of <see cref="Delta"/>: the turfs and ceilings whose light changed this tick.</summary>
public sealed partial class Delta
{
    /// <summary>Body: the turfs and ceilings whose light changed this tick (<see cref="LightState.None"/>: it went out).</summary>
    public List<TurfLightChange> TurfLightChanged { get; } = new();
}
