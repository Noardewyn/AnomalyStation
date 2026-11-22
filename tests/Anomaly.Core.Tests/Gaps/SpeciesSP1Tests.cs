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
/// Gaps package SP1 (D333, D338): the first roundstart species, the lizard and the felinid. A player who picks them in Character Setup
/// joins with the species' organs, bodyparts, features and languages; the lizard's tongue hisses and he speaks Draconic, the felinid
/// Nekomimetic; the lizard is cold-blooded and wags his tail; the felinid lands on his feet, hates water, licks wounds and bites when
/// feral; purrbation gives a human cat ears and takes them away; the species' parts are the bodypart overlays tg draws (the snout, the
/// horns, the frills, the spines, the tail and its spines, the body markings, the cat ears and tail), in the mutant colour, on the
/// layers and with the sprite states the client composes.
/// </summary>
public class SpeciesSP1Tests
{
    // The server's config (ServerHost.TgConfigText): ROUNDSTART_RACES human, felinid and lizard (tg's game_options.txt); the
    // selectable species and the species preference's choices are made again from it
    internal static void RoundstartRaces(AudioRoom r) => r.In(() =>
    {
        ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_keyed_list_roundstart_races)]).config_entry_value =
            Dm.list(Dm.kv(SPECIES_HUMAN, TRUE), Dm.kv(SPECIES_FELINE, TRUE), Dm.kv(SPECIES_LIZARD, TRUE));
        GLOB.roundstart_races = Dm.list();
        GLOB.uncommon_roundstart_languages = Dm.list();
        ((datum_preference_choiced)GLOB.preference_entries[typeof(datum_preference_choiced_species)]).cached_values = null;
    });

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        RoundstartRaces(r);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, feed, bob);
    }

    private static string Diag(AudioRoom r, Feed feed) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static void Write(mob_living_carbon_human who, Type preference, object value)
        => who.client.prefs.write_preference(GLOB.preference_entries[preference] as datum_preference, value);

    // The player's preferences onto his body, as the job's spawn does it (tg's safe_transfer_prefs_to)
    private static void Transfer(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() => who.client.prefs.safe_transfer_prefs_to(who, TRUE));
        r.Step(2);
    }

    private static mob_living_carbon_human Lizard(AudioRoom r, mob_living_carbon_human bob, string legs = "Normal Legs")
    {
        r.In(() =>
        {
            Write(bob, typeof(datum_preference_choiced_species), SPECIES_LIZARD);
            Write(bob, typeof(datum_preference_color_mutant_color), "#22aa44");
            Write(bob, typeof(datum_preference_choiced_species_feature_lizard_snout), "Sharp");
            Write(bob, typeof(datum_preference_choiced_species_feature_lizard_horns), "Curled");
            Write(bob, typeof(datum_preference_choiced_species_feature_lizard_frills), "Short");
            Write(bob, typeof(datum_preference_choiced_species_feature_lizard_spines), "Long");
            Write(bob, typeof(datum_preference_choiced_species_feature_lizard_tail), "Dark Tiger");
            Write(bob, typeof(datum_preference_choiced_species_feature_lizard_body_markings), "Light Belly");
            Write(bob, typeof(datum_preference_choiced_lizard_legs), legs);
        });
        Transfer(r, bob);
        return bob;
    }

    private static mob_living_carbon_human Felinid(AudioRoom r, mob_living_carbon_human bob)
    {
        r.In(() =>
        {
            Write(bob, typeof(datum_preference_choiced_species), SPECIES_FELINE);
            Write(bob, typeof(datum_preference_choiced_species_feature_felinid_ears), "Big");
            Write(bob, typeof(datum_preference_choiced_species_feature_tail_felinid), "Cat");
        });
        Transfer(r, bob);
        return bob;
    }

    // Every overlay a bodypart overlay of the mob's limbs draws, by icon state (tg's get_overlay through update_body_parts)
    private static List<(string Icon, string State, float Layer, object Color)> PartOverlays(mob_living_carbon_human who, Func<datum_bodypart_overlay, bool> which = null)
    {
        var found = new List<(string, string, float, object)>();
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(who.bodyparts))
        {
            foreach (datum_bodypart_overlay overlay in Dm.each_cast<datum_bodypart_overlay>(part.bodypart_overlays))
            {
                if (which != null && !which(overlay))
                    continue;
                if (!Dm.truthy(overlay.can_draw_on_bodypart(part, who)))
                    continue;
                foreach ((object image, object _) in Dm.pairs(Waiting.NoSleep(overlay.get_all_overlays(part)) as DmList))
                {
                    if (image is image img)
                        found.Add((Dm.str(img.icon is icon i ? i.dm_resource() : img.icon), Dm.str(img.icon_state), Dm.num(img.layer), img.color));
                }
            }
        }
        return found;
    }

    [Fact]
    public void A_lizard_from_the_preferences_has_his_species_organs_limbs_features_and_languages()
    {
        var (r, feed, bob) = Room(9801);
        Lizard(r, bob);
        r.In(() =>
        {
            Assert.IsType<datum_species_lizard>(bob.dna.species);
            // the features from the preferences
            Assert.Equal("#22aa44", Dm.str(bob.dna.features[FEATURE_MUTANT_COLOR]));
            Assert.Equal("Sharp", Dm.str(bob.dna.features[FEATURE_SNOUT]));
            Assert.Equal("Curled", Dm.str(bob.dna.features[FEATURE_HORNS]));
            Assert.Equal("Short", Dm.str(bob.dna.features[FEATURE_FRILLS]));
            Assert.Equal("Long", Dm.str(bob.dna.features[FEATURE_SPINES]));
            Assert.Equal("Dark Tiger", Dm.str(bob.dna.features[FEATURE_TAIL_LIZARD]));
            Assert.Equal("Light Belly", Dm.str(bob.dna.features[FEATURE_LIZARD_MARKINGS]));
            // the species' organs
            Assert.IsType<obj_item_organ_tongue_lizard>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_eyes_lizard>(bob.get_organ_slot(ORGAN_SLOT_EYES));
            Assert.IsType<obj_item_organ_brain_lizard>(bob.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.IsType<obj_item_organ_liver_lizard>(bob.get_organ_slot(ORGAN_SLOT_LIVER));
            Assert.IsType<obj_item_organ_tail_lizard>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
            Assert.IsType<obj_item_organ_snout>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_SNOUT));
            Assert.IsType<obj_item_organ_horns>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_HORNS));
            Assert.IsType<obj_item_organ_frills>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_FRILLS));
            Assert.IsType<obj_item_organ_spines>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_SPINES));
            // the lizard's limbs, in his mutant colour
            var chest = Assert.IsType<obj_item_bodypart_chest_lizard>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.Equal(SPECIES_LIZARD, chest.limb_id);
            Assert.Equal("#22aa44", Dm.str(chest.draw_color));
            Assert.IsType<obj_item_bodypart_head_lizard>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.IsType<obj_item_bodypart_leg_left_lizard>(bob.get_bodypart(BODY_ZONE_L_LEG));
            // Draconic, which he speaks and understands with Common
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_draconic), SPOKEN_LANGUAGE)));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_draconic), UNDERSTOOD_LANGUAGE)));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_common), SPOKEN_LANGUAGE)));
            // cold blood and the mutant colours
            Assert.True(HAS_TRAIT(bob, TRAIT_COLD_BLOODED));
            Assert.True(HAS_TRAIT(bob, TRAIT_MUTANT_COLORS));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_digitigrade_lizard_squashes_his_legs_into_clothes_without_a_digitigrade_variant()
    {
        var (r, feed, bob) = Room(9802);
        Lizard(r, bob, legs: DIGITIGRADE_LEGS);
        r.In(() =>
        {
            var left = Assert.IsType<obj_item_bodypart_leg_left_digitigrade>(bob.get_bodypart(BODY_ZONE_L_LEG));
            Assert.IsType<obj_item_bodypart_leg_right_digitigrade>(bob.get_bodypart(BODY_ZONE_R_LEG));
            Assert.True(Dm.truthy(Dm.bitand(left.bodytype, BODYTYPE_DIGITIGRADE)));
            Assert.Equal(BODYPART_ID_DIGITIGRADE, left.limb_id); // bare legs are free
            Assert.True(Dm.truthy(Dm.bitand(bob.bodyshape, BODYSHAPE_DIGITIGRADE)));
        });
        // a jumpsuit with a digitigrade variant leaves the legs free and is drawn with tg's digitigrade legs (its worn icon's legs
        // masked off, the GAGS digitigrade legs drawn in)
        var suit = r.In(() => Dm.@new<obj_item_clothing_under_color_grey>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(suit, bob.w_uniform);
            Assert.Equal(BODYPART_ID_DIGITIGRADE, ((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_L_LEG)).limb_id);
            var worn = bob.overlays_standing[UNIFORM_LAYER] as mutable_appearance;
            string recipe = (worn?.icon as icon)?.dm_resource();
            Assert.NotNull(recipe);
            Assert.Contains("legs_mask", recipe);
            Assert.Contains("gags:", recipe);
        });
        // shoes without a digitigrade variant do not fit his feet (tg's species can_equip)
        var shoes = r.In(() =>
        {
            var s = Dm.@new<obj_item_clothing_shoes_sneakers_black>(bob.loc);
            s.supports_variations_flags = NONE;
            return s;
        });
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(bob.equip_to_slot_if_possible(shoes, ITEM_SLOT_FEET))));
        Assert.True(feed.Any("The footwear around here isn't compatible with your feet!"), Diag(r, feed));
        // a jumpsuit without one squashes them into the plain lizard's legs
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(suit)));
        var plain = r.In(() =>
        {
            var s = Dm.@new<obj_item_clothing_under_color_grey>(bob.loc);
            s.supports_variations_flags = NONE;
            return s;
        });
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_if_possible(plain, ITEM_SLOT_ICLOTHING)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(plain, bob.w_uniform);
            var left = (obj_item_bodypart)bob.get_bodypart(BODY_ZONE_L_LEG);
            Assert.Equal(SPECIES_LIZARD, left.limb_id);
            Assert.False(Dm.truthy(Dm.bitand(bob.bodyshape, BODYSHAPE_DIGITIGRADE)));
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(plain)));
        r.Step(2);
        r.In(() => Assert.Equal(BODYPART_ID_DIGITIGRADE, ((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_L_LEG)).limb_id));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_felinid_from_the_preferences_has_cat_ears_a_tail_fangs_and_speaks_nekomimetic()
    {
        var (r, feed, bob) = Room(9803);
        Felinid(r, bob);
        r.In(() =>
        {
            Assert.IsType<datum_species_human_felinid>(bob.dna.species);
            Assert.True(isfelinid(bob));
            Assert.Equal("Big", Dm.str(bob.dna.features[FEATURE_EARS]));
            Assert.Equal("Cat", Dm.str(bob.dna.features[FEATURE_TAIL_CAT]));
            Assert.IsType<obj_item_organ_ears_cat>(bob.get_organ_slot(ORGAN_SLOT_EARS));
            Assert.IsType<obj_item_organ_tail_cat>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
            Assert.IsType<obj_item_organ_tongue_cat>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_brain_felinid>(bob.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.IsType<obj_item_organ_eyes_felinid>(bob.get_organ_slot(ORGAN_SLOT_EYES));
            Assert.NotNull(bob.get_organ_by_type(typeof(obj_item_organ_fangs_cat)));
            // the human's limbs (felinids use the human sprites)
            Assert.Equal(SPECIES_HUMAN, ((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_CHEST)).limb_id);
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_nekomimetic), SPOKEN_LANGUAGE)));
            // the felinid's traits from its species, brain and tongue
            Assert.True(HAS_TRAIT(bob, TRAIT_HATED_BY_DOGS));
            Assert.True(HAS_TRAIT(bob, TRAIT_CATLIKE_INSTINCT));
            Assert.True(HAS_TRAIT(bob, TRAIT_WATER_HATER));
            Assert.True(HAS_TRAIT(bob, TRAIT_WOUND_LICKER));
            Assert.True(HAS_TRAIT(bob, TRAIT_CARPOTOXIN_IMMUNE));
            Assert.True(HAS_TRAIT(bob, TRAIT_CAT_EMOTES_ALLOWED));
        });
        // a spray of water startles the water hater (other_reagents.dm:206); a human shrugs it off
        mob_living_carbon_human alice = r.Dummy(5, 4);
        r.In(() =>
        {
            foreach (mob_living_carbon_human sprayed in new[] { bob, alice })
            {
                var spray = Dm.@new<datum_reagents>(50); // a spray bottle's puff of water (tg's expose with VAPOR)
                Waiting.NoSleep(spray.add_reagent(typeof(datum_reagent_water), 10));
                Waiting.NoSleep(spray.expose(sprayed, VAPOR));
            }
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_incapacitating_incapacitated)));
            Assert.Null(alice.has_status_effect(typeof(datum_status_effect_incapacitating_incapacitated)));
        });
        // and he can purr (the cat tongue's cat emotes)
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.emote("purr", intentional: TRUE)));
        r.Step(2);
        Assert.True(feed.Any("purrs"), Diag(r, feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lizard_hisses_and_speaks_draconic_the_felinid_nekomimetic()
    {
        var (r, feed, bob) = Room(9804);
        Lizard(r, bob);
        mob_living_carbon_human alice = r.Dummy(5, 4); // a human who understands neither
        r.In(() => Waiting.NoSleep(bob.say("yes sir")));
        r.Step(Second);
        Assert.True(feed.Any("Yesss sssir"), Diag(r, feed)); // the lizard tongue's hiss (tongue/lizard modify_speech)

        // in Draconic: the human does not understand it, the lizard does
        r.In(() => bob.get_language_holder().selected_language = typeof(datum_language_draconic));
        string said = "the station is safe";
        string heard = null;
        r.In(() =>
        {
            datum_language draconic = GLOB.language_datum_instances[typeof(datum_language_draconic)] as datum_language;
            heard = Dm.str(draconic.scramble_sentence(said, Dm.list()));
            Assert.NotEqual(said, heard);
            Assert.False(Dm.truthy(alice.has_language(typeof(datum_language_draconic), UNDERSTOOD_LANGUAGE)));
            Assert.True(Dm.truthy(bob.can_speak_language(typeof(datum_language_draconic))));
            datum_language neko = GLOB.language_datum_instances[typeof(datum_language_nekomimetic)] as datum_language;
            Assert.NotEqual(said, Dm.str(neko.scramble_sentence(said, Dm.list())));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lizards_tail_wags_with_the_emote_and_stops_when_he_falls_unconscious()
    {
        var (r, feed, bob) = Room(9805);
        Lizard(r, bob);
        var tail = r.In(() => (obj_item_organ_tail)bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
        r.In(() => Waiting.NoSleep(bob.emote("wag", intentional: TRUE)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(tail.wag_flags, WAG_WAGGING))));
        Assert.True(feed.Chat.Any(c => c.Html.Contains(" wags h", StringComparison.Ordinal) && c.Html.Contains(" tail.", StringComparison.Ordinal)), Diag(r, feed));
        // the wagging tail is tg's animated wagging_ state, its spines wag with it
        var states = r.In(() => PartOverlays(bob, o => o is datum_bodypart_overlay_mutant_tail or datum_bodypart_overlay_mutant_tail_spines).Select(o => o.State).ToList());
        Assert.Contains("m_tail_lizard_wagging_dtiger_BEHIND", states);
        Assert.Contains(states, s => s.StartsWith("m_tailspines_", StringComparison.Ordinal) && s.Contains("wagging_long", StringComparison.Ordinal));
        r.Step(Second); // the emote's cooldown
        r.In(() => Waiting.NoSleep(bob.emote("wag", intentional: TRUE)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(tail.wag_flags, WAG_WAGGING))));
        Assert.True(feed.Any("stops wagging h"), Diag(r, feed));
        // the helpers, and a knocked out lizard stops wagging
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(bob.wag_tail())));
        r.In(() => Waiting.NoSleep(bob.Unconscious(10 * SECONDS)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(tail.wag_flags, WAG_WAGGING))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_cold_blooded_lizard_takes_the_temperature_of_his_surroundings()
    {
        var (r, feed, bob) = Room(9806);
        Lizard(r, bob);
        mob_living_carbon_human alice = r.Dummy(6, 4);
        r.In(() =>
        {
            bob.bodytemperature = BODYTEMP_NORMAL - 20;
            alice.bodytemperature = BODYTEMP_NORMAL - 20;
        });
        r.Step(10 * Second);
        // the human warms himself back towards his normal; the cold-blooded lizard does not make his own heat
        float lizard = r.In(() => bob.bodytemperature), human = r.In(() => alice.bodytemperature);
        Assert.True(human > lizard, $"human {human}, lizard {lizard}");
        r.In(() =>
        {
            var perks = ((datum_species)bob.dna.species).create_pref_temperature_perks() as DmList;
            Assert.Contains("Cold-blooded", Dm.each(perks).Select(p => Dm.str(((DmList)p)[SPECIES_PERK_NAME])));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Purrbation_makes_a_human_a_felinid_and_gives_a_lizard_cat_ears_and_takes_them_away()
    {
        var (r, feed, bob) = Room(9807);
        r.In(() => Waiting.NoSleep(purrbation_apply(bob)));
        r.Step(2);
        r.In(() =>
        {
            // a plain human becomes a felinid who was not born one
            var cat = Assert.IsType<datum_species_human_felinid>(bob.dna.species);
            Assert.Equal(FALSE, cat.original_felinid);
            Assert.IsType<obj_item_organ_ears_cat>(bob.get_organ_slot(ORGAN_SLOT_EARS));
            Assert.IsType<obj_item_organ_tail_cat>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
        });
        Assert.True(feed.Any("Something is nya~t right."), Diag(r, feed));
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(purrbation_toggle(bob))));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_human>(bob.dna.species);
            Assert.IsNotType<obj_item_organ_ears_cat>(bob.get_organ_slot(ORGAN_SLOT_EARS));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
        });
        Assert.True(feed.Any("You are no longer a cat."), Diag(r, feed));

        // a lizard keeps his species: he loses his spines and gets cat ears and a cat tail; removed, his lizard tail and spines come back
        Lizard(r, bob);
        r.In(() => Waiting.NoSleep(purrbation_apply(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_lizard>(bob.dna.species);
            Assert.IsType<obj_item_organ_ears_cat>(bob.get_organ_slot(ORGAN_SLOT_EARS));
            Assert.IsType<obj_item_organ_tail_cat>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_SPINES));
        });
        r.In(() => Waiting.NoSleep(purrbation_remove(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<obj_item_organ_tail_lizard>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL));
            Assert.IsType<obj_item_organ_spines>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_SPINES));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_feral_felinid_bites_when_he_punches_and_carpotoxin_does_not_harm_him()
    {
        var (r, feed, bob) = Room(9808);
        Felinid(r, bob);
        var fangs = r.In(() => (obj_item_organ_fangs_cat)bob.get_organ_by_type(typeof(obj_item_organ_fangs_cat)));
        var action = r.In(() => Dm.each(bob.actions).OfType<datum_action_item_action_organ_action_go_feral>().SingleOrDefault());
        Assert.NotNull(action);
        r.In(() => Waiting.NoSleep(action.Trigger()));
        r.Step(2);
        Assert.Equal(TRUE, r.In(() => fangs.feral_mode));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_FERAL_BITER)));
        // the felinid brain makes the feral biter's attacking limb his mouth
        var brain = r.In(() => (obj_item_organ_brain_felinid)bob.get_organ_slot(ORGAN_SLOT_BRAIN));
        mob_living_carbon_human alice = r.Dummy(5, 4);
        r.In(() => Assert.Same(bob.get_bodypart(BODY_ZONE_HEAD), brain.get_attacking_limb(alice)));
        r.In(() => Waiting.NoSleep(action.Trigger()));
        r.Step(2);
        Assert.Equal(FALSE, r.In(() => fangs.feral_mode));
        // tame, he still bites now and then from mania and hunger: tg's chance is 10 (fed; 1 when hungry) times his sanity level, 20% at
        // a neutral sanity (brain_item.dm:561, the moods since gaps MS1)
        int bites = r.In(() => Enumerable.Range(0, 200).Count(_ => ReferenceEquals(bob.get_bodypart(BODY_ZONE_HEAD), brain.get_attacking_limb(alice))));
        Assert.Equal((float)SANITY_LEVEL_NEUTRAL, r.In(() => bob.mob_mood.sanity_level));
        Assert.InRange(bites, 20, 60);

        // carpotoxin: harmless to his cat tongue
        r.In(() => Waiting.NoSleep(((datum_reagents)bob.reagents).add_reagent(typeof(datum_reagent_toxin_carpotoxin), 10)));
        r.Step(Second);
        var toxin = r.In(() => ((datum_reagents)bob.reagents).has_reagent(typeof(datum_reagent_toxin_carpotoxin)) as datum_reagent_toxin_carpotoxin);
        Assert.NotNull(toxin);
        Assert.Equal(0f, r.In(() => toxin.toxpwr));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lizards_parts_are_tgs_overlays_in_his_colour_on_their_layers()
    {
        var (r, feed, bob) = Room(9809);
        Lizard(r, bob);
        var parts = r.In(() => PartOverlays(bob));
        var states = parts.Select(p => p.State).ToList();
        // the snout, horns and frills on the head, adjacent (and the snout in front), from lizard_misc.dmi
        Assert.Contains("m_snout_sharp_ADJ", states);
        Assert.Contains("m_horns_curled_ADJ", states);
        Assert.Contains("m_frills_short_ADJ", states);
        Assert.All(parts.Where(p => p.State.StartsWith("m_snout", StringComparison.Ordinal)), p => Assert.Equal("icons/mob/human/species/lizard/lizard_misc.dmi", p.Icon));
        // the back spines and the tail (behind the body and adjacent) with its spines
        Assert.Contains("m_spines_long_ADJ", states);
        Assert.Contains("m_tail_lizard_dtiger_BEHIND", states);
        Assert.Contains("m_tail_lizard_dtiger_FRONT", states);
        Assert.Contains(states, s => s.StartsWith("m_tailspines_", StringComparison.Ordinal) && s.EndsWith("_long_BEHIND", StringComparison.Ordinal));
        // the light belly marking on the chest, coloured by the mutant colour
        Assert.Contains(states, s => s.Contains("lbelly_chest", StringComparison.Ordinal));
        // the snout drawn in the mutant colour (tg's color_source ORGAN_COLOR_INHERIT through the limb)
        Assert.All(parts.Where(p => p.State.StartsWith("m_snout", StringComparison.Ordinal)), p => Assert.Equal("#22aa44", Dm.str(p.Color)));
        // the mob's appearance carries the limbs' overlays the client composes (the lizard limbs' greyscale sprites)
        r.In(() =>
        {
            DmList overlays = bob.overlays as DmList;
            Assert.NotNull(overlays);
            Assert.True(Dm.length(overlays) > 0);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_felinids_ears_and_tail_are_drawn_from_the_cat_features_sheet()
    {
        var (r, feed, bob) = Room(9810);
        Felinid(r, bob);
        var parts = r.In(() => PartOverlays(bob));
        // the big ears behind the head (their front layer is a holder of the outer and the inner ear) and the cat tail
        Assert.Contains(parts, p => p.Icon == "icons/mob/human/cat_features.dmi" && p.State == "m_ears_big_BEHIND");
        Assert.Contains(parts, p => p.Icon == "icons/mob/human/cat_features.dmi" && p.State == "m_tail_cat_default_BEHIND");
        Assert.Contains(parts, p => p.Icon == "icons/mob/human/cat_features.dmi" && p.State == "m_tail_cat_default_FRONT");
        // in the felinid's hair colour (tg's ORGAN_COLOR_HAIR)
        string hair = r.In(() => Dm.str(bob.hair_color));
        Assert.All(parts.Where(p => p.State.StartsWith("m_tail_cat", StringComparison.Ordinal)), p => Assert.Equal(hair, Dm.str(p.Color)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_species_preference_lists_the_human_the_felinid_and_the_lizard_with_their_pages()
    {
        var (r, feed, bob) = Room(9811);
        r.In(() =>
        {
            DmList selectable = get_selectable_species();
            Assert.Equal(new[] { SPECIES_HUMAN, SPECIES_FELINE, SPECIES_LIZARD }.OrderBy(s => s), Dm.each(selectable).Select(Dm.str).OrderBy(s => s));
            // the uncommon roundstart languages: Draconic and Nekomimetic
            Assert.True(Dm.@in(typeof(datum_language_draconic), GLOB.uncommon_roundstart_languages));
            Assert.True(Dm.@in(typeof(datum_language_nekomimetic), GLOB.uncommon_roundstart_languages));
            var species = (datum_preference)GLOB.preference_entries[typeof(datum_preference_choiced_species)];
            DmList data = species.compile_constant_data() as DmList;
            DmList lizard = data[SPECIES_LIZARD] as DmList;
            Assert.EndsWith("Lizardperson", Dm.str(lizard["name"])); // tg's \improper, stripped by the json
            Assert.Equal("Lizardperson", Dm.str(lizard["icon"])); // sanitize_css_class_name
            Assert.True(Dm.length(lizard["lore"]) >= 3);
            Assert.Contains("feature_lizard_snout", Dm.each(lizard["enabled_features"] as DmList).Select(Dm.str));
            Assert.Contains("feature_mcolor", Dm.each(lizard["enabled_features"] as DmList).Select(Dm.str));
            DmList perks = lizard["perks"] as DmList;
            Assert.Contains("Cold-blooded", Dm.each(perks["neutral"] as DmList).Concat(Dm.each(perks["negative"] as DmList)).Concat(Dm.each(perks["positive"] as DmList)).Select(p => Dm.str(((DmList)p)["name"])));
            DmList felinid = data[SPECIES_FELINE] as DmList;
            Assert.EndsWith("Felinid", Dm.str(felinid["name"]));
            Assert.Contains("feature_cat_ears", Dm.each(felinid["enabled_features"] as DmList).Select(Dm.str));
            DmList human = data[SPECIES_HUMAN] as DmList;
            Assert.Equal(TRUE, Dm.num(human["use_skintones"]));
            // the digitigrade legs only for the lizard
            var legs = (datum_preference)GLOB.preference_entries[typeof(datum_preference_choiced_lizard_legs)];
            Write(bob, typeof(datum_preference_choiced_species), SPECIES_LIZARD);
            Assert.Equal(TRUE, Dm.num(legs.is_accessible(bob.client.prefs)));
            Write(bob, typeof(datum_preference_choiced_species), SPECIES_FELINE);
            Assert.Equal(FALSE, Dm.num(legs.is_accessible(bob.client.prefs)));
        });
        // the species page's pictures: each species' dummy in the assistant's clothes, flattened and cropped as tg's spritesheet
        DmList icons = r.In(() => Waiting.NoSleep(bob.client.prefs.species_preview_icons()));
        Assert.Equal(3, r.In(() => (int)icons.len));
        Assert.StartsWith("iconops:", r.In(() => Dm.str(((DmList)icons["Lizardperson"])[1])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_lizards_side_shot_pictures_face_east()
    {
        var (r, feed, bob) = Room(9812);
        r.In(() =>
        {
            var snout = (datum_preference_choiced_species_feature)GLOB.preference_entries[typeof(datum_preference_choiced_species_feature_lizard_snout)];
            var picture = snout.icon_for("Round") as icon;
            string recipe = picture.dm_resource();
            Assert.StartsWith("iconops:", recipe);
            Assert.Contains(",d,4", recipe); // the recipe's direction pick: EAST
            Assert.Contains("m_snout_round_ADJ", recipe);
            var markings = (datum_preference_choiced_species_feature)GLOB.preference_entries[typeof(datum_preference_choiced_species_feature_lizard_body_markings)];
            Assert.Contains("male_dtiger_chest", (markings.icon_for("Dark Tiger Body") as icon).dm_resource());
        });
        r.AssertNoRuntimes();
    }
}
