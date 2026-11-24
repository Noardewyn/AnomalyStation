using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 95 (package T, slice T4a, Codec.V95.cs): a click's and a throw's aim zone gains the eyes (7), the mouth (8) and the groin (9),
/// the modifiers byte's spare bit 3 its high bit: <c>zone = ((mods &gt;&gt; 4) &amp; 7) | (mods &amp; 8)</c>. Every byte of version 94 keeps its meaning.
/// </summary>
public class ProtocolV95AimZoneTests
{
    [Fact]
    public void TheVersionIs95OrLater() => Assert.True(ProtocolVersion.Current >= 95);

    [Fact]
    public void TheZonesAreTgsDollsPreciseZonesAfterTheSixLimbs()
    {
        Assert.Equal((7, 8, 9), ((int)AimZone.Eyes, (int)AimZone.Mouth, (int)AimZone.Groin));
        Assert.Equal(AimZone.Groin, AimZone.Last);
    }

    [Fact]
    public void EveryZoneRoundTripsOnAClickAndAThrowWithItsBitsInTheModifiersByte()
    {
        var aim = new ShotAim(800, 2048, 1300, 16384, -2000);
        for (int z = 0; z <= (int)AimZone.Last; z++)
        {
            var zone = (AimZone)z;
            byte zoneBits = (byte)(((z & 7) << 4) | (z & 8));
            var click = new ActionMessage(5, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.Shift | ClickModifiers.Alt, null)
                { TargetKind = TargetKind.Entity, IconX = 16, IconY = 26, AimZone = zone };
            byte[] bytes = Codec.Encode(click);
            Assert.Equal(new byte[] { 10, 5, 4, 1, 7, 1, (byte)(0x05 | zoneBits), 16, 26 }, bytes);
            Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(click, back);

            // a throw: a plain left click with the camera's aim (bit 7) after the zone's bits
            var thrown = new ActionMessage(7, ActionKind.Throw, 42, ClickButton.Left, ClickModifiers.None, null)
                { TargetKind = TargetKind.Entity, IconX = 12, IconY = 20, AimZone = zone, Aim = aim };
            bytes = Codec.Encode(thrown);
            Assert.Equal((byte)(0x80 | zoneBits), bytes[6]);
            Assert.True(Codec.TryDecode(bytes, out back, out error), error.ToString());
            Assert.Equal(thrown, back);
        }
    }

    [Fact]
    public void ZonesTenToFifteenAreRefusedBothWays()
    {
        byte[] good = Codec.Encode(new ActionMessage(5, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, IconX = 16, IconY = 26 });
        for (int z = 10; z <= 15; z++)
        {
            byte[] bytes = (byte[])good.Clone();
            bytes[6] = (byte)(((z & 7) << 4) | (z & 8));
            Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out ProtocolError error), $"zone {z}");
            Assert.Equal(ProtocolError.BadValue, error);
            var zone = (AimZone)z;
            Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(5, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, AimZone = zone }));
            Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(5, ActionKind.Throw, 7, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, AimZone = zone }));
        }
    }

    // Corrected by slice T4b (version 95 also): a hover on an entity now carries a zone (ProtocolV95HoverZoneTests); what stays is that a hover has no
    // modifiers byte, so a byte after its target is no zone of the modifiers' layout: 0x78 is refused as a zone of 120.
    [Fact]
    public void AHoverCarriesNoModifiersByte()
    {
        byte[] hover = Codec.Encode(new ActionMessage(5, ActionKind.Hover, 7, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity });
        Assert.True(Codec.TryDecode(hover, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(AimZone.None, back.AimZone);
        Assert.False(Codec.TryDecode(hover.Concat(new byte[] { 0x78 }).ToArray(), out ActionMessage _, out _));
    }

    // The host's own checks (ServerHost.ValidAction, which does not rely on the codec): a click or a throw takes zones 1 to 9; zone 10 and above is
    // refused. Corrected by slice T4b: a hover on an entity takes them too (it was refused before).
    [Theory]
    [InlineData(ActionKind.Click, AimZone.Eyes, true)]
    [InlineData(ActionKind.Click, AimZone.Mouth, true)]
    [InlineData(ActionKind.Click, AimZone.Groin, true)]
    [InlineData(ActionKind.Throw, AimZone.Groin, true)]
    [InlineData(ActionKind.Click, (AimZone)10, false)]
    [InlineData(ActionKind.Click, (AimZone)255, false)]
    [InlineData(ActionKind.Throw, (AimZone)10, false)]
    [InlineData(ActionKind.Hover, AimZone.Eyes, true)]
    [InlineData(ActionKind.Hover, (AimZone)10, false)]
    public void TheHostTakesZonesUpToTheGroinOnAClickAThrowOrAHover(ActionKind kind, AimZone zone, bool taken)
    {
        bool clicked = kind != ActionKind.Hover;
        var action = new ActionMessage(1, kind, 5, clicked ? ClickButton.Left : ClickButton.None, ClickModifiers.None, null)
            { TargetKind = TargetKind.Entity, IconX = clicked ? (byte)16 : (byte)0, IconY = clicked ? (byte)26 : (byte)0, AimZone = zone };
        System.Reflection.MethodInfo valid = typeof(ServerHost).GetMethod("ValidAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(valid);
        object[] args = { action, null };
        Assert.Equal(taken, (bool)valid.Invoke(null, args));
        Assert.Equal(taken, args[1] == null); // the reason refused
    }

    [Fact]
    public void EveryModifiersByteOfVersion94DecodesAsBefore()
    {
        byte[] good = Codec.Encode(new ActionMessage(5, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity, IconX = 16, IconY = 26 });
        for (int mods = 0; mods < 0x80; mods++)
        {
            if ((mods & 0x08) != 0 || ((mods >> 4) & 7) > 6)
                continue; // bit 3 set or a zone of 7: version 94 refused them
            byte[] bytes = (byte[])good.Clone();
            bytes[6] = (byte)mods;
            Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), $"0x{mods:X2}: {error}");
            // version 94's reading: bits 0-2 the modifiers, bits 4-6 the zone
            Assert.Equal(((ClickModifiers)(mods & 7), (AimZone)((mods >> 4) & 7)), (back.Modifiers, back.AimZone));
            Assert.Equal(bytes, Codec.Encode(back));
        }
    }
}
