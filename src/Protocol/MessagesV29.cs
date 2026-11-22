namespace Anomaly.Protocol;

// Version 29 (the eye blur, protocol.md section 38): the game plane. tg filters a player's whole game view through the plane master
// controller of its hud's game planes (/atom/movable/plane_master_controller/game: the eye blur's gaussian blur, a drug's colour); the
// port has no plane masters, so the controller's own appearance, with its filters, goes to its player in the private block whenever it
// changes, and the client draws those filters over its 3D view.

public sealed partial class OwnBlock
{
    /// <summary>Version 29: the block carries <see cref="GamePlane"/> (it changed since the last block, or this is a Snapshot's).</summary>
    public bool GamePlaneChanged { get; set; }

    /// <summary>
    /// Version 29: the appearance of the connection's game plane master controller (tg's <c>plane_master_controllers[PLANE_MASTERS_GAME]</c>
    /// of its mob's hud), in the private or public table, carrying the filters tg put on it; <see cref="TableRef.None"/> when the mob
    /// has no hud.
    /// </summary>
    public TableRef GamePlane { get; set; }
}

public sealed partial class ReplicaWorld
{
    /// <summary>Version 29: the appearance of the own game plane (tg's game plane master controller), <see cref="TableRef.None"/> for none.</summary>
    public TableRef GamePlane { get; private set; }

    /// <summary>Version 29: the filters on the own game plane (the eye blur's blur), in tg's order; empty for none.</summary>
    public IReadOnlyList<FilterEntry> GamePlaneFilters
        => !GamePlane.IsNone && TryGetAppearance(GamePlane, out AppearanceEntry plane) && plane.Filters != null ? plane.Filters : Array.Empty<FilterEntry>();
}
