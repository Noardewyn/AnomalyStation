using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 40 (M9 slice E, docs/ai/specs/protocol.md section 49): the RadioChannels message (the worn headset's radio prefixes,
/// the chat window's Radio list) and the Message button's keybinding code. Bytes, round trips, the prefixes' rules, the replica.
/// </summary>
public class ProtocolV40Tests
{
    [Fact]
    public void TheIdAndTheCodeFollowVersion39()
    {
        Assert.True(ProtocolVersion.Current >= 40);
        Assert.Equal(21, (int)MessageId.RadioChannels);
        Assert.True(WireReader.IsKnownMessageId(21));
        Assert.Equal(14, (int)KeybindingCode.OpenMessenger);
        Assert.True(KeybindingCode.Last >= KeybindingCode.OpenMessenger);
    }

    [Fact]
    public void RadioChannelsHaveTheirBytesAndRoundTrip()
    {
        var message = new RadioChannelsMessage();
        message.Prefixes.Add(":e ");
        message.Prefixes.Add(":c ");
        byte[] bytes = Codec.Encode(message);
        // id 21, count 2, two strings (a string's length plus one)
        Assert.Equal(new byte[] { 21, 2, 4, (byte)':', (byte)'e', (byte)' ', 4, (byte)':', (byte)'c', (byte)' ' }, bytes);
        Assert.True(Codec.TryDecode(bytes, out RadioChannelsMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(new[] { ":e ", ":c " }, back.Prefixes);
        Assert.True(Codec.TryDecode(Codec.Encode(new RadioChannelsMessage()), out back, out _));
        Assert.Empty(back.Prefixes);
    }

    [Fact]
    public void APrefixIsAColonAKeyAndASpaceOnce()
    {
        Assert.True(RadioPrefixes.IsValid(":0 "));
        Assert.False(RadioPrefixes.IsValid(":E "));
        Assert.False(RadioPrefixes.IsValid(";"));
        Assert.False(RadioPrefixes.IsValid(":e"));
        var twice = new RadioChannelsMessage();
        twice.Prefixes.Add(":e ");
        twice.Prefixes.Add(":e ");
        Assert.Throws<ProtocolException>(() => Codec.Encode(twice));
        var bad = new RadioChannelsMessage();
        bad.Prefixes.Add(".e ");
        Assert.Throws<ProtocolException>(() => Codec.Encode(bad));
        var one = new RadioChannelsMessage();
        one.Prefixes.Add(":e ");
        byte[] bytes = Codec.Encode(one);
        bytes[3] = (byte)'.';
        Assert.False(Codec.TryDecode(bytes, out RadioChannelsMessage _, out _));
    }

    [Fact]
    public void TheReplicaKeepsTheLastList()
    {
        var replica = new ReplicaWorld();
        IReadOnlyList<string> raised = null;
        replica.RadioChannelsChanged += p => raised = p;
        var message = new RadioChannelsMessage();
        message.Prefixes.Add(":m ");
        Assert.True(replica.TryApply(Codec.Encode(message), out MessageId applied, out _));
        Assert.Equal(MessageId.RadioChannels, applied);
        Assert.Equal(new[] { ":m " }, replica.RadioPrefixes);
        Assert.Same(replica.RadioPrefixes, raised);
    }

    [Fact]
    public void TheMessageButtonIsAKeybindingCode()
    {
        var press = new ActionMessage(3, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.OpenMessenger };
        byte[] bytes = Codec.Encode(press);
        Assert.Equal(new byte[] { 10, 3, 6, 14, 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal(KeybindingCode.OpenMessenger, back.Keybinding);
        bytes[3] = (byte)(KeybindingCode.Last + 1);
        Assert.False(Codec.TryDecode(bytes, out back, out _));
    }
}
