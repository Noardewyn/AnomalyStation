using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the mob under the crosshair (scope 2f: the first mob along the camera ray).</summary>
public class RayPickerTests
{
    private static bool Open(int x, int y, int z) => false;

    private static bool Pick(IReadOnlyList<PickCandidate> c, double yaw, double pitch, out long id, Func<int, int, int, bool> solid = null, double max = 10, long exclude = 99)
        => RayPicker.TryPick(10f, 10f, 1, 1.6f, yaw, pitch, c, solid ?? Open, max, exclude, out id, out _);

    [Fact]
    public void TheMobStraightAheadIsPicked()
    {
        var c = new[] { new PickCandidate(5, 10f, 11f, 1, false) }; // one tile (2 m) north
        Assert.True(Pick(c, 0, 0, out long id));
        Assert.Equal(5, id);
        Assert.False(Pick(c, Math.PI, 0, out _)); // looking south
    }

    [Fact]
    public void LookingPastTheShoulderOrOverTheHeadMisses()
    {
        var c = new[] { new PickCandidate(5, 10f, 11f, 1, false) };
        Assert.False(Pick(c, 0.5, 0, out _));      // 29 degrees to the left
        Assert.False(Pick(c, 0, 0.6, out _));      // up over the head
        Assert.True(Pick(c, 0, -0.1, out _));      // slightly down still hits the body
        Assert.False(Pick(c, 0, -0.9, out _));     // steeply down: the floor in front of the feet
    }

    [Fact]
    public void TheNearestOfTwoInLineIsPickedAndTheOwnBodyIsNeverPicked()
    {
        var c = new[]
        {
            new PickCandidate(7, 10f, 13f, 1, false),
            new PickCandidate(6, 10f, 11.5f, 1, false),
            new PickCandidate(99, 10f, 10f, 1, false),
        };
        Assert.True(Pick(c, 0, 0, out long id));
        Assert.Equal(6, id);
    }

    [Fact]
    public void AWallBetweenStopsTheRay()
    {
        var c = new[] { new PickCandidate(5, 10f, 12f, 1, false) };
        Assert.True(Pick(c, 0, 0, out _));
        Assert.False(Pick(c, 0, 0, out _, (x, y, z) => x == 10 && y == 11));
        // A wall beside the track does not.
        Assert.True(Pick(c, 0, 0, out _, (x, y, z) => x == 11 && y == 11));
        // The eye's own tile never blocks.
        Assert.True(Pick(c, 0, 0, out _, (x, y, z) => x == 10 && y == 10));
    }

    [Fact]
    public void ALyingMobIsAFlatDiscOnTheFloor()
    {
        var lying = new[] { new PickCandidate(5, 10f, 11f, 1, true) };
        Assert.False(Pick(lying, 0, 0, out _));            // straight ahead passes over it at eye height
        Assert.True(Pick(lying, 0, -0.55, out _));          // looking down at it
        var standing = new[] { new PickCandidate(5, 10f, 11f, 1, false) };
        Assert.True(Pick(standing, 0, 0, out _));
    }

    [Fact]
    public void ADistantMobOrAnotherLevelIsNotPicked()
    {
        Assert.False(Pick(new[] { new PickCandidate(5, 10f, 16f, 1, false) }, 0, 0, out _, max: 6));
        Assert.True(Pick(new[] { new PickCandidate(5, 10f, 16f, 1, false) }, 0, 0, out _, max: 20));
        Assert.False(Pick(new[] { new PickCandidate(5, 10f, 11f, 2, false) }, 0, 0, out _));
    }

    [Fact]
    public void ARayFromAboveHitsTheTopOfAMob()
    {
        Assert.True(RayPicker.HitCylinder(0, 0, 3.0, 0, 0, -1, 0, 0, 0.45, 1.8, out double t));
        Assert.Equal(1.2, t, 6);
        Assert.False(RayPicker.HitCylinder(0, 0, 3.0, 0, 0, 1, 0, 0, 0.45, 1.8, out _));
    }

    [Fact]
    public void ADiagonalTrackSeesWallsToo()
    {
        // From (10,10) looking north-east (yaw -45 degrees) a wall at (11,11) is hit before a mob at (12,12).
        var c = new[] { new PickCandidate(5, 12f, 12f, 1, false) };
        Assert.True(Pick(c, -Math.PI / 4, 0, out _));
        Assert.False(Pick(c, -Math.PI / 4, 0, out _, (x, y, z) => x == 11 && y == 11));
    }
}
