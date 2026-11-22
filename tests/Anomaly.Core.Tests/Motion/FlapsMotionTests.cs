using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// DV360: tg's airtight plastic flaps (<see cref="ICollisionView.IsFlaps"/>) stop a standing capsule at their tile as a wall; a lying one, or
/// one with <see cref="MotionParams.PassesFlaps"/>, walks in, and a capsule already on a flaps tile walks out.
/// </summary>
public class FlapsMotionTests
{
    private static int WalkEast(ref MotionState s, MotionParams p, ICollisionView view, int ticks)
    {
        for (int i = 0; i < ticks; i++)
            Step(ref s, Frame(127, 0), p, view);
        return s.TileX;
    }

    [Fact]
    public void FlapsStopAStandingCapsuleAndLetALyingOrPassingOneThrough()
    {
        GridView view = new GridView().Flaps(6, 5);
        MotionState standing = MotionState.AtTile(5, 5, 1);
        Assert.Equal(5, WalkEast(ref standing, Run, view, 10));

        MotionState lying = MotionState.AtTile(5, 5, 1);
        lying.Posture = Posture.Lying;
        lying.Driver = MotionDriver.Prone;
        Assert.True(WalkEast(ref lying, Run, view, 10) >= 7);

        MotionState passing = MotionState.AtTile(5, 5, 1);
        Assert.True(WalkEast(ref passing, Run with { PassesFlaps = true }, view, 10) >= 7);

        MotionState inside = MotionState.AtTile(6, 5, 1); // stood up under the flaps
        Assert.True(WalkEast(ref inside, Run, view, 10) >= 7);
    }
}
