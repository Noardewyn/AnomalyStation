using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// Status effects on a real human, written from tg's DM: the stun family (status_procs.dm:21-380, debuffs.dm:8-239) runs on
/// SSpriority_effects (wait 0.2 s, priority_effects.dm:5), whose process() takes seconds_per_tick off the effect's duration
/// (_status_effect.dm:101-122); staggered and dazed run on SSfastprocess (staggered.dm:5-100). Durations are in deciseconds
/// and fall in 0.2 s steps (2 ds, 4 ticks).
/// </summary>
public class StatusEffectTests
{
    private const int Tps = MedRoom.TicksPerSecond;

    private static bool Has(mob_living m, Type effect) => Dm.truthy(m.has_status_effect(effect));

    private static void AssertMobility(mob_living m, int lost)
    {
        // init_signals.dm:127-222: IMMOBILIZED -> MOBILITY_MOVE, FLOORED -> MOBILITY_STAND, HANDS_BLOCKED -> USE|PICKUP|STORAGE,
        // UI_BLOCKED -> MOBILITY_UI, PULL_BLOCKED -> MOBILITY_PULL
        foreach (int flag in new[] { MOBILITY_MOVE, MOBILITY_STAND, MOBILITY_USE, MOBILITY_UI, MOBILITY_PULL })
        {
            bool has = Dm.truthy(Dm.bitand(m.mobility_flags, flag));
            Assert.Equal((lost & flag) == 0, has);
        }
    }

