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

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 7 (the machine frames and the wall-mounted frames, D148) through the real server on MetaStation. One of Meta's
/// suit storage units is taken apart: the screwdriver opens its maintenance hatch, the crowbar leaves a wired machine frame,
/// its circuit board, its capacitor, its airlock electronics and the board's glass and cable (_machinery.dm:925, :970); the
/// board and the parts go back into the frame by hand and the screwdriver finishes a new suit storage unit (machine_frame.dm).
/// Then a fire alarm frame is mounted on the Auxiliary Base zone's west wall (wall_mounted.dm:48), takes its electronics and
/// five cable, the screwdriver closes it (firealarm.dm), and an empty hand pulls it: the area's fire alarm goes off. The
/// client sees the frame's states (box_1, box_2) and the alarm's (fire_b0, fire_b1, fire_b2).
/// </summary>
public class MachineFrameGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int WallX = 42, WallY = 155; // the zone's west wall

    private readonly ITestOutputHelper _output;

    public MachineFrameGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static void EmptyHands(RealHeadlessWorld w, mob_living_carbon_human me, bool delete)
    {
        using (w.World.Enter())
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                {
                    if (delete)
                        GlobalProcs.qdel(old);
                    else
                        Waiting.NoSleep(me.dropItemToGround(old));
                }
    }

    private static T Give<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        EmptyHands(w, me, delete: true);
        using (w.World.Enter())
        {
            DevCommands.Give(me, path);
            return me.get_active_held_item() as T;
        }
    }

    private static bool Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, float seconds = 1, ClickButton button = ClickButton.Left)
    {
        bool sent = p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None);
        w.Run((int)(seconds * Second));
        return sent;
    }

    private static string StateOf(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).First();
    }

    [MetaServerFact]
    public void A_suit_storage_unit_is_taken_apart_and_rebuilt_and_a_fire_alarm_frame_is_mounted_and_finished()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 58, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        // ---- 1. A suit storage unit of Meta's: its board and default parts (Initialize, circuitboard.dm:141).
        obj_machinery_suit_storage_unit unit = In(w, () => Dm.each(Dm.world).OfType<obj_machinery_suit_storage_unit>()
            .First(u => u.z == z && !Dm.truthy(u.state_open) && u.GetType() == typeof(obj_machinery_suit_storage_unit_standard_unit)));
        int ux = In(w, () => (int)unit.x), uy = In(w, () => (int)unit.y);
        _output.WriteLine($"the unit at ({ux},{uy}): parts {In(w, () => string.Join(", ", Dm.each(unit.component_parts).Select(o => o.GetType().Name)))}");
        Assert.NotNull(In(w, () => unit.circuit_value));
        int sx = ux, sy = uy - 1;
        if (In(w, () => ((turf)Builtins.locate(sx, sy, z)).density != 0))
            sy = uy + 1;
        Put(w, me, sx, sy);
        Give<obj_item_screwdriver>(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, unit));
        Assert.True(In(w, () => Dm.truthy(unit.panel_open)), "the maintenance hatch is open");
        Give<obj_item_crowbar>(w, me, "/obj/item/crowbar");
        Assert.True(Click(w, p, unit));
        Assert.True(In(w, () => GlobalProcs.QDELETED(unit)));
        var frame = Assert.Single(On<obj_structure_frame_machine>(w, ux, uy, z));
        Assert.Equal(FRAME_STATE_WIRED, In(w, () => Dm.num(frame.state)));
        Assert.Equal("box_1", StateOf(replica, frame));
        var board = Assert.Single(On<obj_item_circuitboard_machine_suit_storage_unit>(w, ux, uy, z));
        var parts = new List<obj_item> { board };
        parts.Add(Assert.Single(On<obj_item_stock_parts_capacitor>(w, ux, uy, z)));
        parts.Add(On<obj_item_electronics_airlock>(w, ux, uy, z).First());
        parts.Add(On<obj_item_stack_sheet_glass>(w, ux, uy, z).First());
        parts.Add(On<obj_item_stack_cable_coil>(w, ux, uy, z).First());
        Assert.Equal(2f, In(w, () => Dm.num(((obj_item_stack)parts[3]).amount)));
        Assert.Equal(5f, In(w, () => Dm.num(((obj_item_stack)parts[4]).amount)));

        // ---- 2. The board and each part back in by hand, then the screwdriver: a new unit.
        foreach (obj_item part in parts)
        {
            EmptyHands(w, me, delete: true);
            using (w.World.Enter())
                Waiting.NoSleep(me.put_in_active_hand(part));
            Assert.True(Click(w, p, frame));
            if (part == board)
                Assert.Equal("box_2", StateOf(replica, frame));
        }
        Assert.All(In(w, () => Dm.each(frame.req_components).Select(k => Dm.num(frame.req_components[k])).ToList()), n => Assert.Equal(0f, n));
        Give<obj_item_screwdriver>(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, frame));
        Assert.True(In(w, () => GlobalProcs.QDELETED(frame)));
        var built = Assert.Single(On<obj_machinery_suit_storage_unit>(w, ux, uy, z));
        Assert.NotNull(In(w, () => built.circuit_value));
        Assert.Contains(In(w, () => Dm.each(built.component_parts).ToList()), o => o is datum_stock_part_capacitor);
        Assert.True(replica.Entities.Any(e => e.Id == built.dm_id), "the client sees the new unit");

        // ---- 3. A fire alarm frame on the zone's west wall, finished and pulled.
        Put(w, me, WallX + 1, WallY);
        using (w.World.Enter())
            Waiting.NoSleep(me.setDir(Dm.WEST));
        var wallframe = Give<obj_item_wallframe_firealarm>(w, me, "/obj/item/wallframe/firealarm");
        heard.Clear();
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, WallX, WallY, 1), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => GlobalProcs.QDELETED(wallframe)), "the frame is mounted");
        var alarm = Assert.Single(On<obj_machinery_firealarm>(w, WallX + 1, WallY, z));
        Assert.Equal((float)Dm.WEST, In(w, () => Dm.num(alarm.dir)));
        Assert.Equal("fire_b0", StateOf(replica, alarm));
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File == "sound/machines/click.ogg");
        Give<obj_item_electronics_firealarm>(w, me, "/obj/item/electronics/firealarm");
        Assert.True(Click(w, p, alarm));
        Assert.Equal("fire_b1", StateOf(replica, alarm));
        Give<obj_item_stack_cable_coil>(w, me, "/obj/item/stack/cable_coil");
        Assert.True(Click(w, p, alarm));
        Assert.Equal(FIRE_ALARM_BUILD_SECURED, In(w, () => Dm.num(alarm.buildstage)));
        Assert.Equal("fire_b2", StateOf(replica, alarm));
        Give<obj_item_screwdriver>(w, me, "/obj/item/screwdriver");
        Assert.True(Click(w, p, alarm));
        Assert.False(In(w, () => Dm.truthy(alarm.panel_open)), "the alarm is closed");
        EmptyHands(w, me, delete: true);
        Assert.True(Click(w, p, alarm, 2));
        Assert.Equal(1f, In(w, () => Dm.num(alarm.my_area.active_alarms[ALARM_FIRE])));
        _output.WriteLine($"the alarm shows {StateOf(replica, alarm)}");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
