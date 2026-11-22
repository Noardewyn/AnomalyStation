using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// The bridge on real bodies, with tg's own movespeed modifiers and keybindings (no slowdown written by the test): the walk/run
/// toggle, damage (human.dm:960-966: from 40 points of deficiency, deficiency / 75), the crawl (CRAWLING_ADD_SLOWDOWN 4, the lying
/// angle by the direction of each Move, living.dm:1084-1092), the shove's stagger (STAGGERED_SLOWDOWN_STRENGTH 0.85).
/// The speeds are those of the scope 2c table.
/// </summary>
public class RealBodyLocomotionTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);
    private const int C = T2Room.C;

    private static float Slowdown(T2Room r, mob_living who) => Dm.num(r.In(() => who.cached_multiplicative_slowdown));

    [Fact]
    public void ARunningHumanHasTgsRunDelayAndTheToggleKeyMakesItWalkAndBack()
    {
        var r = new T2Room(1501);
        mob_living_carbon_human m = r.Join(2, 8, "Alice", name: "mover");
        Assert.Equal(T2Room.Run, Slowdown(r, m));
        Assert.Equal(MOVE_INTENT_RUN, r.In(() => m.move_intent));
        Assert.Equal(0.5f / 1.5f, r.Params(m).TilesPerTick, 6);

        r.Command(() => ClientSeam.ToggleMoveIntent(m.client));
        r.Step();
        Assert.Equal(MOVE_INTENT_WALK, r.In(() => m.move_intent));
        Assert.Equal(T2Room.Walk, Slowdown(r, m));
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 3);
        Assert.Equal(3 * 32, r.State(m).X - x); // WALK_DELAY 4 ds: 2.5 tiles a second
        Assert.Equal(8, r.Params(m).MoveDelayTicks);

        r.Command(() => ClientSeam.ToggleMoveIntent(m.client));
        r.Step();
        Assert.Equal(MOVE_INTENT_RUN, r.In(() => m.move_intent));
        x = r.State(m).X;
        r.Go(m, 127, 0, East, 3);
        Assert.Equal(85 * 3, r.State(m).X - x);
        r.AssertNoRuntimes();
    }

    // adjust_brute_loss spreads the damage evenly over the six limbs and updatehealth counts each limb by its body_damage_coeff
    // (1 for the chest and the head, 0.75 for the arms and the legs): the health deficiency of b brute is b * (2 + 4 * 0.75) / 6.
    [Theory]
    [InlineData(48, 1.5 + 40 / 75.0)]   // deficiency 40: the first point of the slowdown (the 2.03 of the speed table)
    [InlineData(72, 1.5 + 60 / 75.0)]
    [InlineData(96, 1.5 + 80 / 75.0)]
    [InlineData(47, 1.5)]               // deficiency 39.2: none
    public void DamageSlowsTheCapsuleAsHumanUpdateHealthSays(int brute, double expectedSlowdown)
    {
        var r = new T2Room(1502);
        mob_living_carbon_human m = r.Join(2, 8, "Alice", name: "mover");
        r.Command(() => Waiting.NoSleep(m.adjust_brute_loss(brute)));
        r.Step();
        Assert.Equal((float)expectedSlowdown, Slowdown(r, m), 3);
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 4);
        double perTick = (r.State(m).X - x) / 4.0;
        Assert.InRange(perTick, 256 * 0.5 / expectedSlowdown - 1, 256 * 0.5 / expectedSlowdown + 1);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RestingLaysTheCapsuleDownAndCrawlingIsFourDecisecondsSlowerTurningTheBodyByTheDirection()
    {
        var r = new T2Room(1503);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.Command(() => ClientSeam.Rest(m.client));
        r.Step(2);
        Assert.Equal(LYING_DOWN, Dm.num(r.In(() => m.body_position)));
        MotionState s = r.State(m);
        Assert.Equal((Posture.Lying, MotionDriver.Prone), (s.Posture, s.Driver));
        Assert.Equal(T2Room.Run + 4f, Slowdown(r, m)); // CRAWLING_ADD_SLOWDOWN
        Assert.Equal(0.5f / 5.5f, r.Params(m).TilesPerTick, 5);

        // east: the lying angle is 90, west: 270 (lying_angle_on_movement, called by the Move of each crossing)
        r.Go(m, 127, 0, East, 12);
        Assert.True(r.LocOf(m).X > 8, "the crawl did not cross a border");
        Assert.Equal(90f, Dm.num(r.In(() => m.lying_angle)));
        r.Go(m, -127, 0, East, 30);
        Assert.Equal(270f, Dm.num(r.In(() => m.lying_angle)));
        r.AssertOnLoc(m);
        // a crawl is a Move like any other: the capsule's speed is the crawl's
        int x = r.State(m).X;
        r.Go(m, -127, 0, East, 5);
        Assert.InRange(x - r.State(m).X, 5 * 23 - 2, 5 * 23 + 2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShovedHumanIsStaggeredAndSlowerForThreeSeconds()
    {
        var r = new T2Room(1504);
        mob_living_carbon_human b = r.Dummy(9, 8, name: "target");
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "shover");
        r.Go(a, 0, 0, East, 2);
        r.Command(() => ClientSeam.Click(a.client, b, ClientSeam.ClickParams(RIGHT_CLICK)));
        r.Step();
        Assert.Equal(T2Room.Run + 0.85f, Slowdown(r, b), 3); // STAGGERED_SLOWDOWN_STRENGTH
        // STAGGERED_SLOWDOWN_LENGTH 3 s = 60 ticks, less up to one SSpriority_effects wait (0.2 s = 4 ticks): each fire takes a
        // whole wait off the duration (_status_effect.dm:108), the first one too, whatever the subsystem's phase
        r.Step(55);
        Assert.Equal(T2Room.Run + 0.85f, Slowdown(r, b), 3);
        r.Step(9);
        Assert.Equal(T2Room.Run, Slowdown(r, b), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AKnockedDownHumanCrawlsAndAParalyzedOneCannotMove()
    {
        // tg: knocked down (TRAIT_FLOORED) a mob lies and keeps MOBILITY_MOVE: it crawls, 4 ds slower; paralyzed (immobilized and
        // incapacitated) it has no MOBILITY_MOVE and the gate says no
        var r = new T2Room(1505);
        mob_living_carbon_human m = r.Join(5, 8, "Alice", name: "mover");
        r.Go(m, 127, 0, East, 2);
        r.Command(() => Waiting.NoSleep(m.Knockdown(40)));
        r.Go(m, 127, 0, East, 1);
        Assert.Equal(Posture.Lying, r.State(m).Posture);
        Assert.Equal(T2Room.Run + 4f, Slowdown(r, m));
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 5);
        Assert.InRange(r.State(m).X - x, 5 * 23 - 2, 5 * 23 + 2);

        r.Command(() => Waiting.NoSleep(m.Paralyze(40)));
        r.Go(m, 127, 0, East, 1);
        x = r.State(m).X;
        r.Go(m, 127, 0, East, 10);
        Assert.Equal(x, r.State(m).X);
        Assert.False(r.Params(m).CanMove);
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadHumanStopsAndTheSeamKeepsItsCapsuleOnItsTile()
    {
        var r = new T2Room(1506);
        mob_living_carbon_human m = r.Join(5, 8, "Alice", name: "mover");
        r.Go(m, 127, 0, East, 1);
        r.Command(() => Waiting.NoSleep(m.death()));
        r.Step();
        Assert.Equal(DEAD, Dm.num(r.In(() => m.stat)));
        int x = r.State(m).X;
        r.Go(m, 127, 0, East, 6);
        Assert.Equal(x, r.State(m).X);
        Assert.Equal(Posture.Lying, r.State(m).Posture);
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }
}
