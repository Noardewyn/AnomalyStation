using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M9 slice C (D259): the big chat window's log (tgui-panel's chat) and its input row.</summary>
public class ChatLogTests
{
    private const string Say = "<span class='game say'><span class='name'>Alice</span> <span class='message'>says, \"Hi\"</span></span>";
    private const string Ooc = "<span class='ooc'><span class='prefix'>OOC:</span> <EM>Alice:</EM> <span class='message linkify'>brb</span></span>";

    [Theory]
    [InlineData(Say, ChatLog.TypeLocalChat)]
    [InlineData("<span class='emote'><b>Alice</b> waves.</span>", ChatLog.TypeLocalChat)]
    [InlineData(Ooc, ChatLog.TypeOoc)]
    [InlineData("<span class='danger'>Bob punches you!</span>", ChatLog.TypeCombat)]
    [InlineData("<span class=\"warning\">You are on fire!</span>", ChatLog.TypeWarning)]
    [InlineData("<span class='notice'>You put it on.</span>", ChatLog.TypeInfo)]
    [InlineData("<span class='notice pm'>An admin PM.</span>", ChatLog.TypeAdminPm)] // tg's .notice:not(.pm), then .pm
    [InlineData("<div class='boldannounce'>The round is starting.</div>", ChatLog.TypeSystem)]
    [InlineData("<span class=radio>[Common] Bob says</span>", ChatLog.TypeRadio)]
    [InlineData("plain text", ChatLog.TypeUnknown)]
    [InlineData("<b class='unknownclass'>x</b>", ChatLog.TypeUnknown)]
    public void ALineIsTypedByTgsSelectors(string html, string type) => Assert.Equal(type, ChatLog.TypeOf(html));

    [Fact]
    public void TheServersTypeWins()
    {
        var log = new ChatLog();
        log.Add(ChatLog.TypeRadio, Say, 0);
        Assert.Equal(ChatLog.TypeRadio, log.Visible[0].Type);
        Assert.Equal("Alice says, \"Hi\"", log.Visible[0].Plain);
        Assert.NotNull(log.Visible[0].Spans); // tg's colours
    }

    [Fact]
    public void ThePagesShowTheTypesTheyTake()
    {
        var log = new ChatLog();
        Assert.Equal(new[] { "All", "Local", "Radio", "Combat", "OOC", "Messages", "Dead" }, log.Pages.Select(p => p.Name));
        log.Add(null, Say, 0);
        log.Add(null, Ooc, 1);
        log.Add(null, "<span class='boldannounce'>Restarting</span>", 2);
        Assert.Equal(3, log.Visible.Count);
        int generation = log.Generation;
        log.ChangePage(log.Pages[1]);
        Assert.Equal(new[] { ChatLog.TypeLocalChat, ChatLog.TypeSystem }, log.Visible.Select(m => m.Type)); // the system lines are on every page
        Assert.NotEqual(generation, log.Generation);
        log.ChangePage(log.Pages[4]);
        Assert.Equal(new[] { ChatLog.TypeOoc, ChatLog.TypeSystem }, log.Visible.Select(m => m.Type));
        log.ChangePage(log.Pages[0]);
        Assert.Equal(3, log.Visible.Count);
        // M9 slice E (DV171): the PDA's lines, typed by the server, on the Messages page and the main one
        log.Add(ChatLog.TypePda, "<span class='infoplain'><b>PDA message from Bob Roe (Cook), </b>\"hi\"</span>", 3);
        Assert.Equal(4, log.Visible.Count);
        log.ChangePage(log.Pages[5]);
        Assert.Equal(new[] { ChatLog.TypeSystem, ChatLog.TypePda }, log.Visible.Select(m => m.Type));
        // M10x slice B: tg's dead chat line (deadchat_broadcast's .deadsay) on the Dead page
        log.Add(null, "<span class='deadsay'><span class='game'><span class='prefix'>DEAD:</span> <span class='name'>Alice</span> <span class='message'>boo</span></span></span>", 4);
        log.ChangePage(log.Pages[6]);
        Assert.Equal(new[] { ChatLog.TypeSystem, ChatLog.TypeDeadChat }, log.Visible.Select(m => m.Type));
    }

