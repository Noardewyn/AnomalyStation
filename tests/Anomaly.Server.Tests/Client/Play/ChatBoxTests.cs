using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// D184: tg's boxed message (the health analyzer's blue box, the penlight's and stethoscope's plain box) is drawn whole in the chat in
/// its box's colours; any other chat message has no box, and the chat feed keeps a boxed message whole past its line limit.
/// </summary>
public class ChatBoxTests
{
    private const string Scan = "<div class='boxed_message blue_box'><span class='info'>Analyzing results for <b>Bob</b> (12:00:01):</span><br>"
        + "<span class='info ml-1'>Overall status: <b>88% healthy</b></span><br><span class='info ml-1'>Organ status:</span><font face='Verdana'>"
        + "<table class='ml-2'><tr><td style='width:8em;'><font color='#ff0000'><b>Organ:</b></font></td><td style='width:30em;'><font color='#ff0000'><b>Status</b></font></td></tr>"
        + "<tr><td><font color='#cc3333'>Liver:</font></td><td><font color='#ffcc33'>Mildly Damaged</font></td></tr></table></font></div>";

    [Fact]
    public void ABoxedMessageHasItsBoxAndOtherChatHasNone()
    {
        Assert.Equal(ChatBox.Blue, ChatBox.Of(Scan));
        Assert.Equal(ChatBox.Plain, ChatBox.Of("<div class=\"boxed_message\"><span class='info'>You place the stethoscope against Bob's chest:</span></div>"));
        Assert.Equal(ChatBox.Purple, ChatBox.Of("<div class='boxed_message purple_box'><span class='purple'><b>Tip of the round: </b>Hi.</span></div>"));
        Assert.Equal(ChatBox.Red, ChatBox.Of("<div class='boxed_message red_box'>x</div>"));
        Assert.Null(ChatBox.Of("<span class='notice'>You switch the health analyzer to check physical health.</span>"));
        Assert.Null(ChatBox.Of(null));
        Assert.Equal(0x40u, ChatBox.Plain.Border & 0xFF); // tg's hsla(220, 40%, 75%, 0.25)
        Assert.Equal(0xFFu, ChatBox.Plain.Bg & 0xFF);
    }

    [Fact]
    public void AnnouncementsAndAdminPmsAreBoxedToo()
    {
        // tg's fieldset_block of an admin PM: the box classes after "fieldset"
        Assert.Equal(ChatBox.Red, ChatBox.Of("<fieldset class='fieldset boxed_message red_box'><legend class='fieldset_legend'>Administrator private message</legend>Hi</fieldset>"));
        ChatBox? alert = ChatBox.Of("<div class='chat_alert_red'><span class='major_announcement_title'>Alert</span></div>");
        Assert.NotNull(alert);
        Assert.True(alert.Value.Alert);
        Assert.Equal(ChatStyles.Hsl(0, 100, 12.9), alert.Value.Bg); // tg's red stripe
        Assert.Equal(ChatStyles.Hsl(0, 100, 16.1), alert.Value.Border); // and its alternate
        Assert.Equal(ChatBox.OocAlert, ChatBox.Of("<div class='ooc_alert'>Server Admin Announcement</div>"));
        Assert.Null(ChatBox.Of("<div class='chat_alert_nonsense'>x</div>"));
        // the player's own "PM to-Admins" is a plain notice in tg; typed as an admin PM it gets the admin PM's red box (DV598)
        Assert.Equal(ChatBox.Red, ChatBox.Of("<span class='notice'>PM to-<b>Admins</b>: help</span>", ChatLog.TypeAdminPm));
        Assert.Null(ChatBox.Of("<span class='notice'>hello</span>", ChatLog.TypeInfo));
    }

    [Fact]
    public void AnOocLineIsLargerAndBold()
    {
        var spans = new List<ChatSpan>();
        string plain = HtmlText.ToPlain("<span class='ooc'><span class='prefix'>OOC:</span> <EM>bob:</EM> <span class='message linkify'>hi</span></span>", null, spans);
        Assert.EndsWith("hi", plain);
        Assert.All(spans, s => Assert.True(s.Style.Bold && s.Style.Scale == ChatStyles.OocScale));
        Assert.Equal(1f, ChatStyles.Apply(ChatStyles.Base, "notice").Scale);
    }

    [Fact]
    public void TheFeedKeepsABoxedMessageWhole()
    {
        string plain = HtmlText.ToPlain(Scan) + "\n" + new string('x', ChatFeed.MaxChars * 2);
        var feed = new ChatFeed();
        feed.Add(plain, 0, box: ChatBox.Blue);
        feed.Add(plain, 0);
        List<ChatLine> lines = feed.Visible(0);
        Assert.Equal(plain, lines[0].Text);
        Assert.Equal(ChatBox.Blue, lines[0].Box);
        Assert.Equal(ChatFeed.MaxChars, lines[1].Text.Length);
        Assert.Null(lines[1].Box);
    }
}
