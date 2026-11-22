using Anomaly.Core.Motion;

namespace Anomaly.Core.Tests.Motion;

/// <summary>Faces in the controller (M3): a pane on a tile border stops the capsule over many ticks from either side, and the two views of one face agree.</summary>
public class FaceSweepTests
{
    private static MotionState Walk(GridView view, MotionState start, sbyte mx, sbyte my, int ticks, MotionParams p)
    {
        MotionState s = start;
        var events = new MotionEvents();
        for (int i = 0; i < ticks; i++)
        {
            events.Clear();
            MotionStep.Step(ref s, new MotionInput((uint)(i + 1), mx, my, 0, MotionInputFlags.None), p, view, events);
        }

        return s;
    }

    private static readonly MotionParams Run = new(0.3333f, 0.175f, true, 3);

    [Fact]
    public void AFaceStopsAWalkerComingFromEitherSideOverManyTicks()
    {
        // The face between tiles 7 and 8, seen as tile 8's west face or as tile 7's east face: the same stop.
        foreach (GridView view in new[] { new GridView().Edge(8, 5, TileEdges.West), new GridView().Edge(7, 5, TileEdges.East) })
        {
            MotionState east = Walk(view, MotionState.AtTile(5, 5, 1), 127, 0, 30, Run);
            Assert.Equal(7 * 256 + 128 - 45, east.X);
            MotionState west = Walk(view, MotionState.AtTile(11, 5, 1), -127, 0, 30, Run);
            Assert.Equal(8 * 256 - 128 + 45, west.X);
            // Free along the face and around it.
            MotionState north = Walk(view, MotionState.AtTile(8, 5, 1), 0, 127, 12, Run);
            Assert.Equal(8 * 256, north.X);
            Assert.True(north.Y > 5 * 256 + 256);
        }
    }

    [Fact]
    public void ADiagonalOrNorthFaceStopsOnlyThatAxis()
    {
        var view = new GridView().Edge(8, 5, TileEdges.North | TileEdges.West);
        MotionState north = Walk(view, MotionState.AtTile(8, 3, 1), 0, 127, 20, Run);
        Assert.Equal(5 * 256 + 128 - 45, north.Y);
        MotionState west = Walk(view, MotionState.AtTile(11, 5, 1), -127, 0, 20, Run);
        Assert.Equal(8 * 256 - 128 + 45, west.X);
        MotionState south = Walk(view, MotionState.AtTile(8, 8, 1), 0, -127, 20, Run);
        Assert.Equal(5 * 256 + 128 + 45, south.Y); // from the north the face blocks too: it is the tile's north border
    }
}
