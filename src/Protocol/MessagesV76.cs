namespace Anomaly.Protocol;

// Protocol version 76 (D325, multi-z; docs/ai/specs/protocol.md section 77; the codec: Codec.V76.cs).
// - MapGrid ends with the levels linked to the level below them (tg's ZTRAIT_DOWN): such a level and the one under it are one
//   stack, which the client draws together, each level at its height, seen through open space.

public sealed partial class MapGrid
{
    /// <summary>Version 76: the levels (1-based, ascending, each above level 1) with tg's <c>ZTRAIT_DOWN</c>: level z stands on z - 1.</summary>
    public List<int> LinkedBelowLevels { get; } = new();
}
