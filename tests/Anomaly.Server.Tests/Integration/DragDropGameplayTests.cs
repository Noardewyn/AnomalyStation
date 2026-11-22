using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Drag and drop through the real server on MetaStation (protocol version 17): the client's Drop action reaches tg's /client/MouseDrop.
/// The assistant drags the backpack from the back slot (the item drawn in the slot, a version-12 vis child) onto the empty inactive
/// hand slot: tg's storage on_mousedrop_onto takes it off the back into that hand, and the client sees it move from the back slot to
/// the hand slot. A jetpack then goes on the free back with E. The backpack, dragged from the hand onto a table, dumps its contents onto
/// the table's tile after tg's 2 seconds (storage dump_content_at); a plain item dragged onto the table lies on it (tg has no handler
/// for it: the port's gesture, D225, is tg's click on the table with it in hand). Forged drops (another player's HUD, a thing out of view) are dropped,
/// never a strike.
/// </summary>
public class DragDropGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public DragDropGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, m.z)));
        w.Run(Second);
    }

    private static long ScreenId(RealHeadlessWorld w, mob_living_carbon_human me, string key)
        => In(w, () => ((atom)me.hud_used.screen_objects[key]).dm_id);

    // The screen entry whose vis children (version 12) show the item, or null.
    private static ScreenEntry? HolderOf(ReplicaWorld replica, long item)
    {
        foreach (ScreenEntry e in replica.Screen.Values)
            if (e.VisChildren != null && e.VisChildren.Contains(item))
                return e;
        return null;
    }

    private static void Drop(RealHeadlessWorld w, RealHeadlessWorld.Player p, ClickTarget source, ClickTarget over, byte deciseconds = 4)
    {
        Assert.True(p.Session.Drop(new DragRelease(source, over, ClickButton.Left, ClickModifiers.None, deciseconds)));
        w.Run(Second);
    }

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    // A table on the level with an open, empty floor tile west of it, nearest to (x, y).
    private static (obj_structure_table Table, int X, int Y) TableNear(RealHeadlessWorld w, int x, int y, int z) => In(w, () =>
    {
        foreach (obj_structure_table table in Dm.each<obj_structure_table>(Dm.world)
            .Where(t => Dm.eq(t.z, z) && t.GetType() == typeof(obj_structure_table))
            .OrderBy(t => Math.Abs(Dm.num(t.x) - x) + Math.Abs(Dm.num(t.y) - y)))
        {
            if (Builtins.locate(Dm.num(table.x) - 1, Dm.num(table.y), z) is turf_open floor && !Dm.each_cast<atom>(floor.contents).Any(a => Dm.truthy(a.density)))
                return (table, (int)Dm.num(table.x) - 1, (int)Dm.num(table.y));
        }
        return (null, 0, 0);
    });

    [MetaServerFact]
    public void The_assistant_drags_the_backpack_off_into_a_hand_wears_a_jetpack_and_empties_the_backpack_onto_a_table()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        RealHeadlessWorld.Player other = w.Join("Other");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var them = (mob_living_carbon_human)w.MobOf(other);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        w.Run(Second);

        // ---- 1. The backpack, worn on the back: the client draws it in the back slot (a vis child of the slot's screen entry).
        var pack = In(w, () => me.back as obj_item_storage_backpack);
        Assert.NotNull(pack);
        int contents = In(w, () => (int)Dm.length(pack.contents));
        _output.WriteLine($"the assistant's {In(w, () => pack.name)} holds {contents} things");
        Assert.True(contents > 0, "the assistant's backpack has its tg contents");
        long backSlot = ScreenId(w, me, Dm.text("item_slot:[]", Defines.ITEM_SLOT_BACK));
        Assert.Equal(backSlot, HolderOf(replica, pack.dm_id)?.Id);

        // ---- 2. Drag it onto the empty inactive hand slot: it leaves the back and is held there (tg's storage on_mousedrop_onto).
        int active = In(w, () => (int)me.active_hand_index);
        int inactive = active == 1 ? 2 : 1;
        long handSlot = ScreenId(w, me, Dm.text("hand_slot:[]", inactive));
        Assert.Null(In(w, () => me.get_item_for_held_index(inactive)));
        Drop(w, p, Screen(pack.dm_id), Screen(handSlot));
        Assert.Null(In(w, () => me.back));
        Assert.Same(pack, In(w, () => me.get_item_for_held_index(inactive)));
        Assert.Equal(handSlot, HolderOf(replica, pack.dm_id)?.Id); // the client sees it in the hand slot now
        Assert.Null(In(w, () => me.get_active_held_item()));
        Session s = w.Host.SessionOf(1);
        Assert.Equal((1, 0), (s.DropsApplied, s.DropsRejected));

        // ---- 3. A jetpack on the free back with E: picked up with the empty active hand, then quick-equipped.
        (int sx, int sy) = In(w, () => ((int)me.x, (int)me.y));
        w.Host.EnqueueConsoleLine($"spawn /obj/item/tank/jetpack/carbondioxide {sx} {sy} {z}");
        w.Run(Second);
        var jet = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(sx, sy, z)).contents).OfType<obj_item_tank_jetpack_carbondioxide>().FirstOrDefault());
        Assert.NotNull(jet);
        Assert.True(p.Session.Click(jet.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(jet, In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
        Assert.Same(jet, In(w, () => me.back));
        Assert.Equal(backSlot, HolderOf(replica, jet.dm_id)?.Id);

        // ---- 4. The backpack from the hand onto a table: its contents are dumped onto the table's tile after tg's 2 s do_after.
        var (table, tx, ty) = TableNear(w, sx, sy, z);
        Assert.NotNull(table);
        _output.WriteLine($"a table at ({In(w, () => table.x)},{In(w, () => table.y)}); the assistant west of it at ({tx},{ty})");
        Put(w, me, tx, ty);
        Assert.Contains(replica.Entities, e => e.Id == table.dm_id);
        turf tableTile = In(w, () => (turf)table.loc);
        int before = In(w, () => Dm.each_cast<atom>(tableTile.contents).OfType<obj_item>().Count());
        Drop(w, p, Screen(pack.dm_id), Entity(table.dm_id));
        Assert.Equal(contents, In(w, () => (int)Dm.length(pack.contents))); // not before the do_after ends
        w.Run(2 * Second);
        Assert.Equal(0, In(w, () => (int)Dm.length(pack.contents)));
        int after = In(w, () => Dm.each_cast<atom>(tableTile.contents).OfType<obj_item>().Count());
        _output.WriteLine($"items on the table's tile: {before} before, {after} after");
        Assert.Equal(before + contents, after);

        // ---- 5. A plain item dragged onto the table: tg has no handler for it; the port's gesture (D225) is tg's click on the table with
        // it in hand (table_place), so it lies on the table.
        obj_item first = In(w, () => Dm.each_cast<atom>(tableTile.contents).OfType<obj_item>().Last(i => i.atom_storage == null)); // a box would dump (tg's)
        Assert.True(p.Session.Click(first.dm_id, ClickButton.Left, ClickModifiers.None)); // picked up off the table into the empty hand
        w.Run(Second);
        Assert.Same(first, In(w, () => me.get_active_held_item()));
        Drop(w, p, Screen(first.dm_id), Entity(table.dm_id));
        Assert.Same(tableTile, In(w, () => first.loc));

        // ---- 6. Forged drops: another player's hand slot, a thing out of view; dropped and counted, never a strike.
        long theirHand = ScreenId(w, them, Dm.text("hand_slot:[]", 1));
        Drop(w, p, Screen(pack.dm_id), Screen(theirHand));
        Drop(w, p, Entity(9_999_999), Entity(table.dm_id));
        Assert.Same(me, In(w, () => pack.loc));
        Assert.Equal(2, s.DropsRejected);
        Assert.Equal(0, s.Strikes);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        GC.KeepAlive(other);
    }
}
