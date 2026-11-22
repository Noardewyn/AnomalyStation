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
/// The M7 supermatter slice (G5, D163): tg's supermatter crystal (power/supermatter/*.dm and datums/components/supermatter_crystal.dm).
/// The emitter's beam energises it (eat_bullets), its energy decays as calculate_internal_energy computes (the cubic loss below the
/// linear threshold, the linear one above), the gases change its heat, waste and power (supermatter_gas.dm), it heats its tile's gas
/// and releases plasma and oxygen (process_atmos part 5), it zaps the tesla coils, which put the energy on their network, it takes
/// damage above its heat limit and heals below it, it dusts a man who touches or walks into it and consumes a thrown item, and it
/// pulses radiation. tg has no unit test of the crystal (unit_tests/atmospherics_sanity.dm only names its area).
/// </summary>
public class SupermatterTests
{
    private const int Second = 20;
    private const int Width = 13, Height = 9, SmX = 7, SmY = 5;

    private static AudioRoom Room(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(Width, Height, seed: seed, mapExtras: extras);
        r.Step(Second);
        return r;
    }

    /// <summary>A crystal on (x, y); frozen, SSair's machinery run skips it (the time stop's SM_PROCESS_TIMESTOP, supermatter.dm:546),
    /// so the tests call process_atmos themselves. Its activation is logged, as the tests power it by hand.</summary>
    private static obj_machinery_power_supermatter_crystal Crystal(AudioRoom r, int x = SmX, int y = SmY, bool frozen = true) => r.In(() =>
    {
        var sm = Dm.@new<obj_machinery_power_supermatter_crystal_engine>(locate(x, y, 1));
        if (frozen)
            sm.time_frozen();
        sm.activation_logged = TRUE;
        return sm;
    });

    /// <summary>One process_atmos of a frozen crystal, as SSair's machinery run calls it.</summary>
    private static object Process(AudioRoom r, obj_machinery_power_supermatter_crystal sm) => r.In(() =>
    {
        sm.time_unfrozen();
        object result = sm.process_atmos(SSair.wait / 10f);
        sm.time_frozen();
        return result;
    });

    private static datum_gas_mixture TileAir(AudioRoom r, int x, int y) => r.In(() => ((turf_open)locate(x, y, 1)).air);

    /// <summary>Sets the tile's gas to the mix (a gas string, as the maps' initial_gas_mix).</summary>
    private static void SetAir(AudioRoom r, int x, int y, string mix) => r.In(() =>
    {
        datum_gas_mixture air = ((turf_open)locate(x, y, 1)).air;
        air.moles = Dm.list();
        air.temperature = TCMB;
        air.merge(SSair.parse_gas_string(mix) as datum_gas_mixture);
        datum_gas_mixture parsed = SSair.parse_gas_string(mix) as datum_gas_mixture;
        air.temperature = parsed.temperature;
    });


    /// <summary>tg's power loss (supermatter.dm:716-719) of a momentary power.</summary>
    private static float TgPowerLoss(float momentary)
    {
        float threshold = MathF.Sqrt(POWERLOSS_LINEAR_RATE / 3 * MathF.Pow(POWERLOSS_CUBIC_DIVISOR, 3));
        float offset = -1 * threshold * POWERLOSS_LINEAR_RATE + MathF.Pow(threshold / POWERLOSS_CUBIC_DIVISOR, 3);
        return momentary < threshold ? -MathF.Pow(momentary / POWERLOSS_CUBIC_DIVISOR, 3) : -(momentary * POWERLOSS_LINEAR_RATE + offset);
    }

