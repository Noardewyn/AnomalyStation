using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/VotePanel.tsx (the owner, 2026-11-08: votes).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The kit has no Dimmer or BlockQuote: the cooldown's dimmer stands in place of the vote options it covers (they cannot be clicked
// under it either), and a vote's name is a box.

public static class VotePanel
{
    // tg: VotePanel.tsx:8  enum VoteConfig
    private const double ConfigNone = -1, ConfigEnabled = 1;

    // tg: VotePanel.tsx:52  enum VoteSystem
    private const double VoteSingle = 1, VoteMulti = 2;

    // tg: VotePanel.tsx:67
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue currentVote = data.Get("currentVote");
        bool voting = currentVote is { Kind: UiValueKind.Object };
        UiValue user = data.Get("user");

        string windowTitle = "Vote";
        if (voting)
        {
            string question = currentVote.Str("question", null);
            windowTitle += ": " + Capitalize(string.IsNullOrEmpty(question) ? currentVote.Get("vote").Str("name") : question);
        }

        bool lowerAdmin = user.Truthy("isLowerAdmin");
        double lastVoteTime = data.Num("LastVoteTime"), voteCd = data.Num("VoteCD");

        // tg: VotePanel.tsx:81  <Window title={windowTitle} width={400} height={500}>
        return c.Window(400, 500, windowTitle, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Section)
                {
                    Title = "New Vote",
                    Buttons = lowerAdmin
                        ? new UiNode(UiKind.Stack).With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "resetCooldown", Icon = "refresh", Disabled = lastVoteTime + voteCd <= 0, Text = "Reset cooldown", OnClick = () => c.Act("resetCooldown") }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "toggleDeadVote", Disabled = !user.Truthy("isUpperAdmin"), Checkbox = !data.Truthy("deadVoteEnabled"), Color = "primary", Text = "Dead votes", OnClick = () => c.Act("toggleDeadVote") }))
                        : null,
                }.With(VoteOptions(c, user, lastVoteTime, voteCd))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "active", Fill = true, Scrollable = true, Title = "Active Vote" }.With(ChoicesPanel(c, voting ? currentVote : null, user))),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Section).With(TimePanel(c, voting ? currentVote : null, lowerAdmin)))));
    }

    // tg: VotePanel.tsx:129  VoteOptionDimmer, VotePanel.tsx:145  VoteOptions
    private static UiNode VoteOptions(TguiContext c, UiValue user, double lastVoteTime, double voteCd)
    {
        if (lastVoteTime + voteCd > 0)
            return new UiNode(UiKind.Box) { Key = "cooldown", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { FontSize = 2, Bold = true, Text = "Vote Cooldown" },
                new UiNode(UiKind.Box) { FontSize = 1.5, Text = Math.Floor((voteCd + lastVoteTime) / 10) + "s" });

        bool lowerAdmin = user.Truthy("isLowerAdmin"), upperAdmin = user.Truthy("isUpperAdmin");
        var list = new UiNode(UiKind.Stack) { Vertical = true, Justify = "space-between" };
        foreach (UiValue option in c.Data.Get("possibleVotes")?.Items ?? Array.Empty<UiValue>())
        {
            string name = option.Str("name");
            double config = option.Num("config");
            var row = new UiNode(UiKind.Stack);
            if (lowerAdmin)
                row.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "toggle-" + name,
                    Color = "primary",
                    Checkbox = config == ConfigEnabled || config == ConfigNone,
                    Disabled = !upperAdmin || config == ConfigNone,
                    Tooltip = config == ConfigNone ? "This vote cannot be disabled." : null,
                    Text = "Active",
                    OnClick = () => c.Act("toggleVote", ("voteName", name)),
                }));
            row.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "call-" + name, Disabled = !option.Truthy("canBeInitiated"), Icon = "play", OnClick = () => c.Act("callVote", ("voteName", name)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "name-" + name, Tooltip = option.Str("message"), Text = name + " Vote" }));
            list.With(new UiNode(UiKind.StackItem) { Key = name }.With(row));
        }
        return list;
    }

    // tg: VotePanel.tsx:213  ChoicesPanel
    private static UiNode ChoicesPanel(TguiContext c, UiValue currentVote, UiValue user)
    {
        if (currentVote == null)
            return new UiNode(UiKind.NoticeBox) { Text = "No vote active!" };

        double countMethod = currentVote.Num("countMethod");
        bool multi = countMethod == VoteMulti;
        if (countMethod != VoteSingle && !multi)
            return new UiNode(UiKind.Fragment);
        IReadOnlyList<UiValue> choices = currentVote.Get("choices")?.Items ?? Array.Empty<UiValue>();
        bool ghost = user.Truthy("isGhost");
        string single = user.Str("singleSelection", null);
        UiValue multiSelection = user.Get("multiSelection");
        string ckey = user.Str("ckey");

        var labeled = new UiNode(UiKind.LabeledList);
        foreach (UiValue choice in choices)
        {
            string name = choice.Str("name");
            double votes = choice.Num("votes");
            bool mine = multi ? multiSelection is { Kind: UiValueKind.Object } && multiSelection.Num(ckey + name) == 1 : single != null && name == single;
            var content = new UiNode(UiKind.Fragment);
            if (mine)
                content.With(new UiNode(UiKind.Icon) { Key = "mine", Align = "right", Color = multi ? "blue" : "green", Icon = "vote-yea" });
            // the single vote shows the counts only with public statistics (or to an admin); the multiple one always
            if (multi || currentVote.Truthy("displayStatistics") || user.Truthy("isLowerAdmin"))
                content.With(new UiNode(UiKind.Box) { Key = "votes", Text = votes + " Votes" });
            labeled.With(
                new UiNode(UiKind.LabeledItem)
                {
                    Key = name,
                    Label = Capitalize(name),
                    TextAlign = "right",
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "vote-" + name,
                        Tooltip = ghost ? "Ghost voting was disabled by an admin." : null,
                        Disabled = ghost || !multi && single == name,
                        Text = "Vote",
                        OnClick = () => c.Act(multi ? "voteMulti" : "voteSingle", ("voteOption", name)),
                    },
                }.With(content),
                new UiNode(UiKind.Divider) { Key = "divider-" + name });
        }
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.NoticeBox) { Color = "green", Text = multi ? "Select any number of options" : "Select one option" },
            choices.Count != 0 ? labeled : null);
    }

    // tg: VotePanel.tsx:302  TimePanel
    private static UiNode TimePanel(TguiContext c, UiValue currentVote, bool lowerAdmin)
    {
        var row = new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
            new UiNode(UiKind.Box) { FontSize = 1.5, Text = currentVote != null ? $"Time remaining: {currentVote.Num("timeRemaining")}s" : "No current vote" });
        if (lowerAdmin)
            row.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "endNow", Color = "green", Disabled = currentVote == null, Text = "End Now", OnClick = () => c.Act("endNow") }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "cancel", Color = "red", Disabled = currentVote == null, Text = "Cancel", OnClick = () => c.Act("cancel") })));
        return row;
    }

    // tg's replace(/^\w/, (c) => c.toUpperCase())
    private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text ?? "" : char.ToUpperInvariant(text[0]) + text[1..];
}
