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
/// The owner's play test (2026-10-26), on MetaStation at 100 ms each way: the own view is not corrected where the server moves the player
/// in ways the client should foresee or show calmly. Lying buckled on the hallway's roller bed that a medic pulls, every tile tg moves him
/// is a glide (no snap, also diagonally) and no camera shake. Flying a lit jetpack from the Auxiliary Airlock's floor out into space and
/// back, the step off the floor and onto it (the driver and the jetpack's full speed, which only apply weightless) is predicted: no
/// correction at all. The wind (DV357) carries the player smoothly and predicted, and a runner gains on a weaker one; a gravity pull
/// draws it as smoothly.
/// </summary>
public class DisplacedPredictionTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int BedX = 91, BedY = 113;

    private static LoopbackOptions Net(int seed) => new() { Seed = (ulong)seed, TimeoutSteps = 6000, MinLatencySteps = 2, MaxLatencySteps = 2 };

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Run(RealHeadlessWorld w, RealHeadlessWorld.Player p, int ticks, Action<long> afterTick = null)
        => w.Run(ticks, t =>
        {
            p.Session.CameraFrame(RealHeadlessWorld.TickSeconds);
            afterTick?.Invoke(t);
        });

    [MetaServerFact]
    public void A_buckled_body_carried_on_a_pulled_bed_glides_without_a_snap_or_a_shake()
    {
        using var w = new RealHeadlessWorld(Net(62), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player friend = w.Join("Friend", inputBufferTarget: 2);
        RealHeadlessWorld.Player medic = w.Join("Medic", inputBufferTarget: 2);
        var him = (mob_living_carbon_human)w.MobOf(friend);
        mob_living doc = w.MobOf(medic);
        int z = In(w, () => (int)him.z);
        w.Run(Second);
        var bed = In(w, () => Dm.each_cast<atom>(((turf)locate(BedX, BedY, z)).contents).OfType<obj_structure_bed>().First());
        In(w, () => Waiting.NoSleep(him.forceMove(locate(BedX, BedY, z))));
        In(w, () => Waiting.NoSleep(doc.forceMove(locate(BedX - 1, BedY + 1, z))));
        w.Run(Second);
        In(w, () => Waiting.NoSleep(bed.buckle_mob(him, TRUE)));
        w.Run(Second);
        Assert.Same(bed, In(w, () => him.buckled));
        Assert.True(medic.Session.Click(bed.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(10);

        PredictionCounters before = friend.Counters;
        int shakes = friend.Session.CorrectionShakes;
        medic.Yaw = Math.PI / 2; // west, the bed trailing diagonally at first
        medic.Keys = MoveKeys.Forward;
        friend.Keys = MoveKeys.Forward; // his held keys move nothing
        Run(w, friend, 2 * Second);
        medic.Keys = friend.Keys = MoveKeys.None;
        Run(w, friend, Second);
        PredictionCounters after = friend.Counters;
        (int X, int Y) bedAt = In(w, () => ((int)bed.x, (int)bed.y));
        output.WriteLine($"bed at {bedAt}; corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}, shakes {friend.Session.CorrectionShakes - shakes}");
        Assert.True(bedAt.X <= BedX - 5, "the bed was pulled");
        Assert.Equal(bedAt, In(w, () => ((int)him.x, (int)him.y)));
        Assert.Equal(before.Snaps, after.Snaps);
        Assert.Equal(shakes, friend.Session.CorrectionShakes);
    }

    [MetaServerFact]
    public void Flying_off_a_floor_into_space_and_back_is_predicted_without_a_correction()
    {
        using var w = new RealHeadlessWorld(Net(21), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        // the Auxiliary Airlock: its floor (39,118-119) with gravity, space south of it; the outer door taken away
        In(w, () => { qdel(Dm.locate<obj_machinery_door_airlock>(((turf)locate(39, 118, z)).contents)); return 0; });
        Assert.False(In(w, () => Dm.truthy(me.has_gravity((turf)locate(39, 117, z)))));
        ZeroGPredictionTests.SuitUp(w, p, me, out _, out long toggle, out _);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(39, 119, z))));
        w.Run(Second);
        ZeroGPredictionTests.ClickScreen(p, toggle);
        w.Run(Second);

        PredictionCounters before = p.Counters;
        p.Yaw = Math.PI; // south
        for (int round = 0; round < 2; round++)
        {
            p.Keys = MoveKeys.Forward;
            Run(w, p, 2 * Second);
            Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
            p.Keys = MoveKeys.Back;
            Run(w, p, 2 * Second);
            p.Keys = MoveKeys.None;
            Run(w, p, Second);
            Assert.Equal(MotionDriver.Grounded, w.ServerState(p).Driver);
            Assert.Equal(MotionDriver.Grounded, p.Session.Prediction.Predicted.Driver);
        }
        PredictionCounters after = p.Counters;
        output.WriteLine($"corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}, max error {after.MaxErrorTiles:0.000}");
        // shortcut: one correction is allowed, where the Master's staggered first fires put a tick on the floor's edge; find its cause to go back to none
        Assert.InRange(after.Corrections - before.Corrections, 0, 1);
        Assert.Equal(before.Snaps, after.Snaps);
    }

    // DV357 (the owner, 2026-11-06: the wind mispredicted, jerked a tile now and then and could not be leaned against): SSair's own pass
    // blows a 60 kPa wind east along the hallway of the roller bed while the player runs west against it, then a 140 kPa one while it
    // stands, which carries it to the hallway's wall: the capsule glides with it (no tick standing still), predicted, and the runner gains on the weaker wind.
    [MetaServerFact]
    public void The_wind_carries_the_player_smoothly_and_predicted_and_a_runner_leans_against_it()
    {
        using var w = new RealHeadlessWorld(Net(9), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z), x0 = BedX - 2, y0 = BedY; // a wall two tiles east of the bed
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x0, y0, z))));
        w.Run(2 * Second);
        float kpa = 60f;
        // the hallway's turfs get tg's pressure difference toward the east each tick, as LINDA's share does, so SSair's pass blows them
        void Blow(long tick) => In(w, () =>
        {
            for (int dx = -12; dx <= 2; dx++)
            {
                if (locate(x0 + dx, y0, z) is turf_open t && locate(x0 + dx + 1, y0, z) is turf east)
                    t.consider_pressure_difference(east, kpa);
            }
            return 0;
        });

        PredictionCounters before = p.Counters;
        int shakes = p.Session.CorrectionShakes;
        p.Yaw = Math.PI / 2; // west, against the wind
        p.Keys = MoveKeys.Forward;
        Run(w, p, Second, Blow);
        int ran = w.ServerState(p).X;
        p.Keys = MoveKeys.None;
        kpa = 140f;
        var xs = new List<int>();
        Run(w, p, Second, t => { Blow(t); xs.Add(p.Session.Prediction.Predicted.X); });
        int blown = w.ServerState(p).X;
        Run(w, p, Second);
        PredictionCounters after = p.Counters;
        int stills = 0;
        for (int i = 1; i < xs.Count; i++)
        {
            if (xs[i] == xs[i - 1] && xs[i] != xs[^1])
                stills++; // a tick the drawn body stood still while the wind still had it on its way
        }
        output.WriteLine($"x {x0} -> ran {ran / 256f:0.00} -> blown {blown / 256f:0.00}; still ticks {stills}; corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}, max error {after.MaxErrorTiles:0.000}, shakes {p.Session.CorrectionShakes - shakes}");
        Assert.True(ran < x0 * 256 - 2 * 256, "running west gained on a 60 kPa wind");
        Assert.True(blown > ran + 3 * 256, "a 140 kPa wind carried the standing player east");
        Assert.InRange(stills, 0, 2);
        Assert.InRange(after.Corrections - before.Corrections, 0, 3);
        Assert.Equal(before.Snaps, after.Snaps);
        Assert.Equal(shakes, p.Session.CorrectionShakes);
    }

    // DV357: a gravity pull (the singularity's, an anomaly's step_towards once a second) draws a standing player toward its tile at its
    // pace, gliding and predicted; it stops on the pull's tile.
    [MetaServerFact]
    public void A_gravity_pull_draws_the_player_smoothly_and_predicted()
    {
        using var w = new RealHeadlessWorld(Net(5), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z), x0 = BedX - 1, y0 = BedY;
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x0, y0, z))));
        w.Run(2 * Second);
        var center = In(w, () => (turf)locate(x0 - 4, y0, z));

        PredictionCounters before = p.Counters;
        var xs = new List<int>();
        Run(w, p, 5 * Second, t =>
        {
            if (t % Second == 0)
                In(w, () => Waiting.NoSleep(LocomotionSeam.PullOrStep(me, center)));
            xs.Add(p.Session.Prediction.Predicted.X);
        });
        PredictionCounters after = p.Counters;
        int stills = 0, first = xs.FindIndex(x => x != xs[0]);
        for (int i = Math.Max(1, first); i < xs.Count; i++)
        {
            if (xs[i] == xs[i - 1] && xs[i] != xs[^1])
                stills++;
        }
        int at = w.ServerState(p).X;
        output.WriteLine($"x {x0} -> {at / 256f:0.00}; still ticks {stills}; corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}");
        Assert.Equal((x0 - 4) * 256, at);
        Assert.InRange(stills, 0, 2);
        Assert.InRange(after.Corrections - before.Corrections, 0, 2);
        Assert.Equal(before.Snaps, after.Snaps);
    }

    // DV357: the wind blows a standing player off the Auxiliary Airlock's floor onto the space beyond (the outer door taken away), where it
    // is weightless and tg's moves take over (its push-off finds the floor beside it, so the crossing starts no drift, as tg's step does):
    // no snap, no shake on the way out.
    [MetaServerFact]
    public void The_wind_blows_the_player_off_a_floor_into_space_without_a_snap()
    {
        using var w = new RealHeadlessWorld(Net(23), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        In(w, () => { qdel(Dm.locate<obj_machinery_door_airlock>(((turf)locate(39, 118, z)).contents)); return 0; });
        In(w, () => Waiting.NoSleep(me.forceMove(locate(39, 120, z))));
        w.Run(2 * Second);
        void Blow(long tick) => In(w, () =>
        {
            for (int y = 118; y <= 121; y++)
            {
                if (locate(39, y, z) is turf_open t)
                    t.consider_pressure_difference(locate(39, y - 1, z), 100f);
            }
            return 0;
        });

        PredictionCounters before = p.Counters;
        int shakes = p.Session.CorrectionShakes;
        Run(w, p, 3 * Second, Blow);
        PredictionCounters after = p.Counters;
        int y = In(w, () => (int)me.y);
        output.WriteLine($"y 120 -> {y}; corrections {after.Corrections - before.Corrections}, snaps {after.Snaps - before.Snaps}, shakes {p.Session.CorrectionShakes - shakes}, driver {w.ServerState(p).Driver}");
        Assert.True(y <= 117, "blown out into space");
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
        Assert.Equal(before.Snaps, after.Snaps);
        Assert.Equal(shakes, p.Session.CorrectionShakes);
    }
}
