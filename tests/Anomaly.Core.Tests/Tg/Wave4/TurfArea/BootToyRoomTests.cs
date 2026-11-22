using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// The toy room of scope.md section 7 item 1 as wave 5's toy runner builds it: in the map step of tg's boot
/// (TgWorld.Boot's compiledMap), before world/New, so that every new atom waits for SSatoms, which the real Master
/// initializes at tick 20; then SSarea_contents fires under the real Master.
/// </summary>
public class BootToyRoomTests
{
    private const int W = 6, H = 5, D = 2;

    private static void BuildToyRoom(World world)
    {
        Dm.world.turf = typeof(turf_open_indestructible);
        Dm.world.area = typeof(area_space);
        Dm.world.maxx = W;
        Dm.world.maxy = H;
        Dm.world.maxz = D;
        SSmapping.gravity_by_z_level.len = D;
        ToyRoom.LinkLevels(D);
        var space = (area)GLOB.areas_by_type[typeof(area_space)];
        for (int z = 1; z <= D; z++)
        {
            for (int y = 1; y <= H; y++)
            {
                for (int x = 1; x <= W; x++)
                {
                    if (x == 1 || y == 1 || x == W || y == H)
                        Dm.@new<turf_closed_indestructible>(locate(x, y, z));
                }
            }
        }
        space.turfs_by_zlevel.len = D;
        for (int z = 1; z <= D; z++)
            space.turfs_by_zlevel[(float)z] = block(1, 1, z, W, H, z);
        var room = Dm.@new<area_misc_testroom>();
        for (int z = 1; z <= D; z++)
        {
            foreach (turf T in Dm.each<turf>(block(2, 2, z, W - 1, H - 1, z)))
                T.change_area((area)T.loc, room);
        }
    }

    private static World Boot(out TgTestWorld.Log log, ulong seed = 3)
    {
        World w = TgTestWorld.Start(out log, seed, compiledMap: BuildToyRoom);
        w.Step(30);   // Master.Initialize sleeps 10 ds, then initializes every stage at tick 20
        Assert.True(TgTestWorld.Initialized(w));
        return w;
    }

    [Fact]
    public void TheRealMastersSSatomsInitializesTheToyRoomOnce()
    {
        World w = Boot(out TgTestWorld.Log log);
        using (w.Enter())
        {
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, W, H, D)))
                Assert.True(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)), T.GetType().Name + " at " + T.x + "," + T.y + "," + T.z);
            var room = (area)GLOB.areas_by_type[typeof(area_misc_testroom)];
            Assert.True(Dm.truthy(Dm.bitand(room.flags_1, INITIALIZED_1)));
            Assert.Equal(4 * 3 * D, room.areasize);   // the 4 x 3 interior of each level
        }
        Assert.Empty(log.Runtimes);
    }

    [Fact]
    public void SSareaContentsTrimsTheLooseTurfsOfTheToyRoomUnderTheRealMaster()
    {
        World w = Boot(out TgTestWorld.Log log);
        area space, room;
        turf bouncer;
        using (w.Enter())
        {
            space = (area)GLOB.areas_by_type[typeof(area_space)];
            room = (area)GLOB.areas_by_type[typeof(area_misc_testroom)];
            bouncer = locate(3, 3, 1);
            // a turf that bounces between the two areas more than ALLOWED_LOOSE_TURFS (100) times
            for (int i = 0; i < 101; i++)
            {
                bouncer.change_area(room, space);
                bouncer.change_area(space, room);
            }
            Assert.True(((DmList)space.turfs_to_uncontain_by_zlevel[1f]).len > 100);
            Assert.True(((DmList)room.turfs_to_uncontain_by_zlevel[1f]).len > 100);
        }

        w.Step(120);

        using (w.Enter())
        {
            Assert.True(SSarea_contents.times_fired > 0);
            Assert.Same(room, bouncer.loc);
            // both areas were over the limit: SSarea_contents cleaned them down, so nothing is left to uncontain
            Assert.Equal(0, space.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, room.turfs_to_uncontain_by_zlevel.len);
            Assert.Equal(0, SSarea_contents.marked_for_clearing.len);
            foreach (area a in new[] { space, room })
                a.cannonize_contained_turfs();
            Assert.Equal(1, TurfAreaLists.Of<turf>(room.turfs_by_zlevel[1f]).Count(T => T == bouncer));
            Assert.Equal(0, TurfAreaLists.Of<turf>(space.turfs_by_zlevel[1f]).Count(T => T == bouncer));
        }
        Assert.Empty(log.Runtimes);
    }

    [Fact]
    public void TheSameBootTwiceGivesTheSameStateHash()
    {
        World a = Boot(out _, seed: 9);
        World b = Boot(out _, seed: 9);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        a.Step(60);
        b.Step(60);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }
}
