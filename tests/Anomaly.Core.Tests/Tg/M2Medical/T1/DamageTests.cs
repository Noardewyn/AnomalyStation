using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// Damage on a real human, written from tg's DM: <c>/mob/living/proc/apply_damage</c> (damage_procs.dm:30),
/// <c>/mob/living/carbon/apply_damage</c> (carbon/damage_procs.dm:1), the bodypart's <c>receive_damage</c>
/// (_bodyparts.dm:706), <c>/mob/living/carbon/updatehealth</c> (carbon.dm:426) and <c>update_stat</c> (carbon.dm:610).
/// The human has six bodyparts: chest and head (max_damage 200, body_damage_coeff 1) and four limbs (max 50, coeff 0.75).
/// </summary>
public class DamageTests
{
    private static readonly string[] Zones = { BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG };

    private static float Coeff(string zone) => zone == BODY_ZONE_CHEST || zone == BODY_ZONE_HEAD ? 1f : 0.75f;

    private static float Max(string zone) => zone == BODY_ZONE_CHEST || zone == BODY_ZONE_HEAD ? 200f : 50f;

    [Fact]
    public void AHumanStartsWithSixBodypartsAndTgsLimbNumbers()
    {
        var room = new MedRoom(seed: 101);
        var human = room.Human();
        room.In(() =>
        {
            // limb numbers: LIMB_MAX_HP_CORE 200 / LIMB_MAX_HP_DEFAULT 50, LIMB_BODY_DAMAGE_COEFFICIENT_TOTAL 1 / _DEFAULT 0.75
            // (__DEFINES/bodyparts.dm:9-25, surgery/bodyparts/default_parts.dm)
            foreach (string zone in Zones)
            {
                var part = MedRoom.Part(human, zone);
                Assert.NotNull(part);
                Assert.Equal(Max(zone), part.max_damage);
                Assert.Equal(Coeff(zone), part.body_damage_coeff);
                Assert.Equal(0f, part.brute_dam);
                Assert.Equal(0f, part.burn_dam);
            }
            Assert.Equal(100f, human.maxHealth);
            Assert.Equal(100f, human.health);
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(0f, human.crit_threshold);
            Assert.Equal(-30f, human.hardcrit_threshold);
            Assert.Equal(-100f, human.dead_threshold);
        });
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(BODY_ZONE_CHEST, BRUTE)]
    [InlineData(BODY_ZONE_HEAD, BRUTE)]
    [InlineData(BODY_ZONE_L_ARM, BRUTE)]
    [InlineData(BODY_ZONE_R_ARM, BRUTE)]
    [InlineData(BODY_ZONE_L_LEG, BRUTE)]
    [InlineData(BODY_ZONE_R_LEG, BRUTE)]
    [InlineData(BODY_ZONE_CHEST, BURN)]
    [InlineData(BODY_ZONE_HEAD, BURN)]
    [InlineData(BODY_ZONE_L_ARM, BURN)]
    [InlineData(BODY_ZONE_R_ARM, BURN)]
    [InlineData(BODY_ZONE_L_LEG, BURN)]
    [InlineData(BODY_ZONE_R_LEG, BURN)]
    public void DamageToAZoneHurtsOnlyThatBodypartAndHealthCountsTheLimbMultiplier(string zone, string type)
    {
        var room = new MedRoom(seed: 102);
        var human = room.Human();
        room.In(() =>
        {
            // carbon/damage_procs.dm:20-24: a named zone is used as given (no 80% roll), get_bodypart(zone)
            object dealt = MedRoom.Apply(human, 20, type, zone);
            Assert.Equal(20f, Dm.num(dealt)); // living apply_damage returns damage_dealt: the part's new damage minus its old
            foreach (string z in Zones)
            {
                var part = MedRoom.Part(human, z);
                float expectedBrute = z == zone && type == BRUTE ? 20f : 0f;
                float expectedBurn = z == zone && type == BURN ? 20f : 0f;
                Assert.Equal(expectedBrute, part.brute_dam);
                Assert.Equal(expectedBurn, part.burn_dam);
            }
            // get_brute_loss / get_fire_loss add the bodyparts (carbon/damage_procs.dm:30-40), without the limb multiplier
            Assert.Equal(type == BRUTE ? 20f : 0f, MedRoom.Brute(human));
            Assert.Equal(type == BURN ? 20f : 0f, MedRoom.Burn(human));
            // updatehealth (carbon.dm:432-436): health = maxHealth - sum(damage * body_damage_coeff)
            Assert.Equal(100f - 20f * Coeff(zone), human.health, 3);
            Assert.Equal(STABLE, human.stat);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ToxinAndOxygenDamageCountOneToOneAndStaminaNotAtAll()
    {
        var room = new MedRoom(seed: 103);
        var human = room.Human();
        room.In(() =>
        {
            Assert.Equal(25f, Dm.num(MedRoom.Apply(human, 25, TOX)));
            Assert.Equal(75f, human.health);
            Assert.Equal(10f, Dm.num(MedRoom.Apply(human, 10, OXY)));
            Assert.Equal(65f, human.health);
            Assert.Equal(25f, MedRoom.Tox(human));
            Assert.Equal(10f, MedRoom.Oxy(human));
            // stamina damage is not part of updatehealth's sum (carbon.dm:436)
            Assert.Equal(30f, Dm.num(MedRoom.Apply(human, 30, STAMINA)));
            Assert.Equal(30f, MedRoom.Stam(human));
            Assert.Equal(65f, human.health);
            Assert.Equal(STABLE, human.stat);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HealthIsTheSumOfEveryDamageTypeWithTheZoneMultipliers()
    {
        var room = new MedRoom(seed: 104);
        var human = room.Human();
        room.In(() =>
        {
            MedRoom.Apply(human, 10, BRUTE, BODY_ZONE_CHEST);   // -10
            MedRoom.Apply(human, 8, BURN, BODY_ZONE_L_LEG);     // -6 (0.75)
            MedRoom.Apply(human, 12, BRUTE, BODY_ZONE_R_ARM);   // -9
            MedRoom.Apply(human, 4, TOX);                        // -4
            MedRoom.Apply(human, 3, OXY);                        // -3
            Assert.Equal(100f - 10f - 6f - 9f - 4f - 3f, human.health, 3);
            Assert.Equal(22f, MedRoom.Brute(human));
            Assert.Equal(8f, MedRoom.Burn(human));
            // get_total_damage: round(brute + burn + tox + oxy, DAMAGE_PRECISION) (damage_procs.dm:168)
            Assert.Equal(22f + 8f + 4f + 3f, Dm.num(human.get_total_damage()), 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALimbTakesNoMoreThanItsMaxDamageAndTheCappedDamageIsWhatApplyDamageReturns()
    {
        var room = new MedRoom(seed: 105);
        var human = room.Human();
        room.In(() =>
        {
            // _bodyparts.dm:787-792: brute/burn are scaled to can_inflict = max_damage - get_damage()
            object dealt = MedRoom.Apply(human, 80, BRUTE, BODY_ZONE_L_ARM);
            var arm = MedRoom.Part(human, BODY_ZONE_L_ARM);
            Assert.Equal(50f, arm.brute_dam);
            Assert.Equal(50f, Dm.num(dealt));
            Assert.Equal(100f - 50f * 0.75f, human.health, 3);
            // a full limb takes nothing more (can_inflict <= 0)
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 10, BURN, BODY_ZONE_L_ARM)));
            Assert.Equal(0f, arm.burn_dam);
            // a mixed hit is scaled together: 30 brute on the right arm, then 40 burn leaves room for 20
            MedRoom.Apply(human, 30, BRUTE, BODY_ZONE_R_ARM);
            object burned = MedRoom.Apply(human, 40, BURN, BODY_ZONE_R_ARM);
            var rarm = MedRoom.Part(human, BODY_ZONE_R_ARM);
            Assert.Equal(20f, rarm.burn_dam);
            Assert.Equal(50f, rarm.get_damage());
            Assert.Equal(20f, Dm.num(burned));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void BlockedAndForcedAndNonPositiveDamageFollowApplyDamage()
    {
        var room = new MedRoom(seed: 106);
        var human = room.Human();
        room.In(() =>
        {
            // damage_procs.dm:46-51: damage_amount = damage * (100 - blocked) / 100 (blocked += damage_resistance, 0)
            Assert.Equal(10f, Dm.num(MedRoom.Apply(human, 20, BRUTE, BODY_ZONE_CHEST, blocked: 50)));
            Assert.Equal(10f, MedRoom.Part(human, BODY_ZONE_CHEST).brute_dam);
            // fully blocked: damage_amount <= 0, return 0
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 20, BRUTE, BODY_ZONE_CHEST, blocked: 100)));
            Assert.Equal(10f, MedRoom.Part(human, BODY_ZONE_CHEST).brute_dam);
            // forced skips blocked
            Assert.Equal(20f, Dm.num(MedRoom.Apply(human, 20, BRUTE, BODY_ZONE_CHEST, forced: TRUE, blocked: 100)));
            Assert.Equal(30f, MedRoom.Part(human, BODY_ZONE_CHEST).brute_dam);
            // zero and negative damage never reach the switch
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 0, BRUTE)));
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, -5, BRUTE)));
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, -5, TOX)));
            Assert.Equal(0f, MedRoom.Tox(human));
            Assert.Equal(70f, human.health);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ApplyDamageWithoutAZoneRollsEightyPercentChestAndOtherwiseTheWeightedZones()
    {
        // carbon.get_random_valid_zone (mob_helpers.dm:60-80) -> ran_zone(null, 80, limbs): prob(80) gives the chest
        // (check_zone(null)); otherwise pick_weight(chest 1, head 1, each limb 4): the chest has 80% + 20% * 1/18.
        var room = new MedRoom(seed: 107);
        var human = room.Human();
        var counts = new Dictionary<string, int>();
        const int Samples = 6000;
        room.In(() =>
        {
            foreach (string z in Zones)
                counts[z] = 0;
            for (int i = 0; i < Samples; i++)
            {
                string zone = Dm.str(human.get_random_valid_zone());
                counts[zone]++;
            }
        });
        double chest = counts[BODY_ZONE_CHEST] / (double)Samples;
        Assert.InRange(chest, 0.80, 0.83); // expected 0.8111
        Assert.InRange(counts[BODY_ZONE_HEAD] / (double)Samples, 0.003, 0.022); // expected 0.0111
        foreach (string limb in new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
            Assert.InRange(counts[limb] / (double)Samples, 0.030, 0.060); // expected 0.0444
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TakeOverallDamageSpreadsOverTheSixPartsAndBruteOneHundredFiftyIsSoftCritNotHardCrit()
    {
        // carbon/damage_procs.dm:269-302: round(brute / parts.len, 0.1) per picked part, picked at random and removed:
        // 150 / 6 = 25 on every part (scope.md 2e example 2): 25 + 25 + 4 * 25 * 0.75 = 125 of health.
        var room = new MedRoom(seed: 108);
        var human = room.Human();
        room.In(() =>
        {
            object result = Waiting.NoSleep(human.take_overall_damage(150));
            Assert.Equal(-150f, Dm.num(result)); // the net amount of damage (negative for damage)
            foreach (string z in Zones)
                Assert.Equal(25f, MedRoom.Part(human, z).brute_dam);
            Assert.Equal(-25f, human.health, 3);
            Assert.Equal(SOFT_CRIT, human.stat);
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_FLOORED, STAT_TRAIT));
            Assert.False(HAS_TRAIT(human, TRAIT_KNOCKEDOUT));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TakeOverallDamageThatKillsKillsThroughUpdatestat()
    {
        // 300 brute: 50 per part (the limbs' max_damage 50): 50 + 50 + 4 * 50 * 0.75 = 250 of health -> -150, dead (<= -100)
        var room = new MedRoom(seed: 109);
        var human = room.Human();
        room.In(() =>
        {
            Waiting.NoSleep(human.take_overall_damage(300));
            foreach (string z in Zones)
                Assert.Equal(50f, MedRoom.Part(human, z).brute_dam);
            Assert.Equal(-150f, human.health, 3);
            Assert.Equal(DEAD, human.stat);
            Assert.Equal(room.Time, human.timeofdeath);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HealOverallDamageHealsTheDamagedPartsAndReturnsTheNetHealing()
    {
        var room = new MedRoom(seed: 110);
        var human = room.Human();
        room.In(() =>
        {
            MedRoom.Apply(human, 20, BRUTE, BODY_ZONE_CHEST);
            MedRoom.Apply(human, 20, BRUTE, BODY_ZONE_L_ARM);
            MedRoom.Apply(human, 6, BURN, BODY_ZONE_HEAD);
            // carbon/damage_procs.dm:226-263: heals the damaged parts in random order until the amounts are used up
            object healed = Waiting.NoSleep(human.heal_overall_damage(30, 4));
            Assert.Equal(34f, Dm.num(healed), 3);
            Assert.Equal(10f, MedRoom.Brute(human), 3);
            Assert.Equal(2f, MedRoom.Burn(human), 3);
            // negative arguments are treated as positive (:230-231)
            object more = Waiting.NoSleep(human.heal_overall_damage(-100, -100));
            Assert.Equal(12f, Dm.num(more), 3);
            Assert.Equal(0f, MedRoom.Brute(human));
            Assert.Equal(0f, MedRoom.Burn(human));
            Assert.Equal(100f, human.health, 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TakeBodypartDamageHurtsExactlyOneDamageablePart()
    {
        var room = new MedRoom(seed: 111);
        var human = room.Human();
        room.In(() =>
        {
            object dealt = Waiting.NoSleep(human.take_bodypart_damage(8, 3));
            Assert.Equal(-11f, Dm.num(dealt), 3);
            int damaged = Dm.each_cast<obj_item_bodypart>(human.get_bodyparts()).Count(p => p.brute_dam > 0 || p.burn_dam > 0);
            Assert.Equal(1, damaged);
            Assert.Equal(8f, MedRoom.Brute(human), 3);
            Assert.Equal(3f, MedRoom.Burn(human), 3);
            // heal_bodypart_damage heals one damaged part and returns the healed amount
            object healed = Waiting.NoSleep(human.heal_bodypart_damage(5, 5));
            Assert.Equal(8f, Dm.num(healed), 3);
            Assert.Equal(3f, MedRoom.Brute(human), 3);
            Assert.Equal(0f, MedRoom.Burn(human), 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AdjustAndSetLossReturnTheDifferenceAndClampAtZeroAndTwiceMaxHealth()
    {
        var room = new MedRoom(seed: 112);
        var human = room.Human();
        room.In(() =>
        {
            // adjust_tox_loss: clamp(toxloss + amount, 0, maxHealth * 2) (damage_procs.dm:242); the return is the difference
            Assert.Equal(-60f, Dm.num(Waiting.NoSleep(human.adjust_tox_loss(60))));
            Assert.Equal(60f, MedRoom.Tox(human));
            Assert.Equal(-140f, Dm.num(Waiting.NoSleep(human.adjust_tox_loss(500)))); // 200 is the cap
            Assert.Equal(200f, MedRoom.Tox(human));
            Assert.Equal(200f, Dm.num(Waiting.NoSleep(human.adjust_tox_loss(-1000)))); // healing stops at 0
            Assert.Equal(0f, MedRoom.Tox(human));
            // set_oxy_loss returns old - new
            Assert.Equal(-40f, Dm.num(Waiting.NoSleep(human.set_oxy_loss(40))));
            Assert.Equal(30f, Dm.num(Waiting.NoSleep(human.set_oxy_loss(10))));
            Assert.Equal(10f, MedRoom.Oxy(human));
            // a zero adjust does nothing and returns 0
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(human.adjust_oxy_loss(0))));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OxygenAndBurnDamageStopAtTwiceMaxHealthToo()
    {
        var room = new MedRoom(seed: 122);
        var human = room.Human();
        room.In(() =>
        {
            // adjust_oxy_loss clamps like adjust_tox_loss: (damage_procs.dm:325), 0 to maxHealth * 2 (200)
            Assert.Equal(-200f, Dm.num(Waiting.NoSleep(human.adjust_oxy_loss(500, forced: TRUE))));
            Assert.Equal(200f, MedRoom.Oxy(human));
            Assert.Equal(-100f, human.health);
            Assert.Equal(DEAD, human.stat);
            Waiting.NoSleep(human.set_oxy_loss(0));
            // the carbon's passout test is `>=` OXYLOSS_PASSOUT_THRESHOLD (50): carbon_defense.dm:507
            Waiting.NoSleep(human.set_oxy_loss(49.9f));
            Assert.False(HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT));
            Waiting.NoSleep(human.set_oxy_loss(50));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT));
            Waiting.NoSleep(human.set_oxy_loss(49.9f));
            Assert.False(HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RanZoneKeepsTheGivenZoneEightyPercentOfTheTime()
    {
        // mob_helpers.dm:35-41: prob(80) -> check_zone(zone), else pick_weight(head 1, chest 1, each limb 4)
        var room = new MedRoom(seed: 123);
        int head = 0;
        const int Samples = 4000;
        room.In(() =>
        {
            for (int i = 0; i < Samples; i++)
                if (Dm.str(ran_zone(BODY_ZONE_HEAD)) == BODY_ZONE_HEAD)
                    head++;
        });
        Assert.InRange(head / (double)Samples, 0.79, 0.83); // 0.8 + 0.2 / 18 = 0.811
        // a precise zone is converted by check_zone (mob_helpers.dm:11-31)
        room.In(() =>
        {
            Assert.Equal(BODY_ZONE_L_ARM, Dm.str(check_zone(BODY_ZONE_PRECISE_L_HAND)));
            Assert.Equal(BODY_ZONE_HEAD, Dm.str(check_zone(BODY_ZONE_PRECISE_EYES)));
            Assert.Equal(BODY_ZONE_R_LEG, Dm.str(check_zone(BODY_ZONE_PRECISE_R_FOOT)));
            Assert.Equal(BODY_ZONE_CHEST, Dm.str(check_zone(BODY_ZONE_PRECISE_GROIN)));
            Assert.Equal(BODY_ZONE_CHEST, Dm.str(check_zone(null)));
        });
    }

    [Fact]
    public void AdjustBruteAndBurnLossGoThroughTheBodypartsAndSetBruteLossAdjustsByTheDifference()
    {
        var room = new MedRoom(seed: 113);
        var human = room.Human();
        room.In(() =>
        {
            // carbon adjust_brute_loss(amount > 0) = take_overall_damage(brute = amount) (carbon/damage_procs.dm:48-52)
            object dealt = Waiting.NoSleep(human.adjust_brute_loss(60));
            Assert.Equal(-60f, Dm.num(dealt), 3);
            Assert.Equal(60f, MedRoom.Brute(human), 3);
            foreach (string z in Zones)
                Assert.Equal(10f, MedRoom.Part(human, z).brute_dam, 3);
            // set_brute_loss(amount): adjust_brute_loss(amount - current) (:62-69)
            Waiting.NoSleep(human.set_brute_loss(24));
            Assert.Equal(24f, MedRoom.Brute(human), 3);
            Waiting.NoSleep(human.set_brute_loss(0));
            Assert.Equal(0f, MedRoom.Brute(human), 3);
            Assert.Equal(100f, human.health, 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void DamageMovesStatAtExactlyZeroMinusThirtyAndMinusOneHundred()
    {
        var room = new MedRoom(seed: 114);
        var human = room.Human();
        void Set(float chestBrute)
            => room.In(() =>
            {
                Waiting.NoSleep(human.set_brute_loss(0));
                MedRoom.Apply(human, chestBrute, BRUTE, BODY_ZONE_CHEST);
            });
        // carbon.dm:610-625: dead at health <= dead_threshold, then hard crit at <= hardcrit_threshold, soft crit at <= crit_threshold
        Set(99.9f);
        room.In(() => { Assert.Equal(0.1f, human.health, 3); Assert.Equal(STABLE, human.stat); });
        Set(100f);
        room.In(() => { Assert.Equal(0f, human.health, 3); Assert.Equal(SOFT_CRIT, human.stat); });
        Set(129.9f);
        room.In(() => { Assert.Equal(-29.9f, human.health, 3); Assert.Equal(SOFT_CRIT, human.stat); });
        Set(130f);
        room.In(() => { Assert.Equal(-30f, human.health, 3); Assert.Equal(HARD_CRIT, human.stat); });
        Set(199.9f);
        room.In(() => { Assert.Equal(-99.9f, human.health, 3); Assert.Equal(HARD_CRIT, human.stat); });
        // 200 brute on the chest is its max_damage: the health is -100, dead
        Set(200f);
        room.In(() => { Assert.Equal(-100f, human.health, 3); Assert.Equal(DEAD, human.stat); });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheStatTraitsFollowTheStat()
    {
        var room = new MedRoom(seed: 115);
        var human = room.Human();
        room.In(() =>
        {
            MedRoom.Apply(human, 100, BRUTE, BODY_ZONE_CHEST);
            // living.dm:2531-2566: soft crit adds FLOORED, HANDS_BLOCKED, INCAPACITATED, FORCE_WHISPER from STAT_TRAIT
            Assert.Equal(SOFT_CRIT, human.stat);
            foreach (string t in new[] { TRAIT_FLOORED, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED, TRAIT_FORCE_WHISPER })
                Assert.True(HAS_TRAIT_FROM(human, t, STAT_TRAIT), t);
            Assert.False(HAS_TRAIT(human, TRAIT_KNOCKEDOUT));
            Assert.False(HAS_TRAIT(human, TRAIT_DEAF));
            MedRoom.Apply(human, 30, BRUTE, BODY_ZONE_CHEST);
            // hard crit: DEAF, FLOORED, HANDS_BLOCKED, INCAPACITATED, KNOCKEDOUT (not FORCE_WHISPER)
            Assert.Equal(HARD_CRIT, human.stat);
            foreach (string t in new[] { TRAIT_DEAF, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED, TRAIT_KNOCKEDOUT })
                Assert.True(HAS_TRAIT_FROM(human, t, STAT_TRAIT), t);
            Assert.False(HAS_TRAIT_FROM(human, TRAIT_FORCE_WHISPER, STAT_TRAIT));
            // healing back to stable removes them all
            Waiting.NoSleep(human.heal_overall_damage(500, 500));
            Assert.Equal(STABLE, human.stat);
            foreach (string t in new[] { TRAIT_DEAF, TRAIT_FLOORED, TRAIT_HANDS_BLOCKED, TRAIT_INCAPACITATED, TRAIT_KNOCKEDOUT, TRAIT_FORCE_WHISPER })
                Assert.False(HAS_TRAIT_FROM(human, t, STAT_TRAIT), t);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobInSoftCritIsSlowedByTwoAndHardCritKeepsOnlyTheCrawl()
    {
        var room = new MedRoom(seed: 116);
        var human = room.Human();
        room.In(() =>
        {
            float baseSlowdown = Dm.num(human.cached_multiplicative_slowdown);
            MedRoom.Apply(human, 100, BRUTE, BODY_ZONE_CHEST);
            Assert.Equal(SOFT_CRIT, human.stat);
            // carbon.dm:439-442: SOFTCRIT_ADD_SLOWDOWN (2) while stat == SOFT_CRIT; floored means lying: the crawl modifier,
            // CRAWLING_ADD_SLOWDOWN 4 (carbon.dm:415-424); human.dm:960-966: health_deficiency 100 / 75 as damage_slowdown
            Assert.True(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_softcrit))));
            Assert.True(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling))));
            Assert.Equal(baseSlowdown + 2f + 4f + 100f / 75f, Dm.num(human.cached_multiplicative_slowdown), 3);
            // hard crit: the soft crit modifier is removed again; deficiency is 130
            MedRoom.Apply(human, 30, BRUTE, BODY_ZONE_CHEST);
            Assert.Equal(HARD_CRIT, human.stat);
            Assert.False(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_softcrit))));
            Assert.Equal(baseSlowdown + 4f + 130f / 75f, Dm.num(human.cached_multiplicative_slowdown), 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanBelowSixtyHealthIsSlowedByTheDeficiencyOverSeventyFive()
    {
        var room = new MedRoom(seed: 121);
        var human = room.Human();
        room.In(() =>
        {
            float baseSlowdown = Dm.num(human.cached_multiplicative_slowdown);
            // human.dm:962-966: deficiency = max(maxHealth - health, staminaloss); from 40 on, slowdown deficiency / 75
            MedRoom.Apply(human, 39.9f, BRUTE, BODY_ZONE_CHEST);
            Assert.False(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_damage_slowdown))));
            Assert.Equal(baseSlowdown, Dm.num(human.cached_multiplicative_slowdown), 3);
            MedRoom.Apply(human, 0.1f, BRUTE, BODY_ZONE_CHEST);
            Assert.True(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_damage_slowdown))));
            Assert.Equal(baseSlowdown + 40f / 75f, Dm.num(human.cached_multiplicative_slowdown), 3);
            MedRoom.Apply(human, 20, BRUTE, BODY_ZONE_CHEST);
            Assert.Equal(baseSlowdown + 60f / 75f, Dm.num(human.cached_multiplicative_slowdown), 3);
            Waiting.NoSleep(human.heal_overall_damage(100, 100));
            Assert.False(Dm.truthy(human.has_movespeed_modifier(typeof(datum_movespeed_modifier_damage_slowdown))));
            Assert.Equal(baseSlowdown, Dm.num(human.cached_multiplicative_slowdown), 3);
            // stamina counts as deficiency too (the max with the missing health)
            MedRoom.Apply(human, 50, STAMINA);
            Assert.Equal(baseSlowdown + 50f / 75f, Dm.num(human.cached_multiplicative_slowdown), 3);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void GodmodeIgnoresDamageButForcedAdjustsStillLandWithoutAnUpdate()
    {
        var room = new MedRoom(seed: 117);
        var human = room.Human();
        room.In(() =>
        {
            ADD_TRAIT(human, TRAIT_GODMODE, "t1");
            // can_adjust_*_loss refuse under godmode unless forced (damage_procs.dm:178, :254); the bodypart refuses too
            // (_bodyparts.dm:719-721); updatehealth and update_stat return at once (carbon.dm:427, :611)
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 50, BRUTE, BODY_ZONE_CHEST)));
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 50, BURN)));
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 50, TOX)));
            Assert.Equal(0f, Dm.num(MedRoom.Apply(human, 50, OXY)));
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(human.adjust_brute_loss(50))));
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(human.take_overall_damage(60))));
            Assert.Equal(0f, MedRoom.Brute(human) + MedRoom.Burn(human) + MedRoom.Tox(human) + MedRoom.Oxy(human));
            Assert.Equal(100f, human.health);
            Assert.Equal(STABLE, human.stat);
            // forced damage is not refused (can_adjust_tox_loss: !forced && GODMODE), and updatehealth is skipped under godmode
            Assert.Equal(-40f, Dm.num(Waiting.NoSleep(human.adjust_tox_loss(40, forced: TRUE))));
            Assert.Equal(40f, MedRoom.Tox(human));
            Assert.Equal(100f, human.health); // TgQuirkGodmodeKeepsHealthUntilTheTraitIsRemoved
            REMOVE_TRAIT(human, TRAIT_GODMODE, "t1");
            Waiting.NoSleep(human.updatehealth());
            Assert.Equal(60f, human.health);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ToxinsAboveHealthWithoutLimbsMultipliersKillAtMinusOneHundred()
    {
        // 200 tox is the cap (maxHealth * 2): health -100 exactly, dead
        var room = new MedRoom(seed: 118);
        var human = room.Human();
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_tox_loss(199));
            Assert.Equal(HARD_CRIT, human.stat);
            Waiting.NoSleep(human.adjust_tox_loss(1));
            Assert.Equal(-100f, human.health);
            Assert.Equal(DEAD, human.stat);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheDamageMultiplierConfigScalesAdjustAndBodypartDamage()
    {
        var room = new MedRoom(seed: 119);
        room.In(() => Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_damage_multiplier), 2f)));
        var human = room.Human();
        room.In(() =>
        {
            // adjust_tox_loss multiplies by CONFIG_GET(number/damage_multiplier) (:242); receive_damage does as well (_bodyparts.dm:723)
            Waiting.NoSleep(human.adjust_tox_loss(10));
            Assert.Equal(20f, MedRoom.Tox(human));
            MedRoom.Apply(human, 10, BRUTE, BODY_ZONE_CHEST);
            Assert.Equal(20f, MedRoom.Part(human, BODY_ZONE_CHEST).brute_dam);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HealDamageTypeHealsTheNamedTypeAndGetCurrentDamageOfTypeReadsIt()
    {
        var room = new MedRoom(seed: 120);
        var human = room.Human();
        room.In(() =>
        {
            MedRoom.Apply(human, 30, BRUTE, BODY_ZONE_CHEST);
            MedRoom.Apply(human, 20, BURN, BODY_ZONE_CHEST);
            MedRoom.Apply(human, 10, TOX);
            MedRoom.Apply(human, 5, OXY);
            Assert.Equal(30f, Dm.num(human.get_current_damage_of_type(BRUTE)));
            Assert.Equal(20f, Dm.num(human.get_current_damage_of_type(BURN)));
            Assert.Equal(10f, Dm.num(human.get_current_damage_of_type(TOX)));
            Assert.Equal(5f, Dm.num(human.get_current_damage_of_type(OXY)));
            MedRoom.Heal(human, 12, BRUTE);
            MedRoom.Heal(human, -4, TOX); // heal_damage_type: abs(heal_amount) * -1 (damage_procs.dm:127)
            Assert.Equal(18f, MedRoom.Brute(human), 3);
            Assert.Equal(6f, MedRoom.Tox(human), 3);
            // heal_ordered_damage heals up to `amount` in the given order (damage_procs.dm:517-529)
            object healed = Waiting.NoSleep(human.heal_ordered_damage(30, Dm.list(BURN, BRUTE, TOX)));
            Assert.Equal(30f, Dm.num(healed), 3);
            Assert.Equal(0f, MedRoom.Burn(human), 3);
            Assert.Equal(8f, MedRoom.Brute(human), 3); // 20 burn first, then 10 of the 18 brute
            Assert.Equal(6f, MedRoom.Tox(human), 3);
        });
        room.AssertNoRuntimes();
    }
}
