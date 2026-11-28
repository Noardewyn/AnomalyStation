using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>ui-overhaul.md section 2: the overhaul's tokens (the wireframes' section 7 palette, the Tab overlay's shares, the size unit).</summary>
public class UiTokensTests
{
    [Theory]
    [InlineData(UiTokens.Common, "8FD694")]
    [InlineData(UiTokens.Security, "FF4D4D")]
    [InlineData(UiTokens.Engineering, "FFA62B")]
    [InlineData(UiTokens.Medical, "38D9F5")]
    [InlineData(UiTokens.Science, "C77DFF")]
    [InlineData(UiTokens.Command, "FFE156")]
    [InlineData(UiTokens.LocalText, "FFFFFF")]
    [InlineData(UiTokens.LocalBack, "1E242B")]
    public void The_palette_is_the_wireframes_hex_values_and_opaque(uint rgba, string hex)
    {
        Assert.Equal(hex, (rgba >> 8).ToString("X6"));
        Assert.Equal(0xFFu, rgba & 0xFF);
    }

    [Fact]
    public void The_chip_is_the_local_pills_slate_with_a_white_rim()
    {
        Assert.Equal(UiTokens.LocalBack >> 8, UiTokens.ChipBack >> 8);
        Assert.Equal(0xFFFFFFu, UiTokens.ChipRim >> 8);
    }

    [Fact]
    public void The_overlay_shares_are_ordered_inside_the_screen()
    {
        Assert.Equal(0.28f, UiTokens.OverlayLeftShare);
        Assert.Equal(0.64f, UiTokens.OverlayRightShare);
        Assert.True(0f < UiTokens.OverlayLeftShare && UiTokens.OverlayLeftShare < UiTokens.OverlayRightShare && UiTokens.OverlayRightShare < 1f);
    }

    [Theory]
    [InlineData(1920f, 1080f, 1f, 1f)]
    [InlineData(1920f, 1080f, 2f, 2f)]
    [InlineData(1920f, 1080f, 0.5f, 0.5f)]
    [InlineData(1280f, 720f, 1f, 2f / 3f)]
    [InlineData(3840f, 2160f, 1f, 1f)] // a higher screen does not grow it, as the HUD's text does not
    public void The_unit_is_the_hud_scale_at_1080_lines(float width, float height, float scale, float unit)
    {
        Assert.Equal(unit, UiTokens.Unit(new HudMetrics(width, height, scale)), 4);
    }
}
