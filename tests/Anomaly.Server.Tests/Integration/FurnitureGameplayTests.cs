using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 7 through the real server on MetaStation, in the Auxiliary Base Construction zone (plan section 4): the player
/// takes rods and iron from the aux base tables, builds tg's "table frame" from the rods' StackCrafting window and puts one
/// iron sheet on it (table_frames.dm:54: the table), builds a stool (a chair) from the iron's window, takes it apart with the
/// wrench's right click (1 iron, chair.dm:80), opens a locker and cuts it apart with a lit welder (2 iron,
/// closets.dm:924), and turns the room's air vent off, unwrenches it into its fitting and wrenches the fitting down again (the vent joins the
/// pipenet again, pipe/construction.dm:268). The client sees the frame, the table, the chair, the fitting and the vent come and
/// go and hears the tools.
/// </summary>
public class FurnitureGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int TableX = 51, TableY = 149; // the aux base table: iron x2 fifty, glass fifty
    private const int RodsX = 51, RodsY = 148; // rods fifty and plasteel 10
    private const int VentX = 48, VentY = 148; // the room's air vent
    private const int ZoneX = 45, ZoneY = 154; // the zone's plating

    private readonly ITestOutputHelper _output;

    public FurnitureGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static List<T> On<T>(RealHeadlessWorld w, int x, int y, int z)
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().ToList());

    private static float Sheets<T>(RealHeadlessWorld w, int x, int y, int z) where T : obj_item_stack
        => In(w, () => On<T>(w, x, y, z).Where(s => s.GetType() == typeof(T)).Sum(s => Dm.num(s.amount)));

    private static T Tool<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                    Waiting.NoSleep(me.dropItemToGround(old));
            DevCommands.Give(me, path);
            return me.get_active_held_item() as T;
        }
    }

    private static void Take(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, obj_item item)
    {
        using (w.World.Enter())
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                    Waiting.NoSleep(me.dropItemToGround(old));
        w.Run(2);
        Assert.True(Click(w, p, item));
        Assert.True(In(w, () => Dm.truthy(me.is_holding(item))), "the item is in hand");
    }

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 1, ClickButton button = ClickButton.Left)
    {
        bool sent = p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static bool Sees(ReplicaWorld replica, atom a) => replica.Entities.Any(e => e.Id == a.dm_id);

    [MetaServerFact]
    public void A_table_and_a_chair_are_built_a_locker_is_cut_apart_and_a_vent_is_unwrenched_and_wrenched_down()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 57, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        // ---- 1. Rods from the table: their window's "table frame" (2 rods, 1 s, rods.dm:3) on a zone tile.
        Put(w, me, RodsX - 1, RodsY);
        obj_item_stack_rods rods = On<obj_item_stack_rods>(w, RodsX, RodsY, z).First();
        Take(w, p, me, rods);
        Put(w, me, ZoneX, ZoneY);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("StackCrafting", window.Interface);
        Shown(windows, replica, window.Window).Walk().First(n => n.Key == "table frame").OnClick();
        w.Run(2 * Second);
        var frame = Assert.Single(On<obj_structure_table_frame>(w, ZoneX, ZoneY, z));
        Assert.True(Sees(replica, frame), "the client sees the table frame");
        Assert.Equal("table_frame", In(w, () => frame.icon_state));
        Assert.True(p.Session.UiClose(window.Window));
        w.Run(Second);
        Assert.Null(Shown(windows, replica, window.Window)); // the client's frame loop sees the window close

        // ---- 2. Iron from the table: one sheet on the frame makes the table (table_frames.dm:54, 2 s).
        Put(w, me, TableX - 1, TableY - 1);
        obj_item_stack_sheet_iron iron = On<obj_item_stack_sheet_iron>(w, TableX, TableY, z).First();
        Take(w, p, me, iron);
        float before = In(w, () => Dm.num(iron.amount));
        Put(w, me, ZoneX + 1, ZoneY);
        Assert.True(Click(w, p, frame, 3));
        var table = Assert.Single(On<obj_structure_table>(w, ZoneX, ZoneY, z));
        Assert.Equal(before - 1, In(w, () => Dm.num(iron.amount)));
        Assert.True(Sees(replica, table), "the client sees the table");
        Assert.False(Sees(replica, frame), "the frame is gone from the client");

        // ---- 3. The iron's window: a stool (metal_recipes, sheet_types.dm:18) on the player's tile, then the wrench's right click.
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        UiNode content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "stool").OnClick();
        w.Run(2 * Second);
        var chair = Assert.Single(On<obj_structure_chair_stool>(w, ZoneX + 1, ZoneY, z));
        Assert.Equal(before - 2, In(w, () => Dm.num(iron.amount)));
        Assert.True(Sees(replica, chair), "the client sees the stool");
        Assert.True(p.Session.UiClose(window.Window));
        w.Run(Second);
        Put(w, me, ZoneX + 2, ZoneY);
        Tool<obj_item_wrench>(w, me, "/obj/item/wrench");
        heard.Clear();
        Assert.True(Click(w, p, chair, 1, ClickButton.Right));
        Assert.True(In(w, () => GlobalProcs.QDELETED(chair)));
        Assert.Equal(1f, Sheets<obj_item_stack_sheet_iron>(w, ZoneX + 1, ZoneY, z)); // chair.dm:80 (buildstackamount 1)
        Assert.False(Sees(replica, chair), "the stool is gone from the client");
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File == "sound/items/tools/ratchet.ogg");

        // ---- 4. A locker opened by hand and cut apart with a lit welder (closets.dm:924, 4 s): its two iron sheets.
        var locker = In(w, () => Dm.@new<obj_structure_closet>(Builtins.locate(ZoneX + 3, ZoneY + 2, z)));
        w.Run(Second);
        Put(w, me, ZoneX + 3, ZoneY + 1);
        using (w.World.Enter())
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                    GlobalProcs.qdel(old);
        Assert.True(Click(w, p, locker));
        Assert.True(In(w, () => Dm.truthy(locker.opened)), "the locker is open");
        var welder = Tool<obj_item_weldingtool>(w, me, "/obj/item/weldingtool");
        p.Session.Keybinding(KeybindingCode.ActivateInhand); // light it
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(welder.welding)), "the welder is lit");
        heard.Clear();
        Assert.True(Click(w, p, locker, 5));
        Assert.True(In(w, () => GlobalProcs.QDELETED(locker)));
        Assert.Equal(2f, Sheets<obj_item_stack_sheet_iron>(w, ZoneX + 3, ZoneY + 2, z));
        Assert.False(Sees(replica, locker), "the locker is gone from the client");
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File.StartsWith("sound/items/tools/welder", StringComparison.Ordinal));
        p.Session.Keybinding(KeybindingCode.ActivateInhand); // put it out
        w.Run(Second);

        // ---- 5. The room's air vent unwrenched (atmosmachinery.dm:425) into its fitting, the fitting wrenched down again.
        var vent = Assert.Single(On<obj_machinery_atmospherics_components_unary_vent_pump>(w, VentX, VentY, z));
        Type ventType = vent.GetType();
        _output.WriteLine($"vent {In(w, () => vent.name)}, its pipeline {In(w, () => vent.parents[1] != null)}");
        Put(w, me, VentX - 1, VentY);
        Tool<obj_item_wrench>(w, me, "/obj/item/wrench");
        // a running vent refuses the wrench ("turn it off first!", vent_pump.dm:337)
        Assert.True(Click(w, p, vent, 3));
        Assert.False(In(w, () => GlobalProcs.QDELETED(vent)), "the running vent stays");
        // turned off as the room's air alarm does (its AirAlarm window is not ported yet: the vent's own set_on)
        using (w.World.Enter())
            vent.set_on(Dm.FALSE);
        Assert.True(Click(w, p, vent, 4));
        Assert.True(In(w, () => GlobalProcs.QDELETED(vent)));
        var fitting = Assert.Single(On<obj_item_pipe>(w, VentX, VentY, z));
        Assert.Equal(typeof(obj_item_pipe_directional_vent), fitting.GetType());
        Assert.Equal(ventType, In(w, () => fitting.pipe_type)); // make_from_existing (construction.dm:183)
        Assert.True(Sees(replica, fitting), "the client sees the vent fitting");
        Put(w, me, VentX - 1, VentY); // the pressure may have thrown the player
        Assert.True(Click(w, p, fitting, 2));
        Assert.True(In(w, () => GlobalProcs.QDELETED(fitting)));
        var rebuilt = Assert.Single(On<obj_machinery_atmospherics_components_unary_vent_pump>(w, VentX, VentY, z));
        w.Run(3 * Second);
        Assert.NotNull(In(w, () => rebuilt.parents[1] as datum_pipeline));
        Assert.True(In(w, () => Dm.@in(rebuilt, ((datum_pipeline)rebuilt.parents[1]).other_atmos_machines)), "the vent is in its pipeline again");
        Assert.True(Sees(replica, rebuilt), "the client sees the vent again");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
