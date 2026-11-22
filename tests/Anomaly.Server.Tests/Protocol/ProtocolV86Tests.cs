using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 86 (DV503): a context hint's Extra action and the ContextPick it sends, a target and the entry's label.</summary>
public class ProtocolV86Tests
{
    [Fact]
    public void AContextPickCarriesItsTargetAndLabel()
    {
        var pick = new ActionMessage(9, ActionKind.ContextPick, 42, ClickButton.None, ClickModifiers.None, "Flip") { TargetKind = TargetKind.Entity };
        Assert.True(Codec.TryDecode(Codec.Encode(pick), out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(pick, back);
        Assert.Equal(ActionKind.ContextPick, ActionKinds.Last);
        Assert.Equal(ContextHintAction.Extra, ContextHintAction.Last);
        Assert.Throws<ProtocolException>(() => Codec.Encode(pick with { Text = null }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(pick with { Target = 0, TargetKind = TargetKind.None }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(pick with { Button = ClickButton.Left }));

        var hints = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 42, Name = "table" };
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Flip", ContextHintAction.Extra));
        Assert.True(Codec.TryDecode(Codec.Encode(hints), out ContextHintsMessage hintsBack, out error), error.ToString());
        Assert.Equal(ContextHintAction.Extra, Assert.Single(hintsBack.Hints).Action);
    }
}
