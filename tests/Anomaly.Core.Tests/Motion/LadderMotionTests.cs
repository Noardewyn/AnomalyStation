using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// DV397: a ladder's rails (<see cref="LadderBody"/>) stand across the north side of its tile: a capsule walking north onto the tile stops before
/// them, touching, at <see cref="LadderBody.Front"/>; one walking along that side is stopped too; the rest of the tile is free to cross.
/// </summary>
public class LadderMotionTests
{
    private const int Radius = 45; // MotionMath.DefaultRadius in subunits

    [Fact]
    public void WalkingNorthStopsBeforeTheRailsAtTheClimbingSpot()
    {
        GridView view = new GridView().Ladder(5, 6);
        MotionState s = MotionState.AtTile(5, 5, 1);
        for (int i = 0; i < 30; i++)
            Step(ref s, Frame(0, 127), Run, view);
        Assert.Equal(LadderBody.Front(5, 6, Radius), (s.X, s.Y));
    }

    [Fact]
    public void WalkingAlongTheRailsIsStoppedAndTheSouthOfTheTileIsFree()
    {
        GridView view = new GridView().Ladder(6, 5);
        // along the rails' line, from the tile west of the ladder: stopped before the west rail
        MotionState along = MotionState.AtTile(4, 5, 1);
        along.Y += LadderBody.Line;
        for (int i = 0; i < 30; i++)
            Step(ref along, Frame(127, 0), Run, view);
        Assert.True(along.X <= 6 * 256 - LadderBody.HalfSpan - Radius, $"stopped at {along.X}");
        // across the tile's south half: no obstacle
        MotionState south = MotionState.AtTile(4, 5, 1);
        south.Y -= 40;
        for (int i = 0; i < 30; i++)
            Step(ref south, Frame(127, 0), Run, view);
        Assert.True(south.TileX >= 8, $"reached tile {south.TileX}");
    }

    [Fact]
    public void ACapsuleSqueezedIntoTheRailsIsPushedOutAndNoContactIsRaised()
    {
        GridView view = new GridView().Ladder(5, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.Y += LadderBody.Line - 20;
        List<MotionEvent> events = Step(ref s, Frame(0, 0), Run, view);
        (int fx, int fy) = LadderBody.Front(5, 5, Radius);
        Assert.Equal(fx, s.X);
        Assert.True(s.Y <= fy, $"pushed to {s.Y}");
        Assert.DoesNotContain(events, e => e.Kind == MotionEventKind.Contact);
    }
}
