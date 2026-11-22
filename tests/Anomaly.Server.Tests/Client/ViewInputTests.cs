using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: the view-relative direction mapping (D101).</summary>
public class ViewInputTests
{
    private const double Deg = Math.PI / 180.0;

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(90.0, 1)]
    [InlineData(180.0, 2)]
    [InlineData(-90.0, 3)]
    [InlineData(270.0, 3)]
    [InlineData(360.0, 0)]
    [InlineData(-180.0, 2)]
    [InlineData(44.0, 0)]
    [InlineData(46.0, 1)]
    [InlineData(-44.0, 0)]
    [InlineData(-46.0, 3)]
    [InlineData(134.0, 1)]
    [InlineData(136.0, 2)]
    [InlineData(-720.0 - 10.0, 0)]
    public void YawIsQuantizedToTheNearestCardinal(double degrees, int expected)
    {
        Assert.Equal(expected, ViewInput.QuarterTurns(degrees * Deg));
    }

    [Fact]
    public void ABrokenYawFacesNorth()
    {
        Assert.Equal(0, ViewInput.QuarterTurns(double.NaN));
        Assert.Equal(0, ViewInput.QuarterTurns(double.PositiveInfinity));
    }

    [Theory]
    // heading north: the keys are the map's directions
    [InlineData(0, MoveKeys.Forward, HeldDirs.North)]
    [InlineData(0, MoveKeys.Back, HeldDirs.South)]
    [InlineData(0, MoveKeys.Left, HeldDirs.West)]
    [InlineData(0, MoveKeys.Right, HeldDirs.East)]
    // heading west (+90 degrees): forward is west, left is south
    [InlineData(1, MoveKeys.Forward, HeldDirs.West)]
    [InlineData(1, MoveKeys.Back, HeldDirs.East)]
    [InlineData(1, MoveKeys.Left, HeldDirs.South)]
    [InlineData(1, MoveKeys.Right, HeldDirs.North)]
    // heading south
    [InlineData(2, MoveKeys.Forward, HeldDirs.South)]
    [InlineData(2, MoveKeys.Back, HeldDirs.North)]
    [InlineData(2, MoveKeys.Left, HeldDirs.East)]
    [InlineData(2, MoveKeys.Right, HeldDirs.West)]
    // heading east
    [InlineData(3, MoveKeys.Forward, HeldDirs.East)]
    [InlineData(3, MoveKeys.Back, HeldDirs.West)]
    [InlineData(3, MoveKeys.Left, HeldDirs.North)]
    [InlineData(3, MoveKeys.Right, HeldDirs.South)]
    public void OneKeyIsRotatedByTheHeading(int turns, MoveKeys keys, HeldDirs expected)
    {
        Assert.Equal(expected, ViewInput.ToAbsolute(keys, turns));
    }

    [Theory]
    [InlineData(0, MoveKeys.Forward | MoveKeys.Right, HeldDirs.North | HeldDirs.East)]
    [InlineData(0, MoveKeys.Back | MoveKeys.Left, HeldDirs.South | HeldDirs.West)]
    [InlineData(1, MoveKeys.Forward | MoveKeys.Right, HeldDirs.West | HeldDirs.North)]
    [InlineData(1, MoveKeys.Forward | MoveKeys.Left, HeldDirs.West | HeldDirs.South)]
    [InlineData(2, MoveKeys.Forward | MoveKeys.Left, HeldDirs.South | HeldDirs.East)]
    [InlineData(3, MoveKeys.Back | MoveKeys.Right, HeldDirs.West | HeldDirs.South)]
    public void TwoHeldKeysGiveADiagonal(int turns, MoveKeys keys, HeldDirs expected)
    {
        Assert.Equal(expected, ViewInput.ToAbsolute(keys, turns));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OppositeKeysCancelAtEveryHeading(int turns)
    {
        Assert.Equal(HeldDirs.None, ViewInput.ToAbsolute(MoveKeys.Forward | MoveKeys.Back, turns));
        Assert.Equal(HeldDirs.None, ViewInput.ToAbsolute(MoveKeys.Left | MoveKeys.Right, turns));
        Assert.Equal(HeldDirs.None, ViewInput.ToAbsolute(MoveKeys.Forward | MoveKeys.Back | MoveKeys.Left | MoveKeys.Right, turns));
        // The key that is not cancelled still counts (tg: "hold left and right and up to make sure you only go up").
        Assert.Equal(ViewInput.ToAbsolute(MoveKeys.Forward, turns), ViewInput.ToAbsolute(MoveKeys.Forward | MoveKeys.Left | MoveKeys.Right, turns));
    }

    [Fact]
    public void TurnsWrapAround()
    {
        foreach (MoveKeys keys in new[] { MoveKeys.Forward, MoveKeys.Left | MoveKeys.Back })
        {
            Assert.Equal(ViewInput.ToAbsolute(keys, 3), ViewInput.ToAbsolute(keys, -1));
            Assert.Equal(ViewInput.ToAbsolute(keys, 1), ViewInput.ToAbsolute(keys, 5));
        }
    }

    [Fact]
    public void ForwardAlwaysWalksTowardsWhereTheViewFaces()
    {
        // The facing direction of a yaw (Godot: -Z is north, +X east, yaw turns counter-clockwise) against the walking direction.
        for (int degrees = -180; degrees < 180; degrees += 3)
        {
            double yaw = degrees * Deg;
            double east = -Math.Sin(yaw);
            double north = Math.Cos(yaw);
            HeldDirs walk = ViewInput.ToAbsolute(MoveKeys.Forward, ViewInput.QuarterTurns(yaw));
            double walkEast = (walk == HeldDirs.East ? 1 : 0) - (walk == HeldDirs.West ? 1 : 0);
            double walkNorth = (walk == HeldDirs.North ? 1 : 0) - (walk == HeldDirs.South ? 1 : 0);
            // The walking direction is within 45 degrees (plus rounding) of where the view faces.
            Assert.True(east * walkEast + north * walkNorth >= Math.Cos(45.5 * Deg), $"yaw {degrees}: walks {walk}");
        }
    }

    [Fact]
    public void DescribeNamesTheDirections()
    {
        Assert.Equal("-", ViewInput.Describe(HeldDirs.None));
        Assert.Equal("NE", ViewInput.Describe(HeldDirs.North | HeldDirs.East));
        Assert.Equal("SW", ViewInput.Describe(HeldDirs.West | HeldDirs.South));
    }
}

