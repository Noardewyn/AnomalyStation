using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 82 (D329): the drop key's PutDown action carries a click's target, pixel and aim and is a plain left click.</summary>
public class ProtocolV82Tests
{
    [Fact]
    public void APutDownCarriesAClicksTargetPixelAndAimAndIsAPlainLeftClick()
    {
        var aim = new ShotAim(800, 2048, 1300, 16384, -2000);
        var onTable = new ActionMessage(7, ActionKind.PutDown, 42, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, IconX = 12, IconY = 20, Aim = aim };
        Assert.True(Codec.TryDecode(Codec.Encode(onTable), out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(onTable, back);
        var onFloor = new ActionMessage(8, ActionKind.PutDown, 0, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 4, TurfY = 4, TurfZ = 2, Aim = aim };
        Assert.True(Codec.TryDecode(Codec.Encode(onFloor), out back, out error), error.ToString());
        Assert.Equal(onFloor, back);
        Assert.Throws<ProtocolException>(() => Codec.Encode(onTable with { Button = ClickButton.Right }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(onTable with { Modifiers = ClickModifiers.Shift }));
    }
}
