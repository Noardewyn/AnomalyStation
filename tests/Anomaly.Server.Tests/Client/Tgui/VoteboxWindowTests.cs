using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps ST5: the voting box's operator page (votingbox.dm:43, tg's /datum/browser "votebox", 300 by 300) as the client's Browser
/// window draws it: its heading and its links to the box's Topic (toggle_vote, tally and the rest) kept as tg's links.
/// </summary>
public class VoteboxWindowTests
{
    [Fact]
    public void TheVotingBoxPageIsDrawnWithItsLinks()
    {
        const string page = "<html><head><title>Voting Box</title></head><body><div class='uiContent'>" +
            "<h1>Best lunch</h1>Voting: <a href='byond://?src=[0x3001];act=toggle_vote'>Maintenance Mode</a><br>" +
            "Tally votes: <a href='byond://?src=[0x3001];act=tally'>Tally</a><br></div></body></html>";
        string json = "{\"html\":[" + System.Text.Json.JsonSerializer.Serialize(page) + "],\"width\":300,\"height\":300}";
        var context = new TguiContext(1, Limits.BrowseUiInterface, "Voting Box", 2, UiValue.FromJson(json), (_, _) => { }, new TguiLocalState())
        {
            Topic = _ => { },
        };
        UiNode drawn = Assert.Single(Browser.Render(context).Walk(), n => n.Kind == UiKind.Html);
        Assert.Equal((300.0, 300.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("act=toggle_vote", drawn.TextValue);
        Assert.Contains("act=tally", drawn.TextValue);
        Assert.Contains("Best lunch", drawn.TextValue);
        Assert.DoesNotContain("<title>", drawn.TextValue);
        Assert.NotNull(drawn.OnInput);
    }
}