    [Fact]
    public void StunLastsExactlyItsDurationInPointTwoSecondSteps()
    {
        var room = new MedRoom(seed: 301);
        var human = room.Human();
        long start = room.Tick;
        room.In(() =>
        {
            Waiting.NoSleep(human.Stun(2 * SECONDS));
            Assert.True(Dm.truthy(human.IsStun()));
            Assert.Equal(20f, Dm.num(human.AmountStun()));
            // debuffs.dm:56-60: INCAPACITATED, IMMOBILIZED and HANDS_BLOCKED from TRAIT_STATUS_EFFECT("stun")
            string source = TRAIT_STATUS_EFFECT("stun");
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_INCAPACITATED, source));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_IMMOBILIZED, source));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_HANDS_BLOCKED, source));
            Assert.False(HAS_TRAIT(human, TRAIT_FLOORED));
            Assert.Equal(STANDING_UP, human.body_position);
            // no one is knocked down by a stun; the mobility flags lose MOVE, USE|PICKUP|STORAGE, UI and PULL (incapacitated adds
            // UI_BLOCKED and PULL_BLOCKED, debuffs/init_signals.dm:206)
            AssertMobility(human, MOBILITY_MOVE | MOBILITY_USE | MOBILITY_UI | MOBILITY_PULL);
        });
        room.Step(Tps); // 1 s: 4 or 5 fires of 0.2 s each take 2 ds (M3 P3: by the phase of SSpriority_effects' random first
                        // fire, master.dm:653, which moved when SSmachines joined the Master)
        room.In(() => Assert.InRange(Dm.num(human.AmountStun()), 8f, 12f));
        int toEnd = room.StepUntil(() => !Dm.truthy(human.IsStun()), 2 * Tps);
        long total = room.Tick - start;
        // 10 fires of 2 ds: the effect leaves on the tenth SSpriority_effects fire, 36 to 40 ticks after it started, or up to
        // 4 more while the subsystem's random first fire after the boot is still pending (master.dm:653)
        Assert.InRange(total, 36, 44);
        room.In(() =>
        {
            Assert.False(HAS_TRAIT(human, TRAIT_INCAPACITATED));
            AssertMobility(human, 0);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StunDurationsFollowStunSetStunAdjustStun()
    {
        var room = new MedRoom(seed: 302);
        var human = room.Human();
        room.In(() =>
        {
            Waiting.NoSleep(human.Stun(2 * SECONDS));
            // Stun: "Can't go below remaining duration" -> max(amount, duration) (status_procs.dm:44-46)
            Waiting.NoSleep(human.Stun(1 * SECONDS));
            Assert.Equal(20f, Dm.num(human.AmountStun()));
            Waiting.NoSleep(human.Stun(3 * SECONDS));
            Assert.Equal(30f, Dm.num(human.AmountStun()));
            // AdjustStun adds (:73-75), SetStun sets (:63-65)
            Waiting.NoSleep(human.AdjustStun(1 * SECONDS));
            Assert.Equal(40f, Dm.num(human.AmountStun()));
            Waiting.NoSleep(human.SetStun(5));
            Assert.Equal(5f, Dm.num(human.AmountStun()));
            // zero or less never applies a new stun (`else if(amount > 0)`)
            Waiting.NoSleep(human.SetStun(0));
            Assert.False(Dm.truthy(human.IsStun()));
            Waiting.NoSleep(human.Stun(0));
            Waiting.NoSleep(human.AdjustStun(-10));
            Assert.False(Dm.truthy(human.IsStun()));
            Assert.Equal(0f, Dm.num(human.AmountStun()));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KnockdownFloorsTheHumanAndTheGetUpTakesAnotherSecond()
    {
        var room = new MedRoom(seed: 303);
        var human = room.Human(4, 4);
        var rec = room.Record(human, life: false, breath: false, death: false, stat: false, position: true);
        float standingSlowdown = room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        long start = room.Tick;
        room.In(() =>
        {
            Waiting.NoSleep(human.Knockdown(2 * SECONDS));
            Assert.True(Dm.truthy(human.IsKnockdown()));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_FLOORED, TRAIT_STATUS_EFFECT("knockdown")));
            // living.dm:2730-2735 on_floored_start: lie down with a lying angle of 90 or 270; living.dm:737-748 on_lying_down
            Assert.Equal(LYING_DOWN, human.body_position);
            Assert.True(human.lying_angle == 90f || human.lying_angle == 270f, $"lying_angle {human.lying_angle}");
            Assert.False(Dm.truthy(human.density));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_UNDENSE, LYING_DOWN_TRAIT));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_UI_BLOCKED, LYING_DOWN_TRAIT));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_PULL_BLOCKED, LYING_DOWN_TRAIT));
            Assert.Equal(3.8f, human.layer); // LYING_MOB_LAYER
            // a floored human faces north or south ("We are and look helpless", living.dm:742)
            Assert.True(Dm.truthy(Dm.bitand(human.dir, Dm.bitor(NORTH, SOUTH))));
            // the crawl modifier (carbon.dm:415-424)
            Assert.True(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.Equal(standingSlowdown + 4f, Dm.num(human.cached_multiplicative_slowdown), 3);
            // FLOORED loses MOBILITY_STAND; lying down adds UI_BLOCKED and PULL_BLOCKED (on_lying_down)
            AssertMobility(human, MOBILITY_STAND | MOBILITY_UI | MOBILITY_PULL);
        });
        // the knockdown (20 ds) ends on the 10th fire, then get_up's do_after takes 1 s (20 ticks): standing 56 to 62 ticks after
        int toFloorEnd = room.StepUntil(() => !HAS_TRAIT(human, TRAIT_FLOORED), 3 * Tps);
        // the upper bound moves a tick with each live subsystem the Master staggers before SSpriority_effects' first fire
        // (master.dm:658's rand per subsystem; M7's SSsun and SSradiation): the status effect's own timing is unchanged
        Assert.InRange(toFloorEnd, 36, 42);
        room.In(() =>
        {
            Assert.Equal(LYING_DOWN, human.body_position); // the do_after is running
            Assert.False(Dm.truthy(human.density));
            AssertMobility(human, 0 | MOBILITY_UI | MOBILITY_PULL); // lying down still blocks the UI and pulling until it stands
        });
        int toStand = room.StepUntil(() => human.body_position == STANDING_UP, 2 * Tps);
        // gaps MS1: the human's sanity has moved off its start, so tg's neutral sanity gives the high sanity actionspeed (-0.1,
        // mood.dm:656, actionspeed/modifiers/mood.dm:5): the get-up's do_after takes 0.9 s
        room.In(() => Assert.NotNull(human.has_actionspeed_modifier(typeof(datum_actionspeed_modifier_high_sanity))));
        Assert.InRange(toStand, Tps * 9 / 10, Tps * 9 / 10 + 2);
        room.In(() =>
        {
            Assert.True(Dm.truthy(human.density));
            Assert.Equal(0f, human.lying_angle);
            Assert.NotEqual(3.8f, human.layer); // on_standing_up: layer back to initial(layer) (living.dm:752-755)
            Assert.False(HAS_TRAIT(human, TRAIT_UNDENSE));
            Assert.False(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.Equal(standingSlowdown, Dm.num(human.cached_multiplicative_slowdown), 3);
            AssertMobility(human, 0);
        });
        // the two parts above: the floor's end at 36 to 42 ticks, the 0.9 s get-up (gaps MS1) after it (gaps CI1: the bound was 56,
        // from before MS1's high sanity; the circuits' subsystems move the Master's stagger, so the floor ends at 37 on this seed)
        Assert.InRange(room.Tick - start, 36 + Tps * 9 / 10, 42 + Tps * 9 / 10 + 2);
        // two body position changes: down at once, up at the end
        Assert.Equal(new[] { (float)LYING_DOWN, (float)STANDING_UP }, rec.Positions.Select(p => Dm.num(p.NewPosition)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ParalyzeIsStunPlusKnockdownTraitsAndImmobilizeOnlyHoldsTheLegs()
    {
        var room = new MedRoom(seed: 304);
        var human = room.Human();
        var other = room.Human(5, 5);
        room.In(() =>
        {
            Waiting.NoSleep(human.Paralyze(2 * SECONDS));
            Assert.True(Dm.truthy(human.IsParalyzed()));
            string source = TRAIT_STATUS_EFFECT("paralyzed");
            foreach (string t in new[] { TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED })
                Assert.True(HAS_TRAIT_FROM(human, t, source), t);
            Assert.Equal(LYING_DOWN, human.body_position);
            Assert.Equal(20f, Dm.num(human.AmountParalyzed()));

            Waiting.NoSleep(other.Immobilize(2 * SECONDS));
            Assert.True(Dm.truthy(other.IsImmobilized()));
            Assert.True(HAS_TRAIT_FROM(other, TRAIT_IMMOBILIZED, TRAIT_STATUS_EFFECT("immobilized")));
            Assert.False(HAS_TRAIT(other, TRAIT_FLOORED));
            Assert.False(HAS_TRAIT(other, TRAIT_INCAPACITATED));
            Assert.False(HAS_TRAIT(other, TRAIT_HANDS_BLOCKED));
            Assert.Equal(STANDING_UP, other.body_position);
            AssertMobility(other, MOBILITY_MOVE);
        });
        room.StepUntil(() => !Dm.truthy(other.IsImmobilized()), 3 * Tps);
        room.StepUntil(() => !Dm.truthy(human.IsParalyzed()), 3 * Tps);
        room.StepUntil(() => human.body_position == STANDING_UP, 3 * Tps);
        room.In(() => AssertMobility(other, 0));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StunAndKnockdownRespectTheirStatusFlagsAndStunImmunity()
    {
        var room = new MedRoom(seed: 305);
        var human = room.Human();
        room.In(() =>
        {
            // check_stun_immunity (status_procs.dm:12-30): a missing flag, TRAIT_STUNIMMUNE and godmode block; ignore_canstun forces
            // a stun through everything but godmode
            ADD_TRAIT(human, TRAIT_STUNIMMUNE, "t1");
            Waiting.NoSleep(human.Stun(1 * SECONDS));
            Waiting.NoSleep(human.Knockdown(1 * SECONDS));
            Assert.False(Dm.truthy(human.IsStun()));
            Assert.False(Dm.truthy(human.IsKnockdown()));
            Waiting.NoSleep(human.Stun(1 * SECONDS, ignore_canstun: TRUE));
            Assert.True(Dm.truthy(human.IsStun()));
            Waiting.NoSleep(human.SetStun(0, ignore_canstun: TRUE));
            REMOVE_TRAIT(human, TRAIT_STUNIMMUNE, "t1");

            ADD_TRAIT(human, TRAIT_GODMODE, "t1");
            Waiting.NoSleep(human.Stun(1 * SECONDS, ignore_canstun: TRUE));
            Assert.False(Dm.truthy(human.IsStun()));
            REMOVE_TRAIT(human, TRAIT_GODMODE, "t1");

            // a knockdown only needs CANKNOCKDOWN (not CANSTUN)
            human.status_flags = Dm.bitand(human.status_flags, ~CANSTUN);
            Waiting.NoSleep(human.Knockdown(1 * SECONDS));
            Assert.True(Dm.truthy(human.IsKnockdown()));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void UnconsciousAndSleepingKnockTheHumanOutAndOverTogether()
    {
        var room = new MedRoom(seed: 306);
        var human = room.Human();
        var sleeper = room.Human(5, 5);
        float firesAtStart = 0;
        room.In(() =>
        {
            firesAtStart = SSpriority_effects.times_fired;
            Waiting.NoSleep(human.Unconscious(3 * SECONDS));
            Assert.True(Dm.truthy(human.IsUnconscious()));
            // debuffs.dm:132-143: TRAIT_KNOCKEDOUT from the status effect, which starts /datum/status_effect/knocked_out
            // (debuffs.dm:146-185): FLOORED, IMMOBILIZED, INCAPACITATED, HANDS_BLOCKED, blindness
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT("unconscious")));
            Assert.True(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_knocked_out))));
            foreach (string t in new[] { TRAIT_HANDS_BLOCKED, TRAIT_IMMOBILIZED, TRAIT_INCAPACITATED, TRAIT_FLOORED })
                Assert.True(HAS_TRAIT_FROM(human, t, TRAIT_STATUS_EFFECT("is_knocked_out")), t);
            Assert.True(Dm.truthy(human.is_blind_from(TRAIT_STATUS_EFFECT("is_knocked_out"))));
            Assert.Equal(LYING_DOWN, human.body_position);
            Assert.True(IS_UNCONSCIOUS(human));

            Waiting.NoSleep(sleeper.Sleeping(3 * SECONDS));
            Assert.True(Dm.truthy(sleeper.IsSleeping()));
            Assert.True(HAS_TRAIT_FROM(sleeper, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT("sleeping")));
            Assert.Equal(LYING_DOWN, sleeper.body_position);
        });
        // 15 fires of 2 ds (_status_effect.dm:107-124): the effect leaves on the fifteenth SSpriority_effects fire, 57 to 60 ticks
        // after it started, or up to 4 more while the subsystem's random first fire after the boot is still pending
        // (master.dm:621; M5 slice 1 moved that phase: SSburning joined the Master and draws a start offset of its own in that loop)
        room.StepUntil(() => !Dm.truthy(human.IsUnconscious()), 3 * Tps + 4);
        room.In(() =>
        {
            Assert.Equal(15f, SSpriority_effects.times_fired - firesAtStart);
            Assert.False(HAS_TRAIT(human, TRAIT_KNOCKEDOUT));
            Assert.False(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_knocked_out))));
            Assert.False(Dm.truthy(human.is_blind_from(TRAIT_STATUS_EFFECT("is_knocked_out"))));
        });
        room.StepUntil(() => human.body_position == STANDING_UP, 3 * Tps);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SleepingHealsDamagedOrgansAndMinorWoundsOnEveryTwoSecondTick()
    {
        // debuffs.dm:249-331: every 2 s tick, sleep_quality = 0.1 for a neutral sanity (the mood, gaps MS1: debuffs.dm:258; lit turf,
        // eyes not covered, not deaf, no bed); a carbon's damaged organs heal healing_factor * sleep_quality * 5 * maxHealth each tick;
        // brute, burn (0.4 * quality * seconds) and toxins (0.2 * quality * seconds) heal while health / maxHealth > 0.8
        var room = new MedRoom(seed: 314);
        var sleeper = room.Human(4, 4);
        var control = room.Human(5, 5);
        room.In(() =>
        {
            foreach (var h in new[] { sleeper, control })
            {
                Waiting.NoSleep(h.adjust_organ_loss(ORGAN_SLOT_LIVER, 20f));
                MedRoom.Apply(h, 5, BRUTE, BODY_ZONE_CHEST);
                Waiting.NoSleep(h.adjust_tox_loss(5));
            }
            Waiting.NoSleep(sleeper.Sleeping(10 * SECONDS));
        });
        room.Step(12 * Tps);
        room.In(() =>
        {
            Assert.False(Dm.truthy(sleeper.IsSleeping()));
            var l1 = (obj_item_organ_liver)sleeper.get_organ_slot(ORGAN_SLOT_LIVER);
            var l2 = (obj_item_organ_liver)control.get_organ_slot(ORGAN_SLOT_LIVER);
            // five sleep ticks of 0.0005 * 0.1 * 5 * 100 = 0.025 of liver damage on top of what Life heals in both
            Assert.Equal(0.125f, (float)l2.damage - (float)l1.damage, 2);
            // 5 ticks of 0.4 * 0.1 * 2 = 0.08 brute (the bodypart rounds to 0.1: 0.1 off each tick) and 0.2 * 0.1 * 2 = 0.04
            // toxins more than the control; health 90 / 100 is above 0.8
            Assert.Equal(0.5f, MedRoom.Brute(control) - MedRoom.Brute(sleeper), 1);
            Assert.Equal(0.2f, MedRoom.Tox(control) - MedRoom.Tox(sleeper), 2);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void IncapacitateOnlyBlocksActionsAndBlindnessAndDeathClearNothingElse()
    {
        var room = new MedRoom(seed: 307);
        var human = room.Human();
        room.In(() =>
        {
            Waiting.NoSleep(human.incapacitate(2 * SECONDS));
            Assert.Equal(20f, Dm.num(human.amount_incapacitated()));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_INCAPACITATED, TRAIT_STATUS_EFFECT("incapacitated")));
            Assert.False(HAS_TRAIT(human, TRAIT_FLOORED));
            Assert.False(HAS_TRAIT(human, TRAIT_IMMOBILIZED));
            Assert.Equal(STANDING_UP, human.body_position);
            // on_incapacitated_trait_gain adds UI_BLOCKED and PULL_BLOCKED from TRAIT_INCAPACITATED
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_UI_BLOCKED, TRAIT_INCAPACITATED));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_PULL_BLOCKED, TRAIT_INCAPACITATED));
            Assert.True(Dm.truthy(human.incapacitated));
        });
        room.StepUntil(() => Dm.num(human.amount_incapacitated()) == 0, 3 * Tps);
        room.In(() =>
        {
            Assert.False(HAS_TRAIT(human, TRAIT_UI_BLOCKED));
            Assert.False(Dm.truthy(human.incapacitated));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ASecondSourceOfATraitKeepsTheMobilityLostUntilTheLastEnds()
    {
        var room = new MedRoom(seed: 308);
        var human = room.Human();
        room.In(() =>
        {
            Waiting.NoSleep(human.Stun(1 * SECONDS));
            Waiting.NoSleep(human.Immobilize(3 * SECONDS));
            AssertMobility(human, MOBILITY_MOVE | MOBILITY_USE | MOBILITY_UI | MOBILITY_PULL);
        });
        room.StepUntil(() => !Dm.truthy(human.IsStun()), 2 * Tps);
        room.In(() =>
        {
            // the stun is gone, the immobilize holds: MOVE is still lost, hands and the UI are back
            Assert.True(HAS_TRAIT(human, TRAIT_IMMOBILIZED));
            AssertMobility(human, MOBILITY_MOVE);
        });
        room.StepUntil(() => !Dm.truthy(human.IsImmobilized()), 3 * Tps);
        room.In(() => AssertMobility(human, 0));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StaggerSlowsByPointEightFiveAndIsCappedAtTenSecondsAndStacksUpToTheCap()
    {
        var room = new MedRoom(seed: 309);
        var human = room.Human();
        float before = room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH));
            Assert.True(Has(human, typeof(datum_status_effect_staggered)));
            Assert.Equal(30f, Dm.num(human.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))));
            Assert.Equal(before + 0.85f, Dm.num(human.cached_multiplicative_slowdown), 3);
            // adjust_timed_status_effect (status_procs.dm:681-720): adds, but never past max_duration (10 s)
            Waiting.NoSleep(human.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS));
            Assert.Equal(60f, Dm.num(human.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))));
            Waiting.NoSleep(human.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS));
            Waiting.NoSleep(human.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS));
            Assert.Equal(100f, Dm.num(human.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))));
            Waiting.NoSleep(human.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS));
            Assert.Equal(100f, Dm.num(human.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))));
            // the slowdown is one modifier, not one per stack
            Assert.Equal(before + 0.85f, Dm.num(human.cached_multiplicative_slowdown), 3);
            // a negative duration takes time off; reaching 0 removes the effect at once
            Waiting.NoSleep(human.adjust_staggered(-1000));
            Assert.False(Has(human, typeof(datum_status_effect_staggered)));
            Assert.Equal(before, Dm.num(human.cached_multiplicative_slowdown), 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StaggerEndsAfterThreeSecondsInFastProcessSteps()
    {
        var room = new MedRoom(seed: 310);
        var human = room.Human();
        long start = room.Tick;
        room.In(() => Waiting.NoSleep(human.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH)));
        room.StepUntil(() => !Has(human, typeof(datum_status_effect_staggered)), 5 * Tps);
        // SSfastprocess (SS_POST_FIRE_TIMING from SSprocessing, processing.dm:6) takes 2 ds off the duration per 0.2 s fire: 30 ds are
        // 15 fires of about 4 ticks, each fire later by at most a tick
        Assert.InRange(room.Tick - start, 56, 66);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadHumanCannotBeStaggeredAndDeathClearsStagger()
    {
        var room = new MedRoom(seed: 311);
        var human = room.Human();
        var dead = room.Human(5, 5);
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH));
            Assert.True(Has(human, typeof(datum_status_effect_staggered)));
            Waiting.NoSleep(human.death());
            Assert.Equal(DEAD, human.stat);
            Assert.False(Has(human, typeof(datum_status_effect_staggered))); // staggered.dm:33-37 clear_staggered on COMSIG_LIVING_DEATH

            Waiting.NoSleep(dead.death());
            Waiting.NoSleep(dead.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH)); // on_apply: FALSE for the dead
            Assert.False(Has(dead, typeof(datum_status_effect_staggered)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void DazedLastsThreeSecondsAndGoesWhenTheMobStandsOrIsStunned()
    {
        var room = new MedRoom(seed: 312);
        var human = room.Human();
        var other = room.Human(5, 5);
        var third = room.Human(6, 6);
        room.In(() =>
        {
            // Knockdown(amount, daze_amount): status_procs.dm:96-108 applies dazed after the knockdown
            Waiting.NoSleep(human.Knockdown(2 * SECONDS, daze_amount: 3 * SECONDS));
            Assert.True(Has(human, typeof(datum_status_effect_dazed)));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_DAZED, TRAIT_STATUS_EFFECT("dazed")));
            Assert.Equal(30f, Dm.num(human.get_timed_status_effect_duration(typeof(datum_status_effect_dazed))));
            // a stun (amount > 0) clears the daze (staggered.dm:94-100), clear_daze on COMSIG_LIVING_STATUS_STUN/PARALYZE/IMMOBILIZE
            Waiting.NoSleep(other.Knockdown(2 * SECONDS, daze_amount: 3 * SECONDS));
            Assert.True(Has(other, typeof(datum_status_effect_dazed)));
            Waiting.NoSleep(other.Stun(1 * SECONDS));
            Assert.False(Has(other, typeof(datum_status_effect_dazed)));
            Assert.False(HAS_TRAIT(other, TRAIT_DAZED));
            // no daze for the immobilized (on_apply: TRAIT_IMMOBILIZED) and none for the dead
            Waiting.NoSleep(third.Immobilize(5 * SECONDS));
            Waiting.NoSleep(third.Knockdown(2 * SECONDS, daze_amount: 3 * SECONDS));
            Assert.False(Has(third, typeof(datum_status_effect_dazed)));
        });
        // the daze leaves when the human stands (clear_daze_on_stand): set_body_position(STANDING_UP) at about 3 s
        room.StepUntil(() => human.body_position == STANDING_UP, 4 * Tps);
        room.In(() => Assert.False(Has(human, typeof(datum_status_effect_dazed))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobKnockedDownWhileCrawlingTurnsToTheSideItMovesTo()
    {
        var room = new MedRoom(seed: 313);
        var human = room.Human(5, 5);
        room.In(() => Waiting.NoSleep(human.Knockdown(10 * SECONDS)));
        room.In(() =>
        {
            Assert.True(human.lying_angle == 90f || human.lying_angle == 270f);
            // living.dm:1047-1049, :1084-1092: Move on a mob with a lying angle sets it from the direction: EAST 90, WEST 270,
            // north and south leave it
            Waiting.NoSleep(human.Move(locate(6, 5, 1), EAST));
            Assert.Equal(90f, human.lying_angle);
            Waiting.NoSleep(human.Move(locate(5, 5, 1), WEST));
            Assert.Equal(270f, human.lying_angle);
            Waiting.NoSleep(human.Move(locate(5, 6, 1), NORTH));
            Assert.Equal(270f, human.lying_angle);
            Waiting.NoSleep(human.Move(locate(6, 6, 1), EAST));
            Assert.Equal(90f, human.lying_angle);
            Waiting.NoSleep(human.Move(locate(6, 5, 1), SOUTH));
            Assert.Equal(90f, human.lying_angle);
        });
        room.AssertNoRuntimes();
    }
}
