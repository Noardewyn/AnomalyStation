using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using System.IO;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D329 (DV418) through the real server with a headless client, on the fixture tests/fixtures/maps/t2/multiz (two linked levels, a 3 x 3
/// room each, open space at (3, 3, 2)): the drop key puts the held wrench down where the crosshair is: on the floor in reach at that point,
/// on a table at that point of its top (tg's placing), over the open space (it falls to the level below), and out of reach at the feet.
/// </summary>
public class PutDownGameplayTests
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

    private sealed class Room : IDisposable
    {
        public readonly RealHeadlessWorld W;
        public readonly RealHeadlessWorld.Player Alice;
        public readonly mob_living_carbon_human Mob;
        public readonly obj_item_wrench Wrench;

        public Room(ulong seed, Action<RealHeadlessWorld> furnish = null)
        {
            W = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
            Alice = W.Join("Alice");
            Mob = (mob_living_carbon_human)W.MobOf(Alice);
            using (W.World.Enter())
            {
                Waiting.NoSleep(Mob.forceMove(locate(2, 4, 2)));
                Wrench = Dm.@new<obj_item_wrench>((turf)locate(2, 4, 2));
                Waiting.NoSleep(Mob.put_in_active_hand(Wrench));
                furnish?.Invoke(W);
            }
            W.Run(40);
        }

        // the crosshair from her eye on the point (x, y) tiles, lift metres over her level's floor
        public ShotAim AimAt(double x, double y, double lift)
        {
            (float ox, float oy, int z) = Alice.Session.Prediction.RenderPosition();
            double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
            double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = lift - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
        }

        public void Dispose() => W.Dispose();
    }

    [Fact]
    public void OnTheFloorInReachTheWrenchLiesWhereTheCrosshairIs()
    {
        using var r = new Room(11);
        Assert.True(r.Alice.Session.PutDown(new ClickTarget(TargetKind.Turf, 0, 3, 4, 2, 16, 16), r.AimAt(3.25, 4.3, 0)));
        r.W.Run(5);
        using (r.W.World.Enter())
        {
            Assert.Null(r.Mob.get_active_held_item());
            Assert.Same(locate(3, 4, 2), r.Wrench.loc);
            Assert.InRange(r.Wrench.pixel_x - r.Wrench.base_pixel_x, 7, 9); // 0.25 of a tile east of the middle
            Assert.InRange(r.Wrench.pixel_y - r.Wrench.base_pixel_y, 9, 11); // 0.3 north
        }
    }

    [Fact]
    public void OnATableTheWrenchLiesWhereTheCrosshairMeetsItsTop()
    {
        long table = 0;
        using var r = new Room(12, w => table = Dm.@new<obj_structure_table>((turf)locate(3, 4, 2)).dm_id);
        Assert.True(r.Alice.Session.PutDown(new ClickTarget(TargetKind.Entity, table, 0, 0, 0, 16, 16), r.AimAt(2.8, 3.7, FlightSeam.TableTopPixels / 16.0)));
        r.W.Run(5);
        using (r.W.World.Enter())
        {
            Assert.Null(r.Mob.get_active_held_item());
            Assert.Same(locate(3, 4, 2), r.Wrench.loc);
            Assert.InRange(r.Wrench.pixel_x - r.Wrench.base_pixel_x, -7, -5); // 0.2 of a tile west of the middle
            Assert.InRange(r.Wrench.pixel_y - r.Wrench.base_pixel_y, -11, -9); // 0.3 south
        }
    }

    [Fact]
    public void OverTheOpenSpaceTheWrenchFallsToTheLevelBelow()
    {
        using var r = new Room(13);
        Assert.True(r.Alice.Session.PutDown(new ClickTarget(TargetKind.Turf, 0, 3, 3, 2, 16, 16), r.AimAt(3, 3, 0)));
        r.W.Run(60);
        using (r.W.World.Enter())
        {
            Assert.Null(r.Mob.get_active_held_item());
            Assert.Equal(1f, Dm.num(r.Wrench.z));
        }
    }

    [Fact]
    public void OutOfReachTheWrenchIsDroppedAtTheFeet()
    {
        using var r = new Room(14);
        Assert.True(r.Alice.Session.PutDown(new ClickTarget(TargetKind.Turf, 0, 4, 2, 2, 16, 16), r.AimAt(4, 2, 0)));
        r.W.Run(5);
        using (r.W.World.Enter())
        {
            Assert.Null(r.Mob.get_active_held_item());
            Assert.Same(locate(2, 4, 2), r.Wrench.loc); // tg's drop
        }
    }
}
