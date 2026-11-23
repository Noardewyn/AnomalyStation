using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M9 slice A (D250): the say window's model, a port of tgui-say (its own ChannelIterator and ChatHistory tests first).</summary>
public class SayModelTests
{
    private readonly List<SayOut> _sent = new();

    private SayModel Model() => new(_sent.Add);

    [Fact]
    public void TheChannelsCycleWithoutAdmin()
    {
        var it = new SayChannelIterator();
        Assert.Equal("Say", it.Current);
        Assert.Equal(new[] { "Radio", "Me", "OOC", "Pray", "Say" }, Enumerable.Range(0, 5).Select(_ => it.Next()));
        it.Set("OOC");
        Assert.False(it.IsVisible);
        it.Set("Admin");
        Assert.Equal("Admin", it.Next()); // tg: a blacklisted channel does not leak into another
    }

    // M10x slice A (D281): from tg's lobby only OOC and Pray reach anyone
    [Fact]
    public void InTheLobbyTheWindowOffersOocAndPray()
    {
        SayModel model = Model();
        model.Lobby = true;
        model.Open(SayChannels.Say);
        Assert.Equal(SayChannels.Ooc, model.Channel);
        var it = new SayChannelIterator { Lobby = true };
        Assert.Equal(new[] { "Pray", "OOC", "Pray" }, Enumerable.Range(0, 3).Select(_ => it.Next()));
        it.Lobby = false;
        Assert.Equal("Say", it.Next());

        var input = new ChatInputModel(_sent.Add);
        input.Lobby = true;
        Assert.Equal(SayChannels.Ooc, input.Channel);
        input.SetChannel(ChatInputModel.Whisper);
        Assert.Equal(SayChannels.Ooc, input.Channel);
        Assert.False(input.Allowed(SayChannels.Radio));
        input.IncrementChannel();
        Assert.Equal(SayChannels.Pray, input.Channel);
        input.IncrementChannel();
        Assert.Equal(SayChannels.Ooc, input.Channel);
        input.Lobby = false;
        input.SetChannel(SayChannels.Say);
        Assert.Equal(SayChannels.Say, input.Channel);
    }

    [Fact]
    public void EachSpeechKeySelectsItsChannelInTheOpenChatWindowsInput()
    {
        // ui-overhaul.md section 5 (playtest 2026-11-23): with the chat window open the radio, OOC and me keys only focused its input, left on Say
        Assert.Equal(SayChannels.Radio, ChatInputModel.ChannelOfKey(PlayAction.Radio));
        Assert.Equal(SayChannels.Ooc, ChatInputModel.ChannelOfKey(PlayAction.Ooc));
        Assert.Equal(SayChannels.Me, ChatInputModel.ChannelOfKey(PlayAction.Me));
        Assert.Equal(SayChannels.Say, ChatInputModel.ChannelOfKey(PlayAction.Say));
        Assert.Equal(SayChannels.Pray, ChatInputModel.ChannelOfKey(PlayAction.Pray));
        Assert.Null(ChatInputModel.ChannelOfKey(PlayAction.DropItem));
        Assert.Null(ChatInputModel.ChannelOfKey(null));
        var input = new ChatInputModel(_sent.Add);
        foreach (PlayAction key in new[] { PlayAction.Radio, PlayAction.Ooc, PlayAction.Me, PlayAction.Say, PlayAction.Pray, PlayAction.Say })
        {
            input.SetChannel(ChatInputModel.ChannelOfKey(key)); // what ChatWindow.FocusInput(channel) does before it focuses
            Assert.Equal(ChatInputModel.ChannelOfKey(key), input.Channel);
        }
        // the default keys reach them: ;, [ and Shift+'
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(SayChannels.Radio, ChatInputModel.ChannelOfKey(keys.ActionOfPress("Semicolon", shift: false)));
        Assert.Equal(SayChannels.Ooc, ChatInputModel.ChannelOfKey(keys.ActionOfPress("Bracketleft", shift: false)));
        Assert.Equal(SayChannels.Me, ChatInputModel.ChannelOfKey(keys.ActionOfPress("Apostrophe", shift: true)));
    }

    [Fact]
    public void TheHistoryKeepsFiveAndWalksBothWays()
    {
        var history = new SayChatHistory();
        for (int i = 1; i <= 6; i++)
            history.Add("m" + i);
        Assert.Equal("m6", history.GetOlderMessage());
        Assert.Equal(1, history.Index);
        Assert.Equal("m5", history.GetOlderMessage());
        for (int i = 0; i < 3; i++)
            history.GetOlderMessage();
        Assert.Null(history.GetOlderMessage()); // m1 fell off
        Assert.Equal("m3", history.GetNewerMessage());
        history.Reset();
        Assert.True(history.IsAtLatest);
        Assert.Null(history.GetNewerMessage());
    }

