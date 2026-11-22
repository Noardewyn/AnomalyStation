using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's request (2026-10-19): a ghost, a weightless body and a jetpack flight move freely, not tile by tile, as the living body walks.
/// Through the real server on MetaStation: DV68, a ghost flies as a capsule, at any angle and through walls, 30 per cent slower than DV54's
/// ten tiles a second, predicted by its client; DV69, a weightless body drifts by tg's drift as an even velocity, a fraction of a tile a tick.
/// </summary>
public class FreeFlightGameplayTests
{
    private const int Second = 20;
    private readonly ITestOutputHelper _output;

    public FreeFlightGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static RealHeadlessWorld Meta(ulong seed)
        => new(new LoopbackOptions { Seed = seed, TimeoutSteps = 8000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void WalkingOffAPlatformPreservesTheFreeAngleAndStopsOnLanding(int latencySteps)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions
        {
            Seed = 93, TimeoutSteps = 800,
            MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps,
        }, strict: false);
        var p = w.Join("Bob", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        var me = w.MobOf(p);
        int bootRuntimes = w.World.RuntimeErrorCount;
        using (w.World.Enter())
        {
            GlobalProcs.ADD_TRAIT(me, Defines.TRAIT_GODMODE, "test");
            // No decompression impulse: this scenario measures walking's inertia alone.
            for (int x = 2; x < 24; x++)
                for (int y = 2; y < 24; y++)
                    if (locate(x, y, 1) is turf_open floor)
                        floor.air = GlobalProcs.SSair.parse_gas_string(Defines.AIRLESS_ATMOS) as datum_gas_mixture;
            for (int x = 10; x <= 16; x++)
                for (int y = 2; y <= 22; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
            Waiting.NoSleep(me.forceMove(locate(9, 10, 1)));
        }
        w.Run(10);
        double angle = Math.Atan2(115, 54);
        p.Yaw = -angle;
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < Second && In(w, () => me.drift_handler) == null; i++)
            w.Run(1);
        var drift = Assert.IsType<datum_drift_handler>(In(w, () => me.drift_handler));
        Assert.InRange(In(w, () => Dm.num(drift.drifting_loop.angle)), angle * 180 / Math.PI - 1, angle * 180 / Math.PI + 1);
        p.Keys = MoveKeys.None;
        p.Yaw += Math.PI; // looking back must not turn the drift
        w.Run(6); // allow the in-flight key frames to arrive
        var states = new List<MotionState> { w.ServerState(p) };
        w.Run(Second, _ => states.Add(w.ServerState(p)));
        for (int i = 1; i < states.Count; i++)
        {
            int dx = states[i].X - states[i - 1].X, dy = states[i].Y - states[i - 1].Y;
            Assert.InRange(dx, 15, 40);
            Assert.InRange(dy, 5, 25);
            Assert.InRange((double)dy / dx, 54d / 115 - 0.05, 54d / 115 + 0.05);
            Assert.False(states[i].Slide.Active);
        }
        w.Run(6 * Second);
        Assert.Equal(17, w.ServerState(p).TileX);
        Assert.Equal(MotionDriver.Grounded, w.ServerState(p).Driver);
        Assert.Null(In(w, () => me.drift_handler));
        var stopped = w.ServerState(p);
        w.Run(Second);
        Assert.Equal((stopped.X, stopped.Y), (w.ServerState(p).X, w.ServerState(p).Y));
        Assert.Equal((stopped.X, stopped.Y), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_ghost_flies_at_an_angle_through_walls_without_stepping_from_tile_to_tile()
    {
        using RealHeadlessWorld w = Meta(91);
        RealHeadlessWorld.Player p = w.Join("Alice");
        mob_living body = w.MobOf(p);
        int bootRuntimes = w.World.RuntimeErrorCount;
        using (w.World.Enter())
            Waiting.NoSleep(body.death());
        w.Run(5);
        p.Yaw = Math.PI / 2 - 0.5; // west and a little north (Godot's yaw: counter-clockwise from north)
        p.Keys = MoveKeys.Forward;
        w.Run(2);
        var ghost = In(w, () => Dm.each(Dm.world).OfType<mob_dead_observer>().FirstOrDefault());
        Assert.NotNull(ghost);
        long id = In(w, () => ghost.dm_id);
        var states = new List<MotionState>();
        bool crossedWall = false;
        w.Run(2 * Second, _ =>
        {
            Assert.True(LocomotionSeam.TryGetState(w.World, id, out MotionState s, out MotionParams parameters), "the ghost is a capsule");
            Assert.True(parameters.NoClip);
            states.Add(s);
            crossedWall |= In(w, () => ghost.loc is turf_closed);
        });
        p.Keys = MoveKeys.None;
        w.Run(3);

        // an even 0.35 tiles a tick at the view's angle: both axes move every tick, by a fraction of a tile, never a whole one
        int moving = 0, offCentre = 0;
        for (int i = 1; i < states.Count; i++)
        {
            int dx = states[i].X - states[i - 1].X, dy = states[i].Y - states[i - 1].Y;
            double step = Math.Sqrt((double)dx * dx + (double)dy * dy) / MotionMath.SubunitsPerTile;
            if (step < 0.01)
                continue;
            moving++;
            Assert.InRange(step, 0.3, 0.4);
            Assert.True(dx < 0 && dy > 0, $"tick {i}: moved ({dx}, {dy}), not west and north");
            if (states[i].X % MotionMath.SubunitsPerTile != 0 || states[i].Y % MotionMath.SubunitsPerTile != 0)
                offCentre++;
        }
        _output.WriteLine($"the ghost: {moving} ticks moving, {offCentre} off a tile's centre; through a wall: {crossedWall}");
        Assert.True(moving >= 2 * Second - 4, $"the ghost moved in {moving} ticks");
        Assert.True(offCentre > moving / 2, "the ghost flies between the tiles' centres");
        Assert.True(crossedWall, "the ghost passed through a wall");
        // its client predicts it: the drawn ghost is where the server has it
        Assert.True(LocomotionSeam.TryGetState(w.World, id, out MotionState now, out _));
        (float x, float y, _) = p.Session.Prediction.RenderPosition();
        Assert.InRange(x - MotionMath.ToTiles(now.X), -0.3f, 0.3f);
        Assert.InRange(y - MotionMath.ToTiles(now.Y), -0.3f, 0.3f);
        Assert.Equal(0, p.Counters.Snaps);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_weightless_body_drifts_at_tgs_drift_speed_a_fraction_of_a_tile_each_tick()
    {
        using RealHeadlessWorld w = Meta(92);
        RealHeadlessWorld.Player p = w.Join("Bob");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int bootRuntimes = w.World.RuntimeErrorCount;
        w.Host.EnqueueConsoleLine("godmode Bob"); // no suit: space's cold and vacuum are not this test's
        w.Run(2);
        int z = In(w, () => (int)me.z);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(39, 116, z)))); // outside the Auxiliary Airlock (the jetpack tests' spot)
        w.Run(Second);
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(me.newtonian_move(GlobalProcs.dir2angle(Dm.WEST))))), "tg's push west");
        Assert.NotNull(In(w, () => me.drift_handler));
        var states = new List<MotionState>();
        var tiles = new List<int>();
        w.Run(Second, _ =>
        {
            states.Add(w.ServerState(p));
            tiles.Add(In(w, () => (int)me.x));
        });
        var steps = new List<int>();
        for (int i = 1; i < states.Count; i++)
            steps.Add(states[i - 1].X - states[i].X);
        _output.WriteLine("drift steps (subunits a tick): " + string.Join(" ", steps) + $"; tiles {tiles[0]} to {tiles[^1]}");
        // tg's DEFAULT_INERTIA_SPEED: a tile per 5 deciseconds, a tenth of a tile a tick (25.6 subunits), every tick, never a tile at once
        Assert.All(steps.Skip(1), step => Assert.InRange(step, 15, 40));
        Assert.True(tiles[^1] <= tiles[0] - 1, "the body drifted a tile and more west, its loc following");
        Assert.Equal(MotionMath.TileOf(states[^1].X), tiles[^1]); // the capsule's tile is its loc
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
