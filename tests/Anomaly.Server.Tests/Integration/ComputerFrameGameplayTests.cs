using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 computer frames (D171, buildandrepair.dm) through the real server on MetaStation: an engineer takes the Bridge power
/// monitoring console apart with a screwdriver and then rebuilds it stage by stage with a crowbar, a wirecutter, the board, its
/// cable and glass, each by clicks. The client replica sees the frame's icon_state of every stage ("4" down to "0" and up again),
/// hears the tools and sees the new console light its screen.
/// </summary>
public class ComputerFrameGameplayTests(ITestOutputHelper output)
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

    private static List<T> On<T>(RealHeadlessWorld w, int x, int y, int z)
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().ToList());

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { Dm.SOUTH, Dm.NORTH, Dm.EAST, Dm.WEST })
        {
            turf t = Builtins.get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static void Hold(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item)
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old && old != item)
                    GlobalProcs.qdel(old);
            if (!Dm.each(me.held_items).Contains(item))
                Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(item))));
        }
    }

    private static obj_item Give(RealHeadlessWorld w, mob_living_carbon_human me, string path)
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                    GlobalProcs.qdel(old);
            DevCommands.Give(me, path);
            return me.get_active_held_item() as obj_item;
        }
    }

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 1)
    {
        bool sent = p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    private static string StateOf(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).First();
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return shown;
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        }
        return shown;
    }

    [MetaServerFact]
    public void An_engineer_takes_the_bridge_power_monitor_apart_and_rebuilds_it_and_the_client_sees_each_stage_and_hears_the_tools()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        bool Heard(string filePrefix) => heard.Any(e => e.Kind == SoundKind.Play && e.File.StartsWith(filePrefix, StringComparison.Ordinal));

        var monitors = In(w, () => Dm.each_cast<obj_machinery_computer_monitor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_monitor))).ToList());
        obj_machinery_computer_monitor bridge = monitors.Single(m => In(w, () => Dm.str(m.name)) == "Bridge Power Monitoring Console");
        int z = In(w, () => (int)bridge.z), cx = In(w, () => (int)bridge.x), cy = In(w, () => (int)bridge.y);
        turf stand = In(w, () => Beside(bridge));
        Assert.NotNull(stand);
        In(w, () => Waiting.NoSleep(me.forceMove(stand)));
        w.Run(Second);
        var board = In(w, () => bridge.circuit_value);
        Assert.IsType<obj_item_circuitboard_computer_powermonitor>(board);
        Assert.Contains("power", OverlaysOf(replica, bridge));

        // ---- 1. The screwdriver takes the monitor out: a glassed frame, state 4, anchored and facing as the console did
        Give(w, me, "/obj/item/screwdriver");
        heard.Clear();
        Assert.True(Click(w, p, bridge, 3));
        Assert.True(In(w, () => GlobalProcs.QDELETED(bridge)));
        Assert.True(Heard("sound/items/tools/screwdriver"), "the screwdriver is heard");
        var frame = Assert.Single(On<obj_structure_frame_computer>(w, cx, cy, z));
        Assert.Equal(FRAME_COMPUTER_STATE_GLASSED, In(w, () => Dm.num(frame.state)));
        Assert.True(In(w, () => Dm.truthy(frame.anchored)));
        Assert.Equal("4", StateOf(replica, frame));

        // ---- 2. Down to the bare frame: the crowbar (glass), the wirecutter (cable), the screwdriver and the crowbar (board)
        Give(w, me, "/obj/item/crowbar");
        heard.Clear();
        Assert.True(Click(w, p, frame));
        Assert.Equal("3", StateOf(replica, frame));
        Assert.True(Heard("sound/items/tools/crowbar"), "the crowbar is heard");
        var glass = Assert.Single(On<obj_item_stack_sheet_glass>(w, cx, cy, z));
        Give(w, me, "/obj/item/wirecutters");
        heard.Clear();
        Assert.True(Click(w, p, frame));
        Assert.Equal("2", StateOf(replica, frame));
        Assert.True(Heard("sound/items/tools/wirecutter"), "the wirecutter is heard");
        var cable = Assert.Single(On<obj_item_stack_cable_coil>(w, cx, cy, z));
        Give(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, frame));
        Assert.Equal("1", StateOf(replica, frame));
        Give(w, me, "/obj/item/crowbar");
        Assert.True(Click(w, p, frame));
        Assert.Equal("0", StateOf(replica, frame));
        Assert.Contains(board, On<obj_item_circuitboard>(w, cx, cy, z));

        // ---- 3. Up: the board by hand, the screwdriver, the cable, the glass, the screwdriver
        Hold(w, me, board);
        heard.Clear();
        Assert.True(Click(w, p, frame));
        Assert.Equal("1", StateOf(replica, frame));
        Assert.True(Heard("sound/items/deconstruct"), "the board clicks in");
        Give(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, frame));
        Assert.Equal("2", StateOf(replica, frame));
        Hold(w, me, cable);
        Assert.True(Click(w, p, frame, 3));
        Assert.Equal("3", StateOf(replica, frame));
        Hold(w, me, glass);
        heard.Clear();
        Assert.True(Click(w, p, frame, 3));
        Assert.Equal("4", StateOf(replica, frame));
        Assert.True(Heard("sound/items/deconstruct"), "the glass goes on");
        Give(w, me, "/obj/item/screwdriver");
        heard.Clear();
        Assert.True(Click(w, p, frame));
        Assert.True(In(w, () => GlobalProcs.QDELETED(frame)));
        Assert.True(Heard("sound/items/tools/screwdriver"), "the screwdriver completes it");
        w.Run(Second);

        // ---- 4. The new console: the same board, anchored, screen and keyboard drawn by the client
        var built = Assert.Single(On<obj_machinery_computer_monitor>(w, cx, cy, z));
        Assert.Same(board, In(w, () => built.circuit_value));
        Assert.True(In(w, () => Dm.truthy(built.anchored)));
        Assert.True(replica.Entities.Any(e => e.Id == built.dm_id), "the client sees the new console");
        Assert.Contains("power", OverlaysOf(replica, built));
        Assert.Contains("power_key", OverlaysOf(replica, built));
        output.WriteLine($"the rebuilt console overlays: {string.Join(", ", OverlaysOf(replica, built))}");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
