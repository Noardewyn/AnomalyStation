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
/// The click chain from the client seam, written from the DM (client_procs.dm:866-943, click.dm:71-186, other_mobs.dm:2-95):
/// /client/Click's click limits and the intercept, then ClickOn's gates in tg's order: next_click (world.time must pass it; each
/// click moves it one decisecond on), the incapacitated check (INCAPABLE_RESTRAINTS and INCAPABLE_STASIS ignored), face_atom,
/// next_move (a click before it only turns the mob), the hands-blocked path, reach by Adjacent (a mob target costs
/// CLICK_CD_MELEE = 8 ds = 16 ticks), and UnarmedAttack to attack_hand (a right click asks attack_hand_secondary first).
/// A tick is 0.5 ds, so the 1 ds next_click spaces two clicks 3 ticks apart (the click at t blocks t + 0.5 and t + 1).
/// </summary>
public class ClickChainGoldenTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    private sealed class Rig
    {
        public readonly T2Room Room;
        public readonly mob_living_carbon_human A;

        public Rig(ulong seed, int ax = 8, int ay = 8, bool liftSecondLimit = true)
        {
            Room = new T2Room(seed);
            // /client/Click drops the 16th click of a 10 ds bucket (second_click_limit 15): tests that click every tick lift it
            if (liftSecondLimit)
                Room.In(() => Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_second_click_limit), 0)));
            A = Room.Join(ax, ay, "Alice", name: "clicker"); // a real human: the brain gives TRAIT_ADVANCEDTOOLUSER, the inventory the hands
        }

        public void Click(atom target, string button = LEFT_CLICK, bool shift = false)
        {
            Room.Command(() => ClientSeam.Click(A.client, target, ClientSeam.ClickParams(button, shift: shift)));
            Room.Step();
        }

        public mob_living_carbon_human Other(int x, int y, string name = "other") => Room.Dummy(x, y, name: name);

        public float Time => Room.In(() => world.time);
    }

    // ---- /client/Click ----

    [Fact]
    public void TheSecondClickLimitIsFifteenAndTheClientSignalSeesOnlyTheOnesThatPass()
    {
        var rig = new Rig(1101, liftSecondLimit: false);
        T2Room r = rig.Room;
        turf floor = r.In(() => locate(9, 8, 1));
        int seen = 0;
        r.In(() => On(rig.A.client, COMSIG_CLIENT_CLICK, "client_click", 0, act: _ => seen++));
        for (int i = 0; i < 20; i++)
        {
            string p = ClientSeam.ClickParams(LEFT_CLICK);
            r.Command(() => ClientSeam.Click(rig.A.client, floor, p));
        }
        r.Step();
        Assert.Equal(15, seen); // second_click_limit 15 (config_entry/number/second_click_limit): the 16th to the 20th are ignored
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInterceptedClickIsSwallowedOnceAndTheNextOneWorks()
    {
        var rig = new Rig(1102);
        T2Room r = rig.Room;
        turf floor = r.In(() => locate(9, 8, 1));
        int clicks = 0;
        r.In(() =>
        {
            On(rig.A, COMSIG_MOB_CLICKON, "clickon", 0, act: _ => clicks++);
            rig.A.client.click_intercept_time = world.time + 100;
        });
        rig.Click(floor);
        Assert.Equal(0, clicks);
        Assert.Equal(0f, Dm.num(r.In(() => rig.A.client.click_intercept_time)));
        r.Step(4);
        rig.Click(floor);
        Assert.Equal(1, clicks);
        r.AssertNoRuntimes();
    }

    // ---- ClickOn ----

    [Fact]
    public void NextClickSpacesTwoClicksThreeTicksApart()
    {
        var rig = new Rig(1103);
        T2Room r = rig.Room;
        turf floor = r.In(() => locate(9, 8, 1));
        var passed = new List<int>();
        int tick = 0;
        r.In(() => On(rig.A, COMSIG_MOB_CLICKON, "clickon", 0, act: _ => passed.Add(tick)));
        for (tick = 0; tick < 9; tick++)
            rig.Click(floor); // one click every tick; a floor tile gives no melee cooldown
        Assert.Equal(new[] { 0, 3, 6 }, passed);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(new[] { 0, 15 }, new[] { 0 })]               // 7.5 ds after the first: next_move (8 ds) is not over
    [InlineData(new[] { 0, 16 }, new[] { 0, 16 })]           // 8 ds: world.time >= next_move passes
    [InlineData(new[] { 0, 13, 16 }, new[] { 0, 16 })]       // a click that bounced off next_move moves next_click a ds on, so it is skipped...
    [InlineData(new[] { 0, 14, 16 }, new[] { 0 })]           // ...and the one it moved next_click past (8.0 <= 8.0) is dropped too
    [InlineData(new[] { 0, 16, 31 }, new[] { 0, 16 })]
    [InlineData(new[] { 0, 16, 32, 48, 63 }, new[] { 0, 16, 32, 48 })]
    public void AMeleeClickOnAMobMakesTheNextOneWaitExactlySixteenTicks(int[] clickTicks, int[] attackTicks)
    {
        var rig = new Rig(1104);
        T2Room r = rig.Room;
        mob_living_carbon_human b = rig.Other(9, 8);
        var attacks = new List<int>();
        int tick = 0;
        r.In(() => On(b, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => attacks.Add(tick)));
        for (tick = 0; tick <= clickTicks.Max(); tick++)
        {
            if (clickTicks.Contains(tick))
                rig.Click(b);
            else
                r.Step();
        }
        Assert.Equal(attackTicks, attacks); // next_move = world.time + 8: the click at world.time >= next_move passes
        AssertCooldown(rig, 8);
        r.AssertNoRuntimes();
    }

    private static void AssertCooldown(Rig rig, float deciseconds)
    {
        // the last click that passed set next_move to its world.time + deciseconds
        float next = Dm.num(rig.Room.In(() => rig.A.next_move));
        Assert.Equal(0f, (next - deciseconds) % 0.5f);
    }

    [Fact]
    public void ASelfClickIsDirectAccessAndCostsTheMeleeCooldownToo()
    {
        var rig = new Rig(1105);
        T2Room r = rig.Room;
        int hands = 0;
        r.In(() => On(rig.A, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => hands++));
        float t = rig.Time + World.TickLag;
        rig.Click(rig.A);
        Assert.Equal(1, hands);
        Assert.Equal(t + CLICK_CD_MELEE, Dm.num(r.In(() => rig.A.next_move)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ClickingAnObjectNextToYouReachesItsAttackHandWithoutAnyCooldownAndDoesNotMovePlayer()
    {
        var rig = new Rig(1106);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        var heard = new List<string>();
        r.In(() =>
        {
            On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 2);
            On(box, COMSIG_ATOM_ATTACK_HAND_SECONDARY, "attack_hand_secondary", 2);
        });
        float before = Dm.num(r.In(() => rig.A.next_move));
        rig.Click(box);
        Assert.Single(r.Trace);
        Assert.StartsWith("attack_hand box clicker", r.Trace[0]);
        Assert.Equal(before, Dm.num(r.In(() => rig.A.next_move))); // ismob(A) is false: no CLICK_CD_MELEE
        r.Trace.Clear();
        r.Step(4);
        rig.Click(box, RIGHT_CLICK);
        // right_click_attack_chain asks attack_hand_secondary first; its default answer is SECONDARY_ATTACK_CALL_NORMAL, so
        // the normal chain follows
        Assert.Equal(2, r.Trace.Count);
        Assert.StartsWith("attack_hand_secondary box clicker", r.Trace[0]);
        Assert.StartsWith("attack_hand box clicker", r.Trace[1]);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASecondaryAttackThatCancelsTheChainStopsTheNormalAttackHand()
    {
        var rig = new Rig(1107);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        r.In(() =>
        {
            On(box, COMSIG_ATOM_ATTACK_HAND_SECONDARY, "attack_hand_secondary", 0, result: COMPONENT_CANCEL_ATTACK_CHAIN);
            On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
        });
        rig.Click(box, RIGHT_CLICK);
        Assert.Equal(new[] { "attack_hand_secondary box" }, r.Trace);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheUnarmedAttackSignalsComeBeforeTheAttackHandAndCanCancelIt()
    {
        var rig = new Rig(1108);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        datum_test_integration_probe early = null;
        r.In(() =>
        {
            early = On(rig.A, COMSIG_LIVING_EARLY_UNARMED_ATTACK, "early", 2);
            On(rig.A, COMSIG_LIVING_UNARMED_ATTACK, "unarmed", 2);
            On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
        });
        rig.Click(box);
        // (source, target, proximity_flag): adjacent, so TRUE; the clicker's modifiers follow
        Assert.Equal(new[] { "early clicker box 1", "unarmed clicker box 1", "attack_hand box" }, r.Trace);
        r.Trace.Clear();
        r.In(() => early.result = COMPONENT_CANCEL_ATTACK_CHAIN);
        r.Step(4);
        rig.Click(box);
        Assert.Equal(new[] { "early clicker box 1" }, r.Trace);
        r.Trace.Clear();
        r.In(() => early.result = COMPONENT_SKIP_ATTACK);
        r.Step(4);
        rig.Click(box);
        Assert.Equal(new[] { "early clicker box 1" }, r.Trace);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClickBeforeNextMoveOnlyTurnsTheMobFaceAtomRunsBeforeTheCooldownGate()
    {
        var rig = new Rig(1109, 8, 8);
        T2Room r = rig.Room;
        mob_living_carbon_human east = rig.Other(9, 8, "east");
        mob_living_carbon_human north = rig.Other(8, 9, "north");
        r.In(() =>
        {
            On(east, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
            On(north, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
            On(rig.A, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
        });
        rig.Click(east);
        Assert.Equal(EAST, r.In(() => rig.A.dir));
        Assert.Equal(new[] { "dir clicker 2 4", "attack_hand east" }, r.Trace); // turned (south -> east) and attacked
        r.Trace.Clear();
        r.Step(4); // next_click is over, next_move (8 ds) is not
        rig.Click(north);
        Assert.Equal(NORTH, r.In(() => rig.A.dir)); // face_atom happens before the next_move gate
        Assert.Equal(new[] { "dir clicker 4 1" }, r.Trace);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(INCAPABLE_RESTRAINTS, true)]
    [InlineData(INCAPABLE_STASIS, true)]
    [InlineData(INCAPABLE_RESTRAINTS | INCAPABLE_STASIS, true)]
    [InlineData(INCAPABLE_GRAB, false)]
    [InlineData(INCAPABLE_GRAB | INCAPABLE_RESTRAINTS, false)]
    [InlineData(0, true)]
    public void TheIncapacitatedCheckIgnoresRestraintsAndStasisOnly(int incapacitated, bool clickGoesThrough)
    {
        var rig = new Rig(1110);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        int hands = 0;
        r.In(() =>
        {
            On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => hands++);
            rig.A.incapacitated = incapacitated;
        });
        rig.Click(box);
        Assert.Equal(clickGoesThrough ? 1 : 0, hands);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HandsBlockedChargesTheHandcuffedDelayAndAsksUnarmedAttackWithTheReachFlagThenRefuses()
    {
        var rig = new Rig(1111);
        T2Room r = rig.Room;
        obj near = r.In(() => Thing("near", locate(9, 8, 1)));
        obj far = r.In(() => Thing("far", locate(12, 8, 1)));
        r.In(() =>
        {
            ADD_TRAIT(rig.A, TRAIT_HANDS_BLOCKED, "t2");
            On(rig.A, COMSIG_LIVING_EARLY_UNARMED_ATTACK, "early", 2);
            On(near, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
            On(far, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
        });
        float t = rig.Time + World.TickLag;
        rig.Click(near);
        Assert.Equal(t + CLICK_CD_HANDCUFFED, Dm.num(r.In(() => rig.A.next_move)));
        Assert.Equal(new[] { "early clicker near 1" }, r.Trace); // proximity = Adjacent(near) = TRUE; can_unarmed_attack then refuses
        r.Trace.Clear();
        r.Step(25); // 10 ds out
        rig.Click(far);
        Assert.Equal(new[] { "early clicker far 0" }, r.Trace); // not adjacent: 0
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheForwardButtonsAreIgnored()
    {
        var rig = new Rig(1112);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        r.In(() => On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0));
        foreach (string button in new[] { BUTTON4, BUTTON5 })
        {
            string p = button + "=1;" + BUTTON + "=" + button;
            r.Command(() => ClientSeam.Click(rig.A.client, box, p));
            r.Step(4);
        }
        Assert.Empty(r.Trace);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClickOnTheClickersOwnTurfIsDirectAccessAndGivesNoCooldown()
    {
        var rig = new Rig(1113);
        T2Room r = rig.Room;
        turf here = r.In(() => (turf)rig.A.loc);
        float before = Dm.num(r.In(() => rig.A.next_move));
        r.In(() => On(rig.A, COMSIG_LIVING_UNARMED_ATTACK, "unarmed", 2));
        rig.Click(here);
        Assert.Equal(new[] { "unarmed clicker (8,8) 1" }, r.Trace);
        Assert.Equal(before, Dm.num(r.In(() => rig.A.next_move)));
        r.AssertNoRuntimes();
    }

    // ---- reach: Adjacent ----

    [Fact]
    public void ReachIsTgsAdjacentAMobsTileAndItsEightNeighboursAndNoFurther()
    {
        var rig = new Rig(1114);
        T2Room r = rig.Room;
        int hands = 0, ranged = 0;
        r.In(() => On(rig.A, COMSIG_MOB_ATTACK_RANGED, "ranged", 0, act: _ => ranged++));
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                obj box = r.In(() => Thing("box", locate(8 + dx, 8 + dy, 1)));
                hands = 0;
                ranged = 0;
                r.In(() => On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => hands++));
                r.Step(4);
                rig.Click(box);
                bool adjacent = Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1;
                Assert.True(adjacent == (hands == 1), $"({dx},{dy}): attack_hand {hands} but adjacent is {adjacent}");
                Assert.True(adjacent == (ranged == 0), $"({dx},{dy}): ranged {ranged}");
                r.In(() => qdel(box));
            }
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADiagonalNeighbourIsReachedThroughAFreeCornerAndNotThroughTwoBlockedOnes()
    {
        var rig = new Rig(1115);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 9, 1)));
        int hands = 0;
        r.In(() => On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => hands++));
        rig.Click(box);
        Assert.Equal(1, hands);                                    // both corners free
        obj c1 = r.Crate(9, 8);
        r.Step(4);
        rig.Click(box);
        Assert.Equal(2, hands);                                    // one blocked corner: the other lets the click through
        obj c2 = r.Crate(8, 9);
        r.Step(4);
        rig.Click(box);
        Assert.Equal(2, hands);                                    // both corners hold a dense object: out of reach
        r.In(() => qdel(c1));
        r.Step(4);
        rig.Click(box);
        Assert.Equal(3, hands);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADenseBorderObjectFacingTheClickerBlocksTheReachOfItsTile()
    {
        var rig = new Rig(1116);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        obj window = r.Edge(9, 8); // ClickCross: a dense ON_BORDER_1 object blocks when its dir includes the way the click goes
        int hands = 0;
        r.In(() => On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => hands++));
        r.In(() => window.dir = NORTH);           // the click comes from the west: a cardinal dir that is not west does not block
        rig.Click(box);
        Assert.Equal(1, hands);
        r.In(() => window.dir = WEST);            // west: the click crosses this border
        r.Step(4);
        rig.Click(box);
        Assert.Equal(1, hands);
        r.In(() => window.dir = NORTHEAST);       // a diagonal dir is a full-tile window: it blocks every direction
        r.Step(4);
        rig.Click(box);
        Assert.Equal(1, hands);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARangedClickIsRangedAttackOrItsSecondaryAndNeverCostsTheCooldown()
    {
        var rig = new Rig(1117);
        T2Room r = rig.Room;
        obj far = r.In(() => Thing("far", locate(12, 8, 1)));
        r.In(() =>
        {
            On(rig.A, COMSIG_MOB_ATTACK_RANGED, "ranged", 1);
            On(rig.A, COMSIG_MOB_ATTACK_RANGED_SECONDARY, "ranged_secondary", 1);
            On(far, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0);
        });
        float before = Dm.num(r.In(() => rig.A.next_move));
        rig.Click(far);
        r.Step(4);
        rig.Click(far, RIGHT_CLICK);
        Assert.Equal(new[] { "ranged clicker far", "ranged_secondary clicker far" }, r.Trace);
        Assert.Equal(before, Dm.num(r.In(() => rig.A.next_move)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadOrDeletedTargetAndAGoneClientDoNothing()
    {
        var rig = new Rig(1118);
        T2Room r = rig.Room;
        obj box = r.In(() => Thing("box", locate(9, 8, 1)));
        int hands = 0;
        r.In(() => On(box, COMSIG_ATOM_ATTACK_HAND, "attack_hand", 0, act: _ => hands++));
        r.In(() => qdel(box));
        r.Step();
        rig.Click(box);
        Assert.Equal(0, hands);
        r.AssertNoRuntimes();
    }
}
