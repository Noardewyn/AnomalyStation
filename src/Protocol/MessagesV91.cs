namespace Anomaly.Protocol;

// Protocol version 91 (the owner, 2026-11-20, revising D119; docs/ai/specs/protocol.md section 88; the codec: Codec.V91.cs).
// - MapGrid ends with the height of every level's walls and ceiling, the server's setting: the client builds its world with it, so a shot, a
//   throw or a pointer meets the ceiling where the server says it does.

public sealed partial class MapGrid
{
    /// <summary>The lowest and highest <see cref="WallHeightMillimetres"/>.</summary>
    public const int MinWallHeightMillimetres = 2000, MaxWallHeightMillimetres = 4000;

    /// <summary>Version 91: the height of every level's walls and ceiling, millimetres (2000: as high as a tile is wide).</summary>
    public int WallHeightMillimetres { get; set; } = MinWallHeightMillimetres;
}
