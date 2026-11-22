using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the keys and the view yaw as the world-space wish vector of a MotionInput.</summary>
public class MoveWishTests
{
    private const double Quarter = Math.PI / 2;

    private static void Near((float East, float North) wish, float east, float north)
    {
        Assert.Equal(east, wish.East, 4);
        Assert.Equal(north, wish.North, 4);
    }

    [Fact]
    public void ForwardWalksTheWayTheViewFaces()
    {
        Near(MoveWish.FromKeys(MoveKeys.Forward, 0), 0, 1);            // north
        Near(MoveWish.FromKeys(MoveKeys.Forward, Quarter), -1, 0);     // +90 degrees faces west
        Near(MoveWish.FromKeys(MoveKeys.Forward, Math.PI), 0, -1);     // south
        Near(MoveWish.FromKeys(MoveKeys.Forward, -Quarter), 1, 0);     // east
    }

    [Fact]
    public void LeftRightAndBackAreRelativeToTheView()
    {
        Near(MoveWish.FromKeys(MoveKeys.Right, 0), 1, 0);
        Near(MoveWish.FromKeys(MoveKeys.Left, 0), -1, 0);
        Near(MoveWish.FromKeys(MoveKeys.Back, 0), 0, -1);
        Near(MoveWish.FromKeys(MoveKeys.Right, Quarter), 0, 1);        // facing west, the right hand is north
        Near(MoveWish.FromKeys(MoveKeys.Left, Quarter), 0, -1);
    }

    [Fact]
    public void ADiagonalIsNotFasterThanAStraightLine()
    {
        (float east, float north) = MoveWish.FromKeys(MoveKeys.Forward | MoveKeys.Right, 0);
        Assert.Equal(1.0, Math.Sqrt(east * east + north * north), 4);
        Assert.Equal(east, north, 4);
        // At an angle between the grid directions too: the wish is not quantized to tiles (DV15).
        (float e2, float n2) = MoveWish.FromKeys(MoveKeys.Forward, 0.3);
        Assert.Equal(1.0, Math.Sqrt(e2 * e2 + n2 * n2), 4);
        Assert.NotEqual(0f, e2);
    }

    [Fact]
    public void OppositeKeysCancelAndNoKeysIsNothing()
    {
        Near(MoveWish.FromKeys(MoveKeys.Forward | MoveKeys.Back, 0.7), 0, 0);
        Near(MoveWish.FromKeys(MoveKeys.Left | MoveKeys.Right, 0.7), 0, 0);
        Near(MoveWish.FromKeys(MoveKeys.None, 0.7), 0, 0);
        Near(MoveWish.FromKeys(MoveKeys.Forward | MoveKeys.Back | MoveKeys.Right, 0), 1, 0);
    }

    [Fact]
    public void TheWireComponentsAreClamped()
    {
        Assert.Equal((sbyte)127, MoveWish.ToWire(1f));
        Assert.Equal((sbyte)-127, MoveWish.ToWire(-1f));
        Assert.Equal((sbyte)127, MoveWish.ToWire(5f));
        Assert.Equal((sbyte)-127, MoveWish.ToWire(-5f));
        Assert.Equal((sbyte)0, MoveWish.ToWire(0f));
        Assert.Equal((sbyte)0, MoveWish.ToWire(float.NaN));
        Assert.Equal((sbyte)64, MoveWish.ToWire(0.5f));
    }

    [Fact]
    public void TheCompassYawIsTheNegativeOfGodotsYaw()
    {
        Assert.Equal((ushort)0, MoveWish.CompassYaw(0));
        Assert.Equal((ushort)16384, MoveWish.CompassYaw(-Quarter));       // east: 90 degrees clockwise
        Assert.Equal((ushort)49152, MoveWish.CompassYaw(Quarter));        // west
        Assert.Equal((ushort)32768, MoveWish.CompassYaw(Math.PI));
        Assert.Equal((ushort)32768, MoveWish.CompassYaw(-Math.PI));
        Assert.Equal((ushort)16384, MoveWish.CompassYaw(-Quarter + 2 * Math.PI));
        Assert.Equal((ushort)0, MoveWish.CompassYaw(double.NaN));
    }

    [Fact]
    public void TheCompassYawRoundTrips()
    {
        for (ushort compass = 0; compass < ushort.MaxValue - 100; compass = (ushort)(compass + 997))
        {
            double yaw = MoveWish.GodotYaw(compass);
            Assert.InRange(MoveWish.CompassYaw(yaw), compass - 1, compass + 1);
        }
        Assert.Equal((byte)64, MoveWish.YawByte(16384));
        Assert.Equal((ushort)(64 * 256 + 128), MoveWish.FromYawByte(64));
    }

    [Fact]
    public void TheIntegratorWeighsAKeyByTheTimeItWasHeld()
    {
        var integrator = new WishIntegrator();
        integrator.Add(1f, 0f, 0.02);
        Assert.Equal((sbyte)51, integrator.Peek(0.05).East); // 40 per cent of 127
        Assert.Equal((sbyte)0, integrator.Peek(0.05).North);
        integrator.Add(1f, 0f, 0.03);
        Assert.Equal((sbyte)127, integrator.Take(0.05).East);
        Assert.Equal((sbyte)0, integrator.Peek(0.05).East); // taken: starts again
    }

    [Fact]
    public void TheIntegratorNeverExceedsFullSpeedAndFollowsAChangingDirection()
    {
        var integrator = new WishIntegrator();
        integrator.Add(1f, 0f, 0.08); // a long frame
        (sbyte e, sbyte n) = integrator.Peek(0.05);
        Assert.Equal((sbyte)127, e);
        Assert.Equal((sbyte)0, n);
        integrator.Reset();
        integrator.Add(1f, 0f, 0.025);
        integrator.Add(0f, 1f, 0.025);
        (e, n) = integrator.Peek(0.05);
        Assert.Equal(e, n);
        Assert.InRange((int)e, 60, 66); // (0.5, 0.5) of 127 is 63.5, and its length 0.707 is below 1
        Assert.Equal((sbyte)0, new WishIntegrator().Peek(0.0).East);
    }
}
