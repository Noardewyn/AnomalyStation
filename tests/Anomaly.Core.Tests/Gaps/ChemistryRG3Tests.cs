using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RG3 (D333, D339): the chemistry's other products in a booted toy room. The drugs (drug_reagents.dm): space drugs'
/// drugginess and its overdose's hallucinations, nicotine, methamphetamine's speed and overdose, bath salts, krokodil's skin falling
/// off, maintenance tar, and the trips that tint and warp the drugged player's game plane (the mushroom hallucinogen, blastoff,
/// saturnX). The pyrotechnics (pyrotechnic_reagents.dm): thermite melting a wall, welding fuel splashed and lit, chlorine
/// trifluoride, cryostylane and pyrosium with oxygen, flash powder. The other reagents (other_reagents.dm): water on a burning body
/// and a hot floor, space cleaner, lube, hair dye and colorful reagent, holy water, the mutation toxins, carpet, the plant
/// nutrients. A gas's reagent (atmos_gas_reagents.dm) and eigenstasium with its eigenlinked lockers. A sweep runs every live
/// reagent of these files through its procs.
/// </summary>
public class ChemistryRG3Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, bool player = false)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = player ? r.Join(4, 4, "Bob") : r.Dummy(4, 4);
        r.Step(Second);
        return (r, feed, bob);
    }

    private static datum_reagents Blood(mob_living who) => (datum_reagents)who.reagents;

    private static T Dose<T>(mob_living who, float amount) where T : datum_reagent
    {
        Waiting.NoSleep(Blood(who).add_reagent(typeof(T), amount));
        return (T)Blood(who).has_reagent(typeof(T));
    }

    private static float Remaining(mob_living who, Type effect)
    {
        var found = who.has_status_effect(effect) as datum_status_effect;
        return found == null ? 0 : Dm.num(found.duration); // the deciseconds it has left (_status_effect.dm, process)
    }

    private static void Clear(mob_living who) => Waiting.NoSleep(Blood(who).clear_reagents());

    // ---- the drugs ----

    [Fact]
    public void SpaceDrugsMakeAHumanDruggedAndTheOverdoseHallucinates()
    {
        var (r, feed, bob) = Room(10101);
        r.In(() =>
        {
            var drugs = Dose<datum_reagent_drug_space_drugs>(bob, 20);
            Waiting.NoSleep(drugs.on_mob_life(bob, 2, 1));
            // drug_reagents.dm:24: set_drugginess(30 SECONDS * ratio * seconds)
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_drugginess)));
            Assert.True(Dm.num(bob.get_timed_status_effect_duration(typeof(datum_status_effect_drugginess))) >= 50 * SECONDS);
            Clear(bob);

            // past 30 u the overdose: "You start tripping hard!" and the hallucinations (while they last less than the volume)
            drugs = Dose<datum_reagent_drug_space_drugs>(bob, 40);
            Waiting.NoSleep(drugs.overdose_start(bob, 1));
            for (int i = 0; i < 40 && bob.has_status_effect(typeof(datum_status_effect_hallucination)) == null; i++)
                Waiting.NoSleep(drugs.overdose_process(bob, 2, 1));
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_hallucination)));
        });
        Assert.True(feed.Any("You start tripping hard!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void NicotineShakesOffStunsAndJitterAndItsOverdosePoisonsAndChokes()
    {
        var (r, _, bob) = Room(10102);
        r.In(() =>
        {
            Waiting.NoSleep(bob.Paralyze(30 * SECONDS));
            Waiting.NoSleep(bob.set_jitter(20 * SECONDS));
            var nicotine = Dose<datum_reagent_drug_nicotine>(bob, 10);
            float before = Remaining(bob, typeof(datum_status_effect_incapacitating_paralyzed));
            Waiting.NoSleep(nicotine.on_mob_life(bob, 2, 1));
            // drug_reagents.dm:109: no jitter, 20 s a second off every stun
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_jitter)));
            Assert.InRange(Remaining(bob, typeof(datum_status_effect_incapacitating_paralyzed)), before - 40 * SECONDS - 1, before - 40 * SECONDS + 1);

            float tox = Dm.num(bob.get_tox_loss()), oxy = Dm.num(bob.get_oxy_loss());
            Waiting.NoSleep(nicotine.overdose_process(bob, 2, 1));
            Assert.Equal(tox + 0.8f, Dm.num(bob.get_tox_loss()), 3);
            Assert.Equal(oxy + 8.8f, Dm.num(bob.get_oxy_loss()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MethamphetamineSpeedsAHumanUpAndItsOverdoseHurtsHisBrain()
    {
        var (r, _, bob) = Room(10103);
        r.In(() =>
        {
            var meth = Dose<datum_reagent_drug_methamphetamine>(bob, 10);
            Waiting.NoSleep(meth.on_mob_metabolize(bob));
            Assert.NotNull(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_methamphetamine)));
            float brain = Dm.num(bob.get_organ_loss(ORGAN_SLOT_BRAIN));
            Waiting.NoSleep(meth.on_mob_life(bob, 2, 1));
            // 0.67 * rand(1, 4) * 2 of brain damage and the jitter (drug_reagents.dm:206)
            Assert.InRange(Dm.num(bob.get_organ_loss(ORGAN_SLOT_BRAIN)) - brain, 1.34f - 0.01f, 5.36f + 0.01f);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_jitter)));
            float tox = Dm.num(bob.get_tox_loss());
            Waiting.NoSleep(meth.overdose_process(bob, 2, 1));
            Assert.Equal(tox + 1.34f, Dm.num(bob.get_tox_loss()), 3);
            Waiting.NoSleep(meth.on_mob_end_metabolize(bob, 1));
            Assert.Null(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_methamphetamine)));

            // the purer the meth, the bluer (drug_reagents.dm:180): 75% is halfway from #78C8FA to #FAFAFA. (At 50% tg's BlendRGB is
            // rgb_gradient's "loop" index 1, which wraps back to the first colour.)
            Clear(bob);
            var holder = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_drug_methamphetamine), 10, added_purity: 0.75f));
            var impure = (datum_reagent)holder.has_reagent(typeof(datum_reagent_drug_methamphetamine));
            Assert.Equal("#B9E1FA", Dm.str(impure.color).ToUpperInvariant());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BathSaltsMakeAStunImmuneRavingLunatic()
    {
        var (r, _, bob) = Room(10104);
        r.In(() =>
        {
            Waiting.NoSleep(Blood(bob).add_reagent(typeof(datum_reagent_drug_bath_salts), 10));
        });
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_STUNIMMUNE));
            Assert.True(HAS_TRAIT(bob, TRAIT_SLEEPIMMUNE));
            // the psychotic brawling of bath salts, its brain damage and hallucinations (drug_reagents.dm:262, :275)
            Assert.NotNull(bob.has_trauma_type(typeof(datum_brain_trauma_special_psychotic_brawling_bath_salts)));
            Assert.True(Dm.num(bob.get_organ_loss(ORGAN_SLOT_BRAIN)) > 0);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_hallucination)));
            Clear(bob);
        });
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_STUNIMMUNE));
            Assert.Null(bob.has_trauma_type(typeof(datum_brain_trauma_special_psychotic_brawling_bath_salts)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ImpureKrokodilMakesTheSkinFallOffAtItsThirtySixthCycle()
    {
        var (r, feed, bob) = Room(10105);
        r.In(() =>
        {
            var krokodil = Dose<datum_reagent_drug_krokodil>(bob, 10);
            krokodil.creation_purity = 0.5f;
            krokodil.current_cycle = 35;
            Waiting.NoSleep(krokodil.on_mob_life(bob, 2, 1));
            Assert.False(HAS_TRAIT(bob, TRAIT_HUSK));
            krokodil.current_cycle = 36;
            float brute = Dm.num(bob.get_brute_loss());
            Waiting.NoSleep(krokodil.on_mob_life(bob, 2, 1));
            Assert.True(HAS_TRAIT(bob, TRAIT_HUSK));
            Assert.Equal(brute + 25, Dm.num(bob.get_brute_loss()), 1);
            float brainBefore = Dm.num(bob.get_organ_loss(ORGAN_SLOT_BRAIN));
            Waiting.NoSleep(krokodil.overdose_process(bob, 2, 1));
            Assert.Equal(brainBefore + 0.5f, Dm.num(bob.get_organ_loss(ORGAN_SLOT_BRAIN)), 3);
        });
        Assert.True(feed.Any("Your skin falls off easily!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MaintenanceTarShakesOffStunsAtTheLiversCost()
    {
        var (r, _, bob) = Room(10106);
        r.In(() =>
        {
            var tar = Dose<datum_reagent_drug_maint_tar>(bob, 10);
            float liver = Dm.num(bob.get_organ_loss(ORGAN_SLOT_LIVER));
            Waiting.NoSleep(tar.on_mob_life(bob, 2, 1));
            Assert.Equal(liver + 3, Dm.num(bob.get_organ_loss(ORGAN_SLOT_LIVER)), 3); // 1.5 a second (drug_reagents.dm:520)
            float tox = Dm.num(bob.get_tox_loss());
            Waiting.NoSleep(tar.overdose_process(bob, 2, 1));
            Assert.Equal(tox + 10, Dm.num(bob.get_tox_loss()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTripsTintAndWarpTheDruggedPlayersGamePlaneUntilTheDrugIsGone()
    {
        var (r, _, bob) = Room(10107, player: true);
        atom_movable_plane_master_controller Plane() => bob.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        r.In(() =>
        {
            Assert.NotNull(bob.hud_used);
            Assert.Null(Plane().get_filter("rainbow"));
            var mushroom = Dose<datum_reagent_drug_mushroomhallucinogen>(bob, 10);
            Waiting.NoSleep(mushroom.on_mob_metabolize(bob));
            // the hue turn in HSL and the wave (drug_reagents.dm:567, :576); the animate() of each ends on its last step
            var rainbow = (dm_filter)Plane().get_filter("rainbow");
            Assert.NotNull(rainbow);
            Assert.Equal("color", rainbow.type);
            Assert.Equal(FILTER_COLOR_HSL, Dm.num(rainbow.Get("space")));
            Assert.Equal(1f, Dm.num(((DmList)rainbow.Get("color"))[17]), 3); // col_filter_reset: the full turn
            var wave = (dm_filter)Plane().get_filter("psilocybin_wave");
            Assert.NotNull(wave);
            Assert.Equal(2f, Dm.num(wave.Get("size")));
            Assert.Equal(32f, Dm.num(wave.Get("offset")));
            Waiting.NoSleep(mushroom.on_mob_end_metabolize(bob, 1));
            Assert.Null(Plane().get_filter("rainbow"));
            Assert.Null(Plane().get_filter("psilocybin_wave"));
            Clear(bob);

            // blastoff: an HCY hue between red and blue and a wave
            var blastoff = Dose<datum_reagent_drug_blastoff>(bob, 10);
            Waiting.NoSleep(blastoff.on_mob_metabolize(bob));
            var tint = (dm_filter)Plane().get_filter("blastoff_filter");
            Assert.Equal(FILTER_COLOR_HCY, Dm.num(tint.Get("space")));
            Assert.Equal(0.832f, Dm.num(((DmList)tint.Get("color"))[17]), 3);
            Assert.NotNull(Plane().get_filter("blastoff_wave"));
            Assert.Equal(SOUND_ENVIRONMENT_PSYCHOTIC, bob.sound_environment_override);
            Waiting.NoSleep(blastoff.on_mob_end_metabolize(bob, 1));
            Assert.Null(Plane().get_filter("blastoff_filter"));
            Assert.Equal(NONE, bob.sound_environment_override); // tg sets NONE (0), not SOUND_ENVIRONMENT_NONE (drug_reagents.dm:661)
            Clear(bob);

            // saturnX: the colours drained in HCY (two thirds of the chroma) and the radial blur
            var saturnx = Dose<datum_reagent_drug_saturnx>(bob, 10);
            Waiting.NoSleep(saturnx.on_mob_metabolize(bob));
            var drain = (dm_filter)Plane().get_filter("saturnx_filter");
            Assert.Equal(FILTER_COLOR_HCY, Dm.num(drain.Get("space")));
            Assert.Equal(0.68f, Dm.num(((DmList)drain.Get("color"))[6]), 3);
            Assert.Equal("radial_blur", ((dm_filter)Plane().get_filter("saturnx_blur")).type);
            Waiting.NoSleep(saturnx.on_mob_end_metabolize(bob, 1));
            Assert.Null(Plane().get_filter("saturnx_filter"));
            Assert.Null(Plane().get_filter("saturnx_blur"));
        });
        r.AssertNoRuntimes();
    }

    // ---- the pyrotechnics ----

    [Fact]
    public void ThermiteOnAWallBurnsThroughItOnceLit()
    {
        var (r, _, _) = Room(10110);
        turf wall = r.In(() => Waiting.NoSleep(locate(7, 6, 1).ChangeTurf(typeof(turf_closed_wall))) as turf);
        r.In(() =>
        {
            Waiting.NoSleep(Dm.@new<datum_reagent_thermite>().expose_turf(wall, 50));
            var thermite = wall.GetComponent(typeof(datum_component_thermite)) as datum_component_thermite;
            Assert.NotNull(thermite);
            Assert.Equal(50f, Dm.num(thermite.amount));
            thermite.thermite_melt();
            Assert.NotNull(Dm.locate<obj_effect_overlay_thermite>(wall));
        });
        r.Step(20 * Second); // min(50 * 0.35 s, 20 s)
        r.In(() =>
        {
            turf now = locate(7, 6, 1);
            Assert.IsNotType<turf_closed_wall>(now);
            Assert.Null(now.GetComponent(typeof(datum_component_thermite)));
            Assert.Null(Dm.locate<obj_effect_overlay_thermite>(now));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WeldingFuelSplashedOnABodyMakesItBurnOnceLitAndChlorineTrifluorideSetsItAlightAtOnce()
    {
        var (r, _, bob) = Room(10111);
        r.In(() =>
        {
            Waiting.NoSleep(Dm.@new<datum_reagent_fuel>().expose_mob(bob, TOUCH, 30));
            Assert.Equal(3f, bob.fire_stacks, 3); // reac_volume / 10 (other_reagents.dm:1357)
            Assert.False(Dm.truthy(bob.on_fire));
            bob.ignite_mob();
            Assert.True(Dm.truthy(bob.on_fire));
            bob.extinguish_mob();
            Waiting.NoSleep(bob.set_fire_stacks(0));

            // clf3: min(volume / 5, 10) stacks, lit, and a hotspot under him (pyrotechnic_reagents.dm:82)
            Waiting.NoSleep(Dm.@new<datum_reagent_clf3>().expose_mob(bob, TOUCH, 20));
            Assert.True(bob.fire_stacks >= 4f, "fire stacks " + bob.fire_stacks); // 4, and the hotspot's own fire on him
            Assert.True(Dm.truthy(bob.on_fire));
            Assert.NotNull(Dm.locate<obj_effect_hotspot>(bob.loc));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChlorineTrifluorideOnAFloorSetsHotspotsRoundIt()
    {
        var (r, _, _) = Room(10112);
        turf floor = r.In(() =>
        {
            for (int x = 6; x <= 8; x++)
                for (int y = 4; y <= 6; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron)));
            return locate(7, 5, 1);
        });
        r.In(() =>
        {
            Waiting.NoSleep(Dm.@new<datum_reagent_clf3>().expose_turf(floor, 0));
            int hot = 0;
            foreach (turf t in Dm.each_cast<turf>(block(6, 4, 1, 8, 6, 1)))
                if (Dm.locate<obj_effect_hotspot>(t) != null)
                    hot++;
            Assert.Equal(9, hot);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CryostylaneCoolsAndPyrosiumWarmsABodyWhileThereIsOxygen()
    {
        var (r, _, bob) = Room(10113);
        r.In(() =>
        {
            bob.bodytemperature = 310;
            bob.coretemperature = 310;
            var cryo = Dose<datum_reagent_cryostylane>(bob, 10);
            Assert.Equal(COLOR_CYAN, bob.color);
            Dose<datum_reagent_oxygen>(bob, 10);
            Waiting.NoSleep(cryo.on_mob_life(bob, 2, 1));
            Assert.Equal(310 - 60, bob.bodytemperature, 1);
            Assert.Equal(310 - 60, bob.coretemperature, 1);
            Assert.Equal(8f, Dm.num(Blood(bob).get_reagent_amount(typeof(datum_reagent_oxygen))), 2);
            Clear(bob);
            Assert.Equal(COLOR_WHITE, bob.color);

            bob.bodytemperature = 310;
            bob.coretemperature = 310;
            var pyro = Dose<datum_reagent_pyrosium>(bob, 10);
            Dose<datum_reagent_oxygen>(bob, 10);
            Waiting.NoSleep(pyro.on_mob_life(bob, 2, 1));
            Assert.Equal(310 + 30, bob.bodytemperature, 1);
            Assert.Equal(310 + 30, bob.coretemperature, 1);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FlashPowderLitInTheOpenFlashesAndKnocksDownWhoeverIsNear()
    {
        var (r, _, bob) = Room(10114);
        r.In(() =>
        {
            var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(5, 4, 1));
            var holder = (datum_reagents)beaker.reagents;
            Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_flash_powder), 30));
            var powder = (datum_reagent_flash_powder)holder.has_reagent(typeof(datum_reagent_flash_powder));
            Assert.Equal(SPARK_ACT_NON_DESTRUCTIVE, Dm.num(Waiting.NoSleep(powder.on_spark_act(0, 0))));
            // a distance of 1: knocked down for 20 s (pyrotechnic_reagents.dm:274)
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown)));
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_incapacitating_paralyzed)));
        });
        r.AssertNoRuntimes();
    }

    // ---- the other reagents ----

    [Fact]
    public void WaterPutsOutABurningBodyAndCoolsAHotspotOffAFloor()
    {
        var (r, _, bob) = Room(10115);
        turf floor = r.In(() => Waiting.NoSleep(locate(7, 5, 1).ChangeTurf(typeof(turf_open_floor_iron))) as turf);
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_fire_stacks(5));
            bob.ignite_mob();
            Assert.True(Dm.truthy(bob.on_fire));
            Waiting.NoSleep(Dm.@new<datum_reagent_water>().expose_mob(bob, TOUCH, 30));
            Assert.False(Dm.truthy(bob.on_fire));

            Dm.@new<obj_effect_hotspot>(floor);
            Assert.NotNull(Dm.locate<obj_effect_hotspot>(floor));
            Waiting.NoSleep(Dm.@new<datum_reagent_water>().expose_turf(floor, 10));
            Assert.Null(Dm.locate<obj_effect_hotspot>(floor));
            // 10 u: the floor wet for min(15 s, 60 s) (other_reagents.dm:121)
            Assert.NotNull(floor.GetComponent(typeof(datum_component_wet_floor)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SpaceCleanerWashesADecalOffAFloorAndLubeMakesItSlippery()
    {
        var (r, _, _) = Room(10116);
        turf floor = r.In(() => Waiting.NoSleep(locate(7, 5, 1).ChangeTurf(typeof(turf_open_floor_iron))) as turf);
        r.In(() =>
        {
            Dm.@new<obj_effect_decal_cleanable_dirt>(floor);
            Assert.NotNull(Dm.locate<obj_effect_decal_cleanable_dirt>(floor));
            Waiting.NoSleep(Dm.@new<datum_reagent_space_cleaner>().expose_turf(floor, 5));
            Assert.Null(Dm.locate<obj_effect_decal_cleanable_dirt>(floor));

            Waiting.NoSleep(Dm.@new<datum_reagent_lube>().expose_turf(floor, 5));
            var wet = floor.GetComponent(typeof(datum_component_wet_floor)) as datum_component_wet_floor;
            Assert.NotNull(wet);
            Assert.True(Dm.truthy(Dm.bitand(wet.highest_strength, TURF_WET_LUBE)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HairDyeAndColorfulReagentRecolourAHumansHair()
    {
        var (r, _, bob) = Room(10117);
        r.In(() =>
        {
            var dye = Dm.@new<datum_reagent_hair_dye>();
            Waiting.NoSleep(dye.expose_mob(bob, TOUCH, 10));
            Assert.Contains(bob.hair_color, Dm.each<string>(dye.potential_colors).ToList());
            Assert.Contains(bob.facial_hair_color, Dm.each<string>(dye.potential_colors).ToList());

            var colorful = Dm.@new<datum_reagent_colorful_reagent>();
            Waiting.NoSleep(colorful.expose_mob(bob, TOUCH, 10));
            Assert.Contains(bob.hair_color, Dm.each<string>(colorful.random_color_list).ToList());
            // on a thing: a washable colour (other_reagents.dm:2419)
            var plush = Dm.@new<obj_item_toy_plush_carpplushie>(locate(5, 4, 1));
            Waiting.NoSleep(colorful.expose_atom(plush, 5));
            Assert.NotNull(plush.atom_colours[WASHABLE_COLOUR_PRIORITY]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HolyWaterOnlyJittersAndDizziesAnOrdinaryBelieverAfterTenUnits()
    {
        var (r, _, bob) = Room(10118);
        r.In(() =>
        {
            var holy = Dose<datum_reagent_water_holywater>(bob, 30);
            Waiting.NoSleep(holy.on_mob_life(bob, 2, 1));
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_jitter)));
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_dizziness)));
            // 25 s metabolized (10 units): the stutter and the dizziness (other_reagents.dm:382)
            ((DmList)holy.data)["deciseconds_metabolized"] = 25 * SECONDS;
            Waiting.NoSleep(holy.on_mob_life(bob, 2, 1));
            Assert.True(Remaining(bob, typeof(datum_status_effect_dizziness)) >= 10 * SECONDS - 1);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_speech_stutter)));
            Assert.Equal(0f, Dm.num(bob.get_fire_loss()));
        });
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(datum_reagent_mutationtoxin_lizard), typeof(datum_species_lizard), 10119)]
    [InlineData(typeof(datum_reagent_mutationtoxin_felinid), typeof(datum_species_human_felinid), 10120)]
    public void AMutationToxinTurnsAHumanIntoItsRaceAtItsTwentiethCycle(Type toxinType, Type species, int seed)
    {
        var (r, feed, bob) = Room((ulong)seed);
        r.In(() =>
        {
            Waiting.NoSleep(Blood(bob).add_reagent(toxinType, 10));
            var toxin = (datum_reagent_mutationtoxin)Blood(bob).has_reagent(toxinType);
            toxin.current_cycle = 19;
            Waiting.NoSleep(toxin.on_mob_life(bob, 2, 1));
            Assert.IsType<datum_species_human>(bob.dna.species);
            toxin.current_cycle = 20;
            Waiting.NoSleep(toxin.on_mob_life(bob, 2, 1));
            Assert.IsType(species, bob.dna.species);
            Assert.False(Dm.truthy(Blood(bob).has_reagent(toxinType)));
        });
        Assert.True(feed.Any("You've become a"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHumanMutationToxinKeepsAHumanAndAnInertSpeciesIsNotTakenOn()
    {
        var (r, _, bob) = Room(10121);
        r.In(() =>
        {
            var toxin = Dose<datum_reagent_mutationtoxin>(bob, 10);
            toxin.current_cycle = 20;
            Waiting.NoSleep(toxin.on_mob_life(bob, 2, 1));
            Assert.IsType<datum_species_human>(bob.dna.species);
            Clear(bob);
            // the slime mutation toxin's jellyperson is not ported (XB1-2): the human stays a human (GAP(D338))
            var classic = Dose<datum_reagent_mutationtoxin_classic>(bob, 10);
            classic.current_cycle = 20;
            Waiting.NoSleep(classic.on_mob_life(bob, 2, 1));
            Assert.IsType<datum_species_human>(bob.dna.species);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CarpetLaysACarpetOnAFloor()
    {
        var (r, _, _) = Room(10122);
        turf floor = r.In(() => Waiting.NoSleep(locate(7, 5, 1).ChangeTurf(typeof(turf_open_floor_iron))) as turf);
        r.In(() =>
        {
            Waiting.NoSleep(Dm.@new<datum_reagent_carpet_royal_blue>().expose_turf(floor, 5));
            Assert.IsType<turf_open_floor_carpet_royalblue>(locate(7, 5, 1));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PlantNutrientsFeedATraysPlantAndNicotineKillsItsPests()
    {
        var (r, _, bob) = Room(10123);
        r.In(() =>
        {
            var tray = Dm.@new<obj_machinery_hydroponics_constructable>(locate(6, 4, 1));
            tray.pestlevel = 5;
            var holder = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_drug_nicotine), 10));
            Waiting.NoSleep(((datum_reagent)holder.has_reagent(typeof(datum_reagent_drug_nicotine))).on_hydroponics_apply(tray, bob));
            Assert.InRange(tray.pestlevel, 3f, 4f); // -rand(1, 2) (drug_reagents.dm:101)
            Assert.Equal(10f, tray.toxic, 2);
            float nutrients = Dm.num(((datum_reagents)tray.reagents).total_volume);
            Waiting.NoSleep(((datum_reagents)tray.reagents).add_reagent(typeof(datum_reagent_plantnutriment_eznutriment), 10));
            Assert.True(Dm.num(((datum_reagents)tray.reagents).total_volume) > nutrients);
        });
        r.AssertNoRuntimes();
    }

    // ---- the gases' reagents ----

    [Fact]
    public void FreonSlowsAndHealiumPutsABodyToSleepWhileHealingIt()
    {
        var (r, _, bob) = Room(10124);
        r.In(() =>
        {
            var freon = Dose<datum_reagent_freon>(bob, 5);
            Waiting.NoSleep(freon.on_mob_metabolize(bob));
            Assert.NotNull(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_freon)));
            Waiting.NoSleep(freon.on_mob_end_metabolize(bob, 1));
            Assert.Null(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_freon)));

            Waiting.NoSleep(bob.adjust_brute_loss(20));
            float brute = Dm.num(bob.get_brute_loss());
            var healium = Dose<datum_reagent_healium>(bob, 5);
            Waiting.NoSleep(healium.on_mob_life(bob, 2, 1));
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_incapacitating_sleeping)));
            Assert.Equal(brute - 4, Dm.num(bob.get_brute_loss()), 2);
        });
        r.AssertNoRuntimes();
    }

    // ---- eigenstasium ----

    [Fact]
    public void EigenstasiumLeavesAHologramWhereADrinkerWasAndSendsHimBackWhenItIsGone()
    {
        var (r, feed, bob) = Room(10125);
        r.In(() =>
        {
            Dose<datum_reagent_eigenstate>(bob, 5);
            var eigen = (datum_reagent_eigenstate)Blood(bob).has_reagent(typeof(datum_reagent_eigenstate));
            Assert.NotNull(eigen.eigenstate);
            Assert.Same(locate(4, 4, 1), eigen.eigenstate.loc);
            Assert.Equal(170, eigen.eigenstate.alpha);
            Assert.Same(locate(4, 4, 1), eigen.location_return);
            Waiting.NoSleep(bob.forceMove(locate(9, 7, 1)));
            var hologram = eigen.eigenstate;
            Clear(bob);
            Assert.True(get_dist(bob, locate(4, 4, 1)) <= 3, "he came back to " + bob.x + ", " + bob.y);
            Assert.True(QDELETED(hologram));
        });
        Assert.True(feed.Any("You feel like part of yourself has split off!"));
        Assert.True(feed.Any("You feel strangely whole again."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EigenstasiumsOverdoseStartsTheWildRideThatBluespaceDustHoldsBack()
    {
        var (r, _, bob) = Room(10126);
        // the clone's lines come from tg's strings/eigenstasium.json (neutral.dm:544, through strings()), as the server mounts them
        if (Anomaly.Core.Tests.Tools.TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, Anomaly.Core.Tests.Tools.TgClone.Root);
        r.In(() =>
        {
            var eigen = Dose<datum_reagent_eigenstate>(bob, 20);
            Waiting.NoSleep(eigen.overdose_start(bob, 1));
            Assert.Equal(REAGENTS_METABOLISM + 0.5f, eigen.metabolization_rate, 3);
            var ride = bob.has_status_effect(typeof(datum_status_effect_eigenstasium)) as datum_status_effect_eigenstasium;
            Assert.NotNull(ride);
            Clear(bob);
            // phase 1: hungry and jittery
            ride.current_cycle = 9;
            float nutrition = bob.nutrition;
            Waiting.NoSleep(ride.tick(2));
            Assert.Equal(10f, ride.current_cycle);
            Assert.Equal(nutrition - 4, bob.nutrition, 2);
            // bluespace dust holds it back: 7.5 a tick off the cycle
            Dose<datum_reagent_bluespace>(bob, 5);
            Waiting.NoSleep(ride.tick(2));
            Assert.Equal(2.5f, ride.current_cycle, 2);
            Clear(bob);
            // phase 3: a clone from another reality turns up beside him
            ride.current_cycle = 100;
            ride.phase_3_cycle = 1;
            Waiting.NoSleep(ride.tick(2));
            Assert.NotNull(ride.alt_clone);
            Assert.Equal(bob.real_name, ride.alt_clone.real_name);
            var clone = ride.alt_clone;
            // the next tick snaps it back across
            Waiting.NoSleep(ride.tick(2));
            Assert.True(QDELETED(clone));
            bob.remove_status_effect(typeof(datum_status_effect_eigenstasium));
        });
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EigenlinkedLockersSendWhatIsPutInOneToTheOtherAndRefuseAWelder()
    {
        var (r, feed, bob) = Room(10127, player: true);
        r.In(() =>
        {
            var first = Dm.@new<obj_structure_closet>(locate(2, 2, 1));
            var second = Dm.@new<obj_structure_closet>(locate(9, 7, 1));
            Assert.Equal(TRUE, Dm.num(GLOB.closet_teleport_controller.create_new_link(Dm.list(first, second), subtle: FALSE)));
            Assert.Equal(200, first.alpha);
            Assert.Equal(COLOR_PERIWINKLEE, ((DmList)first.atom_colours[FIXED_COLOUR_PRIORITY])[1]); // list(colour, type) (atom_color.dm:33)
            Assert.True(HAS_TRAIT(first, TRAIT_BANNED_FROM_CARGO_SHUTTLE));
            var plush = Dm.@new<obj_item_toy_plush_carpplushie>(locate(2, 2, 1));
            Assert.Equal(TRUE, Dm.num(Waiting.NoSleep(first.insert(plush))));
            Assert.Same(second, plush.loc);

            var welder = Dm.@new<obj_item_weldingtool>(locate(4, 4, 1));
            Assert.Equal(ITEM_INTERACT_BLOCKING, Dm.num(first._SendSignal(COMSIG_ATOM_TOOL_ACT(TOOL_WELDER), Dm.list(first, bob, welder))));

            // a locker deleted leaves the link: the other is plain again
            qdel(first);
            GLOB.closet_teleport_controller.remove_eigen_entry(second);
            Assert.Equal(255, second.alpha);
            Assert.False(HAS_TRAIT(second, TRAIT_BANNED_FROM_CARGO_SHUTTLE));
        });
        Assert.True(feed.Any("The items shimmer and fizzle"));
        Assert.True(feed.Any("makes it impossible to use"));
        r.AssertNoRuntimes();
    }

    // ---- the sweep ----

    /// <summary>
    /// A sweep over every live reagent declared in RG3's files (the drugs, the pyrotechnics, the other reagents, the gases' and
    /// eigenstasium): each is put in a fresh body's blood and runs its tg procs once (add, metabolize, life, overdose, end of
    /// metabolism, the ingestion and the splash on the body, a splash on a floor and on an item, delete), so that no translated proc
    /// throws on the paths a reagent takes.
    /// </summary>
    [Fact]
    public void EveryLiveReagentOfTheseFilesRunsItsProcsWithoutAnError()
    {
        var (r, _, _) = Room(10199);
        var failures = new List<string>();
        int ran = 0;
        List<Type> types = r.In(() => Dm.each<Type>(typesof(typeof(datum_reagent)))
.Where(t => t != typeof(datum_reagent) && !Dm.eq(Dm.initial(t, "abstract_type"), t))
            .Where(t => !InertSeam.IsInertType(t) && !typeof(datum_reagent_consumable).IsAssignableFrom(t) && !typeof(datum_reagent_medicine).IsAssignableFrom(t)
                && !typeof(datum_reagent_toxin).IsAssignableFrom(t) && !typeof(datum_reagent_impurity).IsAssignableFrom(t) && !typeof(datum_reagent_inverse).IsAssignableFrom(t))
            .ToList());
        Assert.True(types.Count > 200, "live reagents: " + types.Count);
        mob_living_carbon_human body = null;
        foreach (Type type in types)
        {
            if (body == null || r.In(() => QDELETED(body) || body.stat == DEAD || !ishuman(body)))
            {
                body = r.Dummy(5, 5);
                r.Step(1);
            }
            var who = body;
            r.In(() =>
            {
                string step = "add";
                try
                {
                    Waiting.NoSleep(Blood(who).clear_reagents());
                    Waiting.NoSleep(Blood(who).add_reagent(type, 10));
                    if (Blood(who).has_reagent(type) is not datum_reagent reagent)
                        return; // it reacted away at once
                    ran++;
                    step = "on_mob_metabolize";
                    Waiting.NoSleep(reagent.on_mob_metabolize(who));
                    step = "on_mob_life";
                    reagent.current_cycle = 20;
                    Waiting.NoSleep(reagent.on_mob_life(who, 2, 1));
                    step = "overdose";
                    Waiting.NoSleep(reagent.overdose_start(who, 1));
                    Waiting.NoSleep(reagent.overdose_process(who, 2, 1));
                    step = "on_mob_end_metabolize";
                    Waiting.NoSleep(reagent.on_mob_end_metabolize(who, 1));
                    step = "expose_mob";
                    Waiting.NoSleep(reagent.expose_mob(who, INGEST, 5));
                    Waiting.NoSleep(reagent.expose_mob(who, TOUCH, 5));
                    step = "expose_turf";
                    Waiting.NoSleep(reagent.expose_turf(locate(7, 6, 1) as turf, 5));
                    step = "expose_obj";
                    var target = Dm.@new<obj_item_toy_plush_carpplushie>(locate(7, 6, 1));
                    Waiting.NoSleep(reagent.expose_obj(target, 5, TOUCH));
                    step = "on_mob_delete";
                    if (QDELETED(who))
                        return; // gibbed (kronkaine's gore) or turned into another mob (magillitis' gorilla): a fresh body next
                    Waiting.NoSleep(Blood(who).clear_reagents());
                    Waiting.NoSleep(who.set_wet_stacks(0)); // tg's hellwater crashes on a wet body (set_fire_stacks of a negative sum)
                    Waiting.NoSleep(who.set_fire_stacks(0));
                    who.remove_status_effect(typeof(datum_status_effect_eigenstasium));
                    Waiting.NoSleep(who.fully_heal(HEAL_ALL));
                    if (who.loc != locate(5, 5, 1))
                        Waiting.NoSleep(who.forceMove(locate(5, 5, 1)));
                }
                catch (Exception e)
                {
                    failures.Add(DmTypes.PathOf(type) + " " + step + ": " + e.GetType().Name + " " + e.Message.Split('\n')[0]);
                }
            });
        }
        r.Step(2 * Second);
        var runtimes = r.Room.RuntimeText.Select(t => t.Split('\n')[0]).Distinct().ToList();
        Assert.True(ran > 150, ran + " of " + types.Count + " reagents stayed in the blood to run");
        Assert.True(failures.Count == 0 && runtimes.Count == 0, $"{ran} reagents ran; failures:\n" + string.Join("\n", failures) + "\nruntimes:\n" + string.Join("\n", runtimes));
    }
}
