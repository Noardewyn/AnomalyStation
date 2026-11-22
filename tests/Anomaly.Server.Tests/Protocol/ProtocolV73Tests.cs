using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 73 (DV360): a MapGrid cell's flaps bit is a valid flag and no face, and the own parameters' PassesFlaps round-trips.</summary>
public class ProtocolV73Tests
{
    [Fact]
    public void TheFlapsBitIsAValidCellFlagAndPassesFlapsRoundTrips()
    {
        Assert.True(ProtocolVersion.Current >= 73);
        Assert.Equal(CellFlags.Flaps, CellFlags.All & CellFlags.Flaps);
        Assert.Equal(0, CellFlags.Flaps & (CellFlags.Faces | CellFlags.Opaque | CellFlags.Starlight | CellFlags.ClimbWalkable));
        foreach (bool on in new[] { false, true })
        {
            var d = new Delta { Tick = 4, Own = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun with { PassesFlaps = on } } };
            d.CellFlagsChanged.Add(new CellFlagsChange(1, 1, 1, CellFlags.Flaps));
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
            Assert.Equal(on, back.Own.Params.PassesFlaps);
            Assert.Equal(CellFlags.Flaps, back.CellFlagsChanged[0].Flags);
        }
    }
}
