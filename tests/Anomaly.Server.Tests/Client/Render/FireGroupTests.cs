using Anomaly.Client.Shared.Light;
using Xunit;

namespace Anomaly.Server.Tests.Client.Render;

public class FireGroupTests
{
    [Fact]
    public void ThreeByThreeTilesShareOneLamp()
    {
        Assert.Equal(ShadowedLightSelector.FireGroup(3, 6), ShadowedLightSelector.FireGroup(5, 8));
        Assert.NotEqual(ShadowedLightSelector.FireGroup(5, 8), ShadowedLightSelector.FireGroup(6, 8));
        Assert.NotEqual(ShadowedLightSelector.FireGroup(5, 8), ShadowedLightSelector.FireGroup(5, 9));
    }
}
