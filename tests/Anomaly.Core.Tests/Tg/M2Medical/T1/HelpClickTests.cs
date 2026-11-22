using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// The help click (a left click with an empty hand and combat mode off), written from tg's DM: the human's <c>attack_hand</c>
/// (human_defense.dm:119) -> the species' <c>spec_attack_hand</c> (_species.dm:996) -> <c>help</c> (:745) -> <c>help_shake_act</c>
/// (carbon_defense.dm:287-402): a shaken mob loses 6 s of stun, knockdown, unconsciousness, paralysis and immobilisation and
/// 10 s of sleep (status_procs.dm:435), stops resting and stands up at once.
/// </summary>
public class HelpClickTests
{
    private static void Help(mob_living_carbon_human helper, mob_living_carbon_human target)
        => Waiting.NoSleep(target.attack_hand(helper, Dm.list()));

    [Fact]
    public void AHelpClickOnAStandingHumanChangesNothing()
    {
        var room = new MedRoom(seed: 901, size: 12);
        var helper = room.Human(4, 4);
        var target = room.Human(5, 4);
        room.In(() =>
        {
            Help(helper, target);
            Assert.Equal(STANDING_UP, target.body_position);
            Assert.Same(locate(5, 4, 1), target.loc);
            Assert.False(Dm.truthy(target.IsKnockdown()));
            Assert.Equal(100f, target.health);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ShakingAKnockedDownHumanTakesSixSecondsOffItsKnockdownAndStaysFloored()
    {
        var room = new MedRoom(seed: 902, size: 12);
        var helper = room.Human(4, 4);
        var target = room.Human(5, 4);
        room.In(() =>
        {
            Waiting.NoSleep(target.Knockdown(10 * SECONDS));
            Assert.Equal(LYING_DOWN, target.body_position);
            Help(helper, target);
            Assert.Equal(40f, Dm.num(target.AmountKnockdown()));
            Assert.Equal(LYING_DOWN, target.body_position);
            Assert.True(HAS_TRAIT(target, TRAIT_FLOORED));
            Help(helper, target);
            // TgQuirkNegativeDurationUntilTheNextFire: AdjustKnockdown(-60) on 40 leaves -20 (status_procs.dm:131); the effect goes
            // at its next SSpriority_effects process
            Assert.Equal(-20f, Dm.num(target.AmountKnockdown()));
        });
        room.StepUntil(() => !Dm.truthy(target.IsKnockdown()), 8);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ShakingShortensEveryDisablingStatusEffectByItsOwnAmount()
    {
        var room = new MedRoom(seed: 903, size: 12);
        var helper = room.Human(4, 4);
        var target = room.Human(5, 4);
        room.In(() =>
        {
            Waiting.NoSleep(target.Stun(10 * SECONDS));
            Waiting.NoSleep(target.Immobilize(10 * SECONDS));
            Waiting.NoSleep(target.Paralyze(10 * SECONDS));
            Waiting.NoSleep(target.Unconscious(10 * SECONDS));
            Waiting.NoSleep(target.Sleeping(20 * SECONDS));
            Help(helper, target);
            // adjust_status_effects_on_shake_up (status_procs.dm:435-441): -6 s of each, -10 s of sleep
            Assert.Equal(40f, Dm.num(target.AmountStun()));
            Assert.Equal(40f, Dm.num(target.AmountParalyzed()));
            Assert.Equal(40f, Dm.num(target.AmountImmobilized()));
            Assert.Equal(40f, Dm.num(target.AmountUnconscious()));
            Assert.Equal(100f, Dm.num(target.AmountSleeping()));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ShakingARestingHumanMakesItStandUpAtOnce()
    {
        var room = new MedRoom(seed: 904, size: 12);
        var helper = room.Human(4, 4);
        var target = room.Human(5, 4);
        room.In(() =>
        {
            Waiting.NoSleep(target.toggle_resting());
            Assert.Equal(LYING_DOWN, target.body_position);
            Help(helper, target);
            // help_shake_act: set_resting(FALSE), then get_up(TRUE) for a mob that is down but not floored (:392-395)
            Assert.Equal(FALSE, target.resting);
            Assert.Equal(STANDING_UP, target.body_position);
            Assert.True(Dm.truthy(target.density));
        });
        room.Step(2 * MedRoom.TicksPerSecond); // the get_up do_after that set_resting started ends without a second change
        room.In(() => Assert.Equal(STANDING_UP, target.body_position));
        room.AssertNoRuntimes();
    }
}
