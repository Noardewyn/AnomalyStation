using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// D184: the scan readout panel's model. tg's boxed message (the health analyzer's blue box, the penlight's and stethoscope's plain box)
/// opens whole as plain lines, a table's cells apart (the chat keeps its first line); any other chat message leaves it as it is; the next readout replaces the shown
/// one; Esc closes it (and says so, else Esc does its other work); its key shows the last one again.
/// </summary>
public class ReadoutModelTests
{
    private const string Scan = "<div class='boxed_message blue_box'><span class='info'>Analyzing results for <b>Bob</b> (12:00:01):</span><br>"
        + "<span class='info ml-1'>Overall status: <b>88% healthy</b></span><br><span class='info ml-1'>Organ status:</span><font face='Verdana'>"
        + "<table class='ml-2'><tr><td style='width:8em;'><font color='#ff0000'><b>Organ:</b></font></td><td style='width:30em;'><font color='#ff0000'><b>Status</b></font></td></tr>"
        + "<tr><td><font color='#cc3333'>Liver:</font></td><td><font color='#ffcc33'>Mildly Damaged</font></td></tr></table></font></div>";

    [Fact]
    public void ABoxedMessageOpensWholeAndOtherChatLeavesThePanel()
    {
        var model = new ReadoutModel();
        Assert.False(model.Offer("<span class='notice'>You switch the health analyzer to check physical health.</span>"));
        Assert.False(model.Open);
        Assert.True(model.Offer(Scan));
        Assert.True(model.Open);
        Assert.Equal("Analyzing results for Bob (12:00:01):\nOverall status: 88% healthy\nOrgan status:\nOrgan: Status\nLiver: Mildly Damaged", model.Text);
        Assert.Equal("Analyzing results for Bob (12:00:01):", ReadoutModel.Headline(model.Text)); // the chat keeps the first line
        int version = model.Version;
        Assert.False(model.Offer("<span class='notice'>Bob analyzes Alice's vitals.</span>"));
        Assert.Equal(version, model.Version);

        Assert.True(model.Offer("<div class='boxed_message'><span class='info'>You place the stethoscope against Bob's chest:\n</span><span class='danger ml-1'>You don't hear a heartbeat!</span>\n</div>"));
        Assert.Contains("You don't hear a heartbeat!", model.Text);
        Assert.DoesNotContain("Analyzing", model.Text);

        Assert.False(model.Offer("<div class='boxed_message'><span class='infoplain'>You are the Research Director.</span></div>")); // the role greeting stays in the chat
        Assert.False(model.Offer("<div class='boxed_message purple_box'><span class='purple'><b>Tip of the round: </b>Hi.</span></div>")); // so does the tip of the round
    }

    [Fact]
    public void EscClosesItAndItsKeyShowsTheLastOneAgain()
    {
        var model = new ReadoutModel();
        Assert.False(model.Close()); // nothing open: Esc does its other work
        model.Offer(Scan);
        Assert.True(model.Close());
        Assert.False(model.Open);
        Assert.False(model.Close());
        model.Toggle();
        Assert.True(model.Open);
        Assert.Contains("Mildly Damaged", model.Text);
        model.Toggle();
        Assert.False(model.Open);
    }
}
