using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 89: the own parameters' closing HaltsAtHandhold byte round-trips (tg's drift halting beside a wall).</summary>
public class ProtocolV89HaltTests
{
    [Fact]
    public void TheHandholdHaltFlagRoundTrips()
    {
        foreach (bool on in new[] { false, true })
        {
            var d = new Delta { Tick = 4, Own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { DriftX = 30, HaltsAtHandhold = on } } };
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
            Assert.Equal(on, back.Own.Params.HaltsAtHandhold);
        }
    }
}
