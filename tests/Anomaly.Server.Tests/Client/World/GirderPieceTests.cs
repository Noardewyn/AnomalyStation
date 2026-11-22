using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M5 slice 4 (D142): a girder is drawn as a piece, the full-tile window's joined slab showing tg's girder sprite (MapRenderer's straight-run picture),
/// its arms toward the walls it stands between, the way tg's girder smooths with walls and girders (girders.dm:13).
/// </summary>
public class GirderPieceTests
{
    private static PlanEntity Girder(string path, int x, int y)
        => new PlanEntity(1, path, x, y, 1, 2, TableRef.Public(1), 0, 0, LightState.None, null);

    [Fact]
    public void EveryGirderIsAJoinedSlabPiece()
    {
        foreach (string path in new[] { "/obj/structure/girder", "/obj/structure/girder/reinforced", "/obj/structure/girder/displaced" })
            Assert.Equal(RenderKind.Girder, PropKinds.Classify(path));
        Assert.Equal(new[] { PieceParts.FullWindow }, PieceParts.PartsOf(RenderKind.Girder));
        Assert.Equal(RenderKind.Billboard, PropKinds.Classify("/obj/structure/girderish")); // a whole path segment
    }

    [Fact]
    public void AGirderLeftWhereAWallWasJoinsTheWallsBesideIt()
    {
        // the welded wall's cell between a wall north and a wall south: a post and the north and south arms
        bool Wall(int x, int y, int z) => x == 5 && (y == 6 || y == 4);
        Assert.True(ChunkPlanner.TryPlace(Girder("/obj/structure/girder", 5, 5), Wall, Wall, null, null, out Placed placed));
        Assert.Equal(PlacedKind.Piece, placed.Kind);
        Assert.Equal(RenderKind.Girder, placed.Shape);
        Assert.Equal(Joints.North | Joints.South, placed.Joints);
        Assert.Equal(0, placed.Face); // joined in world directions, never turned
    }
}
