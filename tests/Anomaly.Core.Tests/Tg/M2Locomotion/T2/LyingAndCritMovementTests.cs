using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// The capsule gate and its preview against tg's rules for a mob on the floor and in crit (the owner's play test,
/// 2026-10-13): set_stat's STAT_TRAIT (living.dm:2514-2573: soft crit floors, hard crit and death also knock out, and the
/// knocked_out effect immobilizes, debuffs.dm:153-156), Knockdown floors only (debuffs.dm:72-76), the slip's knockdown
/// (_open.dm:540-596), resting and get_up's one second do_after (living.dm:711-726), crawling (CRAWLING_ADD_SLOWDOWN,
/// carbon.dm:416-422) and soft crit's own slowdown (SOFTCRIT_ADD_SLOWDOWN, carbon.dm:444-447), client/Move's move_delay test
/// with Process_Grab and resist_grab's cooldowns (mob_movement.dm:47, :152-164, living.dm:1183-1251), and who may turn
/// (face_atom, click.dm:426).
/// </summary>
public class LyingAndCritMovementTests
{
    private const int Second = 20; // ticks per second
    private static readonly ushort East = T2Room.Yaw(EAST);
    private static readonly ushort West = T2Room.Yaw(WEST);

    private static float Slowdown(T2Room r, mob_living who) => Dm.num(r.In(() => who.cached_multiplicative_slowdown));

    private static int DirOf(T2Room r, mob_living who) => (int)Dm.num(r.In(() => who.dir));

    // The capsule's distance in subunits over the ticks at a slowdown (0.5 / slowdown tiles a tick).
    private static double PerTick(float slowdown) => T2Room.C * 0.5 / slowdown;

