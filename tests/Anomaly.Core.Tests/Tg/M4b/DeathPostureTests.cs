using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// The owner's play test (2026-10-14): a player who died in a vacuum or in a fire stood on their feet. A body on a space turf is
/// weightless (TRAIT_MOVE_FLOATING), and update_offsets and update_transform arm a TIMER_UNIQUE timer for TRAIT_NO_FLOATING_ANIM
/// then: its hash asked REF() of the GLOBAL_PROC text, which threw and aborted set_body_position after it had set LYING_DOWN
/// and before the lying angle, so the body lay with angle 0 and no turned picture. A floored, resting or dead weightless body
/// lies with its angle and its turned matrix, as one on a floor does, and gets up again.
/// </summary>
public class DeathPostureTests
{
    private const int Second = 20;

    private static mob_living_carbon_human BobInSpace(T2Room r)
    {
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.Step(Second);
        r.In(() => DevCommands.Breach(locate(bob.x, bob.y, 1) as turf)); // the floor under bob becomes space
        r.Step(Second);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_MOVE_FLOATING)), "space is weightless");
        return bob;
    }

    private static void AssertLyingTurned(T2Room r, mob_living_carbon_human bob)
    {
        Assert.Equal(LYING_DOWN, r.In(() => bob.body_position));
        float angle = r.In(() => bob.lying_angle);
        Assert.True(angle == LYING_ANGLE_EAST || angle == LYING_ANGLE_WEST, "the lying angle: " + angle);
        Assert.Equal(angle, r.In(() => bob.lying_prev));
        matrix m = r.In(() => (matrix)bob.transform);
        Assert.NotEqual(0f, Math.Abs(m.b)); // the picture is turned a quarter: its x axis runs up the screen
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AKnockedDownBodyInSpaceLiesWithItsAngle()
    {
        var r = new T2Room(4430);
        mob_living_carbon_human bob = BobInSpace(r);
        r.In(() => Waiting.NoSleep(bob.Knockdown(5 * Second * 20f)));
        r.Step(2);
        AssertLyingTurned(r, bob);
    }

    [Fact]
    public void ADeadBodyInSpaceLiesWithItsAngle()
    {
        var r = new T2Room(4431);
        mob_living_carbon_human bob = BobInSpace(r);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(400)));
        r.Step(Second);
        Assert.Equal(DEAD, r.In(() => bob.stat));
        AssertLyingTurned(r, bob);
    }

    [Fact]
    public void ARestingBodyInSpaceLiesAndGetsUp()
    {
        var r = new T2Room(4432);
        mob_living_carbon_human bob = BobInSpace(r);
        r.In(() => Waiting.NoSleep(bob.set_resting(TRUE)));
        r.Step(2);
        AssertLyingTurned(r, bob);

        r.In(() => Waiting.NoSleep(bob.set_resting(FALSE)));
        r.Step(2 * Second); // tg's get_up takes a second
        Assert.Equal(STANDING_UP, r.In(() => bob.body_position));
        Assert.Equal(0f, r.In(() => bob.lying_angle));
        r.AssertNoRuntimes();
    }
}
