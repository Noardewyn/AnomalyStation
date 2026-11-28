using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 15 (DV698) through a real server and a headless client: a human holding a flashlight and an oxygen tank, a breath mask on, gets
/// their action buttons in the server's Actions message with their names; the client's slots fill by themselves, the tank's action is dragged onto
/// slot 2 by the menu's rules and Shift+2 opens the internals (tg's click on the button: the server's effect, and the button's active state comes
/// back); the flashlight's action dragged onto slot 2 swaps with it and Shift+2 turns the light on; the tank put down leaves its slot empty.
/// </summary>
public class ActionsMenuGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static long ButtonOf(RealHeadlessWorld w, mob_living_carbon_human body, obj_item item)
        => In(w, () => (Dm.each_cast<datum_action>(item.actions).Single().viewers[body.hud_used] as atom_movable_screen_movable_action_button)?.dm_id ?? 0);

    // Shift and the digit of a slot, as the game scene runs it: the key's action, the slot's button, tg's click on it
    private static void PressShiftDigit(RealHeadlessWorld.Player p, KeyBindings keys, ActionSlots slots, int digit)
    {
        PlayAction? action = keys.ActionOfPress("Key" + digit, shift: true);
        Assert.Equal(PlayAction.ActionButton1 + (byte)(digit - 1), action);
        long button = slots.ButtonIn(action.Value - PlayAction.ActionButton1 + 1);
        Assert.NotEqual(0, button);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
    }

    private static void Drag(ActionSlots slots, long button, int slot)
    {
        var mouse = new ActionsMenuInput();
        mouse.Press(button, 10, 10);
        mouse.Move(200, 10);
        Assert.True(mouse.Dragging);
        Assert.Equal((ActionsMenuOutcome.Assigned, button), mouse.Release(slot, slots));
    }

    [Fact]
    public void A_held_flashlight_and_tank_fill_the_slots_and_Shift_2_uses_the_action_dragged_onto_slot_2()
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 1501, TimeoutSteps = 9000 });
        using (w)
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            var body = (mob_living_carbon_human)w.MobOf(alice);
            ReplicaWorld replica = alice.Session.Replica;
            int runtimes = w.World.RuntimeErrorCount;
            (obj_item_flashlight light, obj_item_tank_internals_oxygen tank) = In(w, () =>
            {
                foreach (obj_item held in new[] { body.get_active_held_item() as obj_item, body.get_inactive_held_item() as obj_item, body.wear_mask as obj_item })
                    if (held != null)
                        qdel(held);
                var mask = Dm.@new<obj_item_clothing_mask_breath>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(mask, ITEM_SLOT_MASK))));
                var flashlight = Dm.@new<obj_item_flashlight>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(flashlight))));
                var oxygen = Dm.@new<obj_item_tank_internals_oxygen>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_inactive_hand(oxygen))));
                return (flashlight, oxygen);
            });
            w.Run(Second);
            long lightButton = ButtonOf(w, body, light), tankButton = ButtonOf(w, body, tank);
            Assert.NotEqual(0, lightButton);
            Assert.NotEqual(0, tankButton);

            // the server lists both buttons with their names, the tank's not active yet; the client has their screen entries
            ActionButtonState lightState = Assert.IsType<ActionButtonState>(replica.Actions.Find(lightButton));
            ActionButtonState tankState = Assert.IsType<ActionButtonState>(replica.Actions.Find(tankButton));
            Assert.Equal(In(w, () => Dm.jointext(Dm.list(Dm.each_cast<datum_action>(tank.actions).Single().viewers[body.hud_used]), "")), tankState.Name);
            Assert.False(string.IsNullOrEmpty(lightState.Name));
            Assert.False(tankState.Active);
            Assert.Equal(0, tankState.CooldownTicks);
            Assert.True(replica.Screen.ContainsKey(lightButton) && replica.Screen.ContainsKey(tankButton));

            // the slots fill by themselves, in the order tg made the buttons
            var slots = new ActionSlots();
            slots.Sync(ActionSlots.Drawable(replica.Actions, replica.Screen));
            Assert.NotEqual(0, slots.SlotOf(lightButton));
            Assert.NotEqual(0, slots.SlotOf(tankButton));
            // right clicks empty both slots; the next sync does not put them back
            Assert.Equal(ActionsMenuOutcome.Cleared, ActionsMenuInput.RightClick(slots.SlotOf(lightButton), slots));
            Assert.Equal(ActionsMenuOutcome.Cleared, ActionsMenuInput.RightClick(slots.SlotOf(tankButton), slots));
            slots.Sync(ActionSlots.Drawable(replica.Actions, replica.Screen));
            Assert.Equal((0, 0), (slots.SlotOf(lightButton), slots.SlotOf(tankButton)));

            // the tank's action from the list onto slot 2, then Shift+2: the internals open (tg's set_internals through the button's click)
            Drag(slots, tankButton, 2);
            Assert.Equal(tankButton, slots.ButtonIn(2));
            KeyBindings keys = KeyBindings.Defaults();
            PressShiftDigit(alice, keys, slots, 2);
            w.Run(Second);
            Assert.Same(tank, In(w, () => body.@internal));
            Assert.True(replica.Actions.Find(tankButton)?.Active, "the server reports the internals' button active");

            // the flashlight's action onto slot 5, then onto slot 2: a swap, the tank's action goes to 5; Shift+2 now turns the light on
            Drag(slots, lightButton, 5);
            Drag(slots, lightButton, 2);
            Assert.Equal(lightButton, slots.ButtonIn(2));
            Assert.Equal(tankButton, slots.ButtonIn(5));
            Assert.False(In(w, () => Dm.truthy(light.light_on)));
            PressShiftDigit(alice, keys, slots, 2);
            w.Run(Second);
            Assert.True(In(w, () => Dm.truthy(light.light_on)), "the flashlight is on");
            Assert.Same(tank, In(w, () => body.@internal)); // the internals untouched

            // the tank put down: its button goes, and its slot is left empty (not drawn); the flashlight keeps slot 2
            In(w, () => Waiting.NoSleep(body.dropItemToGround(tank)));
            w.Run(Second);
            Assert.Null(replica.Actions.Find(tankButton));
            slots.Sync(ActionSlots.Drawable(replica.Actions, replica.Screen));
            Assert.Equal(0, slots.SlotOf(tankButton));
            Assert.False(slots.Drawn(5, dragging: false));
            Assert.Equal(lightButton, slots.ButtonIn(2));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }
}
