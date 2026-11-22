using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// Crit and death at tg's timings (scope.md 2e examples), fast-forwarded in ticks. The expected values come from a small model
/// of tg's DM, run over the SSmobs fires the world actually had: SSmobs fires every 40 ticks; a breath is taken on a fire whose
/// <c>times_fired % 4 == 0</c> or when the last breath failed (carbon/life.dm:55); in hard crit <c>losebreath</c> grows by 1 per
/// breath, in soft crit by 0.25 (:93-97); at 1 the breath is missed (:100) and the empty breath costs 3 oxygen damage, times 0.22
/// in crit (_lungs.dm:768-791); a good breath heals 5 oxygen damage when the mob is stable (_lungs.dm:261); the stat follows the
/// health: soft crit at 0, hard crit at -30, dead at -100 (carbon.dm:610-625).
/// </summary>
public class CritAndDeathTimingTests
{
    private const int Fire = 2 * MedRoom.TicksPerSecond;

    /// <summary>tg's rounding of the human's health to DAMAGE_PRECISION (carbon.dm:436).</summary>
    private static float HealthOf(float fixedLoss, float oxy) => MathF.Round((100f - oxy - fixedLoss) / 0.1f) * 0.1f;

    private static int StatOf(float health) => health <= -100f ? DEAD : health <= -30f ? HARD_CRIT : health <= 0f ? SOFT_CRIT : STABLE;

    private sealed class Model
    {
        public float Oxy;
        public float Lose;
        public bool Failed;
        public int Stat;
        public readonly List<(int Fire, int NewStat)> Changes = new List<(int, int)>();
        public int Breaths;
        public int DeathFire = -1;

        public static Model Run(IReadOnlyList<float> timesFired, float fixedLoss, float startOxy)
        {
            var m = new Model { Oxy = startOxy };
            m.Stat = StatOf(HealthOf(fixedLoss, m.Oxy));
            for (int i = 0; i < timesFired.Count && m.Stat != DEAD; i++)
            {
                bool breath = timesFired[i] % 4 == 0 || m.Failed;
                if (!breath)
                    continue;
                m.Breaths++;
                if (m.Stat == HARD_CRIT)
                    m.Lose += 1f;
                else if (m.Stat == SOFT_CRIT)
                    m.Lose += 0.25f;
                bool miss = m.Lose >= 1f;
                if (miss)
                {
                    m.Lose -= 1f;
                    m.Failed = true;
                    float dealt = 3f;
                    if (m.Stat == SOFT_CRIT || m.Stat == HARD_CRIT)
                        dealt *= 0.22f;
                    m.Oxy = MathF.Min(m.Oxy + dealt, 200f);
                }
                else
                {
                    m.Failed = false;
                    if (m.Stat == STABLE && m.Oxy > 0)
                        m.Oxy = MathF.Max(m.Oxy - 5f, 0f);
                }
                int stat = StatOf(HealthOf(fixedLoss, m.Oxy));
                if (stat != m.Stat)
                {
                    m.Stat = stat;
                    m.Changes.Add((i, stat));
                    if (stat == DEAD)
                        m.DeathFire = i;
                }
            }
            return m;
        }
    }

    private static (List<float> TimesFired, List<long> Ticks) LivesOf(datum_test_t1_recorder rec)
        => (rec.Lives.Select(l => l.TimesFired).ToList(), rec.Lives.Select(l => l.Tick).ToList());

