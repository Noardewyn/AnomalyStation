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
/// Pressing into a blocked face (D105, scope 2a "Attempt"): the capsule stops at the face and calls tg's Move into the
/// blocked tile at once and again at tg's move delay, so Enter fails and Bump runs. The expected cadence is a literal model
/// of tg's client/Move (mob_movement.dm:47-52 and :94-127): an attempt happens on every tick whose world.time is not below
/// move_delay, and a Move that bumps still advances move_delay by cached_multiplicative_slowdown from the recent
/// move_delay (the fractional accumulation of :111-119), the way a held key bumps a wall in tg. The wall is the toy room's
/// rim: x = 16, the floor ends at x = 15.
/// </summary>
public class WallBumpCadenceTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    // tg's client/Move bookkeeping for a key held from tick 0: the ticks (of n) on which Move is attempted. move_delay is
    // in deciseconds, a tick is 0.5 ds (world.tick_lag).
    private static List<int> TgAttemptTicks(float slowdown, int ticks)
    {
        const double tick_lag = 0.5;
        var attempts = new List<int>();
        double move_delay = 0;
        for (int tick = 0; tick < ticks; tick++)
        {
            double time = tick * tick_lag;
            if (time < move_delay) // client/Move: if(world.time < move_delay) return FALSE
                continue;
            double old_move_delay = move_delay;
            move_delay = time + tick_lag;
            // :111-116 If the move was recent, count using old_move_delay
            move_delay = old_move_delay + tick_lag > time ? old_move_delay : time;
            move_delay += slowdown;
            attempts.Add(tick);
        }
        return attempts;
    }

    [Theory]
    [InlineData(1.5f)] // run: RUN_DELAY
    [InlineData(4f)]   // walk: WALK_DELAY
    [InlineData(2.5f)] // run with one point of slowdown
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(5.5f)] // crawling
    public void APressAttemptsMoveOnTgsMoveDelayCadenceFromTheFirstTick(float slowdown)
    {
        var r = new T2Room(201);
        // the east wall is x = 16; stand against it at once: tile 15's east face is at 15 * 256 + 128 = 3968
        mob_living_carbon_human m = r.Join(15, 8, "Alice", slowdown, "mover");
        r.Go(m, 0, 0, East, 2);
        r.In(() => On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1));
        // walk to the face first, so the press starts from a known tick
        int walkTicks = 0;
        while (r.State(m).X < 15 * T2Room.C + 128 - 45 && walkTicks < 400)
        {
            r.Go(m, 127, 0, East);
            walkTicks++;
        }
        Assert.Equal(15 * T2Room.C + 128 - 45, r.State(m).X); // the leading face reaches the line but never passes it

        var bumps = new List<int>();
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1, act: _ => bumps.Add((int)r.World.Tick)));
        long startTick = r.World.Tick;
        const int span = 90;
        r.Go(m, 127, 0, East, span);

        // the ticks are numbered 1..span from the start of the hold (World.Tick is the tick being run); align tg's model to
        // the first bump: from there the key is held and every attempt is tg's
        Assert.NotEmpty(bumps);
        int first = bumps[0] - (int)startTick;
        List<int> expected = TgAttemptTicks(slowdown, span - first + 1).Select(t => t + first).ToList();
        List<int> got = bumps.Select(t => t - (int)startTick).ToList();
        Assert.Equal(expected, got);
        Assert.Equal((15, 8, 1), r.LocOf(m));
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("pre_move", StringComparison.Ordinal)); // a bump never passes PRE_MOVE
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFirstPressAttemptsAtOnceAndEachAttemptIsTgsFailedMoveChain()
    {
        var r = new T2Room(202);
        mob_living_carbon_human m = r.Join(15, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, East, 2);
        turf here = r.In(() => locate(15, 8, 1));
        turf wall = r.In(() => locate(16, 8, 1));
        r.In(() =>
        {
            T2Room.WatchChain(m, here, wall);
            On(m, COMSIG_MOB_CLIENT_MOVED, "client_moved", 3);
            On(wall, COMSIG_ATOM_BUMPED, "bumped", 1);
        });
        r.Trace.Clear();
        // 1280 -> 3840 (tile 15 centre) is 0; the face is at 3968 - 45 = 3923: from the centre (3840) two ticks reach it
        // (3925 clamps to 3923), and the second of them is the first press
        r.Go(m, 127, 0, East, 4);
        Assert.Equal(15 * T2Room.C + 128 - 45, r.State(m).X);
        // a failed Move (move-chain 3g, "Enter fail"): ATTEMPTED_MOVE, Exit, CanPass, then the bump; nothing after Enter
        string[] chain = r.Trace.ToArray();
        Assert.Equal(new[]
        {
            "attempted mover (16,8) 4",
            "exit (15,8) mover 4",
            "tried_pass_turf (16,8) mover 8",
            "bump mover (16,8)",     // COMSIG_MOVABLE_BUMP(src; bumped) with the wall turf
            "bumped (16,8) mover",   // bumped.Bumped(src) -> COMSIG_ATOM_BUMPED
        }, chain.Take(5));
        Assert.DoesNotContain(r.Trace, l => l.StartsWith("pre_move", StringComparison.Ordinal) || l.StartsWith("exited", StringComparison.Ordinal) || l.StartsWith("client_moved", StringComparison.Ordinal));
        Assert.Equal((15, 8, 1), r.LocOf(m));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWishMoreThanFortyFiveDegreesOffTheFaceNormalSlidesAlongTheWallWithoutBumping()
    {
        var r = new T2Room(203);
        mob_living_carbon_human m = r.Join(15, 8, "Alice", name: "mover");
        r.Go(m, 127, 0, East, 3); // against the east wall
        int bumpsBefore = 0;
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1));
        // (100, 127): the north component is the larger one, so the east wall is not pressed (the wish is 51 degrees off)
        int y0 = r.State(m).Y;
        r.Go(m, 100, 127, East, 12);
        Assert.Equal(bumpsBefore, r.Trace.Count(l => l.StartsWith("bump", StringComparison.Ordinal)));
        Assert.True(r.State(m).Y > y0 + 100, "the capsule slides north along the wall");
        // (127, 100): 38 degrees off the normal: a press (and the slide goes on)
        r.Go(m, 127, 100, East, 6);
        Assert.True(r.Trace.Count(l => l.StartsWith("bump", StringComparison.Ordinal)) >= 1);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnExactDiagonalIntoAWallPressesItAndAWallBehindTheCornerBumpsBothFaces()
    {
        var r = new T2Room(204);
        // tile (15,15) is the room's north-east corner of the floor: x = 16 and y = 16 are walls
        mob_living_carbon_human m = r.Join(15, 15, "Alice", name: "mover");
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1));
        r.Go(m, 127, 127, East, 4); // the diagonal into the corner: both faces are pressed (tg's diagonal bumps both)
        string[] bumps = r.Trace.Where(l => l.StartsWith("bump", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "bump mover (15,16)", "bump mover (16,15)" }, bumps.Take(2)); // north/south face first
        Assert.Equal((15, 15, 1), r.LocOf(m));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void StandingStillNextToAWallOrBeingUnableToMoveNeverBumps()
    {
        var r = new T2Room(205);
        mob_living_carbon_human m = r.Join(15, 8, "Alice", name: "mover");
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1));
        r.Go(m, 127, 0, East, 3);
        int bumps = r.Trace.Count(l => l.StartsWith("bump", StringComparison.Ordinal));
        Assert.True(bumps >= 1);
        r.Go(m, 0, 0, East, 12); // no wish
        Assert.Equal(bumps, r.Trace.Count(l => l.StartsWith("bump", StringComparison.Ordinal)));
        // the gate says no (MOBILITY_MOVE gone): the press is not made
        r.In(() => m.mobility_flags = Dm.bitand(m.mobility_flags, Dm.bitnot(MOBILITY_MOVE)));
        r.Go(m, 127, 0, East, 12);
        Assert.Equal(bumps, r.Trace.Count(l => l.StartsWith("bump", StringComparison.Ordinal)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADenseObjectOnATileIsASolidTileAndBumpedLikeAWall()
    {
        var r = new T2Room(206);
        mob_living_carbon_human m = r.Join(5, 8, "Alice", name: "mover");
        obj crate = r.Crate(7, 8);
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1));
        r.Go(m, 127, 0, East, 12);
        // the capsule stops against the crate's west face (7 * 256 - 128 = 1664) and the crate is what Enter bumps
        Assert.Equal(6 * T2Room.C + 128 - 45, r.State(m).X);
        Assert.Contains("bump mover crate", r.Trace);
        Assert.Equal((6, 8, 1), r.LocOf(m));
        Assert.Equal((7f, 8f), r.In(() => (crate.x, crate.y))); // it did not move (the mover has no push in M2)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMoveDelayIsATimeNotAHeldKeyCounterSoLettingGoDoesNotRestartTheCadence()
    {
        // tg's move_delay is a time: release the key for a few ticks and press again before the delay is out and the
        // attempt still waits for it (mob_movement.dm:47)
        var r = new T2Room(207);
        mob_living_carbon_human m = r.Join(15, 8, "Alice", T2Room.Walk, "mover"); // WALK_DELAY: 8 ticks per attempt
        r.Go(m, 127, 0, East, 70);                                                // reach the wall and bump
        var bumps = new List<int>();
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1, act: _ => bumps.Add((int)r.World.Tick)));
        while (bumps.Count == 0)
            r.Go(m, 127, 0, East);
        int atTick = bumps[0];
        r.Go(m, 0, 0, East, 3);      // let go for 3 ticks
        r.Go(m, 127, 0, East, 20);   // press again: the next attempt is 8 ticks after the last one, not at once
        Assert.True(bumps.Count >= 2);
        Assert.Equal(atTick + 8, bumps[1]);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFractionalSlowdownBumpsOnTheContractsFlooredDelayWhichIsFasterThanTgsAccumulatedMoveDelay()
    {
        // MotionParams.MoveDelayTicks is max(1, floor(slowdown / tick_lag)) by contract (waiting-model.md section 8): 2.2 ds is
        // 4 ticks. tg's client/Move accumulates the fractional move_delay (mob_movement.dm:111-119), so it attempts at the first
        // tick at or after t0 + n x 2.2 ds: ticks 0, 5, 9, 14, 18, ... (4.4 ticks on average). The bridge keeps the contract's
        // floor, so its attempts come every 4 ticks (up to 0.99 ticks early per attempt); pinned here, reported as a difference.
        var r = new T2Room(208);
        mob_living_carbon_human m = r.Join(15, 8, "Alice", 2.2f, "mover");
        r.Go(m, 127, 0, East, 30);
        var bumps = new List<int>();
        r.In(() => On(m, COMSIG_MOVABLE_BUMP, "bump", 1, act: _ => bumps.Add((int)r.World.Tick)));
        r.Go(m, 127, 0, East, 60);
        Assert.True(bumps.Count >= 10);
        for (int i = 1; i < bumps.Count; i++)
            Assert.Equal(4, bumps[i] - bumps[i - 1]);
        List<int> tg = TgAttemptTicks(2.2f, 60);
        Assert.True(tg.Zip(tg.Skip(1), (a, b) => b - a).Distinct().Count() > 1, string.Join(",", tg)); // tg's own spacing is not constant (5, 4, 5, 4, ...)
        r.AssertNoRuntimes();
    }
}
