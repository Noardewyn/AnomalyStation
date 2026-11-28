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
/// ui-overhaul.md section 12, slice 4E through a real server and a headless client: a belt put on after joining fills the bar and its number key takes
/// the first tool (the designer's report: the belt cells had no keys, an options file from before section 10 kept the digits on the action buttons);
/// tg's quick-equip keys of a slot (smart_equip_targeted); the world interact key (DV697), tg's empty-hand click also while an item is held.
/// </summary>
public class KeysSlice4EGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // Alice in a jumpsuit with nothing else worn and empty hands.
    private static (RealHeadlessWorld W, RealHeadlessWorld.Player Alice, mob_living_carbon_human Body) Bare(ulong seed)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.belt, body.back, body.l_store, body.r_store, body.wear_id, body.s_store, body.get_active_held_item() as obj_item, body.get_inactive_held_item() as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
        });
        w.Run(Second);
        return (w, alice, body);
    }

    private static ClickTarget On(atom thing) => new(TargetKind.Entity, thing.dm_id, 0, 0, 0, 16, 16);

    [Fact]
    public void A_toolbelt_put_on_after_joining_by_the_inventory_drag_fills_the_bar_and_the_hotbar_key_1_takes_its_first_tool()
    {
        var (w, alice, body) = Bare(4401);
        using (w)
        {
            int runtimes = w.World.RuntimeErrorCount;
            Assert.False(alice.Session.Hotbar.BeltWorn, "joined without a belt");
            obj_item_storage_belt_utility_full belt = In(w, () =>
            {
                var made = Dm.@new<obj_item_storage_belt_utility_full>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(made))));
                return made;
            });
            w.Run(Second);
            // the client's route: the held belt dragged onto the belt slot (an inventory move to tg's slot)
            Assert.True(alice.Session.InventoryMove(belt.dm_id, InventoryTarget.Slot, 0, (int)ITEM_SLOT_BELT));
            w.Run(Second);
            Assert.Same(belt, In(w, () => body.belt));
            Assert.True(alice.Session.Hotbar.BeltWorn, "the bar learns of the belt");
            obj_item first = In(w, () => Dm.each<obj_item>(belt.atom_storage.real_location.contents).First());
            Assert.Equal(HotbarCellState.Holding, alice.Session.Hotbar.StateOf(1));
            Assert.Equal(first.dm_id, alice.Session.Hotbar.Cells[0].Entity);

            // the number key of an options file from before section 10 (its digits were the action buttons'): the upgrade gives 1 back to the bar
            PlayOptions old = PlayOptions.Parse("options_version=5\n" + string.Concat(Enumerable.Range(1, 9).Select(n => $"bind.action_button{n}=Key{n},Kp{n}\n")));
            PlayAction? pressed = old.Bindings.ActionOfPress("Key1", shift: false);
            Assert.Equal(PlayAction.Hotbar1, pressed);
            Assert.True(alice.Session.HotbarPress(HotbarModel.CellOf(pressed.Value)));
            w.Run(10);
            Assert.Same(first, In(w, () => body.get_active_held_item()));
            Assert.Equal(HotbarCellState.Out, alice.Session.Hotbar.StateOf(1));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    [Fact]
    public void The_quick_equip_keys_of_a_slot_equip_into_an_empty_slot_store_into_its_storage_and_take_the_last_item_out()
    {
        var (w, alice, body) = Bare(4402);
        using (w)
        {
            int runtimes = w.World.RuntimeErrorCount;
            // Shift+E with nothing worn and an empty hand: tg's warning, nothing moves
            alice.Session.Keybinding(KeybindingCode.QuickEquipBelt);
            w.Run(10);
            Assert.Null(In(w, () => body.belt));

            // Shift+E holding a toolbelt with no belt worn: tg's smart equip puts it on
            obj_item_storage_belt_utility belt = In(w, () =>
            {
                var made = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(made))));
                return made;
            });
            w.Run(2);
            alice.Session.Keybinding(KeybindingCode.QuickEquipBelt);
            w.Run(10);
            Assert.Same(belt, In(w, () => body.belt));
            Assert.Null(In(w, () => body.get_active_held_item()));

            // Shift+E holding a wrench: into the belt; again with the hand empty: the belt's last item comes out
            obj_item wrench = In(w, () =>
            {
                var made = Dm.@new<obj_item_wrench>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(made))));
                return made;
            });
            w.Run(2);
            alice.Session.Keybinding(KeybindingCode.QuickEquipBelt);
            w.Run(10);
            Assert.True(In(w, () => wrench.loc == belt.atom_storage.real_location), "the wrench went into the belt");
            Assert.Null(In(w, () => body.get_active_held_item()));
            alice.Session.Keybinding(KeybindingCode.QuickEquipBelt);
            w.Run(10);
            Assert.Same(wrench, In(w, () => body.get_active_held_item()));

            // Shift+G: the wrench into the empty left pocket, and back out into the empty hand
            alice.Session.Keybinding(KeybindingCode.QuickEquipLeftPocket);
            w.Run(10);
            Assert.Same(wrench, In(w, () => body.l_store));
            alice.Session.Keybinding(KeybindingCode.QuickEquipLeftPocket);
            w.Run(10);
            Assert.Same(wrench, In(w, () => body.get_active_held_item()));
            Assert.Null(In(w, () => body.l_store));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    [Fact]
    public void The_world_interact_key_opens_a_closet_while_an_item_is_held_and_leaves_an_item_on_the_floor_and_a_mob_in_combat_mode_alone()
    {
        var (w, alice, body) = Bare(4403);
        using (w)
        {
            int runtimes = w.World.RuntimeErrorCount;
            obj_item wrench = null;
            obj_structure_closet closet = null;
            obj_item crowbar = null;
            In(w, () =>
            {
                wrench = Dm.@new<obj_item_wrench>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(wrench))));
                closet = Dm.@new<obj_structure_closet>(get_step(body, Dm.NORTH));
                crowbar = Dm.@new<obj_item_crowbar>(get_turf(body));
            });
            w.Run(Second);

            // E on the closet with the wrench in hand: tg's empty-hand click opens it, the wrench stays held (a left click would hit the closet with it)
            Assert.False(In(w, () => Dm.truthy(closet.opened)));
            Assert.True(alice.Session.Interact(On(closet)));
            w.Run(Second);
            Assert.True(In(w, () => Dm.truthy(closet.opened)), "the closet opened");
            Assert.Same(wrench, In(w, () => body.get_active_held_item()));

            // E on an item on the floor with the hand full: nothing (tg's pick-up would drop it at the feet), the crowbar stays where it lay
            atom lay = In(w, () => crowbar.loc);
            Assert.True(alice.Session.Interact(On(crowbar)));
            w.Run(Second);
            Assert.Same(lay, In(w, () => crowbar.loc));
            Assert.Same(wrench, In(w, () => body.get_active_held_item()));

            // with the hand empty, E on the crowbar picks it up as tg's empty-hand click does
            In(w, () => Waiting.NoSleep(body.dropItemToGround(wrench)));
            w.Run(Second);
            Assert.True(alice.Session.Interact(On(crowbar)));
            w.Run(Second);
            Assert.Same(crowbar, In(w, () => body.get_active_held_item()));

            // E on a mob while in combat mode does nothing (no attack: tg's click would punch); the player's own mob is the one at hand here
            In(w, () => { Waiting.NoSleep(body.dropItemToGround(crowbar)); body.set_combat_mode(Dm.TRUE); });
            w.Run(Second);
            float nextMove = In(w, () => Dm.num(body.next_move));
            float brute = In(w, () => Dm.num(body.get_brute_loss()));
            Assert.True(alice.Session.Interact(On(body)));
            w.Run(Second);
            Assert.Equal(nextMove, In(w, () => Dm.num(body.next_move)));
            Assert.Equal(brute, In(w, () => Dm.num(body.get_brute_loss())));

            // the client sends nothing for a screen entry, and the server refuses a target it cannot resolve
            Assert.False(alice.Session.Interact(new ClickTarget(TargetKind.Screen, 5, 0, 0, 0)));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }
}
