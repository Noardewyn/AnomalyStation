using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// Resting, getting up and the body position of a real human, written from tg's DM: <c>toggle_resting</c> and
/// <c>set_resting</c> (living.dm:670-708), <c>get_up</c> with its 1 s <c>do_after</c> (:715-729), <c>set_lying_down</c>,
/// <c>on_lying_down</c>, <c>on_standing_up</c> (:731-756), <c>set_body_position</c> (:2714), <c>set_lying_angle</c> (:2317),
/// <c>on_floored_start</c> and <c>on_floored_end</c> (:2730-2740).
/// </summary>
public class RestAndBodyPositionTests
{
    private const int Tps = MedRoom.TicksPerSecond;

    [Fact]
    public void RestingLaysDownAtOnceOnItsSide()
    {
        var room = new MedRoom(seed: 501);
        var human = room.Human(4, 4);
        var rec = room.Record(human, life: false, breath: false, death: false, stat: false, position: true);
        float standing = room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        room.In(() =>
        {
            Waiting.NoSleep(human.toggle_resting());
            Assert.Equal(TRUE, human.resting);
            // set_resting(TRUE) on a standing mob: set_lying_down() -> set_body_position(LYING_DOWN) (:696-698)
            Assert.Equal(LYING_DOWN, human.body_position);
            // carbon.dm:1131-1142 on_lying_down: lying_angle_on_lying_down picks LYING_ANGLE_EAST or LYING_ANGLE_WEST (90 or 270)
            Assert.True(human.lying_angle == 90f || human.lying_angle == 270f, $"lying_angle {human.lying_angle}");
            Assert.False(Dm.truthy(human.density)); // update_density from TRAIT_UNDENSE
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_UNDENSE, LYING_DOWN_TRAIT));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_UI_BLOCKED, LYING_DOWN_TRAIT));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_PULL_BLOCKED, LYING_DOWN_TRAIT));
            Assert.False(HAS_TRAIT(human, TRAIT_FLOORED));
            Assert.Equal(3.8f, human.layer);
            Assert.True(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.Equal(standing + 4f, Dm.num(human.cached_multiplicative_slowdown), 3);
            // a resting human can still move (crawl): MOBILITY_MOVE and MOBILITY_STAND are kept
            Assert.True(Dm.truthy(Dm.bitand(human.mobility_flags, MOBILITY_MOVE)));
            Assert.True(Dm.truthy(Dm.bitand(human.mobility_flags, MOBILITY_STAND)));
        });
        room.Step(3 * Tps);
        room.In(() => Assert.Equal(LYING_DOWN, human.body_position)); // resting keeps it down
        Assert.Equal(new[] { (float)LYING_DOWN }, rec.Positions.Select(p => Dm.num(p.NewPosition)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void GettingUpTakesOneSecondAndRestoresEverything()
    {
        var room = new MedRoom(seed: 502);
        var human = room.Human(4, 4);
        float standing = room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        room.In(() => Waiting.NoSleep(human.toggle_resting()));
        var rec = room.Record(human, life: false, breath: false, death: false, stat: false, position: true);
        room.In(() =>
        {
            Waiting.NoSleep(human.toggle_resting()); // set_resting(FALSE): get_up(FALSE) starts the do_after (:700-704, :715)
            Assert.Equal(FALSE, human.resting);
            Assert.Equal(LYING_DOWN, human.body_position);
        });
        int ticks = room.StepUntil(() => human.body_position == STANDING_UP, 2 * Tps);
        Assert.InRange(ticks, Tps, Tps + 2); // do_after(src, 1 SECONDS)
        room.In(() =>
        {
            Assert.True(Dm.truthy(human.density));
            Assert.Equal(0f, human.lying_angle);
            Assert.False(HAS_TRAIT(human, TRAIT_UNDENSE));
            Assert.False(HAS_TRAIT(human, TRAIT_UI_BLOCKED));
            Assert.False(HAS_TRAIT(human, TRAIT_PULL_BLOCKED));
            Assert.NotEqual(3.8f, human.layer);
            Assert.False(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.Equal(standing, Dm.num(human.cached_multiplicative_slowdown), 3);
        });
        Assert.Equal(new[] { (float)STANDING_UP }, rec.Positions.Select(p => Dm.num(p.NewPosition)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RestingAgainWhileGettingUpCancelsTheDoAfter()
    {
        var room = new MedRoom(seed: 503);
        var human = room.Human(4, 4);
        room.In(() => Waiting.NoSleep(human.toggle_resting()));
        room.In(() => Waiting.NoSleep(human.toggle_resting())); // getting up
        room.Step(Tps / 2);
        room.In(() => Waiting.NoSleep(human.toggle_resting())); // rest_checks_callback: resting -> the do_after fails (:731-734)
        room.Step(3 * Tps);
        room.In(() =>
        {
            Assert.Equal(TRUE, human.resting);
            Assert.Equal(LYING_DOWN, human.body_position);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnInstantGetUpStandsAtOnce()
    {
        var room = new MedRoom(seed: 504);
        var human = room.Human(4, 4);
        room.In(() => Waiting.NoSleep(human.toggle_resting()));
        room.In(() =>
        {
            // set_resting(FALSE, silent, instant = TRUE): get_up(TRUE) skips the do_after (:715-728)
            Waiting.NoSleep(human.set_resting(FALSE, TRUE, TRUE));
            Assert.Equal(STANDING_UP, human.body_position);
            Assert.Equal(FALSE, human.resting);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WhileFlooredRestingStaysDownAndStandingUpWaitsForTheFloorToEnd()
    {
        var room = new MedRoom(seed: 505);
        var gets = room.Human(4, 4);
        var stays = room.Human(5, 5);
        room.In(() =>
        {
            Waiting.NoSleep(gets.Knockdown(2 * SECONDS));
            Waiting.NoSleep(stays.Knockdown(2 * SECONDS));
            // resting while lying already only sets the flag (:679-683); resting off while floored waits (:703-707)
            Waiting.NoSleep(stays.set_resting(TRUE));
            Assert.Equal(TRUE, stays.resting);
            Assert.Equal(LYING_DOWN, stays.body_position);
            Assert.Equal(FALSE, gets.resting);
            // get_up refuses while floored (:727), even when instant
            gets.get_up(TRUE);
            Assert.Equal(LYING_DOWN, gets.body_position);
            Waiting.NoSleep(gets.set_resting(FALSE, TRUE, TRUE));
            Assert.Equal(LYING_DOWN, gets.body_position);
        });
        room.StepUntil(() => gets.body_position == STANDING_UP, 4 * Tps); // on_floored_end -> get_up() when not resting
        room.In(() =>
        {
            Assert.Equal(LYING_DOWN, stays.body_position); // resting: stays down after the floor ends
            Assert.False(HAS_TRAIT(stays, TRAIT_FLOORED));
            Assert.Equal(TRUE, stays.resting);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ForcedStandingKeepsTheMobUpEvenWhenKnockedDownOrResting()
    {
        var room = new MedRoom(seed: 506);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            ADD_TRAIT(human, TRAIT_FORCED_STANDING, "t1");
            // on_forced_standing_trait_gain: stand and no angle (init_signals.dm:160-165)
            Assert.Equal(STANDING_UP, human.body_position);
            // on_floored_trait_gain returns for a forced standing mob: no lying (:150-152)
            Waiting.NoSleep(human.Knockdown(5 * SECONDS));
            Assert.Equal(STANDING_UP, human.body_position);
            Assert.True(HAS_TRAIT(human, TRAIT_FLOORED));
            Assert.True(Dm.truthy(Dm.bitand(human.mobility_flags, MOBILITY_STAND))); // the flag is only lost when it goes down
            Waiting.NoSleep(human.SetKnockdown(0));
            // set_resting(TRUE) on a forced standing mob: "will lay down as soon as you are able to", no lying (:685-688)
            Waiting.NoSleep(human.set_resting(TRUE));
            Assert.Equal(TRUE, human.resting);
            Assert.Equal(STANDING_UP, human.body_position);
            // losing the trait lays a resting mob down (init_signals.dm:167-172)
            REMOVE_TRAIT(human, TRAIT_FORCED_STANDING, "t1");
            Assert.Equal(LYING_DOWN, human.body_position);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SetBodyPositionAndSetLyingAngleReturnTheOldValueOrNullWhenNothingChanges()
    {
        var room = new MedRoom(seed: 507);
        var human = room.Human(4, 4);
        var rec = room.Record(human, life: false, breath: false, death: false, stat: false, position: true);
        room.In(() =>
        {
            // living.dm:2714-2727: returns null when the position is the same, else the old one; sends COMSIG_LIVING_SET_BODY_POSITION
            Assert.Null(Waiting.NoSleep(human.set_body_position(STANDING_UP)));
            Assert.Equal((float)STANDING_UP, Dm.num(Waiting.NoSleep(human.set_body_position(LYING_DOWN))));
            Assert.Null(Waiting.NoSleep(human.set_body_position(LYING_DOWN)));
            Assert.Equal((float)LYING_DOWN, Dm.num(Waiting.NoSleep(human.set_body_position(STANDING_UP))));
            // carbon on_lying_down turned it to 90 or 270 and standing up keeps the angle (get_up resets it with set_lying_angle(0))
            Assert.True(human.lying_angle == 90f || human.lying_angle == 270f);
            Waiting.NoSleep(human.set_lying_angle(0));
            // set_lying_angle (:2317-2326): the old angle, null for the same; updates the transform on a change
            Assert.Null(Waiting.NoSleep(human.set_lying_angle(0)));
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(human.set_lying_angle(90))));
            Assert.Equal(90f, human.lying_angle);
            Assert.Null(Waiting.NoSleep(human.set_lying_angle(90)));
            Assert.Equal(90f, Dm.num(Waiting.NoSleep(human.set_lying_angle(270))));
            Assert.Equal(270f, Dm.num(Waiting.NoSleep(human.set_lying_angle(0))));
            // a mob that cannot lie down refuses (:2716-2717)
            human.mobility_flags = Dm.bitand(human.mobility_flags, ~MOBILITY_LIEDOWN);
            Assert.Null(Waiting.NoSleep(human.set_body_position(LYING_DOWN)));
            Assert.Equal(STANDING_UP, human.body_position);
        });
        Assert.Equal(new[] { (float)LYING_DOWN, (float)STANDING_UP }, rec.Positions.Select(p => Dm.num(p.NewPosition)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ARestlessMobThatCannotRestIgnoresSetResting()
    {
        var room = new MedRoom(seed: 508);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            human.mobility_flags = Dm.bitand(human.mobility_flags, ~MOBILITY_REST);
            Waiting.NoSleep(human.toggle_resting());
            Assert.Equal(FALSE, human.resting);
            Assert.Equal(STANDING_UP, human.body_position);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadHumanLiesDownAndStaysDownAfterARevive()
    {
        var room = new MedRoom(seed: 509);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.death());
            Assert.Equal(DEAD, human.stat);
            Assert.Equal(LYING_DOWN, human.body_position); // set_stat(DEAD): TRAIT_FLOORED (living.dm:2556-2563) -> on_floored_start
            Assert.False(Dm.truthy(human.density));
        });
        room.AssertNoRuntimes();
    }
}
