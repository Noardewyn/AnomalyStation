using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
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
/// D169 through the real server on MetaStation's engine: the dev console's <c>smdelam X Y 1 tesla</c> (tg's admin path,
/// force_delam) delaminates the crystal into an energy ball of 200 (delamination_effects.dm:112-113). The client sees the ball with
/// tg's sprite move on SSobj's schedule (energy_ball.dm:71), its lightning beams (tesla_zap's Beam, :326), and its mini-balls appear
/// (:130, ten seconds after each threshold) and orbit it: each carries the 3 to 36 steps of its SpinAnimation chain with the translated
/// transform (orbiter.dm:102-106; protocol 32 for the 36), and stands on the ball's tile as it moves. No new runtime.
/// </summary>
public class EnergyBallGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string BallPath = "/obj/energy_ball";
    private const string BeamPath = "/obj/effect/ebeam";

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

    [MetaServerFact]
    public void A_tesla_delamination_makes_a_ball_the_client_sees_move_zap_and_grow_orbiting_mini_balls()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 30000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        obj_machinery_power_supermatter_crystal sm = In(w, () => Assert.Single(Dm.each<obj_machinery_power_supermatter_crystal>(Dm.world).ToList()));
        int smx = (int)In(w, () => Dm.num(sm.x)), smy = (int)In(w, () => Dm.num(sm.y));
        // the engineer watches from twelve tiles north, in the dev godmode (apps.md 1.6a): the ball's zaps and the delamination's
        // blast would kill him, and the ball does not dust a godmode mob (energy_ball.dm:193)
        mob_living engineer = w.MobOf(p);
        In(w, () =>
        {
            ADD_TRAIT(engineer, TRAIT_GODMODE, ADMIN_TRAIT);
            Waiting.NoSleep(engineer.forceMove(locate(smx, smy + 12, 1)));
        });
        w.Run(Second);

        w.Host.EnqueueConsoleLine($"smdelam {smx} {smy} 1 tesla");
        w.Run(Second);
        obj_energy_ball ball = In(w, () => Assert.Single(Dm.each<obj_energy_ball>(Dm.world).ToList()));
        Assert.True(In(w, () => QDELETED(sm)), "the crystal delaminated (_sm_delam.dm:32)");
        Assert.Equal(200f, In(w, () => Dm.num(ball.energy)));
        long ballId = In(w, () => ball.dm_id);

        var ballSpots = new List<(int X, int Y)>();
        var beams = new HashSet<long>();
        var miniIds = new HashSet<long>();
        var miniSteps = new Dictionary<long, int>();
        for (int t = 0; t < 40 * Second; t++)
        {
            w.Run(1);
            ReplicaEntity seenBall = replica.Entities.SingleOrDefault(e => e.Id == ballId);
            Assert.NotNull(seenBall);
            if (ballSpots.Count == 0 || ballSpots[^1] != (seenBall.State.X, seenBall.State.Y))
                ballSpots.Add((seenBall.State.X, seenBall.State.Y));
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath != null))
            {
                if (e.TypePath.StartsWith(BeamPath, StringComparison.Ordinal))
                    beams.Add(e.Id);
                else if (e.TypePath == BallPath && e.Id != ballId)
                {
                    miniIds.Add(e.Id);
                    AnimationTrack spin = e.Animation?.Tracks.FirstOrDefault(tr => tr.Steps.All(s => (s.Vars.Mask & AnimatedVar.Transform) != 0));
                    if (spin != null)
                        miniSteps[e.Id] = spin.Steps.Length;
                }
            }
        }
        List<obj_energy_ball> minis = In(w, () => Dm.each_cast<obj_energy_ball>(ball.orbiting_balls).ToList());
        output.WriteLine($"the ball took {ballSpots.Count} places; the client saw {beams.Count} beam segments, {miniIds.Count} mini-balls, their spins of {string.Join(",", miniSteps.Values)} steps; {minis.Count} orbit it now");
        Assert.Equal(BallPath, TypePathOf(replica, ballId));
        Assert.True(ballSpots.Count >= 10, "the client sees the ball move each SSobj cycle");
        Assert.NotEmpty(beams); // its lightning (tesla_zap's Beam)
        Assert.True(minis.Count >= 5, "the ball grows a mini-ball for each threshold its 200 passes, ten seconds after it");
        Assert.True(miniIds.Count >= minis.Count, "the client sees the mini-balls");
        Assert.True(miniSteps.Count >= minis.Count, "each mini-ball's orbit reaches the client as its SpinAnimation chain");
        Assert.All(miniSteps.Values, n => Assert.Contains(n, new[] { 3, 4, 5, 6, 36 })); // pick(3, 4, 5, 6, 36) segments (energy_ball.dm:153)
        ReplicaEntity last = replica.Entities.Single(e => e.Id == ballId);
        Assert.All(minis, m => Assert.Equal((last.State.X, last.State.Y), replica.Entities.Where(e => e.Id == In(w, () => m.dm_id)).Select(e => (e.State.X, e.State.Y)).Single()));

        w.World.RuntimeErrorSink = sink;
        output.WriteLine($"runtimes: {string.Join(" | ", errors.Select(e => e.ToString()[..Math.Min(e.ToString().Length, 2500)]).Distinct())}");
        Assert.Empty(errors);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private static string TypePathOf(ReplicaWorld replica, long id) => replica.Entities.Single(e => e.Id == id).TypePath;
}
