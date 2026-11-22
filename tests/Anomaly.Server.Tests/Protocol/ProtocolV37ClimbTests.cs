using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 37, M8z slice C (D239): a MapGrid cell's climb-walkable bit and the own capsule's OnClimbable flag (the motion byte's
/// bit 128), so the client predicts a climber walking over tables.
/// </summary>
public class ProtocolV37ClimbTests
{
    [Fact]
    public void TheVersionIsAtLeast37()
    {
        Assert.True(ProtocolVersion.Current >= 37); // BuildInfoTests pins the current one
    }

    [Fact]
    public void TheClimbWalkableBitIsAValidCellFlagAndNoFace()
    {
        Assert.Equal(64, CellFlags.ClimbWalkable);
        Assert.Equal(CellFlags.ClimbWalkable, CellFlags.All & CellFlags.ClimbWalkable);
        Assert.Equal(0, CellFlags.ClimbWalkable & (CellFlags.Faces | CellFlags.Opaque | CellFlags.Starlight));
        Assert.Equal(TileEdges.None, CellFlags.FacesOf(CellFlags.ClimbWalkable));
    }

    [Fact]
    public void TheOnClimbableFlagRoundTripsInThePrivateBlock()
    {
        foreach (bool on in new[] { false, true })
        {
            var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { OnClimbable = on, NoClip = on } };
            var buffer = new byte[256];
            int length = Codec.EncodeOwnBlock(own, buffer);
            Delta d = new() { Tick = 4 };
            d.Own = own;
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
            Assert.Equal(own.Params, decoded.Own.Params);
            Assert.InRange(length, 20, 47); // with version 66's velocity and movement feel (DV263), version 73's flaps byte (DV360) and version 89's handhold halt byte
        }
    }
}
