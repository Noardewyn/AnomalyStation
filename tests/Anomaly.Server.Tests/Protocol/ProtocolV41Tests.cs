using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 41 (M9 slice F, docs/ai/specs/protocol.md section 50): tg's give keybinding, code 15.</summary>
public class ProtocolV41Tests
{
    [Fact]
    public void TheGiveKeyIsTheLastKeybindingCode()
    {
        Assert.True(ProtocolVersion.Current >= 41);
        Assert.Equal(15, (int)KeybindingCode.GiveItem);
        Assert.True(KeybindingCode.Last >= KeybindingCode.GiveItem);
        var press = new ActionMessage(3, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.GiveItem };
        byte[] bytes = Codec.Encode(press);
        Assert.Equal(new byte[] { 10, 3, 6, 15, 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out _));
        Assert.Equal(KeybindingCode.GiveItem, back.Keybinding);
        bytes[3] = (byte)(KeybindingCode.Last + 1);
        Assert.False(Codec.TryDecode(bytes, out back, out _));
    }
}