    [Fact]
    public void ARepeatWithinFiveSecondsCountsOnTheEarlierLine()
    {
        var log = new ChatLog();
        log.Add(null, Say, 0);
        log.Add(null, Ooc, 0.5);
        Assert.Equal(0, log.TakeChanges());
        Assert.Equal(int.MaxValue, log.TakeChanges());
        log.Add(null, Say, 4);
        Assert.Equal(2, log.Visible.Count);
        Assert.Equal(2, log.Visible[0].Times);
        Assert.Equal(0, log.TakeChanges()); // the view redraws from the counted line
        log.Add(null, Say, 5.5); // 5 s after the first: a new line
        Assert.Equal(3, log.Visible.Count);
        Assert.Equal(2, log.TakeChanges());
    }

    [Fact]
    public void UnreadLinesAreCountedOnThePagesThatCannotBeRead()
    {
        var log = new ChatLog();
        ChatPage all = log.Pages[0], local = log.Pages[1], ooc = log.Pages[4];
        log.Add(null, Say, 0);
        Assert.All(log.Pages, p => Assert.Equal(0, p.UnreadCount)); // read on the tracked main page
        log.SetScrollTracking(false);
        log.Add(null, Say + " ", 1);
        Assert.Equal(1, all.UnreadCount);
        Assert.Equal(0, local.UnreadCount); // the same line can be read on the current page
        log.SetScrollTracking(true);
        Assert.Equal(0, all.UnreadCount);
        log.ChangePage(ooc);
        log.Add(null, Say + "  ", 2);
        Assert.Equal(1, all.UnreadCount);
        Assert.Equal(1, local.UnreadCount);
        Assert.Equal(0, ooc.UnreadCount);
        log.ChangePage(local);
        Assert.Equal(0, local.UnreadCount);
    }

