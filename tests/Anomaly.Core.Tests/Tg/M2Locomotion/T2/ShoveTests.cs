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
/// The shove (living_defense.dm:802-887, _species.dm:987-1031, DV17) on real bodies, with capsules: a right click on an adjacent
/// human with an empty hand reaches species.spec_attack_hand and disarm; the target moves tg's full tile in the shove's
/// direction (its capsule slides it over the glide time), against a dense object it is knocked down, otherwise staggered.
/// Includes /datum/unit_test/disarm of combat.dm (the wall shove) in the capsule world.
/// </summary>
public class ShoveTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);
    private const int C = T2Room.C;
    private static readonly Type Knockdown = typeof(datum_status_effect_incapacitating_knockdown);
    private static readonly Type Staggered = typeof(datum_status_effect_staggered);

    private static void RightClick(T2Room r, mob_living_carbon_human who, atom target)
    {
        r.Command(() => ClientSeam.Click(who.client, target, ClientSeam.ClickParams(RIGHT_CLICK)));
        r.Step();
    }

    private static bool Has(T2Room r, mob_living who, Type effect) => Dm.truthy(r.In(() => who.has_status_effect(effect)));

    private static DmList Modifiers(bool right)
    {
        DmList l = Dm.list();
        l[RIGHT_CLICK] = right ? TRUE : FALSE;
        return l;
    }

    // tg: code/modules/unit_tests/combat.dm:/datum/unit_test/disarm/Run (without the toolbox asserts)
    // Attacker --> Victim --> Empty space --> Wall
    [Fact]
    public void UnitTestDisarmTheVictimIsShovedBackOneTileAndKnockedDownAgainstTheWall()
    {
        var r = new T2Room(1301);
        mob_living_carbon_human attacker = r.Dummy<mob_living_carbon_human_consistent>(5, 5, name: "attacker");
        mob_living_carbon_human victim = r.Dummy<mob_living_carbon_human_consistent>(6, 5, name: "victim");
        r.Crate(8, 5); // the barricade: dense and anchored

        // First disarm, world should now look like: Attacker --> Empty space --> Victim --> Wall
        r.Command(() => Waiting.NoSleep(victim.attack_hand(attacker, Modifiers(right: true))));
        r.Step();
        Assert.Equal(7, r.LocOf(victim).X); // "Victim wasn't moved back after being pushed" (loc.x == bottom_left.x + 2 there; +2 from the attacker here)
        Assert.False(Has(r, victim, Knockdown), "Victim was knocked down despite not being against a wall");

        // the capsule is not teleported: it slides the full tile over tg's glide time (4 ticks)
        MotionState s = r.State(victim);
        Assert.True(s.Slide.Active);
        r.AssertOnLoc(victim);
        r.Step(4);
        Assert.Equal((7 * C, 5 * C), (r.State(victim).X, r.State(victim).Y));

        r.Command(() => Waiting.NoSleep(attacker.forceMove(get_step(attacker, EAST))));
        r.Step();

        // Second disarm, victim was against wall and should be down
        r.Command(() => Waiting.NoSleep(victim.attack_hand(attacker, Modifiers(right: true))));
        r.Step();
        Assert.Equal(7, r.LocOf(victim).X); // "Victim was moved after being pushed against a wall"
        Assert.True(Has(r, victim, Knockdown), "Victim was not knocked down after being pushed against a wall");
        r.Step();
        Assert.Equal(Posture.Lying, r.State(victim).Posture); // knocked down: the capsule lies and is no obstacle
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARightClickShoveSlidesTheTargetFourTicksAndStaggersItInTheOpen()
    {
        var r = new T2Room(1302);
        mob_living_carbon_human b = r.Dummy(9, 8, name: "target");
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "shover");
        r.Go(a, 0, 0, East, 2);
        RightClick(r, a, b);
        Assert.Equal((10, 8, 1), r.LocOf(b));
        MotionState first = r.State(b);
        Assert.Equal(new Slide(10 * C, 8 * C, 3), first.Slide); // the first share was played in the tick of the shove
        Assert.Equal(9 * C + 64, first.X);
        var xs = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            r.Step();
            xs.Add(r.State(b).X);
            r.AssertOnLoc(b);
        }
        Assert.Equal(new[] { 9 * C + 128, 9 * C + 192, 10 * C }, xs);
        Assert.True(Has(r, b, Staggered), "an open floor shove staggers (living_defense.dm:883)");
        Assert.False(Has(r, b, Knockdown));
        // the melee cooldown of the shover: the next shove waits 16 ticks (CLICK_CD_MELEE)
        Assert.Equal(1, r.LocOf(b).X - 9);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShoveAgainstTheWallKnocksDownAndAKnockedDownTargetLiesAndIsNoObstacle()
    {
        var r = new T2Room(1303);
        mob_living_carbon_human b = r.Dummy(14, 8, name: "target");
        mob_living_carbon_human a = r.Join(13, 8, "Alice", name: "shover");
        r.Go(a, 0, 0, East, 2);
        RightClick(r, a, b); // b to (15, 8): the last floor tile
        Assert.Equal((15, 8, 1), r.LocOf(b));
        r.Step(30); // past the melee cooldown (16 ticks) and the slide
        // the shover follows (walks east, adjacent), shoves again: the wall at x = 16
        r.Go(a, 127, 0, East, 6);
        Assert.True(Math.Abs(r.State(b).X - r.State(a).X) >= 80);
        RightClick(r, a, b);
        Assert.True(Has(r, b, Knockdown), "shoved into the rim wall");
        r.Step();
        Assert.Equal(Posture.Lying, r.State(b).Posture);
        Assert.Equal(15, r.LocOf(b).X);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShoveOnASharedTileGoesInTheDirectionOfTheCapsuleCentres()
    {
        // DV17 (D105, Q2): two players a metre apart share a 2 m tile; tg's same-turf refusal (can_disarm, and the species'
        // own copy of it) does not apply to capsule mobs, the direction is the 8-sector direction of the centres (the bridge's
        // 22.5-degree rule, lead decision at the M2 integration) and the target moves tg's full tile from its own turf
        var r = new T2Room(1304);
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "shover");
        mob_living_carbon_human b = r.Dummy(8, 8, name: "target"); // same tile, the centres are pushed 90 apart
        r.Go(a, 0, 0, East, 6);
        MotionState sa = r.State(a), sb = r.State(b);
        Assert.Equal(r.LocOf(a), r.LocOf(b));
        long dx = sb.X - sa.X, dy = sb.Y - sa.Y;
        Assert.True(dx * dx + dy * dy >= 90L * 90L - 180, $"the bodies did not separate: ({dx},{dy})");
        // the direction the bridge's 8-sector rule gives (LocomotionSeam.SectorDir)
        float expected = LocomotionSeam.SectorDir(dx, dy);
        Assert.NotEqual(0f, expected);
        var (tx, ty, _) = r.LocOf(b);
        RightClick(r, a, b);
        (int nx, int ny, _) = r.LocOf(b);
        Assert.Equal((tx + ((expected % 8) >= 4 ? 1 : 0) - (expected >= 8 ? 1 : 0), ty + (((int)expected & 1) != 0 ? 1 : 0) - (((int)expected & 2) != 0 ? 1 : 0)), (nx, ny));
        r.AssertOnLoc(b);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShoverWhoIsLyingOrShovingItselfDoesNothingAndAMobsOwnTileStillGuardsNonCapsules()
    {
        var r = new T2Room(1305);
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "shover");
        mob_living_carbon_human b = r.Dummy(9, 8, name: "target");
        r.Go(a, 0, 0, East, 2);
        r.In(() => a.body_position = LYING_DOWN); // can_disarm: body_position != STANDING_UP
        r.Step();
        RightClick(r, a, b);
        Assert.Equal((9, 8, 1), r.LocOf(b));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShoveIntoAnotherHumanKnocksTheTargetAndTheOneInTheWayDownAndMovesNobody()
    {
        // the destination tile holds a human: its connect_loc handler (disarm_precollide) answers COMSIG_LIVING_ACT_SOLID, so
        // the target does not move, and disarm_collision knocks the target (SHOVE_KNOCKDOWN_HUMAN) and the human (collateral) down
        var r = new T2Room(1306);
        mob_living_carbon_human a = r.Join(7, 8, "Alice", name: "shover");
        mob_living_carbon_human b = r.Dummy(8, 8, name: "target");
        mob_living_carbon_human c = r.Dummy(9, 8, name: "in the way");
        r.Go(a, 0, 0, East, 2);
        RightClick(r, a, b);
        Assert.Equal((8, 8, 1), r.LocOf(b));
        Assert.Equal((9, 8, 1), r.LocOf(c));
        Assert.True(Has(r, b, Knockdown), "the target was not knocked down");
        Assert.True(Has(r, c, Knockdown), "the human in the way was not knocked down");
        Assert.False(Has(r, a, Knockdown));
        r.Step();
        Assert.Equal(Posture.Lying, r.State(b).Posture);
        Assert.Equal(Posture.Lying, r.State(c).Posture);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AKnockedDownDazedTargetShovedAgainIsKickedOntoItsSideAndTheKickCannotBeChained()
    {
        // _carbon get_shove_flags: SHOVE_CAN_KICK_SIDE when knocked down, dazed and not paralyzed; the kick paralyzes
        // (SHOVE_CHAIN_PARALYZE) and marks the target no-side-kick (living_defense.dm:861-869)
        var r = new T2Room(1307);
        mob_living_carbon_human b = r.Dummy(15, 8, name: "target");
        mob_living_carbon_human a = r.Join(14, 8, "Alice", name: "shover");
        r.Go(a, 0, 0, East, 2);
        RightClick(r, a, b);                       // b is on the last floor tile: this shove hits the rim wall at once
        Assert.True(Has(r, b, Knockdown));
        r.Step(16);                                // the melee cooldown
        Assert.Equal(Posture.Lying, r.State(b).Posture);
        RightClick(r, a, b);
        Assert.True(Has(r, b, typeof(datum_status_effect_incapacitating_paralyzed)), "kicked onto its side: paralyzed");
        Assert.True(Has(r, b, typeof(datum_status_effect_no_side_kick)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlayersCapsuleLiesAfterAKnockdownAndGetsUpWhenTheStatusAndTheGetUpAreOver()
    {
        // Knockdown(SHOVE_KNOCKDOWN_SOLID) then tg's get_up (a 1 s do_after): about three seconds down in all
        var r = new T2Room(1308);
        mob_living_carbon_human b = r.Dummy(15, 8, name: "target");
        mob_living_carbon_human a = r.Join(14, 8, "Alice", name: "shover");
        r.Go(a, 0, 0, East, 2);
        RightClick(r, a, b);
        int down = 0;
        while (r.State(b).Posture != Posture.Lying && down++ < 5)
            r.Step();
        int ticksDown = 0;
        while (r.State(b).Posture == Posture.Lying && ticksDown < 200)
        {
            r.Step();
            ticksDown++;
        }
        Assert.InRange(ticksDown, 40, 100); // SHOVE_KNOCKDOWN_SOLID 2 s plus the 1 s get_up
        Assert.Equal(Posture.Standing, r.State(b).Posture);
        Assert.Equal(MotionDriver.Grounded, r.State(b).Driver);
        r.AssertNoRuntimes();
    }
}
