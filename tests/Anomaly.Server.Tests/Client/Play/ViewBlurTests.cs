using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// Protocol version 29 (the eye blur, protocol.md section 38.3): the deviation of the view's blur for the game plane's filters. A blur
/// of size s is a gaussian of deviation s / 2 BYOND pixels (D156), scaled from BYOND's 480-pixel-high view to the client's.
/// </summary>
public class ViewBlurTests
{
    private static FilterEntry Blur(float size) => new() { Kind = FilterKind.Blur, Size = size };

    [Fact]
    public void TgsEyeBlurSizesBecomeScreenPixelsOnA1080PixelView()
    {
        // tg's eye blur: 0.6 to 3 BYOND pixels (screen_blur.dm); 1080 / 480 = 2.25 screen pixels a BYOND pixel
        Assert.Equal(0.3f * 2.25f, ViewBlur.Sigma(new[] { Blur(0.6f) }, 1080), 4);
        Assert.Equal(1.5f * 2.25f, ViewBlur.Sigma(new[] { Blur(3f) }, 1080), 4);
        Assert.Equal(1.5f, ViewBlur.Sigma(new[] { Blur(3f) }, 480), 4);
    }

    [Fact]
    public void TwoBlursAddAsVariancesAndOtherFiltersAreNotBlurs()
    {
        Assert.Equal(MathF.Sqrt(0.5f * 0.5f + 1.5f * 1.5f), ViewBlur.Sigma(new[] { Blur(1f), Blur(3f) }, 480), 4);
        Assert.Equal(0f, ViewBlur.Sigma(new[] { new FilterEntry { Kind = FilterKind.Color } }, 1080));
        Assert.Equal(0f, ViewBlur.Sigma(Array.Empty<FilterEntry>(), 1080));
        Assert.Equal(0f, ViewBlur.Sigma(new[] { Blur(1f) }, 0));
        Assert.Equal(0f, ViewBlur.Sigma(null, 1080));
    }
}
