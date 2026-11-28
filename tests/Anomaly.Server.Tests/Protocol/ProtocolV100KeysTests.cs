using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 100 also (ui-overhaul.md section 12, slice 4E): the world interact action (DV697), a thing or a turf and nothing else, and the
/// five quick-equip keybinding codes of tg's quick_equip_belt keybindings.
/// </summary>
public class ProtocolV100KeysTests
{
    [Fact]
    public void AWorldInteractCarriesAThingOrATurfAndNothingElse()
    {
        var onThing = new ActionMessage(9, ActionKind.Interact, 42, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity };
        Assert.True(Codec.TryDecode(Codec.Encode(onThing), out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(onThing, back);
        var onTurf = new ActionMessage(9, ActionKind.Interact, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 3, TurfY = 4, TurfZ = 2 };
        Assert.True(Codec.TryDecode(Codec.Encode(onTurf), out back, out error), error.ToString());
        Assert.Equal(onTurf, back);
        Assert.Equal(ActionKind.Interact, ActionKinds.Last);

        Assert.Throws<ProtocolException>(() => Codec.Encode(onThing with { TargetKind = TargetKind.Screen }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(onThing with { Target = 0, TargetKind = TargetKind.None }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(onThing with { Button = ClickButton.Left }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(onThing with { Modifiers = ClickModifiers.Alt }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(onThing with { Text = "x" }));
    }

    [Fact]
    public void TheQuickEquipKeybindingCodesRoundTripAndNameTgsKeybindings()
    {
        var expected = new Dictionary<KeybindingCode, Type>
        {
            [KeybindingCode.QuickEquipBag] = typeof(datum_keybinding_human_quick_equip_belt_quick_equip_bag),
            [KeybindingCode.QuickEquipBelt] = typeof(datum_keybinding_human_quick_equip_belt),
            [KeybindingCode.QuickEquipLeftPocket] = typeof(datum_keybinding_human_quick_equip_belt_quick_equip_lpocket),
            [KeybindingCode.QuickEquipRightPocket] = typeof(datum_keybinding_human_quick_equip_belt_quick_equip_rpocket),
            [KeybindingCode.QuickEquipSuitStorage] = typeof(datum_keybinding_human_quick_equip_belt_quick_equip_suit_storage),
        };
        Assert.Equal(KeybindingCode.QuickEquipSuitStorage, KeybindingCode.Last);
        foreach ((KeybindingCode code, Type kind) in expected)
        {
            var press = new ActionMessage(9, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = code };
            Assert.True(Codec.TryDecode(Codec.Encode(press), out ActionMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(code, back.Keybinding);
            Assert.Equal(kind, ClientSeam.KeybindingType((byte)code));
        }
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(9, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.Last + 1 }));
    }
}
