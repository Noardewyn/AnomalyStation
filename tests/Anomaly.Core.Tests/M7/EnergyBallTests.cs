using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>Counts the moves of the energy ball (COMSIG_MOVABLE_MOVED).</summary>
[DmPath("/datum/t7_ball_probe")]
public class datum_t7_ball_probe : datum
{
    public int Moves;

    [SignalHandler]
    public virtual void on_moved(datum source = null, object old_loc = null, object movement_dir = null, object forced = null, object old_locs = null) => Moves++;
}

/// <summary>
/// D169: tg's energy ball (power/tesla/energy_ball.dm) and the orbiter component its mini-balls orbit with (datums/components/orbiter.dm).
/// The ball moves on SSobj's two-second schedule, five king's moves a cycle with no mini-balls (energy_ball.dm:71, :102); each cycle it
/// zaps with tg's 695.304 MJ (:79), which an anchored grounding rod takes whole (coil.dm:155); every threshold its energy passes grows a
/// mini-ball ten seconds later (:124-130), nine for the delamination's 200 (delamination_effects.dm:113); the mini-balls orbit it
/// (orbiter.dm:73: a translated transform turned by SpinAnimation) and follow its moves (:158); one that loses its energy loses a
/// mini-ball (:131); a carbon that walks into it is dusted (:158, :190) unless a grounding rod stands within two tiles (:197); its own
/// energy never drains. tg has no unit test of the energy ball.
/// </summary>
public class EnergyBallTests
{
    private const int Second = 20;
    private const int Cx = 16, Cy = 16;
    private const float TeslaDefaultEnergy = 695.304f * MEGA * JOULES; // energy_ball.dm:1

    private static AudioRoom Room(ulong seed, int size = 31)
    {
        var r = new AudioRoom(size, size, seed: seed);
        r.Step(Second);
        return r;
    }

    private static obj_energy_ball Ball(AudioRoom r, float energy, int x = Cx, int y = Cy) => r.In(() =>
    {
        var ball = Dm.@new<obj_energy_ball>(locate(x, y, 1));
        ball.energy = energy;
        return ball;
    });

    private static (int X, int Y) At(AudioRoom r, atom a) => r.In(() => ((int)Dm.num(a.x), (int)Dm.num(a.y)));

    private static List<obj_energy_ball> Minis(AudioRoom r, obj_energy_ball ball)
        => r.In(() => Dm.each_cast<obj_energy_ball>(ball.orbiting_balls).ToList());

