using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>D289: the T-ray images show for 3 s with a fade in and out, then are gone for 1.5 s.</summary>
public class TRayPulseTests
{
    [Theory]
    [InlineData(0.0, 0f)]
    [InlineData(0.2, 0.5f)]
    [InlineData(1.5, 1f)]
    [InlineData(2.8, 0.5f)]
    [InlineData(3.0, 0f)]
    [InlineData(4.4, 0f)]
    [InlineData(4.7, 0.5f)]
    public void TheOpacityFadesInHoldsFadesOutAndRestsOverItsPeriod(double seconds, float opacity)
        => Assert.Equal(opacity, TRayPulse.Opacity(seconds), 3);
}
