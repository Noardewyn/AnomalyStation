using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8y slice A: buckling on MetaStation through the real server (D223). A medic drags his friend in cursor mode onto one of the
/// medbay lobby's chairs (93,103, facing north): tg's 2 s do_after with its warning to the friend, then he sits facing the chair's
/// way (the clients draw him so: his yaw is the chair's dir, DV126), held there with the Buckled alert on his screen; the medic's
/// hand on the chair stands him up. The hallway's emergency medical bed (91,113): the friend dragged onto it lies on it (tg's
/// buckle_lying, the bed's raised picture, its seatbelt sound), and the bed the medic pulls rolls him along. Cuffed on the bed, the
/// friend clicks his Buckled alert and is up only after the cuffs' minute (carbon.dm resist_buckle).
/// </summary>
public class BuckleGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int ChairX = 93, ChairY = 103, BedX = 91, BedY = 113;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(locate(x, y, m.z)));
        w.Run(Second);
    }

    private static T At<T>(RealHeadlessWorld w, int x, int y, int z) where T : atom
        => In(w, () => Dm.each_cast<atom>(((turf)locate(x, y, z)).contents).OfType<T>().FirstOrDefault(t => t.GetType() == typeof(T)));

    private static (int X, int Y) Tile(RealHeadlessWorld w, atom a) => In(w, () => ((int)Dm.num(a.x), (int)Dm.num(a.y)));

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    // The cursor mode's drag (protocol version 17): the medic drags the friend onto the thing.
    private static void Drag(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom dragged, atom over)
    {
        Assert.True(p.Session.Drop(new DragRelease(Entity(dragged.dm_id), Entity(over.dm_id), ClickButton.Left, ClickModifiers.None, 4)));
        w.Run(2);
    }

    // Steers the player to (x, y) from its own predicted position, as a player would (PullGameplayTests).
    private static void WalkTo(RealHeadlessWorld w, RealHeadlessWorld.Player p, double x, double y, int maxTicks = 20 * 20)
    {
        for (int ticks = 0; ticks < maxTicks; ticks++)
        {
            MotionState predicted = p.Session.Prediction.Predicted;
            double dx = x - predicted.X / 256.0, dy = y - predicted.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            p.Keys = MoveKeys.Forward;
            p.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            w.Run(1);
        }
        p.Keys = MoveKeys.None;
        w.Run(10);
    }

    [MetaServerFact]
    public void A_friend_is_buckled_to_a_chair_and_a_roller_bed_by_drag_unbuckled_by_hand_wheeled_and_resists_out_cuffed()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player medic = w.Join("Medic");
        RealHeadlessWorld.Player friend = w.Join("Friend");
        var me = (mob_living_carbon_human)w.MobOf(medic);
        var him = (mob_living_carbon_human)w.MobOf(friend);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var told = new List<string>();
        friend.Client.ServerTextReceived += t => told.Add(t.Html);
        var heard = new List<SoundEvent>();
        medic.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(Second);

        // ---- 1. The chair: the friend north of it, the medic next to both; dragged from the next tile, tg's 2 s first.
        var chair = At<obj_structure_chair>(w, ChairX, ChairY, z);
        Assert.NotNull(chair);
        Assert.Equal(NORTH, In(w, () => Dm.num(chair.dir)));
        Put(w, him, ChairX, ChairY + 1);
        Put(w, me, ChairX + 1, ChairY + 1);
        Drag(w, medic, him, chair);
        Assert.Contains(told, t => t.Contains("starts buckling you to", StringComparison.Ordinal));
        w.Run(Second);
        Assert.Null(In(w, () => him.buckled)); // one second in: not yet
        w.Run(Second + 5);
        Assert.Same(chair, In(w, () => him.buckled));
        Assert.Equal((ChairX, ChairY), Tile(w, him));
        Assert.Equal(NORTH, In(w, () => Dm.num(him.dir)));
        Assert.Contains(told, t => t.Contains("sits you down on", StringComparison.Ordinal));
        // the clients draw him sitting the chair's way (DV126) and upright (buckle_lying 0)
        EntityState seen = medic.Session.Replica.Entities.Single(e => e.Id == him.dm_id).State;
        Assert.Equal(ShotGeometry.YawOfDir((byte)NORTH), seen.Yaw);
        Assert.False(PostureBits.IsLying(seen.Posture));
        // his Buckled alert is on his screen
        long alert = In(w, () => ((datum)him.alerts[ALERT_BUCKLED]).dm_id);
        Assert.Contains(friend.Session.Replica.Screen.Values, e => e.Id == alert);
        // his moves are held: the chair's relaymove refuses them
        friend.Keys = MoveKeys.Forward;
        friend.Yaw = 0;
        w.Run(Second);
        friend.Keys = MoveKeys.None;
        w.Run(5);
        Assert.Equal((ChairX, ChairY), Tile(w, him));
        Assert.Same(chair, In(w, () => him.buckled));
        output.WriteLine("seated ok");

        // ---- 2. The medic's empty hand on the chair stands him up (attack_hand: user_unbuckle_mob).
        Assert.True(medic.Session.Click(chair.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Null(In(w, () => him.buckled));
        Assert.Contains(told, t => t.Contains("stands you up from", StringComparison.Ordinal));
        Assert.DoesNotContain(friend.Session.Replica.Screen.Values, e => e.Id == alert);
        output.WriteLine("stood up ok");

        // ---- 3. The hallway's roller bed (an emergency medical bed): dragged onto it, he lies on it and its seatbelt clicks.
        var bed = At<obj_structure_bed_medical_emergency>(w, BedX, BedY, z);
        Assert.NotNull(bed);
        Put(w, him, BedX - 1, BedY);
        Put(w, me, BedX - 1, BedY + 1);
        Drag(w, medic, him, bed);
        w.Run(2 * Second + 5);
        Assert.Same(bed, In(w, () => him.buckled));
        Assert.Equal(LYING_DOWN, In(w, () => Dm.num(him.body_position)));
        float lying = In(w, () => him.lying_angle);
        Assert.Equal(In(w, () => bed.buckle_lying), lying);
        seen = medic.Session.Replica.Entities.Single(e => e.Id == him.dm_id).State;
        Assert.True(PostureBits.IsLying(seen.Posture));
        Assert.Equal((int)lying, PostureBits.LyingAngle(seen.Posture));
        Assert.Equal("emerg_up", In(w, () => bed.icon_state));
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/machines/buckle/buckle", StringComparison.Ordinal));
        Assert.Contains(told, t => t.Contains("lays you down on", StringComparison.Ordinal));
        output.WriteLine($"lying on the bed at {lying} ok");

        // ---- 4. The medic pulls the bed west along the hallway: the patient rolls with it (handle_buckled_mob_movement).
        Assert.True(medic.Session.Click(bed.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.Same(bed, In(w, () => me.pulling));
        WalkTo(w, medic, BedX - 4, BedY + 1);
        (int bx, int by) = Tile(w, bed);
        output.WriteLine($"the bed pulled to ({bx},{by}), the medic at {Tile(w, me)}");
        Assert.NotEqual((BedX, BedY), (bx, by));
        Assert.Equal((bx, by), Tile(w, him));
        Assert.Same(bed, In(w, () => him.buckled));
        using (w.World.Enter())
            Waiting.NoSleep(me.stop_pulling());
        w.Run(5);

        // ---- 5. Cuffed on the bed, he clicks his Buckled alert: tg's breakout of the cuffs' minute, then he is up.
        using (w.World.Enter())
        {
            var cuffs = Dm.@new<obj_item_restraints_handcuffs>(him.loc);
            Waiting.NoSleep(him.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED));
        }
        w.Run(5);
        float breakout = In(w, () => (float)((obj_item)him.handcuffed).breakouttime);
        Assert.Equal(1 * MINUTES, breakout);
        alert = In(w, () => ((datum)him.alerts[ALERT_BUCKLED]).dm_id);
        Assert.True(friend.Session.Click(new ClickTarget(TargetKind.Screen, alert, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Contains(told, t => t.Contains("You attempt to unbuckle yourself... (This will take around 1 minute and you must stay still.)", StringComparison.Ordinal));
        w.Run(57 * Second);
        Assert.Same(bed, In(w, () => him.buckled)); // under a minute: still on it
        w.Run(3 * Second);
        Assert.Null(In(w, () => him.buckled));
        Assert.NotNull(In(w, () => him.handcuffed));
        Assert.Contains(told, t => t.Contains("You unbuckle yourself from", StringComparison.Ordinal));
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/machines/buckle/unbuckle", StringComparison.Ordinal));
        Assert.Equal("emerg_down", In(w, () => bed.icon_state));
        output.WriteLine("resisted out ok");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // DV187: standing off-centre on the chair's own tile, he drags himself onto it and glides to the seat (tg draws him at his turf).
    [MetaServerFact]
    public void Buckling_oneself_to_the_chair_one_stands_on_moves_onto_the_seat()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 3000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player friend = w.Join("Friend");
        var him = (mob_living_carbon_human)w.MobOf(friend);
        int z = In(w, () => (int)him.z);
        w.Run(Second);
        var chair = At<obj_structure_chair>(w, ChairX, ChairY, z);
        Assert.NotNull(chair);
        Put(w, him, ChairX, ChairY);
        WalkTo(w, friend, ChairX + 0.3, ChairY - 0.3);
        MotionState before = friend.Session.Prediction.Predicted;
        output.WriteLine($"standing at ({before.X / 256.0:0.00},{before.Y / 256.0:0.00})");
        Assert.Equal((ChairX, ChairY), Tile(w, him));
        Assert.True(Math.Abs(before.X - ChairX * 256) > 40 || Math.Abs(before.Y - ChairY * 256) > 40);

        Drag(w, friend, him, chair);
        w.Run(Second);
        Assert.Same(chair, In(w, () => him.buckled));
        MotionState seated = friend.Session.Prediction.Predicted;
        output.WriteLine($"seated at ({seated.X / 256.0:0.00},{seated.Y / 256.0:0.00})");
        Assert.Equal(ChairX * 256, seated.X);
        Assert.Equal(ChairY * 256, seated.Y);
    }
}
