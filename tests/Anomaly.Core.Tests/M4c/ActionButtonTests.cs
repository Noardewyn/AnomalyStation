using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 3 (D146): tg's item action buttons (datums/actions/action.dm, item_action.dm, items/toggles.dm, items/adjust.dm,
/// items/set_internals.dm, components/action_item_overlay.dm, _onclick/hud/screen_objects/action_button.dm, action_group.dm and
/// the hud's action procs, items.dm's add_item_action and the equip and drop hooks). An item's actions are granted to the mob
/// that equips it into an allowed slot (the tank in any slot, the magboots on the feet), each a button in client.screen at tg's
/// listed position with the hud's background and the item's look; dropping the item takes the button away. A click on the
/// button is the action's Trigger: the item's ui_action_click (the tank's internals, the suit's regulator) or attack_self (the
/// magboots); the button's background, icon and overlay follow the state. BYOND's MouseExited (protocol version 14) clears
/// the hover states MouseEntered set.
/// </summary>
public class ActionButtonTests
{
    private const int Second = 20;

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, object slot) where T : obj_item
        => r.In(() =>
        {
            T item = Dm.@new<T>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_or_del(item, slot))), typeof(T).Name + " goes on");
            return item;
        });

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
        => r.In(() =>
        {
            T item = Dm.@new<T>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item))), typeof(T).Name + " is in a hand");
            return item;
        });

    private static atom_movable_screen_movable_action_button ButtonOf(AudioRoom r, mob who, obj_item item)
        => r.In(() =>
        {
            datum_action action = Assert.Single(Dm.each_cast<datum_action>(item.actions));
            return action.viewers[who.hud_used] as atom_movable_screen_movable_action_button;
        });

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob;
        try { bob = r.Join(4, 4, "Bob"); }
        catch (Exception e) { throw new Exception(string.Join("\n", r.Room.RuntimeText), e); }
        r.Step(Second);
        return (r, bob);
    }

    private static void ClickButton(AudioRoom r, mob who, atom_movable_screen_movable_action_button button)
    {
        r.Command(() => ClientSeam.Click(who.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second);
    }

    private static List<string> UnderlayStates(atom a)
        => Dm.each_cast<object>(a.underlays).Select(u => ((IDmAppearance)u).icon_state).ToList();

    private static List<string> OverlayStates(atom a)
        => Dm.each_cast<object>(a.overlays).Select(o => ((IDmAppearance)o).icon_state).ToList();

    [Fact]
    public void ATanksButtonAppearsInTheHandAtTheListsFirstPlaceAndGoesWhenDropped()
    {
        var (r, bob) = Room(7501);
        var tank = Held<obj_item_tank_internals_oxygen>(r, bob);
        atom_movable_screen_movable_action_button button = ButtonOf(r, bob, tank);
        Assert.NotNull(button);
        r.In(() =>
        {
            datum_action action = button.linked_action;
            Assert.IsType<datum_action_item_action_set_internals>(action);
            Assert.Same(bob, action.owner);
            Assert.Contains(action, Dm.each_cast<datum_action>(bob.actions));
            Assert.True(Dm.@in(button, bob.client.screen), "the button is on the player's screen");
            Assert.Equal("Set Internals", button.name);
            // SCRN_OBJ_INSERT_FIRST: the listed group's first place (action_group.dm:69, column 0, row 0)
            Assert.Equal(SCRN_OBJ_IN_LIST, button.location);
            Assert.Equal("WEST+0:6,NORTH+0:-6", button.screen_loc);
            // the hud's background (get_action_buttons_icons: the ui style's "template"), the gold border and the tank's look
            Assert.Equal(new[] { "template" }, UnderlayStates(button));
            Assert.Contains("ab_goldborder", OverlayStates(button));
            Assert.Contains(tank.icon_state as string, OverlayStates(button));
            Assert.Equal("#ffffffff", button.color as string); // available: not reddened
            // the palette stays hidden with nothing in it (INVISIBILITY_ABSTRACT, action_button.dm:328)
            var palette = bob.hud_used.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette;
            Assert.NotNull(palette);
            Assert.Equal(INVISIBILITY_ABSTRACT, palette.invisibility);
            Assert.Equal("WEST+0:23,NORTH-1:5", palette.screen_loc);
        });

        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(tank)))));
        r.In(() =>
        {
            datum_action action = Assert.Single(Dm.each_cast<datum_action>(tank.actions));
            Assert.Null(action.owner);
            Assert.False(Dm.@in(button, bob.client.screen), "the button left the screen");
            Assert.True(QDELETED(button), "the button is deleted");
            Assert.Equal(0f, Dm.length(bob.actions));
            Assert.Null((bob.hud_used.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen_button_palette).screen_loc); // no actions: the palette goes too
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ClickingTheTanksButtonOpensAndClosesTheInternalsAndTheBackgroundFollows()
    {
        var (r, bob) = Room(7502);
        Wear<obj_item_clothing_mask_breath>(r, bob, ITEM_SLOT_MASK);
        var tank = Wear<obj_item_tank_internals_oxygen>(r, bob, ITEM_SLOT_BACK);
        atom_movable_screen_movable_action_button button = ButtonOf(r, bob, tank);
        Assert.NotNull(button);
        Assert.Null(r.In(() => bob.@internal));

        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Same(tank, bob.@internal);
            // set_internals' is_action_active: the hud's active background (open_internals' update_mob_action_buttons)
            Assert.Equal(new[] { "template_active" }, UnderlayStates(button));
        });
        Assert.Single(r.Sounds.Of(bob, "sound/items/internals/internals_on.ogg"));

        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Null(bob.@internal);
            Assert.Equal(new[] { "template" }, UnderlayStates(button));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMagbootsButtonComesOnlyWithTheBootsOnTheFeetAndTogglesThePulse()
    {
        var (r, bob) = Room(7503);
        var held = Held<obj_item_clothing_shoes_magboots>(r, bob);
        // in a hand the boots give no button (item_action_slot_check: slot_flags is the feet)
        r.In(() =>
        {
            datum_action action = Assert.Single(Dm.each_cast<datum_action>(held.actions));
            Assert.Null(action.owner);
            Assert.Equal(0f, Dm.length(bob.actions));
        });
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(held, ITEM_SLOT_FEET)))));
        atom_movable_screen_movable_action_button button = ButtonOf(r, bob, held);
        Assert.NotNull(button);
        r.In(() =>
        {
            Assert.IsType<datum_action_item_action_toggle>(button.linked_action);
            Assert.Equal("Toggle magboots", button.name);
            Assert.Contains("magboots0", OverlayStates(button));
            Assert.Equal(FALSE, held.magpulse);
        });

        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Equal(TRUE, held.magpulse);
            Assert.True(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY), "the pulse is on");
            Assert.Equal("magboots1", held.icon_state as string);
            // the target's icon update forces the item overlay again (action.dm:407, action_item_overlay.dm:66)
            Assert.Contains("magboots1", OverlayStates(button));
            Assert.DoesNotContain("magboots0", OverlayStates(button));
        });

        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Equal(FALSE, held.magpulse);
            Assert.Contains("magboots0", OverlayStates(button));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSuitsRegulatorButtonTurnsItOnAndShowsThermalOn()
    {
        var (r, bob) = Room(7504);
        var suit = Wear<obj_item_clothing_suit_space_eva>(r, bob, ITEM_SLOT_OCLOTHING);
        var helmet = Wear<obj_item_clothing_head_helmet_space_eva>(r, bob, ITEM_SLOT_HEAD);
        atom_movable_screen_movable_action_button button = ButtonOf(r, bob, suit);
        atom_movable_screen_movable_action_button visor = ButtonOf(r, bob, helmet);
        r.In(() =>
        {
            Assert.IsType<datum_action_item_action_toggle_spacesuit>(button.linked_action);
            Assert.Equal("Toggle Suit Thermal Regulator", button.name);
            Assert.Equal("thermal_off", button.icon_state as string);
            Assert.Equal("icons/mob/actions/actions_spacesuit.dmi", Dm.text("[]", button.icon));
            Assert.IsType<datum_action_item_action_adjust_visor>(visor.linked_action);
            Assert.StartsWith("Adjust ", visor.name);
            Assert.EndsWith("EVA helmet Visor", visor.name); // the helmet's name carries BYOND's \improper marker
            // the two buttons share the listed group: the second place
            Assert.Contains(button.screen_loc, new[] { "WEST+0:6,NORTH+0:-6", "WEST+1:8,NORTH+0:-6" });
            Assert.NotEqual(button.screen_loc, visor.screen_loc);
        });

        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Equal(TRUE, suit.thermal_on);
            Assert.Equal("thermal_on", button.icon_state as string); // toggle_spacesuit's update_item_action_buttons
        });
        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Equal(FALSE, suit.thermal_on);
            Assert.Equal("thermal_off", button.icon_state as string);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBreathMasksButtonPushesItUpAndCutsTheInternals()
    {
        var (r, bob) = Room(7505);
        var mask = Wear<obj_item_clothing_mask_breath>(r, bob, ITEM_SLOT_MASK);
        var tank = Wear<obj_item_tank_internals_oxygen>(r, bob, ITEM_SLOT_BACK);
        r.In(() => Waiting.NoSleep(bob.toggle_internals(tank)));
        Assert.Same(tank, r.In(() => bob.@internal));
        atom_movable_screen_movable_action_button button = ButtonOf(r, bob, mask);
        r.In(() =>
        {
            Assert.IsType<datum_action_item_action_adjust>(button.linked_action);
            Assert.Equal("Adjust breath mask", button.name);
        });
        ClickButton(r, bob, button);
        r.In(() =>
        {
            Assert.Equal(TRUE, mask.up);
            Assert.Equal("breath_up", mask.icon_state as string);
            Assert.Contains("breath_up", OverlayStates(button));
        });
        r.Step(5 * Second); // the next breath cuts the tank off (S1: a mask pushed up)
        Assert.Null(r.In(() => bob.@internal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnavailableButtonIsReddenedAndItsClickDoesNothing()
    {
        var (r, bob) = Room(7506);
        var boots = Wear<obj_item_clothing_shoes_magboots>(r, bob, ITEM_SLOT_FEET);
        atom_movable_screen_movable_action_button button = ButtonOf(r, bob, boots);
        // AB_CHECK_HANDS_BLOCKED (item_action.dm:4): handcuffed hands block the button; its status follows the trait signal
        r.In(() => ADD_TRAIT(bob, TRAIT_HANDS_BLOCKED, "test"));
        r.In(() => Assert.Equal("#80000080", button.color as string)); // transparent_when_unavailable: rgb(128,0,0,128)
        ClickButton(r, bob, button);
        r.In(() => Assert.Equal(FALSE, boots.magpulse));
        r.In(() => REMOVE_TRAIT(bob, TRAIT_HANDS_BLOCKED, "test"));
        r.In(() => Assert.Equal("#ffffffff", button.color as string));
        ClickButton(r, bob, button);
        r.In(() => Assert.Equal(TRUE, boots.magpulse));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void DeletingTheItemOrTheHudTakesTheButtonsAway()
    {
        var (r, bob) = Room(7508);
        var tank = Held<obj_item_tank_internals_oxygen>(r, bob);
        var boots = Wear<obj_item_clothing_shoes_magboots>(r, bob, ITEM_SLOT_FEET);
        atom_movable_screen_movable_action_button tankButton = ButtonOf(r, bob, tank);
        atom_movable_screen_movable_action_button bootsButton = ButtonOf(r, bob, boots);
        // the item deleted: its action goes (clear_ref, action.dm:83) and so does the button
        r.In(() => qdel(tank));
        r.In(() =>
        {
            Assert.True(QDELETED(tankButton));
            Assert.False(Dm.@in(tankButton, bob.client.screen));
            Assert.Single(Dm.each_cast<datum_action>(bob.actions));
            Assert.Equal("WEST+0:6,NORTH+0:-6", bootsButton.screen_loc); // the boots' button moves up to the first place
        });
        // the hud deleted (as the body's Destroy does, mob.dm:73; a body with a player first ghostizes, M10): the buttons go with it
        r.In(() => qdel(bob.hud_used));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(bootsButton)));
        Assert.Null(r.In(() => bob.hud_used));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInertActionTypesButtonRefusesItsClick()
    {
        // DV27 (D132): /datum/action/item_action/toggle_flight has its own do_effect (flight.dm), not translated, so
        // its type is inert: its button refuses the click ("not ported yet") instead of running the item's attack_self.
        var (r, bob) = Room(7509);
        var boots = Wear<obj_item_clothing_shoes_magboots>(r, bob, ITEM_SLOT_FEET);
        r.In(() =>
        {
            Assert.True(InertSeam.IsInertType(typeof(datum_action_item_action_toggle_flight)));
            Waiting.NoSleep(boots.add_item_action(typeof(datum_action_item_action_toggle_flight)));
        });
        atom_movable_screen_movable_action_button button = r.In(() => Dm.each_cast<datum_action>(boots.actions)
            .Single(a => a is datum_action_item_action_toggle_flight).viewers[bob.hud_used] as atom_movable_screen_movable_action_button);
        Assert.NotNull(button); // granted on the feet like the boots' own button
        ClickButton(r, bob, button);
        r.In(() => Assert.Equal(FALSE, boots.magpulse));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MouseExitedClearsTheHoverStateMouseEnteredSet()
    {
        var (r, bob) = Room(7507);
        Wear<obj_item_clothing_suit_space_eva>(r, bob, ITEM_SLOT_OCLOTHING);
        atom_movable_screen_spacesuit display = r.In(() => bob.hud_used.screen_objects[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit);
        atom_movable_screen_inventory slot = r.In(() => Dm.each<atom_movable_screen_inventory>(bob.client.screen).First());
        Assert.NotNull(display);

        // over the display: hovering; onto another screen object: BYOND exits the display first
        r.Command(() => ClientSeam.Hover(bob.client, display));
        r.Step();
        Assert.Equal(TRUE, r.In(() => display.hovering));
        r.Command(() => ClientSeam.Hover(bob.client, slot));
        r.Step();
        Assert.Equal(FALSE, r.In(() => display.hovering));

        // back over it, then off to nothing (version 14's Unhover)
        r.Command(() => ClientSeam.Hover(bob.client, display));
        r.Step();
        Assert.Equal(TRUE, r.In(() => display.hovering));
        r.Command(() => ClientSeam.Unhover(bob.client));
        r.Step();
        Assert.Equal(FALSE, r.In(() => display.hovering));
        // an unhover with nothing hovered does nothing
        r.Command(() => ClientSeam.Unhover(bob.client));
        r.Step();
        r.AssertNoRuntimes();
    }
}
