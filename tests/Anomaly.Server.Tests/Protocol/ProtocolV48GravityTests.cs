using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 48: the own capsule's motion parameters end with the tiles around it where the mob would be weightless and its speed on
/// the other kind, so the client predicts stepping off a floor into space and back.
/// </summary>
public class ProtocolV48GravityTests
{
    [Fact]
    public void TheGravityMaskRoundTripsInThePrivateBlock()
    {
        foreach (MotionParams p in new[]
        {
            MotionParams.DefaultRun,
            MotionParams.DefaultRun with { GravityMask = MotionParams.GravityMaskAll >> 1, GravityX = 39, GravityY = 118, OtherTilesPerTick = 0.417f },
        })
        {
            var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(39, 118, 1), Params = p };
            Delta d = new() { Tick = 4, Own = own };
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
            Assert.Equal(p, decoded.Own.Params);
        }
    }

    [Fact]
    public void AMaskWiderThanSevenBySevenIsRefused()
    {
        var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { GravityMask = 1UL << 49, GravityX = 5, GravityY = 5 } };
        Assert.False(Codec.TryDecode(Codec.Encode(new Delta { Tick = 4, Own = own }), out Delta _, out ProtocolError _));
    }
}
