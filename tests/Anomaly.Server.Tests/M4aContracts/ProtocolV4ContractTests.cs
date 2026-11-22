using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server;

namespace Anomaly.Server.Tests.M4aContracts;

/// <summary>The M4a contracts step's protocol version 4 (MessagesV4.cs, the version-4 members, Limits) and the give syntax.</summary>
public class ProtocolV4ContractTests
{
    [Fact]
    public void TheVersionIsAtLeastFour() => Assert.True(ProtocolVersion.Current >= 4); // M4b slice 3: version 5

    [Fact]
    public void TheFlagsFieldIsAChangeableBit()
    {
        Assert.Equal(1u << 15, (uint)NetField.Flags);
        Assert.Equal(NetField.Flags, NetField.Changeable & NetField.Flags);
        Assert.Equal(NetField.Flags, NetField.All & NetField.Flags);
        Assert.Equal(EntityFlags.Thrown | EntityFlags.Lunge | EntityFlags.Hit | EntityFlags.Pushable | EntityFlags.LungeDir, EntityFlags.All); // the lunge: the owner's play test (2026-10-09); Pushable: version 56
        Assert.Equal(0xFF, (int)EntityFlags.All); // every bit of the byte is defined since version 56
    }

    [Fact]
    public void TheHudGroupsAreTgs()
    {
        Assert.Equal(Defines.HUD_GROUP_STATIC, HudGroup.Static);
        Assert.Equal(Defines.HUD_GROUP_TOGGLEABLE_INVENTORY, HudGroup.ToggleableInventory);
        Assert.Equal(Defines.HUD_GROUP_INFO, HudGroup.Info);
        Assert.Equal(Defines.HUD_GROUP_SCREEN_OVERLAYS, HudGroup.ScreenOverlays);
        Assert.Equal(Defines.HUD_GROUP_HOTKEYS, HudGroup.Hotkeys);
        Assert.Equal(Defines.HUD_GROUP_STORAGE, HudGroup.Storage);
        Assert.Equal(Defines.SCREEN_GROUP_AMT, HudGroup.Last);
    }

    [Fact]
    public void EveryKeybindingCodeHasItsTgKeybinding()
    {
        // The protocol's codes and the core's table are one table (ClientSeam.KeybindingType).
        string[] expected =
        {
            "SwapHands:datum_keybinding_mob_swap_hands", "SelectRightHand:datum_keybinding_mob_select_hand_right",
            "SelectLeftHand:datum_keybinding_mob_select_hand_left", "ActivateInhand:datum_keybinding_mob_activate_inhand",
            "DropItem:datum_keybinding_mob_drop_item", "QuickEquip:datum_keybinding_human_quick_equip",
            "ToggleThrowMode:datum_keybinding_living_toggle_throw_mode", "HoldThrowMode:datum_keybinding_living_hold_throw_mode",
            "StopPulling:datum_keybinding_mob_stop_pulling", "Resist:datum_keybinding_living_resist",
        };
        var codes = new (string Name, KeybindingCode Code)[]
        {
            (nameof(KeybindingCode.SwapHands), KeybindingCode.SwapHands), (nameof(KeybindingCode.SelectRightHand), KeybindingCode.SelectRightHand),
            (nameof(KeybindingCode.SelectLeftHand), KeybindingCode.SelectLeftHand), (nameof(KeybindingCode.ActivateInhand), KeybindingCode.ActivateInhand),
            (nameof(KeybindingCode.DropItem), KeybindingCode.DropItem), (nameof(KeybindingCode.QuickEquip), KeybindingCode.QuickEquip),
            (nameof(KeybindingCode.ToggleThrowMode), KeybindingCode.ToggleThrowMode), (nameof(KeybindingCode.HoldThrowMode), KeybindingCode.HoldThrowMode),
            (nameof(KeybindingCode.StopPulling), KeybindingCode.StopPulling), // version 20 (pulling)
            (nameof(KeybindingCode.Resist), KeybindingCode.Resist), // version 37 (M8z)
        };
        string[] actual = codes.Select(c => c.Name + ":" + ClientSeam.KeybindingType((byte)c.Code).Name).ToArray();
        Assert.Equal(expected, actual);
        // version 37 (D244): the two codes after the keybindings are the hidden verbs Sleep and Succumb, no keybinding type
        Assert.Equal((byte)KeybindingCode.VerbSleep, (byte)(codes.Length + 1));
        Assert.Equal(KeybindingCode.Scoreboard, KeybindingCode.Last); // version 93 (DV585): the scoreboard key (version 83, DV422): the level keys (version 59: view pet commands; version 41: give; version 40: the Message button)
        Assert.Equal(17, (int)KeybindingCode.ZLevelUp);
        Assert.Equal(typeof(datum_keybinding_living_view_pet_data), ClientSeam.KeybindingType((byte)KeybindingCode.ViewPetCommands));
        Assert.Equal(typeof(datum_keybinding_living_give), ClientSeam.KeybindingType((byte)KeybindingCode.GiveItem));
        Assert.Null(ClientSeam.KeybindingType((byte)KeybindingCode.VerbEmotePanel));
        Assert.Null(ClientSeam.KeybindingType((byte)KeybindingCode.OpenMessenger));
        Assert.Null(ClientSeam.KeybindingType((byte)KeybindingCode.VerbSleep));
        Assert.Null(ClientSeam.KeybindingType((byte)KeybindingCode.VerbSuccumb));
        Assert.Null(ClientSeam.KeybindingType((byte)KeybindingCode.Last + 1));
    }

    [Fact]
    public void AVersionThreeClickIsAnEntityClick()
    {
        var v3 = new ActionMessage(1, ActionKind.Click, 42, ClickButton.Left, ClickModifiers.None, null);
        Assert.Equal(TargetKind.None, v3.TargetKind);
        Assert.Equal(TargetKind.Entity, v3.EffectiveTargetKind);
        var turf = v3 with { Target = 0, TargetKind = TargetKind.Turf, TurfX = 5, TurfY = 6, TurfZ = 1, IconX = 16, IconY = 8 };
        Assert.Equal(TargetKind.Turf, turf.EffectiveTargetKind);
        Assert.Equal(TargetKind.None, new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null).EffectiveTargetKind);
        Assert.True(Limits.MaxActionMessageBytes <= Limits.MaxInboundMessageBytes);
        Assert.True(Limits.MaxContextHintsMessageBytes < Limits.MaxServerTextBytes);
    }

    [Theory]
    [InlineData("give me /obj/item/card/id/advanced/debug", "me", "/obj/item/card/id/advanced/debug")]
    [InlineData("GIVE Walker /obj/item/weldingtool", "Walker", "/obj/item/weldingtool")]
    public void GiveTakesWhoAndAnItemPath(string line, string who, string path)
    {
        Assert.True(DevCommandLine.TryParse(line, out DevCommand command, out string error), error);
        Assert.Equal(DevVerb.Give, command.Verb);
        Assert.Equal(who, command.Who);
        Assert.Equal(path, command.TypePath);
    }

    [Theory]
    [InlineData("give me")]
    [InlineData("give me card")]
    [InlineData("give me /mob/living/carbon/human")]
    [InlineData("give me /obj/item/../mob")]
    [InlineData("give me /obj/item/card id")]
    public void GiveRefusesWhatIsNotAnItemPath(string line)
    {
        Assert.False(DevCommandLine.TryParse(line, out _, out string error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TheHelpNamesGive() => Assert.Contains("give WHO TYPE", DevCommandLine.Help);
}
