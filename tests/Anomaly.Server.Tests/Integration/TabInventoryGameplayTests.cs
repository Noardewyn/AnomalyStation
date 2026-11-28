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
/// ui-overhaul.md section 12, slice 4B-2: the Tab inventory's client rules against a real server and a headless client. Alice wears a backpack (a
/// screwdriver, a wrench, a pair of gloves) and a toolbelt (a crowbar); her client builds the right column's panels from the server's Storages message
/// and the bar's belt cells from its Hotbar message (StorageColumn), names the places a drag starts and ends on as the HUD pick would
/// (InventoryRules.Resolve over her screen entries and the panels' ids), and sends what InventoryRules.Drop says for a drag from a panel's cell to a
/// hand box, to a belt cell, to the gloves slot and to the floor: each one move, which tg does.
/// </summary>
public class TabInventoryGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Fact]
    public void Drags_from_a_panel_cell_to_a_hand_the_belt_a_slot_and_the_floor_are_sent_by_the_clients_rule_and_tg_does_them()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7401, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        obj_item backpack = null, belt = null, screwdriver = null, wrench = null, gloves = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.back, body.belt, body.l_store, body.r_store, body.s_store, body.gloves,
                body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            screwdriver = Dm.@new<obj_item_screwdriver>(backpack);
            wrench = Dm.@new<obj_item_wrench>(backpack);
            gloves = Dm.@new<obj_item_clothing_gloves_color_black>(backpack);
            belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            Dm.@new<obj_item_crowbar>(belt);
            return 0;
        });
        w.Run(Second);
        int runtimes = w.World.RuntimeErrorCount;
        PlaySession session = alice.Session;

        // what the client's HUD would have this frame: the panels, the belt cells' items, the screen entries the pick resolves against
        List<StoragePanel> panels = null;
        long[] onBar = null;
        long beltId = 0;
        void Refresh()
        {
            CarriedStorage worn = session.Storages.At(StoragePlace.Belt);
            beltId = worn?.Entity ?? 0;
            onBar = StorageColumn.BeltOnBar(session.Hotbar);
            panels = StorageColumn.Panels(session.Storages.Storages, onBar);
        }
        InvPlace Pick(ClickTarget target) => InventoryRules.Resolve(target, session.Replica.Screen, panels, beltId, onBar);
        var screen = (long Id) => new ClickTarget(TargetKind.Screen, Id, 0, 0, 0, 16, 16);
        int CellOf(obj_item item)
        {
            Refresh();
            Assert.Equal(backpack.dm_id, panels[0].Storage);
            int cell = Array.FindIndex(panels[0].Cells, c => c.Entity == item.dm_id);
            Assert.True(cell >= 0, "the item is in the backpack's panel");
            return cell;
        }
        long HandEntry(int index) => session.Replica.Screen.Values.Single(e => e.HandIndex == index).Id;
        int active = In(w, () => (int)Dm.num(body.active_hand_index));
        void Drag(obj_item item, ClickTarget over)
        {
            InvPlace from = Pick(screen(StorageColumn.CellId(0, CellOf(item))));
            Assert.Equal((InvKind.Cell, item.dm_id), (from.Kind, from.Item));
            InvSend send = InventoryRules.Drop(from, Pick(over), active);
            Assert.Equal(InvSendKind.Move, send.Kind);
            Assert.True(session.InventoryMove(send.Item, send.To, send.Storage, send.Index));
            w.Run(10);
            Refresh();
        }

        // a panel cell onto the other hand's box
        int otherHand = active == 1 ? 2 : 1;
        Drag(wrench, screen(HandEntry(otherHand)));
        Assert.True(In(w, () => body.get_item_for_held_index(otherHand) == wrench));
        Assert.DoesNotContain(panels[0].Cells, c => c.Entity == wrench.dm_id);

        // a panel cell onto an empty belt cell of the bar (the crowbar holds cell 1): into the belt
        Assert.Equal(InvKind.Belt, Pick(screen(LiveHud.BeltCellId(4))).Kind);
        Drag(screwdriver, screen(LiveHud.BeltCellId(4)));
        Assert.True(In(w, () => screwdriver.loc == belt));
        Assert.Contains(screwdriver.dm_id, session.Storages.At(StoragePlace.Belt).Items.Select(i => i.Entity));
        Assert.Contains(screwdriver.dm_id, onBar); // and the bar shows it

        // a panel cell onto the gloves slot's cell: tg's worn slot entry, which exists while tg's inventory is expanded (the client presses tg's toggle
        // for the Tab inventory, InventoryExpander)
        ScreenEntry toggle = session.Replica.Screen.Values.Single(e => LiveHud.RoleOf(session.Replica, e) == LiveHudRole.InventoryToggle);
        session.Click(screen(toggle.Id), ClickButton.Left, ClickModifiers.None);
        w.Run(10);
        long glovesSlot = session.Replica.Screen.Values.Single(e => e.SlotId == ITEM_SLOT_GLOVES).Id;
        Assert.Equal((InvKind.Slot, (int)ITEM_SLOT_GLOVES), (Pick(screen(glovesSlot)).Kind, Pick(screen(glovesSlot)).Index));
        Drag(gloves, screen(glovesSlot));
        Assert.True(In(w, () => body.gloves == gloves));

        // a panel cell onto the floor in the view (a turf): dropped at the feet
        obj_item crowbarBack = In(w, () => (obj_item)Dm.@new<obj_item_crowbar>(backpack));
        w.Run(10);
        Assert.True(In(w, () => crowbarBack.loc == backpack));
        turf here = In(w, () => get_turf(body));
        Drag(crowbarBack, new ClickTarget(TargetKind.Turf, 0, (int)Dm.num(here.x), (int)Dm.num(here.y), (int)Dm.num(here.z), 0, 0));
        Assert.True(In(w, () => crowbarBack.loc == get_turf(body)));
        Assert.DoesNotContain(panels[0].Cells, c => c.Entity != 0);

        Assert.Equal(0L, session.Storages.RefusedItem);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Of_two_identical_crowbars_in_the_belt_a_drag_of_the_second_cell_moves_the_one_that_cell_holds()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7402, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        obj_item backpack = null, belt = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.back, body.belt, body.l_store, body.r_store, body.s_store,
                body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
            // two items of one picture (tg gives each screwdriver a random handle colour, so crowbars)
            Dm.@new<obj_item_crowbar>(belt);
            Dm.@new<obj_item_crowbar>(belt);
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            return 0;
        });
        w.Run(Second);
        int runtimes = w.World.RuntimeErrorCount;
        PlaySession session = alice.Session;
        // the Hotbar message names each belt cell's item: two crowbars of one picture, told apart by their entities, as the server's bar holds them
        long[] onBar = StorageColumn.BeltOnBar(session.Hotbar);
        Assert.Equal(session.Hotbar.Cells[0].Appearance, session.Hotbar.Cells[1].Appearance);
        Assert.Equal(In(w, () => BeltHotbar.Peek(body).ItemOf(1).dm_id), onBar[0]);
        Assert.Equal(In(w, () => BeltHotbar.Peek(body).ItemOf(2).dm_id), onBar[1]);
        Assert.NotEqual(onBar[0], onBar[1]);
        long first = onBar[0], second = onBar[1];
        // a drag of cell 2 onto the backpack's header, as the client's rule sends it
        List<StoragePanel> panels = StorageColumn.Panels(session.Storages.Storages, onBar);
        var screen = (long Id) => new ClickTarget(TargetKind.Screen, Id, 0, 0, 0, 16, 16);
        InvPlace from = InventoryRules.Resolve(screen(LiveHud.BeltCellId(2)), session.Replica.Screen, panels, belt.dm_id, onBar);
        Assert.Equal((InvKind.Belt, second), (from.Kind, from.Item));
        int bag = panels.FindIndex(p => p.Storage == backpack.dm_id);
        InvPlace over = InventoryRules.Resolve(screen(StorageColumn.PanelId(bag)), session.Replica.Screen, panels, belt.dm_id, onBar);
        InvSend send = InventoryRules.Drop(from, over, 1);
        Assert.Equal(InvSend.Move(second, InventoryTarget.Storage, backpack.dm_id, 0), send);
        Assert.True(session.InventoryMove(send.Item, send.To, send.Storage, send.Index));
        w.Run(10);
        // the second cell's crowbar is in the backpack; the first stays in the belt
        Assert.True(In(w, () => w.World.Objects.TryGet(second, out object o) && o is obj_item moved && moved.loc == backpack));
        Assert.True(In(w, () => w.World.Objects.TryGet(first, out object o) && o is obj_item kept && kept.loc == belt));
        Assert.Equal(0L, session.Storages.RefusedItem);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_drop_onto_a_bar_cell_gives_the_item_that_cell_and_the_number_keys_then_take_the_right_items()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7403, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        obj_item backpack = null, belt = null, screwdriver = null;
        obj_item[] tools = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.back, body.belt, body.l_store, body.r_store, body.s_store,
                body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            screwdriver = Dm.@new<obj_item_screwdriver>(backpack);
            belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
            tools = new obj_item[] { Dm.@new<obj_item_crowbar>(belt), Dm.@new<obj_item_wrench>(belt), Dm.@new<obj_item_wirecutters>(belt),
                Dm.@new<obj_item_multitool>(belt), Dm.@new<obj_item_weldingtool>(belt) };
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            return 0;
        });
        w.Run(Second);
        int runtimes = w.World.RuntimeErrorCount;
        PlaySession session = alice.Session;
        long[] Cells() => StorageColumn.BeltOnBar(session.Hotbar);
        Assert.Equal(tools.Select(t => t.dm_id).Concat(new long[] { 0, 0 }), Cells());
        var screen = (long Id) => new ClickTarget(TargetKind.Screen, Id, 0, 0, 0, 16, 16);
        void Drag(ClickTarget from, ClickTarget over)
        {
            long[] bar = Cells();
            List<StoragePanel> panels = StorageColumn.Panels(session.Storages.Storages, bar);
            InvSend send = InventoryRules.Drop(InventoryRules.Resolve(from, session.Replica.Screen, panels, belt.dm_id, bar),
                InventoryRules.Resolve(over, session.Replica.Screen, panels, belt.dm_id, bar), 1);
            Assert.Equal(InvSendKind.Move, send.Kind);
            Assert.True(session.InventoryMove(send.Item, send.To, send.Storage, send.Index));
            w.Run(Second);
        }

        // the backpack's screwdriver onto the empty bar cell 6: it is in the belt, in cell 6
        int bag = StorageColumn.Panels(session.Storages.Storages, Cells()).FindIndex(p => p.Storage == backpack.dm_id);
        Drag(screen(StorageColumn.CellId(bag, 0)), screen(LiveHud.BeltCellId(6)));
        Assert.True(In(w, () => screwdriver.loc == belt));
        Assert.Equal(screwdriver.dm_id, Cells()[5]);
        // cell 1 (the crowbar) onto cell 5 (the welder): the two swap on the bar
        Drag(screen(LiveHud.BeltCellId(1)), screen(LiveHud.BeltCellId(5)));
        Assert.Equal(new[] { tools[4].dm_id, tools[1].dm_id, tools[2].dm_id, tools[3].dm_id, tools[0].dm_id, screwdriver.dm_id, 0 }, Cells());
        // the number keys take the items their cells now hold
        int active = In(w, () => (int)Dm.num(body.active_hand_index));
        session.HotbarPress(6);
        w.Run(Second);
        Assert.True(In(w, () => body.get_item_for_held_index(active) == screwdriver));
        session.HotbarPress(6); // back into its cell
        w.Run(Second);
        session.HotbarPress(5);
        w.Run(Second);
        Assert.True(In(w, () => body.get_item_for_held_index(active) == tools[0]));
        session.HotbarPress(5);
        w.Run(Second);
        session.HotbarPress(1);
        w.Run(Second);
        Assert.True(In(w, () => body.get_item_for_held_index(active) == tools[4]));
        Assert.Equal(0L, session.Storages.RefusedItem);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
