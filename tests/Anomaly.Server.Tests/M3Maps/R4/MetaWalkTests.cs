using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.Integration;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.M3Maps.R4;

/// <summary>
/// M3 R4, the owner's walk on Meta (the character could not move; corrections rolled it back to the spawn): the real client
/// stack walks a route from the arrivals spawn through the arrivals corridor, an access door (with the dev access, D117),
/// the corridor north and the hallway east, and a public airlock. It steers to waypoints from its own predicted position,
/// as a player would. The server moves the character (gravity) and the client predicts against the walls and doors the
/// join map has, so corrections are rare: at most a few, on door edges.
/// </summary>
public class MetaWalkTests
{
    private readonly ITestOutputHelper _output;

    public MetaWalkTests(ITestOutputHelper output) => _output = output;

    // Tile coordinates (BYOND x, y; a tile's centre is its integer coordinate).
    private static readonly (double X, double Y, string What)[] Route =
    {
        (50, 122, "the arrivals corridor east"),
        (50, 119, "south through the access door at 50,120"),
        (50, 122, "back north through it"),
        (51.5, 122, "east to the corridor north"),
        (51.5, 127.5, "the corridor north"),
        (64.5, 127.5, "the hallway east"),
        (64.5, 130, "north"),
        (68, 130, "east through the airlock at 66,130"),
    };

    [MetaServerFact]
    public void The_character_walks_Metas_corridors_and_doors_with_few_corrections()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = world.Join("Walker");
        // the spawn is a chair of the docked arrivals shuttle (M10 slice G3): the walk starts in the arrivals corridor
        using (world.World.Enter())
            Anomaly.Core.Kernel.Waiting.NoSleep(world.MobOf(p).forceMove(Builtins.locate(40, 122, 1)));
        world.Run(2);
        PredictionCounters atStart = p.Counters; // the teleport is one snap
        MotionState start = world.ServerState(p);
        Assert.Equal((40.0, 122.0), (start.X / 256.0, start.Y / 256.0));
        world.Host.EnqueueConsoleLine("access Walker all");
        world.Run(2);

        int leg = 0, ticks = 0;
        long lastCorrections = 0;
        while (leg < Route.Length && ticks < 20 * 40)
        {
            (double tx, double ty, string what) = Route[leg];
            var predicted = p.Session.Prediction.Predicted;
            double x = predicted.X / 256.0, y = predicted.Y / 256.0;
            double dx = tx - x, dy = ty - y;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
            {
                _output.WriteLine($"tick {ticks}: reached {tx},{ty} ({what})");
                leg++;
                continue;
            }
            p.Keys = MoveKeys.Forward;
            p.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            world.Run(1);
            ticks++;
            long corrections = p.Counters.Corrections;
            if (corrections != lastCorrections)
            {
                MotionState s = world.ServerState(p);
                _output.WriteLine($"tick {ticks}: correction {corrections} at server {s.X / 256.0:0.00},{s.Y / 256.0:0.00} leg {leg} ({what})");
                lastCorrections = corrections;
            }
        }
        p.Keys = MoveKeys.None;
        world.Run(10);
        MotionState end = world.ServerState(p);
        PredictionCounters c = p.Counters;
        _output.WriteLine($"route: {leg} of {Route.Length} legs in {ticks} ticks; server at {end.X / 256.0:0.00},{end.Y / 256.0:0.00}; reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, mismatches {c.Mismatches}, max error {c.MaxErrorTiles:0.00} tiles");
        Assert.Equal(Route.Length, leg);
        // within the stop's coast (DV263: a run brakes over MoveFeel.Default's 3 ticks after the keys go up)
        Assert.True(Math.Abs(end.X / 256.0 - 68) < 0.5 && Math.Abs(end.Y / 256.0 - 130) < 0.5, "the walk did not end past the airlock");
        Assert.True(c.Corrections - atStart.Corrections <= 4, "corrections " + (c.Corrections - atStart.Corrections));
        Assert.Equal(0, c.Snaps - atStart.Snaps);
    }
}
