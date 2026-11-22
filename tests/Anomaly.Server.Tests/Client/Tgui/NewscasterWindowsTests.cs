using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Silicons SI6b (D326): the client's newscaster windows without the engine: the channel list with the wanted issue, the channel's
/// box and its buttons, the stories with comments and censorship, the channel modal's public and private choice, the wanted modal for
/// security, the bounty board's requests with their applicants and the new bounty, the newspaper's pages, and the Ntos windows.
/// </summary>
public class NewscasterWindowsTests
{
    private static (UiNode Tree, TguiContext Context, List<(string Action, string Params)> Acts) Render(Func<TguiContext, UiNode> render, string name, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (render(c), c, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static UiNode Find(UiNode tree, string key) => tree.Walk().FirstOrDefault(n => n.Key == key);

    private const string CasterJson = "{\"user\":{\"name\":\"Bob\",\"job\":\"Curator\",\"department\":\"SRV\"},\"admin_mode\":1,\"security_mode\":0,\"photo_data\":0,"
        + "\"creating_channel\":0,\"awaiting_approval\":0,\"creating_comment\":0,\"viewing_wanted\":0,\"making_wanted_issue\":1,\"criminal_name\":null,\"crime_description\":null,"
        + "\"wanted\":[{\"active\":1,\"criminal\":\"John Doe\",\"crime\":\"Theft.\",\"author\":\"Sec\",\"image\":null}],"
        + "\"viewing_channel\":3,\"paper\":0,\"channelName\":\"Bob News\",\"channelAuthor\":\"Bob\",\"channelDesc\":\"News &amp; views.\",\"channelLocked\":1,\"channelCensored\":0,\"receivingCrossSector\":0,"
        + "\"messages\":[{\"auth\":\"Bob\",\"body\":\"<i>Hello</i>\",\"time\":\"12:01\",\"channel_num\":3,\"censored_message\":0,\"censored_author\":0,\"ID\":7,\"photo\":null,"
        + "\"comments\":[{\"auth\":\"Al\",\"body\":\"Nice.\",\"time\":\"12:02\"}]},"
        + "{\"auth\":\"Bob\",\"body\":\"Bad\",\"time\":\"12:03\",\"channel_num\":3,\"censored_message\":1,\"censored_author\":1,\"ID\":8,\"photo\":null,\"comments\":null}],"
        + "\"requests\":[{\"owner\":\"Bob\",\"value\":150,\"description\":\"A donut.\",\"acc_number\":555},{\"owner\":\"Al\",\"value\":20,\"description\":\"A pen.\",\"acc_number\":666}],"
        + "\"applicants\":[{\"name\":\"Al\",\"request_id\":555,\"requestee_id\":666}],\"bountyValue\":1,\"bountyText\":\"\","
        + "\"channels\":[{\"name\":\"Station Announcements\",\"author\":\"SS13\",\"censored\":0,\"locked\":1,\"ID\":1},{\"name\":\"Bob News\",\"author\":\"Bob\",\"censored\":0,\"locked\":1,\"ID\":3},"
        + "{\"name\":\"Bad News\",\"author\":\"X\",\"censored\":1,\"locked\":0,\"ID\":4}]}";

    [Fact]
    public void TheNewscasterListsChannelsStoriesAndComments()
    {
        var local = new TguiLocalState();
        var (tree, context, acts) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson, local);
        Assert.Equal((575.0, 560.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Bob | Curator", ByKey(tree, "user").Walk().First(n => n.Kind == UiKind.Box && n.Text != null).Text);
        // the channel list: the wanted issue, the channels (a censored one marked), the new channel tab
        Assert.Equal(("skull-crossbones", "red"), (ByKey(tree, "wanted:0").Icon, ByKey(tree, "wanted:0").Color));
        Assert.True(ByKey(tree, "channel:Bob News").Selected);
        Assert.Equal(("ban", "red"), (ByKey(tree, "channel:Bad News").Icon, ByKey(tree, "channel:Bad News").Color));
        ByKey(tree, "channel:Station Announcements").OnClick();
        ByKey(tree, "create").OnClick();
        // the channel's box: its decoded description; the author may write in a locked channel; the curator's D-Notice
        Assert.Equal("Bob News", ByKey(tree, "channelBox").Title);
        Assert.Equal("News & views.", ByKey(tree, "descText").Text);
        Assert.False(ByKey(tree, "story").Disabled);
        Assert.True(ByKey(tree, "print").Disabled);
        ByKey(tree, "story").OnClick();
        ByKey(tree, "dnotice").OnClick();
        // the stories: the comment under its story, the censored one redacted
        UiNode story = ByKey(tree, "message:7");
        Assert.Equal("By: Bob at 12:01", story.Title);
        Assert.Equal("<i>Hello</i>", ByKey(story, "body").TextValue);
        Assert.Equal("By: Al at 12:02", ByKey(story, "commentAuthor").Text);
        UiNode censored = ByKey(tree, "message:8");
        Assert.Equal("By: [REDACTED]. D-Notice Notice .", censored.Title);
        Assert.NotNull(Find(censored, "censoredBody"));
        Assert.True(ByKey(censored, "comment").Disabled);
        ByKey(story, "comment").OnClick();
        ByKey(story, "censorStory").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("setChannel", "{\"channel\":1}"), ("startCreateChannel", null), ("createStory", "{\"current\":3}"),
            ("channelDNotice", "{\"secure\":true,\"channel\":3}"), ("startComment", "{\"messageID\":7}"), ("storyCensor", "{\"messageID\":7}"),
        }, acts);

        // a stranger cannot write in Bob's locked channel
        var (stranger, _, _) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson.Replace("\"name\":\"Bob\",\"job\"", "\"name\":\"Al\",\"job\""), local);
        Assert.True(ByKey(stranger, "story").Disabled);
    }

