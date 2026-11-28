using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-2: the Tab inventory's other container and Nearby strip through a real server and a headless client, with the
/// client's own model and rules (StorageColumn, InventoryRules, NearbyItems) naming the places a click or drag starts and ends on as the HUD pick would.
/// Alice (a backpack and a toolbelt) opens a box on her tile by tg's own way: her model pins it as the column's last panel and asks for the Tab
/// inventory once; she takes an item out with a click, puts one in with a click on the header, quick-transfers both ways and drags one onto her
/// backpack's panel; she walks away and the panel goes. Then an item dropped beside her shows in the Nearby list (and nothing that is no item lying
/// within a tile does), a click on its cell picks it up, and a drag from her hand onto the strip drops it.
/// </summary>
public class TabInventoryOtherGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Fact]
    public void The_box_on_the_floor_is_the_pinned_panel_every_move_works_with_it_and_items_lying_near_are_the_strip()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7411, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        var bobBody = (mob_living_carbon_human)w.MobOf(bob);
        obj_item backpack = null, belt = null, box = null, pen = null, crayon = null, screwdriver = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.back, body.belt, body.l_store, body.r_store, body.wear_id, body.s_store,
                body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            Waiting.NoSleep(body.forceMove(locate(12, 10, 1)));
            Waiting.NoSleep(bobBody.forceMove(locate(13, 10, 1)));
            backpack = Dm.@new<obj_item_storage_backpack>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(backpack, ITEM_SLOT_BACK))));
            belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(body));
            Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            box = Dm.@new<obj_item_storage_box>(get_turf(body));
            pen = Dm.@new<obj_item_pen>(box);
            crayon = Dm.@new<obj_item_toy_crayon_red>(box);
            screwdriver = Dm.@new<obj_item_screwdriver>(box);
            return 0;
        });
        w.Run(Second);
        int runtimes = w.World.RuntimeErrorCount;
        PlaySession session = alice.Session;
        int active = In(w, () => (int)Dm.num(body.active_hand_index));
        var screen = (long Id) => new ClickTarget(TargetKind.Screen, Id, 0, 0, 0, 16, 16);

        // the client's HUD this frame: the panels (the other container last), the bar's belt items, the strip's items
        List<StoragePanel> panels = null;
        long[] onBar = null;
        List<StorageItem> nearby = null;
        void Refresh()
        {
            onBar = StorageColumn.BeltOnBar(session.Hotbar);
            panels = StorageColumn.Panels(session.Storages.Storages, onBar);
            nearby = NearbyItems.Of(session.Replica);
        }
        InvPlace Pick(ClickTarget target) => InventoryRules.Resolve(target, session.Replica.Screen, panels, session.Storages.At(StoragePlace.Belt)?.Entity ?? 0, onBar,
            nearby: nearby);
        long HandEntry(int index) => session.Replica.Screen.Values.Single(e => e.HandIndex == index).Id;
        void Send(InvSend send)
        {
            switch (send.Kind)
            {
                case InvSendKind.Move:
                    Assert.True(session.InventoryMove(send.Item, send.To, send.Storage, send.Index));
                    break;
                case InvSendKind.WorldClick: // GameScene.SendInventory: the world's click on the item
                    Assert.True(session.Click(new ClickTarget(TargetKind.Entity, send.Item, 0, 0, 0, 16, 16), ClickButton.Left, send.Modifiers));
                    break;
                default:
                    Assert.Fail("nothing to send: " + send);
                    break;
            }
            w.Run(Second);
            Refresh();
        }
        int BoxCell(obj_item item)
        {
            Refresh();
            int cell = Array.FindIndex(panels[^1].Cells, c => c.Entity == item.dm_id);
            Assert.True(cell >= 0, "the item is in the box's panel");
            return cell;
        }
        obj_item Held() => In(w, () => body.get_item_for_held_index(active) as obj_item);

        // nothing open: no pinned panel, nothing asked of Tab
        Refresh();
        Assert.DoesNotContain(panels, p => p.Place == StoragePlace.Other);
        Assert.False(session.Storages.TakeOtherOpened());
        // tg's own opening (a right click on the box): the model has it as the last panel, pinned by the layout, and asks for the Tab inventory once
        Assert.True(session.Click(new ClickTarget(TargetKind.Entity, box.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None));
        w.Run(6);
        Refresh();
        Assert.True(session.Storages.TakeOtherOpened(), "the box's coming turns the Tab inventory on\n" + w.Err);
        Assert.False(session.Storages.TakeOtherOpened());
        Assert.Equal((box.dm_id, StoragePlace.Other), (panels[^1].Storage, panels[^1].Place));
        StorageColumnLayout layout = StorageColumn.Layout(new HudRect(1237, 141, 575, 843), 48f, 1f, panels, 0f, nearby.Count);
        Assert.Equal(panels.Count - 1, layout.Pinned);
        int pinned = layout.Pinned;

        // a click on the pen's cell: to the active hand
        InvSend click = InventoryRules.Click(Pick(screen(StorageColumn.CellId(pinned, BoxCell(pen)))), ClickModifiers.None, active, held: 0) ?? InvSend.Nothing;
        Assert.Equal(InvSend.Move(pen.dm_id, InventoryTarget.Hand, 0, active), click);
        Send(click);
        Assert.Same(pen, Held());
        Assert.DoesNotContain(panels[^1].Cells, c => c.Entity == pen.dm_id);
        // with the pen held, a click on the box's header: into the box
        Send(InventoryRules.Click(Pick(screen(StorageColumn.PanelId(pinned))), ClickModifiers.None, active, pen.dm_id) ?? InvSend.Nothing);
        Assert.True(In(w, () => pen.loc == box));
        Assert.Null(Held());
        // the quick transfer of the crayon in the box: to the active hand; from the hand: into the box (open), not the backpack
        long back = session.Storages.At(StoragePlace.Back).Entity, other = session.Storages.At(StoragePlace.Other).Entity;
        Send(InventoryRules.QuickTransfer(Pick(screen(StorageColumn.CellId(pinned, BoxCell(crayon)))), active, back, other) ?? InvSend.Nothing);
        Assert.Same(crayon, Held());
        Send(InventoryRules.QuickTransfer(Pick(screen(HandEntry(active))), active, back, other) ?? InvSend.Nothing);
        Assert.True(In(w, () => crayon.loc == box));
        // a drag of the screwdriver onto the backpack's panel
        InvPlace from = Pick(screen(StorageColumn.CellId(pinned, BoxCell(screwdriver))));
        Send(InventoryRules.Drop(from, Pick(screen(StorageColumn.PanelId(panels.FindIndex(p => p.Storage == backpack.dm_id)))), active));
        Assert.True(In(w, () => screwdriver.loc == backpack));
        Assert.Contains(screwdriver.dm_id, panels[0].Cells.Select(c => c.Entity));
        Assert.Equal(0L, session.Storages.RefusedItem);

        // she walks two tiles north (tg's Move closes it): the panel goes; Tab is not asked for again
        MotionState start = session.Prediction.Predicted;
        double toX = start.X / 256.0, toY = start.Y / 256.0 + 2;
        for (int i = 0; i < 8 * Second; i++)
        {
            MotionState now = session.Prediction.Predicted;
            double dx = toX - now.X / 256.0, dy = toY - now.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            alice.Keys = MoveKeys.Forward;
            alice.Yaw = Math.Atan2(-dx, dy);
            w.Run(1);
        }
        alice.Keys = MoveKeys.None;
        w.Run(8);
        Refresh();
        Assert.DoesNotContain(panels, p => p.Place == StoragePlace.Other);
        Assert.Equal(-1, StorageColumn.Layout(new HudRect(1237, 141, 575, 843), 48f, 1f, panels, 0f, 0).Pinned);
        Assert.False(session.Storages.TakeOtherOpened());

        // the Nearby strip: a crowbar dropped on the tile east of her and wirecutters on hers; a closet with a wrench in it and Bob beside her are not
        // listed, nor the box two tiles away
        (int x, int y) = In(w, () => ((int)body.x, (int)body.y));
        Assert.True(y >= 12, "two tiles or more from the box");
        obj_item crowbar = null, cutters = null, wrench = null;
        In(w, () =>
        {
            crowbar = Dm.@new<obj_item_crowbar>(locate(x + 1, y, 1));
            cutters = Dm.@new<obj_item_wirecutters>(locate(x, y, 1));
            var closet = Dm.@new<obj_structure_closet>(locate(x - 1, y, 1));
            wrench = Dm.@new<obj_item_wrench>(closet);
            Waiting.NoSleep(bobBody.forceMove(locate(x, y + 1, 1)));
            return 0;
        });
        w.Run(10);
        Refresh();
        Assert.Equal(new[] { cutters.dm_id, crowbar.dm_id }, nearby.Select(i => i.Entity));
        Assert.Contains("crowbar", nearby[1].Name, StringComparison.OrdinalIgnoreCase);
        // a click on the crowbar's cell: tg's click on it where it lies picks it up into the active hand
        InvSend pick = InventoryRules.Click(Pick(screen(StorageColumn.StripCellId(1))), ClickModifiers.None, active, held: 0) ?? InvSend.Nothing;
        Assert.Equal(new InvSend(InvSendKind.WorldClick, crowbar.dm_id), pick);
        Send(pick);
        Assert.Same(crowbar, Held());
        Assert.Equal(new[] { cutters.dm_id }, nearby.Select(i => i.Entity));
        // a drag from the hand onto the strip's title: dropped (the move to the floor), and listed again
        Send(InventoryRules.Drop(Pick(screen(HandEntry(active))), Pick(screen(StorageColumn.StripId)), active));
        Assert.Null(Held());
        Assert.True(In(w, () => crowbar.loc == get_turf(body)));
        Assert.Equal(new[] { cutters.dm_id, crowbar.dm_id }.OrderBy(id => id), nearby.Select(i => i.Entity));
        // a drag from the wirecutters' cell onto the hand box: the same pick-up
        int cuttersCell = nearby.FindIndex(i => i.Entity == cutters.dm_id);
        Send(InventoryRules.Drop(Pick(screen(StorageColumn.StripCellId(cuttersCell))), Pick(screen(HandEntry(active))), active));
        Assert.Same(cutters, Held());
        Assert.Equal(0L, session.Storages.RefusedItem);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
