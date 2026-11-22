using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M9 slice B (D256): the emote wheel on J: held and released over a segment, tapped and clicked, the default eight and "More...".</summary>
public class EmoteWheelTests
{
    [Fact]
    public void TheWheelHasTheEightAndMore()
    {
        List<RadialEntry> entries = EmoteWheel.Entries();
        Assert.Equal(new[] { "Wave", "Nod", "Laugh", "Shrug", "Clap", "Salute", "Sigh", "Scream", EmoteWheel.MoreLabel }, entries.Select(e => e.Label));
        Assert.Equal("salute", EmoteWheel.KeyOf(entries[5]));
        Assert.Equal(RadialKind.EmotePanel, entries[^1].Kind);
        Assert.Null(EmoteWheel.KeyOf(entries[^1]));
    }

    [Fact]
    public void HeldAndReleasedOverASegmentItPicksIt()
    {
        var wheel = new EmoteWheel();
        wheel.Press(0);
        wheel.Update(0.1);
        Assert.False(wheel.IsOpen);
        wheel.Update(0.3);
        Assert.True(wheel.IsOpen);
        wheel.Model.Motion(60, 0); // to the right: the third of nine, clockwise from the top
        RadialEntry? pick = wheel.Release(0.6);
        Assert.Equal("Laugh", pick?.Label);
        Assert.False(wheel.IsOpen);

        wheel.Press(1);
        wheel.Update(1.3);
        Assert.Null(wheel.Release(1.4)); // released in the centre: nothing
    }

    [Fact]
    public void TappedItWaitsForAClickOrEsc()
    {
        var wheel = new EmoteWheel();
        wheel.Press(0);
        Assert.Null(wheel.Release(0.1));
        Assert.True(wheel.IsOpen && wheel.Sticky);
        wheel.Press(0.5); // a press while it waits is the scene's close, not a new hold
        Assert.True(wheel.Sticky);
        wheel.Model.Motion(0, -60); // up: the first
        Assert.Equal("Wave", wheel.Click()?.Label);
        Assert.False(wheel.IsOpen);

        wheel.Press(2);
        wheel.Release(2.1);
        wheel.Close();
        Assert.False(wheel.IsOpen || wheel.Sticky);
        Assert.Null(wheel.Click());
    }
}
