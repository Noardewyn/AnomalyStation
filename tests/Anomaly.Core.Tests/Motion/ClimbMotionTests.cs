using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// D239 (M8z slice C): a capsule with tg's <c>TRAIT_ON_CLIMBABLE</c> (<see cref="MotionParams.OnClimbable"/>) walks through the tiles solid
/// only through climb_walkable structures; every other capsule meets a table as a wall, exactly as before.
/// </summary>
public class ClimbMotionTests
{
    private const int C5 = 5 * 256;

    private static MotionParams Climber => Run with { OnClimbable = true };

    // Walks east for the ticks; returns the crossings' target tiles.
    private static List<int> WalkEast(ref MotionState s, MotionParams p, ICollisionView view, int ticks, sbyte moveY = 0)
    {
        var crossed = new List<int>();
        for (int i = 0; i < ticks; i++)
        {
            foreach (MotionEvent e in Step(ref s, Frame(127, moveY), p, view))
            {
                if (e.Kind == MotionEventKind.Crossed)
                    crossed.Add(e.ToX);
            }
        }
        return crossed;
    }

    [Fact]
    public void WalkingPastARowOfTablesIsTheSameAsWithoutThem()
    {
        // Regression: the row y = 6 is tables; a plain capsule walks along y = 5 next to them, and one presses diagonally into them.
        GridView tables = new GridView().Table(6, 6).Table(7, 6).Table(8, 6);
        GridView walls = new GridView().Solid(6, 6).Solid(7, 6).Solid(8, 6);
        foreach (sbyte up in new sbyte[] { 0, 60 })
        {
            MotionState a = MotionState.AtTile(4, 5, 1), b = MotionState.AtTile(4, 5, 1);
            List<int> ca = WalkEast(ref a, Run, tables, 20, up);
            List<int> cb = WalkEast(ref b, Run, walls, 20, up);
            Assert.Equal((b.X, b.Y), (a.X, a.Y));
            Assert.Equal(cb, ca);
            Assert.False(tables.BoxInSolid(a.X, a.Y, 45));
        }
    }

    [Fact]
    public void APlainCapsuleStopsAtATablesFace()
    {
        GridView view = new GridView().Table(6, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        Assert.Empty(WalkEast(ref s, Run, view, 6));
        Assert.Equal(C5 + 128 - 45, s.X);
    }

    [Fact]
    public void AClimberWalksFromTableToTableAndStepsDown()
    {
        GridView view = new GridView().Table(5, 5).Table(6, 5).Table(7, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        List<int> crossed = WalkEast(ref s, Climber, view, 12);
        Assert.Equal(new[] { 6, 7, 8 }, crossed.Take(3));
        Assert.True(s.X > 8 * 256 - 128);
    }

    [Fact]
    public void AClimberIsStillStoppedByAWall()
    {
        GridView view = new GridView().Table(5, 5).Solid(6, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        Assert.Empty(WalkEast(ref s, Climber, view, 6));
        Assert.Equal(C5 + 128 - 45, s.X);
    }

    [Fact]
    public void ACapsuleOnATableWithoutTheTraitCanStillLeaveItsTile()
    {
        // A rack climbed onto (climbable, not climb_walkable): the capsule stands in a solid tile and walks off it, not onto the next.
        GridView view = new GridView().Solid(5, 5).Solid(6, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        Assert.Empty(WalkEast(ref s, Run, view, 6));
        MotionState w = MotionState.AtTile(5, 5, 1);
        for (int i = 0; i < 6; i++)
            Step(ref w, Frame(-127, 0), Run, view);
        Assert.True(w.X < C5 - 128);
    }
}