    [Fact]
    public void TheChannelAndWantedModalsAndTheLoadingScreen()
    {
        var local = new TguiLocalState();
        var (creating, _, acts) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson.Replace("\"creating_channel\":0", "\"creating_channel\":1"), local);
        Assert.True(ByKey(creating, "private").Selected);
        ByKey(creating, "name").OnCommit("Bob News");
        ByKey(creating, "public").OnClick();
        var (publicChoice, _, _) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson.Replace("\"creating_channel\":0", "\"creating_channel\":1"), local, acts);
        Assert.True(ByKey(publicChoice, "public").Selected);
        ByKey(publicChoice, "submit").OnClick();
        Assert.Equal(new (string, string)[] { ("setChannelName", "{\"channeltext\":\"Bob News\"}"), ("createChannel", "{\"cross_sector\":false,\"lockedmode\":false}") }, acts);

        var (waiting, _, _) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson.Replace("\"awaiting_approval\":0", "\"awaiting_approval\":1"));
        Assert.Equal("Awaiting Central Command approval...", ByKey(waiting, "label").Text);

        // the wanted modal: security edits it, others are told to call security
        string wantedJson = CasterJson.Replace("\"viewing_wanted\":0", "\"viewing_wanted\":1");
        var (security, _, securityActs) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", wantedJson.Replace("\"security_mode\":0", "\"security_mode\":1"));
        Assert.Equal("Active Wanted Issue:", ByKey(security, "headerText").Text);
        Assert.Equal("John Doe", ByKey(security, "criminal").Text);
        Assert.Equal("Posted by Sec", ByKey(security, "author").Text);
        ByKey(security, "setWanted").OnClick();
        Assert.Equal(("submitWantedIssue", null), Assert.Single(securityActs));
        var (crew, _, _) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", wantedJson);
        Assert.Null(Find(crew, "setWanted"));
        Assert.Equal("Please contact your local security officer if spotted.", ByKey(crew, "notice").Children[0].Text);
    }

    [Fact]
    public void TheBountyBoardAppliesPaysAndPostsInBothWindows()
    {
        var local = new TguiLocalState();
        var (tree, _, _) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson, local);
        ByKey(tree, "tab:bounty").OnClick();
        var (board, _, acts) = Render(PhysicalNewscaster.Render, "PhysicalNewscaster", CasterJson, local);
        UiNode own = ByKey(board, "request:Bob");
        Assert.Equal("150 cr", ByKey(own, "value").Text);
        Assert.True(ByKey(own, "apply").Disabled); // Bob's own request
        Assert.Equal("\"A donut.\"", ByKey(own, "description").Text);
        Assert.Equal("Al", ByKey(own, "applicant:666").Children[0].Text);
        ByKey(own, "pay").OnClick();
        ByKey(ByKey(board, "request:Al"), "apply").OnClick();
        ByKey(board, "bountyText").OnCommit("Fix it.");
        ByKey(board, "bountyValue").OnChange(200);
        ByKey(board, "submit").OnClick();
        ByKey(board, "reset").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("payApplicant", "{\"applicant\":666,\"request\":555}"), ("apply", "{\"request\":666}"), ("bountyText", "{\"bountytext\":\"Fix it.\"}"),
            ("bountyVal", "{\"bountyval\":200}"), ("createBounty", null), ("clear", null),
        }, acts);

        var (app, _, _) = Render(NtosBountyBoard.Render, "NtosBountyBoard", CasterJson.Replace("\"user\":", "\"PC_device_theme\":\"ntos\",\"user\":"));
        Assert.NotNull(Find(app, "request:Bob"));
        var (newsApp, newsContext, _) = Render(NtosNewscaster.Render, "NtosNewscaster", CasterJson.Replace("\"user\":", "\"PC_device_theme\":\"ntos\",\"user\":"));
        Assert.Equal((575.0, 700.0), (newsContext.WindowWidth, newsContext.WindowHeight));
        Assert.NotNull(Find(newsApp, "channel:Bob News"));
    }

    [Fact]
    public void TheNewspaperTurnsItsPages()
    {
        string json = "{\"current_page\":0,\"scribble_message\":null,\"wanted_criminal\":\"John Doe\",\"wanted_body\":\"Theft.\",\"wanted_photo\":null,"
            + "\"channels\":[{\"name\":\"Station Announcements\",\"page_number\":1},{\"name\":\"Bob News\",\"page_number\":2}],"
            + "\"channel_data\":[{\"channel_name\":null,\"author_name\":null,\"is_censored\":null,\"channel_messages\":[]}]}";
        var (intro, context, acts) = Render(Newspaper.Render, "Newspaper", json);
        Assert.Equal((300.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("The Griffon", ByKey(intro, "title").Text);
        Assert.Equal("Page 2: Bob News", ByKey(intro, "page:2").Text);
        Assert.Equal("Last Page: Important Security Announcement", ByKey(intro, "wanted").Text);
        Assert.True(ByKey(intro, "prev").Disabled);
        ByKey(intro, "next").OnClick();
        Assert.Equal(("next_page", null), Assert.Single(acts));

        string page = json.Replace("\"current_page\":0", "\"current_page\":1").Replace("\"scribble_message\":null", "\"scribble_message\":\"Doodle\"").Replace("\"channel_data\":[{\"channel_name\":null,\"author_name\":null,\"is_censored\":null,\"channel_messages\":[]}]",
            "\"channel_has_messages\":1,\"channel_data\":[{\"channel_name\":\"Station Announcements\",\"author_name\":\"SS13\",\"is_censored\":0,\"channel_messages\":[{\"message\":\"-Hello\",\"photo\":null,\"author\":\"CentCom\"}]}]");
        var (channel, _, _) = Render(Newspaper.Render, "Newspaper", page);
        Assert.Equal("Channel made by: SS13", ByKey(channel, "author").Text);
        Assert.Equal("-Hello", ByKey(channel, "text").TextValue);
        Assert.Equal("Written by: CentCom", ByKey(channel, "writer").Text);
        Assert.Equal("Doodle", ByKey(channel, "scribble").Text);

        var (ending, _, _) = Render(Newspaper.Render, "Newspaper", json.Replace("\"current_page\":0", "\"current_page\":3"));
        Assert.Equal("Criminal Name: John Doe", ByKey(ending, "criminal").Text);
        Assert.True(ByKey(ending, "next").Disabled);
    }
}