    [Fact]
    public void OpenTypeAndEnterSendTgsMessages()
    {
        SayModel say = Model();
        say.Open("Say");
        say.Input("hello", 0);
        say.Enter();
        Assert.Equal(new[]
        {
            new SayOut(SayTypes.Open, "Say"),
            new SayOut(SayTypes.Typing),
            new SayOut(SayTypes.Save, "Say", "hello"),
            new SayOut(SayTypes.Entry, "Say", "hello"),
            new SayOut(SayTypes.Close),
        }, _sent);
        Assert.False(say.IsOpen);
        Assert.Equal("", say.Value);
    }

    [Fact]
    public void ARadioPrefixMovesToTheButtonAndBackIntoTheEntry()
    {
        SayModel say = Model();
        say.Open("Radio");
        say.Input(".e fire", 0);
        Assert.Equal("fire", say.Value);
        Assert.Equal("Engi", say.ButtonContent);
        Assert.Equal("Engi", say.Theme);
        Assert.Equal("Say", say.Channel);
        say.Enter();
        Assert.Contains(new SayOut(SayTypes.Entry, "Say", ":e fire"), _sent);
        Assert.Null(SayModel.GetPrefix(":x hi"));
        Assert.Equal(":s ", SayModel.GetPrefix(".S yo"));
    }

    [Fact]
    public void BackspaceOnAnEmptyFieldDropsThePrefix()
    {
        SayModel say = Model();
        say.Open("Say");
        say.Input(":m ", 0);
        Assert.Equal("Med", say.ButtonContent);
        say.BackspaceDelete();
        Assert.Null(say.CurrentPrefix);
        Assert.Equal("Say", say.ButtonContent);
    }

    [Fact]
    public void TheQuietChannelsShowNoTypingAndTabTellsTheServerAfterAPause()
    {
        SayModel say = Model();
        say.Open("OOC");
        say.Input("lol", 0);
        Assert.DoesNotContain(_sent, m => m.Type == SayTypes.Typing);
        say.IncrementChannel(1.0); // OOC -> Pray
        say.IncrementChannel(1.1); // Pray -> Say
        say.Advance(1.4);
        Assert.DoesNotContain(_sent, m => m.Type == SayTypes.Thinking);
        say.Advance(1.6);
        Assert.Equal(new SayOut(SayTypes.Thinking, Visible: true), _sent[^1]); // the debounce's last call, 0.4 s after it
        Assert.Equal("Say", say.ButtonContent);
    }

    [Fact]
    public void TypingIsThrottledToOnceInFourSeconds()
    {
        SayModel say = Model();
        say.Open("Say");
        say.Input("a", 0);
        say.Input("ab", 1);
        say.Input("abc", 2);
        say.Advance(3.9);
        Assert.Single(_sent, m => m.Type == SayTypes.Typing);
        say.Advance(4.0);
        Assert.Equal(2, _sent.Count(m => m.Type == SayTypes.Typing));
    }

    [Fact]
    public void TheWindowGrowsWithTheText()
    {
        SayModel say = Model();
        say.Open("Say");
        say.Input(new string('a', 20), 0);
        Assert.Equal(SayModel.SizeSmall, say.Size);
        say.Input(new string('a', 21), 0);
        Assert.Equal(SayModel.SizeMedium, say.Size);
        say.Input(new string('a', 40), 0);
        Assert.Equal(SayModel.SizeLarge, say.Size);
    }

    [Fact]
    public void TheHistoryComesBackWithUpAndTheTypedTextWithDown()
    {
        SayModel say = Model();
        say.Open("Say");
        say.Input("first", 0);
        say.Enter();
        say.Open("Say");
        say.Input("draft", 0);
        say.Arrow(up: true);
        Assert.Equal("first", say.Value);
        Assert.Equal("1", say.ButtonContent);
        say.Arrow(up: false);
        Assert.Equal("draft", say.Value);
        Assert.Equal("Say", say.ButtonContent);
    }

    [Fact]
    public void TheServerPropsForceSaveAndClose()
    {
        SayModel say = Model();
        say.OnServer(new SayModalMessage(SayModalCommand.Props, 10, true), 0);
        Assert.True(say.LightMode);
        Assert.Equal("lightMode", say.Theme);
        say.Open("Say");
        say.Input("an overlong line", 0);
        Assert.Equal(10, say.Value.Length);
        say.OnServer(new SayModalMessage(SayModalCommand.Force, 0, false), 1);
        Assert.Contains(new SayOut(SayTypes.Force, "Say", "an overlon"), _sent);
        Assert.False(say.IsOpen);
        say.Open("Me");
        say.OnServer(new SayModalMessage(SayModalCommand.Close, 0, false), 2);
        Assert.False(say.IsOpen);
    }

    [Fact]
    public void AnEntryAtTheCapIsNotSent()
    {
        SayModel say = Model();
        say.OnServer(new SayModalMessage(SayModalCommand.Props, 5, false), 0);
        say.Open("Say");
        say.Input("12345", 0);
        say.Enter(); // tg: value.length < maxLength
        Assert.DoesNotContain(_sent, m => m.Type == SayTypes.Entry);
    }
}
