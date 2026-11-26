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
/// Gaps package SP3a (D349): the skeleton, the zombie and the golem. The skeleton has no blood or breath and bone organs and limbs; the
/// zombified status effect makes an infectious zombie (claws in every hand, an infection organ, a zombie tongue, a husk body) and
/// unmakes him; a claw's hit plants the infection; the golem is stone whose nutrition is its health, eats minerals for buffs, and is
/// rebuilt, not defibrillated.
/// </summary>
public class SpeciesSP3aTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    [Fact]
    public void A_skeleton_has_no_blood_or_breath_and_bone_organs_and_limbs()
    {
        var (r, bob) = Room(9901);
        r.In(() => bob.set_species(typeof(datum_species_skeleton)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_skeleton>(bob.dna.species);
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBLOOD));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_EASYDISMEMBER));
            Assert.IsType<obj_item_organ_tongue_bone>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_organ_stomach_bone>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            Assert.IsType<obj_item_organ_liver_bone>(bob.get_organ_slot(ORGAN_SLOT_LIVER));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_bodypart_chest_skeleton>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.Equal(SPECIES_SKELETON, ((obj_item_bodypart)bob.get_bodypart(BODY_ZONE_HEAD)).limb_id);
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_common), SPOKEN_LANGUAGE)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_degloved_bone_chest_husks_its_owner_and_giving_it_back_cures_him()
    {
        var (r, bob) = Room(9902);
        r.In(() =>
        {
            var bone = Dm.@new<obj_item_bodypart_chest_skeleton_nonfunctional>(null);
            Assert.True(Dm.truthy(bone.bodypart_disabled));
            bone.on_adding(bob);
            Assert.True(HAS_TRAIT(bob, TRAIT_HUSK));
            Waiting.NoSleep(bone.on_removal(bob));
            Assert.False(HAS_TRAIT(bob, TRAIT_HUSK));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_zombified_status_makes_an_infectious_zombie_and_removing_it_unmakes_him()
    {
        var (r, bob) = Room(9903);
        r.In(() => bob.set_species(typeof(datum_species_zombie)));
        r.Step(2);
        r.In(() =>
        {
            // the plain zombie species wears the uninfected status: bloodless, but with no claws
            Assert.True(Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_zombie_uninfected))));
            Assert.IsType<obj_item_organ_zombie_infection>(bob.get_organ_slot(ORGAN_SLOT_ZOMBIE)); // tg gives the species the organ too
            foreach (obj_item hand in Dm.each_cast<obj_item>(bob.held_items))
                Assert.False(hand is obj_item_mutant_hand);
            bob.remove_status_effect(typeof(datum_status_effect_zombie_uninfected));
            bob.set_species(typeof(datum_species_human));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.apply_status_effect(typeof(datum_status_effect_zombie)))));
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_NODEATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_FAKEDEATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_HUSK));
            Assert.True(Dm.truthy(Dm.bitand(bob.mob_biotypes, MOB_UNDEAD)));
            Assert.False(Dm.truthy(Dm.bitand(bob.mob_biotypes, MOB_ORGANIC)));
            Assert.IsType<obj_item_organ_zombie_infection>(bob.get_organ_slot(ORGAN_SLOT_ZOMBIE));
            Assert.IsType<obj_item_organ_tongue_zombie>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.NotNull(bob.GetComponent(typeof(datum_component_regenerator)));
            Assert.NotNull(bob.GetComponent(typeof(datum_component_mutant_hands)));
            // a claw in every hand
            foreach (obj_item hand in Dm.each_cast<obj_item>(bob.held_items))
                Assert.IsType<obj_item_mutant_hand_zombie>(hand);
        });
        // removing the status takes the claws, the infection and the husk away
        r.In(() => bob.remove_status_effect(typeof(datum_status_effect_zombie)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_NODEATH));
            Assert.False(HAS_TRAIT(bob, TRAIT_HUSK));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_ZOMBIE));
            Assert.Null(bob.GetComponent(typeof(datum_component_mutant_hands)));
            Assert.False(bob.get_organ_slot(ORGAN_SLOT_TONGUE) is obj_item_organ_tongue_zombie);
            Assert.True(Dm.truthy(Dm.bitand(bob.mob_biotypes, MOB_ORGANIC)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_zombie_claws_hit_plants_the_infection_in_a_human()
    {
        var (r, bob) = Room(9904);
        mob_living_carbon_human carol = r.Join(6, 4, "Carol");
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(carol.get_organ_slot(ORGAN_SLOT_ZOMBIE));
            Waiting.NoSleep(try_to_zombie_infect(carol, bob, BODY_ZONE_CHEST, 100));
            Assert.IsType<obj_item_organ_zombie_infection>(carol.get_organ_slot(ORGAN_SLOT_ZOMBIE));
        });
        // the infection rots its host while he lives
        float before = r.In(() => Dm.num(carol.get_tox_loss()));
        r.Step(10 * Second);
        Assert.True(r.In(() => Dm.num(carol.get_tox_loss())) > before);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_golem_is_stone_with_nutrition_for_health_and_a_buff_from_the_minerals_it_eats()
    {
        var (r, bob) = Room(9905);
        r.In(() => bob.set_species(typeof(datum_species_golem)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_golem>(bob.dna.species);
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_NODISMEMBER));
            Assert.True(HAS_TRAIT(bob, TRAIT_ROCK_EATER));
            Assert.IsType<obj_item_organ_stomach_golem>(bob.get_organ_slot(ORGAN_SLOT_STOMACH));
            Assert.IsType<obj_item_bodypart_chest_golem>(bob.get_bodypart(BODY_ZONE_CHEST));
            var arm = Assert.IsType<obj_item_bodypart_arm_left_golem>(bob.get_bodypart(BODY_ZONE_L_ARM));
            Assert.Equal(0.5f, Dm.num(arm.brute_modifier));
            Assert.NotNull(bob.GetComponent(typeof(datum_component_shovel_hands)));
            Assert.True(Dm.num(bob.nutrition) >= NUTRITION_LEVEL_WELL_FED - 1);
            // no defibrillator brings a golem back
            Assert.Equal(DEFIB_FAIL_GOLEM, Dm.num(bob._SendSignal(COMSIG_CARBON_DEFIB_HEART_CHECK, Dm.list(bob))));
            // a stack of iron is golem food, with the buff's own nutrition
            var iron = Dm.@new<obj_item_stack_sheet_iron>(bob.loc);
            Assert.NotNull(iron.GetComponent(typeof(datum_component_golem_food)));
            // the status effects of the minerals
            Waiting.NoSleep(bob.apply_status_effect(typeof(datum_status_effect_golem_uranium), 1f));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOHUNGER));
            Assert.True(Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_golem))));
            bob.remove_status_effect(typeof(datum_status_effect_golem_uranium));
            Assert.False(HAS_TRAIT(bob, TRAIT_NOHUNGER));
        });
        // with no nutrition left the golem crumbles
        r.In(() => Waiting.NoSleep(bob.set_nutrition(1f)));
        r.Step(2);
        Assert.Equal(DEAD, r.In(() => bob.stat));
        r.AssertNoRuntimes();
    }
}
