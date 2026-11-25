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
/// Gaps package SP2a (D349): the moth and the plasmaman. A player who picks the moth in Character Setup joins with his wings, antennae
/// and markings from the preferences, the moth's organs and limbs, Mothic and the cloth-eater's trait; his wings drift, soften falls,
/// burn off in a fire and grow back with a full heal. The plasmaman spawns in his job's envirosuit and helmet with a plasma tank, is
/// sealed against oxygen by them (an envirosuit and a helmet, with gloves), and without them his limbs set him alight in oxygen; the
/// envirosuit puts him out a limited number of times, the helmet's torch and welding visor toggle.
/// </summary>
public class SpeciesSP2aTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_keyed_list_roundstart_races)]).config_entry_value =
                Dm.list(Dm.kv(SPECIES_HUMAN, TRUE), Dm.kv(SPECIES_MOTH, TRUE), Dm.kv(SPECIES_PLASMAMAN, TRUE));
            GLOB.roundstart_races = Dm.list();
            GLOB.uncommon_roundstart_languages = Dm.list();
            ((datum_preference_choiced)GLOB.preference_entries[typeof(datum_preference_choiced_species)]).cached_values = null;
        });
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    private static void Write(mob_living_carbon_human who, Type preference, object value)
        => who.client.prefs.write_preference(GLOB.preference_entries[preference] as datum_preference, value);

    private static void Transfer(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() => who.client.prefs.safe_transfer_prefs_to(who, TRUE));
        r.Step(2);
    }

    // The room's air: a breathable (or chosen) mix on every open turf
    private static void Air(AudioRoom r, string mix)
        => r.In(() =>
        {
            for (int x = 1; x <= 11; x++)
                for (int y = 1; y <= 9; y++)
                    if (locate(x, y, 1) is turf_open open)
                        open.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        });

    private const string Breathable = "o2=21;n2=79;TEMP=293.15";

    private static mob_living_carbon_human Plasmaman(AudioRoom r, mob_living_carbon_human bob)
    {
        r.In(() => Write(bob, typeof(datum_preference_choiced_species), SPECIES_PLASMAMAN));
        Transfer(r, bob);
        return bob;
    }

    // The mob naked: no envirosuit, no helmet, no gloves
    private static void Strip(AudioRoom r, mob_living_carbon_human who)
        => r.In(() =>
        {
            foreach (obj_item item in Dm.each_cast<obj_item>(who.get_equipped_items(TRUE)).ToList())
                qdel(item);
        });

    [Fact]
    public void A_moth_from_the_preferences_has_his_species_organs_limbs_features_and_language()
    {
        var (r, bob) = Room(9901);
        r.In(() =>
        {
            Write(bob, typeof(datum_preference_choiced_species), SPECIES_MOTH);
            Write(bob, typeof(datum_preference_choiced_species_feature_moth_wings), "Monarch");
            Write(bob, typeof(datum_preference_choiced_species_feature_moth_antennae), "Royal");
            Write(bob, typeof(datum_preference_choiced_species_feature_moth_markings), "Reddish");
        });
        Transfer(r, bob);
        r.In(() =>
        {
            Assert.IsType<datum_species_moth>(bob.dna.species);
            Assert.Equal("Monarch", Dm.str(bob.dna.features[FEATURE_MOTH_WINGS]));
            Assert.Equal("Royal", Dm.str(bob.dna.features[FEATURE_MOTH_ANTENNAE]));
            Assert.Equal("Reddish", Dm.str(bob.dna.features[FEATURE_MOTH_MARKINGS]));
            Assert.IsType<obj_item_organ_wings_moth>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS));
            Assert.IsType<obj_item_organ_antennae>(bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_ANTENNAE));
            Assert.IsType<obj_item_organ_tongue_moth>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_eyes_moth>(bob.get_organ_slot(ORGAN_SLOT_EYES));
            Assert.IsType<obj_item_organ_stomach_moth>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            var chest = Assert.IsType<obj_item_bodypart_chest_moth>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.Equal(SPECIES_MOTH, chest.limb_id);
            Assert.IsType<obj_item_bodypart_head_moth>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.IsType<obj_item_bodypart_arm_left_moth>(bob.get_bodypart(BODY_ZONE_L_ARM));
            Assert.IsType<obj_item_bodypart_leg_right_moth>(bob.get_bodypart(BODY_ZONE_R_LEG));
            // the chest's own butt, the cloth-eater, antennae from the head, Mothic
            Assert.Equal("icons/mob/butts.dmi", Dm.str(((icon)chest.get_butt_sprite()).dm_resource()));
            Assert.True(HAS_TRAIT(bob, TRAIT_CLOTH_EATER));
            Assert.True(HAS_TRAIT(bob, TRAIT_ANTENNAE));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_moffic), SPOKEN_LANGUAGE)));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_common), SPOKEN_LANGUAGE)));
            // the lantern heirloom is the species'
            Assert.Contains(typeof(obj_item_flashlight_lantern_heirloom_moth), Dm.each_cast<Type>(((datum_species)bob.dna.species).family_heirlooms));
            // the page's texts and perks
            var species = (datum_species)bob.dna.species;
            Assert.Equal(3, Dm.length(species.get_species_lore()));
            Assert.Equal(4, Dm.length(species.create_pref_unique_perks()));
            // a random moth gets markings of his own
            Assert.True(Dm.truthy(((DmList)species.randomize_features()).Contains(FEATURE_MOTH_MARKINGS)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_moths_wings_drift_soften_falls_burn_off_and_heal_back()
    {
        var (r, bob) = Room(9902);
        Air(r, Breathable);
        r.In(() => Write(bob, typeof(datum_preference_choiced_species), SPECIES_MOTH));
        Transfer(r, bob);
        var wings = r.In(() => bob.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS) as obj_item_organ_wings_moth);
        r.In(() =>
        {
            // tg's flightless wings: a jetpack that drifts, allowed while the wings are whole
            Assert.NotNull(wings.GetComponent(typeof(datum_component_jetpack)));
            Assert.True(Dm.truthy(wings.can_soften_fall()));
            Assert.False(Dm.truthy(wings.allow_flight())); // not in gravity
            Waiting.NoSleep(bob.AddElement(typeof(datum_element_forced_gravity), 0));
            Assert.True(Dm.truthy(wings.allow_flight())); // in a pressurized, zero-g room
        });
        // no lift in a vacuum
        Air(r, "TEMP=2.7");
        r.In(() => Assert.False(Dm.truthy(wings.allow_flight())));
        Air(r, Breathable);
        // a body hot enough and on fire burns them off
        r.In(() =>
        {
            bob.bodytemperature = 900;
            bob.fire_stacks = 3;
            wings.try_burn_wings(bob);
            Assert.True(Dm.truthy(wings.burnt));
            Assert.False(Dm.truthy(wings.allow_flight()));
            Assert.False(Dm.truthy(wings.can_soften_fall()));
            Assert.True(Dm.truthy(((datum_bodypart_overlay_mutant_wings_moth)wings.bodypart_overlay).burnt));
            Assert.Equal("Your wings are all burnt up!", Dm.str(wings.feel_for_damage(TRUE)));
        });
        // a full heal grows them back
        r.In(() => Waiting.NoSleep(bob.fully_heal(HEAL_ALL)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(wings.burnt));
            Assert.True(Dm.truthy(wings.allow_flight()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_moth_eats_clothes_for_nourishment()
    {
        var (r, bob) = Room(9903);
        r.In(() => Write(bob, typeof(datum_preference_choiced_species), SPECIES_MOTH));
        Transfer(r, bob);
        r.In(() =>
        {
            bob.set_combat_mode(FALSE);
            var rag = Dm.@new<obj_item_clothing_under_color_grey>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(rag));
            // the cloth-eater's trait lets a bite of clothing through the attack chain into the snack the clothing makes
            var snack = rag.create_moth_snack() as obj_item_food_clothing;
            Assert.NotNull(snack);
            Assert.Equal(rag.name, snack.name);
            Assert.Same(rag, snack.clothing.resolve());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_moths_features_are_choices_with_pictures_and_apply_from_the_preferences()
    {
        var (r, bob) = Room(9904);
        r.In(() =>
        {
            foreach ((Type type, string feature, string pick) in new[]
            {
                (typeof(datum_preference_choiced_species_feature_moth_antennae), FEATURE_MOTH_ANTENNAE, "Firewatch"),
                (typeof(datum_preference_choiced_species_feature_moth_markings), FEATURE_MOTH_MARKINGS, "Deathshead"),
                (typeof(datum_preference_choiced_species_feature_moth_wings), FEATURE_MOTH_WINGS, "Luna"),
            })
            {
                var pref = (datum_preference_choiced)GLOB.preference_entries[type];
                Assert.Contains(pick, Dm.each(pref.get_choices()).Select(Dm.str));
                DmList icons = (DmList)((DmList)pref.compile_constant_data())["icons"];
                Assert.Equal(Dm.length(pref.get_choices()), Dm.length(icons));
                Assert.NotNull(icons[pick]);
                Write(bob, type, pick);
                Assert.Equal(pick, Dm.str(bob.client.prefs.read_preference(type)));
                bob.client.prefs.apply_prefs_to(bob);
                Assert.Equal(pick, Dm.str(bob.dna.features[feature]));
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_plasmaman_from_the_preferences_has_his_species_organs_limbs_and_traits()
    {
        var (r, bob) = Room(9911);
        Plasmaman(r, bob);
        r.In(() =>
        {
            Assert.IsType<datum_species_plasmaman>(bob.dna.species);
            Assert.IsType<obj_item_organ_lungs_plasmaman>(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_organ_tongue_bone_plasmaman>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_liver_bone_plasmaman>(bob.get_organ_slot(ORGAN_SLOT_LIVER));
            Assert.IsType<obj_item_organ_stomach_bone_plasmaman>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_APPENDIX));
            var chest = Assert.IsType<obj_item_bodypart_chest_plasmaman>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.Equal(SPECIES_PLASMAMAN, chest.limb_id);
            Assert.Equal("icons/mob/butts.dmi", Dm.str(((icon)chest.get_butt_sprite()).dm_resource()));
            Assert.IsType<obj_item_bodypart_head_plasmaman>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.IsType<obj_item_bodypart_leg_left_plasmaman>(bob.get_bodypart(BODY_ZONE_L_LEG));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBLOOD));
            Assert.True(HAS_TRAIT(bob, TRAIT_RADIMMUNE));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_PLASMA_TRANSFORM));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_calcic), SPOKEN_LANGUAGE)));
            var species = (datum_species)bob.dna.species;
            Assert.Equal(4, Dm.length(species.get_species_lore()));
            Assert.Equal(6, Dm.length(species.create_pref_unique_perks()));
            // each of his limbs carries the plasma-based effect
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_plasma_based)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_plasmaman_spawns_in_his_jobs_envirosuit_and_helmet_with_a_plasma_tank_and_does_not_burn()
    {
        var (r, bob) = Room(9912);
        Air(r, Breathable);
        Plasmaman(r, bob);
        r.In(() => Waiting.NoSleep(bob.dress_up_as_job(SSjob.get_job_type(typeof(datum_job_security_officer)) as datum_job)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.IsType<obj_item_clothing_under_plasmaman_security>(bob.w_uniform);
            Assert.IsType<obj_item_clothing_head_helmet_space_plasmaman_security>(bob.head);
            Assert.IsType<obj_item_clothing_gloves_color_plasmaman_black>(bob.gloves);
            Assert.True(Dm.each<obj_item_tank_internals_plasmaman_belt_full>(bob.held_items).Any());
            Assert.True(Dm.truthy(bob.is_atmos_sealed(PLASMAMAN_PREVENT_IGNITION, TRUE)));
        });
        // three seconds of oxygen: the suit keeps him whole
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(bob.on_fire));
            Assert.False(HAS_TRAIT(bob, TRAIT_IGNORE_FIRE_PROTECTION));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_plasmaman_without_his_suit_bursts_into_flames_in_oxygen_and_not_in_a_vacuum_or_plasma()
    {
        var (r, bob) = Room(9913);
        Plasmaman(r, bob);
        Strip(r, bob);
        // plasma and nitrogen: nothing to burn with
        Air(r, "plasma=21;n2=79;TEMP=293.15");
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(bob.on_fire));
            Assert.False(Dm.truthy(bob.is_atmos_sealed(PLASMAMAN_PREVENT_IGNITION, TRUE)));
        });
        // oxygen: his limbs ignite him
        Air(r, Breathable);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bob.on_fire));
            Assert.True(HAS_TRAIT(bob, TRAIT_IGNORE_FIRE_PROTECTION));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_envirosuit_puts_a_burning_plasmaman_out_a_limited_number_of_times()
    {
        var (r, bob) = Room(9914);
        Air(r, Breathable);
        Plasmaman(r, bob);
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_plasmaman))));
        r.Step(Second);
        var suit = r.In(() => bob.w_uniform as obj_item_clothing_under_plasmaman);
        r.In(() =>
        {
            Assert.NotNull(suit);
            Assert.Equal(5f, suit.extinguishes_left);
            // set alight while sealed: the suit extinguishes him
            Waiting.NoSleep(bob.adjust_fire_stacks(3));
            bob.ignite_mob();
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(bob.on_fire));
            Assert.Equal(4f, suit.extinguishes_left);
        });
        // the cartridge fills it up again
        r.In(() =>
        {
            var refill = Dm.@new<obj_item_extinguisher_refill>(bob.loc);
            Waiting.NoSleep(suit.item_interaction(bob, refill, Dm.list()));
            Assert.Equal(5f, suit.extinguishes_left);
            Assert.True(QDELETED(refill));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_plasmamans_helmet_torch_and_welding_visor_toggle_and_a_crayon_draws_a_smile()
    {
        var (r, bob) = Room(9915);
        Plasmaman(r, bob);
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_plasmaman))));
        r.Step(Second);
        var helmet = r.In(() => bob.head as obj_item_clothing_head_helmet_space_plasmaman);
        Assert.NotNull(helmet);
        r.In(() =>
        {
            Assert.True(Dm.truthy(helmet.up)); // the visor up
            Waiting.NoSleep(helmet.attack_self(bob));
            Assert.True(Dm.truthy(helmet.helmet_on));
            Assert.True(Dm.truthy(helmet.light_on));
            Assert.Equal("plasmaman-helm-light", helmet.icon_state);
            // the welding visor down puts the torch out
            Waiting.NoSleep(helmet.adjust_visor(bob));
            Assert.False(Dm.truthy(helmet.up));
            Assert.False(Dm.truthy(helmet.helmet_on));
            Assert.False(Dm.truthy(helmet.light_on));
            Assert.Equal(FLASH_PROTECTION_WELDER, helmet.flash_protect);
            // the visor is drawn over the helmet
            Assert.Contains("envisor", Dm.each(Waiting.NoSleep(helmet.update_overlays())).Select(Dm.str));
        });
        r.AssertNoRuntimes();
    }
}
