using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// M9 (the owner, 2026-10-23): tg's chat lines in tg's dark chat colours in the feed: the style sheet (chat-dark.scss) and the reading of tg's
/// spans, fonts, inline styles and tags into runs of the plain text.
/// </summary>
public class ChatStylesTests
{
    private static (string Text, List<ChatSpan> Spans) Read(string html, List<ChatLink> links = null)
    {
        var spans = new List<ChatSpan>();
        string text = HtmlText.ToPlain(html, links, spans);
        return (text, spans);
    }

    private static ChatStyle At(List<ChatSpan> spans, int index) => spans.Single(s => index >= s.Start && index < s.Start + s.Length).Style;

    [Fact]
    public void TheSheetIsTgsDarkChat()
    {
        Assert.Equal(0xB3CCE6FFu, ChatStyles.Base.Rgba); // hsl(210, 50%, 80%)
        Assert.Equal(new ChatStyle(ChatStyles.Hsl(0, 73.6, 44.5), false, true), ChatStyles.Apply(ChatStyles.Base, "warning"));
        Assert.Equal(new ChatStyle(ChatStyles.Hsl(227, 87.7, 68), false, false), ChatStyles.Apply(ChatStyles.Base, "notice"));
        Assert.Equal(new ChatStyle(ChatStyles.Hsl(47.9, 100, 40), true, false, ChatStyles.OocScale), ChatStyles.Apply(ChatStyles.Base, "ooc")); // larger than tg (DV598)
        Assert.Equal(ChatStyles.Hsl(202, 83.6, 64.1), ChatStyles.Apply(ChatStyles.Base, "medradio").Rgba);
        Assert.Equal(0xFF0000FFu, ChatStyles.Hsl(0, 100, 50));
        // two classes: the later rule of the sheet wins (".bolddanger" comes after ".danger")
        Assert.Equal(new ChatStyle(ChatStyles.Hsl(0, 73.6, 44.5), true, false), ChatStyles.Apply(ChatStyles.Base, "bolddanger danger"));
        Assert.Equal(ChatStyles.Base, ChatStyles.Apply(ChatStyles.Base, "say message unknownclass"));
    }

    [Fact]
    public void ASaidLineIsTheChatColourWithTheNameBold()
    {
        (string text, List<ChatSpan> spans) = Read("<span class='game say'><span class='name'>Bob</span> <span class='message'>says, \"hi\"</span></span>");
        Assert.Equal("Bob says, \"hi\"", text);
        Assert.Equal(ChatStyles.Base with { Bold = true }, At(spans, 0));
        Assert.Equal(ChatStyles.Base, At(spans, 5));
        Assert.Equal(text.Length, spans.Sum(s => s.Length)); // the runs cover the text
    }

    [Fact]
    public void WarningsNoticesFontsAndInlineColoursEachKeepTheirRun()
    {
        (string text, List<ChatSpan> spans) = Read("<span class='warning'>You can't!</span> <span class='notice'>Ok.</span> <font color='#ff0000'>Red</font> <span style='color: #00FF00; font-weight: bold'>Green</span> <em>Em</em> plain");
        Assert.Equal("You can't! Ok. Red Green Em plain", text);
        Assert.Equal(ChatStyles.Apply(ChatStyles.Base, "warning"), At(spans, 0));
        Assert.Equal(ChatStyles.Apply(ChatStyles.Base, "notice"), At(spans, text.IndexOf("Ok", StringComparison.Ordinal)));
        Assert.Equal(0xFF0000FFu, At(spans, text.IndexOf("Red", StringComparison.Ordinal)).Rgba);
        Assert.Equal(new ChatStyle(0x00FF00FFu, true, false), At(spans, text.IndexOf("Green", StringComparison.Ordinal)));
        Assert.Equal(ChatStyles.Base with { Bold = true }, At(spans, text.IndexOf("Em", StringComparison.Ordinal)));
        Assert.Equal(ChatStyles.Base, At(spans, text.IndexOf("plain", StringComparison.Ordinal)));
    }

    [Fact]
    public void LinksBreaksAndTrimmingKeepTheStyles()
    {
        var links = new List<ChatLink>();
        (string text, List<ChatSpan> spans) = Read("<span class='danger'>  Hit!<br><br><br>  </span><span class='radio'>[Common] <a href='byond://?src=1'>Bob</a></span>", links);
        Assert.Equal("Hit!\n[Common] Bob", text); // consecutive breaks are one
        Assert.Equal(ChatStyles.Apply(ChatStyles.Base, "danger"), At(spans, 0));
        Assert.Equal(ChatStyles.Apply(ChatStyles.Base, "radio"), At(spans, text.IndexOf("[Common]", StringComparison.Ordinal)));
        Assert.Equal(new ChatLink(text.IndexOf("Bob", StringComparison.Ordinal), 3, "byond://?src=1"), Assert.Single(links));
        Assert.Equal(text.Length, spans.Sum(s => s.Length));
    }

    [Fact]
    public void TheFeedKeepsALinesRuns()
    {
        var feed = new ChatFeed();
        (string text, List<ChatSpan> spans) = Read("<span class='notice'>You wave.</span>");
        feed.Add(text, 0, null, spans);
        ChatLine line = Assert.Single(feed.Visible(1));
        Assert.Equal(ChatStyles.Apply(ChatStyles.Base, "notice"), line.StyleAt(3));
        Assert.Equal(ChatStyles.Base, new ChatLine("x", 1).StyleAt(0));
    }

    [Fact]
    public void AnAnnouncementIsTgsStripedBox()
    {
        // priority_announce's markup (__HELPERS/priority_announce.dm): the box, the header (title and subheader) and the text
        (string text, List<ChatSpan> spans) = Read("<div class='chat_alert_default'><span class='announcement_header'><span class='major_announcement_title'>Central Command Update</span>"
            + "<span class='subheader_announcement_text'>Test Alert</span></span><span class='major_announcement_text'>All clear.</span></div>");
        Assert.Equal("Central Command Update\nTest Alert\nAll clear.", text); // the parts are blocks
        ChatStyle title = At(spans, 0), sub = At(spans, text.IndexOf("Test", StringComparison.Ordinal)), body = At(spans, text.IndexOf("All", StringComparison.Ordinal));
        Assert.Equal(1.75f, title.Scale);
        Assert.Equal(ChatStyles.Hsl(192.4, 100, 60), title.Rgba); // the default box's header colour
        Assert.Equal(ChatStyles.Hsl(336.1, 100, 66.1), sub.Rgba);
        Assert.True(sub.Bold && body.Bold);
        Assert.Equal(ChatStyles.Hsl(198.6, 100, 6.4), body.Bg); // the stripes 5 % darker
        Assert.Equal("red", ChatStyles.Apply(ChatStyles.Base, "chat_alert_red").Alert);
        Assert.Equal(ChatStyles.Hsl(0, 0, 12.5), ChatStyles.Apply(ChatStyles.Base, "minor_announcement_text").Bg); // outside a box
    }
}
