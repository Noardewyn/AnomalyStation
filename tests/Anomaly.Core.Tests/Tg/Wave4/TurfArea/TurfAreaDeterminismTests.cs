using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// Determinism (translation-rules.md 15): the same scenario twice gives the same World.ComputeStateHash(), and the hash
/// sees what the package keeps (areas' lists, the subsystem's lists, baseturfs, overlay state, the random stream).
/// </summary>
public class TurfAreaDeterminismTests
{
    // Every proc family of the package once: the map build, SSatoms, Enter, change_area round trips, SSarea_contents in many
    // calls, update_appearance and necropolis floors (seeded random draws in Initialize).
    private static ulong Scenario(int variant = 0, ulong seed = 17)
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_indestructible_necropolis), areaType: typeof(area_space), seed: seed);
        using (t.World.Enter())
        {
            Dm.world.maxx = 8;
            Dm.world.maxy = 6;
            for (int y = 1; y <= 6; y++)
            {
                for (int x = 1; x <= 8; x++)
                {
                    if (x == 1 || y == 1 || x == 8 || y == 6)
                        Dm.@new<turf_closed_indestructible>(locate(x, y, 1));
                }
            }
            var space = (area)locate(1, 1, 1).loc;
            space.turfs_by_zlevel.len = 1;
            space.turfs_by_zlevel[1f] = block(1, 1, 1, 8, 6, 1);
            var room = Dm.@new<area_misc_testroom>();
            foreach (turf T in Dm.each<turf>(block(2, 2, 1, 7, 5, 1)))
                T.change_area(space, room);

            var mover = Dm.@new<obj_test_turfarea_probe>(locate(2, 2, 1));
            var blocker = Dm.@new<obj_test_turfarea_occupant>(locate(3, 2, 1));
            blocker.cross_result = 0;
            var thing = Dm.@new<obj_test_turfarea_appearance>(locate(4, 2, 1));
            var sitter = Dm.@new<obj_test_turfarea_sitter>(locate(5, 2, 1));

            TurfAreaWorld.InitializeSSatoms();      // seeded draws: every necropolis floor rolls prob(12)

            Waiting.NoSleep(locate(3, 2, 1).Enter(mover, locate(2, 2, 1)));
            Waiting.NoSleep(locate(1, 2, 1).Enter(mover, locate(2, 2, 1)));
            thing.update_appearance();

            turf bouncer = locate(4, 3, 1);
            for (int i = 0; i < 101 + variant; i++)
            {
                bouncer.change_area(room, space);
                bouncer.change_area(space, room);
            }

            var ss = Dm.@new<datum_controller_subsystem_area_contents>();
            ss.state = SS_PAUSED;
            Waiting.NoSleep(ss.fire(FALSE));
            for (int i = 0; i < 40; i++)
            {
                ss.state = SS_PAUSED;
                Waiting.NoSleep(ss.fire(TRUE));
            }
            Assert.Empty(t.Runtimes);
            return t.World.ComputeStateHash();
        }
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameStateHash()
    {
        Assert.Equal(Scenario(), Scenario());
    }

    [Fact]
    public void TheHashDependsOnTheSeedBecauseNecropolisFloorsRollTheirIconsFromIt()
    {
        Assert.NotEqual(Scenario(seed: 17), Scenario(seed: 18));
    }

    [Fact]
    public void TheHashSeesAnExtraAreaRoundTripThatLeavesTheMapAsItWas()
    {
        // the extra round trip leaves every turf where it was, but the area lists and loose lists differ
        Assert.NotEqual(Scenario(variant: 0), Scenario(variant: 1));
    }

    [Fact]
    public void SSareaContentsStatesAreHashedSoADifferentNumberOfFireCallsDiffers()
    {
        static ulong Run(int calls)
        {
            var t = new TurfAreaWorld(11, 11, 1, areaType: typeof(area_space), seed: 4);
            using (t.World.Enter())
            {
                var space = (area)locate(1, 1, 1).loc;
                space.turfs_by_zlevel.len = 1;
                space.turfs_by_zlevel[1f] = block(1, 1, 1, 11, 11, 1);
                var room = Dm.@new<area_misc_testroom>();
                foreach (turf T in Dm.each<turf>(block(1, 1, 1, 11, 11, 1)))
                    T.change_area(space, room);
                var ss = Dm.@new<datum_controller_subsystem_area_contents>();
                for (int i = 0; i < calls; i++)
                {
                    ss.state = SS_PAUSED;
                    Waiting.NoSleep(ss.fire(i > 0 ? TRUE : FALSE));
                }
                return t.World.ComputeStateHash();
            }
        }

        Assert.Equal(Run(30), Run(30));
        Assert.NotEqual(Run(30), Run(31));
    }
}
