using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tests.Tg.M4aHands.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Flight;

/// <summary>
/// DV416 (the owner, 2026-11-13): a thrown thing flies a ballistic arc (FlightSeam) with tg's moves at every tile it crosses, lands where it
/// touches the floor and rests at that point; it stops short of a wall and falls, a ceiling turns it down, open space lets it fall to the
/// level below, and the air slides a thing along the floor until friction stops it.
/// </summary>
public class FlightTests
{
    // Steps the room a tick at a time, keeping the arc point of each tick (tiles, tiles, tile pixels) and its level
    private static List<(double X, double Y, double Lift, int Z)> Track(T2Room r, atom_movable thing, int ticks)
    {
        var points = new List<(double, double, double, int)>();
        for (int i = 0; i < ticks; i++)
        {
            r.Step();
            r.In(() =>
            {
                FlightArc arc = thing.dm_flight;
                if (!arc.IsNone)
                {
                    (double x, double y, double l) = arc.At(r.World.Tick);
                    points.Add((x, y, l, arc.Z));
                }
            });
        }
        return points;
    }

    private static void Throw(T2Room r, mob_living_carbon_human who, obj_item it, atom target, string aim = null)
    {
        r.In(() =>
        {
            Waiting.NoSleep(who.put_in_hands(it));
            who.throw_mode_on(THROW_MODE_TOGGLE);
        });
        r.Command(() =>
        {
            if (aim != null)
                FlightSeam.NoteAim(who, params2list(aim));
            Waiting.Detach(who.throw_item(target));
        });
    }

