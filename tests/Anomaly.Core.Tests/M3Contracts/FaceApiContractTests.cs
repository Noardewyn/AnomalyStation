using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Motion;

namespace Anomaly.Core.Tests.M3Contracts;

/// <summary>The M3 contracts step's face API (src/Core/Motion/TileFaces.cs, ICollisionView.IsFaceBlocked): one definition of a blocked face, seen from either side.</summary>
public class FaceApiContractTests
{
    [Fact]
    public void AFaceIsBlockedFromBothSides()
    {
        ICollisionView view = new GridView().Edge(5, 5, TileEdges.East); // a window on the east border of (5, 5)
        Assert.True(view.IsFaceBlocked(5, 5, 1, MotionDir.East));
        Assert.True(view.IsFaceBlocked(6, 5, 1, MotionDir.West)); // the same face from the neighbour
        Assert.False(view.IsFaceBlocked(5, 5, 1, MotionDir.West));
        Assert.False(view.IsFaceBlocked(5, 5, 1, MotionDir.North));
        Assert.False(view.IsFaceBlocked(4, 5, 1, MotionDir.East));
    }

    [Fact]
    public void TheNeighboursOppositeFaceBlocksToo()
    {
        ICollisionView view = new GridView().Edge(6, 5, TileEdges.West);
        Assert.True(view.IsFaceBlocked(5, 5, 1, MotionDir.East));
        Assert.True(view.IsFaceBlocked(6, 5, 1, MotionDir.West));
        ICollisionView north = new GridView().Edge(5, 6, TileEdges.South);
        Assert.True(north.IsFaceBlocked(5, 5, 1, MotionDir.North));
        Assert.False(north.IsFaceBlocked(5, 6, 1, MotionDir.North));
    }

    [Fact]
    public void SolidityIsNotAFaceAndNonCardinalDirectionsBlockNothing()
    {
        ICollisionView view = new GridView().Solid(6, 5).Edge(5, 5, TileEdges.North | TileEdges.East);
        Assert.False(view.IsFaceBlocked(5, 5, 1, MotionDir.West)); // (4, 5) open, no face
        Assert.False(((ICollisionView)new GridView().Solid(6, 5)).IsFaceBlocked(5, 5, 1, MotionDir.East)); // a solid neighbour is not a face
        Assert.False(view.IsFaceBlocked(5, 5, 1, MotionDir.North | MotionDir.East));
        Assert.False(view.IsFaceBlocked(5, 5, 1, MotionDir.None));
    }

    [Fact]
    public void TheHelpersMapDirectionsToFaces()
    {
        Assert.Equal(TileEdges.North, TileFaces.Toward(MotionDir.North));
        Assert.Equal(TileEdges.West, TileFaces.Toward(MotionDir.West));
        Assert.Equal(TileEdges.None, TileFaces.Toward(5));
        Assert.Equal(TileEdges.North | TileEdges.East, TileFaces.OfDir(5)); // NORTHEAST
        Assert.Equal(TileEdges.South | TileEdges.West, TileFaces.OfDir(10)); // SOUTHWEST
        Assert.Equal(TileEdges.None, TileFaces.OfDir(16)); // UP is no face
        Assert.Equal(TileEdges.South | TileEdges.West, TileFaces.Opposite(TileEdges.North | TileEdges.East));
        Assert.Equal((5, 6), TileFaces.Neighbour(5, 5, MotionDir.North));
        Assert.Equal((4, 5), TileFaces.Neighbour(5, 5, MotionDir.West));
    }

    [Fact]
    public void TheControllerStopsAtExactlyTheFacesIsFaceBlockedReports()
    {
        // The invariant P5 pins in full: the controller's crossing of a border agrees with IsFaceBlocked (here one probe of
        // M2's sweep, which already reads EdgesOf on both sides).
        var view = new GridView().Edge(5, 5, TileEdges.East);
        var state = MotionState.AtTile(5, 5, 1);
        var events = new MotionEvents();
        var input = new MotionInput(1, MotionInput.MaxMove, 0, 0, default);
        for (int i = 0; i < 40; i++)
        {
            events.Clear();
            MotionStep.Step(ref state, in input, MotionParams.DefaultRun, view, events);
        }
        Assert.Equal(5, MotionMath.TileOf(state.X));
        Assert.True(((ICollisionView)view).IsFaceBlocked(5, 5, 1, MotionDir.East));
    }
}
