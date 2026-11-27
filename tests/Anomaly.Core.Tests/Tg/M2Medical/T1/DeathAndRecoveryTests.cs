using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// Death, revival and healing of a real human, written from tg's DM: <c>/mob/living/death</c> (death.dm:224-262), the carbon's
/// (carbon/death.dm:1-13) and the human's (human/death.dm:25-35), <c>revive</c> (living.dm:888-934), the carbon's
/// <c>revive</c> and <c>heal_and_revive</c> (carbon.dm:652-675), <c>fully_heal</c> (living.dm:977-1026, carbon.dm:676-714) and
/// the organs' decay after death (_organ.dm:160-172, <c>handle_organs</c> carbon/life.dm:503).
/// </summary>
public class DeathAndRecoveryTests
{
    private const int Fire = 2 * MedRoom.TicksPerSecond;
    private const int Tps = MedRoom.TicksPerSecond;

    [Fact]
    public void DeathMarksTheHumanDeadOnceAndStopsTheHeart()
    {
        var room = new MedRoom(seed: 601);
        var human = room.Human(4, 4);
        var rec = room.Record(human, life: false, breath: false);
        room.Step(1);
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_oxy_loss(10));
            Assert.True(Dm.truthy(Waiting.NoSleep(human.death())));
            Assert.Equal(DEAD, human.stat);
            Assert.Equal(room.Time, human.timeofdeath);
            Assert.True(Dm.@in(human, GLOB.dead_mob_list));
            Assert.False(Dm.@in(human, GLOB.alive_mob_list));
            // human/death.dm:30-33: the heart stops and its beat goes
            var heart = (obj_item_organ_heart)human.get_organ_slot(ORGAN_SLOT_HEART);
            Assert.False(Dm.truthy(heart.is_beating()));
            Assert.Equal(BEAT_NONE, heart.beat);
            Assert.True(Dm.truthy(human.undergoing_cardiac_arrest()));
            // living.dm:2556-2563: DEAF, FLOORED, HANDS_BLOCKED, INCAPACITATED, KNOCKEDOUT from the stat
            foreach (string t in new[] { TRAIT_DEAF, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED, TRAIT_KNOCKEDOUT })
                Assert.True(HAS_TRAIT_FROM(human, t, STAT_TRAIT), t);
            // a second death does nothing and does not signal again (death.dm:225-226, carbon/death.dm:2, human/death.dm:26)
            Assert.False(Dm.truthy(Waiting.NoSleep(human.death())));
        });
        Assert.Single(rec.Deaths);
        Assert.Single(rec.StatChanges);
        Assert.Equal(DEAD, Dm.num(rec.StatChanges[0].NewStat));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadHumanDoesNotBreatheOrSuffocateFurther()
    {
        var room = new MedRoom(seed: 602);
        var human = room.Human(4, 4);
        room.In(() => Waiting.NoSleep(human.death()));
        var rec = room.Record(human);
        room.Step(20 * Fire);
        Assert.True(rec.Lives.Count >= 19);
        Assert.Empty(rec.Breaths); // living/life.dm:57-60: handle_breathing only while stat != DEAD
        room.In(() =>
        {
            Assert.Equal(0f, MedRoom.Oxy(human));
            Assert.Equal(DEAD, human.stat);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadHumansOrgansDecayEveryLifeAndTheHeartFailsAboutSixMinutesAfterDeath()
    {
        var room = new MedRoom(seed: 603);
        var human = room.Human(4, 4);
        room.In(() => Waiting.NoSleep(human.death()));
        var rec = room.Record(human, breath: false, death: false, stat: false);
        // _organ.dm:160-167: apply_organ_damage(decay_factor * maxHealth * seconds_per_tick * air_temperature_factor) per Life, the
        // air temperature factor min((bodytemperature - T0C) / 20, 1) = 1 for a body at 36.6 C; decay_factor from the organ files:
        // STANDARD_ORGAN_DECAY = 111 / 100000: heart x2.5, liver x1, stomach x1.15, lungs x0.9, eyes, ears, appendix x1, brain x0.5
        // (the eyes have half the usual maxHealth, 50: eyes/_eyes.dm:12; the brain twice, BRAIN_DAMAGE_DEATH 200: brain_item.dm:18)
        var expectedPerLife = new Dictionary<string, float>
        {
            [ORGAN_SLOT_HEART] = 2.5f * 0.00111f * 100f * 2f,
            [ORGAN_SLOT_LIVER] = 0.00111f * 100f * 2f,
            [ORGAN_SLOT_STOMACH] = 1.15f * 0.00111f * 100f * 2f,
            [ORGAN_SLOT_LUNGS] = 0.9f * 0.00111f * 100f * 2f,
            [ORGAN_SLOT_EYES] = 0.00111f * 50f * 2f,
            [ORGAN_SLOT_EARS] = 0.00111f * 100f * 2f,
            [ORGAN_SLOT_APPENDIX] = 0.00111f * 100f * 2f,
            [ORGAN_SLOT_BRAIN] = 0.5f * 0.00111f * 200f * 2f,
        };
        room.StepUntil(() => rec.Lives.Count == 30, 31 * Fire);
        room.In(() =>
        {
            foreach ((string slot, float perLife) in expectedPerLife)
            {
                var organ = (obj_item_organ)human.get_organ_slot(slot);
                Assert.True(Math.Abs(30f * perLife - (float)organ.damage) < 0.05f, $"{slot}: {organ.damage} after 30 Lives, expected {30f * perLife}");
            }
        });
        // the heart: 0.555 a Life, 99.9 after 180 Lives (not failing), over 100 at the 181st (failing)
        room.StepUntil(() => rec.Lives.Count == 180, 160 * Fire);
        room.In(() =>
        {
            var heart = (obj_item_organ_heart)human.get_organ_slot(ORGAN_SLOT_HEART);
            Assert.Equal(180 * 0.555f, (float)heart.damage, 1);
            Assert.False(Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FAILING)));
        });
        room.Step(Fire);
        room.In(() =>
        {
            var heart = (obj_item_organ_heart)human.get_organ_slot(ORGAN_SLOT_HEART);
            Assert.Equal(100f, (float)heart.damage);
            Assert.True(Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FAILING)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ReviveNeedsHealthAboveTheDeadThresholdAndWithoutHealFlagsLeavesTheHeartStopped()
    {
        var room = new MedRoom(seed: 604);
        var human = room.Human(4, 4);
        var rec = room.Record(human, life: false, breath: false, death: false, stat: true);
        var revives = room.In(() =>
        {
            var r = Dm.@new<datum_test_t1_recorder>();
            r.WatchRevive(human);
            return r;
        });
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_tox_loss(200));
            Assert.Equal(DEAD, human.stat);
            Assert.Equal(-100f, human.health);
            // can_be_revived: health > dead_threshold (living.dm:1037-1043): -100 is not above it
            Assert.False(Dm.truthy(human.can_be_revived()));
            Assert.False(Dm.truthy(Waiting.NoSleep(human.revive())));
            Assert.Equal(DEAD, human.stat);

            Waiting.NoSleep(human.adjust_tox_loss(-10)); // health -90: the stat does not change by itself (update_stat skips the dead)
            Assert.Equal(DEAD, human.stat);
            Assert.True(Dm.truthy(human.can_be_revived()));
            Assert.True(Dm.truthy(Waiting.NoSleep(human.revive())));
            // living.dm:907-915: set_stat(HARD_CRIT), updatehealth -> health -90 is hard crit
            Assert.Equal(HARD_CRIT, human.stat);
            Assert.True(Dm.@in(human, GLOB.alive_mob_list));
            Assert.False(Dm.@in(human, GLOB.dead_mob_list));
            // revive() alone does not restart the heart (only heal_and_revive and the organ regeneration do)
            Assert.True(Dm.truthy(human.undergoing_cardiac_arrest()));
        });
        // the signal ends every revive() call, a refused one too (living.dm:932)
        Assert.Equal(2, revives.Revives.Count);
        Assert.All(revives.Revives, r => Assert.Equal(0f, r.Flags));
        Assert.Equal(new[] { DEAD, HARD_CRIT }.Select(x => (float)x).ToArray(), rec.StatChanges.Select(c => Dm.num(c.NewStat)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ReviveWithHealAllHealsEverythingAndRestartsTheHeart()
    {
        var room = new MedRoom(seed: 605);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.take_overall_damage(300)); // dead at -150
            Waiting.NoSleep(human.adjust_tox_loss(30));
            Waiting.NoSleep(human.adjust_oxy_loss(20));
            Assert.Equal(DEAD, human.stat);
            human.set_blood_volume(300f);
            Assert.False(Dm.truthy(human.can_be_revived()));
            // revive(HEAL_ALL): fully_heal first (living.dm:903-905), which makes health 100, then the revival (:907-915)
            Assert.True(Dm.truthy(Waiting.NoSleep(human.revive(HEAL_ALL))));
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(100f, human.health);
            Assert.Equal(0f, MedRoom.Brute(human) + MedRoom.Burn(human) + MedRoom.Tox(human) + MedRoom.Oxy(human) + MedRoom.Stam(human));
            Assert.Equal(560f, human.blood_volume); // HEAL_BLOOD: restore_blood
            Assert.False(Dm.truthy(human.undergoing_cardiac_arrest())); // regenerate_organs: set_heartattack(FALSE) (_organ.dm:358)
            foreach (var organ in Dm.each_cast<obj_item_organ>(human.organs))
                Assert.Equal(0f, (float)organ.damage);
            foreach (string t in new[] { TRAIT_DEAF, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED, TRAIT_KNOCKEDOUT })
                Assert.False(HAS_TRAIT(human, t), t);
            Assert.True(Dm.@in(human, GLOB.alive_mob_list));
        });
        // the human stands up again: the floor ended, get_up's second
        room.StepUntil(() => human.body_position == STANDING_UP, 3 * Tps);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HealAndReviveHealsToTheTargetAddsExcessHealingAndRestartsTheHeart()
    {
        var room = new MedRoom(seed: 606);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            room.World.Rng.Reseed(606, 1); // the brute's wound rolls, pinned: a bleeding cut would take blood before the revive
            MedRoom.Apply(human, 100, BRUTE, BODY_ZONE_CHEST);
            Waiting.NoSleep(human.adjust_tox_loss(200, forced: TRUE));
            Assert.Equal(DEAD, human.stat);
            // living.dm:936-974: damage down to heal_to (50) of each type, then revive(excess_healing = 10): carbon revive adds
            // 2 * 10 blood (carbon.dm:654-655) and takes 10 of oxygen (-> 0) and of toxins (forced)
            bool alive = Dm.truthy(Waiting.NoSleep(human.heal_and_revive(50)));
            Assert.True(alive);
            Assert.Equal(50f, MedRoom.Brute(human), 2);
            Assert.Equal(40f, MedRoom.Tox(human), 2);
            Assert.Equal(10f, human.health, 2); // 100 - 50 brute on the chest - 40 toxins
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(580f, human.blood_volume);
            Assert.False(Dm.truthy(human.undergoing_cardiac_arrest())); // carbon.dm:674: set_heartattack(FALSE)
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HealAndReviveRefusesAHumanWithoutAHeartOrLungsOrLiver()
    {
        var room = new MedRoom(seed: 607);
        foreach (string slot in new[] { ORGAN_SLOT_HEART, ORGAN_SLOT_LUNGS, ORGAN_SLOT_LIVER })
        {
            var human = room.Human(4, 4);
            room.In(() =>
            {
                var organ = (obj_item_organ)human.get_organ_slot(slot);
                qdel(organ);
                Waiting.NoSleep(human.adjust_tox_loss(10));
                // carbon.dm:652-665: no heart, no lungs (unless NOBREATH) and no liver cannot be healed and revived
                Assert.False(Dm.truthy(Waiting.NoSleep(human.heal_and_revive(50))), slot);
                Assert.Equal(10f, MedRoom.Tox(human), 2);
            });
        }
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ReviveIsRefusedWithoutABrain()
    {
        var room = new MedRoom(seed: 608);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.death());
            var brain = (obj_item_organ)human.get_organ_slot(ORGAN_SLOT_BRAIN);
            qdel(brain);
            // carbon.dm:663-668 can_be_revived: no brain (and no TRAIT_BRAINLESS_CARBON) is not revivable
            Assert.False(Dm.truthy(human.can_be_revived()));
            Assert.False(Dm.truthy(Waiting.NoSleep(human.revive())));
            Assert.Equal(DEAD, human.stat);
        });
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("brute")]
    [InlineData("burn")]
    [InlineData("tox")]
    [InlineData("oxy")]
    [InlineData("stamina")]
    public void FullyHealWithOneDamageFlagHealsOnlyThatType(string which)
    {
        var room = new MedRoom(seed: 609);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.apply_damages(brute: 10, burn: 10, tox: 10, oxy: 10, stamina: 10, def_zone: BODY_ZONE_CHEST));
            float flag = which switch { "brute" => HEAL_BRUTE, "burn" => HEAL_BURN, "tox" => HEAL_TOX, "oxy" => HEAL_OXY, _ => HEAL_STAM };
            Waiting.NoSleep(human.fully_heal(flag));
            // living.dm:977-985 heals each type by its own flag
            Assert.Equal(which == "brute" ? 0f : 10f, MedRoom.Brute(human), 2);
            Assert.Equal(which == "burn" ? 0f : 10f, MedRoom.Burn(human), 2);
            Assert.Equal(which == "tox" ? 0f : 10f, MedRoom.Tox(human), 2);
            Assert.Equal(which == "oxy" ? 0f : 10f, MedRoom.Oxy(human), 2);
            Assert.Equal(which == "stamina" ? 0f : 10f, MedRoom.Stam(human), 2);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void FullyHealRemovesOnlyTheStatusEffectsOfItsFlagAndAlwaysResetsLosebreath()
    {
        var room = new MedRoom(seed: 610);
        var human = room.Human(4, 4);
        var other = room.Human(6, 6);
        var rec = room.In(() =>
        {
            var r = Dm.@new<datum_test_t1_recorder>();
            r.WatchPostHeal(human);
            return r;
        });
        room.In(() =>
        {
            Waiting.NoSleep(human.Knockdown(5 * SECONDS, daze_amount: 5 * SECONDS));
            Waiting.NoSleep(human.adjust_staggered(STAGGERED_SLOWDOWN_LENGTH));
            human.losebreath = 3f;
            // HEAL_CC_STATUS (the incapacitating effects' heal_flag_necessary) clears the knockdown, not stagger and daze, whose
            // flag is the default HEAL_STATUS (_status_effect.dm:34)
            Waiting.NoSleep(human.fully_heal(HEAL_CC_STATUS));
            Assert.False(Dm.truthy(human.IsKnockdown()));
            Assert.True(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_staggered))));
            Assert.True(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_dazed))));
            Assert.Equal(0f, human.losebreath); // living.dm:990: losebreath = 0 whatever the flags
            // HEAL_STATUS clears staggered and dazed
            Waiting.NoSleep(human.fully_heal(HEAL_STATUS));
            Assert.False(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_staggered))));
            Assert.False(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_dazed))));

            // stun and paralysis are cleared by HEAL_CC_STATUS as well, and a heal of another flag leaves them
            Waiting.NoSleep(other.Stun(5 * SECONDS));
            Waiting.NoSleep(other.Paralyze(5 * SECONDS));
            Waiting.NoSleep(other.fully_heal(HEAL_DAMAGE));
            Assert.True(Dm.truthy(other.IsStun()));
            Assert.True(Dm.truthy(other.IsParalyzed()));
            Waiting.NoSleep(other.fully_heal(HEAL_CC_STATUS));
            Assert.False(Dm.truthy(other.IsStun()));
            Assert.False(Dm.truthy(other.IsParalyzed()));
        });
        Assert.Equal(new[] { (float)HEAL_CC_STATUS, (float)HEAL_STATUS }, rec.PostHeals.Select(p => p.Flags).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void FullyHealDoesNotReviveTheDeadAndHealAllWithoutAdminKeepsRestraintsFlagOut()
    {
        var room = new MedRoom(seed: 611);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.take_overall_damage(300));
            Assert.Equal(DEAD, human.stat);
            Waiting.NoSleep(human.fully_heal(HEAL_ALL));
            // updatehealth runs but update_stat skips the dead (carbon.dm:612): healing does not resurrect
            Assert.Equal(100f, human.health);
            Assert.Equal(DEAD, human.stat);
            Assert.True(Dm.@in(human, GLOB.dead_mob_list));
            // HEAL_ALL is ~(HEAL_ADMIN | HEAL_RESTRAINTS) (mobs.dm:1035)
            Assert.False(Dm.truthy(Dm.bitand(HEAL_ALL, HEAL_ADMIN)));
            Assert.False(Dm.truthy(Dm.bitand(HEAL_ALL, HEAL_RESTRAINTS)));
            Assert.True(Dm.truthy(Dm.bitand(HEAL_ALL, HEAL_DAMAGE)));
        });
        room.AssertNoRuntimes();
    }
}
