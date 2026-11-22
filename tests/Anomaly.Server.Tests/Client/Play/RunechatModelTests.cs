using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M9 slice A (DV160): the client half of tg's runechat, finish_image_generation's stacking, decay and fades.</summary>
public class RunechatModelTests
{
    [Fact]
    public void ALineFadesInHoldsAndFadesOutOverItsLifespan()
    {
        var model = new RunechatModel();
        RunechatLine line = model.Add(5, "hi", 50, 11f, 10.0);
        Assert.Equal(0f, line.Alpha(10.0));
        Assert.Equal(0.5f, line.Alpha(10.1), 3);
        Assert.Equal(1f, line.Alpha(12.0));
        Assert.Equal(1f, line.Alpha(14.3), 3);
        Assert.Equal(0f, line.Alpha(15.0));
        Assert.Equal(RunechatModel.StartingHeight, line.PixelZ(10.5));
        model.Prune(15.19);
        Assert.Single(model.Seen[5]);
        model.Prune(15.2); // lifespan plus the grace period
        Assert.False(model.Seen.ContainsKey(5));
    }

    [Fact]
    public void ANewLinePushesTheOlderUpAndShortensItsLife()
    {
        var model = new RunechatModel();
        RunechatLine old = model.Add(5, "one", 50, 11f, 0.0);
        RunechatLine fresh = model.Add(5, "two", 50, 22f, 1.0);
        // the old line rises by the new one's height over the spawn time
        Assert.Equal(RunechatModel.StartingHeight, old.PixelZ(1.0));
        Assert.Equal(RunechatModel.StartingHeight + 22f, old.PixelZ(1.2));
        Assert.Equal(RunechatModel.StartingHeight, fresh.PixelZ(1.2));
        // tg: remaining = (lifespan - EOL) * 0.7^1 * 0.9^(combined lines: 2 + 1)
        double remaining = (5.0 - 0.7) * 0.7 * Math.Pow(0.9, 3);
        Assert.Equal(remaining + 0.7, old.AnimateLifespan, 6);
        Assert.Equal(1f, old.Alpha(1.0 + remaining - 0.01), 3);
        Assert.Equal(0f, old.Alpha(1.0 + remaining + 0.7));
        Assert.True(fresh.Order > old.Order);
    }

    [Fact]
    public void LinesOverOtherSpeakersAreApart()
    {
        var model = new RunechatModel();
        RunechatLine a = model.Add(1, "a", 50, 11f, 0.0);
        model.Add(2, "b", 50, 11f, 0.5);
        Assert.Equal(5.0, a.AnimateLifespan);
        Assert.Equal(RunechatModel.StartingHeight, a.PixelZ(1.0));
    }

    [Fact]
    public void TheIconComesFromTheLinesClasses()
    {
        Assert.Equal("radio", RunechatModel.IconStateOf("<span class='maptext'><span style='color: #fff'><span class='center virtual-speaker'>x</span></span></span>"));
        Assert.Equal("emote", RunechatModel.IconStateOf("<span class='center emote italics'>waves</span>"));
        Assert.Null(RunechatModel.IconStateOf("<span class='center yell'>I emote virtual-speaker!!</span>"));
    }
}
