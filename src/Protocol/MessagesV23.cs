namespace Anomaly.Protocol;

// Version 23 (DV54, protocol.md section 32): the ghost's body. A connection whose mob is a ghost that may re-enter its corpse
// (tg's can_reenter_corpse and its mind's current) gets the body's turf in its private block when it changes, so the client
// marks where the body lies; none when the mob is not such a ghost.

/// <summary>Version 23: a cell of the map (BYOND coordinates, each from 1).</summary>
public readonly record struct MapCell(int X, int Y, int Z);

public sealed partial class OwnBlock
{
    /// <summary>Version 23: the block carries <see cref="Body"/> (it changed since the last block, or this is a Snapshot's).</summary>
    public bool BodyChanged { get; set; }

    /// <summary>Version 23: the turf of the ghost's body, or null when the connection's mob is no ghost with a body to re-enter.</summary>
    public MapCell? Body { get; set; }
}

public sealed partial class ReplicaWorld
{
    /// <summary>Version 23: the turf of the ghost's body (DV54), or null when the own mob is no ghost with a body to re-enter.</summary>
    public MapCell? OwnBody { get; private set; }
}
