using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// The owner (2026-10-19): a firelock stands at both edges of its tile, its frames flush with the walls' faces, not in the tile's middle, where
/// the monitors and tables in the tile stood inside it. Its pick boxes are at the edges, and the tile's middle is free to aim through.
/// </summary>
public class FirelockEdgeTests
{
    private static PlanEntity Entity(string path, int x, int y)
        => new PlanEntity(1, path, x, y, 1, 2, TableRef.Public(1), 0, 0, LightState.None, null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFirelocksPickBoxesAreAtBothEdgesOfItsTile(bool open)
    {
        // between walls east and west: across the north-south passage
        CellClass Class(int x, int y, int z) => (x == 4 || x == 6) && y == 5 ? CellClass.Blocking : CellClass.Open;
        var probe = new PlanProbe { IsWall = (x, y, z) => Class(x, y, z) == CellClass.Blocking, Classify = Class };
        Assert.True(ChunkPlanner.TryPlace(Entity("/obj/machinery/door/firedoor", 5, 5), probe, out Placed firelock));
        Assert.Equal(RenderKind.Firelock, firelock.Shape);

        List<PieceBox> boxes = PieceBoxes.For(firelock, 2.0, open);
        Assert.NotEmpty(boxes);
        // every box lies in one of the two edge bands, a frame's depth from the tile's north or south face
        Assert.All(boxes, b => Assert.True(b.MinNorth >= 1.0 - PieceBoxes.FirelockDepth - 1e-9 || b.MaxNorth <= -1.0 + PieceBoxes.FirelockDepth + 1e-9,
            $"a box from north {b.MinNorth} to {b.MaxNorth} is not at an edge"));
        Assert.Contains(boxes, b => Math.Abs(b.MaxNorth - 1.0) < 1e-9);
        Assert.Contains(boxes, b => Math.Abs(b.MinNorth + 1.0) < 1e-9);
    }
}
