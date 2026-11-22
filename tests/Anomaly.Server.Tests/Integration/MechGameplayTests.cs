using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
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
/// Exosuits on MetaStation through the real server (DV358): a pilot dragged onto a Ripley climbs in, the client takes the mech for
/// its body, and the mech walks a capsule of its own: every way the pilot steers, off the tile grid, at tg's speed, draining its
/// cell by tg's step cost; its pilot climbs out onto the mech's tile.
/// </summary>
public class MechGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    // A clear square of plain floor, so the walks below meet nothing.
    private static (int X, int Y) ClearSquare(RealHeadlessWorld w, int z, int half)
        => In(w, () =>
        {
            int maxx = (int)Dm.num(world.maxx), maxy = (int)Dm.num(world.maxy);
            for (int y = half + 1; y < maxy - half; y++)
                for (int x = half + 1; x < maxx - half; x++)
                {
                    bool clear = true;
                    for (int dy = -half; dy <= half && clear; dy++)
                        for (int dx = -half; dx <= half && clear; dx++)
                        {
                            turf t = locate(x + dx, y + dy, z) as turf;
                            clear = t is turf_open_floor && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));
                        }
                    if (clear)
                        return (x, y);
                }
            throw new InvalidOperationException("no clear square");
        });

    private static void Steer(RealHeadlessWorld w, RealHeadlessWorld.Player p, double yaw, int ticks)
    {
        p.Keys = MoveKeys.Forward;
        p.Yaw = yaw;
        w.Run(ticks);
        p.Keys = MoveKeys.None;
        w.Run(Second / 2);
    }

    [MetaServerFact]
    public void A_pilot_with_an_item_in_hand_cannot_climb_into_a_ripley_until_the_hands_are_free()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 72, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player pilot = w.Join("Pilot");
        var me = (mob_living_carbon_human)w.MobOf(pilot);
        int z = In(w, () => (int)me.z);
        (int cx, int cy) = ClearSquare(w, z, 2);
        w.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e);
        obj_vehicle_sealed_mecha_ripley mech = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(locate(cx - 1, cy, z)));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_crowbar>(me.loc)))));
            return Dm.@new<obj_vehicle_sealed_mecha_ripley>(locate(cx, cy, z));
        });
        w.Run(Second);
        var hints = new List<ContextHintsMessage>();
        pilot.Client.ContextHintsReceived += hints.Add;
        pilot.Session.Rehover();
        pilot.Session.Hover(Entity(mech.dm_id), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == mech.dm_id);
        Assert.NotNull(answer);
        Assert.Equal(RadialKind.DropSelf, RadialModel.Build(answer, 0, true).Single(r => r.Label == "Climb in").Kind);
        Assert.True(pilot.Session.Drop(new DragRelease(new ClickTarget(TargetKind.Entity, pilot.Client.OwnEntity, 0, 0, 0, 16, 16), Entity(mech.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
        w.Run(3 * Second);
        Assert.NotSame(mech, In(w, () => me.loc)); // tg's mecha enter_checks: "You can't enter the exosuit while your hands are occupied!"
        In(w, () => { Waiting.NoSleep(me.drop_all_held_items()); return 0; });
        Assert.True(pilot.Session.Drop(new DragRelease(new ClickTarget(TargetKind.Entity, pilot.Client.OwnEntity, 0, 0, 0, 16, 16), Entity(mech.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
        w.Run(3 * Second);
        Assert.Same(mech, In(w, () => me.loc));
    }

    [MetaServerFact]
    public void A_pilot_climbs_into_a_ripley_walks_it_smoothly_every_way_and_climbs_out()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player pilot = w.Join("Pilot");
        var me = (mob_living_carbon_human)w.MobOf(pilot);
        int z = In(w, () => (int)me.z);
        (int cx, int cy) = ClearSquare(w, z, 4);
        output.WriteLine($"square at {cx},{cy}");
        int bootRuntimes = w.World.RuntimeErrorCount;
        w.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e);

        obj_vehicle_sealed_mecha_ripley mech = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(locate(cx - 1, cy, z)));
            return Dm.@new<obj_vehicle_sealed_mecha_ripley>(locate(cx, cy, z));
        });
        w.Run(Second);
        Assert.True(In(w, () => mech.cell != null), "the mech holds its cell");
        Assert.True(In(w, () => LocomotionSeam.IsMechWalked(mech)), "the mech has a capsule");

        string phase = "before";
        pilot.Session.Prediction.Corrected += r => output.WriteLine($"  correction ({phase}): {r.ErrorTiles:0.000} tiles, {r.Kind}, acked {r.AckedSeq}, replayed {r.Replayed}, own {pilot.Client.OwnEntity}");
        phase = "climbing in";
        long Wrong() => pilot.Counters.Corrections + pilot.Counters.Snaps + pilot.Counters.Mismatches;
        long wrongBefore = Wrong(); // the test's teleport next to the mech is the server's, and counted; from here on nothing may be
        // The radial on the mech offers Climb in (first person cannot drag its own body): the client's DropSelf, tg's MouseDrop of oneself
        // onto the mech, mob_try_enter and its enter_delay
        var hints = new List<ContextHintsMessage>();
        pilot.Client.ContextHintsReceived += hints.Add;
        pilot.Session.Rehover();
        pilot.Session.Hover(Entity(mech.dm_id), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == mech.dm_id);
        Assert.NotNull(answer);
        Assert.Equal(RadialKind.DropSelf, RadialModel.Build(answer, 0, true).Single(r => r.Label == "Climb in").Kind);
        Assert.True(pilot.Session.Drop(new DragRelease(new ClickTarget(TargetKind.Entity, pilot.Client.OwnEntity, 0, 0, 0, 16, 16), Entity(mech.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
        w.Run(3 * Second);
        Assert.Same(mech, In(w, () => me.loc));
        w.Run(Second);
        Assert.Equal(mech.dm_id, pilot.Client.OwnEntity);

        float charge = In(w, () => Dm.num(mech.cell.charge));
        output.WriteLine($"climbing in: corrections {pilot.Counters.Corrections}, snaps {pilot.Counters.Snaps}, mismatches {pilot.Counters.Mismatches}");
        Assert.Equal(wrongBefore, Wrong()); // the body switch starts the prediction over: no correction

        // The cockpit window (tg's Mecha UI, the view stats action): the client draws it from the server's data, and its buttons act
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => pilot.Session.UiAct(window, action, parameters),
            SendClose = window => pilot.Session.UiClose(window),
        };
        In(w, () => { Waiting.NoSleep(mech.ui_interact(me)); return 0; });
        w.Run(Second);
        UiWindowState cockpit = Assert.Single(pilot.Session.Replica.UiWindows.Tgui());
        Assert.Equal("Mecha", cockpit.Interface);
        UiNode Shown()
        {
            windows.Sync(pilot.Session.Replica.UiWindows, 1280, 720, 1);
            return windows.Get(cockpit.Window).Content;
        }
        string text = Shown().AllText();
        output.WriteLine("cockpit: " + text);
        Assert.Contains("Equipment", text);
        Assert.Contains("Safety Protocols Disabled", text); // tg's weapons_safety starts FALSE
        Assert.Contains("Left Arm Module Slot", text);
        Assert.Contains("Compartment is empty", text); // the Ripley's cargo compartment (ejector), selected as its first module
        Assert.Contains(In(w, () => $"{Dm.num(mech.get_integrity())} of {Dm.num(mech.max_integrity)}"), text);
        Shown().Walk().First(n => n.Key == "toggle_safety").OnClick();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(mech.weapons_safety)));
        Assert.Contains("Safety Protocols Enabled", Shown().AllText());
        pilot.Session.UiClose(cockpit.Window);
        w.Run(Second / 2);
        Assert.Equal(wrongBefore, Wrong());

        // DV361: first person draws the arms' equipment as the hands' things (the left arm for the left button)
        In(w, () => { Waiting.NoSleep(Dm.@new<obj_item_mecha_parts_mecha_equipment_hydraulic_clamp>(mech.loc).attach(mech, FALSE)); return 0; });
        w.Run(Second / 2);
        HeldHandsView arms = HeldHands.Find(pilot.Session.Replica);
        Assert.True(arms.Mech);
        Assert.False(arms.Left.IsNone, "the left arm holds the clamp");
        Assert.True(arms.Right.IsNone);
        In(w, () => { Waiting.NoSleep(Dm.@new<obj_item_mecha_parts_mecha_equipment_hydraulic_clamp>(mech.loc).attach(mech, TRUE)); return 0; });
        w.Run(Second / 2);
        Assert.False(HeldHands.Find(pilot.Session.Replica).Right.IsNone, "the second clamp fitted to the right arm in flight is drawn");


        MotionState Now() => In(w, () => { LocomotionSeam.TryGetState(w.World, mech.dm_id, out MotionState s, out _); return s; });

        // all eight ways: the mech ends each leg away from where it began, along the way it was steered
        double[] yaws = { 0, Math.PI / 2, Math.PI, -Math.PI / 2, Math.PI / 4, 3 * Math.PI / 4, -3 * Math.PI / 4, -Math.PI / 4 };
        foreach (double yaw in yaws)
        {
            phase = $"yaw {yaw:0.00}";
            MotionState from = Now();
            Steer(w, pilot, yaw, 10);
            MotionState to = Now();
            double dx = (to.X - from.X) / 256.0, dy = (to.Y - from.Y) / 256.0;
            output.WriteLine($"yaw {yaw:0.00}: moved {dx:0.00},{dy:0.00}");
            double along = dx * -Math.Sin(yaw) + dy * Math.Cos(yaw);
            double across = Math.Abs(dx * Math.Cos(yaw) + dy * Math.Sin(yaw));
            Assert.True(along > 0.5, $"yaw {yaw}: {dx},{dy}");
            Assert.True(across < 0.1, $"yaw {yaw}: {dx},{dy}");
            // off the grid, and the tile the world holds is the one the capsule is on
            (int tx, int ty) = In(w, () => ((int)Dm.num(mech.x), (int)Dm.num(mech.y)));
            Assert.Equal((to.X + 128) >> 8, tx);
            Assert.Equal((to.Y + 128) >> 8, ty);
            // the client's own prediction agrees with the server
            MotionState predicted = pilot.Session.Prediction.Predicted;
            Assert.True(Math.Abs(predicted.X - to.X) < 64 && Math.Abs(predicted.Y - to.Y) < 64, $"predicted {predicted.X},{predicted.Y} server {to.X},{to.Y}");
        }

        // the owner (2026-11-07): no misprediction while the mech walks, starts, stops and turns
        output.WriteLine($"walking: corrections {pilot.Counters.Corrections}, snaps {pilot.Counters.Snaps}, mismatches {pilot.Counters.Mismatches}");
        Assert.Equal(wrongBefore, Wrong());

        // nor while it strafes, turns on the spot and walks into a wall
        In(w, () => { mech.strafe = TRUE; return 0; });
        Steer(w, pilot, Math.PI / 2, 6);
        In(w, () => { mech.strafe = FALSE; return 0; });
        for (int i = 0; i < 16; i++)
        {
            pilot.Yaw = i * Math.PI / 8;
            w.Run(2);
        }
        Steer(w, pilot, 0, 3 * Second);
        output.WriteLine($"strafing, turning, the wall: corrections {pilot.Counters.Corrections}, snaps {pilot.Counters.Snaps}, mismatches {pilot.Counters.Mismatches}");
        Assert.Equal(wrongBefore, Wrong());

        // tiles the mech crossed cost tg's step energy
        Assert.True(In(w, () => Dm.num(mech.cell.charge)) < charge, "the walk drained the cell");

        // tg's mob_exit: the pilot stands on the mech's tile and is its own body again
        In(w, () => { Waiting.NoSleep(mech.mob_exit(me)); return 0; });
        w.Run(Second);
        Assert.IsAssignableFrom<turf>(In(w, () => me.loc));
        Assert.Equal(me.dm_id, pilot.Client.OwnEntity);
        Steer(w, pilot, 3.14, Second); // walking off on foot
        Assert.Equal(wrongBefore, Wrong());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
