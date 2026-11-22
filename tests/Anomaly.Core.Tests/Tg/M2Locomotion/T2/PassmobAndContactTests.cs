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
/// Standing mobs share 2 m tiles and do not swap or push (D105, DV16). Expected from the DM: a mover with PASSMOB passes a
/// mob's tile because /mob has pass_flags_self = PASSMOB (mob_defines.dm:19) and /atom/CanAllowThrough lets a mover whose
/// pass_flags overlap it through (_atom.dm:245-249), so /turf/Enter finds no first_bump (turf.dm:434-466); a contact runs
/// MobBump's lines up to the swap (living.dm:176-203): the walk-intent stop, COMSIG_LIVING_PRE_MOB_BUMP (sent to the bumped
/// mob, can block), COMSIG_LIVING_MOB_BUMP (to the bumper) and COMSIG_LIVING_MOB_BUMPED (to the bumped one). Nothing of the
/// swap (:209-235) or the push (:238-274) happens.
/// </summary>
public class PassmobAndContactTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    private static List<string> Lines(IEnumerable<string> trace, string prefix) => trace.Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    // ---- PASSMOB ----

    [Fact]
    public void ThePremiseAMoverWithoutPassmobIsStoppedByAStandingMobAndOneWithItPasses()
    {
        var r = new T2Room(301);
        mob_living_carbon_human b = r.Dummy(7, 5, name: "other");
        mob_living_carbon_human a = r.Join(6, 5, "Alice", name: "mover");
        r.In(() =>
        {
            Assert.Equal((float)PASSMOB, Dm.num(b.pass_flags_self));                                        // mob_defines.dm:19
            Assert.False(Dm.truthy(Waiting.NoSleep(b.CanPass(a, WEST))));                             // /atom/CanAllowThrough: !density
            Dm.combine(ref a.pass_flags, PASSMOB);
            Assert.True(Dm.truthy(Waiting.NoSleep(b.CanPass(a, WEST))));
            Dm.mask(ref a.pass_flags, Dm.bitnot(PASSMOB));
        });
    }

    [Fact]
    public void TheCrossingMoveHasPassmobOnForTheCallAndOffAfterwards()
    {
        var r = new T2Room(302);
        mob_living_carbon_human a = r.Join(6, 5, "Alice", name: "mover");
        mob_living_carbon_human b = r.Dummy(7, 5, name: "other");
        r.Go(a, 0, 0, East, 2);
        var seen = new List<string>();
        r.In(() =>
        {
            On(a, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2, act: _ => seen.Add("attempted:" + Dm.str(Dm.bitand(a.pass_flags, PASSMOB))));
            On(a, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1, act: _ => seen.Add("pre_move:" + Dm.str(Dm.bitand(a.pass_flags, PASSMOB))));
            On(a, COMSIG_MOVABLE_MOVED, "moved", 5, act: _ => seen.Add("moved:" + Dm.str(Dm.bitand(a.pass_flags, PASSMOB))));
            On(a, COMSIG_MOB_CLIENT_MOVED, "client_moved", 3, act: _ => seen.Add("client_moved:" + Dm.str(Dm.bitand(a.pass_flags, PASSMOB))));
            Assert.Equal(0f, Dm.num(Dm.bitand(a.pass_flags, PASSMOB)));
        });
        r.Go(a, 127, 0, East, 3);
        Assert.Equal((7, 5, 1), r.LocOf(a)); // b's tile: entered, not bumped
        string on = Dm.str(PASSMOB);
        // PASSMOB is on through the whole Move call and off again for client/Move's tail
        Assert.Equal(new[] { "attempted:" + on, "pre_move:" + on, "moved:" + on, "client_moved:0" }, seen);
        Assert.Equal(0f, Dm.num(r.In(() => Dm.bitand(a.pass_flags, PASSMOB))));
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("bump", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMoverThatAlreadyHadPassmobKeepsItAfterTheCrossing()
    {
        var r = new T2Room(303);
        mob_living_carbon_human a = r.Join(6, 5, "Alice", name: "mover");
        r.In(() => Dm.combine(ref a.pass_flags, PASSMOB));
        r.Go(a, 127, 0, East, 3);
        Assert.Equal((7, 5, 1), r.LocOf(a));
        Assert.True(Dm.truthy(r.In(() => Dm.bitand(a.pass_flags, PASSMOB))), "the flag tg's other code set is not ours to remove");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARefusedCrossingAlsoTakesPassmobOffAgain()
    {
        var r = new T2Room(304);
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.In(() => On(a, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE));
        r.Go(a, 127, 0, East, 3);
        Assert.Equal((5, 5, 1), r.LocOf(a));
        Assert.Equal(0f, Dm.num(r.In(() => Dm.bitand(a.pass_flags, PASSMOB))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMobThatIsEnteredIsNotAskedToCrossAsAnObstacleButIsStillCrossedLikeAnyOccupant()
    {
        var r = new T2Room(305);
        mob_living_carbon_human a = r.Join(6, 5, "Alice", name: "mover");
        mob_living_carbon_human b = r.Dummy(7, 5, name: "other");
        r.Go(a, 0, 0, East, 2);
        r.In(() =>
        {
            On(b, COMSIG_MOVABLE_CROSS, "cross", 1);
            On(b, COMSIG_ATOM_TRIED_PASS, "tried_pass", 2);
        });
        r.Trace.Clear();
        r.Go(a, 127, 0, East, 3);
        // turf.dm:448-453: thing.Cross(mover) sends COMSIG_MOVABLE_CROSS then, through CanPass, COMSIG_ATOM_TRIED_PASS
        Assert.Contains("cross other mover", r.Trace);
        Assert.Contains("tried_pass other mover 8", r.Trace);
        Assert.Equal(new object[] { b, a }, r.In(() => Items(locate(7, 5, 1).contents).Where(o => o is mob).ToArray()));
        r.AssertNoRuntimes();
    }

    // ---- contact ----

    [Fact]
    public void AContactSendsPreMobBumpToTheBumpedThenMobBumpToTheBumperThenMobBumpedToTheBumped()
    {
        var r = new T2Room(310);
        mob_living_carbon_human b = r.Dummy(6, 5, name: "other");
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.Go(a, 0, 0, East, 2);
        r.In(() =>
        {
            On(b, COMSIG_LIVING_PRE_MOB_BUMP, "pre_mob_bump", 1);
            On(a, COMSIG_LIVING_MOB_BUMP, "mob_bump", 1);
            On(b, COMSIG_LIVING_MOB_BUMPED, "mob_bumped", 1);
            On(a, COMSIG_MOVABLE_BUMP, "bump", 1);
            On(b, COMSIG_ATOM_BUMPED, "bumped", 1);
            On(b, COMSIG_MOVABLE_MOVED, "b_moved", 1);
        });
        r.Trace.Clear();
        r.Go(a, 127, 0, East, 12);
        string[] first = r.Trace.Take(3).ToArray();
        Assert.Equal(new[] { "pre_mob_bump other mover", "mob_bump mover other", "mob_bumped other mover" }, first);
        // none of the turf-driven bump, no push or swap: B never moved, A's Bump (the tile-driven chain) never ran
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("bump ", StringComparison.Ordinal) || l.StartsWith("bumped ", StringComparison.Ordinal) || l.StartsWith("b_moved", StringComparison.Ordinal));
        Assert.Equal((6, 5, 1), r.LocOf(b));
        Assert.False(Dm.truthy(r.In(() => a.now_pushing)) || Dm.truthy(r.In(() => b.now_pushing)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABlockedPreMobBumpStopsTheBumpSignalsAndDoesNotSilenceTheNextTry()
    {
        var r = new T2Room(311);
        mob_living_carbon_human b = r.Dummy(6, 5, name: "other");
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.Go(a, 0, 0, East, 2);
        datum_test_integration_probe gate = null;
        r.In(() =>
        {
            gate = On(b, COMSIG_LIVING_PRE_MOB_BUMP, "pre_mob_bump", 1, result: COMPONENT_LIVING_BLOCK_PRE_MOB_BUMP);
            On(a, COMSIG_LIVING_MOB_BUMP, "mob_bump", 1);
            On(b, COMSIG_LIVING_MOB_BUMPED, "mob_bumped", 1);
        });
        r.Go(a, 127, 0, East, 12);
        Assert.NotEmpty(Lines(r.Trace, "pre_mob_bump"));
        Assert.Empty(Lines(r.Trace, "mob_bump"));
        Assert.Empty(Lines(r.Trace, "mob_bumped"));
        r.In(() => gate.result = NONE);
        r.Go(a, 127, 0, East, 6);
        Assert.NotEmpty(Lines(r.Trace, "mob_bump"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WalkIntentStopsMobBumpBeforeAnySignal()
    {
        var r = new T2Room(312);
        mob_living_carbon_human b = r.Dummy(6, 5, name: "other");
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.In(() =>
        {
            a.move_intent = MOVE_INTENT_WALK; // living.dm:178: "No bumping/swapping/pushing others if you are on walk intent"
            On(b, COMSIG_LIVING_PRE_MOB_BUMP, "pre_mob_bump", 1);
            On(a, COMSIG_LIVING_MOB_BUMP, "mob_bump", 1);
        });
        r.Go(a, 127, 0, East, 20);
        Assert.Empty(r.Trace);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(1.5f, 3)]
    [InlineData(2.5f, 5)]
    [InlineData(1f, 2)]
    public void AContactIsRepeatedAtMostOncePerMoveDelayPerPair(float slowdown, int delayTicks)
    {
        var r = new T2Room(313);
        mob_living_carbon_human b = r.Dummy(6, 5, name: "other");
        mob_living_carbon_human a = r.Join(5, 5, "Alice", slowdown, "mover");
        r.Go(a, 0, 0, East, 2);
        var ticks = new List<int>();
        r.In(() => On(a, COMSIG_LIVING_MOB_BUMP, "mob_bump", 1, act: _ => ticks.Add((int)r.World.Tick)));
        r.Go(a, 127, 0, East, 120);
        Assert.True(ticks.Count >= 10, "contacts: " + ticks.Count);
        for (int i = 1; i < ticks.Count; i++)
            Assert.True(ticks[i] - ticks[i - 1] >= delayTicks, $"contacts {ticks[i - 1]} and {ticks[i]} are closer than the move delay {delayTicks}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CombatModeChangesNothingBetweenCapsulesNoSwapNoWalkPush()
    {
        // tg: a non-combat pair swaps (living.dm:209) and a combat-mode walker pushes a non-combat mob a tile (:238-274).
        // Under DV16 neither happens, in any combination of modes.
        foreach ((bool aCombat, bool bCombat) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            var r = new T2Room(315);
            mob_living_carbon_human b = r.Dummy(7, 5, name: "other");
            mob_living_carbon_human a = r.Join(6, 5, "Alice", name: "mover");
            r.In(() =>
            {
                a.combat_mode = aCombat ? TRUE : FALSE;
                b.combat_mode = bCombat ? TRUE : FALSE;
            });
            r.Go(a, 0, 0, East, 2);
            r.Go(a, 127, 0, East, 15);
            Assert.Equal((7, 5, 1), r.LocOf(b));
            Assert.Equal((7, 5, 1), r.LocOf(a)); // A entered B's tile (PASSMOB) and stands against its capsule
            MotionState sa = r.State(a), sb = r.State(b);
            Assert.Equal(7 * T2Room.C, sb.X);
            Assert.True(sa.X < sb.X, "A is on the near side of B");
            long dx = sa.X - sb.X, dy = sa.Y - sb.Y;
            Assert.True(dx * dx + dy * dy >= 90L * 90L, "the capsules do not overlap");
            r.AssertNoRuntimes();
        }
    }

    // ---- lying mobs ----

    [Fact]
    public void ALyingMobIsNeitherAnObstacleNorGivesContact()
    {
        var r = new T2Room(320);
        mob_living_carbon_human b = r.Dummy(6, 5, name: "other");
        r.In(() => b.body_position = LYING_DOWN);
        r.Step(); // the bridge reads the posture each tick
        Assert.Equal(Posture.Lying, r.State(b).Posture);
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.In(() => On(a, COMSIG_LIVING_MOB_BUMP, "mob_bump", 1));
        r.Go(a, 127, 0, East, 15);
        Assert.True(r.State(a).X > r.State(b).X + 100, "A walked over B");
        Assert.Empty(r.Trace);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALyingMoverPassesAStandingMobAndStandingUpInsideItIsPushedOut()
    {
        var r = new T2Room(321);
        mob_living_carbon_human b = r.Dummy(7, 5, name: "other");
        mob_living_carbon_human a = r.Join(5, 5, "Alice", T2Room.Run + 4f, "mover"); // crawling: RUN_DELAY + 4
        r.In(() =>
        {
            a.body_position = LYING_DOWN;
            On(a, COMSIG_LIVING_MOB_BUMP, "mob_bump", 1);
        });
        r.Step();
        Assert.Equal(MotionDriver.Prone, r.State(a).Driver);
        r.Go(a, 127, 0, East, 80); // 5.5 ds per tile: 0.09 tile per tick; across B's tile centre
        Assert.True(r.State(a).X > 7 * T2Room.C + 60, "the lying A crawled through B's centre");
        Assert.Empty(r.Trace);
        // standing up on top of B is not left overlapping
        r.In(() => a.body_position = STANDING_UP);
        r.Go(a, 0, 0, East, 6);
        MotionState sa = r.State(a), sb = r.State(b);
        long dx = sa.X - sb.X, dy = sa.Y - sb.Y;
        Assert.True(dx * dx + dy * dy >= 90L * 90L - 4, $"overlap after standing up: {dx},{dy}");
        r.AssertOnLoc(a);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStandingMobStopsTheWalkerAtTheSumOfTheRadii()
    {
        var r = new T2Room(322);
        mob_living_carbon_human b = r.Dummy(9, 5, name: "other");
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.Go(a, 127, 0, East, 80);
        // two radii of 0.175 tile: 45 subunits each, so the centres rest 90 apart, a hair more by the 1 subunit touch margin
        long gap = r.State(b).X - r.State(a).X;
        Assert.InRange(gap, 90, 92);
        Assert.Equal(r.State(b).Y, r.State(a).Y);
        r.AssertOnLoc(a);
        r.AssertNoRuntimes();
    }
}
