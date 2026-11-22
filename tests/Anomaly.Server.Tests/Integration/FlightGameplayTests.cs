using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using System.IO;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV416 (D328) through the real server with a headless client, on the fixture tests/fixtures/maps/t2/multiz (two linked levels, a 3 x 3
/// room each, open space at (3, 3, 2)): a player throws a wrench at the open space beside her with the crosshair on it; it flies an arc
/// the client draws from the server's own segments, drops through the hole and lands on the floor below, and no drawn frame jumps.
/// </summary>
public class FlightGameplayTests(ITestOutputHelper output)
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
    public void AThrownWrenchArcsIntoTheOpenSpaceAndFallsSmoothlyToTheFloorBelow()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        long wrenchId;
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(2, 3, 2)));
            obj_item_wrench wrench = Dm.@new<obj_item_wrench>((turf)locate(2, 3, 2));
            Waiting.NoSleep(aliceMob.put_in_active_hand(wrench));
            wrenchId = wrench.dm_id;
        }
        w.Run(40);
        alice.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        w.Run(5);

        // the crosshair from her eye on the floor of the open space's tile
        (float ox, float oy, int z) = alice.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (3 - 0.5) * 2 - eastE, dy = (3 - 0.5) * 2 - northE, dz = -upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        ShotAim aim = PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
        Assert.True(alice.Session.Throw(new ClickTarget(TargetKind.Turf, 0, 3, 3, 2, 16, 16), aim)); // throw mode toggled on before: the throw turns it off as tg's

        ReplicaWorld replica = alice.Client.Replica;
        bool sawArc = false;
        double last = double.NaN;
        var drawn = new List<double>();
        for (int i = 0; i < 80; i++)
        {
            w.Run(1);
            if (!replica.TryGet(wrenchId, out ReplicaEntity e))
                continue;
            sawArc |= e.State.Flight.IsMoving;
            if (!alice.Session.Poses.TryGetPose(e, out RenderPose pose))
                continue;
            // the drawn height in levels, its east and north in tiles
            double h = pose.Z + pose.Rise / TileSpace.LevelHeight;
            drawn.Add(h);
            if (!double.IsNaN(last))
                Assert.True(Math.Abs(h - last) < 0.45, $"the drawn height jumps from {last} to {h} at tick {i}");
            last = h;
        }
        output.WriteLine("drawn heights: " + string.Join(" ", drawn.Select(v => v.ToString("0.00"))));
        Assert.True(sawArc, "the client got the server's arc");
        Assert.Contains(drawn, v => v > 2.2); // it left her hand above the upper floor
        Assert.Equal(1, last, 2); // and lies on the lower floor
        using (w.World.Enter())
        {
            w.World.Objects.TryGet(wrenchId, out object found);
            var wrench = (obj_item)found;
            Assert.Equal(1f, Dm.num(wrench.z));
            Assert.Null(wrench.throwing);
            Assert.False(wrench.dm_flight.IsMoving);
        }
    }

    // The owner (2026-11-13): R throws at once where the crosshair points: one Throw action with the camera's aim (protocol 81,
    // GameScene.ThrowAtCrosshair), whatever the throw mode is. Alice on (2, 4, 2) aims at the floor near (4, 4, 2)'s west edge.
    [Fact]
    public void OneKeyThrowsTheHeldItemWhereTheCrosshairMeetsTheFloorAndLeavesThrowModeOff()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_wrench wrench;
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(2, 4, 2)));
            wrench = Dm.@new<obj_item_wrench>((turf)locate(2, 4, 2));
            Waiting.NoSleep(aliceMob.put_in_active_hand(wrench));
        }
        w.Run(40);
        (float ox, float oy, int z) = alice.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (3.7 - 0.5) * 2 - eastE, dy = (4 - 0.5) * 2 - northE, dz = -upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        ShotAim aim = PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));

        Assert.True(alice.Session.Throw(new ClickTarget(TargetKind.Turf, 0, 4, 4, 2, 16, 16), aim));
        w.Run(60);
        using (w.World.Enter())
        {
            Assert.Null(aliceMob.get_active_held_item());
            Assert.Equal(THROW_MODE_DISABLED, aliceMob.throw_mode);
            Assert.Same(locate(4, 4, 2), wrench.loc);
            Assert.Equal(3.7, wrench.dm_flight.X0, 1); // where the crosshair met the floor, not the tile's middle
            Assert.False(wrench.dm_flight.IsMoving);
        }
    }

    // The owner (2026-11-20): on a loaded server R sometimes did not throw (the item flashed back into the hand, or hit what was in reach).
    // An overloaded tick (tick usage at 95 or more) makes tg's client/Click queue the click for SSinput's next run; the throw mode must
    // still be on when it runs there.
    [Fact]
    public void AThrowWhoseClickAnOverloadedTickQueuesStillThrows()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_wrench wrench;
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(2, 4, 2)));
            wrench = Dm.@new<obj_item_wrench>((turf)locate(2, 4, 2));
            Waiting.NoSleep(aliceMob.put_in_active_hand(wrench));
        }
        w.Run(40);
        using (w.World.Enter())
        {
            DmWorldState.Current.ChargeTick(w.World, 100);
            float queued = GlobalProcs.SSinput.verb_queue.len;
            ClientSeam.Throw(aliceMob.client, locate(4, 4, 2), ClientSeam.ClickParams(LEFT_CLICK, false, false, false, 16, 16));
            Assert.Equal(queued + 2, GlobalProcs.SSinput.verb_queue.len); // the click, then the mode's restore
            Assert.Same(wrench, aliceMob.get_active_held_item());
            Assert.Equal(THROW_MODE_HOLD, aliceMob.throw_mode);
        }
        w.Run(60);
        using (w.World.Enter())
        {
            Assert.Null(aliceMob.get_active_held_item());
            Assert.Equal(THROW_MODE_DISABLED, aliceMob.throw_mode);
            Assert.IsAssignableFrom<turf>(wrench.loc);
        }
    }

    // Stage 2: Alice herself is thrown into the open space (as a blast or a grab's throw would): her own view rides the server's arc down
    // to the floor below with no jump, her capsule takes no wish in the air, and after the landing her prediction agrees with the server.
    [Fact]
    public void AThrownPlayerSeesHerOwnArcDownThroughTheOpenSpaceAndLandsWithoutACorrection()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        using (w.World.Enter())
            Waiting.NoSleep(aliceMob.forceMove(locate(2, 3, 2)));
        w.Run(40);
        using (w.World.Enter())
            Waiting.Detach(aliceMob.throw_at(locate(3, 3, 2), 3, 1));
        alice.Keys = MoveKeys.Forward; // held all through: no wish moves a body in the air
        Assert.True(alice.Client.Replica.TryGet(alice.Client.OwnEntity, out ReplicaEntity own));
        double Drawn() => alice.Session.Poses.TryGetPose(own, out RenderPose pose) ? pose.Z + pose.Rise / TileSpace.LevelHeight : double.NaN;
        double last = Drawn();
        bool flew = false;
        for (int i = 0; i < 60; i++)
        {
            w.Run(1);
            flew |= own.State.Flight.IsMoving;
            double h = Drawn();
            Assert.True(Math.Abs(h - last) < 0.45, $"the own drawn height jumps from {last} to {h} at tick {i}");
            last = h;
            if (!own.State.Flight.IsMoving && i > 30)
                alice.Keys = MoveKeys.None;
        }
        Assert.True(flew, "the own body flew the server's arc");
        Assert.Equal(1, last, 2);
        w.Run(20);
        MotionState server = w.ServerState(alice);
        Assert.Equal(1, server.Z);
        Assert.Equal((server.X, server.Y, server.Z), (alice.Session.Prediction.Predicted.X, alice.Session.Prediction.Predicted.Y, alice.Session.Prediction.Predicted.Z));
    }
}
