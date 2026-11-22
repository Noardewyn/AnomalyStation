using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.Wave4.Mob;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// do_after and the bridge (move-chain.md section 8; scope 2l "do_after cancelled by a crossing"): a timed action listens to
/// COMSIG_MOVABLE_MOVED on its user and cancels when the user's loc changed (timed_actions.dm:169, on_user_moved: "if the
/// loc is still the old one, return"), unless the action ignores the user's loc. A capsule crossing is tg's real Move, so
/// it cancels; moving inside a tile does not; a slide that tg made (the Move happened before) raises no second Moved.
/// One second is 10 ds, 20 ticks. The progress bar of a player's do_after is a deferred leaf (DV13), so no action shows one.
/// </summary>
public class DoAfterTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    private static (T2Room Room, mob_living_carbon_human User, obj Target) Setup(ulong seed, int x = 5, int y = 5)
    {
        var r = new T2Room(seed);
        mob_living_carbon_human user = r.Join(x, y, "Alice", name: "user");
        obj target = r.In(() => { var t = Dm.@new<obj>(locate(x, y, 1)); t.name = "target"; return t; });
        r.Go(user, 0, 0, East, 2);
        return (r, user, target);
    }

    private static DoAfterCall Begin(T2Room r, mob user, obj target, float seconds = 1, float? flags = null)
    {
        DoAfterCall call = null;
        r.Command(() => call = DoAfterCall.Start(() => do_after(user, seconds * SECONDS, target, timed_action_flags: flags ?? NONE, show_progress: FALSE)));
        r.Step(); // the command runs in this tick's command phase
        Assert.NotNull(call);
        Assert.False(call.Done);
        return call;
    }

    [Fact]
    public void AStandingUserCompletesTheDoAfter()
    {
        var (r, user, target) = Setup(1001);
        DoAfterCall call = Begin(r, user, target);
        r.Go(user, 0, 0, East, 25);
        Assert.True(call.Done);
        AssertNumber(TRUE, call.Result);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WalkingWithinATileDoesNotCancelIt()
    {
        var (r, user, target) = Setup(1002);
        DoAfterCall call = Begin(r, user, target);
        r.Go(user, 127, 0, East, 1); // 85 subunits: inside the tile
        r.Go(user, -127, 0, East, 1);
        r.Go(user, 0, 0, East, 25);
        Assert.True(call.Done);
        AssertNumber(TRUE, call.Result);
        Assert.Equal((5, 5, 1), r.LocOf(user));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrossingCancelsItWithFalseOnTheNextPollOfTheTimedActions()
    {
        var (r, user, target) = Setup(1003);
        DoAfterCall call = Begin(r, user, target, seconds: 5);
        long crossing = -1;
        for (int i = 0; i < 6 && !call.Done; i++)
        {
            r.Go(user, 127, 0, East, 1);
            if (crossing < 0 && r.LocOf(user).X == 6)
                crossing = r.World.Tick;
        }
        Assert.True(crossing > 0, "the user did not cross");
        Assert.True(call.Done, "the do_after was not cancelled by the crossing");
        AssertNumber(FALSE, call.Result);
        // cancelled in the Moved of the crossing's Move, noticed by the polling one tick later (move-chain 8)
        Assert.InRange(call.DoneTick - crossing, 0, 1);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrossingIsIgnoredByAnActionThatIgnoresTheUsersLocation()
    {
        // tg's get_up is such an action: IGNORE_USER_LOC_CHANGE
        var (r, user, target) = Setup(1004);
        DoAfterCall call = Begin(r, user, target, seconds: 1, flags: IGNORE_USER_LOC_CHANGE | IGNORE_TARGET_LOC_CHANGE);
        r.Go(user, 127, 0, East, 6);
        Assert.Equal((7, 5, 1), r.LocOf(user));
        r.Go(user, 0, 0, East, 20);
        Assert.True(call.Done);
        AssertNumber(TRUE, call.Result);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMoveByTgCancelsItAndItsSlideDoesNot()
    {
        // a shove is a Move: the cancel is delivered by its Moved, once; the slide the bridge plays raises no event of its own
        var (r, user, target) = Setup(1005);
        DoAfterCall call = Begin(r, user, target, seconds: 5);
        r.Go(user, 0, 0, East, 3);
        Assert.False(call.Done);
        r.Command(() => Waiting.NoSleep(user.Move(locate(6, 5, 1), EAST)));
        r.Go(user, 0, 0, East, 4);
        Assert.True(call.Done);
        AssertNumber(FALSE, call.Result);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AForceMoveToTheSameLocDoesNotCancelIt()
    {
        var (r, user, target) = Setup(1006);
        DoAfterCall call = Begin(r, user, target);
        r.Command(() => Waiting.NoSleep(user.forceMove(locate(5, 5, 1))));
        r.Go(user, 0, 0, East, 25);
        Assert.True(call.Done);
        AssertNumber(TRUE, call.Result);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARefusedCrossingDoesNotCancelItBecauseTheUserDidNotMove()
    {
        var (r, user, target) = Setup(1007);
        DoAfterCall call = Begin(r, user, target);
        r.In(() => On(user, COMSIG_MOVABLE_PRE_MOVE, "veto", 1, result: COMPONENT_MOVABLE_BLOCK_PRE_MOVE));
        r.Go(user, 127, 0, East, 8); // pressing into a vetoed border, tick after tick
        Assert.False(call.Done);
        r.Go(user, 0, 0, East, 20);
        Assert.True(call.Done);
        AssertNumber(TRUE, call.Result);
        Assert.Equal((5, 5, 1), r.LocOf(user));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APressAgainstAWallDoesNotCancelIt()
    {
        var (r, user, target) = Setup(1008, x: 15, y: 8);
        DoAfterCall call = Begin(r, user, target);
        r.Go(user, 127, 0, East, 12); // bumping the wall at the move delay for 0.6 s
        Assert.False(call.Done);
        r.Go(user, 0, 0, East, 20);
        AssertNumber(TRUE, call.Result);
        r.AssertNoRuntimes();
    }
}
