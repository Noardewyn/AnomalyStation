using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// <c>Life</c> under SSmobs for a real human, written from tg's DM: SSmobs fires every 2 s (mobs.dm:6), carbon
/// <c>handle_breathing</c> (carbon/life.dm:43-65) breathes when <c>SSmobs.times_fired % next_breath == 0</c> or the last
/// breath failed, <c>breathe</c> (:68-125) misses a breath in crit (losebreath), the human lungs' <c>check_breath</c>
/// (_lungs.dm:639) and <c>handle_suffocation</c> (:768) deal 3 oxygen damage per failed breath (x0.22 in crit), human
/// <c>handle_heart</c> (human/life.dm:287), blood in <c>handle_blood</c> (blood.dm:155).
/// </summary>
public class LifeAndBreathTests
{
    private const int Fire = 2 * MedRoom.TicksPerSecond; // SSmobs wait: 2 s = 40 ticks

    [Fact]
    public void SSmobsRunsLifeOnARealHumanEveryTwoSecondsAndNothingBreaksForAMinute()
    {
        var room = new MedRoom(seed: 201);
        var human = room.Human();
        var rec = room.Record(human);
        room.Step(60 * MedRoom.TicksPerSecond);
        Assert.InRange(rec.Lives.Count, 29, 31);
        for (int i = 1; i < rec.Lives.Count; i++)
            Assert.Equal(Fire, rec.Lives[i].Tick - rec.Lives[i - 1].Tick);
        // SSmobs.times_fired counts the fires: the Life signal sees the count before the increment (master.dm:945)
        for (int i = 1; i < rec.Lives.Count; i++)
            Assert.Equal(1f, rec.Lives[i].TimesFired - rec.Lives[i - 1].TimesFired);
        room.In(() =>
        {
            Assert.Equal(100f, human.health);
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(0f, MedRoom.Oxy(human));
            foreach (var organ in Dm.each_cast<obj_item_organ>(human.organs))
                Assert.Equal(0f, organ.damage);
            Assert.Equal(560f, human.blood_volume);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHealthyHumanBreathesTheRoomsAirEveryFourthFireAndTheAirIsConserved()
    {
        var room = new MedRoom(seed: 202);
        var human = room.Human(4, 4);
        var rec = room.Record(human);
        float o2Before = 0, co2Before = 0, totalBefore = 0;
        room.In(() =>
        {
            var air = MedRoom.AirAt(4, 4);
            o2Before = MedRoom.O2(air);
            co2Before = MedRoom.CO2(air);
            totalBefore = Dm.num(air.total_moles());
            Assert.True(o2Before > 0);
            Assert.Equal(0f, co2Before);
        });
        room.Step(40 * MedRoom.TicksPerSecond);
        Assert.True(rec.Breaths.Count >= 5, $"{rec.Breaths.Count} breaths");
        Assert.All(rec.Breaths, b => Assert.Equal(0f, b.TimesFired % 4)); // carbon/life.dm:55
        for (int i = 1; i < rec.Breaths.Count; i++)
            Assert.Equal(4 * Fire, rec.Breaths[i].Tick - rec.Breaths[i - 1].Tick);
        room.In(() =>
        {
            var air = MedRoom.AirAt(4, 4);
            // each breath takes BREATH_PERCENTAGE of the moles (carbon/life.dm:107-111) and returns the oxygen it used as the same
            // moles of carbon dioxide (_lungs.dm:253: breathe_gas_volume(oxygen -> carbon_dioxide)); nothing else changes
            float o2Used = o2Before - MedRoom.O2(air);
            Assert.True(o2Used > 0);
            Assert.Equal(o2Used, MedRoom.CO2(air) - co2Before, 3);
            Assert.Equal(totalBefore, Dm.num(air.total_moles()), 3);
            // one breath takes BREATH_VOLUME / CELL_VOLUME = 1.99 / 2500 of the oxygen
            float perBreath = 1f - MathF.Pow(1f - 1.99f / 2500f, rec.Breaths.Count);
            Assert.Equal(o2Before * perBreath, o2Used, 2);
            Assert.False(Dm.truthy(human.failed_last_breath));
            Assert.Equal(0f, MedRoom.Oxy(human));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutOxygenEveryBreathFailsAndTheNextBreathComesOnTheNextFire()
    {
        var room = new MedRoom(seed: 203);
        var human = room.Human(4, 4);
        room.In(() => MedRoom.RoomAir(air => air.set_gas(typeof(datum_gas_oxygen), 0)));
        var rec = room.Record(human);
        room.Step(16 * Fire);
        // carbon/life.dm:55: (times_fired % next_breath) == 0 || failed_last_breath: after the first failed breath, one per fire
        Assert.True(rec.Breaths.Count >= 12, $"{rec.Breaths.Count} breaths");
        int firstSequentialAt = 0;
        for (int i = 1; i < rec.Breaths.Count; i++)
        {
            long gap = rec.Breaths[i].Tick - rec.Breaths[i - 1].Tick;
            Assert.Equal(Fire, gap);
            firstSequentialAt = i;
        }
        Assert.True(firstSequentialAt > 0);
        room.In(() =>
        {
            Assert.True(Dm.truthy(human.failed_last_breath));
            // handle_suffocation (_lungs.dm:768-791): SUFFOCATION_OXYLOSS (3) per failed breath at stable health, nothing recovers it
            Assert.Equal(3f * rec.Breaths.Count, MedRoom.Oxy(human), 2);
            Assert.Equal(100f - 3f * rec.Breaths.Count, human.health, 2);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ARoomWithNoAirAtAllAlsoSuffocatesAndTheBreathLeavesTheTurfEmpty()
    {
        var room = new MedRoom(seed: 204);
        var human = room.Human(5, 5);
        room.In(() =>
        {
            MedRoom.RoomAir(air =>
            {
                air.set_gas(typeof(datum_gas_oxygen), 0);
                air.set_gas(typeof(datum_gas_nitrogen), 0);
            });
            Assert.Equal(0f, Dm.num(MedRoom.AirAt(5, 5).total_moles()));
            // M4b: the vacuum's own pressure damage (handle_environment_pressure) is not under test here
            ADD_TRAIT(human, TRAIT_RESISTLOWPRESSURE, "test");
        });
        var rec = room.Record(human);
        room.Step(6 * Fire);
        room.In(() =>
        {
            // an empty breath: has_moles is FALSE, failed_last_breath = TRUE (_lungs.dm:659-663), 3 oxy per breath
            Assert.True(rec.Breaths.Count >= 4);
            Assert.Equal(3f * rec.Breaths.Count, MedRoom.Oxy(human), 2);
            Assert.True(Dm.truthy(human.failed_last_breath));
            Assert.Equal(0f, Dm.num(MedRoom.AirAt(5, 5).total_moles()));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ThinAirCostsOxygenDamageInProportionToTheMissingPartialPressure()
    {
        // _lungs.dm:243-262, :768-791: below safe_oxygen_min (16 kPa) the breath fails and costs 3 * (16 - pp) / 16 oxygen damage;
        // the breath's partial pressure is n * R * T / BREATH_VOLUME for the BREATH_PERCENTAGE of the room's moles in it
        var room = new MedRoom(seed: 215);
        var human = room.Human(5, 5);
        const float R = 8.31f, T = 293.15f, CellVolume = 2500f;
        room.In(() =>
        {
            var air = MedRoom.AirAt(5, 5);
            air.set_gas(typeof(datum_gas_oxygen), 8f * CellVolume / (R * T)); // 8 kPa of oxygen
            air.set_gas(typeof(datum_gas_nitrogen), 80f);
        });
        var rec = room.Record(human);
        room.StepUntil(() => rec.Breaths.Count == 1, 6 * Fire);
        room.In(() =>
        {
            Assert.True(Dm.truthy(human.failed_last_breath));
            Assert.Equal(1.5f, MedRoom.Oxy(human), 1); // 3 * (16 - 8) / 16
            // the oxygen the thin breath held was used: carbon dioxide appears in the room
            Assert.True(MedRoom.CO2(MedRoom.AirAt(5, 5)) > 0);
        });
        room.AssertNoRuntimes();

        // 17 kPa is enough: no damage, no failed breath
        var room2 = new MedRoom(seed: 216);
        var human2 = room2.Human(5, 5);
        room2.In(() => MedRoom.AirAt(5, 5).set_gas(typeof(datum_gas_oxygen), 17.5f * CellVolume / (R * T)));
        var rec2 = room2.Record(human2);
        room2.StepUntil(() => rec2.Breaths.Count == 3, 12 * Fire);
        room2.In(() =>
        {
            Assert.False(Dm.truthy(human2.failed_last_breath));
            Assert.Equal(0f, MedRoom.Oxy(human2));
        });
        room2.AssertNoRuntimes();
    }

    [Fact]
    public void ABreathOfGoodAirHealsFiveOxygenDamageWhileTheMobIsStable()
    {
        var room = new MedRoom(seed: 205);
        var human = room.Human(4, 4);
        room.In(() => Waiting.NoSleep(human.adjust_oxy_loss(22)));
        var rec = room.Record(human);
        var seen = 0;
        float expected = 22f;
        for (int i = 0; i < 20; i++)
        {
            room.Step(Fire);
            for (; seen < rec.Lives.Count; seen++)
                if (rec.Lives[seen].TimesFired % 4 == 0 && expected > 0)
                    expected = MathF.Max(expected - 5f, 0f); // _lungs.dm:261-262: stat == STABLE and oxyloss: adjust_oxy_loss(-5) per good breath
            room.In(() => Assert.Equal(expected, MedRoom.Oxy(human), 2));
        }
        Assert.Equal(0f, expected);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void DamagedLungsOrHeartBreatheEveryThirdOrSecondFire()
    {
        // carbon/life.dm:43-55: next_breath 4, minus 1 if the lungs' damage > high_threshold, minus 1 if the heart's is
        var room = new MedRoom(seed: 206);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            var lungs = (obj_item_organ_lungs)human.get_organ_slot(ORGAN_SLOT_LUNGS);
            Assert.Equal(45f, lungs.high_threshold);
            Waiting.NoSleep(human.adjust_organ_loss(ORGAN_SLOT_LUNGS, 50f));
            Assert.Equal(50f, lungs.damage);
        });
        var rec = room.Record(human);
        room.Step(24 * Fire);
        Assert.True(rec.Breaths.Count >= 6);
        Assert.All(rec.Breaths, b => Assert.Equal(0f, b.TimesFired % 3));
        for (int i = 1; i < rec.Breaths.Count; i++)
            Assert.Equal(3 * Fire, rec.Breaths[i].Tick - rec.Breaths[i - 1].Tick);

        // both damaged: every second fire
        var room2 = new MedRoom(seed: 207);
        var human2 = room2.Human(4, 4);
        room2.In(() =>
        {
            Waiting.NoSleep(human2.adjust_organ_loss(ORGAN_SLOT_LUNGS, 50f));
            Waiting.NoSleep(human2.adjust_organ_loss(ORGAN_SLOT_HEART, 50f));
        });
        var rec2 = room2.Record(human2);
        room2.Step(24 * Fire);
        Assert.True(rec2.Breaths.Count >= 8);
        Assert.All(rec2.Breaths, b => Assert.Equal(0f, b.TimesFired % 2));
        for (int i = 1; i < rec2.Breaths.Count; i++)
            Assert.Equal(2 * Fire, rec2.Breaths[i].Tick - rec2.Breaths[i - 1].Tick);

        // a lung damage of exactly the high threshold (45) is not "greater": still every fourth fire
        var room3 = new MedRoom(seed: 208);
        var human3 = room3.Human(4, 4);
        room3.In(() => Waiting.NoSleep(human3.adjust_organ_loss(ORGAN_SLOT_LUNGS, 45f)));
        var rec3 = room3.Record(human3);
        room3.Step(24 * Fire);
        Assert.All(rec3.Breaths, b => Assert.Equal(0f, b.TimesFired % 4));
        room.AssertNoRuntimes();
        room2.AssertNoRuntimes();
        room3.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(0f, 0f, 4)]
    [InlineData(45f, 0f, 4)]     // exactly the high threshold is not "greater"
    [InlineData(45.1f, 0f, 3)]
    [InlineData(0f, 45f, 4)]
    [InlineData(0f, 45.1f, 3)]
    [InlineData(45.1f, 45.1f, 2)]
    [InlineData(100f, 100f, 2)]
    public void TheBreathCadenceIsDecidedByTimesFiredModuloNextBreath(float lungDamage, float heartDamage, int nextBreath)
    {
        // carbon/life.dm:43-55, called directly with SSmobs.times_fired set (no world step, so no organ healing in between)
        var room = new MedRoom(seed: 219);
        var human = room.Human(4, 4);
        var rec = room.Record(human, life: false, death: false, stat: false);
        room.In(() =>
        {
            if (lungDamage > 0)
                Waiting.NoSleep(human.adjust_organ_loss(ORGAN_SLOT_LUNGS, lungDamage));
            if (heartDamage > 0)
                Waiting.NoSleep(human.adjust_organ_loss(ORGAN_SLOT_HEART, heartDamage));
            for (int tf = 0; tf < 12; tf++)
            {
                int before = rec.Breaths.Count;
                SSmobs.times_fired = tf;
                human.failed_last_breath = FALSE;
                Waiting.NoSleep(human.handle_breathing(2f));
                bool breathed = rec.Breaths.Count > before;
                Assert.Equal(tf % nextBreath == 0, breathed);
                human.failed_last_breath = FALSE;
            }
            // a failed last breath always breathes
            SSmobs.times_fired = 1;
            human.failed_last_breath = TRUE;
            int n = rec.Breaths.Count;
            Waiting.NoSleep(human.handle_breathing(2f));
            Assert.Equal(n + 1, rec.Breaths.Count);
        });
    }

    [Fact]
    public void OrgansHealByTheirHealingFactorEachLifeWhenNotFailing()
    {
        var room = new MedRoom(seed: 209);
        var human = room.Human(4, 4);
        obj_item_organ_liver liver = null;
        room.In(() =>
        {
            liver = (obj_item_organ_liver)human.get_organ_slot(ORGAN_SLOT_LIVER);
            Waiting.NoSleep(human.adjust_organ_loss(ORGAN_SLOT_LIVER, 20f));
            Assert.Equal(20f, liver.damage);
        });
        var rec = room.Record(human, breath: false);
        room.Step(6 * Fire);
        // _organ.dm:174-198: damage falls by healing_factor * maxHealth * seconds_per_tick per Life (STANDARD_ORGAN_HEALING 0.0005,
        // maxHealth 100, 2 s = 0.1 per fire), satiety 0 adds nothing
        int fires = rec.Lives.Count;
        Assert.True(fires >= 5);
        room.In(() => Assert.Equal(20f - 0.1f * fires, (float)liver.damage, 2));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void FailingLungsMakeTheOwnerMissEveryBreath()
    {
        var room = new MedRoom(seed: 210);
        var human = room.Human(4, 4);
        obj_item_organ_lungs lungs = null;
        room.In(() =>
        {
            lungs = (obj_item_organ_lungs)human.get_organ_slot(ORGAN_SLOT_LUNGS);
            Waiting.NoSleep(human.adjust_organ_loss(ORGAN_SLOT_LUNGS, 100f));
            Assert.True(Dm.truthy(Dm.bitand(lungs.organ_flags, ORGAN_FAILING))); // check_damage_thresholds: damage >= maxHealth
        });
        var rec = room.Record(human);
        room.Step(10 * Fire);
        // carbon/life.dm:90-91: lungs failing: losebreath++, the breath is missed: oxy damage, one breath per fire
        room.In(() =>
        {
            Assert.True(Dm.truthy(human.failed_last_breath));
            Assert.True(MedRoom.Oxy(human) >= 3f * (rec.Breaths.Count - 1));
            Assert.True(MedRoom.Oxy(human) > 0);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void HardCritMissesEveryBreathForPointSixSixOxygenEach()
    {
        var room = new MedRoom(seed: 211);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_oxy_loss(130)); // health -30: HARD_CRIT exactly
            Assert.Equal(HARD_CRIT, human.stat);
        });
        var rec = room.Record(human);
        room.Step(20 * Fire);
        room.In(() =>
        {
            // carbon/life.dm:93-95: stat == HARD_CRIT: losebreath++ every breath, so every breath is missed (:100-103);
            // the empty breath fails (_lungs.dm:661) and handle_suffocation deals 3 * 0.22 = 0.66 (SUFFOCATION_OXYLOSS_CRIT_MODIFIER)
            int breaths = rec.Breaths.Count;
            Assert.True(breaths >= 15);
            Assert.Equal(130f + 0.66f * breaths, MedRoom.Oxy(human), 2);
            Assert.Equal(HARD_CRIT, human.stat);
            Assert.True(Dm.truthy(human.failed_last_breath));
        });
        // after the first breath, one per fire (failed_last_breath)
        for (int i = 1; i < rec.Breaths.Count; i++)
            Assert.Equal(Fire, rec.Breaths[i].Tick - rec.Breaths[i - 1].Tick);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SoftCritMissesOneBreathInFourAndTheTgQuirkOfTheRecoveryBreath()
    {
        // carbon/life.dm:93-97: soft crit adds 0.25 to losebreath per breath; at 1 a breath is missed (:100). The missed breath fails
        // (failed_last_breath), so the next fire breathes again: breath fires 0, 4, 8, 12 (the 4th breath: missed), 13, 16, 20, 24
        // (missed), 25, ... : one miss, of 0.66 oxygen damage, every 12 fires.
        var room = new MedRoom(seed: 212);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_oxy_loss(100)); // health 0: SOFT_CRIT exactly
            Assert.Equal(SOFT_CRIT, human.stat);
        });
        var rec = room.Record(human);
        var oxyAtFire = new List<float>();
        long start = room.Tick;
        for (int fire = 0; fire < 40; fire++)
        {
            room.Step(Fire);
            oxyAtFire.Add(room.In(() => MedRoom.Oxy(human)));
        }
        Assert.True(rec.Lives.Count >= 38);
        // the oxygen damage only ever grows in steps of 0.66, and the steps are 12 fires apart (a 0.5 s slack is a fire)
        var steps = new List<int>();
        float prev = 100f;
        for (int i = 0; i < oxyAtFire.Count; i++)
        {
            if (oxyAtFire[i] > prev + 0.001f)
            {
                Assert.Equal(0.66f, oxyAtFire[i] - prev, 2);
                steps.Add(i);
            }
            prev = oxyAtFire[i];
        }
        Assert.True(steps.Count >= 3, $"{steps.Count} steps");
        for (int i = 1; i < steps.Count; i++)
            Assert.Equal(12, steps[i] - steps[i - 1]);
        room.In(() => Assert.Equal(SOFT_CRIT, human.stat));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACardiacArrestCostsOxygenAndBruteEveryLifeAndKeepsTheHumanUnconscious()
    {
        var room = new MedRoom(seed: 213);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            Assert.False(Dm.truthy(human.undergoing_cardiac_arrest()));
            Assert.True(Dm.truthy(human.can_heartattack()));
            Assert.True(Dm.truthy(human.set_heartattack(TRUE))); // heart.Stop()
            Assert.True(Dm.truthy(human.undergoing_cardiac_arrest()));
            var heart = (obj_item_organ_heart)human.get_organ_slot(ORGAN_SLOT_HEART);
            Assert.False(Dm.truthy(heart.is_beating()));
            Assert.False(Dm.truthy(human.set_heartattack(TRUE))); // already stopped: no change
        });
        var rec = room.Record(human, breath: false);
        room.StepUntil(() => rec.Lives.Count == 1, Fire + 2); // up to the first Life: SSmobs' first fire is random, as in the bleeding test below
        Assert.Single(rec.Lives);
        room.In(() =>
        {
            // human/life.dm:287-297 handle_heart: adjust_oxy_loss(4 * seconds_per_tick), Unconscious(80), adjust_brute_loss(1 * seconds_per_tick)
            Assert.Equal(8f, MedRoom.Oxy(human), 2);
            Assert.True(Dm.truthy(human.IsUnconscious()));
            Assert.InRange(MedRoom.Brute(human), 1.4f, 2.1f);
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, TRAIT_STATUS_EFFECT("unconscious")));
        });
        room.Step(Fire);
        room.In(() => Assert.Equal(16f, MedRoom.Oxy(human), 1)); // the breath heals nothing while the mob is unconscious? no: stable heals 5 per breath
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RestartingTheHeartEndsTheArrest()
    {
        var room = new MedRoom(seed: 214);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            human.set_heartattack(TRUE);
            Assert.True(Dm.truthy(human.undergoing_cardiac_arrest()));
            Assert.True(Dm.truthy(human.set_heartattack(FALSE))); // heart.Restart()
            Assert.False(Dm.truthy(human.undergoing_cardiac_arrest()));
        });
        room.Step(5 * Fire);
        room.In(() => Assert.Equal(0f, MedRoom.Brute(human)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void BloodStaysAtFiveHundredSixtyForAHealthyHumanAndRegeneratesWithNutritionWhenLow()
    {
        var room = new MedRoom(seed: 215);
        var human = room.Human(4, 4);
        room.In(() => human.nutrition = 450f); // NUTRITION_LEVEL_WELL_FED: nutrition_ratio round(450 / 450, 0.2) = 1
        room.Step(3 * Fire);
        room.In(() => Assert.Equal(560f, human.blood_volume));
        room.In(() => human.set_blood_volume(500f));
        var rec = room.Record(human, breath: false);
        room.Step(4 * Fire);
        int lives = rec.Lives.Count;
        // blood.dm:178-188: BLOOD_REGEN_FACTOR 0.25 * heart multiplier 1 * nutrition ratio * seconds_per_tick 2 = 0.5 per Life
        room.In(() => Assert.Equal(500f + 0.5f * lives, human.blood_volume, 2));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ABleedingLimbLosesHalfABloodUnitPerSecondForAsManyLivesAsItHasStacks()
    {
        var room = new MedRoom(seed: 216);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            human.nutrition = 0f; // no regeneration
            MedRoom.Part(human, BODY_ZONE_CHEST).adjustBleedStacks(3);
            // refresh_bleed_rate: generic_bleedstacks > 0 gives a cached_bleed_rate of 0.5 (_bodyparts.dm:1683)
            Assert.Equal(0.5f, MedRoom.Part(human, BODY_ZONE_CHEST).cached_bleed_rate, 3);
            Assert.Equal(0.5f, Dm.num(human.get_bleed_rate()), 3);
        });
        var rec = room.Record(human, breath: false);
        // up to the first Life: SSmobs' phase comes from its random first fire after the boot (master.dm:621), so a fixed Fire + 2
        // ticks held one Life or two (M5 slice 1 moved that phase: SSburning joined the Master and draws a start offset of its own)
        room.StepUntil(() => rec.Lives.Count == 1, Fire + 2);
        // handle_blood: bleed(bleed_rate * seconds_per_tick) = 1 unit per Life, then adjustBleedStacks(-1, 0)
        room.In(() =>
        {
            Assert.Equal(559f, human.blood_volume, 2);
            Assert.Equal(2f, MedRoom.Part(human, BODY_ZONE_CHEST).generic_bleedstacks);
        });
        room.Step(2 * Fire);
        Assert.Equal(3, rec.Lives.Count);
        room.In(() =>
        {
            Assert.Equal(557f, human.blood_volume, 2);
            Assert.Equal(0f, MedRoom.Part(human, BODY_ZONE_CHEST).generic_bleedstacks);
            Assert.Equal(0f, MedRoom.Part(human, BODY_ZONE_CHEST).cached_bleed_rate, 3);
        });
        room.Step(3 * Fire);
        room.In(() => Assert.Equal(557f, human.blood_volume, 2)); // the stacks are used up: no more bleeding
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LowBloodCostsOxygenDamageUntilItReachesTheMissingBloodPercent()
    {
        // handle_blood's random hiccup (blood.dm:216, SPT_PROB(2.5) then prob(50): losebreath, or stamina loss) is left out of the
        // model below, so the first seed whose 24 fires bring no hiccup is checked (every subsystem added to the Master moves the
        // random stream: the start offsets of master.dm:609-621)
        for (ulong seed = 227; seed < 267; seed++)
        {
            var room = new MedRoom(seed: seed);
            var human = room.Human(4, 4);
            room.In(() =>
            {
                human.nutrition = 0f;
                human.set_blood_volume(400f);
            });
            var rec = room.Record(human, breath: false);
            // blood.dm:248-255: target_oxyloss = (1 - 400 / 560) * 100 = 28.57; while oxyloss < target, adjust_oxy_loss(
            // round(0.01 * (560 - 400), 0.25) * seconds_per_tick) = 1.5 * 2 = 3 per Life; a good breath (every 4th fire, in Life before
            // handle_blood) heals 5 of it while the mob is stable (_lungs.dm:261)
            int seen = 0;
            float expected = 0f;
            float peak = 0f;
            bool hiccup = false;
            var checks = new List<(float Expected, float Oxy, float Blood)>();
            for (int i = 0; i < 24 && !hiccup; i++)
            {
                room.Step(Fire);
                for (; seen < rec.Lives.Count; seen++)
                {
                    if (rec.Lives[seen].TimesFired % 4 == 0 && expected > 0)
                        expected = MathF.Max(expected - 5f, 0f);
                    if (expected < 28.5714f)
                        expected += 3f;
                    peak = MathF.Max(peak, expected);
                }
                room.In(() =>
                {
                    hiccup = Dm.num(human.losebreath) > 0 || Dm.num(human.get_stamina_loss()) > 0;
                    checks.Add((expected, MedRoom.Oxy(human), human.blood_volume));
                });
            }
            if (hiccup)
                continue;
            foreach (var (e, oxy, blood) in checks)
            {
                Assert.Equal(400f, blood);
                Assert.Equal(e, oxy, 2);
            }
            Assert.True(peak >= 28.5714f, $"the oxygen damage grew to {peak}");
            room.AssertNoRuntimes();
            return;
        }
        Assert.Fail("every seed brought a hiccup");
    }

    [Fact]
    public void BloodBelowOneHundredTwelveKillsTheHumanOnTheNextLife()
    {
        var room = new MedRoom(seed: 218);
        var human = room.Human(4, 4);
        room.In(() =>
        {
            human.nutrition = 0f;
            human.set_blood_volume(111f); // BLOOD_VOLUME_SURVIVE is 560 * 0.2 = 112
        });
        var rec = room.Record(human, breath: false);
        room.Step(Fire + 2);
        // blood.dm:241-246: -INFINITY to BLOOD_VOLUME_SURVIVE: death()
        room.In(() => Assert.Equal(DEAD, human.stat));
        Assert.Single(rec.Deaths);
        room.AssertNoRuntimes();
    }
}