    [Fact]
    public void TheBallMovesFiveKingsMovesEveryTwoSecondsAndCracklesAndDrawsItsLight()
    {
        AudioRoom r = Room(8101, 61);
        r.Join(6, 31, "Listener"); // 25 tiles away: within the bolt's world.view + 30 (:73), out of its zaps' reach
        obj_energy_ball ball = Ball(r, 0, 31, 31); // no energy: no mini-balls (the first threshold is 32)
        Assert.Equal(10f, r.In(() => ball.light_range)); // set_light(10, 7, "#5e5edd") (:49)
        Assert.Equal(7f, r.In(() => ball.light_power));
        // each forceMove of the ball sends COMSIG_MOVABLE_MOVED (energy_ball.dm:108): the probe counts its steps
        var probe = r.In(() =>
        {
            var p = Dm.@new<datum_t7_ball_probe>();
            p.RegisterSignal(ball, COMSIG_MOVABLE_MOVED, nameof(datum_t7_ball_probe.on_moved));
            return p;
        });
        // a cycle is a fire of SSobj (one process of the ball); the first is staggered by a random 0 to 2 s (master.dm:621)
        var cycles = new List<int>();
        float fired = r.In(() => SSobj.times_fired);
        (int X, int Y) last = At(r, ball);
        int stepsSeen = 0;
        for (int t = 0; t < 12 * Second; t++)
        {
            r.Step(1);
            float firedNow = r.In(() => SSobj.times_fired);
            (int X, int Y) now = At(r, ball);
            int steps = probe.Moves - stepsSeen;
            stepsSeen = probe.Moves;
            if (firedNow == fired)
                Assert.Equal(0, steps); // the ball moves only in its process
            else
            {
                cycles.Add(t);
                // move(4): i in 0 to 4, five steps of one tile each in one cycle (:102); in the open nothing blocks one. Five random steps
                // may cancel out (1 in 27), so the ball ends 0 to 5 tiles away, whatever the random stream (the seed is not an assertion)
                Assert.Equal(5, steps);
                Assert.InRange(Math.Max(Math.Abs(now.X - last.X), Math.Abs(now.Y - last.Y)), 0, 5);
            }
            fired = firedNow;
            last = now;
        }
        Assert.True(cycles.Count >= 5, $"the ball moves each SSobj cycle (SSobj fired at ticks {string.Join(",", cycles)})");
        // the cycles come two seconds apart (SSobj's wait, processing/obj.dm)
        Assert.All(cycles.Zip(cycles.Skip(1)), p => Assert.Equal(2 * Second, p.Second - p.First));
        Assert.True(r.Sounds.Heard.Count(s => s.File == "sound/effects/magic/lightningbolt.ogg") >= 5, "each cycle cracks its bolt (:73)");
        Assert.Equal(0f, r.In(() => Dm.num(ball.energy)));
        Assert.Empty(Minis(r, ball));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EachCycleZapsWithTgsPowerWhichAGroundingRodTakesWhole()
    {
        AudioRoom r = Room(8102);
        // anchored rods on every tile within six of the ball: wherever its five moves take it, a rod is the closest target (tesla_zap's
        // priorities, :204: no coil, so a rod; it takes the bolt whole and gives nothing back, so the chain stops)
        var rods = r.In(() =>
        {
            var list = new List<obj_machinery_power_energy_accumulator_grounding_rod>();
            for (int x = Cx - 6; x <= Cx + 6; x++)
                for (int y = Cy - 6; y <= Cy + 6; y++)
                    if (x != Cx || y != Cy)
                        list.Add(Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod_anchored>(locate(x, y, 1)));
            return list;
        });
        obj_energy_ball ball = Ball(r, 0);
        // a rod releases a share of what it stores each machines cycle (energy_accumulator.dm:38); what it released shows in processed_energy
        float Held() => r.In(() => rods.Sum(g => g.stored_energy + g.processed_energy));
        int cycles = 0;
        for (int t = 0; t < 7 * Second && cycles < 3; t++)
        {
            float[] before = r.In(() => rods.Select(g => g.stored_energy).ToArray());
            float held = Held();
            r.Step(1);
            float[] after = r.In(() => rods.Select(g => g.stored_energy).ToArray());
            int struck = Enumerable.Range(0, rods.Count).Count(i => after[i] - before[i] > TeslaDefaultEnergy / 2);
            if (struck == 0)
                continue;
            Assert.Equal(1, struck); // one bolt a cycle, taken whole by the closest rod
            if (cycles++ == 0) // nothing stored before the first: the rods hold exactly one TESLA_DEFAULT_ENERGY (:79)
                Assert.Equal(TeslaDefaultEnergy, Held() - held, 1f);
        }
        Assert.Equal(3, cycles);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoHundredEnergyGrowsNineMiniBallsThatOrbitAndFollowTheBall()
    {
        AudioRoom r = Room(8103, 61);
        r.Join(6, 31, "Listener"); // 25 tiles away: within the chargeup's world.view + 30 (:129)
        obj_energy_ball ball = Ball(r, 200, 31, 31); // as effect_tesla (delamination_effects.dm:113: "Gets us about 9 balls")
        r.Step(50 * Second);
        List<obj_energy_ball> minis = Minis(r, ball);
        Assert.Equal(9, minis.Count); // 32 * 1.25^n passes 200 nine times (:125-127)
        Assert.Equal(32f * MathF.Pow(1.25f, 9), r.In(() => ball.energy_to_raise), 2);
        Assert.Equal(9, r.Sounds.Heard.Count(s => s.File == "sound/effects/magic/lightning_chargeup.ogg")); // :129
        Assert.Equal(200f, r.In(() => Dm.num(ball.energy))); // the ball's energy never drains
        Assert.Contains("There are 9 mini-balls orbiting it.", r.In(() => Dm.each(Waiting.NoSleep(ball.examine(null))).Select(o => Dm.str(o)).ToList()));

        foreach (obj_energy_ball mini in minis)
        {
            Assert.True(r.In(() => Dm.truthy(mini.miniball)));
            Assert.Equal(0f, r.In(() => Dm.num(mini.energy))); // a mini-ball's energy is kept at 0 (:67)
            Assert.Same(ball, r.In(() => mini.orbiting?.parent));
            Assert.Same(r.In(() => mini.orbiting), r.In(() => ball.orbiters));
            Assert.Same(ball, r.In(() => mini.orbit_target));
            Assert.Same(r.In(() => get_turf(ball)), r.In(() => mini.loc));
            // its transform: scaled by 0.3 to 0.7, turned +-90 (head first) and moved out by the orbit's radius (:148-153, orbiter.dm:94-104):
            // (96 + 96) * 0.4 to 0.8, less a quarter
            var m = (matrix)r.In(() => mini.transform);
            float radius = MathF.Sqrt(m.c * m.c + m.f * m.f);
            Assert.Contains(new[] { 0.4f, 0.5f, 0.6f, 0.7f, 0.8f }, k => Math.Abs(192 * k * 0.75f - radius) < 0.01f);
            float scale = MathF.Sqrt(m.a * m.a + m.d * m.d);
            Assert.Contains(new[] { 0.3f, 0.4f, 0.5f, 0.6f, 0.7f }, k => Math.Abs(k - scale) < 0.001f);
            Assert.True(r.In(() => HAS_TRAIT(mini, TRAIT_NO_FLOATING_ANIM)));
        }

        // they follow the ball's moves (move_react through the movement detector, orbiter.dm:158)
        for (int i = 0; i < 6 * Second; i++)
        {
            r.Step(1);
            turf at = r.In(() => get_turf(ball));
            Assert.All(minis, mini => Assert.Same(at, r.In(() => mini.loc)));
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABallBelowItsLowerThresholdLosesAMiniBallAndDestroyingItTakesTheRest()
    {
        AudioRoom r = Room(8104, 61);
        obj_energy_ball ball = Ball(r, 50, 31, 31); // 32, 40 and 50 pass: three mini-balls
        r.Step(18 * Second);
        Assert.Equal(3, Minis(r, ball).Count);
        Assert.Equal(30f, r.In(() => ball.energy_to_lower)); // energy_to_raise - 20 when it passed 50 (:126)
        List<obj_energy_ball> before = Minis(r, ball);
        r.In(() => ball.energy = 10); // below 30: one mini-ball goes each cycle (:131-136)
        r.Step(5 * Second);
        List<obj_energy_ball> after = Minis(r, ball);
        Assert.True(after.Count < 3, "a mini-ball was taken");
        Assert.All(before.Except(after), gone => Assert.True(r.In(() => QDELETED(gone))));
        Assert.Equal(after.Count, r.In(() => ball.orbiters == null ? 0 : (int)ball.orbiters.orbiter_list.len));

        r.In(() => qdel(ball));
        Assert.All(after, mini => Assert.True(r.In(() => QDELETED(mini)), "QDEL_LIST(orbiting_balls) (:60)"));
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AManWhoWalksIntoTheBallIsDustedUnlessAGroundingRodIsNear()
    {
        AudioRoom r = Room(8105);
        obj_energy_ball ball = Ball(r, 0);
        mob_living_carbon_human bob = r.Dummy(Cx - 1, Cy);
        r.In(() => Waiting.NoSleep(step(bob, EAST)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(bob)) || r.In(() => bob.stat == DEAD), "Bob walks into the ball and is dusted (Bumped, :158)");

        // a grounding rod within two tiles of the ball keeps a carbon from being dusted (:197)
        AudioRoom r2 = Room(8106);
        obj_energy_ball ball2 = Ball(r2, 0);
        r2.In(() => Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod_anchored>(locate(Cx, Cy + 2, 1)));
        mob_living_carbon_human alice = r2.Dummy(Cx - 1, Cy);
        r2.In(() => Waiting.NoSleep(ball2.dust_mobs(alice)));
        r2.Step(2);
        Assert.False(r2.In(() => QDELETED(alice)), "the rod grounds the ball");
        r.AssertNoRuntimes();
        r2.AssertNoRuntimes();
    }

    /// <summary>DEVIATION(DV83): an inert monkey (D112: no DNA, organs or limbs) is neither a bolt's living target nor dusted.</summary>
    [Fact]
    public void AnInertHumanIsNeitherZappedNorDusted()
    {
        AudioRoom r = Room(8107);
        mob_living_carbon_human_species_cerulean monkey = r.In(() => Dm.@new<mob_living_carbon_human_species_cerulean>(locate(Cx + 1, Cy, 1)));
        Assert.True(r.In(() => InertSeam.IsInert(monkey)));
        obj_energy_ball ball = Ball(r, 0);
        r.In(() => Waiting.NoSleep(ball.dust_mobs(monkey)));
        r.In(() => Waiting.NoSleep(tesla_zap(ball, 3, 1e6f)));
        r.Step(4 * Second);
        Assert.False(r.In(() => QDELETED(monkey)));
        r.AssertNoRuntimes();
    }
}
