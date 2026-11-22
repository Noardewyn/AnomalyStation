using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.MultiZ;

/// <summary>
/// D325: tg's stairs (stairs.dm) and ladders (ladders.dm) on the multi-z fixture (tests/fixtures/maps/t2/multiz.dmm: two linked
/// levels, a 3 x 3 room on each from (2, 2) to (4, 4), open space at (3, 3, 2) over the middle of the lower room).
/// </summary>
public class StairsAndLaddersTests
{
    private static World Boot(out TgTestWorld.Log log) => T2Maps.Boot("multiz", out log);

    [Fact]
    public void WalkingOffTheTopStairInItsDirectionClimbsToTheLevelAbove()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            var stairs = Dm.@new<obj_structure_stairs_north>(locate(3, 3, 1));
            Assert.Equal(TRUE, Dm.num(stairs.isTerminator()));
            Assert.Equal("stairs", stairs.icon_state);
            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(3, 2, 1));
            world.Step(2);
            float before = Dm.num(bob.get_brute_loss());

            Assert.True(Dm.truthy(Waiting.NoSleep(bob.Move(locate(3, 3, 1), NORTH))));
            Assert.Same(locate(3, 3, 1), bob.loc);
            Waiting.NoSleep(bob.Move(locate(3, 4, 1), NORTH)); // on_exit_stairs blocks the step and climbs
            world.Step(2);
            Assert.Same(locate(3, 4, 2), bob.loc);
            Assert.Equal(FALSE, Dm.num(bob.currently_z_moving));
            Assert.Equal(before, Dm.num(bob.get_brute_loss()));
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void TheTopStairCannotBeEnteredFromItsHighSideAndStepsMergeTheirSprites()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            var stairs = Dm.@new<obj_structure_stairs_north>(locate(3, 3, 1));
            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(3, 4, 1));
            world.Step(2);
            Waiting.NoSleep(bob.Move(locate(3, 3, 1), SOUTH));
            Assert.Same(locate(3, 4, 1), bob.loc); // Cross refuses a mover on the stairs' dir side

            // a second step to the east: the west one is the left end (-l), the east one the right end (-r)
            var east = Dm.@new<obj_structure_stairs_north>(locate(4, 3, 1));
            Assert.Equal("stairs-l", stairs.icon_state);
            Assert.Equal("stairs-r", east.icon_state);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void FallingOntoTheTopStairIsInterceptedAndAnUnconsciousFallerTumblesDown()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            Dm.@new<obj_structure_stairs_north>(locate(3, 3, 1));
            mob_living_carbon_human awake = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            world.Step(2);
            float before = Dm.num(awake.get_brute_loss());
            Waiting.NoSleep(awake.Move(locate(3, 3, 2), EAST));
            Assert.Same(locate(3, 3, 1), awake.loc);
            world.Step(OpenSpaceTests.LandingTicks); // DV435: the stairs take it as it reaches them
            Assert.Equal(before, Dm.num(awake.get_brute_loss())); // FALL_INTERCEPTED: no ZImpactDamage
            Assert.Equal(0f, Dm.num(awake.AmountKnockdown()));

            mob_living_carbon_human sleeper = Dm.@new<mob_living_carbon_human>(locate(2, 3, 2));
            world.Step(2);
            Waiting.NoSleep(sleeper.Sleeping(100));
            float sleeperBefore = Dm.num(sleeper.get_brute_loss());
            Waiting.NoSleep(sleeper.forceMove(locate(3, 3, 2)));
            Assert.Equal(1f, sleeper.z);
            world.Step(OpenSpaceTests.LandingTicks);
            // on_fall: rand(4, 8) brute spread over the body, a 5 second knockdown
            Assert.InRange(Dm.num(sleeper.get_brute_loss()) - sleeperBefore, 3.5f, 8.5f);
            Assert.InRange(Dm.num(sleeper.AmountKnockdown()), 4 * SECONDS, 5 * SECONDS);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void LaddersLinkVerticallyAndAClimbTakesASecondToTheOtherLevel()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            var bottom = Dm.@new<obj_structure_ladder>(locate(2, 2, 1));
            var top = Dm.@new<obj_structure_ladder>(locate(2, 2, 2));
            Assert.Same(top, bottom.up);
            Assert.Same(bottom, top.down);
            Assert.Equal("ladder10", bottom.icon_state);
            Assert.Equal("ladder01", top.icon_state);
            // the top ladder opens a hole in the floor (ladder_hole: the turf's transparency)
            Assert.True(istransparentturf(locate(2, 2, 2)));
            Assert.Equal(12f, top.pixel_z);

            mob_living_carbon_human bob = Dm.@new<mob_living_carbon_human>(locate(2, 3, 1));
            world.Step(2);
            Waiting.NoSleep(bottom.use(bob, going_up: TRUE));
            Assert.Same(locate(2, 3, 1), bob.loc); // the climb is a 1 second do_after
            world.Step(25);
            Assert.Same(locate(2, 2, 2), bob.loc);

            // down again (tg's right click)
            Waiting.NoSleep(top.use(bob, going_up: FALSE));
            world.Step(25);
            Assert.Same(locate(2, 2, 1), bob.loc);

            // a removed ladder unlinks and closes the hole
            qdel(top);
            Assert.Null(bottom.up);
            Assert.Equal("ladder00", bottom.icon_state);
            Assert.False(istransparentturf(locate(2, 2, 2)));
            Assert.Empty(log.Runtimes);
        }
    }

    // the owner (2026-11-14): a ladder linked to nothing (every ladder of a single-level station) lies flat and has no rails (DV397)
    [Fact]
    public void OnlyALinkedLadderHasRails()
    {
        World world = Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            var lone = Dm.@new<obj_structure_ladder>(locate(4, 4, 1));
            Assert.Equal(Anomaly.Core.Motion.LadderBody.LyingState, lone.icon_state);
            Assert.False(LocomotionSeam.LadderAt(world, 4, 4, 1));
            var bottom = Dm.@new<obj_structure_ladder>(locate(2, 2, 1));
            Dm.@new<obj_structure_ladder>(locate(2, 2, 2));
            Assert.True(LocomotionSeam.LadderAt(world, 2, 2, 1));
            Assert.True(LocomotionSeam.LadderAt(world, 2, 2, 2));
            Assert.Empty(log.Runtimes);
        }
    }
}
