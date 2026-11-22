using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Newscaster.jsx, PhysicalNewscaster.tsx and
// NtosNewscaster.tsx (silicons SI6b, D326: the newscaster and its app): the Newscaster and Bounty Board tabs, the channel list with the
// wanted issue and a new channel, the channel's box with its description, story, photo, D-Notice and newspaper buttons, its stories
// with their comments and censorship, the channel and comment modals, the wanted issue modal with security's editing and the loading
// screen of a cross-sector channel. The stories' HTML is drawn as tg's processed text (UiKind.Html); the photos' images are not
// shown (the server renders no icons, D104). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Newscaster
{
    private const string TabKey = "tab_main";
    private const string LockedKey = "newscasterLockedmode";
    private const string CrossSectorKey = "newscasterCrossSector";
    private const int NEWSCASTER_SCREEN = 1;
    private const int BOUNTYBOARD_SCREEN = 2;

    private const string CENSOR_MESSAGE = "This channel has been deemed as threatening to   the welfare of the station, and marked with a Nanotrasen D-Notice.";

    // tg: Newscaster.jsx:34
    public static UiNode Content(TguiContext c)
    {
        int screenmode = c.UseState(TabKey, NEWSCASTER_SCREEN);
        var root = new UiNode(UiKind.Fragment) { Key = "newscaster" };
        UiNode creation = ChannelCreation(c);
        if (creation != null)
            root.With(creation);
        UiNode comment = CommentCreation(c);
        if (comment != null)
            root.With(comment);

        var stack = new UiNode(UiKind.Stack) { Key = "main", Fill = true, Vertical = true };
        UiNode wanted = WantedScreen(c);
        if (wanted != null)
            stack.With(wanted);
        stack.With(
            new UiNode(UiKind.StackItem) { Key = "tabsItem" }.With(new UiNode(UiKind.Tabs) { Key = "tabs", Fluid = true, TextAlign = "center" }.With(
                new UiNode(UiKind.Tab) { Key = "tab:newscaster", Color = "Green", Selected = screenmode == NEWSCASTER_SCREEN, Text = "Newscaster", OnClick = () => c.SetState(TabKey, NEWSCASTER_SCREEN) },
                new UiNode(UiKind.Tab) { Key = "tab:bounty", Selected = screenmode == BOUNTYBOARD_SCREEN, Text = "Bounty Board", OnClick = () => c.SetState(TabKey, BOUNTYBOARD_SCREEN) })),
            new UiNode(UiKind.StackItem) { Key = "screen", Grow = 1 }.With(screenmode == BOUNTYBOARD_SCREEN ? BountyBoard.Content(c) : NewscasterContent(c)));
        return root.With(stack);
    }

    /** The modal menu that contains the prompts to making new channels. */
    // tg: Newscaster.jsx:77
    private static UiNode ChannelCreation(TguiContext c)
    {
        UiValue data = c.Data;
        bool lockedmode = c.UseState(LockedKey, true);
        bool crossSector = c.UseState(CrossSectorKey, false);
        if (data.Truthy("awaiting_approval"))
            return LoadingScreen("Awaiting Central Command approval...");
        if (!data.Truthy("creating_channel"))
            return null;

        return new UiNode(UiKind.Modal) { Key = "channelCreation", TextAlign = "center" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "nameItem" }.With(
                new UiNode(UiKind.Box) { Key = "nameLabel" }.With(
                    new UiNode(UiKind.Box) { Key = "nameText", Text = "Enter channel name here:" },
                    new UiNode(UiKind.Button) { Key = "cancel", Color = "red", Icon = "times", OnClick = () => c.Act("cancelCreation") }),
                new UiNode(UiKind.TextArea)
                {
                    Key = "name",
                    Height = "40px",
                    Width = "240px",
                    Background = "black",
                    MaxLength = 42,
                    Tooltip = "Channel Name",
                    OnCommit = value => c.Act("setChannelName", ("channeltext", value)),
                }),
            new UiNode(UiKind.StackItem) { Key = "descItem" }.With(
                new UiNode(UiKind.Box) { Key = "descLabel", Text = "Enter channel description here:" },
                new UiNode(UiKind.TextArea)
                {
                    Key = "desc",
                    Height = "150px",
                    Width = "240px",
                    Background = "black",
                    MaxLength = 512,
                    Tooltip = "Channel Description",
                    OnCommit = value => c.Act("setChannelDesc", ("channeldesc", value)),
                }),
            new UiNode(UiKind.StackItem) { Key = "privacyItem" }.With(new UiNode(UiKind.Section) { Key = "privacy" }.With(
                new UiNode(UiKind.Box) { Key = "privacyText", Text = "Set Channel as Public or Private" },
                new UiNode(UiKind.Box) { Key = "privacyButtons" }.With(
                    new UiNode(UiKind.Button) { Key = "public", Selected = !lockedmode, Disabled = crossSector, Text = "Public", OnClick = () => c.SetState(LockedKey, false) },
                    new UiNode(UiKind.Button) { Key = "private", Selected = lockedmode, Disabled = crossSector, Text = "Private", OnClick = () => c.SetState(LockedKey, true) }))),
            new UiNode(UiKind.StackItem) { Key = "crossItem" }.With(new UiNode(UiKind.Button)
            {
                Key = "crossSector",
                Fluid = true,
                Checkbox = crossSector,
                Tooltip = "Cross-sector newscaster messaging will require Central Command approval for each article. Cross-sector channels are automatically locked.",
                Text = "Make cross-sector?",
                OnClick = () =>
                {
                    c.SetState(CrossSectorKey, !crossSector);
                    c.SetState(LockedKey, true);
                },
            }),
            new UiNode(UiKind.StackItem) { Key = "submitItem" }.With(new UiNode(UiKind.Button)
            {
                Key = "submit",
                Text = "Submit Channel",
                OnClick = () => c.Act("createChannel", ("cross_sector", crossSector), ("lockedmode", lockedmode)),
            })));
    }

    /** The modal menu that contains the prompts to making new comments. */
    // tg: Newscaster.jsx:193
    private static UiNode CommentCreation(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("creating_comment"))
            return null;
        UiValue viewingMessage = data.Get("viewing_message");
        return new UiNode(UiKind.Modal) { Key = "commentCreation", TextAlign = "center" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "commentItem" }.With(
                new UiNode(UiKind.Box) { Key = "commentLabel" }.With(
                    new UiNode(UiKind.Box) { Key = "commentText", Text = "Enter comment:" },
                    new UiNode(UiKind.Button) { Key = "cancel", Color = "red", Icon = "times", OnClick = () => c.Act("cancelCreation") }),
                new UiNode(UiKind.TextArea)
                {
                    Key = "comment",
                    Height = "120px",
                    Width = "240px",
                    Background = "black",
                    MaxLength = 512,
                    Tooltip = "Channel Name",
                    OnCommit = value => c.Act("setCommentBody", ("commenttext", value)),
                }),
            new UiNode(UiKind.StackItem) { Key = "submitItem" }.With(new UiNode(UiKind.Button)
            {
                Key = "submit",
                Text = "Submit Comment",
                OnClick = () => c.Act("createComment", ("messageID", viewingMessage)),
            })));
    }

    // tg: Newscaster.jsx:247
    private static UiNode WantedScreen(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("viewing_wanted"))
            return null;
        bool securityMode = data.Truthy("security_mode");
        IReadOnlyList<UiValue> wanted = data.Get("wanted")?.Items ?? Array.Empty<UiValue>();
        var modal = new UiNode(UiKind.Modal) { Key = "wanted", TextAlign = "center", Width = 25 };
        int index = 0;
        foreach (UiValue activeWanted in wanted)
        {
            bool active = activeWanted.Truthy("active");
            var issue = new UiNode(UiKind.Stack) { Key = "issue:" + index++, Vertical = true }.With(new UiNode(UiKind.StackItem) { Key = "issueItem" }.With(
                new UiNode(UiKind.Box) { Key = "header", Bold = true, Color = "red" }.With(
                    new UiNode(UiKind.Box) { Key = "headerText", Bold = true, Color = "red", Text = active ? "Active Wanted Issue:" : "Dismissed Wanted Issue:" },
                    new UiNode(UiKind.Button) { Key = "close", Color = "red", Icon = "times", OnClick = () => c.Act("cancelCreation") })));
            if (activeWanted.Truthy("criminal"))
            {
                // tg: <Image src={activeWanted.image}>: the wanted photo (no picture has an image here, D104)
                ((UiNode)issue.Children[0]).With(
                    new UiNode(UiKind.Section) { Key = "criminalSection" }.With(
                        new UiNode(UiKind.Box) { Key = "criminal", Bold = true, Text = activeWanted.Str("criminal") },
                        new UiNode(UiKind.Box) { Key = "crime", Color = "label", Text = activeWanted.Str("crime") }),
                    new UiNode(UiKind.Box) { Key = "author", Color = "label", Text = "Posted by " + (activeWanted.Truthy("author") ? activeWanted.Str("author") : "N/A") });
            }
            modal.With(issue, new UiNode(UiKind.Divider) { Key = "divider:" + index });
        }

        if (securityMode)
        {
            string criminalName = data.Str("criminal_name");
            string crimeDescription = data.Str("crime_description");
            bool photoData = data.Truthy("photo_data");
            modal.With(
                new UiNode(UiKind.LabeledList) { Key = "wantedFields" }.With(
                    new UiNode(UiKind.LabeledItem) { Key = "criminalNameItem", Label = "Criminal Name" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "criminalName",
                        Icon = "pen",
                        Text = string.IsNullOrEmpty(criminalName) ? " N/A" : criminalName,
                        OnClick = () => c.Act("setCriminalName"),
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "crimeItem", Label = "Criminal Activity" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "crime",
                        Icon = "pen",
                        Text = string.IsNullOrEmpty(crimeDescription) ? " N/A" : crimeDescription,
                        OnClick = () => c.Act("setCrimeData"),
                    })),
                new UiNode(UiKind.Section) { Key = "wantedButtons" }.With(
                    new UiNode(UiKind.Button) { Key = "photo", Icon = "camera", Selected = photoData, Text = photoData ? "Remove photo" : "Attach photo", OnClick = () => c.Act("togglePhoto") },
                    new UiNode(UiKind.Button) { Key = "setWanted", Icon = "volume-up", Text = "Set Wanted Issue", OnClick = () => c.Act("submitWantedIssue") },
                    new UiNode(UiKind.Button) { Key = "clearWanted", Icon = "times", Color = "red", Text = "Clear Wanted", OnClick = () => c.Act("clearWantedIssue") }));
        }
        else
        {
            var notice = new UiNode(UiKind.Box) { Key = "notice" };
            foreach (UiValue activeWanted in wanted)
                notice.With(new UiNode(UiKind.Box) { Text = activeWanted.Truthy("active") ? "Please contact your local security officer if spotted." : "No wanted issue posted. Have a secure day." });
            modal.With(notice);
        }
        return modal;
    }

    // tg: Newscaster.jsx:359
    private static UiNode NewscasterContent(TguiContext c) => new UiNode(UiKind.Stack) { Key = "content", Fill = true, Vertical = true }.With(
        new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "topStack", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "selectorItem", Grow = 1 }.With(ChannelSelector(c)),
            new UiNode(UiKind.StackItem) { Key = "rightItem", Grow = 2 }.With(new UiNode(UiKind.Stack) { Key = "rightStack", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "userItem" }.With(Vending.UserDetails(c.Data)),
                new UiNode(UiKind.StackItem) { Key = "boxItem", Grow = 1 }.With(ChannelBox(c)))))),
        new UiNode(UiKind.StackItem) { Key = "messagesItem", Grow = 1 }.With(ChannelMessages(c)));

    /** The Channel Box is the basic channel information where buttons live.*/
    // tg: Newscaster.jsx:393
    private static UiNode ChannelBox(TguiContext c)
    {
        UiValue data = c.Data;
        bool channelLocked = data.Truthy("channelLocked");
        string channelAuthor = data.Str("channelAuthor");
        bool channelCensored = data.Truthy("channelCensored");
        bool receivingCrossSector = data.Truthy("receivingCrossSector");
        UiValue viewingChannel = data.Get("viewing_channel");
        bool adminMode = data.Truthy("admin_mode");
        bool photoData = data.Truthy("photo_data");
        double paper = data.Num("paper");
        string userName = data.Get("user")?.Str("name");
        bool cannotWrite = (channelLocked && channelAuthor != userName) || channelCensored || receivingCrossSector;

        UiNode description = channelCensored
            ? new UiNode(UiKind.Section) { Key = "censored" }.With(new UiNode(UiKind.Box) { Key = "censoredText", Color = "red", Text = "ATTENTION: " + CENSOR_MESSAGE })
            : new UiNode(UiKind.Section) { Key = "description", Fill = true, Scrollable = true }.With(
                new UiNode(UiKind.Box) { Key = "descText", FontSize = "1.2em", Wrap = true, Text = System.Net.WebUtility.HtmlDecode(data.Str("channelDesc") ?? "") });

        var buttons = new UiNode(UiKind.Box) { Key = "storyButtons" }.With(
            new UiNode(UiKind.Button) { Key = "story", Icon = "print", Disabled = cannotWrite, Text = "Submit Story", OnClick = () => c.Act("createStory", ("current", viewingChannel)) },
            new UiNode(UiKind.Button) { Key = "photo", Icon = "camera", Selected = photoData, Disabled = cannotWrite, Text = "Select Photo", OnClick = () => c.Act("togglePhoto") });
        if (adminMode)
        {
            buttons.With(new UiNode(UiKind.Button)
            {
                Key = "dnotice",
                Icon = "ban",
                Tooltip = "Censor the whole channel and its contents as dangerous to the station.",
                Disabled = viewingChannel == null || viewingChannel.IsNull,
                Text = "D-Notice",
                OnClick = () => c.Act("channelDNotice", ("secure", true), ("channel", viewingChannel)),
            });
        }

        return new UiNode(UiKind.Section) { Key = "channelBox", Fill = true, Title = data.Str("channelName") }.With(new UiNode(UiKind.Stack) { Key = "boxStack", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "descItem", Grow = 1 }.With(description),
            new UiNode(UiKind.StackItem) { Key = "buttonsItem" }.With(
                buttons,
                new UiNode(UiKind.Box) { Key = "printBox" }.With(new UiNode(UiKind.Button)
                {
                    Key = "print",
                    Icon = "newspaper",
                    Tooltip = paper <= 0 ? "Insert paper first!" : "",
                    Disabled = paper <= 0,
                    Text = "Print Newspaper",
                    OnClick = () => c.Act("printNewspaper"),
                }))));
    }

    /** Channel select is the left-hand menu where all the channels are listed. */
    // tg: Newscaster.jsx:485
    private static UiNode ChannelSelector(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue viewingChannel = data.Get("viewing_channel");
        var tabs = new UiNode(UiKind.Tabs) { Key = "channels", Vertical = true };
        int index = 0;
        foreach (UiValue activeWanted in data.Get("wanted")?.Items ?? Array.Empty<UiValue>())
        {
            bool active = activeWanted.Truthy("active");
            tabs.With(new UiNode(UiKind.Tab) { Key = "wanted:" + index++, Icon = active ? "skull-crossbones" : null, Color = active ? "red" : "grey", Text = "Wanted Issue", OnClick = () => c.Act("toggleWanted") });
        }
        foreach (UiValue channel in data.Get("channels")?.Items ?? Array.Empty<UiValue>())
        {
            UiValue id = channel.Get("ID");
            bool censored = channel.Truthy("censored");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "channel:" + channel.Str("name"),
                Selected = viewingChannel != null && !viewingChannel.IsNull && viewingChannel.AsNum() == id.AsNum(),
                Icon = censored ? "ban" : null,
                Color = censored ? "red" : "white",
                Text = channel.Str("name"),
                OnClick = () => c.Act("setChannel", ("channel", id)),
            });
        }
        tabs.With(new UiNode(UiKind.Tab) { Key = "create", Color = "Green", Text = "Create Channel [+]", OnClick = () => c.Act("startCreateChannel") });
        return new UiNode(UiKind.Section) { Key = "selector", Fill = true }.With(tabs);
    }

    /** This is where the channels comments get spangled out (tm) */
    // tg: Newscaster.jsx:538
    private static UiNode ChannelMessages(TguiContext c)
    {
        UiValue data = c.Data;
        if (data.Truthy("channelCensored"))
        {
            return new UiNode(UiKind.Section) { Key = "messages" }.With(
                new UiNode(UiKind.Box) { Key = "censored", Color = "red", Text = "ATTENTION: Comments cannot be read at this time." },
                new UiNode(UiKind.Box) { Key = "censored2", Color = "red", Text = "Thank you for your understanding, and have a secure day." });
        }
        UiValue viewingChannel = data.Get("viewing_channel");
        bool adminMode = data.Truthy("admin_mode");
        bool channelLocked = data.Truthy("channelLocked");
        string channelAuthor = data.Str("channelAuthor");
        string userName = data.Get("user")?.Str("name");

        var section = new UiNode(UiKind.Section) { Key = "messages" };
        foreach (UiValue message in data.Get("messages")?.Items ?? Array.Empty<UiValue>())
        {
            UiValue id = message.Get("ID");
            // tg: messages.filter((message) => message.ID !== viewing_channel): a story whose number is the channel's is left out
            if (viewingChannel != null && !viewingChannel.IsNull && id.AsNum() == viewingChannel.AsNum())
                continue;
            bool censoredAuthor = message.Truthy("censored_author");
            bool censoredMessage = message.Truthy("censored_message");
            var buttons = new UiNode(UiKind.Fragment) { Key = "buttons" };
            if (adminMode)
            {
                buttons.With(
                    new UiNode(UiKind.Button) { Key = "censorStory", Icon = "comment-slash", Tooltip = "Censor Story", OnClick = () => c.Act("storyCensor", ("messageID", id)) },
                    new UiNode(UiKind.Button) { Key = "censorAuthor", Icon = "user-slash", Tooltip = "Censor Author", OnClick = () => c.Act("authorCensor", ("messageID", id)) });
            }
            buttons.With(new UiNode(UiKind.Button)
            {
                Key = "comment",
                Icon = "comment",
                Tooltip = "Leave a Comment.",
                Disabled = censoredAuthor || censoredMessage || userName == "Unknown" || (channelLocked && channelAuthor != userName),
                OnClick = () => c.Act("startComment", ("messageID", id)),
            });

            var story = new UiNode(UiKind.Section)
            {
                Key = "message:" + TguiFormat.JsNumber(id.AsNum()),
                Title = censoredAuthor ? "By: [REDACTED]. D-Notice Notice ." : "By: " + message.Str("auth") + " at " + message.Str("time"),
                Color = censoredAuthor ? "red" : null,
                Buttons = buttons,
            };
            if (censoredMessage)
                story.With(new UiNode(UiKind.Box) { Key = "censoredBody", Color = "red", Text = "This message was deemed dangerous to the general welfare of the station and therefore marked with a D-Notice." });
            else
                story.With(new UiNode(UiKind.Html) { Key = "body", TextValue = message.Str("body") ?? "" });
            // tg: <Image src={message.photo}>: the story's photo (no picture has an image here, D104)
            int commentIndex = 0;
            foreach (UiValue comment in message.Get("comments")?.Items ?? Array.Empty<UiValue>())
            {
                story.With(new UiNode(UiKind.Box) { Key = "comment:" + commentIndex++ }.With(
                    new UiNode(UiKind.Box) { Key = "commentAuthor", Color = "label", Text = "By: " + comment.Str("auth") + " at " + comment.Str("time") },
                    new UiNode(UiKind.Html) { Key = "commentBody", TextValue = comment.Str("body") ?? "" }));
            }
            story.With(new UiNode(UiKind.Divider) { Key = "divider" });
            section.With(story);
        }
        return section;
    }

    // tg: common/LoadingScreen.tsx  (the toolbox does not spin here)
    private static UiNode LoadingScreen(string label) => new UiNode(UiKind.Modal) { Key = "loading", TextAlign = "center" }.With(
        new UiNode(UiKind.Stack) { Key = "loadingStack", Align = "center", Fill = true, Justify = "center", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "iconItem" }.With(new UiNode(UiKind.Icon) { Key = "icon", Color = "blue", Icon = "toolbox", Size = 4 }),
            new UiNode(UiKind.StackItem) { Key = "label", Text = label }));
}

public static class PhysicalNewscaster
{
    // tg: PhysicalNewscaster.tsx:4
    public static UiNode Render(TguiContext c) => c.Window(575, 560, null, Newscaster.Content(c));
}

public static class NtosNewscaster
{
    // tg: NtosNewscaster.tsx:4
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 575, 700, null, Newscaster.Content(c));
}
