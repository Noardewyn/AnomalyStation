using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 36 (M8y slice D, D230, docs/ai/specs/protocol.md section 45): a context hint carries its action (a click, a drop of the own
/// mob onto the target, the stop-pulling keybinding). Bytes, round trip, the refusal of an unknown action.
/// </summary>
public class ProtocolV36Tests
{
    private static ContextHintsMessage Chair(params ContextHint[] hints)
    {
        var m = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "chair" };
        m.Hints.AddRange(hints);
        return m;
    }

    [Fact]
    public void TheVersionIsAtLeast36()
    {
        Assert.True(ProtocolVersion.Current >= 36); // BuildInfoTests pins the current one
    }

    [Fact]
    public void AHintCarriesItsActionAfterTheModifiers()
    {
        ContextHintsMessage m = Chair(new ContextHint(ClickButton.Left, ClickModifiers.None, "Buckle", ContextHintAction.DropSelf),
            new ContextHint(ClickButton.Left, ClickModifiers.Ctrl, "Pull"),
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Stop pulling", ContextHintAction.StopPulling));
        byte[] bytes = Codec.Encode(m);
        byte[] expected = new byte[] { 11, 1, 7, 6 }.Concat("chair"u8.ToArray()).Concat(new byte[] { 3, 1, 0, 1, 7 }).Concat("Buckle"u8.ToArray())
            .Concat(new byte[] { 1, 2, 0, 5 }).Concat("Pull"u8.ToArray()).Concat(new byte[] { 1, 0, 2, 13 }).Concat("Stop pulling"u8.ToArray()).ToArray();
        Assert.Equal(expected, bytes);
        Assert.True(Codec.TryDecode(bytes, out ContextHintsMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(m.Hints, decoded.Hints);
    }

    [Fact]
    public void AnUnknownActionIsRefusedBothWays()
    {
        byte[] bytes = Codec.Encode(Chair(new ContextHint(ClickButton.Left, ClickModifiers.None, "Buckle", ContextHintAction.DropSelf)));
        const int action = 4 + 5 + 3; // id, kind, target, name length; "chair"; count, button, modifiers
        Assert.Equal((byte)ContextHintAction.DropSelf, bytes[action]);
        bytes[action] = (byte)ContextHintAction.Last + 1;
        Assert.False(Codec.TryDecode(bytes, out ContextHintsMessage _, out _));
        Assert.ThrowsAny<Exception>(() => Codec.Encode(Chair(new ContextHint(ClickButton.Left, ClickModifiers.None, "x", (ContextHintAction)((int)ContextHintAction.Last + 1)))));
    }
}