    [Fact]
    public void AHardCritHumanCannotMoveAndItsBodyKeepsItsDir()
    {
        var r = new T2Room(1701);
        mob_living_carbon_human m = r.Join(4, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        r.Command(() => Waiting.NoSleep(m.adjust_brute_loss(165))); // health -37.5: below HEALTH_THRESHOLD_FULLCRIT
        r.Step();
        Assert.Equal(HARD_CRIT, Dm.num(r.In(() => m.stat)));
        Assert.True(r.In(() => HAS_TRAIT(m, TRAIT_IMMOBILIZED)), "hard crit knocks out, knocked_out immobilizes");
        Assert.Equal(Posture.Lying, r.State(m).Posture);
        Assert.False(r.Params(m).CanMove);
        int fell = DirOf(r, m);
        Assert.True(fell == NORTH || fell == SOUTH, "a floored mob facing east or west turns north or south (on_lying_down, living.dm:747)");

        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 10);
        Assert.Equal(x, r.State(m).X);
        Assert.False(r.Params(m).CanMove);
        r.Go(m, -127, 0, West, 5); // looking and pushing the other way neither moves nor turns the body
        Assert.Equal(x, r.State(m).X);
        Assert.Equal(fell, DirOf(r, m));
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASoftCritHumanCrawlsAtTgsCritCrawlSpeedAndTurnsOnlyWhileCrawling()
    {
        var r = new T2Room(1702);
        mob_living_carbon_human m = r.Join(4, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        r.Command(() => Waiting.NoSleep(m.adjust_brute_loss(125))); // health -4.2: soft crit
        r.Step();
        Assert.Equal(SOFT_CRIT, Dm.num(r.In(() => m.stat)));
        Assert.False(r.In(() => HAS_TRAIT(m, TRAIT_IMMOBILIZED)), "soft crit floors and incapacitates, it does not immobilize");
        Assert.True(r.In(() => HAS_TRAIT(m, TRAIT_FLOORED)));
        Assert.Equal(LYING_DOWN, Dm.num(r.In(() => m.body_position)));
        Assert.True(r.Params(m).CanMove);
        float health = Dm.num(r.In(() => m.health));
        float expected = T2Room.Run + 4f + 2f + (100f - health) / 75f; // crawling, soft crit, the damage slowdown
        Assert.Equal(expected, Slowdown(r, m), 3);

        // looking around in crit does not turn the body (face_atom refuses IS_UNCONSCIOUS_OR_CRIT); it fell facing north or
        // south (on_lying_down, living.dm:747)
        int fell = DirOf(r, m);
        Assert.True(fell == NORTH || fell == SOUTH);
        r.Go(m, 0, 0, West, 3);
        Assert.Equal(fell, DirOf(r, m));

        // crawling does (tg's Move turns the mob), at the crit crawl's speed
        int x = r.State(m).X;
        r.Go(m, -127, 0, West, 6);
        Assert.Equal((int)WEST, DirOf(r, m));
        Assert.InRange(x - r.State(m).X, 6 * (PerTick(expected) - 1), 6 * PerTick(expected) + 1); // a step is whole subunits
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanSlippedOnWaterIsKnockedDownForSixSecondsCrawlsMeanwhileAndGetsUpASecondAfter()
    {
        var r = new T2Room(1703);
        mob_living_carbon_human m = r.Join(4, 8, "Alice", name: "mover");
        r.In(() => Waiting.NoSleep(((turf_open)locate(6, 8, 1)).MakeSlippery(TURF_WET_WATER, 100)));
        r.Go(m, 0, 0, East, 2);
        int fellAt = -1;
        for (int i = 0; i < 2 * Second && fellAt < 0; i++)
        {
            r.Go(m, 127, 0, East);
            if (r.In(() => m.body_position == LYING_DOWN))
                fellAt = i;
        }
        Assert.True(fellAt >= 0, "the running human slipped");
        // water: intensity 60 is a 6 second knockdown and no paralysis (wet_floor.dm:90-92, _open.dm:586-587)
        Assert.True(r.In(() => HAS_TRAIT(m, TRAIT_FLOORED)));
        Assert.False(r.In(() => HAS_TRAIT(m, TRAIT_IMMOBILIZED)), "a water slip only knocks down");
        Assert.True(r.Params(m).CanMove, "knocked down, tg lets the mob crawl");
        Assert.Equal(T2Room.Run + 4f, Slowdown(r, m));
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 5);
        Assert.InRange(r.State(m).X - x, 5 * PerTick(T2Room.Run + 4f) - 2, 5 * PerTick(T2Room.Run + 4f) + 2);

        // the knockdown ends about 120 ticks after the slip (the status effect's processing step); get_up's do_after takes 20 more
        int stoodAt = -1;
        for (int i = 6; i < 9 * Second && stoodAt < 0; i++)
        {
            r.Go(m, 0, 0, East);
            if (r.In(() => m.body_position == STANDING_UP))
                stoodAt = i;
        }
        Assert.InRange(stoodAt, 7 * Second - 5, 7 * Second + 5);
        Assert.Equal(T2Room.Run, Slowdown(r, m));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARestingHumanCrawlsAndStandsUpOnlyAfterTgsOneSecondGetUpCrawlingMeanwhile()
    {
        var r = new T2Room(1704);
        mob_living_carbon_human m = r.Join(4, 8, "Alice", name: "mover");
        r.Command(() => ClientSeam.Rest(m.client));
        r.Step(2);
        Assert.Equal(LYING_DOWN, Dm.num(r.In(() => m.body_position)));
        Assert.True(r.Params(m).CanMove);
        r.Go(m, 127, 0, East, 4);

        r.Command(() => ClientSeam.Rest(m.client)); // stand up: get_up(), a 1 second do_after that moving does not break
        r.Step();
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, Second - 3);
        Assert.Equal(LYING_DOWN, Dm.num(r.In(() => m.body_position)));
        Assert.True(r.State(m).X > x, "the mob crawls while it gets up");
        r.Go(m, 127, 0, East, 4);
        Assert.Equal(STANDING_UP, Dm.num(r.In(() => m.body_position)));
        Assert.Equal(T2Room.Run, Slowdown(r, m));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadHumanCannotMoveCannotTurnAndThePreviewSaysSo()
    {
        var r = new T2Room(1705);
        mob_living_carbon_human m = r.Join(5, 8, "Alice", name: "mover");
        r.Go(m, 127, 0, East, 1);
        r.Command(() => Waiting.NoSleep(m.death()));
        r.Step();
        Assert.Equal(DEAD, Dm.num(r.In(() => m.stat)));
        Assert.False(r.Params(m).CanMove);
        int fell = DirOf(r, m);
        int x = r.State(m).X;
        r.Go(m, -127, 0, West, 6);
        Assert.Equal(x, r.State(m).X);
        Assert.Equal(fell, DirOf(r, m));
        Assert.False(r.Params(m).CanMove);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAggressivelyGrabbedHumanResistsOnceAMoveAndThenWaitsTgsFourSeconds()
    {
        // Process_Grab -> resist_grab(TRUE): GRAB_AGGRESSIVE escapes with BASE_GRAB_RESIST_CHANCE 60 %; a failure costs
        // stamina and pushes client.move_delay 4 seconds, and client/Move's first test holds every move until then.
        bool sawAFailure = false;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var r = new T2Room(1710 + seed);
            mob_living_carbon_human b = r.Dummy(4, 8, name: "grabber");
            mob_living_carbon_human m = r.Join(5, 8, "Alice", name: "grabbed");
            r.Go(m, 0, 0, East, 2);
            int rolls = 0;
            r.In(() =>
            {
                Waiting.NoSleep(b.start_pulling(m));
                Waiting.NoSleep(b.setGrabState(GRAB_AGGRESSIVE));
                On(b, COMSIG_MOVABLE_GRABBED_RESISTING, "resisting", 1, act: _ => rolls++);
            });
            r.Go(m, 0, 0, East, 1);
            Assert.Same(b, r.In(() => m.pulledby));
            Assert.False(r.Params(m).CanMove, "an aggressive grab previews as held");

            int x = r.State(m).X;
            r.Go(m, 127, 0, East, 1);
            Assert.Equal(1, rolls);
            if (r.In(() => m.pulledby) == null)
            {
                r.Go(m, 127, 0, East, 3);
                Assert.True(r.State(m).X > x, "broke free and moved");
                r.AssertNoRuntimes();
                continue;
            }
            sawAFailure = true;
            Assert.False(r.Params(m).CanMove);
            r.Go(m, 127, 0, East, 4 * Second - 2);
            Assert.Equal(1, rolls); // no roll during the 4 seconds
            Assert.Equal(x, r.State(m).X);
            r.Go(m, 127, 0, East, 3);
            Assert.True(rolls >= 2, "the next move after 4 seconds resists again");
            r.AssertNoRuntimes();
        }
        Assert.True(sawAFailure, "no seed failed its first resist roll");
    }

    [Fact]
    public void WhenTheGateRefusesTheHeldWishThePredictorIsToldNoAndTheIdlePreviewStaysYes()
    {
        // a COMSIG_MOB_CLIENT_PRE_LIVING_MOVE blocker (as a slip's force_move registers) refuses each held move; the preview
        // sends no signal, so the private block follows the gate of the tick
        var r = new T2Room(1720);
        mob_living_carbon_human m = r.Join(5, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        r.In(() => On(m, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, "blocker", 2, result: COMSIG_MOB_CLIENT_BLOCK_PRE_LIVING_MOVE));
        Assert.True(r.Params(m).CanMove);
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 3);
        Assert.Equal(x, r.State(m).X);
        Assert.False(r.Params(m).CanMove);
        r.Go(m, 0, 0, East, 1);
        Assert.True(r.Params(m).CanMove);
        r.AssertNoRuntimes();
    }
}
