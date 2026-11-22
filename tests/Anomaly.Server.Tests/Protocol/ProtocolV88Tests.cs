using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 88 (DV527): an aim ends with the render tick its client drew the world at, 1/16 tick, as a varuint.</summary>
public class ProtocolV88Tests
{
    [Fact]
    public void AnAimCarriesTheTickItsClientDrew()
    {
        var click = new ActionMessage(3, ActionKind.Click, 9, ClickButton.Left, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Entity,
            IconX = 16,
            IconY = 27,
            Aim = new ShotAim(256, 512, 1300, 16384, -2, Seen: 1234 * 16 + 8),
        };
        byte[] bytes = Codec.Encode(click);
        // 19752 as a varuint: 0xA8 0x9A 0x01
        Assert.Equal(new byte[] { 0xA8, 0x9A, 0x01 }, bytes[^3..]);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(click.Aim, back.Aim);
        Assert.Equal(1234.5, back.Aim.Value.SeenTicks);
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Aim = click.Aim.Value with { Seen = -1 } }));
    }
}
