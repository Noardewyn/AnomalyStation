using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using System.IO;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;


namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D325, multi-z through the real server: the fixture tests/fixtures/maps/t2/multiz (two linked levels, a 3 x 3 room each, open
/// space at (3, 3, 2) over the lower room). The join map names the stack (protocol 76), a session holds the entities of the
/// other level of its stack, and a player who walks onto the open space falls to the floor below.
/// </summary>
public class MultiZGameplayTests
{
    private static string Fixture()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tests", "fixtures", "maps", "t2", "multiz");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("tests/fixtures/maps/t2/multiz not found");
    }

    [Fact]
    public void TheClientGetsTheStackSeesTheOtherLevelAndAWalkerFallsThroughOpenSpace()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        Assert.Equal(new[] { 2 }, w.Replicator.ReadMapGrid().LinkedBelowLevels);

        RealHeadlessWorld.Player p = w.Join("Faller");
        ReplicaWorld replica = p.Client.Replica;
        Assert.True(replica.IsLinkedBelow(2));
        Assert.True(replica.SameStack(1, 2));
        Assert.False(replica.IsLinkedBelow(1));

        // the player stands west of the hole on the upper level; a wrench lies on the lower level, under the other side of the room
        long wrench;
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human, "joined as " + own?.GetType().Name);
        using (w.World.Enter())
        {
            Waiting.NoSleep(own.forceMove(locate(2, 3, 2)));
            wrench = Dm.@new<obj_item_wrench>(locate(4, 2, 1)).dm_id;
        }
        w.Run(20);
        Assert.True(replica.TryGet(wrench, out _), "the other level of the stack is in the session's interest");

        p.Yaw = -Math.PI / 2; // east (Godot's yaw turns left)
        p.Keys = MoveKeys.Forward;
        // DV371: the fall is drawn easing down from the upper floor, no frame a jump
        Assert.True(replica.TryGet(p.Client.OwnEntity, out ReplicaEntity drawnOwn));
        double Drawn() => p.Session.Poses.TryGetPose(drawnOwn, out RenderPose pose) ? pose.Z + pose.Rise / TileSpace.LevelHeight : double.NaN;
        double last = Drawn();
        for (int i = 0; i < 50; i++)
        {
            if (i == 30) p.Keys = MoveKeys.None;
            w.Run(1);
            double h = Drawn();
            Assert.True(Math.Abs(h - last) < 0.45, "the drawn height jumps from " + last + " to " + h + " at tick " + i);
            last = h;
        }
        Assert.Equal(1, last, 3);
        using (w.World.Enter())
        {
            var faller = (mob_living)own;
            Assert.Equal(1f, faller.z);
            Assert.True(faller.get_brute_loss() is float brute && brute > 0, "the fall hurt the legs");
        }
        Assert.Equal(1, w.ServerState(p).Z);
        Assert.Equal(1, replica.TryGet(p.Client.OwnEntity, out ReplicaEntity ownEntity) ? ownEntity.State.Z : -1);
    }

    [Fact]
    public void AnElevatorCarriesItsRiderUpAndDownAndAWalkerStepsOntoTheRaisedCarWithNoMisprediction()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Rider");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human);
        obj_structure_transport_linear car;
        using (w.World.Enter())
        {
            // a one-tile public car under the open space: tg's elevator shaft runs up through it
            car = Dm.@new<obj_structure_transport_linear_public>(locate(3, 3, 1));
            Waiting.NoSleep(own.forceMove(locate(3, 3, 1)));
        }
        w.Run(40);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long before = Misses();

        // up: the car moves after its 3 s ride (move_after_delay's timer) with the rider on it; DV371: the ride is announced and the
        // rider is drawn rising over it, never a jump
        using (w.World.Enter())
            Assert.True(Dm.truthy(Waiting.NoSleep(car.transport_controller_datum.simple_move_wrapper(Dm.UP, car.elevator_vertical_speed))));
        w.Run(2);
        Assert.Equal(1, p.Client.Replica.TryGet(p.Client.OwnEntity, out ReplicaEntity riding) ? riding.State.Ride.Dz : 0);
        Assert.Equal(1, p.Client.Replica.TryGet(car.dm_id, out ReplicaEntity carEntity) ? carEntity.State.Ride.Dz : 0);
        RideSmoothly(w, p, 1, 2, 20 * 4, car.dm_id);
        Assert.True(riding.State.Ride.IsNone, "the ride ends on arrival");
        MotionState server = w.ServerState(p);
        Assert.Equal(2, car.z);
        Assert.Equal((2, 3, 3), (server.Z, server.TileX, server.TileY));
        Assert.Equal((server.X, server.Y, server.Z), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y, p.Session.Prediction.Predicted.Z));
        Assert.Equal(before, Misses());

        // off the raised car westward and back onto it: it holds a walker over the open space (BLOCK_Z_OUT_DOWN), no fall
        p.Yaw = Math.PI / 2; // west
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 60 && w.ServerState(p).TileX != 2; i++)
            w.Run(1);
        p.Yaw = -Math.PI / 2; // east
        for (int i = 0; i < 60 && w.ServerState(p).TileX != 3; i++)
            w.Run(1);
        p.Keys = MoveKeys.None;
        w.Run(20);
        server = w.ServerState(p);
        Assert.Equal((2, 3), (server.Z, server.TileX));
        Assert.Equal(before, Misses());

        // down again, the rider with it
        using (w.World.Enter())
            Assert.True(Dm.truthy(Waiting.NoSleep(car.transport_controller_datum.simple_move_wrapper(Dm.DOWN, car.elevator_vertical_speed))));
        RideSmoothly(w, p, 2, 1, 20 * 4, car.dm_id);
        server = w.ServerState(p);
        Assert.Equal(1, car.z);
        Assert.Equal(1, server.Z);
        Assert.Equal((server.X, server.Y, server.Z), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y, p.Session.Prediction.Predicted.Z));
        Assert.Equal(before, Misses());
        using (w.World.Enter())
            Assert.Equal(0f, Dm.num(((mob_living)own).get_brute_loss()));
    }

    // the owner (2026-11-10): walking about a moving car, the rider was drawn sinking under it: the tile it left let it go (its ride ended) and the
    // one it entered took it without one. On tg's debug map's 3 x 3 car at (37..39, 23..25, 1), the rider walks east across it while it rises.
    [Fact]
    public void ARiderWalkingAcrossAMovingCarRisesWithIt()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "multiz_debug", MapsFolder = RepoMaps() };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Walker");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        obj_structure_transport_linear car = null;
        using (w.World.Enter())
        {
            Waiting.NoSleep(own.forceMove(locate(37, 24, 6)));
            foreach (obj_structure_transport_linear module in Dm.each<obj_structure_transport_linear>(locate(38, 24, 6)))
                car = module;
        }
        Assert.NotNull(car);
        w.Run(40);
        using (w.World.Enter())
            Assert.True(Dm.truthy(Waiting.NoSleep(car.transport_controller_datum.simple_move_wrapper(Dm.UP, car.elevator_vertical_speed))));
        w.Run(2);
        Assert.True(p.Client.Replica.TryGet(p.Client.OwnEntity, out ReplicaEntity ownEntity));
        double Drawn() => p.Session.Poses.TryGetPose(ownEntity, out RenderPose pose) ? pose.Z + pose.Rise / TileSpace.LevelHeight : double.NaN;
        double last = Drawn(), highest = last;
        bool walked = false;
        p.Yaw = -Math.PI / 2; // east
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 20 * 4; i++)
        {
            w.Run(1);
            if (w.ServerState(p).TileX >= 38)
            {
                walked = true;
                p.Keys = MoveKeys.None;
            }
            double h = Drawn();
            Assert.True(h >= last - 1e-3, "the rider sinks from " + last + " to " + h + " at tick " + i);
            Assert.True(Math.Abs(h - last) < 0.2, "the rider jumps from " + last + " to " + h + " at tick " + i);
            (last, highest) = (h, Math.Max(highest, h));
        }
        Assert.True(walked, "the rider walked onto the next tile of the car");
        Assert.Equal(7, car.z);
        Assert.Equal(7, w.ServerState(p).Z);
        Assert.Equal(7, last, 2);
    }

    // DV371: runs the world tick by tick and checks the own body's drawn height (and the car's, which the map renderer carries by the same pose,
    // the owner's report 2026-11-09: the platform jumped) goes from level from to level to without a jump
    private static void RideSmoothly(RealHeadlessWorld w, RealHeadlessWorld.Player p, int from, int to, int ticks, long car = 0)
    {
        Assert.True(p.Client.Replica.TryGet(p.Client.OwnEntity, out ReplicaEntity own));
        ReplicaEntity carEntity = null;
        Assert.True(car == 0 || p.Client.Replica.TryGet(car, out carEntity));
        double Drawn(ReplicaEntity e) => p.Session.Poses.TryGetPose(e, out RenderPose pose) ? pose.Z + pose.Rise / TileSpace.LevelHeight : double.NaN;
        foreach (ReplicaEntity e in carEntity == null ? new[] { own } : new[] { own, carEntity })
            Assert.Equal(from, Drawn(e), 2);
        double[] last = { Drawn(own), carEntity == null ? 0 : Drawn(carEntity) }, lowest = (double[])last.Clone(), highest = (double[])last.Clone();
        for (int i = 0; i < ticks; i++)
        {
            w.Run(1);
            for (int k = 0; k < (carEntity == null ? 1 : 2); k++)
            {
                double h = Drawn(k == 0 ? own : carEntity);
                string who = k == 0 ? "the rider" : "the car";
                Assert.True(Math.Abs(h - last[k]) < 0.2, who + "'s drawn height jumps from " + last[k] + " to " + h + " at tick " + i);
                Assert.True(to > from ? h >= last[k] - 1e-3 : h <= last[k] + 1e-3, who + "'s ride turns back from " + last[k] + " to " + h + " at tick " + i);
                (last[k], lowest[k], highest[k]) = (h, Math.Min(lowest[k], h), Math.Max(highest[k], h));
            }
        }
        for (int k = 0; k < (carEntity == null ? 1 : 2); k++)
        {
            Assert.Equal(to, last[k], 2);
            Assert.Equal(Math.Min(from, to), lowest[k], 2);
            Assert.Equal(Math.Max(from, to), highest[k], 2);
        }
    }

    [Fact]
    public void AClimberGoesUpAndDownALadderOverTheClimbWithNoJumpAndNoMisprediction()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Climber");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human);
        obj_structure_ladder bottom, top;
        using (w.World.Enter())
        {
            bottom = Dm.@new<obj_structure_ladder>(locate(2, 2, 1));
            top = Dm.@new<obj_structure_ladder>(locate(2, 2, 2));
            Waiting.NoSleep(own.forceMove(locate(2, 2, 1)));
        }
        w.Run(40);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long before = Misses();

        using (w.World.Enter())
            Waiting.Detach(bottom.start_travelling(own, 1f));
        RideSmoothly(w, p, 1, 2, 40);
        MotionState server = w.ServerState(p);
        Assert.Equal((2, 2, 2), (server.Z, server.TileX, server.TileY));
        Assert.Equal(before, Misses());

        using (w.World.Enter())
            Waiting.Detach(top.start_travelling(own, 0f));
        RideSmoothly(w, p, 2, 1, 40);
        Assert.Equal(1, w.ServerState(p).Z);
        Assert.Equal(before, Misses());
    }

    // DV397 (the owner, 2026-11-11): a climber from the tile beside the ladder is drawn onto the ladder over the climb and arrives before its rails on the
    // other level; walking into the rails stops the body before them, the same on the server and in the prediction
    [Fact]
    public void AClimberFromBesideTheLadderIsDrawnOntoItArrivesBeforeItsRailsAndCannotWalkThroughThem()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Climber");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human);
        obj_structure_ladder bottom;
        using (w.World.Enter())
        {
            bottom = Dm.@new<obj_structure_ladder>(locate(2, 2, 1));
            Dm.@new<obj_structure_ladder>(locate(2, 2, 2));
            Waiting.NoSleep(own.forceMove(locate(3, 2, 1))); // east of the ladder (at the edge of the tile the move kept, DV370)
        }
        w.Run(40);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long before = Misses();
        Assert.True(p.Client.Replica.TryGet(p.Client.OwnEntity, out ReplicaEntity drawn));
        (int fx, int fy) = LadderBody.Front(2, 2, MotionMath.Quantize(MotionMath.DefaultRadius));
        (float spotX, float spotY) = (MotionMath.ToTiles(fx), MotionMath.ToTiles(fy));

        using (w.World.Enter())
            Waiting.Detach(bottom.start_travelling(own, 1f));
        Assert.True(p.Session.Poses.TryGetPose(drawn, out RenderPose start));
        Assert.True(start.X > 2.4f, $"stands beside the ladder at {start.X}");
        float lastX = start.X;
        for (int i = 0; i < 40; i++)
        {
            w.Run(1);
            Assert.True(p.Session.Poses.TryGetPose(drawn, out RenderPose pose));
            Assert.True(Math.Abs(pose.X - lastX) < 0.3f, $"the drawn climber jumps from x {lastX} to {pose.X} at tick {i}");
            lastX = pose.X;
        }
        Assert.Equal(spotX, lastX, 2);
        MotionState server = w.ServerState(p);
        Assert.Equal((2, fx, fy), (server.Z, server.X, server.Y));
        Assert.Equal(before, Misses());

        // walking north into the rails of the upper ladder: stopped before them, and the prediction agrees
        p.Yaw = 0;
        p.Keys = MoveKeys.Forward;
        w.Run(20);
        p.Keys = MoveKeys.None;
        w.Run(10);
        server = w.ServerState(p);
        Assert.Equal((2, 2, 2), (server.Z, server.TileX, server.TileY));
        Assert.True(server.Y <= fy, $"walked into the rails to {server.Y}");
        Assert.Equal(spotY, p.Session.Poses.TryGetPose(drawn, out RenderPose stood) ? stood.Y : float.NaN, 2);
        Assert.Equal(before, Misses());
    }

    // the owner (2026-11-10): still a misprediction going up the stairs. tg's debug map has lone stairs in an open room, walked onto at any angle
    [Theory]
    [InlineData(2, 2, 0.785, MoveKeys.Forward)]
    [InlineData(4, 2, -0.785, MoveKeys.Forward)]
    [InlineData(3, 2, 0.3, MoveKeys.Forward)]
    [InlineData(3, 2, -0.3, MoveKeys.Forward)]
    [InlineData(2, 2, 0, MoveKeys.Forward | MoveKeys.Right)]
    [InlineData(2, 3, 1.4, MoveKeys.Forward)]
    [InlineData(3, 2, 0, MoveKeys.Forward, 0.5f)] // the server's own speed (DV70)
    [InlineData(2, 2, 0.785, MoveKeys.Forward, 0.5f)]
    [InlineData(3, 2, 0.3, MoveKeys.Forward, 0.5f)]
    public void AWalkerCrossingTheStairsAtAnAngleIsNeverMispredicted(int startX, int startY, double yaw, MoveKeys keys, float speed = 1f)
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "multiz", MapsFolder = Fixture(), MoveSpeedScale = speed };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Climber");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        using (w.World.Enter())
        {
            Dm.@new<obj_structure_stairs_north>(locate(3, 3, 1));
            Waiting.NoSleep(own.forceMove(locate(startX, startY, 1)));
        }
        w.Run(40);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long before = Misses();
        p.Yaw = yaw;
        p.Keys = keys;
        var seen = new List<string>();
        w.Run(120, _ =>
        {
            MotionState s = w.ServerState(p);
            seen.Add($"{s.TileX},{s.TileY},{s.Z} c{p.Session.Prediction.Predicted.TileX},{p.Session.Prediction.Predicted.TileY},{p.Session.Prediction.Predicted.Z} m{Misses() - before}");
        });
        p.Keys = MoveKeys.None;
        w.Run(20);
        MotionState server = w.ServerState(p);
        Assert.True(before == Misses(), string.Join(" | ", seen));
        Assert.Equal((server.X, server.Y, server.Z), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y, p.Session.Prediction.Predicted.Z));
    }

    [Fact]
    public void AWalkerClimbsTheStairsAndComesBackDownWithNoMisprediction()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Climber");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human);
        using (w.World.Enter())
        {
            // north stairs under the open space: leaving them northward climbs to (3, 4, 2)
            Dm.@new<obj_structure_stairs_north>(locate(3, 3, 1));
            Waiting.NoSleep(own.forceMove(locate(3, 2, 1)));
        }
        w.Run(40);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;

        long before = Misses();
        p.Yaw = 0; // north
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 80 && w.ServerState(p).Z != 2; i++)
            w.Run(1);
        w.Run(2);
        p.Keys = MoveKeys.None;
        w.Run(20);
        MotionState server = w.ServerState(p);
        Assert.Equal(2, server.Z);
        Assert.Equal(4, server.TileY);
        Assert.Equal((server.X, server.Y, server.Z), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y, p.Session.Prediction.Predicted.Z));
        Assert.Equal(before, Misses());
        using (w.World.Enter())
            Assert.Equal(0f, Dm.num(((mob_living)own).get_brute_loss()));

        // back south off the floor onto the open space over the stairs: the fall lands on them (FALL_INTERCEPTED) and the walk goes on down
        p.Yaw = Math.PI;
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 80 && w.ServerState(p).TileY != 2; i++)
            w.Run(1);
        p.Keys = MoveKeys.None;
        w.Run(20);
        server = w.ServerState(p);
        Assert.Equal((1, 2), (server.Z, server.TileY));
        Assert.Equal((server.X, server.Y, server.Z), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y, p.Session.Prediction.Predicted.Z));
        Assert.Equal(before, Misses());
        using (w.World.Enter())
            Assert.Equal(0f, Dm.num(((mob_living)own).get_brute_loss()));
    }

    private static string RepoMaps()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "maps");
            if (File.Exists(Path.Combine(candidate, "map_jsons", "multiz_debug.json"))) return candidate;
        }
        throw new InvalidOperationException("maps/map_jsons/multiz_debug.json not found");
    }

    // the owner (2026-11-10) on tg's debug map: its south stairs at (21, 16, 1) and north stairs at (23, 16, 2), each a lone stair in a room
    [Theory]
    [InlineData(21, 18, 6, Math.PI, 0.5f, 1)] // a railing on the edge the stairs go up by: the climb is a press there
    [InlineData(21, 18, 6, Math.PI, 1f, 1)]
    [InlineData(23, 14, 7, 0, 0.5f, 1)]
    [InlineData(23, 14, 7, 0, 1f, 1)]
    [InlineData(22, 18, 6, 2.9, 0.5f, 0)] // past the stairs' side
    [InlineData(22, 14, 7, 0.3, 0.5f, -1)] // the railings round the open space keep it out from the east; it walks in from the south and falls onto the stairs
    public void OnTheDebugMapAWalkerClimbsEachStairsWithNoMisprediction(int startX, int startY, int startZ, double yaw, float speed, int levels)
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "multiz_debug", MapsFolder = RepoMaps(), MoveSpeedScale = speed };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Climber");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        using (w.World.Enter())
            Waiting.NoSleep(own.forceMove(locate(startX, startY, startZ)));
        w.Run(60);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long before = Misses();
        p.Yaw = yaw;
        p.Keys = MoveKeys.Forward;
        var seen = new List<string>();
        w.Run(100, _ =>
        {
            MotionState s = w.ServerState(p);
            seen.Add($"{s.TileX},{s.TileY},{s.Z} c{p.Session.Prediction.Predicted.TileX},{p.Session.Prediction.Predicted.TileY},{p.Session.Prediction.Predicted.Z} m{Misses() - before}");
        });
        p.Keys = MoveKeys.None;
        w.Run(20);
        MotionState server = w.ServerState(p);
        Assert.True(before == Misses(), string.Join(" | ", seen.Distinct()));
        Assert.True(server.Z == startZ + levels, "level " + server.Z + ": " + string.Join(" | ", seen.Distinct()));
        Assert.Equal((server.X, server.Y, server.Z), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y, p.Session.Prediction.Predicted.Z));
    }

    // the owner (2026-11-20): at the top of the stairs the body sank under the floor for a moment. A step off the upper floor onto the open
    // space over the top stair was DV435's timed fall to the floor under the flight (0.64 s, no walking, corrected on the client); tg's
    // stairs take it at once (intercept_zImpact), so the walk goes on down the flight
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void OnTheDebugMapAStepOffTheTopOntoTheStairsIsNoTimedFall(float speed)
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "multiz_debug", MapsFolder = RepoMaps(), MoveSpeedScale = speed };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Climber");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        using (w.World.Enter())
            Waiting.NoSleep(own.forceMove(locate(23, 18, 8))); // above the north stairs at (23, 16, 7)
        w.Run(60);
        long Misses() => p.Counters.Corrections + p.Counters.Snaps + p.Counters.Mismatches;
        long before = Misses();
        int flying = 0;
        p.Yaw = Math.PI; // south, onto the open space over the stairs
        p.Keys = MoveKeys.Forward;
        w.Run(40, _ => flying += FlightSeam.Of(w.World).IsFlying(own) ? 1 : 0);
        p.Keys = MoveKeys.None;
        w.Run(20);
        Assert.Equal(0, flying);
        Assert.Equal(7, w.ServerState(p).Z);
        Assert.Equal(before, Misses());
    }

    // the owner (2026-11-10): looking down through open space, the hover named nothing, and neither a point nor a shot went there: the server
    // refused every target off the own level. Alice on level 2 beside the open space at (3, 3, 2), Bob on level 1 beyond it.
    [Fact]
    public void ThroughOpenSpaceAPlayerHoversPointsAtAndShootsWhatIsOnTheLevelBelow()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(2, 3, 2)));
            Waiting.NoSleep(bobMob.forceMove(locate(4, 3, 1)));
        }
        w.Run(40);
        var hints = new List<ContextHintsMessage>();
        alice.Client.ContextHintsReceived += m => hints.Add(m);

        // the hover of the floor below is taken
        Assert.True(alice.Session.Hover(new ClickTarget(TargetKind.Turf, 0, 3, 3, 1, 16, 16), w.Now));
        w.Run(20);
        Assert.Contains(hints, h => h.TargetKind == TargetKind.Turf && (h.TurfX, h.TurfY, h.TurfZ) == (3, 3, 1)); // answered, not refused (a turf's name is the client's)

        // Alice's eye 1.3 m over (2, 3, 2) to Bob's chest 0.9 m over level 1, two tiles east: the line crosses level 2's floor over the open space
        (float ox, float oy, int z) = alice.Session.Prediction.RenderPosition();
        Assert.Equal(2, z);
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (4 - 0.5) * 2 - eastE, dy = (3 - 0.5) * 2 - northE, dz = -TileSpace.LevelHeight + 0.9 - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        ShotAim aim = PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));

        // Shift + middle click points at Bob
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 16), ClickButton.Middle, ClickModifiers.Shift, aim));
        w.Run(10);
        using (w.World.Enter())
            Assert.NotEmpty(Dm.each<obj_effect_temp_visual_point>(((turf)locate(2, 3, 2)).contents).ToList()); // tg puts the arrow on the pointer's tile

        // a shot along the same aim burns him
        alice.Session.DevCommand("shoot me /obj/projectile/beam/emitter/hitscan");
        w.Run(10);
        float Burn() { using (w.World.Enter()) return Dm.num(bobMob.get_fire_loss()); }
        float before = Burn();
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None, aim));
        w.Run(10);
        Assert.True(Burn() > before, "the shot reached Bob on the level below");
    }

    // the owner (2026-11-13): aiming up at another deck, a gun did not fire. Alice on level 1 beside the cell under the open space at
    // (3, 3, 2), Bob on level 2 beyond it; her laser gun and her full-auto gun both reach him.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARealGunShootsUpThroughOpenSpaceAtTheLevelAbove(bool fullAuto)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(2, 3, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(4, 3, 2)));
            obj_item_gun gun = fullAuto ? Dm.@new<obj_item_gun_energy_laser_cybersun_unrestricted>((turf)locate(2, 3, 1)) : Dm.@new<obj_item_gun_energy_laser>((turf)locate(2, 3, 1));
            gun.pin = Dm.@new<obj_item_firing_pin>(gun);
            Waiting.NoSleep(aliceMob.put_in_active_hand(gun));
        }
        w.Run(40);
        (float ox, float oy, int z) = alice.Session.Prediction.RenderPosition();
        Assert.Equal(1, z);
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (4 - 0.5) * 2 - eastE, dy = (3 - 0.5) * 2 - northE, dz = TileSpace.LevelHeight + 0.9 - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        ShotAim aim = PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
        float Burn() { using (w.World.Enter()) return Dm.num(bobMob.get_fire_loss()); }
        float before = Burn();
        var target = new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 16);
        alice.Session.MouseDown(target, ClickButton.Left, ClickModifiers.None, aim, w.Now);
        Assert.True(alice.Session.Click(target, ClickButton.Left, ClickModifiers.None, aim));
        w.Run(10);
        alice.Session.MouseUp(target, ClickModifiers.None, aim);
        w.Run(10);
        Assert.True(Burn() > before, "the shot reached Bob on the level above");
    }
}
