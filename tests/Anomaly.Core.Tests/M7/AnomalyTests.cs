using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// The M7 supermatter slice, part 2 (D167): tg's anomalies that an overpowered supermatter makes (supermatter_anomaly_gen,
/// supermatter_extra_effects.dm:162): the base (effects/anomalies/_anomalies.dm: its lifespan, countdown, processing on SSobj and
/// detonation), the flux wave (it shocks who touches it), the gravitational (it pulls, knocks down and throws), the pyroclastic (it
/// breathes hot plasma and oxygen) and the hallucination anomaly (its decoys; its pulses are M8's).
/// </summary>
public class AnomalyTests
{
    private const int Second = 20;
    private const int X = 7, Y = 5;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        r.Step(Second);
        return r;
    }

    /// <summary>An anomaly at (X, Y) made as supermatter_anomaly_gen makes it (no core), held still (move_chance 0).</summary>
    private static T Make<T>(AudioRoom r, params object[] args) where T : obj_effect_anomaly => r.In(() =>
    {
        var a = (T)Dm.@new(typeof(T), new object[] { locate(X, Y, 1) }.Concat(args).ToArray());
        a.move_chance = 0;
        return a;
    });

    private static void Effect(AudioRoom r, obj_effect_anomaly a, float seconds = 2) => r.In(() => Waiting.NoSleep(a.anomalyEffect(seconds)));

    [Fact]
    public void AnAnomalyLivesItsSpanWithItsCountdownThenDetonates()
    {
        AudioRoom r = Room(7951);
        obj_effect_anomaly_flux flux = Make<obj_effect_anomaly_flux>(r, 30 * SECONDS, FALSE, FLUX_NO_EMP);
        // the SM's: no core (drops_core FALSE), and the third argument is flux's emp_zap (anomalies_flux.dm:10)
        Assert.Null(r.In(() => flux.anomaly_core));
        Assert.Equal((float)FLUX_NO_EMP, r.In(() => flux.emp_zap));
        Assert.Equal(300f, r.In(() => Dm.num(flux.lifespan)));
        Assert.True(r.In(() => Dm.@in(flux, SSobj.processing)), "it processes on SSobj (_anomalies.dm:34)");
        obj_effect_countdown_anomaly countdown = r.In(() => flux.countdown);
        Assert.NotNull(countdown);
        Assert.True(r.In(() => Dm.truthy(countdown.started)));
        Assert.InRange(r.In(() => Dm.num(countdown.get_value())), 25, 30);
        Assert.True(r.In(() => Dm.length(flux.filters)) > 0, "its wibbly filters (apply_wibbly_filters, anomalies_flux.dm:17)");
        // past its death time: the next SSobj process (every 2 s) detonates it (FLUX_NO_EMP: sparks) and deletes it
        r.Step(29 * Second);
        Assert.False(r.In(() => QDELETED(flux)));
        r.Step(5 * Second);
        Assert.True(r.In(() => QDELETED(flux)));
        Assert.True(r.In(() => QDELETED(countdown)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFluxAnomalyShocksTheManWhoTouchesIt()
    {
        AudioRoom r = Room(7952);
        obj_effect_anomaly_flux flux = Make<obj_effect_anomaly_flux>(r, null, FALSE, FLUX_NO_EMP);
        mob_living_carbon_human bob = r.Dummy(X - 1, Y);
        Assert.True(r.In(() => Dm.truthy(flux.density)), "a flux wave is dense");
        // it can shock once an effect cycle has charged it (anomalies_flux.dm:21); walking into it then burns 20 (no gloves help)
        Effect(r, flux);
        // read at once: every SSobj process recharges the wave (anomalyEffect sets canshock), so a step of the world could recharge it
        r.In(() => Waiting.NoSleep(step(bob, EAST)));
        float burn = r.In(() => Dm.num(bob.get_fire_loss()));
        Assert.True(burn > 0, "the flux wave shocks Bob");
        Assert.Equal(0f, r.In(() => flux.canshock)); // one shock per charge
        r.In(() => Waiting.NoSleep(flux.mobShock(bob)));
        Assert.Equal(burn, r.In(() => Dm.num(bob.get_fire_loss())));
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGravitationalAnomalyPullsLooseThingsAndThrowsTheManOnIt()
    {
        AudioRoom r = Room(7953);
        obj_effect_anomaly_grav grav = Make<obj_effect_anomaly_grav>(r, null, FALSE);
        obj_item_wrench wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(X + 3, Y, 1)));
        mob_living_carbon_human bob = r.Join(X, Y, "Bob"); // a player: he hears the detonation
        Effect(r, grav);
        // the wrench three tiles east is pulled a step closer (anomalies_gravity.dm:22); Bob on its tile is knocked down and thrown
        Assert.Equal(X + 2f, r.In(() => Dm.num(wrench.x)));
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(bob.IsKnockdown())), "the gravity knocks Bob down (gravShock)");
        Assert.False(r.In(() => bob.loc == locate(X, Y, 1)), "Bob is thrown off the anomaly's tile");
        // its detonation: the gravity circle wave and the cosmic energy sound
        r.In(() => Waiting.NoSleep(grav.detonate()));
        Assert.Single(r.In(() => Dm.each<obj_effect_temp_visual_circle_wave_gravity>().ToList()));
        Assert.Contains(r.Sounds.Heard, s => s.File == "sound/effects/magic/cosmic_energy.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APyroclasticAnomalyBreathesHotPlasmaAndOxygen()
    {
        AudioRoom r = Room(7954);
        obj_effect_anomaly_pyro pyro = Make<obj_effect_anomaly_pyro>(r, null, FALSE);
        datum_gas_mixture air = r.In(() => ((turf_open)locate(X, Y, 1)).air);
        float plasma0 = r.In(() => Dm.num(air.moles[typeof(datum_gas_plasma)]));
        // every ten seconds of its effect (releasedelay): 5 moles of each at 1000 K (anomalies_pyroclastic.dm:35)
        Assert.Equal(FALSE, r.In(() => Dm.num(Waiting.NoSleep(pyro.anomalyEffect(4)))));
        Assert.Equal(TRUE, r.In(() => Dm.num(Waiting.NoSleep(pyro.anomalyEffect(6)))));
        Assert.InRange(r.In(() => Dm.num(air.moles[typeof(datum_gas_plasma)])) - plasma0, 4.99f, 5.01f);
        Assert.True(r.In(() => air.temperature) > 293.15f);
        // its detonation breathes 500 of each (the slime it makes is the slimes', M10)
        r.In(() => Waiting.NoSleep(pyro.detonate()));
        Assert.True(r.In(() => Dm.num(air.moles[typeof(datum_gas_plasma)])) - plasma0 > 500);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHallucinationAnomalyHasDecoysButNotTheSupermattersOne()
    {
        AudioRoom r = Room(7955);
        obj_effect_anomaly_hallucination sm = Make<obj_effect_anomaly_hallucination_supermatter>(r, null, FALSE);
        Assert.Empty(r.In(() => Dm.each<obj_effect_anomaly_hallucination_decoy>().ToList()));
        r.In(() => qdel(sm));
        // a plain one spawns decoys around it, 35% of the eight tiles each (anomalies_hallucination.dm:58)
        r.In(() =>
        {
            for (int i = 0; i < 6; i++)
                Dm.@new<obj_effect_anomaly_hallucination>(locate(X, Y, 1));
        });
        List<obj_effect_anomaly_hallucination_decoy> decoys = r.In(() => Dm.each<obj_effect_anomaly_hallucination_decoy>().ToList());
        Assert.NotEmpty(decoys);
        Assert.True(r.In(() => HAS_TRAIT(decoys[0], TRAIT_ILLUSORY_EFFECT)));
        Assert.NotNull(r.In(() => decoys[0].report_text));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSupermatterSpawnsEachKindNearTheSpot()
    {
        AudioRoom r = Room(7956);
        foreach (string kind in new[] { FLUX_ANOMALY, GRAVITATIONAL_ANOMALY, PYRO_ANOMALY, HALLUCINATION_ANOMALY })
            r.In(() => supermatter_anomaly_gen(locate(X, Y, 1), kind, 1));
        r.In(() =>
        {
            Assert.Single(Dm.each<obj_effect_anomaly_flux>().ToList());
            Assert.Single(Dm.each<obj_effect_anomaly_grav>().ToList());
            Assert.Single(Dm.each<obj_effect_anomaly_pyro>().ToList());
            Assert.Single(Dm.each<obj_effect_anomaly_hallucination_supermatter>().ToList());
            foreach (obj_effect_anomaly a in Dm.each<obj_effect_anomaly>())
            {
                Assert.InRange(get_dist(a, locate(X, Y, 1)), 0, 1);
                Assert.Null(a.anomaly_core); // has_changed_lifespan's anomalies drop no core
                Assert.InRange(Dm.num(a.lifespan), 15 * SECONDS, 35 * SECONDS);
            }
        });
        r.AssertNoRuntimes();
    }
}
