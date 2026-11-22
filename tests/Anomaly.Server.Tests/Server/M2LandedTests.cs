namespace Anomaly.Server.Tests.Server;

/// <summary>The probes of <see cref="M2Landed"/> answer without throwing, whatever has landed.</summary>
public class M2LandedTests
{
    [Fact]
    public void TheProbesAnswer()
    {
        _ = M2Landed.Protocol;
        _ = M2Landed.Motion;
        _ = M2Landed.Locomotion;
        _ = M2Landed.Click;
        _ = M2Landed.Medical;
        Assert.Null(M2Landed.Missing(M2Needs.None));
        Assert.Equal(M2Landed.Has(M2Needs.All), M2Landed.Missing(M2Needs.All) == null);
    }

    [M2Fact(M2Needs.All)]
    public void EverythingLandedRunsThis()
    {
        Assert.True(M2Landed.Has(M2Needs.All));
    }
}
