using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

public class StairRampTests
{
    [Theory]
    [InlineData(1, 5f, 4.5f, 0f)]   // north stairs: the south edge is the bottom
    [InlineData(1, 5f, 5f, 0.5f)]
    [InlineData(1, 5f, 5.5f, 1f)]   // the north edge, where the walker steps onto the level above
    [InlineData(2, 5f, 4.5f, 1f)]   // south stairs: the other way
    [InlineData(4, 5.25f, 5f, 0.75f)]
    [InlineData(8, 5.25f, 5f, 0.25f)]
    [InlineData(1, 5f, 7f, 1f)]     // clamped off the tile
    [InlineData(5, 5f, 5.5f, 0f)]   // a diagonal has no ramp
    public void ProgressRunsUpTheStairsDir(byte dir, float x, float y, float expected)
        => Assert.Equal(expected, StairRamp.Progress(dir, x, y, 5, 5), 3);
}
