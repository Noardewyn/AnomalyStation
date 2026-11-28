using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, slice 4D-1: the Tab inventory's strip view through a real server and a headless client, with the client's own window
/// manager (the strip window withheld as on the human HUD), model (StripView: tg's StripMenu window data as the client receives it) and rules
/// (InventoryRules with the strip window) naming the places a click or drag starts and ends on as the HUD pick would. Alice opens the strip on a sleeping
/// Bob by tg's way (the radial's Strip: Bob dropped onto her) and the Tab flag is taken once; she strips his shoes with a click (the slot shows tg's
/// interacting, then empty; the shoes lie on the floor and in her Nearby list), puts a beanie on his head with a click and the shoes back on with a
/// drag from her active hand, strips his ID with a drag onto the Nearby strip, sees his full pocket hidden, sets his sensors from the right-click
/// menu's act, and closes it with the X; opened again, the flag is taken once more.
/// </summary>
public class TabStripViewGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    [Fact]
    public void Alice_strips_and_dresses_a_sleeping_Bob_from_the_strip_view_and_closes_it_with_the_X()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7412, TimeoutSteps = 30000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        var bobBody = (mob_living_carbon_human)w.MobOf(bob);
        obj_item shoes = null, beanie = null, pen = null, id = null;
        obj_item_clothing_under jumpsuit = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.get_item_for_held_index(1) as obj_item, body.get_item_for_held_index(2) as obj_item, bobBody.head as obj_item, bobBody.shoes as obj_item,
                bobBody.l_store as obj_item, bobBody.w_uniform as obj_item, bobBody.wear_id as obj_item })
                if (item != null)
                    qdel(item);
            Waiting.NoSleep(body.forceMove(locate(12, 10, 1)));
            Waiting.NoSleep(bobBody.forceMove(locate(13, 10, 1)));
            jumpsuit = Dm.@new<obj_item_clothing_under_color_grey>(bobBody.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobBody.equip_to_slot_if_possible(jumpsuit, ITEM_SLOT_ICLOTHING))));
            shoes = Dm.@new<obj_item_clothing_shoes_sneakers_black>(bobBody.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobBody.equip_to_slot_if_possible(shoes, ITEM_SLOT_FEET))));
            id = Dm.@new<obj_item_card_id>(bobBody.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobBody.equip_to_slot_if_possible(id, ITEM_SLOT_ID))));
            pen = Dm.@new<obj_item_pen>(bobBody.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobBody.equip_to_slot_if_possible(pen, ITEM_SLOT_LPOCKET))));
            beanie = Dm.@new<obj_item_clothing_head_beanie>(body.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(body.put_in_active_hand(beanie))));
            Waiting.NoSleep(bobBody.Unconscious(120 * SECONDS));
            return 0;
        });
        w.Run(Second);
        int runtimes = w.World.RuntimeErrorCount;
        PlaySession session = alice.Session;
        ReplicaWorld replica = session.Replica;
        int active = In(w, () => (int)Dm.num(body.active_hand_index));
        string bobName = In(w, () => bobBody.name);

        // the client's window manager as the human HUD has it: the strip window withheld (the strip view shows it)
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => session.UiAct(window, action, parameters),
            SendClose = window => session.UiClose(window),
            Withhold = window => window.Interface == StripView.Interface,
        };
        var openings = new StripOpenings();
        StripModel strip = null;
        List<StoragePanel> panels = null;
        long[] onBar = null;
        List<StorageItem> nearby = null;
        void Refresh()
        {
            windows.Sync(replica.UiWindows, 1920, 1080, 1);
            strip = StripView.ModelOf(StripView.Current(windows.Open));
            onBar = StorageColumn.BeltOnBar(session.Hotbar);
            panels = StorageColumn.Panels(session.Storages.Storages, onBar);
            nearby = NearbyItems.Of(replica);
        }
        bool NewWindow() => openings.TakeNew(windows.Open.Where(x => x.Interface == StripView.Interface).Select(x => x.Index), humanHud: true);
        InvPlace Pick(long screenId) => InventoryRules.Resolve(Screen(screenId), replica.Screen, panels, 0, onBar, true, nearby, strip);
        InvPlace Slot(string key) => Pick(StripView.SlotId(StripView.IndexOf(key)));
        long HandEntry(int index) => replica.Screen.Values.Single(e => e.HandIndex == index).Id;
        // GameScene.SendInventory: tg's act on the strip window shown, or its close
        void Send(InvSend send)
        {
            TguiWindow window = windows.Get(strip.Window);
            switch (send.Kind)
            {
                case InvSendKind.StripUse:
                    window.Context.Act("use", ("key", StripView.Keys[send.Index]));
                    break;
                case InvSendKind.StripClose:
                    windows.Close(window);
                    break;
                default:
                    Assert.Fail("not a strip act: " + send);
                    break;
            }
        }
        StripSlot At(string key) => strip.At(StripView.IndexOf(key));
        void RunUntil(Func<bool> done, int ticks)
        {
            for (int i = 0; i < ticks && !done(); i++)
            {
                w.Run(1);
                Refresh();
            }
        }
        IEnumerable<(long, string, int)> Mobs()
        {
            ReplicaEntity me = replica.Entities.Single(e => e.Id == session.Replica.OwnEntity);
            return replica.Entities.Where(e => e.Id != me.Id && e.IsMob()).Select(e => (e.Id, e.State.Name, Math.Max(Math.Abs(e.State.X - me.State.X), Math.Abs(e.State.Y - me.State.Y))));
        }

        // 1. tg's way of opening it (the radial's Strip, GameScene.ExecuteChoice: Bob dropped onto Alice): the window opens, withheld, and the Tab
        // inventory is asked for once
        Refresh();
        Assert.Null(strip);
        Assert.True(session.Drop(new DragRelease(On(bobBody), On(body), ClickButton.Left, ClickModifiers.None, 255)));
        RunUntil(() => strip != null, Second);
        Assert.NotNull(strip);
        Assert.True(windows.AnyOpen && !windows.AnyShown, "the strip window is open in tg and not drawn");
        Assert.True(NewWindow());
        Assert.False(NewWindow());
        Assert.Equal("Stripping: " + bobName, strip.Title);
        Assert.True(strip.Interactive);
        Assert.Equal(bobBody.dm_id, StripView.TargetOf(strip.Name, bobBody.dm_id, Mobs()));
        Assert.Equal(bobBody.dm_id, StripView.TargetOf(strip.Name, 0, Mobs())); // the one mob of that name next to her
        // what tg's window shows: the shoes by name and icon, the empty head, the full pocket hidden ("obscured left pocket")
        Assert.Equal((StripLook.Item, In(w, () => shoes.name), In(w, () => shoes.icon_state)), (At("shoes").Look, At("shoes").Name, At("shoes").IconState));
        Assert.Equal(StripLook.Empty, At("head").Look);
        Assert.Equal((StripLook.Hidden, "obscured left pocket"), (At("left_pocket").Look, At("left_pocket").Tooltip));
        Assert.Null(At("handcuffs"));
        output.WriteLine(string.Join(", ", StripView.Keys.Select((k, i) => strip.At(i) is StripSlot s ? k + ":" + s.Look : null).Where(s => s != null)));
        // the layout places a cell for every slot tg's window shows
        StripLayout layout = StripView.Layout(new HudRect(538, 141, 691, 843), new HudRect(1237, 141, 575, 843), new HudRect(538, 141, 1274, 843), 48f, 6, 1f, strip);
        for (int i = 0; i < StripView.Keys.Length; i++)
            Assert.Equal(strip.At(i) != null, layout.Cells[i].Width > 0);

        // 2. a click on the shoes' cell: tg's use; the slot shows tg's interacting, then empty; the shoes lie on the floor and in the Nearby list
        long beanieHeld = beanie.dm_id;
        InvSend click = InventoryRules.Click(Slot("shoes"), ClickModifiers.None, active, beanieHeld, strip) ?? InvSend.Nothing;
        Assert.Equal(InvSend.StripUse(StripView.IndexOf("shoes")), click);
        Send(click);
        RunUntil(() => At("shoes").Interacting, 2 * Second);
        Assert.True(At("shoes").Interacting, "the slot is marked as being worked on");
        Assert.Same(shoes, In(w, () => bobBody.shoes));
        RunUntil(() => At("shoes").Look == StripLook.Empty, (int)(In(w, () => shoes.strip_delay) * 2) + 2 * Second);
        Assert.Equal(StripLook.Empty, At("shoes").Look);
        Assert.False(At("shoes").Interacting);
        Assert.Null(In(w, () => bobBody.shoes));
        Assert.True(In(w, () => shoes.loc == bobBody.loc));
        Assert.Contains(nearby, i => i.Entity == shoes.dm_id);

        // 3. a click on the empty head with the beanie held: put on
        click = InventoryRules.Click(Slot("head"), ClickModifiers.None, active, beanieHeld, strip) ?? InvSend.Nothing;
        Assert.Equal(InvSend.StripUse(StripView.IndexOf("head")), click);
        Send(click);
        RunUntil(() => In(w, () => bobBody.head) == beanie, (int)(In(w, () => beanie.equip_delay_other) * 2) + 3 * Second);
        Assert.Same(beanie, In(w, () => bobBody.head));
        RunUntil(() => At("head").Look == StripLook.Item, Second); // the window's next update (SStgui's fire)
        Assert.Equal(StripLook.Item, At("head").Look);
        // with an empty hand an empty-looking slot is still tg's use (tg decides: here nothing is there, see 6b)
        Assert.Equal(InvSend.StripUse(StripView.IndexOf("suit")), InventoryRules.Click(Slot("suit"), ClickModifiers.None, active, 0, strip));

        // 4. the shoes picked up (the Nearby cell's click) and dragged from the active hand onto their empty slot: put on
        int shoesCell = nearby.FindIndex(i => i.Entity == shoes.dm_id);
        InvSend pick = InventoryRules.Click(Pick(StorageColumn.StripCellId(shoesCell)), ClickModifiers.None, active, 0, strip) ?? InvSend.Nothing;
        Assert.Equal(new InvSend(InvSendKind.WorldClick, shoes.dm_id), pick);
        Assert.True(session.Click(On(shoes), ClickButton.Left, ClickModifiers.None));
        RunUntil(() => In(w, () => body.get_item_for_held_index(active)) == shoes, Second);
        InvPlace hand = Pick(HandEntry(active));
        Assert.Equal((InvKind.Hand, shoes.dm_id), (hand.Kind, hand.Item));
        Assert.Equal(InvSend.Nothing, InventoryRules.Drop(hand, Slot("head"), active, strip)); // onto a full one: nothing
        InvSend drop = InventoryRules.Drop(hand, Slot("shoes"), active, strip);
        Assert.Equal(InvSend.StripUse(StripView.IndexOf("shoes")), drop);
        Send(drop);
        RunUntil(() => In(w, () => bobBody.shoes) == shoes, (int)(In(w, () => shoes.equip_delay_other) * 2) + 3 * Second);
        Assert.Same(shoes, In(w, () => bobBody.shoes));

        // 5. his ID dragged onto the Nearby strip: tg's strip; it falls to the floor
        drop = InventoryRules.Drop(Slot("id"), Pick(StorageColumn.StripId), active, strip);
        Assert.Equal(InvSend.StripUse(StripView.IndexOf("id")), drop);
        Send(drop);
        RunUntil(() => In(w, () => bobBody.wear_id) == null, (int)(In(w, () => id.strip_delay) * 2) + 3 * Second);
        Assert.Null(In(w, () => bobBody.wear_id));
        Assert.True(In(w, () => id.loc == bobBody.loc));

        // 6. the right-click menu on his jumpsuit offers tg's "Adjust sensors"; its pick is tg's alt act, then tg's list window
        In(w, () => jumpsuit.set_sensor_mode(SENSOR_OFF));
        IReadOnlyList<(string Action, string Label)> offered = InventoryRules.StripAlternates(Slot("jumpsuit"), strip);
        Assert.Contains(("adjust_sensor", "Adjust sensors"), offered);
        windows.Get(strip.Window).Context.Act("alt", ("key", "jumpsuit"), ("alternate_action", "adjust_sensor")); // GameScene.OnStripMenuPicked
        RunUntil(() => replica.UiWindows.Values.Any(v => v.Interface == "ListInputWindow"), Second);
        UiWindowState list = Assert.Single(replica.UiWindows.Values, v => v.Interface == "ListInputWindow");
        Assert.True(windows.AnyShown, "tg's list window is drawn");
        Assert.True(session.UiAct(list.Window, "submit", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("entry", UiValue.Of("Tracking")) })));
        RunUntil(() => In(w, () => jumpsuit.sensor_mode) == SENSOR_COORDS, (int)(In(w, () => jumpsuit.strip_delay) * 0.5f) * 2 + 3 * Second);
        Assert.Equal(SENSOR_COORDS, In(w, () => jumpsuit.sensor_mode));

        // 6b. a wig on his head: tg's data shows a worn item with TRAIT_EXAMINE_SKIP as an empty slot, yet tg's window sends `use` on it; a click with an
        // empty hand on the empty-looking head is that `use`, and tg strips the wig onto the floor
        obj_item wig = null;
        In(w, () =>
        {
            qdel(bobBody.head as obj_item);
            wig = Dm.@new<obj_item_clothing_head_wig>(bobBody.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(bobBody.equip_to_slot_if_possible(wig, ITEM_SLOT_HEAD))));
            return 0;
        });
        RunUntil(() => At("head").Look == StripLook.Empty, 2 * Second);
        Assert.Equal(StripLook.Empty, At("head").Look);
        Assert.Same(wig, In(w, () => bobBody.head));
        Assert.Null(In(w, () => body.get_item_for_held_index(active)));
        click = InventoryRules.Click(Slot("head"), ClickModifiers.None, active, 0, strip) ?? InvSend.Nothing;
        Assert.Equal(InvSend.StripUse(StripView.IndexOf("head")), click);
        Send(click);
        RunUntil(() => In(w, () => bobBody.head) == null, (int)(In(w, () => wig.strip_delay) * 2) + 3 * Second);
        Assert.Null(In(w, () => bobBody.head));
        Assert.True(In(w, () => wig.loc == bobBody.loc));

        // 7. the X: tg's close; the strip view ends (no strip window), nothing asks for the Tab inventory
        click = InventoryRules.Click(Pick(StripView.CloseId), ClickModifiers.None, active, 0, strip) ?? InvSend.Nothing;
        Assert.Equal(new InvSend(InvSendKind.StripClose), click);
        Send(click);
        RunUntil(() => strip == null, Second);
        Assert.Null(strip);
        Assert.DoesNotContain(replica.UiWindows.Values, v => v.Interface == StripView.Interface);
        Assert.False(NewWindow());
        // opened again: a new window, the flag once more
        Assert.True(session.Drop(new DragRelease(On(bobBody), On(body), ClickButton.Left, ClickModifiers.None, 255)));
        RunUntil(() => strip != null, Second);
        Assert.NotNull(strip);
        Assert.True(NewWindow());
        Assert.False(NewWindow());
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }
}
