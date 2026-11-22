namespace Anomaly.Protocol;

// Protocol version 60 (D305, docs/ai/specs/protocol.md section 62; the codec: Codec.V60.cs).
// - MapGrid ends with the levels that have tg's ZTRAIT_NOPARALLAX (Lavaland), where tg draws no space behind the map; the client draws
//   its own sky there and no light of space.

public sealed partial class MapGrid
{
    /// <summary>Version 60: the levels (1-based, ascending) with tg's <c>ZTRAIT_NOPARALLAX</c>.</summary>
    public List<int> NoParallaxLevels { get; } = new();
}
