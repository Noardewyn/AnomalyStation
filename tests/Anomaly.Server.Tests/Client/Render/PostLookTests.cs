using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>D298: the post-processing look's options have the owner's defaults, are saved, and are checked against their ranges and choices.</summary>
public class PostLookTests
{
    [Fact]
    public void TheLookHasTheOwnersDefaultsAndItsOptionsAreSaved()
    {
        var options = new PlayOptions();
        // the owner's defaults (2026-10-31)
        Assert.Equal((0, 100, 100, 0, 40, 40, 30, 50, 130, 1, 40), (options.Tonemap, options.SaturationPercent, options.ContrastPercent, options.SplitTonePercent,
            options.FogTiles, options.GrainPercent, options.VignettePercent, options.BloomPercent, options.BloomThresholdPercent, options.OutlineWidth, options.OutlinePercent));
        options.Tonemap = 2;
        options.SaturationPercent = 80;
        options.ContrastPercent = 115;
        options.SplitTonePercent = 40;
        options.FogTiles = 24;
        options.GrainPercent = 30;
        options.VignettePercent = 50;
        options.BloomPercent = 150;
        options.BloomThresholdPercent = 90;
        options.OutlineWidth = 2;
        options.OutlinePercent = 70;
        PlayOptions read = PlayOptions.Parse(options.ToText(), null);
        Assert.Equal((2, 80, 115, 40, 24, 30, 50, 150, 90, 2, 70), (read.Tonemap, read.SaturationPercent, read.ContrastPercent, read.SplitTonePercent,
            read.FogTiles, read.GrainPercent, read.VignettePercent, read.BloomPercent, read.BloomThresholdPercent, read.OutlineWidth, read.OutlinePercent));
    }

    [Fact]
    public void AValueOutOfItsRangeOrChoicesIsRefused()
    {
        var options = new PlayOptions();
        Assert.False(options.TrySet("contrast", "40", out _));
        Assert.True(options.TrySet("saturation", "200", out _));
        Assert.False(options.TrySet("saturation", "201", out _));
        Assert.False(options.TrySet("fog", "20", out _));
        Assert.False(options.TrySet("tonemap", "3", out _));
        Assert.False(options.TrySet("bloom_threshold", "100", out string problem));
        Assert.Equal("expected 90, 130, 180", problem);
        Assert.Equal(6, PostLook.FogStart(16)); // two fifths of the way
        Assert.True(options.TrySet("retro_lines", "270", out _)); // D297's removed retro look: an older file's line is ignored
    }
}
