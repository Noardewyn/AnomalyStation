using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 38 (M9 slice A, docs/ai/specs/protocol.md section 47): the Say action (tg's say window to /datum/tgui_say), the SayModal
/// and Runechat messages. Bytes, round trips, the caps.
/// </summary>
public class ProtocolV38Tests
{
    private static ActionMessage Say(string type, string channel = null, string entry = null, bool visible = false)
        => new(7, ActionKind.Say, 0, ClickButton.None, ClickModifiers.None, entry) { SayType = type, SayChannel = channel, SayVisible = visible };

    [Fact]
    public void TheIdsAndTheKindFollowVersion37()
    {
        Assert.True(ProtocolVersion.Current >= 38);
        Assert.Equal(18, (int)ActionKind.Say);
        Assert.True(ActionKinds.Last >= ActionKind.Say);
        Assert.Equal(19, (int)MessageId.SayModal);
        Assert.Equal(20, (int)MessageId.Runechat);
    }

    [Fact]
    public void AnEntryHasItsBytesAndRoundTrips()
    {
        ActionMessage entry = Say(SayTypes.Entry, SayChannels.Say, "hi");
        byte[] bytes = Codec.Encode(entry);
        // id 10, seq 7, kind 18, "entry", "Say", "hi" (a string's length plus one, 0 being none), visible 0
        Assert.Equal(new byte[] { 10, 7, 18, 6, (byte)'e', (byte)'n', (byte)'t', (byte)'r', (byte)'y', 4, (byte)'S', (byte)'a', (byte)'y', 3, (byte)'h', (byte)'i', 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(SayTypes.Entry, back.SayType);
        Assert.Equal(SayChannels.Say, back.SayChannel);
        Assert.Equal("hi", back.Text);
        Assert.False(back.SayVisible);
    }

    [Fact]
    public void ALongEntryFitsTheSayCapButNoOtherAction()
    {
        string text = new string('ж', Limits.MaxSayEntryChars); // two UTF-8 bytes each
        byte[] bytes = Codec.Encode(Say(SayTypes.Entry, SayChannels.Ooc, text));
        Assert.True(bytes.Length > Limits.MaxActionMessageBytes && bytes.Length <= Limits.MaxSayActionBytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal(text, back.Text);

        Assert.Throws<ProtocolException>(() => Codec.Encode(Say(SayTypes.Entry, SayChannels.Say, new string('a', Limits.MaxSayEntryChars + 1))));
        // a topic as long as a say is refused: only a say may be longer than MaxActionMessageBytes
        var topic = new ActionMessage(1, ActionKind.Topic, 0, ClickButton.None, ClickModifiers.None, new string('a', 400));
        Assert.Throws<ProtocolException>(() => Codec.Encode(topic));
    }

    [Fact]
    public void ThinkingCarriesItsFlagAndOnlyThinkingDoes()
    {
        byte[] bytes = Codec.Encode(Say(SayTypes.Thinking, visible: true));
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.True(back.SayVisible);
        Assert.Null(back.SayChannel);
        Assert.Throws<ProtocolException>(() => Codec.Encode(Say(SayTypes.Typing, visible: true)));
        bytes[^1] = 2;
        Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out _));
    }

    [Fact]
    public void UnknownTypesAndChannelsAreRefused()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(Say("shout", SayChannels.Say, "x")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Say(SayTypes.Entry, "Dead", "x")));
        byte[] bytes = Codec.Encode(Say(SayTypes.Entry, SayChannels.Say, "x"));
        bytes[5] = (byte)'E'; // "Entry"
        Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out _));
    }

    [Fact]
    public void SayModalHasItsBytesAndRoundTrips()
    {
        byte[] props = Codec.Encode(new SayModalMessage(SayModalCommand.Props, 1024, true));
        Assert.Equal(new byte[] { 19, 1, 0x80, 0x08, 1 }, props);
        Assert.True(Codec.TryDecode(props, out SayModalMessage back, out _));
        Assert.Equal(new SayModalMessage(SayModalCommand.Props, 1024, true), back);
        foreach (SayModalCommand command in new[] { SayModalCommand.Force, SayModalCommand.Save, SayModalCommand.Close })
        {
            byte[] bytes = Codec.Encode(new SayModalMessage(command, 0, false));
            Assert.Equal(new byte[] { 19, (byte)command }, bytes);
            Assert.True(Codec.TryDecode(bytes, out back, out _));
            Assert.Equal(command, back.Command);
        }
        Assert.False(Codec.TryDecode(new byte[] { 19, 5 }, out back, out _));
        Assert.False(Codec.TryDecode(new byte[] { 19, 1, 0x81, 0x08, 0 }, out back, out _)); // a cap over 1024
    }

    [Fact]
    public void RunechatHasItsBytesAndRoundTrips()
    {
        var line = new RunechatMessage(300, "<span class='maptext'>hi</span>", 50);
        byte[] bytes = Codec.Encode(line);
        Assert.Equal(20, bytes[0]);
        Assert.Equal(new byte[] { 0xAC, 0x02 }, bytes[1..3]);
        Assert.Equal(50, bytes[^1]);
        Assert.True(Codec.TryDecode(bytes, out RunechatMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(line, back);
        Assert.Throws<ProtocolException>(() => Codec.Encode(new RunechatMessage(0, "x", 50)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new RunechatMessage(1, "x", Limits.MaxRunechatLifespan + 1)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new RunechatMessage(1, "", 50)));
    }
}
