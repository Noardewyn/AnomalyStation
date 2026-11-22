using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Pulling (2026-10-15), RealServerPredictionTests' rig: the client's real stack against the real server at 0, 100 and 200 ms one way.
/// The puller ctrl-clicks a crate (no drag slowdown), a locker (tg's drag_slowdown 1.5) or another player, standing or lying (tg's
/// PULL_PRONE_SLOWDOWN 1.5), standing still, then walks a route that never turns back into what it drags. The pulled thing follows
/// into the tiles the puller leaves; the puller's own prediction is corrected no more than walking alone (zero), because the pull's
/// slowdown reaches the client in the parameters of the tick the pull starts (DV48: refreshed before every capsule step).
/// </summary>
public class PullPredictionTests
{
    private readonly ITestOutputHelper _output;

    public PullPredictionTests(ITestOutputHelper output) => _output = output;

    private static LoopbackOptions Net(int latencySteps) => new() { Seed = 5, TimeoutSteps = 400, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps };

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static (int X, int Y) Tile(RealHeadlessWorld w, atom a) => In(w, () => ((int)Dm.num(a.x), (int)Dm.num(a.y)));

    // North, east, north-east, east, north-east: no leg has a component against an earlier one, so the puller never walks into what
    // trails behind it (after a north step it is south: a south-east leg would press it, tg's PushAM, which lets go: living.dm:382).
    private static void Route(RealHeadlessWorld w, RealHeadlessWorld.Player p, Action<long> afterTick = null)
    {
        (MoveKeys Keys, int Ticks)[] legs =
        {
            (MoveKeys.Forward, 8), (MoveKeys.Right, 8), (MoveKeys.Forward | MoveKeys.Right, 6), (MoveKeys.Right, 6), (MoveKeys.Forward | MoveKeys.Right, 6),
        };
        p.Yaw = 0;
        foreach ((MoveKeys keys, int ticks) in legs)
        {
            p.Keys = keys;
            w.Run(ticks, afterTick);
        }
        p.Keys = MoveKeys.None;
        w.Run(10, afterTick);
    }