    [Fact]
    public void ThePruneKeepsTgsCapsAndWaitsWhileScrolledUp()
    {
        var log = new ChatLog();
        for (int i = 0; i < 2600; i++)
            log.Add(null, Say + i, i * 10);
        log.SetScrollTracking(false);
        log.PruneMessages();
        Assert.Equal(2600, log.Visible.Count);
        log.SetScrollTracking(true);
        log.Advance(ChatLog.MessagePruneInterval);
        Assert.Equal(ChatLog.MaxVisibleMessages, log.Visible.Count);
        Assert.Equal(ChatLog.MaxPersistedMessages, log.Count);
        Assert.EndsWith("2599", log.Visible[^1].Html, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearDropsTheCurrentPagesLines()
    {
        var log = new ChatLog();
        log.Add(null, Say, 0);
        log.Add(null, Ooc, 0);
        log.ChangePage(log.Pages[4]);
        log.Clear();
        Assert.Empty(log.Visible);
        log.ChangePage(log.Pages[0]);
        Assert.Equal(ChatLog.TypeLocalChat, Assert.Single(log.Visible).Type);
    }

    private readonly List<SayOut> _sent = new();

    [Fact]
    public void TheInputShapesWhispersAndShoutsAsTgReadsThem()
    {
        Assert.Equal("#hi", ChatInputModel.Shape(ChatInputModel.Whisper, null, "hi"));
        Assert.Equal("#:e hi", ChatInputModel.Shape(ChatInputModel.Whisper, ":e ", "hi"));
        Assert.Equal("Help!!", ChatInputModel.Shape(ChatInputModel.Shout, null, "Help"));
        Assert.Equal("Help!!", ChatInputModel.Shape(ChatInputModel.Shout, null, "Help."));
        Assert.Equal("Help!!", ChatInputModel.Shape(ChatInputModel.Shout, null, "Help!"));
        Assert.Equal("What?!!", ChatInputModel.Shape(ChatInputModel.Shout, null, "What?"));
        Assert.Equal(":e hi", ChatInputModel.Shape(SayChannels.Say, ":e ", "hi"));
        Assert.Equal("waves", ChatInputModel.Shape(SayChannels.Me, null, "waves"));
    }

    [Fact]
    public void TheInputTalksToTgsSayWindowAndKeepsTheKeyboard()
    {
        var input = new ChatInputModel(_sent.Add);
        input.SetChannel(ChatInputModel.Whisper);
        Assert.Empty(_sent); // not focused: nothing to the server yet
        input.Focus();
        Assert.Empty(_sent); // the owner (2026-10-26): no indicator until something is typed
        input.Input("the code is 1234", 0);
        Assert.Equal(new SayOut(SayTypes.Open, SayChannels.Say), _sent[0]);
        Assert.Equal(SayTypes.Typing, _sent[^1].Type);
        Assert.True(input.Enter());
        Assert.Contains(new SayOut(SayTypes.Entry, SayChannels.Say, "#the code is 1234"), _sent);
        Assert.True(input.Focused);
        Assert.Equal("", input.Value);
        Assert.Equal(new SayOut(SayTypes.Close), _sent[^1]); // sent: the indicator goes until the next line is typed
        input.Arrow(true);
        Assert.Equal("the code is 1234", input.Value);
        Assert.Equal("1", input.ButtonContent);
        input.Arrow(false);
        Assert.Equal("", input.Value);
        int sent = _sent.Count;
        input.Blur();
        Assert.Equal(sent, _sent.Count); // nothing typed since: the server's window is already closed
        input.Input("still here", 1);
        Assert.Equal("still here", input.Value); // the text stays while the player walks
    }

    [Fact]
    public void TabCyclesTheWindowsChannelsAndAPrefixMovesToTheButton()
    {
        var input = new ChatInputModel(_sent.Add);
        var seen = new List<string>();
        for (int i = 0; i < ChatInputModel.Channels.Count; i++)
        {
            seen.Add(input.Channel);
            input.IncrementChannel();
        }
        Assert.Equal(new[] { "Say", "Whisper", "Shout", "Radio", "Me", "OOC", "Pray" }, seen);
        Assert.Equal(SayChannels.Say, input.Channel);
        input.SetChannel(SayChannels.Radio);
        input.Input(":e fire in the SM", 0);
        Assert.Equal(SayChannels.Say, input.Channel); // tg: a prefix sets the say channel
        Assert.Equal("Engi", input.ButtonContent);
        Assert.Equal("Engi", input.Theme);
        Assert.Equal("fire in the SM", input.Value);
        input.Focus();
        input.Enter();
        Assert.Contains(new SayOut(SayTypes.Entry, SayChannels.Say, ":e fire in the SM"), _sent);
        Assert.Null(input.Prefix);
        input.SetChannel(SayChannels.Ooc);
        _sent.Clear();
        input.Input("brb", 5);
        Assert.DoesNotContain(_sent, s => s.Type == SayTypes.Typing); // tg's quiet channel
        input.Enter();
        Assert.Contains(new SayOut(SayTypes.Entry, SayChannels.Ooc, "brb"), _sent);
    }

    [Fact]
    public void TheServersForceSayTakesTheFocusedLine()
    {
        var input = new ChatInputModel(_sent.Add);
        input.SetChannel(ChatInputModel.Shout);
        input.Focus();
        input.Input("Help", 0);
        input.OnServer(new SayModalMessage(SayModalCommand.Force, 0, false));
        Assert.Equal(new SayOut(SayTypes.Force, SayChannels.Say, "Help!!"), _sent[^1]);
        Assert.Equal("", input.Value);
        input.OnServer(new SayModalMessage(SayModalCommand.Props, 300, false));
        Assert.Equal(300, input.MaxLength);
    }
}
