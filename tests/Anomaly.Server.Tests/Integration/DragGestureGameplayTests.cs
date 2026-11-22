using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8y slice D (D224-D226, D230) on MetaStation through the real server: the cursor mode's drags of items (the client's Drop action) and the
/// radial's Buckle, Unbuckle, Pull and Stop pulling (the server's context hints, version 36). A pen from the floor goes into the hand slot
/// it is dropped on and onto the player into his free hand; a full hand and full hands say so. The backpack from the floor goes on the empty
/// back slot; a pen dropped on the worn backpack's slot, on a box on the floor and on the open backpack's cells goes in; a bulky one is "too
/// big!". On the empty back slot a pen is picked up and refused with tg's line; on a jetpack's slot it is "slot taken". A held pen and a
/// floor pen dropped on a table lie on it; a held pen dropped on the next floor lies there, on a far floor tg says it is too far away; an
/// anchored pen stays. The radial on the medbay lobby's chair buckles the player and unbuckles him; on a crate it pulls it and stops.
/// </summary>
public class DragGestureGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int ChairX = 93, ChairY = 103;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        Do(w, () => Waiting.NoSleep(m.forceMove(locate(x, y, m.z))));
        w.Run(Second);
    }

    private static T Spawn<T>(RealHeadlessWorld w, atom loc) where T : atom_movable
    {
        T thing = In(w, () => Dm.@new<T>(loc));
        w.Run(2);
        return thing;
    }

    internal static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    internal static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    internal static ClickTarget Cell(turf t) => new(TargetKind.Turf, 0, (int)Dm.num(t.x), (int)Dm.num(t.y), (int)Dm.num(t.z), 16, 16);

    private static void Drop(RealHeadlessWorld w, RealHeadlessWorld.Player p, ClickTarget source, ClickTarget over)
    {
        Assert.True(p.Session.Drop(new DragRelease(source, over, ClickButton.Left, ClickModifiers.None, 5)));
        w.Run(Second);
    }

    internal static long ScreenId(RealHeadlessWorld w, mob m, string key) => In(w, () => ((atom)m.hud_used.screen_objects[key]).dm_id);

    // A table with an open, empty floor tile west of it, nearest to (x, y) (DragDropGameplayTests).
    internal static (obj_structure_table Table, int X, int Y) TableNear(RealHeadlessWorld w, int x, int y, int z) => In(w, () =>
    {
        foreach (obj_structure_table table in Dm.each<obj_structure_table>(Dm.world)
            .Where(t => Dm.eq(t.z, z) && t.GetType() == typeof(obj_structure_table))
            .OrderBy(t => Math.Abs(Dm.num(t.x) - x) + Math.Abs(Dm.num(t.y) - y)))
        {
            if (locate(Dm.num(table.x) - 1, Dm.num(table.y), z) is turf_open floor && !Dm.each_cast<atom>(floor.contents).Any(a => Dm.truthy(a.density)))
                return (table, (int)Dm.num(table.x) - 1, (int)Dm.num(table.y));
        }
        return (null, 0, 0);
    });

    // The radial's entries for the target now: a hover (a re-hover, as the radial's opening sends), the server's hints, RadialModel's build.
    private static List<RadialEntry> Radial(RealHeadlessWorld w, RealHeadlessWorld.Player p, List<ContextHintsMessage> hints, atom target)
    {
        hints.Clear();
        w.Run(4);
        p.Session.Rehover();
        p.Session.Hover(Entity(target.dm_id), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == target.dm_id);
        Assert.NotNull(answer);
        return RadialModel.Build(answer, 0, true);
    }

    [MetaServerFact]
    public void Items_dragged_into_hands_slots_containers_onto_tables_and_floors_and_the_radial_buckles_and_pulls()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 64, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Medic");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var texts = new List<ServerText>();
        p.Client.ServerTextReceived += texts.Add;
        var hints = new List<ContextHintsMessage>();
        p.Client.ContextHintsReceived += hints.Add;
        bool Told(string type, string text) => texts.Any(t => t.Type == type && t.Html.Contains(text, StringComparison.Ordinal));
        w.Run(Second);

        Put(w, me, ChairX + 1, ChairY + 1);
        turf here = In(w, () => (turf)me.loc), west = In(w, () => (turf)locate(ChairX, ChairY + 1, z));
        Do(w, () =>
        {
            foreach (obj_item held in Dm.each_cast<obj_item>(me.held_items).Where(i => i != null).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
        });
        int active = In(w, () => (int)me.active_hand_index), inactive = active == 1 ? 2 : 1;
        long inactiveHand = ScreenId(w, me, Dm.text("hand_slot:[]", inactive));
        long backSlot = ScreenId(w, me, Dm.text("item_slot:[]", ITEM_SLOT_BACK));

        // ---- 1. Hands: a floor pen into the hand slot it is dropped on; a second one there: "hand full"; onto himself: the free hand;
        // a third onto himself: "hands full".
        var a = Spawn<obj_item_pen>(w, here);
        Drop(w, p, Entity(a.dm_id), Screen(inactiveHand));
        Assert.Same(a, In(w, () => me.get_item_for_held_index(inactive)));
        var b = Spawn<obj_item_pen>(w, here);
        Drop(w, p, Entity(b.dm_id), Screen(inactiveHand));
        Assert.True(Told("balloon", "hand full"));
        Drop(w, p, Entity(b.dm_id), Entity(me.dm_id)); // the health doll in first person (DragModel.SelfOnDoll)
        Assert.Same(b, In(w, () => me.get_item_for_held_index(active)));
        var c = Spawn<obj_item_pen>(w, here);
        Drop(w, p, Entity(c.dm_id), Entity(me.dm_id));
        Assert.True(Told("balloon", "hands full"));
        Do(w, () => { Waiting.NoSleep(me.dropItemToGround(a)); Waiting.NoSleep(me.dropItemToGround(b)); });
        output.WriteLine("hands ok");

        // ---- 2. The backpack from the floor onto the empty back slot; a pen on its slot, on a box on the floor, on its open cells: in;
        // a bulky pen: "too big!".
        var pack = In(w, () => me.back as obj_item_storage_backpack);
        Assert.NotNull(pack);
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(pack)));
        w.Run(Second);
        Drop(w, p, Entity(pack.dm_id), Screen(backSlot));
        Assert.Same(pack, In(w, () => me.back));
        Drop(w, p, Entity(c.dm_id), Screen(backSlot));
        Assert.Same(pack, In(w, () => c.loc));
        var box = Spawn<obj_item_storage_box>(w, west);
        var d = Spawn<obj_item_pen>(w, here);
        Drop(w, p, Entity(d.dm_id), Entity(box.dm_id));
        Assert.Same(box, In(w, () => d.loc));
        var bulky = Spawn<obj_item_pen>(w, here);
        Do(w, () => bulky.w_class = WEIGHT_CLASS_BULKY);
        Drop(w, p, Entity(bulky.dm_id), Entity(box.dm_id));
        Assert.True(Told("balloon", "too big!"));
        Assert.Same(here, In(w, () => bulky.loc));
        Do(w, () => Waiting.NoSleep(pack.atom_storage.open_storage(me)));
        w.Run(2);
        long cell = In(w, () => Dm.each_cast<object>(me.client.screen).OfType<atom_movable_screen_storage_cell>().First().dm_id);
        var e = Spawn<obj_item_pen>(w, here);
        Drop(w, p, Entity(e.dm_id), Screen(cell));
        Assert.Same(pack, In(w, () => e.loc));
        Do(w, () => pack.atom_storage.hide_contents(me));
        output.WriteLine("containers ok");

        // ---- 3. The empty back slot refuses a pen with tg's line (picked up, it stays in the hand); a jetpack's slot is "slot taken".
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(pack)));
        w.Run(Second);
        Drop(w, p, Entity(bulky.dm_id), Screen(backSlot));
        Assert.Contains(texts, t => t.Html.Contains("You are unable to equip that!", StringComparison.Ordinal));
        Assert.Same(bulky, In(w, () => me.get_active_held_item()));
        var jet = Spawn<obj_item_tank_jetpack_carbondioxide>(w, here);
        Do(w, () => Waiting.NoSleep(me.equip_to_slot_if_possible(jet, ITEM_SLOT_BACK)));
        w.Run(Second);
        var f = Spawn<obj_item_pen>(w, here);
        Drop(w, p, Entity(f.dm_id), Screen(backSlot));
        Assert.True(Told("balloon", "slot taken"));
        Assert.Same(here, In(w, () => f.loc));
        output.WriteLine("slots ok");

        // ---- 4. A table: the held pen and a floor pen dropped on it lie on it (tg's click with the pen in hand, table_place).
        (int hx, int hy) = In(w, () => ((int)me.x, (int)me.y));
        var (table, tx, ty) = TableNear(w, hx, hy, z);
        Assert.NotNull(table);
        Put(w, me, tx, ty);
        turf tableTile = In(w, () => (turf)table.loc), standing = In(w, () => (turf)me.loc);
        Drop(w, p, Screen(bulky.dm_id), Entity(table.dm_id));
        Assert.Same(tableTile, In(w, () => bulky.loc));
        var g = Spawn<obj_item_pen>(w, standing);
        Drop(w, p, Entity(g.dm_id), Entity(table.dm_id));
        Assert.Same(tableTile, In(w, () => g.loc));
        output.WriteLine($"table at ({In(w, () => table.x)},{In(w, () => table.y)}) ok");

        // ---- 5. Floors: the held pen onto the next open floor lies there; onto a far one, tg's "too far away"; an anchored pen stays.
        var h = Spawn<obj_item_pen>(w, standing);
        Do(w, () => Waiting.NoSleep(me.put_in_active_hand(h)));
        turf next = In(w, () => Dm.each<turf_open>(range(1, me)).First(t => t != me.loc && !Dm.each_cast<atom>(t.contents).Any(x => Dm.truthy(x.density))
            && Dm.truthy(Waiting.NoSleep(t.IsReachableBy(me)))));
        turf far = In(w, () => Dm.each<turf_open>(range(4, me)).First(t => get_dist(me, t) >= 3 && !Dm.each_cast<atom>(t.contents).Any(x => Dm.truthy(x.density))));
        w.Run(Second);
        Drop(w, p, Screen(h.dm_id), Cell(far));
        Assert.Contains(texts, t => t.Html.Contains("You are too far away!", StringComparison.Ordinal));
        Assert.Same(me, In(w, () => h.loc));
        Drop(w, p, Screen(h.dm_id), Cell(next));
        Assert.Same(next, In(w, () => h.loc));
        var anchored = Spawn<obj_item_pen>(w, standing);
        Do(w, () => Waiting.NoSleep(anchored.set_anchored(Dm.TRUE)));
        Drop(w, p, Entity(anchored.dm_id), Screen(inactiveHand));
        Assert.True(Told("balloon", "anchored"));
        Assert.Same(standing, In(w, () => anchored.loc));
        output.WriteLine("floors ok");

        // ---- 6. The radial on the chair: Buckle (a drop of himself onto it), then Unbuckle (the empty hand's click).
        Do(w, () => { foreach (obj_item held in Dm.each_cast<obj_item>(me.held_items).Where(i => i != null).ToList()) Waiting.NoSleep(me.dropItemToGround(held)); });
        Put(w, me, ChairX + 1, ChairY + 1);
        var chair = In(w, () => Dm.each_cast<atom>(((turf)locate(ChairX, ChairY, z)).contents).OfType<obj_structure_chair>().First());
        RadialEntry buckle = Radial(w, p, hints, chair).Single(r => r.Label == "Buckle");
        Assert.Equal(RadialKind.DropSelf, buckle.Kind);
        Assert.True(p.Session.Drop(new DragRelease(Entity(me.dm_id), Entity(chair.dm_id), ClickButton.Left, ClickModifiers.None, 255))); // as GameScene's choice
        w.Run(Second);
        Assert.Same(chair, In(w, () => me.buckled));
        List<RadialEntry> seated = Radial(w, p, hints, chair);
        Assert.DoesNotContain(seated, r => r.Label == "Buckle");
        RadialEntry unbuckle = seated.Single(r => r.Label == "Unbuckle");
        Assert.Equal((RadialKind.Hint, ClickButton.Left, ClickModifiers.None), (unbuckle.Kind, unbuckle.Button, unbuckle.Modifiers));
        Assert.True(p.Session.Click(chair.dm_id, unbuckle.Button, unbuckle.Modifiers));
        w.Run(Second);
        Assert.Null(In(w, () => me.buckled));
        output.WriteLine("buckle ok");

        // ---- 7. The radial on a crate: Pull (the ctrl-click), then Stop pulling (the keybinding).
        (int mx, int my) = In(w, () => ((int)me.x, (int)me.y));
        turf crateSpot = In(w, () => Dm.each<turf_open>(range(1, me)).First(t => t != me.loc && !Dm.each_cast<atom>(t.contents).Any(x => Dm.truthy(x.density))));
        var crate = Spawn<obj_structure_closet_crate>(w, crateSpot);
        w.Run(Second);
        RadialEntry pull = Radial(w, p, hints, crate).Single(r => r.Label == "Pull");
        Assert.Equal((RadialKind.Hint, ClickButton.Left, ClickModifiers.Ctrl), (pull.Kind, pull.Button, pull.Modifiers));
        Assert.True(p.Session.Click(crate.dm_id, pull.Button, pull.Modifiers));
        w.Run(Second);
        Assert.Same(crate, In(w, () => me.pulling));
        List<RadialEntry> pulling = Radial(w, p, hints, crate);
        Assert.DoesNotContain(pulling, r => r.Label == "Pull");
        Assert.Equal(RadialKind.StopPulling, pulling.Single(r => r.Label == "Stop pulling").Kind);
        p.Session.Keybinding(KeybindingCode.StopPulling);
        w.Run(Second);
        Assert.Null(In(w, () => me.pulling));
        output.WriteLine($"pull at ({mx},{my}) ok");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
