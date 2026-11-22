using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>A false wall (false_walls.dm): a wall block while shut, tg's frame sprite while open, opening or closing.</summary>
public class FalseWallTests
{
    [Fact]
    public void AShutFalseWallIsABlockAndAnOpenOneItsSprite()
    {
        Assert.Equal(RenderKind.FalseWall, PropKinds.Classify("/obj/structure/falsewall", "wall-12"));
        Assert.Equal(RenderKind.FalseWall, PropKinds.Classify("/obj/structure/falsewall/reinforced", "reinforced_wall-5"));
        foreach (string state in new[] { "wall-open", "wall-opening", "wall-closing" })
            Assert.Equal(RenderKind.Billboard, PropKinds.Classify("/obj/structure/falsewall", state));
        Assert.True(BlockFaces.Applies(RenderKind.FalseWall));
        Assert.Equal(new[] { PieceParts.FullWindow }, PieceParts.PartsOf(RenderKind.FalseWall));
    }
}