    [Fact]
    public void AThrownItemFliesAnArcThroughTgsTilesAndRestsWhereItLands()
    {
        var r = new T2Room(4400);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        obj_item it = HandsTests.Item(r, 3, 8, "rock");
        turf target = r.In(() => locate(8, 8, 1));
        Throw(r, bob, it, target);
        var points = Track(r, it, 30);
        Assert.True(points.Count > 5, "the flight has an arc");
        Assert.Contains(points, p => p.Lift > FlightSeam.HandPixels); // it rises out of the hand before it falls
        for (int i = 1; i < points.Count; i++)
            Assert.True(Math.Abs(points[i].X - points[i - 1].X) < 1, "no tile jumps: the arc moves under a tile a tick");
        r.In(() =>
        {
            Assert.Null(it.throwing);
            Assert.Same(target, it.loc);
            FlightArc rest = it.dm_flight;
            Assert.False(rest.IsMoving);
            Assert.Equal(8, rest.X0, 2);
            Assert.Equal(8, rest.Y0, 2);
            Assert.Equal(0, rest.Lift0, 2);
        });
        // tg moving it itself (a pick-up) ends its rest point
        r.In(() => Waiting.NoSleep(bob.put_in_hands(it)));
        Assert.True(r.In(() => it.dm_flight.IsNone));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAimedThrowLandsWhereTheCrosshairMeetsTheFloor()
    {
        var r = new T2Room(4401);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        obj_item it = HandsTests.Item(r, 3, 8, "rock");
        turf target = r.In(() => locate(6, 8, 1));
        // the eye at 1.3 m over the tile's centre, looking east and down at the floor 2.8 tiles off: (5.8, 8) on tile 6
        double down = Math.Atan(20.8 / (2.8 * 32)) * 180 / Math.PI;
        Throw(r, bob, it, target, ClientSeam.AimParam(3 * 32, 8 * 32, 20.8, 90, -down));
        Track(r, it, 30);
        r.In(() =>
        {
            Assert.Same(target, it.loc);
            Assert.Equal(5.8, it.dm_flight.X0, 2);
            Assert.Equal(8, it.dm_flight.Y0, 2);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThrowAtAWallStopsShortOfItsFaceAndFallsToTheFloor()
    {
        var r = new T2Room(4402);
        int from = T2Room.Size - 5;
        mob_living_carbon_human bob = r.Join(from, 8, "Bob", name: "bob");
        obj_item it = HandsTests.Item(r, from, 8, "rock");
        int wallX = r.In(() =>
        {
            for (int x = from; x <= T2Room.Size; x++)
                if (Dm.truthy(locate(x, 8, 1).density)) return x;
            return -1;
        });
        Assert.True(wallX > 0);
        // aimed level at chest height: it reaches the wall in the air
        Throw(r, bob, it, r.In(() => locate(wallX, 8, 1)), ClientSeam.AimParam(from * 32, 8 * 32, 20.8, 90, 0));
        Track(r, it, 40);
        r.In(() =>
        {
            Assert.Null(it.throwing);
            Assert.Same(locate(wallX - 1, 8, 1), it.loc);
            FlightArc rest = it.dm_flight;
            Assert.False(rest.IsMoving);
            Assert.Equal(wallX - 1 + 0.5 - FlightSeam.WallMargin, rest.X0, 2);
            Assert.Equal(0, rest.Lift0, 2); // it fell from the wall to the floor
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACeilingTurnsASteepThrowBackDown()
    {
        var r = new T2Room(4403);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        obj_item it = HandsTests.Item(r, 3, 8, "rock");
        r.In(() => CeilingSystem.Of(r.World).Initialize());
        Throw(r, bob, it, r.In(() => locate(5, 8, 1)), ClientSeam.AimParam(3 * 32, 8 * 32, 20.8, 90, 70));
        var points = Track(r, it, 40);
        Assert.All(points, p => Assert.True(p.Lift <= FlightSeam.CeilingPixels + 1e-3, "never through the ceiling: " + p.Lift));
        Assert.Contains(points, p => p.Lift > FlightSeam.CeilingPixels - 1);
        Assert.Equal(0, r.In(() => it.dm_flight.Lift0), 2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThingThrownIntoOpenSpaceFallsOnToTheLevelBelowWithoutAJump()
    {
        World world = T2Maps.Boot("multiz", out TgTestWorld.Log log);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        obj_item_wrench wrench;
        using (world.Enter())
        {
            wrench = Dm.@new<obj_item_wrench>(locate(2, 3, 2));
            Waiting.NoSleep(wrench.throw_at(locate(3, 3, 2), 7, 1));
        }
        var heights = new List<double>(); // the height over level 1's floor, tile pixels
        int lowest = 2;
        for (int i = 0; i < 60; i++)
        {
            world.Step();
            using (world.Enter())
            {
                FlightArc arc = wrench.dm_flight;
                if (arc.IsNone)
                    continue;
                (_, _, double lift) = arc.At(world.Tick);
                heights.Add(lift + (arc.Z - 1) * FlightArc.PixelsPerTile);
                lowest = Math.Min(lowest, arc.Z);
            }
        }
        using (world.Enter())
        {
            Assert.Equal(1, lowest);
            Assert.Equal(1f, Dm.num(wrench.z));
            Assert.Null(wrench.throwing);
            Assert.False(wrench.dm_flight.IsMoving);
            Assert.Empty(log.Runtimes);
        }
        for (int i = 1; i < heights.Count; i++)
            Assert.True(Math.Abs(heights[i] - heights[i - 1]) < 16, "the drop through the level is continuous");
    }

    [Fact]
    public void TheAirSlidesAThingAlongTheFloorAndFrictionStopsItBetweenTiles()
    {
        var r = new T2Room(4404);
        obj_item it = HandsTests.Item(r, 4, 8, "paper");
        Assert.True(r.In(() => FlightSeam.Push(it, EAST, 100)));
        var points = Track(r, it, 80);
        Assert.True(points.Count > 3);
        r.In(() =>
        {
            FlightArc rest = it.dm_flight;
            Assert.False(rest.IsMoving);
            Assert.True(rest.X0 > 4.6, "it slid off its tile: " + rest.X0);
            Assert.Equal((int)Math.Round(rest.X0), (int)Dm.num(it.x)); // tg's loc is the tile it rests on
            Assert.Equal(0, rest.Lift0, 3);
        });
        r.AssertNoRuntimes();
    }

    // The owner (2026-11-18): things the air pushed against a grille jerked to and fro at every air pass
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AThingTheAirPushesAgainstAGrilleStaysPut(bool big)
    {
        var r = new T2Room(4405);
        atom_movable thing = big
            ? r.In(() => (atom_movable)Dm.@new<obj_structure_closet>(locate(4, 8, 1)))
            : HandsTests.Item(r, 4, 8, "paper");
        r.In(() => Dm.@new<obj_structure_grille>(locate(5, 8, 1)));
        var xs = new List<double>();
        for (int pass = 0; pass < 12; pass++)
        {
            r.In(() => thing.experience_pressure_difference(200f, EAST));
            for (int i = 0; i < 4; i++)
            {
                r.In(() => xs.Add(thing.dm_flight.IsNone ? Dm.num(thing.x) : thing.dm_flight.At(r.World.Tick + 2).X)); // as a client draws it ahead
                r.Step();
            }
        }
        r.In(() => Assert.Same(locate(4, 8, 1), thing.loc));
        // the first passes take it to the grille (an item rests at its face, a big thing back on its tile's middle); then it stays
        List<double> late = xs.Skip(xs.Count / 2).ToList();
        Assert.True(late.Max() - late.Min() < 0.01, "it stays put: " + string.Join(",", late.Select(x => x.ToString("0.00"))));
        Assert.True(late.Max() <= 4.5 - FlightSeam.WallMargin + 0.01, "short of the grille: " + late.Max());
        r.AssertNoRuntimes();
    }
}
