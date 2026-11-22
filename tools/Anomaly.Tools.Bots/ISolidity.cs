using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>
/// What a bot knows about walls: the solidity grid of its replica. Tile centres are at whole numbers (tile x covers
/// [x - 0.5, x + 0.5)), x and y from 1, BYOND's y grows north. A small interface so that the planners are tested on plain
/// grids, with no replica and no codec.
/// </summary>
public interface ISolidity
{
    /// <summary>True when the tile (x, y) of level z blocks capsules; outside the map: true.</summary>
    bool IsSolid(int x, int y, int z);
}

/// <summary>The solidity of a <see cref="ReplicaWorld"/> (MapGrid version 2's bits and the Delta's cell changes).</summary>
public sealed class ReplicaSolidity : ISolidity
{
    private readonly ReplicaWorld _replica;

    public ReplicaSolidity(ReplicaWorld replica)
    {
        _replica = replica ?? throw new ArgumentNullException(nameof(replica));
    }

    public bool IsSolid(int x, int y, int z) => _replica.IsSolid(x, y, z);
}
