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
/// Gaps package SP2c (D349): the jelly family (jelly, slime, luminescent, stargazer) and the cerulean. The jellyperson's blood is its body
/// (regeneration, cannibalizing its limbs, regrowing them); the slimeperson splits and swaps bodies, also on death; the luminescent glows and
/// holds a slime extract; the stargazer links minds; the cerulean has a fish tail instead of legs, its own organs, features and breath.
/// </summary>
public class SpeciesSP2cTests
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

    private static List<object> L(object list)
    {
        var items = new List<object>();
        foreach (object x in Dm.each(list))
            items.Add(x);
        return items;
    }

    private static void Become(AudioRoom r, mob_living_carbon_human who, Type species)
    {
        r.In(() => who.set_species(species));
        r.Step(2);
    }

    [Fact]
    public void A_slimeperson_has_jelly_limbs_organs_traits_language_and_its_actions()
    {
        var (r, feed, bob) = Room(9901);
        Become(r, bob, typeof(datum_species_jelly_slime));
        r.In(() =>
        {
            Assert.IsType<datum_species_jelly_slime>(bob.dna.species);
            Assert.IsType<obj_item_bodypart_chest_jelly_slime>(bob.get_bodypart(BODY_ZONE_CHEST));
            var head = Assert.IsType<obj_item_bodypart_head_jelly_slime>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.Equal(SPECIES_SLIMEPERSON, head.limb_id);
            Assert.True(Dm.truthy(Dm.bitand(head.biological_state, BIO_JELLY)));
            Assert.IsType<obj_item_organ_tongue_jelly>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_lungs_slime>(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.True(HAS_TRAIT(bob, TRAIT_TOXINLOVER));
            Assert.True(HAS_TRAIT(bob, TRAIT_MUTANT_COLORS));
            var species = (datum_species_jelly_slime)bob.dna.species;
            Assert.NotNull(species.regenerate_limbs);
            Assert.NotNull(species.slime_split);
            Assert.NotNull(species.swap_body);
            Assert.Contains(bob, L(species.bodies));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_slime), SPOKEN_LANGUAGE)));
            // the whole body is jelly, so the grille squeeze is active
            var effect = Assert.IsType<datum_status_effect_grouped_bodypart_effect_slime_passgrille>(bob.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_slime_passgrille)));
            Assert.True(Dm.truthy(effect.is_active));
            Assert.IsType<obj_item_bodypart_chest_jelly_slime>(bob.get_bodypart(BODY_ZONE_CHEST));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_jellyperson_with_little_jelly_eats_its_limbs_and_regrows_them_from_its_blood()
    {
        var (r, feed, bob) = Room(9902);
        Become(r, bob, typeof(datum_species_jelly_slime));
        // below the bad threshold the body cannibalizes a limb (legs first), which gives jelly back
        r.In(() =>
        {
            bob.set_blood_volume(100);
            Assert.Equal(2f, bob.num_legs);
            ((datum_species_jelly)bob.dna.species).slime_blood(bob, 2);
            Assert.Equal(1f, bob.num_legs);
            Assert.True(Dm.num(bob.get_blood_volume()) > 100 + 20 - 1);
            Assert.True(feed.Any("drawn back into your body"));
        });
        // with enough jelly the regenerate limbs action brings it back for 40 jelly
        r.In(() =>
        {
            bob.set_blood_volume(BLOOD_VOLUME_NORMAL);
            var action = ((datum_species_jelly)bob.dna.species).regenerate_limbs;
            Assert.True(Dm.truthy(Waiting.NoSleep(action.IsAvailable())));
            Waiting.NoSleep(action.Activate());
            Assert.Equal(2f, bob.num_legs);
            Assert.Equal(BLOOD_VOLUME_NORMAL - 40, Dm.num(bob.get_blood_volume()), 0.5);
            Assert.False(Dm.truthy(Waiting.NoSleep(action.IsAvailable()))); // nothing is missing
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_slimeperson_splits_its_body_swaps_into_the_copy_and_moves_there_when_it_dies()
    {
        var (r, feed, bob) = Room(9903);
        Become(r, bob, typeof(datum_species_jelly_slime));
        mob_living_carbon_human spare = null;
        r.In(() =>
        {
            bob.set_blood_volume(BLOOD_VOLUME_SLIME_SPLIT + 10);
            var species = (datum_species_jelly_slime)bob.dna.species;
            Assert.True(Dm.truthy(Waiting.NoSleep(species.slime_split.IsAvailable())));
            Waiting.NoSleep(species.slime_split.make_dupe());
            Assert.Equal(2, length(species.bodies));
            spare = (mob_living_carbon_human)L(species.bodies).Single(b => b != bob);
            Assert.IsType<datum_species_jelly_slime>(spare.dna.species);
            Assert.Same(species.bodies, ((datum_species_jelly_slime)spare.dna.species).bodies);
            Assert.Equal((BLOOD_VOLUME_SLIME_SPLIT + 10) * 0.45, Dm.num(bob.get_blood_volume()), 1);
            // the mind went to the new body
            Assert.True(Dm.truthy(spare.mind?.active));
        });
        r.Step(2);
        r.In(() =>
        {
            var species = (datum_species_jelly_slime)bob.dna.species;
            // the window's data: both bodies, the spare occupied by "owner" (us in the spare), the original available
            var data = (DmList)Waiting.NoSleep(((datum_species_jelly_slime)spare.dna.species).swap_body.ui_data(spare));
            var bodies = (DmList)data["bodies"];
            Assert.Equal(2, length(bodies));
            var rows = L(bodies).Cast<DmList>().ToDictionary(b => Dm.str(b["occupied"]));
            Assert.Equal(new[] { "available", "owner" }, rows.Keys.OrderBy(k => k));
                        Assert.True(Dm.truthy(rows["available"]["swappable"]));
            // swapping back
            Waiting.NoSleep(((datum_species_jelly_slime)spare.dna.species).swap_body.swap_to_dupe(spare.mind, bob));
            Assert.True(Dm.truthy(bob.mind?.active));
            Assert.Same(bob, bob.mind.current);
        });
        // a death moves the mind to a free body
        r.In(() =>
        {
            Waiting.NoSleep(bob.death());
            Assert.Same(spare, spare.mind?.current);
            Assert.True(Dm.truthy(spare.mind?.active));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_luminescent_glows_and_integrates_and_ejects_a_slime_extract()
    {
        var (r, feed, bob) = Room(9904);
        Become(r, bob, typeof(datum_species_jelly_luminescent));
        r.In(() =>
        {
            var species = (datum_species_jelly_luminescent)bob.dna.species;
            Assert.NotNull(species.glow);
            Assert.Equal(3, length(species.luminescent_actions));
            var extract = Dm.@new<obj_item_slime_extract_grey>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(extract));
            var integrate = (datum_action_innate_integrate_extract)species.luminescent_actions[1];
            Waiting.NoSleep(integrate.Activate());
            Assert.Same(extract, species.current_extract);
            Assert.Same(bob, extract.loc);
            // the minor activation: the base extract does nothing and gives back no cooldown (activate() returns FALSE)
            var minor = (datum_action_innate_use_extract)species.luminescent_actions[2];
            Assert.True(Dm.truthy(Waiting.NoSleep(minor.IsAvailable())));
            Waiting.NoSleep(minor.Activate());
            Assert.True(feed.Any("Nothing happened"));
            Assert.True(species.extract_cooldown <= world.time);
            // eject: back into the hand
            Waiting.NoSleep(integrate.Activate());
            Assert.Null(species.current_extract);
            Assert.Same(extract, bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_stargazer_links_a_mind_that_can_then_speak_on_the_link_until_it_dies()
    {
        var (r, feed, bob) = Room(9905);
        Become(r, bob, typeof(datum_species_jelly_stargazer));
        mob_living_carbon_human alice = r.Dummy(5, 4);
        r.In(() =>
        {
            var species = (datum_species_jelly_stargazer)bob.dna.species;
            Assert.NotNull(species.project_action);
            var linker = Assert.IsType<datum_component_mind_linker_active_linking>(bob.GetComponent(typeof(datum_component_mind_linker_active_linking)));
            Assert.IsType<datum_action_innate_link_minds>(linker.linker_action);
            Assert.True(Dm.truthy(linker.link_mob(alice)));
            Assert.IsType<datum_action_innate_linked_speech>(linker.linked_mobs[alice]);
            Assert.True(feed.Any("You are now connected to"));
            Assert.False(Dm.truthy(linker.link_mob(alice)));
            Waiting.NoSleep(alice.death());
            Assert.Equal(0, length(linker.linked_mobs));
        });
        // losing the species breaks the link
        Become(r, bob, typeof(datum_species_human));
        r.In(() => Assert.Null(bob.GetComponent(typeof(datum_component_mind_linker_active_linking))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cerulean_has_a_fish_tail_instead_of_legs_its_organs_its_features_and_breathes_water_vapor()
    {
        var (r, feed, bob) = Room(9906);
        Become(r, bob, typeof(datum_species_human_cerulean));
        r.In(() =>
        {
            var species = (datum_species_human_cerulean)bob.dna.species;
            Assert.Equal(SPECIES_CERULEAN, species.id);
            Assert.IsType<obj_item_organ_tail_fish_cerulean>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
            Assert.IsType<obj_item_organ_tongue_fish>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_lungs_fish>(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_organ_stomach_fish>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            Assert.IsType<obj_item_organ_liver_fish>(bob.get_organ_slot(ORGAN_SLOT_LIVER));
            Assert.Null(bob.get_bodypart(BODY_ZONE_L_LEG));
            Assert.Null(bob.get_bodypart(BODY_ZONE_R_LEG));
            Assert.True(HAS_TRAIT(bob, TRAIT_FREE_FLOAT_MOVEMENT));
            Assert.True(HAS_TRAIT(bob, TRAIT_SWIMMER));
            Assert.True(HAS_TRAIT(bob, TRAIT_BLOCK_ATTACHING_LEGS));
            Assert.Equal(GAS_WATER_VAPOR, Dm.str(species.get_breath_type()));
            Assert.Contains(typeof(obj_item_organ_wings_dragon), L(((obj_item_bodypart_chest)bob.get_bodypart(BODY_ZONE_CHEST)).wing_types));
            // its Character Setup features
            var features = (DmList)species.get_features();
            Assert.Contains("feature_cerulean_frills", L(features).Select(Dm.str));
            Assert.Contains("feature_fish_tail_color", L(features).Select(Dm.str));
            var randomized = (DmList)species.randomize_features();
            Assert.NotEqual("#fdfbf3", Dm.str(randomized[FEATURE_TAIL_FISH_COLOR]));
            // the tail's own pool: only cerulean tails, the skeleton one locked away
            var pool = (DmList)((obj_item_organ_tail_fish_cerulean)bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL)).get_valid_restyles();
            Assert.Equal(new[] { "Cerulean" }, L(pool).Select(Dm.str).ToArray());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cerulean_torn_tail_hurts_and_the_skeletal_tail_does_not_bleed()
    {
        var (r, feed, bob) = Room(9907);
        Become(r, bob, typeof(datum_species_human_cerulean));
        r.In(() =>
        {
            var tail = (obj_item_organ_tail_fish_cerulean)bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL);
            Assert.True(Dm.truthy(tail.splatter_check(bob)));
            Assert.False(Dm.truthy(new obj_item_organ_tail_fish_cerulean_skeletal().splatter_check(bob)));
            float before = Dm.num(bob.get_brute_loss());
            Waiting.NoSleep(tail.mob_remove(bob));
            Assert.True(Dm.num(bob.get_brute_loss()) > before + 30);
            Assert.True(feed.Any("grotesquely torn"));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_cerulean_preferences_set_the_tail_colour_and_the_frills()
    {
        var (r, feed, bob) = Room(9908);
        Become(r, bob, typeof(datum_species_human_cerulean));
        r.In(() =>
        {
            var color = (datum_preference)GLOB.preference_entries[typeof(datum_preference_color_fish_tail_color)];
            var frills = (datum_preference)GLOB.preference_entries[typeof(datum_preference_toggle_cerulean_frills)];
            color.apply_to_human(bob, "#112233", bob.client?.prefs);
            Assert.Equal("#112233", Dm.str(bob.dna.features[FEATURE_TAIL_FISH_COLOR]));
            Assert.NotEqual("#fdfbf3", Dm.str(color.create_default_value()));
            frills.apply_to_human(bob, TRUE, bob.client?.prefs);
            Assert.Equal("Aquatic", Dm.str(bob.dna.features[FEATURE_FRILLS]));
            Assert.NotNull(bob.get_organ_by_type(typeof(obj_item_organ_frills)));
        });
        r.AssertNoRuntimes();
    }
}