    // The south-west corner of the first open 12 x 9 floor (no wall, nothing dense) of the room: the route's ground.
    private static (int X, int Y) OpenGround(RealHeadlessWorld w) => In(w, () =>
    {
        bool Open(int x, int y) => Builtins.locate(x, y, 1) is turf_open t && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));
        int maxx = (int)Dm.world.maxx, maxy = (int)Dm.world.maxy;
        for (int y = 1; y + 8 <= maxy; y++)
            for (int x = 1; x + 11 <= maxx; x++)
                if (Enumerable.Range(0, 12).All(dx => Enumerable.Range(0, 9).All(dy => Open(x + dx, y + dy))))
                    return (x, y);
        throw new InvalidOperationException("no open 12 x 9 floor in the room");
    });

    private (RealHeadlessWorld World, RealHeadlessWorld.Player Puller, mob_living Mob, int X, int Y) Start(int latencySteps)
    {
        var w = new RealHeadlessWorld(Net(latencySteps), new ServerConfig { Seed = 7, RoomWidth = 32, RoomHeight = 32, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f });
        RealHeadlessWorld.Player p = w.Join("Puller", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        mob_living me = w.MobOf(p);
        // the puller stands a tile in and two up from the open ground's corner, the thing it pulls south of it
        (int gx, int gy) = OpenGround(w);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(gx + 1, gy + 2, 1)));
        w.Run(20);
        return (w, p, me, gx + 1, gy + 2);
    }

    private void Pull(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living me, atom_movable thing, string what, int latencySteps)
    {
        float before = In(w, () => Dm.num(me.cached_multiplicative_slowdown));
        Assert.True(p.Session.Click(thing.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(10);
        Assert.Same(thing, In(w, () => me.pulling));
        long corrections = p.Counters.Corrections, snaps = p.Counters.Snaps;
        (int X, int Y) start = Tile(w, me);
        int lost = 0;
        Route(w, p, _ =>
        {
            if (lost == 0 && In(w, () => me.pulling) == null)
                lost = 1;
        });
        float during = In(w, () => Dm.num(me.cached_multiplicative_slowdown));
        (int mx, int my) = Tile(w, me);
        (int tx, int ty) = Tile(w, thing);
        long c = p.Counters.Corrections - corrections, s = p.Counters.Snaps - snaps;
        _output.WriteLine($"{latencySteps * 50} ms, {what}: slowdown {before} -> {during}; puller at ({mx},{my}), {what} at ({tx},{ty}); corrections {c}, snaps {s}, max error {p.Counters.MaxErrorTiles:0.000} tiles, mismatches {p.Counters.Mismatches}");
        Assert.Equal(0, lost);
        Assert.Same(thing, In(w, () => me.pulling));
        Assert.Equal(1, Math.Max(Math.Abs(mx - tx), Math.Abs(my - ty))); // it followed
        Assert.True(mx >= start.X + 2, "the puller walked the route"); // its east legs are strafes (63 %, D314), a locker's 1.5 slower still
        Assert.Equal(0, c); // no worse than walking alone (RealServerPredictionTests: zero)
        Assert.Equal(0, s);
        MotionState server = w.ServerState(p);
        Assert.Equal((server.X, server.Y), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void PullingACrateOrALockerCostsThePullerNoCorrection(int latencySteps)
    {
        var (w, p, me, x0, y0) = Start(latencySteps);
        using (w)
        {
            var crate = In(w, () => Dm.@new<obj_structure_closet_crate>(Builtins.locate(x0, y0 - 1, 1)));
            Pull(w, p, me, crate, "crate", latencySteps);
            float walk = In(w, () => Dm.num(me.cached_multiplicative_slowdown));

            // stop, back to (4, 4), a locker south: tg's drag_slowdown 1.5 slows the puller
            p.Session.Keybinding(KeybindingCode.StopPulling);
            w.Run(15); // past the latency
            Assert.Null(In(w, () => me.pulling));
            using (w.World.Enter())
                Waiting.NoSleep(me.forceMove(Builtins.locate(x0, y0, 1)));
            w.Run(20);
            var locker = In(w, () => Dm.@new<obj_structure_closet>(Builtins.locate(x0, y0 - 1, 1)));
            Pull(w, p, me, locker, "locker", latencySteps);
            Assert.Equal(walk + 1.5f, In(w, () => Dm.num(me.cached_multiplicative_slowdown)), 3);
            Assert.Equal(0, w.World.RuntimeErrorCount);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void PullingAStandingOrALyingPlayerCostsThePullerNoCorrection(int latencySteps)
    {
        var (w, p, me, x0, y0) = Start(latencySteps);
        using (w)
        {
            RealHeadlessWorld.Player other = w.Join("Pulled", inputBufferTarget: latencySteps > 0 ? 2 : 1);
            mob_living them = w.MobOf(other);
            using (w.World.Enter())
                Waiting.NoSleep(them.forceMove(Builtins.locate(x0, y0 - 1, 1)));
            w.Run(20);
            float walk = In(w, () => Dm.num(me.cached_multiplicative_slowdown));
            Pull(w, p, me, them, "standing player", latencySteps);
            Assert.Equal(walk, In(w, () => Dm.num(me.cached_multiplicative_slowdown)), 3);

            p.Session.Keybinding(KeybindingCode.StopPulling);
            w.Run(15); // past the latency
            using (w.World.Enter())
            {
                Waiting.NoSleep(me.forceMove(Builtins.locate(x0, y0, 1)));
                Waiting.NoSleep(them.forceMove(Builtins.locate(x0, y0 - 1, 1)));
                Waiting.NoSleep(them.set_resting(1, 1, 1));
            }
            w.Run(20);
            Assert.Equal(Defines.LYING_DOWN, In(w, () => them.body_position));
            Pull(w, p, me, them, "lying player", latencySteps);
            Assert.Equal(walk + Defines.PULL_PRONE_SLOWDOWN, In(w, () => Dm.num(me.cached_multiplicative_slowdown)), 3);
            Assert.Equal(0, w.World.RuntimeErrorCount);
        }
    }
}