    [Fact]
    public void TheEmittersBeamEnergisesTheCrystal()
    {
        AudioRoom r = Room(7801);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        SetAir(r, SmX, SmY, "n2=100;TEMP=293.15");
        // a hitscan emitter beam (30 damage, emitter.dm:45) from the west: eat_bullets takes it before bullet_act
        obj_projectile_beam_emitter_hitscan beam = r.In(() =>
        {
            var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(SmX - 3, SmY, 1));
            Waiting.NoSleep(p.fire(90));
            return p;
        });
        Assert.True(r.In(() => QDELETED(beam)), "the crystal eats the beam (supermatter_hit_procs.dm:53)");
        Assert.Equal(30 * SUPERMATTER_DEFAULT_BULLET_ENERGY, r.In(() => sm.external_power_immediate));
        Assert.Equal(0f, r.In(() => sm.external_damage_immediate)); // a laser is no BULLET: it only powers

        // the next cycle adds it as SM_POWER_EXTERNAL_IMMEDIATE and takes the decay off the sum (supermatter.dm:705-726)
        Process(r, sm);
        DmList factors = r.In(() => sm.internal_energy_factors);
        Assert.Equal(60f, r.In(() => Dm.num(factors[SM_POWER_EXTERNAL_IMMEDIATE])));
        Assert.Equal(0f, r.In(() => sm.external_power_immediate));
        Assert.True(r.In(() => sm.internal_energy) > 50, "the crystal holds the beam's energy: " + r.In(() => sm.internal_energy));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePowerDecaysCubicallyThenLinearlyAsTgComputes()
    {
        AudioRoom r = Room(7802);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        // the linear threshold: sqrt(0.83 / 3 * 500^3) (supermatter.dm:237), with an offset that makes the change smooth
        float threshold = r.In(() => sm.powerloss_linear_threshold);
        Assert.InRange(threshold, 5880, 5882);
        foreach (float energy in new[] { 100f, 1000f, 3000f, threshold + 1000, 9000f })
        {
            float after = r.In(() =>
            {
                sm.internal_energy = energy;
                sm.gas_heat_power_generation = 0;
                sm.gas_powerloss_inhibition = 0;
                sm.psy_coeff = 0;
                sm.absorbed_gasmix = Dm.@new<datum_gas_mixture>();
                sm.calculate_internal_energy();
                return sm.internal_energy;
            });
            float expected = MathF.Max(energy + TgPowerLoss(energy), 0);
            Assert.InRange(after, expected - Math.Max(0.01f, energy * 1e-5f), expected + Math.Max(0.01f, energy * 1e-5f));
        }
        // CO2's full powerloss inhibition gives the loss back (SM_POWER_POWERLOSS_GAS)
        float inhibited = r.In(() =>
        {
            sm.internal_energy = 3000;
            sm.gas_powerloss_inhibition = 1;
            sm.calculate_internal_energy();
            return sm.internal_energy;
        });
        Assert.InRange(inhibited, 2999.99f, 3000.01f);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("n2", -2.5f, 0, 0, -1)]
    [InlineData("o2", 0, 0.15f, 0, 1)]
    [InlineData("co2", 1, 0, 0, 1)]
    [InlineData("plasma", 14, 0.4f, 0, 1)]
    [InlineData("n2o", 0, 0, 5, 0)]
    [InlineData("tritium", 9, 3, 0, 1)]
    [InlineData("water_vapor", 11, -0.25f, 0, 1)]
    public void APureGasGivesItsSmGasFactors(string gas, float heatModifier, float transmission, float heatResistance, float heatPower)
    {
        AudioRoom r = Room(7803);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        r.In(() =>
        {
            sm.absorbed_gasmix = SSair.parse_gas_string(gas + "=100;TEMP=293.15") as datum_gas_mixture;
            sm.calculate_gases();
        });
        Assert.InRange(r.In(() => sm.gas_heat_modifier), heatModifier - 1e-4f, heatModifier + 1e-4f);
        Assert.InRange(r.In(() => sm.gas_power_transmission_rate), transmission - 1e-4f, transmission + 1e-4f);
        Assert.InRange(r.In(() => sm.gas_heat_resistance), heatResistance - 1e-4f, heatResistance + 1e-4f);
        // clamped to 0..1 (supermatter.dm:683)
        Assert.InRange(r.In(() => sm.gas_heat_power_generation), Math.Clamp(heatPower, 0, 1) - 1e-4f, Math.Clamp(heatPower, 0, 1) + 1e-4f);
        Assert.Equal(1f, r.In(() => Dm.num(sm.gas_percentage[r.In(() => (object)gas_id2path(gas))])));

        // the heat modifier makes the waste multiplier (1 + modifier, at least 0.5) and the transmission the zap rate
        r.In(() =>
        {
            sm.calculate_waste_multiplier();
            sm.calculate_zap_transmission_rate();
        });
        Assert.InRange(r.In(() => sm.waste_multiplier), MathF.Max(1 + heatModifier, 0.5f) - 1e-3f, MathF.Max(1 + heatModifier, 0.5f) + 1e-3f);
        Assert.InRange(r.In(() => sm.zap_transmission_rate), BASE_POWER_TRANSMISSION_RATE * (1 + transmission) - 0.1f, BASE_POWER_TRANSMISSION_RATE * (1 + transmission) + 0.1f);
        // N2O raises the heat limit: (T0C + 40) * (1 + resistance), plus the low moles term at 100 moles (supermatter.dm:820)
        r.In(() => sm.calculate_temp_limit());
        float expectedLimit = (T0C + HEAT_PENALTY_THRESHOLD) * (1 + heatResistance) + Math.Clamp(2 - 100 / 100f, 0, 1) * (T0C + HEAT_PENALTY_THRESHOLD);
        Assert.InRange(r.In(() => sm.temp_limit), expectedLimit - 0.01f, expectedLimit + 0.01f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OxygenHeatGivesPowerAndNitrogenNone()
    {
        AudioRoom r = Room(7804);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        float Heat(string mix) => r.In(() =>
        {
            sm.internal_energy = 0;
            sm.absorbed_gasmix = SSair.parse_gas_string(mix) as datum_gas_mixture;
            sm.calculate_gases();
            return Dm.num(((DmList)sm.calculate_internal_energy())[SM_POWER_HEAT]);
        });
        // 1/6 MeV per kelvin times the heat power generation (supermatter.dm:707)
        Assert.InRange(Heat("o2=100;TEMP=600"), 600 / 6f - 0.01f, 600 / 6f + 0.01f);
        Assert.Equal(0f, Heat("n2=100;TEMP=600"));
        // a quarter oxygen: a quarter of the heat (gas_heat_power_generation is the mole-weighted sum, clamped)
        Assert.Equal(0f, Heat("o2=25;n2=75;TEMP=600")); // 0.25 * 1 + 0.75 * -1 < 0: clamped to 0
        Assert.InRange(Heat("o2=75;n2=25;TEMP=600"), 0.5f * 100 - 0.01f, 0.5f * 100 + 0.01f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrystalHeatsItsTileAndReleasesPlasmaAndOxygen()
    {
        AudioRoom r = Room(7805);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        SetAir(r, SmX, SmY, "n2=100;TEMP=293.15");
        datum_gas_mixture air = TileAir(r, SmX, SmY);
        float before = r.In(() => air.temperature);
        r.In(() => sm.internal_energy = 1000);
        Process(r, sm);
        // the waste of the absorbed 15%: N2 gives a waste multiplier of 0.5 (clamped), device energy 0.65 * the energy
        float energy = r.In(() => sm.internal_energy);
        Assert.InRange(r.In(() => sm.waste_multiplier), 0.4999f, 0.5001f);
        Assert.True(r.In(() => air.temperature) > before, $"the tile is heated: {before} -> {r.In(() => air.temperature)}");
        Assert.True(r.In(() => Dm.num(air.moles[typeof(datum_gas_plasma)])) > 0, "plasma is released (supermatter.dm:349)");
        Assert.True(r.In(() => Dm.num(air.moles[typeof(datum_gas_oxygen)])) > 0, "oxygen is released (supermatter.dm:350)");
        // tg's amounts: plasma = device_energy * waste / 650, of the energy after this cycle's calculation
        float device = energy * REACTION_POWER_MODIFIER;
        float plasma = r.In(() => Dm.num(air.moles[typeof(datum_gas_plasma)]));
        Assert.InRange(plasma, device * 0.5f / PLASMA_RELEASE_MODIFIER * 0.999f, device * 0.5f / PLASMA_RELEASE_MODIFIER * 1.001f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ItTakesDamageAboveItsHeatLimitAndHealsBelowIt()
    {
        AudioRoom r = Room(7806);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        float Damage(string mix, float start) => r.In(() =>
        {
            sm.damage = start;
            sm.absorbed_gasmix = SSair.parse_gas_string(mix) as datum_gas_mixture;
            sm.calculate_gases();
            sm.calculate_temp_limit();
            sm.calculate_damage();
            return Dm.num(sm.damage);
        });
        // 2000 moles at 1313.15 K: the limit is T0C + 40 = 313.15 (no low-moles term above 200 moles): (1313.15 - 313.15) / 24000
        float hot = Damage("n2=2000;TEMP=1313.15", 0);
        Assert.InRange(hot, 1000 / 24000f + 200 / 3200f - 1e-4f, 1000 / 24000f + 200 / 3200f + 1e-4f); // with the moles above 1800 (:852)
        // 500 moles at 113.15 K: (113.15 - 313.15) / 6000, clamped at -0.1
        float cold = Damage("n2=500;TEMP=113.15", 10);
        Assert.InRange(cold, 10 - 200 / 6000f - 1e-4f, 10 - 200 / 6000f + 1e-4f);
        Assert.Equal(0f, Damage("n2=500;TEMP=113.15", 0)); // never below 0
        // the heat damage stops at 0.15 a cycle
        Assert.InRange(Damage("n2=500;TEMP=20000", 0), 0.1499f, 0.1501f);
        // the status follows the damage
        Assert.Equal((float)SUPERMATTER_WARNING, r.In(() => { sm.damage = 10; return Dm.num(sm.get_status()); }));
        Assert.Equal((float)SUPERMATTER_DANGER, r.In(() => { sm.damage = 61; return Dm.num(sm.get_status()); }));
        Assert.Equal((float)SUPERMATTER_EMERGENCY, r.In(() => { sm.damage = 80; return Dm.num(sm.get_status()); }));
        Assert.InRange(r.In(() => Dm.num(sm.get_integrity_percent())), 19.99f, 20.01f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ItsZapsChargeTheCoilsThatFeedTheirNetwork()
    {
        const int LineY = 3;
        obj_machinery_power_energy_accumulator_tesla_coil coil = null;
        var r = Room(7807, w =>
        {
            for (int x = SmX - 2; x <= SmX + 2; x++)
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            coil = Dm.@new<obj_machinery_power_energy_accumulator_tesla_coil_anchored>(locate(SmX, LineY, 1));
        });
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        SetAir(r, SmX, SmY, "n2=100;TEMP=293.15");
        datum_powernet net = r.In(() => coil.powernet as datum_powernet);
        Assert.NotNull(net);
        // a crystal at 2000 MeV: its accumulator fills by internal_energy * zap_transmission_rate per second of SSmachines
        // (supermatter.dm:308) and discharges a tenth when the zap is due (4 - energy / 1000 seconds after the last)
        float stored = 0;
        for (int i = 0; i < 40 && stored == 0; i++)
        {
            r.In(() => sm.internal_energy = 2000);
            r.Step(Second / 2);
            Process(r, sm);
            stored = r.In(() => coil.stored_energy + coil.processed_energy);
        }
        Assert.True(stored > 0, "the crystal's bolt reaches the coil");
        Assert.True(r.In(() => Dm.each<datum_beam>().Any(b => b.origin == sm && b.target == coil)), "the bolt is drawn from the crystal to the coil");
        // the coil releases it onto the network over the machines' cycles (energy_accumulator.dm): the network's available power
        // rises from nothing (the coil is its only source)
        float maxAvail = 0;
        for (int i = 0; i < 6; i++)
        {
            r.Step(SSmachinesWait(r) * 2);
            maxAvail = Math.Max(maxAvail, r.In(() => net.avail));
        }
        Assert.True(maxAvail > 0, "the coil puts the crystal's energy on its network");
        r.AssertNoRuntimes();
    }

    private static int SSmachinesWait(AudioRoom r) => r.In(() => (int)Dm.num(SSmachines.wait));

    [Fact]
    public void AManWhoTouchesItIsDusted()
    {
        AudioRoom r = Room(7808);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        mob_living_carbon_human bob = r.Join(SmX - 1, SmY, "Bob");
        r.Step(2);
        r.Command(() => ClientSeam.Click(bob.client, sm, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
        Assert.True(r.In(() => QDELETED(bob)) || r.In(() => bob.stat == DEAD), "Bob is dusted by his touch");
        // a dusted mob adds 100 * (mob_size + 2) * 2 to the trickle (consume, supermatter_crystal.dm:316): a human is MOB_SIZE_HUMAN
        Assert.True(r.In(() => sm.external_power_trickle) > 0 || r.In(() => sm.internal_energy) > 0);
        Assert.True(r.Sounds.Heard.Any(s => s.File == "sound/effects/supermatter.ogg"), "the dusting sounds");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AManWhoWalksIntoItIsDusted()
    {
        AudioRoom r = Room(7809);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        mob_living_carbon_human bob = r.Dummy(SmX - 1, SmY);
        r.In(() => Waiting.NoSleep(step(bob, EAST)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(bob)) || r.In(() => bob.stat == DEAD), "Bob walks into the crystal and is dusted (bumped_hit)");
        Assert.InRange(r.In(() => sm.external_power_trickle), 100 * (MOB_SIZE_HUMAN + 2) * 2 - 1, 100 * (MOB_SIZE_HUMAN + 2) * 2 + 1);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThrownItemIsConsumed()
    {
        AudioRoom r = Room(7810);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        mob_living_carbon_human bob = r.Dummy(SmX - 3, SmY);
        obj_item_wrench wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(SmX - 2, SmY, 1)));
        float wclass = r.In(() => Dm.num(wrench.w_class));
        r.In(() => Waiting.NoSleep(wrench.throw_at(sm, 5, 1, bob)));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(wrench)), "the wrench flashes to ash");
        Assert.InRange(r.In(() => sm.external_power_trickle), 70 * wclass - 0.01f, 70 * wclass + 0.01f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ItPulsesRadiationAsItsPowerAndIntegrityDecide()
    {
        AudioRoom r = Room(7811);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        datum_radiation_pulse_information pulse = r.In(() =>
        {
            SSradiation.processing = Dm.list();
            sm.internal_energy = 1500;
            sm.damage = 0;
            sm.emit_radiation();
            return SSradiation.processing[1] as datum_radiation_pulse_information;
        });
        Assert.NotNull(pulse);
        // full integrity: threshold = 1 - power / 5000 = 0.7 and chance 3% (supermatter_extra_effects.dm:44, :53)
        Assert.InRange(r.In(() => Dm.num(pulse.threshold)), 0.6999f, 0.7001f);
        Assert.InRange(r.In(() => Dm.num(pulse.chance)), 2.999f, 3.001f);
        Assert.Equal(8f, r.In(() => Dm.num(pulse.max_range)));
        // half integrity: the power nudged to 1500, the threshold raised to the (1 / 0.5)^2 = 4th power, the chance up the slope
        pulse = r.In(() =>
        {
            SSradiation.processing = Dm.list();
            sm.internal_energy = 0;
            sm.damage = 50;
            sm.emit_radiation();
            return SSradiation.processing[1] as datum_radiation_pulse_information;
        });
        Assert.InRange(r.In(() => Dm.num(pulse.threshold)), MathF.Pow(0.7f, 4) - 1e-4f, MathF.Pow(0.7f, 4) + 1e-4f);
        Assert.InRange(r.In(() => Dm.num(pulse.chance)), (0.37f * 0.5f + 0.03f) * 100 - 1e-3f, (0.37f * 0.5f + 0.03f) * 100 + 1e-3f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ItGlowsAndHumsAsItsGasAndPowerDecide()
    {
        AudioRoom r = Room(7812);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        SetAir(r, SmX, SmY, "o2=100;TEMP=293.15");
        r.In(() => sm.internal_energy = 1000);
        Process(r, sm);
        // oxygen's heat power generation above 0.8 makes it glow red (supermatter.dm:536, _sm_delam.dm:122, :137)
        Assert.Equal("sm-glow", r.In(() => sm.icon_state));
        Assert.Equal(SUPERMATTER_RED, r.In(() => (string)sm.light_color));
        Assert.True(r.In(() => sm.light_range) >= 4);
        Assert.NotNull(r.In(() => sm.get_filter("ray")));
        // the hum: the loop's volume follows the energy (supermatter_extra_effects.dm:59)
        Assert.InRange(r.In(() => Dm.num(sm.soundloop.volume)), 50, 100);
        Assert.True(r.In(() => Dm.truthy(sm.soundloop.is_active())), "the hum plays");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStrategyFollowsTheGasAndPowerAndTheCountdownStartsAboveTheExplosionPoint()
    {
        AudioRoom r = Room(7813);
        obj_machinery_power_supermatter_crystal sm = Crystal(r);
        // the default is explosive (Initialize, supermatter.dm:204); set_delam's purge picks the first that can_select, in
        // GLOB.sm_delam_list's order: cascade, singularity (1800 moles), tesla (5000 MeV), explosive (_sm_delam.dm:2)
        Assert.IsType<datum_sm_delam_explosive>(r.In(() => sm.delamination_strategy));
        Type Pick(string mix, float energy) => r.In(() =>
        {
            sm.absorbed_gasmix = SSair.parse_gas_string(mix) as datum_gas_mixture;
            sm.internal_energy = energy;
            sm.calculate_gases();
            sm.set_delam(SM_DELAM_PRIO_NONE, null);
            return sm.delamination_strategy.GetType();
        });
        Assert.Equal(typeof(datum_sm_delam_singularity), Pick("n2=2000;TEMP=293.15", 0));
        Assert.Equal(typeof(datum_sm_delam_tesla), Pick("n2=100;TEMP=293.15", 6000));
        Assert.Equal(typeof(datum_sm_delam_explosive), Pick("n2=100;TEMP=293.15", 100));
        // the tesla strategy's look: the cyan light and rays (common_delams.dm:91, :108)
        Pick("n2=100;TEMP=293.15", 6000);
        r.In(() => sm.delamination_strategy.filters(sm));
        Assert.Equal(SUPERMATTER_TESLA_COLOUR, r.In(() => (string)((dm_filter)sm.get_filter("ray")).Get("color")));

        // above the explosion point the countdown starts (supermatter.dm:337): the crystal is delaminating and the causality
        // field shows (_sm_delam.dm:110); healed below it, the countdown stops (:608-622)
        SetAir(r, SmX, SmY, "n2=100;TEMP=293.15");
        r.In(() =>
        {
            sm.internal_energy = 0;
            sm.damage = 101;
        });
        Process(r, sm);
        Assert.True(r.In(() => Dm.truthy(sm.final_countdown)), "the countdown starts");
        Assert.Equal((float)SUPERMATTER_DELAMINATING, r.In(() => Dm.num(sm.get_status())));
        Assert.Contains(r.In(() => Dm.each<object>(sm.delamination_strategy.overlays(sm)).OfType<mutable_appearance>().Select(a => a.icon_state).ToList()), s => s == "causality_field");
        r.In(() => sm.damage = 50);
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(sm.final_countdown)), "healed, the countdown stops");
        r.AssertNoRuntimes();
    }
}
