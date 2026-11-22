using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 50: with a gravity mask the motion parameters say whether the mob steers on a weightless tile with nothing to push off
/// and the speed of a drift from rest, and the parameters carry the first tick a drift's input block lets the held wish move.
/// </summary>
public class ProtocolV50SpaceStepTests
{
    [Fact]
    public void TheSpaceStepParametersRoundTripInThePrivateBlock()
    {
        foreach (MotionParams p in new[]
        {
            MotionParams.DefaultRun with { CanMove = false, UnblockTicks = 11 },
            MotionParams.DefaultRun with { GravityMask = 3, GravityX = 7, GravityY = 9, OtherTilesPerTick = 0.4f, InertiaTilesPerTick = 0.1f },
            MotionParams.DefaultRun with { GravityMask = 3, GravityX = 7, GravityY = 9, OtherTilesPerTick = 0.4f, OtherCanMove = true, InertiaTilesPerTick = 0.2f },
        })
        {
            var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(7, 9, 1), Params = p };
            Assert.True(Codec.TryDecode(Codec.Encode(new Delta { Tick = 4, Own = own }), out Delta decoded, out ProtocolError error), error.ToString());
            Assert.Equal(p, decoded.Own.Params);
        }
    }

    [Fact]
    public void TheSpaceStepParametersNeedAGravityMask()
    {
        var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { InertiaTilesPerTick = 0.1f } };
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new Delta { Tick = 4, Own = own }));
    }
}