    [Fact]
    public void OxygenOneHundredFiftyFallsIntoHardCritAndDiesAfterSeventySixMissedBreathsAtTheComputedTick()
    {
        var room = new MedRoom(seed: 701);
        var human = room.Human(4, 4);
        long damagedAt = room.Tick;
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_oxy_loss(150));
            Assert.Equal(-50f, human.health);
            Assert.Equal(HARD_CRIT, human.stat);
        });
        var rec = room.Record(human);
        room.Step(100 * MedRoom.TicksPerSecond); // 2,000 ticks: still alive
        room.In(() => Assert.Equal(HARD_CRIT, human.stat));
        room.StepUntil(() => human.stat == DEAD, 3300 - 2000);
        (List<float> tf, List<long> ticks) = LivesOf(rec);
        Model model = Model.Run(tf, 0f, 150f);
        Assert.True(model.DeathFire > 0, "the model says the human survives");
        Assert.Equal(76, model.Breaths); // 76 * 0.66 = 50.16 > 50 > 75 * 0.66
        Assert.Single(rec.Deaths);
        Assert.Equal(ticks[model.DeathFire], rec.Deaths[0]);
        Assert.Equal(model.Breaths, rec.Breaths.Count);
        long window = rec.Deaths[0] - damagedAt;
        Assert.InRange(window, 3000, 3160); // scope.md 2e example 1
        for (int i = 1; i < rec.Breaths.Count; i++)
            Assert.Equal(Fire, rec.Breaths[i].Tick - rec.Breaths[i - 1].Tick);
        room.In(() =>
        {
            Assert.Equal(DEAD, human.stat);
            Assert.Equal(200f, MedRoom.Oxy(human), 0); // clamped at maxHealth * 2
            Assert.Equal(rec.Deaths[0], room.World.Tick);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ToxinsOneHundredFiftyDieInTheSameWindowAsOxygenDoes()
    {
        var room = new MedRoom(seed: 702);
        var human = room.Human(4, 4);
        long damagedAt = room.Tick;
        room.In(() =>
        {
            Waiting.NoSleep(human.adjust_tox_loss(150));
            Assert.Equal(HARD_CRIT, human.stat);
        });
        var rec = room.Record(human);
        room.StepUntil(() => human.stat == DEAD, 3300);
        (List<float> tf, List<long> ticks) = LivesOf(rec);
        // the model with 150 as a fixed loss (toxins are not part of the oxygen the breaths add)
        Model model = Model.Run(tf, 150f, 0f);
        Assert.Equal(76, model.Breaths);
        Assert.Equal(ticks[model.DeathFire], rec.Deaths[0]);
        Assert.InRange(rec.Deaths[0] - damagedAt, 3000, 3160);
        room.In(() => Assert.InRange(MedRoom.Oxy(human), 50f, 50.5f));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void BruteOneHundredFiftyThroughTakeOverallDamageIsSoftCritThenHardCritWhenTheMissedBreathsAddUpThenDeath()
    {
        // scope.md 2e example 2: 25 on each of the six parts = 125 of health: health -25, SOFT_CRIT. Soft crit misses one breath in
        // four (0.66 each); the 8th miss takes the oxygen damage to 5.28, below -30; in hard crit every breath is missed.
        var room = new MedRoom(seed: 703);
        var human = room.Human(4, 4);
        long damagedAt = room.Tick;
        room.In(() =>
        {
            Waiting.NoSleep(human.take_overall_damage(150));
            Assert.Equal(-25f, human.health, 3);
            Assert.Equal(SOFT_CRIT, human.stat);
        });
        var rec = room.Record(human);
        room.Step(60 * MedRoom.TicksPerSecond);
        room.In(() => Assert.Equal(SOFT_CRIT, human.stat)); // a minute in: still soft crit
        room.StepUntil(() => human.stat == DEAD, 8 * 60 * MedRoom.TicksPerSecond);
        (List<float> tf, List<long> ticks) = LivesOf(rec);
        Model model = Model.Run(tf, 125f, 0f);
        Assert.True(model.DeathFire > 0);
        // the model's stat changes and the world's agree to the tick
        Assert.Equal(model.Changes.Count, rec.StatChanges.Count);
        for (int i = 0; i < model.Changes.Count; i++)
        {
            Assert.Equal(ticks[model.Changes[i].Fire], rec.StatChanges[i].Tick);
            Assert.Equal((float)model.Changes[i].NewStat, Dm.num(rec.StatChanges[i].NewStat));
        }
        Assert.Equal(new[] { (float)HARD_CRIT, (float)DEAD }, rec.StatChanges.Select(c => Dm.num(c.NewStat)).ToArray());
        Assert.Equal(ticks[model.DeathFire], rec.Deaths[0]);
        double minutes = (rec.Deaths[0] - damagedAt) / (double)(60 * MedRoom.TicksPerSecond);
        Assert.InRange(minutes, 6.5, 7.0); // "about 6.8 min" (scope.md 2e)
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AStableHumanInGoodAirNeverLosesHealthAndAHumanJustAboveSoftCritStaysStable()
    {
        var room = new MedRoom(seed: 704);
        var human = room.Human(4, 4);
        // health 0.1: stable; CANT_WOUND, since a random bleeding wound would drain blood into oxygen damage (handle_blood)
        room.In(() => Waiting.NoSleep(human.apply_damage(99.9f, BRUTE, BODY_ZONE_CHEST, wound_bonus: CANT_WOUND)));
        var rec = room.Record(human);
        room.Step(120 * MedRoom.TicksPerSecond);
        room.In(() =>
        {
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(0.1f, human.health, 3);
            Assert.Equal(0f, MedRoom.Oxy(human));
        });
        Assert.Empty(rec.StatChanges);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OxygenDamageFromAnAirlessRoomKnocksOutAtFiftyAndKillsAtTheComputedTick()
    {
        // a room without air: every breath fails (3 oxygen damage; 0.66 once in crit). Knocked out (OXYLOSS_TRAIT) at oxygen 50,
        // soft crit at 100 (health 0), hard crit at 130, dead at 200.
        var room = new MedRoom(seed: 705);
        var human = room.Human(5, 5);
        room.In(() =>
        {
            MedRoom.RoomAir(air =>
            {
                air.set_gas(typeof(datum_gas_oxygen), 0);
                air.set_gas(typeof(datum_gas_nitrogen), 0);
            });
            // M4b: the vacuum's own pressure damage (handle_environment_pressure) would add brute; the model is oxygen only
            ADD_TRAIT(human, TRAIT_RESISTLOWPRESSURE, "test");
        });
        var rec = room.Record(human);
        var seen = new List<(string What, long Tick)>();
        bool knockedSeen = false;
        for (int i = 0; i < 600 * MedRoom.TicksPerSecond && room.In(() => human.stat != DEAD); i++)
        {
            room.Step();
            if (!knockedSeen && room.In(() => HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT)))
            {
                knockedSeen = true;
                float oxyNow = room.In(() => MedRoom.Oxy(human));
                Assert.InRange(oxyNow, 50f, 53.01f);
                seen.Add(("knocked out", room.Tick));
            }
        }
        Assert.True(knockedSeen);
        (List<float> tf, List<long> ticks) = LivesOf(rec);
        // the airless variant of the model: from the first fire with times_fired % 4 == 0 every breath misses (failed_last_breath)
        float oxy = 0;
        int stat = STABLE;
        int deathFire = -1;
        bool failed = false;
        for (int i = 0; i < tf.Count && stat != DEAD; i++)
        {
            if (!(tf[i] % 4 == 0 || failed))
                continue;
            failed = true;
            oxy = MathF.Min(oxy + (stat == STABLE ? 3f : 3f * 0.22f), 200f);
            stat = StatOf(HealthOf(0f, oxy));
            if (stat == DEAD)
                deathFire = i;
        }
        Assert.True(deathFire > 0);
        Assert.Equal(ticks[deathFire], rec.Deaths[0]);
        room.In(() => Assert.Equal(DEAD, human.stat));
        room.AssertNoRuntimes();
    }
}
