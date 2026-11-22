using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 49: the own capsule's motion parameters end with the speed and the move delay after a walk/run toggle, so the client
/// predicts the frames from its toggle on.
/// </summary>
public class ProtocolV49IntentTests
{
    [Fact]
    public void TheToggledSpeedRoundTripsInThePrivateBlock()
    {
        foreach (MotionParams p in new[]
        {
            MotionParams.DefaultRun,
            MotionParams.DefaultRun with { IntentTilesPerTick = 0.125f, IntentMoveDelayTicks = 8 },
            MotionParams.DefaultRun with { GravityMask = 3, GravityX = 7, GravityY = 9, OtherTilesPerTick = 0.4f, IntentTilesPerTick = 0.125f, IntentMoveDelayTicks = 8 },
        })
        {
            var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(7, 9, 1), Params = p };
            Assert.True(Codec.TryDecode(Codec.Encode(new Delta { Tick = 4, Own = own }), out Delta decoded, out ProtocolError error), error.ToString());
            Assert.Equal(p, decoded.Own.Params);
        }
    }

    [Fact]
    public void ANegativeToggledSpeedIsNotEncoded()
    {
        var own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { IntentTilesPerTick = -1f } };
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new Delta { Tick = 4, Own = own }));
    }
}
