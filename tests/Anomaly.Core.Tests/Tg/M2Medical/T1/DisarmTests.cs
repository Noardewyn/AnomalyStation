using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// The shove, written from tg's DM: <c>/mob/living/proc/disarm</c> (living_defense.dm:802-887), <c>can_disarm</c> (:889),
/// <c>get_shove_flags</c> of carbons (carbon_defense.dm:665), the human's <c>disarm_precollide</c> and <c>disarm_collision</c>
/// (human_defense.dm:126-145), the species' <c>spec_attack_hand</c> (_species.dm:996) and tg's unit test
/// <c>/datum/unit_test/disarm</c> (combat.dm:72). The room is 12 by 12: the floor is 2..11, the rim is wall.
/// </summary>
public class DisarmTests
{
    private const int Tps = MedRoom.TicksPerSecond;
    private const int Size = 12;

    private static bool Has(mob_living m, Type effect) => Dm.truthy(m.has_status_effect(effect));

    private static float Duration(mob_living m, Type effect) => Dm.num(m.get_timed_status_effect_duration(effect));

    private static void Shove(mob_living shover, mob_living target) => Waiting.NoSleep(shover.disarm(target));

    [Fact]
    public void AShoveOnAFreeTilePushesOneTileAndStaggersForThreeSeconds()
    {
        var room = new MedRoom(seed: 401, size: Size);
        var shover = room.Human(4, 5);
        var target = room.Human(5, 5);
        room.In(() =>
        {
            Shove(shover, target);
            // disarm: shove_dir = get_dir(loc, target.loc), target.Move(get_step(target.loc, shove_dir), shove_dir) (:818-829)
            Assert.Same(locate(6, 5, 1), target.loc);
            Assert.Same(locate(4, 5, 1), shover.loc);
            Assert.False(Dm.truthy(target.IsKnockdown()));
            Assert.Equal(STANDING_UP, target.body_position);
            Assert.Equal(GRAB_PASSIVE, target.grab_state);
            // SHOVE_CAN_STAGGER (carbon_defense.dm:667): adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 SECONDS) (:883-884)
            Assert.True(Has(target, typeof(datum_status_effect_staggered)));
            Assert.Equal(30f, Duration(target, typeof(datum_status_effect_staggered)));
            Assert.False(Has(target, typeof(datum_status_effect_dazed)));
            Assert.False(Has(shover, typeof(datum_status_effect_staggered)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RepeatedShovesStackStaggerUpToTenSeconds()
    {
        var room = new MedRoom(seed: 402, size: Size);
        var shover = room.Human(3, 5);
        var target = room.Human(4, 5);
        room.In(() =>
        {
            for (int i = 1; i <= 5; i++)
            {
                Shove(shover, target);
                Waiting.NoSleep(shover.Move(locate((int)shover.x + 1, 5, 1), EAST)); // follow the target
                Assert.Equal(MathF.Min(30f * i, 100f), Duration(target, typeof(datum_status_effect_staggered)));
            }
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AShoveIntoAWallKnocksDownForTwoSecondsAndDazesForThree()
    {
        var room = new MedRoom(seed: 403, size: Size);
        var shover = room.Human(10, 5);
        var target = room.Human(11, 5); // the east rim (x = 12) is wall
        room.In(() =>
        {
            Shove(shover, target);
            Assert.Same(locate(11, 5, 1), target.loc); // Move into the wall failed: SHOVE_BLOCKED
            // :850-860: Knockdown(SHOVE_KNOCKDOWN_SOLID = 2 SECONDS, daze_amount = 3 SECONDS), then return before the stagger
            Assert.True(Dm.truthy(target.IsKnockdown()));
            Assert.Equal(20f, Dm.num(target.AmountKnockdown()));
            Assert.True(Has(target, typeof(datum_status_effect_dazed)));
            Assert.Equal(30f, Duration(target, typeof(datum_status_effect_dazed)));
            Assert.False(Has(target, typeof(datum_status_effect_staggered)));
            Assert.Equal(LYING_DOWN, target.body_position);
            Assert.Equal(STANDING_UP, shover.body_position);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AShoveIntoAnotherHumanKnocksTheTargetDownTwoSecondsAndTheBystanderOneDecisecond()
    {
        var room = new MedRoom(seed: 404, size: Size);
        var shover = room.Human(4, 5);
        var target = room.Human(5, 5);
        var bystander = room.Human(6, 5);
        room.In(() =>
        {
            Shove(shover, target);
            // human_defense.dm:126-145: the human on the destination blocks (disarm_precollide returns COMSIG_LIVING_ACT_SOLID), the
            // collision knocks the target down for SHOVE_KNOCKDOWN_HUMAN (2 s) and the bystander for SHOVE_KNOCKDOWN_COLLATERAL (1 ds),
            // both dazed 3 s, and the shove is handled (no stagger)
            Assert.Same(locate(5, 5, 1), target.loc);
            Assert.Same(locate(6, 5, 1), bystander.loc);
            Assert.Equal(20f, Dm.num(target.AmountKnockdown()));
            Assert.Equal(1f, Dm.num(bystander.AmountKnockdown()));
            Assert.True(Has(target, typeof(datum_status_effect_dazed)));
            Assert.True(Has(bystander, typeof(datum_status_effect_dazed)));
            Assert.Equal(30f, Duration(target, typeof(datum_status_effect_dazed)));
            Assert.False(Has(target, typeof(datum_status_effect_staggered)));
            Assert.False(Has(bystander, typeof(datum_status_effect_staggered)));
            Assert.Equal(LYING_DOWN, target.body_position);
            Assert.Equal(LYING_DOWN, bystander.body_position);
        });
        // the bystander's 1 ds knockdown ends at the first SSpriority_effects fire; it stands again after get_up's second
        room.StepUntil(() => !Dm.truthy(bystander.IsKnockdown()), 8);
        room.StepUntil(() => bystander.body_position == STANDING_UP, 2 * Tps);
        room.In(() => Assert.Equal(LYING_DOWN, target.body_position));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AKnockedDownAndDazedTargetIsKickedOntoItsSideAndParalyzedForThreeSeconds()
    {
        var room = new MedRoom(seed: 405, size: Size);
        var shover = room.Human(4, 5);
        var target = room.Human(5, 5);
        room.In(() =>
        {
            Waiting.NoSleep(target.Knockdown(10 * SECONDS, daze_amount: 3 * SECONDS));
            Assert.True(Has(target, typeof(datum_status_effect_dazed)));
            Assert.True(Dm.truthy(target.get_shove_flags(shover, null)) );
            Assert.True(Dm.truthy(Dm.bitand(target.get_shove_flags(shover, null), SHOVE_CAN_KICK_SIDE)));
            Shove(shover, target);
            // :861-869: Paralyze(SHOVE_CHAIN_PARALYZE = 3 s), no_side_kick for 3.6 s, SetKnockdown(0) after 3 s; the earlier
            // Move (:818-829) already pushed the lying target one tile
            Assert.True(Dm.truthy(target.IsParalyzed()));
            Assert.Equal(30f, Dm.num(target.AmountParalyzed()));
            Assert.True(Has(target, typeof(datum_status_effect_no_side_kick)));
            Assert.True(HAS_TRAIT_FROM(target, TRAIT_NO_SIDE_KICK, TRAIT_STATUS_EFFECT("no side kick")));
            Assert.Equal(36f, Duration(target, typeof(datum_status_effect_no_side_kick)));
            Assert.False(Has(target, typeof(datum_status_effect_dazed))); // Paralyze clears the daze (staggered.dm:90-96)
            Assert.Same(locate(6, 5, 1), target.loc);
            // a second kick is refused while no_side_kick lasts: the flags lose SHOVE_CAN_KICK_SIDE (carbon_defense.dm:669-671)
            Assert.False(Dm.truthy(Dm.bitand(target.get_shove_flags(shover, null), SHOVE_CAN_KICK_SIDE)));
        });
        // the SetKnockdown(0) timer fires after 3 s: the knockdown (10 s) is cleared, the paralysis ends too at 3 s
        room.Step(3 * Tps + 6);
        room.In(() =>
        {
            Assert.False(Dm.truthy(target.IsKnockdown()));
            Assert.False(Dm.truthy(target.IsParalyzed()));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AShoveIsRefusedWhenTheShoverLiesDownOrIsTheTargetOrStandsOnTheTargetsTile()
    {
        var room = new MedRoom(seed: 406, size: Size);
        var shover = room.Human(4, 5);
        var target = room.Human(5, 5);
        var same = room.Human(4, 5);
        room.In(() =>
        {
            Assert.True(Dm.truthy(shover.can_disarm(target)));
            Assert.False(Dm.truthy(shover.can_disarm(shover))); // src == target
            Assert.False(Dm.truthy(shover.can_disarm(same))); // loc == target.loc
            Shove(shover, same);
            Assert.False(Has(same, typeof(datum_status_effect_staggered)));
            Assert.Same(locate(4, 5, 1), same.loc);

            Waiting.NoSleep(shover.set_resting(TRUE));
            Assert.Equal(LYING_DOWN, shover.body_position);
            Assert.False(Dm.truthy(shover.can_disarm(target))); // body_position != STANDING_UP
            Shove(shover, target);
            Assert.Same(locate(5, 5, 1), target.loc);
            Assert.False(Has(target, typeof(datum_status_effect_staggered)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ARightClickAttackHandIsAShoveLikeTgsUnitTestDisarm()
    {
        // combat.dm:72-102 (without the toolbox): Attacker --> Victim --> Empty space --> Wall
        var room = new MedRoom(seed: 407, size: Size);
        var attacker = room.Human(9, 5);
        var victim = room.Human(10, 5);
        room.In(() =>
        {
            var modifiers = Dm.list();
            modifiers[RIGHT_CLICK] = TRUE;
            // First disarm: Attacker --> Empty space --> Victim --> Wall
            Waiting.NoSleep(victim.attack_hand(attacker, modifiers));
            Assert.Equal(11f, victim.loc.x); // "Victim wasn't moved back after being pushed"
            Assert.False(Has(victim, typeof(datum_status_effect_incapacitating_knockdown))); // "Victim was knocked down despite not being against a wall"

            Waiting.NoSleep(attacker.Move(locate(10, 5, 1), EAST));
            // Second disarm, victim was against wall and should be down
            Waiting.NoSleep(victim.attack_hand(attacker, modifiers));
            Assert.Equal(11f, victim.loc.x); // "Victim was moved after being pushed against a wall"
            Assert.True(Has(victim, typeof(datum_status_effect_incapacitating_knockdown))); // "Victim was not knocked down after being pushed against a wall"
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACornerShoveGoesAlongTheDiagonalAndAWallOnEitherAxisStopsIt()
    {
        var room = new MedRoom(seed: 409, size: Size);
        var shover = room.Human(4, 4);
        var target = room.Human(5, 5);
        room.In(() =>
        {
            // get_dir(NE) = NORTH|EAST: the destination is get_step(target.loc, NORTHEAST) (living_defense.dm:818-819)
            Assert.Equal(Dm.bitor(NORTH, EAST), Dm.num(get_dir(shover.loc, target.loc)));
            Shove(shover, target);
            Assert.Same(locate(6, 6, 1), target.loc);
            Assert.True(Has(target, typeof(datum_status_effect_staggered)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSecondShoveOfAKnockedDownTargetIntoAWallKicksItOntoItsSide()
    {
        // the target was knocked down and dazed by a wall shove; shoving it again is a side kick (the SHOVE_CAN_KICK_SIDE branch
        // comes before the knockdown in the flag test at :850: SHOVE_BLOCKED and not (KNOCKDOWN_BLOCKED | CAN_KICK_SIDE))
        var room = new MedRoom(seed: 410, size: Size);
        var shover = room.Human(10, 5);
        var target = room.Human(11, 5);
        room.In(() =>
        {
            Shove(shover, target);
            Assert.True(Dm.truthy(target.IsKnockdown()));
            Assert.True(HAS_TRAIT(target, TRAIT_DAZED));
            Shove(shover, target);
            Assert.True(Dm.truthy(target.IsParalyzed()));
            Assert.Equal(30f, Dm.num(target.AmountParalyzed()));
            Assert.Same(locate(11, 5, 1), target.loc);
        });
        room.AssertNoRuntimes();
    }

    // ---- DV17: capsule mobs shove on a shared tile (docs/ai/DEVIATIONS.md DV17, D105) ----
    // A capsule's centre is in 1/256 tiles: tile (8, 8) is x, y = 2048 +- 128. A frame (moveX east, moveY north, 127 at most)
    // moves a mob 85 units a tick; two standing capsules never overlap, so the walking one is pushed off the other's centre in
    // some direction: the test reads both centres and applies the deviation's rule to them.

    private static (mob_living_carbon_human Shover, mob_living_carbon_human Target) SharedTile(CapsuleRoom room, int x, int y, sbyte moveX, sbyte moveY)
    {
        var shover = room.Join(x, y, "Shover");
        var target = room.Dummy(x, y);
        room.Walk(shover, moveX, moveY, 1);
        Assert.Equal(room.Tile(shover), room.Tile(target));
        return (shover, target);
    }

    /// <summary>The direction DV17 names: BYOND's get_dir (a sign test) from the shover's centre to the target's.</summary>
    private static (int Dx, int Dy) CentreDir(CapsuleRoom room, mob_living shover, mob_living target)
    {
        MotionState a = room.State(shover);
        MotionState b = room.State(target);
        return (Math.Sign(b.X - a.X), Math.Sign(b.Y - a.Y));
    }

    [Theory]
    [InlineData(127, 0)]
    [InlineData(-127, 0)]
    [InlineData(0, -127)]
    [InlineData(127, -127)]
    [InlineData(-127, -127)]
    public void Dv17TwoCapsuleMobsOnOneTileShoveAlongTheLineOfTheirCentres(int moveX, int moveY)
    {
        var room = new CapsuleRoom(seed: 411);
        (mob_living_carbon_human shover, mob_living_carbon_human target) = SharedTile(room, 8, 8, (sbyte)moveX, (sbyte)moveY);
        (int dx, int dy) = CentreDir(room, shover, target);
        Assert.NotEqual((0, 0), (dx, dy));
        room.In(() =>
        {
            // get_dir of the two turfs is 0 (one turf), so only the centres can give the shove a direction
            Assert.Equal(0f, Dm.num(get_dir(shover.loc, target.loc)));
            // can_disarm's same-turf refusal does not apply to two capsule mobs (DV17)
            Assert.True(Dm.truthy(shover.can_disarm(target)));
            Waiting.NoSleep(shover.disarm(target));
            // the destination is the target's tile plus one step along that direction: tg's full tile
            Assert.Same(locate(8 + dx, 8 + dy, 1), target.loc);
            Assert.Same(locate(8, 8, 1), shover.loc);
            Assert.True(Has(target, typeof(datum_status_effect_staggered)));
            Assert.Equal(30f, Duration(target, typeof(datum_status_effect_staggered)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void Dv17TheClickPathShovesOnASharedTileToo()
    {
        var room = new CapsuleRoom(seed: 412);
        (mob_living_carbon_human shover, mob_living_carbon_human target) = SharedTile(room, 8, 8, 127, 0);
        (int dx, int dy) = CentreDir(room, shover, target);
        room.In(() =>
        {
            var modifiers = Dm.list();
            modifiers[RIGHT_CLICK] = TRUE;
            // spec_attack_hand -> the species' disarm (_species.dm:987) has its own same-turf refusal, which DV17 lifts as well
            Waiting.NoSleep(target.attack_hand(shover, modifiers));
            Assert.Same(locate(8 + dx, 8 + dy, 1), target.loc);
            Assert.True(Has(target, typeof(datum_status_effect_staggered)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void Dv17AShoveIntoAWallFromASharedTileKnocksDown()
    {
        var room = new CapsuleRoom(seed: 413);
        // the floor is 2..15: the rim (x = 16) is wall; the shover is pushed west of the target's centre, so the shove goes east
        var shover = room.Join(15, 8, "Shover");
        var target = room.Dummy(15, 8);
        room.Walk(shover, -127, 0, 1);
        (int dx, int dy) = CentreDir(room, shover, target);
        Assert.Equal(1, dx);
        room.In(() =>
        {
            Waiting.NoSleep(shover.disarm(target));
            if (dy == 0)
            {
                Assert.Same(locate(15, 8, 1), target.loc);
                Assert.True(Dm.truthy(target.IsKnockdown()));
                Assert.Equal(20f, Dm.num(target.AmountKnockdown()));
                Assert.True(Has(target, typeof(datum_status_effect_dazed)));
            }
        });
        room.AssertNoRuntimes();
    }
}