/// <summary>M1 P6: the stream of absolute masks (an InputFrame goes out when the mask changes).</summary>
public class InputTrackerTests
{
    private const double Deg = Math.PI / 180.0;

    [Fact]
    public void NothingHeldReportsNothing()
    {
        var tracker = new InputTracker();
        Assert.False(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.None, dirs);
        tracker.SetYaw(2.0);
        Assert.False(tracker.TryTakeChange(out _));
    }

    [Fact]
    public void AKeyPressAndReleaseAreReportedOnce()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Forward);
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.North, dirs);
        Assert.False(tracker.TryTakeChange(out _));
        Assert.False(tracker.TryTakeChange(out _));

        tracker.SetKeys(MoveKeys.None);
        Assert.True(tracker.TryTakeChange(out dirs));
        Assert.Equal(HeldDirs.None, dirs);
        Assert.False(tracker.TryTakeChange(out _));
    }

    [Fact]
    public void AddingASecondKeyChangesTheMask()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Forward);
        tracker.TryTakeChange(out _);
        tracker.SetKeys(MoveKeys.Forward | MoveKeys.Right);
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.North | HeldDirs.East, dirs);
    }

    [Fact]
    public void OppositeKeysReportNothingNew()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Left);
        Assert.True(tracker.TryTakeChange(out _));
        tracker.SetKeys(MoveKeys.Left | MoveKeys.Right); // both held: cancelled
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.None, dirs);
    }

    [Fact]
    public void TurningWhileAKeyIsHeldSendsTheNewDirection()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Forward);
        tracker.TryTakeChange(out _);

        tracker.SetYaw(90 * Deg); // now facing west
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.West, dirs);

        tracker.SetYaw(180 * Deg);
        Assert.True(tracker.TryTakeChange(out dirs));
        Assert.Equal(HeldDirs.South, dirs);

        tracker.SetYaw(-90 * Deg);
        Assert.True(tracker.TryTakeChange(out dirs));
        Assert.Equal(HeldDirs.East, dirs);
    }

    [Fact]
    public void TurningWithNothingHeldSendsNothing()
    {
        var tracker = new InputTracker();
        for (int degrees = 0; degrees < 720; degrees += 10)
        {
            tracker.SetYaw(degrees * Deg);
            Assert.False(tracker.TryTakeChange(out _));
        }
    }

    [Fact]
    public void TheHeadingHasAHysteresisOnTheDiagonal()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Forward);
        tracker.TryTakeChange(out _);

        // Resting on the 45 degree boundary, with the mouse jittering across it: no flips.
        int changes = 0;
        for (int i = 0; i < 200; i++)
        {
            tracker.SetYaw((i % 2 == 0 ? 44.0 : 46.5) * Deg);
            if (tracker.TryTakeChange(out _))
                changes++;
        }
        Assert.Equal(0, changes);
        Assert.Equal(0, tracker.Turns);

        tracker.SetYaw(51 * Deg); // clearly past the boundary
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.West, dirs);
        Assert.Equal(1, tracker.Turns);

        // And back: it stays west until the yaw is more than 50 degrees from west's centre (40 degrees).
        tracker.SetYaw(44 * Deg);
        Assert.False(tracker.TryTakeChange(out _));
        tracker.SetYaw(39 * Deg);
        Assert.True(tracker.TryTakeChange(out dirs));
        Assert.Equal(HeldDirs.North, dirs);
    }

    [Fact]
    public void TheYawWrapsAroundTheFullCircle()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Forward);
        tracker.TryTakeChange(out _);
        tracker.SetYaw(360 * Deg + 10 * Deg);
        Assert.False(tracker.TryTakeChange(out _));
        tracker.SetYaw(-90 * Deg);
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.East, dirs);
        tracker.SetYaw(270 * Deg + 8 * Deg); // the same heading again, through a full turn
        Assert.False(tracker.TryTakeChange(out _));
    }

    [Fact]
    public void ReleaseAllReportsTheRelease()
    {
        var tracker = new InputTracker();
        tracker.SetKeys(MoveKeys.Back | MoveKeys.Left);
        tracker.TryTakeChange(out _);
        tracker.ReleaseAll();
        Assert.True(tracker.TryTakeChange(out HeldDirs dirs));
        Assert.Equal(HeldDirs.None, dirs);
        Assert.Equal(HeldDirs.None, tracker.Reported);
    }
}
