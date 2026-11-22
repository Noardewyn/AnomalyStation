using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M10;

/// <summary>
/// Transit tubes (transit_tube.dm, station.dm, transit_tube_pod.dm, transit_tube_construction.dm) in a booted toy room: a player walks into a
/// dispenser, rides a temporary pod through a curve, a diagonal and a second curve and is let out at the far dispenser; a reusable pod
/// shuttles between two terminus stations and is turned around; the RPD's unattached tube is wrenched into a tube.
/// </summary>
public class TransitTubeTests
{
    private const int Second = 20;

    private static T Place<T>(AudioRoom r, int x, int y, int dir) where T : atom
        => r.In(() =>
        {
            T thing = Dm.@new<T>(locate(x, y, 1));
            Waiting.NoSleep(thing.setDir(dir));
            (thing as obj_structure_transit_tube)?.init_tube_dirs(); // a mapped tube has its dir before Initialize
            return thing;
        });

    /// <summary>
    /// Steps to the first fire of SSobj, where the stations process (station.dm:27) and launch a waiting pod. The Master staggers a
    /// subsystem's first fire by a random 0 to 2 s (master.dm:621), so the pods' timeline is counted from that fire, not from the start.
    /// </summary>
    private static void StepToFirstSobjFire(AudioRoom r)
    {
        float before = r.In(() => SSobj.times_fired);
        for (int ticks = 0; r.In(() => SSobj.times_fired) == before; ticks++)
        {
            Assert.True(ticks <= 2 * Second + 2, "SSobj did not fire within its stagger and one period");
            r.Step();
        }
    }

    [Fact]
    public void ADispenserPodCarriesThePlayerThroughCurvesToTheOtherDispenser()
    {
        var r = new AudioRoom(12, 9, seed: 26101);
        // (3,4) dispenser (E/W, boarded walking south) -> (4,4) curve E->SE -> (5,3) diagonal -> (6,2) curve SE->E -> (7,2) dispenser
        Place<obj_structure_transit_tube_station_dispenser>(r, 3, 4, (int)NORTH);
        Place<obj_structure_transit_tube_curved>(r, 4, 4, (int)WEST);
        Place<obj_structure_transit_tube_diagonal>(r, 5, 3, (int)EAST);
        Place<obj_structure_transit_tube_curved>(r, 6, 2, (int)EAST);
        Place<obj_structure_transit_tube_station_dispenser>(r, 7, 2, (int)NORTH);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.Step(Second);

        r.Command(() => ClientSeam.SetHeldDirections(bob.client, (int)SOUTH));
        r.Step(4);
        r.Command(() => ClientSeam.SetHeldDirections(bob.client, 0));
        r.Step(2);
        Assert.True(r.In(() => bob.loc is obj_structure_transit_tube_pod_dispensed), "the dispenser makes a pod around the player");

        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.Same(locate(7, 2, 1), bob.loc);
            Assert.Empty(Enumerable.Range(1, 12).SelectMany(x => Enumerable.Range(1, 9).SelectMany(y => Dm.each_cast<atom>(locate(x, y, 1).contents))).OfType<obj_structure_transit_tube_pod>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStationPodShuttlesToTheTerminusAndIsTurnedAround()
    {
        var r = new AudioRoom(12, 9, seed: 26102);
        Place<obj_structure_transit_tube_station_reverse>(r, 3, 4, (int)NORTH); // tube_dirs EAST
        for (int x = 4; x <= 6; x++)
            Place<obj_structure_transit_tube>(r, x, 4, (int)EAST);
        Place<obj_structure_transit_tube_station_reverse>(r, 7, 4, (int)SOUTH); // tube_dirs WEST
        var pod = r.In(() => Dm.@new<obj_structure_transit_tube_pod>(locate(3, 4, 1)));
        r.In(() => Waiting.NoSleep(pod.setDir(EAST)));

        // from SSobj's first fire the station closes (CLOSE_DURATION + 2), the pod rides four tiles and stops; the terminus turns it west for the way back
        StepToFirstSobjFire(r);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Same(locate(7, 4, 1), pod.loc);
            Assert.Equal((float)WEST, Dm.num(pod.dir));
            Assert.False(Dm.truthy(pod.moving));
        });
        // after the terminus' cooldown (5 seconds) it is launched back
        r.Step(8 * Second);
        Assert.Same(r.In(() => locate(3, 4, 1)), r.In(() => pod.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARidersCapsuleGlidesWithThePodNeverJumping()
    {
        // inside the pod the rider's capsule stands on the pod's turf and slides with it over the pod's glide time, so the client's
        // predictor replays a slide, not a jump of a tile at each step (the owner, 2026-10-27: mispredictions while riding)
        var r = new AudioRoom(12, 9, seed: 26104);
        LocomotionSeam.Install(r.World);
        Place<obj_structure_transit_tube_station_reverse>(r, 3, 4, (int)NORTH);
        for (int x = 4; x <= 6; x++)
            Place<obj_structure_transit_tube>(r, x, 4, (int)EAST);
        Place<obj_structure_transit_tube_station_reverse>(r, 7, 4, (int)SOUTH);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        var pod = r.In(() => Dm.@new<obj_structure_transit_tube_pod>(locate(3, 4, 1)));
        r.In(() =>
        {
            LocomotionSeam.Register(bob);
            Waiting.NoSleep(bob.forceMove(pod));
            Waiting.NoSleep(pod.setDir(EAST));
        });

        MotionState State() => r.In(() => { LocomotionSeam.TryGetState(World.Current, bob.dm_id, out MotionState s, out _); return s; });
        StepToFirstSobjFire(r); // the station launches the pod at its process; the ride from there is the same whatever the stagger
        int slides = 0, jumps = 0;
        MotionState last = State();
        for (int t = 0; t < 2 * Second; t++)
        {
            r.Step(1);
            MotionState now = State();
            if (now.Slide.Active) slides++;
            if (!now.Slide.Active && !last.Slide.Active && (now.X != last.X || now.Y != last.Y)) jumps++;
            last = now;
        }
        Assert.Same(r.In(() => locate(7, 4, 1)), r.In(() => pod.loc));
        Assert.Equal((7, 4), (last.TileX, last.TileY));
        Assert.True(slides > 0, $"the rider glides with the pod (slides {slides}, jumps {jumps})");
        Assert.Equal(0, jumps);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnattachedTubeIsWrenchedIntoATube()
    {
        var r = new AudioRoom(12, 9, seed: 26103);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var husk = r.In(() => Dm.@new<obj_structure_c_transit_tube_curved>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(husk.setDir(EAST)));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(Dm.@new<obj_item_wrench>(get_turf(bob)))));
        r.Command(() => ClientSeam.Click(bob.client, husk, "left=1;button=left;icon-x=16;icon-y=16"));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(husk));
            obj_structure_transit_tube tube = Dm.locate<obj_structure_transit_tube_curved>(locate(5, 4, 1));
            Assert.NotNull(tube);
            Assert.Equal((float)EAST, Dm.num(tube.dir));
            Assert.Equal(new[] { (float)EAST, (float)NORTHWEST }, Dm.each(tube.tube_dirs).Select(d => Dm.num(d)).ToArray());
        });
        r.AssertNoRuntimes();
    }
}
