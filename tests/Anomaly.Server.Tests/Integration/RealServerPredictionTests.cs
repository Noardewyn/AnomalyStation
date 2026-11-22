using Anomaly.Client.Shared;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// P8's headless prediction test rerun against the real server (M2 integration, item 6): the client's real stack
/// (<see cref="RealHeadlessWorld"/>) and the booted tg world with the locomotion bridge, over the loopback at 0, 100 and 200 ms
/// one way. Walking alone is never corrected and the replay is bit-equal; a shove costs the shoved player one to three
/// corrections; the capsule moves at tg's run and walk speeds (RUN_DELAY 1.5, WALK_DELAY 4: 13.3 and 5 m/s at 2 m tiles).
/// </summary>
public class RealServerPredictionTests
{
    private readonly ITestOutputHelper _output;

    public RealServerPredictionTests(ITestOutputHelper output) => _output = output;

    private static LoopbackOptions Net(int latencySteps, double loss = 0, ulong seed = 5)
        => new() { Seed = seed, TimeoutSteps = 400, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps, LossRate = loss };

    // P8's walk: along the room, into walls, around, through the doorways.
    private static void WalkAbout(RealHeadlessWorld world, RealHeadlessWorld.Player p, int seconds)
    {
        MoveKeys[] plan = { MoveKeys.Forward, MoveKeys.Forward | MoveKeys.Right, MoveKeys.Right, MoveKeys.Back | MoveKeys.Right, MoveKeys.Back, MoveKeys.Left, MoveKeys.Forward | MoveKeys.Left };
        double[] yaws = { 0, 0.3, 1.2, -0.8, 2.0, -1.5, 0.0 };
        for (int s = 0; s < seconds; s++)
        {
            p.Keys = plan[s % plan.Length];
            p.Yaw = yaws[s % yaws.Length];
            world.Run(20);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void WalkingAloneOnTheRealServerGivesZeroCorrectionsAndEqualBits(int latencySteps)
    {
        using var world = new RealHeadlessWorld(Net(latencySteps));
        RealHeadlessWorld.Player walker = world.Join("Walker", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        WalkAbout(world, walker, 30);
        PredictionCounters c = walker.Counters;
        _output.WriteLine($"{latencySteps * 50} ms: reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, mismatches {c.Mismatches}, max error {c.MaxErrorTiles:0.000} tiles");
        Assert.True(c.Reconciliations > 400, "reconciliations " + c.Reconciliations);
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Snaps);
        Assert.Equal(0, c.Mismatches);
        walker.Keys = MoveKeys.None;
        world.Run(20);
        MotionState server = world.ServerState(walker);
        Assert.Equal((server.X, server.Y), (walker.Session.Prediction.Predicted.X, walker.Session.Prediction.Predicted.Y));
        Assert.Equal(0, world.World.RuntimeErrorCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void AShoveCostsTheShovedPlayerOneToThreeCorrections(int latencySteps)
    {
        using var world = new RealHeadlessWorld(Net(latencySteps));
        RealHeadlessWorld.Player bob = world.Join("Bob", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        RealHeadlessWorld.Player alice = world.Join("Alice", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        mob_living bobMob = world.MobOf(bob), aliceMob = world.MobOf(alice);
        int shoves = 0;
        for (int round = 0; round < 3; round++)
        {
            // Alice stands a tile west of Bob (a tg-made move: both clients see a snap, then rest).
            using (world.World.Enter())
            {
                turf bobTurf = (turf)bobMob.loc;
                Waiting.NoSleep(aliceMob.forceMove(Builtins.locate((int)bobTurf.x - 1, (int)bobTurf.y, 1)));
            }
            world.Run(30);
            long before = bob.Counters.Corrections + bob.Counters.Snaps;
            (int bx, int by, _) = Tile(world, bobMob);
            Assert.True(alice.Session.Click(bobMob.dm_id, ClickButton.Right, ClickModifiers.None));
            world.Run(40);
            (int ax, int ay, _) = Tile(world, bobMob);
            if ((ax, ay) == (bx, by))
                continue; // a wall east of Bob: knocked down in place
            shoves++;
            long corrections = bob.Counters.Corrections + bob.Counters.Snaps - before;
            _output.WriteLine($"{latencySteps * 50} ms, shove {round + 1}: ({bx},{by}) -> ({ax},{ay}), corrections {corrections}, snaps {bob.Counters.Snaps}, max error {bob.Counters.MaxErrorTiles:0.00}");
            Assert.InRange(corrections, 1, 3);
            MotionState server = world.ServerState(bob);
            Assert.Equal((server.X, server.Y), (bob.Session.Prediction.Predicted.X, bob.Session.Prediction.Predicted.Y));
        }
        Assert.True(shoves >= 1, "no shove moved Bob");
        Assert.Equal(0, world.World.RuntimeErrorCount);
    }

    private static (int X, int Y, int Z) Tile(RealHeadlessWorld world, mob_living mob)
    {
        using (world.World.Enter())
            return ((int)mob.x, (int)mob.y, (int)mob.z);
    }

    [Fact]
    public void ThePlayerRunsAndWalksAtTgsSpeeds()
    {
        using var world = new RealHeadlessWorld(Net(0));
        RealHeadlessWorld.Player p = world.Join("Runner");
        double run = Speed(world, p, 1);
        p.Session.ToggleMoveIntent();
        world.Run(5);
        double walk = Speed(world, p, -1);
        _output.WriteLine($"run {run:0.00} m/s, walk {walk:0.00} m/s");
        Assert.Equal(13.33, run, 1); // 0.5 / 1.5 tiles per tick, 20 ticks per second, 2 m per tile
        Assert.Equal(5.0, walk, 1); // 0.5 / 4
    }

    // Metres per second of the server's capsule walking north (+1) or south (-1): the median step of 10 ticks in the open
    // (a tick that slides along a wall or crosses into a slower tile is not the speed).
    private double Speed(RealHeadlessWorld world, RealHeadlessWorld.Player p, int north)
    {
        p.Keys = MoveKeys.Forward; // facing the way it goes: backward is slower (DV263)
        p.Yaw = north > 0 ? 0 : Math.PI;
        world.Run(4 + MoveFeel.Default.AccelTicks); // up to speed (the input clock's lead, the speed ramp)
        var steps = new List<double>();
        MotionState a = world.ServerState(p);
        for (int i = 0; i < 10; i++)
        {
            world.Run(1);
            MotionState b = world.ServerState(p);
            steps.Add(Math.Sqrt((double)(b.X - a.X) * (b.X - a.X) + (double)(b.Y - a.Y) * (b.Y - a.Y)));
            a = b;
        }
        p.Keys = MoveKeys.None;
        world.Run(10);
        _output.WriteLine("steps (1/256 tile): " + string.Join(" ", steps));
        steps.Sort();
        double median = steps[steps.Count / 2];
        return median / MotionMath.SubunitsPerTile / RealHeadlessWorld.TickSeconds * 2.0;
    }
}
