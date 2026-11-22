using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 37 (M8z slice B, D238, docs/ai/specs/protocol.md section 46): a context hint's action may be a drop of the hinted mob
/// onto the own mob (the radial's Strip). Bytes, round trip, the refusal of the first unknown action.
/// </summary>
public class ProtocolV37Tests
{
    private static ContextHintsMessage Man(params ContextHint[] hints)
    {
        var m = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "man" };
        m.Hints.AddRange(hints);
        return m;
    }

    [Fact]
    public void TheVersionIsAtLeast37()
    {
        Assert.True(ProtocolVersion.Current >= 37); // BuildInfoTests pins the current one
    }

    [Fact]
    public void StripIsActionThreeAfterTheModifiers()
    {
        Assert.Equal(3, (byte)ContextHintAction.DropOntoSelf);
        ContextHintsMessage m = Man(new ContextHint(ClickButton.Left, ClickModifiers.None, "Strip", ContextHintAction.DropOntoSelf));
        byte[] bytes = Codec.Encode(m);
        byte[] expected = new byte[] { 11, 1, 7, 4 }.Concat("man"u8.ToArray()).Concat(new byte[] { 1, 1, 0, 3, 6 }).Concat("Strip"u8.ToArray()).ToArray();
        Assert.Equal(expected, bytes);
        Assert.True(Codec.TryDecode(bytes, out ContextHintsMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(m.Hints, decoded.Hints);
    }

    [Fact]
    public void TheActionAfterStripIsRefusedBothWays()
    {
        byte[] bytes = Codec.Encode(Man(new ContextHint(ClickButton.Left, ClickModifiers.None, "Strip", ContextHintAction.DropOntoSelf)));
        const int action = 4 + 3 + 3; // id, kind, target, name length; "man"; count, button, modifiers
        Assert.Equal((byte)ContextHintAction.DropOntoSelf, bytes[action]);
        bytes[action] = (byte)ContextHintAction.Last + 1;
        Assert.False(Codec.TryDecode(bytes, out ContextHintsMessage _, out _));
        Assert.ThrowsAny<Exception>(() => Codec.Encode(Man(new ContextHint(ClickButton.Left, ClickModifiers.None, "x", (ContextHintAction)((byte)ContextHintAction.Last + 1)))));
    }
}
