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
/// Species package SP2b (D349): the ethereal with its lustrous subspecies, the podperson, the mushroomperson and the snailperson.
/// Each is made on a human with set_species: its organs, limbs, traits, features and language, and its own mechanics (the ethereal's
/// glow and charge, the lustrous's trauma, the pod's photosynthesis, the mushroomperson's mushpunch and spores, the snail's shell,
/// slime trail and salt).
/// </summary>
public class SpeciesSP2bTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, feed, bob);
    }

    private static string Diag(AudioRoom r, Feed feed) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static void Become(AudioRoom r, mob_living_carbon_human who, Type species)
    {
        r.In(() => who.set_species(species));
        r.Step(2);
    }

    private static datum_status_effect_grouped_bodypart_effect_ethereal_glow Glow(mob_living_carbon_human who)
        => who.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_ethereal_glow)) as datum_status_effect_grouped_bodypart_effect_ethereal_glow;

    [Fact]
    public void An_ethereal_has_the_species_organs_limbs_traits_colour_and_language()
    {
        var (r, feed, bob) = Room(9901);
        Become(r, bob, typeof(datum_species_ethereal));
        r.In(() =>
        {
            Assert.IsType<datum_species_ethereal>(bob.dna.species);
            Assert.IsType<obj_item_organ_heart_ethereal>(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.IsType<obj_item_organ_stomach_ethereal>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            Assert.IsType<obj_item_organ_lungs_ethereal>(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_organ_tongue_ethereal>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_bodypart_chest_ethereal>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.IsType<obj_item_bodypart_head_ethereal>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.True(HAS_TRAIT(bob, TRAIT_MUTANT_COLORS));
            Assert.True(HAS_TRAIT(bob, TRAIT_AGENDER));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOHUNGER)); // the stomach's: ethereals have their own hunger mechanic
            Assert.Equal(0.5f, Dm.num(((datum_species)bob.dna.species).siemens_coeff));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_voltaic))));
            // the heart carries the mutant colour the species set
            var heart = (obj_item_organ_heart_ethereal)bob.get_organ_slot(ORGAN_SLOT_HEART);
            Assert.Equal(Dm.str(bob.dna.features[FEATURE_MUTANT_COLOR]), heart.ethereal_color);
        });
    }

    [Fact]
    public void The_ethereals_random_features_pick_from_its_colour_list_and_the_lustrous_from_its_own()
    {
        var (r, _, bob) = Room(9902);
        r.In(() =>
        {
            var ethereal = (datum_species)Dm.@new<datum_species_ethereal>();
            for (int i = 0; i < 10; i++)
                Assert.Contains(Dm.str(((DmList)ethereal.randomize_features())[FEATURE_MUTANT_COLOR]), Dm.each(GLOB.color_list_ethereal).Select(k => Dm.str(GLOB.color_list_ethereal[k])));
            var lustrous = (datum_species)Dm.@new<datum_species_ethereal_lustrous>();
            for (int i = 0; i < 10; i++)
                Assert.Contains(Dm.str(((DmList)lustrous.randomize_features())[FEATURE_MUTANT_COLOR]), Dm.each(GLOB.color_list_lustrous).Select(k => Dm.str(GLOB.color_list_lustrous[k])));
            Assert.Contains("feature_ethcolor", Dm.each(ethereal.get_features()).Select(Dm.str));
        });
    }

    [Fact]
    public void An_ethereal_glows_in_its_colour_goes_dark_to_an_emp_and_the_glow_comes_back()
    {
        var (r, feed, bob) = Room(9903);
        r.In(() => bob.dna.features[FEATURE_MUTANT_COLOR] = "#3399ff");
        Become(r, bob, typeof(datum_species_ethereal));
        r.Step(Second);
        r.In(() =>
        {
            var glow = Glow(bob);
            Assert.NotNull(glow);
            Assert.NotNull(glow.ethereal_light);
            Assert.True(Dm.truthy(glow.ethereal_light.light_on), "the glow is lit");
            Assert.True(Dm.num(glow.ethereal_light.light_range) > 0);
            // a healthy ethereal glows in its own colour, its limbs and hair take it
            Assert.Equal("#3399ff", Dm.str(glow.current_color).ToLowerInvariant());
            Assert.Equal("#3399ff", Dm.str(((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_CHEST)).draw_color).ToLowerInvariant());
            // an EMP puts it out for ten seconds
            Waiting.NoSleep(bob.emp_act(EMP_LIGHT));
            Assert.True(Dm.truthy(glow.disrupted));
            Assert.False(Dm.truthy(glow.ethereal_light.light_on));
        });
        Assert.True(feed.Any("You feel the light of your body leave you."), Diag(r, feed));
        r.Step(11 * Second);
        r.In(() =>
        {
            var glow = Glow(bob);
            Assert.False(Dm.truthy(glow.disrupted));
            Assert.True(Dm.truthy(glow.ethereal_light.light_on));
        });
        Assert.True(feed.Any("You feel more energized as your shine comes back."), Diag(r, feed));
    }

    [Fact]
    public void A_wounded_ethereals_glow_dims_with_his_health_and_the_glow_emotes_flare_it()
    {
        var (r, feed, bob) = Room(9904);
        Become(r, bob, typeof(datum_species_ethereal));
        r.Step(Second);
        float healthy = r.In(() => Dm.num(Glow(bob).ethereal_light.light_range));
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_brute_loss(60));
            Waiting.NoSleep(bob.updatehealth());
        });
        r.Step(2);
        float hurt = r.In(() => Dm.num(Glow(bob).ethereal_light.light_range));
        Assert.True(hurt < healthy, hurt + " < " + healthy);
        // the glow emote brightens it for a while
        object ran = r.In(() => Waiting.NoSleep(bob.emote("glow", intentional: TRUE)));
        r.Step(2);
        Assert.True(Dm.truthy(ran) && Dm.num(r.In(() => Glow(bob).powermult)) > 1, "ran=" + ran + " " + Diag(r, feed));
        float glowing = r.In(() => Dm.num(Glow(bob).ethereal_light.light_range));
        Assert.True(glowing > hurt, glowing + " > " + hurt);
        r.Step(6 * Second);
        float after = r.In(() => Dm.num(Glow(bob).ethereal_light.light_range));
        Assert.True(after < glowing, after + " < " + glowing);
        // flicker toggles the light and stops by itself
        r.In(() => Waiting.NoSleep(bob.emote("flicker")));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Glow(bob).flickering)), Diag(r, feed));
        r.Step(8 * Second);
        Assert.False(r.In(() => Dm.truthy(Glow(bob).flickering)));
    }

    [Fact]
    public void An_emagged_ethereal_flickers_through_colours_for_two_minutes()
    {
        var (r, feed, bob) = Room(9905);
        Become(r, bob, typeof(datum_species_ethereal));
        r.Step(Second);
        var colours = new HashSet<string>();
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.emag_act(bob)))));
        for (int i = 0; i < 12; i++)
        {
            r.Step(Second / 2);
            colours.Add(r.In(() => Dm.str(Glow(bob).current_color)));
        }
        Assert.True(colours.Count > 3, string.Join(",", colours));
        Assert.True(feed.Any("starts flickering in an array of colors!"), Diag(r, feed));
        r.Step(120 * Second);
        Assert.False(r.In(() => Dm.truthy(Glow(bob).emageffect)));
        Assert.True(feed.Any("stops flickering and goes back to their normal state!"), Diag(r, feed));
    }

    [Fact]
    public void The_ethereals_stomach_drains_absorbs_shocks_burns_when_empty_and_overcharges()
    {
        var (r, feed, bob) = Room(9906);
        Become(r, bob, typeof(datum_species_ethereal));
        r.In(() =>
        {
            var stomach = (obj_item_organ_stomach_ethereal)bob.get_organ_slot(ORGAN_SLOT_STOMACH);
            float full = Dm.num(stomach.cell.charge__proc());
            Assert.True(full > ETHEREAL_CHARGE_NORMAL - 1, "a new ethereal starts well charged: " + full);
            // the load: a second of life drains the discharge rate
            Waiting.NoSleep(stomach.adjust_charge(-1000));
            float before = Dm.num(stomach.cell.charge__proc());
            Waiting.NoSleep(stomach.on_life(1));
            Assert.True(Dm.num(stomach.cell.charge__proc()) < before);
            // the shock is absorbed into the stomach
            before = Dm.num(stomach.cell.charge__proc());
            bob._SendSignal(COMSIG_LIVING_ELECTROCUTE_ACT, Dm.list(bob, 10f, null, 1f, NONE));
            Assert.True(Dm.num(stomach.cell.charge__proc()) > before);
            // empty: toxin damage and the alert
            Waiting.NoSleep(stomach.adjust_charge(-100000));
            Assert.Equal(ETHEREAL_CHARGE_NONE, Dm.num(stomach.cell.charge__proc()));
            float tox = Dm.num(bob.get_tox_loss());
            Waiting.NoSleep(stomach.handle_charge(bob, 1));
            Assert.True(Dm.num(bob.get_tox_loss()) > tox);
            Assert.NotNull(bob.alerts?[ALERT_ETHEREAL_CHARGE]);
            // overloaded: the overcharge alert and toxin
            Waiting.NoSleep(stomach.adjust_charge(1000000));
            Assert.Equal(ETHEREAL_CHARGE_DANGEROUS, Dm.num(stomach.cell.charge__proc()));
            Waiting.NoSleep(stomach.handle_charge(bob, 1));
            Assert.NotNull(bob.alerts?[ALERT_ETHEREAL_OVERCHARGE]);
        });
    }

    [Fact]
    public void An_ethereals_crystal_core_revives_him_after_death()
    {
        var (r, feed, bob) = Room(9907);
        Become(r, bob, typeof(datum_species_ethereal));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);
        Assert.True(feed.Any("Crystals start forming around your dead body."), Diag(r, feed));
        r.Step(CRYSTALIZE_PRE_WAIT_TIME * 2 + Second);
        Assert.True(r.In(() => bob.loc is obj_structure_ethereal_crystal), Diag(r, feed));
        r.Step(CRYSTALIZE_HEAL_TIME * 2 + 2 * Second);
        r.In(() =>
        {
            Assert.True(Dm.num(bob.stat) != DEAD, "the ethereal was revived");
            Assert.True(bob.loc is turf);
        });
    }

    [Fact]
    public void A_lustrous_has_the_bluespace_prophet_trauma_and_tenacity_and_loses_them_with_the_species()
    {
        var (r, feed, bob) = Room(9908);
        Become(r, bob, typeof(datum_species_ethereal_lustrous));
        r.In(() =>
        {
            Assert.IsType<obj_item_organ_brain_lustrous>(bob.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.IsType<obj_item_organ_tongue_ethereal_lustrous>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_bodypart_head_ethereal_lustrous>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_VIRUSIMMUNE));
            Assert.True(HAS_TRAIT(bob, TRAIT_TENACIOUS));
            Assert.NotNull(bob.has_trauma_type(typeof(datum_brain_trauma_special_bluespace_prophet)));
        });
        Become(r, bob, typeof(datum_species_human));
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_TENACIOUS));
            Assert.Null(bob.has_trauma_type(typeof(datum_brain_trauma_special_bluespace_prophet)));
        });
    }

    [Fact]
    public void A_podperson_has_its_plant_parts_and_features_and_photosynthesises()
    {
        var (r, feed, bob) = Room(9910);
        Become(r, bob, typeof(datum_species_pod));
        r.In(() =>
        {
            Assert.IsType<datum_species_pod>(bob.dna.species);
            Assert.IsType<obj_item_organ_heart_pod>(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.IsType<obj_item_organ_stomach_pod>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            Assert.IsType<obj_item_organ_lungs_pod>(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_organ_liver_pod>(bob.get_organ_slot(ORGAN_SLOT_LIVER));
            Assert.IsType<obj_item_organ_tongue_pod>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_pod_hair>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_POD_HAIR));
            Assert.IsType<obj_item_bodypart_chest_pod>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.True(HAS_TRAIT(bob, TRAIT_PLANT_SAFE));
            Assert.True(Dm.truthy(Dm.bitand(bob.mob_biotypes, MOB_PLANT)));
            Assert.Equal(1.5f, Dm.num(((datum_species)bob.dna.species).heatmod));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_sylvan))) || Dm.truthy(bob.has_language(typeof(datum_language_common))));
            Assert.NotNull(Dm.str(bob.dna.features[FEATURE_POD_HAIR]));
            Assert.NotNull(((obj_item_bodypart_chest_pod)bob.get_bodypart(BODY_ZONE_CHEST)).get_butt_sprite());
            // the limbs carry the photosynthesis effect
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_photosynthesis)));
        });
    }

    [Fact]
    public void A_podperson_heals_and_feeds_in_the_light_and_starves_without_nutrition()
    {
        var (r, feed, bob) = Room(9911);
        Become(r, bob, typeof(datum_species_pod));
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_brute_loss(20));
            Waiting.NoSleep(bob.set_nutrition(NUTRITION_LEVEL_FED));
        });
        float brute = r.In(() => Dm.num(bob.get_brute_loss()));
        float nutrition = r.In(() => Dm.num(bob.nutrition));
        r.Step(10 * Second);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) < brute, "healed in the light");
        Assert.True(r.In(() => Dm.num(bob.nutrition)) > nutrition, "fed by the light");
        // starving, he withers even in the light
        r.In(() => Waiting.NoSleep(bob.set_nutrition(NUTRITION_LEVEL_STARVING - 100)));
        brute = r.In(() => Dm.num(bob.get_brute_loss()));
        r.Step(10 * Second);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > brute, "withers when starving");
    }

    [Fact]
    public void A_podperson_inside_a_closet_gets_no_light_and_starves()
    {
        var (r, feed, bob) = Room(9912);
        Become(r, bob, typeof(datum_species_pod));
        r.In(() =>
        {
            var closet = Dm.@new<obj_structure_closet>(locate(5, 5, 1));
            Waiting.NoSleep(bob.set_nutrition(NUTRITION_LEVEL_STARVING));
            Waiting.NoSleep(bob.forceMove(closet));
        });
        float brute = r.In(() => Dm.num(bob.get_brute_loss()));
        r.Step(6 * Second);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > brute, Diag(r, feed));
    }

    [Fact]
    public void A_mushroomperson_has_a_cap_the_mushpunch_a_red_cap_hair_and_spores_when_it_dies()
    {
        var (r, feed, bob) = Room(9920);
        Become(r, bob, typeof(datum_species_mush));
        r.Step(Second);
        r.In(() =>
        {
            Assert.IsType<datum_species_mush>(bob.dna.species);
            Assert.IsType<obj_item_organ_mushroom_cap>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_POD_HAIR));
            Assert.IsType<obj_item_organ_tongue_mush>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_eyes_night_vision_mushroom>(bob.get_organ_slot(ORGAN_SLOT_EYES));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOFLASH));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_UNDERWEAR));
            Assert.Equal("Round", Dm.str(bob.dna.features[FEATURE_MUSH_CAP]));
            Assert.Equal("#FF4B19", Dm.str(((datum_species)bob.dna.species).get_fixed_hair_color(bob)));
            // the cap overlay is red by default
            var cap = (obj_item_organ_mushroom_cap)bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_POD_HAIR);
            var overlay = (datum_bodypart_overlay_mutant_mushroom_cap)cap.bodypart_overlay;
            Assert.Equal("#FF4B19", Dm.str(overlay.override_color((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_HEAD))));
            // the mushpunch is taught, locked to him
            Assert.Contains(Dm.each(bob.martial_arts), a => a is datum_martial_art_mushpunch m && Dm.truthy(m.locked_to_use));
        });
        // dead, spores grow and brown mushrooms sprout after 45 seconds
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);
        r.Step(46 * Second);
        Assert.True(r.In(() => Dm.each(get_turf(bob)).Any(x => x is obj_structure_glowshroom_brownshroom)), "the brown mushrooms sprouted");
        // the species loss forgets the art
        Become(r, bob, typeof(datum_species_human));
        r.In(() => Assert.DoesNotContain(Dm.each(bob.martial_arts), a => a is datum_martial_art_mushpunch));
    }

    [Fact]
    public void A_snailperson_wears_a_shell_he_cannot_drop_and_loses_it_with_the_species()
    {
        var (r, feed, bob) = Room(9930);
        Become(r, bob, typeof(datum_species_snail));
        r.In(() =>
        {
            Assert.IsType<datum_species_snail>(bob.dna.species);
            Assert.IsType<obj_item_organ_liver_snail>(bob.get_organ_slot(ORGAN_SLOT_LIVER));
            Assert.IsType<obj_item_organ_tongue_snail>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_eyes_snail>(bob.get_organ_slot(ORGAN_SLOT_EYES));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_SLIP_ALL));
            Assert.Equal(2f, Dm.num(((datum_species)bob.dna.species).siemens_coeff));
            Assert.Equal(0.5f, Dm.num(((datum_species)bob.dna.species).coldmod));
            var shell = bob.get_item_by_slot(ITEM_SLOT_BACK);
            Assert.IsType<obj_item_storage_backpack_snail>(shell);
            Assert.True(HAS_TRAIT((datum)shell, TRAIT_NODROP));
            // he is slow on his feet
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_snail))));
        });
        Become(r, bob, typeof(datum_species_human));
        r.In(() =>
        {
            Assert.IsNotType<obj_item_storage_backpack_snail>(bob.get_item_by_slot(ITEM_SLOT_BACK));
            Assert.False(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_snail))));
        });
    }

    [Fact]
    public void A_crawling_snailperson_is_fast_and_leaves_a_slippery_trail()
    {
        var (r, feed, bob) = Room(9931);
        Become(r, bob, typeof(datum_species_snail));
        r.In(() => Waiting.NoSleep(bob.set_resting(TRUE)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_snail_crawl))), "crawling speed");
            Waiting.NoSleep(bob.forceMove(locate(6, 4, 1)));
        });
        r.Step(2);
        r.In(() =>
        {
            var tile = (turf)locate(6, 4, 1);
            Assert.NotNull(tile.GetComponent(typeof(datum_component_wet_floor)));
            Waiting.NoSleep(bob.set_resting(FALSE));
        });
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_snail_crawl)))));
    }

    [Fact]
    public void Salt_burns_a_snailperson_and_his_tongue_drawls()
    {
        var (r, feed, bob) = Room(9932);
        Become(r, bob, typeof(datum_species_snail));
        r.In(() =>
        {
            Waiting.NoSleep(((datum_reagents)bob.reagents).add_reagent(typeof(datum_reagent_consumable_salt), 10));
        });
        float burn = r.In(() => Dm.num(bob.get_fire_loss()));
        r.Step(10 * Second);
        Assert.True(r.In(() => Dm.num(bob.get_fire_loss())) > burn, "the salt scours him");
        r.In(() =>
        {
            var tongue = (obj_item_organ_tongue_snail)bob.get_organ_slot(ORGAN_SLOT_TONGUE);
            var args = Dm.list("Hi!");
            tongue.modify_speech(bob, args);
            Assert.Equal("HHHiii!", Dm.str(args[SPEECH_MESSAGE]));
        });
    }

    [Fact]
    public void Every_new_species_is_a_live_selectable_type_with_its_text()
    {
        var (r, _, _) = Room(9940);
        r.In(() =>
        {
            foreach (Type t in new[] { typeof(datum_species_ethereal), typeof(datum_species_ethereal_lustrous), typeof(datum_species_pod), typeof(datum_species_mush), typeof(datum_species_snail) })
            {
                var species = (datum_species)Dm.@new(t);
                Assert.False(InertSeam.IsInertType(t), t.Name + " is inert");
                Assert.False(string.IsNullOrEmpty(Dm.str(species.get_physical_attributes())), t.Name);
                Assert.True(Dm.truthy(species.get_species_lore()), t.Name);
            }
            foreach (Type t in new[] { typeof(datum_species_ethereal), typeof(datum_species_pod), typeof(datum_species_snail) })
                Assert.True(Dm.truthy(((datum_species)Dm.@new(t)).create_pref_unique_perks()));
        });
    }
}
