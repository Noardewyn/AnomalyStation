using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 39 (M9 slice B, docs/ai/specs/protocol.md section 48): the Emote action (the emote wheel's pick, tg's emote keybinding)
/// and the emote panel's verb code. Bytes, round trips, the key's rules.
/// </summary>
public class ProtocolV39Tests
{
    private static ActionMessage Emote(string key) => new(7, ActionKind.Emote, 0, ClickButton.None, ClickModifiers.None, key);

    [Fact]
    public void TheKindAndTheCodeFollowVersion38()
    {
        Assert.True(ProtocolVersion.Current >= 39);
        Assert.Equal(19, (int)ActionKind.Emote);
        Assert.True(ActionKinds.Last >= ActionKind.Emote);
        Assert.Equal(13, (int)KeybindingCode.VerbEmotePanel);
        Assert.True(KeybindingCode.Last >= KeybindingCode.VerbEmotePanel);
    }

    [Fact]
    public void AnEmoteHasItsBytesAndRoundTrips()
    {
        byte[] bytes = Codec.Encode(Emote("wave"));
        // id 10, seq 7, kind 19, "wave" (a string's length plus one)
        Assert.Equal(new byte[] { 10, 7, 19, 5, (byte)'w', (byte)'a', (byte)'v', (byte)'e' }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(ActionKind.Emote, back.Kind);
        Assert.Equal("wave", back.Text);
        Assert.True(Codec.TryDecode(Codec.Encode(Emote("blink_r")), out back, out _));
        Assert.Equal("blink_r", back.Text);
    }

    [Fact]
    public void AKeyIsAShortLowercaseWord()
    {
        Assert.True(EmoteKeys.IsValid("scratchhead"));
        Assert.False(EmoteKeys.IsValid(null));
        Assert.False(EmoteKeys.IsValid(""));
        Assert.False(EmoteKeys.IsValid("Wave"));
        Assert.False(EmoteKeys.IsValid("wave at bob"));
        Assert.False(EmoteKeys.IsValid(new string('a', Limits.MaxEmoteKeyBytes + 1)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Emote("Wave")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Emote(null)));
        byte[] bytes = Codec.Encode(Emote("wave"));
        bytes[4] = (byte)'W';
        Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out _));
    }

    [Fact]
    public void TheEmotePanelIsAKeybindingCode()
    {
        var press = new ActionMessage(3, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.VerbEmotePanel };
        byte[] bytes = Codec.Encode(press);
        Assert.Equal(new byte[] { 10, 3, 6, 13, 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal(KeybindingCode.VerbEmotePanel, back.Keybinding);
        bytes[3] = (byte)(KeybindingCode.Last + 1); // versions 40 and 41 added codes 14 and 15
        Assert.False(Codec.TryDecode(bytes, out back, out _));
    }
}
