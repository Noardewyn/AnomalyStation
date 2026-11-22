using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice B: the core medicines (D182) at tg's numbers. One metabolism of a body's blood (a Life tick, seconds_per_tick 2)
/// metabolizes metabolization_rate * 2 units (a medicine's REAGENT_REVERSE_METABOLISM divides by metabolism_efficiency 1), so its
/// metabolization_ratio (REM * the volume) is the rate in REAGENTS_METABOLISM: 0.5 for the category 2 medicines, 0.25 for
/// epinephrine, salbutamol, omnizine and sanguirite. Every effect below is tg's coefficient * that ratio * 2.
/// </summary>
public class MedicineTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        // tg's strings/ files (the scars' texts of a wound, the tips of mannitol's overdose), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.In(() =>
        {
            for (int x = 2; x <= 8; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static datum_reagents Blood(mob_living_carbon who) => (datum_reagents)who.reagents;

    private static void Add(mob_living_carbon who, Type reagent, float units) => Waiting.NoSleep(Blood(who).add_reagent(reagent, units));

    private static void Metabolize(mob_living_carbon who) => Waiting.NoSleep(Blood(who).metabolize(who, SSMOBS_DT(), can_overdose: TRUE));

    private static float Num(object x) => Dm.num(x);

    private static obj_item_bodypart Part(mob_living_carbon who, string zone) => (obj_item_bodypart)who.get_bodypart(zone);

    private static void Hurt(mob_living_carbon who, string zone, float brute, float burn)
    {
        Waiting.NoSleep(Part(who, zone).receive_damage(brute, burn, wound_bonus: CANT_WOUND));
        Waiting.NoSleep(who.updatehealth());
    }

    [Fact]
    public void LibitalHealsThreeBruteATickAndHurtsTheLiver()
    {
        var r = Room(8201);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Hurt(d, BODY_ZONE_CHEST, 40, 0);
            Add(d, typeof(datum_reagent_medicine_c2_libital), 10);
            float liver = Num(d.get_organ_loss(ORGAN_SLOT_LIVER));
            Metabolize(d);
            // -3 * normalise_creation_purity (0.75 / 0.75) * 0.5 * 2 brute, 0.3 * 0.5 * 2 liver damage (cat2_medicine_reagents.dm:113)
            Assert.Equal(37f, Num(d.get_brute_loss()), 3);
            Assert.Equal(liver + 0.3f, Num(d.get_organ_loss(ORGAN_SLOT_LIVER)), 3);
            Assert.Equal(9.8f, Num(Blood(d).get_reagent_amount(typeof(datum_reagent_medicine_c2_libital))), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EpinephrineBelowCritStopsTheCritDamageAndTakesLosebreath()
    {
        var r = Room(8202);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.adjust_tox_loss(110));
            Assert.True(d.health <= d.crit_threshold, $"health {d.health}");
            d.losebreath = 10;
            Add(d, typeof(datum_reagent_medicine_epinephrine), 10);
            Metabolize(d);
            // metabolized_traits: TRAIT_NOCRITDAMAGE, so the crit's oxygen damage (carbon/life.dm handle_critical) stops
            Assert.True(HAS_TRAIT(d, TRAIT_NOCRITDAMAGE));
            // losebreath - 4 * 0.25 * 2; below crit -1 * 0.25 * 2 toxin
            Assert.Equal(8f, d.losebreath, 3);
            Assert.Equal(109.5f, Num(d.get_tox_loss()), 3);

            // purged: the trait leaves with it
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_medicine_epinephrine), 100));
            Assert.False(HAS_TRAIT(d, TRAIT_NOCRITDAMAGE));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SalbutamolClearsLosebreathAndOxygenDamage()
    {
        var r = Room(8203);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            d.losebreath = 3;
            Waiting.NoSleep(d.adjust_oxy_loss(20));
            Add(d, typeof(datum_reagent_medicine_salbutamol), 10);
            Metabolize(d);
            // -6 * 0.25 * 2 oxygen, losebreath - 4 * 0.25 * 2 (medicine_reagents.dm:591)
            Assert.Equal(17f, Num(d.get_oxy_loss()), 3);
            Assert.Equal(1f, d.losebreath, 3);
            Metabolize(d);
            Assert.Equal(0f, d.losebreath, 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SanguiriteLowersTheBloodiestSlashsFlowAndThinsTheBleeding()
    {
        var r = Room(8204);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            var slash = (datum_wound)Waiting.NoSleep(d.cause_wound_of_type_and_severity(WOUND_SLASH, Part(d, BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE));
            float flow = Num(slash.blood_flow); // moderate: its 1u of determination stays under the combat high's 2.5u (no bleed modifier of its own)
            Add(d, typeof(datum_reagent_medicine_coagulant), 10);
            Metabolize(d);
            // coagulant_effect(METABOLIZE_FREE_CONSTANT(0.5) * 0.3 * 0.25 * 2): 0.5 * 0.2 / 0.05 = 2, so 0.3 off the flow
            Assert.Equal(flow - 0.3f, Num(slash.blood_flow), 3);
            // on_mob_metabolize: MODIFY_PHYSIOLOGY(PHYS_COEFF_BLEED, 0.7)
            Assert.Equal(0.7f, Num(GET_PHYSIOLOGY(d, PHYS_COEFF_BLEED)), 3);
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_medicine_coagulant), 100));
            Assert.Equal(1f, Num(GET_PHYSIOLOGY(d, PHYS_COEFF_BLEED)), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PenthriteKeepsAStoppedHeartPatientAliveAndBurnsHisHeart()
    {
        var r = Room(8205);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            d.set_heartattack(TRUE);
            Assert.Equal(TRUE, d.undergoing_cardiac_arrest());
            Waiting.NoSleep(d.adjust_tox_loss(110)); // health -10: under crit, over penthrite's -30 * 2 * 1 floor
            Assert.Equal(-10f, d.health, 3);
            Add(d, typeof(datum_reagent_medicine_c2_penthrite), 10);
            float heart = Num(d.get_organ_loss(ORGAN_SLOT_HEART));
            Metabolize(d);
            // TRAIT_STABLEHEART: no heart needed, so no cardiac arrest (carbon/life.dm needs_heart)
            Assert.True(HAS_TRAIT(d, TRAIT_STABLEHEART));
            Assert.Equal(FALSE, d.undergoing_cardiac_arrest());
            Assert.True(HAS_TRAIT(d, TRAIT_NOSOFTCRIT) && HAS_TRAIT(d, TRAIT_NOHARDCRIT));
            // -2 * 0.5 * 2 toxin; the heart takes 1 * max(9.8 / 10, 1) * 0.5 * 2
            Assert.Equal(108f, Num(d.get_tox_loss()), 3);
            Assert.Equal(heart + 1f, Num(d.get_organ_loss(ORGAN_SLOT_HEART)), 3);
            Assert.NotNull(d.alerts["penthrite"]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MorphineMakesDrowsyFromItsThirtySixthCycleAndOverdosesAtThirty()
    {
        var r = Room(8206);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Add(d, typeof(datum_reagent_medicine_morphine), 10);
            var morphine = (datum_reagent)Blood(d).has_reagent(typeof(datum_reagent_medicine_morphine));
            Metabolize(d);
            Assert.Null(d.has_status_effect(typeof(datum_status_effect_drowsiness)));
            Assert.True(Dm.truthy(d.movespeed_mod_immunities), "the damage slowdown immunity");

            morphine.current_cycle = 36; // 7.5u to 10u: adjust_drowsiness_up_to(2 SECONDS * 0.5 * 2, 12 SECONDS)
            Metabolize(d);
            var drowsy = (datum_status_effect)d.has_status_effect(typeof(datum_status_effect_drowsiness));
            Assert.NotNull(drowsy);
            Assert.Equal(2 * SECONDS, Num(drowsy.duration), 3);
            // the drowsiness blurs the eyes (drowsiness.dm:42), the view's blur on the client
            Waiting.NoSleep(drowsy.tick(2));
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_eye_blur)));

            Add(d, typeof(datum_reagent_medicine_morphine), 25);
            Metabolize(d);
            Assert.Equal(TRUE, morphine.overdosed);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void DizzinessDecaysFiveTimesFasterRestingAndEndsWithDeath()
    {
        var r = Room(8207);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.set_dizzy_if_lower(10 * SECONDS));
            var dizzy = (datum_status_effect)d.has_status_effect(typeof(datum_status_effect_dizziness));
            Assert.Equal(10 * SECONDS, Num(dizzy.duration), 3);
            d.resting = TRUE;
            Waiting.NoSleep(dizzy.tick(2)); // remove_duration((5 - 1) * 2)
            Assert.Equal(2 * SECONDS, Num(dizzy.duration), 3);
            Waiting.NoSleep(d.death());
            Assert.Null(d.has_status_effect(typeof(datum_status_effect_dizziness)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHeartAttacksStagesEndInACardiacArrestAndShocksPushItBack()
    {
        var r = Room(8208);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.apply_status_effect(typeof(datum_status_effect_heart_attack)));
            var attack = (datum_status_effect_heart_attack)d.has_status_effect(typeof(datum_status_effect_heart_attack));
            Assert.Equal(150f, attack.time_until_stoppage);

            // each tick takes one off; shocks put time back (defib 50, minor shock 18, electrocution 20 + 1.15 * damage)
            Waiting.NoSleep(attack.tick(2));
            Assert.Equal(149f, attack.time_until_stoppage);
            d._SendSignal(COMSIG_HEARTATTACK_DEFIB, Dm.list(d));
            Assert.Equal(199f, attack.time_until_stoppage);
            d._SendSignal(COMSIG_LIVING_MINOR_SHOCK, Dm.list(d));
            Assert.Equal(217f, attack.time_until_stoppage);

            // over ATTACK_CURE_THRESHOLD (160) the attack is cured
            Waiting.NoSleep(attack.tick(2));
            Assert.True(QDELETED(attack));

            // stage three (70 and under): it shows (TRAIT_DISEASELIKE_SEVERITY_HIGH)
            Waiting.NoSleep(d.apply_status_effect(typeof(datum_status_effect_heart_attack)));
            attack = (datum_status_effect_heart_attack)d.has_status_effect(typeof(datum_status_effect_heart_attack));
            attack.time_until_stoppage = 70;
            Waiting.NoSleep(attack.tick(2));
            Assert.Equal(TRUE, attack.visible);
            Assert.True(HAS_TRAIT(d, TRAIT_DISEASELIKE_SEVERITY_HIGH));

            // at 0 the heart stops, the desperation keeps the body up for a moment
            attack.time_until_stoppage = 0;
            Waiting.NoSleep(attack.tick(2));
            Assert.True(QDELETED(attack));
            Assert.False(HAS_TRAIT(d, TRAIT_DISEASELIKE_SEVERITY_HIGH));
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_heart_desperation)));
            Assert.True(HAS_TRAIT(d, TRAIT_STABLEHEART));
            d.remove_status_effect(typeof(datum_status_effect_heart_desperation));
            Assert.Equal(TRUE, d.undergoing_cardiac_arrest());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SalineGlucoseDilutesTheBloodAndItsStacksGrow()
    {
        var r = Room(8209);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Add(d, typeof(datum_reagent_medicine_salglu_solution), 10);
            var dilution = (datum_status_effect_stacking_saline_glucose_dilution)d.has_status_effect(typeof(datum_status_effect_stacking_saline_glucose_dilution));
            Assert.NotNull(dilution);
            Assert.Equal(1f, dilution.stacks);
            Assert.Equal(5 - 0.02f, dilution.get_blood_multiplier(), 3);
            d.set_blood_volume(BLOOD_VOLUME_BAD);
            // get_blood_volume(apply_modifiers): + saline.volume * the multiplier (blood.dm:39-43)
            Assert.Equal(BLOOD_VOLUME_BAD + 10 * (5 - 0.02f), Num(d.get_blood_volume(apply_modifiers: TRUE)), 2);
            Waiting.NoSleep(dilution.tick(2)); // saline in the blood: add_stacks(seconds)
            Assert.Equal(3f, dilution.stacks);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ANewWoundsSecondWindPutsDeterminationInTheBlood()
    {
        var r = Room(8210);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.cause_wound_of_type_and_severity(WOUND_SLASH, Part(d, BODY_ZONE_L_ARM), WOUND_SEVERITY_SEVERE));
            // WOUND_DETERMINATION_SEVERE (_wounds.dm:485)
            Assert.Equal(2.5f, Num(Blood(d).get_reagent_amount(typeof(datum_reagent_determination))), 3);
            Metabolize(d);
            // >= 2.5u: the combat high (the determined status effect) and TRAIT_ANALGESIA
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_determined)));
            Assert.True(HAS_TRAIT(d, TRAIT_ANALGESIA));
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_determination), 100));
            Assert.Null(d.has_status_effect(typeof(datum_status_effect_determined)));
        });
        r.AssertNoRuntimes();
    }

    /// <summary>The rest of the core set, one dummy each: one of tg's numbers per medicine.</summary>
    [Fact]
    public void TheRestOfTheCoreSetAtTgsNumbers()
    {
        var r = Room(8211);
        r.In(() =>
        {
            int n = 0;
            mob_living_carbon_human Fresh()
            {
                int i = n++;
                return Dm.@new<mob_living_carbon_human>(locate(2 + i % 7, 2 + i / 7, 1));
            }

            // oxandrolone: burns over 25 heal 4 * 0.5 * 2
            var d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 0, 40);
            Add(d, typeof(datum_reagent_medicine_oxandrolone), 5);
            Metabolize(d);
            Assert.Equal(36f, Num(d.get_fire_loss()), 3);

            // salicylic acid: bruises of 25 and under heal 0.5 * 0.5 * 2
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 20, 0);
            Add(d, typeof(datum_reagent_medicine_sal_acid), 5);
            Metabolize(d);
            Assert.Equal(19.5f, Num(d.get_brute_loss()), 3);

            // pentetic acid: -2 * 0.5 * 2 toxin, and purges 2 * 0.5 * 2 of every other reagent
            d = Fresh();
            Waiting.NoSleep(d.adjust_tox_loss(10));
            Add(d, typeof(datum_reagent_medicine_pen_acid), 5);
            Add(d, typeof(datum_reagent_water), 10);
            Metabolize(d);
            Assert.Equal(8f, Num(d.get_tox_loss()), 3);
            Assert.Equal(10 - 2 - 0.4f, Num(Blood(d).get_reagent_amount(typeof(datum_reagent_water))), 3);

            // potassium iodide: halts the radiation's effects while metabolized
            d = Fresh();
            Add(d, typeof(datum_reagent_medicine_potass_iodide), 5);
            Metabolize(d);
            Assert.True(HAS_TRAIT(d, TRAIT_HALT_RADIATION_EFFECTS));

            // atropine: losebreath to 0
            d = Fresh();
            d.losebreath = 5;
            Add(d, typeof(datum_reagent_medicine_atropine), 5);
            Metabolize(d);
            Assert.Equal(0f, d.losebreath);

            // spaceacillin: virus resistance while in the blood
            d = Fresh();
            Add(d, typeof(datum_reagent_medicine_spaceacillin), 5);
            Assert.True(HAS_TRAIT(d, TRAIT_VIRUS_RESISTANCE));

            // mannitol: brain -1 * 1 * 2 * (1 / 0.75)
            d = Fresh();
            Waiting.NoSleep(d.adjust_organ_loss(ORGAN_SLOT_BRAIN, 20));
            Add(d, typeof(datum_reagent_medicine_mannitol), 5);
            Metabolize(d);
            Assert.Equal(20 - 2 / 0.75f, Num(d.get_organ_loss(ORGAN_SLOT_BRAIN)), 2);

            // leporazine: a cold body warms by 20 * TEMPERATURE_DAMAGE_COEFFICIENT * 1 * 2
            d = Fresh();
            d.bodytemperature = 200;
            Add(d, typeof(datum_reagent_medicine_leporazine), 5);
            Metabolize(d);
            Assert.Equal(200 + 20 * TEMPERATURE_DAMAGE_COEFFICIENT * 2, d.bodytemperature, 2);

            // omnizine: every damage -2 * 0.5 * 0.25 * 2
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 10, 10);
            Add(d, typeof(datum_reagent_medicine_omnizine), 5);
            Metabolize(d);
            Assert.Equal(9.5f, Num(d.get_brute_loss()), 3);
            Assert.Equal(9.5f, Num(d.get_fire_loss()), 3);

            // inacusiate: the ears heal 2 * (1 / 0.75) * 1 * 2
            d = Fresh();
            var ears = (obj_item_organ_ears)d.get_organ_slot(ORGAN_SLOT_EARS);
            Waiting.NoSleep(ears.apply_organ_damage(20));
            Add(d, typeof(datum_reagent_medicine_inacusiate), 5);
            Metabolize(d);
            Assert.Equal(20 - 4 / 0.75f, Num(ears.damage), 2);

            // oculine: the eyes heal 1 * 1 * 2 * (1 / 0.75)
            d = Fresh();
            var eyes = (obj_item_organ_eyes)d.get_organ_slot(ORGAN_SLOT_EYES);
            Waiting.NoSleep(eyes.apply_organ_damage(5));
            Add(d, typeof(datum_reagent_medicine_oculine), 5);
            Metabolize(d);
            Assert.Equal(5 - 2 / 0.75f, Num(eyes.damage), 2);

            // aiuri: burns -2 * 0.5 * 2, the eyes take 0.25 * 0.5 * 2
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 0, 10);
            Add(d, typeof(datum_reagent_medicine_c2_aiuri), 5);
            Metabolize(d);
            Assert.Equal(8f, Num(d.get_fire_loss()), 3);
            Assert.Equal(0.25f, Num(d.get_organ_loss(ORGAN_SLOT_EYES)), 3);

            // lenturi: burns -3.75 * 0.5 * 2, the stomach takes 0.4 * 0.5 * 2
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 0, 10);
            float stomach = Num(d.get_organ_loss(ORGAN_SLOT_STOMACH));
            Add(d, typeof(datum_reagent_medicine_c2_lenturi), 5);
            Metabolize(d);
            Assert.Equal(round(10 - 3.75f, DAMAGE_PRECISION), Num(d.get_fire_loss()), 3); // the limb keeps DAMAGE_PRECISION
            Assert.Equal(stomach + 0.4f, Num(d.get_organ_loss(ORGAN_SLOT_STOMACH)), 3);

            // probital: brute -3 * 0.5 * 2, stamina + 3.5 * 0.5 * 2
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 10, 0);
            Add(d, typeof(datum_reagent_medicine_c2_probital), 5);
            Metabolize(d);
            Assert.Equal(7f, Num(d.get_brute_loss()), 3);
            Assert.Equal(3.5f, Num(d.get_stamina_loss()), 3);

            // hercuri: burns under 50 heal 2.25 * 0.5 * 2, the fire stacks drop 1 * 0.5 * 2
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 0, 10);
            Add(d, typeof(datum_reagent_medicine_c2_hercuri), 5);
            Metabolize(d);
            Assert.Equal(round(10 - 2.25f, DAMAGE_PRECISION), Num(d.get_fire_loss()), 3);

            // convermol: the first cycle heals nothing (2.5 * 0.5 * (1 - 1)); the second -2 * 1.25 * 0.5 * 2
            d = Fresh();
            Waiting.NoSleep(d.adjust_oxy_loss(20));
            Add(d, typeof(datum_reagent_medicine_c2_convermol), 5);
            Metabolize(d);
            Assert.Equal(20f, Num(d.get_oxy_loss()), 3);
            Metabolize(d);
            Assert.Equal(17.5f, Num(d.get_oxy_loss()), 3);

            // tirimol: oxygen -4.5 * 0.5 * 2, stamina + 2 * 0.5 * 2
            d = Fresh();
            Waiting.NoSleep(d.adjust_oxy_loss(20));
            Add(d, typeof(datum_reagent_medicine_c2_tirimol), 5);
            Metabolize(d);
            Assert.Equal(15.5f, Num(d.get_oxy_loss()), 3);
            Assert.Equal(2f, Num(d.get_stamina_loss()), 3);

            // seiver at the blood's 310.15 K: min(round(5 + (310.15 - 1000) / 175, 0.1), 5) * 0.5 * 2 = 1.1 toxin healed
            d = Fresh();
            Waiting.NoSleep(d.adjust_tox_loss(10));
            Add(d, typeof(datum_reagent_medicine_c2_seiver), 5);
            Blood(d).chem_temp = 310.15f; // after the add, which mixes in the reagent's 300 K
            Metabolize(d);
            Assert.Equal(10 - 1.1f, Num(d.get_tox_loss()), 2);

            // multiver alone: toxin -0.5 * 1 * 0.5 * 2, the lungs take 0.5 * 0.5 * 2
            d = Fresh();
            Waiting.NoSleep(d.adjust_tox_loss(10));
            float lungs = Num(d.get_organ_loss(ORGAN_SLOT_LUNGS));
            Add(d, typeof(datum_reagent_medicine_c2_multiver), 5);
            Metabolize(d);
            Assert.Equal(9.5f, Num(d.get_tox_loss()), 3);
            Assert.Equal(lungs + 0.5f, Num(d.get_organ_loss(ORGAN_SLOT_LUNGS)), 3);

            // syriniver (rate 0.75): toxin -1.34 * 0.75 * 2, the liver takes 0.54 * 0.75 * 2
            d = Fresh();
            Waiting.NoSleep(d.adjust_tox_loss(10));
            float liver = Num(d.get_organ_loss(ORGAN_SLOT_LIVER));
            Add(d, typeof(datum_reagent_medicine_c2_syriniver), 5);
            Metabolize(d);
            Assert.Equal(10 - 2.01f, Num(d.get_tox_loss()), 3);
            Assert.Equal(liver + 0.81f, Num(d.get_organ_loss(ORGAN_SLOT_LIVER)), 3);

            // synthflesh on a patch: 1.25 * 10 brute and burn healed, (12.5 + 12.5) * (0.5 + 0.25 * (1 - 0.75)) toxin
            d = Fresh();
            Hurt(d, BODY_ZONE_CHEST, 20, 20);
            var patch = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(patch.add_reagent(typeof(datum_reagent_medicine_c2_synthflesh), 10));
            Waiting.NoSleep(patch.expose(d, PATCH));
            Assert.Equal(7.5f, Num(d.get_brute_loss()), 3);
            Assert.Equal(7.5f, Num(d.get_fire_loss()), 3);
            Assert.Equal(25 * 0.5625f, Num(d.get_tox_loss()), 3);

            // toxin: METABOLIZE_FREE_CONSTANT(0.5) * 1.5 * 1 * 1 * 2 (toxin_reagents.dm:27)
            d = Fresh();
            Add(d, typeof(datum_reagent_toxin), 5);
            Metabolize(d);
            Assert.Equal(1.5f, Num(d.get_tox_loss()), 3);

            // formaldehyde (rate 0.1, toxpwr 1): 0.5 * 0.2 / 0.1 * 1 * 0.5 * 2 unless it turns to histamine
            d = Fresh();
            Add(d, typeof(datum_reagent_toxin_formaldehyde), 5);
            Metabolize(d);
            Assert.True(Num(d.get_tox_loss()) >= 1f - 0.001f, $"formaldehyde's toxin {Num(d.get_tox_loss())}");

            // iron and blood: the base reagent's metabolism at their rates (0.2 and 12.5 * 0.2 a second)
            d = Fresh();
            Add(d, typeof(datum_reagent_iron), 5);
            Add(d, typeof(datum_reagent_blood), 10);
            Metabolize(d);
            Assert.Equal(4.6f, Num(Blood(d).get_reagent_amount(typeof(datum_reagent_iron))), 3);
            Assert.Equal(5f, Num(Blood(d).get_reagent_amount(typeof(datum_reagent_blood))), 3);

            // Pump-Up: baton resistance while metabolized, the jitter at once
            d = Fresh();
            Add(d, typeof(datum_reagent_drug_pumpup), 5);
            Metabolize(d);
            Assert.True(HAS_TRAIT(d, TRAIT_BATON_RESISTANCE));
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_jitter)));

            // capsaicin (a consumable: a fixed rate): the first cycles heat 0.5 * TEMPERATURE_DAMAGE_COEFFICIENT * 5 * 1 * 2
            d = Fresh();
            float temperature = d.bodytemperature;
            Add(d, typeof(datum_reagent_consumable_capsaicin), 5);
            Metabolize(d);
            Assert.Equal(temperature + 0.5f * TEMPERATURE_DAMAGE_COEFFICIENT * 5 * 2, d.bodytemperature, 2);

            // condensed capsaicin sprayed in an unprotected face: knocked down, blinded a while, no smell
            d = Fresh();
            var spray = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(spray.add_reagent(typeof(datum_reagent_consumable_condensedcapsaicin), 10));
            Waiting.NoSleep(spray.expose(d, TOUCH));
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown)));
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_temporary_blindness)));
            Assert.True(HAS_TRAIT(d, TRAIT_ANOSMIA));
        });
        r.AssertNoRuntimes();
    }
}
